using UnityEngine;

namespace AppV2.Runtime.Scripts.Input
{
    public class XRInputTransforms : IInputTransformsProvider
    {
        private readonly Transform _head;
        private readonly Transform _left;
        private readonly Transform _right;

        private readonly Transform _hip;
        private readonly Transform _leftFoot;
        private readonly Transform _rightFoot;

        // Anchor-Pose in WORLD (bleibt fix, bis du sie neu setzt)
        private Vector3 _anchorPos;
        private Quaternion _anchorRot;
        private bool _hasAnchor;

        public XRInputTransforms(Transform head, Transform left, Transform right,Transform hip, Transform leftFoot, Transform rightFoot)
        {
            _head = head;
            _left = left;
            _right = right;
            _hip = hip;
            _leftFoot = leftFoot;
            _rightFoot = rightFoot;
        }

        // Bspw in Start rufen, oder von States aus.
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

            // Position: world -> local (nur �ber anchorPose, NICHT �ber aktuelles TakeRoot)
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

        public bool TryGetHipPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_head, out position, out rotation);
        
        public bool TryGetLeftFootPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_left, out position, out rotation);

        public bool TryGetRightFootPose(out Vector3 position, out Quaternion rotation)
            => WorldToAnchorLocal(_right, out position, out rotation);
        

    }

}


