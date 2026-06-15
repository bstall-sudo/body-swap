using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;


namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarCalibrationController : MonoBehaviour
    {
        private IReadOnlyList<RoleRig> roles;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            this.roles = roles;
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
                PlaceAvatarAtUserPosition(i, playerStagePose);
            }
        }

        public void PlaceAvatarAtUserPosition(int roleIndex, StagePose playerStagePose)
        {
            RoleRig role = roles[roleIndex];

            if (role.root == null)
            {
                UnityEngine.Debug.LogWarning($"Role {roleIndex} has no root its RoleRig.");
                return;
            }

            Debug.Log(
                $"[PlaceRoleAt AFTER ROOT] role={role.roleId}, " +
                $"root.local={role.roleRoot.localPosition}, root.world={role.roleRoot.position}, " +
                $"tech.local={role.root.localPosition}, tech.world={role.root.position}"
            );
            

            role.root.localPosition = playerStagePose.Position;
            role.root.localRotation = playerStagePose.Rotation;

            Debug.Log(
                $"[PlaceRoleAt AFTER ROOT] role={role.roleId}, " +
                $"root.local={role.roleRoot.localPosition}, root.world={role.roleRoot.position}, " +
                $"tech.local={role.root.localPosition}, tech.world={role.root.position}"
            );
            /*
            role.visualRigRoot.localPosition = playerStagePose.Position;
            role.visualRigRoot.localRotation = playerStagePose.Rotation;

            role.avatarRoot.localPosition = playerStagePose.Position;
            role.avatarRoot.localRotation = playerStagePose.Rotation;
            */
            // das wird jetzt durch die Zeilen oben erfüllt
            roles[roleIndex].visualRigFollower?.SetVisualRigToPlayerPosition();
            roles[roleIndex].rigFollower?.SetAvatarToPlayerPosition();
            
            UnityEngine.Debug.Log(
                $"Role({roleIndex}) placed at localPosition: {playerStagePose.Position}, " +
                $"localRotation: {playerStagePose.Rotation.eulerAngles}."
            );
            
        }

        

        public void CalibrateRole(int roleIndex)
        {
            if (!IsValidIndex(roleIndex)) return;

            var avatar = roles[roleIndex].avatar;

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
            avatar.RigFollower.CalibrateTargetsFromAvatar();
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


        public void PlaceRoleAt(int roleIndex, Vector3 localFloorPosition, Quaternion localRotation)
        {
            if (!IsValidIndex(roleIndex)) return;

            RoleRig role = roles[roleIndex];

            if (role == null || role.root == null)
            {
                Debug.LogWarning($"Role or role.root missing for role {roleIndex}.");
                return;
            }

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

            PlaceAvatarAtUserPosition(roleIndex, stagePose);
        }


    }
}
