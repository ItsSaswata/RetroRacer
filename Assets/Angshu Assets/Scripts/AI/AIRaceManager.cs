using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Track;

public class AIRaceManager : MonoBehaviour
{
    [Header("AI Racers Configuration")]
    [SerializeField] private TrackGenerator trackGenerator;
    [SerializeField] private GameObject aiVehiclePrefab;
    [SerializeField, Range(1, 10)] private int numberOfAIRacers = 3;
    [SerializeField] private float spacingDistance = 10f;
    [SerializeField] private float startingOffset = 5f;
    
    [Header("AI Difficulty Settings")]
    [SerializeField, Range(0f, 1f)] private float minSkillLevel = 0.5f;
    [SerializeField, Range(0f, 1f)] private float maxSkillLevel = 0.9f;
    [SerializeField, Range(0f, 1f)] private float minAggressiveness = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxAggressiveness = 0.8f;
    
    private List<AIVehicleController> aiRacers = new List<AIVehicleController>();
    
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
    }
    
    private void SpawnAIRacers()
    {
        if (aiVehiclePrefab == null)
        {
            Debug.LogError("AI vehicle prefab not assigned!");
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
        
        // Get starting position from racing line
        Vector3 startPosition = trackGenerator.transform.TransformPoint(trackGenerator.RacingLine.Points[0]);
        Vector3 startDirection = trackGenerator.transform.TransformPoint(trackGenerator.RacingLine.Points[5]) - startPosition;
        startDirection.y = 0;
        startDirection.Normalize();
        
        // Spawn AI racers with spacing
        for (int i = 0; i < numberOfAIRacers; i++)
        {
            // Calculate position with offset
            Vector3 position = startPosition - (startDirection * (startingOffset + i * spacingDistance));
            position.y += 0.5f; // Slight height offset to prevent ground collision issues
            
            // Calculate rotation to face forward along track
            Quaternion rotation = Quaternion.LookRotation(startDirection);
            
            // Instantiate AI vehicle
            GameObject aiVehicle = Instantiate(aiVehiclePrefab, position, rotation);
            aiVehicle.name = $"AI_Racer_{i+1}";
            
            // Configure AI controller
            AIVehicleController aiController = aiVehicle.GetComponent<AIVehicleController>();
            if (aiController != null)
            {
                // Set track reference
                aiController.trackGenerator = trackGenerator;
                
                // Set random difficulty parameters
                SetRandomDifficulty(aiController, i);
                
                aiRacers.Add(aiController);
            }
            else
            {
                // Add AI controller if not present
                aiController = aiVehicle.AddComponent<AIVehicleController>();
                aiController.trackGenerator = trackGenerator;
                SetRandomDifficulty(aiController, i);
                aiRacers.Add(aiController);
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