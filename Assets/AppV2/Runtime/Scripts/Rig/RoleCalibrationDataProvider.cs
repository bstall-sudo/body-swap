using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Rig
{
    public class RoleCalibrationDataProvider
    {
        private IReadOnlyList<RoleRig> _roles;

        private readonly List<string> _targetNames = new()
        {
            "headTarget",
            "leftHandTarget",
            "rightHandTarget",
            "hipTarget",
            "leftFootTarget",
            "rightFootTarget"
        };

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            _roles = roles;
        }

        public List<ConversationRoleMeta> CreateRoleMetas()
        {
            var result = new List<ConversationRoleMeta>();

            if (_roles == null) return result;

            for (int i = 0; i < _roles.Count; i++)
            {
                var role = _roles[i];

                result.Add(new ConversationRoleMeta
                {
                    RoleId = role.roleId,
                    RoleIndex = i,
                    RoleName = role.avatarName,
                    HeightOfRoleCm = role.heightOfRoleCm,
                    SittingIdle = role.sittingIdle,
                    Calibration = CaptureCalibration(role)
                });
            }

            return result;
        }

        private RoleCalibrationData CaptureCalibration(RoleRig role)
        {
            ValidateTargetNames(role);
            Transform visualRoot = role.visualRigRoot;

            return new RoleCalibrationData
            {
                headTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "headTarget")),
                leftHandTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "leftHandTarget")),
                rightHandTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "rightHandTarget")),
                hipTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "hipTarget")),
                leftFootTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "leftFootTarget")),
                rightFootTarget = CaptureLocalTransform(FindDeepChildByName(visualRoot, "rightFootTarget"))
            };
        }

        private TransformData CaptureLocalTransform(Transform t)
        {
            if (t == null) return null;

            return new TransformData
            {
                LocalPosition = t.localPosition,
                LocalRotation = t.localRotation,
                //LocalScale = t.localScale
            };
        }

        private Transform FindDeepChildByName(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChildByName(root.GetChild(i), targetName);

                if (found != null)
                    return found;
            }

            return null;
        }

        private void ValidateTargetNames(RoleRig role)
        {
            if (role.rigFollower == null)
            {
                UnityEngine.Debug.LogWarning($"[RoleCalibrationDataProvider] Role '{role.roleId}' has no rigFollower.");
                return;
            }

            var followerNames = role.rigFollower.ikTargetNames;

            if (followerNames == null)
            {
                UnityEngine.Debug.LogWarning($"[RoleCalibrationDataProvider] Role '{role.roleId}' rigFollower.ikTargetNames is null.");
                return;
            }

            if (followerNames.Count != _targetNames.Count)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RoleCalibrationDataProvider] Target count mismatch in role '{role.roleId}'. " +
                    $"Provider has {_targetNames.Count}, rigFollower has {followerNames.Count}."
                );
                return;
            }

            foreach (var name in _targetNames)
            {
                var t = FindDeepChildByName(role.visualRigRoot, name);

                if (t == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RoleCalibrationDataProvider] Target '{name}' not found in VisualRig of role '{role.roleId}'."
                    );
                }
            }
        }
    }
}