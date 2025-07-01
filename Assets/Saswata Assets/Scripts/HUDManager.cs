using UnityEngine;
using Ashsvp;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("References")]
    public GearSystem gearSystem;
    public RectTransform speedNeedleTransform;
    public RectTransform reactiveHUDGroup; // Drag your ReactiveHUDGroup here

    [Header("Lap Counter")]
    public TextMeshProUGUI lapText;
    public AIRaceManager raceManager;

    [Header("Speedometer Settings")]
    public float minAngle = 0f;           // Needle at 0 speed (top)
    public float maxAngle = -270f;        // Needle at max speed (left)
    public float maxSpeed = 180f;         // Max speed on your dial

    [Header("Motion Settings (NFS Heat Style)")]
    public float maxXOffset = 10f;        // Side sway
    public float maxYOffset = 4f;         // Bump motion
    public float maxZOffset = 6f;         // Forward/backward motion
    public float motionSmoothing = 4f;    // How smoothly HUD moves

    [Header("Shake Settings")]
    public float shakeSpeedThreshold = 160f; // Start shaking above this speed
    public float shakeMultiplier = 0.2f;     // Shake intensity multiplier
    public float maxShakeAmount = 5f;        // Cap for shake

    private Vector2 currentOffset;
    private Vector2 hudOffsetVelocity;

    private Transform carTransform;
    private Rigidbody carRigidbody;

    private void Start()
    {
        if (gearSystem != null)
        {
            carTransform = gearSystem.transform;
            carRigidbody = carTransform.GetComponent<Rigidbody>();
        }

        // Assign AIRaceManager from object named "RaceManager"
        GameObject raceManagerObj = GameObject.Find("RaceManager");
        if (raceManagerObj != null)
        {
            raceManager = raceManagerObj.GetComponent<AIRaceManager>();
        }
        else
        {
            Debug.LogWarning("[HUDManager] RaceManager GameObject not found in scene.");
        }
    }


    private void Update()
    {
        UpdateSpeedometer();
        UpdateHUDMotion();
        UpdateLapDisplay();

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

        float speed = gearSystem.VehicleSpeed;
        Vector3 localVel = carTransform.InverseTransformDirection(carRigidbody.linearVelocity);
        Vector3 angularVel = carRigidbody.angularVelocity;

        // Motion intensity based on speed (normalized)
        float speedFactor = Mathf.InverseLerp(0f, gearSystem.gearSpeeds[^1], speed);

        // Reactive offsets
        float swayX = Mathf.Clamp(-angularVel.y * maxXOffset * speedFactor * 1.5f, -maxXOffset, maxXOffset);
        float bobY = Mathf.Clamp(localVel.y * maxYOffset * speedFactor, -maxYOffset, maxYOffset);
        float thrustZ = Mathf.Clamp(-localVel.z * 0.3f * speedFactor, -maxZOffset, maxZOffset);

        // Smooth target offset
        Vector2 targetOffset = new Vector2(swayX, bobY + thrustZ);
        currentOffset = Vector2.SmoothDamp(currentOffset, targetOffset, ref hudOffsetVelocity, 1f / motionSmoothing);

        // Add shake if speed is high
        Vector2 shakeOffset = Vector2.zero;
        if (speed > shakeSpeedThreshold)
        {
            float shakeAmount = (speed - shakeSpeedThreshold) * shakeMultiplier;
            shakeAmount = Mathf.Min(shakeAmount, maxShakeAmount);
            shakeOffset = Random.insideUnitCircle * shakeAmount;
        }

        // Apply final offset
        reactiveHUDGroup.anchoredPosition = currentOffset + shakeOffset;
    }
    private void UpdateLapDisplay()
    {
        if (lapText == null || raceManager == null || raceManager.playerState == null)
            return;

        int currentLap = raceManager.playerState.currentLap;
        int totalLaps = raceManager.numberOfLaps;

        // Clamp to avoid showing "Lap 4 / 3" if finished
        currentLap = Mathf.Min(currentLap, totalLaps);

        lapText.text = $"Lap {currentLap} / {totalLaps}";
    }

}
