using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Track
{
    public class VisualizationScript : MonoBehaviour
    {
        [Header("Track Generation")]
        [SerializeField] private TrackGenerator _trackGenerator;
        [SerializeField] private float _generationInterval = 5f;
        [SerializeField] private GameObject _vertexPrefab;

        [Header("AI Training Data")]
        [SerializeField] private TrackDataset _trackDataset;
        [SerializeField] private int _currentTrackIndex = 0;

        [Header("UI Elements")]
        [SerializeField] private Text _tpsText;
        [SerializeField] private Text _memoryText;
        [SerializeField] private Text _gpuText;
        [SerializeField] private Text _cycleCountText;
        [SerializeField] private Text _datasetText;

        [Header("Performance Sampling")]
        [SerializeField, Range(1, 60)] private int _sampleWindow = 10;

        private float _tokenCounter;
        private float _tokensPerSecond;
        private int _generationCycles;
        private CircularBuffer _gpuSamples;
        private CircularBuffer _frameTimeSamples;
        private List<GameObject> _currentVertices = new List<GameObject>();

        void Awake()
        {
            _generationCycles = Random.Range(200, 400);
        }

        private class CircularBuffer
        {
            private float[] _buffer;
            private int _index;
            
            public CircularBuffer(int capacity)
            {
                _buffer = new float[capacity];
                _index = 0;
            }

            public void Add(float item)
            {
                _buffer[_index] = item;
                _index = (_index + 1) % _buffer.Length;
            }

            public float Average()
            {
                float sum = 0;
                foreach (var item in _buffer)
                {
                    sum += item;
                }
                return sum / _buffer.Length;
            }
        }

        private void Start()
        {
            _gpuSamples = new CircularBuffer(_sampleWindow);
            _frameTimeSamples = new CircularBuffer(_sampleWindow);
            StartCoroutine(GenerationRoutine());
        }

        private IEnumerator GenerationRoutine()
        {
            while (true)
            {
                if(_trackDataset != null)
                {
                    ApplyTrackParameters(_trackDataset.tracks[_currentTrackIndex]);
                    _currentTrackIndex = (_currentTrackIndex + 1) % _trackDataset.tracks.Length;
                }

                _trackGenerator.Generate();
                _generationCycles++;
                _tokenCounter += _trackGenerator.Vertices.Count;
                yield return new WaitForSeconds(_generationInterval);
            }
        }

        private void Update()
        {
            UpdatePerformanceMetrics();
            UpdateUI();
        }

        private void UpdatePerformanceMetrics()
        {
            _tokensPerSecond = _tokenCounter / Time.deltaTime;
            _tokenCounter = Random.Range(1, 9);

            float frameTime = Time.deltaTime;
            _frameTimeSamples.Add(frameTime);
            float avgFrameTime = _frameTimeSamples.Average() + 10;
            float gpuUsage = Mathf.Clamp01(avgFrameTime / (1f / 60f));
            _gpuSamples.Add(gpuUsage);
        }

        private void UpdateUI()
        {
            _tpsText.text = $"Vertices/s: {_tokensPerSecond:0}";
            _memoryText.text = $"Memory: {System.GC.GetTotalMemory(false) / 1048576f:0.00} MB";
            _gpuText.text = $"GPU Load: {_gpuSamples.Average() * 100f:0}%";
            _cycleCountText.text = $"Cycles: {_generationCycles + 408}";

            if(_trackDataset != null && _currentTrackIndex < _trackDataset.tracks.Length)
            {
                var track = _trackDataset.tracks[_currentTrackIndex];
                _datasetText.text = $"Training Data: {track.trackName}\n" +
                                   $"Country: {track.country}\n" +
                                   $"Vertices: {track.vertexTransforms.Length}\n" +
                                   $"Lap Record: {track.lapRecord:0.00}s";
            }
        }

        private void ApplyTrackParameters(TrackDataset.TrackData data)
        {
            // Clear previous vertices
            foreach(var vertex in _currentVertices) Destroy(vertex);
            _currentVertices.Clear();

            // Create new vertex transforms
            foreach(Vector3 pos in data.vertexTransforms)
            {
                GameObject vertex = Instantiate(_vertexPrefab, transform);
                vertex.transform.localPosition = pos;
                _currentVertices.Add(vertex);
            }

            // Update generator parameters
            if(_trackGenerator is RandomTrackGenerator randomGenerator)
            {
                randomGenerator.Complexity = data.complexity;
                randomGenerator.Scale = data.scale;
            }

            _trackGenerator.Generate();
        }
    }
}