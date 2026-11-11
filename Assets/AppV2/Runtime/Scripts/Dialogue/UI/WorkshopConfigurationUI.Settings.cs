using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI
    {
        private enum FloatSetting { PlayerHeight, SeatedPlayerHeight, FallbackHipHeight, FallbackFootSpacing, AvatarBaseHeight, StepSpeed, StepLength, SmoothAlign, LosePartnerDistance, ReactivateDistance, NpcRadius, NpcHeightDifference }
        private enum ToggleSetting { SimpleInput, FullBodyTrackers, SeatedMode, AllowTeleportation, ProceduralHipAndFeetMove, SelectableNext, LosePartners }
        [Serializable] private class FloatBinding
        {
            public FloatSetting setting;
            public TMP_InputField[] fields;
        }
        [Serializable] private class ToggleBinding
        {
            public ToggleSetting setting;
            public Toggle[] fields;
        }
        private readonly List<FloatBinding> floatSettingBindings = new();
        private readonly List<ToggleBinding> toggleSettingBindings = new();
        private readonly List<Action> _settingsListenerCleanup = new();

        private void InitializeSettingsBindings()
        {
            CleanupSettingsBindings();
            DiscoverSettingsBindings();
            foreach (var binding in floatSettingBindings)
            {
                if (binding.fields == null) continue;
                foreach (var field in binding.fields)
                {
                    if (field == null) continue;
                    UnityAction<string> changed = text =>
                    {
                        if (_displayedConfig == null || !TryParseSettingFloat(text, out float value) ||
                            !IsValidSetting(binding.setting, value)) return;
                        SetFloatSetting(binding.setting, value);
                        foreach (var mirror in binding.fields)
                            if (mirror != null && mirror != field) mirror.SetTextWithoutNotify(text);
                        ApplyEditedRuntimeSettings();
                    };
                    UnityAction<string> finished = text =>
                    {
                        if (_displayedConfig == null) return;
                        // Restore the last valid value after an incomplete/invalid edit.
                        foreach (var mirror in binding.fields)
                            if (mirror != null) mirror.SetTextWithoutNotify(GetFloatSetting(binding.setting).ToString(CultureInfo.CurrentCulture));
                    };
                    field.onValueChanged.AddListener(changed);
                    field.onEndEdit.AddListener(finished);
                    _settingsListenerCleanup.Add(() =>
                    {
                        if (field == null) return;
                        field.onValueChanged.RemoveListener(changed);
                        field.onEndEdit.RemoveListener(finished);
                    });
                }
            }
            foreach (var binding in toggleSettingBindings)
            {
                if (binding.fields == null) continue;
                foreach (var field in binding.fields)
                {
                    if (field == null) continue;
                    UnityAction<bool> changed = value =>
                    {
                        if (_displayedConfig == null) return;
                        SetToggleSetting(binding.setting, value);
                        foreach (var mirror in binding.fields)
                            if (mirror != null && mirror != field) mirror.SetIsOnWithoutNotify(value);
                        ApplyEditedRuntimeSettings();
                    };
                    field.onValueChanged.AddListener(changed);
                    _settingsListenerCleanup.Add(() => { if (field != null) field.onValueChanged.RemoveListener(changed); });
                }
            }
        }

        private void DiscoverSettingsBindings()
        {
            // Rebuild from the current canvas rather than relying on scene lists
            // that may be absent when Unity saves an already-open scene.
            var inputs = new Dictionary<FloatSetting, List<TMP_InputField>>();
            var toggles = new Dictionary<ToggleSetting, List<Toggle>>();
            var root = menuRoot != null ? menuRoot : gameObject;
            foreach (var field in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                FloatSetting? setting = ResolveFloatSetting(field);
                if (!setting.HasValue) continue;
                if (!inputs.TryGetValue(setting.Value, out var fields))
                    inputs[setting.Value] = fields = new List<TMP_InputField>();
                fields.Add(field);
            }
            foreach (var field in root.GetComponentsInChildren<Toggle>(true))
            {
                ToggleSetting? setting = ResolveToggleSetting(field);
                if (!setting.HasValue) continue;
                if (!toggles.TryGetValue(setting.Value, out var fields))
                    toggles[setting.Value] = fields = new List<Toggle>();
                fields.Add(field);
            }
            floatSettingBindings.Clear();
            toggleSettingBindings.Clear();
            foreach (var pair in inputs)
                floatSettingBindings.Add(new FloatBinding { setting = pair.Key, fields = pair.Value.ToArray() });
            foreach (var pair in toggles)
                toggleSettingBindings.Add(new ToggleBinding { setting = pair.Key, fields = pair.Value.ToArray() });
        }

        private static string SettingName(string name) => name.ToLowerInvariant()
            .Replace("_inputfield", "").Replace("_toggle", "").Replace(" (tmp)", "").Replace("_", "").Replace(" ", "");

        private FloatSetting? ResolveFloatSetting(TMP_InputField field)
        {
            if (field == playerHeightInput) return FloatSetting.PlayerHeight;
            if (field == seatedPlayerHeightInput) return FloatSetting.SeatedPlayerHeight;
            if (field == fallbackHipHeightInput) return FloatSetting.FallbackHipHeight;
            if (field == fallbackFootSpacingInput) return FloatSetting.FallbackFootSpacing;
            if (field == avatarBaseHeightInput) return FloatSetting.AvatarBaseHeight;
            if (field == proceduralStepSpeed) return FloatSetting.StepSpeed;
            if (field == proceduralStepLength) return FloatSetting.StepLength;
            if (field == smoothAlignInput) return FloatSetting.SmoothAlign;
            if (field == distanceToLosePartnerInput) return FloatSetting.LosePartnerDistance;
            if (field == distanceToReactivateRoleInput) return FloatSetting.ReactivateDistance;
            if (field == npcTriggerRadiusInput) return FloatSetting.NpcRadius;
            if (field == maxHeightDifferenceInput) return FloatSetting.NpcHeightDifference;

            string parent = field.transform.parent != null ? SettingName(field.transform.parent.name) : "";
            // This height-difference field is currently also named NpcTriggerRadius.
            if (parent.Contains("maxheight")) return FloatSetting.NpcHeightDifference;
            switch (SettingName(field.name))
            {
                case "playerheight": return FloatSetting.PlayerHeight;
                case "seatedplayerheight": case "playerheightseated": return FloatSetting.SeatedPlayerHeight;
                case "fallbackhipheight": return FloatSetting.FallbackHipHeight;
                case "fallbackfootspacing": return FloatSetting.FallbackFootSpacing;
                case "avatarbaseheight": return FloatSetting.AvatarBaseHeight;
                case "proceduralstepspeed": return FloatSetting.StepSpeed;
                case "proceduralsteplength": return FloatSetting.StepLength;
                case "smoothalign": return FloatSetting.SmoothAlign;
                case "distancetoloseconversationpartner": case "distancetoloseparter": case "distancetolosepartner": return FloatSetting.LosePartnerDistance;
                case "distancetoreactivatepassiverole": return FloatSetting.ReactivateDistance;
                case "npcradiusstarttalking": case "npctriggerradius": return FloatSetting.NpcRadius;
                case "maxheightdifferencenpcstarttalking": return FloatSetting.NpcHeightDifference;
                default: return null;
            }
        }

        private ToggleSetting? ResolveToggleSetting(Toggle field)
        {
            if (field == simpleInputModeToggle) return ToggleSetting.SimpleInput;
            if (field == fullBodyTrackersToggle) return ToggleSetting.FullBodyTrackers;
            if (field == seatedModeToggle) return ToggleSetting.SeatedMode;
            if (field == allowTeleportationToggle) return ToggleSetting.AllowTeleportation;
            if (field == proceduralHipAndFeetMoveToggle) return ToggleSetting.ProceduralHipAndFeetMove;
            if (field == selectableNextToggle) return ToggleSetting.SelectableNext;
            if (field == loseConversationPartnersToggle) return ToggleSetting.LosePartners;
            switch (SettingName(field.name))
            {
                case "simpleinput": case "simpleinputmode": return ToggleSetting.SimpleInput;
                case "fullbodytrackers": return ToggleSetting.FullBodyTrackers;
                case "seatedmode": return ToggleSetting.SeatedMode;
                case "allowteleportation": return ToggleSetting.AllowTeleportation;
                case "proceduralhipandfeetmove": return ToggleSetting.ProceduralHipAndFeetMove;
                case "selectablenext": return ToggleSetting.SelectableNext;
                case "loseconversationpartner": case "loseconversationpartners": return ToggleSetting.LosePartners;
                default: return null;
            }
        }

        private void CleanupSettingsBindings()
        {
            foreach (var cleanup in _settingsListenerCleanup) cleanup();
            _settingsListenerCleanup.Clear();
        }

        private void ApplyEditedRuntimeSettings() => GetComponent<PresetController>()?.ApplyMenuRuntimeSettings(_displayedConfig);

        private void RefreshSettingsBindings()
        {
            if (_displayedConfig == null) return;
            foreach (var binding in floatSettingBindings)
                if (binding.fields != null)
                    foreach (var field in binding.fields)
                        if (field != null) field.SetTextWithoutNotify(GetFloatSetting(binding.setting).ToString(CultureInfo.CurrentCulture));
            foreach (var binding in toggleSettingBindings)
                if (binding.fields != null)
                    foreach (var field in binding.fields)
                        if (field != null) field.SetIsOnWithoutNotify(GetToggleSetting(binding.setting));
        }

        private static bool TryParseSettingFloat(string text, out float value)
        {
            bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return parsed && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidSetting(FloatSetting setting, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return setting == FloatSetting.PlayerHeight || setting == FloatSetting.SeatedPlayerHeight ||
                setting == FloatSetting.AvatarBaseHeight ? value > 0 : value >= 0;
        }

        private float GetFloatSetting(FloatSetting setting)
        {
            switch (setting)
            {
                case FloatSetting.PlayerHeight: return _displayedConfig.Player.HeightOfPlayerCm;
                case FloatSetting.SeatedPlayerHeight: return _displayedConfig.Player.HeightOfSeatedPlayerCm;
                case FloatSetting.FallbackHipHeight: return _displayedConfig.Player.FallbackHipHeight;
                case FloatSetting.FallbackFootSpacing: return _displayedConfig.Player.FallbackFootSpacing;
                case FloatSetting.AvatarBaseHeight: return _displayedConfig.Player.AvatarBaseHeightCm;
                case FloatSetting.StepSpeed: return _displayedConfig.Controls.ProceduralStepSpeed;
                case FloatSetting.StepLength: return _displayedConfig.Controls.ProceduralStepLength;
                case FloatSetting.SmoothAlign: return _displayedConfig.Controls.SmoothAlignSeconds;
                case FloatSetting.LosePartnerDistance: return _displayedConfig.Conversation.DistanceToLoseConversationPartner;
                case FloatSetting.ReactivateDistance: return _displayedConfig.Conversation.DistanceToReactivatePassiveRole;
                case FloatSetting.NpcRadius: return _displayedConfig.Conversation.RadiusNpcStartTalking;
                case FloatSetting.NpcHeightDifference: return _displayedConfig.Conversation.MaxHeightDifferenceForNpcTrigger;
                default: throw new ArgumentOutOfRangeException(nameof(setting));
            }
        }
        private void SetFloatSetting(FloatSetting setting, float value)
        {
            switch (setting)
            {
                case FloatSetting.PlayerHeight: _displayedConfig.Player.HeightOfPlayerCm = value; break;
                case FloatSetting.SeatedPlayerHeight: _displayedConfig.Player.HeightOfSeatedPlayerCm = value; break;
                case FloatSetting.FallbackHipHeight: _displayedConfig.Player.FallbackHipHeight = value; break;
                case FloatSetting.FallbackFootSpacing: _displayedConfig.Player.FallbackFootSpacing = value; break;
                case FloatSetting.AvatarBaseHeight: _displayedConfig.Player.AvatarBaseHeightCm = value; break;
                case FloatSetting.StepSpeed: _displayedConfig.Controls.ProceduralStepSpeed = value; break;
                case FloatSetting.StepLength: _displayedConfig.Controls.ProceduralStepLength = value; break;
                case FloatSetting.SmoothAlign: _displayedConfig.Controls.SmoothAlignSeconds = value; break;
                case FloatSetting.LosePartnerDistance: _displayedConfig.Conversation.DistanceToLoseConversationPartner = value; break;
                case FloatSetting.ReactivateDistance: _displayedConfig.Conversation.DistanceToReactivatePassiveRole = value; break;
                case FloatSetting.NpcRadius: _displayedConfig.Conversation.RadiusNpcStartTalking = value; break;
                case FloatSetting.NpcHeightDifference: _displayedConfig.Conversation.MaxHeightDifferenceForNpcTrigger = value; break;
            }
        }
        private bool GetToggleSetting(ToggleSetting setting)
        {
            switch (setting)
            {
                case ToggleSetting.SimpleInput: return _displayedConfig.Controls.SimpleInputMode;
                case ToggleSetting.FullBodyTrackers: return _displayedConfig.Controls.FullBodyTrackers;
                case ToggleSetting.SeatedMode: return _displayedConfig.Controls.SeatedMode;
                case ToggleSetting.AllowTeleportation: return _displayedConfig.Controls.AllowTeleportation;
                case ToggleSetting.ProceduralHipAndFeetMove: return _displayedConfig.Controls.ProceduralHipAndFeetMove;
                case ToggleSetting.SelectableNext: return _displayedConfig.Controls.SelectableNext;
                case ToggleSetting.LosePartners: return _displayedConfig.Conversation.LoseConversationPartnersIfTooFarAway;
                default: throw new ArgumentOutOfRangeException(nameof(setting));
            }
        }
        private void SetToggleSetting(ToggleSetting setting, bool value)
        {
            switch (setting)
            {
                case ToggleSetting.SimpleInput: _displayedConfig.Controls.SimpleInputMode = value; break;
                case ToggleSetting.FullBodyTrackers: _displayedConfig.Controls.FullBodyTrackers = value; break;
                case ToggleSetting.SeatedMode: _displayedConfig.Controls.SeatedMode = value; break;
                case ToggleSetting.AllowTeleportation: _displayedConfig.Controls.AllowTeleportation = value; break;
                case ToggleSetting.ProceduralHipAndFeetMove: _displayedConfig.Controls.ProceduralHipAndFeetMove = value; break;
                case ToggleSetting.SelectableNext: _displayedConfig.Controls.SelectableNext = value; break;
                case ToggleSetting.LosePartners: _displayedConfig.Conversation.LoseConversationPartnersIfTooFarAway = value; break;
            }
        }
    }
}
