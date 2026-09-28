using UnityEngine;

namespace AppV2.Runtime.Scripts.Loader
{
    [ExecuteAlways]
    public class StageSpawnPoint : MonoBehaviour
    {
        public string spawnId = "default";

        [Header("Ground Snapping")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Wie weit oberhalb des Spawnpoints nach Ground gesucht wird.")]
        [SerializeField] private float searchUp = 2f;

        [Tooltip("Wie weit unterhalb des Spawnpoints nach Ground gesucht wird.")]
        [SerializeField] private float searchDown = 5f;

        private Vector3 _lastPosition;

        private void OnEnable()
        {
            _lastPosition = transform.position;
        }

#if UNITY_EDITOR

        private void Update()
        {
            // Im PlayMode soll nichts automatisch gesnappt werden.
            if (Application.isPlaying)
                return;

            Vector3 currentPosition = transform.position;

            bool xOrZChanged =
                !Mathf.Approximately(currentPosition.x, _lastPosition.x) ||
                !Mathf.Approximately(currentPosition.z, _lastPosition.z);

            if (xOrZChanged)
            {
                SnapToGround();
            }

            _lastPosition = transform.position;
        }

#endif

        public void SnapToGround()
        {
            Vector3 originalPosition = transform.position;

            int layerMask = GetGroundLayerMask();

            if (layerMask == 0)
                return;

            Vector3 rayOrigin =
                originalPosition + Vector3.up * searchUp;

            float rayDistance = searchUp + searchDown;

            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                layerMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits.Length == 0)
            {
                Debug.LogWarning(
                    $"[StageSpawnPoint] No ground found for {name}",
                    this
                );
                return;
            }

            // Ground auswählen, dessen Y-Position
            // der aktuellen Y-Position des Spawnpoints am nächsten ist.
            RaycastHit closestHit = hits[0];

            float closestDistance =
                Mathf.Abs(closestHit.point.y - originalPosition.y);

            for (int i = 1; i < hits.Length; i++)
            {
                float distance =
                    Mathf.Abs(hits[i].point.y - originalPosition.y);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHit = hits[i];
                }
            }

            transform.position = new Vector3(
                originalPosition.x,
                closestHit.point.y,
                originalPosition.z
            );
        }

        private int GetGroundLayerMask()
        {
            // Wenn im Inspector "Nothing" ausgewählt ist,
            // automatisch den Layer "Ground" verwenden.
            if (groundLayer.value == 0)
            {
                int groundLayerIndex = LayerMask.NameToLayer("Ground");

                if (groundLayerIndex == -1)
                {
                    Debug.LogWarning(
                        $"[StageSpawnPoint] Layer 'Ground' does not exist.",
                        this
                    );

                    return 0;
                }

                return 1 << groundLayerIndex;
            }

            return groundLayer.value;
        }
    }
}