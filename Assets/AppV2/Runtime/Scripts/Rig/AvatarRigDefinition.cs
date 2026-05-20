using UnityEngine;
using UnityEngine.Animations.Rigging;
using AppV2.Runtime.Scripts.Dialogue;
using UnityEngine.Rendering;
using AppV2.Runtime.Scripts.DataStructures;

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

        [Header("BlendShapes")]
        [SerializeField] private SkinnedMeshRenderer[] headSkinnedMeshRenderers;

        private readonly System.Collections.Generic.Dictionary<string, BlendShapeRef> _blendShapesByLowerName = new();

        [SerializeField] private string mouthOpenBlendShapeName;
        [SerializeField] private string eyebrowRaiseBlendShapeName;

        [SerializeField] private string mouthOpenSearchPart = "open";
        [SerializeField] private string eyebrowRaiseSearchPart = "eyebrow";

        private BlendShapeRef? _mouthOpenBlendShape;
        private BlendShapeRef? _eyebrowRaiseBlendShape;

        private struct BlendShapeRef
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public string Name;
        }


        private Renderer[] _headRenderers;

        public Animator Animator => animator;
        public RigBuilder RigBuilder => rigBuilder;
        public AvatarRigFollower RigFollower => rigFollower;

        //Lower Body Ik ist da, damit die Wichtung auf 0 geschaltet werden kann, falls SeatedMode
        [Header("Lower Body IK")]
        [SerializeField] private TwoBoneIKConstraint leftLegIK;
        [SerializeField] private TwoBoneIKConstraint rightLegIK;
        [SerializeField] private MultiPositionConstraint hipPositionConstraint;
        [SerializeField] private MultiRotationConstraint hipRotationConstraint;

        [SerializeField] private string leftLegIKNameContains = "leftFoot";
        [SerializeField] private string rightLegIKNameContains = "rightFoot";
        [SerializeField] private string hipNameContains = "hipTarget";



        [Header("Rig Modes")]
        [SerializeField] private UnityEngine.Animations.Rigging.Rig recordPlaybackModeRig;
        public string recordPlaybackModeRigName = "RecordPlaybackMode";
        [SerializeField] private UnityEngine.Animations.Rigging.Rig idleModeRig;
        public string idleModeRigName = "IdleMode";

        [Header("Idle")]
        [SerializeField] private Transform lookAtTarget;
        [SerializeField] private string standingIdleAnimationStateName = "Idle";
        [SerializeField] private string sittingIdleAnimationStateName = "Sitting Idle";
        [SerializeField] private string recordPlaybackStateName = "T-Pose";
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

            CacheHeadRenderers();

            AutoFillLowerBodyIKs();

   
        }

        [ContextMenu("Auto Fill Lower Body IKs")]
private void AutoFillLowerBodyIKs()
{
    AutoFillLegIKs();
    AutoFillHipConstraints();
}

