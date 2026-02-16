using UnityEngine;

namespace App.Runtime.Debug
{
    [System.Serializable]
    public struct PoseSample
    {
        public Vector3 Pos;
        public Quaternion Rot;
    }

    [System.Serializable]
    public struct PoseFrame
    {
        public float Time;
        public PoseSample Head;
        public PoseSample Left;
        public PoseSample Right;
    }
}
