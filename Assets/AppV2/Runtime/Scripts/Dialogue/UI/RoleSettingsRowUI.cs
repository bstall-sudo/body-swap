using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class RoleSettingsRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text roleNameText;
        [SerializeField] private TMP_Dropdown avatarDropdown;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private TMP_InputField seatedHeightInput;
        [SerializeField] private Button removeRoleButton;

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
        }

        public void Initialize(
            int roleIndex,
            string avatarId,
            string height,
            string seatedHeight,
            List<string> avatarIds)
        {
            _roleIndex = roleIndex;
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

        private void OnChanged(int value)
        {
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
            if (removeRoleButton != null) removeRoleButton.onClick.RemoveListener(RemoveRole);
            if (avatarDropdown != null) avatarDropdown.onValueChanged.RemoveListener(OnChanged);
            if (heightInput != null) heightInput.onEndEdit.RemoveListener(OnChanged);
            if (seatedHeightInput != null) seatedHeightInput.onEndEdit.RemoveListener(OnChanged);
        }
    }
}
