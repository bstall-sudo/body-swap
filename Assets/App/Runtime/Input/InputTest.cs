using UnityEngine;
using App.Runtime.Input;

public class InputTest : MonoBehaviour
{
    public XRControllerInput input;

    void OnEnable()
    {
        input.OnRecordToggle += () => UnityEngine.Debug.Log("RecordToggle!");
        input.OnSwitchRole += () => UnityEngine.Debug.Log("SwitchRole!");
        input.OnResetStage += () => UnityEngine.Debug.Log("ResetStage!");
    }

    void OnDisable()
    {
        input.OnRecordToggle -= () => UnityEngine.Debug.Log("RecordToggle!");
        input.OnSwitchRole -= () => UnityEngine.Debug.Log("SwitchRole!");
        input.OnResetStage -= () => UnityEngine.Debug.Log("ResetStage!");
    }
}