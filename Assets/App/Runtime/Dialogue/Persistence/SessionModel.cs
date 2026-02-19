using System;
using System.Collections.Generic;

namespace App.Runtime.Dialogue.Persistence
{
    [Serializable]
    public class SessionModel
    {
        public string SessionId;
        public string CreatedUtc;

        // Reihenfolge der Takes im Dialog (A1, B1, A2, ...)
        public List<TakeMeta> Takes = new();
    }

    [Serializable]
    public class TakeMeta
    {
        public string TakeId;        // "take_0001"
        public string Speaker;      // "A" oder "B"
        public float DurationSec;

        public string FramesFile;   // "take_0001_A.frames.jsonl"
        public string AudioFile;    // "take_0001_A.wav"
    }
}
