using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Rig
{
    public class RolesVisualsVisibilityHandler : MonoBehaviour
    {
        private IReadOnlyList<RoleRig> roles;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            this.roles = roles;
        }

        public void SetAllVisible(bool visible)
        {
            if (roles == null) return;

            for (int i = 0; i < roles.Count; i++)
            {
                roles[i].visualRolesVisibility?.SetVisible(visible);
            }
        }

        public void SetOnlyRoleVisible(int roleIndex)
        {
            if (roles == null) return;

            for (int i = 0; i < roles.Count; i++)
            {
                roles[i].visualRolesVisibility?.SetVisible(i == roleIndex);
            }
        }
    }
}