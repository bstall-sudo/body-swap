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
        }

        private void OnDestroy()
        {
            addNpcButton.onClick.RemoveListener(AddNpcGroup);
        }

        public void Display(PresetConfig config)
        {
            _config = config;

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
                    Destroy(row.gameObject);
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

                _rows.Add(row);
            }
        }
    }
}