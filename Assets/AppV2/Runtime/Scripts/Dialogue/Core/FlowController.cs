using System.Diagnostics;
using UnityEngine;
using System.Collections.Generic;

using AppV2.Runtime.Scripts.Input;
using AppV2.Runtime.Scripts.Dialogue.States;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.UI;

namespace AppV2.Runtime.Scripts.Dialogue
{
    public class FlowController : MonoBehaviour
    {
        private IState _state;

        //das kommt von der ConversationStage Inspector und bedeutet "kann man den nächsten 
        // nächsten Sprecher / Zuhörer auswählen oder nicht.
        private bool selectableNext;

        // für die Steuerung bspw. XR => grip, trigger, keyboard => spacekey, enter, backspace
        public KeyboardInputEvents keyboardInput;
        public XRInputEvents XRInput;

        private bool _primaryAction;
        private bool _secondaryAction;
        private bool _reset;
        private bool _startInPlaybackFullConversationMode;

        //für die Steuerung der Scenen, aktiven Rollen etc.
        public FlowStateData _data;

        //diese Aktionen werden bspw. von States gerufen. Sie kommen irgendwann. bspw. mitten im Frame, 
        // damit sie am Anfang des Frames ausgeführt werden => ConsumePrimary etc.
        public void RequestPrimaryAction() => _primaryAction = true;
        public void RequestSecondaryAction() => _secondaryAction = true;
        public void RequestResetAction() => _reset = true;

        public bool ConsumePrimaryAction() { var v = _primaryAction; _primaryAction = false; return v; }
        public bool ConsumeSecondaryAction() { var v = _secondaryAction; _secondaryAction = false; return v; }
        public bool ConsumeResetAction() { var v = _reset; _reset = false; return v; }

        //damit die States Zugriff haben auf die ConversationStage, um ConversationStage Funktionen zu rufen
        [Header("Objekt mit ConversationStage Script Komponente")]
        public ConversationStage Stage; 

        //damit man merkt, wann es vom Listener zum Speaker wechselt.
        [SerializeField] 
        [Header("Objekt mit ConversationStatusUI-Script Komponente")]
        private ConversationStatusUI statusUI;
        public ConversationStatusUI StatusUI => statusUI;
        public bool _xrMode;

        string _currentNpcGroupId = "";

        private void Awake()
        {
            _data = new FlowStateData();
            _data.Initialize(Stage.roles);
            _startInPlaybackFullConversationMode = Stage.StartInPlaybackFullConversationMode;
            _xrMode = Stage.UseXR;
            UnityEngine.Debug.Log($"[FlowController] roleCount allRoles is: {Stage.roleCount} | activeRoles: {_data.ActiveRoleCount}");
        }

        private void OnDestroy()
        {

        }

        private void Start()
        {
            foreach (var role in _data.AllRoles)
            {
                if (role?.root == null)
                    continue;

                UnityEngine.Debug.Log(
                    $"[NPC Position] [FLOW START BEGIN] {role.roleId}: " +
                    $"local={role.root.localPosition}, " +
                    $"world={role.root.position}"
                );
            }
            selectableNext = Stage.selectableNext;
            if(_startInPlaybackFullConversationMode){
                SetState(new PlaybackFullConversationState(this));
            }else{
                if(_xrMode){
                   
                    SetState(new CalibrationState(this));
                }else {
                    if(selectableNext)
                    {
                        SetState(new ChooseSpeakerState(this));
                    }else{
                        SetState(new RecordSpeakerState(this));
                    }
                }
            }

           
            
        }

        private void Update()
        {
            _state?.Tick(Time.deltaTime);
        }

        public void SetState(IState next)
        {
            string oldStateName = _state?.GetType().Name ?? "null";
            string newStateName = next?.GetType().Name ?? "null";

            Stage.DebugRolePositions(
                $"SetState BEGIN | {oldStateName} -> {newStateName} | frame={Time.frameCount}"
            );

            _state?.Exit();

            Stage.DebugRolePositions(
                $"SetState AFTER EXIT | {oldStateName} -> {newStateName} | frame={Time.frameCount}"
            );

            _state = next;

            Stage.DebugRolePositions(
                $"SetState BEFORE ENTER | {oldStateName} -> {newStateName} | frame={Time.frameCount}"
            );

            _state?.Enter();

            Stage.DebugRolePositions(
                $"SetState AFTER ENTER | {oldStateName} -> {newStateName} | frame={Time.frameCount}"
            );
        }

