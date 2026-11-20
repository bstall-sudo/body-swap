using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Loader;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class RoleSettingsRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text roleNameText;
        [SerializeField] private TMP_Dropdown avatarDropdown;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private TMP_InputField seatedHeightInput;
        [SerializeField] private Button removeRoleButton;
        [SerializeField] private TMP_Dropdown spawnPointDropdown;
        [SerializeField] private TMP_Dropdown lookAtSpawnPointDropdown;
        [SerializeField] private Toggle isActiveConversationPartnerToggle;
        [SerializeField] private Image avatarPreviewImage;
        [SerializeField] private Image spawnPreviewImage;
        private AvatarCatalog _avatarCatalog;
        private EnvironmentLoader _environmentLoader;
        private string _environmentId;
        private string _stageSpawnId;
        private readonly List<string> _spawnValues = new();
        private readonly List<string> _lookValues = new();
        public event Action<int, string, string, bool> PlacementChanged;

        public void ConfigurePlacement(List<string> spawnIds, string stageSpawnId, StartRoleSettings settings,
            EnvironmentLoader environmentLoader = null, string environmentId = null)
        {
            _environmentLoader = environmentLoader;
            _environmentId = string.IsNullOrWhiteSpace(environmentId) ? "default" : environmentId;
            _stageSpawnId = string.IsNullOrWhiteSpace(stageSpawnId) ? "default" : stageSpawnId;
            // The prefab originally gives both dropdowns the same name. Resolve by
            // their owning row label for old scene/prefab instances without new bindings.
            foreach (var dropdown in GetComponentsInChildren<TMP_Dropdown>(true))
            {
                string parent = dropdown.transform.parent != null ? dropdown.transform.parent.name : "";
                if (spawnPointDropdown == null && parent == "AvatarSpawnPoint_Text") spawnPointDropdown = dropdown;
                if (lookAtSpawnPointDropdown == null && parent == "AvatarLookingDirection_Text") lookAtSpawnPointDropdown = dropdown;
            }
            if (isActiveConversationPartnerToggle == null)
                foreach (var toggle in GetComponentsInChildren<Toggle>(true))
                    if (toggle.name == "isActiveConversationPartnerToggle") isActiveConversationPartnerToggle = toggle;
            if (spawnPointDropdown != null)
            {
                spawnPointDropdown.onValueChanged.RemoveListener(OnPlacementChanged);
                SetSpawnOptions(spawnPointDropdown, _spawnValues, spawnIds, settings.SpawnPointId,
                    $"StageSpawnPoint ({_stageSpawnId})");
                spawnPointDropdown.onValueChanged.AddListener(OnPlacementChanged);
            }
            RefreshSpawnPreview();
            if (lookAtSpawnPointDropdown != null)
            {
                lookAtSpawnPointDropdown.onValueChanged.RemoveListener(OnPlacementChanged);
                SetSpawnOptions(lookAtSpawnPointDropdown, _lookValues, spawnIds, settings.LookAtSpawnPointId, "Standardausrichtung");
                lookAtSpawnPointDropdown.onValueChanged.AddListener(OnPlacementChanged);
            }
            if (isActiveConversationPartnerToggle != null)
            {
                isActiveConversationPartnerToggle.onValueChanged.RemoveListener(OnPartnerChanged);
                isActiveConversationPartnerToggle.SetIsOnWithoutNotify(settings.StartsActive);
                isActiveConversationPartnerToggle.onValueChanged.AddListener(OnPartnerChanged);
            }
        }

        private static void SetSpawnOptions(TMP_Dropdown dropdown, List<string> values,
            List<string> ids, string selected, string defaultLabel)
        {
            values.Clear(); values.Add("");
            var labels = new List<string> { defaultLabel };
            if (ids != null) foreach (var id in ids)
                if (!string.IsNullOrWhiteSpace(id) && !values.Contains(id)) { values.Add(id); labels.Add(id); }
            selected ??= "";
            if (!values.Contains(selected)) { values.Add(selected); labels.Add(selected + " (nicht im Environment)"); }
            dropdown.ClearOptions(); dropdown.AddOptions(labels);
            dropdown.SetValueWithoutNotify(values.IndexOf(selected)); dropdown.RefreshShownValue();
        }

        private string SelectedSpawnId =>
            spawnPointDropdown != null && _spawnValues.Count > spawnPointDropdown.value
                ? _spawnValues[spawnPointDropdown.value]
                : "";

        private void RefreshSpawnPreview()
        {
            string spawnId = SelectedSpawnId;
            if (string.IsNullOrEmpty(spawnId))
                spawnId = _stageSpawnId;

            SetPreview(spawnPreviewImage, _environmentLoader != null
                ? _environmentLoader.GetSpawnPreview(_environmentId, spawnId)
                : null);
        }

        private void OnPlacementChanged(int _)
        {
            RefreshSpawnPreview();
            NotifyPlacementChanged();
        }
        private void OnPartnerChanged(bool _) => NotifyPlacementChanged();
        private void NotifyPlacementChanged()
        {
            PlacementChanged?.Invoke(_roleIndex,
                SelectedSpawnId,
                lookAtSpawnPointDropdown != null && _lookValues.Count > lookAtSpawnPointDropdown.value ? _lookValues[lookAtSpawnPointDropdown.value] : "",
                isActiveConversationPartnerToggle == null || isActiveConversationPartnerToggle.isOn);
        }

        private int _roleIndex;

        public event Action<int, string, string, string> Changed;
        public event Action<int> RemoveRequested;

        public void SetRemoveAllowed(bool allowed)
        {
            if (removeRoleButton != null) removeRoleButton.interactable = allowed;
        }

        private void RemoveRole() => RemoveRequested?.Invoke(_roleIndex);

        public void SetAvatarWithoutNotify(string avatarId)
        {
            int index = avatarDropdown.options.FindIndex(option => option.text == avatarId);
            if (index < 0) return;
            avatarDropdown.SetValueWithoutNotify(index);
            avatarDropdown.RefreshShownValue();
            RefreshAvatarPreview();
        }

        public void Initialize(
            int roleIndex,
            string avatarId,
            string height,
            string seatedHeight,
            List<string> avatarIds,
            AvatarCatalog avatarCatalog = null)
        {
            _roleIndex = roleIndex;
            _avatarCatalog = avatarCatalog;
            // Rows are reused; detach before updating dropdowns and input values.
            avatarDropdown.onValueChanged.RemoveListener(OnChanged);
            heightInput.onEndEdit.RemoveListener(OnChanged);
            seatedHeightInput.onEndEdit.RemoveListener(OnChanged);
            heightInput.SetTextWithoutNotify(height);
            seatedHeightInput.SetTextWithoutNotify(seatedHeight);
            if (removeRoleButton == null)
            {
                foreach (var button in GetComponentsInChildren<Button>(true))
                    if (button.name == "RemoveRole") removeRoleButton = button;
            }
            if (removeRoleButton != null)
            {
                removeRoleButton.onClick.RemoveListener(RemoveRole);
                removeRoleButton.onClick.AddListener(RemoveRole);
            }

            roleNameText.text =
                $"Role {(char)('A' + roleIndex)}";

            avatarDropdown.ClearOptions();

            if (avatarIds == null || avatarIds.Count == 0)
            {
                Debug.LogWarning(
                    "[RoleSettingsRowUI] No avatars available."
                );

                SetPreview(avatarPreviewImage, null);
                avatarDropdown.interactable = false;
                return;
            }

            avatarDropdown.interactable = true;
            avatarDropdown.AddOptions(avatarIds);

            int selected = avatarIds.IndexOf(avatarId);

            if (selected < 0)
            {
                Debug.LogWarning(
                    $"[RoleSettingsRowUI] AvatarId '{avatarId}' " +
                    "not found in AvatarCatalog."
                );
                if (!string.IsNullOrWhiteSpace(avatarId))
                {
                    selected = avatarDropdown.options.Count;
                    avatarDropdown.options.Add(new TMP_Dropdown.OptionData(avatarId));
                }
            }

            avatarDropdown.SetValueWithoutNotify(
                Mathf.Max(0, selected)
            );

            avatarDropdown.RefreshShownValue();
            RefreshAvatarPreview();

            heightInput.SetTextWithoutNotify(height);
            seatedHeightInput.SetTextWithoutNotify(seatedHeight);

            avatarDropdown.onValueChanged.RemoveListener(
                OnChanged
            );
            heightInput.onEndEdit.RemoveListener(
                OnChanged
            );
            seatedHeightInput.onEndEdit.RemoveListener(
                OnChanged
            );

            avatarDropdown.onValueChanged.AddListener(
                OnChanged
            );
            heightInput.onEndEdit.AddListener(
                OnChanged
            );
            seatedHeightInput.onEndEdit.AddListener(
                OnChanged
            );
        }

        private void RefreshAvatarPreview()
        {
            string avatarId = avatarDropdown.options[avatarDropdown.value].text;
            SetPreview(avatarPreviewImage, _avatarCatalog != null ? _avatarCatalog.GetAvatarPreview(avatarId) : null);
        }

        private static void SetPreview(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void OnChanged(int value)
        {
            RefreshAvatarPreview();
            NotifyChanged();
        }

        private void OnChanged(string value)
        {
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (avatarDropdown.options.Count == 0)
                return;

            Changed?.Invoke(
                _roleIndex,
                avatarDropdown.options[
                    avatarDropdown.value
                ].text,
                heightInput.text,
                seatedHeightInput.text
            );
        }

        private void OnDestroy()
        {
            if (spawnPointDropdown != null) spawnPointDropdown.onValueChanged.RemoveListener(OnPlacementChanged);
            if (lookAtSpawnPointDropdown != null) lookAtSpawnPointDropdown.onValueChanged.RemoveListener(OnPlacementChanged);
            if (isActiveConversationPartnerToggle != null) isActiveConversationPartnerToggle.onValueChanged.RemoveListener(OnPartnerChanged);
            if (removeRoleButton != null) removeRoleButton.onClick.RemoveListener(RemoveRole);
            if (avatarDropdown != null) avatarDropdown.onValueChanged.RemoveListener(OnChanged);
            if (heightInput != null) heightInput.onEndEdit.RemoveListener(OnChanged);
            if (seatedHeightInput != null) seatedHeightInput.onEndEdit.RemoveListener(OnChanged);
        }
    }
}
