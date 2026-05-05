using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Rig
{
    public class RoleCalibrationDataApplier
    {

        private IReadOnlyList<RoleRig> _roles;
        
        
        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            _roles = roles;
        }
     

        public void ApplyRoleMetasToScene(List<RoleRig> roles, SessionModel session)
        {
            if (roles == null || session == null || session.Roles == null) return;

            for (int i = 0; i < session.Roles.Count; i++)
            {
                var meta = session.Roles[i];

                if (meta.RoleIndex < 0 || meta.RoleIndex >= roles.Count)
                    continue;

                RoleRig role = roles[meta.RoleIndex];

                ApplyCalibration(role, meta.Calibration);

                if(role.avatarName != meta.RoleName)
                {
                    UnityEngine.Debug.LogWarning(
                    $"[RoleCalibrationDataApplier] Role Name mismatch between Scene and loaded SessionModel. Scene: role.avatarName '{role.avatarName}'. " +
                    $"loaded Session: role.avatarName '{meta.RoleName}'."
                );

                }
                
                role.heightOfRoleCm = meta.HeightOfRoleCm;
                role.sittingIdle = meta.SittingIdle;

                role.rigFollower?.BuildMapAndEnableFollow();
            }
        }

        private void ApplyCalibration(RoleRig role, RoleCalibrationData calibration)
        {
            if (role == null || role.visualRigRoot == null || calibration == null)
                return;

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "headTarget"),
                calibration.headTarget
            );

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "leftHandTarget"),
                calibration.leftHandTarget
            );

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "rightHandTarget"),
                calibration.rightHandTarget
            );

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "hipTarget"),
                calibration.hipTarget
            );

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "leftFootTarget"),
                calibration.leftFootTarget
            );

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "rightFootTarget"),
                calibration.rightFootTarget
            );
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
        private void ApplyLocalTransform(Transform target, TransformData source)
        {
            if (target == null || source == null){
                UnityEngine.Debug.LogWarning(
                    "[RoleCalibrationDataApplier] Target or Source is null.");
                
            };

    
            target.localPosition = source.LocalPosition;
            target.localRotation = source.LocalRotation;
                
        }
    }
}

