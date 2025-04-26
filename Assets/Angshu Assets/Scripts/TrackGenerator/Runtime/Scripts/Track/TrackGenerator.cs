using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Track
{
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class TrackGenerator : MonoBehaviour
    {
        [field: SerializeField, Tooltip("The resolution of the track. The number of segments that'll be used to generate the track mesh.")]
        public int Resolution { get; private set; } = 10000;

        [field: SerializeField, Tooltip("The width of the track.")]
        public float Width { get; private set; } = 3f;

        [field: SerializeField, Tooltip("Add a Mesh Collider to the track.")]
        public bool GenerateCollider { get; private set; } = true;

        [Header("Terrain Integration")]
        [SerializeField, Tooltip("Reference to the terrain the track should follow")]
        private Terrain _terrain;

        [SerializeField, Tooltip("Vertical offset above terrain surface")]
        private float _terrainOffset = 0.1f;

        [Header("Track Verticality")]
        [SerializeField, Tooltip("Maximum height variation from base terrain")]
        private float _maxVerticality = 5f;

        [SerializeField, Tooltip("Scale of verticality noise")]
        private float _verticalityNoiseScale = 0.5f;

        [Header("Track Thickness")]
        [SerializeField, Tooltip("Vertical thickness of the track mesh.")]
        private float _thickness = 1.0f;

        [Header("Material Animation")]
        [SerializeField] private float _integrationDuration = 2f;
        [SerializeField] private AnimationCurve _integrationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Debug Visualization")]
        [SerializeField] private bool _showDebugLines = false;
        [SerializeField] private bool _showVertexNormals = false;
        [SerializeField] private Color _debugColor = Color.cyan;
        [SerializeField] private float _debugLineLength = 2f;

        private static readonly int IntegrationValueProperty = Shader.PropertyToID("_IntegrationValue");
        private bool _isAnimatingMaterial = false;

        private SplineContainer _splineContainer;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        [SerializeField] private List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> Vertices => vertices;

        public Spline Spline
        {
            get => _splineContainer.Spline;
            set => _splineContainer.Spline = value;
        }

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (GenerateCollider && !TryGetComponent<MeshCollider>(out _meshCollider))
            {
                _meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }

        public void Generate()
        {
            StartCoroutine(StartRoutines());
        }

        public IEnumerator StartRoutines()
        {
            StartCoroutine(GenerateVertices());
            yield return new WaitForSeconds(0.4f);
            StartCoroutine(GenerateSpline());
            yield return new WaitForSeconds(0.4f);
            StartCoroutine(GenerateMesh());
        }

        protected abstract Path GetPath();

        public IEnumerator GenerateVertices()
        {
            yield return new WaitForSeconds(0.01f);
            using (Path path = GetPath())
            {
                path.Generate(transform);
                vertices = path.Vertices.ConvertAll(v => (Vector3)v);

                // Apply terrain height and verticality
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 worldPos = transform.TransformPoint(vertices[i]);

                    // Get base terrain height
                    float terrainHeight = _terrain != null ?
                        _terrain.SampleHeight(worldPos) :
                        worldPos.y;

                    // Add procedural verticality
                    float verticality = Mathf.PerlinNoise(
                        worldPos.x * _verticalityNoiseScale,
                        worldPos.z * _verticalityNoiseScale
                    ) * _maxVerticality;

                    // Combine terrain and verticality
                    worldPos.y = terrainHeight + _terrainOffset + verticality;
                    vertices[i] = transform.InverseTransformPoint(worldPos);
                }
            }
        }

        public IEnumerator GenerateSpline()
        {
            yield return new WaitForSeconds(0.01f);
            Spline.Clear();

            foreach (Vector3 vertex in vertices)
            {
                Spline.Add(transform.InverseTransformPoint(vertex));
            }

            Spline.Closed = true;
        }

        public IEnumerator GenerateMesh()
        {
            yield return new WaitForSeconds(0.01f);
            int vLength = (Resolution + 2) * 2;

            NativeArray<float3> verticesNative = new NativeArray<float3>(vLength, Allocator.TempJob);
            NativeArray<float2> uvs = new NativeArray<float2>(vLength, Allocator.TempJob);
            NativeArray<int> triangles = new NativeArray<int>(vLength * 3, Allocator.TempJob);
            NativeSpline spline = new NativeSpline(Spline, Allocator.TempJob);

            new CalculateUVsAndVerticesJob
            {
                Vertices = verticesNative,
                UVs = uvs,
                Length = vLength,
                Spline = spline,
                Width = Width
            }.Schedule(vLength, 8).Complete();

            new CalculateTrianglesJob
            {
                Triangles = triangles,
                VerticesLength = vLength
            }.Schedule(triangles.Length, 8).Complete();

            Spline = new Spline(spline, true);

            // Convert job output to mesh
            Vector3[] topVertices = Array.ConvertAll(verticesNative.ToArray(), v => (Vector3)v);

            // Add thickness by duplicating vertices downward
            Vector3[] allVertices = new Vector3[topVertices.Length * 2];
            List<int> allTriangles = new List<int>(triangles.ToArray());

            // Copy top vertices and create bottom vertices
            for (int i = 0; i < topVertices.Length; i++)
            {
                allVertices[i] = topVertices[i]; // Top vertex
                allVertices[i + topVertices.Length] = new Vector3(
                    topVertices[i].x,
                    topVertices[i].y - _thickness,
                    topVertices[i].z
                ); // Bottom vertex
            }

            // Generate side triangles with corrected winding order
            for (int i = 0; i < topVertices.Length - 2; i += 2)
            {
                // Current segment indices
                int tl = i;          // Top left
                int tr = i + 1;      // Top right
                int bl = i + topVertices.Length;     // Bottom left
                int br = i + 1 + topVertices.Length; // Bottom right

                // Next segment indices (for connecting sides between segments)
                int ntl = i + 2;     // Next top left
                int ntr = i + 3;     // Next top right
                int nbl = i + 2 + topVertices.Length; // Next bottom left
                int nbr = i + 3 + topVertices.Length; // Next bottom right

                // Left side (facing outward)
                allTriangles.Add(tl); allTriangles.Add(bl); allTriangles.Add(ntl);
                allTriangles.Add(ntl); allTriangles.Add(bl); allTriangles.Add(nbl);

                // Right side (facing outward)
                allTriangles.Add(tr); allTriangles.Add(ntr); allTriangles.Add(br);
                allTriangles.Add(br); allTriangles.Add(ntr); allTriangles.Add(nbr);

                // Bottom face (optional)
                allTriangles.Add(bl); allTriangles.Add(br); allTriangles.Add(nbl);
                allTriangles.Add(nbl); allTriangles.Add(br); allTriangles.Add(nbr);
            }

            // Fix UVs (same as before)
            Vector2[] originalUVs = Array.ConvertAll(uvs.ToArray(), v => (Vector2)v);
            Vector2[] allUVs = new Vector2[originalUVs.Length * 2];
            originalUVs.CopyTo(allUVs, 0);
            originalUVs.CopyTo(allUVs, originalUVs.Length);

            // Update mesh
            _meshFilter.mesh.Clear();
            _meshFilter.mesh.vertices = allVertices;
            _meshFilter.mesh.uv = allUVs;
            _meshFilter.mesh.triangles = allTriangles.ToArray();

            // Fixed Y-axis tiling at 40
            if (_meshRenderer.sharedMaterial != null)
            {
                _meshRenderer.sharedMaterial.SetFloat(IntegrationValueProperty, 0f);
                if (!_isAnimatingMaterial)
                {
                    StartCoroutine(AnimateIntegrationValue());
                }
                _meshRenderer.sharedMaterial.mainTextureScale = new Vector2(1, 40);
            }

            _meshFilter.mesh.RecalculateNormals();

            if (_meshCollider != null && GenerateCollider)
            {
                _meshCollider.sharedMesh = _meshFilter.sharedMesh;
            }

            // Debug visualization
            if (_showDebugLines)
            {
                DrawDebugLines();
            }

            // Dispose
            verticesNative.Dispose();
            uvs.Dispose();
            triangles.Dispose();
            spline.Dispose();
        }

        private IEnumerator AnimateIntegrationValue()
        {
            _isAnimatingMaterial = true;
            Material material = _meshRenderer.sharedMaterial;
            float elapsedTime = 0f;

            // Reset integration value at start
            material.SetFloat(IntegrationValueProperty, 0f);

            while (elapsedTime < _integrationDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / _integrationDuration;
                float curveValue = _integrationCurve.Evaluate(normalizedTime);

                material.SetFloat(IntegrationValueProperty, curveValue);
                yield return null;
            }

            // Ensure we end at exactly 1
            material.SetFloat(IntegrationValueProperty, 1f);
            _isAnimatingMaterial = false;
        }

        private void Update()
        {
            if (_showDebugLines)
            {
                // Add pulsing effect to lines
                float pulse = Mathf.PingPong(Time.time, 1f);
                _debugColor.a = pulse;

                // Randomly shift line length for "scanning" effect
                _debugLineLength = 1.5f + Mathf.Sin(Time.time) * 0.5f;
            }
        }

        private void DrawDebugLines()
        {
            if (vertices.Count < 2) return;

            // Draw main track path
            for (int i = 0; i < vertices.Count - 1; i++)
            {
                Vector3 start = transform.TransformPoint(vertices[i]);
                Vector3 end = transform.TransformPoint(vertices[i + 1]);
                Debug.DrawLine(start, end, _debugColor);

                // Add geeky perpendicular lines
                Vector3 mid = Vector3.Lerp(start, end, 0.5f);
                Vector3 perpendicular = Vector3.Cross((end - start).normalized, Vector3.up);
                Debug.DrawLine(mid, mid + perpendicular * _debugLineLength, Color.magenta);
            }

            // Draw connection between first and last vertex
            Debug.DrawLine(
                transform.TransformPoint(vertices[0]),
                transform.TransformPoint(vertices[vertices.Count - 1]),
                _debugColor
            );

            if (_showVertexNormals)
            {
                // Draw fake normals with random directions
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 worldPos = transform.TransformPoint(vertex);
                    Vector3 randomDir = UnityEngine.Random.onUnitSphere * _debugLineLength;
                    Debug.DrawLine(worldPos, worldPos + randomDir, Color.green);
                }
            }

            // Add computational-looking grid
            float gridSize = 10f;
            Vector3 boundsCenter = transform.TransformPoint(vertices[0]);
            for (float x = -gridSize; x <= gridSize; x += gridSize / 2)
            {
                for (float z = -gridSize; z <= gridSize; z += gridSize / 2)
                {
                    Vector3 gridPoint = boundsCenter + new Vector3(x, 0, z);
                    Debug.DrawLine(gridPoint, gridPoint + Vector3.up * 0.5f, Color.yellow);
                }
            }
        }
    }
}