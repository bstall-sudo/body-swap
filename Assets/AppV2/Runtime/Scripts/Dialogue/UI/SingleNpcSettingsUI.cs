using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class SingleNpcSettingsUI : MonoBehaviour
    {
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private SingleNpcSettingsRowUI rowPrefab;
        [SerializeField] private TMP_Text npcGroupIdDisplay;
        [SerializeField] private AvatarCatalog avatarCatalog;
        private readonly List<SingleNpcSettingsRowUI> _rows = new();

        private AvatarCatalog ResolveAvatarCatalog()
        {
            if (avatarCatalog != null) return avatarCatalog;
            // Older scene instances may not yet have the new inspector reference.
            // Resolve only an unambiguous catalog from this panel's scene.
            AvatarCatalog found = null;
            foreach (var candidate in FindObjectsByType<AvatarCatalog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                if (found != null) return null;
                found = candidate;
            }
            avatarCatalog = found;
            return avatarCatalog;
        }

        public void Display(NpcGroupSettings group, string storageFolder, AvatarCatalog catalog)
        {
            foreach (var row in _rows)
                if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            _rows.Clear();
            // The scene contains an editor preview row; dynamic rows replace it.
            foreach (Transform child in rowsContainer)
                if (child.GetComponent<SingleNpcSettingsRowUI>() != null) child.gameObject.SetActive(false);
            npcGroupIdDisplay.text = group.NpcGroupId;
            if (string.IsNullOrWhiteSpace(group.SessionId) || string.IsNullOrWhiteSpace(group.WorkshopFolderName) ||
                string.IsNullOrWhiteSpace(group.SessionFolderName))
            {
                npcGroupIdDisplay.text += " — Bitte zuerst eine Aufnahme auswählen.";
                return;
            }
            try
            {
                var session = new SessionStore(storageFolder).LoadSessionModel(group.SessionId,
                    workshopFolderName: group.WorkshopFolderName, sessionFolderName: group.SessionFolderName);
                // Own scene reference also works when this page is opened without
                // first displaying the active-role configuration.
                var availableCatalog = catalog != null ? catalog : ResolveAvatarCatalog();
                var avatars = availableCatalog != null ? availableCatalog.GetAllAvatarIds() : new List<string>();
                if (availableCatalog == null)
                    Debug.LogWarning("[SingleNpcSettingsUI] AvatarCatalog ist nicht zugewiesen.", this);
                bool estimated = false;
                if (session?.Roles != null)
                    foreach (var source in session.Roles)
                    {
                        if (source == null || string.IsNullOrWhiteSpace(source.RoleId)) continue;
                        var row = Instantiate(rowPrefab, rowsContainer);
                        row.Initialize(group, source, avatars);
                        row.gameObject.SetActive(true);
                        _rows.Add(row);
                        estimated |= source.HeightOfSeatedRoleCm <= 0;
                    }
                if (estimated) npcGroupIdDisplay.text += " (alte Aufnahme: Sitzhöhe geschätzt)";
            }
            catch (Exception ex)
            {
                npcGroupIdDisplay.text += " — Aufnahme konnte nicht geladen werden.";
                Debug.LogWarning($"[SingleNpcSettingsUI] {ex.Message}", this);
            }
        }
    }
}
