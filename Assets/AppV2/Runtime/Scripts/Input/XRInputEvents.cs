using UnityEngine;
using UnityEngine.InputSystem;



using AppV2.Runtime.Scripts.Dialogue;

namespace AppV2.Runtime.Scripts.Input
{
    public class XRInputEvents : MonoBehaviour
    {
        public enum InputMode
        {
            Standard,
            Simple
        }

        [Header("XR Input Actions Asset")]
        public InputActionAsset actions;

        [Header("Object with FlowController Script")]
        public FlowController flow;

        [Header("Input Mode")]
        [SerializeField] private InputMode inputMode = InputMode.Standard;

        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _resetAction;

        // Extra Action für rechten Trigger im Simple Mode
        private InputAction _rightTriggerAsPrimary;

        private void OnEnable()
        {
            if (actions == null)
            {
                Debug.LogError("XRInputEvents: No InputActionAsset assigned.");
                return;
            }

            if (flow == null)
            {
                Debug.LogError("XRInputEvents: FlowController reference is missing.");
                return;
            }

            _primaryAction =
                actions.FindAction("BodySwapStateMachine/PrimaryAction", false);

            _secondaryAction =
                actions.FindAction("BodySwapStateMachine/SecondaryAction", false);

            _resetAction =
                actions.FindAction("BodySwapStateMachine/ResetAction", false);


            // Rechter Trigger wird separat angelegt.
            // Wird nur im Simple Mode benutzt.
            _rightTriggerAsPrimary = new InputAction(
                name: "RightTriggerAsPrimary",
                type: InputActionType.Button,
                binding: "<XRController>{RightHand}/triggerPressed"
            );


            if (_primaryAction != null)
            {
                _primaryAction.performed += OnPrimaryPerformed;
                _primaryAction.Enable();
            }

            if (_resetAction != null)
            {
                _resetAction.performed += OnResetPerformed;
                _resetAction.Enable();
            }

            ApplyInputMode();
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

            if (_rightTriggerAsPrimary != null)
            {
                _rightTriggerAsPrimary.performed -= OnPrimaryPerformed;
                _rightTriggerAsPrimary.Disable();
                _rightTriggerAsPrimary.Dispose();
                _rightTriggerAsPrimary = null;
            }
        }


        // --------------------------------------------------
        // INPUT MODE
        // --------------------------------------------------

        public void SetInputMode(InputMode mode)
        {
            inputMode = mode;

            if (isActiveAndEnabled)
                ApplyInputMode();
        }

        private void ApplyInputMode()
        {
            // Erst sauber entfernen
            if (_secondaryAction != null)
            {
                _secondaryAction.performed -= OnSecondaryPerformed;
                _secondaryAction.Disable();
            }

            if (_rightTriggerAsPrimary != null)
            {
                _rightTriggerAsPrimary.performed -= OnPrimaryPerformed;
                _rightTriggerAsPrimary.Disable();
            }


            switch (inputMode)
            {
                case InputMode.Standard:

                    // Links = Primary
                    // Rechts = Secondary

                    if (_secondaryAction != null)
                    {
                        _secondaryAction.performed += OnSecondaryPerformed;
                        _secondaryAction.Enable();
                    }

                    break;


                case InputMode.Simple:

                    // Links = Primary
                    // Rechts = ebenfalls Primary

                    if (_rightTriggerAsPrimary != null)
                    {
                        _rightTriggerAsPrimary.performed += OnPrimaryPerformed;
                        _rightTriggerAsPrimary.Enable();
                    }

                    break;
            }

            Debug.Log($"[XRInputEvents] Input mode: {inputMode}");
        }


        // --------------------------------------------------
        // EVENTS
        // --------------------------------------------------

        private void OnPrimaryPerformed(InputAction.CallbackContext ctx)
            => flow.RequestPrimaryAction();

        private void OnSecondaryPerformed(InputAction.CallbackContext ctx)
            => flow.RequestSecondaryAction();

        private void OnResetPerformed(InputAction.CallbackContext ctx)
            => flow.RequestResetAction();
    }
}