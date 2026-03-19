using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class RecordListenersState : IState
    {
        private readonly FlowController _flow;

        private int toBeRecorded;
        private int sceneCount;
        private List<int> reactiveIdles;
        private List<int> playbacks;

        //das kommt von der ConversationStage Inspector und bedeutet "kann man den nächsten 
        // nächsten Sprecher / Zuhörer auswählen oder nicht.
        private bool selectableNext;

        private bool _allplaybaksStoped = false;
        private bool _waitingForRecordingSave = false;
        private bool _startWaitingToSwitchToFullPlayback = false;

        public DialogueMode Mode => DialogueMode.RecordListeners;

        public RecordListenersState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {

            if (_flow == null)
            {
                UnityEngine.Debug.LogError("RecordListenersState: _flow is null");
                return;
            }

            if (_flow.Stage == null)
            {
                UnityEngine.Debug.LogError("RecordListenersState: _flow.Stage is null");
                return;
            }

            if (_flow.StatusUI != null)
            {
                _flow.StatusUI.ShowListenerState();
                _flow.StatusUI.ShowTransitionToListener();
            }
            
            selectableNext = _flow.Stage.selectableNext;
            sceneCount = _flow._data.SceneCount;
            UnityEngine.Debug.Log("[RecordListenersState] Enter");
            toBeRecorded = _flow.RecLiStateSetActiveListener();
            reactiveIdles = _flow._data.ReactiveIdles;
            playbacks = _flow._data.Playbacks;

            _flow.Stage.PlaybackStart(playbacks, sceneCount);
            //_flow.Stage.StartReactiveIdle(reactiveIdles);
            _flow.Stage.RecordingBegin(toBeRecorded,sceneCount);



        }

        public void Tick(float dt)
        {

            // Solange noch nicht beendet wird: normales Verhalten
            if (!_waitingForRecordingSave)
            {
                _flow.Stage.DriveActiveRoleFromInput(toBeRecorded);
                _flow.Stage.RecordingTick(toBeRecorded, sceneCount);
                _flow.Stage.PlaybackTick(playbacks);
            }

            if (!_waitingForRecordingSave && _flow.Stage.PlaybacksAreAllStopped() )
                {
                    UnityEngine.Debug.Log("[RecordListenersState] All playbacks are stopped, stopping recording now.");

                    _flow.Stage.RecordingEnd(toBeRecorded, sceneCount);
                    
                    _allplaybaksStoped = true;
                   
                    _waitingForRecordingSave = true;
                }
           if (_waitingForRecordingSave)
            {
                _flow.Stage.RecordingTick(toBeRecorded, sceneCount);
                // Wir warten nur noch auf das fertige Speichern
                
                if (_flow.Stage.RecordingSaveCompleted())
                {
                    UnityEngine.Debug.Log("[RecordListenersState] Recording was fully saved, switching to next state.");

                    // wenn _startWaithing... wird im ConsumeSecondaryAction auf true gesetzt.
                    if(!_startWaitingToSwitchToFullPlayback){

                        if (selectableNext)
                        {
                            _flow.SetState(new ChooseSpeakerState(_flow));
                        }
                        else
                        {
                            _flow.SetState(new RecordSpeakerState(_flow));
                        }
                    // wenn _startWaitingToSwitchToFullPlayback == true -> dann geht es nach dem Save zu PlaybackFullConversationState.
                    } else{
                        _flow.SetState(new PlaybackFullConversationState(_flow));

                    }
                    return;
                }
            }


            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[RecordListenersState] Consumed PrimaryAction");
   
            }

            if (_flow.ConsumeSecondaryAction())
            {
                _flow.Stage.RecordingEnd(toBeRecorded, sceneCount);
                UnityEngine.Debug.Log("[RecordListenersState] Consumed SecondaryAction");
                _startWaitingToSwitchToFullPlayback = true;
                
            }

            if (_flow.ConsumeResetAction())
            {
                
                

                if (!_waitingForRecordingSave && _allplaybaksStoped)
                {
                    UnityEngine.Debug.Log("[RecordListenersState] Consumed ResetAction -> FinalizeConversationState ");
                    
                } else {

                    UnityEngine.Debug.Log("[RecordListenersState] Consumed ResetAction -> has no effect when waiting for RecordingSave or playbacks still running");

                }
              
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[RecordListenersState] Exit");
        }

    }
}