using UnityEngine;

[CreateAssetMenu(fileName = "New Track Dataset", menuName = "AI/Track Dataset")]
public class TrackDataset : ScriptableObject
{
    [System.Serializable]
    public struct TrackData
    {
        public string trackName;
        public string country;
        public int seed;
        public float complexity;
        public float scale;
        public Vector3[] vertexTransforms; 
        public float lapRecord;
    }

    public TrackData[] tracks = new TrackData[100];
}