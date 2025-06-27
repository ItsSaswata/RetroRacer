using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ashsvp;
using Track;

[RequireComponent(typeof(SimcadeVehicleController))]
public class AIVehicleController : MonoBehaviour
{
    [Header("AI Configuration")]
    [SerializeField, Tooltip("Reference to the track generator that contains the racing line for the AI to follow")]
    public TrackGenerator trackGenerator;
    
    [SerializeField, Range(1, 20), Tooltip("How many points ahead on the racing line the AI will target. Higher values make the AI look further ahead and take smoother lines")]
    private int lookaheadPoints = 8;
    
    [SerializeField, Range(0f, 1f), Tooltip("Maximum steering angle the AI can use. Lower values make the AI take wider turns, higher values allow sharper turning")]
    private float maxSteeringAngle = 0.7f;
    
    [SerializeField, Range(0f, 1f), Tooltip("Maximum speed multiplier applied to the racing line's recommended speed. Higher values allow the AI to drive closer to the maximum possible speed")]
    private float maxSpeedMultiplier = 0.85f;
    
    [SerializeField, Range(0f, 1f), Tooltip("Minimum speed multiplier applied to the racing line's recommended speed. Higher values prevent the AI from driving too slowly")]
    private float minSpeedMultiplier = 0.6f;
    
    [SerializeField, Range(0f, 10f), Tooltip("How quickly the AI adjusts its steering. Higher values make steering more responsive but potentially less stable")]
    private float steeringSpeed = 2.5f;
    
    [SerializeField, Range(0f, 10f), Tooltip("How quickly the AI adjusts its acceleration/braking. Higher values make throttle control more responsive")]
    private float accelerationSpeed = 1.5f;
    
    [SerializeField, Tooltip("Whether the AI should use nitro on straight sections of the track")]
    private bool useNitroOnStraights = true;
    
    [SerializeField, Range(0f, 1f), Tooltip("Minimum recommended speed threshold for using nitro. Higher values mean nitro is only used on longer straights")]
    private float nitroThreshold = 0.85f;
    
  
    
    [SerializeField, Tooltip("Layer mask for sensor raycasts (should include the layer that cars are on)")]
    private LayerMask carDetectionLayerMask = -1;
    
    [Header("Corner Handling")]
    [SerializeField, Range(5, 30), Tooltip("How many points ahead the AI looks to detect corners. Higher values allow earlier corner detection")]
    private int cornerDetectionLookahead = 25;
    
    [SerializeField, Range(0.1f, 1f), Tooltip("How much the AI reduces speed in corners. Lower values cause more aggressive braking in corners")]
    private float cornerSpeedReductionFactor = 0.5f;
    
    [SerializeField, Range(0.05f, 0.5f), Tooltip("Minimum curvature threshold to consider a section as a corner. Lower values detect more subtle corners")]
    private float cornerDetectionThreshold = 0.12f;
    
    [SerializeField, Range(1f, 20f), Tooltip("Distance at which the AI starts braking for corners. Higher values make the AI brake earlier before corners")]
    private float brakingDistance = 15f;
    
    [SerializeField, Range(0.1f, 5f), Tooltip("Multiplier for braking intensity. Higher values make the AI brake more aggressively")]
    private float brakingIntensityMultiplier = 1.8f;
    

    
    // Difficulty settings
    [Header("Difficulty Settings")]
    [SerializeField, Range(0f, 1f), Tooltip("Overall driving skill of the AI. Higher values improve cornering precision, braking timing, and racing line following")]
    private float skillLevel = 0.75f;
    
    [SerializeField, Range(0f, 1f), Tooltip("How aggressively the AI drives. Higher values increase target speeds and make the AI take more risks")]
    private float aggressiveness = 0.6f;
    
    // Debug visualization
    [Header("Debug")]
    [SerializeField, Tooltip("Whether to show debug visualization for AI decision making")]
    private bool showDebugInfo = true;
    
    [SerializeField, Tooltip("Color used for visualizing the target point the AI is steering towards")]
    private Color targetPointColor = Color.blue;
    
    [SerializeField, Tooltip("Color used for visualizing the racing line path ahead")]
    private Color pathColor = Color.yellow;
    
    [Header("Humanization")]
    [SerializeField, Range(0f, 5f), Tooltip("How far (in meters) the AI can randomly deviate from the racing line.")]
    private float pathRandomness = 3.5f;

    [SerializeField, Range(0.1f, 5f), Tooltip("How quickly the AI's random path deviation changes over time.")]
    private float randomnessChangeRate = 0.2f;
     
    [Header("Overtaking")]
    [SerializeField, Tooltip("How long to be stuck behind a car before attempting to overtake.")]
    private float overtakeTriggerTime = 2.0f;
    [SerializeField, Tooltip("How far to the side to move when overtaking.")]
    private float overtakeLaneOffset = 3.0f;
    [SerializeField, Tooltip("The maximum corner sharpness where an overtake is allowed.")]
    private float maxOvertakeCornerFactor = 0.2f;
    [SerializeField, Tooltip("Time to wait after completing an overtake before starting another.")]
    private float overtakeCooldown = 3.0f;
    