        //Funktionen, die von den States gerufen werden
        public void IncrementSceneCount()
        {
            _data.SceneCount++;
            //UnityEngine.Debug.Log($"SceneCount is now: {_data.SceneCount}");
        }

        //Das wird am Anfang von RecordListenerState gerufen, um Speaker und 
        // reactiveIdles zu setzen
        public int RecSpeakStateSetSpeaker()
        {
            int nextSpeaker;
            int selected = _data.SelectedNext;

            if(selected == -1){
                nextSpeaker = _data.SceneCount  % _data.ActiveRoleCount;
                //UnityEngine.Debug.Log($"Next Default Speaker has index: {nextSpeaker}");
                //update Rollen, die im Idle sind in _data- object (FlowStateData)
                RecSpeakStateSetReactiveIdles(nextSpeaker);
                //update nächster Sprecher in _data- object (FlowStateData)
                _data.ToBeRecorded = nextSpeaker;
                return nextSpeaker;
            }
            else{
                if(selected > (_data.ActiveRoleCount -1) || selected < 0){
                    UnityEngine.Debug.LogError($"selected Speaker index is out of Range");
                    return -1000;
                }else{
                    //update Rollen, die im Idle sind in _data- object (FlowStateData)
                    RecSpeakStateSetReactiveIdles(selected);
                    //update nächster Sprecher in _data- object (FlowStateData)
                    _data.ToBeRecorded = selected;
                    //Damit die Frage, ob ein nächster Sprecher ausgesucht wurde, nächstes mal wieder funktioniert, zurücksetzen.
                    _data.SelectedNext = -1;
                    //UnityEngine.Debug.Log($"Next Selected Speaker has index: {selected}");
                    return selected;
                }
            }
        }

        //Das wird in RecSpeakStateSetSpeaker gerufen updated das FlowStateData-Object
        public void RecSpeakStateSetReactiveIdles(int nextSpeaker)
        {
            //playbacks leeren bevor es weiter geht.
            _data.Playbacks = new List<int>();
            List<int> reactiveIdles = new List<int>();
            if(nextSpeaker > (_data.AllRoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.AllRoleCount; i++)
                {
                    if(_data.AllRoles[i].roleIndex != nextSpeaker){
                        if (_data.AllRoles[i].isActiveConversationPartner)
                        {
                            reactiveIdles.Add(_data.AllRoles[i].roleIndex);
                    
                        }
                        
                    }
                }
                _data.ReactiveIdles = reactiveIdles;
            }
        }

        public int RecLiStateSetActiveListener()
        {
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"_data.ReactiveIdle List is empty, cannot choose next Listener");
                return -1000;
            }
            int nextListener;
            int reactiveIdlesIndex;
            int selected = _data.SelectedNext;
           
            // checken ob ein nächster Aktiver Zuhörer gewählt wurde.
            if(selected == -1)
            {
                reactiveIdlesIndex = 0;
            }else
            {
                reactiveIdlesIndex = selected;
                // nachher den "nächsten ausgewählten Zuhörer" wieder zurücksetzen, damit es nächstes mal wieder klappt.
                _data.SelectedNext = -1;
            }
            
            //nächsten Sprecher setzen, entweder 0 (default) oder etwas anderes, fall ein nächster gewählt wurde.
            nextListener = _data.ReactiveIdles[reactiveIdlesIndex];
            //den gewählten Sprecher aus der Liste der Idles entfernen. 
            _data.ReactiveIdles.RemoveAt(reactiveIdlesIndex);
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
            // im FlowStateData Objekt den aktuellen nächsten Aufzunehmenden setzen.
            _data.ToBeRecorded = nextListener;

                
            //UnityEngine.Debug.Log($"Next Active Listener has index: {nextListener}");
            
