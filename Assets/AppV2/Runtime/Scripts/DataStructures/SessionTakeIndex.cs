using System.Collections.Generic;

using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.DataStructures
{
    public class SessionTakeIndex
    {
        private readonly Dictionary<string, TakeMeta> _takeMetaBySceneAndRole =
            new Dictionary<string, TakeMeta>();

        private string BuildTakeKey(int roleIndex, int sceneCount)
        {
            return $"{sceneCount}:{roleIndex}";
        }

        public bool HasTakeForScene(int roleIndex, int sceneCount)
        {
            string key = BuildTakeKey(roleIndex, sceneCount);
            return _takeMetaBySceneAndRole.ContainsKey(key);
        }

        public bool TryGetTakeForScene(int roleIndex, int sceneCount, out TakeMeta takeMeta)
        {
            string key = BuildTakeKey(roleIndex, sceneCount);

            UnityEngine.Debug.Log($"TryGetTakeForScene: looking for key={key}");

            bool found = _takeMetaBySceneAndRole.TryGetValue(key, out takeMeta);

            UnityEngine.Debug.Log($"TryGetTakeForScene: found={found}");

            return found;
        }

        public void StoreTakeMeta(TakeMeta meta)
        {
            if (meta == null)
                return;
            string key = BuildTakeKey(meta.RoleIndex, meta.SceneCount);
            UnityEngine.Debug.Log(
                $"StoreTakeMeta: key={key}, " +
                $"sceneCount={meta.SceneCount}, " +
                $"roleIndex={meta.RoleIndex}, " +
                $"takeId={meta.TakeId}, " +
                $"roleId={meta.RoleId}"
            );

            
            _takeMetaBySceneAndRole[key] = meta;
        }

        public void Clear()
        {
            _takeMetaBySceneAndRole.Clear();
        }

        public void RebuildFromSession(SessionModel session)
        {
            Clear();

            if (session == null)
            {
                UnityEngine.Debug.LogError("SessionTakeIndex.RebuildFromSession: session is null.");
                return;
            }

            if (session.Takes == null)
            {
                UnityEngine.Debug.LogWarning("SessionTakeIndex.RebuildFromSession: session.Takes is null.");
                return;
            }

            foreach (var meta in session.Takes)
            {
                UnityEngine.Debug.Log($"RebuildFromSession: {meta.SessionId}, {meta.TakeId}, {meta.RoleId}");

                StoreTakeMeta(meta);
            }

            UnityEngine.Debug.Log($"SessionTakeIndex rebuilt. Take count: {session.Takes.Count}");
        }
    }
}