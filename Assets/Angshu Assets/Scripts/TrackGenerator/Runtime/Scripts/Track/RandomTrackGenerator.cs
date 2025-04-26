using UnityEngine;

namespace Track
{
    public class RandomTrackGenerator : TrackGenerator
    {
        // Modified properties with public setters
        [field: SerializeField, Tooltip("The size of the track. The number of voronoi cells that'll be used to generate the track.")]
        public int Size { get; set; } = 10;

        [field: SerializeField, Tooltip("The scale of the track.")]
        public float Scale { get; set; } = 25;
        
        [field: SerializeField, Range(0f, 1f)] 
        public float Complexity { get; set; } = .5f;

        protected override Path GetPath()
        {
            return new RandomPath(Size, Scale, Complexity);
        }
    }
}