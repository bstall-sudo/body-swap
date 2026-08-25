using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class RecordRemainingIdlesAfterPreRecordedEncounterState: IState
    {
        private readonly FlowController _flow;

        private int _toBeRecorded;
        private int _sceneCount;
        private List<int> _activeRoles;

        
        private int _sceneCountWhilePlaybackPreRecorded;

        private int _sceneCountBeforePlaybackPreRecorded;
        private List<int> _reactiveIdles;
        
        private List<int> _preRecordedRolesIndices;
        private List<int> _playbacks;

        //das kommt von der ConversationStage Inspector und bedeutet "kann man den nächsten 
        // nächsten Sprecher / Zuhörer auswählen oder nicht.
        private bool _selectableNext;

        private bool _allplaybaksStoped = false;

        private bool _goToPlaybackPreRecordedScenes = false;

        public bool _goToSpeakerState = false;
        private bool _allplaybaksStopped = false;
        private bool _goToRecordRemainingIdles = true;
        private bool _waitingForRecordingSave = false;
        private bool _startWaitingToSwitchToFullPlayback = false;

        private bool _isUsingXr;

        public DialogueMode Mode => DialogueMode.RecordListeners;

        public RecordRemainingIdlesAfterPreRecordedEncounterState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Enter Start");
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
                _flow.StatusUI.ShowCustomCue(
                    "Zuhörer verbliebene Rollen",
                    new Vector2(0f, 180f),
                    new Vector2(500f, 0f),
                    Color.red
                );
            }
            

            _sceneCountWhilePlaybackPreRecorded = _flow._data.SceneCountWhilePlaybackPreRecorded;
            _sceneCountBeforePlaybackPreRecorded = _flow._data.SceneCountBeforePlaybackPreRecorded;

            _selectableNext = _flow.Stage.selectableNext;
            _preRecordedRolesIndices = _flow._data.CurrentPreRecordedPlaybacks;
            _sceneCount = _flow._data.SceneCountWhilePlaybackPreRecorded;
            
            //CheckFlowCondition();

            _toBeRecorded = _flow._data.ToBeRecorded;
            _activeRoles = GetActiveRoleIndices(_toBeRecorded); 
            
            if(_isUsingXr){
                //HöhenAnpassung der XR-Kamera.
                _flow.Stage.ApplyActiveRoleEmbodimentHeight(_toBeRecorded);
                //Anpassung Grösse der Welt an Rollengrösse anpassen.
                //_flow.Stage.ApplyVisualScaleToConversationStage(toBeRecorded);
                _flow.Stage.ValidateFootSolver(_toBeRecorded);
            }
            
            PrepareStartPlaybacksReactiveIdlesForScene(_sceneCount, _toBeRecorded);
            _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);

            PrintRoleLists("[RecordRemainingIdlesAfterPreRecordedEncounterState] at End", _playbacks, _reactiveIdles,_toBeRecorded);
            //UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Enter End");

        }

        public void Tick(float dt)
        {
            if (!_allplaybaksStopped)
            {
                _flow.Stage.DriveAndRecordTickActiveRole(_toBeRecorded, _sceneCount, dt);
                _flow.Stage.PlaybackTick(_playbacks);
                //_flow.Stage.ReactiveIdleStart(_reactiveIdles, _playbacks[0]);
                //
                _allplaybaksStopped = _flow.Stage.PlaybacksAreAllStopped(_playbacks);
            }
            if (_allplaybaksStopped)
            {
                _flow.Stage.RecordingEnd(_toBeRecorded, _sceneCount);
                _sceneCount++;
                PrintRoleLists("[RecordRemainingIdlesAfterPreRecordedEncounterState] Update before Update", _playbacks, _reactiveIdles,_toBeRecorded);
                _playbacks = _flow.Stage.PlaybackIndicesWithTakeForScene(_activeRoles, _sceneCount);
                _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                _reactiveIdles = GetIdles(_sceneCount, _toBeRecorded);
                PrintRoleLists("[RecordRemainingIdlesAfterPreRecordedEncounterState] Update after Update", _playbacks, _reactiveIdles,_toBeRecorded);
                if (_playbacks.Count > 0)
                {
                    //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] after update: SceneCount for Prerecorded Scenes is: {_sceneCountForPreRecordedScenes}");
                    
                    
                    PrepareStartPlaybacksReactiveIdlesForScene(_sceneCount, _toBeRecorded);
                    _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);
                    _allplaybaksStopped = false;
                }
                else
                {
                    //UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] No more scenes found. Restart PlaybackFullConversation.");
                    
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    //_flow.Stage.RecordingEnd(_toBeRecorded, _sceneCount);
                    //_flow._data.TimesPreRecordedPlaybacksWerePlayed --;

                    //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] TimesPreRecordedPlaybacksWerePlayed: {_flow._data.TimesPreRecordedPlaybacksWerePlayed}.");
                    /*
                    if(_flow._data.TimesPreRecordedPlaybacksWerePlayed <= 0)
                    {
                        _flow._data.GoToSpeakerState = true;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = false;
                    } */
                    SetFutureFlowDirection();
                    //_flow.PlaybackPreRecordedToRecordRemaining_DataAdjustments();
                    PrintRoleLists(
                            "[RecordRemainingIdlesAfterPreRecordedEncounterState] -> before PlayerAlignState", 
                            _flow._data.Playbacks,
                            _flow._data.ReactiveIdles,
                            _flow._data.ToBeRecorded
                            );
                    _flow.SetState(new PlayerAlignState(_flow));
                }  
            }


            if (_flow.ConsumePrimaryAction())
            {
                UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Consumed PrimaryAction");
   
            }

            if (_flow.ConsumeSecondaryAction())
            {
                //_flow.Stage.RecordingEnd(_toBeRecorded, _sceneCount);
                UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Consumed SecondaryAction");
                _startWaitingToSwitchToFullPlayback = true;
                
            }

            if (_flow.ConsumeResetAction())
            {
                
                

                if (!_waitingForRecordingSave && _allplaybaksStoped)
                {
                    UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Consumed ResetAction -> FinalizeConversationState ");
                    
                } else {

                    UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Consumed ResetAction -> has no effect when waiting for RecordingSave or playbacks still running");

                }
              
            }
        }

        public void Exit()
        {
            _flow.Stage.ReactiveIdleEnd(_reactiveIdles);

            _flow._data.GoToPlaybackPreRecordedState = _goToPlaybackPreRecordedScenes;
            _flow._data.GoToRecordRemainingState = _goToRecordRemainingIdles;
            _flow._data.GoToSpeakerState = _goToSpeakerState;
            
            //hier muss noch eine _flow.RecordRemainingToEnd_DataAdjustments(); hin
            //
            
            /*
            if(_isUsingXr){
                // Augenhöhe / MainCamera wieder auf neutral setzen.
                _flow.Stage.ResetEmbodimentHeight();
                //Grösse der Welt wieder zurücksetzen
                _flow.Stage.ResetVisualScaleOfConversationStage(toBeRecorded);
            }*/
           
            //UnityEngine.Debug.Log("[RecordRemainingIdlesAfterPreRecordedEncounterState] Exit");
            PrintRoleLists("[RecordRemainingIdlesAfterPreRecordedEncounterState] Exit", _playbacks, _reactiveIdles,_toBeRecorded);
        }

        private void PrepareStartPlaybacksReactiveIdlesForScene(int sceneCount, int toBeRecorded)
        {
            
            
            //hier werden alle existierenden Rollen in die Liste der Playbacks aufgenommen
            _playbacks = _flow.Stage.PlaybackIndicesWithTakeForScene(_activeRoles, sceneCount);
            
            
            //_playbacks = _flow._data.Playbacks;
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] SceneCount is: {_playbacks.Count} playbacks: [" + string.Join(", ", _playbacks) + "]");
            _flow.Stage.PlaybackStart(_playbacks, sceneCount, sceneCount);

            //alle Rollen ohne Take sind in ReactiveIdles
            _reactiveIdles = GetIdles(sceneCount, toBeRecorded);

            //---------- Das muss ev. noch geändert werden
            _flow.Stage.ReactiveIdleStart(_reactiveIdles, toBeRecorded);

        }

        private List<int> GetIdles(int sceneCount, int toBeRecorded)
        {
            List<int> playbacks  = new List<int>();
            playbacks = _flow.Stage.PlaybackIndicesWithTakeForScene(_activeRoles, sceneCount);
            List<int> idles  = new List<int>();
            foreach(RoleRig role in _flow._data.Roles)
            {
                if (!playbacks.Contains(role.roleIndex))
                {
                    idles.Add(role.roleIndex);
                }
                
            }
            idles.Remove(toBeRecorded);

            return idles;
        }

        private List<int> GetActiveRoleIndices(int toBeRecorded)
        {
            List<int> activeRoles  = new List<int>();
            foreach(RoleRig role in _flow._data.Roles)
            {
                
                activeRoles.Add(role.roleIndex);
                
                
            }
            activeRoles.Remove(toBeRecorded);
            return activeRoles;
        }

        private void SetFutureFlowDirection()
        {
            
            if (_flow._data.ReactiveIdles.Count > 0)
            {
                _goToRecordRemainingIdles = true;
                _goToPlaybackPreRecordedScenes = false;
                _goToSpeakerState = false;  
            }
            else
            {
                _goToRecordRemainingIdles = false;
                _goToPlaybackPreRecordedScenes = false;
                _goToSpeakerState = true;              
            }
        }

        //für das Debugging
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

            Debug.Log(
                $"[{text}] " +
                $"[RoleLists] " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"sceneCount={_sceneCount} | " +
                $"toBeRecorded={toBeRecorded} " 
            );
        }


    }
}