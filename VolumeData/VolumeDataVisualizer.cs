using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

namespace MarchingCubes {
    public sealed class VolumeDataVisualizer : MonoBehaviour {
        [SerializeField] private TextAsset _volumeData = null;
        [SerializeField] private Vector3Int _dimensions = new(256, 256, 113);
        [SerializeField] private float _gridScale = 4.0f / 256;
        [SerializeField] private int _triangleBudget = 65536 * 16;

        [SerializeField, HideInInspector] private ComputeShader _converterCompute = null;
        [SerializeField, HideInInspector] private ComputeShader _builderCompute = null;

        [CreateProperty] public float TargetValue { get; set; } = 0.4f;
        private float _builtTargetValue;

        private int VoxelCount => _dimensions.x * _dimensions.y * _dimensions.z;

        private ComputeBuffer _voxelBuffer;
        private MeshBuilder _builder;

        private void Start() {
            _voxelBuffer = new ComputeBuffer(VoxelCount, sizeof(float));
            _builder = new MeshBuilder(_dimensions, _triangleBudget, _builderCompute);

            // Voxel data conversion (ushort -> float)
            using var readBuffer = new ComputeBuffer(VoxelCount / 2, sizeof(uint));
            readBuffer.SetData(_volumeData.bytes);

            _converterCompute.SetInts("Dims", _dimensions);
            _converterCompute.SetBuffer(0, "Source", readBuffer);
            _converterCompute.SetBuffer(0, "Voxels", _voxelBuffer);
            _converterCompute.DispatchThreads(0, _dimensions);

            // UI data source
            FindFirstObjectByType<UIDocument>().rootVisualElement.dataSource = this;
        }

        private void OnDestroy() {
            _voxelBuffer.Dispose();
            _builder.Dispose();
        }

        private void Update() {
            // Rebuild the isosurface only when the target value has been changed.
            if (TargetValue == _builtTargetValue) return;

            _builder.BuildIsosurface(_voxelBuffer, TargetValue, _gridScale);
            GetComponent<MeshFilter>().sharedMesh = _builder.Mesh;

            _builtTargetValue = TargetValue;
        }
    }
}
