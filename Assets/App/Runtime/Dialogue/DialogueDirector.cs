using UnityEngine;
using UnityEngine.InputSystem;
using App.Runtime.Input;
using System.Collections;
using System.Diagnostics;
using System;
using System.IO;

using App.Runtime.Dialogue.Persistence;

namespace App.Runtime.Dialogue
{
    public class DialogueDirector : MonoBehaviour
    {
        [Header("Stage Root")]
        public Transform TakeRoot; // this transform

        [Header("Actors (move with BodyPose)")]
        public Transform ActorA;
        public Transform ActorB;

        [Header("Role A cubes under ActorA")]
        public Transform A_Head, A_Left, A_Right;

        [Header("Role B cubes under ActorB")]
        public Transform B_Head, B_Left, B_Right;

        [Header("Audio Sources (not under TakeRoot)")]
        public AudioSource AudioA;
        public AudioSource AudioB;

        [Header("XR References")]
        public Transform XrHead;
        public Transform XrLeft;
        public Transform XrRight;

        [Header("Settings")]
        public bool UseXR = true;
        public float SmoothAlignSeconds = 0.6f;

        private InputRouter _router;
        private XRInputProvider _xrProvider;

        private TakeRecorder _recA, _recB;
        private TakePlayer _playA, _playB;

        private TakeData _lastTakeA, _lastTakeB;

        private bool _aIsLive = true;
        private bool _recording = false;

        private readonly SmoothAlign _align = new();

        private SessionStore _store;
        private SessionModel _session;
        private int _takeCounter = 0;

