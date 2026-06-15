using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private ConversationStage stage;
    [SerializeField] private EnvironmentLoader environmentLoader;

    private string _previousEnvironmentId;
    private string _previousStageSpawnId;

    public SessionModel LoadSessionScene(
        string sessionId,
        SessionStore store,
        SessionTakeIndex takeIndex)
    {
        if (stage == null || environmentLoader == null)
        {
            Debug.LogError("[SceneLoader] Missing references.");
            return null;
        }

        SessionModel session = store.LoadSessionModel(sessionId);

        Debug.Log($"[LoadSessionScene] RoleCount: {session.RoleCount}");


        if (session == null)
        {
            Debug.LogError($"[SceneLoader] Could not load session: {sessionId}");
            return null;
        }

        Debug.Log($"[SceneLoader] Loading session: {session.SessionId}");

        // 1. Environment laden
        environmentLoader.LoadEnvironment(session.EnvironmentId);

        // 2. StageSpawn setzen
        string stageSpawnId = session.StageSpawnId;

        StageSpawnPoint stageSpawn =
            environmentLoader.GetSpawnPoint(stageSpawnId);

        if (stageSpawn != null)
        {
            stage.PlaceStageRoot(stageSpawn.transform);
        }
        else
        {
            Debug.LogWarning(
                $"[SceneLoader] No stage spawn found: {stageSpawnId}. Keeping current StageRoot pose."
            );
        }

        Debug.Log($"[LoadSessionScene] RoleCount: {session.RoleCount}");
        // 3. RoleCount setzen
        stage.ApplyRoleCountFromSession(session.RoleCount);

        // 4. Rollen / Avatare / SpawnIds anwenden
        ApplyRolesFromSession(session);

        // 5. TakeIndex neu aufbauen
        takeIndex.RebuildFromSession(session);

        // 6. Session in ConversationStage setzen
        stage.SetSession(session);

        return session;
    }

    private void ApplyRolesFromSession(SessionModel session)
    {
        if (session.Roles == null)
            return;

        Debug.Log($"[ApplyRolesFromSession] RoleCount: {session.RoleCount}");
        Debug.Log($"[ApplyRolesFromSession] stage.Roles.Count: {stage.Roles.Count}");
        foreach (ConversationRoleMeta roleMeta in session.Roles)
        {
            if (roleMeta.RoleIndex < 0 || roleMeta.RoleIndex >= stage.Roles.Count)
            {
                Debug.LogWarning($"[SceneLoader] Invalid role index: {roleMeta.RoleIndex}");
                continue;
            }

            RoleRig role = stage.Roles[roleMeta.RoleIndex];

            role.roleId = roleMeta.RoleId;
            role.avatarId = roleMeta.AvatarId;
            role.avatarSpawnId = roleMeta.AvatarSpawnId;

            // Avatar laden
            if (role.avatarLoader != null)
            {
                role.avatarLoader.LoadAvatar(role.avatarId);
            }

            Debug.Log($"[ApplyRolesFromSession] roleIndex: {roleMeta.RoleIndex}, role.roleId: {role.roleId}, role.avatarId: {role.avatarId}, role.avatarSpawnId: {role.avatarSpawnId} ");

            // RoleRoot platzieren
            PlaceRoleRoot(role);
        }
    }

    public void LoadSceneForRecordingMode(
        string environmentId,
        string stageSpawnId,
        List<RoleRig> roles)
    {
        LoadEnvironmentForStageRecMode(environmentId, stageSpawnId);

        LoadAvatarsForRoles(roles);
    }

    private void LoadAvatarsForRoles(List<RoleRig> roles)
    {
        if (roles == null)
            return;

        for (int i = 0; i < roles.Count; i++)
        {
            RoleRig role = roles[i];

            if (role == null)
                continue;

            if (string.IsNullOrWhiteSpace(role.avatarId))
            {
                role.avatarId = "default";
            }

            if (role.avatarLoader == null)
            {
                Debug.LogWarning($"[SceneLoader] No AvatarLoader found for role {i} / {role.roleId}");
                continue;
            }

            role.avatarLoader.LoadAvatar(role.avatarId);

            Debug.Log(
                $"[SceneLoader] Loaded avatar '{role.avatarId}' for role {role.roleId}"
            );
        }
    }
