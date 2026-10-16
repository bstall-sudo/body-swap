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

            _flow.PresetController.WorkshopStartRequested += OnWorkshopStartRequested;

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

        private void OnWorkshopStartRequested()
        {
            // Hier deinen bisherigen Übergang zum CalibrationState aufrufen.
            // Beispiel, falls FlowController eine passende Methode anbietet:
            _flow.SetState(new CalibrationState(_flow));
            
        }

        public void Exit()
        {
            _flow.PresetController.WorkshopStartRequested -= OnWorkshopStartRequested;

            _flow.ConfigurationUI.Hide();
            UnityEngine.Debug.Log("[SceneConfigurationState] Exit");
        }
    }
}