using UnityEngine;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class FinalizeConversationState : IState
    {
        private readonly FlowController _flow;

        public DialogueMode Mode => DialogueMode.FinalizeConversation;

        public FinalizeConversationState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[FinalizeConversationState] Enter");
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[FinalizeConversationState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[FinalizeConversationState] Consumed SecondaryAction");
                _flow.SetState(new PlaybackFullConversationState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[FinalizeConversationState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[FinalizeConversationState] Exit");
        }

    }
}