    // Public properties for external management
    public bool IsRacing { get; set; } = false;
    public float RaceProgress { get; private set; }
    public float RubberBandingFactor { get; set; } = 1f;
    
    // Private variables
    private SimcadeVehicleController vehicleController;
    private RacingLine racingLine;
    private int currentWaypointIndex = 0;
    private float currentSteer = 0f;
    private float currentAcceleration = 0f;
    private float currentBrake = 0f;
    private float currentHandbrake = 0f;
    private List<AIVehicleController> otherVehicles = new List<AIVehicleController>();
    private float perlinSeed;
    private bool isOvertaking = false;
    private float timeStuck = 0f;
    private float overtakeCooldownTimer = 0f;
    private float targetOvertakeOffset = 0f;
    private float currentOvertakeOffset = 0f;
    private AIVehicleController carToOvertake = null;
    private float originalAcceleration;
    
    // Avoidance system state
    private float committedAvoidanceOffset = 0f;
    private float avoidanceCommitmentTimer = 0f;
    private float avoidanceCommitmentDuration = 1.5f;
    private float lastAvoidanceDecisionTime = 0f;
    private const float avoidanceDetectionRadius = 10f;
    private const float avoidanceAwarenessAngle = 120f; // degrees
    
    // Defensive driving state
    private bool isDefending = false;
    private float defenseTimer = 0f;
    private float defenseDuration = 0f;
    // Handbrake parameters (randomized per car)
    private float handbrakeStrength;
    private float handbrakeThresholdRandomized;
    
    [SerializeField]
    private bool isDrifty = false;
    
    // Nitro system for AI
    private float aiNitroCooldownTimer = 0f;
    private float aiNitroCooldownDuration = 0f;
    
    // Nitro slowdown state
    private float nitroSlowdownTimer = 0f;
    private float nitroSlowdownDuration = 2.5f; // seconds to enforce slowdown after nitro
    private bool isNitroSlowingDown = false;
    
    // Nitro at race start
    private bool forceStartNitro = false;
    private float startNitroTimer = 0f;
    private float startNitroDuration = 2f;
    
    private bool wasRacing = false;
    
    private void Awake()
    {
        vehicleController = GetComponent<SimcadeVehicleController>();
        
        // Always enable Auto Counter Steer for AI
        vehicleController.AutoCounterSteer = true;
        
        // Disable the camera for AI cars (if present)
        if (vehicleController.cinemachineCamera != null)
        {
            vehicleController.cinemachineCamera.gameObject.SetActive(false);
            // Optionally, also disable alternativeCamera if needed:
            // if (vehicleController.alternativeCamera != null) vehicleController.alternativeCamera.gameObject.SetActive(false);
        }
        
        // Find track generator if not assigned
        if (trackGenerator == null)
        {
            trackGenerator = FindFirstObjectByType<TrackGenerator>();
            if (trackGenerator == null)
            {
                Debug.LogError("No TrackGenerator found in scene. AI vehicle will not function properly.");
                enabled = false;
                return;
            }
        }
        
        // Get racing line from track generator
        racingLine = trackGenerator.RacingLine;
        if (racingLine == null || racingLine.Points.Count == 0)
        {
            Debug.LogError("No racing line found on track generator. AI vehicle will not function properly.");
            enabled = false;
            return;
        }
        
        // Find other AI vehicles in the scene
        AIVehicleController[] allVehicles = FindObjectsByType<AIVehicleController>(FindObjectsSortMode.None);
        foreach (var vehicle in allVehicles)
        {
            if (vehicle != this)
            {
                otherVehicles.Add(vehicle);
            }
        }
        
        // Ensure InputManager is enabled for AI control
        if (vehicleController.inputManager != null)
        {
            vehicleController.inputManager.enabled = true;
        }

        // Initialize a random seed for this vehicle to make its behavior unique
        perlinSeed = Random.Range(0f, 1000f);
        // Store the original acceleration value from the vehicle controller
        originalAcceleration = vehicleController.Acceleration;

        // Drifty/non-drifty switch
        
            handbrakeStrength = Random.Range(0.05f, 0.1f);
            handbrakeThresholdRandomized = Random.Range(0.12f, 0.19f);
            vehicleController.driftFactor = Random.Range(0.52f, 0.63f);
      
        Debug.Log($"[AI] {gameObject.name} isDrifty={isDrifty}, handbrakeStrength={handbrakeStrength:F2}, handbrakeThreshold={handbrakeThresholdRandomized:F2}, driftFactor={vehicleController.driftFactor:F2}");

        aiNitroCooldownDuration = Random.Range(22f, 30f);
        aiNitroCooldownTimer = aiNitroCooldownDuration;

        // Randomize nitro acceleration multiplier for this AI car
        vehicleController.nitroAccelerationMultiplier = Random.Range(1.01f, 1.05f);
        
        // Set higher turn angle for all AI cars
        vehicleController.MaxTurnAngle = 33f;
    }
    
