using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class ProceduralHipSolver : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform head;
        [SerializeField] private float hipHeight = 0.95f;
        [SerializeField] private float hipBackOffset = 0.08f;
        [SerializeField] private float smoothSpeed = 12f;

        public void ApplySolver(float dt)
        {
            if (bodyRoot == null)
                return;

            Vector3 targetPos = bodyRoot.position + Vector3.up * hipHeight;
            targetPos -= bodyRoot.forward * hipBackOffset;

            Quaternion targetRot = bodyRoot.rotation;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Exp(-smoothSpeed * dt)
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                1f - Mathf.Exp(-smoothSpeed * dt)
            );
        }
    }
}