using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubes {
    public class MeshBuilderCPU {
        public Mesh Mesh => _mesh;

        private List<Vector3> _positions = new();
        private List<Vector3> _normals = new();
        private List<Vector2> _uvs = new();
        private List<int> _indices = new();

        private Mesh _mesh;
        private int _size;

        private static readonly (int, int)[] EdgeVertices = new (int, int)[12] {
            (0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7)
        };

        private static readonly (int, int, int)[] CornerVertices = new (int, int, int)[8] {
            (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)
        };

        public MeshBuilderCPU(int size) {
            _size = size;
            _mesh = new Mesh() {
                indexFormat = IndexFormat.UInt32,
            };
        }

        public void BuildIsosurface(float[] voxels, float isovalue, float scale, float uvScale) {
            _positions.Clear();
            _normals.Clear();
            _uvs.Clear();
            _indices.Clear();

            int Index(int x, int y, int z) => x + y * _size + z * _size * _size;

            for (int z = 0; z < _size - 1; z++) {
                for (int y = 0; y < _size - 1; y++) {
                    for (int x = 0; x < _size - 1; x++) {
                        float[] cornerValues = new float[8];
                        Vector3[] cornerPositions = new Vector3[8];
                        for (int i = 0; i < 8; i++) {
                            (int cx, int cy, int cz) = CornerVertices[i];

                            int gx = cx + x;
                            int gy = cy + y;
                            int gz = cz + z;

                            float value = voxels[Index(gx, gy, gz)];
                            cornerValues[i] = value;

                            float fx = (gx + 0.5f - _size * 0.5f) * scale;
                            float fy = (gy + 0.5f - _size * 0.5f) * scale;
                            float fz = (gz + 0.5f - _size * 0.5f) * scale;
                            cornerPositions[i] = new Vector3(fx, fy, fz);
                        }

                        int cubeIndex = 0;
                        for (int i = 0; i < 8; i++) {
                            if (cornerValues[i] < isovalue) {
                                cubeIndex |= 1 << i;
                            }
                        }

                        if (cubeIndex == 0 || cubeIndex == 255) continue;

                        ulong packed = PrecalculatedData.TriangleTable[cubeIndex];
                        uint triX = (uint)(packed & 0xFFFFFFFF);
                        uint triY = (uint)(packed >> 32);

                        Vector3[] edgeVerts = new Vector3[12];
                        for (int i = 0; i < 12; i++) {
                            (int v0, int v1) = EdgeVertices[i];

                            float valueA = cornerValues[v0];
                            float valueB = cornerValues[v1];
                            float t = (isovalue - valueA) / (valueB - valueA);

                            edgeVerts[i] = Vector3.Lerp(cornerPositions[v0], cornerPositions[v1], t);
                        }

                        for (int i = 0; i < 15; i += 3) {
                            int e1 = EdgeIndexFromTriangleTable(triX, triY, i);
                            int e2 = EdgeIndexFromTriangleTable(triX, triY, i + 1);
                            int e3 = EdgeIndexFromTriangleTable(triX, triY, i + 2);

                            if (e1 == 15) break;

                            int index = _positions.Count;

                            Vector3 v1 = edgeVerts[e1];
                            Vector3 v2 = edgeVerts[e2];
                            Vector3 v3 = edgeVerts[e3];

                            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                            Vector2 uv1 = new Vector2(0, 0) * uvScale;
                            Vector2 uv2 = new Vector2(1, 0) * uvScale;
                            Vector2 uv3 = new Vector2(0, 1) * uvScale;

                            _positions.Add(v1);
                            _positions.Add(v2);
                            _positions.Add(v3);

                            _normals.Add(normal);
                            _normals.Add(normal);
                            _normals.Add(normal);

                            _uvs.Add(uv1);
                            _uvs.Add(uv2);
                            _uvs.Add(uv3);

                            _indices.Add(index);
                            _indices.Add(index + 1);
                            _indices.Add(index + 2);
                        }
                    }
                }
            }

            _mesh.Clear();

            _mesh.SetVertices(_positions);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0);

            Vector3 extent = new Vector3(_size, _size, _size) * scale;
            _mesh.bounds = new Bounds(Vector3.zero, extent);
        }

        private static int EdgeIndexFromTriangleTable(uint x, uint y, int index) {
            return (int)(0xfu & (index < 8 ? x >> ((index + 0) * 4) : y >> ((index - 8) * 4)));
        }
    }
}
