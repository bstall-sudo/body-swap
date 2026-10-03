using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
//using System.Numerics;

namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarCalibrationController : MonoBehaviour
    {
        private IReadOnlyList<RoleRig> roles;

        private bool _usePreRecordedCalibrationData;

        [SerializeField] public EnvironmentLoader environmentLoader;

        public void Initialize(IReadOnlyList<RoleRig> roles, bool usePreRecordedCalibration)
        {
            this.roles = roles;
            this._usePreRecordedCalibrationData = usePreRecordedCalibration;
        }

        public int RoleCount => roles?.Count ?? 0;

        public void SetOnlyRoleVisible(int visibleIndex)
        {
            if (roles == null) return;

            for (int i = 0; i < roles.Count; i++)
            {
                bool visible = i == visibleIndex;
                roles[i].avatar?.SetVisible(visible);
            }
        }

        public void SetAvatarHeadVisible(int roleIndex, bool visible){
            if (!IsValidIndex(roleIndex)) return;

            var avatar = roles[roleIndex].avatar;


            if (avatar == null)
            {
                Debug.LogWarning($"No AvatarRigDefinition assigned for role {roleIndex}.");
                return;
            }

            avatar.SetHeadVisible(visible);


        }

        public void SetAllAvatarHeadsVisible(bool visible){

            for (int i = 0; i < roles.Count; i++)
            {
                SetAvatarHeadVisible(i, visible);
            }


        }

        public void PlaceAvatarsAtUserPosition(StagePose playerStagePose)
        {
            for (int i = 0; i < roles.Count; i++)
            {
                //if (roles[i].hasPreRecordedTakes) continue;
                PlaceAvatarAtUserPosition(i, playerStagePose);
            }
        }

        public void PlaceAvatarAtUserPosition(int roleIndex, StagePose playerStagePose)
        {
            RoleRig role = roles[roleIndex];

            if (role.root == null)
            {
                UnityEngine.Debug.LogWarning($"Role {roleIndex} has no root for its RoleRig.");
                return;
            }

            /*
            Debug.Log(
                $"[PlaceRoleAt AFTER ROOT] role={role.roleId}, " +
                $"root.local={role.roleRoot.localPosition}, root.world={role.roleRoot.position}, " +
                $"tech.local={role.root.localPosition}, tech.world={role.root.position}"
            );
            */

            role.root.localPosition = playerStagePose.Position;
            role.root.localRotation = playerStagePose.Rotation;
            /*
            Debug.Log(
                $"[PlaceRoleAt AFTER ROOT] role={role.roleId}, " +
                $"root.local={role.roleRoot.localPosition}, root.world={role.roleRoot.position}, " +
                $"tech.local={role.root.localPosition}, tech.world={role.root.position}"
            );
            */
            /*
            role.visualRigRoot.localPosition = playerStagePose.Position;
            role.visualRigRoot.localRotation = playerStagePose.Rotation;

            role.avatarRoot.localPosition = playerStagePose.Position;
            role.avatarRoot.localRotation = playerStagePose.Rotation;
            */
            // das wird jetzt durch die Zeilen oben erfüllt
            roles[roleIndex].visualRigFollower?.SetVisualRigToPlayerPosition();
            roles[roleIndex].rigFollower?.SetAvatarToPlayerPosition();
            /*
            UnityEngine.Debug.Log(
                $"Role({roleIndex}) placed at localPosition: {playerStagePose.Position}, " +
                $"localRotation: {playerStagePose.Rotation.eulerAngles}."
            );
            */
        }

        

        public void CalibrateRole(int roleIndex)
        {
            if (!IsValidIndex(roleIndex)) return;

            var avatar = roles[roleIndex].avatar;

            var role = roles[roleIndex];

            if (avatar == null)
            {
                Debug.LogWarning($"No avatar assigned for role {roleIndex}.");
                return;
            }

            if (avatar.RigFollower == null)
            {
                Debug.LogWarning($"No AvatarRigFollower assigned for role {roleIndex}.");
                return;
            }

            avatar.RigFollower.BuildMap();
            if (_usePreRecordedCalibrationData &&
                role.hasPreRecordedTakes &&
                role.preRecordedCalibration != null)
            {
                ApplyPreRecordedCalibration(role);
            }
            else
            {
                role.rigFollower.CalibrateTargetsFromAvatar();
            }
            //Debug.Log($"CalibrateRole({roleIndex}) was called.");
        }

        public void ShowAllRoles()
        {
            for (int i = 0; i < roles.Count; i++)
            {
                roles[i].avatar?.SetVisible(true);
            }
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < roles.Count;
        }


        public void PlaceRoleAt(int roleIndex, Vector3 localFloorPosition, Quaternion localRotation, Transform stageRoot)
        {
            if (!IsValidIndex(roleIndex)) return;

            RoleRig role = roles[roleIndex];

            if (role == null || role.root == null)
            {
                Debug.LogWarning($"Role or role.root missing for role {roleIndex}.");
                return;
            }

            if (!role.hasPreRecordedTakes)
            {
                Vector3 localPos = localFloorPosition;
                //das deaktivieren, weil das überschreibt die GroundHeight Funktion mit der der InitialStartPos aus dem RoleRig
                //localPos.y = role.root.localPosition.y;

                role.root.localPosition = localPos;
                role.root.localRotation = localRotation;

                role.initialStartPos = role.root.localPosition;
                role.initialStartYawDeg = role.root.localRotation.eulerAngles.y;
                role.hasInitialStartPose = true;

                StagePose stagePose = new StagePose
                {
                    Position = localPos,
                    Rotation = localRotation
                };

                /*
                Vector3 testValuePos = new Vector3();
                Quaternion tesValueRot = new Quaternion();

                StagePose testStagePose = new StagePose
                {
                    Position = testValuePos,
                    Rotation = tesValueRot
                }; */

                PlaceAvatarAtUserPosition(roleIndex, stagePose);

            }
            else
            {
                PlaceImportedNpcRoleAtSpawnPoint(roleIndex, stageRoot);   
            }
            
        }

        public void PlaceImportedNpcRoleAtSpawnPoint(int roleIndex, Transform stageRoot)
        {
            if (roleIndex < 0 || roleIndex >= roles.Count)
                return;

            RoleRig role = roles[roleIndex];

            if (role == null || !role.hasPreRecordedTakes)
                return;

            if (role.root == null)
            {
                Debug.LogWarning(
                    $"[PlaceImportedNpcRoleAtSpawnPoint] root missing: {role.roleId}"
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(role.roleSpawnId))
            {
                Debug.LogWarning(
                    $"[PlaceImportedNpcRoleAtSpawnPoint] roleSpawnId missing for {role.roleId}"
                );
                return;
            }

            StageSpawnPoint spawn =
                environmentLoader.GetSpawnPoint(role.roleSpawnId);

            if (spawn == null)
            {
                Debug.LogWarning(
                    $"[PlaceImportedNpcRoleAtSpawnPoint] Spawn not found: {role.roleSpawnId}"
                );
                return;
            }

            TransformData sourceStartPose = role.preRecordedStartRootPose;

            if (sourceStartPose != null)
            {
                TransformData transformedPose =
                    TransformStartPoseToCurrentStage(
                        sourceStartPose,
                        stageRoot,
                        spawn.transform
                    );

                role.root.localPosition = transformedPose.LocalPosition;
                role.root.localRotation = transformedPose.LocalRotation;
            }
            else
            {
                // Fallback: direkt auf SpawnPoint
                role.root.position = spawn.transform.position;
                role.root.rotation = spawn.transform.rotation;
            }


            // TechnicalRoot -> VisualRoot
            //role.visualRigFollower?.SetVisualRigToPlayerPosition();

            // VisualRoot -> AvatarRoot
            /*role.rigFollower?.SetAvatarToPlayerPosition();*/
            /*
            Debug.Log(
                $"[PlaceImportedNpcRoleAtSpawnPoint] {role.roleId} " +
                $"spawn={role.roleSpawnId}, local={role.root.localPosition}"
            );
            */
        }

        private TransformData TransformStartPoseToCurrentStage(
            TransformData sourcePose,
            Transform stageRoot,
            Transform roleSpawn)
        {
            if (sourcePose == null ||
                stageRoot == null ||
                roleSpawn == null)
                return null;

            Vector3 worldPos =
                roleSpawn.TransformPoint(
                    sourcePose.LocalPosition
                );

            Quaternion worldRot =
                roleSpawn.rotation *
                sourcePose.LocalRotation;

            return new TransformData
            {
                LocalPosition =
                    stageRoot.InverseTransformPoint(worldPos),

                LocalRotation =
                    Quaternion.Inverse(stageRoot.rotation) *
                    worldRot
            };
        }

        private void ApplyPreRecordedCalibration(RoleRig role)
        {
            if (role == null ||
                role.visualRigRoot == null ||
                role.preRecordedCalibration == null)
            {
                return;
            }

            RoleCalibrationData data = role.preRecordedCalibration;

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "headTarget"),
                data.headTarget);

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "leftHandTarget"),
                data.leftHandTarget);

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "rightHandTarget"),
                data.rightHandTarget);

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "hipTarget"),
                data.hipTarget);

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "leftFootTarget"),
                data.leftFootTarget);

            ApplyLocalTransform(
                FindDeepChildByName(role.visualRigRoot, "rightFootTarget"),
                data.rightFootTarget);

            
            role.rigFollower.SetCalibrated(true);
        }

        private void ApplyLocalTransform(
            Transform target,
            TransformData data)
        {
            if (target == null || data == null)
                return;

            target.localPosition = data.LocalPosition;
            target.localRotation = data.LocalRotation;
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

    }


}
