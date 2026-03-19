using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    public class SessionStore
    {
        private readonly string _appFolder;

        private string sessionFolderName = "Sessions";

        public SessionStore(string appFolder = "AppData")
        {
            _appFolder = appFolder;
        }


        

        public string Root => Path.Combine(Application.persistentDataPath, _appFolder, sessionFolderName);

        public string CreateNewSessionFolder(out string sessionId)
        {
            //sessionId = Guid.NewGuid().ToString("N");
            sessionId =  DateTime.UtcNow.ToString("yyyy-MM-dd-UTC-HH-mm-ss_ff");
            string dir = Path.Combine(Root, sessionId);
            Directory.CreateDirectory(dir);
            return dir;
        }

        //public string DirPathToCombineWithApplicationPersistentDataPath => Path.Combine( _appFolder, sessionFolderName, sessionId);

        public string GetSessionFolder(string sessionId)
            => Path.Combine(Root, sessionId);

        public string GetSessionJsonPath(string sessionId)
            => Path.Combine(GetSessionFolder(sessionId), "session.json");

        public string FramesFileName(string takeId)
            => $"{takeId}.frames.jsonl";

        public string AudioFileName(string takeId)
            => $"{takeId}.wav";

        public void SaveSessionModel(SessionModel model)
        {
            string path = GetSessionJsonPath(model.SessionId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(model, true));
        }

        public SessionModel LoadSessionModel(string sessionId)
        {
            string path = GetSessionJsonPath(sessionId);
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SessionModel>(json);
        }


        public string GetLatestSessionId()
        {
            if (!Directory.Exists(Root))
            {
                UnityEngine.Debug.LogWarning($"SessionStore.GetLatestSessionId: Root does not exist: {Root}");
                return null;
            }

            string[] directories = Directory.GetDirectories(Root);

            if (directories == null || directories.Length == 0)
            {
                UnityEngine.Debug.LogWarning("SessionStore.GetLatestSessionId: No session directories found.");
                return null;
            }

            Array.Sort(directories, StringComparer.Ordinal);

            string latestDirectory = directories[^1];
            string latestSessionId = Path.GetFileName(latestDirectory);

            UnityEngine.Debug.Log($"Latest sessionId found: {latestSessionId}");
            return latestSessionId;
        }

        public TakeData LoadTakeData(TakeMeta meta)
        {
            string sessionFolder = GetSessionFolder(meta.SessionId);

            string framesPath = Path.Combine(sessionFolder, meta.FramesFile);
            string audioPath  = Path.Combine(sessionFolder, meta.AudioFile);

            // Frames laden
            List<Frame> frames = new List<Frame>();

            if (File.Exists(framesPath))
            {
                frames = JsonlFrames.ReadAll(framesPath);
            }
            else
            {
                UnityEngine.Debug.LogError($"Frames file not found: {framesPath}");
            }

            // Audio laden
            AudioClip clip = null;

            if (!string.IsNullOrEmpty(meta.AudioFile) && File.Exists(audioPath))
            {
                clip = WavUtility.LoadWav(audioPath);
            }

            // TakeData erzeugen
            TakeData take = new TakeData
            {
                Frames = frames,
                AudioClip = clip,
                DurationSec = meta.DurationSec
            };

            return take;
        }
    }
}
