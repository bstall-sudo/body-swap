using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class MirrorSetVisibility : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private float distanceFromAvatar = 1.5f;
        [SerializeField] private float heightOffset = 1.2f;

        public void ActivateMirror(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(active);
            }
        }

        public void PlaceMirrorInFrontOfAvatar(Transform avatarRoot)
        {
            if (avatarRoot == null)
            {
                Debug.LogError("[MirrorSetVisibility] avatarRoot is null.");
                return;
            }

            // Avatar Position
            Vector3 avatarPos = avatarRoot.position;

            // Spiegel vor Avatar
            Vector3 mirrorPos =
                avatarPos +
                avatarRoot.forward * distanceFromAvatar;

            mirrorPos.y += heightOffset;

            transform.position = mirrorPos;

            // Spiegel schaut zurück zum Avatar
            Vector3 lookTarget = avatarPos;
            lookTarget.y += heightOffset;

            transform.LookAt(lookTarget);

            // Spiegel umdrehen, damit Vorderseite korrekt zeigt
            transform.Rotate(0f, 180f, 0f);
        }
    }
}
