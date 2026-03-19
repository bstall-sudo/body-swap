using System.Diagnostics;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class TestState : IState
    {
        private readonly FlowController _flow;
        public DialogueMode Mode => DialogueMode.Test;

        public TestState(FlowController flow) => _flow = flow;

        public void Enter() => UnityEngine.Debug.Log("[TestState] Enter");
        public void Tick(float dt)
        {
            // zurück nach Idle bei PrimaryAction
            if (_flow.ConsumePrimaryAction())
                _flow.SetState(new IdleState(_flow));
        }
        public void Exit() => UnityEngine.Debug.Log("[TestState] Exit");
    }
}