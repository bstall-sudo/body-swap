using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Config
{
    [Serializable]
    public class RecordedSessionOption
    {
        public string WorkshopFolderName;
        public string SessionFolderName;
        public string SessionId;

        public string DisplayName =>
            $"{WorkshopFolderName} / {SessionFolderName} / {SessionId}";
    }

    public static class RecordedSessionBrowser
    {
        public static List<RecordedSessionOption> FindSessions(
            string storageFolderName = "SessionRecordingData", string persistentDataPath = null)
        {
            var result = new List<RecordedSessionOption>();

            string root = Path.Combine(
                persistentDataPath ?? Application.persistentDataPath,
                storageFolderName
            );

            if (!Directory.Exists(root))
                return result;

            foreach (string workshopPath in Directory.GetDirectories(root))
            {
                string workshopName = Path.GetFileName(workshopPath);

                foreach (string sessionFolderPath in
                    Directory.GetDirectories(workshopPath))
                {
                    string sessionFolderName =
                        Path.GetFileName(sessionFolderPath);

                    foreach (string sessionPath in
                        Directory.GetDirectories(sessionFolderPath))
                    {
                        // Nur tatsächlich gespeicherte Sessions
                        if (!File.Exists(
                            Path.Combine(sessionPath, "session.json")))
                            continue;

                        result.Add(new RecordedSessionOption
                        {
                            WorkshopFolderName = workshopName,
                            SessionFolderName = sessionFolderName,
                            SessionId = Path.GetFileName(sessionPath)
                        });
                    }
                }
            }

            result.Sort((a, b) =>
                string.Compare(
                    a.DisplayName,
                    b.DisplayName,
                    StringComparison.OrdinalIgnoreCase
                ));

            return result;
        }

        public static RecordedSessionOption FindLatestSession(
            string storageFolderName = "SessionRecordingData", string persistentDataPath = null)
        {
            string root = Path.Combine(persistentDataPath ?? Application.persistentDataPath, storageFolderName);
            RecordedSessionOption latest = null;
            DateTime newest = DateTime.MinValue;
            foreach (var session in FindSessions(storageFolderName, persistentDataPath))
            {
                string json = Path.Combine(root, session.WorkshopFolderName, session.SessionFolderName, session.SessionId, "session.json");
                DateTime written = File.GetLastWriteTimeUtc(json);
                if (latest != null && written <= newest) continue;
                newest = written;
                latest = session;
            }
            return latest;
        }
    }
}
