using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Track
{
    /// <summary>
    /// Generates and stores an optimal racing line for AI vehicles to follow
    /// </summary>
    public class RacingLine
    {
        private List<Vector3> _racingLinePoints = new List<Vector3>();
        private List<float> _recommendedSpeeds = new List<float>(); // Speed values at each point (0-1 normalized)
        
        public List<Vector3> Points => _racingLinePoints;
        public List<float> RecommendedSpeeds => _recommendedSpeeds;
        
        /// <summary>
        /// Generates an optimal racing line based on the track vertices
        /// </summary>
        /// <param name="trackVertices">The vertices of the track</param>
        /// <param name="trackWidth">The width of the track</param>
        /// <param name="resolution">Number of points to generate for the racing line</param>
        /// <param name="cornerCuttingFactor">How aggressively to cut corners (0-1)</param>
        public void Generate(List<Vector3> trackVertices, float trackWidth, int resolution, float cornerCuttingFactor = 0.7f)
        {
            if (trackVertices.Count < 3)
                return;
                
            _racingLinePoints.Clear();
            _recommendedSpeeds.Clear();
            
            // Increase the number of track vertices by interpolating between existing points
            List<Vector3> denseTrackVertices = InterpolateTrackVertices(trackVertices, resolution);
            
            // Create a smoothed racing line that cuts corners
            List<Vector3> smoothedLine = new List<Vector3>();
            
            // First pass: Create initial racing line by offsetting from track center
            for (int i = 0; i < denseTrackVertices.Count; i++)
            {
                int prevIndex = (i - 1 + denseTrackVertices.Count) % denseTrackVertices.Count;
                int nextIndex = (i + 1) % denseTrackVertices.Count;
                
                Vector3 prevPoint = denseTrackVertices[prevIndex];
                Vector3 currentPoint = denseTrackVertices[i];
                Vector3 nextPoint = denseTrackVertices[nextIndex];
                
                // Calculate direction vectors
                Vector3 toPrev = (prevPoint - currentPoint).normalized;
                Vector3 toNext = (nextPoint - currentPoint).normalized;
                
                // Calculate the angle between segments
                float angle = Vector3.Angle(toPrev, toNext);
                
                // Determine the side to offset based on the turn direction
                Vector3 cross = Vector3.Cross(toPrev, toNext);
                float turnDirection = Mathf.Sign(cross.y); // Positive for left turns, negative for right turns
                
                // Calculate offset based on corner angle and track width
                // Sharper corners get less offset to avoid cutting too much
                // Reduce the maximum offset to ensure racing line stays within track boundaries
                float offsetFactor = Mathf.Lerp(0.1f, cornerCuttingFactor * 0.5f, Mathf.InverseLerp(0, 90, angle));
                
                // Limit the offset to a percentage of track width to ensure it stays within bounds
                float maxOffsetDistance = trackWidth * 0.4f; // Maximum 40% of track width
                Vector3 offsetDirection = Vector3.Cross(toNext - toPrev, Vector3.up).normalized * turnDirection;
                Vector3 offset = offsetDirection * Mathf.Min(trackWidth * offsetFactor, maxOffsetDistance);
                
                // Apply offset to create racing line point
                Vector3 racingLinePoint = currentPoint + offset;
                smoothedLine.Add(racingLinePoint);
                
                // Calculate recommended speed based on corner sharpness
                // Sharper corners = slower speed
                float speedFactor = Mathf.Lerp(0.3f, 1.0f, Mathf.InverseLerp(0, 180, angle));
                _recommendedSpeeds.Add(speedFactor);
            }
            
            // Second pass: Apply advanced smoothing and ensure the racing line stays within track boundaries
            for (int i = 0; i < smoothedLine.Count; i++)
            {
                // Use a wider window for smoothing (5 points instead of 3)
                int idx1 = (i - 2 + smoothedLine.Count) % smoothedLine.Count;
                int idx2 = (i - 1 + smoothedLine.Count) % smoothedLine.Count;
                int idx3 = i;
                int idx4 = (i + 1) % smoothedLine.Count;
                int idx5 = (i + 2) % smoothedLine.Count;
                
                // Apply weighted smoothing with more weight to the current point
                Vector3 smoothedPoint = (smoothedLine[idx1] * 0.1f + 
                                       smoothedLine[idx2] * 0.2f + 
                                       smoothedLine[idx3] * 0.4f + 
                                       smoothedLine[idx4] * 0.2f + 
                                       smoothedLine[idx5] * 0.1f);
                
                // Get the corresponding track center point
                Vector3 trackCenterPoint = denseTrackVertices[i % denseTrackVertices.Count];
                
                // Calculate vector from track center to smoothed point
                Vector3 centerToPoint = smoothedPoint - trackCenterPoint;
                
                // More conservative boundary checking - reduce to 40% of track width for safety
                float maxSafeDistance = trackWidth * 0.4f;
                
                // Apply stricter boundary checking
                if (centerToPoint.magnitude > maxSafeDistance)
                {
                    // Clamp the point to be within the safe distance from track center
                    // Use a gradual approach to avoid sharp transitions
                    float clampFactor = Mathf.Lerp(1.0f, maxSafeDistance / centerToPoint.magnitude, 0.8f);
                    centerToPoint *= clampFactor;
                    smoothedPoint = trackCenterPoint + centerToPoint;
                }
                
                _racingLinePoints.Add(smoothedPoint);
            }
            
            // Third pass: Final smoothing pass to ensure C2 continuity
            List<Vector3> finalSmoothPass = new List<Vector3>(_racingLinePoints);
            _racingLinePoints.Clear();
            
            for (int i = 0; i < finalSmoothPass.Count; i++)
            {
                int prevIndex = (i - 1 + finalSmoothPass.Count) % finalSmoothPass.Count;
                int nextIndex = (i + 1) % finalSmoothPass.Count;
                
                // Apply a final light smoothing
                Vector3 finalPoint = finalSmoothPass[prevIndex] * 0.25f + 
                                    finalSmoothPass[i] * 0.5f + 
                                    finalSmoothPass[nextIndex] * 0.25f;
                
                _racingLinePoints.Add(finalPoint);
            }
            
            // Recalculate recommended speeds based on the final smoothed racing line
            _recommendedSpeeds.Clear();
            
            for (int i = 0; i < _racingLinePoints.Count; i++)
            {
                int prevIndex = (i - 1 + _racingLinePoints.Count) % _racingLinePoints.Count;
                int nextIndex = (i + 1) % _racingLinePoints.Count;
                
                // Calculate the angle between the current point and its neighbors
                Vector3 toPrev = (_racingLinePoints[prevIndex] - _racingLinePoints[i]).normalized;
                Vector3 toNext = (_racingLinePoints[nextIndex] - _racingLinePoints[i]).normalized;
                
                // Calculate the angle between segments
                float angle = Vector3.Angle(toPrev, toNext);
                
                // Calculate curvature - sharper turns have higher curvature
                float curvature = 1.0f - (Vector3.Dot(toPrev, toNext) + 1.0f) / 2.0f;
                
                // Calculate recommended speed based on curvature
                // Use a more nuanced curve: slower in sharp corners, faster in straights
                float speedFactor = Mathf.Lerp(0.3f, 1.0f, Mathf.Pow(1.0f - curvature, 0.7f));
                
                // Ensure smooth speed transitions by limiting speed changes between adjacent points
                if (i > 0)
                {
                    float prevSpeed = _recommendedSpeeds[i - 1];
                    float maxChange = 0.1f; // Maximum allowed speed change between points
                    speedFactor = Mathf.Clamp(speedFactor, prevSpeed - maxChange, prevSpeed + maxChange);
                }
                
                _recommendedSpeeds.Add(speedFactor);
            }
            
            // Apply a final smoothing pass to the speeds
            List<float> smoothedSpeeds = new List<float>(_recommendedSpeeds);
            _recommendedSpeeds.Clear();
            
            for (int i = 0; i < smoothedSpeeds.Count; i++)
            {
                int prev = (i - 1 + smoothedSpeeds.Count) % smoothedSpeeds.Count;
                int next = (i + 1) % smoothedSpeeds.Count;
                
                // Weighted average for smoother speed transitions
                float smoothedSpeed = smoothedSpeeds[prev] * 0.25f + 
                                     smoothedSpeeds[i] * 0.5f + 
                                     smoothedSpeeds[next] * 0.25f;
                
                _recommendedSpeeds.Add(smoothedSpeed);
            }
        }
        
        /// <summary>
        /// Interpolates between track vertices to create a denser set of points using Catmull-Rom splines
        /// </summary>
        /// <param name="trackVertices">Original track vertices</param>
        /// <param name="resolution">Target number of points</param>
        /// <returns>Interpolated track vertices</returns>
        private List<Vector3> InterpolateTrackVertices(List<Vector3> trackVertices, int resolution)
        {
            List<Vector3> denseVertices = new List<Vector3>();
            
            if (trackVertices.Count < 4)
            {
                // Fall back to simple linear interpolation if we don't have enough points for splines
                int pointsPerSegment = Mathf.Max(1, resolution / trackVertices.Count);
                
                for (int i = 0; i < trackVertices.Count; i++)
                {
                    Vector3 currentPoint = trackVertices[i];
                    Vector3 nextPoint = trackVertices[(i + 1) % trackVertices.Count];
                    
                    // Add the current point
                    denseVertices.Add(currentPoint);
                    
                    // Add interpolated points between current and next
                    for (int j = 1; j < pointsPerSegment; j++)
                    {
                        float t = (float)j / pointsPerSegment;
                        Vector3 interpolatedPoint = Vector3.Lerp(currentPoint, nextPoint, t);
                        denseVertices.Add(interpolatedPoint);
                    }
                }
                
                return denseVertices;
            }
            
            // Calculate how many interpolated points to add between each pair of vertices
            // We want approximately 'resolution' total points around the track
            int pointsPerSplineSegment = Mathf.Max(1, resolution / trackVertices.Count);
            
            // Use Catmull-Rom splines for smoother curves
            for (int i = 0; i < trackVertices.Count; i++)
            {
                // Get four points for the spline (p0, p1, p2, p3)
                // For a closed loop, we wrap around at the edges
                Vector3 p0 = trackVertices[(i - 1 + trackVertices.Count) % trackVertices.Count];
                Vector3 p1 = trackVertices[i];
                Vector3 p2 = trackVertices[(i + 1) % trackVertices.Count];
                Vector3 p3 = trackVertices[(i + 2) % trackVertices.Count];
                
                // Add the current point
                denseVertices.Add(p1);
                
                // Add interpolated points using Catmull-Rom spline
                for (int j = 1; j < pointsPerSplineSegment; j++)
                {
                    float t = (float)j / pointsPerSplineSegment;
                    Vector3 interpolatedPoint = CatmullRomPoint(p0, p1, p2, p3, t);
                    denseVertices.Add(interpolatedPoint);
                }
            }
            
            return denseVertices;
        }
        
        /// <summary>
        /// Calculates a point on a Catmull-Rom spline
        /// </summary>
        /// <param name="p0">First control point</param>
        /// <param name="p1">Second control point (start of segment)</param>
        /// <param name="p2">Third control point (end of segment)</param>
        /// <param name="p3">Fourth control point</param>
        /// <param name="t">Interpolation parameter (0-1)</param>
        /// <returns>Interpolated point on the spline</returns>
        private Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // Catmull-Rom spline formula
            float t2 = t * t;
            float t3 = t2 * t;
            
            // Coefficients for the Catmull-Rom spline
            float a = -0.5f * t3 + t2 - 0.5f * t;
            float b = 1.5f * t3 - 2.5f * t2 + 1.0f;
            float c = -1.5f * t3 + 2.0f * t2 + 0.5f * t;
            float d = 0.5f * t3 - 0.5f * t2;
            
            // Calculate the point on the spline
            return a * p0 + b * p1 + c * p2 + d * p3;
        }
        
        /// <summary>
        /// Gets the closest point on the racing line to the given position
        /// </summary>
        public (int index, Vector3 point, float speed) GetClosestPoint(Vector3 position)
        {
            if (_racingLinePoints.Count == 0)
                return (-1, Vector3.zero, 1.0f);
                
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            
            for (int i = 0; i < _racingLinePoints.Count; i++)
            {
                float distance = Vector3.Distance(position, _racingLinePoints[i]);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            return (closestIndex, _racingLinePoints[closestIndex], _recommendedSpeeds[closestIndex]);
        }
        
        /// <summary>
        /// Gets the next target point on the racing line
        /// </summary>
        public (Vector3 point, float speed) GetNextTargetPoint(int currentIndex, int lookaheadPoints = 5)
        {
            if (_racingLinePoints.Count == 0)
                return (Vector3.zero, 1.0f);
                
            int targetIndex = (currentIndex + lookaheadPoints) % _racingLinePoints.Count;
            return (_racingLinePoints[targetIndex], _recommendedSpeeds[targetIndex]);
        }
    }
}