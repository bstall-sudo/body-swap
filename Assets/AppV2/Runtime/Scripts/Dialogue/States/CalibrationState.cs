using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class CalibrationState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForCalibration;
        private bool selectableNext;

        public DialogueMode Mode => DialogueMode.Calibration;

        public CalibrationState(FlowController flow)
        {
            _flow = flow;
            _currentRoleIndexForCalibration = 0;
        }

        public void Enter()
        {
            Debug.Log("[CalibrationState] Enter");

            selectableNext = _flow.Stage.selectableNext;

            _currentRoleIndexForCalibration = 0;
            
            _flow.Stage.RolesVisualsVisibilityHandler.SetOnlyRoleVisible(_currentRoleIndexForCalibration);
            // Set XR-Cam to Role height
            _flow.Stage.ApplyActiveRoleEmbodimentHeight(_currentRoleIndexForCalibration);
            ShowCurrentRoleOrFinish();
        }

        public void Tick(float dt)
        
        {
            _flow.Stage.DriveActiveRoleFromInput(_currentRoleIndexForCalibration);
            if (_flow.ConsumePrimaryAction())
            {

                // 1. Aktuelle sichtbare Rolle kalibrieren
                _flow.Stage.AvatarCalibration
                    .CalibrateRole(_currentRoleIndexForCalibration);

                // 2. Zur nächsten Rolle wechseln
                _currentRoleIndexForCalibration++;

                _flow.Stage.RolesVisualsVisibilityHandler.SetOnlyRoleVisible(_currentRoleIndexForCalibration);
                // Set XR-Cam to Role height
                _flow.Stage.ApplyActiveRoleEmbodimentHeight(_currentRoleIndexForCalibration);

                ShowCurrentRoleOrFinish();
            }

            if (_flow.ConsumeSecondaryAction())
            {
                FinishCalibration();
            }
        }

        public void Exit()
        {
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            //set visibility of visualRig (Debug-) Cubes
            _flow.Stage.RolesVisualsVisibilityHandler.SetAllVisible(false);
            // reset XR-Cam position to level 0 again
            _flow.Stage.ResetEmbodimentHeight();
        }

        private void ShowCurrentRoleOrFinish()
        {
            if (_currentRoleIndexForCalibration >= _flow.Stage.roleCount)
            {
                FinishCalibration();
                return;
            }

            _flow.Stage.AvatarCalibration
                .SetOnlyRoleVisible(_currentRoleIndexForCalibration);
        }

        private void FinishCalibration()
        {
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            if (selectableNext)
            {
                _flow.SetState(new ChooseSpeakerState(_flow));
            }
            else
            {
                _flow.SetState(new RecordSpeakerState(_flow));
            }
        }
    }
}