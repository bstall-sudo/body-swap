using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class ReactiveIdleController
    {
        private readonly Dictionary<int, Quaternion> _defaultHeadLocalRotations =
            new Dictionary<int, Quaternion>();

        public void RegisterRoleIfNeeded(int roleIndex, RoleRig role)
        {
            if (role == null || role.head == null)
                return;

            if (_defaultHeadLocalRotations.ContainsKey(roleIndex))
                return;

            _defaultHeadLocalRotations[roleIndex] = role.head.localRotation;
        }

        public void ResetHead(int roleIndex, RoleRig role, float resetSpeed = 8f)
        {
            if (role == null || role.head == null)
                return;

            RegisterRoleIfNeeded(roleIndex, role);

            Quaternion defaultRot = _defaultHeadLocalRotations[roleIndex];
            role.head.localRotation = Quaternion.Slerp(
                role.head.localRotation,
                defaultRot,
                Time.deltaTime * resetSpeed
            );
        }

        public void TickLookAt(
            int roleIndex,
            RoleRig role,
            Transform target,
            float turnSpeed = 8f,
            float maxYaw = 60f,
            float maxPitch = 25f)
        {
            if (role == null || role.head == null || target == null)
                return;

            RegisterRoleIfNeeded(roleIndex, role);

            Transform head = role.head;
            Transform parent = head.parent;
            if (parent == null)
                return;

            Vector3 toTargetWorld = target.position - head.position;
            if (toTargetWorld.sqrMagnitude < 0.0001f)
                return;

            Vector3 toTargetLocal = parent.InverseTransformDirection(toTargetWorld.normalized);

            float yaw = Mathf.Atan2(toTargetLocal.x, toTargetLocal.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(
                toTargetLocal.y,
                new Vector2(toTargetLocal.x, toTargetLocal.z).magnitude
            ) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            Quaternion defaultRot = _defaultHeadLocalRotations[roleIndex];
            Quaternion desiredLocalRotation = defaultRot * Quaternion.Euler(pitch, yaw, 0f);

            head.localRotation = Quaternion.Slerp(
                head.localRotation,
                desiredLocalRotation,
                Time.deltaTime * turnSpeed
            );
        }
    }
}