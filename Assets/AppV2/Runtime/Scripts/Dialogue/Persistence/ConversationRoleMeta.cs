using System;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    [Serializable]
    public class ConversationRoleMeta
    {
        public string RoleId;
        public int RoleIndex;
        public int HeightCm;
    }
}