using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubes {
    public sealed class MeshBuilder : System.IDisposable {
        public Mesh Mesh => _mesh;

        public MeshBuilder(int size, int budget, ComputeShader compute)
          => Initialize((size, size, size), budget, compute);

        public void Dispose()
          => ReleaseAll();

        public void BuildIsosurface(ComputeBuffer voxels, float target, float scale)
          => RunCompute(voxels, target, scale);

        private (int x, int y, int z) _grids;
        private int _triangleBudget;
        private ComputeShader _compute;

        private void Initialize((int, int, int) dims, int budget, ComputeShader compute) {
            _grids = dims;
            _triangleBudget = budget;
            _compute = compute;

            AllocateBuffers();
            AllocateMesh(3 * _triangleBudget);
        }

        private void ReleaseAll() {
            ReleaseBuffers();
            ReleaseMesh();
        }

        private void RunCompute(ComputeBuffer voxels, float target, float scale) {
            _counterBuffer.SetCounterValue(0);

            // Isosurface reconstruction
            _compute.SetInts("Dims", _grids);
            _compute.SetInt("MaxTriangle", _triangleBudget);
            _compute.SetFloat("Scale", scale);
            _compute.SetFloat("Isovalue", target);
            _compute.SetBuffer(0, "TriangleTable", _triangleTable);
            _compute.SetBuffer(0, "Voxels", voxels);
            _compute.SetBuffer(0, "VertexBuffer", _vertexBuffer);
            _compute.SetBuffer(0, "IndexBuffer", _indexBuffer);
            _compute.SetBuffer(0, "Counter", _counterBuffer);
            _compute.DispatchThreads(0, _grids);

            // Clear unused area of the buffers.
            _compute.SetBuffer(1, "VertexBuffer", _vertexBuffer);
            _compute.SetBuffer(1, "IndexBuffer", _indexBuffer);
            _compute.SetBuffer(1, "Counter", _counterBuffer);
            _compute.DispatchThreads(1, 1024, 1, 1);

            // Bounding box
            var ext = new Vector3(_grids.x, _grids.y, _grids.z) * scale;
            _mesh.bounds = new Bounds(Vector3.zero, ext);
        }

        private ComputeBuffer _triangleTable;
        private ComputeBuffer _counterBuffer;

        private void AllocateBuffers() {
            // Marching cubes triangle table
            _triangleTable = new ComputeBuffer(256, sizeof(ulong));
            _triangleTable.SetData(PrecalculatedData.TriangleTable);

            // Buffer for triangle counting
            _counterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
        }

        private void ReleaseBuffers() {
            _triangleTable.Dispose();
            _counterBuffer.Dispose();
        }

        private Mesh _mesh;
        private GraphicsBuffer _vertexBuffer;
        private GraphicsBuffer _indexBuffer;

        private void AllocateMesh(int vertexCount) {
            _mesh = new Mesh();

            // We want GraphicsBuffer access as Raw (ByteAddress) buffers.
            _mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            // Vertex position: float32 x 3
            var vp = new VertexAttributeDescriptor
              (VertexAttribute.Position, VertexAttributeFormat.Float32, 3);

            // Vertex normal: float32 x 3
            var vn = new VertexAttributeDescriptor
              (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);

            // Vertex/index buffer formats
            _mesh.SetVertexBufferParams(vertexCount, vp, vn);
            _mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);

            // Submesh initialization
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount),
                             MeshUpdateFlags.DontRecalculateBounds);

            // GraphicsBuffer references
            _vertexBuffer = _mesh.GetVertexBuffer(0);
            _indexBuffer = _mesh.GetIndexBuffer();
        }

        void ReleaseMesh() {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            Object.Destroy(_mesh);
        }
    }
}