    private void Start()
    {
        // Find initial position on racing line
        Vector3 localPosition = trackGenerator.transform.InverseTransformPoint(transform.position);
        (currentWaypointIndex, _, _) = racingLine.GetClosestPoint(localPosition);
        
        // Configure input manager for AI control
        if (vehicleController.inputManager != null)
        {
            // Ensure the InputManager is enabled
            vehicleController.inputManager.enabled = true;
            
            // Set initial AI inputs
            vehicleController.inputManager.SetAIInputs(0f, 0f, 0f, false);
        }
        
        // Refresh the list of other vehicles after a short delay to ensure all cars are spawned
        StartCoroutine(RefreshOtherVehiclesAfterDelay());
    }
    
    private IEnumerator RefreshOtherVehiclesAfterDelay()
    {
        yield return new WaitForSeconds(3f); // Wait 3 seconds for all cars to spawn
        RefreshOtherVehicles();
    }
    
    private void RefreshOtherVehicles()
    {
        otherVehicles.Clear();
        AIVehicleController[] allVehicles = FindObjectsByType<AIVehicleController>(FindObjectsSortMode.None);
        foreach (var vehicle in allVehicles)
        {
            if (vehicle != this && vehicle != null)
            {
                otherVehicles.Add(vehicle);
            }
        }
    }
    
    private void Update()
    {
        // Detect race start
        if (!wasRacing && IsRacing)
        {
            forceStartNitro = true;
            startNitroTimer = startNitroDuration;
        }
        wasRacing = IsRacing;

        if (!IsRacing)
        {
            // Before the race starts, keep the handbrake on to stay stationary.
            if(vehicleController.inputManager != null)
            {
                vehicleController.inputManager.SetAIInputs(0f, 0f, 1f, false);
            }
            return;
        }

        if (racingLine == null || racingLine.Points.Count == 0) return;
        
        // --- 1. Find our position and progress on the racing line ---
        Vector3 localPosition = trackGenerator.transform.InverseTransformPoint(transform.position);
        (currentWaypointIndex, _, _) = racingLine.GetClosestPoint(localPosition);
        
        if (racingLine.Points.Count > 0)
        {
            int nextWaypointIndex = (currentWaypointIndex + 1) % racingLine.Points.Count;
            float distanceToNext = Vector3.Distance(localPosition, racingLine.Points[nextWaypointIndex]);
            float segmentLength = Vector3.Distance(racingLine.Points[currentWaypointIndex], racingLine.Points[nextWaypointIndex]);
            
            float progressBetweenWaypoints = (segmentLength > 0) ? Mathf.Clamp01(1f - (distanceToNext / segmentLength)) : 0f;
            RaceProgress = (currentWaypointIndex + progressBetweenWaypoints) / racingLine.Points.Count;
        }

        // --- 2. Determine our ideal state (target point and speed) ---
        int adjustedLookahead = Mathf.RoundToInt(lookaheadPoints * (1f + skillLevel));
        (Vector3 targetPoint, float recommendedSpeed) = racingLine.GetNextTargetPoint(currentWaypointIndex, adjustedLookahead);
        float cornerFactor = DetectUpcomingCorners();
        
        float idealTargetSpeed = recommendedSpeed;
        if (cornerFactor > 0)
        {
            idealTargetSpeed *= Mathf.Lerp(1.0f, cornerSpeedReductionFactor, Mathf.Pow(cornerFactor, 0.7f));
        }
        idealTargetSpeed *= Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, skillLevel);
        idealTargetSpeed *= (1f + aggressiveness * 0.2f);

        // --- 3. Check for overtaking and apply lateral offsets ---
        UpdateOvertakingLogic(idealTargetSpeed);
        currentOvertakeOffset = Mathf.Lerp(currentOvertakeOffset, targetOvertakeOffset, Time.deltaTime * 2f);

        // --- NEW: Dynamic Avoidance System ---
        float avoidanceOffset = CalculateDynamicAvoidanceOffset();
        // Commitment logic: only change direction every X seconds
        if (Time.time - lastAvoidanceDecisionTime > avoidanceCommitmentDuration)
        {
            committedAvoidanceOffset = avoidanceOffset;
            avoidanceCommitmentTimer = avoidanceCommitmentDuration;
            lastAvoidanceDecisionTime = Time.time;
        }
        else
        {
            avoidanceCommitmentTimer -= Time.deltaTime;
        }

        float randomOffset = (pathRandomness > 0)
            ? (Mathf.PerlinNoise(Time.time * randomnessChangeRate, perlinSeed) * 2f - 1f) * pathRandomness
            : 0f;

