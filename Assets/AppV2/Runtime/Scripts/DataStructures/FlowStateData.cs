using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.DataStructures
{
    public class FlowStateData
    {
        public int RoleCount;
        public int SceneCount;
        public int ToBeRecorded;
        public int SelectedNext;
        public List<int> Playbacks;
        public List<int> ReactiveIdles;
     
        public void Initialize(int roleCount)
        {
            RoleCount = roleCount;
            ToBeRecorded = 0;
            SelectedNext = -1;
            SceneCount = -1;
            Playbacks = new List<int>();
            ReactiveIdles = new List<int>();
        }
    }
}