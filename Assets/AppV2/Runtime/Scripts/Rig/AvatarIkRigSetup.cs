using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

using AnimationRig = UnityEngine.Animations.Rigging.Rig;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarIKRigSetup : MonoBehaviour
    {
        [Header("Avatar Rig")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform rigRoot;

        [Header("Bone References")]
        [SerializeField] private Transform hipBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform headRootBone;
        [SerializeField] private Transform leftHandBone;
        [SerializeField] private Transform rightHandBone;
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform rightFootBone;

        [Header("Head Aim Settings")]
        [SerializeField] private Transform avatarForwardReference;

        [SerializeField]
        private MultiAimConstraintData.Axis headAimAxis = MultiAimConstraintData.Axis.Z;

        [Header("Animation Rigging")]
        [SerializeField] private RigBuilder rigBuilder;

        [Header("Record / Playback Rig")]
        [SerializeField] private AnimationRig recordPlaybackMode;

        [SerializeField] private Transform hipTarget;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [Header("Idle Rig")]
        [SerializeField] private AnimationRig idleMode;

        [SerializeField] private Transform headIk;
        [SerializeField] private Transform lookAtTarget;

        #if UNITY_EDITOR
        [Header("Animator Controller")]
        [SerializeField] private AnimatorController animatorController;
        #endif



        // ============================================================
        // 0. Set Rig to Humanoid Create animator Controller
        // ============================================================
        #if UNITY_EDITOR

        [ContextMenu("Setup Complete Avatar")]
        private void SetupCompleteAvatar()
        {
            Debug.Log(
                $"[{name}] ========================================\n" +
                $"STARTING COMPLETE AVATAR SETUP\n" +
                $"========================================",
                this
            );

            try
            {
                // --------------------------------------------------------
                // 1. Configure imported Mixamo FBX files
                //
                // T-Pose:
                //   Humanoid
                //   Create Avatar From This Model
                //
                // Idle / Sitting Idle:
                //   Humanoid
                //   Copy From Other Avatar
                // --------------------------------------------------------

                Debug.Log(
                    $"[{name}] Step 1/5: Configure Mixamo Animations...",
                    this
                );

                ConfigureMixamoAnimations();


                // --------------------------------------------------------
                // 2. Create Animator Controller
                //
                // T-Pose
                // Idle
                // Sitting Idle
                // --------------------------------------------------------

                Debug.Log(
                    $"[{name}] Step 2/5: Create Animator Controller...",
                    this
                );

                CreateAnimatorController();


                // --------------------------------------------------------
                // 3. Resolve bones and other references
                //
                // Also resolves Head Aim Axis
                // --------------------------------------------------------

                Debug.Log(
                    $"[{name}] Step 3/5: Resolve References...",
                    this
                );

                ResolveReferences();


                // --------------------------------------------------------
                // 4. Create Rig hierarchy and IK targets
                //
                // RecordPlaybackMode
                // IdleMode
                // --------------------------------------------------------

                Debug.Log(
                    $"[{name}] Step 4/5: Create IK Rig Setup...",
                    this
                );

                CreateIKRigSetup();


                // --------------------------------------------------------
                // 5. Add all Animation Rigging constraints
                // --------------------------------------------------------

                Debug.Log(
                    $"[{name}] Step 5/5: Add IK Components...",
                    this
                );

                AddIKComponents();


                // --------------------------------------------------------
                // Save everything
                // --------------------------------------------------------

                EditorUtility.SetDirty(this);

                if (animator != null)
                    EditorUtility.SetDirty(animator);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();


                Debug.Log(
                    $"[{name}] ========================================\n" +
                    $"COMPLETE AVATAR SETUP FINISHED\n" +
                    $"========================================",
                    this
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[{name}] COMPLETE AVATAR SETUP FAILED!\n\n" +
                    $"{exception}",
                    this
                );
            }
        }

        [ContextMenu("Configure Mixamo Animations")]
        private void ConfigureMixamoAnimations()
        {
            // ------------------------------------------------------------
            // 1. Find Animator
            // ------------------------------------------------------------

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogError(
                    $"[{name}] No Animator found.",
                    this
                );
                return;
            }


            // ------------------------------------------------------------
            // 2. Determine folder from current Avatar
            // ------------------------------------------------------------

            string avatarAssetPath =
                AssetDatabase.GetAssetPath(animator.avatar);

            if (string.IsNullOrEmpty(avatarAssetPath))
            {
                Debug.LogError(
                    $"[{name}] Could not determine the FBX path " +
                    $"from the Animator Avatar.",
                    this
                );
                return;
            }

            string folderPath =
                System.IO.Path.GetDirectoryName(avatarAssetPath)
                    ?.Replace("\\", "/");

            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogError(
                    $"[{name}] Could not determine Avatar folder.",
                    this
                );
                return;
            }

            Debug.Log(
                $"[{name}] Configuring Mixamo animations in:\n" +
                folderPath,
                this
            );


            // ------------------------------------------------------------
            // 3. Find the three FBX files
            // ------------------------------------------------------------

            string tPosePath =
                FindModelPathInFolder(
                    folderPath,
                    "T-Pose"
                );

            string idlePath =
                FindModelPathInFolder(
                    folderPath,
                    "Idle",
                    "Sitting Idle"
                );

            string sittingIdlePath =
                FindModelPathInFolder(
                    folderPath,
                    "Sitting Idle"
                );


            // ------------------------------------------------------------
            // 4. Validate
            // ------------------------------------------------------------

            bool missingFile = false;

            if (string.IsNullOrEmpty(tPosePath))
            {
                Debug.LogError(
                    $"[{name}] Could not find T-Pose FBX.",
                    this
                );

                missingFile = true;
            }

            if (string.IsNullOrEmpty(idlePath))
            {
                Debug.LogError(
                    $"[{name}] Could not find Idle FBX.",
                    this
                );

                missingFile = true;
            }

            if (string.IsNullOrEmpty(sittingIdlePath))
            {
                Debug.LogError(
                    $"[{name}] Could not find Sitting Idle FBX.",
                    this
                );

                missingFile = true;
            }

            if (missingFile)
            {
                Debug.LogError(
                    $"[{name}] Configuration aborted because " +
                    $"one or more FBX files are missing.",
                    this
                );

                return;
            }


            Debug.Log(
                $"[{name}] Found Mixamo files:\n" +
                $"T-Pose: {tPosePath}\n" +
                $"Idle: {idlePath}\n" +
                $"Sitting Idle: {sittingIdlePath}",
                this
            );


            // ============================================================
            // 5. CONFIGURE T-POSE FIRST
            // ============================================================

            ModelImporter tPoseImporter =
                AssetImporter.GetAtPath(tPosePath)
                as ModelImporter;

            if (tPoseImporter == null)
            {
                Debug.LogError(
                    $"[{name}] Could not get ModelImporter for T-Pose.",
                    this
                );
                return;
            }


            // Humanoid
            tPoseImporter.animationType =
                ModelImporterAnimationType.Human;

            // Create Avatar From This Model
            tPoseImporter.avatarSetup =
                ModelImporterAvatarSetup.CreateFromThisModel;


            // ------------------------------------------------------------
            // Apply / Reimport
            //
            // This is important:
            // Unity needs to create the Avatar before Idle can reference it.
            // ------------------------------------------------------------

            tPoseImporter.SaveAndReimport();


            // ============================================================
            // 6. GET GENERATED T-POSE AVATAR
            // ============================================================

            Avatar tPoseAvatar =
                FindAvatarInModel(tPosePath);

            if (tPoseAvatar == null)
            {
                Debug.LogError(
                    $"[{name}] T-Pose was imported as Humanoid, " +
                    $"but no Avatar could be found.\n\n" +
                    $"Check the Rig tab of:\n{tPosePath}",
                    this
                );

                return;
            }


            if (!tPoseAvatar.isValid)
            {
                Debug.LogError(
                    $"[{name}] T-Pose Avatar exists but is NOT valid:\n" +
                    tPoseAvatar.name,
                    this
                );

                return;
            }


            if (!tPoseAvatar.isHuman)
            {
                Debug.LogError(
                    $"[{name}] T-Pose Avatar exists but is not Humanoid.",
                    this
                );

                return;
            }


            Debug.Log(
                $"[{name}] T-Pose Avatar created successfully: " +
                $"{tPoseAvatar.name}",
                this
            );


            // ============================================================
            // 7. CONFIGURE IDLE
            // ============================================================

            ConfigureAnimationWithAvatar(
                idlePath,
                tPoseAvatar,
                "Idle"
            );


            // ============================================================
            // 8. CONFIGURE SITTING IDLE
            // ============================================================

            ConfigureAnimationWithAvatar(
                sittingIdlePath,
                tPoseAvatar,
                "Sitting Idle"
            );


            // ============================================================
            // 9. SAVE
            // ============================================================

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();


            Debug.Log(
                $"[{name}] Mixamo animation configuration complete!\n\n" +
                $"T-Pose:\n" +
                $"  Humanoid\n" +
                $"  Create From This Model\n\n" +

                $"Idle:\n" +
                $"  Humanoid\n" +
                $"  Copy From Other Avatar\n" +
                $"  Avatar: {tPoseAvatar.name}\n\n" +

                $"Sitting Idle:\n" +
                $"  Humanoid\n" +
                $"  Copy From Other Avatar\n" +
                $"  Avatar: {tPoseAvatar.name}",
                this
            );
        }






        private void ConfigureAnimationWithAvatar(
            string assetPath,
            Avatar sourceAvatar,
            string animationName)
        {
            ModelImporter importer =
                AssetImporter.GetAtPath(assetPath)
                as ModelImporter;

            if (importer == null)
            {
                Debug.LogError(
                    $"[{name}] Could not get ModelImporter for " +
                    $"{animationName}:\n{assetPath}",
                    this
                );

                return;
            }


            // Humanoid
            importer.animationType =
                ModelImporterAnimationType.Human;

            // Copy From Other Avatar
            importer.avatarSetup =
                ModelImporterAvatarSetup.CopyFromOther;

            // Use T-Pose Avatar
            importer.sourceAvatar =
                sourceAvatar;


            // Apply changes
            importer.SaveAndReimport();


            Debug.Log(
                $"[{name}] Configured {animationName}:\n" +
                $"Humanoid / Copy From Other Avatar / " +
                $"{sourceAvatar.name}",
                this
            );
        }




        private Avatar FindAvatarInModel(
            string assetPath)
        {
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    assetPath
                );

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Avatar avatar)
                {
                    return avatar;
                }
            }

            return null;
        }

        private string FindModelPathInFolder(
            string folderPath,
            string requiredName,
            params string[] excludedNames)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Model",
                    new[] { folderPath }
                );

            foreach (string guid in guids)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(guid);

                string fileName =
                    System.IO.Path.GetFileNameWithoutExtension(
                        assetPath
                    );


                // Must contain required name
                if (!fileName.Contains(
                        requiredName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                // Check exclusions
                bool excluded = false;

                foreach (string excludedName in excludedNames)
                {
                    if (fileName.Contains(
                            excludedName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }

                if (excluded)
                    continue;


                return assetPath;
            }

            return null;
        }

        [ContextMenu("Create Animator Controller")]
        private void CreateAnimatorController()
        {
            // ------------------------------------------------------------
            // 1. Resolve Animator
            // ------------------------------------------------------------

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogError(
                    $"[{name}] No Animator found.",
                    this
                );
                return;
            }


            // ------------------------------------------------------------
            // 2. Find FBX belonging to the Animator
            // ------------------------------------------------------------

            string avatarAssetPath =
                AssetDatabase.GetAssetPath(animator.avatar);

            if (string.IsNullOrEmpty(avatarAssetPath))
            {
                Debug.LogError(
                    $"[{name}] Could not determine the FBX path from the Animator Avatar.",
                    this
                );
                return;
            }

            string folderPath =
                System.IO.Path.GetDirectoryName(avatarAssetPath)
                    ?.Replace("\\", "/");

            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogError(
                    $"[{name}] Could not determine Avatar folder.",
                    this
                );
                return;
            }

            Debug.Log(
                $"[{name}] Searching animations in: {folderPath}",
                this
            );


            // ------------------------------------------------------------
            // 3. Find animation clips
            // ------------------------------------------------------------

            AnimationClip tPose =
                FindAnimationClipInFolder(
                    folderPath,
                    "T-Pose"
                );

            AnimationClip idle =
                FindAnimationClipInFolder(
                    folderPath,
                    "Idle",
                    "Sitting Idle"
                );

            AnimationClip sittingIdle =
                FindAnimationClipInFolder(
                    folderPath,
                    "Sitting Idle"
                );


            // ------------------------------------------------------------
            // 4. Validate
            // ------------------------------------------------------------

            if (tPose == null)
            {
                Debug.LogError(
                    $"[{name}] Could not find animation 'T-Pose' in {folderPath}.",
                    this
                );
            }

            if (idle == null)
            {
                Debug.LogError(
                    $"[{name}] Could not find animation 'Idle' in {folderPath}.",
                    this
                );
            }

            if (sittingIdle == null)
            {
                Debug.LogError(
                    $"[{name}] Could not find animation 'Sitting Idle' in {folderPath}.",
                    this
                );
            }

            if (tPose == null ||
                idle == null ||
                sittingIdle == null)
            {
                Debug.LogError(
                    $"[{name}] Animator Controller was NOT created because animations are missing.",
                    this
                );

                return;
            }


            // ------------------------------------------------------------
            // 5. Controller name = Avatar GameObject name
            // ------------------------------------------------------------

            string controllerName =
                gameObject.name;

            // Remove "(Clone)" just in case.
            controllerName =
                controllerName.Replace("(Clone)", "").Trim();

            string controllerPath =
                $"{folderPath}/{controllerName}.controller";


            // ------------------------------------------------------------
            // 6. Check whether Controller already exists
            // ------------------------------------------------------------

            AnimatorController existingController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    controllerPath
                );

            if (existingController != null)
            {
                Debug.LogWarning(
                    $"[{name}] Animator Controller already exists:\n" +
                    controllerPath +
                    "\nExisting Controller will be used.",
                    this
                );

                animatorController = existingController;
                animator.runtimeAnimatorController =
                    animatorController;

                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(this);

                return;
            }


            // ------------------------------------------------------------
            // 7. Create Controller
            // ------------------------------------------------------------

            animatorController =
                AnimatorController.CreateAnimatorControllerAtPath(
                    controllerPath
                );


            // ------------------------------------------------------------
            // 8. Get State Machine
            // ------------------------------------------------------------

            AnimatorStateMachine stateMachine =
                animatorController.layers[0].stateMachine;


            // ------------------------------------------------------------
            // 9. Create states IN ORDER
            // ------------------------------------------------------------

            AnimatorState tPoseState =
                stateMachine.AddState(
                    "T-Pose",
                    new Vector3(300, 50, 0)
                );

            tPoseState.motion = tPose;


            AnimatorState idleState =
                stateMachine.AddState(
                    "Idle",
                    new Vector3(300, 130, 0)
                );

            idleState.motion = idle;


            AnimatorState sittingIdleState =
                stateMachine.AddState(
                    "Sitting Idle",
                    new Vector3(300, 210, 0)
                );

            sittingIdleState.motion = sittingIdle;


            // ------------------------------------------------------------
            // 10. T-Pose = Default State
            // ------------------------------------------------------------

            stateMachine.defaultState =
                tPoseState;


            // ------------------------------------------------------------
            // 11. Assign Controller to Animator
            // ------------------------------------------------------------

            Undo.RecordObject(
                animator,
                "Assign Animator Controller"
            );

            animator.runtimeAnimatorController =
                animatorController;


            // ------------------------------------------------------------
            // 12. Save
            // ------------------------------------------------------------

            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(animatorController);
            EditorUtility.SetDirty(this);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();


            Debug.Log(
                $"[{name}] Animator Controller created successfully:\n" +
                $"{controllerPath}\n\n" +
                $"States:\n" +
                $"1. T-Pose: {tPose.name}\n" +
                $"2. Idle: {idle.name}\n" +
                $"3. Sitting Idle: {sittingIdle.name}",
                this
            );
        }

        

        private AnimationClip FindAnimationClipInFolder(
            string folderPath,
            string requiredName,
            params string[] excludedNames)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Model",
                    new[] { folderPath }
                );

            foreach (string guid in guids)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(guid);

                string fileName =
                    System.IO.Path.GetFileNameWithoutExtension(
                        assetPath
                    );

                // --------------------------------------------------------
                // Required name
                // --------------------------------------------------------

                if (!fileName.Contains(
                        requiredName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                // --------------------------------------------------------
                // Excluded names
                //
                // Important for:
                //
                // Idle
                // Sitting Idle
                //
                // Otherwise searching "Idle" could return Sitting Idle.
                // --------------------------------------------------------

                bool excluded = false;

                foreach (string excludedName in excludedNames)
                {
                    if (fileName.Contains(
                            excludedName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }

                if (excluded)
                    continue;


                // --------------------------------------------------------
                // FBX contains sub-assets.
                // Find actual AnimationClip.
                // --------------------------------------------------------

                UnityEngine.Object[] assets =
                    AssetDatabase.LoadAllAssetsAtPath(
                        assetPath
                    );

                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset is not AnimationClip clip)
                        continue;

                    // Unity sometimes contains internal preview clips.
                    if (clip.name.StartsWith("__preview__"))
                        continue;

                    Debug.Log(
                        $"[{name}] Found '{requiredName}': " +
                        $"{assetPath} -> {clip.name}",
                        this
                    );

                    return clip;
                }
            }

            return null;
        }

        #endif


        // ============================================================
        // 1. RESOLVE REFERENCES
        // ============================================================

        [ContextMenu("Resolve References")]
        public void ResolveReferences()
        {
    #if UNITY_EDITOR
            Undo.RecordObject(this, "Resolve Avatar IK References");
    #endif

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (rigBuilder == null)
                rigBuilder = GetComponent<RigBuilder>();

            ResolveBones();

            ResolveHeadAimAxis();

    #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
    #endif

            Debug.Log($"[{name}] IK bone references resolved.", this);
        }


        private void ResolveBones()
        {
            // --------------------------------------------------------
            // Preferred method: Humanoid Animator
            // --------------------------------------------------------

            if (animator != null &&
                animator.avatar != null &&
                animator.avatar.isHuman)
            {
                hipBone ??= animator.GetBoneTransform(HumanBodyBones.Hips);
                headBone ??= animator.GetBoneTransform(HumanBodyBones.Head);

                leftHandBone ??= animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHandBone ??= animator.GetBoneTransform(HumanBodyBones.RightHand);

                leftFootBone ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFootBone ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);

                // Your ChainIK root should preferably be Spine2 / UpperChest.
                headRootBone ??=
                    animator.GetBoneTransform(HumanBodyBones.UpperChest);

                if (headRootBone == null)
                    headRootBone =
                        animator.GetBoneTransform(HumanBodyBones.Chest);

                rigRoot ??= hipBone != null
                    ? hipBone.parent
                    : null;
            }

            // --------------------------------------------------------
            // Fallback: search hierarchy by name
            // --------------------------------------------------------

            Transform searchRoot = rigRoot != null
                ? rigRoot
                : transform;

            hipBone ??= FindTransformContaining(
                searchRoot,
                "hips",
                "hip"
            );

            headBone ??= FindTransformContaining(
                searchRoot,
                "head"
            );

            headRootBone ??= FindTransformContaining(
                searchRoot,
                "spine2",
                "spine_02",
                "upperchest",
                "upper_chest"
            );

            leftHandBone ??= FindTransformContaining(
                searchRoot,
                "lefthand",
                "left_hand",
                "hand_l"
            );

            rightHandBone ??= FindTransformContaining(
                searchRoot,
                "righthand",
                "right_hand",
                "hand_r"
            );

            leftFootBone ??= FindTransformContaining(
                searchRoot,
                "leftfoot",
                "left_foot",
                "foot_l"
            );

            rightFootBone ??= FindTransformContaining(
                searchRoot,
                "rightfoot",
                "right_foot",
                "foot_r"
            );
        }

        private void ResolveHeadAimAxis()
        {
            if (headBone == null)
            {
                Debug.LogWarning(
                    $"[{name}] Cannot resolve Head Aim Axis because headBone is missing.",
                    this
                );
                return;
            }

            // If no special reference is assigned,
            // use the AvatarIKRigSetup object's forward direction.
            Transform forwardReference =
                avatarForwardReference != null
                    ? avatarForwardReference
                    : transform;

            Vector3 avatarForward = forwardReference.forward.normalized;

            // All six possible local bone axes in world space.
            Vector3[] directions =
            {
                headBone.right,      // +X
                -headBone.right,     // -X

                headBone.up,         // +Y
                -headBone.up,        // -Y

                headBone.forward,    // +Z
                -headBone.forward    // -Z
            };

            MultiAimConstraintData.Axis[] axes =
            {
                MultiAimConstraintData.Axis.X,
                MultiAimConstraintData.Axis.X_NEG,

                MultiAimConstraintData.Axis.Y,
                MultiAimConstraintData.Axis.Y_NEG,

                MultiAimConstraintData.Axis.Z,
                MultiAimConstraintData.Axis.Z_NEG
            };

            float bestDot = -1f;
            int bestIndex = 0;

            for (int i = 0; i < directions.Length; i++)
            {
                float dot = Vector3.Dot(
                    directions[i].normalized,
                    avatarForward
                );

                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }

            headAimAxis = axes[bestIndex];

            Debug.Log(
                $"[{name}] Head Aim Axis automatically resolved: " +
                $"{headAimAxis} " +
                $"(alignment with avatar forward: {bestDot:F3})",
                this
            );
        }


        // ============================================================
        // 2. CREATE RIG + TARGET HIERARCHY
        // ============================================================

        [ContextMenu("Create IK Rig Setup")]
        public void CreateIKRigSetup()
        {
    #if UNITY_EDITOR

            ResolveReferences();

            if (!ValidateBones())
                return;

            // --------------------------------------------------------
            // RigBuilder
            // --------------------------------------------------------

            if (rigBuilder == null)
            {
                rigBuilder = Undo.AddComponent<RigBuilder>(gameObject);
            }

            // --------------------------------------------------------
            // RecordPlaybackMode
            // --------------------------------------------------------

            recordPlaybackMode = GetOrCreateRig(
                "RecordPlaybackMode",
                recordPlaybackMode
            );

            hipTarget = GetOrCreateChild(
                recordPlaybackMode.transform,
                "hipTarget"
            );

            headTarget = GetOrCreateChild(
                recordPlaybackMode.transform,
                "headTarget"
            );

            leftHand = GetOrCreateChild(
                recordPlaybackMode.transform,
                "leftHand"
            );

            rightHand = GetOrCreateChild(
                recordPlaybackMode.transform,
                "rightHand"
            );

            leftFoot = GetOrCreateChild(
                recordPlaybackMode.transform,
                "leftFoot"
            );

            rightFoot = GetOrCreateChild(
                recordPlaybackMode.transform,
                "rightFoot"
            );


            // --------------------------------------------------------
            // IdleMode
            // --------------------------------------------------------

            idleMode = GetOrCreateRig(
                "IdleMode",
                idleMode
            );

            headIk = GetOrCreateChild(
                idleMode.transform,
                "HeadIk"
            );

            lookAtTarget = GetOrCreateChild(
                headIk,
                "lookAtTarget"
            );


            // --------------------------------------------------------
            // Register layers in RigBuilder
            // --------------------------------------------------------

            EnsureRigLayer(recordPlaybackMode);
            EnsureRigLayer(idleMode);


            // --------------------------------------------------------
            // Initial target transforms
            // --------------------------------------------------------

            CopyWorldTransform(hipBone, hipTarget);
            CopyWorldTransform(headBone, headTarget);

            CopyWorldTransform(leftHandBone, leftHand);
            CopyWorldTransform(rightHandBone, rightHand);

            CopyWorldTransform(leftFootBone, leftFoot);
            CopyWorldTransform(rightFootBone, rightFoot);

            CopyWorldTransform(headBone, headIk);

            // approximately 1m forward from head
            Transform forwardReference =
                avatarForwardReference != null
                    ? avatarForwardReference
                    : transform;

            Vector3 avatarForward =
                forwardReference.forward.normalized;

            lookAtTarget.position =
                headBone.position + avatarForward * 1.0f;

            lookAtTarget.rotation =
                Quaternion.LookRotation(
                    avatarForward,
                    Vector3.up
                );


            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(rigBuilder);

            Debug.Log($"[{name}] IK Rig hierarchy created.", this);

    #else

            Debug.LogWarning(
                "IK rig setup can only be created inside the Unity Editor."
            );

    #endif
        }


        // ============================================================
        // 3. ADD CONSTRAINTS
        // ============================================================

        [ContextMenu("Add IK Components")]
        public void AddIKComponents()
        {
    #if UNITY_EDITOR

            if (!ValidateSetup())
                return;


            // ========================================================
            // HIP
            // ========================================================

            SetupHipPositionConstraint();
            SetupHipRotationConstraint();


            // ========================================================
            // HEAD
            // ========================================================

            SetupHeadChainIK();


            // ========================================================
            // HANDS
            // ========================================================

            SetupTwoBoneIK(
                leftHand,
                leftHandBone,
                "leftHandTarget"
            );

            SetupTwoBoneIK(
                rightHand,
                rightHandBone,
                "rightHandTarget"
            );


            // ========================================================
            // FEET
            // ========================================================

            SetupTwoBoneIK(
                leftFoot,
                leftFootBone,
                "leftFootTarget"
            );

            SetupTwoBoneIK(
                rightFoot,
                rightFootBone,
                "rightFootTarget"
            );


            // ========================================================
            // IDLE HEAD AIM
            // ========================================================

            SetupIdleHeadAim();


            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(rigBuilder);

            Debug.Log($"[{name}] IK constraints created.", this);

    #endif
        }


    #if UNITY_EDITOR

        // ============================================================
        // HIP POSITION
        // ============================================================

        private void SetupHipPositionConstraint()
        {
            MultiPositionConstraint constraint =
                GetOrAddComponent<MultiPositionConstraint>(hipTarget.gameObject);

            constraint.weight = 1f;

            var data = constraint.data;

            data.constrainedObject = hipBone;

            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = true;

            data.maintainOffset = false;

            WeightedTransformArray sources =
                new WeightedTransformArray();

            sources.Add(
                new WeightedTransform(
                    hipTarget,
                    1f
                )
            );

            data.sourceObjects = sources;

            constraint.data = data;

            EditorUtility.SetDirty(constraint);
        }


        // ============================================================
        // HIP ROTATION
        // ============================================================

        private void SetupHipRotationConstraint()
        {
            MultiRotationConstraint constraint =
                GetOrAddComponent<MultiRotationConstraint>(hipTarget.gameObject);

            constraint.weight = 1f;

            var data = constraint.data;

            data.constrainedObject = hipBone;

            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = true;

            data.maintainOffset = false;

            WeightedTransformArray sources =
                new WeightedTransformArray();

            sources.Add(
                new WeightedTransform(
                    hipTarget,
                    1f
                )
            );

            data.sourceObjects = sources;

            constraint.data = data;

            EditorUtility.SetDirty(constraint);
        }


        // ============================================================
        // HEAD CHAIN IK
        // ============================================================

        private void SetupHeadChainIK()
        {
            ChainIKConstraint constraint =
                GetOrAddComponent<ChainIKConstraint>(
                    headTarget.gameObject
                );

            constraint.weight = 1f;

            var data = constraint.data;

            data.root = headRootBone;
            data.tip = headBone;
            data.target = headTarget;

            data.chainRotationWeight = 0.4f;
            data.tipRotationWeight = 1f;

            data.maxIterations = 15;
            data.tolerance = 0.0001f;

            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;

            constraint.data = data;

            EditorUtility.SetDirty(constraint);
        }


        // ============================================================
        // TWO BONE IK
        // ============================================================

        private void SetupTwoBoneIK(
            Transform container,
            Transform tip,
            string targetName)
        {
            if (container == null || tip == null)
                return;

            // Expected hierarchy:
            //
            // UpperArm
            //   LowerArm
            //      Hand
            //
            // or
            //
            // UpperLeg
            //   LowerLeg
            //      Foot

            Transform mid = tip.parent;

            if (mid == null)
            {
                Debug.LogError(
                    $"Cannot determine MID bone for {tip.name}.",
                    tip
                );

                return;
            }

            Transform root = mid.parent;

            if (root == null)
            {
                Debug.LogError(
                    $"Cannot determine ROOT bone for {tip.name}.",
                    tip
                );

                return;
            }


            TwoBoneIKConstraint constraint =
                GetOrAddComponent<TwoBoneIKConstraint>(
                    container.gameObject
                );


            // --------------------------------------------------------
            // Target
            // --------------------------------------------------------

            Transform target = FindDirectChild(
                container,
                targetName
            );

            if (target == null)
            {
                GameObject go = new GameObject(targetName);

                Undo.RegisterCreatedObjectUndo(
                    go,
                    $"Create {targetName}"
                );

                target = go.transform;
                target.SetParent(container, true);
            }

            CopyWorldTransform(tip, target);


            // --------------------------------------------------------
            // Hint
            // --------------------------------------------------------

            string hintName =
                targetName.Replace("Target", "Hint");

            Transform hint = FindDirectChild(
                container,
                hintName
            );

            if (hint == null)
            {
                GameObject go = new GameObject(hintName);

                Undo.RegisterCreatedObjectUndo(
                    go,
                    $"Create {hintName}"
                );

                hint = go.transform;
                hint.SetParent(container, true);
            }


            // Put hint roughly in front of joint.
            Vector3 rootToTip =
                tip.position - root.position;

            Vector3 rootToMid =
                mid.position - root.position;

            Vector3 projected =
                Vector3.Project(
                    rootToMid,
                    rootToTip
                );

            Vector3 bendDirection =
                rootToMid - projected;

            if (bendDirection.sqrMagnitude < 0.0001f)
            {
                bendDirection = container.forward;
            }

            hint.position =
                mid.position +
                bendDirection.normalized * 0.3f;


            // --------------------------------------------------------
            // Constraint data
            // --------------------------------------------------------

            var data = constraint.data;

            data.root = root;
            data.mid = mid;
            data.tip = tip;

            data.target = target;
            data.hint = hint;

            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 1f;
            data.hintWeight = 1f;

            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = false;

            constraint.data = data;
            constraint.weight = 1f;

            EditorUtility.SetDirty(constraint);
        }


        // ============================================================
        // IDLE HEAD AIM
        // ============================================================

        private void SetupIdleHeadAim()
        {
            MultiAimConstraint constraint =
                GetOrAddComponent<MultiAimConstraint>(
                    headIk.gameObject
                );

            constraint.weight = 1f;

            var data = constraint.data;

            data.constrainedObject = headBone;

            data.aimAxis = headAimAxis;
            data.upAxis = MultiAimConstraintData.Axis.Y;

            data.worldUpType =
                MultiAimConstraintData.WorldUpType.SceneUp;

            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = true;

            data.maintainOffset = false;

            data.limits = new Vector2(
                -70f,
                70f
            );

            WeightedTransformArray sources =
                new WeightedTransformArray();

            sources.Add(
                new WeightedTransform(
                    lookAtTarget,
                    1f
                )
            );

            data.sourceObjects = sources;

            constraint.data = data;

            EditorUtility.SetDirty(constraint);
        }


        // ============================================================
        // RIG CREATION
        // ============================================================

        private AnimationRig GetOrCreateRig(
            string rigName,
            AnimationRig existing)
        {
            if (existing != null)
                return existing;

            Transform found = FindDirectChild(
                transform,
                rigName
            );

            if (found != null)
            {
                AnimationRig existingRig =
                    found.GetComponent<AnimationRig>();

                if (existingRig != null)
                    return existingRig;

                return Undo.AddComponent<AnimationRig>(
                    found.gameObject
                );
            }


            GameObject go =
                new GameObject(rigName);

            Undo.RegisterCreatedObjectUndo(
                go,
                $"Create {rigName}"
            );

            go.transform.SetParent(
                transform,
                false
            );

            return Undo.AddComponent<AnimationRig>(go);
        }


        private void EnsureRigLayer(AnimationRig rig)
        {
            if (rig == null)
                return;

            if (rigBuilder.layers.Any(
                    x => x.rig == rig))
                return;

            var layers = rigBuilder.layers;

            layers.Add(
                new RigLayer(rig)
            );

            rigBuilder.layers = layers;
        }


        // ============================================================
        // OBJECT CREATION
        // ============================================================

        private Transform GetOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform existing =
                FindDirectChild(
                    parent,
                    childName
                );

            if (existing != null)
                return existing;

            GameObject go =
                new GameObject(childName);

            Undo.RegisterCreatedObjectUndo(
                go,
                $"Create {childName}"
            );

            go.transform.SetParent(
                parent,
                false
            );

            return go.transform;
        }


        private Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child =
                    parent.GetChild(i);

                if (child.name.Equals(
                        childName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }


        // ============================================================
        // HIERARCHY SEARCH
        // ============================================================

        private Transform FindTransformContaining(
            Transform root,
            params string[] searchTerms)
        {
            if (root == null)
                return null;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);

            foreach (string term in searchTerms)
            {
                string lower =
                    term.ToLowerInvariant();

                foreach (Transform t in all)
                {
                    string objectName =
                        t.name.ToLowerInvariant();

                    if (objectName.Contains(lower))
                        return t;
                }
            }

            return null;
        }


        // ============================================================
        // COMPONENT HELPER
        // ============================================================

        private T GetOrAddComponent<T>(
            GameObject go)
            where T : Component
        {
            T component =
                go.GetComponent<T>();

            if (component != null)
                return component;

            return Undo.AddComponent<T>(go);
        }


        // ============================================================
        // TRANSFORM HELPER
        // ============================================================

        private void CopyWorldTransform(
            Transform source,
            Transform target)
        {
            if (source == null ||
                target == null)
                return;

            Undo.RecordObject(
                target,
                $"Move {target.name}"
            );

            target.position =
                source.position;

            target.rotation =
                source.rotation;
        }


        // ============================================================
        // VALIDATION
        // ============================================================

        private bool ValidateBones()
        {
            bool valid = true;

            CheckBone(
                hipBone,
                nameof(hipBone),
                ref valid
            );

            CheckBone(
                headBone,
                nameof(headBone),
                ref valid
            );

            CheckBone(
                headRootBone,
                nameof(headRootBone),
                ref valid
            );

            CheckBone(
                leftHandBone,
                nameof(leftHandBone),
                ref valid
            );

            CheckBone(
                rightHandBone,
                nameof(rightHandBone),
                ref valid
            );

            CheckBone(
                leftFootBone,
                nameof(leftFootBone),
                ref valid
            );

            CheckBone(
                rightFootBone,
                nameof(rightFootBone),
                ref valid
            );

            return valid;
        }


        private void CheckBone(
            Transform bone,
            string boneName,
            ref bool valid)
        {
            if (bone != null)
                return;

            Debug.LogError(
                $"[{name}] Bone reference missing: {boneName}",
                this
            );

            valid = false;
        }


        private bool ValidateSetup()
        {
            if (!ValidateBones())
                return false;

            if (recordPlaybackMode == null)
            {
                Debug.LogError(
                    "RecordPlaybackMode does not exist. " +
                    "Run 'Create IK Rig Setup' first.",
                    this
                );

                return false;
            }

            if (idleMode == null)
            {
                Debug.LogError(
                    "IdleMode does not exist. " +
                    "Run 'Create IK Rig Setup' first.",
                    this
                );

                return false;
            }

            return true;
        }

    #endif
    }
}