using UnityEngine;
using UnityEngine.InputSystem;

public class TrackerCubeTest : MonoBehaviour
{
    public InputActionReference positionAction;
    public InputActionReference rotationAction;

    private void OnEnable()
    {
        positionAction.action.Enable();
        rotationAction.action.Enable();
    }

    private void OnDisable()
    {
        positionAction.action.Disable();
        rotationAction.action.Disable();
    }

    private void Update()
    {
        Vector3 pos = positionAction.action.ReadValue<Vector3>();
        Quaternion rot = rotationAction.action.ReadValue<Quaternion>();

        transform.localPosition = pos;
        transform.localRotation = rot;

        Debug.Log($"Tracker pos: {pos}, rot: {rot.eulerAngles}");
    }
}