            return nextListener;
            
        }


        //Das wird in RecLiStateSetActiveListener gerufen updated das FlowStateData-Object
        public void RecLiStateSetReactiveIdles(int nextSpeaker)
        {
            //playbacks leeren bevor es weiter geht.
            _data.Playbacks = new List<int>();
            List<int> reactiveIdles = new List<int>();
            if(nextSpeaker > (_data.AllRoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.AllRoleCount; i++)
                {
                    if(_data.AllRoles[i].roleIndex != nextSpeaker){
                        if (_data.AllRoles[i].isActiveConversationPartner)
                        {
                            reactiveIdles.Add(_data.AllRoles[i].roleIndex);
                    
                        }
                        
                    }
                }
                _data.ReactiveIdles = reactiveIdles;
            }
        }

        public void SpeakerStateEnter(){
            int nextSpeaker;
            int selected = _data.SelectedNext;

            if(selected == -1){
                nextSpeaker = _data.SceneCount  % _data.ActiveRoleCount;
                UnityEngine.Debug.Log($"Next Default Speaker has index: {nextSpeaker}");
                //update Rollen, die im Idle sind in _data- object (FlowStateData)
                SpeakerStateEnterSetLists(nextSpeaker);
                //update nächster Sprecher in _data- object (FlowStateData)
                _data.ToBeRecorded = nextSpeaker;
                
            }
            else{
                if(selected > (_data.AllRoleCount -1) || selected < 0){
                    UnityEngine.Debug.LogError($"selected Speaker index is out of Range");
                    
                }else{
                    //update Rollen, die im Idle sind in _data- object (FlowStateData)
                    SpeakerStateEnterSetLists(selected);
                    //update nächster Sprecher in _data- object (FlowStateData)
                    _data.ToBeRecorded = selected;
                    //Damit die Frage, ob ein nächster Sprecher ausgesucht wurde, nächstes mal wieder funktioniert, zurücksetzen.
                    _data.SelectedNext = -1;
                    //UnityEngine.Debug.Log($"Next Selected Speaker has index: {selected}");
                    
                }
            }
        }


        public void SpeakerStateEnterSetLists(int nextSpeaker)
        {
            //playbacks leeren bevor es weiter geht.
            _data.Playbacks = new List<int>();

      
            
            List<int> reactiveIdles = new List<int>();
            if(nextSpeaker > (_data.AllRoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.AllRoleCount; i++)
                {
                    if(_data.AllRoles[i].roleIndex != nextSpeaker){
                        if (_data.AllRoles[i].isActiveConversationPartner)
                        {
                            reactiveIdles.Add(_data.AllRoles[i].roleIndex);
                            UnityEngine.Debug.Log($"[SpeakerStateEnterSetLists] Role with Index{_data.AllRoles[i].roleIndex} was added to reactiveIdles.");
                            
                        }
                        
                    }
                }
                _data.ReactiveIdles = reactiveIdles;
            }
        }


        public bool SpeakerStateExitAutoSelection(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                
                return false;
                
            }

            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[SpeakerStateExit] still in reactive Idles: {var}");
            }
            
            int nextListener;
            
            
            //nächsten Sprecher setzen, mit 0 weil AutoSelection.
            nextListener = _data.ReactiveIdles[0];

            _data.ReactiveIdles.RemoveAt(0);
            
           
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
  
            // im FlowStateData Objekt den aktuellen nächsten Aufzunehmenden setzen.
            _data.ToBeRecorded = nextListener;

                
            UnityEngine.Debug.Log($"[SpeakerStateExit] Next Active Listener has index: {nextListener}");
            
            return true;
        }

        public bool SpeakerStateExitAutoSelectionGoingToPlaybackPreRecordedScenesState(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                
                return false;
                
            }

            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[SpeakerStateExit] still in reactive Idles: {var}");
            }
            // Die ganze Funktion kann man eigentlich weglassen, weil sie nicht tut, ausser print
                
            
            return true;
        }
       

        public bool SpeakerStateExitManualSelection(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                return false;
            }

            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[SpeakerStateExit] still in reactive Idles: {var}");
            }
            
            _data.ReactiveIdles.Remove(_data.ToBeRecorded);
           
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);

  

            return true;
        }
        public bool ListenerStateExitAutoSelection(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.Log($"[ListenerStateExit] Exit: ReactiveIdle List is empty -> Switch to RecordSpeakerState: SceneCount is: {_data.SceneCount}");
                IncrementSceneCount();
                SpeakerStateEnter();
                return false;
            }
            int nextListener;
        

            
            //nächsten Sprecher setzen, entweder 0 weil AutoSelection
            nextListener = _data.ReactiveIdles[0];
            //den gewählten Sprecher aus der Liste der Idles entfernen. 
            _data.ReactiveIdles.RemoveAt(0);
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
            // im FlowStateData Objekt den aktuellen nächsten Aufzunehmenden setzen.
            _data.ToBeRecorded = nextListener;


            //UnityEngine.Debug.Log($"Next Active Listener has index: {nextListener}");
            
            return true;
        }

        public void RecordRemainingExitAutoSelection()
        {
            _data.SceneCountWhilePlaybackPreRecorded ++;

            
        }

        public void RecordRemainingNextToBeRecorded()
        {
            if(_data.ReactiveIdles.Count > 0)
            {
                _data.ToBeRecorded = _data.ReactiveIdles[0];
                _data.ReactiveIdles.RemoveAt(0);
            }
            else
            {
                UnityEngine.Debug.LogError("ReactiveIdles has no Elements, so ToBeRecorded can't get selected from there");
            }
            
            
            
        }
        

        

        public bool ListenerStateExitManualSelection(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.Log($"[ListenerStateExit] Exit: ReactiveIdle List is empty -> Switch to RecordSpeakerState: SceneCount is: {_data.SceneCount}");
                IncrementSceneCount();
                SpeakerStateEnter();
                return false;
            }

            
           
            //den gewählten Sprecher aus der Liste der Idles entfernen. 
            _data.ReactiveIdles.Remove(_data.ToBeRecorded);
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
     
            
            return true;
        }


        public bool IsPlayerNearNpc(int roleIndex, Transform player, float radius)
        {
            if(roleIndex < 0 || roleIndex > _data.AllRoleCount)
            {
                UnityEngine.Debug.LogError("[FlowController]: roleIndex is out of Range");
            }
            RoleRig npc = _data.AllRoles[roleIndex];
            float distance = Vector3.Distance(
                npc.root.position,
                player.position
            );

            return distance <= radius;
        }

