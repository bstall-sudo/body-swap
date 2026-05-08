using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IKFootSolver : MonoBehaviour
{
    public bool isMovingForward;

    [SerializeField] LayerMask terrainLayer = default;
    [SerializeField] Transform body = default;
    [SerializeField] IKFootSolver otherFoot = default;
    [SerializeField] float speed = 4;
    [SerializeField] float stepDistance = .2f;
    [SerializeField] float stepLength = .2f;
    [SerializeField] float sideStepLength = .1f;

    [SerializeField] float stepHeight = .3f;
    [SerializeField] Vector3 footOffset = default;

    public Vector3 footRotOffset;
    public float footYPosOffset = 0.1f;

    public float rayStartYOffset = 1.0f;
    public float rayLength = 3.0f;
    
    float footSpacing;
    Vector3 oldPosition, currentPosition, newPosition;
    Vector3 oldNormal, currentNormal, newNormal;
    float lerp;

    private void Start()
    {
        footSpacing = Vector3.Dot(transform.position - body.position, body.right);

        Ray ray = new Ray(transform.position + Vector3.up * 1.0f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit info, 3.0f, terrainLayer))
        {
            currentPosition = newPosition = oldPosition = info.point + footOffset;
            currentNormal = newNormal = oldNormal = info.normal;
        }
        else
        {
            currentPosition = newPosition = oldPosition = transform.position;
            currentNormal = newNormal = oldNormal = Vector3.up;

            Debug.LogWarning($"{name}: No ground found on Start()");
        }

        lerp = 1;
    }

    // Update is called once per frame

    void Update()
    {
        transform.position = currentPosition + Vector3.up * footYPosOffset;
        transform.localRotation = Quaternion.Euler(footRotOffset);

        Ray ray = new Ray(transform.position + Vector3.up, Vector3.down);

        Debug.DrawRay(transform.position + Vector3.up, Vector3.down * rayLength, Color.red);
            
        if (Physics.Raycast(ray, out RaycastHit info, rayLength, terrainLayer.value))
        {
            Debug.Log("Raycast");
            if (Vector3.Distance(newPosition, info.point) > stepDistance && !otherFoot.IsMoving() && lerp >= 1)
            {
                oldPosition = currentPosition;
                oldNormal = currentNormal;

                lerp = 0;

                Vector3 direction = Vector3.ProjectOnPlane(info.point - currentPosition, Vector3.up).normalized;

                float angle = Vector3.Angle(body.forward, direction);
                isMovingForward = angle < 50 || angle > 130;

                if (isMovingForward)
                {
                    newPosition = info.point + direction * stepLength + footOffset;
                    newNormal = info.normal;
                }
                else
                {
                    newPosition = info.point + direction * sideStepLength + footOffset;
                    newNormal = info.normal;
                }
            }
        }

        if (lerp < 1)
        {
            Vector3 tempPosition = Vector3.Lerp(oldPosition, newPosition, lerp);
            tempPosition.y += Mathf.Sin(lerp * Mathf.PI) * stepHeight;

            currentPosition = tempPosition;
            currentNormal = Vector3.Lerp(oldNormal, newNormal, lerp);

            lerp += Time.deltaTime * speed;
        }
        else
        {
            currentPosition = newPosition;
            currentNormal = newNormal;

            oldPosition = newPosition;
            oldNormal = newNormal;
        }

        transform.position = currentPosition + Vector3.up * footYPosOffset;
    }

    private void OnDrawGizmos()
    {

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(newPosition, 0.1f);
    }



    public bool IsMoving()
    {
        return lerp < 1;
    }



}
