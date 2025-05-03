using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ashsvp;
using Track;

[RequireComponent(typeof(SimcadeVehicleController))]
public class AIVehicleController : MonoBehaviour
{
    [Header("AI Configuration")]
    [SerializeField] public TrackGenerator trackGenerator;
    [SerializeField, Range(1, 20)] private int lookaheadPoints = 5;
    [SerializeField, Range(0f, 1f)] private float maxSteeringAngle = 0.8f;
    [SerializeField, Range(0f, 1f)] private float maxSpeedMultiplier = 0.9f;
    [SerializeField, Range(0f, 1f)] private float minSpeedMultiplier = 0.5f;
    [SerializeField, Range(0f, 10f)] private float steeringSpeed = 3f;
    [SerializeField, Range(0f, 10f)] private float accelerationSpeed = 2f;
    [SerializeField] private bool useNitroOnStraights = true;
    [SerializeField, Range(0f, 1f)] private float nitroThreshold = 0.8f;
    [SerializeField, Range(0f, 1f)] private float avoidanceStrength = 0.5f;
    [SerializeField, Range(1f, 10f)] private float avoidanceDistance = 5f;
    
    [Header("Corner Handling")]
    [SerializeField, Range(5, 30)] private int cornerDetectionLookahead = 20; // Increased to look further ahead
    [SerializeField, Range(0.1f, 1f)] private float cornerSpeedReductionFactor = 0.4f; // More aggressive speed reduction
    [SerializeField, Range(0.1f, 1f)] private float cornerDetectionThreshold = 0.2f; // Detect corners earlier
    [SerializeField, Range(1f, 10f)] private float brakingDistance = 5f; // Increased braking distance
    
    // Difficulty settings
    [Header("Difficulty Settings")]
    [SerializeField, Range(0f, 1f)] private float skillLevel = 0.8f; // Higher = better driving
    [SerializeField, Range(0f, 1f)] private float aggressiveness = 0.7f; // Higher = more aggressive driving
    
    // Debug visualization
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Color targetPointColor = Color.blue;
    [SerializeField] private Color pathColor = Color.yellow;
    
    // Private variables
    private SimcadeVehicleController vehicleController;
    private RacingLine racingLine;
    private int currentWaypointIndex = 0;
    private float currentSteer = 0f;
    private float currentAcceleration = 0f;
    private float currentBrake = 0f;
    private bool isUsingNitro = false;
    private List<AIVehicleController> otherVehicles = new List<AIVehicleController>();
    
    private void Awake()
    {
        vehicleController = GetComponent<SimcadeVehicleController>();
        
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
    }
    
    private void Update()
    {
        if (racingLine == null || racingLine.Points.Count == 0) return;
        
        // Get current position in local space of track
        Vector3 localPosition = trackGenerator.transform.InverseTransformPoint(transform.position);
        
        // Find closest point on racing line
        (currentWaypointIndex, _, _) = racingLine.GetClosestPoint(localPosition);
        
        // Get target point ahead on racing line
        int adjustedLookahead = Mathf.RoundToInt(lookaheadPoints * (1f + skillLevel));
        (Vector3 targetPoint, float recommendedSpeed) = racingLine.GetNextTargetPoint(currentWaypointIndex, adjustedLookahead);
        
        // Convert target point to world space
        Vector3 worldTargetPoint = trackGenerator.transform.TransformPoint(targetPoint);
        
        // Calculate steering direction
        Vector3 directionToTarget = worldTargetPoint - transform.position;
        Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);
        float targetSteer = Mathf.Clamp(localDirection.x / localDirection.magnitude, -maxSteeringAngle, maxSteeringAngle);
        
        // Apply skill level to steering precision
        targetSteer *= Mathf.Lerp(1.5f, 1.0f, skillLevel); // Less skilled drivers oversteer
        
        // Adjust for other vehicles (collision avoidance)
        Vector3 avoidanceVector = CalculateAvoidanceVector();
        Vector3 localAvoidance = transform.InverseTransformDirection(avoidanceVector);
        targetSteer += localAvoidance.x * avoidanceStrength;
        
        // Smooth steering
        currentSteer = Mathf.Lerp(currentSteer, targetSteer, Time.deltaTime * steeringSpeed * (1f + skillLevel));
        
        // Detect upcoming corners by analyzing multiple points ahead
        float cornerFactor = DetectUpcomingCorners();
        
        // Calculate target speed based on recommended speed from racing line
        float targetSpeed = recommendedSpeed;
        
        // Apply corner speed reduction if approaching a sharp turn
        if (cornerFactor > 0)
        {
            // Reduce speed based on corner sharpness
            float cornerSpeedReduction = Mathf.Lerp(1.0f, cornerSpeedReductionFactor, cornerFactor);
            targetSpeed *= cornerSpeedReduction;
        }
        