//diese Funktion schaut, ob der Spieler in der Nähe von Figuren mit PreRecordedScenes ist, abhängig von der adjustFlowStateData 
//Variable, passt dann diese Funktion auch gleich die Daten im FlowStateData an.
        public bool PlayerNearNpcs(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius)
        {
            bool playbackPreRecordedScene = false; 

            if (roleIndicesOfPassiveRoles == null)
                return playbackPreRecordedScene;

            foreach (int passiveIndex in roleIndicesOfPassiveRoles)
            {
                if (!IsPlayerNearNpc(
                        passiveIndex,
                        _data.AllRoles[playerIndex].root,
                        radius))
                {
                    continue;
                }
                else
                {
                    playbackPreRecordedScene = true;
                    break;
                }

                
            }
            
            return playbackPreRecordedScene;
            
        }

/*
        public void AdjustFlowStateDataBeforeGoingToPlaybackPreRecordedScenes(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius)
        {
            List<int> passiveSnapshot =
                new List<int>(roleIndicesOfPassiveRoles);

            foreach (int passiveIndex in passiveSnapshot)
            {
                if (!IsPlayerNearNpc(
                        passiveIndex,
                        _data.AllRoles[playerIndex].root,
                        radius))
                {
                    continue;
                }

                RoleRig npc = _data.AllRoles[passiveIndex];
                string currentNpcGroupId = npc.npcGroupId;

                List<int> activatedIndices = new List<int>();
                
                for (int i = 0; i < _data.AllRoles.Count; i++)
                {
                    RoleRig role = _data.AllRoles[i];

                    if (role.npcGroupId != currentNpcGroupId)
                        continue;
                    UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]: npcGroupId: {role.npcGroupId} roleIndex: {role.roleIndex}, sourceRoleIndex: {role.sourceRoleIndex}");
                    
                    //die aktuellen PreRecorded werden erst den (aktiven Rollen) zugefügt, wenn für alle Reactive Idles Takes aufgenommen worden sind.
                    if(_data.ReactiveIdles.Count == 0)
                    {
                        _data.Roles.Add(role);
                    }
                    
                    _data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                    activatedIndices.Add(role.roleIndex);
                }

                foreach (int index in activatedIndices)
                {
                    _data.IndicesOfPassiveRoles.Remove(index);
                }
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] before update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                _data.CurrentPreRecordedPlaybacksCount = _data.ActiveRoleCount;
                _data.ActiveRoleCount += _data.CurrentPreRecordedPlaybacks.Count;
                 
                _data.SceneCountBeforePlaybackPreRecorded = _data.SceneCount;
                _data.SceneCount++;
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] after update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]Player came near NPC group: {currentNpcGroupId}");
            }
        }
        */

        public void RecordSpeakerToPlaybackPreRecorded_DataAdjustments(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius)
        {
            
            _currentNpcGroupId = "";

            foreach (int passiveIndex in roleIndicesOfPassiveRoles)
            {
                if (IsPlayerNearNpc(
                        passiveIndex,
                        _data.AllRoles[playerIndex].root,
                        radius))
                {
                    RoleRig npc = _data.AllRoles[passiveIndex];
                    _currentNpcGroupId = npc.npcGroupId;
                    break;
                }
            }
                
            if(_currentNpcGroupId != "")
            {
                foreach (int i in roleIndicesOfPassiveRoles)
                {
                    RoleRig role = _data.AllRoles[i];

                    if (role.npcGroupId != _currentNpcGroupId)
                    {
                       continue;
                    }
                    else
                    {
                        _data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                    }
                        
                    UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]: npcGroupId: {role.npcGroupId} roleIndex: {role.roleIndex}, sourceRoleIndex: {role.sourceRoleIndex}");
                    
                    //_data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                    
                }
                _data.TimesPreRecordedPlaybacksWerePlayed = _data.ActiveRoleCount;
                _data.SceneCountBeforePlaybackPreRecorded = _data.SceneCount;
                _data.CurrentNpcGroupId = _currentNpcGroupId;
            }
               
                
                

                //UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] before update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                
                
                 
                
                _data.SceneCount++;
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] after update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]Player came near NPC group: {_currentNpcGroupId}");
            }
        

        public void PlaybackPreRecordedToRecordRemaining_DataAdjustments()
        {
            //_data.SceneCount = _data.SceneCountBeforePlaybackPreRecorded;
            _data.Playbacks.Add(_data.ToBeRecorded);

            foreach (int i in _data.CurrentPreRecordedPlaybacks)
            {
                _data.Playbacks.Add(i);
            }
            if(_data.ReactiveIdles.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"[FlowController] No Reactive Idle found");
            }
            else
            {
                _data.ToBeRecorded = _data.ReactiveIdles[0];
                _data.ReactiveIdles.RemoveAt(0);
            }
            foreach (RoleRig role in _data.AllRoles)
            {
                if (!_data.Roles.Contains(role))
                {
                    _data.Roles.Add(role);
                    _data.IndicesOfPassiveRoles.Remove(role.roleIndex);
                }
            }
        }

        public void RecordRemainingToPlaybackPreRecorded_DataAdjustments()
        {
            _data.SceneCount ++;
        
        }
