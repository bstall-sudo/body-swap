using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public sealed class RecordingEndPanelUI : MonoBehaviour
    {
        private Button _remove, _delete, _save;
        private TMP_Dropdown _workshop, _folder;
        private TMP_InputField _name;
        private TMP_Text _warning;
        private RecordingSessionEditor _recording;
        private readonly List<string> _workshops = new(), _folders = new();
        private bool _bound;
        private bool _busy;
        private string SelectedWorkshop => _workshops.Count > 0 ? _workshops[_workshop.value] : null;
        private string SelectedFolder => _folders.Count > 0 ? _folders[_folder.value] : null;

        private T Find<T>(string objectName) where T : Component
        {
            foreach (var component in GetComponentsInChildren<T>(true))
                if (component.name == objectName) return component;
            throw new InvalidOperationException("RecordingEndPanel: " + objectName + " fehlt.");
        }

        public void Initialize()
        {
            if (_bound) return;
            _remove = Find<Button>("RemoveLastTake_Button");
            _delete = Find<Button>("DeleteLastRecording");
            _save = Find<Button>("SaveSessionWithNewName");
            _workshop = Find<TMP_Dropdown>("SelectWorkshopFolderForSaving_Dropdown");
            _folder = Find<TMP_Dropdown>("SelectSessionFolderForSavingRecording_Dropdown");
            _name = Find<TMP_InputField>("NewNameForRecordingInput");
            _remove.onClick.AddListener(RemoveLastTake);
            _delete.onClick.AddListener(DeleteRecording);
            _save.onClick.AddListener(SaveRecording);
            _workshop.onValueChanged.AddListener(WorkshopChanged);
            _folder.onValueChanged.AddListener(FolderChanged);
            _name.onValueChanged.AddListener(NameChanged);
            var warning = new GameObject("RecordingNameWarning", typeof(RectTransform), typeof(TextMeshProUGUI));
            warning.transform.SetParent(_name.transform, false);
            var rect = (RectTransform)warning.transform;
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -4); rect.sizeDelta = new Vector2(0, 50);
            _warning = warning.GetComponent<TextMeshProUGUI>();
            if (_name.textComponent != null) _warning.font = _name.textComponent.font;
            _warning.fontSize = 18; _warning.color = Color.red; _warning.raycastTarget = false;
            _warning.enableAutoSizing = true; _warning.fontSizeMin = 12; _warning.fontSizeMax = 18;
            _bound = true;
            RefreshButtons();
        }

        public void Display(RecordingSessionEditor recording)
        {
            Initialize();
            _recording = recording;
            _name.SetTextWithoutNotify("");
            _warning.text = "";
            _workshops.Clear(); _workshops.AddRange(recording.Workshops());
            SetOptions(_workshop, _workshops, recording.Workshop);
            RefreshFolders(recording.Folder);
            RefreshButtons();
        }

        private static void SetOptions(TMP_Dropdown dropdown, List<string> values, string selected)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(values.Count > 0 ? new List<string>(values) : new List<string> { "Keine Ordner vorhanden" });
            dropdown.SetValueWithoutNotify(Mathf.Max(0, values.IndexOf(selected)));
            dropdown.RefreshShownValue(); dropdown.interactable = values.Count > 0;
        }
        private void RefreshFolders(string selected)
        {
            _folders.Clear();
            if (_recording != null && SelectedWorkshop != null) _folders.AddRange(_recording.Folders(SelectedWorkshop));
            SetOptions(_folder, _folders, selected);
        }
        private void WorkshopChanged(int _) { Run(() => RefreshFolders(null)); }
        private void FolderChanged(int _) => RefreshButtons();
        private void NameChanged(string _) => RefreshButtons();

        private void RefreshButtons()
        {
            if (!_bound) return;
            bool exists = !_busy && _recording != null && _recording.Exists;
            _remove.interactable = exists && _recording.HasTakes;
            _delete.interactable = exists;
            string validation = exists ? _recording.SaveValidation(SelectedWorkshop, SelectedFolder, _name.text) : "";
            _save.interactable = exists && validation == null;
            _warning.text = validation ?? "";
        }

        private void Run(Action action)
        {
            if (_busy || _recording == null) return;
            _busy = true;
            string error = null;
            try { action(); }
            catch (Exception ex) { error = ex.Message; Debug.LogWarning("[RecordingEndPanel] " + error, this); }
            finally
            {
                _busy = false;
                try { RefreshButtons(); }
                catch (Exception ex) { _remove.interactable = _delete.interactable = _save.interactable = false; error = ex.Message; }
                if (error != null) _warning.text = error;
            }
        }
        private void RemoveLastTake() => Run(() => _recording.RemoveLastTakeRound());
        private void DeleteRecording() => Run(() =>
        {
            _recording.Delete();
            string key = "BodySwap.LastRecording." + _recording.StorageFolder + ".";
            if (PlayerPrefs.GetString(key + "Workshop") == _recording.Workshop &&
                PlayerPrefs.GetString(key + "Folder") == _recording.Folder && PlayerPrefs.GetString(key + "Session") == _recording.SessionId)
            {
                PlayerPrefs.DeleteKey(key + "Workshop"); PlayerPrefs.DeleteKey(key + "Folder"); PlayerPrefs.DeleteKey(key + "Session"); PlayerPrefs.Save();
            }
        });
        private void SaveRecording() => Run(() =>
        {
            _recording.SaveAs(SelectedWorkshop, SelectedFolder, _name.text);
            string key = "BodySwap.LastRecording." + _recording.StorageFolder + ".";
            PlayerPrefs.SetString(key + "Workshop", _recording.Workshop); PlayerPrefs.SetString(key + "Folder", _recording.Folder);
            PlayerPrefs.SetString(key + "Session", _recording.SessionId); PlayerPrefs.Save();
        });
    }
}
