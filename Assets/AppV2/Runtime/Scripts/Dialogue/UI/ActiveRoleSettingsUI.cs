using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AppV2.Runtime.Scripts.Config;

using AppV2.Runtime.Scripts.Loader;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class ActiveRolesSettingsUI : MonoBehaviour
    {
        [Header("Avatar Catalog")]
        [SerializeField] private AvatarCatalog avatarCatalog;
        [SerializeField] private TMP_Dropdown roleCountDropdown;
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private RoleSettingsRowUI rowPrefab;

        private PresetConfig _config;
        private readonly List<RoleSettingsRowUI> _rows = new();

        private List<string> _availableAvatarIds = new();

        private bool _isUpdatingUI = false;

        private void Awake()
        {
            if (roleCountDropdown != null)
            {
                roleCountDropdown.onValueChanged.AddListener(
                    OnRoleCountChanged
                );
            }
        }

        private void OnDestroy()
        {
            if (roleCountDropdown != null)
            {
                roleCountDropdown.onValueChanged.RemoveListener(
                    OnRoleCountChanged
                );
            }
        }

        private void OnRoleCountChanged(int selectedIndex)
        {
            if (_config == null || _isUpdatingUI)
                return;

            // Dropdown-Einträge: 1, 2, 3, 4 ...
            int newRoleCount = selectedIndex + 1;

            _config.StartRoles.RoleCount = newRoleCount;

            EnsureRoleSettingsCount(newRoleCount);

            RebuildRows(_availableAvatarIds);

            Debug.Log(
                $"[ActiveRolesSettingsUI] RoleCount changed to {newRoleCount}"
            );
        }

        private void EnsureRoleSettingsCount(int requiredCount)
        {
            if (_config.StartRoles.Roles == null)
            {
                _config.StartRoles.Roles =
                    new List<StartRoleSettings>();
            }

            while (_config.StartRoles.Roles.Count < requiredCount)
            {
                int index = _config.StartRoles.Roles.Count;

                StartRoleSettings newRole = new StartRoleSettings
                {
                    RoleId = ((char)('A' + index)).ToString(),

                    AvatarId = "",

                    // 0 bedeutet: Spielergröße verwenden
                    HeightOfRoleCm = 0,
                    HeightOfSeatedRoleCm = 0
                };

                _config.StartRoles.Roles.Add(newRole);
            }
        }

        public void Display(PresetConfig config)
        {
            _config = config;

            if (_config == null || _config.StartRoles == null)
                return;

            if (avatarCatalog == null)
            {
                Debug.LogError(
                    "[ActiveRolesSettingsUI] AvatarCatalog reference missing."
                );
                return;
            }

            _availableAvatarIds = avatarCatalog.GetAllAvatarIds();

            int count = Mathf.Max(1, _config.StartRoles.RoleCount);

            _config.StartRoles.RoleCount = count;

            EnsureRoleSettingsCount(count);

            _isUpdatingUI = true;

            roleCountDropdown.SetValueWithoutNotify(count - 1);
            roleCountDropdown.RefreshShownValue();

            _isUpdatingUI = false;

            RebuildRows(_availableAvatarIds);
        }

        private void RebuildRows(List<string> avatarIds)
        {
            foreach (var row in _rows)
            {
                if (row != null)
                {
                    row.Changed -= OnRoleChanged;
                    Destroy(row.gameObject);
                }
            }

            _rows.Clear();

            int count = _config.StartRoles.RoleCount;

            for (int i = 0; i < count; i++)
            {
                StartRoleSettings role =
                    _config.StartRoles.Roles[i];

                RoleSettingsRowUI row =
                    Instantiate(rowPrefab, rowsContainer);

                row.Initialize(
                    i,
                    role.AvatarId,
                    role.HeightOfRoleCm > 0
                        ? role.HeightOfRoleCm.ToString()
                        : "",
                    role.HeightOfSeatedRoleCm > 0
                        ? role.HeightOfSeatedRoleCm.ToString()
                        : "",
                    avatarIds
                );

                row.Changed += OnRoleChanged;

                _rows.Add(row);
            }
        }

        private void OnRoleChanged(
            int index,
            string avatarId,
            string height,
            string seatedHeight)
        {
            StartRoleSettings role =
                _config.StartRoles.Roles[index];

            role.AvatarId = avatarId;

            role.HeightOfRoleCm =
                int.TryParse(height, out int h) ? h : 0;

            role.HeightOfSeatedRoleCm =
                int.TryParse(seatedHeight, out int sh) ? sh : 0;
        }
    }
}