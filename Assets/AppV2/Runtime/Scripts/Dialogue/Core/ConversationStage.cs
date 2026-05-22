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

        //Um die Spiegel auszuschalten, welche für den CalibrationState gebraucht werden.
        [SerializeField] private MirrorSetVisibility mirrorSetVisibility;
        public MirrorSetVisibility MirrorSetVisibility  => mirrorSetVisibility;

        //Um die Münder bzw. Gesichter der Avatare zu animieren.
        private AnimateBlendShapesViaAudioSources _blendShapeAudioAnimator;
        public AnimateBlendShapesViaAudioSources BlendShapeAudioAnimator => _blendShapeAudioAnimator;

        [Header("Grösse des Spielers")]
        public float heightOfPlayerCm = 180f;
        public float heightOfSeatedPlayerCm = 133f;
        public float avatarBaseHeightCm = 200f;
        public bool autoPlayerSizeRecognition = true;

        private RecordingController _recordingController;
        private PlaybackController _playbackController;
        private ReactiveIdleController _reactiveIdleController;
        private SessionTakeIndex _takeIndex;

        public SessionStore _store;
        public SessionModel _session;
        public RoleCalibrationDataProvider _calibrationDataProvider;
        public RoleCalibrationDataApplier _calibrationDataApplier;


        // höhenanpassung der Kamera, damit man als kleine Rolle aus der Perspektive des kopfes der kleinere Figur schaut.
        [SerializeField] private Transform embodimentOffsetRoot;
        private float _baseCameraOffsetY;
        //das wird gebraucht für die StartPlayerAlignToActorSeated, damit das XR Rig auf den Role Root springen kann.
        private Vector3 _playerAlignTargetOriginPosWorld;
        // diese Variable entscheided, ob TickPlayerAlign den SeatedMode, oder den StandingMode verwendet.
        private bool _playerAlignUseHeadOffset = true;

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

                    roles[i].ResolveAvatarName(false);
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
        public bool SeatedMode = false;

        public bool ProceduralHipAndFeetMove = false;
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
        public Transform XrHip;
        public Transform XrLeftFoot;
        public Transform XrRightFoot;

        [SerializeField] private float fallbackFootSpacing = 0.2f;
        [SerializeField] private float fallbackHipHeight = 0.9f;

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
                _calibrationDataApplier = new RoleCalibrationDataApplier();
                _calibrationDataApplier.Initialize(roles);
                _calibrationDataApplier.ApplyRoleMetasToScene(roles, _playbackController._session);
                MirrorSetVisibility.ActivateMirror(false);
                TurnOffVisibilityOfVisualRig();
                

            }else{
                
                // hier werden jetzt die avatarName für jede Rolle gesetzt. hier sind nun die LogWarnings auf true
                // im OnValidate waren die Logwarnings auf false, damit man nicht mit Warnungen zugespammt wird.
                for (int i = 0; i < roles.Count; i++)
                {
                    roles[i].ResolveAvatarName(true);
                }
                
                _playbackController.Initialize(roles, heightOfPlayerCm, _store, _takeIndex);
                // hier wird das RecordingController Objekt kreiert mit roleCount, damit RecordingController die entsprechenden Listen anlegen kann.
                _recordingController = new RecordingController(roles, roleCount, _store, _takeIndex);
                //das ist wichtig, damit dei Targets vom visualRig, welche den IKChainTargets vom Avatar (im CalibrationState) angeglichen werden im SessionModel abgespeichert werden können
                // Das passiert im Exit von CalibrationState.
                _calibrationDataProvider = new RoleCalibrationDataProvider();
                _calibrationDataProvider.Initialize(roles); 

                

                
            }
            //Für die Animation der Münder
            _blendShapeAudioAnimator = new AnimateBlendShapesViaAudioSources();
            _blendShapeAudioAnimator.Initialize(roles);
            _reactiveIdleController.Initialize(roles, _blendShapeAudioAnimator);

            InitializeRoleHeightsFromPlayerIfNeeded();

            // used in CalibrationState
            avatarCalibration.Initialize(roles);
            // used in CalibrationState to toggle visibility of the (Debug-) cubes of RolesVisuals
            rolesVisualsVisibilityHandler.Initialize(roles);
            
        }
        private void TurnOffVisibilityOfVisualRig()
        {
            //UnityEngine.Debug.Log("TurnOffVisibilityOfVisualRig was called");
            for (int i = 0; i < roles.Count; i++){
                roles[i].visualRolesVisibility.SetVisible(false);
            }

        }

        //called in CalibrationState
        public void PlaceMirrorInFrontOfPlayer()
        {
            MirrorSetVisibility.PlaceMirrorInFrontOfAvatar(roles[0].visualRigRoot);
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
                _input = new XRInputTransforms(XrHead, XrLeftHand, XrRightHand, XrHip, XrLeftFoot, XrRightFoot);

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

        public void SaveTargetTransformsAfterCalibration(){
            
            _recordingController.SaveTargetTransformsToSessionModel(_calibrationDataProvider.CreateRoleMetas(), SeatedMode);

        }


        public void RecordingBegin(int roleIndex, int sceneCount)
        {
            //UnityEngine.Debug.Log($"[BeginnRecording] RoleIndex: {roleIndex} XR HEAD WORLD Y before recording: {XrHead.position.y}");
            //UnityEngine.Debug.Log($"[BeginnRecording] RoleIndex: {roleIndex} XR ORIGIN WORLD POS before recording: {XrOrigin.position}");
            RoleRig role = roles[roleIndex];
            float roleScale = (float)roles[roleIndex].heightOfRoleCm / heightOfPlayerCm;
            avatarCalibration.SetAvatarHeadVisible(roleIndex,false);
            //damit die Figur entweder sitting oder standing Idle als Grundposition hat.
            ApplyRecordingAvatarPoseForRole(roleIndex);
            //public void BeginRecording(Transform stageRoot, float roleScale, int roleIndex,  int sceneCount, IInputTransformsProvider input)
            _recordingController.BeginRecording(_stageRoot, roleScale, roleIndex, sceneCount, _input);
        }

        public void RecordingTick(int roleIndex, int sceneCount)
        {
            RoleRig role = roles[roleIndex];

            _recordingController.TickRecording(roleIndex, role.roleId, sceneCount);  
            
        }

        public void RecordingEnd(int roleIndex, int sceneCount)
        {
            //UnityEngine.Debug.Log($"RecordingEnd called in Conversation Stage roleIndex{roleIndex} sceneCount{sceneCount}");
            RoleRig role = roles[roleIndex];
            _recordingController.EndRecording( roleIndex, role.roleId, sceneCount); 
            avatarCalibration.SetAvatarHeadVisible(roleIndex,true);
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
            bool copyRootForPlayback = true;
            // hier true, weil im PlaybackMode auch der Root Position und Rotation kopiert werden soll, wenn RoleRig.sittingIdle == true.
            // im RecordMode soll der Root nicht mitkopiert werden, wenn SeatedMode.
            RigUpdatePipeline(roleIndices, copyRootForPlayback);
            _blendShapeAudioAnimator.Tick(Time.deltaTime, roleIndices);

        }

        public void RigUpdatePipeline(List<int> roleIndices, bool copyRootPosRot)
        {
            for (int i = 0; i < roleIndices.Count; i++)
            {
                int roleIndex = roleIndices[i];

                ApplyFollower(roleIndex, copyRootPosRot);   
            }  
        }

        public bool PlaybacksAreAllStopped(){
            return _playbackController.ArePlaybacksStopped();
        }

        //wird im PlaybackFullConversationState benötigt, damit Playback enden kann, wenn keine Takes vorhanden sind.
        public bool PlaybackHasAnyTakeForScene(int sceneCount)
        {
            return _playbackController.HasAnyTakeForScene(sceneCount);
        }
/*
        //damit avatar im Sitting Idle ist, während der Aufnahme, das wird hier im conversationStage von RecordingBegin gerufen. 
        public void ApplyRecordingAvatarPoseForRole(int roleIndex)
        {
            if (roleIndex < 0 || roleIndex >= roles.Count)
                return;

            var role = roles[roleIndex];

            if (role.avatar == null)
                return;

            //role.avatar.SetRigModeRecordPlayback();
            _reactiveIdleController.SetRoleToRecordPlayback(roleIndex, SeatedMode);
        }
*/
        //damit avatar im Sitting Idle ist, während der Aufnahme, das wird hier im conversationStage von RecordingBegin gerufen. 
        public void ApplyRecordingAvatarPoseForRole(int roleIndex)
        {
            if (roleIndex < 0 || roleIndex >= roles.Count)
                return;

            var role = roles[roleIndex];

            if (role.avatar == null)
                return;

            bool seated = SeatedMode || role.sittingIdle;

            /*
            role.avatar.SetRigModeRecordPlayback();
            role.avatar.PlayBasePose(
                seated ? AvatarBasePose.SittingIdle : AvatarBasePose.TPose
            );*/
            role.avatar.SetLowerBodyIKWeight(seated ? 0f : 1f);
            _reactiveIdleController.SetRoleToRecordPlayback(roleIndex, SeatedMode);

            
        }

        public void ReactiveIdleStart(List<int> reactiveIdles, int speakerRoleIndex)
        {
            if (reactiveIdles == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int roleIndex = reactiveIdles[i];
                _reactiveIdleController.SetRoleToIdleLookingAt(roleIndex, speakerRoleIndex, SeatedMode);
            }
        }

        public void ReactiveIdleTick(List<int> reactiveIdles, List<int> playbacks, int speakerRoleIndex, float dt)
        {
            if (reactiveIdles == null) return;
            //_reactiveIdleController.UpdateIdleLookTargets(reactiveIdles, speakerRoleIndex);

            // diese Funktion führt zu Performanceproblemen.
            //_reactiveIdleController.UpdateIdleLookTargetsToLoudestSpeaker(reactiveIdles, playbacks, speakerRoleIndex, dt);

            
            _reactiveIdleController.UpdateIdleLookTargetsToLoudestSpeakerThrottled(
                reactiveIdles,
                playbacks,
                speakerRoleIndex,
                dt
            );
        }

        public void ReactiveIdleEnd(List<int> reactiveIdles)
        {
            if (reactiveIdles == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int roleIndex = reactiveIdles[i];
                _reactiveIdleController.SetRoleToRecordPlayback(roleIndex, SeatedMode);
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
                        
        //Diese Funktion stellt die Execution-Order sicher.
        public void DriveAndRecordTickActiveRole(int roleIndex, int sceneCount, float dt)
        {
            DriveActiveRoleFromInput(roleIndex, dt);
            ApplyProceduralLowerBodyIfNeeded(roleIndex, dt);

            ApplyFollower(roleIndex, false);
            RecordingTick(roleIndex, sceneCount);
        }


        public void ApplyFollower(int roleIndex, bool copyRootPosRot)
        {
            bool avatarIsSeated = SeatedMode || roles[roleIndex].sittingIdle;

            if (roles[roleIndex].visualRigFollower != null)
            {
                roles[roleIndex].visualRigFollower.ApplyFollow(avatarIsSeated, copyRootPosRot);
            }
            else
            {
                UnityEngine.Debug.LogError($"No VisualRigFollower assigned in RoleRig for Rig: '{roles[roleIndex].avatarName}' with ID: '{roles[roleIndex].roleId}'");
            }
            

            if (roles[roleIndex].rigFollower != null)
            {
                roles[roleIndex].rigFollower.ApplyFollow(avatarIsSeated, copyRootPosRot);
            }                
            else
            {
                UnityEngine.Debug.LogError($"No AvatarRigFollower assigned in RoleRig for Rig: '{roles[roleIndex].avatarName}' with ID: '{roles[roleIndex].roleId}'");
            }
            
        }

        public void ApplyFollowerCalibrationState(int roleIndex)
        {
            if (roles[roleIndex].visualRigFollower != null)
            {
                //hier (false) weil im CalibrationState soll alles folgen, egal ob, SeatedMode oder sittingIdle, man steht ja...
                roles[roleIndex].visualRigFollower.ApplyFollow(false);
            }
            else
            {
                UnityEngine.Debug.LogError($"No VisualRigFollower assigned in RoleRig for Rig: '{roles[roleIndex].avatarName}' with ID: '{roles[roleIndex].roleId}'");
            }
            

            if (roles[roleIndex].rigFollower != null)
            {
                //hier (false) weil im CalibrationState soll alles folgen, egal ob, SeatedMode oder sittingIdle, man steht ja...
                roles[roleIndex].rigFollower.ApplyFollow(false);
            }                
            else
            {
                UnityEngine.Debug.LogError($"No AvatarRigFollower assigned in RoleRig for Rig: '{roles[roleIndex].avatarName}' with ID: '{roles[roleIndex].roleId}'");
            }

        }


        public void ApplyProceduralLowerBodyIfNeeded(int roleIndex, float dt)
        {
            if (FullBodyTrackers) return;
            if (!ProceduralHipAndFeetMove) return;

            var rig = roles[roleIndex];

            rig.hipSolver?.ApplySolver(dt);
            rig.leftFootSolver?.ApplySolver(dt);
            rig.rightFootSolver?.ApplySolver(dt);
        }

        public void DriveActiveRoleFromInput(int roleIndex, float dt)
        {
            bool seated = SeatedMode || roles[roleIndex].sittingIdle;

            if (seated)
            {
                DriveActiveRoleFromInputSeatedMode(roleIndex);
                return;
            }

            DriveActiveRoleFromInputStandingMode(roleIndex, dt);
        }

        public void DriveActiveRoleFromInputStandingMode(int roleIndex, float dt)
        {
            if (!_input.TryGetHeadPose(out var headPos, out var headRot))
                return;

            var rig = roles[roleIndex];

            if (_input is KeyboardInputTransforms)
            {
                Vector3 p = rig.root.position;
                p.x = headPos.x;
                p.z = headPos.z;

                float keyboardYaw = headRot.eulerAngles.y;
                rig.root.SetPositionAndRotation(p, Quaternion.Euler(0f, keyboardYaw, 0f));
                return;
            }

            var input = _input;
            if (input == null) return;

            float embodimentDeltaY = 0f;
            if (embodimentOffsetRoot != null)
                embodimentDeltaY = embodimentOffsetRoot.localPosition.y - _baseCameraOffsetY;

            if (!input.TryGetHeadPose(out var headStage, out var headRotStage)) return;
            if (!input.TryGetLeftHandPose(out var leftStage, out var leftRotStage)) return;
            if (!input.TryGetRightHandPose(out var rightStage, out var rightRotStage)) return;

            headStage.y -= embodimentDeltaY;
            leftStage.y -= embodimentDeltaY;
            rightStage.y -= embodimentDeltaY;

            Vector3 bodyPos;
            float yaw;

            bool inputForHip = input.TryGetHipPose(out var hipStageForBody, out var hipRotStageForBody);
            
            bool hasHipForBody =
                FullBodyTrackers &&
                inputForHip;

            //if has Hip, the body position should be derived from the hip
            if (hasHipForBody )
            {
                
                hipStageForBody.y -= embodimentDeltaY;

                bodyPos = hipStageForBody;
                bodyPos.y = 0f;

                yaw = hipRotStageForBody.eulerAngles.y;
            }
            //if does not have Hip, the body position should be derived from the head
            else
            {
                bodyPos = headStage;
                bodyPos.y = 0f;

                yaw = headRotStage.eulerAngles.y;
            }

            Transform actor = rig.root;
            actor.localPosition = bodyPos;
            actor.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Quaternion invActorRot = Quaternion.Inverse(actor.localRotation);

            Vector3 ToLocalPos(Vector3 pStage)
            {
                Vector3 delta = pStage - bodyPos;
                return invActorRot * delta;
            }

            Quaternion ToLocalRot(Quaternion rStage)
            {
                return invActorRot * rStage;
            }

            if (rig.head)
            {
                rig.head.localPosition = ToLocalPos(headStage);
                rig.head.localRotation = ToLocalRot(headRotStage);
            }

            if (rig.leftHand)
            {
                rig.leftHand.localPosition = ToLocalPos(leftStage);
                rig.leftHand.localRotation = ToLocalRot(leftRotStage);
            }

            if (rig.rightHand)
            {
                rig.rightHand.localPosition = ToLocalPos(rightStage);
                rig.rightHand.localRotation = ToLocalRot(rightRotStage);
            }

            if (FullBodyTrackers)
            {
                if (input.TryGetHipPose(out var hipStage, out var hipRotStage))
                {
                    hipStage.y -= embodimentDeltaY;

                    if (hasHipForBody && rig.hip)
                    {
                        rig.hip.localPosition = ToLocalPos(hipStage);
                        rig.hip.localRotation = ToLocalRot(hipRotStage);
                    }
                }

                if (input.TryGetLeftFootPose(out var leftFootStage, out var leftFootRotStage))
                {
                    leftFootStage.y -= embodimentDeltaY;

                    if (rig.leftFoot)
                    {
                        rig.leftFoot.localPosition = ToLocalPos(leftFootStage);
                        rig.leftFoot.localRotation = ToLocalRot(leftFootRotStage);
                    }
                }

                if (input.TryGetRightFootPose(out var rightFootStage, out var rightFootRotStage))
                {
                    rightFootStage.y -= embodimentDeltaY;

                    if (rig.rightFoot)
                    {
                        rig.rightFoot.localPosition = ToLocalPos(rightFootStage);
                        rig.rightFoot.localRotation = ToLocalRot(rightFootRotStage);
                    }
                }
            }
            else
            {
                // Fallback: lower body bleibt unter dem Kopf / Root.
                if (rig.hip)
                {
                    rig.hip.localPosition = new Vector3(0f, fallbackHipHeight, 0f);
                    rig.hip.localRotation = Quaternion.identity;
                }

                if (rig.leftFoot)
                {
                    rig.leftFoot.localPosition = new Vector3(- fallbackFootSpacing, 0f, 0f);
                    rig.leftFoot.localRotation = Quaternion.identity;
                }

                if (rig.rightFoot)
                {
                    rig.rightFoot.localPosition = new Vector3(fallbackFootSpacing, 0f, 0f);
                    rig.rightFoot.localRotation = Quaternion.identity;
                }
            };
        }


        public void DriveActiveRoleFromInputSeatedMode(int roleIndex)
        {
            var rig = roles[roleIndex];

            var input = _input;
            if (input == null)
                return;

            if (rig.root == null)
                return;

            
            

            if (!input.TryGetHeadPose(out var headStage, out var headRotStage)) return;
            if (!input.TryGetLeftHandPose(out var leftStage, out var leftRotStage)) return;
            if (!input.TryGetRightHandPose(out var rightStage, out var rightRotStage)) return;
            
            // testweise deaktivieren, um zu schauen, ob der Offset dann verschwindet
            /*
            float embodimentDeltaY = 0f;
            
            if (embodimentOffsetRoot != null)
                embodimentDeltaY = embodimentOffsetRoot.localPosition.y - _baseCameraOffsetY;
            headStage.y -= embodimentDeltaY;
            leftStage.y -= embodimentDeltaY;
            rightStage.y -= embodimentDeltaY;
            */
            //Feinkorrektur des höhenunterschiedes bei skalierten Rollen
            float seatedDeltaM = (rig.heightOfSeatedRoleCm - heightOfSeatedPlayerCm) / 100f;

            float headFactor =    0.6f;
            float handFactor =    0.3f;

            /*
            if(heightOfSeatedPlayerCm<rig.heightOfSeatedRoleCm)
            {
                headFactor =  - headFactor;
                handFactor = -  handFactor;

            }
            */
            if(heightOfSeatedPlayerCm>rig.heightOfSeatedRoleCm)
            {
                headFactor =   headFactor*(headFactor * 5);
                handFactor =   handFactor*(headFactor * 3);
                

            }

            
            


            headStage.y -= seatedDeltaM * headFactor;
            leftStage.y -= seatedDeltaM * handFactor;
            rightStage.y -= seatedDeltaM * handFactor;
            


            // Root bleibt, wo er ist.
            Vector3 bodyPos = rig.root.localPosition;
            Quaternion bodyRot = rig.root.localRotation;

            Quaternion invBodyRot = Quaternion.Inverse(bodyRot);

            Vector3 ToLocalPos(Vector3 pStage)
            {
                Vector3 delta = pStage - bodyPos;
                return invBodyRot * delta;
            }

            Quaternion ToLocalRot(Quaternion rStage)
            {
                return invBodyRot * rStage;
            }

            if (rig.head)
            {
                rig.head.localPosition = ToLocalPos(headStage);
                rig.head.localRotation = ToLocalRot(headRotStage);
            }

            if (rig.leftHand)
            {
                rig.leftHand.localPosition = ToLocalPos(leftStage);
                rig.leftHand.localRotation = ToLocalRot(leftRotStage);
            }

            if (rig.rightHand)
            {
                rig.rightHand.localPosition = ToLocalPos(rightStage);
                rig.rightHand.localRotation = ToLocalRot(rightRotStage);
            }

            //hip and feet werden nicht bearbeitet im SeatedMode
        }


        
        //das wird aus den States RecordListener/RecordSpeaker beim Rollenwechsel gerufen.
        public void ValidateFootSolver(int roleIndex)
        {
            if (ProceduralHipAndFeetMove)
            {
                var rig = roles[roleIndex];
                rig.leftFootSolver?.ValidateSetup(rig.roleId);
                rig.rightFootSolver?.ValidateSetup(rig.roleId);
            }
        }

        public void StartPlayerAlignToActor(int roleIndex, float duration)
        {
            if (SeatedMode || roles[roleIndex].sittingIdle)
            {
                StartPlayerAlignToActorSeated(roleIndex, duration);
                return;
            }
            else
            {
                StartPlayerAlignToActorStanding(roleIndex, duration);
            }
            
        }
        
        //das wird bei RoleSwitch von den AlignStates im Enter() gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        //Diese Funktion berechnet nur Start- und ZielPosition, um wieviel muss das XrRig bewegt werden, damit XR-Head am richtigen Ort landet.
        public void StartPlayerAlignToActorStanding(int roleIndex, float duration)
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
                if(roles[roleIndex].sittingIdle || SeatedMode)
                {
                    roleScale = (float)roles[roleIndex].heightOfSeatedRoleCm / heightOfSeatedPlayerCm; 
                }
                
                    
       
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

            // hier wird jetzt gesetzt, dass TickPlayerAlignStanding verwendet wird.
            _playerAlignUseHeadOffset = true;

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

        public void StartPlayerAlignToActorSeated(int roleIndex, float duration)
        {
            if (XrOrigin == null || _stageRoot == null)
            {
                Debug.LogError("XrOrigin or _stageRoot == null");
                return;
            }

            if (roleIndex < 0 || roleIndex >= roles.Count)
            {
                Debug.LogError($"Invalid roleIndex: {roleIndex}");
                return;
            }

            if (!_recordingController.TryGetLastEndPose(
                    roleIndex,
                    out Vector3 targetBodyPosLocal,
                    out float targetBodyYaw))
            {
                Debug.LogWarning($"No last Body end pose found for roleIndex {roleIndex}");
                return;
            }

            Vector3 targetRootWorld = _stageRoot.TransformPoint(targetBodyPosLocal);
            Quaternion targetRootRotWorld =
                _stageRoot.rotation * Quaternion.Euler(0f, targetBodyYaw, 0f);

            _playerAlignFromPos = XrOrigin.position;
            _playerAlignFromYaw = YawOf(XrOrigin.rotation);

            _playerAlignToYaw = YawOf(targetRootRotWorld);

            _playerAlignToPos = targetRootWorld;
            _playerAlignToPos.y = _playerAlignFromPos.y;

            _playerAlignDur = Mathf.Max(0.05f, duration);
            _playerAlignT = 0f;
            _playerAlignActive = true;

            _playerAlignTargetOriginPosWorld = targetRootWorld;

            // hier wird jetzt gesetzt, dass TickPlayerAlignSeated verwendet wird.
            _playerAlignUseHeadOffset = false;

            Debug.Log(
                $"[SeatedAlign] role={roles[roleIndex].roleId}, " +
                $"targetRootWorld={targetRootWorld}, targetYaw={_playerAlignToYaw}"
            );
        }
                                
        


        public void TickPlayerAlign()
        {
            if (_playerAlignUseHeadOffset)
            {
                TickPlayerAlignStanding();
            }
            else
            {  
                TickPlayerAlignSeated();
            }

        }
        //das wird bei RoleSwitch von den AlignStates im Tick() gerufen, damit man das playback des letzten Takes aus dem Blickpunkt der anderen Figur sieht.
        //Diese Funktion verschiebt das XrRig, damit XR-Head am richtigen Ort landet.
        public void TickPlayerAlignStanding()
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

        public void TickPlayerAlignSeated()
        {
            if (!_playerAlignActive || XrOrigin == null)
                return;

            _playerAlignT += Time.deltaTime;
            float u = Mathf.Clamp01(_playerAlignT / _playerAlignDur);

            float currentOriginYaw = YawOf(XrOrigin.rotation);
            float yaw = Mathf.LerpAngle(currentOriginYaw, _playerAlignToYaw, u);
            Quaternion targetOriginRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 desiredOriginPos;


            desiredOriginPos = _playerAlignTargetOriginPosWorld;


            Vector3 currentOriginPos = XrOrigin.position;

            Vector3 pos = currentOriginPos;
            pos.x = Mathf.Lerp(currentOriginPos.x, desiredOriginPos.x, u);
            pos.z = Mathf.Lerp(currentOriginPos.z, desiredOriginPos.z, u);
            pos.y = currentOriginPos.y;

            XrOrigin.position = pos;
            XrOrigin.rotation = targetOriginRotation;

            if (u >= 1f)
                _playerAlignActive = false;
        }

        public bool PlayerAlignFinished(){
            //UnityEngine.Debug.Log("TickPlayerAlign: finished");
            return !_playerAlignActive;
            
        }


        //Das wird im CalibrationState(SeatedMode) gerufen, damit die Rollen am richtigen Ort platziert werden können.
        public StagePose GetPlayerPosRotForSeatedModeRigCalibration()
        {
            if (XrHead == null)
            {
                UnityEngine.Debug.LogError("[GetPlayerPosRotForSeatedModeRigCalibration] XrHead not found!");
                return default;
            }

            if (_stageRoot == null)
            {
                UnityEngine.Debug.LogError("[GetPlayerPosRotForSeatedModeRigCalibration] _stageRoot not found!");
                return default;
            }

            Vector3 worldPos = XrHead.position;
            worldPos.y = 0f;

            Quaternion worldRot = Quaternion.Euler(
                0f,
                YawOf(XrHead.rotation),
                0f
            );

            Vector3 stageLocalPos = _stageRoot.InverseTransformPoint(worldPos);
            Quaternion stageLocalRot = Quaternion.Inverse(_stageRoot.rotation) * worldRot;

            return new StagePose
            {
                Position = stageLocalPos,
                Rotation = stageLocalRot
            };
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
                    role.heightOfSeatedRoleCm = (int)heightOfSeatedPlayerCm;
                }
                if (role != null && role.heightOfRoleCm > 0 && role.heightOfSeatedRoleCm <= 0)
                {
                    
                    role.heightOfSeatedRoleCm = (int)(role.heightOfRoleCm /1.35);
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



        // höhenanpassung der Kamera, damit man als kleine Rolle aus der Perspektive des kopfes der kleinere Figur schaut.
        // Funktion wird im Enter() des RecordSpeaker- und RecordListenersState gerufen.
        public void ApplyActiveRoleEmbodimentHeight(int roleIndex, bool forceStandingHeight = false)
        {
            if (embodimentOffsetRoot == null)
                return;

            if (roleIndex < 0 || roleIndex >= roles.Count)
                return;

            RoleRig role = roles[roleIndex];

            bool useSeatedHeight =
                !forceStandingHeight &&
                (SeatedMode || role.sittingIdle);

            float playerEyeHeightCm;
            float roleEyeHeightCm;

            if (useSeatedHeight)
            {   
                
                playerEyeHeightCm = heightOfSeatedPlayerCm * 0.95f;

                //testweise deaktivieren
                //roleEyeHeightCm = heightOfSeatedPlayerCm * 0.95f;
                roleEyeHeightCm = role.heightOfSeatedRoleCm * 0.95f;
                UnityEngine.Debug.Log($"[ApplyActiveRoleEmbodimentHeight] in SeatedMode: Height of Seated Role: {roleEyeHeightCm}, Height of Seated Player: {playerEyeHeightCm}");
            }
            else
            {
                playerEyeHeightCm = heightOfPlayerCm * 0.92f;
                roleEyeHeightCm = role.heightOfRoleCm * 0.92f;
            }

            float deltaM = (roleEyeHeightCm - playerEyeHeightCm) / 100f;

            Vector3 p = embodimentOffsetRoot.localPosition;
            p.y = _baseCameraOffsetY + deltaM;
            embodimentOffsetRoot.localPosition = p;

            Debug.Log(
                $"ApplyActiveRoleEmbodimentHeight: role={role.roleId}, " +
                $"seated={useSeatedHeight}, " +
                $"playerEyeHeightCm={playerEyeHeightCm}, " +
                $"roleEyeHeightCm={roleEyeHeightCm}, " +
                $"deltaM={deltaM}, newY={p.y}"
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