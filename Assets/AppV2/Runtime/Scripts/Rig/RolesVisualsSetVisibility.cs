using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class RolesVisualsSetVisibility : MonoBehaviour
    {

        [Header("Visibility")]
        [SerializeField] private Renderer[] renderersToToggle;

        private void Awake()
        {

            if (renderersToToggle == null || renderersToToggle.Length == 0)
                renderersToToggle = GetComponentsInChildren<Renderer>(true);
        }

        public void SetVisible(bool visible)
        {
            if (renderersToToggle == null) return;

            for (int i = 0; i < renderersToToggle.Length; i++)
            {
                if (renderersToToggle[i] != null)
                    renderersToToggle[i].enabled = visible;
            }
        }
    }

}

