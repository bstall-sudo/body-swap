using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.Persistence
{
    public static class JsonlFrames
    {
        public static void WriteAll(string path, List<Frame> frames)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var sw = new StreamWriter(path);

            foreach (var f in frames)
            {
                sw.WriteLine(JsonUtility.ToJson(f));
            }
        }

        public static List<Frame> ReadAll(string path)
        {
            var list = new List<Frame>();
            using var sr = new StreamReader(path);

            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var f = JsonUtility.FromJson<Frame>(line);
                list.Add(f);
            }
            return list;
        }
    }
}
