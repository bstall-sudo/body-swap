using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace App.Runtime.Input
{
    public class XRControllerInput : MonoBehaviour
    {
        [Header("Input Actions Asset")]
        public InputActionAsset Actions;

        private InputAction _recordAction;
        private InputAction _switchAction;
        private InputAction _resetAction;

        public event Action OnRecordToggle;
        public event Action OnSwitchRole;
        public event Action OnResetStage;

        private void OnEnable()
        {
            
            if (Actions == null)
            {
                UnityEngine.Debug.LogError("No InputActionAsset assigned.");
                return;
            }

            // ActionMap + Action Name müssen exakt so heißen wie in deinem Asset
            _recordAction = Actions.FindAction("RecordToggle");
            _switchAction = Actions.FindAction("SwitchRole");
            _resetAction = Actions.FindAction("ResetStage");

            _recordAction?.Enable();
            _switchAction?.Enable();
            _resetAction?.Enable();

            if (_recordAction != null)
                _recordAction.performed += ctx => OnRecordToggle?.Invoke();

            if (_switchAction != null)
                _switchAction.performed += ctx => OnSwitchRole?.Invoke();

            if (_resetAction != null)
                _resetAction.performed += ctx => OnResetStage?.Invoke();
        }

        private void OnDisable()
        {
            _recordAction?.Disable();
            _switchAction?.Disable();
            _resetAction?.Disable();
        }
    }
}