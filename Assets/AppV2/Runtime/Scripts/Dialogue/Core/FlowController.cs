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
            //UnityEngine.Debug.Log($"[FlowController] roleCount allRoles is: {Stage.roleCount} | activeRoles: {_data.ActiveRoleCount}");
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
                /*
                UnityEngine.Debug.Log(
                    $"[NPC Position] [FLOW START BEGIN] {role.roleId}: " +
                    $"local={role.root.localPosition}, " +
                    $"world={role.root.position}"
                );
                */
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

            //PrintRoleLists("FlowController PreRecorded Debug | SetState BEGIN |", oldStateName, newStateName, _data.Playbacks,_data.ReactiveIdles,_data.SceneCount,_data.ToBeRecorded);
                   

            //PrintRoleListsAndFlowStateDataWithStateInfo("FlowController PreRecorded Debug | SetState BEGIN |", oldStateName, newStateName);
                   

            _state?.Exit();

            //PrintRoleLists("FlowController PreRecorded Debug | SetState AFTER EXIT |", oldStateName, newStateName, _data.Playbacks,_data.ReactiveIdles,_data.SceneCount,_data.ToBeRecorded);
            //PrintRoleListsAndFlowStateDataWithStateInfo("FlowController PreRecorded Debug | SetState AFTER EXIT |", oldStateName, newStateName);
                   

            _state = next;

            //PrintRoleLists("FlowController PreRecorded Debug | SetState BEFORE ENTER|", oldStateName, newStateName, _data.Playbacks,_data.ReactiveIdles,_data.SceneCount,_data.ToBeRecorded);
            //PrintRoleListsAndFlowStateDataWithStateInfo("FlowController PreRecorded Debug | SetState BEFORE ENTER|", oldStateName, newStateName);
            _state?.Enter();
            //PrintRoleLists("FlowController PreRecorded Debug | SetState AFTER ENTER|", oldStateName, newStateName, _data.Playbacks,_data.ReactiveIdles,_data.SceneCount,_data.ToBeRecorded);
            PrintRoleListsAndFlowStateDataWithStateInfo($"FlowController PreRecorded Debug | SetState AFTER ENTER| RoleCount is: {_data.Roles.Count}", oldStateName, newStateName);

        }

        //Funktionen, die von den States gerufen werden

        //Funktion, um aktive Gesprächspartner, wieder zu entfernen, damit die Anzahl aktiver Rollen nicht immer zunimmt.
        // die Idee ist, dass Rollen immer immer im Radius des Players sein müssen, um weiterhin aktiv sein zu können
        // die Entfernung wird jeweils am Ende vom RecordSpeakerState und am Ende vom RecordListenerState abgefragt und entsprechend im FlowStateData-Objekt eingetragen wird.

        public void RemoveActiveRolesTooFarAwayFromPlayer( int toBeRecorded, bool listenerState = true)
        {
            if(toBeRecorded <0 || toBeRecorded > _data.AllRoles.Count)
            {
                UnityEngine.Debug.LogError($"ToBeRecorded with Index: {toBeRecorded} is out of Range.");
                return;
            }

            float radius = Stage.distanceToLoseActiveConversationPartner;
            

            List<RoleRig> tooFarAwayRoles = ActiveRolesThatAreNotNearPlayer(toBeRecorded,radius);

            foreach(RoleRig r in tooFarAwayRoles)
            {
                r.isActiveConversationPartner = false;

                
                
                int index = r.roleIndex;
                _data.Roles.Remove(r);
                UnityEngine.Debug.Log($"[RecordListenerState] in Scene: {_data.SceneCount} [FlowController] Role with index: {r.roleIndex} was removed from _data.Roles | GoToPlaybackPreRecordedState: {_data.GoToPlaybackPreRecordedState}");
                if (!_data.IndicesOfPassiveRoles.Contains(index ))
                {
                    _data.IndicesOfPassiveRoles.Add(index );
                }
                
                
                _data.ReactiveIdles.Remove(index);
                if (listenerState)
                {
                    _data.Playbacks.Remove(index);
                }
                          
                
            }

            foreach (RoleRig role in _data.Roles)
            {
                UnityEngine.Debug.Log($"[RecordListenerState] in Scene: {_data.SceneCount} [FlowController] Role with index: {role.roleIndex} is in _data.Roles");
            }

            _data.ActiveRoleCount = _data.Roles.Count;
        }

         public void AddPassiveRolesToActiveRoles( int toBeRecorded)
        {
            if(toBeRecorded <0 || toBeRecorded > _data.AllRoles.Count)
            {
                UnityEngine.Debug.LogError($"ToBeRecorded with Index: {toBeRecorded} is out of Range.");
                return;
            }

            float radius = Stage.distanceToLoseActiveConversationPartner;
            

            List<RoleRig> newActiveRoles = PassiveRolesThatAreNearPlayer(toBeRecorded,radius);

            if(newActiveRoles.Count > 0)
            {
                 foreach(RoleRig r in newActiveRoles)
                {
                    r.isActiveConversationPartner = true;

                    
                    
                    int index = r.roleIndex;
                    _data.Roles.Add(r);
                    //UnityEngine.Debug.Log($"[RecordListenerState] in Scene: {_data.SceneCount} [FlowController] Role with index: {r.roleIndex} was removed from _data.Roles | GoToPlaybackPreRecordedState: {_data.GoToPlaybackPreRecordedState}");
                    if (_data.IndicesOfPassiveRoles.Contains(index ))
                    {
                        _data.IndicesOfPassiveRoles.Remove(index );
                    }
                    
                    
                    _data.ReactiveIdles.Add(index);
                    
                            
                    
                }

               

                _data.ActiveRoleCount = _data.Roles.Count;
                
            }
           
        }


        public void IncrementSceneCount()
        {
            _data.SceneCount++;
            //UnityEngine.Debug.Log($"SceneCount is now: {_data.SceneCount}");
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
            int nextSpeakerIndex;
            int selected = _data.SelectedNext;

            if(selected == -1){

                List<int> activeRolesIndices = new List<int>();

                foreach (RoleRig role in _data.Roles)
                {
                    activeRolesIndices.Add(role.roleIndex);
                }
                nextSpeakerIndex = _data.SceneCount  % _data.ActiveRoleCount;
                nextSpeaker = activeRolesIndices[nextSpeakerIndex];
                
                UnityEngine.Debug.Log($"[FlowController][ListenerStateExitAutoSelection][SpeakerStateEnter][_data.SceneCount  % _data.ActiveRoleCount] -> {_data.SceneCount % _data.ActiveRoleCount}  activeRoleCount is: {_data.ActiveRoleCount}");
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
            if(_data.Roles.Count == 1)
            {
                //UnityEngine.Debug.Log($"[FlowController] [SpeakerStateExitAutoSelection()] SceneCount={_data.SceneCount} still in reactive Idles: [" + string.Join(", ", _data.ReactiveIdles) + $"] toBeRecoreded={_data.ToBeRecorded}");
                _data.ToBeRecorded = _data.Roles[0].roleIndex;
                _data.GoToSpeakerState = true;
                SpeakerStateEnterSetLists(_data.ToBeRecorded);
                _data.SceneCount ++;
                return true;
                
            }
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                
                return false;
                
            }

/*
            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[SpeakerStateExit] still in reactive Idles: {var}");
            }*/
            
            int nextListener;
            
            
            //nächsten Sprecher setzen, mit 0 weil AutoSelection.
            nextListener = _data.ReactiveIdles[0];

            _data.ReactiveIdles.RemoveAt(0);
            
           
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
  
            // im FlowStateData Objekt den aktuellen nächsten Aufzunehmenden setzen.
            _data.ToBeRecorded = nextListener;

                
            //UnityEngine.Debug.Log($"[SpeakerStateExit] Next Active Listener has index: {nextListener}");
            
            return true;
        }


        public bool SpeakerStateExitManualSelection(){
            //das braucht man eigentlich nicht, weil manualSelection wird bei nur einer Rolle eigentlich nicht 
            // gebraucht. Aber sicher ist sicher.
            if(_data.Roles.Count == 1)
            {
                _data.GoToSpeakerState = true;
                _data.SceneCount ++;
                return true;
                
            }
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                return false;
            }
