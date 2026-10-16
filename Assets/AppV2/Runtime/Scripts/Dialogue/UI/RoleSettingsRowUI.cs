using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class RoleSettingsRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text roleNameText;
        [SerializeField] private TMP_Dropdown avatarDropdown;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private TMP_InputField seatedHeightInput;

        private int _roleIndex;

        public event Action<int, string, string, string> Changed;

        public void Initialize(
            int roleIndex,
            string avatarId,
            string height,
            string seatedHeight,
            List<string> avatarIds)
        {
            _roleIndex = roleIndex;

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
    }
}