using UnityEngine;

namespace App.Runtime.Input
{
    public class XRInputProvider : IInputProvider
    {
        private readonly Transform _head;
        private readonly Transform _left;
        private readonly Transform _right;

        // Anchor-Pose in WORLD (bleibt fix, bis du sie neu setzt)
        private Vector3 _anchorPos;
        private Quaternion _anchorRot;
        private bool _hasAnchor;

        public XRInputProvider(Transform head, Transform left, Transform right)
        {
            _head = head;
            _left = left;
            _right = right;
        }

        // Das rufst du auf, wenn du "B" drückst:
        public void SetAnchorFromTakeRoot(Transform takeRoot)
        {
            if (takeRoot == null) return;
            _anchorPos = takeRoot.position;
            _anchorRot = takeRoot.rotation;
            _hasAnchor = true;
        }

        private bool WorldToAnchorLocal(Transform t, out Vector3 localPos, out Quaternion localRot)
        {
            localPos = default;
            localRot = default;

            if (!_hasAnchor || t == null) return false;

            // Position: world -> local (nur über anchorPose, NICHT über aktuelles TakeRoot)
            Vector3 delta = t.position - _anchorPos;
            localPos = Quaternion.Inverse(_anchorRot) * delta;

            // Rotation: world -> local
            localRot = Quaternion.Inverse(_anchorRot) * t.rotation;

            return true;
        }

        public bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_head, out position, out rotation);

        public bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_left, out position, out rotation);

        public bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_right, out position, out rotation);

        public bool RecordPressed() => false;
        public bool SwitchPressed() => false;
    }
}
