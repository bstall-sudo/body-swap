using System.Collections.Generic;
using UnityEngine;

namespace App.Runtime.Dialogue
{
    [System.Serializable]
    public struct BodyPose
    {
        public Vector3 Pos;      // Stage-local (y = 0)
        public float YawDeg;     // nur Y Rotation
    }

    [System.Serializable]
    public struct PoseSample
    {
        public Vector3 Pos;      // local relativ zum ActorRoot
        public Quaternion Rot;   // local relativ zum ActorRoot
    }

    [System.Serializable]
    public struct Frame
    {
        public float T;          // Sekunden seit Take-Start (dsp-basiert)
        public BodyPose Body;
        public PoseSample Head;
        public PoseSample Left;
        public PoseSample Right;
    }

    public class TakeData
    {
        public List<Frame> Frames = new();
        public AudioClip AudioClip;     // aufgenommen
        public float DurationSec;       // aus letztem Frame
        //public float AudioStartOffsetSec; // wieviel später Audio effektiv startete

    }
}
