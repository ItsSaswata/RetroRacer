#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class TrackDatasetGenerator : EditorWindow
{
    private TrackDataset dataset;
    private readonly string[] countries = {
        "USA", "Monaco", "Italy", "Belgium", "Japan",
        "Brazil", "UK", "Germany", "Spain", "UAE"
    };

    [MenuItem("AI/Generate Track Dataset")]
    public static void ShowWindow()
    {
        GetWindow<TrackDatasetGenerator>("Dataset Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Generate Fake F1 Track Dataset", EditorStyles.boldLabel);
        dataset = (TrackDataset)EditorGUILayout.ObjectField("Dataset", dataset, typeof(TrackDataset), false);
        if (GUILayout.Button("Generate 100 Tracks")) GenerateFakeTracks();
    }

    private void GenerateFakeTracks()
    {
        dataset.tracks = new TrackDataset.TrackData[100];
        
        for(int i = 0; i < 100; i++)
        {
            string country = countries[Random.Range(0, countries.Length)];
            int vertexCount = Random.Range(50, 61); // 50-60 vertices
            Vector3[] vertices = new Vector3[vertexCount];
            float scale = Random.Range(20f, 40f);

            // Generate random vertex positions
            for(int v = 0; v < vertexCount; v++)
            {
                vertices[v] = new Vector3(
                    Random.Range(-scale, scale),
                    0,
                    Random.Range(-scale, scale)
                );
            }

            dataset.tracks[i] = new TrackDataset.TrackData
            {
                trackName = $"GP_{country}_{i+1:000}",
                country = country,
                seed = Random.Range(0, 99999),
                complexity = Random.Range(0.3f, 0.9f),
                scale = scale,
                vertexTransforms = vertices,
                lapRecord = Random.Range(78.5f, 125.5f)
            };
        }
        
        EditorUtility.SetDirty(dataset);
        AssetDatabase.SaveAssets();
    }
}
#endif