        float totalLateralOffset = randomOffset + currentOvertakeOffset + committedAvoidanceOffset;

        if (totalLateralOffset != 0)
        {
            int targetIndex = (currentWaypointIndex + adjustedLookahead) % racingLine.Points.Count;
            int nextPointIndex = (targetIndex + 1) % racingLine.Points.Count;
            Vector3 tangent = (racingLine.Points[nextPointIndex] - racingLine.Points[targetIndex]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Vector3.up);
            targetPoint += normal * totalLateralOffset;
        }
        
        Vector3 worldTargetPoint = trackGenerator.transform.TransformPoint(targetPoint);
        
        // --- 4. Calculate final speed and steering ---
        currentSteer = Mathf.Lerp(currentSteer, GetTargetSteer(worldTargetPoint), Time.deltaTime * steeringSpeed * (1f + skillLevel));
        float finalTargetSpeed = idealTargetSpeed * RubberBandingFactor;

        // Determine acceleration/braking
        float currentSpeed = vehicleController.carVelocity.magnitude / vehicleController.MaxSpeed;
        
        // Calculate how much we need to slow down based on corner factor - more gradual curve
        float cornerBrakingFactor = Mathf.Pow(cornerFactor, 0.8f) * 1.2f; // Less aggressive exponential curve
        
        // NEW: Enhanced braking when nitro is active or approaching sharp corners
        if (vehicleController.isNitroActive && cornerFactor > 0.1f)
        {
            // More aggressive braking when nitro is active and approaching corners
            cornerBrakingFactor *= 2.0f;
        }
        
        // NEW: Emergency braking for very sharp corners
        if (cornerFactor > 0.25f)
        {
            cornerBrakingFactor *= 1.5f;
        }
        
        // NEW: Two-phase braking approach for more realistic driving
        // Phase 1: Cut throttle when approaching corners or slightly over target speed
        // Phase 2: Apply brakes only when significantly over target speed or in sharp corners
        
        bool shouldAccelerate = currentSpeed < finalTargetSpeed && cornerFactor < 0.1f;
        bool needsThrottleCut = cornerFactor > 0.05f || currentSpeed > finalTargetSpeed * 1.05f;
        bool needsActiveBraking = currentSpeed > finalTargetSpeed * 1.15f || cornerFactor > 0.3f;
        
        // NEW: Enhanced braking triggers when nitro is active
        if (vehicleController.isNitroActive)
        {
            needsThrottleCut = cornerFactor > 0.03f || currentSpeed > finalTargetSpeed * 1.02f; // More sensitive
            needsActiveBraking = currentSpeed > finalTargetSpeed * 1.1f || cornerFactor > 0.2f; // More aggressive
        }
        
        if (shouldAccelerate)
        {
            // Need to accelerate - only when well below target speed and not approaching corners
            float accelerationFactor = 1.0f - (cornerFactor * 3.0f); // More aggressive acceleration reduction near corners
            accelerationFactor = Mathf.Max(0.05f, accelerationFactor); // Lower minimum acceleration
            
            currentAcceleration = Mathf.Lerp(currentAcceleration, accelerationFactor, Time.deltaTime * accelerationSpeed);
            currentBrake = Mathf.Lerp(currentBrake, 0f, Time.deltaTime * accelerationSpeed * 2.0f); // Release brakes quickly
        }
        else if (needsThrottleCut)
        {
            // Phase 1: Cut throttle to slow down naturally
            // This creates a more realistic "lift off" behavior before applying brakes
            
            // Cut throttle completely when approaching corners or over target speed
            currentAcceleration = Mathf.Lerp(currentAcceleration, 0f, Time.deltaTime * accelerationSpeed * 1.5f);
            
            // Only apply light braking if significantly over target speed
            float speedDifference = currentSpeed - finalTargetSpeed;
            float lightBrakingIntensity = 0f;
            
            if (speedDifference > 0.1f) // Only brake if 10% over target speed
            {
                lightBrakingIntensity = Mathf.Clamp01(speedDifference * brakingIntensityMultiplier * 0.3f); // Very light braking
            }
            
            currentBrake = Mathf.Lerp(currentBrake, lightBrakingIntensity, Time.deltaTime * accelerationSpeed * 1.5f);
        }
        else if (needsActiveBraking)
        {
            // Phase 2: Apply active braking when necessary
            float speedDifference = currentSpeed - finalTargetSpeed;
            
            // Calculate braking intensity based on speed difference and corner factor
            float brakingIntensity = Mathf.Clamp01(speedDifference * brakingIntensityMultiplier * 0.8f);
                
            // Add corner-based braking for sharp turns
            if (cornerFactor > 0.3f)
            {
                float cornerBraking = cornerBrakingFactor * 0.6f; // Gentler corner braking
                brakingIntensity = Mathf.Max(brakingIntensity, cornerBraking);
            }
            
            // Cut throttle completely when actively braking
            currentAcceleration = Mathf.Lerp(currentAcceleration, 0f, Time.deltaTime * accelerationSpeed * 2.0f);
            
            // Apply brakes gradually
            currentBrake = Mathf.Lerp(currentBrake, brakingIntensity, Time.deltaTime * accelerationSpeed * 1.5f);
        }
        else
        {
            // Coasting - gradually reduce inputs
            currentAcceleration = Mathf.Lerp(currentAcceleration, 0f, Time.deltaTime * accelerationSpeed);
            currentBrake = Mathf.Lerp(currentBrake, 0f, Time.deltaTime * accelerationSpeed);
        }
        
