using UnityEngine;

namespace MarchingCubes {
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public sealed class VolumeRenderer : MonoBehaviour {
        public float[] Data = null;
        public int TriangleBudget = 65536 * 16;
        public float Isovalue = 0.5f;
        public float UVScale = 1f;

        private MeshBuilder _builder;
        private float _builtTargetValue;

        private void Start() {
            _builder = new MeshBuilder(MarchingCubes.ChunkSize + 1);
        }

        private void Update() {
            if (Isovalue == _builtTargetValue) return;

            _builder.BuildIsosurface(Data, Isovalue, MarchingCubes.Scale, UVScale);
            GetComponent<MeshFilter>().sharedMesh = _builder.Mesh;
            GetComponent<MeshCollider>().sharedMesh = _builder.Mesh;

            _builtTargetValue = Isovalue;
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
