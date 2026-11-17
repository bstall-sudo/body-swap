#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AppV2.Runtime.Scripts.Config;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Dialogue.UI;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Rig;

[InitializeOnLoad]
public static class NpcOverridesRegression
{
    static NpcOverridesRegression() { EditorApplication.delayCall += Run; }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    static T Get<T>(object o,string name) => (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
    static void Run()
    {
        if (!File.Exists("Temp/NpcOverridesRegression.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/NpcOverridesRegression.request");
        var report = new StringBuilder();
        GameObject root=null;
        try
        {
            string storage=Path.GetFullPath("Temp/NpcOverridesRegressionData");
            var store=new SessionStore(storage,"Workshop","Sessions");
            var source=new SessionModel { SessionId="source", Roles=new List<ConversationRoleMeta>{
                new ConversationRoleMeta { RoleId="A",RoleIndex=0,RoleName="Original",AvatarId="default",HeightOfRoleCm=180,HeightOfSeatedRoleCm=133 },
                new ConversationRoleMeta { RoleId="B",RoleIndex=1,RoleName="Legacy",AvatarId="legacy-avatar",HeightOfRoleCm=150 }
            }};
            store.SaveSessionModel(source);
            string original=File.ReadAllText(store.GetSessionJsonPath("source"));
            string frames=Path.Combine(store.GetSessionFolder("source"),"take.frames.jsonl");File.WriteAllText(frames,"unchanged-motion");
            var group=new NpcGroupSettings { NpcGroupId="NpcGroup0",WorkshopFolderName="Workshop",SessionFolderName="Sessions",SessionId="source" };
            root=new GameObject("NPC overwrite regression"); root.SetActive(false);
            var rowPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AppV2/Runtime/Scripts/Dialogue/UI/UiElements/SingleNpcSettingsPrefab.prefab");
            var row=UnityEngine.Object.Instantiate(rowPrefab,root.transform).GetComponent<SingleNpcSettingsRowUI>();
            Check(row!=null,"Prefab script resolves");
            row.Initialize(group,source.Roles[0],new List<string>{"default","replacement"});
            Get<TMP_Dropdown>(row,"avatarDropdown").value=1;
            Get<TMP_InputField>(row,"heightInput").text="130";
            Get<TMP_InputField>(row,"seatedHeightInput").text="95";
            Check(group.AvatarOverrides.Count==1 && group.AvatarOverrides[0].AvatarId=="replacement" && group.AvatarOverrides[0].HeightOfRoleCm==130 && group.AvatarOverrides[0].HeightOfSeatedRoleCm==95,"Row edits persist all three values");
            Get<TMP_InputField>(row,"heightInput").text="";
            Get<TMP_InputField>(row,"heightInput").onEndEdit.Invoke("");
            Check(Get<TMP_InputField>(row,"heightInput").text=="130","Empty height preserves last valid value");
            var config=new PresetConfig { NpcGroups=new List<NpcGroupSettings>{group} };
            config=JsonUtility.FromJson<PresetConfig>(JsonUtility.ToJson(config));
            Check(config.NpcGroups[0].AvatarOverrides[0].HeightOfSeatedRoleCm==95,"Preset JSON round-trip");
            var import=new PreRecordedSceneImport { npcGroupId=group.NpcGroupId,workshopFolderName="Workshop",sessionFolderName="Sessions",sessionId="source",avatarOverrides=NpcRoleOverrides.Copy(group.AvatarOverrides) };
            var imported=PreRecordedSceneImporter.BuildRoleRigsFromImportedSession(import,store,group.NpcGroupId);
            Check(imported.Count==2 && imported[0].avatarId=="replacement" && imported[0].heightOfRoleCm==130 && imported[0].heightOfSeatedRoleCm==95,"Importer applies overrides by source RoleId");
            Check(imported[1].avatarId=="legacy-avatar" && imported[1].heightOfSeatedRoleCm==(int)(150/1.35f),"Other roles inherit original, legacy seated height estimated");
            var stage=root.AddComponent<ConversationStage>();stage.roles=imported;stage.npcHeightOverwrites=true;stage.heightOfPlayerCm=170;stage.heightOfSeatedPlayerCm=120;
            typeof(ConversationStage).GetMethod("InitializeRoleHeightsFromPlayerIfNeeded",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stage,null);
            Check(imported[0].heightOfRoleCm==130 && imported[0].heightOfSeatedRoleCm==95 && imported[1].heightOfRoleCm==170,"Explicit NPC sizes take priority over general NPC overwrite");
            var controller=root.AddComponent<PresetController>();
            typeof(PresetController).GetField("conversationStage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,stage);
            var groups=new List<NpcGroupSettings>();stage.preRecordedScenes=new List<PreRecordedSceneImport>{import};
            typeof(PresetController).GetMethod("ReadNpcGroupSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{groups});
            Check(groups[0].AvatarOverrides[0].HeightOfRoleCm==130,"Stage to preset retains overrides");
            typeof(PresetController).GetMethod("ApplyNpcGroupSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{groups});
            Check(stage.preRecordedScenes[0].avatarOverrides[0].AvatarId=="replacement","Preset to stage retains overrides");
            var provider=new RoleCalibrationDataProvider();provider.Initialize(imported);
            var recording=new SessionModel { SessionId="recording",Roles=provider.CreateRoleMetas(),NpcGroups=stage.preRecordedScenes };
            store.SaveSessionModel(recording);var loaded=store.LoadSessionModel("recording");
            Check(loaded.Roles[0].AvatarId=="replacement" && loaded.Roles[0].HeightOfRoleCm==130 && loaded.Roles[0].HeightOfSeatedRoleCm==95 && loaded.NpcGroups[0].avatarOverrides[0].HeightOfRoleCm==130,"New session JSON retains effective metadata and override snapshot");
            Get<Button>(row,"resetToOriginButton").onClick.Invoke();
            Check(group.AvatarOverrides.Count==0 && Get<TMP_InputField>(row,"heightInput").text=="180" && Get<TMP_InputField>(row,"seatedHeightInput").text=="133","Reset restores immutable source values");
            var scenePanel=UnityEngine.Object.FindFirstObjectByType<SingleNpcSettingsUI>(FindObjectsInactive.Include);
            Check(scenePanel!=null,"Scene resolves SingleNpcSettingsUI");
            var panel=UnityEngine.Object.Instantiate(scenePanel.gameObject,root.transform).GetComponent<SingleNpcSettingsUI>();
            panel.Display(group,storage,null);
            var sceneCatalog=Get<AvatarCatalog>(panel,"avatarCatalog");
            Check(sceneCatalog!=null,"Single NPC panel has its own catalog reference");
            var catalogIds=sceneCatalog.GetAllAvatarIds();
            Check(catalogIds.Count>1,"Real scene catalog contains multiple avatars");
            foreach(var npcRow in Get<List<SingleNpcSettingsRowUI>>(panel,"_rows"))
            {
                var dropdown=Get<TMP_Dropdown>(npcRow,"avatarDropdown");
                Check(dropdown.name=="Overwrite_SelectNpcAvatar_Dropdown","Renamed prefab dropdown is referenced");
                Check(dropdown.template!=null && dropdown.itemText!=null,"Dropdown list template and label assigned");
                Check(dropdown.template.IsChildOf(dropdown.transform) && dropdown.template.GetComponentInChildren<Toggle>(true)!=null,"Dropdown template has child Toggle");
                Check(dropdown.itemText.GetComponentInParent<Toggle>(true)!=null,"Item label is inside the template Toggle");

                foreach(var id in catalogIds)
                    Check(dropdown.options.Exists(option=>option.text==id),"Catalog option missing: "+id);
            }

            Check(Get<List<SingleNpcSettingsRowUI>>(panel,"_rows").Count==2 && Get<TMP_Text>(panel,"npcGroupIdDisplay").text.StartsWith(group.NpcGroupId),"Selected group panel loads exactly its source roles and displays group ID");
            var navigation=UnityEngine.Object.FindFirstObjectByType<MenuNavigation>(FindObjectsInactive.Include);
            var serialized=new SerializedObject(navigation);var pages=serialized.FindProperty("pages");bool registered=false;
            for(int i=0;i<pages.arraySize;i++)
                if(pages.GetArrayElementAtIndex(i).FindPropertyRelative("pageId").stringValue=="SingleNpcRolesSettingsPanel")
                    registered=pages.GetArrayElementAtIndex(i).FindPropertyRelative("panel").objectReferenceValue==scenePanel.gameObject;
            Check(registered,"Single NPC panel is registered in menu navigation");
            var back=Array.Find(scenePanel.GetComponentsInChildren<Button>(true),b=>b.name=="BackButton");
            var backData=new SerializedObject(back).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            Check(backData.arraySize>0 && backData.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue=="ShowPage" && backData.GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments.m_StringArgument").stringValue=="NpcRolesSettingsPanel","Back button routes to NPC group panel");
            var groupRow=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AppV2/Runtime/Scripts/Dialogue/UI/UiElements/NpcSettingsNpcPrefab.prefab"),root.transform).GetComponent<NpcSettingsRowUI>();
            groupRow.Initialize(group,new List<RecordedSessionOption>());bool opened=false;
            groupRow.OverridesRequested+=g=>opened=ReferenceEquals(g,group);
            Get<Button>(groupRow,"npcOverwritesButton").onClick.Invoke();Check(opened,"NPC overwrite button passes selected group");
            NpcRoleOverrides.Set(group,source.Roles[0],"replacement",130,95);
            Get<TMP_Dropdown>(groupRow,"sessionSelection").onValueChanged.Invoke(0);
            Check(group.AvatarOverrides.Count==0,"Changing source clears overrides from previous session");
            Check(File.ReadAllText(store.GetSessionJsonPath("source"))==original && File.ReadAllText(frames)=="unchanged-motion","Original session and motion frames remain unchanged");
            report.AppendLine("PASS: prefab bindings, UI edit/empty height/reset, source switching, group button, preset/controller roundtrip, importer, global height precedence, seated legacy fallback, recording JSON, source immutability, scene panel content/navigation/back button, renamed NPC dropdown contains every actual scene catalog ID without injected catalog.");
        }
        catch(Exception ex){report.AppendLine("FAIL: "+ex);}
        finally { if(root!=null)UnityEngine.Object.DestroyImmediate(root);File.WriteAllText("Temp/NpcOverridesRegression.txt",report.ToString()); }
    }
}
#endif
