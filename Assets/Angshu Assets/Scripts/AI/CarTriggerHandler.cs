using UnityEngine;

public class CarTriggerHandler : MonoBehaviour
{
    [Header("References")]
    public AIRaceManager raceManager;
    
    [Header("Fall Detection")]
    [SerializeField] private float fallThreshold = -50f; // Y position below which car is considered fallen
    [SerializeField] private float fallCheckInterval = 0.5f; // How often to check for falling
    
    private float lastFallCheck = 0f;
    
    private void Start()
    {
        // Find the race manager if not assigned
        if (raceManager == null)
        {
            raceManager = FindFirstObjectByType<AIRaceManager>();
            if (raceManager == null)
            {
                Debug.LogError("No AIRaceManager found in scene. CarTriggerHandler will not function properly.");
                enabled = false;
                return;
            }
        }
    }
    
    private void Update()
    {
        // Periodic fall detection as backup
        if (Time.time - lastFallCheck > fallCheckInterval)
        {
            lastFallCheck = Time.time;
            
            // Check if car has fallen below threshold
            if (transform.position.y < fallThreshold)
            {
                Debug.Log($"{gameObject.name} detected as fallen (Y: {transform.position.y:F1}) - teleporting to safety");
                raceManager.HandleCarFall(gameObject);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (raceManager == null) return;
        
        // Handle fall detection
        if (other.CompareTag("Fall"))
        {
            raceManager.HandleCarFall(gameObject);
        }
        
        // Handle checkpoint detection
        if (other.CompareTag("Checkpoint"))
        {
            raceManager.HandleCheckpoint(gameObject, other.transform);
        }
        
        // Handle start/finish line detection
        if (other.CompareTag("StartFinish"))
        {
            raceManager.HandleStartFinish(gameObject);
        }
    }
} 