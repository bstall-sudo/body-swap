using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace AppV2.Runtime.Scripts.Input
{
    public class HapticsService : MonoBehaviour
    {
        [SerializeField] private HapticImpulsePlayer leftHaptics;
        [SerializeField] private HapticImpulsePlayer rightHaptics;

        public void PulseBoth(float amplitude = 0.5f, float duration = 0.08f)
        {
            leftHaptics?.SendHapticImpulse(amplitude, duration);
            rightHaptics?.SendHapticImpulse(amplitude, duration);
        }

        public void Confirm()
        {
            PulseBoth(0.8f, 0.15f);
        }

        public void Click()
        {
            PulseBoth(0.25f, 0.05f);
        }

        public void Error()
        {
            PulseBoth(1f, 0.08f);
        }
    }
}