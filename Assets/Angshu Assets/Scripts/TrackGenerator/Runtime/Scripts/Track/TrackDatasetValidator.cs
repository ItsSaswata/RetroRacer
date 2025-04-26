#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class TrackDatasetValidator : EditorWindow
{
    [MenuItem("AI/Validate Dataset")]
    public static void ValidateDataset()
    {
        TrackDataset dataset = AssetDatabase.LoadAssetAtPath<TrackDataset>("Assets/F1_TrainingDataset.asset");
        int errors = 0;

        foreach(var track in dataset.tracks)
        {
            if(!track.trackName.Contains(track.country))
            {
                Debug.LogError($"Mismatch: {track.trackName} ≠ {track.country}");
                errors++;
            }
            if(track.vertexTransforms.Length < 50 || track.vertexTransforms.Length > 60)
            {
                Debug.LogError($"Bad vertex count: {track.trackName} ({track.vertexTransforms.Length})");
                errors++;
            }
        }

        Debug.Log($"Validation complete. {errors} errors found.");
    }
}
#endif