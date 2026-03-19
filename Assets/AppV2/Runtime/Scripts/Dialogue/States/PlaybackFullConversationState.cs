using UnityEngine;
using System.Collections.Generic;


namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class PlaybackFullConversationState : IState
    {
        private readonly FlowController _flow;

        private int _sceneCount;
        private bool _startInPlaybackFullConversationMode;
        
        private List<int> _playbacks;
        private bool _allplaybaksStopped = false;

        public DialogueMode Mode => DialogueMode.PlaybackFullConversation;

        public PlaybackFullConversationState(FlowController flow)
        {
            _flow = flow;
            _playbacks = new List<int>();
            
        }

        public void Enter()
        {
            _sceneCount = 0;
            UnityEngine.Debug.Log($"[PlaybackFullConversationState] SceneCount is: {_sceneCount}");
            
            SetPlaybacks(_flow._data.RoleCount);
            UnityEngine.Debug.Log($"[PlaybackFullConversationState] SceneCount is: {_playbacks.Count}");
            _flow.Stage.PlaybackStart(_playbacks, _sceneCount);
        }

        public void Tick(float dt)
        {
            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed SecondaryAction");
                _flow.SetState(new IdleState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[PlaybackFullConversationState] Consumed ResetAction");
            }

            if (!_allplaybaksStopped)
            {
                _flow.Stage.PlaybackTick(_playbacks);
                _allplaybaksStopped = _flow.Stage.PlaybacksAreAllStopped();
            }
            if(_allplaybaksStopped)
            {   
                _sceneCount++;
                if (_flow.Stage.PlaybackHasAnyTakeForScene(_sceneCount))
                {
                    UnityEngine.Debug.Log($"[PlaybackFullConversationState] Starting scene {_sceneCount}");
                    _flow.Stage.PlaybackStart(_playbacks, _sceneCount);
                    _allplaybaksStopped = false;
                }
                else
                {
                    UnityEngine.Debug.Log("[PlaybackFullConversationState] No more scenes found. Going to IdleState.");
                    _flow.SetState(new PlaybackFullConversationState(_flow));
                }
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[PlaybackFullConversationState] Exit");
        }

        private void SetPlaybacks(int roleCount){
            for(int i = 0; i < roleCount; i++)
                {
                    _playbacks.Add(i);   
                }
            
        }

    }
}