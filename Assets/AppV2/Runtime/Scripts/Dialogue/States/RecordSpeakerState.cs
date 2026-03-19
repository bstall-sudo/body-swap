using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class RecordSpeakerState : IState
    {
        private readonly FlowController _flow;

        private int toBeRecorded;
        private int sceneCount;

        //das kommt von der ConversationStage Inspector und bedeutet "kann man den nächsten 
        // nächsten Sprecher / Zuhörer auswählen oder nicht.
        private bool selectableNext;
        private bool _waitingForRecordingSave = false;
        private bool _isRecording;
        private List<int> reactiveIdles;

        //das wird in ConsumeSecondaryAction auf true gesetzt, damit, sobald möglich in den PlaybackFullConversationState gewechselt werden kann.
        private bool _startWaitingToSwitchToFullPlayback = false;

        public DialogueMode Mode => DialogueMode.RecordSpeaker;

        public RecordSpeakerState(FlowController flow)
        {
            _flow = flow;
            
        }

        public void Enter()
        {
            selectableNext = _flow.Stage.selectableNext;
            
            _flow.IncrementSceneCount();
            // diese Funktion gibt einen Default oder einen Default oder einen Selected Sprecher zurück 
            // und setzt ReactiveIdles auf alle ausser Sprecher und Playbacks = []  
            toBeRecorded = _flow.RecSpeakStateSetSpeaker();
            sceneCount = _flow._data.SceneCount;
            reactiveIdles = _flow._data.ReactiveIdles;
            /*UnityEngine.Debug.Log($"[RecordSpeakerState] Scene: {sceneCount}, Speaker Index: {toBeRecorded}, reactive Idles: ");
            foreach (int number in reactiveIdles){
                UnityEngine.Debug.Log($"Index: {number}");
            }
            */
             if (_flow == null)
            {
                UnityEngine.Debug.LogError("RecordSpeakerState: _flow is null");
                return;
            }

            if (_flow.Stage == null)
            {
                UnityEngine.Debug.LogError("RecordSpeakerState: _flow.Stage is null");
                return;
            }

            if (_flow.StatusUI != null)
            {
                _flow.StatusUI.ShowSpeakerState();
                _flow.StatusUI.ShowTransitionToSpeaker();
            }

            _flow.Stage.RecordingBegin(toBeRecorded,sceneCount);
            _isRecording = true;
            
        }

        public void Tick(float dt)


        {
            if(_isRecording && !_waitingForRecordingSave){
                _flow.Stage.DriveActiveRoleFromInput(toBeRecorded);
                _flow.Stage.RecordingTick(toBeRecorded,sceneCount);
            }

            if(!_isRecording && _waitingForRecordingSave ){
                _flow.Stage.RecordingTick(toBeRecorded,sceneCount);
            }
           
            if (!_isRecording && _flow.Stage.RecordingSaveCompleted())
            {
                _waitingForRecordingSave = false;
                // wenn _startWaithing... wird im ConsumeSecondaryAction auf true gesetzt.
                if(!_startWaitingToSwitchToFullPlayback){
                    if(selectableNext)
                    {
                        _flow.SetState(new ChooseSpeakerState(_flow));
                    }
                    else
                    {
                        _flow.SetState(new RecordListenersState(_flow));
                    }
                }
                // wenn _startWaitingToSwitchToFullPlayback == true -> dann geht es nach dem Save zu PlaybackFullConversationState.
                else
                {
                    _flow.SetState(new PlaybackFullConversationState(_flow));
                }


            }
            


            if (_flow.ConsumePrimaryAction())
            {
                if(_isRecording){
                    UnityEngine.Debug.Log("[RecordSpeakerState] Consumed PrimaryAction");
                    _flow.Stage.RecordingEnd(toBeRecorded,sceneCount);
                    _isRecording = false;
                    _waitingForRecordingSave = true;

                }
                
                
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[RecordSpeakerState] Consumed SecondaryAction");
                _startWaitingToSwitchToFullPlayback = true;
                
                
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[RecordSpeakerState] Consumed ResetAction");
            }
        }

        public void Exit()
        {
            UnityEngine.Debug.Log("[RecordSpeakerState] Exit");
        }

    }
}