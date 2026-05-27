using UnityEngine;
using System.Collections.Generic;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class ChooseSpeakerState : IState
    {
        private readonly FlowController _flow;

        private int _toBeRecorded;
        private int sceneCount;

        private List<int> _reactiveIdles;

        private List<int> _playbacks;

        private int _roleCount;

        private int currentlySelected;

        private List<int> _selectableRoles;
        private int actionCounter;
       

        private Transform _stageRoot;

        //das kommt von der ConversationStage Inspector und bedeutet "kann man den nächsten 
        // nächsten Sprecher / Zuhörer auswählen oder nicht.
        private bool selectableNext;

        /*
        private bool _allplaybaksStoped = false;
        private bool _waitingForRecordingSave = false;
        private bool _startWaitingToSwitchToFullPlayback = false;

        
        */
        private bool _isUsingXr;
        public DialogueMode Mode => DialogueMode.ChooseSpeaker;

        public ChooseSpeakerState(FlowController flow)
        {
            _flow = flow;
        }

        public void Enter()
        {

            _isUsingXr = _flow.Stage.UseXR;

            if (_flow == null)
            {
                UnityEngine.Debug.LogError("[ChooseSpeakerState] _flow is null");
                return;
            }

            if (_flow.Stage == null)
            {
                UnityEngine.Debug.LogError("[ChooseSpeakerState] _flow.Stage is null");
                return;
            }

            if (_flow.StatusUI != null)
            {
                _flow.StatusUI.ShowChooseSpeakerState();
            }
            

            _reactiveIdles = _flow._data.ReactiveIdles;

            _roleCount = _flow._data.RoleCount;


            if (_reactiveIdles == null)
            {
                Debug.LogWarning("[ChooseSpeakerState] ReactiveIdles was null, creating empty list.");
                _reactiveIdles = new List<int>();
            }

            _selectableRoles = new List<int>();

            _playbacks = _flow._data.Playbacks;
            if (_playbacks == null)
            {
                Debug.LogWarning("[ChooseSpeakerState] _playbacks was null, creating empty list.");
                _playbacks = new List<int>();
            }

            if(_playbacks.Count < _roleCount)
            {
                for (int i = 0; i < _roleCount; i++)
                {
                    if (!_playbacks.Contains(i))
                    {
                        _selectableRoles.Add(i);
                        Debug.Log($"[ChooseSpeakerState] role {i} added to selectableRoles because ReactiveIdles is empty.");
                    }
                    
                    
                }

            }
            else
            {
                for (int i = 0; i < _roleCount; i++)
                {
                    _selectableRoles.Add(i);
                    Debug.Log($"[ChooseSpeakerState] role {i} added to selectableRoles because ReactiveIdles is empty.");  
                }

                
            }
            currentlySelected = _selectableRoles[0];

 

            _toBeRecorded = _flow._data.ToBeRecorded;
            _stageRoot = _flow.Stage._stageRoot;
            
            sceneCount = _flow._data.SceneCount;

            _flow.Stage.ChooseSpeakerController.MoveXrOriginBackFromStage();
            _flow.Stage.ChooseSpeakerController.SelectNextCylinderVisible(true);
            _flow.Stage.ChooseSpeakerController.SetCylinderToSelected(currentlySelected);
            if (sceneCount > -1 && _playbacks.Count>0)
            {
                _flow.Stage.PlaybackStart(_playbacks, sceneCount);
                
            }
            
         
            UnityEngine.Debug.Log("[ChooseSpeakerState] Enter");
        }

        public void Tick(float dt)
        {


            if (_playbacks.Count > 0)
            {
                _flow.Stage.PlaybackTick(_playbacks);
            }
            

            if (_flow.ConsumePrimaryAction())
            {
                actionCounter ++;
                if(_selectableRoles.Count > 0)
                {
                    currentlySelected = _selectableRoles[actionCounter % _selectableRoles.Count];
                    
                }
                
                UnityEngine.Debug.Log($"[ChooseSpeakerState] currentlySelected ={currentlySelected}, actionCounter={actionCounter}");

                // order: string text, List<int> playbacks, List<int> reactiveIdles, List<int> currentlySelectable, int toBeRecorded, int currentlySelected)
                PrintRoleLists("[ChooseSpeakerState] Enter", _playbacks, _reactiveIdles, _selectableRoles, _toBeRecorded, currentlySelected);
                _flow.Stage.ChooseSpeakerController.SetCylinderToSelected(currentlySelected);
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed PrimaryAction");
                // sp�ter: _flow.SetState(new CalibrateState(_flow));
            }

            if (_flow.ConsumeSecondaryAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed SecondaryAction");
                _flow._data.SelectedNext = currentlySelected;
                if(_isUsingXr){
                    // GoToSpeakerState wird hier schon gesetzt, weil im RecordListenerState Exit die reactiveIdles schon neu gesetzt werden
                    // basierend auf den Reactive Idles muss der PlayerAlignState den Ziel State bestimmen.
                    if (sceneCount< 0)
                    {
                        UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount<0 sceneCount = {sceneCount}");
                        _flow._data.GoToSpeakerState = true;
                    }
                    else
                    {
                        if(_reactiveIdles.Count > 0){
                            UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount>=0 sceneCount = {sceneCount} _reactiveIdles.Count = {_reactiveIdles.Count}");
                            _flow._data.GoToSpeakerState = false;
                        }else{
                            UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount>=0 sceneCount = {sceneCount} reactiveIdlesCount<= 0  _reactiveIdles.Count = {_reactiveIdles.Count}");
                            _flow._data.GoToSpeakerState = true;
                        }
                            
                    }
                    UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount<0 sceneCount = {sceneCount}");
                    _flow.SetState(new PlayerAlignState(_flow));
                }else{
                    if(_reactiveIdles.Count > 0){
                        _flow.SetState(new RecordListenersState(_flow));
                    }else{
                        _flow.SetState(new RecordSpeakerState(_flow));
                    }
                }

            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed ResetAction");
            }
        }

        public void Exit()
        {

            if (_playbacks.Count > 0)
            {
                _flow.Stage.PlaybackStop(_playbacks);
            }
            if(_playbacks.Count == _roleCount)
            {
                _playbacks = new List<int>();

                //hier müssen dann playbacks wieder auf 0 gesetzt werden. 
                _flow._data.Playbacks = _playbacks;
                _flow._data.ReactiveIdles = _selectableRoles;
                _flow.IncrementSceneCount();
            }

            _flow._data.SelectedNext = currentlySelected;
            _flow._data.ToBeRecorded = currentlySelected;
            _flow.Stage.ChooseSpeakerController.SelectNextCylinderVisible(false);
            UnityEngine.Debug.Log("[ChooseSpeakerState] Exit");
        }

        private void PrintRoleLists(
            string text, 
            List<int> playbacks,
            List<int> reactiveIdles,
            List<int> currentlySelectable,
            int toBeRecorded,
            int currentlySelected)
        {
            string playbacksString =
                playbacks == null || playbacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", playbacks) + "]";

            string reactiveIdlesString =
                reactiveIdles == null || reactiveIdles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", reactiveIdles) + "]";

            string currentlySelectableString =
                currentlySelectable == null || currentlySelectable.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", currentlySelectable) + "]";

            Debug.Log(
                $"[{text}] " +
                $"[RoleLists] " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"currentlySelectable={currentlySelectableString} | " +
                $"toBeRecorded={toBeRecorded} | " +
                $"currentlySelected={currentlySelected}"
            );
        }

    }
}