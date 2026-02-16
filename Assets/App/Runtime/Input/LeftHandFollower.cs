using UnityEngine;
using App.Runtime.Input;

public class LeftHandFollower : MonoBehaviour

{
    private InputRouter _input;

    void Start()
    {
        _input = new InputRouter();
        _input.SetProvider(new KeyboardInputProvider());
    }

    void Update()
    {
        if (_input.Provider.TryGetLeftHandPose(out var pos, out var rot))
        {
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}