        // Use handbrake for sharp corners to prevent loss of control
        // NEW: Slam handbrake at sharp corners, release smoothly (player-like drift)
        if (cornerFactor > handbrakeThresholdRandomized)
        {
            // "Press" handbrake hard at the start of a sharp corner
            currentHandbrake = Mathf.Lerp(currentHandbrake, 1.0f, Time.deltaTime * 2f);
            if (showDebugInfo && currentHandbrake > 0.7f)
            {
                Debug.Log($"[AI] {gameObject.name} DRIFTING! Handbrake: {currentHandbrake:F2} (cornerFactor: {cornerFactor:F2})");
            }
        }
        else
        {
            // Release handbrake smoothly
            currentHandbrake = Mathf.Lerp(currentHandbrake, 0f, Time.deltaTime * 2f);
        }
        
        // --- SIMPLE AI Nitro System ---
        aiNitroCooldownTimer -= Time.deltaTime;
        if (nitroSlowdownTimer > 0f) nitroSlowdownTimer -= Time.deltaTime;
        float lookaheadCorner = GetLookaheadCornerFactor(1f);
        float doubleLookaheadCorner = GetLookaheadCornerFactor(2f);
        bool isSafeForNitro = lookaheadCorner < 0.1f && doubleLookaheadCorner < 0.1f;
        if (forceStartNitro)
        {
            startNitroTimer -= Time.deltaTime;
            if (startNitroTimer > 0f && isSafeForNitro)
            {
                // Force nitro at race start (only if both lookaheads are safe)
                if (vehicleController.inputManager != null)
                {
                    vehicleController.inputManager.SetAIInputs(currentSteer, currentAcceleration, currentHandbrake > 0f ? currentHandbrake : currentBrake, true);
                }
                return;
            }
            else
            {
                forceStartNitro = false;
            }
        }
        bool canUseNitro = aiNitroCooldownTimer <= 0f && vehicleController.currentNitro > 10f && !vehicleController.isNitroActive && !vehicleController.isNitroCooldown;
        bool useNitro = false;
        if (canUseNitro && isSafeForNitro && Random.value < 0.3f)
        {
            useNitro = true;
            aiNitroCooldownTimer = aiNitroCooldownDuration = Random.Range(22f, 30f); // Reset cooldown
            nitroSlowdownTimer = nitroSlowdownDuration;
            isNitroSlowingDown = true;
            Debug.Log($"[AI] {gameObject.name} USING NITRO! (Simple 30% logic, double lookahead)");
        }
        if (vehicleController.inputManager != null)
        {
            vehicleController.inputManager.SetAIInputs(currentSteer, currentAcceleration, currentHandbrake > 0f ? currentHandbrake : currentBrake, useNitro);
        }
        
        // After nitro, enforce slowdown to 90% of recommended speed
        if (isNitroSlowingDown)
        {
            if (nitroSlowdownTimer > 0f)
            {
                float slowdownTarget = recommendedSpeed * 0.9f;
                float slowdownCurrentSpeed = vehicleController.carVelocity.magnitude / vehicleController.MaxSpeed;
                if (slowdownCurrentSpeed > slowdownTarget)
                {
                    // Aggressively cut throttle and apply brakes
                    currentAcceleration = Mathf.Lerp(currentAcceleration, 0f, Time.deltaTime * 3f);
                    currentBrake = Mathf.Lerp(currentBrake, 1f, Time.deltaTime * 2f);
                }
            }
            else
            {
                isNitroSlowingDown = false;
            }
        }
        
