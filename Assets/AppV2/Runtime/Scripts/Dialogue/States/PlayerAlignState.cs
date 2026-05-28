using System.Diagnostics;
using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class PlayerAlignState : IState
    {
        private readonly FlowController _flow;
        private int _roleToAlignTo;
        private List<int> _reactiveIdles;
        private List<int> _playbacks;
        private float _smoothAlignSeconds;

        public DialogueMode Mode => DialogueMode.PlayerAlignState;

        public PlayerAlignState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            _reactiveIdles = _flow._data.ReactiveIdles;
            _playbacks = _flow._data.Playbacks;
            
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
                //UnityEngine.Debug.Log($"[PlayerAlignState] Enter || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} || ReactiveIdles.Count: {_flow._data.ReactiveIdles.Count} || GoToSpeakerState: {_flow._data.GoToSpeakerState}");
                if(_flow._data.SceneCount == -1){
                    _flow._data.GoToSpeakerState = true;
                    //UnityEngine.Debug.Log($"[PlayerAlignState] Enter || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} || ReactiveIdles.Count: {_flow._data.ReactiveIdles.Count} || GoToSpeakerState: {_flow._data.GoToSpeakerState}");
                }

                        
                //private void PrintRoleLists(string text, List<int> playbacks, List<int> reactiveIdles, int toBeRecorded)

                if(_flow._data.GoToSpeakerState){
                    UnityEngine.Debug.Log($"[PlayerAlignState] GoTo RecordSpeakerState || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} || ReactiveIdles.Count: {_flow._data.ReactiveIdles.Count} || GoToSpeakerState: {_flow._data.GoToSpeakerState}");
                    
                    PrintRoleLists("[PlayerAlignState] GoTo RecordSpeakerState", _playbacks, _reactiveIdles, _roleToAlignTo );
                    _flow.SetState(new RecordSpeakerState(_flow)); 
                }else{
                    UnityEngine.Debug.Log($"[PlayerAlignState] GoTo RecordListenersState || Scene is: {_flow._data.SceneCount} || Role to Align to has index: {_roleToAlignTo} || ReactiveIdles.Count: {_flow._data.ReactiveIdles.Count} || GoToSpeakerState: {_flow._data.GoToSpeakerState}");
                    PrintRoleLists("[PlayerAlignState] GoTo RecordListenersState", _playbacks, _reactiveIdles, _roleToAlignTo );
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

        private void PrintRoleLists(
            string text, 
            List<int> playbacks,
            List<int> reactiveIdles,
            int toBeRecorded
            )
        {
            string playbacksString =
                playbacks == null || playbacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", playbacks) + "]";

            string reactiveIdlesString =
                reactiveIdles == null || reactiveIdles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", reactiveIdles) + "]";

            UnityEngine.Debug.Log(
                $"[{text}] " +
                $"[RoleLists] " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"toBeRecorded={toBeRecorded} " 
            );
        }
    }
}