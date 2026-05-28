using UnityEngine;
using System.Collections.Generic;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class PlaybackFullConversationState : IState
    {
        private readonly FlowController _flow;

        private List<int> _noTakes;

        private List<int> _reactiveIdles;
        private List<int> _playbacks;

        private bool _seatedMode;
        private int _sceneCount;
        private int _roleCount;
        private bool _startInPlaybackFullConversationMode;
        
        
        private bool _allplaybaksStopped = false;

        public DialogueMode Mode => DialogueMode.PlaybackFullConversation;

        public PlaybackFullConversationState(FlowController flow)
        {
            _flow = flow;
            _playbacks = new List<int>();
            
        }

        public void Enter()
        {
            _roleCount =  _flow._data.RoleCount;
            _seatedMode = _flow.Stage.SeatedMode;

            if (_seatedMode)
            {
                _flow.Stage.ChooseSpeakerController.MoveXrOriginBackFromStage();
            }

            
            _sceneCount = 0;
            //UnityEngine.Debug.Log($"[PlaybackFullConversationState] SceneCount is: {_sceneCount}");
            

            //damit man im PlaybackFullConversationState auf Standarhöhe ist und nicht auf der Höhe des letzten Recordings.
            _flow.Stage.ResetEmbodimentHeight();

            if (_flow.StatusUI != null)
            {
                _flow.StatusUI.ShowPlaybackFullConversationState();
            }

            PrepareStartPlaybacksReactiveIdlesForScene();
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed SecondaryAction");
                _flow.SetState(new IdleState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed ResetAction");
            }

            if (!_allplaybaksStopped)
            {
                _flow.Stage.PlaybackTick(_playbacks);
                _flow.Stage.ReactiveIdleStart(_reactiveIdles, _playbacks[0]);
                _allplaybaksStopped = _flow.Stage.PlaybacksAreAllStopped();
            }
            if(_allplaybaksStopped)
            {   
                _sceneCount++;
                if (_flow.Stage.PlaybackHasAnyTakeForScene(_sceneCount))
                {
                    UnityEngine.Debug.Log($"[PlaybackFullConversationState] Starting scene {_sceneCount}");
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    PrepareStartPlaybacksReactiveIdlesForScene();
                    
                    _allplaybaksStopped = false;
                }
                else
                {
                    UnityEngine.Debug.Log("[PlaybackFullConversationState] No more scenes found. Restart PlaybackFullConversation.");
                    _sceneCount = 0;
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    _flow.SetState(new PlaybackFullConversationState(_flow));
                }
            }
        }

        private void PrepareStartPlaybacksReactiveIdlesForScene()
        {
            //hier werden alle existierenden Rollen in die Liste der Playbacks aufgenommen
            _playbacks = PlaybackCandidates(_roleCount);
            //UnityEngine.Debug.Log($"[PlaybackFullConversationState] SceneCount is: {_playbacks.Count}");
            _noTakes = _flow.Stage.PlaybackStart(_playbacks, _sceneCount);

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
            UnityEngine.Debug.Log("[PlaybackFullConversationState] Exit");
        }

        private List<int> PlaybackCandidates(int roleCount){
            List<int> playbackCandidates =new List<int>();

            for(int i = 0; i < roleCount; i++)
                {
                    playbackCandidates.Add(i);   
                }
            return playbackCandidates;
        }

    }
}