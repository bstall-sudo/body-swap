using System;
using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.Rig;
using AppV2.Runtime.Scripts.Loader;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.DataStructures
{
    [Serializable]
    public class RoleRig
    {
        public string avatarId; 
        public string avatarSpawnId= "";

        //roleSpawnId ist die Position der Figur
        public string roleSpawnId= "";
        public string roleId;                 // z.B. "A", "B", "C" oder "Role 1" das sollte in Zukunft entfernt werden, weil es sich mit slotId deckt.

        public string slotId;        // z.B. "A"
        
        public string sourceRoleId;        // z.B. "A"
        public int roleIndex;
        public int sourceRoleIndex;

        [Header("Pre-Recorded Inputs")]
        
        
        public string npcGroupId;           // z.B. "NpcGroup1"
        public bool hasPreRecordedTakes = false;
        public bool isActiveConversationPartner = true;
        public string takeSource; // Pfad zur importierten Session / Take-Datei

        public string sourceSessionId;
        public string sourceWorkshopFolderName;
        public string sourceSessionFolderName;

        public AvatarLoader avatarLoader;

        public Transform roleRoot;
        public Transform root;
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;
        public Transform leftFoot;
        public Transform rightFoot;
        public Transform hip;

        


        public ProceduralHipSolver hipSolver;
        public ProceduralFootSolver leftFootSolver;
        public ProceduralFootSolver rightFootSolver;
        
        public AudioSource audioSource;
        [Header("Role size")]
        public int heightOfRoleCm = 180;
     
        public int heightOfSeatedRoleCm = 133;

        public float SeatedHeadYOffsetM
        {
            get
            {
                float deltaCm = heightOfRoleCm - heightOfSeatedRoleCm;
                return (deltaCm / 100f) * 0.6f;
            }
        }

        [Header("Initial placement")]
        public bool hasInitialStartPose = false;
        public Vector3 initialStartPos;
        public float initialStartYawDeg;

        public TransformData preRecordedStartRootPose;
        


    

        [Header("Visual Debug Rig")]
        public Transform visualRigRoot;
        public RolesVisualsSetVisibility visualRolesVisibility;

        [Header("Avatar Rig")]
        public bool sittingIdle = false;
        public Transform avatarRoot;
        public string avatarName;
        public AvatarRigDefinition avatar;
        public AvatarRigFollower rigFollower;

        public VisualRigFollower visualRigFollower;

        public float visualGroundOffsetY = 0f;

        [Tooltip("If true, heightOfRoleCm will be initialized from the player height once.")]
        public bool usePlayerHeightAsDefault = true;

        //das ist im SessionModel definiert
        public RoleCalibrationData preRecordedCalibration;

    
        public void AutoResolveReferences()
        {
            if (roleRoot == null)
            {
                Debug.LogWarning("[RoleRig] roleRoot is null.");
                return;
            }

            Transform technicalRoot = roleRoot.Find("TechnicalRoot");
            Transform visualRoot = roleRoot.Find("VisualRoot");
            Transform avatarContainer = roleRoot.Find("Avatar");
            

            if (technicalRoot != null)
            {
                root = technicalRoot;
                head = technicalRoot.Find("head");
                leftHand = technicalRoot.Find("leftHand");
                rightHand = technicalRoot.Find("rightHand");
                hip = technicalRoot.Find("hip");
                leftFoot = technicalRoot.Find("leftFoot");
                rightFoot = technicalRoot.Find("rightFoot");

                audioSource = technicalRoot.GetComponentInChildren<AudioSource>(true);

                hipSolver = technicalRoot.GetComponentInChildren<ProceduralHipSolver>(true);
                leftFootSolver = leftFoot?.GetComponent<ProceduralFootSolver>();
                rightFootSolver = rightFoot?.GetComponent<ProceduralFootSolver>();
            }

            if (visualRoot != null)
            {
                visualRigRoot = visualRoot.Find("ScaleRoot/visualRigRoot");

                visualRolesVisibility = visualRoot.GetComponentInChildren<RolesVisualsSetVisibility>(true);
                visualRigFollower = visualRoot.GetComponentInChildren<VisualRigFollower>(true);
            }

            if (avatarContainer != null)
            {
                avatarRoot = avatarContainer;

                avatar = avatarRoot.GetComponentInChildren<AvatarRigDefinition>(true);
                rigFollower = avatarRoot.GetComponentInChildren<AvatarRigFollower>(true);
                avatarLoader = roleRoot.GetComponent<AvatarLoader>();

                ResolveAvatarName();
            }

            Debug.Log($"[RoleRig] Resolved {roleId}");
        }    



        public void ResolveAvatarName(bool logWarnings = false)
        {
            if (avatarRoot == null)
            {
                if (logWarnings) Debug.LogWarning($"[RoleRig] avatarRoot is null for role '{roleId}'.");
                return;
            }

            var animator = avatarRoot.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                avatarName = animator.gameObject.name;
            }
            else if (logWarnings)
            {
                Debug.LogWarning($"[RoleRig] No Animator found under '{avatarRoot.name}'.");
            }
        }
    }
}