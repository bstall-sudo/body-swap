using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Rig;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class ChooseSpeakerController : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private float _distanceFromStage = 3.5f;
        [SerializeField] private float _heightOffset = 0.0f;

        [SerializeField] public Transform _stageRoot;
        private Vector3 _startStagePosition;
        private Quaternion _startStageRotation;
        private Vector3 _startStageScale;

        [SerializeField] public Transform _selectNextSpeakerCylinder;
        [SerializeField] public MeshRenderer _selectNextRenderer;
        private IReadOnlyList<RoleRig> _roles;

        public Transform XrHead;

        public Transform XrOrigin;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            this._roles = roles;
            if (_stageRoot != null)
            {
                _startStagePosition = _stageRoot.position;
                _startStageRotation = _stageRoot.rotation;
                _startStageScale = _stageRoot.localScale;
            }
                    
        }

        public void SelectNextCylinderVisible(bool visible)
        {
            //Debug.Log("[SelectNextCylinderVisible] was called.");
            _selectNextRenderer.enabled=visible;
        }


        public void MoveXrOriginBackFromStage()
        {
            

            if (XrOrigin == null || _stageRoot == null)
            {
                Debug.LogError("[MoveXrOriginBackFromStage] XrOrigin or _stageRoot is null.");
                return;
            }

            Vector3 stagePos = _stageRoot.position;
            Vector3 originPos = XrOrigin.position;

            Vector3 dir = originPos - stagePos;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = -_stageRoot.forward;
                dir.y = 0f;
            }

            dir.Normalize();

            Vector3 targetPos = stagePos + dir * _distanceFromStage;
            targetPos.y = originPos.y;

            XrOrigin.position = targetPos;

            // Optional: zur Stage schauen
            Vector3 lookDir = stagePos - XrOrigin.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.0001f)
            {
                XrOrigin.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }

        public void SetCylinderToSelected(int roleIndex)
        {

            //Debug.Log("[SetCylinderToSelection] was called.");
            _selectNextSpeakerCylinder.localPosition = _roles[roleIndex].avatarRoot.localPosition;
        }
    }
}
