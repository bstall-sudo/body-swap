using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Input;


using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Dialogue.Services;

namespace AppV2.Runtime.Scripts.Dialogue
{
       
    public class ConversationStage : MonoBehaviour
    {

        [Header("Stage Root Transforms (self)")]
        public Transform _stageRoot; // this transform


        [Header("Name for storage folder in ApplicationPersistentDataPath (nur ändern wenn wirklich nötig)")]
        [Header("")]
        [SerializeField]
        public String _storageFolderName = "SessionRecordingData";


        [Header("Roles")]
        [Min(1)]
        public int roleCount = 2;

        private RecordingController _recordingController;
        private PlaybackController _playbackController;
        private SessionTakeIndex _takeIndex;

        public SessionStore _store;
        public SessionModel _session;


        //////////////////////////////////// - für die Roles im Inspektor ///////////////////////////////////////
        // Unity kann Listen serialisieren, wenn das Element [Serializable] ist
        public List<RoleRig> roles = new List<RoleRig>();

        // Wird im Editor aufgerufen, wenn du Werte im Inspector änderst
        private void OnValidate()
        {
            roleCount = Mathf.Max(1, roleCount);

            if (roles == null) roles = new List<RoleRig>();

            // Liste auf gewünschte Länge bringen
            while (roles.Count < roleCount)
            {
                var idx = roles.Count;
                roles.Add(new RoleRig
                {
                    roleId = DefaultRoleId(idx) // "A", "B", "C", ...
                });
            }

            while (roles.Count > roleCount)
            {
                roles.RemoveAt(roles.Count - 1);
            }

            // Falls roleId leer ist, setzen
            for (int i = 0; i < roles.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(roles[i].roleId))
                    roles[i].roleId = DefaultRoleId(i);
            }
        }

        private static string DefaultRoleId(int index)
        {
            // 0->A, 1->B, 2->C ... danach Role 27 etc.
            int a = (int)'A';
            if (index < 26) return ((char)(a + index)).ToString();
            return $"Role {index + 1}";
        }

        //////////////////////////////////// - für die Roles im Inspektor ///////////////////////////////////////

        public IInputTransformsProvider _input;
        private XRInputTransforms _xrInput;
        private KeyboardInputTransforms _keyboardInput;

        [Header("Settings")]
        public bool UseXR = false;
        public float SmoothAlignSeconds = 0.6f;

        [Header("Start im Playback Mode. Grundeinstellung: letzte Aufnahme, sonst SessionId")]
        public bool StartInPlaybackFullConversationMode = false;
        public string FolderSessionId = "";

        [Header("Kann der nächste Sprecher/Zuhörer gewählt werden")]
        public bool selectableNext = false;

        [Header("XR References")]
        public Transform XrHead;
        public Transform XrLeft;
        public Transform XrRight;


        private void Awake()
        {
            _takeIndex = new SessionTakeIndex();
            _store = new SessionStore(_storageFolderName);

            // das Playback-Objekt wird mit einer RoleRig-Liste und dem Ordnernamen initiiert
            _playbackController = new PlaybackController();
            if(StartInPlaybackFullConversationMode){

                UnityEngine.Debug.Log($"startIn Playback Full Conversation Mode. Folder Id is: {FolderSessionId}");
                if(FolderSessionId == ""){
                    //UnityEngine.Debug.Log($"startIn Playback Full Conversation Mode. FolderSessionId is Empty String : {FolderSessionId}+++++++++++++++++++++++++++++++++++++++++++++");
                    InitializePlaybackFromLatestSession();
                }else{
                    InitializePlaybackFromSession(FolderSessionId);
                    /*
                    string folderAndSessionId = Path.Combine(_storageFolderName, FolderSessionId);
                    UnityEngine.Debug.Log($"startIn Playback Full Conversation Mode. FolderSessionId is Empty String : {folderAndSessionId}+++++++++++++++++++++++++++++++++++++++++++++");
                    _store = new SessionStore(folderAndSessionId);
                    */
                }
                

            }else{
                
                _playbackController.Initialize(roles, _store, _takeIndex);
                // hier wird das RecordingController Objekt kreiert mit roleCount, damit RecordingController die entsprechenden Listen anlegen kann.
                _recordingController = new RecordingController(roleCount, _store, _takeIndex);
            }
            
        }
            
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if(roles.Count == 0) {
                UnityEngine.Debug.LogError("No Roles assigned in ConversationStage.");
                return;
            }

