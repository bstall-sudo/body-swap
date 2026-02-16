//Interface um zwischen Keyboard und VR-Input wechseln zu können

using UnityEngine;

namespace App.Runtime.Input
{
    public interface IInputProvider
    {
        bool TryGetHeadPose(out Vector3 position, out Quaternion rotation);
        bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation);
        bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation);

       
        bool RecordPressed();
        bool SwitchPressed();
    }
}
