using UnityEngine;
using UnityEngine.Animations.Rigging;
using AppV2.Runtime.Scripts.Dialogue;

namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarRigDefinition : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Animator animator;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private AvatarRigFollower rigFollower;
       

        [Header("Visibility")]
        [SerializeField] private Renderer[] renderersToToggle;

        public Animator Animator => animator;
        public RigBuilder RigBuilder => rigBuilder;
        public AvatarRigFollower RigFollower => rigFollower;

        [Header("Rig Modes")]
        [SerializeField] private UnityEngine.Animations.Rigging.Rig recordPlaybackModeRig;
        public string recordPlaybackModeRigName = "RecordPlaybackMode";
        [SerializeField] private UnityEngine.Animations.Rigging.Rig idleModeRig;
        public string idleModeRigName = "IdleMode";

        [Header("Idle")]
        [SerializeField] private Transform lookAtTarget;
        [SerializeField] private string standingIdleAnimationStateName = "Idle";
        [SerializeField] private string sittingIdleAnimationStateName = "Sitting Idle";
        [SerializeField] private string recordPlaybackStateName = "T-Pose 0";
        public string lookAtTargetName = "lookAtTarget";
        public Transform LookAtTarget => lookAtTarget;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (rigBuilder == null)
                rigBuilder = GetComponentInChildren<RigBuilder>(true);

            if (rigFollower == null)
                rigFollower = GetComponent<AvatarRigFollower>();

            if (renderersToToggle == null || renderersToToggle.Length == 0)
                renderersToToggle = GetComponentsInChildren<Renderer>(true);

            if (lookAtTarget == null)
            {
                lookAtTarget = FindDeepChild(transform, lookAtTargetName);

                if (lookAtTarget == null)
                {
                    UnityEngine.Debug.LogError($"[{name}] lookAtTarget with name '{lookAtTargetName}' not found.");
                }
            }

            if (recordPlaybackModeRig == null)
            {
                Transform rigTransform = FindDeepChild(transform, recordPlaybackModeRigName);

                if (rigTransform != null)
                    recordPlaybackModeRig = rigTransform.GetComponent<UnityEngine.Animations.Rigging.Rig>();

                if (recordPlaybackModeRig == null)
                    UnityEngine.Debug.LogError($"[{name}] RecordPlaybackMode Rig with name '{recordPlaybackModeRigName}' not found or has no Rig component.");
            }

            if (idleModeRig == null)
            {
                Transform rigTransform = FindDeepChild(transform, idleModeRigName);

                if (rigTransform != null)
                    idleModeRig = rigTransform.GetComponent<UnityEngine.Animations.Rigging.Rig>();

                if (idleModeRig == null)
                    UnityEngine.Debug.LogError($"[{name}] IdleMode Rig with name '{idleModeRigName}' not found or has no Rig component.");
            }
        }

        // in RoleRig there is a field AvatarRigDefinition avatar. The AvatarCalibrationController calls SetVisible
        // via roles[i] this is used in CalibrationState to toggle visibility
        public void SetVisible(bool visible)
        {
            if (renderersToToggle == null) return;

            for (int i = 0; i < renderersToToggle.Length; i++)
            {
                if (renderersToToggle[i] != null)
                    renderersToToggle[i].enabled = visible;
            }
        }

        public void SetRigModeIdle()
        {
            if (recordPlaybackModeRig != null)
                recordPlaybackModeRig.weight = 0f;

            if (idleModeRig != null)
                idleModeRig.weight = 1f;
        }

        public void SetRigModeRecordPlayback()
        {
            if (recordPlaybackModeRig != null)
                recordPlaybackModeRig.weight = 1f;

            if (idleModeRig != null)
                idleModeRig.weight = 0f;
        }

        public void PlayIdleAnimation(bool sittingIdle)
        {
            if (animator == null) return;

            string stateName = sittingIdle
                ? sittingIdleAnimationStateName
                : standingIdleAnimationStateName;

            animator.Play(stateName, 0, 0f);
            
        }

        public void BackToTPose()
        {
            if (animator == null) return;
            animator.Play(recordPlaybackStateName, 0, 0f);
            
        }

        public void SetLookAtTargetWorldPosition(Vector3 worldPosition)
        {
            if (lookAtTarget == null){
                UnityEngine.Debug.LogError($"[{name}] lookAtTarget was not found.");
                return;
            } 

            lookAtTarget.position = worldPosition;

            //UnityEngine.Debug.Log($"[{name}] SetLookAtTargetWorldPosition was called.");
        }


        private Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent.name == targetName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform result = FindDeepChild(child, targetName);

                if (result != null)
                    return result;
            }

            return null;
        }

        
    }
}