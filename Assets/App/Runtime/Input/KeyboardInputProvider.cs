using UnityEngine;
using UnityEngine.InputSystem;

namespace App.Runtime.Input
{
    public class KeyboardInputProvider : IInputProvider
    {
        private Vector3 _head = new(0, 1.6f, 0);
        private Vector3 _left = new(-0.2f, 1.3f, 0.3f);
        private Vector3 _right = new(0.2f, 1.3f, 0.3f);

        private float _yaw = 0f;

        public bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            // Head bewegen: WASD, hoch/runter: q/e, drehen: y/x
            float speed = 2f;

            if (Keyboard.current.wKey.isPressed) _head += Vector3.forward * speed * Time.deltaTime;
            if (Keyboard.current.sKey.isPressed) _head += Vector3.back * speed * Time.deltaTime;
            if (Keyboard.current.aKey.isPressed) _head += Vector3.left * speed * Time.deltaTime;
            if (Keyboard.current.dKey.isPressed) _head += Vector3.right * speed * Time.deltaTime;

            if (Keyboard.current.qKey.isPressed) _head += Vector3.up * speed * Time.deltaTime;
            if (Keyboard.current.eKey.isPressed) _head += Vector3.down * speed * Time.deltaTime;

            if (Keyboard.current.zKey.isPressed) _yaw -= 90f * Time.deltaTime;
            if (Keyboard.current.xKey.isPressed) _yaw += 90f * Time.deltaTime;

            position = _head;
            rotation = Quaternion.Euler(0, _yaw, 0);
            return true;
        }

        public bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation)
        {
            // Left Hand: TFGH (wie WASD rechts), r/Hoch, z/Runter
            position = default;
            rotation = default;
            float speed = 2f;

            if (Keyboard.current.tKey.isPressed) _left += Vector3.forward * speed * Time.deltaTime;
            if (Keyboard.current.gKey.isPressed) _left += Vector3.back * speed * Time.deltaTime;
            if (Keyboard.current.fKey.isPressed) _left += Vector3.left * speed * Time.deltaTime;
            if (Keyboard.current.hKey.isPressed) _left += Vector3.right * speed * Time.deltaTime;


            if (Keyboard.current.rKey.isPressed) _left += Vector3.up * speed * Time.deltaTime;
            if (Keyboard.current.yKey.isPressed) _left += Vector3.down * speed * Time.deltaTime;

            position = _left;
            rotation = Quaternion.identity;
            return true;
        }

        public bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation)
        {
            // Right Hand: IJKL, u/Hoch, o/Runter
            position = default;
            rotation = default;
            float speed = 2f;

            if (Keyboard.current.iKey.isPressed) _right += Vector3.forward * speed * Time.deltaTime;
            if (Keyboard.current.kKey.isPressed) _right += Vector3.back * speed * Time.deltaTime;
            if (Keyboard.current.jKey.isPressed) _right += Vector3.left * speed * Time.deltaTime;
            if (Keyboard.current.lKey.isPressed) _right += Vector3.right * speed * Time.deltaTime;

            if (Keyboard.current.uKey.isPressed) _right += Vector3.up * speed * Time.deltaTime;
            if (Keyboard.current.oKey.isPressed) _right += Vector3.down * speed * Time.deltaTime;

            position = _right;
            rotation = Quaternion.identity;
            return true;
        }

        public bool RecordPressed() => Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool SwitchPressed() => Keyboard.current.tabKey.wasPressedThisFrame;
    }
}
