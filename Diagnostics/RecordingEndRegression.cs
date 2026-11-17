#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Dialogue.UI;

[InitializeOnLoad]
public static class RecordingEndRegression
{
    static RecordingEndRegression() { EditorApplication.delayCall+=Run; }
    static void Check(bool v,string name){if(!v)throw new Exception(name);}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static SessionModel Load(string path)=>JsonUtility.FromJson<SessionModel>(File.ReadAllText(Path.Combine(path,"session.json")));
    static void ExpectFailure(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
    static SessionModel Fixture(string path,string id,params string[] ids)
    {
        Directory.CreateDirectory(path);
        var s=new SessionModel{SessionId=id};
        foreach(var take in ids)
        {
            int round=int.Parse(take.Substring(5,4));
            var meta=new TakeMeta{TakeId=take,SceneCount=round,RoleId=take.Substring(10),FramesFile=take+".frames.jsonl",AudioFile=take+".wav"};
            s.Takes.Add(meta);File.WriteAllText(Path.Combine(path,meta.FramesFile),"frames-"+take);File.WriteAllText(Path.Combine(path,meta.AudioFile),"audio-"+take);
        }
        File.WriteAllText(Path.Combine(path,"session.json"),JsonUtility.ToJson(s));return s;
    }
    static void Run()
    {
        if(!File.Exists("Temp/RecordingEndRegression.request")||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete("Temp/RecordingEndRegression.request");GameObject root=null;
        try
        {
            string storage=Path.GetFullPath("Temp/RecordingEndRegressionData-"+Guid.NewGuid().ToString("N"));
            string source=Path.Combine(storage,"Workshop","Sessions","recording");
            Fixture(source,"recording","take_0000_A","take_0000_B","take_0001_B");
            Directory.CreateDirectory(Path.Combine(storage,"OtherWorkshop","Target"));
            var editor=new RecordingSessionEditor(storage,"Workshop","Sessions","recording");
            Check(editor.RemoveLastTakeRound()==1,"A0 B0 B1 removes B1 only");
            Check(Load(source).Takes.Count==2&&File.Exists(Path.Combine(source,"take_0000_A.frames.jsonl"))&&File.Exists(Path.Combine(source,"take_0000_B.wav"))&&!File.Exists(Path.Combine(source,"take_0001_B.wav"))&&!File.Exists(Path.Combine(source,"take_0001_B.frames.jsonl")),"JSON and both media files agree");
            Fixture(source,"recording","take_0000_A","take_0000_B","take_0001_A","take_0001_B");
            Check(editor.RemoveLastTakeRound()==2&&Load(source).Takes.Count==2,"Highest round removed for all participating roles");
            Check(editor.RemoveLastTakeRound()==2&&!editor.HasTakes&&editor.RemoveLastTakeRound()==0,"Repeated remove stops at empty session");
            Fixture(source,"recording","take_0000_A","take_0000_B");
            File.WriteAllText(Path.Combine(source,"notes.txt"),"extra-data");
            string originalJson=File.ReadAllText(Path.Combine(source,"session.json"));
            Check(editor.SaveValidation("OtherWorkshop","Target","")!=null,"Empty name disabled");
            Check(editor.SaveValidation("OtherWorkshop","Target","../outside")!=null&&!RecordingSessionEditor.ValidName("CON"),"Invalid and reserved names rejected");
            editor.SaveAs("OtherWorkshop","Target","new-name");
            string copy=editor.SessionPath;
            Check(Load(copy).SessionId=="new-name"&&Load(copy).Takes.Count==2&&File.ReadAllText(Path.Combine(copy,"notes.txt"))=="extra-data","Save as copies entire session with updated ID");
            Check(File.ReadAllText(Path.Combine(source,"session.json"))==originalJson,"Save as preserves source session");
            Check(editor.SaveValidation("OtherWorkshop","Target","NEW-NAME")!=null,"Case-insensitive collision rejected");
            ExpectFailure(()=>editor.SaveAs("OtherWorkshop","Target","new-name"),"Collision cannot overwrite existing session");
            ExpectFailure(()=>new RecordingSessionEditor(storage,"Workshop","Sessions",".."),"Source traversal rejected");
            editor.Delete();Check(!Directory.Exists(copy)&&Directory.Exists(source),"Delete affects only selected session");
            var sourceEditor=new RecordingSessionEditor(storage,"Workshop","Sessions","recording");
            var scenePanel=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="RecordingEndPanel");
            root=new GameObject("Recording end regression");root.SetActive(false);
            var panelObject=UnityEngine.Object.Instantiate(scenePanel.gameObject,root.transform);var panel=panelObject.GetComponent<RecordingEndPanelUI>();if(panel==null)panel=panelObject.AddComponent<RecordingEndPanelUI>();
            panel.Display(sourceEditor);
            var input=Field<TMP_InputField>(panel,"_name");var save=Field<Button>(panel,"_save");var warning=Field<TMP_Text>(panel,"_warning");
            Check(!save.interactable,"Save button initially disabled");
            input.text="recording";Check(!save.interactable&&warning.text.Contains("bereits")&&warning.color==Color.red,"Duplicate name gives red warning");
            input.text="valid-copy";Check(save.interactable&&warning.text=="","Unique name enables save");
            input.text="";Check(!save.interactable,"Clearing name disables save");
            Check(Field<TMP_Dropdown>(panel,"_workshop").options.Count==2,"Workshop folders populated from storage");
            Field<TMP_Dropdown>(panel,"_workshop").value=Field<TMP_Dropdown>(panel,"_workshop").options.FindIndex(o=>o.text=="OtherWorkshop");
            Check(Field<TMP_Dropdown>(panel,"_folder").options[0].text=="Target","Session folders follow selected workshop");
            Field<Button>(panel,"_remove").onClick.Invoke();Check(!Field<Button>(panel,"_remove").interactable&&Load(source).Takes.Count==0,"UI remove button edits files and disables at empty");
            Field<Button>(panel,"_delete").onClick.Invoke();Check(!Directory.Exists(source)&&!save.interactable&&!Field<Button>(panel,"_delete").interactable,"UI delete disables completed recording actions");
            Check(Directory.Exists(Path.Combine(storage,"OtherWorkshop","Target")),"Parent and unrelated folders retained");
            string finishPath=Path.Combine(storage,"Workshop","Sessions","finish");Fixture(finishPath,"finish","take_0000_A");
            var menu=new GameObject("Isolated end menu");menu.SetActive(false);menu.transform.SetParent(root.transform);
            panelObject.transform.SetParent(menu.transform);panelObject.name="RecordingEndPanel";
            var nav=menu.AddComponent<MenuNavigation>();var basePage=new GameObject("RecordBase");basePage.transform.SetParent(menu.transform);nav.RegisterPage("RecordBase",basePage);
            var configuration=root.AddComponent<WorkshopConfigurationUI>();
            typeof(WorkshopConfigurationUI).GetField("menuRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(configuration,menu);
            typeof(WorkshopConfigurationUI).GetField("startWorkshopButton",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(configuration,new GameObject("Test start",typeof(RectTransform),typeof(Button)).GetComponent<Button>());
            Field<Button>(configuration,"startWorkshopButton").transform.SetParent(root.transform);
            configuration.ShowRecordingEnd(new RecordingSessionEditor(storage,"Workshop","Sessions","finish"));
            Check(nav.CurrentPageId=="RecordingEndPanel"&&panelObject.activeSelf&&!basePage.activeSelf,"Completed recording menu registers and opens only the end panel");
            File.WriteAllText("Temp/RecordingEndRegression.txt","PASS: uneven takes A0/B0/B1; multi-role round deletion; media+JSON consistency; empty sessions; save-as full copy and SessionId; original retained; collision/path validation; session-only deletion; actual panel controls; empty/duplicate/valid name gating; red warning; dependent folder dropdowns; completed recording panel registration and navigation.\n");
        }
        catch(Exception ex){File.WriteAllText("Temp/RecordingEndRegression.txt","FAIL: "+ex);}
        finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
    }
}
#endif
