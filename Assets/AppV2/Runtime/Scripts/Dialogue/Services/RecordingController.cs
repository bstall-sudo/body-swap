using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Input;

using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class RecordingController 
    {
        private TakeRecorder _takeRecorder;
  
        private bool _isRecording = false;
        private bool _isSaving = false;
        private IInputTransformsProvider _input;

        private SessionStore _store;
        private SessionModel _session;
        private string sessionFolder;
        // hier wird abgespeichert, welche Role in welcher Szene schon einen Take hat. -> Wichtig für PlaybackController
        private SessionTakeIndex _takeIndex;

        public List<RoleRig> _roles;

    
        // Pending finalize state
        private bool _hasPendingFinalize;
        private int _pendingFinalizeTicksRemaining;
        private string _pendingRoleId;
        private int _pendingRoleIndex;

        private int _pendingSceneCount;
        private TakeData _pendingTake;

        private (
            AudioClip clip,
            int startSample,
            int sampleCount,
            int channels,
            int sampleRate
        ) _pendingTrimInfo;
        
        private List<bool> _hasDesiredStartList;
        private List<bool> _hasLastFrameList;
        private List<Vector3> _lastEndPosList;
        private List<float> _lastEndYawList;
        //die sind zum alignen, direkt in den Kopf
        private List<Vector3> _lastHeadEndPosList;
        private List<float> _lastHeadEndYawList;
        private List<int> _takeCounterList;
        //private List<TakeData> _lastTakeList;

        // der RecordingController merkt sich, welche Rolle schon einen Take in welcher Szene hat.
        private readonly Dictionary<string, TakeMeta> _takeMetaBySceneAndRole =
        new Dictionary<string, TakeMeta>();
        private int _currentSceneCount = -1;


        public RecordingController(List<RoleRig> roles, int roleCount, SessionStore _storeFromConversationStage, SessionTakeIndex takeIndex, SessionModel session)
        {
            _roles = roles;
            _hasDesiredStartList = new List<bool>(new bool[roleCount]);
            _hasLastFrameList    = new List<bool>(new bool[roleCount]);

            _lastEndPosList = new List<Vector3>(new Vector3[roleCount]);
            _lastEndYawList = new List<float>(new float[roleCount]);

            _lastHeadEndPosList = new List<Vector3>(new Vector3[roleCount]);
            _lastHeadEndYawList = new List<float>(new float[roleCount]);

            _takeCounterList = new List<int>(new int[roleCount]);

            //_lastTakeList = new List<TakeData>(new TakeData[roleCount]);


            _store = _storeFromConversationStage; // 
            sessionFolder = _store.GetCurrentSessionFolder();

            // in diesem Index ist verzeichnet, wo welcher Take gespeichert ist, jeweils für $"{sceneCount}:{roleIndex}" siehe SessionTakeIndex
            _takeIndex = takeIndex;

            _session = session;

            _store.SaveSessionModel(_session);
            //UnityEngine.Debug.Log("Session folder: " + sessionFolder);
        }

        //das ist wichtig, damit dei Targets vom visualRig, welche den IKChainTargets vom Avatar (im CalibrationState) angeglichen werden im SessionModel abgespeichert werden können
        // Das passiert im FinishCalibration() von CalibrationState _flow.Stage.SaveTargetTransformsAfterCalibration();
        public void SaveTargetTransformsToSessionModel(
            List<ConversationRoleMeta> roleMetas,
            bool seatedMode)
        {
            if (_session == null)
            {
                UnityEngine.Debug.LogError(
                    "[RecordingController] Cannot save role calibration. SessionModel is null."
                );
                return;
            }

            if (roleMetas == null)
                roleMetas = new List<ConversationRoleMeta>();

            // Wenn globaler SeatedMode aktiv ist,
            // alle Rollen als SittingIdle markieren.
            if (seatedMode)
            {
                for (int i = 0; i < roleMetas.Count; i++)
                {
                    roleMetas[i].SittingIdle = true;
                }
            }

            _session.Roles = roleMetas;

            _store.SaveSessionModel(_session);

            //UnityEngine.Debug.Log($"[RecordingController] Saved {_session.Roles.Count} role calibration entries to session model.");
        }

        //public void BeginRecording(Transform stageRoot, Transform roleRoot, string roleId,float roleScale, int roleIndex,  int sceneCount, IInputTransformsProvider input)
        // Transform roleRoot, string roleId
        public void BeginRecording(
            Transform stageRoot,
            float roleScale,
            int roleIndex,
            int sceneCount,
            IInputTransformsProvider input)
        {
            if (roleIndex < 0 || roleIndex >= _roles.Count)
            {
                Debug.LogError(
                    $"BeginRecording: invalid roleIndex {roleIndex}, roles.Count={_roles.Count}");
                return;
            }

            if (roleIndex >= _hasLastFrameList.Count)
            {
                Debug.LogError(
                    $"BeginRecording: roleIndex {roleIndex} exceeds _hasLastFrameList.Count={_hasLastFrameList.Count}");
                return;
            }

            Transform roleRoot = _roles[roleIndex].root;
            string roleId = _roles[roleIndex].roleId;

            if (stageRoot == null)
            {
                Debug.LogError("BeginRecording: stageRoot is null.");
                return;
            }

            if (roleRoot == null)
            {
                Debug.LogError($"BeginRecording: roleRoot is null for role {roleId}.");
                return;
            }

            if (input == null)
            {
                Debug.LogError("BeginRecording: input is null.");
                return;
            }

            _currentSceneCount = sceneCount;

            Debug.Log(
                $"[xx] [RecordingController.BeginRecording] CREATE recorder " +
                $"role={roleIndex}, scene={sceneCount}");

            _takeRecorder =
                new TakeRecorder(stageRoot, _roles[roleIndex], roleIndex);

            _takeRecorder.SetRoleScale(roleScale);

            ApplyDesiredStartPoseForRole(roleIndex);

            _takeRecorder.Begin();

            _isRecording = true;

            Debug.Log(
                $"[xx] [RecordingController.BeginRecording] DONE " +
                $"role={roleIndex}, scene={sceneCount}, " +
                $"recorderNull={_takeRecorder == null}");
        }

        //hier wird entweder auf die Pos/Rot Daten aus _lastEndPosList/_lastEndYawList zurückgegriffen, oder InitialStartPose verwendet. 
        private void ApplyDesiredStartPoseForRole(int roleIndex)
        {
            _takeRecorder.ClearDesiredStartPose();

            Debug.Log(
                $"[DESIRED START] [NpcIntegration Debug] role={roleIndex} | " +
                $"rootNow={_roles[roleIndex].root.localPosition} | " +
                $"hasLast={_hasLastFrameList[roleIndex]} | " +
                $"hasInitial={_roles[roleIndex].hasInitialStartPose} | " +
                $"initial={_roles[roleIndex].initialStartPos}"
            );

            if (_hasLastFrameList[roleIndex])
            {
                Debug.Log(
                    $"[DESIRED START] [NpcIntegration Debug] role={roleIndex} USING LAST END " +
                    $"{_lastEndPosList[roleIndex]}"
                );

                _takeRecorder.SetDesiredStartPose(
                    _lastEndPosList[roleIndex],
                    _lastEndYawList[roleIndex]
                );

                return;
            }

            if (_roles[roleIndex].hasInitialStartPose)
            {
                Debug.Log(
                    $"[DESIRED START] [NpcIntegration Debug] role={roleIndex} USING INITIAL " +
                    $"{_roles[roleIndex].initialStartPos}"
                );

                _takeRecorder.SetDesiredStartPose(
                    _roles[roleIndex].initialStartPos,
                    _roles[roleIndex].initialStartYawDeg
                );

                Debug.Log(
                    $"[DESIRED START] [NpcIntegration Debug] role={roleIndex} NO REBASE"
                );

                return;
            }
        }

        
        public void TickRecording( int roleIndex, string roleId, int sceneCount) 
        {
            if (_isRecording && _takeRecorder != null){
                
                _takeRecorder.Tick();
            }

            // 2) Wenn Trim/Persist noch aussteht: runterzählen
            if (_hasPendingFinalize)
            {
                _pendingFinalizeTicksRemaining--;

                if (_pendingFinalizeTicksRemaining <= 0)
                {
                    FinalizePendingTrim(roleIndex, roleId,  sceneCount);
                }
            }
                
            
        }

        public void EndRecording(int roleIndex, string roleId, int sceneCount)
        {
            if (_takeRecorder == null)
            {
                Debug.LogError($"EndRecording: no recorder for scene={sceneCount}");
                return;
            }

            if (!_isRecording)
            {
                Debug.LogError("EndRecording: _isRecording is false");
                return;
            }

            if (roleIndex < 0 || roleIndex >= _hasLastFrameList.Count)
            {
                Debug.LogError($"EndRecording: invalid roleIndex {roleIndex}");
                return;
            }

            _isRecording = false;

            var recorder = _takeRecorder;

            var info = recorder.EndAndGetTrimInfo();
            var take = recorder.Current;

            // Wichtig: globale Referenz sofort freigeben.
            _takeRecorder = null;

            if (take == null)
            {
                Debug.LogError("EndRecording: take is null.");
                return;
            }

            if (take.Frames != null && take.Frames.Count > 0)
            {
                var lastFrame = take.Frames[^1];

                _lastEndPosList[roleIndex] = lastFrame.Body.Pos;
                _lastEndYawList[roleIndex] = lastFrame.Body.YawDeg;
                _lastHeadEndPosList[roleIndex] = lastFrame.Head.Pos;
                _lastHeadEndYawList[roleIndex] = lastFrame.Head.Rot.eulerAngles.y;

                _hasLastFrameList[roleIndex] = true;
            }

            if (info.HasValue)
            {
                _pendingTrimInfo = info.Value;
                _pendingRoleId = roleId;
                _pendingRoleIndex = roleIndex;
                _pendingSceneCount = sceneCount;
                _pendingTake = take;

                _pendingFinalizeTicksRemaining = 2;
                _hasPendingFinalize = true;
            }
            else
            {
                PersistTake(roleIndex, roleId, sceneCount, take);
            }
        }                   

        private void FinalizePendingTrim(int roleIndex, string roleId,  int sceneCount)
        {
            if (!_hasPendingFinalize)
                return;

            _hasPendingFinalize = false;

            if (_pendingTake == null)
            {
                UnityEngine.Debug.LogError("FinalizePendingTrim: _pendingTake is null.");
                return;
            }

            var trimmed = TakeRecorder.TrimMicClip(
                _pendingTrimInfo.clip,
                _pendingTrimInfo.startSample,
                _pendingTrimInfo.sampleCount,
                _pendingTrimInfo.channels,
                _pendingTrimInfo.sampleRate
            );

            _pendingTake.AudioClip = trimmed;
       
            //_lastTakeList[_pendingRoleIndex] = _pendingTake;

            PersistTake(_pendingRoleIndex, _pendingRoleId,  _pendingSceneCount, _pendingTake);

            //UnityEngine.Debug.Log($"FinalizePendingTrim: persisted roleId={_pendingRoleId}");
        }


        private void PersistTake(int roleIndex, string roleId, int sceneCount, TakeData take)
        {
            if (take == null)
            {
                UnityEngine.Debug.LogError("PersistTake: take is null.");
                return;
            }

            _isSaving = true;
            //UnityEngine.Debug.Log($"PersistTake called in RecordingController roleIndex{roleIndex}, roleId: {roleId} sceneCount{sceneCount}");
            try
            {
                string takeId = $"take_{sceneCount:0000}_{roleId}";

                string folder = _store.GetSessionFolder(_session.SessionId);
                string framesName = _store.FramesFileName(takeId);
                string audioName = _store.AudioFileName(takeId);

                string framesPath = Path.Combine(folder, framesName);
                string audioPath = Path.Combine(folder, audioName);

                // 1) Frames
                JsonlFrames.WriteAll(framesPath, take.Frames);

                // 2) Audio
                if (take.AudioClip != null)
                    WavUtility.SaveWav(audioPath, take.AudioClip);

                // 3) Meta
                var meta = new TakeMeta
                {
                    //SessionId = _session.SessionId,
                    TakeId = takeId,
                    RoleId = roleId,
                    RoleIndex = roleIndex,
                    SceneCount = sceneCount,
                    DurationSec = take.DurationSec,
                    FramesFile = framesName,
                    AudioFile = take.AudioClip != null ? audioName : null
                };

                _session.Takes.Add(meta);
                _store.SaveSessionModel(_session);

                _takeIndex.StoreTakeMeta(meta);

                //UnityEngine.Debug.Log($"Saved take {takeId} speaker={roleId} frames={framesName} audio={audioName}");
            }
            finally
            {
                _isSaving = false;
                CleanupFinishedRecording();
            }
        }

    public bool SaveCompleted()
    {
        return !_isRecording && !_hasPendingFinalize && !_isSaving;
    }

    //damit sich die Recording Daten nicht im RAM anhäufen.
    private void CleanupFinishedRecording()
    {
        if (_pendingTake != null && _pendingTake.AudioClip != null)
        {
            UnityEngine.Object.Destroy(_pendingTake.AudioClip);
            _pendingTake.AudioClip = null;
        }

        _pendingTake = null;
        
    }

    // das wird im ConversationStage gebraucht, damit man an die richtige Stelle Lerpen kann.
    // die Funktion gibt also den letzten EndPos/Ywa pro roleIndex zurück.
    public bool TryGetLastEndPose(int roleIndex, out Vector3 pos, out float yaw)
    {
        pos = default;
        yaw = 0f;

        if (roleIndex < 0 || roleIndex >= _hasLastFrameList.Count)
            return false;

        // 1. Letzte aufgenommene Endposition
        if (_hasLastFrameList[roleIndex])
        {
            pos = _lastEndPosList[roleIndex];
            yaw = _lastEndYawList[roleIndex];
            return true;
        }

        RoleRig role = _roles[roleIndex];

        // 2. Definierte Initialposition
        if (role.hasInitialStartPose)
        {
            pos = role.initialStartPos;
            yaw = role.initialStartYawDeg;
            return true;
        }

        // 3. Fallback: aktuelle Position der Rolle
        if (role.root != null)
        {
            pos = role.root.localPosition;
            yaw = role.root.localEulerAngles.y;

            Debug.Log(
                $"[TryGetLastEndPose] [NpcIntegration Debug] No last/initial pose for role {roleIndex}. " +
                $"Using current root pose of role with ID: {_roles[roleIndex].roleId} and Index: {_roles[roleIndex].roleIndex} : pos={pos}, yaw={yaw:F1}"
            );

            return true;
        }

        return false;
    }

    // das wird im ConversationStage gebraucht, damit man an die richtige Stelle Lerpen kann. Insbesondere, damit man direkt in den Kopf alignen kann.
    // die Funktion gibt also den letzten EndPos/Ywa pro roleIndex zurück.
    public bool TryGetLastHeadEndPose(int roleIndex, out Vector3 pos, out float yaw)
    {
        pos = default;
        yaw = 0f;

        if (roleIndex < 0 || roleIndex >= _hasLastFrameList.Count)
            return false;

        // 1. Letzte aufgezeichnete Head-EndPose
        if (_hasLastFrameList[roleIndex])
        {
            pos = _lastHeadEndPosList[roleIndex];
            yaw = _lastHeadEndYawList[roleIndex];
            return true;
        }

        if (roleIndex < 0 || roleIndex >= _roles.Count)
        {
            UnityEngine.Debug.LogError("RoleIndex out of Range");
            return false;
        }

        RoleRig role = _roles[roleIndex];

        if (role == null)
            return false;

        // 2. InitialPlacement benutzen
        if (role.hasInitialStartPose)
        {
            float estimatedHeadHeightMeters =
                role.heightOfRoleCm * 0.01f * 0.9f;

            pos = Vector3.up * estimatedHeadHeightMeters;
            yaw = 0f;

            return true;
        }

        // 3. Fallback: aktuelle Head-Pose relativ zum Role-Root
        if (role.root != null && role.head != null)
        {
            pos = role.root.InverseTransformPoint(role.head.position);

            Quaternion headLocalRot =
                Quaternion.Inverse(role.root.rotation) * role.head.rotation;

            yaw = headLocalRot.eulerAngles.y;

            Debug.Log(
                $"[TryGetLastHeadEndPose] [NpcIntegration Debug] " +
                $"role={roleIndex} USING CURRENT HEAD | " +
                $"headBodyLocal={pos} | yaw={yaw:F1}"
            );

            return true;
        }

        return false;
    }

    //für das Debuggen von StartPlayerAlignStanding in conversationStage, weil die neu Integrierten NPC's nicht korrekt platziert werden.
    public bool HasLastFrame(int roleIndex)
    {
        return roleIndex >= 0 &&
            roleIndex < _hasLastFrameList.Count &&
            _hasLastFrameList[roleIndex];
    }

    }
}