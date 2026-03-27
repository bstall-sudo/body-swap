using System;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Visual rig")]
    public Transform avatarRoot;

    public float visualGroundOffsetY = 0f;

    [Tooltip("If true, heightOfRoleCm will be initialized from the player height once.")]
     public bool usePlayerHeightAsDefault = true;
}
}