/*
        public bool PlayerNearNpcs(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius,
            )
        {
            bool playbackPreRecordedScene = false; 

            if (roleIndicesOfPassiveRoles == null)
                return playbackPreRecordedScene;

            List<int> passiveSnapshot =
                new List<int>(roleIndicesOfPassiveRoles);

            
            foreach (int passiveIndex in passiveSnapshot)
            {
                if (!IsPlayerNearNpc(
                        passiveIndex,
                        _data.AllRoles[playerIndex].root,
                        radius))
                {
                    continue;
                }

                playbackPreRecordedScene = true;
                RoleRig npc = _data.AllRoles[passiveIndex];
                string _currentNpcGroupId = npc.npcGroupId;

                List<int> activatedIndices = new List<int>();
                
                for (int i = 0; i < _data.AllRoles.Count; i++)
                {
                    RoleRig role = _data.AllRoles[i];

                    if (role.npcGroupId != _currentNpcGroupId)
                        continue;
                    UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]: npcGroupId: {role.npcGroupId} roleIndex: {role.roleIndex}, sourceRoleIndex: {role.sourceRoleIndex}");
                    _data.Roles.Add(role);
                    //_data.ReactiveIdles.Add(role.roleIndex);
                    _data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                    activatedIndices.Add(role.roleIndex);
                }

                foreach (int index in activatedIndices)
                {
                    _data.IndicesOfPassiveRoles.Remove(index);
                }
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] before update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                _data.ActiveRoleCount = _data.Roles.Count;
                _data.SceneCount++;
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] after update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]Player came near NPC group: {_currentNpcGroupId}");

                
                return playbackPreRecordedScene;
            }
            return playbackPreRecordedScene;
        }
        */
        

    }
}