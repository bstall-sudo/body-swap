using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class CalibrationState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForCalibration;
        private bool selectableNext;
        private bool _avatarPlacementAtStart;

        public DialogueMode Mode => DialogueMode.Calibration;

        public CalibrationState(FlowController flow)
        {
            _flow = flow;
            _currentRoleIndexForCalibration = 0;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[CalibrationState] Enter");

            selectableNext = _flow.Stage.selectableNext;

            _currentRoleIndexForCalibration = 0;
            _avatarPlacementAtStart = _flow.Stage.AvatarPlacementAtStart;
            
            _flow.Stage.RolesVisualsVisibilityHandler.SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            //make head invisible for rig that will be calibrated.
            _flow.Stage.AvatarCalibration.SetAvatarHeadVisible(_currentRoleIndexForCalibration,false);
            // Set XR-Cam to Role height
            _flow.Stage.ApplyActiveRoleEmbodimentHeight(_currentRoleIndexForCalibration);
            ShowCurrentRoleOrFinish();
        }

        public void Tick(float dt)
        
        {
            _flow.Stage.DriveActiveRoleFromInput(_currentRoleIndexForCalibration, dt);

            //Visual und TechnicalRig folgen hier sepparat, weil ja nicht recorded wird und auch proceduralMove noch nicht aktiv sein soll.
            _flow.Stage.ApplyFollower(_currentRoleIndexForCalibration);
            if (_flow.ConsumePrimaryAction())
            {

                UnityEngine.Debug.Log($"[CalibrationState] ConsumePrimaryAction was called");
                // 1. Aktuelle sichtbare Rolle kalibrieren
                _flow.Stage.AvatarCalibration
                    .CalibrateRole(_currentRoleIndexForCalibration);

                //make head of calibrated avatar visible again.
                _flow.Stage.AvatarCalibration.SetAvatarHeadVisible(_currentRoleIndexForCalibration,true);

                // 2. Zur nächsten Rolle wechseln
                _currentRoleIndexForCalibration++;

                _flow.Stage.RolesVisualsVisibilityHandler.SetOnlyRoleVisible(_currentRoleIndexForCalibration);

                

                // Set XR-Cam to Role height
                _flow.Stage.ApplyActiveRoleEmbodimentHeight(_currentRoleIndexForCalibration);

                ShowCurrentRoleOrFinish();
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log($"[CalibrationState] ConsumeSecondaryAction was called");
                FinishCalibration();
            }
        }

        public void Exit()
        {
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            _flow.Stage.AvatarCalibration.SetAllAvatarHeadsVisible(true);

            //set visibility of visualRig (Debug-) Cubes
            _flow.Stage.RolesVisualsVisibilityHandler.SetAllVisible(false);
            // reset XR-Cam position to level 0 again
            _flow.Stage.ResetEmbodimentHeight();
            
        }

        private void ShowCurrentRoleOrFinish()
        {
            UnityEngine.Debug.Log($"[CalibrationState] ShowCurrentRoleOrFinish() was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
            if (_currentRoleIndexForCalibration >= _flow.Stage.roleCount)
            {
                UnityEngine.Debug.Log($"[CalibrationState] ShowCurrentRoleOrFinish() before FinishCalibration was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
                FinishCalibration();
                return;
            }

            _flow.Stage.AvatarCalibration.SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            //make head invisible for rig that will be calibrated.
            _flow.Stage.AvatarCalibration.SetAvatarHeadVisible(_currentRoleIndexForCalibration,false);
        }

        private void FinishCalibration()
        {
            UnityEngine.Debug.Log($"[CalibrationState] FinishCalibration() was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            //_flow.Stage.MirrorSetVisibility.ActivateMirror(false);

            _flow.Stage.SaveTargetTransformsAfterCalibration();

            if(_avatarPlacementAtStart){
                _flow.SetState(new AvatarPlacementState(_flow));

            }else{
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
}