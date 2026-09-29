using System.Diagnostics;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class SceneConfigurationState : IState
    {
        private readonly FlowController _flow;

        public DialogueMode Mode => DialogueMode.SceneConfigurationState;

        public SceneConfigurationState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[SceneConfigurationState] Enter");

            _flow.ConfigurationUI.Show();

            _flow.PresetController.BeginConfiguration();
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[SceneConfigurationState] Consumed PrimaryAction");
                _flow.SetState(new CalibrationState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[SceneConfigurationState] Consumed SecondaryAction");
                
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[SceneConfigurationState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[SceneConfigurationState] Exit");
        }
    }
}