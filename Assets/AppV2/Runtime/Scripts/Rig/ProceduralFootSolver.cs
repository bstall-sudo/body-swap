using UnityEngine;

namespace AppV2.Runtime.Scripts.Rig
{
    public class ProceduralFootSolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private ProceduralFootSolver otherFoot;

        [Header("Ground")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float rayStartHeight = 1.0f;
        [SerializeField] private float rayLength = 3.0f;

        [Header("Foot placement")]
        [SerializeField] private float stepDistance = 0.28f;
        [SerializeField] private float stepLength = 0.22f;
        [SerializeField] private float sideStepLength = 0.12f;
        [SerializeField] private float stepHeight = 0.12f;
        [SerializeField] private float stepSpeed = 5.0f;

        [Header("Offsets")]
        [SerializeField] private Vector3 footOffset = Vector3.zero;
        [SerializeField] private Vector3 footRotationOffset = Vector3.zero;
        [SerializeField] private float footYPosOffset = 0.02f;

        private float _footSpacing;
        private float _lerp = 1f;

        private Vector3 _oldPosition;
        private Vector3 _currentPosition;
        private Vector3 _newPosition;

        private Vector3 _oldNormal = Vector3.up;
        private Vector3 _currentNormal = Vector3.up;
        private Vector3 _newNormal = Vector3.up;

        public bool IsMoving => _lerp < 1f;

        private void Start()
        {
            if (bodyRoot == null)
            {
                Debug.LogError($"[{name}] bodyRoot is missing.");
                return;
            }

            _footSpacing = Vector3.Dot(transform.position - bodyRoot.position, bodyRoot.right);

            Vector3 start = bodyRoot.position + bodyRoot.right * _footSpacing + Vector3.up * rayStartHeight;

            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                _currentPosition = _newPosition = _oldPosition = hit.point + footOffset;
                _currentNormal = _newNormal = _oldNormal = hit.normal;
            }
            else
            {
                _currentPosition = _newPosition = _oldPosition = transform.position;
                _currentNormal = _newNormal = _oldNormal = Vector3.up;

                Debug.LogWarning($"[{name}] No ground found on Start(). Check collider/layer.");
            }

            ApplyPose();
        }

        public void ApplySolver(float dt)
        {
            if (bodyRoot == null)
                return;

            Vector3 desiredFootBase =
                bodyRoot.position + bodyRoot.right * _footSpacing;

            Vector3 rayOrigin = desiredFootBase + Vector3.up * rayStartHeight;

            Debug.DrawRay(rayOrigin, Vector3.down * rayLength, Color.red);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
            {
                bool canStep =
                    _lerp >= 1f &&
                    (otherFoot == null || !otherFoot.IsMoving);

                float distance =
                    Vector3.Distance(_newPosition, hit.point + footOffset);

                if (canStep && distance > stepDistance)
                {
                    _oldPosition = _currentPosition;
                    _oldNormal = _currentNormal;

                    _lerp = 0f;

                    Vector3 direction = Vector3.ProjectOnPlane(
                        hit.point - _currentPosition,
                        Vector3.up
                    );

                    if (direction.sqrMagnitude > 0.0001f)
                        direction.Normalize();
                    else
                        direction = bodyRoot.forward;

                    float angle = Vector3.Angle(bodyRoot.forward, direction);
                    bool forwardStep = angle < 50f || angle > 130f;

                    float length = forwardStep ? stepLength : sideStepLength;

                    _newPosition = hit.point + direction * length + footOffset;
                    _newNormal = hit.normal;
                }
            }

            if (_lerp < 1f)
            {
                Vector3 p = Vector3.Lerp(_oldPosition, _newPosition, _lerp);
                p.y += Mathf.Sin(_lerp * Mathf.PI) * stepHeight;

                _currentPosition = p;
                _currentNormal = Vector3.Lerp(_oldNormal, _newNormal, _lerp);

                _lerp += dt * stepSpeed;
            }
            else
            {
                _currentPosition = _newPosition;
                _currentNormal = _newNormal;
                _oldPosition = _newPosition;
                _oldNormal = _newNormal;
            }

            ApplyPose();
        }

        private void ApplyPose()
        {
            transform.position = _currentPosition + Vector3.up * footYPosOffset;

            Quaternion groundTilt = Quaternion.FromToRotation(Vector3.up, _currentNormal);
            Quaternion offset = Quaternion.Euler(footRotationOffset);

            transform.rotation = groundTilt * bodyRoot.rotation * offset;
        }
    }
}