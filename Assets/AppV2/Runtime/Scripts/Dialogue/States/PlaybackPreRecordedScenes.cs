using UnityEngine;
using System.Collections.Generic;
using AppV2.Runtime.Scripts.DataStructures;

using System.Data;

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

        private float _radiusNpcStartTalking;

        private int _toBeRecorded;
        private bool _startInPlaybackFullConversationMode;
        
        private string _npcGroupId;
        private bool _allplaybaksStopped = false;
        private bool _waitingForRecordingSave = false;
         private bool _saveCompleted = false;

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
            //Debug.Log($"[PlaybackFullPreRecordedScenes] Enter: roleCount is: {_roleCount}, SceneCount is: {_sceneCount} SceneCount For PrerecordedScenes is: {_sceneCountForPreRecordedScenes} [checkSceneCount01]");
            _roleCount =  _flow._data.CurrentPreRecordedPlaybacks.Count;
            _preRecordedRolesIndices = _flow._data.CurrentPreRecordedPlaybacks;
            _seatedMode = _flow.Stage.SeatedMode;
            _toBeRecorded = _flow._data.ToBeRecorded;
            //Debug.Log($"[PlaybackFullPreRecordedScenes] Enter: _toBeRecorded index is: {_toBeRecorded}");
            _roleCount =  _flow._data.CurrentPreRecordedPlaybacks.Count;
            _radiusNpcStartTalking = _flow.Stage._radiusNpcStartTalking;
            //Debug.Log($"[PlaybackFullPreRecordedScenes] Enter after RoleCountUpdate: roleCount is: {_roleCount}, SceneCount is: {_sceneCount} SceneCount For PrerecordedScenes is: {_sceneCountForPreRecordedScenes}");

            _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);

            if (_seatedMode)
            {
                _flow.Stage.ChooseSpeakerController.MoveXrOriginBackFromStage();
            }

            
            
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] SceneCount is: {_sceneCount}");
            

          

            if (_flow.StatusUI != null)
            {
                _flow.StatusUI.ShowPlaybackPreRecordedScene();
                _flow.StatusUI.ShowCustomCue(
                    "Hör zu!",
                    new Vector2(0f, 180f),
                    new Vector2(500f, 0f),
                    Color.red
                );
            }
            
            //Debug.Log($"[PlaybackFullPreRecordedScenes] Enter: roleCount is: {_roleCount}, SceneCountForPreRecordedScenes is: {_sceneCountForPreRecordedScenes} activeRoles: {_flow._data.IndicesOfPassiveRoles}");
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
                //_flow.SetState(new IdleState(_flow));
            }

            if (_flow.ConsumeResetAction())
            {
                //UnityEngine.Debug.Log("[PlaybackFullPreRecordedScenes] Consumed ResetAction");
            }

            if (!_allplaybaksStopped)
            {
                _flow.Stage.DriveAndRecordTickActiveRole(
                    _toBeRecorded, _sceneCount, dt
                );

                _flow.Stage.PlaybackTick(_playbacks);

                _allplaybaksStopped =
                    _flow.Stage.PlaybacksAreAllStopped(_preRecordedRolesIndices);
            }
            else if (!_waitingForRecordingSave && !_saveCompleted)
            {
                // Playbacks beendet, Recording abschließen

                _flow.Stage.DriveAndRecordTickActiveRole(
                    _toBeRecorded, _sceneCount, dt
                );

                _flow.Stage.RecordingEnd(_toBeRecorded, _sceneCount);

                //Debug.Log( $"[PRE REC] RecordingEnd: Scene {_sceneCount}");

                _waitingForRecordingSave = true;
            }
            else if (_waitingForRecordingSave && !_saveCompleted)
            {
                // Recorder weiter aktualisieren, damit Finalisierung erfolgt
                _flow.Stage.DriveAndRecordTickActiveRole(
                    _toBeRecorded, _sceneCount, dt
                );
                // Warten, bis Recording gespeichert wurde

                if (_flow.Stage.RecordingSaveCompleted())
                {
                    //Debug.Log("[PRE REC] RecordingSaveCompleted!");

                    _sceneCountForPreRecordedScenes++;
                    _sceneCount++;

                    _waitingForRecordingSave = false;
                    _saveCompleted = true;
                }
            }

            if (_saveCompleted && _allplaybaksStopped)
            {
                _saveCompleted = false;

                _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
               
                bool preRecordedPlaybackTakesLeft = _flow.Stage.PlaybackHasAnyTakeForSceneForIndexList(_preRecordedRolesIndices, _sceneCountForPreRecordedScenes);
                bool playerNearCurrentNpcs = _flow.PlayerNearNpcs(_flow._data.CurrentPreRecordedPlaybacks, _flow._data.ToBeRecorded, _radiusNpcStartTalking);
                bool playerNearOtherNpcs = _flow.PlayerNearNewNpcsThatAreNotCurrentNpcs(_flow._data.IndicesOfPassiveRoles, _flow._data.CurrentPreRecordedPlaybacks, _toBeRecorded,_radiusNpcStartTalking);
                
                //Fall 1
                if (preRecordedPlaybackTakesLeft && playerNearCurrentNpcs && !playerNearOtherNpcs)
                {
                    //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] after update: SceneCount for Prerecorded Scenes is: {_sceneCountForPreRecordedScenes}");
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    PrepareStartPlaybacksReactiveIdlesForScene();
                    _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);
                    _allplaybaksStopped = false;
                    _saveCompleted  = false;
                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 1 TakesLeft && Player is near Current NPC and NOT near other NPCs. ");
                }
                //Fall 2
                if (preRecordedPlaybackTakesLeft && playerNearCurrentNpcs && playerNearOtherNpcs)
                {
                    _flow.Stage.ReactiveIdleEnd(_reactiveIdles);
                    PrepareStartPlaybacksReactiveIdlesForScene();
                    _flow.Stage.RecordingBegin(_toBeRecorded,_sceneCount);
                    _allplaybaksStopped = false;
                    _saveCompleted  = false;
                    Debug.LogError($"[PlaybackPreRecordedScenes] Fall 2 Player is near Current NPC and near other NPCs that should not be possible. ");
                }
                //Fall 3
                if (preRecordedPlaybackTakesLeft && !playerNearCurrentNpcs && !playerNearOtherNpcs)
                {
                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 3 No Takes Left, Player is NOT near Current NPC and Not near other NPCs RoleCount ={_flow._data.Roles.Count} ");
                    _flow.PlaybackPreRecordedToSpeakerIfPlayerWentOn_DataAdjustments();
                    if(_flow._data.Roles.Count == 1)
                    {
                        
                        _flow._data.GoToSpeakerState = true;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = false;
                        _flow.SetState(new RecordSpeakerState(_flow));
                        
                    }else
                    {
                        _flow._data.GoToSpeakerState = false;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = true;
                        _flow.SetState(new PlayerAlignState(_flow));
                    }
                }
                //Fall 4
                if (preRecordedPlaybackTakesLeft && !playerNearCurrentNpcs && playerNearOtherNpcs)
                {
                    //hier wird die letzte abgespielte PreRecorded Scene in die aktuelle Session integriert.
                    _flow.Stage.SwitchNpcGroupToCurrentSession(_flow._data.CurrentNpcGroupId);
                    
                    _npcGroupId = _flow.GetNpcGroupId(_flow._data.IndicesOfPassiveRoles, _toBeRecorded, _radiusNpcStartTalking);
                    //in diesem Fall, soll die Anzahl der aktiven Rollen reduziert werden, weil es sonst zu kompliziert wird.

                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 4 Some Takes Left, Player is NOT near Current NPC and near other NPCs RoleCount ={_flow._data.Roles.Count} ");
                    if(_flow._data.Roles.Count != 1)
                    {
                        _flow._data.ActiveRoleCount =1;
                        List<int> indicesToBeRemovedFromActiveRoles = new List<int>();

                        foreach(RoleRig role in _flow._data.Roles){
                            int i = role.roleIndex;
                            if(i != _toBeRecorded && !_flow._data.IndicesOfPassiveRoles.Contains(i))
                            { 
                                indicesToBeRemovedFromActiveRoles.Add(i);
                                role.isActiveConversationPartner = false;
                                
                                _flow._data.IndicesOfPassiveRoles.Add(i);
                                
                            }
             
                        }
                      
                        foreach(RoleRig role in _flow._data.Roles)
                        {
                            if (!indicesToBeRemovedFromActiveRoles.Contains(role.roleIndex))
                            {
                                continue;
                            }
                            else
                            {
                                _flow._data.Roles.Remove(role);
                            }
                        }
                    }
                    _flow._data.Playbacks.Clear();
                    _flow._data.ReactiveIdles.Clear();
                    _flow._data.ActiveRoleCount = _flow._data.Roles.Count;
                    _flow._data.CurrentNpcGroupId = _npcGroupId;
                    _flow._data.SceneCount = _sceneCount;
                    _flow.RecordSpeakerToPlaybackPreRecorded_DataAdjustments(
                        _flow._data.IndicesOfPassiveRoles,
                        _toBeRecorded,
                        _radiusNpcStartTalking,
                        _npcGroupId);
                    _flow._data.GoToSpeakerState = false;
                    _flow._data.GoToPlaybackPreRecordedState = true;
                    _flow._data.GoToRecordRemainingState = false;
                    _flow.SetState(new PlaybackFullPreRecordedScenes(_flow));
                        
                }
                
                //Fall 5
                if (!preRecordedPlaybackTakesLeft && playerNearCurrentNpcs && !playerNearOtherNpcs)
                {
                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 5 NoTakes Left Player is near Current NPC and Not near other NPCs ");
                    if(_flow._data.Roles.Count == 1)
                    {
                        Debug.Log($"[PlaybackPreRecordedScenes] Fall 5 No Takes Left, Player is near Current NPC and Not near other NPCs RoleCount ={_flow._data.Roles.Count} ");
                        _flow.PlaybackPreRecordedToSpeaker_DataAdjustments();
                        _flow._data.GoToSpeakerState = true;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = false;
                        _flow.SetState(new RecordSpeakerState(_flow));
                        
                    }else
                    {   
                        Debug.Log($"[PlaybackPreRecordedScenes] Fall 5 No Takes Left, Player is near Current NPC and Not near other NPCs RoleCount ={_flow._data.Roles.Count} ");
                        _flow.PlaybackPreRecordedToRecordRemaining_DataAdjustments();
                        _flow._data.GoToSpeakerState = false;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = true;
                        _flow.SetState(new PlayerAlignState(_flow));
                    }
                    
                }
                //Fall 6
                if (!preRecordedPlaybackTakesLeft && playerNearCurrentNpcs && playerNearOtherNpcs)
                {
                    Debug.LogError($"[PlaybackPreRecordedScenes] Player is near Current NPC and near other NPCs that should not be possible. ");
                }
                //Fall 7
                if(!preRecordedPlaybackTakesLeft && !playerNearCurrentNpcs && !playerNearOtherNpcs)
                {
                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 7 No Takes Left, Player NOT is near Current NPC and Not near other NPCs RoleCount ={_flow._data.Roles.Count} ");
                    //erst letzte PreRecorded zu session hinzufügen, etc.
                    _flow.PlaybackPreRecordedToSpeaker_DataAdjustments();

                    //dann alle zu weit entfernten aktiven Rollen entfernen.
                    _flow.RemoveActiveRolesTooFarAwayFromPlayer(_toBeRecorded);
                    _flow.SpeakerStateExitAutoSelection();
                    if(_flow._data.Roles.Count == 1)
                    {
                        
                        _flow._data.GoToSpeakerState = true;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = false;
                        _flow.SetState(new RecordSpeakerState(_flow));
                        
                    }else
                    {   
                        _flow._data.GoToSpeakerState = false;
                        _flow._data.GoToPlaybackPreRecordedState = false;
                        _flow._data.GoToRecordRemainingState = false;
                        _flow.SetState(new PlayerAlignState(_flow));
                    }
                }
                //Fall 8
                if(!preRecordedPlaybackTakesLeft && !playerNearCurrentNpcs && playerNearOtherNpcs)
                {
                    Debug.Log($"[PlaybackPreRecordedScenes] Fall 8 No Takes Left, Player NOT is near Current NPC and NEAR other NPCs RoleCount ={_flow._data.Roles.Count} ");
                    //erst letzte PreRecorded zu session hinzufügen, etc.
                    _flow.PlaybackPreRecordedToSpeaker_DataAdjustments();

                    //dann alle zu weit entfernten aktiven Rollen entfernen.
                    _flow.RemoveActiveRolesTooFarAwayFromPlayer(_toBeRecorded);
                    _npcGroupId = _flow.GetNpcGroupId(_flow._data.IndicesOfPassiveRoles, _toBeRecorded, _radiusNpcStartTalking);
                    _flow._data.SceneCount = _sceneCount;
                    _flow.RecordSpeakerToPlaybackPreRecorded_DataAdjustments(_flow._data.IndicesOfPassiveRoles, _toBeRecorded, _radiusNpcStartTalking, _npcGroupId);
                    _flow.SetState(new PlaybackFullPreRecordedScenes(_flow));
               
                    
                }


      
           
                    
                    
            }
        }
        

        private void PrepareStartPlaybacksReactiveIdlesForScene()
        {
            //hier werden alle existierenden Rollen in die Liste der Playbacks aufgenommen
            _playbacks = PlaybackCandidates();
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] SceneCount is: {_playbacks.Count} playbacks: [" + string.Join(", ", _playbacks) + "]");
            //hier wird das kopieren der PreRecoreded Data zum aktuellen Session Ordner gestartet.
            _noTakes = _flow.Stage.PlaybackStart(_playbacks,_sceneCount, _sceneCountForPreRecordedScenes);

            //alle Rollen ohne Take sind in ReactiveIdles
            _reactiveIdles = _noTakes;

            //---------- Das muss ev. noch geändert werden
            _flow.Stage.ReactiveIdleStart(_reactiveIdles, _playbacks[0]);

            //Rollen ohne Takes werden von den Playbacks entfernt
            foreach (int idleIndex in _noTakes)
            {
                _playbacks.Remove(idleIndex);
            }

            
            
        }

        public void Exit()
        {
            //PrepareStartPlaybacksReactiveIdlesForScene();
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] Exit: CurrentNpcGroupId is: {_flow._data.CurrentNpcGroupId}");
            //
            _flow._data.SceneCount = _sceneCount;
            //UnityEngine.Debug.Log($"[PlaybackFullPreRecordedScenes] Exit: _flow._data.SceneCount is: {_flow._data.SceneCount}");
            _flow._data.SceneCountWhilePlaybackPreRecorded = _flow._data.SceneCountBeforePlaybackPreRecorded;
            //UnityEngine.Debug.Log($"[PlaybackPreRecordedScenes] Indices of IndicesOfPassiveRoles has length (before update): {_flow._data.IndicesOfPassiveRoles.Count}");
            //UnityEngine.Debug.Log($"[PlaybackPreRecordedScenes] Active Roles have length (before update): {_flow._data.Roles.Count}");
            // preRecordedPlaybacks zu aktiven Rollen zufügen

     
            
            
            //UnityEngine.Debug.Log($"[PlaybackPreRecordedScenes] Indices of IndicesOfPassiveRoles has length (after update): {_flow._data.IndicesOfPassiveRoles.Count}");
            //UnityEngine.Debug.Log($"[PlaybackPreRecordedScenes] Active Roles have length (after update): {_flow._data.Roles.Count}");
            //_flow.Stage.SwitchNpcGroupToCurrentSession(_flow._data.CurrentNpcGroupId);

            /*PrintRoleLists(
                            "[PlaybackPreRecorededScenes] At Exit -> before PlayerAlignState", 
                            _flow._data.Playbacks,
                            _flow._data.ReactiveIdles,
                            _flow._data.CurrentPreRecordedPlaybacks,
                            _flow._data.ToBeRecorded
                            );*/
        }

        private List<int> PlaybackCandidates(){
            List<int> playbackCandidates =new List<int>();

            foreach (RoleRig role in _flow._data.AllRoles)
                {
                    if(role.npcGroupId == _flow._data.CurrentNpcGroupId)
                    {
                        playbackCandidates.Add(role.roleIndex);
                    }
                       
                }
            
            return playbackCandidates;
        }

        private void PrintRoleLists(
            string text, 
            List<int> playbacks,
            List<int> reactiveIdles,
            List<int> currentPreRecorded,
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

            string currentPreRecordedString =
                currentPreRecorded == null || currentPreRecorded.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", currentPreRecorded) + "]";

            Debug.Log(
                $"[{text}] " +
                $"[RoleLists] " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"currentPreRecorded={currentPreRecordedString} | " +
                $"toBeRecorded={toBeRecorded} " 
            );
        }

    }
}