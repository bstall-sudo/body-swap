using UnityEngine;

namespace AppV2.Runtime.Scripts.DataStructures
{
    [System.Serializable]
    public class PreRecordedSceneImport
    {
        public bool enabled = true;
        public string npcGroupId = "NpcGroup_";

        public string sessionSourcePath;
        public string workshopFolderName;
        public string sessionFolderName;
        public string sessionId;
        

        public string npcGroupSpawnId = "default";
    }
    
}
