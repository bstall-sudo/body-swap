using UnityEngine;
using System;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public static class RolePlacementUtility
    {
        /// <summary>Group all authored roles (including initially passive ones) by resolved
        /// spawn. Imported NPCs keep their separate source-session placement path.</summary>
        public static Dictionary<int, StagePose> BuildStartPoses(IReadOnlyList<RoleRig> roles,
            Transform stageRoot, string stageSpawnId, Func<string, Transform> resolveSpawn,
            Func<Vector3, float> groundY = null)
        {
            var result = new Dictionary<int, StagePose>();
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (role == null || role.hasPreRecordedTakes) continue;
                string key = role.startSpawnPointId ?? "";
                if (key == (string.IsNullOrWhiteSpace(stageSpawnId) ? "default" : stageSpawnId)) key = "";
                Transform spawn = string.IsNullOrWhiteSpace(key) ? null : resolveSpawn?.Invoke(key);
                if (spawn == null)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                        Debug.LogWarning($"[RolePlacement] Spawn '{key}' missing for {role.roleId}; using StageSpawnPoint.");
                    key = "";
                }
                if (!groups.TryGetValue(key, out var members))
                {
                    groups[key] = members = new List<int>();
                    anchors[key] = spawn;
                }
                members.Add(i);
            }
            foreach (var group in groups)
            {
                Transform spawn = anchors[group.Key];
                Vector3 center = spawn != null ? stageRoot.InverseTransformPoint(spawn.position) : Vector3.zero;
                Quaternion frame = spawn != null
                    ? Quaternion.Euler(0f, (Quaternion.Inverse(stageRoot.rotation) * spawn.rotation).eulerAngles.y, 0f)
                    : Quaternion.identity;
                for (int k = 0; k < group.Value.Count; k++)
                {
                    int index = group.Value[k];
                    Vector3 offset = group.Value.Count == 1 ? Vector3.zero : GetCirclePlacementPosition(k, group.Value.Count);
                    Vector3 position = center + frame * offset;
                    if (groundY != null) position.y = groundY(position);
                    Quaternion rotation = group.Value.Count == 1 ? frame : frame * GetCirclePlacementRotation(offset);
                    string targetId = roles[index].lookAtSpawnPointId;
                    if (!string.IsNullOrWhiteSpace(targetId))
                    {
                        Transform target = resolveSpawn?.Invoke(targetId);
                        if (target != null)
                        {
                            Vector3 direction = stageRoot.InverseTransformPoint(target.position) - position;
                            direction.y = 0f;
                            if (direction.sqrMagnitude > 0.0001f) rotation = Quaternion.LookRotation(direction, Vector3.up);
                        }
                        else Debug.LogWarning($"[RolePlacement] Look target '{targetId}' missing for {roles[index].roleId}; keeping default facing.");
                    }
                    result[index] = new StagePose { Position = position, Rotation = rotation };
                }
            }
            return result;
        }

        public static Vector3 GetCirclePlacementPosition(
            int roleIndex,
            int roleCount,
            float radius = 1.5f)
        {
            float angle =
                roleIndex *
                Mathf.PI *
                2f /
                Mathf.Max(1, roleCount);

            return new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );
        }

        public static Quaternion GetCirclePlacementRotation(
            Vector3 localPosition)
        {
            Vector3 flatDir = -localPosition;
            flatDir.y = 0f;

            if (flatDir.sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }
    }
}