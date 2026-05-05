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

            //UnityEngine.Debug.Log($"SetVisible was called for visible={visible} ---- Turn");
        
        
            renderersToToggle = GetComponentsInChildren<Renderer>(true);
                //UnityEngine.Debug.Log($"RenderersToToggle.Length ={renderersToToggle?.Length} ---- Turn");

            
            for (int i = 0; i < renderersToToggle.Length; i++)
            {
                //UnityEngine.Debug.Log($"SetVisible was called for i={i} ---- Turn");
                if (renderersToToggle[i] != null)
                    renderersToToggle[i].enabled = visible;
            }
        }
    }

}