        void Start()
        {
            if (TakeRoot == null) TakeRoot = transform;

            _router = new InputRouter();

            if (UseXR)
            {
                _xrProvider = new XRInputProvider(XrHead, XrLeft, XrRight);
                _router.SetProvider(_xrProvider);

                // Anchor einmalig setzen (wie vorher)
                _xrProvider.SetAnchorFromTakeRoot(TakeRoot);
            }
            else
            {
                _xrProvider = null;
                _router.SetProvider(new KeyboardInputProvider());
            }

            // Recorder/Player anlegen
            _recA = new TakeRecorder(TakeRoot, ActorA, _router.Provider);
            _recB = new TakeRecorder(TakeRoot, ActorB, _router.Provider);

            _playA = new TakePlayer(ActorA, A_Head, A_Left, A_Right, AudioA);
            _playB = new TakePlayer(ActorB, B_Head, B_Left, B_Right, AudioB);

            _store = new SessionStore("YourApp"); // Ordnername frei
            string sessionFolder = _store.CreateNewSessionFolder(out string sessionId);

            _session = new SessionModel
            {
                SessionId = sessionId,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            _store.SaveSessionModel(_session);

            UnityEngine.Debug.Log("Session folder: " + sessionFolder);

        }

        void Update()
        {
            // B: Bühne neu setzen + Anchor setzen (wie bei dir)
            if (UseXR && Keyboard.current.bKey.wasPressedThisFrame)
                ResetTakeRootToHead();

            // SPACE: Aufnahme togglen
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                ToggleRecord();

            // TAB: Switch + Smooth Align
            if (Keyboard.current.tabKey.wasPressedThisFrame)
                SwitchRolesSmooth();

            // Align tick
            if (_align.Active) _align.Tick(Time.deltaTime);

            // Live Update (mit Align)
            UpdateLiveRigWithAlign();

            // Recording tick
            if (_recording)
            {
                if (_aIsLive) _recA.Tick();
                else _recB.Tick();
            }

            // Playback tick (die andere Rolle spielt ab)
            _playA.Tick();
            _playB.Tick();
        }

        private void ToggleRecord()
        {
            if (!_recording)
            {
                _recording = true;

                // Beim Start: Anchor auf Bühne setzen (damit tracking->stage stabil ist)
                _xrProvider?.SetAnchorFromTakeRoot(TakeRoot);

                if (_aIsLive) _recA.Begin();
                else _recB.Begin();

                // Playback stoppen
                if (_aIsLive) _playB.Stop(); else _playA.Stop();
            }
            else
            {
                _recording = false;

                if (_aIsLive)
                {
                    UnityEngine.Debug.Log($"Recording stopped.");
                    var info = _recA.EndAndGetTrimInfo();
                    if (info.HasValue)
                        StartCoroutine(FinalizeTrimNextFrame(true, info.Value));
                    else
                        _lastTakeA = _recA.Current; // fallback
                }
                else
                {
                    var info = _recB.EndAndGetTrimInfo();
                    if (info.HasValue)
                        StartCoroutine(FinalizeTrimNextFrame(false, info.Value));
                    else
                        _lastTakeB = _recB.Current; // fallback
                }

            }
        }

        private void SwitchRolesSmooth()
        {
            // Recording ggf. beenden
            if (_recording) ToggleRecord();

            // Neue live Rolle = andere
            _aIsLive = !_aIsLive;

            // Playback des Nicht-Live starten (letzte Aufnahme)
            if (_aIsLive)
            {
                if (_lastTakeB != null) _playB.Begin(_lastTakeB);
            }
            else
            {
                if (_lastTakeA != null) _playA.Begin(_lastTakeA);
            }

            // Smooth Align: Wir wollen die aktuelle Tracking-Pose sanft in die Story-Startpose der neuen Live-Rolle bringen.
            // Zielpose = aktueller ActorRoot der Rolle (oder letzter Endpunkt). Für MVP: nimm aktuelle ActorRoot Pose.
            Vector3 targetPos = (_aIsLive ? ActorA.localPosition : ActorB.localPosition);
            float targetYaw = (_aIsLive ? ActorA.localEulerAngles.y : ActorB.localEulerAngles.y);

            // aktuelle Tracking Pose (stage-local) aus Provider
            if (_router.Provider.TryGetHeadPose(out var headStage, out var headRotStage))
            {
                Vector3 fromPos = headStage; fromPos.y = 0f;
                float fromYaw = headRotStage.eulerAngles.y;

                _align.StartAlign(fromPos, fromYaw, targetPos, targetYaw, SmoothAlignSeconds);
            }
        }

        private void UpdateLiveRigWithAlign()
        {
            var p = _router.Provider;
            if (p == null) return;

            if (!p.TryGetHeadPose(out var headStage, out var headRotStage)) return;
            p.TryGetLeftHandPose(out var leftStage, out var leftRotStage);
            p.TryGetRightHandPose(out var rightStage, out var rightRotStage);

            // Wenn Align aktiv: ersetze BodyPose (pos+yaw) durch interpolierte, aber behalte relative Head/Hands
            Vector3 bodyPos = headStage; bodyPos.y = 0f;
            float yaw = headRotStage.eulerAngles.y;

            if (_align.Active)
            {
                _align.GetCurrent(out bodyPos, out yaw);
            }

            Transform actor = _aIsLive ? ActorA : ActorB;
            Transform h = _aIsLive ? A_Head : B_Head;
            Transform l = _aIsLive ? A_Left : B_Left;
            Transform r = _aIsLive ? A_Right : B_Right;

            actor.localPosition = bodyPos;
            actor.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Head/Hands actor-local setzen (damit sie “kleben”, aber der Body sanft aligned)
            var invActorRot = Quaternion.Inverse(actor.localRotation);

            Vector3 ToLocalPos(Vector3 pStage)
            {
                Vector3 delta = pStage - bodyPos;
                return invActorRot * delta;
            }

            Quaternion ToLocalRot(Quaternion rStage) => invActorRot * rStage;

            if (h) { h.localPosition = ToLocalPos(headStage); h.localRotation = ToLocalRot(headRotStage); }
            if (l) { l.localPosition = ToLocalPos(leftStage); l.localRotation = ToLocalRot(leftRotStage); }
            if (r) { r.localPosition = ToLocalPos(rightStage); r.localRotation = ToLocalRot(rightRotStage); }
        }

        private IEnumerator FinalizeTrimNextFrame(bool wasALive, (AudioClip clip, int startSample, int sampleCount, int channels, int sampleRate) info)
        {
            // 1–2 Frames warten, damit Unity den Mic-Puffer final schreibt
            yield return null;
            yield return null;

            var trimmed = TakeRecorder.TrimMicClip(info.clip, info.startSample, info.sampleCount, info.channels, info.sampleRate);

            if (wasALive)
            {
                _lastTakeA = _recA.Current;
                _lastTakeA.AudioClip = trimmed;
                PersistTake("A", _lastTakeA);
            }
            else
            {
                _lastTakeB = _recB.Current;
                _lastTakeB.AudioClip = trimmed;
                PersistTake("B", _lastTakeB);
            }
        }


        private void ResetTakeRootToHead()
        {
            if (XrHead == null) return;

            Vector3 newPos = XrHead.position; newPos.y = 0f;
            float yaw = XrHead.eulerAngles.y;
            Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

            TakeRoot.SetPositionAndRotation(newPos, newRot);

            // Anchor neu setzen
            _xrProvider?.SetAnchorFromTakeRoot(TakeRoot);
        }

        private void PersistTake(string speaker, TakeData take)
        {
            _takeCounter++;
            string takeId = $"take_{_takeCounter:0000}";

            string folder = _store.GetSessionFolder(_session.SessionId);
            string framesName = _store.FramesFileName(takeId, speaker);
            string audioName = _store.AudioFileName(takeId, speaker);

            string framesPath = Path.Combine(folder, framesName);
            string audioPath = Path.Combine(folder, audioName);

            // 1) Frames
            JsonlFrames.WriteAll(framesPath, take.Frames);

            // 2) Audio
            if (take.AudioClip != null)
                WavUtility.SaveWav(audioPath, take.AudioClip);

            // 3) Meta
            _session.Takes.Add(new TakeMeta
            {
                TakeId = takeId,
                Speaker = speaker,
                DurationSec = take.DurationSec,
                FramesFile = framesName,
                AudioFile = audioName
            });

            _store.SaveSessionModel(_session);

            UnityEngine.Debug.Log($"Saved take {takeId} speaker={speaker} frames={framesName} audio={audioName}");
        }

    }
}