            if (UseXR)
            {
                _input = new XRInputTransforms(XrHead, XrLeft, XrRight);

                //Das setzt den Anker für die Transforms siehe XRInputTransforms
                _input.SetAnchorFromTakeRoot(_stageRoot);
        

            }
            else
            {
              
                _input = new KeyboardInputTransforms();
            }

        }

        public void RecordingBegin(int roleIndex, int sceneCount)
        {
            RoleRig role = roles[roleIndex];
            _recordingController.BeginRecording(_stageRoot, role.root, role.roleId, roleIndex, sceneCount, _input);
        }

        public void RecordingTick(int roleIndex, int sceneCount)
        {
            RoleRig role = roles[roleIndex];
            _recordingController.TickRecording( roleIndex, role.roleId, sceneCount);
            
        }

        public void RecordingEnd(int roleIndex, int sceneCount)
        {
            //UnityEngine.Debug.Log($"RecordingEnd called in Conversation Stage roleIndex{roleIndex} sceneCount{sceneCount}");
            RoleRig role = roles[roleIndex];
            _recordingController.EndRecording( roleIndex, role.roleId, sceneCount); 
        }

        public bool RecordingSaveCompleted(){
            if(_recordingController !=null){
                return _recordingController.SaveCompleted();
            }else{
                UnityEngine.Debug.LogError($"there is no _recordingController");
                return true;
            }
            

        }

        
        public void PlaybackStart(List<int> roleIndices,  int sceneCount){

                

                if(_takeIndex.TryGetTakeForScene(roleIndices[0], sceneCount, out TakeMeta takeMeta)){
                    string sessionId = takeMeta.SessionId;
                    _playbackController.PlaybackForIndexListBegin(roleIndices, sceneCount, sessionId);
                } else{
                    UnityEngine.Debug.LogError($"No Take found for scene: {sceneCount}");
                }
        }
      
        public void PlaybackTick(List<int> roleIndices){
            _playbackController.TickForIndexList(roleIndices);
        }

        public bool PlaybacksAreAllStopped(){
            return _playbackController.ArePlaybacksStopped();
        }

        //wird im PlaybackFullConversationState benötigt, damit Playback enden kann, wenn keine Takes vorhanden sind.
        public bool PlaybackHasAnyTakeForScene(int sceneCount)
        {
            return _playbackController.HasAnyTakeForScene(sceneCount);
        }

        //wenn der Folder Session Id String leer is, dann soll die letzte Aufnahme geladen werden.
        public string InitializePlaybackFromLatestSession()
        {
            string latestSessionId = _store.GetLatestSessionId();

            if (string.IsNullOrEmpty(latestSessionId))
            {
                UnityEngine.Debug.LogError("ConversationStage.InitializePlaybackFromLatestSession: No latest session found.");
                return null;
            }
            //UnityEngine.Debug.Log($"startIn Playback Full Conversation Mode. InitializeFromSession : {latestSessionId}+++++++++++++++++++++++++++++++++++++++++++++");

            _playbackController.InitializeFromSession(roles, _store, _takeIndex, latestSessionId);
            return latestSessionId;
        }


        public void InitializePlaybackFromSession(string sessionId)
        {
            
            _playbackController.InitializeFromSession(roles, _store, _takeIndex, sessionId);
        }
                        


        // Wird z.B. im Update() von den States pro Frame aufgerufen
        public void DriveActiveRoleFromInput(int roleIndex)
        {

            if (!_input.TryGetHeadPose(out var headPos, out var headRot))
                return;

            var rig = roles[roleIndex];

            //kann man später löschen, weil das führt nur zu abfragen pro Frame.
            if (_input is KeyboardInputTransforms)
            {
                // Root anhand HeadPose bewegen (Fix 1: nur XZ + yaw)
                Vector3 p = rig.root.position;
                p.x = headPos.x;
                p.z = headPos.z;

                float yaw = headRot.eulerAngles.y;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);

                rig.root.SetPositionAndRotation(p, r);

            } else{
                    var p = _input;
                    if (p == null) return;

                    if (!p.TryGetHeadPose(out var headStage, out var headRotStage)) return;
                    p.TryGetLeftHandPose(out var leftStage, out var leftRotStage);
                    p.TryGetRightHandPose(out var rightStage, out var rightRotStage);

                    // Wenn Align aktiv: ersetze BodyPose (pos+yaw) durch interpolierte, aber behalte relative Head/Hands
                    Vector3 bodyPos = headStage; bodyPos.y = 0f;
                    float yaw = headRotStage.eulerAngles.y;

                    Transform actor = rig.root;
                    Transform h = rig.head;
                    Transform l = rig.leftHand;
                    Transform r = rig.rightHand;

                    actor.localPosition = bodyPos;
                    actor.localRotation = Quaternion.Euler(0f, yaw, 0f);

                    // Head/Hands actor-local setzen (damit sie �kleben�, aber der Body sanft aligned)
                    var invActorRot = Quaternion.Inverse(actor.localRotation);

                    Vector3 ToLocalPos(Vector3 pStage)
                    {
                        Vector3 delta = pStage - bodyPos;
                        return invActorRot * delta;
                    }

                    Quaternion ToLocalRot(Quaternion rStage) => invActorRot * rStage;

                    if (h) { h.localPosition = ToLocalPos(headStage); h.localRotation = ToLocalRot(headRotStage); }
                    if (l) { l.localPosition = ToLocalPos(leftStage); l.localRotation = ToLocalRot(leftRotStage); }
                    if (r) { r.localPosition = ToLocalPos(rightStage); r.localRotation = ToLocalRot(rightRotStage); }
            }
   
        }
/*
        //das wird bei RoleSwitch gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        private void StartPlayerAlignToActor(Transform actor, float duration)
        {
            if (XrOrigin == null || XrHead == null || actor == null) return;

            // From
            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = YawOf(XrOrigin.rotation);

            // Head floor (world)
            Vector3 headFloor = XrHead.position;
            headFloor.y = actor.position.y;

            // We want: headFloor -> actor.position
            Vector3 delta = actor.position - headFloor;
            _playerAlignToPos = XrOrigin.position + delta;

            // Yaw: align head yaw to actor yaw (only yaw)
            float headYaw = YawOf(XrHead.rotation);
            float actorYaw = YawOf(actor.rotation);
            float deltaYaw = Mathf.DeltaAngle(headYaw, actorYaw);

            _playerAlignToYaw = _playerAlignFromYaw + deltaYaw;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;
        }*/
    }

}