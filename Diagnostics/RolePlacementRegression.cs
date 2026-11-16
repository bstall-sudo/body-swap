#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.Dialogue.UI;
using AppV2.Runtime.Scripts.Dialogue.States;
using AppV2.Runtime.Scripts.Dialogue.Services;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Rig;
[InitializeOnLoad]
public static class RolePlacementRegression
{
    static RolePlacementRegression()
    { EditorApplication.playModeStateChanged += s => {if(s==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=Run;}; }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Near(Vector3 a,Vector3 b,string message){Check(Vector3.Distance(a,b)<0.002f,message+" actual="+a+" expected="+b);}
    static void Set(object o,string name,object value)
    {o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(o,value);}
    static void Run()
    {
        if(!EditorApplication.isPlaying||!File.Exists("Temp/RolePlacementRegression.request"))return;
        File.Delete("Temp/RolePlacementRegression.request");
        var report=new StringBuilder("PlayMode="+Application.isPlaying+"\n");
        GameObject stageObject=null,env=null,rowObject=null;
        try
        {
            stageObject=new GameObject("Placement regression stage");stageObject.SetActive(false);
            stageObject.transform.SetPositionAndRotation(new Vector3(20,3,-10),Quaternion.Euler(0,37,0));
            var stage=stageObject.AddComponent<ConversationStage>();stage._stageRoot=stageObject.transform;
            env=new GameObject("Placement regression environment");env.SetActive(false);
            var points=new Dictionary<string,Transform>();
            foreach(var id in new[]{"main","remote","single","look"})
            {
                var p=new GameObject(id).transform;p.SetParent(env.transform,false);
                Vector3 local=id=="main"?Vector3.zero:id=="remote"?new Vector3(10,0,5):id=="single"?new Vector3(-7,0,8):new Vector3(10,0,20);
                p.SetPositionAndRotation(stageObject.transform.TransformPoint(local),stageObject.transform.rotation*Quaternion.Euler(0,id=="remote"?90:0,0));
                var spawn=p.gameObject.AddComponent<StageSpawnPoint>();spawn.spawnId=id;points[id]=p;
            }
            var loader=env.AddComponent<EnvironmentLoader>();Set(loader,"_currentEnvironment",env);stage.environmentLoader=loader;
            var roles=new List<RoleRig>();
            for(int i=0;i<6;i++)
            {
                var root=new GameObject("Role"+i).transform;root.SetParent(stageObject.transform,false);
                var technical=new GameObject("TechnicalRoot").transform;technical.SetParent(root,false);
                roles.Add(new RoleRig{roleId=((char)('A'+i)).ToString(),roleIndex=i,roleRoot=root,root=technical,
                    startSpawnPointId=i==0?"":i==1?"main":i<4?"remote":"single",
                    lookAtSpawnPointId=i==2?"look":"",isActiveConversationPartner=i==1||i==3,
                    initialIsActiveConversationPartner=i==1||i==3,hasPreRecordedTakes=i==5});
            }
            stage.roles=roles;stage.roleCount=roles.Count;
            roles[5].sourceRoleIndex=42;
            stage.BuildActiveRoles();
            Check(!roles[0].isActiveConversationPartner&&roles[1].isActiveConversationPartner&&roles[5].sourceRoleIndex==42,"reference rebuild preserves activation and imported source index");
            Func<string,Transform> resolve=id=>points.TryGetValue(id,out var t)?t:null;
            var poses=RolePlacementUtility.BuildStartPoses(roles,stageObject.transform,"main",resolve,p=>0.25f);
            Check(poses.Count==5&&!poses.ContainsKey(5),"imported NPC excluded");
            Near(poses[0].Position,new Vector3(0,.25f,1.5f),"default circle unchanged");
            Near(poses[1].Position,new Vector3(0,.25f,-1.5f),"explicit StageSpawn shares default group");
            Near(poses[2].Position,new Vector3(11.5f,.25f,5),"remote group member 1");
            Near(poses[3].Position,new Vector3(8.5f,.25f,5),"remote group member 2");
            Near(poses[4].Position,new Vector3(-7,.25f,8),"single role at its spawn");
            var direction=new Vector3(10,0,20)-poses[2].Position;direction.y=0;
            Check(Vector3.Dot(poses[2].Rotation*Vector3.forward,direction.normalized)>0.999f,"explicit look target");
            var cal=stageObject.AddComponent<AvatarCalibrationController>();cal.Initialize(roles,false);
            foreach(var entry in poses)cal.PlaceRoleAt(entry.Key,entry.Value.Position,entry.Value.Rotation,stageObject.transform);
            Check(roles[2].hasInitialStartPose&&Mathf.Abs(Mathf.DeltaAngle(roles[2].initialStartYawDeg,poses[2].Rotation.eulerAngles.y))<0.01f,"initial yaw seeded");
            roles[5].roleSpawnId="remote";roles[5].preRecordedStartRootPose=new TransformData{LocalPosition=new Vector3(2,0,3),LocalRotation=Quaternion.Euler(0,25,0)};
            var flow=stageObject.AddComponent<FlowController>();flow.Stage=stage;flow._data=new FlowStateData();flow._data.Initialize(roles);
            new AvatarPlacementState(flow){_stageRoot=stageObject.transform}.PlacePreRecordedRole(5);
            Near(roles[5].root.localPosition,new Vector3(13,0,3),"NPC source offset transformed once");
            Check(Mathf.Abs(Mathf.DeltaAngle(roles[5].initialStartYawDeg,115f))<0.01f,"NPC yaw composed once");
            Check(flow._data.ToBeRecorded==1,"first active role can have nonzero index");
            var selectable=flow._data.GetSelectableRoleIndices(new List<int>{1});
            Check(selectable.Count==1&&selectable[0]==3,"sparse active selection");
            Check(flow.SetConversationPartnerActive(2,true),"runtime activation");
            Check(flow._data.Roles.Exists(r=>r.roleIndex==2)&&!flow._data.IndicesOfPassiveRoles.Contains(2),"activation membership");
            foreach(var r in roles)r.isActiveConversationPartner=false;flow._data.RefreshMembership();
            Check(flow._data.ActiveRoleCount==0&&flow._data.ToBeRecorded==-1&&flow._data.GetSelectableRoleIndices(null).Count==0,"all-passive safe wait");
            roles[3].isActiveConversationPartner=true;flow._data.RefreshMembership();Check(flow._data.ToBeRecorded==3,"late inspector activation");
            report.AppendLine("PASS grouped placement, rotated/translated StageRoot, ground height, look target, initial yaw, NPC composition, sparse/all-passive membership and runtime activation");

            var config=new PresetConfig{PresetId="placement"};
            config.StartRoles.Roles.Add(new StartRoleSettings{RoleId="A",SpawnPointId="remote",LookAtSpawnPointId="look",IsActiveConversationPartner=false,HasConversationPartnerSetting=true});
            var store=new PresetStore(Path.GetFullPath("Temp/RolePlacementPresetTest"));Check(store.SavePreset(config),"preset save");
            var loaded=store.LoadPreset("placement");Check(loaded.StartRoles.Roles[0].SpawnPointId=="remote"&&!loaded.StartRoles.Roles[0].StartsActive,"preset roundtrip false preserved");
            var legacy=JsonUtility.FromJson<StartRoleSettings>("{\"RoleId\":\"A\"}");Check(legacy.StartsActive,"legacy preset active default");

            var provider=new RoleCalibrationDataProvider();provider.Initialize(roles);
            var session=new SessionModel{RoleCount=roles.Count,Roles=provider.CreateRoleMetas()};
            var loadedSession=JsonUtility.FromJson<SessionModel>(JsonUtility.ToJson(session));
            Check(!loadedSession.Roles[2].InitialIsActiveConversationPartner&&loadedSession.Roles[2].HasInitialConversationPartnerSetting,"persist original activation, not runtime activation");
            var scene=stageObject.AddComponent<SceneLoader>();Set(scene,"stage",stage);
            foreach(var r in roles)r.root.localPosition=Vector3.zero;
            typeof(SceneLoader).GetMethod("ApplyRolesFromSession",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scene,new object[]{loadedSession});
            Near(roles[2].root.localPosition,poses[2].Position,"loaded off-stage start pose not recomputed/doubled");
            var player=new TakePlayer(roles[2].root,null,null,null,null,null,null,null,null);
            typeof(TakePlayer).GetMethod("ApplyFrame",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{new Frame{Body=new BodyPose{Pos=poses[2].Position,YawDeg=poses[2].Rotation.eulerAngles.y}}});
            Near(roles[2].root.position,stageObject.transform.TransformPoint(poses[2].Position),"frame playback stage space");
            report.AppendLine("PASS preset/session JSON, legacy default, inactive metadata, loaded off-stage pose and TakePlayer frame playback");

            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AppV2/Runtime/Scripts/Dialogue/UI/UiElements/RoleSettingsRolePrefab.prefab");
            rowObject=UnityEngine.Object.Instantiate(prefab);rowObject.SetActive(false);var row=rowObject.GetComponent<RoleSettingsRowUI>();
            var settings=loaded.StartRoles.Roles[0];int changes=0;
            row.Initialize(7,"example","130","100",new List<string>{"example"});
            row.ConfigurePlacement(new List<string>{"main","remote","single","look"},"main",settings);
            row.PlacementChanged+=(index,spawn,look,active)=>{Check(index==7,"row index");changes++;settings.SpawnPointId=spawn;settings.LookAtSpawnPointId=look;settings.IsActiveConversationPartner=active;settings.HasConversationPartnerSetting=true;};
            var spawnDrop=(TMP_Dropdown)typeof(RoleSettingsRowUI).GetField("spawnPointDropdown",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(row);
            var lookDrop=(TMP_Dropdown)typeof(RoleSettingsRowUI).GetField("lookAtSpawnPointDropdown",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(row);
            var toggle=(Toggle)typeof(RoleSettingsRowUI).GetField("isActiveConversationPartnerToggle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(row);
            Check(spawnDrop!=null&&lookDrop!=null&&spawnDrop!=lookDrop&&toggle!=null,"distinct prefab bindings");
            spawnDrop.value=3;lookDrop.value=1;toggle.isOn=true;
            Check(changes==3&&settings.SpawnPointId=="single"&&settings.LookAtSpawnPointId=="main"&&settings.StartsActive,"row events");
            row.ConfigurePlacement(new List<string>{"main","remote"},"remote",settings);
            row.ConfigurePlacement(new List<string>{"main","remote"},"remote",settings);
            Check(changes==3&&spawnDrop.options[spawnDrop.value].text.Contains("single"),"environment refresh preserves missing selection without events");
            spawnDrop.value=0;Check(changes==4&&settings.SpawnPointId=="","no duplicate listeners and default option");
            report.AppendLine("PASS actual extended row prefab: separate spawn/look bindings, toggle, events, environment refresh/missing IDs, default selection, idempotent listeners");
            Set(loader,"environments",new List<EnvironmentEntry>{new EnvironmentEntry{environmentId="test",prefab=env}});
            config.Environment.EnvironmentId="test";config.Environment.StageSpawnId="main";
            var activeObject=new GameObject("Independent role UI");activeObject.transform.SetParent(stageObject.transform,false);
            var ui=activeObject.AddComponent<ActiveRolesSettingsUI>();
            var rows=new GameObject("Rows").transform;rows.SetParent(activeObject.transform,false);
            Set(ui,"rowsContainer",rows);Set(ui,"rowPrefab",prefab.GetComponent<RoleSettingsRowUI>());
            ui.Display(config,loader);
            var integrated=rows.GetChild(0).GetComponent<RoleSettingsRowUI>();
            var integratedSpawn=(TMP_Dropdown)typeof(RoleSettingsRowUI).GetField("spawnPointDropdown",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(integrated);
            Check(integratedSpawn.options.Count==5,"environment options without UI ancestry");
            integratedSpawn.value=integratedSpawn.options.FindIndex(o=>o.text=="single");
            Check(config.StartRoles.Roles[0].SpawnPointId=="single","integrated row updates config");
            ui.RefreshSpawnOptions("missing");Check(config.StartRoles.Roles[0].SpawnPointId=="single","missing environment retains selection");
            ui.RefreshSpawnOptions("test");Check(integratedSpawn.options.Count==5,"environment options refreshed");
            report.AppendLine("PASS integrated ActiveRolesSettingsUI receives EnvironmentLoader independently of canvas ancestry and refreshes spawn options/config");
        }
        catch(Exception ex){report.AppendLine("FAIL "+ex);}
        finally{if(rowObject!=null)UnityEngine.Object.DestroyImmediate(rowObject);if(env!=null)UnityEngine.Object.DestroyImmediate(env);if(stageObject!=null)UnityEngine.Object.DestroyImmediate(stageObject);}
        File.WriteAllText("Temp/RolePlacementRegression.txt",report.ToString());Debug.Log("Role placement regression completed");
    }
}
#endif
