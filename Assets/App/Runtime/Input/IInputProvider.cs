//Interface um zwischen Keyboard und VR-Input wechseln zu können

using UnityEngine;

namespace App.Runtime.Input
{
    public interface IInputProvider
    {
        /* 
         * Funktion gibt boolian zurück, mit out lassen sich mehrere Werte zurückgeben.
         * 
         * Beispiel:nur wenn true, werden transform.position und transform.rotation auf pos und rot gesetzt.
                if (provider.TryGetHeadPose(out Vector3 pos, out Quaternion rot))
                    {
                        transform.position = pos;
                        transform.rotation = rot;
                    }
         */
        bool TryGetHeadPose(out Vector3 position, out Quaternion rotation);
        bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation);
        bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation);

       
        bool RecordPressed();
        bool SwitchPressed();
    }
}
