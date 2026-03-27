using System.Diagnostics;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class PlayerAlignState : IState
    {
        private readonly FlowController _flow;
        private int _roleToAlignTo;
        private float _smoothAlignSeconds;

        public DialogueMode Mode => DialogueMode.PlayerAlignState;

        public PlayerAlignState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            
            _smoothAlignSeconds = _flow.Stage.SmoothAlignSeconds;

            
            _roleToAlignTo = _flow._data.ToBeRecorded;

            UnityEngine.Debug.Log($"[PlayerAlignState] Enter || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} ");
            

            
            _flow.Stage.StartPlayerAlignToActor(_roleToAlignTo, _smoothAlignSeconds);
        }

        public void Tick(float dt)
        {

            _flow.Stage.TickPlayerAlign();
            if (_flow.Stage.PlayerAlignFinished())
            {
                UnityEngine.Debug.Log($"[PlayerAlignState] Enter || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} || ReactiveIdles.Count: {_flow._data.ReactiveIdles.Count}");
                
                if(_flow._data.GoToSpeakerState){
                    _flow.SetState(new RecordSpeakerState(_flow)); 
                }else{
                    _flow.SetState(new RecordListenersState(_flow));
                }
                
                 
            }



            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[PlayerAlignState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[PlayerAlignState] Consumed SecondaryAction");
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[PlayerAlignState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[PlayerAlignState] Exit");
        }
    }
}