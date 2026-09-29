using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using AppV2.Runtime.Scripts.Config;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class WorkshopConfigurationUI : MonoBehaviour
    {
        // ============================================================
        // ROOT
        // ============================================================

        [Header("Root")]
        [SerializeField] private GameObject menuRoot;


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
        [SerializeField] private TMP_InputField environmentIdInput;
        [SerializeField] private TMP_InputField stageSpawnIdInput;


        // ============================================================
        // PLAYER
        // ============================================================

        [Header("Player")]
        [SerializeField] private TMP_InputField playerHeightInput;
        [SerializeField] private TMP_InputField seatedPlayerHeightInput;

        [SerializeField] private TMP_InputField fallbackRootSpacingInput;
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
            if (loadPresetButton != null)
                loadPresetButton.onClick.AddListener(
                    () => LoadPresetRequested?.Invoke());

            if (savePresetButton != null)
                savePresetButton.onClick.AddListener(
                    () => SavePresetRequested?.Invoke());

            if (savePresetAsButton != null)
                savePresetAsButton.onClick.AddListener(
                    () => SavePresetAsRequested?.Invoke());

            if (startWorkshopButton != null)
                startWorkshopButton.onClick.AddListener(
                    () => StartWorkshopRequested?.Invoke());
        }


        // ============================================================
        // SHOW / HIDE
        // ============================================================

        public void Show()
        {
            if (menuRoot != null)
                menuRoot.SetActive(true);
        }

        public void Hide()
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);
        }


        // ============================================================
        // DISPLAY CONFIG
        // ============================================================

        public void DisplayConfig(PresetConfig config)
        {
            if (config == null)
                return;


            // PRESET

            presetNameInput.text =
                config.PresetName;


            // ENVIRONMENT

            environmentIdInput.text =
                config.Environment.EnvironmentId;

            stageSpawnIdInput.text =
                config.Environment.StageSpawnId;


            // PLAYER

            playerHeightInput.text =
                config.Player.HeightOfPlayerCm.ToString();

            seatedPlayerHeightInput.text =
                config.Player.HeightOfSeatedPlayerCm.ToString();

            fallbackRootSpacingInput.text =
                config.Player.FallbackRootSpacing.ToString();

            fallbackHipHeightInput.text =
                config.Player.FallbackHipHeight.ToString();

            fallbackFootSpacingInput.text =
                config.Player.FallbackFootSpacing.ToString();

            avatarBaseHeightInput.text =
                config.Player.AvatarBaseHeightCm.ToString();


            // CONTROLS

            simpleInputModeToggle.isOn =
                config.Controls.SimpleInputMode;

            fullBodyTrackersToggle.isOn =
                config.Controls.FullBodyTrackers;

            seatedModeToggle.isOn =
                config.Controls.SeatedMode;

            allowTeleportationToggle.isOn =
                config.Controls.AllowTeleportation;

            proceduralHipAndFeetMoveToggle.isOn =
                config.Controls.ProceduralHipAndFeetMove;

            selectableNextToggle.isOn =
                config.Controls.SelectableNext;

            smoothAlignInput.text =
                config.Controls.SmoothAlignSeconds.ToString();


            // CONVERSATION

            loseConversationPartnersToggle.isOn =
                config.Conversation
                    .LoseConversationPartnersIfTooFarAway;

            distanceToLosePartnerInput.text =
                config.Conversation
                    .DistanceToLoseConversationPartner
                    .ToString();

            distanceToReactivateRoleInput.text =
                config.Conversation
                    .DistanceToReactivatePassiveRole
                    .ToString();

            npcTriggerRadiusInput.text =
                config.Conversation
                    .RadiusNpcStartTalking
                    .ToString();

            maxHeightDifferenceInput.text =
                config.Conversation
                    .MaxHeightDifferenceForNpcTrigger
                    .ToString();
        }


        // ============================================================
        // READ UI -> CONFIG
        // ============================================================

        public void WriteToConfig(PresetConfig config)
        {
            if (config == null)
                return;


            // PRESET

            config.PresetName =
                presetNameInput.text;


            // ENVIRONMENT

            config.Environment.EnvironmentId =
                environmentIdInput.text;

            config.Environment.StageSpawnId =
                stageSpawnIdInput.text;


            // PLAYER

            TryReadFloat(
                playerHeightInput,
                ref config.Player.HeightOfPlayerCm);

            TryReadFloat(
                seatedPlayerHeightInput,
                ref config.Player.HeightOfSeatedPlayerCm);

            TryReadFloat(
                fallbackRootSpacingInput,
                ref config.Player.FallbackRootSpacing);

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

        public void SetPresetOptions(
            System.Collections.Generic.List<string> presetIds)
        {
            presetDropdown.ClearOptions();

            if (presetIds == null)
                return;

            presetDropdown.AddOptions(presetIds);
        }


        public string GetSelectedPresetId()
        {
            if (presetDropdown.options.Count == 0)
                return null;

            return presetDropdown.options[
                presetDropdown.value
            ].text;
        }


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

            if (float.TryParse(input.text, out float value))
                target = value;
        }
    }
}