using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class VisualRigFollower : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private Transform technicalRoot;
        [SerializeField] private Transform visualRoot;

        [Header("Auto setup")]
        [SerializeField] private bool autoBuildMapOnAwake = true;

        [Header("Root copy")]
        [SerializeField] private bool copyRootPosition = true;
        [SerializeField] private bool copyRootRotation = true;
        [SerializeField] private bool copyRootScale = false;

        [Header("Bone copy")]
        [SerializeField] private bool copyLocalPosition = true;
        [SerializeField] private bool copyLocalRotation = true;
        [SerializeField] private bool copyLocalScale = false;

        [Header("Names to match")]
        [SerializeField] private List<string> boneNames = new()
        {
            "head",
            "leftHand",
            "rightHand",
            "hip",
            "leftFoot",
            "rightFoot"
        };

        private readonly List<TransformPair> _pairs = new();

        [System.Serializable]
        private class TransformPair
        {
            public string name;
            public Transform technical;
            public Transform visual;
        }

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

            if (autoBuildMapOnAwake)
                BuildMap();
        }

        [ContextMenu("Build Map")]
        public void BuildMap()
        {
            _pairs.Clear();

            if (technicalRoot == null)
            {
                Debug.LogError($"[{name}] technicalRoot is null.");
                return;
            }

            if (visualRoot == null)
            {
                Debug.LogError($"[{name}] visualRoot is null.");
                return;
            }

            foreach (string boneName in boneNames)
            {
                Transform tech = FindDeepChildByName(technicalRoot, boneName);
                Transform vis = FindDeepChildByName(visualRoot, boneName);

                if (tech == null)
                {
                    Debug.LogWarning($"[{name}] Technical bone not found: {boneName}");
                    continue;
                }

                if (vis == null)
                {
                    Debug.LogWarning($"[{name}] Visual bone not found: {boneName}");
                    continue;
                }

                _pairs.Add(new TransformPair
                {
                    name = boneName,
                    technical = tech,
                    visual = vis
                });
            }

            Debug.Log($"[{name}] BuildMap complete. Pairs: {_pairs.Count}");
        }

        public void ApplyFollow()
        {
            if (technicalRoot != null && visualRoot != null)
            {
                if (copyRootPosition)
                    visualRoot.position = technicalRoot.position;

                if (copyRootRotation)
                    visualRoot.rotation = technicalRoot.rotation;

                if (copyRootScale)
                    visualRoot.localScale = technicalRoot.localScale;
            }

            for (int i = 0; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];

                if (pair.technical == null || pair.visual == null)
                    continue;

                if (copyLocalPosition)
                    pair.visual.localPosition = pair.technical.localPosition;

                if (copyLocalRotation)
                    pair.visual.localRotation = pair.technical.localRotation;

                if (copyLocalScale)
                    pair.visual.localScale = pair.technical.localScale;
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