        // Debug visualization
        if (showDebugInfo)
        {
            // Draw line to target point
            Debug.DrawLine(transform.position, worldTargetPoint, targetPointColor);
            
            // Draw racing line ahead
            int startIdx = currentWaypointIndex;
            int endIdx = (currentWaypointIndex + 20) % racingLine.Points.Count;
            for (int i = startIdx; i != endIdx; i = (i + 1) % racingLine.Points.Count)
            {
                int nextIdx = (i + 1) % racingLine.Points.Count;
                Vector3 start = trackGenerator.transform.TransformPoint(racingLine.Points[i]);
                Vector3 end = trackGenerator.transform.TransformPoint(racingLine.Points[nextIdx]);
                Debug.DrawLine(start, end, pathColor);
            }
            
            // Draw handbrake activation
            if (currentHandbrake > 0f)
            {
                Debug.DrawRay(transform.position + Vector3.up * 4.5f, Vector3.right * currentHandbrake * 5f, Color.magenta);
            }
            
            // Draw nitro lookahead (green line)
            int longLookahead = Mathf.RoundToInt(lookaheadPoints * 2.5f);
            int lookIdx = (currentWaypointIndex + longLookahead) % racingLine.Points.Count;
            Vector3 lookaheadPoint = trackGenerator.transform.TransformPoint(racingLine.Points[lookIdx]);
            Debug.DrawLine(transform.position + Vector3.up * 2f, lookaheadPoint + Vector3.up * 2f, Color.green);
        }

