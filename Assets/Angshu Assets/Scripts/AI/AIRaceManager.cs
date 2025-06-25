using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Track;
using System.Linq;

public class AIRaceManager : MonoBehaviour
{
    [Header("AI Racers Configuration")]
    [SerializeField] private TrackGenerator trackGenerator;
    [SerializeField] private List<GameObject> aiVehiclePrefabs;
    [SerializeField, Range(1, 10)] private int numberOfAIRacers = 3;
    [SerializeField] private float startingOffset = 5f;
    
    [Header("AI Difficulty Settings")]
    [SerializeField, Range(0f, 1f)] private float minSkillLevel = 0.5f;
    [SerializeField, Range(0f, 1f)] private float maxSkillLevel = 0.9f;
    [SerializeField, Range(0f, 1f)] private float minAggressiveness = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxAggressiveness = 0.8f;
    
    [Header("Rubber Banding")]
    [SerializeField, Range(0f, 1f), Tooltip("How strongly the rubber banding effect pulls cars together.")]
    private float rubberBandingStrength = 0.5f;
    [SerializeField, Tooltip("The max speed boost given to cars that are behind.")]
    private float maxSpeedBoost = 1.2f; // 20% speed boost
    [SerializeField, Tooltip("The max speed penalty given to the car in the lead.")]
    private float maxSpeedPenalty = 0.9f; // 10% speed penalty
    
    private List<AIVehicleController> aiRacers = new List<AIVehicleController>();
    private bool raceIsActive = false;
    
    // Position tracking
    public List<AIVehicleController> SortedRacers { get; private set; } = new List<AIVehicleController>();
    public Dictionary<AIVehicleController, int> CarPositions { get; private set; } = new Dictionary<AIVehicleController, int>();
    private Dictionary<AIVehicleController, float> overtakeCooldowns = new Dictionary<AIVehicleController, float>();
    
    private void Start()
    {
        // Find track generator if not assigned
        if (trackGenerator == null)
        {
            trackGenerator = FindFirstObjectByType<TrackGenerator>();
            if (trackGenerator == null)
            {
                Debug.LogError("No TrackGenerator found in scene. AI race manager will not function properly.");
                enabled = false;
                return;
            }
        }
        
        // Wait for track generation to complete before spawning AI racers
        StartCoroutine(SpawnAIRacersWhenReady());
        StartCoroutine(UpdatePositionsRoutine());
        StartCoroutine(PeriodicOvertakeOrders());
    }
    
    private IEnumerator SpawnAIRacersWhenReady()
    {
        // Wait for track generation to complete
        yield return new WaitForSeconds(2f); // Give time for track generation to complete
        
        // Make sure racing line is generated
        if (trackGenerator.RacingLine == null || trackGenerator.RacingLine.Points.Count == 0)
        {
            Debug.LogWarning("No racing line found on track. AI racers will not be spawned.");
            yield break;
        }
        
        SpawnAIRacers();

        // Start the race with a countdown
        StartCoroutine(StartRaceCountdown());
    }
    
    private void SpawnAIRacers()
    {
        if (aiVehiclePrefabs == null || aiVehiclePrefabs.Count == 0)
        {
            Debug.LogError("AI vehicle prefabs list is not assigned or is empty!");
            return;
        }
        
        // Clear any existing AI racers
        foreach (var racer in aiRacers)
        {
            if (racer != null)
            {
                Destroy(racer.gameObject);
            }
        }
        aiRacers.Clear();
        
        // Get starting position and orientation from the racing line
        Vector3 startPosition = trackGenerator.transform.TransformPoint(trackGenerator.RacingLine.Points[0]);
        Vector3 startDirection = trackGenerator.transform.TransformPoint(trackGenerator.RacingLine.Points[5]) - startPosition;
        startDirection.y = 0;
        startDirection.Normalize();
        Vector3 sideDirection = Vector3.Cross(startDirection, Vector3.up);

        // Grid layout settings
        int carsPerRow = 2;
        float rowSpacing = 12f; // Increased spacing between rows
        float colSpacing = 6f; // Increased spacing between cars in the same row
        
        // Spawn AI racers in a grid
        for (int i = 0; i < numberOfAIRacers; i++)
        {
            int row = i / carsPerRow;
            int col = i % carsPerRow;
            
            // Calculate position with offset
            Vector3 rowOffset = -startDirection * (row * rowSpacing + startingOffset);
            // Center the grid: (0 - 0.5) = -0.5, (1 - 0.5) = 0.5 for a 2-car row.
            float gridCenterOffset = (carsPerRow - 1) * 0.5f; 
            Vector3 colOffset = sideDirection * (col - gridCenterOffset) * colSpacing;
            
            Vector3 position = startPosition + rowOffset + colOffset;
            position.y += 0.5f; // Slight height offset to prevent ground collision issues
            
            // Calculate rotation to face forward along track
            Quaternion rotation = Quaternion.LookRotation(startDirection);
            
            // Instantiate a random AI vehicle from the list
            GameObject prefabToSpawn = aiVehiclePrefabs[Random.Range(0, aiVehiclePrefabs.Count)];
            GameObject aiVehicle = Instantiate(prefabToSpawn, position, rotation);
            aiVehicle.name = $"AI_Racer_{i+1}";
            
            // Configure AI controller
            AIVehicleController aiController = aiVehicle.GetComponent<AIVehicleController>();
            if (aiController != null)
            {
                // Set track reference
                aiController.trackGenerator = trackGenerator;
                
                // Set random difficulty parameters
                SetRandomDifficulty(aiController, i);
                
                // Ensure the car is not racing yet
                aiController.IsRacing = false;
                
                aiRacers.Add(aiController);
            }
            else
            {
                // Add AI controller if not present
                aiController = aiVehicle.AddComponent<AIVehicleController>();
                aiController.trackGenerator = trackGenerator;
                SetRandomDifficulty(aiController, i);
                aiController.IsRacing = false;
                aiRacers.Add(aiController);
            }
        }
    }

