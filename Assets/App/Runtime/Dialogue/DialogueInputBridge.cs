using UnityEngine;
using App.Runtime.Input;

namespace App.Runtime.Dialogue
{
    public class DialogueInputBridge : MonoBehaviour
    {
        public XRControllerInput Input;
        public DialogueDirector Director;

        private void OnEnable()
        {
            if (Input == null || Director == null) return;

            Input.OnRecordToggle += Director.ToggleRecordPublic;
            Input.OnSwitchRole += Director.SwitchRolesPublic;
            Input.OnResetStage += Director.ResetStagePublic;
        }

        private void OnDisable()
        {
            if (Input == null || Director == null) return;

            Input.OnRecordToggle -= Director.ToggleRecordPublic;
            Input.OnSwitchRole -= Director.SwitchRolesPublic;
            Input.OnResetStage -= Director.ResetStagePublic;
        }
    }
}