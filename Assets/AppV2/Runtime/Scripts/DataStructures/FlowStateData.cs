using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.DataStructures
{
    public class FlowStateData
    {
        public List<RoleRig> Roles;
        public List<RoleRig> AllRoles;

        public List<int> IndicesOfPassiveRoles;
        public int RoleCount;

        public int ActiveRoleCount;
        public int SceneCount;
        public int ToBeRecorded;
        public int SelectedNext;
        public List<int> Playbacks;
        public List<int> ReactiveIdles;
        public bool GoToSpeakerState;
     
        public void Initialize(List<RoleRig> roles)
        {
            AllRoles = roles;
            Roles = GetActiveRoles();
            IndicesOfPassiveRoles = GetPassiveRolesIndices();
            ActiveRoleCount = Roles.Count;
            RoleCount = roles.Count;
            ToBeRecorded = 0;
            SelectedNext = -1;
            SceneCount = -1;
            Playbacks = new List<int>();
            ReactiveIdles = new List<int>();
            GoToSpeakerState = false;
        }

        public List<RoleRig> GetActiveRoles()
        {
            Roles = new List<RoleRig>();

            foreach(RoleRig role in AllRoles)
            {
                if (role.isActiveConversationPartner)
                {
                    Roles.Add(role);

                    UnityEngine.Debug.Log($"[FlowStateData] Role with Index: {role.roleIndex} is active");
                }
                else
                {
                    UnityEngine.Debug.Log($"[FlowStateData] Role with Index: {role.roleIndex} is passive");
                }

                

            }
            return Roles;
        }

        public List<int> GetPassiveRolesIndices()
        {
            List<int> passiveRolesIndices = new List<int>();

            foreach(RoleRig role in AllRoles)
            {
                if (!role.isActiveConversationPartner)
                {
                    passiveRolesIndices.Add(role.roleIndex);

                    UnityEngine.Debug.Log($"[FlowStateData] Role with Index: {role.roleIndex} was added to passiveRoleIndices");
                }
          

                

            }
            return passiveRolesIndices;
        }
    }
}