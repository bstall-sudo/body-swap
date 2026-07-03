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

        public int SessionVersion = 1;
        public string EnvironmentId = "default";
        public int RoleCount = 2;

        public string StageSpawnId = "default";
        public List<ConversationRoleMeta> Roles = new();
        public List<TakeMeta> Takes = new();
    }

    [Serializable]
    public class ConversationRoleMeta
    {
        public string RoleId;

        public string AvatarId;
        public string AvatarSpawnId = "default";
        public int RoleIndex;
        public string RoleName;

        public int HeightOfRoleCm;
        public bool SittingIdle;

        

        //das bezieht sich auf TechnicalRoot
        public TransformData StartRootPose;

        //das bezieht sich bspw. auf RoleA/RoleB... nützlich für die Begegnung mit zuvor eingespielten Figuren.
        public TransformData StartRoleRootPose;

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
        
        public string TakeId;
        public string RoleId;
        public float DurationSec;
        public int SceneCount;
        public int RoleIndex;

        public bool usesPreRecordedCalibration = false;
        public string sourceRoleId;
        public string npcGroupId;
        public string FramesFile;
        public string AudioFile;
    }
}