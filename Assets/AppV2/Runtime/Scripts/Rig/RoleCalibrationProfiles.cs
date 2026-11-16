using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Rig
{
    /// <summary>Snapshots of target offsets and their tracking/scale reference. Applying
    /// a playback profile never rewrites role identity/height or the live snapshot.</summary>
    public static class RoleCalibrationProfiles
    {
        private static readonly string[] Names = { "headTarget", "leftHandTarget", "rightHandTarget",
            "hipTarget", "leftFootTarget", "rightFootTarget" };

        public static RoleCalibrationProfile Capture(RoleRig role, float playerHeightCm, bool proceduralLowerBody = false)
        {
            if (role?.visualRigRoot == null) return null;
            var poses = new TransformData[6];
            for (int i = 0; i < Names.Length; i++)
            {
                var target = Find(role.visualRigRoot, Names[i]);
                if (target == null) return null;
                poses[i] = new TransformData { LocalPosition = target.localPosition, LocalRotation = target.localRotation };
            }
            return new RoleCalibrationProfile {
                Calibration = new RoleCalibrationData { headTarget=poses[0], leftHandTarget=poses[1],
                    rightHandTarget=poses[2], hipTarget=poses[3], leftFootTarget=poses[4], rightFootTarget=poses[5] },
                HasScale = role.avatarRoot != null,
                VisualScale = role.visualRigRoot.localScale,
                AvatarScale = role.avatarRoot != null ? role.avatarRoot.localScale : Vector3.one,
                PlayerHeightCm = playerHeightCm,
                RoleHeightCm = role.heightOfRoleCm,
                UsesProceduralLowerBody = proceduralLowerBody,
                HipReferenceHeightM = role.hipSolver != null ? role.hipSolver.NeutralLocalPosition.y : playerHeightCm / 200f
            };
        }

        public static RoleCalibrationProfile ForTake(RoleRig role, TakeMeta take)
        {
            if (role == null) return null;
            if (role.hasPreRecordedTakes && !role.usePreRecordedCalibrationData)
                return role.liveCalibrationProfile;
            return take?.CalibrationProfile ??
                (take != null && take.usesPreRecordedCalibration && role.usePreRecordedCalibrationData
                    ? role.preRecordedCalibrationProfile ?? role.liveCalibrationProfile : role.liveCalibrationProfile);
        }

        public static bool ApplyLive(RoleRig role)
        {
            if (role?.liveCalibrationProfile == null) return false;
            bool changed = !ReferenceEquals(role.activeCalibrationProfile, role.liveCalibrationProfile);
            if (!Apply(role, role.liveCalibrationProfile)) return false;
            if (changed && role.liveCalibrationProfile.UsesProceduralLowerBody)
                role.hipSolver?.ResetToNeutralPose();
            return true;
        }

        public static void ApplyForTake(RoleRig role, TakeMeta take)
        {
            var profile = ForTake(role, take);
            if (Apply(role, profile)) role.lastTrackingCalibrationProfile = profile;
        }

        public static bool Apply(RoleRig role, RoleCalibrationProfile profile)
        {
            if (role?.visualRigRoot == null || profile?.Calibration == null) return false;
            var data = profile.Calibration;
            var poses = new[] { data.headTarget,data.leftHandTarget,data.rightHandTarget,data.hipTarget,data.leftFootTarget,data.rightFootTarget };
            // Validate everything before changing any offsets/scales.
            var targets = new Transform[6];
            for (int i = 0; i < Names.Length; i++)
            {
                targets[i] = Find(role.visualRigRoot,Names[i]);
                if (targets[i] == null || poses[i] == null) return false;
            }
            if (profile.HasScale)
            {
                role.visualRigRoot.localScale = profile.VisualScale;
                if (role.avatarRoot != null) role.avatarRoot.localScale = profile.AvatarScale;
            }
            for (int i = 0; i < targets.Length; i++)
                targets[i].SetLocalPositionAndRotation(poses[i].LocalPosition,poses[i].LocalRotation);
            if (profile.HipReferenceHeightM > 0f)
                role.hipSolver?.ConfigureNeutralHeight(profile.HipReferenceHeightM);
            role.rigFollower?.SetCalibrated(true);
            role.activeCalibrationProfile = profile;
            return true;
        }

        public static RoleCalibrationProfile ForRoleHeight(RoleCalibrationProfile profile, int roleHeightCm)
        {
            var copy = Copy(profile);
            if (copy != null && copy.HasScale && copy.RoleHeightCm > 0 && roleHeightCm > 0)
            {
                float ratio = (float)roleHeightCm / copy.RoleHeightCm;
                copy.VisualScale *= ratio;
                copy.AvatarScale *= ratio;
                copy.RoleHeightCm = roleHeightCm;
            }
            return copy;
        }

        public static RoleCalibrationProfile Copy(RoleCalibrationProfile profile)
        {
            // Profiles are value snapshots; never alias source-session metadata.
            return profile == null ? null : JsonUtility.FromJson<RoleCalibrationProfile>(JsonUtility.ToJson(profile));
        }

        public static Transform Find(Transform root,string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
