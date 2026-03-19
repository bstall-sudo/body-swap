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
}
}