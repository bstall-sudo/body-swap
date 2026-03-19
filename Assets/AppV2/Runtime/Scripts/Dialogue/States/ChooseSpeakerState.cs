using UnityEngine;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class ChooseSpeakerState : IState
    {
        private readonly FlowController _flow;

        public DialogueMode Mode => DialogueMode.ChooseSpeaker;

        public ChooseSpeakerState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[ChooseSpeakerState] Enter");
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed SecondaryAction");
                _flow.SetState(new RecordSpeakerState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[ChooseSpeakerState] Exit");
        }

    }
}