private void AutoFillLegIKs()
{
    var allLegIKs = GetComponentsInChildren<TwoBoneIKConstraint>(true);

    for (int i = 0; i < allLegIKs.Length; i++)
    {
        var ik = allLegIKs[i];
        string n = ik.name.ToLowerInvariant();

        if (leftLegIK == null && n.Contains(leftLegIKNameContains.ToLowerInvariant()))
        {
            leftLegIK = ik;
            continue;
        }

        if (rightLegIK == null && n.Contains(rightLegIKNameContains.ToLowerInvariant()))
        {
            rightLegIK = ik;
            continue;
        }
    }
}

        private void AutoFillHipConstraints()
        {
            var positionConstraints = GetComponentsInChildren<MultiPositionConstraint>(true);

            for (int i = 0; i < positionConstraints.Length; i++)
            {
                var c = positionConstraints[i];
                string n = c.name.ToLowerInvariant();

                if (hipPositionConstraint == null && n.Contains(hipNameContains.ToLowerInvariant()))
                {
                    hipPositionConstraint = c;
                    break;
                }
            }

            var rotationConstraints = GetComponentsInChildren<MultiRotationConstraint>(true);

            for (int i = 0; i < rotationConstraints.Length; i++)
            {
                var c = rotationConstraints[i];
                string n = c.name.ToLowerInvariant();

                if (hipRotationConstraint == null && n.Contains(hipNameContains.ToLowerInvariant()))
                {
                    hipRotationConstraint = c;
                    break;
                }
            }
        }


        private void CacheHeadRenderers()
        {
            SkinnedMeshRenderer[] allSkinnedMeshes =
                GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var foundRenderers = new System.Collections.Generic.List<Renderer>();
            var foundSkinnedMeshes = new System.Collections.Generic.List<SkinnedMeshRenderer>();

            for (int i = 0; i < allSkinnedMeshes.Length; i++)
            {
                SkinnedMeshRenderer smr = allSkinnedMeshes[i];

                if (smr == null)
                    continue;

                string lowerName = smr.gameObject.name.ToLowerInvariant();

                // z.B. "avatar_head" oder "myCharacter_head"
                if (lowerName.EndsWith("_head"))
                {
                    foundRenderers.Add(smr);
                    foundSkinnedMeshes.Add(smr);

                    Debug.Log($"[{name}] Head SkinnedMeshRenderer found: '{smr.gameObject.name}'");
                }
            }

            _headRenderers = foundRenderers.ToArray();
            headSkinnedMeshRenderers = foundSkinnedMeshes.ToArray();

            if (_headRenderers.Length == 0)
            {
                Debug.LogWarning(
                    $"[{name}] No SkinnedMeshRenderer ending with '_head' was found."
                );
            }

            CacheHeadBlendShapes();
        }

        private void CacheHeadBlendShapes()
        {
            _blendShapesByLowerName.Clear();

            if (headSkinnedMeshRenderers == null || headSkinnedMeshRenderers.Length == 0)
                return;

            for (int r = 0; r < headSkinnedMeshRenderers.Length; r++)
            {
                SkinnedMeshRenderer smr = headSkinnedMeshRenderers[r];

                if (smr == null || smr.sharedMesh == null)
                    continue;

                Mesh mesh = smr.sharedMesh;

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string blendShapeName = mesh.GetBlendShapeName(i);
                    string key = blendShapeName.ToLowerInvariant();

                    if (_blendShapesByLowerName.ContainsKey(key))
                        continue;

                    _blendShapesByLowerName.Add(key, new BlendShapeRef
                    {
                        Renderer = smr,
                        Index = i,
                        Name = blendShapeName
                    });
                }
            }

            ResolveImportantBlendShapes();
        }

        public bool SetHeadBlendShapeWeight(string blendShapeName, float weight)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
                return false;

            string key = blendShapeName.ToLowerInvariant();

            if (!_blendShapesByLowerName.TryGetValue(key, out BlendShapeRef blendShape))
            {
                Debug.LogWarning($"[{name}] BlendShape '{blendShapeName}' not found.");
                return false;
            }

            weight = Mathf.Clamp(weight, 0f, 100f);
            blendShape.Renderer.SetBlendShapeWeight(blendShape.Index, weight);

            return true;
        }

        private void ResolveImportantBlendShapes()
        {
            _mouthOpenBlendShape = ResolveBlendShape(
                mouthOpenBlendShapeName,
                mouthOpenSearchPart,
                "mouthOpen"
            );

            _eyebrowRaiseBlendShape = ResolveBlendShape(
                eyebrowRaiseBlendShapeName,
                eyebrowRaiseSearchPart,
                "eyebrowRaise"
            );
        }

        private BlendShapeRef? ResolveBlendShape(
            string explicitName,
            string fallbackSearchPart,
            string label)
        {
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                string key = explicitName.ToLowerInvariant();

                if (_blendShapesByLowerName.TryGetValue(key, out BlendShapeRef exact))
                {
                    Debug.Log($"[{name}] {label} BlendShape set explicitly: '{exact.Name}'");
                    return exact;
                }

                Debug.LogWarning($"[{name}] Explicit {label} BlendShape '{explicitName}' not found.");
            }

            if (!string.IsNullOrWhiteSpace(fallbackSearchPart))
            {
                string lowerPart = fallbackSearchPart.ToLowerInvariant();

                foreach (var kvp in _blendShapesByLowerName)
                {
                    BlendShapeRef blendShape = kvp.Value;

                    if (blendShape.Name.ToLowerInvariant().Contains(lowerPart))
                    {
                        Debug.Log($"[{name}] {label} BlendShape auto-found: '{blendShape.Name}'");

                        if (label == "mouthOpen")
                            mouthOpenBlendShapeName = blendShape.Name;

                        if (label == "eyebrowRaise")
                            eyebrowRaiseBlendShapeName = blendShape.Name;

                        return blendShape;
                    }
                }
            }

            Debug.LogWarning($"[{name}] No {label} BlendShape found.");
            return null;
        }

        public void AnimateMouth(float weight)
        {
            SetBlendShapeWeight(_mouthOpenBlendShape, weight);
        }

 

        public void AnimateEyebrow(float weight)
        {
            SetBlendShapeWeight(_eyebrowRaiseBlendShape, weight);
        }

        private void SetBlendShapeWeight(BlendShapeRef? blendShapeRef, float weight)
        {
            if (!blendShapeRef.HasValue)
                return;

            BlendShapeRef blendShape = blendShapeRef.Value;

            if (blendShape.Renderer == null)
                return;

            weight = Mathf.Clamp(weight, 0f, 100f);
            blendShape.Renderer.SetBlendShapeWeight(blendShape.Index, weight);
        }

        public void SetHeadVisible(bool visible)
        {
            if (_headRenderers == null || _headRenderers.Length == 0)
                return;

            for (int i = 0; i < _headRenderers.Length; i++)
            {
                Renderer r = _headRenderers[i];

                if (r == null)
                    continue;

                if (visible)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                else
                {
                    // invisible but still casts shadows
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
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


        public void SetLowerBodyIKWeight(float weight)
        {
            weight = Mathf.Clamp01(weight);

            if (leftLegIK != null)
                leftLegIK.weight = weight;

            if (rightLegIK != null)
                rightLegIK.weight = weight;

            if (hipPositionConstraint != null)
                hipPositionConstraint.weight = weight;

            if (hipRotationConstraint != null)
                hipRotationConstraint.weight = weight;
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

            //animator.Play(stateName, 0, 0f);
            animator.Play("Base Layer.Sitting Idle", 0, 0f);
            Debug.Log($"[{name}] PlayIdleAnimation: {stateName}");
            
        }



        public void PlayBasePose(AvatarBasePose pose)
        {
            if (animator == null) return;

            string stateName = pose switch
            {
                AvatarBasePose.SittingIdle => sittingIdleAnimationStateName,
                AvatarBasePose.StandingIdle => standingIdleAnimationStateName,
                _ => recordPlaybackStateName
            };

            //animator.Play(stateName, 0, 0f);
            animator.Play("Base Layer.Sitting Idle", 0, 0f);
            Debug.Log($"[{name}] PlayBasePose: {stateName}");
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