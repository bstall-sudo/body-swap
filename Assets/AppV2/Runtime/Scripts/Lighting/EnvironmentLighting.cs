using UnityEngine;
using UnityEngine.Rendering;

namespace AppV2.Runtime.Scripts.Lighting
{
    public class EnvironmentLighting : MonoBehaviour
    {
        [Header("Ambient Lighting")]
        [SerializeField] private Color ambientColor = Color.gray;
        [SerializeField] private float ambientIntensity = 1f;

        [Header("Skybox")]
        [SerializeField] private Material skyboxMaterial;

        private void OnEnable()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientIntensity = ambientIntensity;

            if (skyboxMaterial != null)
                RenderSettings.skybox = skyboxMaterial;

            DynamicGI.UpdateEnvironment();
        }
    }
}