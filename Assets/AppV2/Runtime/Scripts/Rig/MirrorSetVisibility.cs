using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class MirrorSetVisibility : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] public float distanceFromAvatar = 1.5f;
        [SerializeField] private float heightOffset = - 1.2f;

        public void ActivateMirror(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(active);
            }
        }

        public void PlaceMirrorAtGroundPosition(
            Vector3 groundWorldPosition,
            Vector3 playerWorldPosition)
        {
            // Spiegel steht auf dem Boden
            Vector3 mirrorPos = groundWorldPosition;

            // Nur falls Pivot etwas über/unter dem Spiegelboden liegt
            mirrorPos.y += heightOffset;

            transform.position = mirrorPos;


            // Nur horizontal zum Spieler schauen
            Vector3 directionToPlayer =
                playerWorldPosition - mirrorPos;

            directionToPlayer.y = 0f;

            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        directionToPlayer.normalized,
                        Vector3.up
                    );

                // Falls für dein Spiegelmodell notwendig
                transform.Rotate(-15f, 180f, 0f);
            }
        }
    }
}
