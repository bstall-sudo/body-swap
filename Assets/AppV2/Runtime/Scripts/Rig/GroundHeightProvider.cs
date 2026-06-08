using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public enum GroundMode
    {
        FixedHeight,
        Raycast
    }

    public class GroundHeightProvider : MonoBehaviour
    {
        [Header("Ground Mode")]
        [SerializeField]
        private GroundMode groundMode = GroundMode.FixedHeight;

        [Header("Stage")]
        [SerializeField]
        private Transform stageRoot;

        [Header("Fixed Height")]
        [SerializeField]
        private float fixedGroundY = 0f;

        [Header("Raycast")]
        [SerializeField]
        private LayerMask groundLayer;

        [SerializeField]
        private float rayStartHeight = 5f;

        [SerializeField]
        private float rayLength = 20f;

        private void Awake()
        {
            if (stageRoot == null)
                stageRoot = transform;
        }

        public GroundMode Mode
        {
            get => groundMode;
            set => groundMode = value;
        }

        public float FixedGroundY
        {
            get => fixedGroundY;
            set => fixedGroundY = value;
        }

        /// <summary>
        /// Returns the ground height in STAGE LOCAL SPACE.
        /// </summary>
        public float GetGroundYStageLocal(Vector3 stageLocalPosition)
        {
            switch (groundMode)
            {
                case GroundMode.FixedHeight:
                    return fixedGroundY;

                case GroundMode.Raycast:
                    return GetGroundYFromRaycast(stageLocalPosition);

                default:
                    return fixedGroundY;
            }
        }

        private float GetGroundYFromRaycast(Vector3 stageLocalPosition)
        {
            if (stageRoot == null)
                return fixedGroundY;

            Vector3 worldPos =
                stageRoot.TransformPoint(stageLocalPosition);

            Vector3 rayOrigin =
                worldPos + Vector3.up * rayStartHeight;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    rayLength,
                    groundLayer))
            {
                Vector3 hitStageLocal =
                    stageRoot.InverseTransformPoint(hit.point);

                return hitStageLocal.y;
            }

            return fixedGroundY;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (groundMode != GroundMode.Raycast)
                return;

            if (stageRoot == null)
                return;

            Gizmos.color = Color.green;

            Vector3 stagePos = stageRoot.position;

            Vector3 rayOrigin =
                stagePos + Vector3.up * rayStartHeight;

            Gizmos.DrawLine(
                rayOrigin,
                rayOrigin + Vector3.down * rayLength);
        }
#endif
    }
}