using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class XRDeviceDebug : MonoBehaviour
{
    void Update()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        foreach (var d in devices)
        {
            Debug.Log($"{d.name} | {d.characteristics}");
        }
    }
}