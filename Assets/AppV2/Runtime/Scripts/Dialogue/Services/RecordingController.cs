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
      

        // Pending finalize state
        private bool _hasPendingFinalize;
        private int _pendingFinalizeTicksRemaining;
        private string _pendingRoleId;
        private int _pendingRoleIndex;
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
        private List<int> _takeCounterList;
        //private List<TakeData> _lastTakeList;

        // der RecordingController merkt sich, welche Rolle schon einen Take in welcher Szene hat.
        private readonly Dictionary<string, TakeMeta> _takeMetaBySceneAndRole =
        new Dictionary<string, TakeMeta>();
        private int _currentSceneCount = -1;


        public RecordingController(int roleCount, SessionStore _storeFromConversationStage, SessionTakeIndex takeIndex)
        {
            _hasDesiredStartList = new List<bool>(new bool[roleCount]);
            _hasLastFrameList    = new List<bool>(new bool[roleCount]);

            _lastEndPosList = new List<Vector3>(new Vector3[roleCount]);
            _lastEndYawList = new List<float>(new float[roleCount]);

            _takeCounterList = new List<int>(new int[roleCount]);

            //_lastTakeList = new List<TakeData>(new TakeData[roleCount]);


            _store = _storeFromConversationStage; // 
            sessionFolder = _store.CreateNewSessionFolder(out string sessionId);

            // in diesem Index ist verzeichnet, wo welcher Take gespeichert ist, jeweils für $"{sceneCount}:{roleIndex}" siehe SessionTakeIndex
            _takeIndex = takeIndex;

            _session = new SessionModel
            {
                SessionId = sessionId,
                CreatedUtc = DateTime.UtcNow.ToString("o")
                
            };
            _store.SaveSessionModel(_session);
            UnityEngine.Debug.Log("Session folder: " + sessionFolder);
        }


        public void BeginRecording(Transform stageRoot, Transform roleRoot, string roleId, int roleIndex, int sceneCount, IInputTransformsProvider input)
        {

            if (stageRoot == null)
            {
                UnityEngine.Debug.LogError("BeginRecording: stageRoot is null.");
                return;
            }

            if (roleRoot == null)
            {
                UnityEngine.Debug.LogError($"BeginRecording: roleRoot is null for role {roleId}.");
                return;
            }

            if (input == null)
            {
                UnityEngine.Debug.LogError("BeginRecording: input is null.");
                return;
            }

            if (roleIndex < 0 || roleIndex >= _hasLastFrameList.Count)
            {
                UnityEngine.Debug.LogError($"BeginRecording: invalid roleIndex {roleIndex}.");
                return;
            }
            
            _currentSceneCount = sceneCount;
            _takeRecorder = new TakeRecorder(stageRoot, roleRoot, roleId, roleIndex, input);

            if (_hasLastFrameList[roleIndex]) _takeRecorder.SetDesiredStartPose(_lastEndPosList[roleIndex], _lastEndYawList[roleIndex]);
            _takeRecorder.Begin();
            _isRecording = true;
            UnityEngine.Debug.Log($"BeginRecording: roleId={roleId}, roleIndex={roleIndex}");
      

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
            if (!_isRecording)
            {
                UnityEngine.Debug.LogError("EndRecording: _isReorcing is false");
                return;
            }
            if (_takeRecorder == null)
            {
                UnityEngine.Debug.LogError("EndRecording: there is no _takeRecorder.");
                return;
            }

            if (roleIndex < 0 || roleIndex >= _hasLastFrameList.Count)
            {
                UnityEngine.Debug.LogError($"EndRecording: invalid roleIndex {roleIndex}.");
                return;
            }
                

            _isRecording = false;

            var info = _takeRecorder.EndAndGetTrimInfo();

            // Frames sind sofort verfügbar:
            var take = _takeRecorder.Current;

            
            if (take == null)
            {
                UnityEngine.Debug.LogError("EndRecording: take is null.");
                return;
            }
         

            if (take != null && take.Frames.Count > 0)
            {
                var lastFrame = take.Frames[take.Frames.Count - 1];
                _lastEndPosList[roleIndex] = lastFrame.Body.Pos;
                _lastEndYawList[roleIndex]  = lastFrame.Body.YawDeg;
                _hasLastFrameList[roleIndex]  = true;
            }


            //_lastTakeList[roleIndex] = take;

            // Falls es Trim-Info gibt, um 2 Ticks verzögert finalisieren
            if (info.HasValue)
            {
                _pendingTrimInfo = info.Value;
                _pendingRoleId = roleId;
                _pendingRoleIndex = roleIndex;
                _pendingTake = take;
                _pendingFinalizeTicksRemaining = 2;
                _hasPendingFinalize = true;

                UnityEngine.Debug.Log($"EndRecording: pending finalize started for roleId={roleId}");
            }
            else
            {
                
                // Kein Trim nötig -> direkt speichern
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

            PersistTake(_pendingRoleIndex, _pendingRoleId,  sceneCount, _pendingTake);

            UnityEngine.Debug.Log($"FinalizePendingTrim: persisted roleId={_pendingRoleId}");
        }


        private void PersistTake(int roleIndex, string roleId, int sceneCount, TakeData take)
        {
            if (take == null)
            {
                UnityEngine.Debug.LogError("PersistTake: take is null.");
                return;
            }

            _isSaving = true;
            UnityEngine.Debug.Log($"PersistTake called in RecordingController roleIndex{roleIndex}, roleId: {roleId} sceneCount{sceneCount}");
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
                    SessionId = _session.SessionId,
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

                UnityEngine.Debug.Log($"Saved take {takeId} speaker={roleId} frames={framesName} audio={audioName}");
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
        _takeRecorder = null;
    }

    }
}