using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.Input;


using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Dialogue.Services;
using AppV2.Runtime.Scripts.Rig;

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
        // This is used, so that CalibrationState can call Methods from AvatarRigFollower and AvatarRigDefinition 
        // e.g. set avatars visible invisible, set rot/pos of RolesVisual Targets (Cubes) to IkChainTargets.  
        [SerializeField] private AvatarCalibrationController avatarCalibration;
        public AvatarCalibrationController AvatarCalibration => avatarCalibration;

        // to set visibility of the (Debug-) Cubes of the RolesVisuals, => is a getter, I can access rolesVisualsVisibilityHandler somewhere else, but I 
        // can not write to it
        [SerializeField] private RolesVisualsVisibilityHandler rolesVisualsVisibilityHandler;
        public RolesVisualsVisibilityHandler RolesVisualsVisibilityHandler  => rolesVisualsVisibilityHandler;

        [Header("Grösse des Spielers")]
        public float heightOfPlayerCm = 180f;
        public float avatarBaseHeightCm = 200f;
        public bool autoPlayerSizeRecognition = true;

        private RecordingController _recordingController;
        private PlaybackController _playbackController;
        private ReactiveIdleController _reactiveIdleController;
        private SessionTakeIndex _takeIndex;

        public SessionStore _store;
        public SessionModel _session;

        // höhenanpassung der Kamera, damit man als kleine Rolle aus der Perspektive des kopfes der kleinere Figur schaut.
        [SerializeField] private Transform embodimentOffsetRoot;
        private float _baseCameraOffsetY;

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
        public bool FullBodyTrackers = false;
        public bool AvatarPlacementAtStart = true;

        [Header("Start im Playback Mode. Grundeinstellung: letzte Aufnahme, sonst SessionId")]
        public bool StartInPlaybackFullConversationMode = false;
        public string FolderSessionId = "";

        [Header("Kann der nächste Sprecher/Zuhörer gewählt werden")]
        public bool selectableNext = false;

        [Header("XR References")]
        public Transform XrHead;
        public Transform XrLeftHand;
        public Transform XrRightHand;
        public Transform XrOrigin;

        // wird für StartPlayerAlignToActor gebraucht.
        //damit man beim RoleSwitch an die Stelle der anderen Figur teleportiert wird
        private Vector3 _playerAlignTargetHeadPosWorld;
        private bool _playerAlignActive;
        private Vector3 _playerAlignFromPos, _playerAlignToPos;
        private float _playerAlignFromYaw, _playerAlignToYaw;
        private float _playerAlignT, _playerAlignDur;
  
        //Helper, damit man beim RoleSwitch an die Stelle der anderen Figur teleportiert wird
        private static float YawOf(Quaternion q) => q.eulerAngles.y;
        private static Quaternion YawRot(float yawDeg) => Quaternion.Euler(0f, yawDeg, 0f);


        private void Awake()
        {
            _takeIndex = new SessionTakeIndex();
            _store = new SessionStore(_storageFolderName);

            // das Playback-Objekt wird mit einer RoleRig-Liste und dem Ordnernamen initiiert
            _playbackController = new PlaybackController();
            _reactiveIdleController = new ReactiveIdleController();

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
                
                
                
                _playbackController.Initialize(roles, heightOfPlayerCm, _store, _takeIndex);
                // hier wird das RecordingController Objekt kreiert mit roleCount, damit RecordingController die entsprechenden Listen anlegen kann.
                _recordingController = new RecordingController(roles, roleCount, _store, _takeIndex);

                
            }
            _reactiveIdleController.Initialize(roles);

            InitializeRoleHeightsFromPlayerIfNeeded();

            // used in CalibrationState
            avatarCalibration.Initialize(roles);
            // used in CalibrationState to toggle visibility of the (Debug-) cubes of RolesVisuals
            rolesVisualsVisibilityHandler.Initialize(roles);
            
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
                _input = new XRInputTransforms(XrHead, XrLeftHand, XrRightHand);

                //Das setzt den Anker für die Transforms siehe XRInputTransforms
                _input.SetAnchorFromTakeRoot(_stageRoot);
                //Für die Höhenanpassung der MainCamera bei Rollen mit unterschiedlichen Grössen
                if (embodimentOffsetRoot != null)
                {
                    _baseCameraOffsetY = 0f;
                    UnityEngine.Debug.Log($"Base CameraOffset Y = {_baseCameraOffsetY}");
                }
                    

            }
            else
            {
              
                _input = new KeyboardInputTransforms();
            }

            //Avatare Grösse anpassen
            ApplyAllRoleVisualScales();

        }

        public void RecordingBegin(int roleIndex, int sceneCount)
        {
            UnityEngine.Debug.Log($"[BeginnRecording] RoleIndex: {roleIndex} XR HEAD WORLD Y before recording: {XrHead.position.y}");
            UnityEngine.Debug.Log($"[BeginnRecording] RoleIndex: {roleIndex} XR ORIGIN WORLD POS before recording: {XrOrigin.position}");
            RoleRig role = roles[roleIndex];
            float roleScale = (float)roles[roleIndex].heightOfRoleCm / heightOfPlayerCm;

            //public void BeginRecording(Transform stageRoot, float roleScale, int roleIndex,  int sceneCount, IInputTransformsProvider input)
            _recordingController.BeginRecording(_stageRoot, roleScale, roleIndex, sceneCount, _input);
        }

        public void RecordingTick(int roleIndex, int sceneCount)
        {
            RoleRig role = roles[roleIndex];

            //// damit das embodimentOffset im Recording auch berücksichtigt wird:
            float embodimentDeltaY = embodimentOffsetRoot.localPosition.y - _baseCameraOffsetY;
            _recordingController.TickRecording(roleIndex, role.roleId, sceneCount, embodimentDeltaY);
            //_recordingController.TickRecording( roleIndex, role.roleId, sceneCount);
            
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
                    _playbackController.PlaybackForIndexListBegin(roleIndices, heightOfPlayerCm, sceneCount, sessionId);
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

        public void ReactiveIdleStart(List<int> reactiveIdles, int speakerRoleIndex)
        {
            if (reactiveIdles == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int roleIndex = reactiveIdles[i];
                _reactiveIdleController.SetRoleToIdleLookingAt(roleIndex, speakerRoleIndex);
            }
        }

        public void ReactiveIdleTick(List<int> reactiveIdles, int speakerRoleIndex)
        {
            if (reactiveIdles == null) return;

            _reactiveIdleController.UpdateIdleLookTargets(reactiveIdles, speakerRoleIndex);
        }

        public void ReactiveIdleEnd(List<int> reactiveIdles)
        {
            if (reactiveIdles == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int roleIndex = reactiveIdles[i];
                _reactiveIdleController.SetRoleToRecordPlayback(roleIndex);
            }
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
                        

/*
        // Wird z.B. im Update() von den States pro Frame aufgerufen
        public void DriveActiveRoleFromInput(int roleIndex)
        {
            if (!_input.TryGetHeadPose(out var headPos, out var headRot))
                return;

            var rig = roles[roleIndex];

            // Keyboard-Modus erstmal unverändert lassen
            // (kann man später auch auf roleScale umbauen, wenn gewünscht)
            if (_input is KeyboardInputTransforms)
            {
                Vector3 p = rig.root.position;
                p.x = headPos.x;
                p.z = headPos.z;

                float yaw = headRot.eulerAngles.y;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);

                rig.root.SetPositionAndRotation(p, r);
            }
            else
            {
                var p = _input;
                if (p == null) return;

                float embodimentDeltaY = 0f;
                if (embodimentOffsetRoot != null)
                {
                    embodimentDeltaY = embodimentOffsetRoot.localPosition.y - _baseCameraOffsetY;
                }

                if (!p.TryGetHeadPose(out var headStage, out var headRotStage)) return;
                p.TryGetLeftHandPose(out var leftStage, out var leftRotStage);
                p.TryGetRightHandPose(out var rightStage, out var rightRotStage);


                //hier muss jetzt der Höhenunterschied des EmbodimentCameraOffsets wieder rausgerechnet werden.
                headStage.y -= embodimentDeltaY;
                leftStage.y -= embodimentDeltaY;
                rightStage.y -= embodimentDeltaY;

                // Rollengröße semantisch berechnen, nicht über visualRigRoot.localScale
                float roleScale = 1f;
                if (heightOfPlayerCm > 0.01f)
                {
                    roleScale = (float)rig.heightOfRoleCm / heightOfPlayerCm;
                }

                // Body aus Head ableiten
                Vector3 bodyPos = headStage;
                bodyPos.y = 0f;

                float yaw = headRotStage.eulerAngles.y;

                Transform role = rig.root;
                Transform h = rig.head;
                Transform l = rig.leftHand;
                Transform r = rig.rightHand;

                // Body bleibt unskaliert stage-local
                role.localPosition = bodyPos;
                role.localRotation = Quaternion.Euler(0f, yaw, 0f);

                // Head/Hands relativ zum Body, dann embodied auf Rollenmaß skalieren
                Quaternion invActorRot = Quaternion.Inverse(role.localRotation);

                Vector3 ToLocalPosScaled(Vector3 pStage)
                {
                    Vector3 delta = pStage - bodyPos;
                    Vector3 localNeutral = invActorRot * delta;
                    Vector3 localEmbodied = localNeutral * roleScale;
                    return localEmbodied;
                }

                Quaternion ToLocalRot(Quaternion rStage)
                {
                    return invActorRot * rStage;
                }

                if (h)
                {
                    h.localPosition = ToLocalPosScaled(headStage);
                    h.localRotation = ToLocalRot(headRotStage);

                }

                if (l)
                {
                    l.localPosition = ToLocalPosScaled(leftStage);
                    l.localRotation = ToLocalRot(leftRotStage);
                }

                if (r)
                {
                    r.localPosition = ToLocalPosScaled(rightStage);
                    r.localRotation = ToLocalRot(rightRotStage);
                }

                // Optionales Debug:
                // UnityEngine.Debug.Log($"DriveActiveRoleFromInput role={rig.roleId} roleScale={roleScale} headLocal={h?.localPosition}");
            }
        }
*/

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


                    float embodimentDeltaY = 0f;
                    if (embodimentOffsetRoot != null)
                    {
                        embodimentDeltaY = embodimentOffsetRoot.localPosition.y - _baseCameraOffsetY;
                    }
                  


                    if (!p.TryGetHeadPose(out var headStage, out var headRotStage)) return;
                    p.TryGetLeftHandPose(out var leftStage, out var leftRotStage);
                    p.TryGetRightHandPose(out var rightStage, out var rightRotStage);


                    //hier muss jetzt der Höhenunterschied des EmbodimentCameraOffsets wieder rausgerechnet werden.
                    headStage.y -= embodimentDeltaY;
                    leftStage.y -= embodimentDeltaY;
                    rightStage.y -= embodimentDeltaY;
                    

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
        //das wird bei RoleSwitch von den AlignStates im Enter() gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        //Diese Funktion berechnet nur Start- und ZielPosition, um wieviel muss das XrRig bewegt werden, damit XR-Head am richtigen Ort landet.
        public void StartPlayerAlignToActor(int roleIndex, float duration)
        {
            if (XrOrigin == null || XrHead == null || _stageRoot == null)
            {
                UnityEngine.Debug.LogError("XrOrigin, XrHead or _stageRoot == null");
                return;
            }

            if (roleIndex < 0 || roleIndex >= roles.Count)
            {
                UnityEngine.Debug.LogError($"Invalid roleIndex: {roleIndex}");
                return;
            }

            if (!_recordingController.TryGetLastEndPose(roleIndex, out Vector3 targetBodyPosLocal, out float targetBodyYaw))
            {
                UnityEngine.Debug.LogWarning($"No last Body end pose found for roleIndex {roleIndex}");
                return;
            }

            if (!_recordingController.TryGetLastHeadEndPose(roleIndex, out Vector3 targetHeadPosBodyLocal, out float targetHeadYawLocal))
            {
                UnityEngine.Debug.LogWarning($"No last Head end pose found for roleIndex {roleIndex}");
                return;
            }

            if (roles[roleIndex].visualRigRoot == null)
            {
                UnityEngine.Debug.LogWarning($"visualRigRoot is null for roleIndex {roleIndex}");
                return;
            }

            if (roles[roleIndex].avatarRoot == null)
            {
                UnityEngine.Debug.LogWarning($"AvatarRoot is null for roleIndex {roleIndex}");
                return;
            }
  

            // 1) Body-Rotation im Stage-Lokalraum
            Quaternion bodyRotStageLocal = Quaternion.Euler(0f, targetBodyYaw, 0f);

      
            float roleScale = 1f;
            // Gleichmäßige Skalierung annehmen
            if (heightOfPlayerCm > 0.01f)
            {
                roleScale = (float)roles[roleIndex].heightOfRoleCm / heightOfPlayerCm;
            }
            // 2) Head-Offset relativ zum Body skalieren
            Vector3 scaledHeadPosBodyLocal = targetHeadPosBodyLocal * roleScale;

            // 3) Head aus body-local in stage-local rekonstruieren
            Vector3 targetHeadPosStageLocal =
                targetBodyPosLocal + (bodyRotStageLocal * scaledHeadPosBodyLocal);
            
            // 4) HeadYaw aus bodyYaw + localHeadYaw rekonstruieren
            float targetHeadYawStage = Mathf.Repeat(targetBodyYaw + targetHeadYawLocal, 360f);

            // 5) Stage-local -> World
            Vector3 targetHeadPosWorld = _stageRoot.TransformPoint(targetHeadPosStageLocal);

            _playerAlignTargetHeadPosWorld = targetHeadPosWorld;

            // Startwerte merken
            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = YawOf(XrOrigin.rotation);

            // Ziel-Yaw berechnen
            float currentHeadYaw = YawOf(XrHead.rotation);
            float deltaYaw = Mathf.DeltaAngle(currentHeadYaw, targetHeadYawStage);
            _playerAlignToYaw = _playerAlignFromYaw + deltaYaw;

            // Lokalen Head-Offset innerhalb des XR-Rigs holen
            Vector3 headLocal = XrOrigin.InverseTransformPoint(XrHead.position);

            // Diesen Offset mit Zielrotation in World drehen
            Quaternion targetOriginRotation = Quaternion.Euler(0f, _playerAlignToYaw, 0f);
            Vector3 rotatedHeadOffsetWorld = targetOriginRotation * headLocal;

            // Rig so platzieren, dass XR-Head exakt auf targetHeadPosWorld landet
            _playerAlignToPos = targetHeadPosWorld - rotatedHeadOffsetWorld;
            //hier die höhe wieder auf 0, bzw. auf die aktuelle y höhe setzen, denn diese wird im CameraOffset angewendet.
            _playerAlignToPos.y = _playerAlignFromPos.y;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;

            /*
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex} TECH ROOT SCALE role {roleIndex}: {roles[roleIndex].root.lossyScale}");
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex} TECH HEAD SCALE role {roleIndex}: {roles[roleIndex].head.lossyScale}");
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex} STAGE ROOT SCALE: {_stageRoot.lossyScale}");
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex}  XR ORIGIN SCALE: {XrOrigin.lossyScale}");
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex} XR HEAD SCALE: {XrHead.lossyScale}");

            UnityEngine.Debug.Log($"[StartAlignEnd]RoleIndex: {roleIndex}  XR HEAD WORLD Y after align: {XrHead.position.y}");
            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex} XR ORIGIN WORLD POS after align: {XrOrigin.position}");

            UnityEngine.Debug.Log($"[AlignTick] RoleIndex: {roleIndex}  targetBodyPosLocal={targetBodyPosLocal}");
            UnityEngine.Debug.Log($"[AlignTick] RoleIndex: {roleIndex}  targetHeadPosBodyLocal={targetHeadPosBodyLocal}");
            UnityEngine.Debug.Log($"[AlignTick] RoleIndex: {roleIndex}  targetBodyYaw={targetBodyYaw}");
            UnityEngine.Debug.Log($"[AlignTick] RoleIndex: {roleIndex}  targetHeadYawLocal={targetHeadYawLocal}");

            UnityEngine.Debug.Log($"[StartAlignEnd] RoleIndex: {roleIndex}  XR ORIGIN Y-Position: {XrOrigin.position.y}");

            
            UnityEngine.Debug.Log(
                $"StartPlayerAlignToActor: roleIndex={roleIndex}, " +
                $"targetBodyPosLocal={targetBodyPosLocal}, targetBodyYaw={targetBodyYaw}, " +
                $"targetHeadPosBodyLocal={targetHeadPosBodyLocal}, targetHeadYawLocal={targetHeadYawLocal}, " +
                $"targetHeadPosStageLocal={targetHeadPosStageLocal}, targetHeadYawStage={targetHeadYawStage}, " +
                $"targetHeadPosWorld={targetHeadPosWorld}, toPos={_playerAlignToPos}, toYaw={_playerAlignToYaw}"
            );
            */
        }
                        
        
        /*
        public void StartPlayerAlignToActor(int roleIndex, float duration)
        {
            if (XrOrigin == null || XrHead == null || _stageRoot == null)
            {
                UnityEngine.Debug.LogError("XrOrigin, XrHead or _stageRoot == null");
                return;
            }

            if (roleIndex < 0 || roleIndex >= roles.Count)
            {
                UnityEngine.Debug.LogError($"Invalid roleIndex: {roleIndex}");
                return;
            }

            if (!_recordingController.TryGetLastEndPose(roleIndex, out Vector3 targetBodyPosLocal, out float targetBodyYaw))
            {
                UnityEngine.Debug.LogWarning($"No last Body end pose found for roleIndex {roleIndex}");
                return;
            }

            if (!_recordingController.TryGetLastHeadEndPose(roleIndex, out Vector3 targetHeadPosLocal, out float targetHeadYaw))
            {
                UnityEngine.Debug.LogWarning($"No last Head end pose for found for roleIndex {roleIndex}");
                return;
            }

            // Gespeicherte Body-Position aus Stage-Lokal in World umrechnen
            Vector3 targetBodyPosWorld = _stageRoot.TransformPoint(targetBodyPosLocal);
            Vector3 targetHeadPosWorld = _stageRoot.TransformPoint(targetHeadPosLocal);

            // Startzustand merken
            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = YawOf(XrOrigin.rotation);

            // Ziel-Yaw berechnen:
            // Der Kopf des Users soll am Ende in dieselbe Yaw-Richtung schauen wie die Zielrolle
            float currentHeadYaw = YawOf(XrHead.rotation);
            float deltaYaw = Mathf.DeltaAngle(currentHeadYaw, targetBodyYaw);
            _playerAlignToYaw = _playerAlignFromYaw + deltaYaw;

            // Lokalen Kopf-Offset innerhalb des XR-Rigs holen
            // Wichtig: nur XZ, nicht Y
            Vector3 headLocal = XrOrigin.InverseTransformPoint(XrHead.position);
            headLocal.y = 0f;

            // Jetzt schon die Zielrotation berücksichtigen
            Quaternion targetOriginRotation = Quaternion.Euler(0f, _playerAlignToYaw, 0f);
            Vector3 rotatedHeadOffsetWorld = targetOriginRotation * headLocal;

            // Rig so platzieren, dass der Kopf-Floor nach der Rotation auf targetBodyPosWorld landet
            _playerAlignToPos = targetBodyPosWorld - rotatedHeadOffsetWorld;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;

            UnityEngine.Debug.Log(
                $"StartPlayerAlignToActor: roleIndex={roleIndex}, " +
                $"targetBodyPosWorld={targetBodyPosWorld}, targetBodyYaw={targetBodyYaw}, " +
                $"headLocal={headLocal}, rotatedHeadOffsetWorld={rotatedHeadOffsetWorld}, " +
                $"toPos={_playerAlignToPos}, toYaw={_playerAlignToYaw}"
            );
        }
        */

        //das wird bei RoleSwitch von den AlignStates im Tick() gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        //Diese Funktion verschiebt das XrRig, damit XR-Head am richtigen Ort landet.
        public void TickPlayerAlign()
        {
            if (!_playerAlignActive || XrOrigin == null || XrHead == null)
                return;

            _playerAlignT += Time.deltaTime;
            float u = Mathf.Clamp01(_playerAlignT / _playerAlignDur);

            float currentOriginYaw = YawOf(XrOrigin.rotation);
            float yaw = Mathf.LerpAngle(currentOriginYaw, _playerAlignToYaw, u);
            Quaternion targetOriginRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 currentHeadLocal = XrOrigin.InverseTransformPoint(XrHead.position);
            Vector3 rotatedHeadOffsetWorld = targetOriginRotation * currentHeadLocal;
            Vector3 desiredOriginPos = _playerAlignTargetHeadPosWorld - rotatedHeadOffsetWorld;

            Vector3 currentOriginPos = XrOrigin.position;

            // nur XZ glätten
            Vector3 pos = currentOriginPos;
            pos.x = Mathf.Lerp(currentOriginPos.x, desiredOriginPos.x, u);
            pos.z = Mathf.Lerp(currentOriginPos.z, desiredOriginPos.z, u);

            // Y bewusst unverändert lassen
            pos.y = currentOriginPos.y;

            XrOrigin.position = pos;
            XrOrigin.rotation = targetOriginRotation;

            if (u >= 1f)
            {
                _playerAlignActive = false;
            }
        }
        //Diese alte TickPlayerAlign Funktion berücksichtigt nicht, dass sich user auch bewegen während des aligns.
        /*
        public void TickPlayerAlign()
        {
            if (!_playerAlignActive || XrOrigin == null)
                return;
        

            _playerAlignT += Time.deltaTime;
            float u = Mathf.Clamp01(_playerAlignT / _playerAlignDur);

            Vector3 pos = Vector3.Lerp(_playerAlignFromPos, _playerAlignToPos, u);
            float yaw = Mathf.LerpAngle(_playerAlignFromYaw, _playerAlignToYaw, u);

            XrOrigin.position = pos;
            XrOrigin.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (u >= 1f)
            {
                _playerAlignActive = false;
            }


        }
        */
        public bool PlayerAlignFinished(){
            //UnityEngine.Debug.Log("TickPlayerAlign: finished");
            return !_playerAlignActive;
            
        }

        //Grösse des Spielers, entweder aufgrund von XR Headset zu Boden Distanz, oder die definierte Variable
        public float GetCurrentPlayerHeightCm()
        {

            if (UseXR && XrHead != null && _stageRoot != null && autoPlayerSizeRecognition)
            {
                float heightMeters = XrHead.position.y - _stageRoot.position.y;
                return heightMeters * 100f;
            }

            return heightOfPlayerCm;
        }

        // Anpassung der Einträge im RoleRig auf SpielerGrösse, wenn keine anderen Werte existieren, ansonsten sind die Rollen so gross, wie im Inspector vermerkt.
        private void InitializeRoleHeightsFromPlayerIfNeeded()
        {
            int playerHeightCm = Mathf.RoundToInt(GetCurrentPlayerHeightCm());

            foreach (var role in roles)
            {
                if (role != null && role.usePlayerHeightAsDefault && role.heightOfRoleCm <= 0)
                {
                    role.heightOfRoleCm = playerHeightCm;
                }
            }
        }

        // hier werden dann die Avatare skaliert, den Einträgen aus dem RoleRig für jede Rolle entsprechend.
        public void ApplyRoleVisualScale(RoleRig role, float playerHeightCm)
        {
            if (role == null || role.visualRigRoot == null || role.avatarRoot == null)
                return;

            if (playerHeightCm <= 0.01f)
                return;

            //float scale = (float)role.heightOfRoleCm / playerHeightCm;

            float visualScale = role.heightOfRoleCm / playerHeightCm;
            float avatarScale = role.heightOfRoleCm / avatarBaseHeightCm;
            

            
            role.visualRigRoot.localScale = Vector3.one * visualScale;

            // Das muss ev. angepasst werden, da die Figuren zurzeit standardmässig 2 m gross sind.
            role.avatarRoot.localScale = Vector3.one * avatarScale;// * avatarScale;

            Vector3 p = role.visualRigRoot.localPosition;
            p.y = role.visualGroundOffsetY;
            role.visualRigRoot.localPosition = p;

            UnityEngine.Debug.Log(
                $"ApplyRoleVisualScale: role={role.roleId}, roleHeight={role.heightOfRoleCm}, " +
                $"playerHeight={playerHeightCm}, avatarScale={avatarScale}, visualScale={visualScale} groundOffsetY={role.visualGroundOffsetY}"
            );
        }

        //Das wird im Start gerufen um die Avatare passend zu skalieren. 
        public void ApplyAllRoleVisualScales()
        {
            float playerHeightCm = GetCurrentPlayerHeightCm();

            foreach (var role in roles)
            {
                ApplyRoleVisualScale(role, playerHeightCm);
            }
        }

/*
        public void ApplyVisualScaleToConversationStage(int roleIndex)
        {
            RoleRig role = roles[roleIndex];
            if (role == null || role.visualRigRoot == null)
                return;

            if (heightOfPlayerCm <= 0.01f)
                return;

            float scale = (float)role.heightOfRoleCm / heightOfPlayerCm;

            
            _stageRoot.localScale = Vector3.one / scale;
            role.visualRigRoot.localScale = Vector3.one * scale;

            Vector3 p = role.visualRigRoot.localPosition;
            p.y = role.visualGroundOffsetY;
            role.visualRigRoot.localPosition = p;

            UnityEngine.Debug.Log(
                $"ApplyVisualScaleToConversationStage: role={role.roleId}, roleHeight={role.heightOfRoleCm}, " +
                $"playerHeight={heightOfPlayerCm}, scale={scale}, groundOffsetY={role.visualGroundOffsetY}" +
                $"WorldScale={_stageRoot.localScale}"
            );
        }

        public void ResetVisualScaleOfConversationStage(int roleIndex)
        {
            RoleRig role = roles[roleIndex];
            if (role == null || role.visualRigRoot == null)
                return;

            if (heightOfPlayerCm <= 0.01f)
                return;

            float scale = (float)role.heightOfRoleCm / heightOfPlayerCm;

            
            _stageRoot.localScale = Vector3.one;
            role.visualRigRoot.localScale = Vector3.one * scale;

            Vector3 p = role.visualRigRoot.localPosition;
            p.y = role.visualGroundOffsetY;
            role.visualRigRoot.localPosition = p;

            UnityEngine.Debug.Log(
                $"ApplyVisualScaleToConversationStage: role={role.roleId}, roleHeight={role.heightOfRoleCm}, " +
                $"playerHeight={heightOfPlayerCm}, scale={scale}, groundOffsetY={role.visualGroundOffsetY}" +
                $"WorldScale={_stageRoot.localScale}"
            );
        }
        */

        // höhenanpassung der Kamera, damit man als kleine Rolle aus der Perspektive des kopfes der kleinere Figur schaut.
        // Funktion wird im Enter() des RecordSpeaker- und RecordListenersState gerufen.
        public void ApplyActiveRoleEmbodimentHeight(int roleIndex)
        {
            if (embodimentOffsetRoot == null)
                return;

            if (roleIndex < 0 || roleIndex >= roles.Count)
                return;

            //die Augenhöhe ist ca. auf 92% der Körpergrösse
            float playerEyeHeightCm = heightOfPlayerCm * 0.92f;
            float roleEyeHeightCm = roles[roleIndex].heightOfRoleCm * 0.92f;

            float deltaM = (roleEyeHeightCm - playerEyeHeightCm) / 100f;

            Vector3 p = embodimentOffsetRoot.localPosition;
            UnityEngine.Debug.Log($"[ApplyActiveRoleEmbodimentHeight]: EmbodimentOffsetRoot of Role={roles[roleIndex].roleId} is: {embodimentOffsetRoot.localPosition}");
            p.y = _baseCameraOffsetY + deltaM;
            embodimentOffsetRoot.localPosition = p;

            UnityEngine.Debug.Log(
                $"ApplyActiveRoleEmbodimentHeight: role={roles[roleIndex].roleId}, " +
                $"baseY={_baseCameraOffsetY}, deltaM={deltaM}, newY={p.y}"
            );
        }

        // höhenanpassung der Kamera, damit man als kleine Rolle aus der Perspektive des kopfes der kleinere Figur schaut.
        // Funktion wird im Exit() des RecordSpeaker- und RecordListenersState gerufen, damit man nachher wieder auf der 0 Ebene ist.
        public void ResetEmbodimentHeight()
        {
            if (embodimentOffsetRoot == null)
                return;

            Vector3 p = embodimentOffsetRoot.localPosition;
            p.y = _baseCameraOffsetY;
            embodimentOffsetRoot.localPosition = p;
        }



    }



}