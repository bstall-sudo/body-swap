using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Environment
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

        public StageSpawnPoint GetSpawnPoint(string spawnId = "default")
        {
            if (_currentEnvironment == null)
                return null;

            StageSpawnPoint[] points =
                _currentEnvironment.GetComponentsInChildren<StageSpawnPoint>(true);

            if (points == null || points.Length == 0)
                return null;

            foreach (var point in points)
            {
                if (point.spawnId == spawnId)
                    return point;
            }

            return points[0];
        }
    }

    [System.Serializable]
    public class EnvironmentEntry
    {
        public string environmentId;
        public GameObject prefab;
    }
    

}
