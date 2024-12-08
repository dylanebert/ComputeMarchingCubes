using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

namespace MarchingCubes {
    public sealed class VolumeDataVisualizer : MonoBehaviour {
        [SerializeField] private TextAsset _volumeData = null;
        [SerializeField] private Vector3Int _dimensions = new(64, 64, 64);
        [SerializeField] private float _gridScale = 1.0f / 64;
        [SerializeField] private int _triangleBudget = 65536 * 16;

        [SerializeField] private ComputeShader _builderCompute = null;

        [CreateProperty] public float TargetValue { get; set; } = 0.4f;
        private float _builtTargetValue;

        private int VoxelCount => _dimensions.x * _dimensions.y * _dimensions.z;

        private ComputeBuffer _voxelBuffer;
        private MeshBuilder _builder;

        private void Start() {
            _voxelBuffer = new ComputeBuffer(VoxelCount, sizeof(float));
            _builder = new MeshBuilder(_dimensions, _triangleBudget, _builderCompute);

            float[] voxelData = new float[VoxelCount];
            System.Buffer.BlockCopy(_volumeData.bytes, 0, voxelData, 0, voxelData.Length * sizeof(float));
            _voxelBuffer.SetData(voxelData);

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

        private void OnDrawGizmos() {
            Gizmos.color = Color.gray;
            Vector3 size = new(_dimensions.x * _gridScale, _dimensions.y * _gridScale, _dimensions.z * _gridScale);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
