#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.DataStructures;
[InitializeOnLoad]
public static class AvatarCalibrationPoseRegression
{
    static AvatarCalibrationPoseRegression()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists("Temp/AvatarPoseRegression.request"))
                EditorApplication.delayCall += Run;
        };
    }
    static void Run()
    {
        if (!EditorApplication.isPlaying || !File.Exists("Temp/AvatarPoseRegression.request")) return;
        File.Delete("Temp/AvatarPoseRegression.request");
        var report = new StringBuilder("PlayMode=" + Application.isPlaying + "\n");
        string[] names = { "head", "leftHand", "rightHand", "hip", "leftFoot", "rightFoot" };
        foreach (string model in new[] { "Bart@T-Pose", "Ritter_Ohne_Schwert@T-Pose", "SchneeMann_001" })
        {
            GameObject shell = null, visual = null;
            try
            {
                report.AppendLine("MODEL " + model);
                shell = new GameObject("Pose regression " + model);
                shell.SetActive(false);
                shell.transform.position = new Vector3(20f, 0f, 20f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayableCharacters/" + model + ".prefab");
                UnityEngine.Object.Instantiate(prefab, shell.transform);
                var definition = shell.AddComponent<AvatarRigDefinition>();
                definition.RefreshReferences();
                visual = new GameObject("Pose regression visual");
                visual.transform.position = shell.transform.position;
                foreach (string name in names)
                {
                    var driver = new GameObject(name).transform; driver.SetParent(visual.transform, false);
                    var target = new GameObject(name + "Target").transform; target.SetParent(driver, false);
                }
                var follower = shell.AddComponent<AvatarRigFollower>();
                var serialized = new SerializedObject(follower);
                serialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                serialized.FindProperty("avatarRoot").objectReferenceValue = shell.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                shell.SetActive(true);
                definition.SetRigModeRecordPlayback();
                definition.PlayBasePose(AvatarBasePose.TPose);
                follower.ResetForCalibration();
                var rigObjects = shell.GetComponentsInChildren<UnityEngine.Animations.Rigging.Rig>(true);
                var weights = new float[rigObjects.Length];
                for (int i = 0; i < weights.Length; i++) weights[i] = rigObjects[i].weight;
                var bone = definition.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Vector3 before = bone.position;
                var role = new RoleRig { roleId=model, avatar=definition, avatarRoot=shell.transform,
                    visualRigRoot=visual.transform, rigFollower=follower };
                bool ok = new AutomaticAvatarCalibration().TryCalibrate(role,180f,0.9f,0.2f,false);
                report.AppendLine("success=" + ok + " restoredLeftBoneCm=" + (100f*Vector3.Distance(before,bone.position)).ToString("F4"));
                for (int i = 0; i < weights.Length; i++)
                    if (weights[i] != rigObjects[i].weight) throw new Exception("Rig weight not restored");
                foreach (string name in new[] { "leftHand", "rightHand" })
                {
                    var t = visual.transform.Find(name + "/" + name + "Target");
                    report.AppendLine(name + " offsetCm=" + (100f*t.localPosition.magnitude).ToString("F3") +
                        " offset=" + t.localPosition.ToString("F4") + " rotation=" + t.localEulerAngles.ToString("F2"));
                    if (t.localPosition.magnitude > 0.10f) throw new Exception("Hand offset exceeds 10cm");
                }
                var targetBefore = visual.transform.Find("leftHand/leftHandTarget").localPosition;
                new AutomaticAvatarCalibration().TryCalibrate(role,180f,0.9f,0.2f,false);
                report.AppendLine("repeatOffsetDifferenceCm=" +
                    (100f * Vector3.Distance(targetBefore,visual.transform.Find("leftHand/leftHandTarget").localPosition)).ToString("F4"));
            }
            catch (Exception ex) { report.AppendLine("FAIL " + ex); }
            finally { if (shell != null) UnityEngine.Object.DestroyImmediate(shell); if (visual != null) UnityEngine.Object.DestroyImmediate(visual); }
        }
        File.WriteAllText("Temp/AvatarPoseRegression.txt",report.ToString());
        Debug.Log("Pose regression finished: Temp/AvatarPoseRegression.txt");
    }
}
#endif
