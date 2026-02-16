using System.Collections.Generic;

namespace App.Runtime.Debug
{
    public class MemoryPoseTrack
    {
        public readonly List<PoseFrame> Frames = new();
        public void Clear() => Frames.Clear();
    }
}
