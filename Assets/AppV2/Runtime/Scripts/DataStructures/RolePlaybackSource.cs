using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.DataStructures
{
    public class RolePlaybackSource
    {
        public SessionStore store;
        public SessionModel session;
        public SessionTakeIndex takeIndex;

        public string sessionId;

        // aktuelle Rolle in der aktiven Szene, z.B. RoleC = 2
        public int targetRoleIndex;

        // Rolle in der Quell-Session, z.B. alte RoleA = 0
        public int sourceRoleIndex;

        public bool isPreRecordedSource;
    }
}