using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Config
{
    public class PresetStore
    {
        private const string MainFolderName = "SessionRecordingData";
        private const string PresetFolderName = "Presets";


        // ============================================================
        // PATHS
        // ============================================================

        public string PresetRootPath =>
            Path.Combine(
                Application.persistentDataPath,
                MainFolderName,
                PresetFolderName
            );


        private string GetPresetPath(string presetId)
        {
            return Path.Combine(
                PresetRootPath,
                $"{presetId}.json"
            );
        }


        private void EnsurePresetDirectoryExists()
        {
            if (!Directory.Exists(PresetRootPath))
            {
                Directory.CreateDirectory(PresetRootPath);

                Debug.Log(
                    $"[PresetStore] Created preset directory: " +
                    $"{PresetRootPath}"
                );
            }
        }


        // ============================================================
        // SAVE
        // ============================================================

        public bool SavePreset(PresetConfig config)
        {
            if (config == null)
            {
                Debug.LogError(
                    "[PresetStore] SavePreset failed: config is null."
                );

                return false;
            }

            if (string.IsNullOrWhiteSpace(config.PresetId))
            {
                Debug.LogError(
                    "[PresetStore] SavePreset failed: PresetId is empty."
                );

                return false;
            }

            try
            {
                EnsurePresetDirectoryExists();

                string json =
                    JsonUtility.ToJson(config, true);

                string path =
                    GetPresetPath(config.PresetId);

                File.WriteAllText(path, json);

                Debug.Log(
                    $"[PresetStore] Saved preset " +
                    $"'{config.PresetName}' " +
                    $"({config.PresetId}) to:\n{path}"
                );

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PresetStore] Could not save preset " +
                    $"'{config.PresetId}'.\n{ex}"
                );

                return false;
            }
        }


        // ============================================================
        // LOAD
        // ============================================================

        public PresetConfig LoadPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                Debug.LogError(
                    "[PresetStore] LoadPreset failed: presetId is empty."
                );

                return null;
            }

            string path =
                GetPresetPath(presetId);

            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[PresetStore] Preset does not exist:\n{path}"
                );

                return null;
            }

            try
            {
                string json =
                    File.ReadAllText(path);

                PresetConfig config =
                    JsonUtility.FromJson<PresetConfig>(json);

                if (config == null)
                {
                    Debug.LogError(
                        $"[PresetStore] Could not deserialize preset: " +
                        $"{presetId}"
                    );

                    return null;
                }

                Debug.Log(
                    $"[PresetStore] Loaded preset " +
                    $"'{config.PresetName}' " +
                    $"({config.PresetId})"
                );

                return config;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PresetStore] Could not load preset " +
                    $"'{presetId}'.\n{ex}"
                );

                return null;
            }
        }


        // ============================================================
        // DELETE
        // ============================================================

        public bool DeletePreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            string path =
                GetPresetPath(presetId);

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    $"[PresetStore] Cannot delete preset. " +
                    $"File does not exist:\n{path}"
                );

                return false;
            }

            try
            {
                File.Delete(path);

                Debug.Log(
                    $"[PresetStore] Deleted preset: {presetId}"
                );

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PresetStore] Could not delete preset " +
                    $"'{presetId}'.\n{ex}"
                );

                return false;
            }
        }


        // ============================================================
        // EXISTS
        // ============================================================

        public bool PresetExists(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            return File.Exists(
                GetPresetPath(presetId)
            );
        }


        // ============================================================
        // GET ALL PRESET IDS
        // ============================================================

        public List<string> GetAllPresetIds()
        {
            EnsurePresetDirectoryExists();

            List<string> presetIds =
                new List<string>();

            string[] files =
                Directory.GetFiles(
                    PresetRootPath,
                    "*.json"
                );

            foreach (string file in files)
            {
                string presetId =
                    Path.GetFileNameWithoutExtension(file);

                presetIds.Add(presetId);
            }

            presetIds.Sort();

            return presetIds;
        }
    }
}