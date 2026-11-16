#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using AppV2.Runtime.Scripts.Dialogue;
using UnityEditor;
using UnityEngine;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
[InitializeOnLoad]
public static class CalibrationProfilesRegression
{
    static CalibrationProfilesRegression()
    {
        EditorApplication.playModeStateChanged += s => {
            if(s == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Run;
        };
    }
    static void Set(UnityEngine.Object component,string key,UnityEngine.Object value)
    {var s=new SerializedObject(component);s.FindProperty(key).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static float Head(RoleRig role,TakeMeta take,float trackingHeight)
    {
        RoleCalibrationProfiles.ApplyForTake(role,take);
        role.visualRigRoot.Find("head").SetLocalPositionAndRotation(new Vector3(0,trackingHeight*0.0092f,0),Quaternion.identity);
        role.rigFollower.ApplyFollow(false);
        return RoleCalibrationProfiles.Find(role.avatarRoot,"headTarget").position.y;
    }
    static void Run()
    {
        if(!EditorApplication.isPlaying || !File.Exists("Temp/CalibrationProfilesRegression.request"))return;
        File.Delete("Temp/CalibrationProfilesRegression.request");
        var report=new StringBuilder("PlayMode="+Application.isPlaying+"\n");
        foreach(string model in new[]{"Bart@T-Pose","Ritter_Ohne_Schwert@T-Pose","SchneeMann_001"})
        {
            GameObject avatar=null,visual=null,control=null;
            try
            {
                avatar=new GameObject("Profile regression avatar");avatar.SetActive(false);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayableCharacters/"+model+".prefab");
                UnityEngine.Object.Instantiate(prefab,avatar.transform);
                var definition=avatar.AddComponent<AvatarRigDefinition>();definition.RefreshReferences();
                visual=new GameObject("Profile regression visual");
                foreach(string name in new[]{"head","leftHand","rightHand","hip","leftFoot","rightFoot"})
                {
                    var driver=new GameObject(name).transform;driver.SetParent(visual.transform,false);
                    var child=new GameObject(name+"Target").transform;child.SetParent(driver,false);
                }
                var follower=avatar.AddComponent<AvatarRigFollower>();Set(follower,"visualRoot",visual.transform);Set(follower,"avatarRoot",avatar.transform);
                avatar.SetActive(true);definition.RefreshReferences();
                var role=new RoleRig{roleId=model,roleIndex=0,heightOfRoleCm=180,avatar=definition,
                    avatarRoot=avatar.transform,visualRigRoot=visual.transform,rigFollower=follower};
                var roles=new List<RoleRig>{role};
                control=new GameObject("Profile regression controller");var controller=control.AddComponent<AvatarCalibrationController>();
                controller.Initialize(roles,true);
                avatar.transform.localScale=Vector3.one*0.9f;
                controller.PrepareForNewCalibration();
                Check(controller.CalibrateAutomatically(180f,0.9f,0.2f,false),"source calibration");
                var source=RoleCalibrationProfiles.Copy(role.liveCalibrationProfile);
                float expected=Head(role,new TakeMeta{CalibrationProfile=source},180f);
                role.hasPreRecordedTakes=true;role.preRecordedCalibration=source.Calibration;
                role.preRecordedCalibrationProfile=RoleCalibrationProfiles.Copy(source);
                visual.transform.localScale=Vector3.one*(180f/130f);
                avatar.transform.localScale=Vector3.one*0.9f;
                controller.PrepareForNewCalibration();
                Check(controller.CalibrateAutomatically(130f,0.65f,0.2f,false),"live imported-role calibration");
                Check(Mathf.Abs(role.liveCalibrationProfile.VisualScale.y-180f/130f)<0.0001f,"live reference scale");
                Check(Mathf.Abs(role.preRecordedCalibrationProfile.VisualScale.y-1f)<0.0001f,"source scale preserved");
                var oldTake=new TakeMeta{usesPreRecordedCalibration=true,CalibrationProfile=RoleCalibrationProfiles.Copy(role.preRecordedCalibrationProfile)};
                var newTake=new TakeMeta{usesPreRecordedCalibration=false,CalibrationProfile=RoleCalibrationProfiles.Copy(role.liveCalibrationProfile)};
                var provider=new RoleCalibrationDataProvider();provider.Initialize(roles);
                var session=new SessionModel{Roles=provider.CreateRoleMetas(),Takes=new List<TakeMeta>{oldTake,newTake}};
                string json=JsonUtility.ToJson(session,true);
                var loaded=JsonUtility.FromJson<SessionModel>(json);
                Check(loaded.Roles[0].LiveCalibration!=null && loaded.Roles[0].PreRecordedCalibration!=null,"both profiles in JSON");
                new RoleCalibrationDataApplier().ApplyRoleMetasToScene(roles,loaded);
                float oldHead=Head(role,loaded.Takes[0],180f);
                float newHead=Head(role,loaded.Takes[1],130f);
                float oldAgain=Head(role,loaded.Takes[0],180f);
                var stageObject=new GameObject("Profile regression inactive stage");stageObject.SetActive(false);
                try
                {
                    var stage=stageObject.AddComponent<ConversationStage>();stage.roles=roles;
                    stage._session=loaded;stage._store=new SessionStore();
                    typeof(ConversationStage).GetField("_takeIndex",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(stage,new SessionTakeIndex());
                    stage.SwitchRoleToCurrentSession(0);
                    Check(ReferenceEquals(role.activeCalibrationProfile,role.liveCalibrationProfile),"actual session switch selects live profile");
                }
                finally{UnityEngine.Object.DestroyImmediate(stageObject);}
                var solver=control.AddComponent<ProceduralHipSolver>();Set(solver,"bodyRoot",visual.transform);
                role.hipSolver=solver;
                role.liveCalibrationProfile.UsesProceduralLowerBody=true;
                role.liveCalibrationProfile.HipReferenceHeightM=0.65f;
                RoleCalibrationProfiles.ApplyForTake(role,oldTake);
                RoleCalibrationProfiles.ApplyLive(role);
                Check(Mathf.Abs(solver.transform.position.y-0.65f)<0.0001f,"live switch snaps procedural hip reference");
                role.hipSolver=null;
                Check(Mathf.Abs(oldHead-expected)<0.002f && Mathf.Abs(newHead-expected)<0.002f && Mathf.Abs(oldAgain-expected)<0.002f,"mixed take head height");
                var legacyOld=new TakeMeta{usesPreRecordedCalibration=true};
                Check(ReferenceEquals(RoleCalibrationProfiles.ForTake(role,legacyOld),role.preRecordedCalibrationProfile),"legacy imported flag");
                Check(ReferenceEquals(RoleCalibrationProfiles.ForTake(role,new TakeMeta()),role.liveCalibrationProfile),"legacy live flag");
                var scaled=RoleCalibrationProfiles.ForRoleHeight(source,130);
                float scaledHead=Head(role,new TakeMeta{CalibrationProfile=scaled},180f);
                Check(Mathf.Abs(scaledHead-expected*130f/180f)<0.002f,"NPC height override scales both references");
                Check(Mathf.Abs(source.VisualScale.y-1f)<0.0001f,"source snapshot immutable");
                // Legacy source metadata: preserve imported offsets and create a fresh live set.
                role.preRecordedCalibrationProfile=null;
                visual.transform.localScale=Vector3.one*(180f/130f);avatar.transform.localScale=Vector3.one*0.9f;
                controller.PrepareForNewCalibration();Check(controller.CalibrateAutomatically(130f,0.65f,0.2f,false),"legacy source migration");
                Check(role.preRecordedCalibrationProfile!=null && role.liveCalibrationProfile!=null,"legacy dual fallback");
                role.usePreRecordedCalibrationData=false;
                Check(ReferenceEquals(RoleCalibrationProfiles.ForTake(role,oldTake),role.liveCalibrationProfile),"source calibration disabled");
                report.AppendLine("PASS "+model+" source180="+oldHead.ToString("F4")+" live130="+newHead.ToString("F4")+
                    " oldAgain="+oldAgain.ToString("F4")+" scaledNPC130="+scaledHead.ToString("F4")+" JSON/legacy/flag/immutability OK");
                File.WriteAllText("Temp/CalibrationProfiles-"+model.Replace('@','_')+".json",json);
            }
            catch(Exception ex){report.AppendLine("FAIL "+model+" "+ex);}
            finally{if(avatar!=null)UnityEngine.Object.DestroyImmediate(avatar);if(visual!=null)UnityEngine.Object.DestroyImmediate(visual);if(control!=null)UnityEngine.Object.DestroyImmediate(control);}
        }
        File.WriteAllText("Temp/CalibrationProfilesRegression.txt",report.ToString());Debug.Log("Calibration profiles regression completed");
    }
}
#endif