/*
    private void PlaceRoleRoot(RoleRig role)
    {
        if (role == null || role.root == null)
            return;

        // Leerer avatarSpawnId = Stage-Ursprung
        if (string.IsNullOrWhiteSpace(role.avatarSpawnId))
        {
            role.root.localPosition = Vector3.zero;
            role.root.localRotation = Quaternion.identity;
            return;
        }

        StageSpawnPoint spawn =
            environmentLoader.GetSpawnPoint(role.avatarSpawnId);

        if (spawn == null)
        {
            Debug.LogWarning(
                $"[SceneLoader] Avatar spawn not found: {role.avatarSpawnId}. Using Stage origin."
            );

            role.root.localPosition = Vector3.zero;
            role.root.localRotation = Quaternion.identity;
            return;
        }
        Debug.Log(
                $"[SceneLoader] Avatar spawn is at x: {spawn.transform.position.x}, y: {spawn.transform.position.y}, z: {spawn.transform.position.z} : {role.avatarSpawnId}. Using Stage origin."
            );
        role.root.SetPositionAndRotation(
            spawn.transform.position,
            spawn.transform.rotation
        );
    }
*/

    private void PlaceRoleRoot(RoleRig role)
    {
        if (role == null || role.root == null)
            return;

        if (string.IsNullOrWhiteSpace(role.avatarSpawnId))
        {
            role.root.localPosition = Vector3.zero;
            role.root.localRotation = Quaternion.identity;
            return;
        }

        StageSpawnPoint spawn =
            environmentLoader.GetSpawnPoint(role.avatarSpawnId);

        if (spawn == null)
        {
            Debug.LogWarning(
                $"[SceneLoader] Avatar spawn not found: {role.avatarSpawnId}. Using Stage origin."
            );

            role.root.localPosition = Vector3.zero;
            role.root.localRotation = Quaternion.identity;
            return;
        }

        Transform stageRoot = stage._stageRoot;

        Vector3 localPos =
            stageRoot.InverseTransformPoint(spawn.transform.position);

        Quaternion localRot =
            Quaternion.Inverse(stageRoot.rotation) * spawn.transform.rotation;

        role.root.localPosition = localPos;
        role.root.localRotation = localRot;

        Debug.Log(
            $"[SceneLoader] Placed role {role.roleId} at spawn {role.avatarSpawnId}: localPos={localPos}, localRotY={localRot.eulerAngles.y}"
        );
    }
    //das wird verwendet um das Environment im RecMode zu laden und die Stage am SpawnPoint zu platzieren.
    public void LoadEnvironmentForStageRecMode(
        string environmentId,
        string stageSpawnId = "default",
        bool placeStageRoot = true,
        bool updateStageEnvironmentId = true,
        bool placeEnvironmentAtStageRoot = false)
    {
        if (stage == null || environmentLoader == null)
        {
            Debug.LogError("[SceneLoader] Missing references.");
            return;
        }

        if (placeEnvironmentAtStageRoot)
        {
            environmentLoader.LoadEnvironmentAtStageRoot(environmentId, stage._stageRoot);
            
        }
        else
        {
            environmentLoader.LoadEnvironment(environmentId);
        }
        

        if (placeStageRoot)
        {
            StageSpawnPoint spawn =
                environmentLoader.GetSpawnPoint(stageSpawnId);

            if (spawn == null)
            {
                Debug.LogWarning(
                    $"[SceneLoader] No StageSpawnPoint found for spawnId: {stageSpawnId}"
                );
            }
            else
            {
                stage.PlaceStageRoot(spawn.transform);
            }
        }

        if (updateStageEnvironmentId)
        {
            stage.SetEnvironmentId(environmentId);
        }
    }
    
    //Damit im calibrationState eine ruhige umgebung ist.
    public void EnterCalibrationEnvironment(
    string calibrationEnvironmentId = "default",
    string calibrationStageSpawnId = "default")
    {
        _previousEnvironmentId = stage.EnvironmentId;
        _previousStageSpawnId = stage.StageSpawnId;

        LoadEnvironmentForStageRecMode(
            calibrationEnvironmentId,
            calibrationStageSpawnId,
            placeStageRoot: false,
            updateStageEnvironmentId: false,
            placeEnvironmentAtStageRoot: true
        );
    }

    public void ExitCalibrationEnvironment()
    {
        LoadEnvironmentForStageRecMode(
            stage.EnvironmentId,
            stage.StageSpawnId,
            placeStageRoot: false,
            updateStageEnvironmentId: false,
            placeEnvironmentAtStageRoot: false
        );
    }
}