using UnityEngine;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class CalibrationState : IState
    {
        private readonly FlowController _flow;

        public DialogueMode Mode => DialogueMode.Calibration;

        public CalibrationState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[CalibrationState] Enter");
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[CalibrationState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[CalibrationState] Consumed SecondaryAction");
                _flow.SetState(new ChooseSpeakerState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[CalibrationState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[CalibrationState] Exit");
        }

    }
}