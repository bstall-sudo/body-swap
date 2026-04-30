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

        private bool _isUsingXr;

        public DialogueMode Mode => DialogueMode.RecordSpeaker;

        public RecordSpeakerState(FlowController flow)
        {
            _flow = flow;
            
        }

        public void Enter()

        {
            _isUsingXr = _flow.Stage.UseXR;
        
            
            // im ersten Durchgang müssen die Variablen des FlowStateData-Objektes im Enter State gerufen werden, 
            // in den späteren Scenen werden die Variablen dann im Exit neu gesetzt, damit sie für die Align szenen schon stimmen.
            // allerdings gibt es jetzt ein Problem mit den Selectables.
            if(_flow._data.SceneCount == -1){

                _flow.IncrementSceneCount();
                
                _flow.SpeakerStateEnter();

                UnityEngine.Debug.Log($"[RecordSpeakerState] Scene: {_flow._data.SceneCount}, Speaker Index: {_flow._data.ToBeRecorded}, reactive Idles: ");
                foreach (int number in _flow._data.ReactiveIdles){
                    UnityEngine.Debug.Log($"Index: {number}");
                }
            }
            sceneCount = _flow._data.SceneCount;
            toBeRecorded = _flow._data.ToBeRecorded;

            /*
            if(_isUsingXr){
                // im XR-Modus wird _flow.RecSpeakStateSetSpeaker() in Scene 0 wird im PlayerAlignToSpeakerState gesetzt.
                
                if(sceneCount == 0){
                    toBeRecorded = _flow.RecSpeakStateSetSpeaker();
                    
                }else{
                    toBeRecorded = _flow._data.ToBeRecorded;
                }
            }else{
                // im KeyboardMode gibt es keinen PlayerAlignToSpeakerState, daher muss _flow.RecSpeakStateSetSpeaker() hier gesetzt werden.
                 // diese Funktion gibt einen Default oder einen Selected Sprecher zurück, je nachdem ob selectable Next im ConversationStage true ist oder nicht
                // und setzt ReactiveIdles auf alle, ausser Sprecher und Playbacks = [] 
                toBeRecorded = _flow.RecSpeakStateSetSpeaker();
            }
            */
            
            

            selectableNext = _flow.Stage.selectableNext;
            reactiveIdles = _flow._data.ReactiveIdles;
            UnityEngine.Debug.Log($"[RecordSpeakerState] Scene: {sceneCount}, Speaker Index: {toBeRecorded}, reactive Idles: ");
            foreach (int number in reactiveIdles){
                UnityEngine.Debug.Log($"Index: {number}");
            }
            
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
            
            
            if(_isUsingXr){
                //HöhenAnpassung der XR-Kamera.
                _flow.Stage.ApplyActiveRoleEmbodimentHeight(toBeRecorded);
                //Anpassung Grösse der Welt an Rollengrösse anpassen.
                //_flow.Stage.ApplyVisualScaleToConversationStage(toBeRecorded);
            }

            _flow.Stage.RecordingBegin(toBeRecorded,sceneCount);
            _flow.Stage.ReactiveIdleStart(reactiveIdles, toBeRecorded);
            _isRecording = true;

            UnityEngine.Debug.Log($"[RecordSpeakerState] Enter || toBeRecorded Index: {toBeRecorded} || Scene: {sceneCount} || ReactiveIdleCount: {reactiveIdles.Count}");
            
        }

        public void Tick(float dt)


        {
            _flow.Stage.ReactiveIdleTick(reactiveIdles, toBeRecorded);
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
                        if(_isUsingXr){
                            _flow.SetState(new PlayerAlignState(_flow));
                        }else{
                            _flow.SetState(new RecordListenersState(_flow));
                        }
                        
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
            _flow.Stage.ReactiveIdleEnd(reactiveIdles);
            _flow.SpeakerStateExit();
            UnityEngine.Debug.Log($"[RecordSpeakerState] To Be Recorded: {_flow._data.ToBeRecorded}");
            
            UnityEngine.Debug.Log("[RecordSpeakerState] Exit");
            //damit der PlayerAlignState weiss, ob er zu RecordSpeaker oder zu RecordListenersState wechseln soll. 
            _flow._data.GoToSpeakerState = false;
            /*
            if(_isUsingXr){
                // Augenhöhe / MainCamera wieder auf neutral setzen.
                //_flow.Stage.ResetEmbodimentHeight();
                //Grösse der Welt wieder zurücksetzen
                //_flow.Stage.ResetVisualScaleOfConversationStage(toBeRecorded);
            }
            */
           
            
        }

    }
}