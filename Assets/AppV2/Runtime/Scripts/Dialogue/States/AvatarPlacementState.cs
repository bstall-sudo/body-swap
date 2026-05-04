using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class AvatarPlacementState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForPlacement;
        private bool selectableNext;



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

            selectableNext = _flow.Stage.selectableNext;

            _currentRoleIndexForPlacement = 0;

            for (int i = 0; i < _flow.Stage.roleCount; i++){

                PlaceCurrentRoleAndAdvance();

            }
            
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

            Vector3 placement = GetTestPlacementPosition(_currentRoleIndexForPlacement);

            _flow.Stage.AvatarCalibration.PlaceRoleAt(
                _currentRoleIndexForPlacement,
                placement,
                lookAt
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
            _flow.Stage.AvatarCalibration.ShowAllRoles();

        }




    }
}