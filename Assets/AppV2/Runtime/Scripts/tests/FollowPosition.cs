using UnityEngine;

public class FollowPosition : MonoBehaviour
{
    [Header("which one to follow")]
    public Transform _headToFollow;

    [Header("self")]
    public Transform _self;

    void LateUpdate()
    {
        if (_headToFollow == null || _self == null) return;

        // Position (nur XZ)
        Vector3 p = _self.position;
        p.x = _headToFollow.position.x;
        p.z = _headToFollow.position.z;
        _self.position = p;

        // Rotation (nur Y)
        Vector3 targetEuler = _headToFollow.rotation.eulerAngles;
        Vector3 selfEuler = _self.rotation.eulerAngles;

        selfEuler.y = targetEuler.y;

        _self.rotation = Quaternion.Euler(selfEuler);
    }
}