using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Dialogue.Services;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class AvatarPlacementState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForPlacement;
        private bool selectableNext;

        private int _roleCount;
        private List<int> _allRolesIndices;


        private Vector3 testVectorPlacement = new Vector3(0f, 0f, 1f);
        private Vector3 lookAt = Vector3.zero;


        public DialogueMode Mode => DialogueMode.AvatarPlacement;

        public AvatarPlacementState(FlowController flow)
        {
            _flow = flow;
            _currentRoleIndexForPlacement = 0;
        }

        public void Enter()
        {
            Debug.Log("[AvatarPlacementState] Enter");

            _roleCount = _flow.Stage.roleCount;
            selectableNext = _flow.Stage.selectableNext;
            _allRolesIndices = new List<int>();
            _currentRoleIndexForPlacement = 0;

            for (int i = 0; i < _flow.Stage.roleCount; i++){
                _allRolesIndices.Add(i);
                PlaceCurrentRoleAndAdvance();
                
                //UnityEngine.Debug.Log($"_allRolesIndices count is: {_allRolesIndices.Count}");

            }
            ////hier true, weil bei VisualRig und AvatarRig sollen auch die Roots Kopiert werden im AvatarPlacement State. 
            _flow.Stage.RigUpdatePipeline(_allRolesIndices, true);

            
            
            
        }

        public void Tick(float dt)
        
        {

            if (_flow.ConsumePrimaryAction())
            {

                

            }

            if (_flow.ConsumeSecondaryAction())
            {
 
              
            }
        }

       private void PlaceCurrentRoleAndAdvance()
        {
            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
                return;
            }

            //Vector3 placement = GetTestPlacementPosition(_currentRoleIndexForPlacement);
            int activeRoleCount =0;

            foreach (RoleRig role in _flow.Stage.roles)
            {
                if (!role.hasPreRecordedTakes)
                {
                    activeRoleCount++;
                }
            }
            Vector3 placement = RolePlacementUtility.GetCirclePlacementPosition(_currentRoleIndexForPlacement, activeRoleCount);

            //y wird hier dem Terrain angeglichen.
            placement.y = _flow.Stage.GetGroundYStageLocal(placement);

            //hier nochmal flach machen, damit die Rotation nicht schief wird
            Vector3 flatPlacement = placement;
            flatPlacement.y = 0f;

            Quaternion rotation = RolePlacementUtility.GetCirclePlacementRotation(flatPlacement);

            
            _flow.Stage.AvatarCalibration.PlaceRoleAt(
                _currentRoleIndexForPlacement,
                placement,
                rotation,
                _flow.Stage._stageRoot
            );

            _currentRoleIndexForPlacement++;

            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
                return;
            }

        }

        private Vector3 GetTestPlacementPosition(int roleIndex)
        {
            float radius = 1.5f;
            float angle = roleIndex * Mathf.PI * 2f / Mathf.Max(1, _flow.Stage.roleCount);

            return new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
        }

        private void GoToNextState()
        {
            if (selectableNext)
            {
                _flow.SetState(new ChooseSpeakerState(_flow));
            }
            else
            {
                _flow.SetState(new PlayerAlignState(_flow));
            }
        }

        

        public void Exit()
        {
            //hier true, weil bei VisualRig und AvatarRig sollen auch die Roots Kopiert werden im AvatarPlacement State. 
            _flow.Stage.RigUpdatePipeline(_allRolesIndices, true);
            _flow.Stage.AvatarCalibration.ShowAllRoles();
            _flow.Stage.AvatarCalibration.ShowAllRoles();

        }




    }
}