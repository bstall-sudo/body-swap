using UnityEngine;
using UnityEngine.InputSystem;

using System.Diagnostics;

using AppV2.Runtime.Scripts.Dialogue;

namespace AppV2.Runtime.Scripts.Input
{
    public class XRInputEvents : MonoBehaviour
    {
        [Header("XR Input Actions Asset")]
        public InputActionAsset actions;

        [Header("Object with FlowController Script")]
        public FlowController flow;

        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _resetAction;

        private void OnEnable()
        {
            if (actions == null)
            {
                UnityEngine.Debug.LogError("XRInputEvents: No InputActionAsset assigned.");
                return;
            }

            if (flow == null)
            {
                UnityEngine.Debug.LogError("XRInputEvents: FlowController reference is missing.");
                return;
            }

            _primaryAction = actions.FindAction("PrimaryAction", throwIfNotFound: false);
            _secondaryAction = actions.FindAction("SecondaryAction", throwIfNotFound: false);
            _resetAction = actions.FindAction("ResetAction", throwIfNotFound: false);

            if (_primaryAction != null)
            {
                _primaryAction.performed += OnPrimaryPerformed;
                _primaryAction.Enable();
            }
            else UnityEngine.Debug.LogError("XRInputEvents: Action 'PrimaryAction' not found.");

            if (_secondaryAction != null)
            {
                _secondaryAction.performed += OnSecondaryPerformed;
                _secondaryAction.Enable();
            }
            else UnityEngine.Debug.LogError("XRInputEvents: Action 'SecondaryAction' not found.");

            if (_resetAction != null)
            {
                _resetAction.performed += OnResetPerformed;
                _resetAction.Enable();
            }
            else UnityEngine.Debug.LogError("XRInputEvents: Action 'ResetAction' not found.");
        }

        private void OnDisable()
        {
            if (_primaryAction != null)
            {
                _primaryAction.performed -= OnPrimaryPerformed;
                _primaryAction.Disable();
            }

            if (_secondaryAction != null)
            {
                _secondaryAction.performed -= OnSecondaryPerformed;
                _secondaryAction.Disable();
            }

            if (_resetAction != null)
            {
                _resetAction.performed -= OnResetPerformed;
                _resetAction.Disable();
            }
        }

        private void OnPrimaryPerformed(InputAction.CallbackContext ctx)
            => flow.RequestPrimaryAction();

        private void OnSecondaryPerformed(InputAction.CallbackContext ctx)
            => flow.RequestSecondaryAction(); 

        private void OnResetPerformed(InputAction.CallbackContext ctx)
            => flow.RequestResetAction();
    }
}