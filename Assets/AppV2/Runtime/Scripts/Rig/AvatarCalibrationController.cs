using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;


namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarCalibrationController : MonoBehaviour
    {
        private IReadOnlyList<RoleRig> roles;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            this.roles = roles;
        }

        public int RoleCount => roles?.Count ?? 0;

        public void SetOnlyRoleVisible(int visibleIndex)
        {
            if (roles == null) return;

            for (int i = 0; i < roles.Count; i++)
            {
                bool visible = i == visibleIndex;
                roles[i].avatar?.SetVisible(visible);
            }
        }

        public void CalibrateRole(int roleIndex)
        {
            if (!IsValidIndex(roleIndex)) return;

            var avatar = roles[roleIndex].avatar;

            if (avatar == null)
            {
                Debug.LogWarning($"No avatar assigned for role {roleIndex}.");
                return;
            }

            if (avatar.RigFollower == null)
            {
                Debug.LogWarning($"No AvatarRigFollower assigned for role {roleIndex}.");
                return;
            }

            avatar.RigFollower.BuildMap();
            avatar.RigFollower.CalibrateTargetsFromAvatar();
        }

        public void ShowAllRoles()
        {
            for (int i = 0; i < roles.Count; i++)
            {
                roles[i].avatar?.SetVisible(true);
            }
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < roles.Count;
        }


        public void PlaceRoleAt(int roleIndex, Vector3 floorPosition, Vector3 lookAtPoint)
        {
            if (!IsValidIndex(roleIndex)) return;

            RoleRig role = roles[roleIndex];

            if (role == null || role.root == null)
            {
                Debug.LogWarning($"Role or role.root missing for role {roleIndex}.");
                return;
            }

            Vector3 pos = floorPosition;
            pos.y = role.root.position.y;

            role.root.position = pos;

            Vector3 direction = lookAtPoint - role.root.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                role.root.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            role.initialStartPos = role.root.position;
            role.initialStartYawDeg = role.root.eulerAngles.y;
            role.hasInitialStartPose = true;
        }


    }
}
