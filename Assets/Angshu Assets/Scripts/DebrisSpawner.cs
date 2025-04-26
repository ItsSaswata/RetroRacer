using UnityEngine;

public class DebrisSpawner : MonoBehaviour {
    public GameObject debrisPrefab;
    public int numberOfDebris = 20;
    public Vector3 spawnArea = new Vector3(10, 10, 10);

    void Start() {
        for (int i = 0; i < numberOfDebris; i++) {
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x),
                Random.Range(-spawnArea.y, spawnArea.y),
                Random.Range(-spawnArea.z, spawnArea.z)
            );
            Instantiate(debrisPrefab, randomPos, Random.rotation);
        }
    }
}