/*
            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[SpeakerStateExit] still in reactive Idles: {var}");
            }
 */           
            _data.ReactiveIdles.Remove(_data.ToBeRecorded);
           
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);

  

            return true;
        }
        public bool ListenerStateExitAutoSelection(){
            if (_data.ReactiveIdles.Count == 0){
                //UnityEngine.Debug.Log($"[ListenerStateExit] Exit: ReactiveIdle List is empty -> Switch to RecordSpeakerState: SceneCount is: {_data.SceneCount}");
                IncrementSceneCount();
                PrintRoleListsAndFlowStateData($"[FlowController][ListenerStateExitAutoSelection][beforeSpeakerStateEnter]", _data.SceneCount, _data.ToBeRecorded);
                SpeakerStateEnter();
                PrintRoleListsAndFlowStateData($"[FlowController][ListenerStateExitAutoSelection][afterSpeakerStateEnter]", _data.SceneCount, _data.ToBeRecorded);
                return false;
            }
            int nextListener;
        

            UnityEngine.Debug.Log($"[FlowController][ListenerStateExitAutoSelection][], _data.ReactiveIdles[0] / nextListener = {_data.ReactiveIdles[0]}, aktuellerToBeRecoreded {_data.ToBeRecorded}");
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
                //UnityEngine.Debug.Log($"[ListenerStateExit] Exit: ReactiveIdle List is empty -> Switch to RecordSpeakerState: SceneCount is: {_data.SceneCount}");
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
                if (_data.AllRoles[passiveIndex].hasPlayedBackPreRecordedTakes)
                {
                    continue;
                }

                if (!_data.AllRoles[passiveIndex].hasPreRecordedTakes)
                {
                    continue;
                }
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

        private List<RoleRig> ActiveRolesThatAreNotNearPlayer(
            int playerIndex,
            float radius)
        {
            List<RoleRig> rolesNotNearPlayer = new List<RoleRig>();

            RoleRig playerRole = _data.AllRoles[playerIndex];

            foreach (RoleRig role in _data.Roles)
            {
                int index = role.roleIndex;

                if (index == playerIndex)
                    continue;

                if (!IsPlayerNearNpc(
                        index,
                        playerRole.root,
                        radius))
                {
                    /*UnityEngine.Debug.Log(
                        $"[RecordListenerState] in Scene: {_data.SceneCount} " +
                        $"[FlowController] Role with index: {role.roleIndex} " +
                        $"is not near Role with Index: {playerIndex}");*/

                    rolesNotNearPlayer.Add(role);
                }
            }

            return rolesNotNearPlayer;
        }

        private List<RoleRig> PassiveRolesThatAreNearPlayer(
            int playerIndex,
            float radius)
        {
            List<RoleRig> rolesNearPlayer = new List<RoleRig>();

            RoleRig playerRole = _data.AllRoles[playerIndex];

            foreach (RoleRig role in _data.AllRoles)
            {
                int index = role.roleIndex;

                if(!_data.IndicesOfPassiveRoles.Contains(index))
                    continue;

                if(role.hasPreRecordedTakes == true && role.hasPlayedBackPreRecordedTakes == false)
                    continue;

                if (index == playerIndex)
                    continue;

                if(_data.Roles.Contains(role))
                    continue;

                if (IsPlayerNearNpc(
                        index,
                        playerRole.root,
                        radius))
                {
                    /*UnityEngine.Debug.Log(
                        $"[RecordListenerState] in Scene: {_data.SceneCount} " +
                        $"[FlowController] Role with index: {role.roleIndex} " +
                        $"is not near Role with Index: {playerIndex}");*/

                    rolesNearPlayer.Add(role);
                }
            }

            return rolesNearPlayer;
        }

        public string GetNpcGroupId(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius
            )
        {
            string npcGroupId = "";

            if (roleIndicesOfPassiveRoles == null)
                return npcGroupId;

            foreach (int passiveIndex in roleIndicesOfPassiveRoles)
            {
                if (!IsPlayerNearNpc(
                        passiveIndex,
                        _data.AllRoles[playerIndex].root,
                        radius))
                {
                    continue;
                }

                // NPC wurde in der Nähe gefunden
                RoleRig npc = _data.AllRoles[passiveIndex];

                npcGroupId = npc.npcGroupId;

                
            }

            return npcGroupId;
        }



        public void RecordSpeakerToPlaybackPreRecorded_DataAdjustments(
            List<int> roleIndicesOfPassiveRoles,
            int playerIndex,
            float radius,
            string npcGroupId)
        {
            
            _currentNpcGroupId = npcGroupId;

                
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
                        if (!_data.CurrentPreRecordedPlaybacks.Contains(role.roleIndex))
                        {
                            _data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                        }
                        
                    }
                        
                    //UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]: npcGroupId: {role.npcGroupId} roleIndex: {role.roleIndex}, sourceRoleIndex: {role.sourceRoleIndex}");
                    
                    //_data.CurrentPreRecordedPlaybacks.Add(role.roleIndex);
                    
                }
                _data.TimesPreRecordedPlaybacksWerePlayed = _data.ActiveRoleCount;
                _data.SceneCountBeforePlaybackPreRecorded = _data.SceneCount;
                _data.CurrentNpcGroupId = _currentNpcGroupId;
            }
               
                
                

                //UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] before update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                
                
                 
                
                _data.SceneCount++;
                //UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs] after update: ActiveRoleCount: {_data.ActiveRoleCount}, SceneCount: {_data.SceneCount}");
                //UnityEngine.Debug.Log($"[FlowController][PlayerNearNpcs]Player came near NPC group: {_currentNpcGroupId}");
            }

        public void SetPreRecordedToActiveConversationPartner(string npcGroupId)
        {
            if (string.IsNullOrWhiteSpace(npcGroupId))
                return;

            foreach (RoleRig role in _data.AllRoles)
            {
                if (role == null)
                    continue;

                if (role.npcGroupId == npcGroupId)
                {
                    role.isActiveConversationPartner = true;
                    role.hasPlayedBackPreRecordedTakes = true;
                }
            }
        }


        public void PlaybackPreRecordedToSpeaker_DataAdjustments()
        {
            // NPC-Gruppe aktivieren
            SetPreRecordedToActiveConversationPartner(
                _data.CurrentNpcGroupId
            );

            Stage.SwitchNpcGroupToCurrentSession(_data.CurrentNpcGroupId);
            //_data.FromPreRecordedToSpeaker = true;

            for (int i = 0; i < _data.AllRoles.Count; i++)
            {
                RoleRig role = _data.AllRoles[i];

                if (role == null)
                    continue;

                int roleIndex = role.roleIndex;

                // -----------------------------------
                // Nur aktive Gesprächspartner behandeln
                // -----------------------------------

                if (!role.isActiveConversationPartner)
                {
                    UnityEngine.Debug.Log($"[FlowController][ListenerStateExitAutoSelection][PlaybackPreRecordedToSpeaker_DataAdjustments] role with Index {i} is not active");
                    continue;
                }
                    


                // -----------------------------------
                // In aktive Roles übernehmen
                // -----------------------------------

                if (!_data.Roles.Contains(role))
                {
                    //role.hasPlayedBackPreRecordedTakes = true;
                    _data.Roles.Add(role);
                }


                // -----------------------------------
                // Nicht mehr passiv
                // -----------------------------------

                _data.IndicesOfPassiveRoles.Remove(roleIndex);
                UnityEngine.Debug.Log($"[FlowController][ListenerStateExitAutoSelection][PlaybackPreRecordedToSpeaker_DataAdjustments] role with Index {roleIndex} was removed from passive Roles");


                // -----------------------------------
                // ReactiveIdle:
                // alle aktiven Rollen außer der,
                // die gerade aufgenommen werden soll
                // -----------------------------------

                if (roleIndex != _data.ToBeRecorded)
                {
                    if (!_data.ReactiveIdles.Contains(roleIndex))
                    {
                        _data.ReactiveIdles.Add(roleIndex);
                    }
                }
                else
                {
                    // Sicherheitshalber entfernen,
                    // falls der Index vorher schon drin war.
                    _data.ReactiveIdles.Remove(roleIndex);
                }
            }

            _data.ActiveRoleCount = _data.Roles.Count;

            // Beim Wechsel zum Speaker gibt es zunächst
            // keine laufenden Playbacks.
            _data.CurrentNpcGroupId = "";
            _data.Playbacks.Clear();
            _data.CurrentPreRecordedPlaybacks.Clear();
           
        }

        public void RecordRemainingToSpeaker_DataAdjustments()
        {
            // NPC-Gruppe aktivieren
            SetPreRecordedToActiveConversationPartner(
                _data.CurrentNpcGroupId
            );
            

            for (int i = 0; i < _data.AllRoles.Count; i++)
            {
                RoleRig role = _data.AllRoles[i];

                if (role == null)
                    continue;

                int roleIndex = role.roleIndex;



                if (!role.isActiveConversationPartner)
                    continue;


                if (!_data.Roles.Contains(role))
                {
                    _data.Roles.Add(role);
                }

                _data.IndicesOfPassiveRoles.Remove(roleIndex);


                if (roleIndex != _data.ToBeRecorded)
                {
                    if (!_data.ReactiveIdles.Contains(roleIndex))
                    {
                        _data.ReactiveIdles.Add(roleIndex);
                    }
                }
                else
                {
                    _data.ReactiveIdles.Remove(roleIndex);
                }
            }

            _data.ActiveRoleCount = _data.Roles.Count;
            _data.CurrentNpcGroupId = "";
            _data.Playbacks.Clear();
            _data.CurrentPreRecordedPlaybacks.Clear();

        }

        public void PlaybackPreRecordedToRecordRemaining_DataAdjustments()
        {
            //_data.SceneCount = _data.SceneCountBeforePlaybackPreRecorded;
            _data.Playbacks.Add(_data.ToBeRecorded);

            Stage.SwitchNpcGroupToCurrentSession(_data.CurrentNpcGroupId);

            foreach (int i in _data.CurrentPreRecordedPlaybacks)
            {
                if (!_data.Playbacks.Contains(i))
                {
                    _data.Playbacks.Add(i);
                }
                
                _data.AllRoles[i].hasPlayedBackPreRecordedTakes = true;
                _data.AllRoles[i].isActiveConversationPartner = true;
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
                if (!_data.Roles.Contains(role) && role.hasPlayedBackPreRecordedTakes == true)
                {   UnityEngine.Debug.Log($"FlowController PreRecorded Debug | role with index {role.roleIndex} was removed from passive Roles.");
                    _data.Roles.Add(role);
                    _data.IndicesOfPassiveRoles.Remove(role.roleIndex);
                }
            }

            _data.CurrentNpcGroupId = "";
            _data.CurrentPreRecordedPlaybacks.Clear();
        }


        public void RecordRemainingToRecordRemaining_DataAdjustments()
        {
            if (!_data.Playbacks.Contains(_data.ToBeRecorded))
                {  
                    _data.Playbacks.Add(_data.ToBeRecorded);
                    
                }
            if(_data.ReactiveIdles.Count == 0)
            {
                UnityEngine.Debug.LogError($"[RecordRemainingToRecordRemaining] _data.ReactiveIdles.Count == {_data.ReactiveIdles.Count} Can not Choose next ToBeRecorded");
            }
            else
            {
                _data.ToBeRecorded = _data.ReactiveIdles[0];
                _data.ReactiveIdles.RemoveAt(0);
            }
        
        }

        private void PrintRoleLists(
            string text, 
            string oldStateName,
            string newStateName,
            List<int> playbacks,
            List<int> reactiveIdles,
            int sceneCount,
            int toBeRecorded)
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
                $"{oldStateName} -> {newStateName} | frame={Time.frameCount}| " +
                $"[RoleLists] " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"sceneCount={sceneCount} | " +
                $"toBeRecorded={toBeRecorded}"
            );
        }

        public void PrintRoleListsAndFlowStateDataWithStateInfo(
            string text, 
            string oldStateName,
            string newStateName
            )
        {
            string currentPreRecordedString =
                _data.CurrentPreRecordedPlaybacks == null ||
                _data.CurrentPreRecordedPlaybacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.CurrentPreRecordedPlaybacks) + "]";

            string passiveRolesString =
                _data.IndicesOfPassiveRoles == null ||
                _data.IndicesOfPassiveRoles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.IndicesOfPassiveRoles) + "]";

            string playbacksString =
                _data.Playbacks == null ||
                _data.Playbacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.Playbacks) + "]";

            string reactiveIdlesString =
                _data.ReactiveIdles == null ||
                _data.ReactiveIdles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.ReactiveIdles) + "]";

            string currentPreRecordedPlaybackString =
                _data.CurrentPreRecordedPlaybacks== null ||
                _data.CurrentPreRecordedPlaybacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.CurrentPreRecordedPlaybacks) + "]";


            // Role overview
            string indicesOfActiveConversationPartners = "";
            string indicesOfNotActivatedRoles = "";

            foreach (RoleRig role in _data.AllRoles)
            {
                string roleInfo = $"{role.roleIndex} ({role.roleId})";

                if (role.isActiveConversationPartner)
                {
                    if (indicesOfActiveConversationPartners.Length > 0)
                        indicesOfActiveConversationPartners += ", ";

                    indicesOfActiveConversationPartners += roleInfo;
                }
                else
                {
                    if (indicesOfNotActivatedRoles.Length > 0)
                        indicesOfNotActivatedRoles += ", ";

                    indicesOfNotActivatedRoles += roleInfo;
                }
            }

            // Make empty lists visible
            indicesOfActiveConversationPartners =
                $"[{indicesOfActiveConversationPartners}]";

            indicesOfNotActivatedRoles =
                $"[{indicesOfNotActivatedRoles}]";


            UnityEngine.Debug.Log(
                $"[{text}] " +
                $"{oldStateName} -> {newStateName} | frame={Time.frameCount}| " +
                $"[RoleLists] " +
                $"currentPreRecorded={currentPreRecordedString} | " +
                $"passiveRoles={passiveRolesString}\ns | " +
                $"NOT activated Conversation Partners={indicesOfNotActivatedRoles} | " +
                $"Activated Conversation Partners={indicesOfActiveConversationPartners}  | " +
                $"activeRoleCount={_data.ActiveRoleCount} | " +
                $"currentNpcIndices={currentPreRecordedPlaybackString} | " +
                $"currentNpcGroupId={_data.CurrentNpcGroupId} | " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | \n" +
                
                $"SceneCount FlowData={_data.SceneCount} | " +
                $"SceneCountWhilePlaybackPreRecorded={_data.SceneCountWhilePlaybackPreRecorded} | " +
                $"SceneCountBeforePlaybackPreRecorded={_data.SceneCountBeforePlaybackPreRecorded} | " +
                $"GoToSpeakerState={_data.GoToSpeakerState} | " +
                $"GoToRecordRemainingState={_data.GoToRecordRemainingState} | " +
                $"GoToPlaybackPreRecordedState={_data.GoToPlaybackPreRecordedState} | " +
                $"toBeRecorded={_data.ToBeRecorded}"
            );
        }


        public void PrintRoleListsAndFlowStateData(
            string text, 
            int sceneCount,
            int toBeRecorded)
        {
            string currentPreRecordedString =
                _data.CurrentPreRecordedPlaybacks == null ||
                _data.CurrentPreRecordedPlaybacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.CurrentPreRecordedPlaybacks) + "]";

            string passiveRolesString =
                _data.IndicesOfPassiveRoles == null ||
                _data.IndicesOfPassiveRoles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.IndicesOfPassiveRoles) + "]";

            string playbacksString =
                _data.Playbacks == null ||
                _data.Playbacks.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.Playbacks) + "]";

            string reactiveIdlesString =
                _data.ReactiveIdles == null ||
                _data.ReactiveIdles.Count == 0
                    ? "[]"
                    : "[" + string.Join(", ", _data.ReactiveIdles) + "]";


            // Role overview
            string indicesOfActiveConversationPartners = "";
            string indicesOfNotActivatedRoles = "";

            foreach (RoleRig role in _data.AllRoles)
            {
                string roleInfo = $"{role.roleIndex} ({role.roleId})";

                if (role.isActiveConversationPartner)
                {
                    if (indicesOfActiveConversationPartners.Length > 0)
                        indicesOfActiveConversationPartners += ", ";

                    indicesOfActiveConversationPartners += roleInfo;
                }
                else
                {
                    if (indicesOfNotActivatedRoles.Length > 0)
                        indicesOfNotActivatedRoles += ", ";

                    indicesOfNotActivatedRoles += roleInfo;
                }
            }

            // Make empty lists visible
            indicesOfActiveConversationPartners =
                $"[{indicesOfActiveConversationPartners}]";

            indicesOfNotActivatedRoles =
                $"[{indicesOfNotActivatedRoles}]";


            UnityEngine.Debug.Log(
                $"[{text}] " +
                $"[RoleLists] " +
                $"currentPreRecorded={currentPreRecordedString} | " +
                $"passiveRoles={passiveRolesString} | " +
                $"NOT activated Conversation Partners={indicesOfNotActivatedRoles} | " +
                $"Activated Conversation Partners={indicesOfActiveConversationPartners} | " +
                $"currentNpcGroupId={_data.CurrentNpcGroupId} | " +
                $"playbacks={playbacksString} | " +
                $"reactiveIdles={reactiveIdlesString} | " +
                $"sceneCount local={sceneCount} | " +
                $"SceneCount FlowData={_data.SceneCount} | " +
                $"SceneCountWhilePlaybackPreRecorded={_data.SceneCountWhilePlaybackPreRecorded} | " +
                $"SceneCountBeforePlaybackPreRecorded={_data.SceneCountBeforePlaybackPreRecorded} | " +
                $"GoToSpeakerState={_data.GoToSpeakerState} | " +
                $"GoToRecordRemainingState={_data.GoToRecordRemainingState} | " +
                $"GoToPlaybackPreRecordedState={_data.GoToPlaybackPreRecordedState} | " +
                $"toBeRecorded={toBeRecorded}"
            );
        }

    }
}