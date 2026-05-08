using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class AnimateBlendShapesViaAudioSources
    {
        private IReadOnlyList<RoleRig> _roles;

        private readonly float[] _samples = new float[256];

        [SerializeField] private float _sensitivity = 1200f;
        [SerializeField] private float _smoothSpeed = 12f;

        // 5x pro Sekunde
        [SerializeField] private float _volumeUpdateInterval = 0.05f;

        private float _volumeTimer = 0f;

        private float[] _cachedVolumes;
        private float[] _currentMouthWeights;
        private float[] _targetMouthWeights;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            _roles = roles;

            _cachedVolumes = new float[roles.Count];
            _currentMouthWeights = new float[roles.Count];
            _targetMouthWeights = new float[roles.Count];
        }

        public void Tick(float dt, List<int> roleIndices)
        {
            if (_roles == null || roleIndices == null)
                return;

            // -------------------------
            // Lautstärke nur 5x/Sekunde
            // -------------------------

            _volumeTimer -= dt;

            if (_volumeTimer <= 0f)
            {
                _volumeTimer = _volumeUpdateInterval;

                UpdateVolumesAndTargets(roleIndices);
            }

            // -------------------------
            // Smooth jede Frame
            // -------------------------

            for (int i = 0; i < roleIndices.Count; i++)
            {
                SmoothAnimateMouth(roleIndices[i], dt);
            }
        }

        private void UpdateVolumesAndTargets(List<int> roleIndices)
        {
            for (int i = 0; i < roleIndices.Count; i++)
            {
                int roleIndex = roleIndices[i];

                if (roleIndex < 0 || roleIndex >= _roles.Count)
                    continue;

                RoleRig role = _roles[roleIndex];

                if (role.audioSource == null || role.avatar == null)
                    continue;

                float volume = GetAudioVolume(role.audioSource);

                _cachedVolumes[roleIndex] = volume;

                float targetWeight =
                    Mathf.Clamp01(volume * _sensitivity) * 100f;

                if (volume < 0.005f)
                    targetWeight = 0f;

                _targetMouthWeights[roleIndex] = targetWeight;
            }
        }

        private void SmoothAnimateMouth(int roleIndex, float dt)
        {
            if (roleIndex < 0 || roleIndex >= _roles.Count)
                return;

            RoleRig role = _roles[roleIndex];

            if (role.avatar == null)
                return;

            _currentMouthWeights[roleIndex] = Mathf.Lerp(
                _currentMouthWeights[roleIndex],
                _targetMouthWeights[roleIndex],
                1f - Mathf.Exp(-_smoothSpeed * dt)
            );

            role.avatar.AnimateMouth(
                _currentMouthWeights[roleIndex]
            );
        }

        private float GetAudioVolume(AudioSource source)
        {
            if (source == null || !source.isPlaying)
                return 0f;

            source.GetOutputData(_samples, 0);

            float sum = 0f;

            for (int i = 0; i < _samples.Length; i++)
            {
                sum += _samples[i] * _samples[i];
            }

            return Mathf.Sqrt(sum / _samples.Length);
        }

        // Vom ReactiveIdleController genutzt
        public float GetVolumeForRole(int roleIndex)
        {
            if (_cachedVolumes == null)
                return 0f;

            if (roleIndex < 0 || roleIndex >= _cachedVolumes.Length)
                return 0f;

            return _cachedVolumes[roleIndex];
        }
    }
}