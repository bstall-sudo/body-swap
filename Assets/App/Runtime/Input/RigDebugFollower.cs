using UnityEngine;
using App.Runtime.Input;
using UnityEngine.InputSystem;


namespace App.Runtime.Debug
{
    public class RigDebugFollower : MonoBehaviour
    {
        [Header("These objects should be CHILDREN of TakeRoot")]
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;

        [Header("Mode")]
        public bool UseXR = false;

        [Header("XR References (from XR Origin)")]
        public Transform XrHead;       // Main Camera
        public Transform XrLeftHand;   // Left Controller
        public Transform XrRightHand;  // Right Controller

        private InputRouter _router;
        private XRInputProvider _xrProvider;

        void Start()
        {
            _router = new InputRouter();
            BuildProvider();
        }

        void OnValidate()
        {
            // Wenn du im Inspector UseXR umschaltest, baut er neu (im Playmode)
            if (Application.isPlaying && _router != null)
                BuildProvider();
        }

        private void BuildProvider()
        {
            if (UseXR)
            {
                _xrProvider = new XRInputProvider(XrHead, XrLeftHand, XrRightHand);
                _router.SetProvider(_xrProvider);

                // Anchor am Anfang einmal setzen (TakeRoot wie er gerade ist)
                _xrProvider.SetAnchorFromTakeRoot(transform);
            }
            else
            {
                _xrProvider = null;
                _router.SetProvider(new KeyboardInputProvider());
            }
        }

        private void ResetTakeRootToHead()
        {
            if (XrHead == null) return;

            Vector3 newPos = XrHead.position;
            newPos.y = 0f; // Boden

            float yaw = XrHead.eulerAngles.y;
            Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

            transform.SetPositionAndRotation(newPos, newRot);
            _xrProvider?.SetAnchorFromTakeRoot(transform);
        }


        void Update()
        {
            if (UseXR && Keyboard.current.bKey.wasPressedThisFrame)
            {
                ResetTakeRootToHead();
            }

            var p = _router.Provider;
            if (p == null) return;

            

            if (Head != null)
            {
                if (p.TryGetHeadPose(out var hp, out var hr))
                {
                    Head.localPosition = hp;
                    Head.localRotation = hr;
                }
            }

            if (LeftHand != null)
            {
                if (p.TryGetLeftHandPose(out var lp, out var lr))
                {
                    LeftHand.localPosition = lp;
                    LeftHand.localRotation = lr;
                }
            }

            if (RightHand != null)
            {
                if (p.TryGetRightHandPose(out var rp, out var rr))
                {
                    RightHand.localPosition = rp;
                    RightHand.localRotation = rr;
                }
            }
        }
    }
}

