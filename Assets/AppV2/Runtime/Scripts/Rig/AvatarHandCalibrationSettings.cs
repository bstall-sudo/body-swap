using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    /// <summary>Optional per-avatar adjustments for automatic calibration only.
    /// Put this on the loaded avatar prefab (beneath the role's Avatar root).
    /// Corrections are saved through the existing session calibration offsets.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BodySwap/Avatar Hand Calibration Settings")]
    public sealed class AvatarHandCalibrationSettings : MonoBehaviour
    {
        [Header("Optional controller reference in avatar-local T-pose")]
        [Tooltip("Off: infer finger direction from Humanoid/Mixamo reference-pose bones. " +
            "On: use the two reference Euler rotations below instead.")]
        public bool overrideReferenceRotations;
        public Vector3 leftReferenceRotation = new Vector3(0f, -90f, 0f);
        public Vector3 rightReferenceRotation = new Vector3(0f, 90f, 0f);

        [Header("Additional IK target rotation in local target axes (degrees)")]
        [Tooltip("Applied after automatic calibration. Start with zero, adjust one axis " +
            "at a time, then restart calibration. Does not alter bones or manual calibration.")]
        public Vector3 leftTargetRotationCorrection;
        public Vector3 rightTargetRotationCorrection;
    }
}
