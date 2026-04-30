using UnityEngine;
using AppV2.Runtime.Scripts.Rig;

namespace AppV2.Runtime.Scripts.Rig
{
    public class RoleRigUpdatePipeline : MonoBehaviour
    {
        [SerializeField] private VisualRigFollower visualRigFollower;
        [SerializeField] private AvatarRigFollower avatarRigFollower;

        private void Awake()
        {
            if (visualRigFollower == null)
                visualRigFollower = GetComponentInChildren<VisualRigFollower>(true);

            if (avatarRigFollower == null)
                avatarRigFollower = GetComponentInChildren<AvatarRigFollower>(true);
        }

        private void LateUpdate()
        {
            if (visualRigFollower != null)
                visualRigFollower.ApplyFollow();

            if (avatarRigFollower != null)
                avatarRigFollower.ApplyFollow();
        }
    }
}