using UnityEngine;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public partial class WorkshopConfigurationUI
    {
        private RecordingEndPanelUI _recordingEndPanel;
        private MenuNavigation _recordingEndNavigation;

        private void InitializeRecordingEndUI()
        {
            var root = menuRoot != null ? menuRoot : gameObject;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "RecordingEndPanel") continue;
                _recordingEndPanel = child.GetComponent<RecordingEndPanelUI>();
                if (_recordingEndPanel == null) _recordingEndPanel = child.gameObject.AddComponent<RecordingEndPanelUI>();
                _recordingEndNavigation = child.GetComponentInParent<MenuNavigation>(true);
                _recordingEndNavigation?.RegisterPage("RecordingEndPanel", child.gameObject);
                _recordingEndPanel.Initialize();
                child.gameObject.SetActive(false);
                break;
            }
        }

        public void ShowRecordingEnd(RecordingSessionEditor recording)
        {
            if (_recordingEndPanel == null) InitializeRecordingEndUI();
            if (_recordingEndPanel == null) { Debug.LogWarning("RecordingEndPanel fehlt.", this); return; }
            Show();
            _recordingEndPanel.Display(recording);
            if (_recordingEndNavigation != null) _recordingEndNavigation.ShowPage("RecordingEndPanel");
            else _recordingEndPanel.gameObject.SetActive(true);
        }
    }
}
