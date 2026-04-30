using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class ReactiveIdleController
    {
        private IReadOnlyList<RoleRig> _roles;
        


        public void Initialize(List<RoleRig> roles)
        {
            _roles = roles;
        }

        public void SetRoleToIdleLookingAt(int idleRoleIndex, int speakerRoleIndex)
        {
            if (!IsValidRoleIndex(idleRoleIndex)) return;
            if (!IsValidRoleIndex(speakerRoleIndex)) return;
            if (idleRoleIndex == speakerRoleIndex) return;

            RoleRig idleRole = _roles[idleRoleIndex];
            RoleRig speakerRole = _roles[speakerRoleIndex];

            if (idleRole.avatar == null)
            {
                UnityEngine.Debug.LogWarning($"[ReactiveIdleController] Role {idleRoleIndex} has no avatar.");
                return;
            }

            if (speakerRole.head == null)
            {
                UnityEngine.Debug.LogWarning($"[ReactiveIdleController] Speaker role {speakerRoleIndex} has no head.");
                return;
            }

            idleRole.avatar.SetRigModeIdle();
            idleRole.avatar.PlayIdleAnimation(idleRole.sittingIdle);
            idleRole.avatar.SetLookAtTargetWorldPosition(speakerRole.head.position);

        }

        public void SetRoleToRecordPlayback(int roleIndex)
        {
            if (!IsValidRoleIndex(roleIndex)) return;

            RoleRig role = _roles[roleIndex];

            if (role.avatar == null)
                return;

            role.avatar.SetRigModeRecordPlayback();
            role.avatar.BackToTPose();
        }

        public void UpdateIdleLookTargets(List<int> reactiveIdles, int speakerRoleIndex)
        {
            if (!IsValidRoleIndex(speakerRoleIndex)) return;

            Transform speakerHead = _roles[speakerRoleIndex].head;

            //UnityEngine.Debug.Log($"speakerhead position is: {speakerHead.position}");
            if (speakerHead == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int idleRoleIndex = reactiveIdles[i];

                if (!IsValidRoleIndex(idleRoleIndex)) continue;

                RoleRig idleRole = _roles[idleRoleIndex];

                if (idleRole.avatar == null) continue;

                idleRole.avatar.SetLookAtTargetWorldPosition(speakerHead.position);
            }
        }

        
        private bool IsValidRoleIndex(int index)
        {
            return _roles != null && index >= 0 && index < _roles.Count;
        }
    }
}