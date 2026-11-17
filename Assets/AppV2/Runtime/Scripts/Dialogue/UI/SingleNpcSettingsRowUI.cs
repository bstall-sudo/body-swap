using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class SingleNpcSettingsRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text npcNameText;
        [SerializeField] private TMP_Dropdown avatarDropdown;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private TMP_InputField seatedHeightInput;
        [SerializeField] private Button resetToOriginButton;
        private NpcGroupSettings _group;
        private ConversationRoleMeta _source;
        private bool _displaying;

        public void Initialize(NpcGroupSettings group, ConversationRoleMeta source, List<string> avatarIds)
        {
            _group = group;
            _source = source;
            avatarDropdown.onValueChanged.RemoveListener(AvatarChanged);
            heightInput.onValueChanged.RemoveListener(HeightChanged);
            seatedHeightInput.onValueChanged.RemoveListener(HeightChanged);
            heightInput.onEndEdit.RemoveListener(EndEdit);
            seatedHeightInput.onEndEdit.RemoveListener(EndEdit);
            resetToOriginButton.onClick.RemoveListener(ResetToOrigin);
            var options = new List<string>(avatarIds ?? new List<string>());
            var overwrite = group.AvatarOverrides?.Find(v => v != null && v.RoleId == source.RoleId);
            string avatar = string.IsNullOrWhiteSpace(overwrite?.AvatarId) ? source.AvatarId : overwrite.AvatarId;
            if (!string.IsNullOrWhiteSpace(source.AvatarId) && !options.Contains(source.AvatarId)) options.Add(source.AvatarId);
            if (!string.IsNullOrWhiteSpace(avatar) && !options.Contains(avatar)) options.Add(avatar);
            avatarDropdown.ClearOptions();
            avatarDropdown.AddOptions(options);
            npcNameText.text = $"{source.RoleId}: {source.RoleName}";
            DisplayValues();
            avatarDropdown.onValueChanged.AddListener(AvatarChanged);
            heightInput.onValueChanged.AddListener(HeightChanged);
            seatedHeightInput.onValueChanged.AddListener(HeightChanged);
            heightInput.onEndEdit.AddListener(EndEdit);
            seatedHeightInput.onEndEdit.AddListener(EndEdit);
            resetToOriginButton.onClick.AddListener(ResetToOrigin);
        }

        private void DisplayValues()
        {
            _displaying = true;
            var v = _group.AvatarOverrides?.Find(o => o != null && o.RoleId == _source.RoleId);
            string avatar = string.IsNullOrWhiteSpace(v?.AvatarId) ? _source.AvatarId : v.AvatarId;
            avatarDropdown.SetValueWithoutNotify(Mathf.Max(0, avatarDropdown.options.FindIndex(o => o.text == avatar)));
            avatarDropdown.RefreshShownValue();
            heightInput.SetTextWithoutNotify((v != null && v.HeightOfRoleCm > 0 ? v.HeightOfRoleCm : _source.HeightOfRoleCm).ToString());
            seatedHeightInput.SetTextWithoutNotify((v != null && v.HeightOfSeatedRoleCm > 0 ? v.HeightOfSeatedRoleCm : NpcRoleOverrides.SeatedHeight(_source)).ToString());
            _displaying = false;
        }

        private void AvatarChanged(int _) => StoreValues();
        private void HeightChanged(string _) => StoreValues();
        private void EndEdit(string _) { StoreValues(); DisplayValues(); }

        private void StoreValues()
        {
            if (_displaying || _source == null) return;
            var previous = _group.AvatarOverrides?.Find(o => o != null && o.RoleId == _source.RoleId);
            // Empty/unfinished text must not erase the previously valid height.
            int height = int.TryParse(heightInput.text, out int h) && h > 0 ? h :
                previous != null && previous.HeightOfRoleCm > 0 ? previous.HeightOfRoleCm : _source.HeightOfRoleCm;
            int seated = int.TryParse(seatedHeightInput.text, out int s) && s > 0 ? s :
                previous != null && previous.HeightOfSeatedRoleCm > 0 ? previous.HeightOfSeatedRoleCm : NpcRoleOverrides.SeatedHeight(_source);
            string avatar = avatarDropdown.options.Count > 0 ? avatarDropdown.options[avatarDropdown.value].text : _source.AvatarId;
            NpcRoleOverrides.Set(_group, _source, avatar, height, seated);
        }

        public void ResetToOrigin()
        {
            _group.AvatarOverrides?.RemoveAll(v => v == null || v.RoleId == _source.RoleId);
            DisplayValues();
        }
    }
}
