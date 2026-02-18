using UnityEngine;
using UnityEngine.InputSystem;
using App.Runtime.Input;

namespace App.Runtime.Tools

{
    public class DialogueDebugController : MonoBehaviour
    {
        [Header("Which rig is live-controlled right now?")]
        public bool RoleAIsLive = true;

        [Header("Role A Targets (children of TakeRoot)")]
        public RigTargets RoleA;

        [Header("Role B Targets (children of TakeRoot)")]
        public RigTargets RoleB;

        [Header("Input Mode")]
        public bool UseXR = true;

        [Header("XR References (from XR Origin)")]
        public Transform XrHead;       // Main Camera
        public Transform XrLeftHand;   // Left Controller
        public Transform XrRightHand;  // Right Controller

        private InputRouter _router;
        private XRInputProvider _xrProvider;

        private readonly MemoryPoseTrack _trackA = new();
        private readonly MemoryPoseTrack _trackB = new();

        private bool _isRecording = false;
        private float _t0 = 0f;

        private bool _isPlayingBack = false;
        private float _playT0 = 0f;
        private int _playIndex = 0;

        void Start()
        {
            _router = new InputRouter();
            BuildProvider();
        }

        private void BuildProvider()
        {
            if (UseXR)
            {
                _xrProvider = new XRInputProvider(XrHead, XrLeftHand, XrRightHand);
                _router.SetProvider(_xrProvider);

                // Anchor initialisieren auf aktuelles TakeRoot
                _xrProvider.SetAnchorFromTakeRoot(transform);
            }
            else
            {
                _xrProvider = null;
                _router.SetProvider(new KeyboardInputProvider());
            }
        }

        void Update()
        {
            // B: TakeRoot zur Kopfposition setzen + Anchor neu setzen
            if (UseXR && Keyboard.current.bKey.wasPressedThisFrame)
            {
                ResetTakeRootToHead();
            }

            // SPACE: Recording togglen (start/stop)
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ToggleRecording();
            }

            // TAB: Rolle wechseln (A <-> B) und Playback starten
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                SwitchRolesAndPlayOther();
            }

            // 1) Live rig updaten
            UpdateLiveRig();

            // 2) Wenn Recording aktiv -> Frames sammeln
            if (_isRecording)
                RecordFrame();

            // 3) Playback Tick
            if (_isPlayingBack)
                PlaybackTick();
        }

        private RigTargets LiveRig => RoleAIsLive ? RoleA : RoleB;
        private RigTargets PlaybackRig => RoleAIsLive ? RoleB : RoleA;

        private MemoryPoseTrack LiveTrack => RoleAIsLive ? _trackA : _trackB;
        private MemoryPoseTrack PlaybackTrack => RoleAIsLive ? _trackB : _trackA;

        private void UpdateLiveRig()
        {
            var p = _router.Provider;
            if (p == null) return;

            if (LiveRig.Head != null && p.TryGetHeadPose(out var hp, out var hr))
            {
                LiveRig.Head.localPosition = hp;
                LiveRig.Head.localRotation = hr;
            }

            if (LiveRig.LeftHand != null && p.TryGetLeftHandPose(out var lp, out var lr))
            {
                LiveRig.LeftHand.localPosition = lp;
                LiveRig.LeftHand.localRotation = lr;
            }

            if (LiveRig.RightHand != null && p.TryGetRightHandPose(out var rp, out var rr))
            {
                LiveRig.RightHand.localPosition = rp;
                LiveRig.RightHand.localRotation = rr;
            }
        }

        private void ToggleRecording()
        {
            if (!_isRecording)
            {
                // Start recording
                _isRecording = true;
                _t0 = Time.time;
                LiveTrack.Clear();

                // Beim Start eines Takes Anchor auf aktuelles TakeRoot setzen
                _xrProvider?.SetAnchorFromTakeRoot(transform);

                // Playback stoppen, falls aktiv
                _isPlayingBack = false;
            }
            else
            {
                // Stop recording
                _isRecording = false;
            }
        }

        private void RecordFrame()
        {
            float t = Time.time - _t0;

            // Wir recorden das, was aktuell am LiveRig an lokalen Posen anliegt:
            var f = new PoseFrame
            {
                Time = t,
                Head = new PoseSample { Pos = LiveRig.Head.localPosition, Rot = LiveRig.Head.localRotation },
                Left = new PoseSample { Pos = LiveRig.LeftHand.localPosition, Rot = LiveRig.LeftHand.localRotation },
                Right = new PoseSample { Pos = LiveRig.RightHand.localPosition, Rot = LiveRig.RightHand.localRotation },
            };

            LiveTrack.Frames.Add(f);
        }

        private void SwitchRolesAndPlayOther()
        {
            // Stop Recording, wenn gerade aktiv
            _isRecording = false;

            // Rollen tauschen
            RoleAIsLive = !RoleAIsLive;

            // Playback des "anderen" starten (also der Rolle, die jetzt NICHT live ist)
            StartPlaybackOfOther();
        }

        private void StartPlaybackOfOther()
        {
            if (PlaybackTrack.Frames.Count == 0)
            {
                _isPlayingBack = false;
                return;
            }

            _isPlayingBack = true;
            _playT0 = Time.time;
            _playIndex = 0;
        }

        private void PlaybackTick()
        {
            float t = Time.time - _playT0;

            // spiele Frames ab bis Zeit erreicht
            var frames = PlaybackTrack.Frames;
            while (_playIndex < frames.Count && frames[_playIndex].Time <= t)
            {
                var f = frames[_playIndex];

                if (PlaybackRig.Head != null)
                {
                    PlaybackRig.Head.localPosition = f.Head.Pos;
                    PlaybackRig.Head.localRotation = f.Head.Rot;
                }

                if (PlaybackRig.LeftHand != null)
                {
                    PlaybackRig.LeftHand.localPosition = f.Left.Pos;
                    PlaybackRig.LeftHand.localRotation = f.Left.Rot;
                }

                if (PlaybackRig.RightHand != null)
                {
                    PlaybackRig.RightHand.localPosition = f.Right.Pos;
                    PlaybackRig.RightHand.localRotation = f.Right.Rot;
                }

                _playIndex++;
            }

            if (_playIndex >= frames.Count)
                _isPlayingBack = false;
        }

        private void ResetTakeRootToHead()
        {
            if (XrHead == null) return;

            Vector3 newPos = XrHead.position;
            newPos.y = 0f;

            float yaw = XrHead.eulerAngles.y;
            Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

            transform.SetPositionAndRotation(newPos, newRot);

            _xrProvider?.SetAnchorFromTakeRoot(transform);
        }
    }
}
