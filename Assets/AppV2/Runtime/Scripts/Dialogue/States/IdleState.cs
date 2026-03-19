using System.Diagnostics;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class IdleState : IState
    {
        private readonly FlowController _flow;

        public DialogueMode Mode => DialogueMode.Idle;

        public IdleState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[IdleState] Enter");
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[IdleState] Consumed PrimaryAction");
                // später: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[IdleState] Consumed SecondaryAction");
                _flow.SetState(new TestState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[IdleState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[IdleState] Exit");
        }
    }
}