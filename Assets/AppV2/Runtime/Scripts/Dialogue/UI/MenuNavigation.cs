using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.UI
{
    public class MenuNavigation : MonoBehaviour
    {
        [Serializable]
        public class MenuPage
        {
            public string pageId;
            public GameObject panel;
        }

        [Header("Menu Pages")]
        [SerializeField]
        private List<MenuPage> pages = new();

        [Header("Start Page")]
        [SerializeField]
        private string startPageId = "RecordBase";

        [Header("Mode Dropdown")]
        [SerializeField]
        private TMP_Dropdown modeDropdown;

        private string _currentPageId;

        public string CurrentPageId => _currentPageId;

        private void Awake()
        {
            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.AddListener(
                    OnModeChanged
                );
            }

            ShowPage(startPageId);
        }

        private void OnDestroy()
        {
            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.RemoveListener(
                    OnModeChanged
                );
            }
        }

        public void ShowPage(string pageId)
        {
            bool found = false;

            foreach (MenuPage page in pages)
            {
                if (page.panel == null)
                    continue;

                bool active = page.pageId == pageId;

                page.panel.SetActive(active);

                if (active)
                    found = true;
            }

            if (!found)
            {
                Debug.LogWarning(
                    $"[WorkshopMenuNavigation] Page not found: {pageId}"
                );
                return;
            }

            _currentPageId = pageId;

            Debug.Log(
                $"[WorkshopMenuNavigation] Showing page: {pageId}"
            );
        }

        private void OnModeChanged(int modeIndex)
        {
            switch (modeIndex)
            {
                case 0:
                    ShowPage("RecordBase");
                    break;

                case 1:
                    ShowPage("Playback");
                    break;
            }
        }

        public void ShowRecordBase()
        {
            ShowPage("RecordBase");
        }

        public void ShowPlayback()
        {
            ShowPage("Playback");
        }

        public void ShowPlayerSettings()
        {
            ShowPage("PlayerSettings");
        }

        public void ShowStorageSettings()
        {
            ShowPage("StorageSettings");
        }

        public void ShowActiveRolesSettings()
        {
            ShowPage("ActiveRolesSettings");
        }

        public void ShowPresetSettings()
        {
            ShowPage("PresetSettings");
        }

        public void ShowAllSettings()
        {
            ShowPage("AllSettings");
        }
    }
}