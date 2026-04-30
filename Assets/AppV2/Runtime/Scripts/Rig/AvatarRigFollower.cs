using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class AvatarRigFollower : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform avatarRoot;

        [Header("Auto setup")]
        [SerializeField] private bool autoBuildMapOnAwake = true;

        [Header("Runtime")]
        [SerializeField] private bool followAfterCalibration = true;
        [SerializeField] private bool isCalibrated = false;

        [Header("Optional root copy")]
        [SerializeField] private bool copyRootPosition = false;
        [SerializeField] private bool copyRootRotation = false;
        [SerializeField] private bool copyRootScale = false;

        [Header("Follow in world space")]
        [SerializeField] private bool copyWorldPosition = true;
        [SerializeField] private bool copyWorldRotation = true;
        [SerializeField] private bool copyLocalScale = false;

        [Header("Names to match")]
        [SerializeField] private List<string> ikTargetNames = new()
        {
            "headTarget",
            "leftHandTarget",
            "rightHandTarget",
            "hipTarget",
            "leftFootTarget",
            "rightFootTarget"
        };

        private readonly List<TransformPair> _pairs = new();

        [System.Serializable]
        private class TransformPair
        {
            public string name;
            public Transform visual;
            public Transform avatar;
        }

        private void Awake()
        {
            if (avatarRoot == null)
                avatarRoot = transform;

            if (autoBuildMapOnAwake)
                BuildMap();
        }

        [ContextMenu("Build Map")]
        public void BuildMap()
        {
            _pairs.Clear();

            if (visualRoot == null)
            {
                Debug.LogError($"[{name}] visualRoot is null.");
                return;
            }

            if (avatarRoot == null)
            {
                Debug.LogError($"[{name}] avatarRoot is null.");
                return;
            }

            foreach (string ikTargetName in ikTargetNames)
            {
                Transform vis = FindDeepChildByName(visualRoot, ikTargetName);
                Transform ava = FindDeepChildByName(avatarRoot, ikTargetName);

                if (vis == null)
                {
                    Debug.LogWarning($"[{name}] Visual target not found: {ikTargetName}");
                    continue;
                }

                if (ava == null)
                {
                    Debug.LogWarning($"[{name}] Avatar target not found: {ikTargetName}");
                    continue;
                }

                _pairs.Add(new TransformPair
                {
                    name = ikTargetName,
                    visual = vis,
                    avatar = ava
                });

                Debug.Log($"[{name}] Mapped {ikTargetName}: visual={vis.name}, avatar={ava.name}");
            }

            Debug.Log($"[{name}] BuildMap complete. Pairs: {_pairs.Count}");
        }

        [ContextMenu("Calibrate Targets From Avatar")]
        public void CalibrateTargetsFromAvatar()
        {
            if (_pairs.Count == 0)
            {
                Debug.LogWarning($"[{name}] No pairs mapped. BuildMap first.");
                return;
            }

            for (int i = 0; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];

                if (pair.visual == null || pair.avatar == null)
                    continue;

                if (copyWorldPosition)
                    pair.visual.position = pair.avatar.position;

                if (copyWorldRotation)
                    pair.visual.rotation = pair.avatar.rotation;

                if (copyLocalScale)
                    pair.visual.localScale = pair.avatar.localScale;
            }

            isCalibrated = true;
            Debug.Log($"[{name}] Calibration complete. Visual targets snapped to avatar targets.");
        }

        public void SetCalibrated(bool value)
        {
            isCalibrated = value;
        }

        // is called in RoleRigUpdatePipeline
        public void ApplyFollow()
        {
            if (!followAfterCalibration || !isCalibrated)
                return;

            if (visualRoot != null && avatarRoot != null)
            {
                if (copyRootPosition)
                    avatarRoot.position = visualRoot.position;

                if (copyRootRotation)
                    avatarRoot.rotation = visualRoot.rotation;

                if (copyRootScale)
                    avatarRoot.localScale = visualRoot.localScale;
            }

            for (int i = 0; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];

                if (pair.visual == null || pair.avatar == null)
                    continue;

                if (copyWorldPosition)
                    pair.avatar.position = pair.visual.position;

                if (copyWorldRotation)
                    pair.avatar.rotation = pair.visual.rotation;

                if (copyLocalScale)
                    pair.avatar.localScale = pair.visual.localScale;
            }
        }

        private Transform FindDeepChildByName(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChildByName(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}