using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public static class RolePlacementUtility
    {
        public static Vector3 GetCirclePlacementPosition(
            int roleIndex,
            int roleCount,
            float radius = 1.5f)
        {
            float angle =
                roleIndex *
                Mathf.PI *
                2f /
                Mathf.Max(1, roleCount);

            return new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
        }

        public static Quaternion GetCirclePlacementRotation(
            Vector3 localPosition)
        {
            Vector3 flatDir = -localPosition;
            flatDir.y = 0f;

            if (flatDir.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }
    }
}