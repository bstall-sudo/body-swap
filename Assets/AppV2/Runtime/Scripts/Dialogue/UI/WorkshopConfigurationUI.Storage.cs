using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using AppV2.Runtime.Scripts.Config;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI
    {
        [Header("Storage")]
        [SerializeField] private ConversationStage storageStage;
        [SerializeField] private TMP_Dropdown storageWorkshopDropdown;
        [SerializeField] private TMP_Dropdown storageSessionDropdown;
        [SerializeField] private TMP_InputField storageWorkshopInput;
        [SerializeField] private TMP_InputField storageSessionInput;
        [SerializeField] private TMP_InputField personalFolderNameInput;
        private TMP_InputField _recordingSessionNameInput;
        private readonly List<string> _workshopFolders = new();
        private readonly List<string> _sessionFolders = new();
        private bool _newWorkshop;
        private bool _newSession;
        private StorageSettings _storageSettings;
        private readonly Dictionary<TMP_InputField, Color> _folderTextColors = new();
        private bool _updatingStorageOptions;
        private const string CreateFolderOption = "Neuen Ordner Erstellen";

        private void InitializeStorageUI()
        {
            foreach (var input in GetComponentsInChildren<TMP_InputField>(true))
                if (input.name == "SessionFolder_InputField (TMP)" && input.transform.parent != null && input.transform.parent.name == "SessionId_Text")
                    _recordingSessionNameInput = input;
            if (_recordingSessionNameInput != null) _recordingSessionNameInput.onValueChanged.AddListener(OnRecordingSessionNameEdited);
            if (storageWorkshopDropdown != null) storageWorkshopDropdown.onValueChanged.AddListener(OnStorageWorkshopSelected);
            if (storageSessionDropdown != null) storageSessionDropdown.onValueChanged.AddListener(OnStorageSessionSelected);
            if (storageWorkshopInput != null) storageWorkshopInput.onValueChanged.AddListener(OnStorageWorkshopEdited);
            if (storageSessionInput != null) storageSessionInput.onValueChanged.AddListener(OnStorageSessionEdited);
            if (personalFolderNameInput != null) personalFolderNameInput.onValueChanged.AddListener(OnPersonalFolderEdited);
        }

        private void CleanupStorageUI()
        {
            if (_recordingSessionNameInput != null) _recordingSessionNameInput.onValueChanged.RemoveListener(OnRecordingSessionNameEdited);
            if (storageWorkshopDropdown != null) storageWorkshopDropdown.onValueChanged.RemoveListener(OnStorageWorkshopSelected);
            if (storageSessionDropdown != null) storageSessionDropdown.onValueChanged.RemoveListener(OnStorageSessionSelected);
            if (storageWorkshopInput != null) storageWorkshopInput.onValueChanged.RemoveListener(OnStorageWorkshopEdited);
            if (storageSessionInput != null) storageSessionInput.onValueChanged.RemoveListener(OnStorageSessionEdited);
            if (personalFolderNameInput != null) personalFolderNameInput.onValueChanged.RemoveListener(OnPersonalFolderEdited);
        }

        private void DisplayStorageSettings(PresetConfig config)
        {
            config.Storage ??= new StorageSettings();
            _storageSettings = config.Storage;
            // Older presets have no storage values; retain the Inspector defaults.
            _storageSettings.StorageFolderName = storageStage != null ? storageStage._storageFolderName : "SessionRecordingData";
            _storageSettings.WorkshopFolderName ??= storageStage != null ? storageStage._workshopFolderName : "Workshop";
            _storageSettings.SessionFolderName ??= storageStage != null ? storageStage._personalFolderName : "Sessions";
            _storageSettings.SessionIdName ??= storageStage != null ? storageStage._sessionIdName : "";
            if (_recordingSessionNameInput != null) _recordingSessionNameInput.SetTextWithoutNotify(_storageSettings.SessionIdName);
            _newWorkshop = false;
            _newSession = false;
            RefreshStorageFolders();
        }

        private string StorageRoot => Path.Combine(Application.persistentDataPath, _storageSettings.StorageFolderName);

        private static List<string> ReadFolderNames(string path)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return names;
            try
            {
                foreach (var folder in Directory.GetDirectories(path)) names.Add(Path.GetFileName(folder));
                names.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Debug.LogWarning($"[StorageSettings] Cannot read folders at '{path}': {ex.Message}");
            }
            return names;
        }

        private void RefreshStorageFolders()
        {
            if (_storageSettings == null) return;
            _workshopFolders.Clear();
            _workshopFolders.AddRange(ReadFolderNames(StorageRoot));
            _newWorkshop |= !_workshopFolders.Contains(_storageSettings.WorkshopFolderName);
            SetStorageOptions(storageWorkshopDropdown, _workshopFolders, _storageSettings.WorkshopFolderName, _newWorkshop);
            RefreshSessionFolders();
            if (storageWorkshopInput != null)
            {
                storageWorkshopInput.SetTextWithoutNotify(_storageSettings.WorkshopFolderName);
                SetFolderInputState(storageWorkshopInput, _newWorkshop);
            }
        }

        private void RefreshSessionFolders()
        {
            _sessionFolders.Clear();
            if (IsFolderName(_storageSettings.WorkshopFolderName))
                _sessionFolders.AddRange(ReadFolderNames(Path.Combine(StorageRoot, _storageSettings.WorkshopFolderName)));
            _newSession |= !_sessionFolders.Contains(_storageSettings.SessionFolderName);
            SetStorageOptions(storageSessionDropdown, _sessionFolders, _storageSettings.SessionFolderName, _newSession);
            if (storageSessionInput != null)
            {
                storageSessionInput.SetTextWithoutNotify(_storageSettings.SessionFolderName);
                SetFolderInputState(storageSessionInput, _newSession);
            }
            if (personalFolderNameInput != null) personalFolderNameInput.SetTextWithoutNotify(_storageSettings.SessionFolderName);
        }

        private void SetStorageOptions(TMP_Dropdown dropdown, List<string> folders, string selected, bool createNew)
        {
            if (dropdown == null) return;
            _updatingStorageOptions = true;
            try
            {
                var options = new List<string>(folders) { CreateFolderOption };
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
                dropdown.SetValueWithoutNotify(createNew ? folders.Count : Mathf.Max(0, folders.IndexOf(selected)));
                dropdown.RefreshShownValue();
            }
            finally { _updatingStorageOptions = false; }
        }

        private void SetFolderInputState(TMP_InputField input, bool editable)
        {
            input.interactable = editable;
            // TMP's selectable disabled colors gray the background; tint the text too.
            if (input.textComponent != null)
            {
                if (!_folderTextColors.ContainsKey(input)) _folderTextColors[input] = input.textComponent.color;
                input.textComponent.color = editable ? _folderTextColors[input] : Color.gray;
            }
        }

        private void OnStorageWorkshopSelected(int index)
        {
            if (_storageSettings == null || _updatingStorageOptions || index < 0 || index > _workshopFolders.Count) return;
            _newWorkshop = index == _workshopFolders.Count;
            _storageSettings.WorkshopFolderName = _newWorkshop ? "" : _workshopFolders[index];
            _newSession = false;
            RefreshStorageFolders();
        }

        private void OnStorageSessionSelected(int index)
        {
            if (_storageSettings == null || _updatingStorageOptions || index < 0 || index > _sessionFolders.Count) return;
            _newSession = index == _sessionFolders.Count;
            _storageSettings.SessionFolderName = _newSession ? "" : _sessionFolders[index];
            RefreshSessionFolders();
        }

        private void OnStorageWorkshopEdited(string value)
        {
            if (_storageSettings == null) return;
            _storageSettings.WorkshopFolderName = value;
            _newSession = false;
            RefreshSessionFolders();
        }

        private void OnStorageSessionEdited(string value)
        {
            if (_storageSettings == null) return;
            _storageSettings.SessionFolderName = value;
            if (personalFolderNameInput != null) personalFolderNameInput.SetTextWithoutNotify(value);
        }

        private void OnPersonalFolderEdited(string value)
        {
            if (_storageSettings == null) return;
            _storageSettings.SessionFolderName = value;
            _newSession = !_sessionFolders.Contains(value);
            RefreshSessionFolders();
        }

        private void OnRecordingSessionNameEdited(string value)
        {
            if (_storageSettings != null) _storageSettings.SessionIdName = value;
        }

        private void WriteStorageSettings(PresetConfig config)
        {
            if (_storageSettings == null) return;
            config.Storage = _storageSettings;
        }

        private static bool IsFolderName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name != "." && name != ".." &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                !name.Contains("/") && !name.Contains("\\") && !name.EndsWith(".") && !name.EndsWith(" ");
        }

        public static bool ValidateStorageSettings(StorageSettings settings, out string error)
        {
            error = null;
            if (settings == null || (settings.WorkshopFolderName == null && settings.SessionFolderName == null)) return true;
            if (!IsFolderName(settings.WorkshopFolderName) || !IsFolderName(settings.SessionFolderName))
            {
                error = "Bitte gültige Workshop- und Session-Ordnernamen eingeben (keine Pfade).";
                return false;
            }
            return true;
        }
    }
}