        // Apply skill level to speed management
        float speedMultiplier = Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, skillLevel);
        targetSpeed *= speedMultiplier;
        
        // Adjust for aggressiveness
        targetSpeed *= (1f + aggressiveness * 0.2f); // More aggressive drivers go faster
        
        // Determine acceleration/braking
        float currentSpeed = vehicleController.carVelocity.magnitude / vehicleController.MaxSpeed;
        
        // Debug current speed vs target speed
        if (showDebugInfo)
        {
            Debug.DrawRay(transform.position + Vector3.up * 3f, Vector3.right * currentSpeed * 5f, Color.green);
            Debug.DrawRay(transform.position + Vector3.up * 3.5f, Vector3.right * targetSpeed * 5f, Color.yellow);
        }
        
        // Calculate how much we need to slow down based on corner factor
        float cornerBrakingFactor = Mathf.Pow(cornerFactor, 0.7f) * 1.5f; // Exponential curve for more aggressive braking
        
        if (currentSpeed < targetSpeed && cornerFactor < 0.3f) // Only accelerate if not approaching a sharp corner
        {
            // Need to accelerate
            float accelerationFactor = 1.0f - (cornerFactor * 2.0f); // Reduce acceleration when approaching corners
            accelerationFactor = Mathf.Max(0.1f, accelerationFactor); // Ensure minimum acceleration
            
            currentAcceleration = Mathf.Lerp(currentAcceleration, accelerationFactor, Time.deltaTime * accelerationSpeed);
            currentBrake = 0f;
            
            // Use nitro on straights if available and not approaching a corner
            if (useNitroOnStraights && recommendedSpeed > nitroThreshold && cornerFactor < 0.1f && !vehicleController.isNitroCooldown)
            {
                isUsingNitro = true;
            }
            else
            {
                isUsingNitro = false;
            }
        }
        else
        {
            // Need to brake/coast
            // Calculate base braking intensity based on speed difference
            float speedDifference = currentSpeed - targetSpeed;
            float brakingIntensity = Mathf.Clamp01(speedDifference * 2.0f); // More aggressive braking
            
            // Apply stronger braking when approaching corners
            brakingIntensity = Mathf.Max(brakingIntensity, cornerBrakingFactor);
            
            // Cut throttle completely when braking
            currentAcceleration = Mathf.Lerp(currentAcceleration, 0f, Time.deltaTime * accelerationSpeed * 2.0f);
            
            // Apply brakes more aggressively
            currentBrake = Mathf.Lerp(currentBrake, brakingIntensity, Time.deltaTime * accelerationSpeed * 3.0f);
            isUsingNitro = false;
            
            // Debug braking intensity
            if (showDebugInfo && brakingIntensity > 0.1f)
            {
                Debug.DrawRay(transform.position + Vector3.up * 4f, Vector3.left * brakingIntensity * 5f, Color.red);
            }
        }
        
        // Apply inputs to vehicle controller
        if (vehicleController.inputManager != null)
        {
            vehicleController.inputManager.SetAIInputs(currentSteer, currentAcceleration, currentBrake, isUsingNitro);
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
            
            // Draw avoidance vector
            Debug.DrawRay(transform.position, avoidanceVector * 5f, Color.red);
        }
    }
    
    private Vector3 CalculateAvoidanceVector()
    {
        Vector3 avoidanceVector = Vector3.zero;
        
        foreach (var vehicle in otherVehicles)
        {
            if (vehicle == null || !vehicle.isActiveAndEnabled) continue;
            
            Vector3 directionToVehicle = vehicle.transform.position - transform.position;
            float distance = directionToVehicle.magnitude;
            
            // Only avoid if within avoidance distance and in front of us
            if (distance < avoidanceDistance && Vector3.Dot(transform.forward, directionToVehicle.normalized) > 0.5f)
            {
                // Calculate avoidance force (stronger as we get closer)
                float avoidanceForce = 1f - (distance / avoidanceDistance);
                avoidanceVector -= directionToVehicle.normalized * avoidanceForce;
            }
        }
        
        return avoidanceVector;
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
            
            // Only consider significant curves
            if (curvature > cornerDetectionThreshold)
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
                float distanceWeight = Mathf.Clamp01(1.0f - (distance / (brakingDistance * 10f)));
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
        float cornerFactor = maxCurvature * Mathf.Clamp01((brakingDistance * 1.5f) / Mathf.Max(0.1f, distanceToCorner));
        
        // Apply skill level - better drivers anticipate corners better
        cornerFactor *= Mathf.Lerp(1.5f, 1.0f, skillLevel);
        
        // Debug visualization for corner detection
        if (showDebugInfo && cornerFactor > 0.1f)
        {            
            // Draw a sphere at the detected corner position
            Debug.DrawLine(transform.position, cornerPosition, Color.red);
            
            // Display the corner factor as text above the vehicle
            Debug.DrawRay(transform.position + Vector3.up * 2f, Vector3.up * cornerFactor * 5f, Color.red);
        }
        
        return Mathf.Clamp01(cornerFactor);
    }
}