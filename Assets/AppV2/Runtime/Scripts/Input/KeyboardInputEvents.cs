using UnityEngine.InputSystem;
using System;
using UnityEngine;

using AppV2.Runtime.Scripts.Dialogue;
namespace AppV2.Runtime.Scripts.Input
{

    public class KeyboardInputEvents : MonoBehaviour
    {
        [Header("Object with FlowController Script")]
        public FlowController flow;


        void Update()
        {


            if (Keyboard.current.spaceKey.wasReleasedThisFrame)
            {
                flow.RequestPrimaryAction();
                
            }

            if (Keyboard.current.enterKey.wasReleasedThisFrame)
            {
                flow.RequestSecondaryAction();

            }

            if (Keyboard.current.backspaceKey.wasReleasedThisFrame)
            {

                flow.RequestResetAction();
            }
        }

    }
}