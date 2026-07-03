using UnityEngine;
using System.Collections.Generic;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class PlaybackFullPreRecordedScenes : IState
    {
        private readonly FlowController _flow;

        private List<int> _noTakes;

        private List<int> _reactiveIdles;

        private List<int> _preRecordedRolesIndices;
        private List<int> _playbacks;

        private bool _seatedMode;
        private int _sceneCountForPreRecordedScenes;

        private int _sceneCount;
        private int _roleCount;

        private int _toBeRecorded;
        private bool _startInPlaybackFullConversationMode;
        
        
        private bool _allplaybaksStopped = false;

        public DialogueMode Mode => DialogueMode.PlaybackFullPreRecordedScenes;

        public PlaybackFullPreRecordedScenes(FlowController flow)
        {
            _flow = flow;
            _playbacks = new List<int>();
            
        }

        public void Enter()
        {
            _sceneCountForPreRecordedScenes = 0;
            _sceneCount = _flow._data.SceneCount;
            Debug.Log($"[PlaybackFullPreRecordedScenes] Enter: roleCount is: {_roleCount}, SceneCount is: {_sceneCount} SceneCount For PrerecordedScenes is: {_sceneCountForPreRecordedScenes}");
            _roleCount =  _flow._data.CurrentPreRecordedPlaybacks.Count;
            _preRecordedRolesIndices = _flow._data.CurrentPreRecordedPlaybacks;
            _seatedMode = _flow.Stage.SeatedMode;
            _toBeRecorded = _flow._data.ToBeRecorded;

            _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);

            if (_seatedMode)
            {
                _flow.Stage.ChooseSpeakerController.MoveXrOriginBackFromStage();
            }

            
            
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] SceneCount is: {_sceneCount}");
            

          

            if (_flow.StatusUI != null)
            {
                //_flow.StatusUI.ShowPlaybackFullConversationState();
                _flow.StatusUI.ShowCustomCue(
                    "AUFNAHME GESTOPPT\n \n PlaybackMode ON",
                    new Vector2(0f, 180f),
                    new Vector2(500f, 0f),
                    Color.red
                );
            }
            _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);
            Debug.Log($"[PlaybackFullPreRecordedScenes] Enter: roleCount is: {_roleCount}, SceneCountForPreRecordedScenes is: {_sceneCountForPreRecordedScenes} activeRoles: {_flow._data.IndicesOfPassiveRoles}");
            PrepareStartPlaybacksReactiveIdlesForScene();
        }

        public void Tick(float dt)
        {
            
            if (_flow.ConsumePrimaryAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] Consumed SecondaryAction");
                _flow.SetState(new IdleState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] Consumed ResetAction");
            }

            if (!_allplaybaksStopped)
            {
                _flow.Stage.DriveAndRecordTickActiveRole(_toBeRecorded, _sceneCount, dt);
                _flow.Stage.PlaybackTick(_playbacks);
                //_flow.Stage.ReactiveIdleStart(_reactiveIdles, _playbacks[0]);
                _allplaybaksStopped = _flow.Stage.PlaybacksAreAllStopped();
            }
            if(_allplaybaksStopped)
            {   _flow.Stage.RecordingEnd(_toBeRecorded,_sceneCount);
                _sceneCountForPreRecordedScenes++;
                if (_flow.Stage.PlaybackHasAnyTakeForScene(_sceneCountForPreRecordedScenes))
                {
                    UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] after update: SceneCount for Prerecorded Scenes is: {_sceneCountForPreRecordedScenes}");
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    PrepareStartPlaybacksReactiveIdlesForScene();
                    
                    _allplaybaksStopped = false;
                }
                else
                {
                    UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] No more scenes found. Restart PlaybackFullConversation.");
                    _sceneCount = 0;
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    _flow.SetState(new PlaybackFullPreRecordedScenes(_flow));
                }
            }
        }

        private void PrepareStartPlaybacksReactiveIdlesForScene()
        {
            //hier werden alle existierenden Rollen in die Liste der Playbacks aufgenommen
            _playbacks = PlaybackCandidates(_roleCount);
            UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] SceneCount is: {_playbacks.Count} playbacks: [" + string.Join(", ", _playbacks) + "]");
            _noTakes = _flow.Stage.PlaybackStart(_playbacks, _sceneCountForPreRecordedScenes);

            //alle Rollen ohne Take sind in ReactiveIdles
            _reactiveIdles = _noTakes;

//-------------------- Das muss ev. noch geändert werden
            _flow.Stage.ReactiveIdleStart(_reactiveIdles, _playbacks[0]);

            //Rollen ohne Takes werden von den Playbacks entfernt
            foreach (int idleIndex in _noTakes)
            {
                _playbacks.Remove(idleIndex);
            }
            
        }

        public void Exit()
        {
            PrepareStartPlaybacksReactiveIdlesForScene();
            UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] Exit");
        }

        private List<int> PlaybackCandidates(int roleCount){
            List<int> playbackCandidates =new List<int>();

            foreach (int i in _preRecordedRolesIndices)
                {
                    playbackCandidates.Add(i);   
                }
            return playbackCandidates;
        }

    }
}