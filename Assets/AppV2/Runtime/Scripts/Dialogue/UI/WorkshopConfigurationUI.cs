using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Config;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI : MonoBehaviour
    {
        // ============================================================
        // ROOT
        // ============================================================

        [Header("Root")]
        [SerializeField] private GameObject menuRoot;


        [SerializeField]
        private ActiveRolesSettingsUI activeRolesSettingsUI;


        // ============================================================
        // PRESET
        // ============================================================

        [Header("Preset")]
        [SerializeField] private TMP_Dropdown presetDropdown;

        [SerializeField] private Button loadPresetButton;
        [SerializeField] private Button savePresetButton;
        [SerializeField] private Button savePresetAsButton;

        [SerializeField] private TMP_InputField presetNameInput;


        // ============================================================
        // ENVIRONMENT
        // ============================================================

        [Header("Environment")]
        [SerializeField] private TMP_Dropdown environmentIdDropdown;
        [SerializeField] private TMP_Dropdown stageSpawnIdDropdown;

        [SerializeField] private EnvironmentLoader environmentLoader;

        private PresetConfig _displayedConfig;

        private List<string> _availableEnvironmentIds = new();
        private List<string> _availableStageSpawnIds = new();


        // ============================================================
        // PLAYER
        // ============================================================

        [Header("Player")]
        [SerializeField] private TMP_InputField playerHeightInput;
        [SerializeField] private TMP_InputField seatedPlayerHeightInput;
        [SerializeField] private TMP_InputField fallbackHipHeightInput;
        [SerializeField] private TMP_InputField fallbackFootSpacingInput;

        [SerializeField] private TMP_InputField avatarBaseHeightInput;


        // ============================================================
        // CONTROLS
        // ============================================================

        [Header("Controls")]
        [SerializeField] private Toggle simpleInputModeToggle;
        [SerializeField] private Toggle fullBodyTrackersToggle;
        [SerializeField] private Toggle seatedModeToggle;
        [SerializeField] private Toggle allowTeleportationToggle;
        [SerializeField] private Toggle proceduralHipAndFeetMoveToggle;
        [SerializeField] private TMP_InputField proceduralStepSpeed;
        [SerializeField] private TMP_InputField proceduralStepLength;
        [SerializeField] private Toggle selectableNextToggle;

        [SerializeField] private TMP_InputField smoothAlignInput;


        // ============================================================
        // CONVERSATION
        // ============================================================

        [Header("Conversation")]
        [SerializeField]
        private Toggle loseConversationPartnersToggle;

        [SerializeField]
        private TMP_InputField distanceToLosePartnerInput;

        [SerializeField]
        private TMP_InputField distanceToReactivateRoleInput;

        [SerializeField]
        private TMP_InputField npcTriggerRadiusInput;

        [SerializeField]
        private TMP_InputField maxHeightDifferenceInput;

        [SerializeField]
        private ImportedNpcsSettingsUI importedNpcsSettingsUI;


        // ============================================================
        // START
        // ============================================================

        [Header("Start")]
        [SerializeField] private Button startWorkshopButton;


        // ============================================================
        // EVENTS
        // ============================================================

        public event Action LoadPresetRequested;
        public event Action SavePresetRequested;
        public event Action SavePresetAsRequested;
        public event Action StartWorkshopRequested;


        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            InitializeStorageUI();
            InitializePlaybackUI();
            InitializeSettingsBindings();
            InitializePresetUI();

            BindStartButton();

            environmentIdDropdown.onValueChanged.AddListener(
                OnEnvironmentChanged
            );

            stageSpawnIdDropdown.onValueChanged.AddListener(
                OnStageSpawnChanged
            );
        }
        private void OnStartClicked()
        {
            if (_presetConfirmation != null && _presetConfirmation.activeSelf) return;
            SynchronizeSelectedStartMode();
            Debug.Log("[WorkshopConfigurationUI] START clicked.", this);
            if (!IsPlaybackSelected && importedNpcsSettingsUI != null)
            {
                List<string> missing =
                    importedNpcsSettingsUI.GetMissingSessions();

                if (missing.Count > 0)
                {
                    Debug.LogError(
                        "[WorkshopConfigurationUI] Workshop cannot start. " +
                        "Missing PreRecorded sessions:\n" +
                        string.Join("\n", missing),
                        this
                    );

                    return;
                }
            }

            if (StartWorkshopRequested == null)
            {
                Debug.LogError(
                    "[WorkshopConfigurationUI] Cannot start: no PresetController is listening to this UI.", this);
                return;
            }

            StartWorkshopRequested.Invoke();
        }

        private void BindStartButton()
        {
            if (startWorkshopButton == null)
            {
                Debug.LogError("[WorkshopConfigurationUI] Start button is not assigned.", this);
                return;
            }

            startWorkshopButton.onClick.RemoveListener(OnStartClicked);
            startWorkshopButton.onClick.AddListener(OnStartClicked);
        }

        public void RequestStart() => OnStartClicked();

        // ============================================================
        // SHOW / HIDE
        // ============================================================

        public void Show()
        {
            if (menuRoot != null)
                menuRoot.SetActive(true);

            // Rebind when opened as well: the canvas can start inactive or be
            // reconfigured while playing. Remove/Add keeps this idempotent.
            BindStartButton();
        }

        public void Hide()
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);
        }


        // ============================================================
        // DISPLAY CONFIG
        // ============================================================


        public void DisplayConfig(PresetConfig config, bool restorePresetPlaybackSelection = false)
        {
            if (config == null)
                return;

            _displayedConfig = config;
            InitializeSettingsBindings();
            DisplayStorageSettings(config);
            DisplayPlaybackSelection(restorePresetPlaybackSelection);

            // ============================================================
            // PRESET
            // ============================================================

            RefreshPresetNamePreview();

            // ============================================================
            // ENVIRONMENT
            // ============================================================

            RefreshEnvironmentDropdown();
            RefreshStageSpawnDropdown();

            // ============================================================
            // PLAYER
            // ============================================================

            playerHeightInput.SetTextWithoutNotify(
                config.Player.HeightOfPlayerCm.ToString()
            );

            seatedPlayerHeightInput.SetTextWithoutNotify(
                config.Player.HeightOfSeatedPlayerCm.ToString()
            );

            fallbackHipHeightInput.SetTextWithoutNotify(
                config.Player.FallbackHipHeight.ToString()
            );

            fallbackFootSpacingInput.SetTextWithoutNotify(
                config.Player.FallbackFootSpacing.ToString()
            );

            avatarBaseHeightInput.SetTextWithoutNotify(
                config.Player.AvatarBaseHeightCm.ToString()
            );

            // ============================================================
            // CONTROLS
            // ============================================================

            simpleInputModeToggle.SetIsOnWithoutNotify(
                config.Controls.SimpleInputMode
            );

            fullBodyTrackersToggle.SetIsOnWithoutNotify(
                config.Controls.FullBodyTrackers
            );

            seatedModeToggle.SetIsOnWithoutNotify(
                config.Controls.SeatedMode
            );

            allowTeleportationToggle.SetIsOnWithoutNotify(
                config.Controls.AllowTeleportation
            );

            proceduralHipAndFeetMoveToggle.SetIsOnWithoutNotify(
                config.Controls.ProceduralHipAndFeetMove
            );

            proceduralStepSpeed.SetTextWithoutNotify(
                config.Controls.ProceduralStepSpeed.ToString()
            );

            proceduralStepLength.SetTextWithoutNotify(
                config.Controls.ProceduralStepLength.ToString()
            );

            selectableNextToggle.SetIsOnWithoutNotify(
                config.Controls.SelectableNext
            );

            smoothAlignInput.SetTextWithoutNotify(
                config.Controls.SmoothAlignSeconds.ToString()
            );

            // ============================================================
            // CONVERSATION
            // ============================================================

            loseConversationPartnersToggle.SetIsOnWithoutNotify(
                config.Conversation.LoseConversationPartnersIfTooFarAway
            );

            distanceToLosePartnerInput.SetTextWithoutNotify(
                config.Conversation.DistanceToLoseConversationPartner.ToString()
            );

            distanceToReactivateRoleInput.SetTextWithoutNotify(
                config.Conversation.DistanceToReactivatePassiveRole.ToString()
            );

            npcTriggerRadiusInput.SetTextWithoutNotify(
                config.Conversation.RadiusNpcStartTalking.ToString()
            );

            maxHeightDifferenceInput.SetTextWithoutNotify(
                config.Conversation.MaxHeightDifferenceForNpcTrigger.ToString()
            );

            // ============================================================
            // ACTIVE ROLES
            // ============================================================

            if (activeRolesSettingsUI != null)
            {
                activeRolesSettingsUI.Display(config);
            }

            // ============================================================
            // IMPORTED NPCS
            // ============================================================

            if (importedNpcsSettingsUI != null)
            {
                importedNpcsSettingsUI.Display(config);
            }
            RefreshSettingsBindings();
        }



        // ============================================================
        // READ UI -> CONFIG
        // ============================================================

        public void WriteToConfig(PresetConfig config)
        {
            if (config == null)
                return;

            WriteStorageSettings(config);
            WritePlaybackSettings(config);


            // PRESET

            // Save-as naming inputs do not rename the currently loaded preset.


            // ENVIRONMENT



            // PLAYER

            TryReadFloat(
                playerHeightInput,
                ref config.Player.HeightOfPlayerCm);

            TryReadFloat(
                seatedPlayerHeightInput,
                ref config.Player.HeightOfSeatedPlayerCm);



            TryReadFloat(
                fallbackHipHeightInput,
                ref config.Player.FallbackHipHeight);

            TryReadFloat(
                fallbackFootSpacingInput,
                ref config.Player.FallbackFootSpacing);

            TryReadFloat(
                avatarBaseHeightInput,
                ref config.Player.AvatarBaseHeightCm);


            // CONTROLS

            config.Controls.SimpleInputMode =
                simpleInputModeToggle.isOn;

            config.Controls.FullBodyTrackers =
                fullBodyTrackersToggle.isOn;

            config.Controls.SeatedMode =
                seatedModeToggle.isOn;

            config.Controls.AllowTeleportation =
                allowTeleportationToggle.isOn;

            config.Controls.ProceduralHipAndFeetMove =
                proceduralHipAndFeetMoveToggle.isOn;

            config.Controls.SelectableNext =
                selectableNextToggle.isOn;

            TryReadFloat(
                smoothAlignInput,
                ref config.Controls.SmoothAlignSeconds);

            TryReadFloat(
                proceduralStepSpeed,
                ref config.Controls.ProceduralStepSpeed
            );

            TryReadFloat(
                proceduralStepLength,
                ref config.Controls.ProceduralStepLength
            );


            // CONVERSATION

            config.Conversation
                    .LoseConversationPartnersIfTooFarAway =
                loseConversationPartnersToggle.isOn;

            TryReadFloat(
                distanceToLosePartnerInput,
                ref config.Conversation
                    .DistanceToLoseConversationPartner);

            TryReadFloat(
                distanceToReactivateRoleInput,
                ref config.Conversation
                    .DistanceToReactivatePassiveRole);

            TryReadFloat(
                npcTriggerRadiusInput,
                ref config.Conversation
                    .RadiusNpcStartTalking);

            TryReadFloat(
                maxHeightDifferenceInput,
                ref config.Conversation
                    .MaxHeightDifferenceForNpcTrigger);
        }


        // ============================================================
        // PRESET DROPDOWN
        // ============================================================

        public void SetPresetOptions(List<string> presetIds) => RefreshPresetOptionLists(presetIds);
        public string GetSelectedPresetId() => _selectedPresetId;
        public void SelectPreset(string presetId) => SelectSharedPreset(presetId);


        // ============================================================
        // HELPERS
        // ============================================================

        private void TryReadInt(
            TMP_InputField input,
            ref int target)
        {
            if (input == null)
                return;

            if (int.TryParse(input.text, out int value))
                target = value;
        }


        private void TryReadFloat(
            TMP_InputField input,
            ref float target)
        {
            if (input == null)
                return;

            if (TryParseSettingFloat(input.text, out float value))
                target = value;
        }

        private void OnDestroy()
        {
            CleanupStorageUI();
            CleanupPlaybackUI();
            CleanupSettingsBindings();
            CleanupPresetUI();
            environmentIdDropdown.onValueChanged.RemoveListener(
                OnEnvironmentChanged
            );

            stageSpawnIdDropdown.onValueChanged.RemoveListener(
                OnStageSpawnChanged
            );
        }

        private void RefreshEnvironmentDropdown()
        {
            if (_displayedConfig == null || environmentLoader == null)
                return;

            _availableEnvironmentIds =
                environmentLoader.GetEnvironmentIds();

            SetDropdownOptions(
                environmentIdDropdown,
                _availableEnvironmentIds,
                _displayedConfig.Environment.EnvironmentId
            );
        }

        private void RefreshStageSpawnDropdown()
        {
            if (_displayedConfig == null || environmentLoader == null)
                return;

            _availableStageSpawnIds =
                environmentLoader.GetSpawnIds(
                    _displayedConfig.Environment.EnvironmentId
                );

            SetDropdownOptions(
                stageSpawnIdDropdown,
                _availableStageSpawnIds,
                _displayedConfig.Environment.StageSpawnId
            );
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

            int selectedIndex = 0;

            // Gespeicherte ID ist vorhanden
            int index = values.IndexOf(selectedValue);

            if (index >= 0)
            {
                options.AddRange(values);
                selectedIndex = index + 1;
            }
            // Gespeicherte ID existiert im aktuellen Environment nicht
            else if (!string.IsNullOrWhiteSpace(selectedValue))
            {
                options.Add($"<color=#E53935>Nicht gefunden:  {selectedValue}</color>");
                options.AddRange(values);
                selectedIndex = 1;
            }
            else
            {
                options.AddRange(values);
            }

            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.RefreshShownValue();
        }

        private void OnEnvironmentChanged(int index)
        {
            if (_displayedConfig == null)
                return;

            string environmentId =
                index > 0 && index <= _availableEnvironmentIds.Count
                    ? _availableEnvironmentIds[index - 1]
                    : "";

            _displayedConfig.Environment.EnvironmentId = environmentId;

            // SpawnIds aktualisieren, ohne gespeicherte Werte zu löschen
            RefreshStageSpawnDropdown();

            if (importedNpcsSettingsUI != null)
            {
                importedNpcsSettingsUI.RefreshSpawnOptions(environmentId);
            }
        }

        private void OnStageSpawnChanged(int index)
        {
            if (_displayedConfig == null)
                return;

            string selectedValue =
                stageSpawnIdDropdown.options[index].text;

            if (index == 0)
            {
                // Benutzer hat bewusst "Bitte auswählen..." gewählt
                _displayedConfig.Environment.StageSpawnId = "";
            }
            else if (_availableStageSpawnIds.Contains(selectedValue))
            {
                _displayedConfig.Environment.StageSpawnId = selectedValue;
            }
            // Bei "Nicht gefunden: ..." bleibt der alte Wert erhalten
        }
    }
}
