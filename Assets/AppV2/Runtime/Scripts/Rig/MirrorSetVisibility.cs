using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class MirrorSetVisibility : MonoBehaviour
    {
        public void ActivateMirror(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(active);
            }
        }
    }
}
