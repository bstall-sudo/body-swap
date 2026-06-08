using UnityEngine;

public class XRLocomotionToggle : MonoBehaviour
{
    [Header("Locomotion Providers")]
    [SerializeField] private Behaviour continuousMoveProvider;
    [SerializeField] private Behaviour continuousTurnProvider;
    [SerializeField] private Behaviour teleportationProvider;

    [Header("Teleport Visual/Input")]
    [SerializeField] private GameObject leftTeleportRay;
    [SerializeField] private GameObject rightTeleportRay;

    public void SetLocomotionEnabled(bool enabled)
    {
        if (continuousMoveProvider != null)
            continuousMoveProvider.enabled = enabled;

        if (continuousTurnProvider != null)
            continuousTurnProvider.enabled = enabled;

        if (teleportationProvider != null)
            teleportationProvider.enabled = enabled;

        if (leftTeleportRay != null)
            leftTeleportRay.SetActive(enabled);

        if (rightTeleportRay != null)
            rightTeleportRay.SetActive(enabled);
    }

    public void EnableLocomotion()
    {
        SetLocomotionEnabled(true);
    }

    public void DisableLocomotion()
    {
        SetLocomotionEnabled(false);
    }
}