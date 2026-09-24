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
            Transform roleSpawn,
            Transform player)
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
                /*
                Debug.Log(
                    $"[PreRecordedTakeImporter] Take already exists. " +
                    $"role={targetRoleIndex}, scene={targetSceneCount}"
                );
                */

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
            ///der folgende Abschnitt importiert die Frames so, dass die Figur zum Player ausgerichtet ist
            // ------------------------------------------------------------
            // Optionales Alignment des NPCs zum Player
            // ------------------------------------------------------------

            Quaternion playerAlignmentRotation = Quaternion.identity;
            Vector3 alignmentPivotWorld = Vector3.zero;

            ConversationRoleMeta sourceRoleMeta =
                    source.session?.Roles?.Find(
                        r => r.RoleIndex == source.sourceRoleIndex
                    );


            if (targetRole.alignWithPlayer && player != null)
            {
                
                if (sourceRoleMeta?.StartRootPose != null)
                {
                    // Startposition des NPCs in der aktuellen Welt
                    alignmentPivotWorld =
                        roleSpawn.TransformPoint(
                            sourceRoleMeta.StartRootPose.LocalPosition
                        );

                    // Alignment nur EINMAL für diesen NPC bestimmen.
                    // Alle folgenden Takes verwenden dasselbe Offset.
                    if (!targetRole.hasPlayerAlignment)
                    {
                        Quaternion originalStartWorldRotation =
                            roleSpawn.rotation *
                            sourceRoleMeta.StartRootPose.LocalRotation;

                        Vector3 directionToPlayer =
                            player.position - alignmentPivotWorld;

                        directionToPlayer.y = 0f;

                        if (directionToPlayer.sqrMagnitude > 0.001f)
                        {
                            Quaternion desiredWorldRotation =
                                Quaternion.LookRotation(
                                    directionToPlayer.normalized,
                                    Vector3.up
                                );

                            targetRole.playerAlignmentYawOffset =
                                Mathf.DeltaAngle(
                                    originalStartWorldRotation.eulerAngles.y,
                                    desiredWorldRotation.eulerAngles.y
                                );

                            targetRole.hasPlayerAlignment = true;

                            Debug.Log(
                                $"[PreRecordedTakeImporter] Player alignment for " +
                                $"{targetRole.roleId}: " +
                                $"{targetRole.playerAlignmentYawOffset:F1}°"
                            );
                        }
                    }

                    if (targetRole.hasPlayerAlignment)
                    {
                        playerAlignmentRotation =
                            Quaternion.Euler(
                                0f,
                                targetRole.playerAlignmentYawOffset,
                                0f
                            );
                    }
                }
            }

            ///-Ende des Abschnitts, der die Frames importiert, sodass die Figur zum Player ausgerichtet ist

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
/*
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
*/
            for (int i = 0; i < frames.Count; i++)
            {
                Frame frame = frames[i];


                // --------------------------------------------------------
                // POSITION
                // --------------------------------------------------------

                Vector3 sourceBodyPos =
                    frame.Body.Pos;

                // Ursprüngliche Transformation:
                // RoleSpawn local -> World
                Vector3 worldPos =
                    roleSpawn.TransformPoint(sourceBodyPos);


                // --------------------------------------------------------
                // OPTIONAL: gesamten Take um NPC-Startpunkt drehen
                // --------------------------------------------------------

                if (targetRole.alignWithPlayer &&
                    targetRole.hasPlayerAlignment)
                {
                    Vector3 offsetFromNpcStart =
                        worldPos - alignmentPivotWorld;

                    worldPos =
                        alignmentPivotWorld +
                        playerAlignmentRotation * offsetFromNpcStart;

                    UnityEngine.Debug.Log($"[PreRecordedTakeImporter] (targetRole.alignWithPlayer && targetRole.hasPlayerAlignment");
                }


                // World -> aktuelle StageRoot local
                Vector3 targetStageLocalPos =
                    stageRoot.InverseTransformPoint(worldPos);


                // --------------------------------------------------------
                // ROTATION
                // --------------------------------------------------------

                Quaternion sourceBodyRot =
                    Quaternion.Euler(
                        0f,
                        frame.Body.YawDeg,
                        0f
                    );

                // RoleSpawn local -> World
                Quaternion worldRot =
                    roleSpawn.rotation *
                    sourceBodyRot;


                // --------------------------------------------------------
                // OPTIONAL: dieselbe Rotation auf Body anwenden
                // --------------------------------------------------------

                if (targetRole.alignWithPlayer &&
                    targetRole.hasPlayerAlignment)
                {
                    worldRot =
                        playerAlignmentRotation *
                        worldRot;
                }


                // World -> aktuelle StageRoot local
                Quaternion targetStageLocalRot =
                    Quaternion.Inverse(stageRoot.rotation) *
                    worldRot;


                // --------------------------------------------------------
                // BODY speichern
                // --------------------------------------------------------

                var body = frame.Body;

                body.Pos =
                    targetStageLocalPos;

                body.YawDeg =
                    targetStageLocalRot.eulerAngles.y;

                frame.Body = body;
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
                        roleSpawn,
                        targetRole.alignWithPlayer,
                        targetRole.hasPlayerAlignment,
                        playerAlignmentRotation,
                        alignmentPivotWorld
                    );

                if (transformedStartPose != null)
                {
                    targetRoleMeta.StartRootPose =
                        transformedStartPose;

                    /*
                    Debug.Log(
                        $"[PreRecordedTakeImporter] Updated StartRootPose " +
                        $"for role={targetRole.roleId}, " +
                        $"sourcePos={sourceRoleMeta.StartRootPose.LocalPosition}, " +
                        $"targetPos={transformedStartPose.LocalPosition}"
                    );
                    */
                }
            }

            _targetStore.SaveSessionModel(_targetSession);

            /*
            Debug.Log(
                $"[PreRecordedTakeImporter] Imported and transformed take: " +
                $"{sourceTakeMeta.TakeId} -> {targetTakeId}, " +
                $"role={targetRole.roleId}, " +
                $"targetScene={targetSceneCount}"
            );
            */
            return true;
        }

        private TransformData TransformStartPoseToCurrentStage(
            TransformData sourcePose,
            Transform stageRoot,
            Transform roleSpawn,
            bool alignWithPlayer,
            bool hasPlayerAlignment,
            Quaternion playerAlignmentRotation,
            Vector3 alignmentPivotWorld)
        {
            if (sourcePose == null ||
                stageRoot == null ||
                roleSpawn == null)
            {
                return null;
            }


            // --------------------------------------------------------
            // Source StartPose -> World
            // --------------------------------------------------------

            Vector3 worldPos =
                roleSpawn.TransformPoint(
                    sourcePose.LocalPosition
                );

            Quaternion worldRot =
                roleSpawn.rotation *
                sourcePose.LocalRotation;


            // --------------------------------------------------------
            // Optionales Player Alignment
            // --------------------------------------------------------

            if (alignWithPlayer && hasPlayerAlignment)
            {
                Vector3 offsetFromPivot =
                    worldPos - alignmentPivotWorld;

                worldPos =
                    alignmentPivotWorld +
                    playerAlignmentRotation * offsetFromPivot;

                worldRot =
                    playerAlignmentRotation *
                    worldRot;
            }


            // --------------------------------------------------------
            // World -> aktuelle StageRoot
            // --------------------------------------------------------

            return new TransformData
            {
                LocalPosition =
                    stageRoot.InverseTransformPoint(
                        worldPos
                    ),

                LocalRotation =
                    Quaternion.Inverse(stageRoot.rotation) *
                    worldRot
            };
        }
    }
}