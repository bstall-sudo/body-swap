using UnityEngine;
using AppV2.Runtime.Scripts.Rig;

namespace AppV2.Runtime.Scripts.Loader
{
    public class AvatarLoader : MonoBehaviour
    {
        [SerializeField] private AvatarCatalog avatarCatalog;
        [SerializeField] private Transform avatarParent;

        private GameObject _currentAvatar;
        private AvatarRigDefinition rigDefinition;
        private AvatarRigFollower rigFollower;

        public GameObject CurrentAvatar => _currentAvatar;

        private void Awake()
        {
            if (avatarCatalog == null)
                avatarCatalog = GetComponentInParent<AvatarCatalog>();

            if (avatarParent == null)
                avatarParent = transform;

            if (rigDefinition == null)
                rigDefinition = GetComponentInChildren<AvatarRigDefinition>(true);
            
            if (rigFollower == null)
                rigFollower = GetComponentInChildren<AvatarRigFollower>(true);

        }

        public void LoadAvatar(string avatarId)
        {
            if (avatarCatalog == null)
            {
                Debug.LogError("[AvatarLoader] AvatarCatalog is missing.");
                return;
            }

            if (avatarParent == null)
                avatarParent = transform;

            AvatarEntry entry = avatarCatalog.GetAvatarEntry(avatarId);

            if (entry == null)
                return;

            if (_currentAvatar != null)
            {
                // Destroy runs at the end of the frame. Detach first so reference
                // resolution in this frame cannot select the previous avatar.
                _currentAvatar.SetActive(false);
                _currentAvatar.transform.SetParent(null, true);
                Destroy(_currentAvatar);
            }

            _currentAvatar = Instantiate(
                entry.prefab,
                avatarParent
            );

            // Finish attaching/preparing the replacement before any rig caches
            // or target maps are rebuilt.
            _currentAvatar.name = entry.avatarId;
            _currentAvatar.transform.localPosition = Vector3.zero;
            _currentAvatar.transform.localRotation = Quaternion.identity;
            _currentAvatar.transform.localScale = Vector3.one;

            rigDefinition = GetComponentInChildren<AvatarRigDefinition>(true);

            if (rigDefinition != null)
                rigDefinition.RefreshReferences(avatarWasReplaced: true);
            else
                Debug.LogError("[AvatarLoader] No AvatarRigDefinition found after loading avatar.");

            
            rigFollower = GetComponentInChildren<AvatarRigFollower>(true);

            if (rigFollower != null)
                rigFollower.RefreshReferences();
            else
                Debug.LogError("[AvatarLoader] No AvatarRigFollower found after loading avatar.");

        }
    }
}
