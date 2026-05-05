using System;
using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.Rig;

namespace AppV2.Runtime.Scripts.DataStructures
{
    [Serializable]
    public class RoleRig
    {
        public string roleId;                 // z.B. "A", "B", "C" oder "Role 1"
        public Transform root;
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;
        
        public AudioSource audioSource;
        [Header("Role size")]
        public int heightOfRoleCm = 180;

        [Header("Initial placement")]
        public bool hasInitialStartPose = false;
        public Vector3 initialStartPos;
        public float initialStartYawDeg;
    

        [Header("Visual Debug Rig")]
        public Transform visualRigRoot;
        public RolesVisualsSetVisibility visualRolesVisibility;

        [Header("Avatar Rig")]
        public bool sittingIdle = false;
        public Transform avatarRoot;
        public string avatarName;
        public AvatarRigDefinition avatar;
        public AvatarRigFollower rigFollower;

        public float visualGroundOffsetY = 0f;

        [Tooltip("If true, heightOfRoleCm will be initialized from the player height once.")]
        public bool usePlayerHeightAsDefault = true;

        public void ResolveAvatarName(bool logWarnings = false)
        {
            if (avatarRoot == null)
            {
                if (logWarnings) Debug.LogWarning($"[RoleRig] avatarRoot is null for role '{roleId}'.");
                return;
            }

            var animator = avatarRoot.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                avatarName = animator.gameObject.name;
            }
            else if (logWarnings)
            {
                Debug.LogWarning($"[RoleRig] No Animator found under '{avatarRoot.name}'.");
            }
        }
    }
}