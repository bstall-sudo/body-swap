using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.DataStructures
{
    public class FlowStateData
    {
        public List<RoleRig> Roles;
        public List<RoleRig> AllRoles;

        public List<int> IndicesOfPassiveRoles;

        public List<int> CurrentPreRecordedPlaybacks;

        public string CurrentNpcGroupId;

        public int TimesPreRecordedPlaybacksWerePlayed;
        public int AllRoleCount;

        public int ActiveRoleCount;
        public int SceneCount;
    
        public int SceneCountWhilePlaybackPreRecorded;
        public int SceneCountBeforePlaybackPreRecorded;
        public int ToBeRecorded;
        public int SelectedNext;
        public List<int> Playbacks;
        public List<int> ReactiveIdles;
        public bool GoToSpeakerState;

        public bool GoToRecordRemainingState;

        public bool GoToPlaybackPreRecordedState;

        //work around zum testen, vermutlich keine gute Idee
        //public bool FromPreRecordedToSpeaker;

        
     
        public void Initialize(List<RoleRig> roles)
        {
            AllRoles = roles;
            Roles = GetActiveRoles(); //Roles sind hier tatsächlich nur alle aktiven Rollen, das ist missverständliche und sollte irgendwann geändert werden.
            IndicesOfPassiveRoles = GetPassiveRolesIndices();
            ActiveRoleCount = Roles.Count;
            AllRoleCount = roles.Count;
            ToBeRecorded = 0;
            SelectedNext = -1;
            SceneCount = -1;
            CurrentPreRecordedPlaybacks = new List<int>();
            Playbacks = new List<int>();
            ReactiveIdles = new List<int>();
            GoToSpeakerState = false;
            GoToRecordRemainingState = false;
            GoToPlaybackPreRecordedState = false;
            //FromPreRecordedToSpeaker = false;
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