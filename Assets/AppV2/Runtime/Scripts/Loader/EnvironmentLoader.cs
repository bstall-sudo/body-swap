using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Loader
{
    public class EnvironmentLoader : MonoBehaviour
    {
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private List<EnvironmentEntry> environments;

        private GameObject _currentEnvironment;

        public GameObject CurrentEnvironment => _currentEnvironment;

        public void LoadEnvironment(string environmentId)
        {
            if (_currentEnvironment != null)
                Destroy(_currentEnvironment);

            EnvironmentEntry entry = environments.Find(e => e.environmentId == environmentId);

            if (entry == null)
            {
                Debug.LogWarning($"Environment not found: {environmentId}. Loading default.");
                entry = environments.Find(e => e.environmentId == "default");
            }

            if (entry == null || entry.prefab == null)
            {
                Debug.LogError("No valid environment prefab found.");
                return;
            }

            _currentEnvironment = Instantiate(
                entry.prefab,
                environmentRoot.position,
                environmentRoot.rotation,
                environmentRoot
            );

            _currentEnvironment.name = entry.environmentId;

            
        }


        public void LoadEnvironmentAtStageRoot(string environmentId, Transform stageRoot)
        {
            LoadEnvironment(environmentId);

            if (_currentEnvironment == null || stageRoot == null)
                return;

            
            _currentEnvironment.transform.position = stageRoot.position;
            _currentEnvironment.transform.rotation = stageRoot.rotation;
            
        }

        public StageSpawnPoint GetSpawnPoint(string spawnId = "default")
        {
            if (_currentEnvironment == null)
            {
                Debug.LogWarning(
                    "[EnvironmentLoader] No environment is currently loaded."
                );

                return null;
            }

            StageSpawnPoint[] points =
                _currentEnvironment.GetComponentsInChildren<StageSpawnPoint>(true);

            if (points == null || points.Length == 0)
            {
                Debug.LogWarning(
                    "[EnvironmentLoader] Current environment contains no StageSpawnPoints."
                );

                return null;
            }

            // Gewünschten SpawnPoint suchen
            foreach (StageSpawnPoint point in points)
            {
                if (point.spawnId == spawnId)
                    return point;
            }

            // Gewünschte ID existiert nicht -> default versuchen
            foreach (StageSpawnPoint point in points)
            {
                if (point.spawnId == "default")
                {
                    Debug.LogWarning(
                        $"[EnvironmentLoader] SpawnId '{spawnId}' not found. " +
                        $"Using 'default' instead."
                    );

                    return point;
                }
            }

            Debug.LogWarning(
                $"[EnvironmentLoader] SpawnId '{spawnId}' not found " +
                $"and no 'default' SpawnPoint exists."
            );

            return null;
        }
        public List<string> GetEnvironmentIds()
        {
            List<string> result = new();

            foreach (EnvironmentEntry entry in environments)
            {
                if (entry == null ||
                    entry.prefab == null ||
                    string.IsNullOrWhiteSpace(entry.environmentId))
                    continue;

                if (!result.Contains(entry.environmentId))
                    result.Add(entry.environmentId);
            }

            result.Sort();

            return result;
        }
        public List<string> GetSpawnIds(string environmentId)
        {
            List<string> result = new();

            // Namen deiner tatsächlichen EnvironmentEntry-Liste
            // gegebenenfalls anpassen.
            EnvironmentEntry entry = environments.Find(
                e => e.environmentId == environmentId
            );

            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning(
                    $"[EnvironmentLoader] Environment not found: {environmentId}"
                );
                return result;
            }

            StageSpawnPoint[] spawnPoints =
                entry.prefab.GetComponentsInChildren<StageSpawnPoint>(true);

            foreach (var spawnPoint in spawnPoints)
            {
                if (string.IsNullOrWhiteSpace(spawnPoint.spawnId))
                    continue;

                if (!result.Contains(spawnPoint.spawnId))
                    result.Add(spawnPoint.spawnId);
            }

            result.Sort();

            return result;
        }

        public Transform GetTransformFromSpawnId(string spawnId = "default")
        {
            StageSpawnPoint spawnPoint = GetSpawnPoint(spawnId);

            return spawnPoint != null
                ? spawnPoint.transform
                : null;
        }
    }

    [System.Serializable]
    public class EnvironmentEntry
    {
        public string environmentId;
        public GameObject prefab;
    }
    

}
