using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI
    {
        [Header("Playback selection")]
        [SerializeField] private TMP_Dropdown playbackWorkshopDropdown;
        [SerializeField] private TMP_Dropdown playbackSessionFolderDropdown;
        [SerializeField] private TMP_Dropdown playbackSceneDropdown;
        [SerializeField] private TMP_Dropdown playbackModeDropdown;
        private List<string> _playbackWorkshops = new();
        private List<string> _playbackFolders = new();
        private List<string> _playbackSessions = new();
        private string _playbackWorkshop;
        private string _playbackFolder;
        private string _playbackSession;
        private bool _updatingPlayback;
        public bool IsPlaybackSelected => playbackModeDropdown != null
            ? playbackModeDropdown.value == 1
            : storageStage != null && storageStage.StartInPlaybackFullConversationMode;

        private void InitializePlaybackUI()
        {
            if (playbackModeDropdown == null)
            {
                foreach (var dropdown in GetComponentsInChildren<TMP_Dropdown>(true))
                    if (dropdown.name == "StartModeDropdown") playbackModeDropdown = dropdown;
            }
            if (playbackModeDropdown != null) playbackModeDropdown.onValueChanged.AddListener(OnPlaybackModeSelected);
            if (playbackWorkshopDropdown != null) playbackWorkshopDropdown.onValueChanged.AddListener(OnPlaybackWorkshopChanged);
            if (playbackSessionFolderDropdown != null) playbackSessionFolderDropdown.onValueChanged.AddListener(OnPlaybackFolderChanged);
            if (playbackSceneDropdown != null) playbackSceneDropdown.onValueChanged.AddListener(OnPlaybackSceneChanged);
        }

        private void CleanupPlaybackUI()
        {
            if (playbackModeDropdown != null) playbackModeDropdown.onValueChanged.RemoveListener(OnPlaybackModeSelected);
            if (playbackWorkshopDropdown != null) playbackWorkshopDropdown.onValueChanged.RemoveListener(OnPlaybackWorkshopChanged);
            if (playbackSessionFolderDropdown != null) playbackSessionFolderDropdown.onValueChanged.RemoveListener(OnPlaybackFolderChanged);
            if (playbackSceneDropdown != null) playbackSceneDropdown.onValueChanged.RemoveListener(OnPlaybackSceneChanged);
        }

        public void SetPlaybackMode(bool playback)
        {
            if (storageStage != null) storageStage.StartInPlaybackFullConversationMode = playback;
            if (playbackModeDropdown != null) playbackModeDropdown.SetValueWithoutNotify(playback ? 1 : 0);
        }

        private void OnPlaybackModeSelected(int index) => SetPlaybackMode(index == 1);

        public void SynchronizeSelectedStartMode() => SetPlaybackMode(IsPlaybackSelected);

        private string PlaybackRoot => Path.Combine(Application.persistentDataPath,
            storageStage != null ? storageStage._storageFolderName : "SessionRecordingData");

        private void DisplayPlaybackSelection(bool restorePresetSelection = false)
        {
            if (storageStage == null) return;
            string key = $"BodySwap.LastRecording.{storageStage._storageFolderName}.";
            _playbackWorkshop = PlayerPrefs.GetString(key + "Workshop", storageStage._workshopFolderName);
            _playbackFolder = PlayerPrefs.GetString(key + "Folder", storageStage._personalFolderName);
            _playbackSession = PlayerPrefs.GetString(key + "Session", storageStage._sessionIdName);
            bool rememberedExists = IsFolderName(_playbackWorkshop) && IsFolderName(_playbackFolder) &&
                IsFolderName(_playbackSession) && File.Exists(Path.Combine(PlaybackRoot, _playbackWorkshop, _playbackFolder, _playbackSession, "session.json"));
            if (!rememberedExists)
            {
                // Existing recordings predate the remembered selection: find the
                // newest saved session across workshops, not by folder-name order.
                var session = RecordedSessionBrowser.FindLatestSession(storageStage._storageFolderName);
                if (session != null)
                {
                    _playbackWorkshop = session.WorkshopFolderName;
                    _playbackFolder = session.SessionFolderName;
                    _playbackSession = session.SessionId;
                }
            }
            // Startup/menu return prefers the last recording across all folders.
            // A confirmed explicit preset load may restore its saved selection.
            if (restorePresetSelection && !string.IsNullOrEmpty(_displayedConfig?.Playback?.SessionId))
            {
                _playbackWorkshop = _displayedConfig.Playback.WorkshopFolderName;
                _playbackFolder = _displayedConfig.Playback.SessionFolderName;
                _playbackSession = _displayedConfig.Playback.SessionId;
            }
            if (_displayedConfig != null) SetPlaybackMode(_displayedConfig.StartInPlaybackFullConversationMode);
            _playbackWorkshops = ReadFolderNames(PlaybackRoot);
            _playbackWorkshop = SetPlaybackOptions(playbackWorkshopDropdown, _playbackWorkshops, _playbackWorkshop);
            RefreshPlaybackFolders();
            if (playbackModeDropdown != null) playbackModeDropdown.SetValueWithoutNotify(IsPlaybackSelected ? 1 : 0);
        }

        private string SetPlaybackOptions(TMP_Dropdown dropdown, List<string> values, string selected)
        {
            int index = values.IndexOf(selected);
            if (index < 0) index = 0;
            if (dropdown != null)
            {
                _updatingPlayback = true;
                try
                {
                    dropdown.ClearOptions();
                    dropdown.AddOptions(values.Count > 0 ? values : new List<string> { "Bitte auswählen..." });
                    dropdown.SetValueWithoutNotify(index);
                    dropdown.RefreshShownValue();
                    dropdown.interactable = values.Count > 0;
                }
                finally { _updatingPlayback = false; }
            }
            return values.Count > 0 ? values[index] : null;
        }

        private void RefreshPlaybackFolders()
        {
            _playbackFolders = IsFolderName(_playbackWorkshop) ? ReadFolderNames(Path.Combine(PlaybackRoot, _playbackWorkshop)) : new List<string>();
            _playbackFolder = SetPlaybackOptions(playbackSessionFolderDropdown, _playbackFolders, _playbackFolder);
            RefreshPlaybackSessions();
        }

        private void RefreshPlaybackSessions()
        {
            _playbackSessions = IsFolderName(_playbackWorkshop) && IsFolderName(_playbackFolder)
                ? ReadFolderNames(Path.Combine(PlaybackRoot, _playbackWorkshop, _playbackFolder)) : new List<string>();
            if (!_playbackSessions.Contains(_playbackSession) && IsFolderName(_playbackWorkshop) && IsFolderName(_playbackFolder))
                _playbackSession = new SessionStore(storageStage._storageFolderName, _playbackWorkshop, _playbackFolder).GetLatestSessionId();
            _playbackSession = SetPlaybackOptions(playbackSceneDropdown, _playbackSessions, _playbackSession);
        }

        private void OnPlaybackWorkshopChanged(int index)
        {
            if (_updatingPlayback || index < 0 || index >= _playbackWorkshops.Count) return;
            _playbackWorkshop = _playbackWorkshops[index];
            _playbackFolder = _playbackSession = null;
            RefreshPlaybackFolders();
        }

        private void OnPlaybackFolderChanged(int index)
        {
            if (_updatingPlayback || index < 0 || index >= _playbackFolders.Count) return;
            _playbackFolder = _playbackFolders[index];
            _playbackSession = null;
            RefreshPlaybackSessions();
        }

        private void OnPlaybackSceneChanged(int index)
        {
            if (_updatingPlayback || index < 0 || index >= _playbackSessions.Count) return;
            _playbackSession = _playbackSessions[index];
        }

        public bool TryStartSelectedPlayback()
        {
            SynchronizeSelectedStartMode();
            // Read the visible selection at START, even if an earlier UI event
            // was missed (for example after re-enabling the canvas).
            _playbackWorkshop = SelectedPlaybackOption(playbackWorkshopDropdown, _playbackWorkshop);
            _playbackFolder = SelectedPlaybackOption(playbackSessionFolderDropdown, _playbackFolder);
            _playbackSession = SelectedPlaybackOption(playbackSceneDropdown, _playbackSession);
            return storageStage != null && storageStage.TryStartPlaybackSession(
                _playbackWorkshop, _playbackFolder, _playbackSession);
        }

        private static string SelectedPlaybackOption(TMP_Dropdown dropdown, string fallback)
        {
            if (dropdown == null) return fallback;
            if (!dropdown.interactable || dropdown.value < 0 || dropdown.value >= dropdown.options.Count) return null;
            return dropdown.options[dropdown.value].text;
        }

        private void WritePlaybackSettings(PresetConfig config)
        {
            config.StartInPlaybackFullConversationMode = IsPlaybackSelected;
            config.Playback = new PlaybackSettings
            {
                WorkshopFolderName = _playbackWorkshop,
                SessionFolderName = _playbackFolder,
                SessionId = _playbackSession
            };
        }
    }
}