    private IEnumerator UpdatePositionsRoutine()
    {
        while (true)
        {
            UpdateRacePositions();
            yield return new WaitForSeconds(1f);
        }
    }

    private void UpdateRacePositions()
    {
        SortedRacers = aiRacers.OrderByDescending(r => r.RaceProgress).ToList();
        CarPositions.Clear();
        for (int i = 0; i < SortedRacers.Count; i++)
        {
            CarPositions[SortedRacers[i]] = i + 1;
        }
        // Debug log for positions
        string posLog = "[RaceManager] Positions: " + string.Join(", ", SortedRacers.Select((r, i) => $"{i+1}:{r.gameObject.name}"));
        Debug.Log(posLog);
    }

    private IEnumerator StartRaceCountdown()
    {
        yield return new WaitForSeconds(1.0f);
        Debug.Log("<color=yellow>3...</color>");
        yield return new WaitForSeconds(1.0f);
        Debug.Log("<color=yellow>2...</color>");
        yield return new WaitForSeconds(1.0f);
        Debug.Log("<color=yellow>1...</color>");
        yield return new WaitForSeconds(1.0f);
        Debug.Log("<color=green>GO!</color>");

        foreach(var racer in aiRacers)
        {
            if (racer != null)
            {
                racer.IsRacing = true;
            }
        }

        // Now that the race has officially started, begin the rubber banding updates.
        if (!raceIsActive)
        {
            StartCoroutine(UpdateRubberBanding());
            raceIsActive = true;
        }
    }
    
    private IEnumerator UpdateRubberBanding()
    {
        while (true)
        {
            // Wait for a short interval before recalculating.
            yield return new WaitForSeconds(1.0f); 

            if (aiRacers.Count < 2) continue;

            float leadProgress = 0f;
            AIVehicleController leader = null;

            // Find the leader
            foreach (var racer in aiRacers)
            {
                if (racer.RaceProgress > leadProgress)
                {
                    leadProgress = racer.RaceProgress;
                    leader = racer;
                }
            }

            if (leader == null) continue;

            // Apply rubber banding to all cars
            foreach (var racer in aiRacers)
            {
                float progressDifference = leadProgress - racer.RaceProgress;
                
                if (racer == leader)
                {
                    // The leader gets slowed down based on how far they are from the car in 2nd place.
                    float leadDistance = progressDifference; // This will be 0, need to find 2nd place
                    float secondProgress = 0;
                    foreach(var other in aiRacers)
                    {
                        if(other != leader && other.RaceProgress > secondProgress)
                        {
                            secondProgress = other.RaceProgress;
                        }
                    }
                    float leadAdvantage = leadProgress - secondProgress;
                    float penaltyFactor = Mathf.InverseLerp(0f, 0.1f, leadAdvantage); // 10% of track ahead
                    racer.RubberBandingFactor = Mathf.Lerp(1f, maxSpeedPenalty, penaltyFactor * rubberBandingStrength);
                }
                else
                {
                    // Other cars get a boost based on how far they are behind the leader.
                    float boostFactor = Mathf.InverseLerp(0f, 0.2f, progressDifference); // 20% of track behind
                    racer.RubberBandingFactor = Mathf.Lerp(1f, maxSpeedBoost, boostFactor * rubberBandingStrength);
                }
            }
        }
    }
    
    // Remove per-car overtaking coroutines and add a single periodic overtake order coroutine
    private IEnumerator PeriodicOvertakeOrders()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f);
            if (!raceIsActive) continue;
            if (SortedRacers.Count < 2) continue;
            for (int i = 1; i < SortedRacers.Count; i++) // skip leader
            {
                var racer = SortedRacers[i];
                var aheadCar = SortedRacers[i - 1];
                Debug.Log($"[RaceManager] {racer.gameObject.name} (P{i+1}) ordered to overtake {aheadCar.gameObject.name} (P{i})");
                racer.ForceOvertake(aheadCar);
            }
        }
    }
    
    private void SetRandomDifficulty(AIVehicleController aiController, int racerIndex)
    {
        // Access the serialized fields using reflection
        var skillField = aiController.GetType().GetField("skillLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var aggressivenessField = aiController.GetType().GetField("aggressiveness", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (skillField != null && aggressivenessField != null)
        {
            // Calculate skill level - lead cars are generally more skilled
            float normalizedIndex = (float)racerIndex / Mathf.Max(1, numberOfAIRacers - 1);
            float skillLevel = Mathf.Lerp(maxSkillLevel, minSkillLevel, normalizedIndex);
            
            // Add some randomness
            skillLevel += Random.Range(-0.1f, 0.1f);
            skillLevel = Mathf.Clamp(skillLevel, minSkillLevel, maxSkillLevel);
            
            // Calculate aggressiveness - random for each car
            float aggressiveness = Random.Range(minAggressiveness, maxAggressiveness);
            
            // Set values
            skillField.SetValue(aiController, skillLevel);
            aggressivenessField.SetValue(aiController, aggressiveness);
        }
    }
    
    // Method to reset race (can be called from other scripts)
    public void ResetRace()
    {
        StartCoroutine(SpawnAIRacersWhenReady());
    }
}