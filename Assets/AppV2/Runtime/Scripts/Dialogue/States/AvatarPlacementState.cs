using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Dialogue.Services;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class AvatarPlacementState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForPlacement;
        private bool selectableNext;

        public Transform _stageRoot;
        private int _roleCount;
        private List<int> _allRolesIndices;


        private Vector3 testVectorPlacement = new Vector3(0f, 0f, 1f);
        private Vector3 lookAt = Vector3.zero;


        public DialogueMode Mode => DialogueMode.AvatarPlacement;

        public AvatarPlacementState(FlowController flow)
        {
            _flow = flow;
            _currentRoleIndexForPlacement = 0;
        }

        public void Enter()
        {
            _roleCount = _flow.Stage.roleCount;
            selectableNext = _flow.Stage.selectableNext;

            _allRolesIndices = new List<int>();
            _currentRoleIndexForPlacement = 0;
            _stageRoot = _flow.Stage._stageRoot;

            var poses = RolePlacementUtility.BuildStartPoses(_flow.Stage.roles, _stageRoot,
                _flow.Stage.StageSpawnId, _flow.Stage.environmentLoader.FindSpawnTransform,
                _flow.Stage.GetGroundYStageLocal);
            for (int i = 0; i < _roleCount; i++)
            {
                _allRolesIndices.Add(i);
                if (_flow.Stage.roles[i].hasPreRecordedTakes) PlacePreRecordedRole(i);
                else if (poses.TryGetValue(i, out var pose))
                    _flow.Stage.AvatarCalibration.PlaceRoleAt(i, pose.Position, pose.Rotation, _stageRoot);
            }

            _flow.Stage.RigUpdatePipeline(
                _allRolesIndices,
                true
            );

            // Alle Figuren haben jetzt ihre endgültige Startposition.
            _flow.Stage._recordingController.UpdateSessionStartRootPoses(
                _flow.Stage.roles,
                _stageRoot
            );

            GoToNextState();
        }

        public void Tick(float dt)
        
        {

            if (_flow.ConsumePrimaryAction())
            {

                

            }

            if (_flow.ConsumeSecondaryAction())
            {
 
              
            }
        }



        public void PlacePreRecordedRole(int roleIndex)
        {
            if (roleIndex < 0 || roleIndex >= _flow.Stage.roles.Count)
            {
                Debug.LogWarning(
                    $"[PlacePreRecordedRole] Invalid roleIndex: {roleIndex}"
                );
                return;
            }

            RoleRig role = _flow.Stage.roles[roleIndex];

            if (role == null || !role.hasPreRecordedTakes)
                return;

            if (_stageRoot == null)
            {
                Debug.LogError("[PlacePreRecordedRole] StageRoot is null.");
                return;
            }

            // Spawnpunkt der NPC-Gruppe im Environment holen
            Transform npcGroupSpawn =
                _flow.Stage.environmentLoader.GetTransformFromSpawnId(role.roleSpawnId);

            if (npcGroupSpawn == null)
            {
                Debug.LogWarning(
                    $"[PlacePreRecordedRole] No spawn point found for " +
                    $"role={role.roleId}, spawnId={role.roleSpawnId}"
                );

                // Letzter Fallback:
                // aktuelle lokale Pose der Rolle beibehalten.
                return;
            }

            Vector3 placement;
            Quaternion rotation;

            // ----------------------------------------------------
            // 1. Bevorzugt:
            // ursprüngliche StartRootPose relativ zum NPC-Spawn
            // ----------------------------------------------------
            if (role.preRecordedStartRootPose != null)
            {
                TransformData sourcePose = role.preRecordedStartRootPose;

                // StartRootPose stammt aus dem lokalen Raum
                // der ursprünglichen ConversationStage.
                //
                // Wir behandeln den neuen NPC-SpawnPoint jetzt als
                // neuen Ursprung dieser ursprünglichen Stage.
                Vector3 worldPos =
                    npcGroupSpawn.TransformPoint(sourcePose.LocalPosition);

                Quaternion worldRot =
                    npcGroupSpawn.rotation * sourcePose.LocalRotation;

                // In den lokalen Raum unserer aktuellen Stage umrechnen,
                // da Role.root unter StageRoot hängt.
                placement =
                    _stageRoot.InverseTransformPoint(worldPos);

                rotation =
                    Quaternion.Inverse(_stageRoot.rotation) * worldRot;
            }
            else
            {
                // ----------------------------------------------------
                // 2. Fallback:
                // keine StartRootPose vorhanden.
                //
                // Rolle direkt auf den NPC-Group-SpawnPoint setzen.
                // ----------------------------------------------------

                placement =
                    _stageRoot.InverseTransformPoint(npcGroupSpawn.position);

                rotation =
                    Quaternion.Inverse(_stageRoot.rotation) *
                    npcGroupSpawn.rotation;

                Debug.LogWarning(
                    $"[PlacePreRecordedRole] " +
                    $"role={role.roleId} has no preRecordedStartRootPose. " +
                    $"Using NpcGroupSpawnPoint directly."
                );
            }

            // ----------------------------------------------------
            // 3. Bodenhöhe des AKTUELLEN Environments verwenden
            // ----------------------------------------------------

            placement.y = _flow.Stage.GetGroundYStageLocal(placement);

            // ----------------------------------------------------
            // 4. Rolle platzieren
            // ----------------------------------------------------

            /*Debug.Log(
                $"[NPC PLACEMENT BEFORE] " +
                $"role={role.roleId}, " +
                $"spawnId={role.roleSpawnId}, " +
                $"npcSpawnWorld={npcGroupSpawn.position}, " +
                $"startPose={role.preRecordedStartRootPose?.LocalPosition}, " +
                $"calculatedPlacement={placement}, " +
                $"rootBeforeLocal={role.root.localPosition}, " +
                $"rootBeforeWorld={role.root.position}"
            );*/
            // Placement is already resolved in stage space. PlaceRoleAt would resolve
            // an imported NPC a second time and discard this ground-adjusted pose.
            role.root.SetLocalPositionAndRotation(placement, rotation);
            role.initialStartPos = placement;
            role.initialStartYawDeg = rotation.eulerAngles.y;
            role.hasInitialStartPose = true;

            /*Debug.Log(
                $"[NPC PLACEMENT AFTER] " +
                $"role={role.roleId}, " +
                $"rootAfterLocal={role.root.localPosition}, " +
                $"rootAfterWorld={role.root.position}, " +
                $"roleRootLocal={role.roleRoot.localPosition}, " +
                $"roleRootWorld={role.roleRoot.position}"
            );*/

            /*Debug.Log(
                $"[PlacePreRecordedRole] " +
                $"role={role.roleId} | " +
                $"spawnId={role.roleSpawnId} | " +
                $"placement={placement} | " +
                $"hasStartPose={role.preRecordedStartRootPose != null}"
            );*/
        }

        private Vector3 GetTestPlacementPosition(int roleIndex)
        {
            float radius = 1.5f;
            float angle = roleIndex * Mathf.PI * 2f / Mathf.Max(1, _flow.Stage.roleCount);

            return new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
        }

        private void GoToNextState()
        {
            _flow._data.RefreshMembership();
            if (selectableNext || _flow._data.ActiveRoleCount == 0)
            {
                _flow.SetState(new ChooseSpeakerState(_flow));
            }
            else
            {
                _flow.SetState(new PlayerAlignState(_flow));
            }
        }

        

        public void Exit()
        {
            //hier true, weil bei VisualRig und AvatarRig sollen auch die Roots Kopiert werden im AvatarPlacement State. 
            _flow.Stage.RigUpdatePipeline(_allRolesIndices, true);
            _flow.Stage.AvatarCalibration.ShowAllRoles();
            _flow.Stage.AvatarCalibration.ShowAllRoles();

        }

        private void LogNpcRigPositions()
        {
            for (int i = 0; i < _flow.Stage.roles.Count; i++)
            {
                RoleRig role = _flow.Stage.roles[i];

                if (!role.hasPreRecordedTakes)
                    continue;

                Debug.Log(
                    $"[NPC RIG POS] NPC placement-Debug   role={role.roleId}\n" +
                    $"roleRoot.world={role.roleRoot?.position}\n" +
                    $"technicalRoot.world={role.root?.position}\n" +
                    $"visualRigRoot.world={role.visualRigRoot?.position}\n" +
                    $"avatarRoot.world={role.avatarRoot?.position}"
                );
            }
        }




    }
}