using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace UnityTemplateProjects
{
    public class Chunk : MonoBehaviour
    {
        public int2 Coords;
        public Mesh Mesh;
        public bool Generating;
        public Mesh.MeshDataArray OutputMeshData;
        
        private JobHandle _handle;
        private NativeArray<int> _indexVertexCounts;
        private Stopwatch _sw;
        private NativeArray<float> _densities;

        private MeshGen _meshGen;

        private void OnDrawGizmos()
        {
            if (_densities.IsCreated && _handle.IsCompleted)
            {
                _handle.Complete();
                var v1 = _meshGen.VoxelSide + 1;
                var delta = 1 /(float) _meshGen.VoxelSide;
                for (int i = 0; i < v1*v1*v1; i++)
                {
                    var coords = MeshGen.IndexToCoords(i, v1);
                    var d = _densities[i];
                    Gizmos.color = d < 0 ? Color.black : Color.green;
                    Gizmos.DrawWireSphere((float3)coords*delta + new float3(Coords.x, 0, Coords.y), delta/2);
                }
            }
        }

        public void Setup(MeshGen meshGen)
        {
            _meshGen = meshGen;
        }

        private void Start()
        {
            _indexVertexCounts = new NativeArray<int>(2, Allocator.Persistent);
            Coords = new int2(int.MaxValue, int.MaxValue);
            Mesh = new Mesh();
            GetComponent<MeshFilter>().sharedMesh = Mesh;
        }

        private void OnDestroy()
        {
            _indexVertexCounts.Dispose();
            if(_densities.IsCreated)
                _densities.Dispose();
        }

        public unsafe void GetCoords(in MeshGen.OctInt coords, out MeshGen.OctFloat densities)
        {
            for (int i = 0; i < 8; i++)
            {
                densities.Value[i] = _densities[coords.Value[i]];
            }
        }

        public void RequestGeneration(int2 coords)
        {
            var densityCount = (_meshGen.VoxelSide + 1)*(_meshGen.VoxelSide + 1)*(_meshGen.VoxelSide + 1);
            var djob = new MeshGen.DensityJob
            {
                VoxelSide = _meshGen.VoxelSide,
                Coords = coords,
                Densities = _densities = new NativeArray<float>(densityCount, Allocator.Persistent)
            };

            var h = djob.Schedule(densityCount, 16);
            var job = new MeshGen.GenJob
            {
                Densities = _densities,
                VoxelSide = _meshGen.VoxelSide,
                OutputMesh = this.OutputMeshData[0],
                Coords = coords,
                IndexVertexCounts = this._indexVertexCounts,
                EdgeTable = _meshGen.EdgeTable,
                TriTable = _meshGen.TriTable,
                EdgeConnection = _meshGen.EdgeConnection,
                EdgeDirection = _meshGen.EdgeDirection,
                
            };
            var maxCubeCount = _meshGen.VoxelSide * _meshGen.VoxelSide * _meshGen.VoxelSide;
            var maxTriCount = maxCubeCount * 6 /*faces*/ * 2 /*tri per face*/;
            var maxIndexCount = maxTriCount * 3;
            job.OutputMesh.SetIndexBufferParams(maxIndexCount, IndexFormat.UInt32);
            var maxVertexCount = maxCubeCount * 6 * 4;
            job.OutputMesh.SetVertexBufferParams(maxVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
            this._handle.Complete();
            this._handle = job.Schedule(h);
            Debug.Log($"Generate {coords} max vert {maxVertexCount} max indices {maxIndexCount}");
            _sw = Stopwatch.StartNew();
        }

        private void Update()
        {
            if (Generating && _handle.IsCompleted)
            {
                _sw.Stop();
                _handle.Complete();
                // Debug.Log($"Complete Chunk in {_sw.ElapsedMilliseconds}ms, Indices: {_indexVertexCounts[0]}, Vertices: {_indexVertexCounts[1]}");
                Generating = false;
                transform.position = new Vector3(Coords.x, 0, Coords.y);
                
                var sm = new SubMeshDescriptor(0, _indexVertexCounts[0], MeshTopology.Triangles)
                {
                    firstVertex = 0,
                    vertexCount = _indexVertexCounts[1],
                    bounds = new Bounds(Vector3.one * .5f, Vector3.one )
                };


                var meshData = OutputMeshData[0];
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, sm, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                Mesh.bounds = sm.bounds;
                Mesh.ApplyAndDisposeWritableMeshData(OutputMeshData, Mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            }
        }
    }
}