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

        private void Awake()
        {
            _data = new FlowStateData();
            _data.Initialize(Stage.roleCount);
            _startInPlaybackFullConversationMode = Stage.StartInPlaybackFullConversationMode;
            _xrMode = Stage.UseXR;
        }

        private void OnDestroy()
        {

        }

        private void Start()
        {
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
            _state?.Exit();
            _state = next;
            UnityEngine.Debug.Log($"[Flow] State -> {_state.Mode}");
            _state.Enter();
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
                nextSpeaker = _data.SceneCount  % _data.RoleCount;
                //UnityEngine.Debug.Log($"Next Default Speaker has index: {nextSpeaker}");
                //update Rollen, die im Idle sind in _data- object (FlowStateData)
                RecSpeakStateSetReactiveIdles(nextSpeaker);
                //update nächster Sprecher in _data- object (FlowStateData)
                _data.ToBeRecorded = nextSpeaker;
                return nextSpeaker;
            }
            else{
                if(selected > (_data.RoleCount -1) || selected < 0){
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
            if(nextSpeaker > (_data.RoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.RoleCount; i++)
                {
                    if(i != nextSpeaker){
                        reactiveIdles.Add(i);
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
            if(nextSpeaker > (_data.RoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.RoleCount; i++)
                {
                    if(i != nextSpeaker){
                        reactiveIdles.Add(i);
                    }
                }
                _data.ReactiveIdles = reactiveIdles;
            }
        }

        public void SpeakerStateEnter(){
            int nextSpeaker;
            int selected = _data.SelectedNext;

            if(selected == -1){
                nextSpeaker = _data.SceneCount  % _data.RoleCount;
                //UnityEngine.Debug.Log($"Next Default Speaker has index: {nextSpeaker}");
                //update Rollen, die im Idle sind in _data- object (FlowStateData)
                SpeakerStateEnterSetLists(nextSpeaker);
                //update nächster Sprecher in _data- object (FlowStateData)
                _data.ToBeRecorded = nextSpeaker;
                
            }
            else{
                if(selected > (_data.RoleCount -1) || selected < 0){
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
            if(nextSpeaker > (_data.RoleCount -1) || nextSpeaker < 0)
            {
                    UnityEngine.Debug.LogError($"nextSpeaker index is out of Range. Index is: {nextSpeaker}");
                    
            }else
            {
                for(int i = 0; i < _data.RoleCount; i++)
                {
                    if(i != nextSpeaker){
                        reactiveIdles.Add(i);
                    }
                }
                _data.ReactiveIdles = reactiveIdles;
            }
        }

        public bool SpeakerStateExit(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.LogError($"[RecordSpeakerState] Exit: ReactiveIdle List is empty");
                
                return false;
                
            }

            foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"still in reactive Idles: {var}");
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
            if(selected == -1)
            {
                _data.ReactiveIdles.RemoveAt(0);
            }else
            {
                _data.ReactiveIdles.Remove(reactiveIdlesIndex);
            }
            //den den Aktiven Sprecher aus der Vorrunde zu den Playbacks hinzufügen.
            _data.Playbacks.Add(_data.ToBeRecorded);
  
            // im FlowStateData Objekt den aktuellen nächsten Aufzunehmenden setzen.
            _data.ToBeRecorded = nextListener;

                
            UnityEngine.Debug.Log($"Next Active Listener has index: {nextListener}");
            
            return true;
        }

        public bool ListenerStateExit(){
            if (_data.ReactiveIdles.Count == 0){
                UnityEngine.Debug.Log($"[RecordListenersState] Exit: ReactiveIdle List is empty -> Switch to RecordSpeakerState: SceneCount is: {_data.SceneCount}");
                IncrementSceneCount();
                SpeakerStateEnter();
                UnityEngine.Debug.Log($"[ListenerStateExit] Reactive Idles Count is: {_data.ReactiveIdles.Count}");
                foreach (int var in _data.ReactiveIdles){
                UnityEngine.Debug.Log($"[ListenerStateExit] still in reactive Idles: {var}");
            }
                return false;
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
            
            return true;
        }


    }
}