using UnityEngine;

public class FollowPositionRotationAll : MonoBehaviour
{

    [Header("which one to follow")]
    public Transform _headToFollow;

    [Header("self")]
    public Transform _self;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void LateUpdate()
    {
        if (_headToFollow == null || _self == null) return;

        // Position (nur XZ)
        Vector3 p = _self.position;
        p = _headToFollow.position;
        _self.position = p;

        // Rotation (nur Y)
        Vector3 targetEuler = _headToFollow.rotation.eulerAngles;
        Vector3 selfEuler = _self.rotation.eulerAngles;

        selfEuler = targetEuler;

        _self.rotation = Quaternion.Euler(selfEuler);
    }
}