using UnityEngine;

using AppV2.Runtime.Scripts.Dialogue;

namespace AppV2.Runtime.Scripts.Rig
{
    public class ProceduralFootSolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private ProceduralFootSolver otherFoot;

        [Header("Objekt mit ConversationStage Script Komponente")]
        private ConversationStage _conversationStage;

        [Header("Ground")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float rayStartHeight = 1.0f;
        [SerializeField] private float rayLength = 3.0f;

        [Header("Foot placement")]
        [SerializeField] private float stepDistance = 0.28f;
        [SerializeField] private float stepLength = 0.22f;
        [SerializeField] private float sideStepLength = 0.12f;
        [SerializeField] private float stepHeight = 0.12f;
        [SerializeField] private float stepSpeed = 10.0f;

        [Header("Step stability")]
        [SerializeField] private float stepTriggerDelay = 0.15f;
        [SerializeField] private float stepCooldown = 0.35f;
        [SerializeField] private float minBodyMovementForStep = 0.03f;

        [Tooltip("Body movements smaller than this are ignored for step triggering.")]
        [SerializeField] private float bodyMovementThreshold = 0.025f;

        [SerializeField] private float walkingSpeedThreshold = 0.15f;





        private float _stepTriggerTimer;
        private float _stepCooldownTimer;

        private Vector3 _lastBodyPosition;
        private Vector3 _bodyVelocity;

        [Header("Offsets")]
        [SerializeField] private Vector3 footOffset = Vector3.zero;
        [SerializeField] private Vector3 footRotationOffset = Vector3.zero;
        [SerializeField] private float footYPosOffset = 0.02f;

        private float _footSpacing;
        private float _lerp = 1f;

    
        private Vector3 _stableBodyPosition;

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

            _conversationStage = FindFirstObjectByType<ConversationStage>();

            stepSpeed = _conversationStage.ProceduralStepSpeed;

            stepLength = _conversationStage.ProceduralStepLength;


            _lastBodyPosition = bodyRoot.position;

            _footSpacing = Vector3.Dot(
                transform.position - bodyRoot.position,
                bodyRoot.right
            );

            _lastBodyPosition = bodyRoot.position;
            _stableBodyPosition = bodyRoot.position;

            Vector3 start =
                bodyRoot.position +
                bodyRoot.right * _footSpacing +
                Vector3.up * rayStartHeight;

            if (Physics.Raycast(
                    start,
                    Vector3.down,
                    out RaycastHit hit,
                    rayLength,
                    groundLayer))
            {
                _currentPosition = _newPosition = _oldPosition =
                    hit.point + footOffset;

                _currentNormal = _newNormal = _oldNormal =
                    hit.normal;
            }
            else
            {
                _currentPosition = _newPosition = _oldPosition =
                    transform.position;

                _currentNormal = _newNormal = _oldNormal =
                    Vector3.up;

                Debug.LogWarning(
                    $"[{name}] No ground found on Start(). Check collider/layer."
                );
            }

            
            ApplyPose();
        }

