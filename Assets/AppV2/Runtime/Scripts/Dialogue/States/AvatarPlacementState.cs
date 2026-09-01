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
            Debug.Log("[AvatarPlacementState] Enter");

            _roleCount = _flow.Stage.roleCount;
            selectableNext = _flow.Stage.selectableNext;
            _allRolesIndices = new List<int>();
            _currentRoleIndexForPlacement = 0;
            _stageRoot = _flow.Stage._stageRoot;

            for (int i = 0; i < _flow.Stage.roleCount; i++){
                _allRolesIndices.Add(i);
                PlaceCurrentRoleAndAdvance();
                
                //UnityEngine.Debug.Log($"_allRolesIndices count is: {_allRolesIndices.Count}");

            }
            ////hier true, weil bei VisualRig und AvatarRig sollen auch die Roots Kopiert werden im AvatarPlacement State. 
            _flow.Stage.RigUpdatePipeline(_allRolesIndices, true);

            
            
            
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

/*
       private void PlaceCurrentRoleAndAdvance()
        {
            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
                return;
            }

            //Vector3 placement = GetTestPlacementPosition(_currentRoleIndexForPlacement);
            int activeRoleCount =0;

            foreach (RoleRig role in _flow.Stage.roles)
            {
                if (!role.hasPreRecordedTakes)
                {
                    activeRoleCount++;
                }
            }
            Vector3 placement = RolePlacementUtility.GetCirclePlacementPosition(_currentRoleIndexForPlacement, activeRoleCount);

            //y wird hier dem Terrain angeglichen.
            placement.y = _flow.Stage.GetGroundYStageLocal(placement);

            //hier nochmal flach machen, damit die Rotation nicht schief wird
            Vector3 flatPlacement = placement;
            flatPlacement.y = 0f;

            Quaternion rotation = RolePlacementUtility.GetCirclePlacementRotation(flatPlacement);

            
            _flow.Stage.AvatarCalibration.PlaceRoleAt(
                _currentRoleIndexForPlacement,
                placement,
                rotation,
                _flow.Stage._stageRoot
            );

            _currentRoleIndexForPlacement++;

            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
                return;
            }

        }
        */

        private void PlaceCurrentRoleAndAdvance()
        {
            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
                return;
            }

            RoleRig role = _flow.Stage.roles[_currentRoleIndexForPlacement];

            int activeRoleCount = 0;
            int activePlacementIndex = 0;

            for (int i = 0; i < _flow.Stage.roles.Count; i++)
            {
                RoleRig r = _flow.Stage.roles[i];

                if (r.hasPreRecordedTakes)
                    continue;

                if (i < _currentRoleIndexForPlacement)
                    activePlacementIndex++;

                activeRoleCount++;
            }

            if (role.hasPreRecordedTakes)
            {
                PlacePreRecordedRole(_currentRoleIndexForPlacement);
            }
            else
            {
                Vector3 placement;
                Quaternion rotation;

                if (activeRoleCount == 1)
                {
                    placement = Vector3.zero;
                    rotation = Quaternion.identity;
                }
                else
                {
                    placement = RolePlacementUtility.GetCirclePlacementPosition(
                        activePlacementIndex,
                        activeRoleCount
                    );

                    Vector3 flatPlacement = placement;
                    flatPlacement.y = 0f;

                    rotation =
                        RolePlacementUtility.GetCirclePlacementRotation(flatPlacement);
                }

                placement.y =
                    _flow.Stage.GetGroundYStageLocal(placement);

                _flow.Stage.AvatarCalibration.PlaceRoleAt(
                    _currentRoleIndexForPlacement,
                    placement,
                    rotation,
                    _flow.Stage._stageRoot
                );
            }

            // Immer weitergehen, egal ob normal oder prerecorded
            _currentRoleIndexForPlacement++;

            if (_currentRoleIndexForPlacement >= _flow.Stage.roleCount)
            {
                GoToNextState();
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

            _flow.Stage.AvatarCalibration.PlaceRoleAt(
                roleIndex,
                placement,
                rotation,
                _stageRoot
            );

            Debug.Log(
                $"[PlacePreRecordedRole] " +
                $"role={role.roleId} | " +
                $"spawnId={role.roleSpawnId} | " +
                $"placement={placement} | " +
                $"hasStartPose={role.preRecordedStartRootPose != null}"
            );
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
            if (selectableNext)
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




    }
}