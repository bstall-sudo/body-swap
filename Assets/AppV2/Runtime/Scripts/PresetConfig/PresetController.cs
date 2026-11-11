using System;
using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.Dialogue.UI;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Config
{
    public class PresetController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ConversationStage conversationStage;

        private PresetStore _presetStore;

        [SerializeField]
        private WorkshopConfigurationUI configurationUI;

        private PresetConfig _currentConfig;

        public PresetConfig CurrentConfig => _currentConfig;

        public event Action WorkshopStartRequested;
        private bool _configurationInitialized;


        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            _presetStore = new PresetStore();

            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] ConversationStage is not assigned."
                );
            }
        }

        private void OnEnable()
        {
            if (configurationUI == null)
                return;

            configurationUI.LoadPresetRequested += OnLoadPresetRequested;
            configurationUI.SavePresetRequested += OnSavePresetRequested;
            configurationUI.SavePresetAsRequested += OnSavePresetAsRequested;
            configurationUI.StartWorkshopRequested += OnStartWorkshopRequested;
        }

        private void OnDisable()
        {
            if (configurationUI == null)
                return;

            configurationUI.LoadPresetRequested -= OnLoadPresetRequested;
            configurationUI.SavePresetRequested -= OnSavePresetRequested;
            configurationUI.SavePresetAsRequested -= OnSavePresetAsRequested;
            configurationUI.StartWorkshopRequested -= OnStartWorkshopRequested;
        }

        private void OnStartWorkshopRequested()
        {
            if (configurationUI.IsPlaybackSelected)
            {
                if (configurationUI.TryStartSelectedPlayback())
                {
                    Debug.Log("[PresetController] Starting PlaybackFullConversation from the selected recording.", this);
                    RememberLoadedPreset();
                    WorkshopStartRequested?.Invoke();
                }
                return;
            }

            if (_currentConfig == null)
            {
                Debug.LogError(
                    "[PresetController] Cannot start: no configuration."
                );
                return;
            }

            // Änderungen aus der UI übernehmen
            configurationUI.WriteToConfig(_currentConfig);

            // Auf ConversationStage anwenden
            if (!ApplyPresetConfig(_currentConfig))
            {
                Debug.LogError(
                    "[PresetController] Cannot start: applying preset failed."
                );
                return;
            }

            Debug.Log(
                $"[PresetController] Starting workshop: " +
                $"{_currentConfig.PresetName}"
            );

            RememberLoadedPreset();
            WorkshopStartRequested?.Invoke();
        }
        
        private string _loadedPresetId;
        private string _loadedPresetFolder;
        private const string LastPresetFolderKey = "BodySwap.LastPreset.Folder";
        private const string LastPresetFileKey = "BodySwap.LastPreset.File";

        private void RememberLoadedPreset()
        {
            if (!PresetStore.ValidName(_loadedPresetFolder) || !PresetStore.ValidName(_loadedPresetId)) return;
            PlayerPrefs.SetString(LastPresetFolderKey, _loadedPresetFolder);
            PlayerPrefs.SetString(LastPresetFileKey, _loadedPresetId);
            PlayerPrefs.Save();
        }

        private bool TryRestoreLastPreset()
        {
            string folder = PlayerPrefs.GetString(LastPresetFolderKey, "");
            string id = PlayerPrefs.GetString(LastPresetFileKey, "");
            if (!PresetStore.ValidName(folder) || !PresetStore.ValidName(id)) return false;
            string previousFolder = _presetStore.SelectedFolder;
            _presetStore.SetFolder(folder);
            var loaded = _presetStore.PresetExists(id) ? _presetStore.LoadPreset(id) : null;
            if (loaded == null)
            {
                _presetStore.SetFolder(previousFolder);
                Debug.LogWarning($"[PresetController] Last preset '{folder}/{id}' is unavailable. Using Inspector settings.", this);
                return false;
            }
            _currentConfig = loaded;
            _loadedPresetId = id;
            _loadedPresetFolder = folder;
            ApplyMenuRuntimeSettings(loaded);
            return true;
        }

        private void OnLoadPresetRequested()
        {
            string id = configurationUI.GetSelectedPresetId();
            if (string.IsNullOrEmpty(id)) return;
            string folder = _presetStore.SelectedFolder;
            configurationUI.ConfirmPresetAction(
                $"Achtung: Du lädst das Preset '{id}'. Deine aktuellen Einstellungen gehen verloren, falls du sie noch nicht gesichert hast. Fortfahren?",
                () =>
                {
                    _presetStore.SetFolder(folder);
                    var loaded = _presetStore.LoadPreset(id);
                    if (loaded == null) return;
                    _currentConfig = loaded;
                    _loadedPresetId = id;
                    _loadedPresetFolder = folder;
                    configurationUI.DisplayConfig(_currentConfig, restorePresetPlaybackSelection: true);
                    configurationUI.SetLoadedPreset(_currentConfig, true);
                    configurationUI.SelectPreset(id);
                    ApplyMenuRuntimeSettings(_currentConfig);
                    RememberLoadedPreset();
                });
        }

        private void OnSavePresetAsRequested()
        {
            if (_currentConfig == null) return;
            configurationUI.WriteToConfig(_currentConfig);
            string name = configurationUI.SaveAsPresetName;
            if (!PresetStore.ValidName(name))
            {
                Debug.LogError("[PresetController] Bitte einen gültigen Preset-Namen eingeben.", this);
                return;
            }
            var saved = JsonUtility.FromJson<PresetConfig>(JsonUtility.ToJson(_currentConfig));
            saved.PresetId = saved.PresetName = name;
            saved.PresetBaseName = configurationUI.SaveAsBaseName;
            saved.PresetSuffix = configurationUI.SaveAsSuffix;
            if (!_presetStore.SavePreset(saved, overwrite: false))
            {
                configurationUI.RefreshPresetNamePreview();
                return;
            }
            _currentConfig = saved;
            _loadedPresetId = name;
            _loadedPresetFolder = _presetStore.SelectedFolder;
            RememberLoadedPreset();
            configurationUI.DisplayConfig(saved);
            configurationUI.SetPresetOptions(_presetStore.GetAllPresetIds());
            configurationUI.SelectPreset(name);
            configurationUI.SetLoadedPreset(saved, true);
        }

        private void OnSavePresetRequested()
        {
            if (_currentConfig == null || string.IsNullOrEmpty(_loadedPresetId)) return;
            string id = _loadedPresetId;
            string folder = _loadedPresetFolder;
            configurationUI.ConfirmPresetAction(
                $"Du bist dabei, die aktuellen Einstellungen des Presets '{id}' zu überschreiben. Die vorherigen Einstellungen gehen verloren. Klicke OK, wenn du einverstanden bist. Wenn du ein neues Preset speichern möchtest, wähle Abbrechen und anschließend Speichern unter.",
                () =>
                {
                    configurationUI.WriteToConfig(_currentConfig);
                    string selectedFolder = _presetStore.SelectedFolder;
                    _presetStore.SetFolder(folder);
                    _currentConfig.PresetId = id;
                    if (!_presetStore.SavePreset(_currentConfig, overwrite: true))
                    {
                        _presetStore.SetFolder(selectedFolder);
                        return;
                    }
                    RememberLoadedPreset();
                    _presetStore.SetFolder(selectedFolder);
                    configurationUI.SetPresetOptions(_presetStore.GetAllPresetIds());
                    if (folder == selectedFolder) configurationUI.SelectPreset(id);
                    configurationUI.RefreshPresetNamePreview();
                });
        }

        public void BeginConfiguration()
        {
            if (configurationUI == null)
            {
                Debug.LogError(
                    "[PresetController] WorkshopConfigurationUI is missing."
                );
                return;
            }

            // Establish the start connection explicitly when configuration opens,
            // independently of the canvas's OnEnable ordering.
            configurationUI.StartWorkshopRequested -= OnStartWorkshopRequested;
            configurationUI.StartWorkshopRequested += OnStartWorkshopRequested;

            if (_configurationInitialized && _currentConfig != null)
            {
                // Returning from a session retains the menu's unsaved configuration.
                configurationUI.WriteToConfig(_currentConfig);
                configurationUI.DisplayConfig(_currentConfig);
                configurationUI.Show();
                return;
            }

            // Für den Moment:
            // Aktuelle Inspector-Einstellungen als Ausgangskonfiguration verwenden.
            _currentConfig = CreateConfigFromCurrentStage(
                "current",
                "Current Configuration"
            );

            if (_currentConfig == null)
            {
                Debug.LogError(
                    "[PresetController] Could not create current configuration."
                );
                return;
            }

            _presetStore.ImportLegacyPresets();
            _loadedPresetId = _loadedPresetFolder = null;
            bool restoredLastPreset = TryRestoreLastPreset();
            configurationUI.InitializePresetBrowser(_presetStore);

            // Vorhandene Presets ins Dropdown
            var presetIds = _presetStore.GetAllPresetIds();

            configurationUI.SetPresetOptions(presetIds);

            // Aktuelle Werte in UI schreiben
            configurationUI.DisplayConfig(_currentConfig);
            configurationUI.SetLoadedPreset(_currentConfig, restoredLastPreset);
            if (restoredLastPreset) configurationUI.SelectPreset(_loadedPresetId);

            configurationUI.Show();
            _configurationInitialized = true;

            Debug.Log(
                "[PresetController] Configuration UI initialized."
            );
        }


        // ============================================================
        // CREATE CONFIG FROM CURRENT CONVERSATION STAGE
        // ============================================================

        public PresetConfig CreateConfigFromCurrentStage(
            string presetId,
            string presetName)
        {
            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] Cannot create preset: " +
                    "ConversationStage is null."
                );

                return null;
            }

            PresetConfig config = new PresetConfig
            {
                PresetId = presetId,
                PresetName = presetName,
                ConfigVersion = 1
            };


            ReadEnvironmentSettings(config.Environment);
            config.Storage = new StorageSettings
            {
                StorageFolderName = conversationStage._storageFolderName,
                WorkshopFolderName = conversationStage._workshopFolderName,
                SessionFolderName = conversationStage._personalFolderName,
                SessionIdName = conversationStage._sessionIdName
            };
            config.StartInPlaybackFullConversationMode = conversationStage.StartInPlaybackFullConversationMode;

            ReadConversationSettings(config.Conversation);

            ReadPlayerSettings(config.Player);

            ReadControlSettings(config.Controls);

            ReadStartRolesSettings(config.StartRoles);

            ReadNpcGroupSettings(config.NpcGroups);


            Debug.Log(
                $"[PresetController] Created config from current stage: " +
                $"'{presetName}' ({presetId})"
            );

            return config;
        }


        // ============================================================
        // ENVIRONMENT
        // ============================================================

        private void ReadEnvironmentSettings(
            EnvironmentSettings settings)
        {
            settings.EnvironmentId =
                conversationStage.EnvironmentId;

            settings.StageSpawnId =
                conversationStage.StageSpawnId;
        }


        // ============================================================
        // CONVERSATION
        // ============================================================

        private void ReadConversationSettings(
            ConversationSettings settings)
        {
            settings.LoseConversationPartnersIfTooFarAway =
                conversationStage.loseConversationPartnersIfTooFarAway;

            settings.DistanceToLoseConversationPartner =
                conversationStage.distanceToLoseActiveConversationPartner;

            settings.DistanceToReactivatePassiveRole =
                conversationStage.distanceToReactivatePassiveRole;

            settings.RadiusNpcStartTalking =
                conversationStage._radiusNpcStartTalking;

            settings.MaxHeightDifferenceForNpcTrigger =
                conversationStage._maxHeightDifferenceForNpcTrigger;
        }


        // ============================================================
        // PLAYER
        // ============================================================

        private void ReadPlayerSettings(
            PlayerSettings settings)
        {
            settings.HeightOfPlayerCm =
                conversationStage.heightOfPlayerCm;

            settings.HeightOfSeatedPlayerCm =
                conversationStage.heightOfSeatedPlayerCm;


            settings.FallbackHipHeight =
                conversationStage.fallbackHipHeight;

            settings.FallbackFootSpacing =
                conversationStage.fallbackFootSpacing;

            settings.AvatarBaseHeightCm =
                conversationStage.avatarBaseHeightCm;
        }


        // ============================================================
        // CONTROLS
        // ============================================================

        private void ReadControlSettings(
            ControlSettings settings)
        {
            settings.SimpleInputMode =
                conversationStage.simpleInputMode;

            settings.FullBodyTrackers =
                conversationStage.FullBodyTrackers;

            settings.SeatedMode =
                conversationStage.SeatedMode;

            settings.AllowTeleportation =
                conversationStage.allowTeleportation;

            settings.ProceduralHipAndFeetMove =
                conversationStage.ProceduralHipAndFeetMove;

            settings.ProceduralStepLength =
                conversationStage._proceduralStepLength;

            settings.ProceduralStepSpeed =
                conversationStage._proceduralStepSpeed;

            settings.SmoothAlignSeconds =
                conversationStage.SmoothAlignSeconds;

            settings.SelectableNext =
                conversationStage.selectableNext;
        }


        // ============================================================
        // START ROLES
        // ============================================================

        private void ReadStartRolesSettings(
            StartRolesSettings settings)
        {
            settings.Roles.Clear();

            settings.RoleCount =
                conversationStage.roleCount;

            for (int i = 0; i < conversationStage.roleCount; i++)
            {
                RoleRig role =
                    conversationStage.roles[i];

                if (role == null)
                {
                    Debug.LogWarning(
                        $"[PresetController] Role {i} is null."
                    );

                    continue;
                }

                if (role.isActiveConversationPartner == false)
                {
                    continue;
                }

                StartRoleSettings roleSettings =
                    new StartRoleSettings
                    {
                        RoleId = role.roleId,

                        AvatarId = role.avatarId,

                        HeightOfRoleCm =
                            role.heightOfRoleCm,

                        HeightOfSeatedRoleCm =
                            role.heightOfSeatedRoleCm,

                        HasInitialStartPose =
                            role.hasInitialStartPose,

                        InitialStartPos =
                            role.initialStartPos,

                        InitialStartYawDeg =
                            role.initialStartYawDeg
                    };

                settings.Roles.Add(roleSettings);
            }

            settings.RoleCount = settings.Roles.Count;
        }


        // ============================================================
        // NPC GROUPS
        // ============================================================

        private void ReadNpcGroupSettings(
            List<NpcGroupSettings> settings)
        {
            settings.Clear();

            foreach (var npc in conversationStage.preRecordedScenes)
            {
                if (npc == null)
                    continue;

                NpcGroupSettings npcSettings =
                    new NpcGroupSettings
                    {
                        Enabled =
                            npc.enabled,

                        NpcGroupId = string.IsNullOrWhiteSpace(npc.npcGroupId) || npc.npcGroupId == "NpcGroup_"
                            ? $"NpcGroup{settings.Count}"
                            : npc.npcGroupId,



                        CanBecomeActiveConversationPartner =
                            npc.canBecomeActiveConversationPartner,

                        WorkshopFolderName =
                            npc.workshopFolderName,

                        SessionFolderName =
                            npc.sessionFolderName,

                        SessionId =
                            npc.sessionId,

                        AlignWithPlayer =
                            npc.alignWithPlayer,

                        NpcGroupSpawnId =
                            npc.npcGroupSpawnId
                    };

                settings.Add(npcSettings);
            }
        }


        // ============================================================
        // SAVE CURRENT STAGE AS PRESET
        // ============================================================

        public bool SaveCurrentStageAsPreset(
            string presetId,
            string presetName)
        {
            PresetConfig config =
                CreateConfigFromCurrentStage(
                    presetId,
                    presetName
                );

            if (config == null)
                return false;

            return _presetStore.SavePreset(config);
        }


        // ============================================================
        // LOAD PRESET
        // ============================================================

        public PresetConfig LoadPreset(string presetId)
        {
            return _presetStore.LoadPreset(presetId);
        }

        public bool ApplyPresetConfig(PresetConfig config)
        {
            if (config == null)
            {
                Debug.LogError(
                    "[PresetController] ApplyPresetConfig failed: config is null."
                );

                return false;
            }

            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] ApplyPresetConfig failed: " +
                    "ConversationStage is null."
                );

                return false;
            }

            if (!WorkshopConfigurationUI.ValidateStorageSettings(config.Storage, out string storageError))
            {
                Debug.LogError($"[PresetController] Cannot start: {storageError}", this);
                return false;
            }

            if (!conversationStage.ConfigureStartRoles(config.StartRoles?.Roles))
                return false;

            if (config.Storage?.WorkshopFolderName != null && config.Storage.SessionFolderName != null)
            {
                conversationStage._workshopFolderName = config.Storage.WorkshopFolderName.Trim();
                conversationStage._personalFolderName = config.Storage.SessionFolderName.Trim();
                if (config.Storage.SessionIdName != null) conversationStage._sessionIdName = config.Storage.SessionIdName;
            }

            ApplyEnvironmentSettings(config.Environment);
            ApplyConversationSettings(config.Conversation);
            ApplyPlayerSettings(config.Player);
            ApplyControlSettings(config.Controls);

            //ApplyStartRoleSettings(config.StartRoles);
            ApplyNpcGroupSettings(config.NpcGroups);

            Debug.Log(
                $"[PresetController] Applied preset " +
                $"'{config.PresetName}' ({config.PresetId})"
            );

            return true;
        }

        // ============================================================
        // APPLY ENVIRONMENT
        // ============================================================

        private void ApplyNpcGroupSettings(List<NpcGroupSettings> settings)
        {
            var imports = new List<PreRecordedSceneImport>();
            if (settings != null)
            {
                foreach (var npc in settings)
                {
                    if (npc == null)
                        continue;

                    imports.Add(new PreRecordedSceneImport
                    {
                        enabled = npc.Enabled,
                        npcGroupId = npc.NpcGroupId,
                        workshopFolderName = npc.WorkshopFolderName,
                        sessionFolderName = npc.SessionFolderName,
                        sessionId = npc.SessionId,
                        npcGroupSpawnId = npc.NpcGroupSpawnId,
                        alignWithPlayer = npc.AlignWithPlayer,
                        canBecomeActiveConversationPartner = npc.CanBecomeActiveConversationPartner
                    });
                }
            }

            conversationStage.preRecordedScenes = imports;
            conversationStage.preRecordedSceneCount = imports.Count;
        }

        private void ApplyEnvironmentSettings(
            EnvironmentSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] EnvironmentSettings are null."
                );
                return;
            }

            conversationStage.SetEnvironmentId(settings.EnvironmentId);
                

            conversationStage.SetStageSpawnId(settings.StageSpawnId);
        }


        // ============================================================
        // APPLY CONVERSATION
        // ============================================================

        private void ApplyConversationSettings(
            ConversationSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] ConversationSettings are null."
                );
                return;
            }

            conversationStage.loseConversationPartnersIfTooFarAway =
                settings.LoseConversationPartnersIfTooFarAway;

            conversationStage.distanceToLoseActiveConversationPartner =
                settings.DistanceToLoseConversationPartner;

            conversationStage.distanceToReactivatePassiveRole =
                settings.DistanceToReactivatePassiveRole;

            conversationStage._radiusNpcStartTalking =
                settings.RadiusNpcStartTalking;

            conversationStage._maxHeightDifferenceForNpcTrigger =
                settings.MaxHeightDifferenceForNpcTrigger;
        }


        // ============================================================
        // APPLY PLAYER
        // ============================================================

        private void ApplyPlayerSettings(
            PlayerSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] PlayerSettings are null."
                );
                return;
            }

            conversationStage.heightOfPlayerCm =
                settings.HeightOfPlayerCm;

            conversationStage.heightOfSeatedPlayerCm =
                settings.HeightOfSeatedPlayerCm;

            conversationStage.fallbackHipHeight =
                settings.FallbackHipHeight;

            conversationStage.fallbackFootSpacing =
                settings.FallbackFootSpacing;

            conversationStage.avatarBaseHeightCm =
                settings.AvatarBaseHeightCm;
        }


        // ============================================================
        // APPLY CONTROLS
        // ============================================================

        private void ApplyControlSettings(
            ControlSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] ControlSettings are null."
                );
                return;
            }

            conversationStage.simpleInputMode =
                settings.SimpleInputMode;

            conversationStage.FullBodyTrackers =
                settings.FullBodyTrackers;

            conversationStage.SeatedMode =
                settings.SeatedMode;

            conversationStage.allowTeleportation =
                settings.AllowTeleportation;

            conversationStage.ProceduralHipAndFeetMove =
                settings.ProceduralHipAndFeetMove;

            conversationStage.SmoothAlignSeconds =
                settings.SmoothAlignSeconds;

            conversationStage.selectableNext =
                settings.SelectableNext;

            conversationStage._proceduralStepSpeed = settings.ProceduralStepSpeed;
            conversationStage._proceduralStepLength = settings.ProceduralStepLength;
        }

        public void ApplyMenuRuntimeSettings(PresetConfig config)
        {
            if (conversationStage == null || config == null) return;
            ApplyPlayerSettings(config.Player);
            ApplyControlSettings(config.Controls);
            ApplyConversationSettings(config.Conversation);
        }

        [ContextMenu("TEST - Save Current Stage As Preset")]
        private void TestSaveCurrentStageAsPreset()
        {
            bool success = SaveCurrentStageAsPreset(
                "test_001",
                "Test Preset"
            );

            Debug.Log(
                success
                    ? "[PresetController] TEST preset saved successfully."
                    : "[PresetController] TEST preset save FAILED."
            );
        }

        [ContextMenu("TEST - Load Preset")]
        private void TestLoadPreset()
        {
            PresetConfig config =
                LoadPreset("test_001");

            if (config == null)
            {
                Debug.LogError(
                    "[PresetController] TEST preset could not be loaded."
                );

                return;
            }
            ApplyPresetConfig(config);

            Debug.Log(
                $"[PresetController] TEST preset loaded:\n" +
                $"Name: {config.PresetName}\n" +
                $"Environment: {config.Environment.EnvironmentId}\n" +
                $"StageSpawn: {config.Environment.StageSpawnId}\n" +
                $"PlayerHeight: {config.Player.HeightOfPlayerCm}\n" +
                $"StartRoles: {config.StartRoles.Roles.Count}\n" +
                $"NpcGroups: {config.NpcGroups.Count}"
            );
        }

       /* public void SelectRecordMode()
        {
            recordConfigurationPanel.SetActive(true);
            playbackConfigurationPanel.SetActive(false);
        }

        public void SelectPlaybackMode()
        {
            recordConfigurationPanel.SetActive(false);
            playbackConfigurationPanel.SetActive(true);
        }*/
    }
}
