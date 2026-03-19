using UnityEngine;

namespace AppV2.Runtime.Scripts.Input
{
    public interface IInputTransformsProvider
    {
        //später hier noch feet und hüfte einfügen
        bool TryGetHeadPose(out Vector3 position, out Quaternion rotation);
        bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation);
        bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation);

        public void SetAnchorFromTakeRoot(Transform takeRoot);
    }
}