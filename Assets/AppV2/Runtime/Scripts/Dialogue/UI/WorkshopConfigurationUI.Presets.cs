using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI
    {
        [Header("Preset versions")]
        [SerializeField] private TMP_Dropdown presetFolderDropdown;
        [SerializeField] private TMP_InputField presetVersionNameInput;
        [SerializeField] private TMP_Text presetVersionNameDisplay;
        [SerializeField] private string presetSuffix = "";
        private readonly List<TMP_Dropdown> _presetDropdowns = new();
        private readonly List<TMP_InputField> _presetNameInputs = new();
        private readonly List<Button> _presetSaveButtons = new();
        private readonly List<Action> _presetCleanup = new();
        private readonly List<string> _presetOptionIds = new();
        private List<string> _presetFolders = new();
        private PresetStore _presetBrowserStore;
        private string _selectedPresetId;
        private string _currentPresetBaseName = "Current Configuration";
        private bool _hasLoadedPreset;
        private GameObject _presetConfirmation;
        private TMP_Text _presetConfirmationText;
        private Action _presetConfirmationAction;
        public string SaveAsBaseName => _presetNameInputs.Count > 0 && !string.IsNullOrWhiteSpace(_presetNameInputs[0].text)
            ? _presetNameInputs[0].text.Trim() : _currentPresetBaseName;
        public string SaveAsSuffix => presetVersionNameInput != null && !string.IsNullOrWhiteSpace(presetVersionNameInput.text)
            ? presetVersionNameInput.text.Trim() : presetSuffix;
        public string SaveAsPresetName { get; private set; }

        private void InitializePresetUI()
        {
            CleanupPresetUI();
            _presetDropdowns.Clear();
            _presetNameInputs.Clear();
            _presetSaveButtons.Clear();
            var root = menuRoot != null ? menuRoot : gameObject;
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (dropdown.name == "PresetFolderDropdown") presetFolderDropdown = dropdown;
                if (dropdown.name != "PresetDropdown") continue;
                _presetDropdowns.Add(dropdown);
                UnityEngine.Events.UnityAction<int> changed = OnSharedPresetSelected;
                dropdown.onValueChanged.AddListener(changed);
                _presetCleanup.Add(() => { if (dropdown != null) dropdown.onValueChanged.RemoveListener(changed); });
            }
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input.name == "PresetVersionNameInput" || input.name == "PresetVersionNameInput (TMP)") presetVersionNameInput = input;
                if (input.name != "PresetNameInput") continue;
                _presetNameInputs.Add(input);
                UnityEngine.Events.UnityAction<string> changed = text =>
                {
                    foreach (var mirror in _presetNameInputs) if (mirror != input) mirror.SetTextWithoutNotify(text);
                    RefreshPresetNamePreview();
                };
                input.onValueChanged.AddListener(changed);
                _presetCleanup.Add(() => { if (input != null) input.onValueChanged.RemoveListener(changed); });
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "PresetVersionNameDisplay_Text" || text.name == "PresetVersionName_Display" || text.name == "PresetVersionName_Display_Text") presetVersionNameDisplay = text;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                UnityEngine.Events.UnityAction clicked = null;
                if (button == loadPresetButton || button.name == "LoadPreset" || button.name == "LoadPresetButton") clicked = () => LoadPresetRequested?.Invoke();
                if (button == savePresetButton || button.name == "SavePreset" || button.name == "SavePresetButton")
                {
                    clicked = () => SavePresetRequested?.Invoke();
                    _presetSaveButtons.Add(button);
                }
                if (button == savePresetAsButton || button.name == "SavePresetAsButton") clicked = () => SavePresetAsRequested?.Invoke();
                if (clicked == null) continue;
                button.onClick.AddListener(clicked);
                var callback = clicked;
                _presetCleanup.Add(() => { if (button != null) button.onClick.RemoveListener(callback); });
            }
            if (presetFolderDropdown != null)
            {
                presetFolderDropdown.onValueChanged.AddListener(OnPresetFolderChanged);
                _presetCleanup.Add(() => { if (presetFolderDropdown != null) presetFolderDropdown.onValueChanged.RemoveListener(OnPresetFolderChanged); });
            }
            if (presetVersionNameInput != null)
            {
                presetVersionNameInput.onValueChanged.AddListener(OnPresetSuffixEdited);
                _presetCleanup.Add(() => { if (presetVersionNameInput != null) presetVersionNameInput.onValueChanged.RemoveListener(OnPresetSuffixEdited); });
            }
        }

        private void CleanupPresetUI()
        {
            foreach (var cleanup in _presetCleanup) cleanup();
            _presetCleanup.Clear();
        }

        public void InitializePresetBrowser(PresetStore store)
        {
            _presetBrowserStore = store;
            InitializePresetUI();
            _presetFolders = store.GetFolders();
            int index = _presetFolders.IndexOf(store.SelectedFolder);
            if (index < 0) index = 0;
            store.SetFolder(_presetFolders[index]);
            if (presetFolderDropdown != null)
            {
                presetFolderDropdown.onValueChanged.RemoveListener(OnPresetFolderChanged);
                presetFolderDropdown.ClearOptions();
                presetFolderDropdown.AddOptions(_presetFolders);
                presetFolderDropdown.SetValueWithoutNotify(index);
                presetFolderDropdown.RefreshShownValue();
                presetFolderDropdown.onValueChanged.AddListener(OnPresetFolderChanged);
            }
            RefreshPresetOptionLists(store.GetAllPresetIds());
        }

        private void OnPresetFolderChanged(int index)
        {
            if (_presetBrowserStore == null || index < 0 || index >= _presetFolders.Count) return;
            _presetBrowserStore.SetFolder(_presetFolders[index]);
            _selectedPresetId = null;
            RefreshPresetOptionLists(_presetBrowserStore.GetAllPresetIds());
        }

        private void RefreshPresetOptionLists(List<string> ids)
        {
            _presetOptionIds.Clear();
            if (ids != null) _presetOptionIds.AddRange(ids);
            foreach (var dropdown in _presetDropdowns)
            {
                dropdown.onValueChanged.RemoveListener(OnSharedPresetSelected);
                dropdown.ClearOptions();
                dropdown.AddOptions(_presetOptionIds.Count > 0 ? _presetOptionIds : new List<string> { "Keine Presets vorhanden" });
                dropdown.interactable = _presetOptionIds.Count > 0;
                dropdown.onValueChanged.AddListener(OnSharedPresetSelected);
            }
            SelectSharedPreset(_selectedPresetId);
            RefreshPresetNamePreview();
        }

        private void OnSharedPresetSelected(int index)
        {
            if (index < 0 || index >= _presetOptionIds.Count) return;
            SelectSharedPreset(_presetOptionIds[index]);
        }

        private void SelectSharedPreset(string id)
        {
            int index = _presetOptionIds.IndexOf(id);
            if (index < 0) index = 0;
            _selectedPresetId = _presetOptionIds.Count > 0 ? _presetOptionIds[index] : null;
            foreach (var dropdown in _presetDropdowns)
            {
                dropdown.SetValueWithoutNotify(index);
                dropdown.RefreshShownValue();
            }
        }

        public void SetLoadedPreset(PresetConfig config, bool saved)
        {
            _hasLoadedPreset = saved;
            presetSuffix = config.PresetSuffix ?? "";
            _currentPresetBaseName = !string.IsNullOrWhiteSpace(config.PresetBaseName) ? config.PresetBaseName
                : !string.IsNullOrWhiteSpace(config.PresetName) ? config.PresetName : "Preset";
            foreach (var input in _presetNameInputs) input.SetTextWithoutNotify("");
            if (presetVersionNameInput != null) presetVersionNameInput.SetTextWithoutNotify("");
            foreach (var button in _presetSaveButtons) button.interactable = _hasLoadedPreset;
            RefreshPresetNamePreview();
        }

        private void OnPresetSuffixEdited(string value) => RefreshPresetNamePreview();
        public void RefreshPresetNamePreview()
        {
            bool newBase = _presetNameInputs.Count > 0 && !string.IsNullOrWhiteSpace(_presetNameInputs[0].text);
            SaveAsPresetName = _presetBrowserStore?.NextVersionName(SaveAsBaseName, SaveAsSuffix, !newBase);
            if (presetVersionNameDisplay != null) presetVersionNameDisplay.text = SaveAsPresetName ?? "Bitte gültigen Preset-Namen eingeben";
        }

        public void ConfirmPresetAction(string message, Action confirmed)
        {
            EnsurePresetConfirmation();
            _presetConfirmationAction = confirmed;
            _presetConfirmationText.text = message;
            _presetConfirmation.SetActive(true);
            _presetConfirmation.transform.SetAsLastSibling();
        }

        private void EnsurePresetConfirmation()
        {
            if (_presetConfirmation != null) return;
            var root = menuRoot != null ? menuRoot.transform : transform;
            _presetConfirmation = new GameObject("PresetConfirmation", typeof(RectTransform), typeof(Image));
            _presetConfirmation.transform.SetParent(root, false);
            var rect = (RectTransform)_presetConfirmation.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            _presetConfirmation.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);
            var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.Find("PresetConfirmation"), false);
            rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.15f, 0.35f); rect.anchorMax = new Vector2(0.85f, 0.8f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            _presetConfirmationText = textObject.GetComponent<TextMeshProUGUI>();
            if (presetNameInput != null && presetNameInput.textComponent != null) _presetConfirmationText.font = presetNameInput.textComponent.font;
            _presetConfirmationText.enableAutoSizing = true; _presetConfirmationText.fontSizeMin = 20; _presetConfirmationText.fontSizeMax = 36;
            _presetConfirmationText.alignment = TextAlignmentOptions.Center;
            _presetConfirmationText.raycastTarget = false;
            CreateConfirmationButton("OK", -140, () =>
            {
                var action = _presetConfirmationAction; _presetConfirmationAction = null;
                _presetConfirmation.SetActive(false); action?.Invoke();
            });
            CreateConfirmationButton("Abbrechen", 140, () =>
            {
                _presetConfirmationAction = null; _presetConfirmation.SetActive(false);
            });
        }

        private void CreateConfirmationButton(string label, float x, UnityEngine.Events.UnityAction clicked)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_presetConfirmation.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.25f); rect.sizeDelta = new Vector2(240, 70); rect.anchoredPosition = new Vector2(x, 0);
            go.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.45f, 1);
            go.GetComponent<Button>().onClick.AddListener(clicked);
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(go.transform, false);
            rect = (RectTransform)text.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var tmp = text.GetComponent<TextMeshProUGUI>(); tmp.text = label; tmp.font = _presetConfirmationText.font; tmp.fontSize = 30;
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        }
    }
}
