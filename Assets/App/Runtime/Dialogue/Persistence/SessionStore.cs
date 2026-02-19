using System;
using System.IO;
using UnityEngine;
//using static System.Net.Mime.MediaTypeNames;

namespace App.Runtime.Dialogue.Persistence
{
    public class SessionStore
    {
        private readonly string _appFolder;

        public SessionStore(string appFolder = "AppData")
        {
            _appFolder = appFolder;
        }

        public string Root => Path.Combine(Application.persistentDataPath, _appFolder, "Sessions");

        public string CreateNewSessionFolder(out string sessionId)
        {
            sessionId = Guid.NewGuid().ToString("N");
            string dir = Path.Combine(Root, sessionId);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetSessionFolder(string sessionId)
            => Path.Combine(Root, sessionId);

        public string GetSessionJsonPath(string sessionId)
            => Path.Combine(GetSessionFolder(sessionId), "session.json");

        public string FramesFileName(string takeId, string speaker)
            => $"{takeId}_{speaker}.frames.jsonl";

        public string AudioFileName(string takeId, string speaker)
            => $"{takeId}_{speaker}.wav";

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
    }
}
