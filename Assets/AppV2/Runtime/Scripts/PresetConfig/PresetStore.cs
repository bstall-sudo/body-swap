using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Config
{
    public class PresetStore
    {
        private readonly string _persistentPath;
        public PresetStore() : this(Application.persistentDataPath) { }
        public PresetStore(string persistentPath) { _persistentPath = persistentPath; }
        public string PresetRootPath => Path.Combine(_persistentPath, "Presets");
        public string SelectedFolder { get; private set; } = "Default";
        public string SelectedFolderPath => Path.Combine(PresetRootPath, SelectedFolder);

        public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name == name.Trim() &&
            name != "." && name != ".." && !name.EndsWith(".") && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            !name.Contains("/") && !name.Contains("\\");

        public void SetFolder(string folder)
        {
            if (!ValidName(folder)) throw new ArgumentException("Invalid preset folder.");
            SelectedFolder = folder;
        }

        private string GetPresetPath(string id)
        {
            if (!ValidName(id)) throw new ArgumentException("Invalid preset file name.");
            return Path.Combine(SelectedFolderPath, id + ".json");
        }

        public void ImportLegacyPresets()
        {
            string oldRoot = Path.Combine(_persistentPath, "SessionRecordingData", "Presets");
            if (!Directory.Exists(oldRoot)) return;
            foreach (string source in Directory.GetFiles(oldRoot, "*.json"))
            {
                string target = Path.Combine(PresetRootPath, "ImportedPresets", Path.GetFileName(source));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target)) File.Copy(source, target, false);
            }
        }

        public List<string> GetFolders()
        {
            Directory.CreateDirectory(PresetRootPath);
            var result = new List<string>();
            foreach (var path in Directory.GetDirectories(PresetRootPath)) result.Add(Path.GetFileName(path));
            if (result.Count == 0)
            {
                Directory.CreateDirectory(Path.Combine(PresetRootPath, "Default"));
                result.Add("Default");
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public bool SavePreset(PresetConfig config, bool overwrite = true)
        {
            if (config == null || !ValidName(config.PresetId)) return false;
            try
            {
                Directory.CreateDirectory(SelectedFolderPath);
                string path = GetPresetPath(config.PresetId);
                string json = JsonUtility.ToJson(config, true);
                if (!overwrite)
                {
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(json);
                }
                else
                {
                    string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temporary, json, new UTF8Encoding(false));
                        if (File.Exists(path)) File.Replace(temporary, path, null);
                        else File.Move(temporary, path);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                return true;
            }
            catch (Exception ex) { Debug.LogError($"[PresetStore] Save failed: {ex.Message}"); return false; }
        }

        public PresetConfig LoadPreset(string id)
        {
            if (!ValidName(id)) return null;
            try
            {
                var config = JsonUtility.FromJson<PresetConfig>(File.ReadAllText(GetPresetPath(id)));
                if (config != null) config.PresetId = id;
                return config;
            }
            catch (Exception ex) { Debug.LogError($"[PresetStore] Load failed: {ex.Message}"); return null; }
        }

        public bool DeletePreset(string id)
        {
            if (!PresetExists(id)) return false;
            File.Delete(GetPresetPath(id));
            return true;
        }
        public bool PresetExists(string id) => ValidName(id) && File.Exists(GetPresetPath(id));
        public List<string> GetAllPresetIds()
        {
            var result = new List<string>();
            if (!Directory.Exists(SelectedFolderPath)) return result;
            foreach (var file in Directory.GetFiles(SelectedFolderPath, "*.json")) result.Add(Path.GetFileNameWithoutExtension(file));
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public string NextVersionName(string baseName, string suffix, bool alwaysVersion)
        {
            string stem = baseName.Trim() + (string.IsNullOrWhiteSpace(suffix) ? "" : "_" + suffix.Trim());
            if (!ValidName(stem)) return null;
            bool versioned = alwaysVersion || PresetExists(stem) || PresetExists(baseName.Trim());
            foreach (var id in GetAllPresetIds())
                if (id.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(id.Substring(stem.Length + 1), out _)) versioned = true;
            if (!versioned) return stem;
            for (int version = 1; version < int.MaxValue; version++)
            {
                string candidate = stem + "_" + version.ToString("D3");
                if (!PresetExists(candidate)) return candidate;
            }
            return null;
        }
    }
}
