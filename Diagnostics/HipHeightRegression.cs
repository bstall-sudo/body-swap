#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.Input;
[InitializeOnLoad]
public static class HipHeightRegression
{
    static HipHeightRegression()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += s => { if(s == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Run; };
    }
    sealed class Input : IInputTransformsProvider
    {
        public float h;
        public bool TryGetHeadPose(out Vector3 p,out Quaternion r){p=new Vector3(0,h*0.0092f,0);r=Quaternion.identity;return true;}
        public bool TryGetLeftHandPose(out Vector3 p,out Quaternion r){p=new Vector3(-0.3f,h*0.007f,0.3f);r=Quaternion.identity;return true;}
        public bool TryGetRightHandPose(out Vector3 p,out Quaternion r){p=new Vector3(0.3f,h*0.007f,0.3f);r=Quaternion.identity;return true;}
        public bool TryGetHipPose(out Vector3 p,out Quaternion r){p=Vector3.zero;r=Quaternion.identity;return false;}
        public bool TryGetLeftFootPose(out Vector3 p,out Quaternion r){p=Vector3.zero;r=Quaternion.identity;return false;}
        public bool TryGetRightFootPose(out Vector3 p,out Quaternion r){p=Vector3.zero;r=Quaternion.identity;return false;}
        public void SetAnchorFromTakeRoot(Transform t){}
    }
    static void Set(UnityEngine.Object component,string key,UnityEngine.Object value)
    { var s=new SerializedObject(component);s.FindProperty(key).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
    static void Run()
    {
        if(!EditorApplication.isPlaying || !File.Exists("Temp/HipRegression.request")) return;
        File.Delete("Temp/HipRegression.request");
        var report=new StringBuilder("PlayMode="+Application.isPlaying+"\n");
        foreach(string model in new[]{"Bart@T-Pose","Ritter_Ohne_Schwert@T-Pose","SchneeMann_001","BaerSchweinchen"})
        foreach(float height in new[]{130f,180f})
        {
            GameObject scene=null,avatar=null,visual=null;
            try
            {
                scene=new GameObject("Hip regression stage");scene.SetActive(false);
                var stage=scene.AddComponent<ConversationStage>();
                stage._stageRoot=scene.transform;stage.automatedPlayerHeightRecognition=false;
                stage.heightOfPlayerCm=height;stage.avatarBaseHeightCm=200f;
                stage.ProceduralHipAndFeetMove=true;stage.FullBodyTrackers=false;stage.SeatedMode=false;
                stage.activeRolesHeightOverwrites=true;
                stage._input=new Input{h=height};
                var tech=new GameObject("TechnicalRoot").transform;tech.SetParent(scene.transform,false);
                visual=new GameObject("Hip regression visual");visual.SetActive(false);
                var vf=visual.AddComponent<VisualRigFollower>();
                Set(vf,"technicalRoot",tech);Set(vf,"visualRoot",visual.transform);
                string[] names={"head","leftHand","rightHand","hip","leftFoot","rightFoot"};
                var techBones=new Transform[6];
                for(int i=0;i<names.Length;i++)
                {
                    techBones[i]=new GameObject(names[i]).transform;techBones[i].SetParent(tech,false);
                    var driver=new GameObject(names[i]).transform;driver.SetParent(visual.transform,false);
                    var childTarget=new GameObject(names[i]+"Target").transform;childTarget.SetParent(driver,false);
                }
                var hip=techBones[3].gameObject.AddComponent<ProceduralHipSolver>();Set(hip,"bodyRoot",tech);
                visual.SetActive(true);
                avatar=new GameObject("Hip regression avatar");avatar.SetActive(false);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayableCharacters/"+model+".prefab");
                UnityEngine.Object.Instantiate(prefab,avatar.transform);
                var definition=avatar.AddComponent<AvatarRigDefinition>();definition.RefreshReferences();
                var follower=avatar.AddComponent<AvatarRigFollower>();Set(follower,"visualRoot",visual.transform);Set(follower,"avatarRoot",avatar.transform);
                avatar.SetActive(true);definition.SetRigModeRecordPlayback();definition.PlayBasePose(AvatarBasePose.TPose);follower.ResetForCalibration();
                var role=new RoleRig{roleId=model,root=tech,head=techBones[0],leftHand=techBones[1],rightHand=techBones[2],
                    hip=techBones[3],leftFoot=techBones[4],rightFoot=techBones[5],hipSolver=hip,
                    visualRigRoot=visual.transform,visualRigFollower=vf,avatarRoot=avatar.transform,avatar=definition,rigFollower=follower};
                stage.roles.Clear();stage.roles.Add(role);stage.roleCount=1;stage.PrepareCalibrationHeight();
                bool ok=new AutomaticAvatarCalibration().TryCalibrate(role,height,stage.fallbackHipHeight,stage.fallbackFootSpacing,true);
                var target=follower.FindDeepChildByName(avatar.transform,"hipTarget");
                var foot=follower.FindDeepChildByName(avatar.transform,"leftFootTarget");
                float expected=target.position.y;
                for(int i=0;i<300;i++)
                { stage.DriveActiveRoleFromInputStandingMode(0,1f/60f);stage.ApplyProceduralLowerBodyIfNeeded(0,1f/60f);stage.ApplyFollower(0,false); }
                float error=Mathf.Abs(target.position.y-expected);
                report.AppendLine(model+" height="+height+" success="+ok+" hipDriver="+role.hip.localPosition.ToString("F4")+
                    " targetExpected="+expected.ToString("F4")+" targetActual="+target.position.y.ToString("F4")+
                    " leftFootTarget="+foot.position.y.ToString("F4")+" errorCm="+(100*error).ToString("F4"));
                if(!ok || error>0.002f || target.position.y<foot.position.y) throw new Exception("Hip reference drift/compression");
            }
            catch(Exception ex){report.AppendLine("FAIL "+model+" "+height+" "+ex);}
            finally{if(scene!=null)UnityEngine.Object.DestroyImmediate(scene);if(avatar!=null)UnityEngine.Object.DestroyImmediate(avatar);if(visual!=null)UnityEngine.Object.DestroyImmediate(visual);}
        }
        File.WriteAllText("Temp/HipRegression.txt",report.ToString());Debug.Log("Hip height regression completed");
    }
}
#endif
