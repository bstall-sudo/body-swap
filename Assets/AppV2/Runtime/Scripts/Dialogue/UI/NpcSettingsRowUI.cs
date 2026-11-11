using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using System.Linq;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class NpcSettingsRowUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Toggle npcEnabled;
        [SerializeField] private TMP_Text npcGroupId;
        [SerializeField] private TMP_Dropdown workshopSelection;
        [SerializeField] private TMP_Dropdown sessionFolderSelection;
        [SerializeField] private TMP_Dropdown sessionSelection;
        [SerializeField] private TMP_Dropdown npcSpawnSelection;
        [SerializeField] private Toggle alignWithPlayerInput;
        [SerializeField] private Toggle canBecomeActiveRoleInput;
        [SerializeField] private Button deleteNpcGroupButton;


        private List<string> _workshopNames = new();
        private List<string> _sessionFolderNames = new();
        private List<string> _sessionIds = new();

        private List<string> _availableSpawnIds = new();
        private NpcGroupSettings _settings;
        private List<RecordedSessionOption> _sessions;


        public event Action Changed;
        public event Action<NpcGroupSettings> DeleteRequested;

        public void Initialize(
            NpcGroupSettings settings,
            List<RecordedSessionOption> sessions)
        {
            _settings = settings;
            _sessions = sessions ?? new List<RecordedSessionOption>();

            // Also support newly added prefab buttons before the field is assigned.
            if (deleteNpcGroupButton == null)
            {
                foreach (var button in GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "DeleteNpcGroup")
                    {
                        deleteNpcGroupButton = button;
                        break;
                    }
                }
            }
            if (deleteNpcGroupButton != null)
            {
                deleteNpcGroupButton.onClick.RemoveListener(OnDeleteClicked);
                deleteNpcGroupButton.onClick.AddListener(OnDeleteClicked);
            }

            // NPC-Gruppe anzeigen
            npcGroupId.text = settings.NpcGroupId;

            // Alte Listener entfernen
            npcEnabled.onValueChanged.RemoveListener(OnChanged);
            alignWithPlayerInput.onValueChanged.RemoveListener(OnChanged);
            canBecomeActiveRoleInput.onValueChanged.RemoveListener(OnChanged);

            workshopSelection.onValueChanged.RemoveListener(OnWorkshopChanged);
            sessionFolderSelection.onValueChanged.RemoveListener(OnSessionFolderChanged);
            sessionSelection.onValueChanged.RemoveListener(OnSessionChanged);

            // Toggles aus Preset übernehmen
            npcEnabled.SetIsOnWithoutNotify(settings.Enabled);

            alignWithPlayerInput.SetIsOnWithoutNotify(
                settings.AlignWithPlayer
            );

            canBecomeActiveRoleInput.SetIsOnWithoutNotify(
                settings.CanBecomeActiveConversationPartner
            );

            // Drei Dropdowns befüllen
            RefreshWorkshopDropdown();
            RefreshSessionFolderDropdown();
            RefreshSessionDropdown();

            // Listener wieder registrieren
            npcEnabled.onValueChanged.AddListener(OnChanged);
            alignWithPlayerInput.onValueChanged.AddListener(OnChanged);
            canBecomeActiveRoleInput.onValueChanged.AddListener(OnChanged);

            workshopSelection.onValueChanged.AddListener(OnWorkshopChanged);
            sessionFolderSelection.onValueChanged.AddListener(OnSessionFolderChanged);
            sessionSelection.onValueChanged.AddListener(OnSessionChanged);
        }

        public void SetSpawnOptions(List<string> spawnIds)
        {
            _availableSpawnIds = spawnIds ?? new List<string>();

            npcSpawnSelection.onValueChanged.RemoveListener(
                OnSpawnChanged
            );

            List<string> options = new()
            {
                "<color=#E53935>Bitte auswählen...</color>"
            };

            string savedSpawnId = _settings.NpcGroupSpawnId;

            int index = _availableSpawnIds.IndexOf(savedSpawnId);
            int selectedIndex = 0;

            if (index >= 0)
            {
                options.AddRange(_availableSpawnIds);
                selectedIndex = index + 1;
            }
            else if (!string.IsNullOrWhiteSpace(savedSpawnId))
            {
                options.Add($"Nicht gefunden: {savedSpawnId}");
                options.AddRange(_availableSpawnIds);
                selectedIndex = 1;
            }
            else
            {
                options.AddRange(_availableSpawnIds);
            }

            npcSpawnSelection.ClearOptions();
            npcSpawnSelection.AddOptions(options);

            npcSpawnSelection.SetValueWithoutNotify(selectedIndex);
            npcSpawnSelection.RefreshShownValue();

            npcSpawnSelection.onValueChanged.AddListener(
                OnSpawnChanged
            );
        }

        private void OnSpawnChanged(int index)
        {
            if (_settings == null)
                return;

            string selectedValue =
                npcSpawnSelection.options[index].text;

            if (index == 0)
            {
                _settings.NpcGroupSpawnId = "";
            }
            else if (_availableSpawnIds.Contains(selectedValue))
            {
                _settings.NpcGroupSpawnId = selectedValue;
            }
            // Ungültige gespeicherte Auswahl nicht überschreiben

            Changed?.Invoke();
        }


        private void SetDropdownOptions(
            TMP_Dropdown dropdown,
            List<string> values,
            string selectedValue)
        {
            dropdown.ClearOptions();

            List<string> options = new()
            {
                "<color=#E53935>Bitte auswählen...</color>"
            };

            options.AddRange(values);

            dropdown.AddOptions(options);

            int index = values.IndexOf(selectedValue);

            dropdown.SetValueWithoutNotify(
                index >= 0 ? index + 1 : 0
            );

            dropdown.RefreshShownValue();
        }

        private void OnWorkshopChanged(int index)
        {
            _settings.WorkshopFolderName =
                index > 0 ? _workshopNames[index - 1] : "";

            // Untergeordnete Auswahl zurücksetzen
            _settings.SessionFolderName = "";
            _settings.SessionId = "";

            RefreshSessionFolderDropdown();
            RefreshSessionDropdown();

            Changed?.Invoke();
        }

        private void OnSessionFolderChanged(int index)
        {
            _settings.SessionFolderName =
                index > 0 ? _sessionFolderNames[index - 1] : "";

            _settings.SessionId = "";

            RefreshSessionDropdown();

            Changed?.Invoke();
        }

        private void OnSessionChanged(int index)
        {
            _settings.SessionId =
                index > 0 ? _sessionIds[index - 1] : "";

            Changed?.Invoke();
        }
        private void RefreshWorkshopDropdown()
        {
            _workshopNames = _sessions
                .Select(s => s.WorkshopFolderName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            SetDropdownOptions(
                workshopSelection,
                _workshopNames,
                _settings.WorkshopFolderName
            );
        }

        private void RefreshSessionFolderDropdown()
        {
            _sessionFolderNames = _sessions
                .Where(s =>
                    s.WorkshopFolderName == _settings.WorkshopFolderName)
                .Select(s => s.SessionFolderName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            SetDropdownOptions(
                sessionFolderSelection,
                _sessionFolderNames,
                _settings.SessionFolderName
            );
        }

        private void RefreshSessionDropdown()
        {
            _sessionIds = _sessions
                .Where(s =>
                    s.WorkshopFolderName == _settings.WorkshopFolderName &&
                    s.SessionFolderName == _settings.SessionFolderName)
                .Select(s => s.SessionId)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            SetDropdownOptions(
                sessionSelection,
                _sessionIds,
                _settings.SessionId
            );
        }


        public void SetGroupEnabledWithoutNotify(bool enabled)
        {
            if (_settings == null)
                return;

            _settings.Enabled = enabled;
            npcEnabled.SetIsOnWithoutNotify(enabled);
        }

        private void OnChanged(bool unused)
        {
            _settings.Enabled = npcEnabled.isOn;

            _settings.AlignWithPlayer =
                alignWithPlayerInput.isOn;

            _settings.CanBecomeActiveConversationPartner =
                canBecomeActiveRoleInput.isOn;

            Changed?.Invoke();
        }

        private void OnDeleteClicked()
        {
            if (_settings != null)
                DeleteRequested?.Invoke(_settings);
        }

        private void OnDestroy()
        {
            if (deleteNpcGroupButton != null)
                deleteNpcGroupButton.onClick.RemoveListener(OnDeleteClicked);
            npcEnabled.onValueChanged.RemoveListener(OnChanged);
            alignWithPlayerInput.onValueChanged.RemoveListener(OnChanged);
            canBecomeActiveRoleInput.onValueChanged.RemoveListener(OnChanged);

            workshopSelection.onValueChanged.RemoveListener(OnWorkshopChanged);
            sessionFolderSelection.onValueChanged.RemoveListener(OnSessionFolderChanged);
            sessionSelection.onValueChanged.RemoveListener(OnSessionChanged);

            npcSpawnSelection.onValueChanged.RemoveListener(OnSpawnChanged);
        }
    }
}
