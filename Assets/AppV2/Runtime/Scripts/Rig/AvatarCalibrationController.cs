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

        public int RoleCount => roles.Count;

        public void SetOnlyRoleVisible(int visibleIndex)
        {
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


    }
}
