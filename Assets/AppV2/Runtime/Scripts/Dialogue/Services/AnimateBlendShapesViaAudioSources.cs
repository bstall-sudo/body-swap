using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class AnimateBlendShapesViaAudioSources
    {
        private IReadOnlyList<RoleRig> _roles;

        private readonly float[] _samples = new float[256];

        private string _mouthOpenNamePart = "open";
        private float _sensitivity = 1200f;
        private float _smoothSpeed = 12f;

        private float[] _currentMouthWeights;

        public void Initialize(IReadOnlyList<RoleRig> roles)
        {
            _roles = roles;
            _currentMouthWeights = new float[roles.Count];
        }

        public void Tick(float dt, List<int> roleIndices)
        {
            if (_roles == null) return;

            for (int i = 0; i < roleIndices.Count; i++)
            {
                AnimateMouthForRole(roleIndices[i], dt);
            }
        }

        private void AnimateMouthForRole(int roleIndex, float dt)
        {
            RoleRig role = _roles[roleIndex];

            if (role.audioSource == null || role.avatar == null)
                return;

            float volume = GetAudioVolume(role.audioSource);

            float targetWeight = Mathf.Clamp01(volume * _sensitivity) * 100f;

            if (volume < 0.005f) targetWeight = 0f;

            _currentMouthWeights[roleIndex] = Mathf.Lerp(
                _currentMouthWeights[roleIndex],
                targetWeight,
                1f - Mathf.Exp(-_smoothSpeed * dt)
            );

            role.avatar.SetFirstHeadBlendShapeContaining(
                _mouthOpenNamePart,
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
    }
}