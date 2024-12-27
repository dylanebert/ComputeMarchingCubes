using UnityEngine;

using static MarchingCubes.MarchingCubesManager;

namespace MarchingCubes {
    public class MarchingCubesVolume : MonoBehaviour {
        public float[] Data = null;
        public float Isovalue = 0.5f;
        public float UVScale = 1f;

        private MeshBuilder _builder;

        private void Start() {
            _builder = new MeshBuilder(ChunkSize + 1);
        }

        private void Update() {
            _builder.BuildIsosurface(Data, Isovalue, Scale, UVScale);
            GetComponent<MeshFilter>().sharedMesh = _builder.Mesh;
            GetComponent<MeshCollider>().sharedMesh = _builder.Mesh;
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.gray;
            Vector3 size = new(
                ChunkSize * Scale,
                ChunkSize * Scale,
                ChunkSize * Scale
            );
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
