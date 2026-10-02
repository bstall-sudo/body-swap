using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using System.Xml.Serialization;

namespace AppV2.Runtime.Scripts.Loader
{
    public static class PreRecordedSceneImporter
    {
        public static List<RoleRig> BuildRoleRigsFromImports(
            List<PreRecordedSceneImport> imports,
            SessionStore store)
        {
            List<RoleRig> importedRoles = new List<RoleRig>();

            if (imports == null) return importedRoles;

            int i = 0;

            foreach (var import in imports)
            {
                if (import == null || !import.enabled) continue;
                
                string npcGroupId = $"NpcGroup{i}";

                string sessionRootPath = Path.Combine(
                Application.persistentDataPath,
                import.workshopFolderName,
                import.sessionFolderName,
                import.sessionId
                );


                if (!Directory.Exists(sessionRootPath))
                {
                    Debug.LogWarning($"[PreRecordedSceneImporter] Session folder not found: {sessionRootPath}");
                    continue;
                }

                // nächster Schritt: session.json lesen
                // vorerst testweise Rollen aus Dateinamen finden
                importedRoles.AddRange(
                    BuildRoleRigsFromImportedSession(import, store, npcGroupId)
                );
            }

            return importedRoles;
        }


        public static List<RoleRig> BuildRoleRigsFromImportedSession(
                PreRecordedSceneImport import,
                SessionStore store, string npcGroupId)
            {
                List<RoleRig> result = new();

                if (import == null || !import.enabled)
                    return result;


                //UnityEngine.Debug.Log($"der SourcePath ist: {import.sessionId}");
                SessionModel sourceSession =
                    store.LoadSessionModel(import.sessionId, workshopFolderName: import.workshopFolderName, sessionFolderName: import.sessionFolderName);

                if (sourceSession == null)
                {
                    Debug.LogError($"[PreRecordedSceneImporter] Could not load source session:");
                    return result;
                }

                if (sourceSession.Roles == null || sourceSession.Roles.Count == 0)
                {
                    Debug.LogWarning($"[PreRecordedSceneImporter] Source session has no roles: {sourceSession.SessionId}");
                    return result;
                }


                foreach (ConversationRoleMeta meta in sourceSession.Roles)
                {
                    if (meta == null || string.IsNullOrWhiteSpace(meta.RoleId))
                        continue;

                    string importedRoleId = $"{npcGroupId}_{meta.RoleId}";

                    RoleRig role = new RoleRig
                    {
                        roleId = importedRoleId,
                        roleIndex = meta.RoleIndex,
                        roleSpawnId = import.npcGroupSpawnId,

                        preRecordedStartRootPose = meta.StartRootPose != null
                            ? new TransformData
                            {
                                LocalPosition = meta.StartRootPose.LocalPosition,
                                LocalRotation = meta.StartRootPose.LocalRotation
                            }
                            : null,

                        hasPreRecordedTakes = true,
                        isActiveConversationPartner = false,

                        npcGroupId = npcGroupId,
                        sourceRoleId = meta.RoleId,
                        sourceRoleIndex = meta.RoleIndex,

                        //takeSource = import.sessionSourcePath,

                        preRecordedCalibration = meta.Calibration,

                        // falls diese Felder in RoleRig existieren:
                        avatarId = meta.AvatarId,
                        avatarName = meta.RoleName,
                        heightOfRoleCm = meta.HeightOfRoleCm,
                        sittingIdle = meta.SittingIdle,
                        alignWithPlayer = import.alignWithPlayer
                    };

                    result.Add(role);
                }

                //Debug.Log($"[PreRecordedSceneImporter] Imported {result.Count} NPC roles from {sourceSession.SessionId}");

                return result;
            }

        private static string ExtractRoleIdFromTakeId(string takeId)
        {
            // "take_0000_A" -> "A"
            int lastUnderscore = takeId.LastIndexOf('_');

            if (lastUnderscore < 0 || lastUnderscore >= takeId.Length - 1)
                return takeId;

            return takeId.Substring(lastUnderscore + 1);
        }
    }
    
}
