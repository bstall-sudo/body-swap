using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class ProceduralHipSolver : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform head;
        [SerializeField] private float hipHeight = 0.95f;
        [SerializeField] private float hipBackOffset = 0.08f;
        [SerializeField] private float smoothSpeed = 12f;

        private float? _calibrationHipHeight;
        private float EffectiveHipHeight => _calibrationHipHeight ?? hipHeight;
        public Vector3 NeutralLocalPosition => new Vector3(0f, EffectiveHipHeight, -hipBackOffset);

        // Calibration and runtime must use the same player-sized neutral reference.
        public void ConfigureNeutralHeight(float heightMeters)
        {
            if (heightMeters > 0f && !float.IsNaN(heightMeters) && !float.IsInfinity(heightMeters))
                _calibrationHipHeight = heightMeters;
        }

        public void ClearCalibrationHeight() => _calibrationHipHeight = null;

        public void ResetToNeutralPose()
        {
            if (bodyRoot == null) return;
            transform.SetPositionAndRotation(
                bodyRoot.position + Vector3.up * EffectiveHipHeight - bodyRoot.forward * hipBackOffset,
                bodyRoot.rotation);
        }

        public void ApplySolver(float dt)
        {
            if (bodyRoot == null)
                return;

            Vector3 targetPos = bodyRoot.position + Vector3.up * EffectiveHipHeight;
            targetPos -= bodyRoot.forward * hipBackOffset;

            Quaternion targetRot = bodyRoot.rotation;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Exp(-smoothSpeed * dt)
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                1f - Mathf.Exp(-smoothSpeed * dt)
            );
        }


    }
}