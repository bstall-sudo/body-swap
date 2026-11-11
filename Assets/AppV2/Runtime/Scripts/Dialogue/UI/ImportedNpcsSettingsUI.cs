using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Loader;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class ImportedNpcsSettingsUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private NpcSettingsRowUI rowPrefab;
        [SerializeField] private Button addNpcButton;
        [Tooltip("Textanzeige der Anzahl aktivierter NPC-Gruppen.")]
        [SerializeField] private TMP_Text activeNpcRolesText;
        [SerializeField] private Toggle allNpcGroupsToggle;

        [SerializeField] private EnvironmentLoader environmentLoader;

        [Header("Storage")]
        [SerializeField]
        private string storageFolderName = "SessionRecordingData";

        private PresetConfig _config;

        private List<RecordedSessionOption> _sessions = new();

        private readonly List<NpcSettingsRowUI> _rows = new();

        private void Awake()
        {
            addNpcButton.onClick.AddListener(AddNpcGroup);
            BindAllGroupsToggle();
        }

        private void OnEnable()
        {
            BindAllGroupsToggle();
            RefreshGroupCount();
        }

        private void BindAllGroupsToggle()
        {
            if (allNpcGroupsToggle != null)
            {
                allNpcGroupsToggle.onValueChanged.RemoveListener(SetAllNpcGroupsEnabled);
                allNpcGroupsToggle.onValueChanged.AddListener(SetAllNpcGroupsEnabled);
            }
        }

        private void OnDestroy()
        {
            addNpcButton.onClick.RemoveListener(AddNpcGroup);
            if (allNpcGroupsToggle != null)
                allNpcGroupsToggle.onValueChanged.RemoveListener(SetAllNpcGroupsEnabled);
        }

        public void Display(PresetConfig config)
        {
            _config = config;
            BindAllGroupsToggle();

            if (_config == null)
                return;

            if (_config.NpcGroups == null)
                _config.NpcGroups = new List<NpcGroupSettings>();

            _sessions = RecordedSessionBrowser.FindSessions(
                storageFolderName
            );

            RebuildRows();
        }

        private void AddNpcGroup()
        {
            if (_config == null)
                return;

            int index = _config.NpcGroups.Count;

            var settings = new NpcGroupSettings
            {
                Enabled = true,
                NpcGroupId = $"NpcGroup{index}",

                AlignWithPlayer = true,
                CanBecomeActiveConversationPartner = true,


            };

            _config.NpcGroups.Add(settings);

            RebuildRows();
        }
        public void RefreshSpawnOptions(string environmentId)
        {
            if (environmentLoader == null)
                return;

            List<string> spawnIds =
                environmentLoader.GetSpawnIds(environmentId);

            foreach (var row in _rows)
            {
                if (row != null)
                    row.SetSpawnOptions(spawnIds);
            }
        }

        private void RebuildRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                {
                    row.Changed -= RefreshGroupCount;
                    row.DeleteRequested -= RemoveNpcGroup;
                    row.gameObject.SetActive(false);
                    Destroy(row.gameObject);
                }
            }

            _rows.Clear();

            List<string> spawnIds = environmentLoader.GetSpawnIds(
                _config.Environment.EnvironmentId
            );

            foreach (var settings in _config.NpcGroups)
            {
                var row = Instantiate(rowPrefab, rowsContainer);

                row.Initialize(settings, _sessions);
                row.SetSpawnOptions(spawnIds);
                row.Changed += RefreshGroupCount;
                row.DeleteRequested += RemoveNpcGroup;

                _rows.Add(row);
            }

            RefreshGroupCount();
        }

        private void RemoveNpcGroup(NpcGroupSettings settings)
        {
            if (_config?.NpcGroups == null || !_config.NpcGroups.Remove(settings))
                return;

            // Keep the existing zero-based naming convention contiguous.
            for (int i = 0; i < _config.NpcGroups.Count; i++)
            {
                if (_config.NpcGroups[i] != null)
                    _config.NpcGroups[i].NpcGroupId = $"NpcGroup{i}";
            }

            RebuildRows();
        }

        private void SetAllNpcGroupsEnabled(bool enabled)
        {
            if (_config?.NpcGroups == null)
                return;

            foreach (var npc in _config.NpcGroups)
            {
                if (npc != null)
                    npc.Enabled = enabled;
            }

            // Keep current rows and dropdown selections intact.
            foreach (var row in _rows)
            {
                if (row != null)
                    row.SetGroupEnabledWithoutNotify(enabled);
            }

            RefreshGroupCount();
        }

        private void RefreshGroupCount()
        {
            int enabled = 0;
            int total = 0;
            if (_config?.NpcGroups != null)
            {
                foreach (var npc in _config.NpcGroups)
                {
                    if (npc == null)
                        continue;
                    total++;
                    if (npc.Enabled)
                        enabled++;
                }
            }

            if (activeNpcRolesText != null)
                activeNpcRolesText.text = $"Aktivierte NPC-Gruppen: {enabled}";

            // Mixed selections appear unchecked. Avoid triggering a bulk change
            // when a single row changes or a preset is displayed.
            if (allNpcGroupsToggle != null)
                allNpcGroupsToggle.SetIsOnWithoutNotify(total > 0 && enabled == total);
        }

        public List<string> GetMissingSessions()
        {
            List<string> missing = new();

            if (_config?.NpcGroups == null)
                return missing;

            // Sessions erneut einlesen, nicht auf einer
            // möglicherweise veralteten Dropdown-Liste basieren.
            var availableSessions =
                RecordedSessionBrowser.FindSessions(storageFolderName);

            foreach (var npc in _config.NpcGroups)
            {
                if (npc == null || !npc.Enabled)
                    continue;

                bool found = availableSessions.Exists(s =>
                    s.WorkshopFolderName == npc.WorkshopFolderName &&
                    s.SessionFolderName == npc.SessionFolderName &&
                    s.SessionId == npc.SessionId
                );

                if (!found)
                {
                    missing.Add(
                        $"{npc.NpcGroupId}: " +
                        $"{npc.WorkshopFolderName}/" +
                        $"{npc.SessionFolderName}/" +
                        $"{npc.SessionId}"
                    );
                }
            }

            return missing;
        }
    }
}
