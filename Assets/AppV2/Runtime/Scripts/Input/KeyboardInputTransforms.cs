using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AppV2.Runtime.Scripts.Input
{
    
    public class KeyboardInputTransforms : IInputTransformsProvider
    {
        private Vector3 _headPosition = new Vector3(0, 1.6f, 0);
        private float _yaw = 0f;

        
        private Vector3 _left = new(-0.2f, 1.3f, 0.3f);
        private Vector3 _right = new(0.2f, 1.3f, 0.3f);

        private Vector3 _hipPosition = new Vector3(0, 1.0f, 0);
    

        
        private Vector3 _leftFoot = new(-0.2f, 0.0f, 0.0f);
        private Vector3 _rightFoot = new(0.2f, 0.0f, 0.0f);



        public bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
        {
            float speed = 2f;

            // Tasten (Input System)
            if (Keyboard.current.wKey.isPressed) _headPosition += Vector3.forward * speed * Time.deltaTime;
            if (Keyboard.current.sKey.isPressed) _headPosition += Vector3.back * speed * Time.deltaTime;
            if (Keyboard.current.aKey.isPressed) _headPosition += Vector3.left * speed * Time.deltaTime;
            if (Keyboard.current.dKey.isPressed) _headPosition += Vector3.right * speed * Time.deltaTime;

            if (Keyboard.current.qKey.isPressed) _yaw -= 90f * Time.deltaTime;
            if (Keyboard.current.eKey.isPressed) _yaw += 90f * Time.deltaTime;

            position = _headPosition;
            rotation = Quaternion.Euler(0, _yaw, 0);
            return true;
        }

        public bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation)
        {
            position = _left;
            rotation = Quaternion.identity;
            return true;
        }

        public bool TryGetRightHandPose(out Vector3 position, out Quaternion rotation)
        {
            position = _right;
            rotation = Quaternion.identity;
            return true;
        }

        public bool TryGetHipPose(out Vector3 position, out Quaternion rotation)
        {
            float speed = 2f;

            // Tasten (Input System)
            if (Keyboard.current.wKey.isPressed) _hipPosition += Vector3.forward * speed * Time.deltaTime;
            if (Keyboard.current.sKey.isPressed) _hipPosition += Vector3.back * speed * Time.deltaTime;
            if (Keyboard.current.aKey.isPressed) _hipPosition += Vector3.left * speed * Time.deltaTime;
            if (Keyboard.current.dKey.isPressed) _hipPosition += Vector3.right * speed * Time.deltaTime;


            position = _hipPosition;
            rotation = Quaternion.Euler(0, _yaw, 0);
            return true;
        }

        public bool TryGetLeftFootPose(out Vector3 position, out Quaternion rotation)
        {
            position = _leftFoot;
            rotation = Quaternion.identity;
            return true;
        }

        public bool TryGetRightFootPose(out Vector3 position, out Quaternion rotation)
        {
            position = _rightFoot;
            rotation = Quaternion.identity;
            return true;
        }

        //Das ist eine Non-Sense Funktion wegen des Interfaces, welche aber bei Keyboard Input keinen Zweck erfüllt.
        public void SetAnchorFromTakeRoot(Transform takeRoot){
            
        }

    }
}