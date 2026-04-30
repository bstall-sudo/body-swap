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

        private bool _isUsingXr;

        public DialogueMode Mode => DialogueMode.RecordListeners;

        public RecordListenersState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            _isUsingXr = _flow.Stage.UseXR;

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



   
            toBeRecorded = _flow._data.ToBeRecorded;
            
            if(_isUsingXr){
                //HöhenAnpassung der XR-Kamera.
                _flow.Stage.ApplyActiveRoleEmbodimentHeight(toBeRecorded);
                //Anpassung Grösse der Welt an Rollengrösse anpassen.
                //_flow.Stage.ApplyVisualScaleToConversationStage(toBeRecorded);
            }
            
            reactiveIdles = _flow._data.ReactiveIdles;
            playbacks = _flow._data.Playbacks;

            _flow.Stage.PlaybackStart(playbacks, sceneCount);
            _flow.Stage.ReactiveIdleStart(reactiveIdles, toBeRecorded);
            _flow.Stage.RecordingBegin(toBeRecorded,sceneCount);

            UnityEngine.Debug.Log($"[RecordListenersState] Enter || Playback by Index: {playbacks[0]} || toBeRecorded Index: {toBeRecorded} || Scene: {sceneCount} || ReactiveIdleCount: {reactiveIdles.Count}");


        }

        public void Tick(float dt)
        {
            _flow.Stage.ReactiveIdleTick(reactiveIdles, toBeRecorded);
            // Solange noch nicht beendet wird: normales Verhalten
            if (!_waitingForRecordingSave)
            {
                _flow.Stage.DriveActiveRoleFromInput(toBeRecorded);
                _flow.Stage.RecordingTick(toBeRecorded, sceneCount);
                _flow.Stage.PlaybackTick(playbacks);
                
            }

            if (!_waitingForRecordingSave && _flow.Stage.PlaybacksAreAllStopped() )
                {
                    //UnityEngine.Debug.Log("[RecordListenersState] All playbacks are stopped, stopping recording now.");

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
                    //UnityEngine.Debug.Log("[RecordListenersState] Recording was fully saved, switching to next state.");

                    // wenn _startWaithing... wird im ConsumeSecondaryAction auf true gesetzt.
                    if(!_startWaitingToSwitchToFullPlayback){

                        if (selectableNext)
                        {
                            _flow.SetState(new ChooseSpeakerState(_flow));
                        }
                        else
                        {   // wenn XR Modus -> dann Align zu neuer Position und wenn es keine aufzunehmenden Figuren 
                            // (reactiveIdles.Count = 0) mehr
                            // gibt, dann in den Align Modus zu neuem Record Speaker State.
                            if(_isUsingXr){
                                // GoToSpeakerState wird hier schon gesetzt, weil im RecordListenerState Exit die reactiveIdles schon neu gesetzt werden
                                // basierend auf den Reactive Idles muss der PlayerAlignState den Ziel State bestimmen.
                                if(reactiveIdles.Count > 0){
                                    _flow._data.GoToSpeakerState = false;
                                }else{
                                    _flow._data.GoToSpeakerState = true;
                                }
                                _flow.SetState(new PlayerAlignState(_flow));
                            }else{
                                if(reactiveIdles.Count > 0){
                                    _flow.SetState(new RecordListenersState(_flow));
                                }else{
                                    _flow.SetState(new RecordSpeakerState(_flow));
                                }
                            }
                            
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
            _flow.Stage.ReactiveIdleEnd(reactiveIdles);
            _flow.ListenerStateExit();
            
            /*
            if(_isUsingXr){
                // Augenhöhe / MainCamera wieder auf neutral setzen.
                //_flow.Stage.ResetEmbodimentHeight();
                //Grösse der Welt wieder zurücksetzen
                //_flow.Stage.ResetVisualScaleOfConversationStage(toBeRecorded);
            }
           */
            UnityEngine.Debug.Log("[RecordListenersState] Exit");
        }

    }
}