using UnityEngine;

namespace MarchingCubes {
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public class MarchingCubesVolume : MonoBehaviour {
        public float[] Data = null;

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;

        public MeshFilter MeshFilter => _meshFilter;
        public MeshCollider MeshCollider => _meshCollider;

        private void Awake() {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();

            var mesh = new Mesh();
            _meshFilter.sharedMesh = mesh;
            _meshCollider.sharedMesh = mesh;
        }

        private void OnDestroy() {
            if (_meshFilter.sharedMesh != null) {
                DestroyImmediate(_meshFilter.sharedMesh);
            }

            if (_meshCollider.sharedMesh != null) {
                DestroyImmediate(_meshCollider.sharedMesh);
            }
        }

        private void OnEnable() {
            MarchingCubesManager.Register(this);
        }

        private void OnDisable() {
            MarchingCubesManager.Unregister(this);
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.gray;
            int chunkSize = MarchingCubesManager.Instance.ChunkSize;
            float scale = MarchingCubesManager.Instance.Scale;
            Vector3 size = new(
                chunkSize * scale,
                chunkSize * scale,
                chunkSize * scale
            );
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
