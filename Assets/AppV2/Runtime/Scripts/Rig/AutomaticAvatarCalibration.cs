using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Rig
{
    /// <summary>Builds the same local target offsets as manual calibration, using a synthetic
    /// tracking T-pose. No tracked pose (or T-pose of the participant) is sampled.</summary>
    public sealed class AutomaticAvatarCalibration
    {
        private static readonly HumanBodyBones[] Bones = {
            HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
        };
        private static readonly string[] MixamoNames = {
            "Head", "LeftHand", "RightHand", "Hips", "LeftFoot", "RightFoot"
        };
        private static readonly string[] Drivers = {
            "head", "leftHand", "rightHand", "hip", "leftFoot", "rightFoot"
        };

        public bool TryCalibrate(RoleRig role, float playerHeightCm, float hipHeight, float footSpacing, bool proceduralLowerBody)
        {
            if (role?.avatar == null || role.rigFollower == null ||
                role.visualRigRoot == null || role.avatarRoot == null || playerHeightCm <= 0f)
                return Fail(role, "Missing rig references or invalid player height.");

            var avatar = role.avatar;
            var animator = avatar.Animator;
            role.visualRigFollower?.SetVisualRigToPlayerPosition();
            role.rigFollower.SetAvatarToPlayerPosition();
            var positions = new Vector3[6];
            var drivers = new Transform[6];
            var handRotations = new Quaternion[2];
            var handSettings = role.avatarRoot.GetComponentInChildren<AvatarHandCalibrationSettings>(true);
            // Sample both bones and authored targets in the evaluated reference pose.
            // Mesh bind matrices may describe a different pose/orientation after Humanoid
            // retargeting (including reversed hand sides). Never mix the two spaces.
            var reference = SampleReferencePose(role);
            var targets = new Transform[6];
            for (int i = 0; i < Bones.Length; i++)
            {
                Transform bone = ResolveBone(role.avatarRoot, animator, Bones[i], MixamoNames[i]);
                drivers[i] = Find(role.visualRigRoot, Drivers[i]);
                targets[i] = Find(role.avatarRoot, Drivers[i] + "Target");
                if (bone == null || drivers[i] == null || targets[i] == null)
                    return Fail(role, "Missing bone or target: " + MixamoNames[i]);
                positions[i] = ReferencePosition(role.avatarRoot, reference, bone);
                if (i == 1 || i == 2)
                    handRotations[i - 1] = GetHandReferenceRotation(role.avatarRoot, animator, bone,
                        i == 1, handSettings, reference);
            }

            role.rigFollower.BuildMap();
            role.visualRigFollower?.SetVisualRigToPlayerPosition();
            role.rigFollower.SetAvatarToPlayerPosition();
            Quaternion neutralRotation = role.avatarRoot.rotation;
            // These are the same neutral lower-body drivers used by the standing input
            // fallback and procedural solvers. Their target offsets remain in Session JSON.
            if (proceduralLowerBody && role.hipSolver != null)
                role.hipSolver.ConfigureNeutralHeight(hipHeight);
            Vector3[] neutralLowerBody = {
                proceduralLowerBody && role.hipSolver != null ? role.hipSolver.NeutralLocalPosition : new Vector3(0f, hipHeight, 0f),
                new Vector3(-footSpacing, 0f, 0f),
                new Vector3(footSpacing, 0f, 0f)
            };
            for (int i = 0; i < drivers.Length; i++)
            {
                drivers[i].rotation = i == 1 || i == 2 ? handRotations[i - 1] : neutralRotation;
                if (i == 0)
                    drivers[i].localPosition = new Vector3(0f, playerHeightCm * 0.0092f, 0f);
                else if (i < 3)
                    drivers[i].position = positions[i];
                else
                    drivers[i].localPosition = neutralLowerBody[i - 3];
            }
            // Avatar targets retain their authored head/hand positional and rotational
            // offsets. Snap them into the synthetic drivers' reference frames.
            role.rigFollower.CalibrateTargetsFromAvatar();
            // Persist target poses captured in the very same evaluation as the bones,
            // including authored head/hand offsets. The sampler restores live transforms.
            for (int i = 0; i < targets.Length; i++)
            {
                var visualTarget = Find(role.visualRigRoot, Drivers[i] + "Target");
                var pose = reference[targets[i]];
                visualTarget.SetPositionAndRotation(role.avatarRoot.TransformPoint(pose.position),
                    role.avatarRoot.rotation * pose.rotation);
            }
            if (handSettings != null)
            {
                // Corrections belong to the persisted visual target offsets, never the
                // skeleton. Manual/imported calibration paths remain unchanged.
                Find(role.visualRigRoot, "leftHandTarget").localRotation *=
                    Quaternion.Euler(handSettings.leftTargetRotationCorrection);
                Find(role.visualRigRoot, "rightHandTarget").localRotation *=
                    Quaternion.Euler(handSettings.rightTargetRotationCorrection);
            }
            if (proceduralLowerBody && role.hipSolver != null)
                role.hipSolver.ResetToNeutralPose();
            return true;
        }

        private static Quaternion GetHandReferenceRotation(Transform root, Animator animator,
            Transform hand, bool left, AvatarHandCalibrationSettings settings,
            Dictionary<Transform, Pose> reference)
        {
            if (settings != null && settings.overrideReferenceRotations)
                return root.rotation * Quaternion.Euler(left
                    ? settings.leftReferenceRotation : settings.rightReferenceRotation);

            // Tracking/controller forward points along the fingers. Bone local axes are
            // importer-dependent, so infer the direction from the same evaluated pose as the targets.
            Transform finger = ResolveBone(root, animator, left
                ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal,
                left ? "LeftHandMiddle1" : "RightHandMiddle1");
            Vector3 wrist = ReferencePosition(root, reference, hand);
            Vector3 forward;
            if (finger != null)
                forward = ReferencePosition(root, reference, finger) - wrist;
            else
            {
                Transform forearm = ResolveBone(root, animator, left
                    ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    left ? "LeftForeArm" : "RightForeArm");
                forward = forearm != null ? wrist - ReferencePosition(root, reference, forearm)
                    : root.TransformDirection(left ? Vector3.left : Vector3.right);
            }
            if (forward.sqrMagnitude < 0.000001f)
                forward = root.TransformDirection(left ? Vector3.left : Vector3.right);
            forward.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(root.up, forward);
            if (up.sqrMagnitude < 0.000001f)
                up = Vector3.ProjectOnPlane(root.forward, forward);
            return Quaternion.LookRotation(forward, up.normalized);
        }

        private static Vector3 ReferencePosition(Transform root,
            Dictionary<Transform, Pose> reference, Transform bone)
            => root.TransformPoint(reference[bone].position);

        private static Dictionary<Transform, Pose> SampleReferencePose(RoleRig role)
        {
            var root = role.avatarRoot;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var original = new Pose[transforms.Length];
            var scales = new Vector3[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                original[i] = new Pose(transforms[i].localPosition, transforms[i].localRotation);
                scales[i] = transforms[i].localScale;
            }
            var rigs = root.GetComponentsInChildren<UnityEngine.Animations.Rigging.Rig>(true);
            var weights = new float[rigs.Length];
            for (int i = 0; i < rigs.Length; i++) weights[i] = rigs[i].weight;
            var builders = root.GetComponentsInChildren<RigBuilder>(true);
            var animator = role.avatar.Animator;
            var culling = animator != null ? animator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
            var result = new Dictionary<Transform, Pose>();
            try
            {
                foreach (var rig in rigs) rig.weight = 0f;
                foreach (var builder in builders) builder.SyncLayers();
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    role.avatar.PlayBasePose(AvatarBasePose.TPose);
                    animator.Update(0f);
                }
                foreach (var t in transforms)
                    result[t] = new Pose(root.InverseTransformPoint(t.position),
                        Quaternion.Inverse(root.rotation) * t.rotation);
            }
            finally
            {
                // No graph rebuild, Rebind, or permanent skeleton/target modifications.
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].SetLocalPositionAndRotation(original[i].position, original[i].rotation);
                    transforms[i].localScale = scales[i];
                }
                for (int i = 0; i < rigs.Length; i++) rigs[i].weight = weights[i];
                foreach (var builder in builders) builder.SyncLayers();
                if (animator != null) animator.cullingMode = culling;
            }
            return result;
        }

        private static Transform ResolveBone(Transform root, Animator animator, HumanBodyBones bone, string name)
        {
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                var mapped = animator.GetBoneTransform(bone);
                if (mapped != null) return mapped;
            }
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                string normalized = candidate.name;
                int colon = normalized.LastIndexOf(':');
                if (colon >= 0) normalized = normalized.Substring(colon + 1);
                if (normalized.StartsWith("mixamorig", System.StringComparison.OrdinalIgnoreCase))
                    normalized = normalized.Substring(9).TrimStart('_');
                if (string.Equals(normalized, name, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static bool Fail(RoleRig role, string reason)
        {
            Debug.LogError($"[AutomaticAvatarCalibration] Role {role?.roleId}: {reason}");
            return false;
        }
    }
}
