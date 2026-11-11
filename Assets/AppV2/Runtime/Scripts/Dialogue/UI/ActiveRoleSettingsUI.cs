using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Loader;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class ActiveRolesSettingsUI : MonoBehaviour
    {
        [SerializeField] private AvatarCatalog avatarCatalog;
        [SerializeField] private TMP_Text activeRoleCountText;
        [SerializeField] private Button addRoleButton;
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private RoleSettingsRowUI rowPrefab;
        [SerializeField] private TMP_Dropdown firstRoleAvatarDropdown;
        private PresetConfig _config;
        private readonly List<RoleSettingsRowUI> _rows = new();
        private List<string> _availableAvatarIds = new();

        private void OnEnable() => BindButtons();

        private void BindButtons()
        {
            if (addRoleButton == null)
            {
                foreach (var button in GetComponentsInChildren<Button>(true))
                    if (button.name == "AddRole") addRoleButton = button;
            }
            if (addRoleButton != null)
            {
                addRoleButton.onClick.RemoveListener(AddRole);
                addRoleButton.onClick.AddListener(AddRole);
            }
            else
                Debug.LogWarning("[ActiveRolesSettingsUI] Add Role Button is not assigned and no child named AddRole was found.", this);
        }

        private void OnDestroy()
        {
            if (addRoleButton != null) addRoleButton.onClick.RemoveListener(AddRole);
            if (firstRoleAvatarDropdown != null)
                firstRoleAvatarDropdown.onValueChanged.RemoveListener(OnFirstRoleAvatarChanged);
        }

        public void Display(PresetConfig config)
        {
            _config = config;
            BindButtons();
            if (_config == null) return;
            _config.StartRoles ??= new StartRolesSettings();
            _config.StartRoles.Roles ??= new List<StartRoleSettings>();
            _availableAvatarIds = avatarCatalog != null ? avatarCatalog.GetAllAvatarIds() : new List<string>();
            FindFirstRoleAvatarDropdown();
            if (_config.StartRoles.Roles.Count == 0) _config.StartRoles.Roles.Add(NewRole());
            RebuildRows();
        }

        private StartRoleSettings NewRole() => new StartRoleSettings
        {
            AvatarId = _availableAvatarIds.Count > 0 ? _availableAvatarIds[0] : "default",
            HeightOfRoleCm = 0,
            HeightOfSeatedRoleCm = 0
        };

        private void AddRole()
        {
            Debug.Log("[ActiveRolesSettingsUI] AddRole clicked.", this);
            if (_config == null || _config.StartRoles.Roles.Count >= 20) return;
            _config.StartRoles.Roles.Add(NewRole());
            RebuildRows();
        }

        private void RemoveRole(int index)
        {
            Debug.Log($"[ActiveRolesSettingsUI] RemoveRole clicked: {index}.", this);
            if (_config == null || _config.StartRoles.Roles.Count <= 1 ||
                index < 0 || index >= _config.StartRoles.Roles.Count) return;
            _config.StartRoles.Roles.RemoveAt(index);
            var removedRow = _rows[index];
            removedRow.Changed -= OnRoleChanged;
            removedRow.RemoveRequested -= RemoveRole;
            removedRow.gameObject.SetActive(false);
            Destroy(removedRow.gameObject);
            _rows.RemoveAt(index);
            RebuildRows();
        }

        private void RebuildRows()
        {
            var settings = _config.StartRoles;
            settings.RoleCount = settings.Roles.Count;
            // Keep existing rows: adding one role should only instantiate one row.
            while (_rows.Count > settings.RoleCount)
            {
                var row = _rows[_rows.Count - 1];
                row.Changed -= OnRoleChanged;
                row.RemoveRequested -= RemoveRole;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
                _rows.RemoveAt(_rows.Count - 1);
            }
            for (int i = 0; i < settings.Roles.Count; i++)
            {
                var role = settings.Roles[i];
                if (string.IsNullOrWhiteSpace(role.AvatarId) && _availableAvatarIds.Count > 0)
                    role.AvatarId = _availableAvatarIds.Contains("default") ? "default" : _availableAvatarIds[0];
                role.RoleId = ((char)('A' + i)).ToString();
                RoleSettingsRowUI row;
                if (i < _rows.Count)
                    row = _rows[i];
                else
                {
                    row = Instantiate(rowPrefab, rowsContainer);
                    _rows.Add(row);
                }
                row.Changed -= OnRoleChanged;
                row.RemoveRequested -= RemoveRole;
                row.Initialize(i, role.AvatarId,
                    role.HeightOfRoleCm > 0 ? role.HeightOfRoleCm.ToString() : "",
                    role.HeightOfSeatedRoleCm > 0 ? role.HeightOfSeatedRoleCm.ToString() : "",
                    _availableAvatarIds);
                row.SetRemoveAllowed(settings.RoleCount > 1);
                row.Changed += OnRoleChanged;
                row.RemoveRequested += RemoveRole;
            }
            if (activeRoleCountText != null) activeRoleCountText.text = $"Aktive Rollen: {settings.RoleCount}";
            if (addRoleButton != null) addRoleButton.interactable = settings.RoleCount < 20;
            RefreshFirstRoleAvatarDropdown();
        }

        private void OnRoleChanged(int index, string avatarId, string height, string seatedHeight)
        {
            var role = _config.StartRoles.Roles[index];
            role.AvatarId = avatarId;
            role.HeightOfRoleCm = int.TryParse(height, out int h) ? h : 0;
            role.HeightOfSeatedRoleCm = int.TryParse(seatedHeight, out int sh) ? sh : 0;
            if (index == 0) RefreshFirstRoleAvatarDropdown();
        }

        private void FindFirstRoleAvatarDropdown()
        {
            if (firstRoleAvatarDropdown != null) return;
            var configurationUI = GetComponentInParent<WorkshopConfigurationUI>(true);
            if (configurationUI == null) return;
            foreach (var dropdown in configurationUI.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (dropdown.name != "ActiveRolesDropdown") continue;
                for (var parent = dropdown.transform.parent; parent != null; parent = parent.parent)
                {
                    if (parent.name != "BaseRecordSettings") continue;
                    firstRoleAvatarDropdown = dropdown;
                    return;
                }
            }
        }

        private void RefreshFirstRoleAvatarDropdown()
        {
            if (firstRoleAvatarDropdown == null || _config?.StartRoles?.Roles == null ||
                _config.StartRoles.Roles.Count == 0) return;

            firstRoleAvatarDropdown.onValueChanged.RemoveListener(OnFirstRoleAvatarChanged);
            var options = new List<string>(_availableAvatarIds);
            string avatarId = _config.StartRoles.Roles[0].AvatarId;
            // Keep an existing missing ID visible rather than silently changing it.
            if (!string.IsNullOrWhiteSpace(avatarId) && !options.Contains(avatarId)) options.Add(avatarId);
            firstRoleAvatarDropdown.ClearOptions();
            firstRoleAvatarDropdown.AddOptions(options);
            firstRoleAvatarDropdown.SetValueWithoutNotify(Mathf.Max(0, options.IndexOf(avatarId)));
            firstRoleAvatarDropdown.RefreshShownValue();
            firstRoleAvatarDropdown.interactable = _availableAvatarIds.Count > 0;
            firstRoleAvatarDropdown.onValueChanged.AddListener(OnFirstRoleAvatarChanged);
        }

        private void OnFirstRoleAvatarChanged(int index)
        {
            if (_config?.StartRoles?.Roles == null || _config.StartRoles.Roles.Count == 0 ||
                index < 0 || index >= firstRoleAvatarDropdown.options.Count) return;

            string avatarId = firstRoleAvatarDropdown.options[index].text;
            _config.StartRoles.Roles[0].AvatarId = avatarId;
            if (_rows.Count > 0) _rows[0].SetAvatarWithoutNotify(avatarId);
        }
    }
}
