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
        [Header("XR Origin (XR Rig)")]
        public Transform XrOrigin;


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

        //Verbindung zu DialogueInputBridge
        public void ToggleRecordPublic() => ToggleRecord();
        public void SwitchRolesPublic() => SwitchRolesSmooth();
        public void ResetStagePublic() => ResetTakeRootToHead();

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

        //für die Rebase Berechnung (neuer Take startet da, wo letzter Take geendet hat)
        private bool _hasLastEndA = false;
        private Vector3 _lastEndPosA;
        private float _lastEndYawA;
        //für die Rebase Berechnung (neuer Take startet da, wo letzter Take geendet hat)
        private bool _hasLastEndB = false;
        private Vector3 _lastEndPosB;
        private float _lastEndYawB;

        //damit man beim RoleSwitch an die Stelle der anderen Figur teleportiert wird
        private bool _playerAlignActive;
        private Vector3 _playerAlignFromPos, _playerAlignToPos;
        private float _playerAlignFromYaw, _playerAlignToYaw;
        private float _playerAlignT, _playerAlignDur;
        //Helper, damit man beim RoleSwitch an die Stelle der anderen Figur teleportiert wird
        private static float YawOf(Quaternion q) => q.eulerAngles.y;
        private static Quaternion YawRot(float yawDeg) => Quaternion.Euler(0f, yawDeg, 0f);

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
            //if (_align.Active) _align.Tick(Time.deltaTime);

            TickPlayerAlign();
            // Live Update (mit Align)
            UpdateLiveRig();

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

        // damit man sozusagen an den Ort des neuen Actors teleportiert wird.
        private void AlignPlayerToActor(Transform actor)
        {
            if (XrOrigin == null || XrHead == null || actor == null) return;

            // current head on floor (world)
            Vector3 headFloor = XrHead.position;
            headFloor.y = actor.position.y; // usually floor

            // desired body position (world) = actor.position
            Vector3 delta = actor.position - headFloor;

            // move XR Origin so the head ends up at actor
            XrOrigin.position += delta;

            // optional: yaw align to actor forward
            float targetYaw = actor.eulerAngles.y;
            float currentYaw = XrHead.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);
            XrOrigin.rotation = Quaternion.Euler(0f, deltaYaw, 0f) * XrOrigin.rotation;
        }

        //Das wird in Update gerufen
        private void TickPlayerAlign()
        {
            if (!_playerAlignActive) return;

            _playerAlignT += Time.deltaTime;
            float a = Mathf.Clamp01(_playerAlignT / _playerAlignDur);
            a = a * a * (3f - 2f * a); // smoothstep

            Vector3 pos = Vector3.Lerp(_playerAlignFromPos, _playerAlignToPos, a);
            float yaw = Mathf.LerpAngle(_playerAlignFromYaw, _playerAlignToYaw, a);

            XrOrigin.position = pos;

            // keep only yaw rotation on origin (safe)
            XrOrigin.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (a >= 1f) _playerAlignActive = false;
        }

        //das wird bei RoleSwitch gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        private void StartPlayerAlignToActor(Transform actor, float duration)
        {
            if (XrOrigin == null || XrHead == null || actor == null) return;

            // From
            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = YawOf(XrOrigin.rotation);

            // Head floor (world)
            Vector3 headFloor = XrHead.position;
            headFloor.y = actor.position.y;

            // We want: headFloor -> actor.position
            Vector3 delta = actor.position - headFloor;
            _playerAlignToPos = XrOrigin.position + delta;

            // Yaw: align head yaw to actor yaw (only yaw)
            float headYaw = YawOf(XrHead.rotation);
            float actorYaw = YawOf(actor.rotation);
            float deltaYaw = Mathf.DeltaAngle(headYaw, actorYaw);

            _playerAlignToYaw = _playerAlignFromYaw + deltaYaw;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;
        }

        //das wird bei RoleSwitch gerufen, damit man das Gefühlt hat im Avatar zu sein. das XR-Rig wird vor der Aufnahme an die Position gefahren, wo die Figur am Ende des letzten Takes stand.
        private void StartPlayerAlignToStagePose(Vector3 stagePos, float stageYawDeg, float duration)
        {
            if (XrOrigin == null || XrHead == null || TakeRoot == null) return;

            // target world position of actor on floor
            Vector3 targetWorldPos = TakeRoot.TransformPoint(stagePos);

            // target world yaw
            float takeRootYaw = TakeRoot.eulerAngles.y;
            float targetWorldYaw = takeRootYaw + stageYawDeg;

            // From origin pose
            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = XrOrigin.eulerAngles.y;

            // current head on floor (world)
            Vector3 headFloor = XrHead.position;
            headFloor.y = targetWorldPos.y;

            // Move origin so head lands at target
            Vector3 delta = targetWorldPos - headFloor;
            _playerAlignToPos = XrOrigin.position + delta;

            // Rotate origin so head yaw matches target yaw
            float headYaw = XrHead.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(headYaw, targetWorldYaw);
            _playerAlignToYaw = _playerAlignFromYaw + deltaYaw;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;
        }

        private void ToggleRecord()
        {
            if (!_recording)
            {
                _recording = true;

                // Beim Start: Anchor auf Bühne setzen (damit tracking->stage stabil ist)
                _xrProvider?.SetAnchorFromTakeRoot(TakeRoot);



                // Wenn es eine desiredStartPose gibt: zuerst alignen, dann Begin
                if (UseXR)
                {
                    if (_aIsLive && _hasLastEndA)
                    {
                        StartPlayerAlignToStagePose(_lastEndPosA, _lastEndYawA, SmoothAlignSeconds);
                        StartCoroutine(BeginRecordingAfterAlign(true, SmoothAlignSeconds));
                        return;
                    }
                    if (!_aIsLive && _hasLastEndB)
                    {
                        StartPlayerAlignToStagePose(_lastEndPosB, _lastEndYawB, SmoothAlignSeconds);
                        StartCoroutine(BeginRecordingAfterAlign(false, SmoothAlignSeconds));
                        return;
                    }
                }

                // erster Take: sofort beginnen
                BeginRecordingNow();

                // Playback stoppen
                if (_aIsLive) _playB.Stop(); else _playA.Stop();
            }
            else
            {
                _recording = false;

                if (_aIsLive)
                {
                    var info = _recA.EndAndGetTrimInfo();

                    // Frames sind sofort verfügbar:
                    var take = _recA.Current;
                    _lastTakeA = take; // schon mal setzen (Audio wird ggf. später ersetzt)

                    if (take != null && take.Frames.Count > 0)
                    {
                        var lastFrame = take.Frames[take.Frames.Count - 1];
                        _lastEndPosA = lastFrame.Body.Pos;
                        _lastEndYawA = lastFrame.Body.YawDeg;
                        _hasLastEndA = true;
                    }

                    if (info.HasValue)
                        StartCoroutine(FinalizeTrimNextFrame(true, info.Value));
                }
                else
                {
                    var info = _recB.EndAndGetTrimInfo();

                    var take = _recB.Current;
                    _lastTakeB = take;

                    if (take != null && take.Frames.Count > 0)
                    {
                        var lastFrame = take.Frames[take.Frames.Count - 1];
                        _lastEndPosB = lastFrame.Body.Pos;
                        _lastEndYawB = lastFrame.Body.YawDeg;
                        _hasLastEndB = true;
                    }

                    if (info.HasValue)
                        StartCoroutine(FinalizeTrimNextFrame(false, info.Value));
                }

            }
        }

        private IEnumerator BeginRecordingAfterAlign(bool forA, float waitSec)
        {
            // warte bis Align fertig (oder waitSec)
            float t0 = Time.time;
            while (_playerAlignActive && Time.time - t0 < waitSec + 0.2f)
                yield return null;

            BeginRecordingNow();
        }

        private void BeginRecordingNow()
        {
            if (_aIsLive)
            {
                if (_hasLastEndA) _recA.SetDesiredStartPose(_lastEndPosA, _lastEndYawA);
                _recA.Begin();
                _playB.Stop();
            }
            else
            {
                if (_hasLastEndB) _recB.SetDesiredStartPose(_lastEndPosB, _lastEndYawB);
                _recB.Begin();
                _playA.Stop();
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

                StartPlayerAlignToActor(_aIsLive ? ActorA : ActorB, SmoothAlignSeconds);


            }
            
        }

    
        private void UpdateLiveRig()
        {
            var p = _router.Provider;
            if (p == null) return;

            if (!p.TryGetHeadPose(out var headStage, out var headRotStage)) return;
            p.TryGetLeftHandPose(out var leftStage, out var leftRotStage);
            p.TryGetRightHandPose(out var rightStage, out var rightRotStage);

            // Wenn Align aktiv: ersetze BodyPose (pos+yaw) durch interpolierte, aber behalte relative Head/Hands
            Vector3 bodyPos = headStage; bodyPos.y = 0f;
            float yaw = headRotStage.eulerAngles.y;

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
                //_lastTakeA = _recA.Current;   //damit das nicht mit dem Rebase in die Quere kommt
                _lastTakeA.AudioClip = trimmed;
                PersistTake("A", _lastTakeA);
            }
            else
            {
                //_lastTakeB = _recB.Current;   //damit das nicht mit dem Rebase in die Quere kommt
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