        public void ApplySolver(float dt)
        {
            if (bodyRoot == null)
                return;

            // Body movement berechnen
            Vector3 bodyDelta = bodyRoot.position - _lastBodyPosition;
            bodyDelta.y = 0f;

            _bodyVelocity = bodyDelta / Mathf.Max(dt, 0.0001f);
            _lastBodyPosition = bodyRoot.position;

            if (_stepCooldownTimer > 0f)
                _stepCooldownTimer -= dt;

            // ---------------------------------------------------------
            // BODY INERTIA
            // Kleine Body-Bewegungen ignorieren.
            // Nur XZ ist relevant.
            // ---------------------------------------------------------

            Vector3 bodyMovement =
                bodyRoot.position - _stableBodyPosition;

            bodyMovement.y = 0f;

            if (bodyMovement.magnitude >= bodyMovementThreshold)
            {
                _stableBodyPosition = bodyRoot.position;
            }

            _lastBodyPosition = bodyRoot.position;

            // ---------------------------------------------------------
            // Gewünschte Fußposition basiert auf der stabilisierten
            // Body-Position.
            // ---------------------------------------------------------

            Vector3 desiredFootBase =
                _stableBodyPosition +
                bodyRoot.right * _footSpacing;

            Vector3 rayOrigin =
                desiredFootBase +
                Vector3.up * rayStartHeight;

            Debug.DrawRay(
                rayOrigin,
                Vector3.down * rayLength,
                Color.red
            );

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    rayLength,
                    groundLayer))
            {
                bool canStep =
                    _lerp >= 1f &&
                    (otherFoot == null || !otherFoot.IsMoving);

                float distance =
                    Vector3.Distance(
                        _newPosition,
                        hit.point + footOffset
                    );

                // -----------------------------------------------------
                // STEP INERTIA
                // Der Fuß muss eine gewisse Zeit außerhalb der
                // erlaubten Distanz sein.
                // -----------------------------------------------------

                Vector3 footToTarget = hit.point + footOffset - _newPosition;
                footToTarget.y = 0f;

                

                bool bodyIsMoving =
                    _bodyVelocity.magnitude > minBodyMovementForStep;

                bool movingTowardTarget = false;

                if (distance > 0.001f && bodyIsMoving)
                {
                    Vector3 targetDirection = footToTarget.normalized;
                    Vector3 bodyDirection = _bodyVelocity.normalized;

                    movingTowardTarget =
                        Vector3.Dot(bodyDirection, targetDirection) > 0.2f;
                }
                float bodySpeed = _bodyVelocity.magnitude;

                bool isWalking =        bodySpeed > walkingSpeedThreshold;

                bool wantsToStep =
                    distance > stepDistance &&
                    bodyIsMoving &&
                    movingTowardTarget;

                if (canStep && wantsToStep)
                {
                    if (isWalking)
                    {
                        // Beim echten Laufen sofort reagieren
                        StartStep(hit);

                        _stepTriggerTimer = 0f;
                        _stepCooldownTimer = 0f;
                    }
                    else if (_stepCooldownTimer <= 0f)
                    {
                        // Bei kleinen Körperbewegungen vorsichtig sein
                        _stepTriggerTimer += dt;

                        if (_stepTriggerTimer >= stepTriggerDelay)
                        {
                            StartStep(hit);

                            _stepTriggerTimer = 0f;
                            _stepCooldownTimer = stepCooldown;
                        }
                    }
                }
                else
                {
                    _stepTriggerTimer = 0f;
                }
            }
            else
            {
                _stepTriggerTimer = 0f;
            }

            // ---------------------------------------------------------
            // STEP ANIMATION
            // ---------------------------------------------------------

            if (_lerp < 1f)
            {
                Vector3 p =
                    Vector3.Lerp(
                        _oldPosition,
                        _newPosition,
                        _lerp
                    );

                p.y +=
                    Mathf.Sin(_lerp * Mathf.PI) *
                    stepHeight;

                _currentPosition = p;

                _currentNormal =
                    Vector3.Lerp(
                        _oldNormal,
                        _newNormal,
                        _lerp
                    );

                float currentStepSpeed =
                _conversationStage != null
                    ? _conversationStage.ProceduralStepSpeed
                    : stepSpeed;

                _lerp += dt * currentStepSpeed;
            }
            else
            {
                _lerp = 1f;

                _currentPosition = _newPosition;
                _currentNormal = _newNormal;

                _oldPosition = _newPosition;
                _oldNormal = _newNormal;
            }

            ApplyPose();
        }

        private void StartStep(RaycastHit hit)
        {
            _oldPosition = _currentPosition;
            _oldNormal = _currentNormal;

            _lerp = 0f;

            Vector3 direction =
                Vector3.ProjectOnPlane(
                    hit.point - _currentPosition,
                    Vector3.up
                );

            if (direction.sqrMagnitude > 0.0001f)
                direction.Normalize();
            else
                direction = bodyRoot.forward;

            float angle =
                Vector3.Angle(
                    bodyRoot.forward,
                    direction
                );

            bool forwardStep =
                angle < 50f ||
                angle > 130f;

            float length =
                forwardStep
                    ? stepLength
                    : sideStepLength;

            float movementSpeed = _bodyVelocity.magnitude;

            float stepLengthFactor = Mathf.InverseLerp(
                0.05f,
                0.5f,
                movementSpeed
            );

            float actualStepLength =
                length * stepLengthFactor;

            _newPosition =
                hit.point +
                direction * actualStepLength +
                footOffset;

            _newNormal = hit.normal;
        }

        private void ApplyPose()
        {
            transform.position =
                _currentPosition +
                Vector3.up * footYPosOffset;

            Quaternion groundTilt =
                Quaternion.FromToRotation(
                    Vector3.up,
                    _currentNormal
                );

            Quaternion offset =
                Quaternion.Euler(
                    footRotationOffset
                );

            transform.rotation =
                groundTilt *
                bodyRoot.rotation *
                offset;
        }

        public void ValidateSetup(string roleName)
        {
        }
    }
}