using System.IO;
using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class PreRecordedTakeImporter
    {
        private readonly SessionStore _targetStore;
        private readonly SessionModel _targetSession;
        private readonly SessionTakeIndex _targetTakeIndex;

        public PreRecordedTakeImporter(
            SessionStore targetStore,
            SessionModel targetSession,
            SessionTakeIndex targetTakeIndex)
        {
            _targetStore = targetStore;
            _targetSession = targetSession;
            _targetTakeIndex = targetTakeIndex;
        }

        public bool ImportTake(
            RoleRig targetRole,
            int targetRoleIndex,
            int targetSceneCount,
            TakeMeta sourceTakeMeta,
            RolePlaybackSource source,
            Transform stageRoot,
            Transform roleSpawn)
        {
            if (targetRole == null)
            {
                Debug.LogError("[PreRecordedTakeImporter] targetRole is null.");
                return false;
            }

            if (sourceTakeMeta == null)
            {
                Debug.LogError("[PreRecordedTakeImporter] sourceTakeMeta is null.");
                return false;
            }

            if (source == null || source.store == null)
            {
                Debug.LogError("[PreRecordedTakeImporter] source/store is null.");
                return false;
            }

            if (stageRoot == null)
            {
                Debug.LogError("[PreRecordedTakeImporter] stageRoot is null.");
                return false;
            }

            if (roleSpawn == null)
            {
                Debug.LogError(
                    $"[PreRecordedTakeImporter] roleSpawn is null for role {targetRole.roleId}."
                );
                return false;
            }


            // ------------------------------------------------------------
            // Prüfen, ob für Role + Scene schon ein Take existiert
            // ------------------------------------------------------------

            if (_targetTakeIndex.HasTakeForScene(
                    targetRoleIndex,
                    targetSceneCount))
            {
                Debug.Log(
                    $"[PreRecordedTakeImporter] Take already exists. " +
                    $"role={targetRoleIndex}, scene={targetSceneCount}"
                );

                return false;
            }


            // ------------------------------------------------------------
            // Neue Namen für aktuelle Session erzeugen
            // ------------------------------------------------------------

            string targetTakeId =
                $"take_{targetSceneCount:0000}_{targetRole.roleId}";

            string targetFramesName =
                _targetStore.FramesFileName(targetTakeId);

            string targetAudioName =
                _targetStore.AudioFileName(targetTakeId);


            // ------------------------------------------------------------
            // Source-Pfade
            // ------------------------------------------------------------

            string sourceFolder =
                source.store.GetSessionFolder(source.sessionId);

            string sourceFramesPath =
                Path.Combine(
                    sourceFolder,
                    sourceTakeMeta.FramesFile
                );

            string sourceAudioPath = null;

            if (!string.IsNullOrWhiteSpace(sourceTakeMeta.AudioFile))
            {
                sourceAudioPath =
                    Path.Combine(
                        sourceFolder,
                        sourceTakeMeta.AudioFile
                    );
            }


            // ------------------------------------------------------------
            // Target-Pfade
            // ------------------------------------------------------------

            string targetFolder =
                _targetStore.GetSessionFolder(
                    _targetSession.SessionId
                );

            Directory.CreateDirectory(targetFolder);

            string targetFramesPath =
                Path.Combine(
                    targetFolder,
                    targetFramesName
                );

            string targetAudioPath =
                Path.Combine(
                    targetFolder,
                    targetAudioName
                );


            // ------------------------------------------------------------
            // Frames prüfen und laden
            // ------------------------------------------------------------

            if (!File.Exists(sourceFramesPath))
            {
                Debug.LogError(
                    $"[PreRecordedTakeImporter] Source frames not found: " +
                    $"{sourceFramesPath}"
                );

                return false;
            }

            List<Frame> frames =
                JsonlFrames.ReadAll(sourceFramesPath);

            if (frames == null || frames.Count == 0)
            {
                Debug.LogError(
                    $"[PreRecordedTakeImporter] No frames found in: " +
                    $"{sourceFramesPath}"
                );

                return false;
            }


            // ------------------------------------------------------------
            // Body-Daten umrechnen
            //
            // ALT:
            // Frame.Body.Pos wird so interpretiert,
            // als läge der Ursprung beim RoleSpawnPoint.
            //
            // NEU:
            // Frame.Body.Pos liegt relativ zur aktuellen StageRoot.
            //
            // Head / Hands / Hip / Feet bleiben unverändert,
            // da diese lokal zum ActorRoot gespeichert sind.
            // ------------------------------------------------------------

            for (int i = 0; i < frames.Count; i++)
            {
                Frame frame = frames[i];


                // ---------- POSITION ----------

                Vector3 sourceBodyPos =
                    frame.Body.Pos;

                // Position relativ zum RoleSpawn -> World
                Vector3 worldPos =
                    roleSpawn.TransformPoint(sourceBodyPos);

                // World -> aktuelle StageRoot local
                Vector3 targetStageLocalPos =
                    stageRoot.InverseTransformPoint(worldPos);


                // ---------- ROTATION ----------

                Quaternion sourceBodyRot =
                    Quaternion.Euler(
                        0f,
                        frame.Body.YawDeg,
                        0f
                    );

                // Rotation relativ zum RoleSpawn -> World
                Quaternion worldRot =
                    roleSpawn.rotation *
                    sourceBodyRot;

                // World -> aktuelle StageRoot local
                Quaternion targetStageLocalRot =
                    Quaternion.Inverse(stageRoot.rotation) *
                    worldRot;


                // --------------------------------------------------------
                // Body separat herausnehmen.
                // Das funktioniert auch, wenn Body ein struct ist.
                // --------------------------------------------------------

                var body = frame.Body;

                body.Pos = targetStageLocalPos;
                body.YawDeg = targetStageLocalRot.eulerAngles.y;

                frame.Body = body;


                // Falls Frame ebenfalls struct ist:
                // geänderten Frame wieder in Liste schreiben.
                frames[i] = frame;
            }


            // ------------------------------------------------------------
            // Transformierte Frames speichern
            // ------------------------------------------------------------

            JsonlFrames.WriteAll(
                targetFramesPath,
                frames
            );


            // ------------------------------------------------------------
            // Audio 1:1 kopieren
            // ------------------------------------------------------------

            string storedAudioName = null;

            if (!string.IsNullOrWhiteSpace(sourceAudioPath))
            {
                if (File.Exists(sourceAudioPath))
                {
                    File.Copy(
                        sourceAudioPath,
                        targetAudioPath,
                        overwrite: true
                    );

                    storedAudioName = targetAudioName;
                }
                else
                {
                    Debug.LogWarning(
                        $"[PreRecordedTakeImporter] Source audio not found: " +
                        $"{sourceAudioPath}"
                    );
                }
            }


            // ------------------------------------------------------------
            // Neues TakeMeta für AKTUELLE Session
            // ------------------------------------------------------------

            TakeMeta importedMeta = new TakeMeta
            {
                TakeId = targetTakeId,

                RoleId = targetRole.roleId,
                RoleIndex = targetRoleIndex,

                SceneCount = targetSceneCount,
                DurationSec = sourceTakeMeta.DurationSec,

                usesPreRecordedCalibration = true,

                sourceRoleId = targetRole.sourceRoleId,
                npcGroupId = targetRole.npcGroupId,

                FramesFile = targetFramesName,
                AudioFile = storedAudioName
            };


            // ------------------------------------------------------------
            // Aktuelle Session aktualisieren
            // ------------------------------------------------------------

            _targetSession.Takes.Add(importedMeta);

            _targetTakeIndex.StoreTakeMeta(importedMeta);

            // ------------------------------------------------------------
            // StartRootPose der importierten Rolle ebenfalls
            // ins Koordinatensystem der aktuellen Stage transformieren
            // ------------------------------------------------------------

            ConversationRoleMeta sourceRoleMeta =
                source.session?.Roles?.Find(
                    r => r.RoleIndex == source.sourceRoleIndex
                );

            ConversationRoleMeta targetRoleMeta =
                _targetSession.Roles?.Find(
                    r => r.RoleIndex == targetRoleIndex
                );

            if (sourceRoleMeta?.StartRootPose != null &&
                targetRoleMeta != null)
            {
                TransformData transformedStartPose =
                    TransformStartPoseToCurrentStage(
                        sourceRoleMeta.StartRootPose,
                        stageRoot,
                        roleSpawn
                    );

                if (transformedStartPose != null)
                {
                    targetRoleMeta.StartRootPose =
                        transformedStartPose;

                    Debug.Log(
                        $"[PreRecordedTakeImporter] Updated StartRootPose " +
                        $"for role={targetRole.roleId}, " +
                        $"sourcePos={sourceRoleMeta.StartRootPose.LocalPosition}, " +
                        $"targetPos={transformedStartPose.LocalPosition}"
                    );
                }
            }

            _targetStore.SaveSessionModel(_targetSession);


            Debug.Log(
                $"[PreRecordedTakeImporter] Imported and transformed take: " +
                $"{sourceTakeMeta.TakeId} -> {targetTakeId}, " +
                $"role={targetRole.roleId}, " +
                $"targetScene={targetSceneCount}"
            );

            return true;
        }

        private TransformData TransformStartPoseToCurrentStage(
            TransformData sourcePose,
            Transform stageRoot,
            Transform roleSpawn)
        {
            if (sourcePose == null ||
                stageRoot == null ||
                roleSpawn == null)
            {
                return null;
            }

            Vector3 worldPos =
                roleSpawn.TransformPoint(sourcePose.LocalPosition);

            Quaternion worldRot =
                roleSpawn.rotation * sourcePose.LocalRotation;

            return new TransformData
            {
                LocalPosition =
                    stageRoot.InverseTransformPoint(worldPos),

                LocalRotation =
                    Quaternion.Inverse(stageRoot.rotation) * worldRot
            };
        }
    }
}