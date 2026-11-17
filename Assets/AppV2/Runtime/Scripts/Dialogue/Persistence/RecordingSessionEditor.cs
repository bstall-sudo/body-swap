using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    /// <summary>Edits only a completed recording; never uses a mutable menu selection as its source.</summary>
    public sealed class RecordingSessionEditor
    {
        public string StorageFolder { get; }
        public string Workshop { get; private set; }
        public string Folder { get; private set; }
        public string SessionId { get; private set; }
        public string Root { get; }
        public string SessionPath => Child(Child(Child(Root, Workshop), Folder), SessionId);
        public bool Exists => Directory.Exists(SessionPath) && File.Exists(Path.Combine(SessionPath, "session.json"));

        public RecordingSessionEditor(string storageFolder, string workshop, string folder, string sessionId)
        {
            StorageFolder = storageFolder;
            Root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, storageFolder));
            Workshop = workshop; Folder = folder; SessionId = sessionId;
            _ = SessionPath; // Validate all source path components immediately.
        }

        public static bool ValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith(".") ||
                name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.IndexOfAny(new[] {'/', '\\', ':', '*', '?', '"', '<', '>', '|'}) >= 0) return false;
            string stem = name.Split('.')[0].ToUpperInvariant();
            return stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL" &&
                !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '0' && stem[3] <= '9');
        }

        private static string Child(string parent, string name)
        {
            if (!ValidName(name)) throw new ArgumentException("Ungültiger Ordner- oder Aufnahmename.");
            string path = Path.GetFullPath(Path.Combine(parent, name));
            string prefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Pfad liegt außerhalb des Aufnahmeordners.");
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Verknüpfte Aufnahmeordner werden nicht bearbeitet.");
            return path;
        }

        public List<string> Workshops() => Directories(Root);
        public List<string> Folders(string workshop) => ValidName(workshop) ? Directories(Child(Root, workshop)) : new List<string>();
        private static List<string> Directories(string root) => !Directory.Exists(root) ? new List<string>() :
            Directory.GetDirectories(root).Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)
                .Select(Path.GetFileName).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

        public string SaveValidation(string workshop, string folder, string name)
        {
            if (!Exists) return "Die Aufnahme ist nicht mehr vorhanden.";
            if (string.IsNullOrWhiteSpace(name)) return "";
            if (!ValidName(name)) return "Bitte einen gültigen Aufnahmenamen eingeben.";
            if (!ValidName(workshop) || !ValidName(folder)) return "Bitte einen Zielordner auswählen.";
            string destination = Child(Child(Root, workshop), folder);
            if (!Directory.Exists(destination)) return "Der Zielordner ist nicht mehr vorhanden.";
            // Also reject case-only conflicts on case-sensitive hosts.
            if (Directory.EnumerateFileSystemEntries(destination).Any(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)))
                return "Dieser Name ist im ausgewählten Ordner bereits vorhanden.";
            return null;
        }

        private SessionModel Read() => JsonUtility.FromJson<SessionModel>(File.ReadAllText(Path.Combine(SessionPath, "session.json")))
            ?? throw new IOException("session.json konnte nicht gelesen werden.");
        public bool HasTakes => Exists && Read().Takes?.Any(t => t != null) == true;

        public int RemoveLastTakeRound()
        {
            var model = Read();
            var takes = model.Takes?.Where(t => t != null).ToList();
            if (takes == null || takes.Count == 0) return 0;
            int last = takes.Max(t => t.SceneCount);
            var removed = takes.Where(t => t.SceneCount == last).ToList();
            var retained = takes.Where(t => t.SceneCount != last).ToList();
            var keepFiles = new HashSet<string>(retained.SelectMany(Files), StringComparer.OrdinalIgnoreCase);
            var filenames = removed.SelectMany(Files).Where(f => !keepFiles.Contains(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // Validate the entire operation before moving any file.
            var paths = filenames.Select(f => Child(SessionPath, f)).ToList();
            string jsonPath = Path.Combine(SessionPath, "session.json");
            string staging = Child(SessionPath, ".remove-take-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var moved = new List<string>();
            bool committed = false;
            try
            {
                foreach (string path in paths)
                    if (File.Exists(path))
                    {
                        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfte Take-Datei.");
                        File.Move(path, Path.Combine(staging, Path.GetFileName(path))); moved.Add(path);
                    }
                model.Takes = retained;
                string updated = Path.Combine(staging, "updated-session.json");
                File.WriteAllText(updated, JsonUtility.ToJson(model, true));
                File.Replace(updated, jsonPath, Path.Combine(staging, "original-session.json"));
                committed = true;
            }
            finally
            {
                if (!committed)
                    foreach (string path in moved) File.Move(Path.Combine(staging, Path.GetFileName(path)), path);
                // A completed transaction may leave cleanup files if the OS temporarily locks them.
                try { Directory.Delete(staging, true); }
                catch (IOException) { if (!committed) throw; }
                catch (UnauthorizedAccessException) { if (!committed) throw; }
            }
            return removed.Count;
        }

        private static IEnumerable<string> Files(TakeMeta take)
        {
            if (!string.IsNullOrWhiteSpace(take.FramesFile)) yield return take.FramesFile;
            else if (!string.IsNullOrWhiteSpace(take.TakeId)) yield return take.TakeId + ".frames.jsonl";
            if (!string.IsNullOrWhiteSpace(take.AudioFile)) yield return take.AudioFile;
            else if (!string.IsNullOrWhiteSpace(take.TakeId)) yield return take.TakeId + ".wav";
        }

        private static void ValidateTree(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfte Dateien/Ordner werden nicht bearbeitet.");
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfte Dateien/Ordner werden nicht bearbeitet.");
                if (Directory.Exists(entry)) ValidateTree(entry);
            }
        }

        public void Delete()
        {
            if (!Exists) throw new IOException("Die Aufnahme ist nicht mehr vorhanden.");
            string path = SessionPath;
            ValidateTree(path);
            Directory.Delete(path, true);
        }

        public void SaveAs(string workshop, string folder, string name)
        {
            string validation = SaveValidation(workshop, folder, name);
            if (validation != null) throw new IOException(string.IsNullOrEmpty(validation) ? "Bitte einen Namen eingeben." : validation);
            string parent = Child(Child(Root, workshop), folder);
            string destination = Child(parent, name);
            string staging = Child(parent, ".saving-" + Guid.NewGuid().ToString("N"));
            ValidateTree(SessionPath);
            try
            {
                CopyDirectory(SessionPath, staging);
                var model = Read(); model.SessionId = name;
                File.WriteAllText(Path.Combine(staging, "session.json"), JsonUtility.ToJson(model, true));
                // Antivirus/indexing can briefly hold newly copied media on Windows.
                // Publish by rename, with a short bounded retry for transient sharing locks.
                for (int attempt = 0; ; attempt++)
                {
                    try { Directory.Move(staging, destination); break; }
                    catch (IOException) when (attempt < 5 && !Directory.Exists(destination) && !File.Exists(destination))
                    { System.Threading.Thread.Sleep(50 * (attempt + 1)); }
                }
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            Workshop = workshop; Folder = folder; SessionId = name;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
