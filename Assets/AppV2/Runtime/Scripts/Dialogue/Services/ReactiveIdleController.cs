using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Rig;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class ReactiveIdleController
    {
        private IReadOnlyList<RoleRig> _roles;
        
        //AnimateBlendShapesViaAudioSources wird gebraucht, um festzustellen, welche Rolle am lautesten spricht,
        // um das lookAtTarget entsprechend zu positionieren
        private AnimateBlendShapesViaAudioSources _audioBlendShapeService;
        private readonly Dictionary<int, Vector3> _currentLookPositions = new();

        private int _currentLoudestRoleIndex = -1;
        private float _loudestSpeakerTimer = 0f;
        private float loudestSpeakerUpdateInterval = 1.0f;

        private float lookAtSmoothSpeed = 4.5f;
        private float minSpeakerVolume = 0.005f;

        public void Initialize(List<RoleRig> roles, AnimateBlendShapesViaAudioSources audioBlendShapeService)
        {
            _roles = roles;
            _audioBlendShapeService = audioBlendShapeService;
        }

        public void SetRoleToIdleLookingAt(int idleRoleIndex, int speakerRoleIndex, bool seatedMode)
        {
            if (!IsValidRoleIndex(idleRoleIndex)) return;
            if (!IsValidRoleIndex(speakerRoleIndex)) return;
            if (idleRoleIndex == speakerRoleIndex) return;

            RoleRig idleRole = _roles[idleRoleIndex];
            RoleRig speakerRole = _roles[speakerRoleIndex];

            if (idleRole.avatar == null)
            {
                UnityEngine.Debug.LogWarning($"[ReactiveIdleController] Role {idleRoleIndex} has no avatar.");
                return;
            }

            if (speakerRole.head == null)
            {
                UnityEngine.Debug.LogWarning($"[ReactiveIdleController] Speaker role {speakerRoleIndex} has no head.");
                return;
            }

            idleRole.avatar.SetRigModeIdle();
            idleRole.avatar.PlayIdleAnimation(idleRole.sittingIdle || seatedMode);
            idleRole.avatar.SetLookAtTargetWorldPosition(speakerRole.head.position);

        }

        public void SetRoleToRecordPlayback(int roleIndex, bool seatedMode)
        {
            if (!IsValidRoleIndex(roleIndex)) return;

            RoleRig role = _roles[roleIndex];

            if (role.avatar == null)
                return;

            role.avatar.SetRigModeRecordPlayback();

            AvatarBasePose pose =
                (seatedMode || role.sittingIdle)
                    ? AvatarBasePose.SittingIdle
                    : AvatarBasePose.TPose;
            //UnityEngine.Debug.Log($"[ReactiveIdleController] Speaker{roleIndex} Posename is: {pose}.");
            role.avatar.PlayBasePose(pose);
        }

        //Das hier ist die alte Methode, welche das LookAtTarget einfach auf die Rolle richtet,
        // welche gerade aufgenommen wird, -> also auf das aktive XR-Rig, das ist aber komisch, 
        // wenn mehrere Figuren auf dem Platz stehen und man immer angesehen wird, auch wenn gerade jemand
        // anderes spricht.
        public void UpdateIdleLookTargets(List<int> reactiveIdles, int speakerRoleIndex)
        {
            if (!IsValidRoleIndex(speakerRoleIndex)) return;

            Transform speakerHead = _roles[speakerRoleIndex].head;

            //UnityEngine.Debug.Log($"speakerhead position is: {speakerHead.position}");
            if (speakerHead == null) return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int idleRoleIndex = reactiveIdles[i];

                if (!IsValidRoleIndex(idleRoleIndex)) continue;

                RoleRig idleRole = _roles[idleRoleIndex];

                if (idleRole.avatar == null) continue;

                idleRole.avatar.SetLookAtTargetWorldPosition(speakerHead.position);
            }
        }

        
        private bool IsValidRoleIndex(int index)
        {
            return _roles != null && index >= 0 && index < _roles.Count;
        }



        private int FindLoudestSpeakingRole(List<int> speakerIndices)
        {
            int loudestRoleIndex = -1;
            float loudestVolume = minSpeakerVolume;

            if (speakerIndices == null)
                return loudestRoleIndex;

            for (int i = 0; i < speakerIndices.Count; i++)
            {
                int roleIndex = speakerIndices[i];

                if (!IsValidRoleIndex(roleIndex))
                    continue;

                float volume = _audioBlendShapeService.GetVolumeForRole(roleIndex);

                if (volume > loudestVolume)
                {
                    loudestVolume = volume;
                    loudestRoleIndex = roleIndex;
                }
            }

            return loudestRoleIndex;
        }

        //diese Funktion verbraucht zu viel Performance, weil in jedem Update Geräuschpegelverglichen werden.
        public void UpdateIdleLookTargetsToLoudestSpeaker(
        List<int> reactiveIdles,
        List<int> playbacks,
        int activePlayerIndex,
        float dt)
        {
            if (_roles == null || reactiveIdles == null || _audioBlendShapeService == null)
                return;

            if (reactiveIdles.Count == 0)
                return;

            List<int> indicesOfCurrentSpeakers = playbacks != null
                ? new List<int>(playbacks)
                : new List<int>();

            if (IsValidRoleIndex(activePlayerIndex) && !indicesOfCurrentSpeakers.Contains(activePlayerIndex))
                indicesOfCurrentSpeakers.Add(activePlayerIndex);

            int loudestRoleIndex = FindLoudestSpeakingRole(indicesOfCurrentSpeakers);

            // Fallback: Wenn niemand laut genug ist, schau auf den aktiven Sprecher.
            if (!IsValidRoleIndex(loudestRoleIndex))
                loudestRoleIndex = activePlayerIndex;

            if (!IsValidRoleIndex(loudestRoleIndex))
                return;

            SmoothLookAtForIdleRoles(reactiveIdles, loudestRoleIndex, dt);
        }

        public void UpdateIdleLookTargetsToLoudestSpeakerThrottled(
            List<int> reactiveIdles,
            List<int> playbacks,
            int activePlayerIndex,
            float dt)
        {
            if (_roles == null || reactiveIdles == null)
                return;

            if (reactiveIdles.Count == 0)
                return;

            _loudestSpeakerTimer -= dt;

            if (_loudestSpeakerTimer <= 0f)
            {
                _loudestSpeakerTimer = loudestSpeakerUpdateInterval;

                List<int> speakers = playbacks != null
                    ? new List<int>(playbacks)
                    : new List<int>();

                if (IsValidRoleIndex(activePlayerIndex) && !speakers.Contains(activePlayerIndex))
                    speakers.Add(activePlayerIndex);

                int loudest = FindLoudestSpeakingRole(speakers);

                if (IsValidRoleIndex(loudest))
                    _currentLoudestRoleIndex = loudest;
                else
                    _currentLoudestRoleIndex = activePlayerIndex;
            }

            if (!IsValidRoleIndex(_currentLoudestRoleIndex))
                return;

            SmoothLookAtForIdleRoles(
                reactiveIdles,
                _currentLoudestRoleIndex,
                dt
            );
        }

        private void SmoothLookAtForIdleRoles(List<int> reactiveIdles, int targetRoleIndex, float dt)
        {
            Transform targetHead = _roles[targetRoleIndex].head;
            if (targetHead == null)
                return;

            for (int i = 0; i < reactiveIdles.Count; i++)
            {
                int idleRoleIndex = reactiveIdles[i];

                if (!IsValidRoleIndex(idleRoleIndex))
                    continue;

                if (idleRoleIndex == targetRoleIndex)
                    continue;

                RoleRig idleRole = _roles[idleRoleIndex];

                if (idleRole.avatar == null || idleRole.avatar.LookAtTarget == null)
                    continue;

                if (!_currentLookPositions.TryGetValue(idleRoleIndex, out Vector3 currentPosition))
                    currentPosition = idleRole.avatar.LookAtTarget.position;

                Vector3 targetPosition = targetHead.position;

                float t = 1f - Mathf.Exp(-lookAtSmoothSpeed * dt);
                Vector3 smoothedPosition = Vector3.Lerp(currentPosition, targetPosition, t);

                _currentLookPositions[idleRoleIndex] = smoothedPosition;

                idleRole.avatar.SetLookAtTargetWorldPosition(smoothedPosition);
            }
        }
    }
}