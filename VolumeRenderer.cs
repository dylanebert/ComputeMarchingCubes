using UnityEngine;

namespace MarchingCubes {
    [RequireComponent(typeof(MeshFilter))]
    public sealed class VolumeRenderer : MonoBehaviour {
        public float[] Data = null;
        public int TriangleBudget = 65536 * 16;
        public ComputeShader BuilderCompute = null;
        public float TargetValue = 0.4f;
        public float UVScale = 1f;

        private ComputeBuffer _voxelBuffer;
        private MeshBuilder _builder;
        private float _builtTargetValue;

        private void Start() {
            int paddedChunkSize = MarchingCubes.ChunkSize + 1;
            int voxelCount = paddedChunkSize * paddedChunkSize * paddedChunkSize;
            _voxelBuffer = new ComputeBuffer(voxelCount, sizeof(float));
            _builder = new MeshBuilder(paddedChunkSize, TriangleBudget, BuilderCompute);
            if (Data != null) {
                _voxelBuffer.SetData(Data);
            }
            else {
                Debug.LogWarning("No data provided for VolumeRenderer");
                float[] voxelData = new float[voxelCount];
                for (int i = 0; i < voxelData.Length; i++) {
                    voxelData[i] = 0f;
                }
                _voxelBuffer.SetData(voxelData);
            }
        }

        private void OnDestroy() {
            _voxelBuffer.Dispose();
            _builder.Dispose();
        }

        private void Update() {
            if (TargetValue == _builtTargetValue) return;

            _builder.BuildIsosurface(_voxelBuffer, TargetValue, MarchingCubes.Scale, UVScale);
            GetComponent<MeshFilter>().sharedMesh = _builder.Mesh;

            _builtTargetValue = TargetValue;
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.gray;
            Vector3 size = new(
                MarchingCubes.ChunkSize * MarchingCubes.Scale,
                MarchingCubes.ChunkSize * MarchingCubes.Scale,
                MarchingCubes.ChunkSize * MarchingCubes.Scale
            );
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
