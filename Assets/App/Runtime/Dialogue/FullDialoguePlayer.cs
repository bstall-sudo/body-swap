using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using App.Runtime.Dialogue.Persistence;
using System.Diagnostics;

namespace App.Runtime.Dialogue
{
    public class FullDialoguePlayer : MonoBehaviour
    {
        [Header("Session")]
        public string SessionId; // in Inspector eintragen (oder später UI)
        public bool AutoPlayOnStart = false;

        [Header("Stores")]
        public string AppFolderName = "YourApp";

        [Header("Role A rig")]
        public Transform ActorA;
        public Transform A_Head, A_Left, A_Right;
        public AudioSource AudioA;

        [Header("Role B rig")]
        public Transform ActorB;
        public Transform B_Head, B_Left, B_Right;
        public AudioSource AudioB;

        [Header("Hold Settings")]
        public float HoldAfterTakeSec = 0.15f;

        private SessionStore _store;
        private SessionModel _session;

        private TakePlayer _playerA;
        private TakePlayer _playerB;



        void Start()
        {

            var director = FindObjectOfType<DialogueDirector>();
            if (director != null)
            {
                director.enabled = false;
            }


            _playerA = new TakePlayer(ActorA, A_Head, A_Left, A_Right, AudioA);
            _playerB = new TakePlayer(ActorB, B_Head, B_Left, B_Right, AudioB);


            _store = new SessionStore(AppFolderName);



            if (AutoPlayOnStart && !string.IsNullOrEmpty(SessionId))
                StartCoroutine(PlayFullDialogue());
        }

        [ContextMenu("Play Full Dialogue")]
        public void PlayNow()
        {
            if (string.IsNullOrEmpty(SessionId))
            {
                UnityEngine.Debug.LogWarning("SessionId is empty.");
                return;
            }
            StartCoroutine(PlayFullDialogue());
        }

        public IEnumerator PlayFullDialogue()
        {
            // 1) Session laden
            _session = _store.LoadSessionModel(SessionId);
            string folder = _store.GetSessionFolder(_session.SessionId);

            // 2) Takes pro Rolle sammeln (in Reihenfolge der Session!)
            var takesA = new List<TakeData>();
            var takesB = new List<TakeData>();

            foreach (var meta in _session.Takes)
            {
                var take = new TakeData();

                // Frames laden
                string framesPath = Path.Combine(folder, meta.FramesFile);
                take.Frames = JsonlFrames.ReadAll(framesPath);

                // Audio optional: wir laden es hier NICHT (kann später rein)
                string audioPath = Path.Combine(folder, meta.AudioFile);
                if (File.Exists(audioPath))
                {
                    take.AudioClip = WavUtility.LoadWav(audioPath);
                }
                else
                {
                    take.AudioClip = null;
                }

                // Dauer
                if (take.Frames.Count > 0)
                    take.DurationSec = take.Frames[take.Frames.Count - 1].T;

                if (meta.Speaker == "A") takesA.Add(take);
                else if (meta.Speaker == "B") takesB.Add(take);
            }

            // 3) Für beide Rollen parallel abspielen:
            // A spielt seine Take-Kette hintereinander, B spielt seine Take-Kette hintereinander
            // (gleichzeitig, so dass beide "immer Bewegung" haben)
            var coA = StartCoroutine(PlayRoleSequence(_playerA, takesA));
            var coB = StartCoroutine(PlayRoleSequence(_playerB, takesB));

            // 4) Warten bis beide fertig sind
            yield return coA;
            yield return coB;

            UnityEngine.Debug.Log("Full dialogue playback finished.");
        }

        private IEnumerator PlayRoleSequence(TakePlayer player, List<TakeData> takes)
        {
            if (player == null || takes == null || takes.Count == 0)
                yield break;

            for (int i = 0; i < takes.Count; i++)
            {
                player.Begin(takes[i]);

                float timeout = takes[i].DurationSec + 2f; // 2 seconds slack
                float start = Time.time;

                while (player.IsPlaying)
                {
                    player.Tick();
                    if (Time.time - start > timeout)
                    {
                        UnityEngine.Debug.LogWarning("Timeout waiting for take to finish. Forcing stop.");
                        player.Stop();
                        break;
                    }
                    yield return null;
                }

                if (HoldAfterTakeSec > 0f)
                {
                    float t0 = Time.time;
                    while (Time.time - t0 < HoldAfterTakeSec)
                        yield return null;
                }
            }
        }


    }
}
