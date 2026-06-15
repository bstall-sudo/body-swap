using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Loader
{
    public class AvatarCatalog : MonoBehaviour
    {
        [SerializeField] private List<AvatarEntry> avatars = new();

        private Dictionary<string, AvatarEntry> _avatarById;

        private void Awake()
        {
            RebuildIndex();

            // Später:
            // LoadExternalAvatarBundles();
        }

        public void RebuildIndex()
        {
            _avatarById = new Dictionary<string, AvatarEntry>();

            foreach (var avatar in avatars)
            {
                if (avatar == null || string.IsNullOrWhiteSpace(avatar.avatarId))
                    continue;

                if (_avatarById.ContainsKey(avatar.avatarId))
                {
                    Debug.LogWarning($"[AvatarCatalog] Duplicate avatarId: {avatar.avatarId}");
                    continue;
                }

                _avatarById.Add(avatar.avatarId, avatar);
            }
        }

        public AvatarEntry GetAvatarEntry(string avatarId)
        {
            if (_avatarById == null)
                RebuildIndex();

            if (!string.IsNullOrWhiteSpace(avatarId) &&
                _avatarById.TryGetValue(avatarId, out AvatarEntry entry) &&
                entry.prefab != null)
            {
                return entry;
            }

            Debug.LogWarning($"[AvatarCatalog] Avatar not found: {avatarId}. Trying default.");

            if (_avatarById.TryGetValue("default", out AvatarEntry defaultEntry) &&
                defaultEntry.prefab != null)
            {
                return defaultEntry;
            }

            Debug.LogError("[AvatarCatalog] No valid default avatar found.");
            return null;
        }
    }

    [System.Serializable]
    public class AvatarEntry
    {
        public string avatarId;
        public GameObject prefab;

        // später nützlich:
        public string sourcePath;
        public bool isExternal;
    }
}