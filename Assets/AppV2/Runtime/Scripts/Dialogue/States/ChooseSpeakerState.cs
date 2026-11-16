using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class ChooseSpeakerState : IState
    {
        //für das doppel klicken mit secondary action, um eine Aufnahme zu überspringen.
        private int secondaryActionCount = 0;
        private float doubleSecondaryActionTriggerTime = 1f;

        private Coroutine secondaryActionTriggerCoroutine;


        private readonly FlowController _flow;

        private int _toBeRecorded;
        private int sceneCount;

        private List<int> _reactiveIdles;

        private List<int> _playbacks;

        private int _roleCount;

        private int currentlySelected;

        private List<int> _selectableRoles;
        private bool _waitingForActivation;
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
            //UnityEngine.Debug.Log("[ChooseSpeakerState] Enter");

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
                
                _flow.StatusUI.ShowCustomCue(
                    "Wähle Figur Modus.\n",
                    new Vector2(0f, 180f),
                    new Vector2(200f, 0f),
                    Color.red
                );
            }
            

            _reactiveIdles = _flow._data.ReactiveIdles;

            _roleCount = _flow._data.ActiveRoleCount;


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
            _flow._data.RefreshMembership();
            RefreshSelectableRoles();

            _toBeRecorded = _flow._data.ToBeRecorded;
            _stageRoot = _flow.Stage._stageRoot;
            
            sceneCount = _flow._data.SceneCount;

            _flow.Stage.ChooseSpeakerController.MoveXrOriginBackFromStage();
            _flow.Stage.ChooseSpeakerController.SelectNextCylinderVisible(currentlySelected >= 0);
            if (currentlySelected >= 0) _flow.Stage.ChooseSpeakerController.SetCylinderToSelected(currentlySelected);
            if (sceneCount > -1 && _playbacks.Count>0)
            {
                //hier zweimal sceneCount, weil es den SceneCount für die aktuelle Session und SceneCount PreRecorded gibt. 
                _flow.Stage.PlaybackStart(_playbacks, sceneCount, sceneCount);
                
            }
            
         
            
        }

        private void RefreshSelectableRoles()
        {
            _roleCount = _flow._data.ActiveRoleCount;
            var next = _flow._data.GetSelectableRoleIndices(_playbacks);
            bool changed = next.Count != _selectableRoles.Count;
            if (!changed) for (int i = 0; i < next.Count; i++) if (next[i] != _selectableRoles[i]) changed = true;
            _selectableRoles = next;
            if (!_selectableRoles.Contains(currentlySelected))
                currentlySelected = _selectableRoles.Count > 0 ? _selectableRoles[0] : -1;
            if (changed)
            {
                actionCounter = Mathf.Max(0, _selectableRoles.IndexOf(currentlySelected));
                _flow.Stage.ChooseSpeakerController.SelectNextCylinderVisible(currentlySelected >= 0);
                if (currentlySelected >= 0) _flow.Stage.ChooseSpeakerController.SetCylinderToSelected(currentlySelected);
            }
            if (currentlySelected < 0 && !_waitingForActivation)
                _flow.StatusUI?.ShowCustomCue("Noch keine aktive Rolle. Bitte zuerst einen Gespraechspartner aktivieren.",
                    new Vector2(0f,180f),new Vector2(500f,0f),Color.white);
            _waitingForActivation = currentlySelected < 0;
        }

        public void Tick(float dt)
        {
            // Also reflects flags changed through the runtime inspector while choosing.
            _flow._data.RefreshMembership();
            RefreshSelectableRoles();


            if (_playbacks.Count > 0)
            {
                _flow.Stage.PlaybackTick(_playbacks);
            }
            

            if (currentlySelected < 0)
            {
                _flow.ConsumePrimaryAction(); _flow.ConsumeSecondaryAction();
                return;
            }
            if (_flow.ConsumePrimaryAction())
            {
                actionCounter ++;
                if(_selectableRoles.Count > 0)
                {
                    currentlySelected = _selectableRoles[actionCounter % _selectableRoles.Count];
                    
                }
                
                //UnityEngine.Debug.Log($"[ChooseSpeakerState] currentlySelected ={currentlySelected}, actionCounter={actionCounter}");

                // order: string text, List<int> playbacks, List<int> reactiveIdles, List<int> currentlySelectable, int toBeRecorded, int currentlySelected)
                //PrintRoleLists("[ChooseSpeakerState] Enter", _playbacks, _reactiveIdles, _selectableRoles, _toBeRecorded, currentlySelected);
                _flow.Stage.ChooseSpeakerController.SetCylinderToSelected(currentlySelected);
                //UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed PrimaryAction");
                
            }

            if (_flow.ConsumeSecondaryAction())
            {
                secondaryActionCount ++;
                //UnityEngine.Debug.Log($"[ChooseSpeakerState] Consumed SecondaryAction secondaryActionCount is: {secondaryActionCount}");
                _flow._data.SelectedNext = currentlySelected;
                if (secondaryActionCount == 1)
                {
                    //Starte WarteZeit, Action von 1 mal Click ist in secondaryActionTriggerCoroutine.
                    // StartCoroutine muss über den FlowController gestartet werden, weil der ein Monobehaviour ist.
                    // dennoch ist SecondaryActionTimer in ChooseSpeakerState gespeichert. muss nur vom Monobehaviour aus gestartet werden.
                    secondaryActionTriggerCoroutine = _flow.StartCoroutine(SecondaryActionTimer());
                    
                }
                else if(secondaryActionCount == 2)
                {
                    if(secondaryActionTriggerCoroutine != null)
                    {
                        _flow.StopCoroutine(secondaryActionTriggerCoroutine);
                    }
                    secondaryActionCount = 0;
                    // hier die Auslösung von doppel click/ secondaryAction 
                    OnDoubleSecAction();
                }
                
            }

            if (_flow.ConsumeResetAction())
            {
                UnityEngine.Debug.Log("[ChooseSpeakerState] Consumed ResetAction");
            }
        }

        //für das doppelklick in secondaryAction
        private System.Collections.IEnumerator SecondaryActionTimer()
        {
            yield return new WaitForSeconds(doubleSecondaryActionTriggerTime);

            // Kein zweiter Klick -> Single Click
            if (secondaryActionCount == 1)
            {
                OnSingleSecAction();
            }

            secondaryActionCount = 0;
        }

        void OnSingleSecAction()
        {
            //Debug.Log("Single Click");
            ContinueConversationLogic();
            
        }


        private void ContinueConversationLogic()
        {
            if(_isUsingXr)
            {
                // GoToSpeakerState wird hier schon gesetzt, weil im RecordListenerState Exit die reactiveIdles schon neu gesetzt werden
                // basierend auf den Reactive Idles muss der PlayerAlignState den Ziel State bestimmen.
                if (sceneCount< 0)
                {
                    //UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount<0 sceneCount = {sceneCount}");
                    _flow._data.GoToSpeakerState = true;
                }
                else
                {
                    if(_playbacks.Count > 0){
                        //UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount>=0 sceneCount = {sceneCount} _reactiveIdles.Count = {_reactiveIdles.Count}");
                        _flow._data.GoToSpeakerState = false;
                    }else{
                        //UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount>=0 sceneCount = {sceneCount} reactiveIdlesCount<= 0  _reactiveIdles.Count = {_reactiveIdles.Count}");
                        _flow._data.GoToSpeakerState = true;
                    }
                        
                }
                //UnityEngine.Debug.Log($"[ChooseSpeakerState] sceneCount<0 sceneCount = {sceneCount}");
                _flow.SetState(new PlayerAlignState(_flow));
            }else
            {
                if(_reactiveIdles.Count > 0){
                    _flow.SetState(new RecordListenersState(_flow));
                }else{
                    _flow.SetState(new RecordSpeakerState(_flow));
                }
            }
            
        }
        private void OnDoubleSecAction()
        {
            //überspringen von Aufnahmen geht nur, wenn es für die entsprechende Scene schon mindestens 1 Aufnahme von einer Rolle gibt.
            // wenn _playbacks == 0, dann gibt es für die Scene noch keine Aufnahmen
            if(_playbacks.Count != 0 )
            {
                //Debug.Log($"[ChooseSpeakerState] Double Secondary Action: Conversation is continued while skipping the takes for some roles | playbacks.Count= {_playbacks.Count}");
                ResetPlaybacksReactiveIdlesIncSceneCount();
                //wenn man Takes überspringt, kommt ein neuer ChooseSpeakerState zum Auswählen des nächsten Sprechers.
                _flow.SetState(new ChooseSpeakerState(_flow));
            }
            else
            {
                //Debug.Log($"[ChooseSpeakerState] Double Secondary Action: Conversation is continued while NOT skipping takes for any roles | playbacks.Count= {_playbacks.Count}");
                ContinueConversationLogic();
            }
            
        }

        private void ResetPlaybacksReactiveIdlesIncSceneCount()
        {
            _flow.Stage.PlaybackStop(_playbacks);
            _playbacks = new List<int>();
            //hier müssen dann playbacks wieder auf 0 gesetzt werden. 
            _flow._data.Playbacks = _playbacks;
             _reactiveIdles = AllRoleIndices(_roleCount);
             _flow._data.ReactiveIdles = _reactiveIdles;
            _flow._data.GoToSpeakerState = true;
            _flow.IncrementSceneCount();
            
        }

        public void Exit()
        {

            if (_playbacks.Count > 0)
            {
                _flow.Stage.PlaybackStop(_playbacks);
            }
            if (_roleCount > 0 && _flow._data.Roles.TrueForAll(r => _playbacks.Contains(r.roleIndex)))
            {
                ResetPlaybacksReactiveIdlesIncSceneCount();
            }

            _flow._data.SelectedNext = currentlySelected;
            if (currentlySelected >= 0) _flow._data.ToBeRecorded = currentlySelected;
            _flow.Stage.ChooseSpeakerController.SelectNextCylinderVisible(false);
            //UnityEngine.Debug.Log("[ChooseSpeakerState] Exit");
        }

        private List<int> AllRoleIndices(int roleCount){
            List<int> allRoleIndices =new List<int>();

            foreach (var role in _flow._data.Roles) allRoleIndices.Add(role.roleIndex);
            return allRoleIndices;
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