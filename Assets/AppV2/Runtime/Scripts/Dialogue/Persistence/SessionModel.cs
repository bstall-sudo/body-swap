using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    [Serializable]
    public class SessionModel
    {
        public string SessionId;
        public string CreatedUtc;

        public List<ConversationRoleMeta> Roles = new();
        public List<TakeMeta> Takes = new();
    }

    [Serializable]
    public class ConversationRoleMeta
    {
        public string RoleId;
        public int RoleIndex;
        public string RoleName;

        public int HeightOfRoleCm;
        public bool SittingIdle;

        public RoleCalibrationData Calibration = new();
    }

    [Serializable]
    public class RoleCalibrationData
    {
        public TransformData headTarget;
        public TransformData leftHandTarget;
        public TransformData rightHandTarget;
        public TransformData hipTarget;
        public TransformData leftFootTarget;
        public TransformData rightFootTarget;
    }

    [Serializable]
    public class TransformData
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        //public Vector3 LocalScale;
    }

    [Serializable]
    public class TakeMeta
    {
        public string SessionId;
        public string TakeId;
        public string RoleId;
        public float DurationSec;
        public int SceneCount;
        public int RoleIndex;

        public string FramesFile;
        public string AudioFile;
    }
}