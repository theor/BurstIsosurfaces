using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
        }

        public void RequestGeneration(int2 coords, MeshGen meshGen)
        {
            var job = new MeshGen.GenJob
            {
                voxelSide = meshGen.VoxelSide,
                scaleFactor = meshGen.ScaleFactor,
                outputMesh = this.OutputMeshData[0],
                coords = coords,
                indexVertexCounts = this._indexVertexCounts,
            };
            var maxCubeCount = meshGen.VoxelSide * meshGen.VoxelSide * meshGen.VoxelSide;
            var maxTriCount = maxCubeCount * 6 /*faces*/ * 2 /*tri per face*/;
            var maxIndexCount = maxTriCount * 3;
            job.outputMesh.SetIndexBufferParams(maxIndexCount, IndexFormat.UInt32);
            var maxVertexCount = maxCubeCount * 6 * 4;
            job.outputMesh.SetVertexBufferParams(maxVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));
            this._handle.Complete();
            this._handle = job.Schedule();
            Debug.Log($"Generate {coords} max vert {maxVertexCount} max indices {maxIndexCount}");
            _sw = Stopwatch.StartNew();
        }

        private void Update()
        {
            if (Generating && _handle.IsCompleted)
            {
                _sw.Stop();
                _handle.Complete();
                Debug.Log($"Complete Chunk in {_sw.ElapsedMilliseconds}ms, Indices: {_indexVertexCounts[0]}, Vertices: {_indexVertexCounts[1]}");
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
                meshData.SetSubMesh(0, sm, MeshUpdateFlags.DontRecalculateBounds);
                Mesh.bounds = sm.bounds;
                Mesh.ApplyAndDisposeWritableMeshData(OutputMeshData, Mesh, MeshUpdateFlags.DontRecalculateBounds);
            }
        }
    }
}