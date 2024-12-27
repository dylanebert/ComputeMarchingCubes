using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MarchingCubes {
    public class MarchingCubesManager : MonoBehaviour {
        [SerializeField] private int _chunkSize = 16;
        [SerializeField] private float _scale = 1f;
        [SerializeField] private float _isovalue = 0.5f;
        [SerializeField] private float _uvScale = 1f;

        private List<MarchingCubesVolume> _volumes = new();

        private NativeArray<ulong> _triangleTable;
        private NativeArray<int2> _edgeVertices;
        private NativeArray<int3> _cornerVertices;

        private JobHandle _jobHandle;
        private NativeStream _stream;
        private NativeArray<float> _data;
        private bool _rebuildScheduled = false;
        private bool _needsAllocate = false;
        private bool _needsRebuild = false;

        public static MarchingCubesManager Instance { get; private set; }

        public static int ChunkSize => Instance._chunkSize;
        public static float Scale => Instance._scale;

        private void Awake() {
            ulong[] triangleTable = PrecalculatedData.TriangleTable;
            _triangleTable = new NativeArray<ulong>(triangleTable.Length, Allocator.Persistent);
            _triangleTable.CopyFrom(triangleTable);

            (int, int)[] edgeVertices = PrecalculatedData.EdgeVertices;
            _edgeVertices = new NativeArray<int2>(edgeVertices.Length, Allocator.Persistent);
            for (int i = 0; i < edgeVertices.Length; i++) {
                _edgeVertices[i] = new int2(edgeVertices[i].Item1, edgeVertices[i].Item2);
            }

            (int, int, int)[] cornerVertices = PrecalculatedData.CornerVertices;
            _cornerVertices = new NativeArray<int3>(cornerVertices.Length, Allocator.Persistent);
            for (int i = 0; i < cornerVertices.Length; i++) {
                _cornerVertices[i] = new int3(cornerVertices[i].Item1, cornerVertices[i].Item2, cornerVertices[i].Item3);
            }

            _data = new NativeArray<float>(0, Allocator.Persistent);
            _stream = new NativeStream(0, Allocator.Persistent);
        }

        private void OnDestroy() {
            if (_rebuildScheduled) {
                _jobHandle.Complete();
            }

            _triangleTable.Dispose();
            _edgeVertices.Dispose();
            _cornerVertices.Dispose();
            _data.Dispose();
            _stream.Dispose();
        }

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("Multiple MarchingCubes instances found in scene. Destroying duplicate.");
#if UNITY_EDITOR
                DestroyImmediate(gameObject);
#else
                Destroy(gameObject);
#endif
                return;
            }

            Instance = this;
        }

        private void Update() {
            if (_needsAllocate) {
                Allocate();
                _needsAllocate = false;
            }

            if (_rebuildScheduled) {
                if (_jobHandle.IsCompleted) {
                    ApplyMesh();
                    _rebuildScheduled = false;
                }
            }
            else {
                if (_needsRebuild) {
                    Build();
                    _needsRebuild = false;
                }
            }
        }

        private void Allocate() {
            _data.Dispose();

            int paddedChunkSize = _chunkSize + 1;
            int chunkDims = paddedChunkSize * paddedChunkSize * paddedChunkSize;
            int totalVoxels = _volumes.Count * chunkDims;
            _data = new NativeArray<float>(totalVoxels, Allocator.Persistent);
            for (int i = 0; i < _volumes.Count; i++) {
                var volume = _volumes[i];
                var voxels = volume.Data;
                int offset = i * chunkDims;
                for (int j = 0; j < chunkDims; j++) {
                    _data[offset + j] = voxels[j];
                }
            }
        }

        private void Build() {
            if (_rebuildScheduled && !_jobHandle.IsCompleted) {
                Debug.LogWarning("Job already running");
                return;
            }

            _stream.Dispose();
            _stream = new NativeStream(_volumes.Count, Allocator.Persistent);

            _jobHandle = new Job {
                Data = _data,
                TriangleTable = _triangleTable,
                EdgeVertices = _edgeVertices,
                CornerVertices = _cornerVertices,
                ChunkSize = _chunkSize + 1,
                Isovalue = _isovalue,
                Scale = _scale,
                UVScale = _uvScale,
                StreamWriter = _stream.AsWriter(),
            }.Schedule(_volumes.Count, 1);

            _rebuildScheduled = true;
        }

        private void ApplyMesh() {
            if (!_jobHandle.IsCompleted) {
                Debug.LogWarning("Job not completed");
                return;
            }

            _jobHandle.Complete();

            var reader = _stream.AsReader();
            for (int i = 0; i < _volumes.Count; i++) {
                reader.BeginForEachIndex(i);

                var mesh = _volumes[i].MeshFilter.sharedMesh;
                mesh.Clear();

                NativeList<Vector3> positions = new(Allocator.Temp);
                NativeList<Vector3> normals = new(Allocator.Temp);
                NativeList<Vector2> uvs = new(Allocator.Temp);
                NativeList<int> indices = new(Allocator.Temp);

                int positionsCount = reader.Read<int>();
                for (int j = 0; j < positionsCount; j++) {
                    positions.Add(reader.Read<Vector3>());
                }

                int normalsCount = reader.Read<int>();
                for (int j = 0; j < normalsCount; j++) {
                    normals.Add(reader.Read<Vector3>());
                }

                int uvsCount = reader.Read<int>();
                for (int j = 0; j < uvsCount; j++) {
                    uvs.Add(reader.Read<Vector2>());
                }

                int indicesCount = reader.Read<int>();
                for (int j = 0; j < indicesCount; j++) {
                    indices.Add(reader.Read<int>());
                }

                reader.EndForEachIndex();

                mesh.SetVertices(positions.AsArray());
                mesh.SetNormals(normals.AsArray());
                mesh.SetUVs(0, uvs.AsArray());
                mesh.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0);

                positions.Dispose();
                normals.Dispose();
                uvs.Dispose();
                indices.Dispose();

                _volumes[i].MeshCollider.sharedMesh = mesh;
            }
        }

        public static void Register(MarchingCubesVolume volume) {
            Instance._volumes.Add(volume);
            Instance._needsAllocate = true;
            Instance._needsRebuild = true;
        }

        public static void Unregister(MarchingCubesVolume volume) {
            Instance._volumes.Remove(volume);
            Instance._needsAllocate = true;
            Instance._needsRebuild = true;
        }

        [BurstCompile]
        public struct Job : IJobParallelFor {
            [ReadOnly] public NativeArray<float> Data;
            [ReadOnly] public NativeArray<ulong> TriangleTable;
            [ReadOnly] public NativeArray<int2> EdgeVertices;
            [ReadOnly] public NativeArray<int3> CornerVertices;
            [ReadOnly] public int ChunkSize;
            [ReadOnly] public float Isovalue;
            [ReadOnly] public float Scale;
            [ReadOnly] public float UVScale;

            public NativeStream.Writer StreamWriter;

            public void Execute(int index) {
                NativeList<float3> positions = new(Allocator.Temp);
                NativeList<float3> normals = new(Allocator.Temp);
                NativeList<float2> uvs = new(Allocator.Temp);
                NativeList<int> indices = new(Allocator.Temp);

                int dataOffset = index * ChunkSize * ChunkSize * ChunkSize;

                for (int z = 0; z < ChunkSize - 1; z++) {
                    for (int y = 0; y < ChunkSize - 1; y++) {
                        for (int x = 0; x < ChunkSize - 1; x++) {
                            NativeArray<float> cornerValues = new(8, Allocator.Temp);
                            NativeArray<float3> cornerPositions = new(8, Allocator.Temp);
                            for (int i = 0; i < 8; i++) {
                                int3 corner = CornerVertices[i];

                                int gx = corner.x + x;
                                int gy = corner.y + y;
                                int gz = corner.z + z;

                                int dataIndex = gx + gy * ChunkSize + gz * ChunkSize * ChunkSize;
                                cornerValues[i] = Data[dataIndex + dataOffset];

                                float fx = (gx + 0.5f - ChunkSize * 0.5f) * Scale;
                                float fy = (gy + 0.5f - ChunkSize * 0.5f) * Scale;
                                float fz = (gz + 0.5f - ChunkSize * 0.5f) * Scale;

                                cornerPositions[i] = new float3(fx, fy, fz);
                            }

                            int cubeIndex = 0;
                            for (int i = 0; i < 8; i++) {
                                if (cornerValues[i] < Isovalue) {
                                    cubeIndex |= 1 << i;
                                }
                            }

                            if (cubeIndex == 0 || cubeIndex == 255) {
                                cornerValues.Dispose();
                                cornerPositions.Dispose();
                                continue;
                            }

                            ulong packed = TriangleTable[cubeIndex];
                            uint triX = (uint)(packed & 0xFFFFFFFF);
                            uint triY = (uint)(packed >> 32);

                            NativeArray<float3> edgeVerts = new(12, Allocator.Temp);
                            for (int i = 0; i < 12; i++) {
                                int2 edge = EdgeVertices[i];
                                int v0 = edge.x;
                                int v1 = edge.y;

                                float valueA = cornerValues[v0];
                                float valueB = cornerValues[v1];
                                float t = (Isovalue - valueA) / (valueB - valueA);

                                edgeVerts[i] = math.lerp(cornerPositions[v0], cornerPositions[v1], t);
                            }

                            for (int i = 0; i < 15; i += 3) {
                                int e1 = EdgeIndexFromTriangleTable(triX, triY, i);
                                int e2 = EdgeIndexFromTriangleTable(triX, triY, i + 1);
                                int e3 = EdgeIndexFromTriangleTable(triX, triY, i + 2);

                                if (e1 == 15) break;

                                int triangleIndex = positions.Length;

                                float3 v1 = edgeVerts[e1];
                                float3 v2 = edgeVerts[e2];
                                float3 v3 = edgeVerts[e3];

                                float3 normal = math.normalize(math.cross(v2 - v1, v3 - v1));

                                float2 uv1 = new float2(0, 0) * UVScale;
                                float2 uv2 = new float2(1, 0) * UVScale;
                                float2 uv3 = new float2(0, 1) * UVScale;

                                positions.Add(v1);
                                positions.Add(v2);
                                positions.Add(v3);

                                normals.Add(normal);
                                normals.Add(normal);
                                normals.Add(normal);

                                uvs.Add(uv1);
                                uvs.Add(uv2);
                                uvs.Add(uv3);

                                indices.Add(triangleIndex);
                                indices.Add(triangleIndex + 1);
                                indices.Add(triangleIndex + 2);
                            }

                            cornerValues.Dispose();
                            cornerPositions.Dispose();
                            edgeVerts.Dispose();
                        }
                    }
                }

                StreamWriter.BeginForEachIndex(index);

                StreamWriter.Write(positions.Length);
                for (int i = 0; i < positions.Length; i++) {
                    StreamWriter.Write(positions[i]);
                }

                StreamWriter.Write(normals.Length);
                for (int i = 0; i < normals.Length; i++) {
                    StreamWriter.Write(normals[i]);
                }

                StreamWriter.Write(uvs.Length);
                for (int i = 0; i < uvs.Length; i++) {
                    StreamWriter.Write(uvs[i]);
                }

                StreamWriter.Write(indices.Length);
                for (int i = 0; i < indices.Length; i++) {
                    StreamWriter.Write(indices[i]);
                }

                StreamWriter.EndForEachIndex();

                positions.Dispose();
                normals.Dispose();
                uvs.Dispose();
                indices.Dispose();
            }

            private readonly int EdgeIndexFromTriangleTable(uint x, uint y, int index) {
                return (int)(0xfu & (index < 8 ? x >> ((index + 0) * 4) : y >> ((index - 8) * 4)));
            }
        }
    }
}
