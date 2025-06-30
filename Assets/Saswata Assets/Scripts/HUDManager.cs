using UnityEngine;
using Ashsvp;

public class HUDManager : MonoBehaviour
{
    [Header("References")]
    public GearSystem gearSystem;
    public RectTransform speedNeedleTransform;
    public RectTransform reactiveHUDGroup; // Assign your ReactiveHUDGroup here

    [Header("Speedometer Settings")]
    public float minAngle = 0f;
    public float maxAngle = -270f;
    public float maxSpeed = 180f;

    [Header("Motion Settings (NFS Heat Feel)")]
    public float maxXOffset = 10f; // side sway
    public float maxYOffset = 6f;  // vertical bump
    public float maxZOffset = 5f;  // forward/back push
    public float motionSmoothing = 5f;

    private Vector2 hudOffsetVelocity;
    private Vector2 currentOffset;

    private Transform carTransform;
    private Rigidbody carRigidbody;

    private void Start()
    {
        if (gearSystem != null)
        {
            carTransform = gearSystem.transform;
            carRigidbody = carTransform.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        UpdateSpeedometer();
        UpdateHUDMotion();
    }

    private void UpdateSpeedometer()
    {
        if (gearSystem == null || speedNeedleTransform == null)
            return;

        float speed = Mathf.Clamp(gearSystem.VehicleSpeed, 0f, maxSpeed);
        float normalizedSpeed = speed / maxSpeed;

        float angle = Mathf.Lerp(minAngle, maxAngle, normalizedSpeed);
        speedNeedleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void UpdateHUDMotion()
    {
        if (reactiveHUDGroup == null || carRigidbody == null || carTransform == null)
            return;

        // Convert velocity to local space
        Vector3 localVel = carTransform.InverseTransformDirection(carRigidbody.linearVelocity);
        Vector3 angularVel = carRigidbody.angularVelocity;

        // X = side sway (based on turning)
        float swayX = Mathf.Clamp(-angularVel.y * maxXOffset, -maxXOffset, maxXOffset);

        // Y = vertical bob (based on bumps or slope)
        float bobY = Mathf.Clamp(localVel.y * 0.5f, -maxYOffset, maxYOffset);

        // Z = forward/back movement (based on accel/brake)
        float thrustZ = Mathf.Clamp(-localVel.z * 0.2f, -maxZOffset, maxZOffset);

        // Combine into offset
        Vector2 targetOffset = new Vector2(swayX, bobY + thrustZ);

        // Smoothly interpolate the motion
        currentOffset = Vector2.SmoothDamp(currentOffset, targetOffset, ref hudOffsetVelocity, 1f / motionSmoothing);

        // Apply to RectTransform
        reactiveHUDGroup.anchoredPosition = currentOffset;
    }
}