        // Defensive driving timer
        if (isDefending)
        {
            defenseTimer += Time.deltaTime;
            if (defenseTimer >= defenseDuration)
            {
                isDefending = false;
                vehicleController.Acceleration = originalAcceleration;
                Debug.Log($"<color=red>{gameObject.name} has STOPPED defending.</color>");
            }
        }
    }

    private float GetTargetSteer(Vector3 worldTargetPoint)
    {
        // Calculate the steering angle needed to follow the racing line.
        Vector3 directionToTarget = worldTargetPoint - transform.position;
        Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);
        float targetSteer = Mathf.Clamp(localDirection.x / localDirection.magnitude, -maxSteeringAngle, maxSteeringAngle);
        targetSteer *= Mathf.Lerp(1.5f, 1.0f, skillLevel);

        return targetSteer;
    }
    
    private void UpdateOvertakingLogic(float idealSpeedNormalized)
    {
        if (overtakeCooldownTimer > 0)
        {
            overtakeCooldownTimer -= Time.deltaTime;
            return;
        }

        if (isOvertaking)
        {
            // Check if overtake is complete
            if (carToOvertake == null) 
            {
                isOvertaking = false;
                targetOvertakeOffset = 0f;
                overtakeCooldownTimer = overtakeCooldown;
                carToOvertake = null;
                vehicleController.Acceleration = originalAcceleration; // Reset acceleration
                return;
            }

            Vector3 directionToTargetCar = carToOvertake.transform.position - transform.position;
            if (Vector3.Dot(transform.forward, directionToTargetCar) < 0)
            {
                Debug.Log($"<color=cyan>{gameObject.name} has COMPLETED overtaking {carToOvertake.name}.</color>");
                isOvertaking = false;
                targetOvertakeOffset = 0f;
                overtakeCooldownTimer = overtakeCooldown;
                carToOvertake = null;
                vehicleController.Acceleration = originalAcceleration; // Reset acceleration
            }
            return; 
        }

        // Find the closest car in front
        AIVehicleController leadCar = FindCarToOvertake(idealSpeedNormalized);
        
        if (leadCar != null)
        {
            timeStuck += Time.deltaTime;
            
            if (timeStuck > overtakeTriggerTime)
            {
                Debug.Log($"<color=orange>{gameObject.name} is STARTING to overtake {leadCar.name}!</color>");
                isOvertaking = true;
                carToOvertake = leadCar;
                timeStuck = 0f;
                float boostFactor = Random.Range(1.1f, 1.3f);
                vehicleController.Acceleration = originalAcceleration * boostFactor; // Boost acceleration
                
                float overtakeDirection = (Random.value > 0.5f) ? 1f : -1f;
                targetOvertakeOffset = overtakeLaneOffset * overtakeDirection;

                // NEW: Ask the car being overtaken to defend
                leadCar.TryStartDefense(vehicleController.Acceleration);
            }
        }
        else
        {
            timeStuck = 0f;
        }
    }

    private AIVehicleController FindCarToOvertake(float idealSpeedNormalized)
    {
        float closestDistance = 15f; 
        AIVehicleController potentialTarget = null;

        foreach (var vehicle in otherVehicles)
        {
            if (vehicle == null || !vehicle.isActiveAndEnabled) continue;
            Vector3 directionToVehicle = vehicle.transform.position - transform.position;
            float distance = directionToVehicle.magnitude;
            
            // More lenient "in front" check - reduce from 0.8 to 0.6 (about 53 degrees)
            bool isInFront = Vector3.Dot(transform.forward, directionToVehicle.normalized) > 0.6f;

            if (isInFront && distance < closestDistance)
            {
                closestDistance = distance;
                potentialTarget = vehicle;
            }
        }

        bool isTargetValid = false;
        if (potentialTarget != null)
        {
            float desiredSpeed = idealSpeedNormalized * vehicleController.MaxSpeed;
            float targetSpeed = potentialTarget.vehicleController.carVelocity.magnitude;
            
            // More lenient speed comparison - consider target slower if we want to go 5% faster
            bool isSlower = desiredSpeed > targetSpeed * 1.05f;
            bool onStraight = DetectUpcomingCorners() < maxOvertakeCornerFactor;

            isTargetValid = isSlower && onStraight;
        }

        if (showDebugInfo)
        {
            foreach (var vehicle in otherVehicles)
            {
                if (vehicle == null || !vehicle.isActiveAndEnabled) continue;
                
                Color debugColor = Color.grey; 
                if (vehicle == potentialTarget)
                {
                    debugColor = isTargetValid ? Color.green : Color.yellow;
                }
                Debug.DrawLine(transform.position, vehicle.transform.position, debugColor);
            }
        }
        
        return isTargetValid ? potentialTarget : null;
    }

    /// <summary>
    /// Detects upcoming corners by analyzing multiple points ahead on the racing line
    /// Returns a value between 0 and 1 indicating the sharpness of upcoming corners
    /// </summary>
    private float DetectUpcomingCorners()
    {
        if (racingLine == null || racingLine.Points.Count == 0) return 0f;
        
        float maxCurvature = 0f;
        float distanceToCorner = float.MaxValue;
        Vector3 cornerPosition = Vector3.zero;
        
        // Look ahead multiple points to detect upcoming corners
        for (int i = 1; i <= cornerDetectionLookahead; i++)
        {
            int idx1 = currentWaypointIndex;
            int idx2 = (currentWaypointIndex + i) % racingLine.Points.Count;
            int idx3 = (currentWaypointIndex + i * 2) % racingLine.Points.Count;
            
            if (idx1 >= racingLine.Points.Count || idx2 >= racingLine.Points.Count || idx3 >= racingLine.Points.Count)
                continue;
                
            Vector3 p1 = racingLine.Points[idx1];
            Vector3 p2 = racingLine.Points[idx2];
            Vector3 p3 = racingLine.Points[idx3];
            
            // Calculate vectors between points
            Vector3 v1 = (p2 - p1).normalized;
            Vector3 v2 = (p3 - p2).normalized;
            
            // Calculate the angle between the vectors (indicates curvature)
            float dot = Vector3.Dot(v1, v2);
            // Normalize to 0-1 range, where 1 is a sharp 180-degree turn
            float curvature = 1f - (dot + 1f) / 2f; 
            
            // Only consider significant curves - lowered threshold to detect more corners
            if (curvature > cornerDetectionThreshold * 0.8f) // 20% lower threshold for initial detection
            {
                // Calculate approximate distance to this corner
                float distance = 0f;
                for (int j = 0; j < i; j++)
                {
                    int fromIdx = (currentWaypointIndex + j) % racingLine.Points.Count;
                    int toIdx = (currentWaypointIndex + j + 1) % racingLine.Points.Count;
                    distance += Vector3.Distance(racingLine.Points[fromIdx], racingLine.Points[toIdx]);
                }
                
                // Weight the curvature by distance - closer corners matter more
                // Reduced the divisor to make distance weighting more significant
                float distanceWeight = Mathf.Clamp01(1.0f - (distance / (brakingDistance * 5f))); // More aggressive distance weighting
                float weightedCurvature = curvature * distanceWeight;
                
                // Keep track of the sharpest corner and its distance
                if (weightedCurvature > maxCurvature)
                {
                    maxCurvature = weightedCurvature;
                    distanceToCorner = distance;
                    cornerPosition = trackGenerator.transform.TransformPoint(p2); // Store corner position for visualization
                }
            }
        }
        
        // Apply braking distance factor - start slowing down earlier for sharper corners
        // Increased the influence of braking distance to start slowing down earlier
        float cornerFactor = maxCurvature * Mathf.Clamp01((brakingDistance * 2.5f) / Mathf.Max(0.1f, distanceToCorner));
        
        // Apply skill level - better drivers anticipate corners better
        cornerFactor *= Mathf.Lerp(1.5f, 1.0f, skillLevel);
        
        // Enhanced debug visualization for corner detection
        if (showDebugInfo && cornerFactor > 0.05f) // Lower threshold to see more corner detections
        {            
            // Draw a line to the detected corner position
            Debug.DrawLine(transform.position, cornerPosition, Color.red);
            
            // Display the corner factor as text above the vehicle
            Debug.DrawRay(transform.position + Vector3.up * 2f, Vector3.up * cornerFactor * 5f, Color.red);
            
            // Draw a sphere at the corner position
            Debug.DrawRay(cornerPosition, Vector3.up * 2f, Color.red);
            Debug.DrawRay(cornerPosition, Vector3.right * 2f, Color.red);
            Debug.DrawRay(cornerPosition, Vector3.forward * 2f, Color.red);
        }
        
        return Mathf.Clamp01(cornerFactor);
    }

    // Defensive driving: called by overtaking car
    public void TryStartDefense(float attackerAcceleration)
    {
        if (isDefending) return; // Already defending

        if (Random.value < 0.5f) // 50% chance
        {
            isDefending = true;
            defenseDuration = Random.Range(5f, 10f);
            defenseTimer = 0f;
            vehicleController.Acceleration = attackerAcceleration;
            Debug.Log($"<color=red>{gameObject.name} is DEFENDING against overtake!</color>");
        }
    }

    // Force an overtake attempt on a specific car (called by AIRaceManager)
    public void ForceOvertake(AIVehicleController target)
    {
        if (target == null || isOvertaking || carToOvertake == target) return;
        Debug.Log($"[AI] {gameObject.name} is FORCED to overtake {target.gameObject.name} by manager.");
        isOvertaking = true;
        carToOvertake = target;
        timeStuck = 0f;
        float boostFactor = Random.Range(1.1f, 1.3f);
        vehicleController.Acceleration = originalAcceleration * boostFactor;
        float overtakeDirection = (Random.value > 0.5f) ? 1f : -1f;
        targetOvertakeOffset = overtakeLaneOffset * overtakeDirection;
        overtakeCooldownTimer = overtakeCooldown;
        // Ask the car being overtaken to defend
        target.TryStartDefense(vehicleController.Acceleration);
    }

    // --- NEW: Dynamic Threat Field Avoidance System ---
    private float CalculateDynamicAvoidanceOffset()
    {
        float totalOffset = 0f;
        float totalThreat = 0f;
        Vector3 myPos = transform.position;
        Vector3 myFwd = transform.forward;
        float mySpeed = vehicleController.carVelocity.magnitude;

        foreach (var other in otherVehicles)
        {
            if (other == null || !other.isActiveAndEnabled) continue;
            Vector3 toOther = other.transform.position - myPos;
            float distance = toOther.magnitude;
            if (distance > avoidanceDetectionRadius) continue;
            float angle = Vector3.Angle(myFwd, toOther);
            if (angle > avoidanceAwarenessAngle * 0.5f) continue;

            // Threat score: closer, more in front, and higher relative speed = higher threat
            float relSpeed = Vector3.Dot(other.vehicleController.carVelocity - vehicleController.carVelocity, myFwd);
            float threat = Mathf.Lerp(1.0f, 0.1f, distance / avoidanceDetectionRadius);
            threat *= Mathf.Lerp(1.0f, 0.2f, angle / (avoidanceAwarenessAngle * 0.5f));
            threat *= 1.0f + Mathf.Clamp01(relSpeed / Mathf.Max(1f, mySpeed));

            // Aggression: aggressive AIs tolerate more, cautious AIs avoid more
            float aggressionFactor = Mathf.Lerp(1.2f, 0.7f, aggressiveness);
            threat *= aggressionFactor;

            // Lateral offset: steer away from the other car's local X position
            Vector3 localToOther = transform.InverseTransformPoint(other.transform.position);
            float side = Mathf.Sign(localToOther.x);
            float offset = side * Mathf.Lerp(1.5f, 3.0f, threat); // more threat = bigger offset
            totalOffset += offset * threat;
            totalThreat += threat;

            // Debug: draw threat lines
            if (showDebugInfo)
            {
                Debug.DrawLine(myPos + Vector3.up * 2f, other.transform.position + Vector3.up * 2f, Color.red);
                Debug.DrawRay(other.transform.position + Vector3.up * 2f, Vector3.up * threat * 2f, Color.magenta);
            }
        }

        // Average the offset by total threat
        float avoidanceOffset = (totalThreat > 0f) ? totalOffset / totalThreat : 0f;

        // If overtaking, bias to the chosen overtake side
        if (isOvertaking && carToOvertake != null)
        {
            avoidanceOffset += Mathf.Sign(targetOvertakeOffset) * 1.0f;
        }

        // Smooth the offset for stability
        avoidanceOffset = Mathf.Clamp(avoidanceOffset, -3.5f, 3.5f);
        return avoidanceOffset;
    }

    private float GetLookaheadCornerFactor(float multiplier)
    {
        if (racingLine == null || racingLine.Points.Count == 0) return 0f;
        int lookahead = Mathf.RoundToInt(lookaheadPoints * multiplier);
        int idx1 = currentWaypointIndex;
        int idx2 = (currentWaypointIndex + lookahead) % racingLine.Points.Count;
        int idx3 = (currentWaypointIndex + lookahead * 2) % racingLine.Points.Count;
        if (idx1 >= racingLine.Points.Count || idx2 >= racingLine.Points.Count || idx3 >= racingLine.Points.Count)
            return 0f;
        Vector3 p1 = racingLine.Points[idx1];
        Vector3 p2 = racingLine.Points[idx2];
        Vector3 p3 = racingLine.Points[idx3];
        Vector3 v1 = (p2 - p1).normalized;
        Vector3 v2 = (p3 - p2).normalized;
        float dot = Vector3.Dot(v1, v2);
        float curvature = 1f - (dot + 1f) / 2f;
        return Mathf.Clamp01(curvature);
    }
}