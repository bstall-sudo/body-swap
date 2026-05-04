using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AppV2.Runtime.Scripts.Rig
{
    public class SafePlayArea : MonoBehaviour
    {
        [Header("Generated safe area")]
        public BoxCollider safeAreaCollider;
        public Transform center;

        [Header("Shrink area slightly for safety")]
        public float safetyMargin = 0.25f;

        private readonly List<Vector3> _boundaryPoints = new();

        public bool TryUpdateFromXRBoundary()
        {
            List<XRInputSubsystem> subsystems = new();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                _boundaryPoints.Clear();

                bool success = subsystem.TryGetBoundaryPoints(_boundaryPoints);

                if (success && _boundaryPoints.Count >= 3)
                {
                    ApplyBoundary(_boundaryPoints);
                    return true;
                }
            }

            Debug.LogWarning("No XR boundary points available. Falling back to manual SafePlayArea.");
            return false;
        }

        private void ApplyBoundary(List<Vector3> points)
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            foreach (var p in points)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }

            Vector3 centerPos = new Vector3(
                (minX + maxX) * 0.5f,
                0f,
                (minZ + maxZ) * 0.5f
            );

            float width = Mathf.Max(0.1f, maxX - minX - safetyMargin * 2f);
            float depth = Mathf.Max(0.1f, maxZ - minZ - safetyMargin * 2f);

            if (center != null)
                center.position = centerPos;

            if (safeAreaCollider != null)
            {
                safeAreaCollider.center = transform.InverseTransformPoint(centerPos);
                safeAreaCollider.size = new Vector3(width, 0.1f, depth);
            }

            Debug.Log($"SafePlayArea updated from XR boundary. Size: {width} x {depth}");
        }
    }
}