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
        public JobHandle Handle;
        public bool Generating;
        public Mesh.MeshDataArray OutputMeshData;
        public NativeArray<int> IndexVertexCounts;
        private Stopwatch _sw;

        private void Start()
        {
            IndexVertexCounts = new NativeArray<int>(2, Allocator.Persistent);
            Coords = new int2(int.MaxValue, int.MaxValue);
            Mesh = new Mesh();
            GetComponent<MeshFilter>().sharedMesh = Mesh;
        }

        private void OnDestroy()
        {
            IndexVertexCounts.Dispose();
        }

        private void Update()
        {
            if (Generating && Handle.IsCompleted)
            {
                _sw.Stop();
                Handle.Complete();
                Debug.Log($"Complete Chunk in {_sw.ElapsedMilliseconds}ms, Indices: {IndexVertexCounts[0]}, Vertices: {IndexVertexCounts[1]}");
                Generating = false;
                transform.position = new Vector3(Coords.x, 0, Coords.y);
                
                var sm = new SubMeshDescriptor(0, IndexVertexCounts[0], MeshTopology.Triangles)
                {
                    firstVertex = 0,
                    vertexCount = IndexVertexCounts[1],
                };


                var meshData = OutputMeshData[0];
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, sm);
                Mesh.ApplyAndDisposeWritableMeshData(OutputMeshData, Mesh);
            }
        }

        public void RequestGeneration()
        {
            _sw = Stopwatch.StartNew();
        }
    }
}