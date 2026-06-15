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

            if (_currentAvatar != null)
                Destroy(_currentAvatar);

            AvatarEntry entry = avatarCatalog.GetAvatarEntry(avatarId);

            if (entry == null)
                return;

            _currentAvatar = Instantiate(
                entry.prefab,
                avatarParent
            );

            rigDefinition = GetComponentInChildren<AvatarRigDefinition>(true);

            if (rigDefinition != null)
                rigDefinition.RefreshReferences();
            else
                Debug.LogError("[AvatarLoader] No AvatarRigDefinition found after loading avatar.");

            
            rigFollower = GetComponentInChildren<AvatarRigFollower>(true);

            if (rigFollower != null)
                rigFollower.RefreshReferences();
            else
                Debug.LogError("[AvatarLoader] No AvatarRigFollower found after loading avatar.");

            _currentAvatar.name = entry.avatarId;

            _currentAvatar.transform.localPosition = Vector3.zero;
            _currentAvatar.transform.localRotation = Quaternion.identity;
            _currentAvatar.transform.localScale = Vector3.one;
        }
    }
}