using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class Chunk : MonoBehaviour
    {
        public int3 Coords;
        public Mesh Mesh;
        public bool Generating;
        public Mesh.MeshDataArray OutputMeshData;
        
        private JobHandle _handle;
        private NativeArray<int> _indexVertexCounts;
        private Stopwatch _sw;
        private NativeArray<float> _densities;

        private MeshGen _meshGen;
        public int Scale;
        public bool InQueue { get; set; }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_densities.IsCreated && _handle.IsCompleted)
            {
                _handle.Complete();
                var v3 = _meshGen.VoxelSide + 3;
                var delta = 1 /(float) _meshGen.VoxelSide;
                for (int i = 0; i < _densities.Length; i++)
                {
                    var coords = MeshGen.IndexToCoords(i, v3);
                    var d = _densities[i];
                    Gizmos.color = d < 0 ? Color.black : Color.green;
                    var vector3 = (float3)Coords + (float3)coords*delta;
                    Gizmos.DrawSphere(vector3, delta/16);
                    UnityEditor.Handles.Label(vector3, $"{vector3} {i}  {d:F2}");
                }
            }
        }
        #endif

        public void Setup(MeshGen meshGen)
        {
            _meshGen = meshGen;
        }

        private void Start()
        {
            _indexVertexCounts = new NativeArray<int>(2, Allocator.Persistent);
            Coords = new int3(int.MaxValue);
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

        public JobHandle RequestGeneration()
        {
            var densityCount = (_meshGen.VoxelSide + 3)*(_meshGen.VoxelSide + 3)*(_meshGen.VoxelSide + 3);
            if(!_densities.IsCreated || _densities.Length != densityCount)
            {
                if (_densities.IsCreated)
                    _densities.Dispose();
                _densities = new NativeArray<float>(densityCount, Allocator.Persistent);
            }
            _handle.Complete();
            var djob = new DensityJob
            {
                VoxelSide = _meshGen.VoxelSide,
                Coords = Coords,
                Densities = _densities,
                Scale = Scale,
                EvalGraph = _meshGen.DensityFormulaEvaluator
            };
            
            
            OutputMeshData = Mesh.AllocateWritableMeshData(1);

            var h = djob.ScheduleParallel(densityCount, 256, default);

            int maxIndexCount;
            int maxVertexCount;// maxCubeCount * 6 * 4;



            if (_meshGen.Algorithm == Algorithm.MarchingCube)
            {

                var maxCubeCount = _meshGen.VoxelSide * _meshGen.VoxelSide * _meshGen.VoxelSide;
                var maxTriCount = maxCubeCount * 5; // maxCubeCount * 6 /*faces*/ * 2 /*tri per face*/;
                maxIndexCount = maxTriCount * 3;
                maxVertexCount = maxIndexCount;
            }
            else
            {
                var vvv = (_meshGen.VoxelSide+1) * (_meshGen.VoxelSide+1) * (_meshGen.VoxelSide+1);
                maxVertexCount = vvv * 7;
                var maxTriCount = vvv * 6;
                maxIndexCount = maxTriCount * 3;
            }
            // Assert.IsTrue(maxIndexCount < ushort.MaxValue);
            OutputMeshData[0].SetIndexBufferParams(maxIndexCount, IndexFormat.UInt32);
            OutputMeshData[0].SetVertexBufferParams(maxVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));

            if (_meshGen.Algorithm == Algorithm.MarchingCube)
            {
                MarchingCubeJob job = new MarchingCubeJob
                {
                    Densities = _densities,
                    Scale = Scale,
                    VoxelSide = _meshGen.VoxelSide,
                    OutputMesh = this.OutputMeshData[0],
                    Coords = Coords,
                    IndexVertexCounts = this._indexVertexCounts,
                    EdgeTable = _meshGen.EdgeTable,
                    TriTable = _meshGen.TriTable,
                    EdgeConnection = _meshGen.EdgeConnection,
                    EdgeDirection = _meshGen.EdgeDirection,
                    
                };
                this._handle = job.Schedule(h);
            }
            else
            {

                var job = new DualContouringJob()
                {
                    EvalGraph = _meshGen.DensityFormulaEvaluator,
                    Densities = _densities,
                    EdgeTable = _meshGen.EdgeTable,
                    Scale = Scale,
                    VoxelSide = _meshGen.VoxelSide,
                    OutputMesh = this.OutputMeshData[0],
                    Coords = Coords,
                    IndexVertexCounts = this._indexVertexCounts,
                    EdgeConnection = _meshGen.EdgeConnection,
                    EdgeDirection = _meshGen.EdgeDirection,

                };
                this._handle = job.Schedule(h);
            }

            // Debug.Log($"Generate {Coords} max vert {maxVertexCount} max indices {maxIndexCount}");
            _sw = Stopwatch.StartNew();
            return _handle;
        }

        private void Update()
        {
            if (Generating && _handle.IsCompleted)
            {
                InQueue = false;
                _sw?.Stop();
                _handle.Complete();
                // Debug.Log($"Complete Chunk in {_sw.ElapsedMilliseconds}ms, Indices: {_indexVertexCounts[0]}, Vertices: {_indexVertexCounts[1]}");
                Generating = false;
                transform.localPosition = new Vector3(Coords.x, Coords.y, Coords.z);
                
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
                // var n = new List<Vector3>();
                // Mesh.RecalculateNormals(MeshUpdateFlags.DontNotifyMeshUsers);
                // Mesh.GetNormals(n);
                // foreach (var vector3 in n)
                // {
                //     Debug.Log(vector3);
                // }
            }
        }
    }
}