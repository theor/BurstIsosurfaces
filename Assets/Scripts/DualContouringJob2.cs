using Eval.Runtime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{

    /*
     * v:         0          2 - 3     < this one
     *  paper:  /              / | 
     *    2 - 3              0   1 
     *        |     
     *        1      
     * v:     1   0
     *        | /
     *    2 - 3
     *
     * v': 1 --- 5    v': 7 --- 6
     *    /|    /|       /|    /|
     *   2 +-- 6 |      4 +-- 5 |
     *   | 0 --+ 4      | 3 --+ 2
     *   |/    |/       |/    |/
     *   3 --- 7        0 --- 1
     *  paper           actual from marching cube
     */

    public struct DualContouringJob2 : IJob
    {
        public int VoxelSide;
        public float Isolevel;
        [WriteOnly]
        public NativeArray<EdgeCase> edgeMasks;

        [ReadOnly]
        public NativeArray<float> Densities;



        public void Execute()
        {

            
            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;
                
            //0.25 - dist(coords, 0.5)
            // 0.25 - y(coords)
            var edgeCases = EdgeCase.EdgeCases(Allocator.Temp);
                
            for (int x = 0; x < v1; x++)
            {
                for (int y = 0; y < v1; y++)
                {
                    for (int z = 0; z < v1; z++)
                    {
                        var localCoords = new int3(x, y, z);

                        MeshGen.GetCornerCoords(localCoords, v3, out var corners);

                        MeshGen.OctFloat voxelDensities;
                        for (int j = 0; j < 8; j++) voxelDensities[j] = Densities[corners[j]];

                        // Debug.Log($"voxel {coords} {localCoords} = {coords+localCoords}\ncorners {corners}\ndensities {voxelDensities}\nbool {voxelDensitiesBool}");
                        
                        byte cubeindex = 0;
                        if (voxelDensities[5] < Isolevel) cubeindex |= 1;
                        if (voxelDensities[2] < Isolevel) cubeindex |= 2;
                        if (voxelDensities[7] < Isolevel) cubeindex |= 4;
                        if (voxelDensities[6] < Isolevel) cubeindex |= 8;

                        var edgeCase = edgeCases[cubeindex];

                        // Debug.Log($"{x} {y} {z} index {cubeindex:X} mask {edgeCase.VertexCount}");

                        // alloc vertex and index
                        // index is in base 1 (so 0 is invalid)
                        var vertIndex = MeshGen.CoordsToIndex(localCoords, v1);
                        
                        edgeMasks[vertIndex] = edgeCase;
                    }
                }
            }

 
            
        }
    }

    public struct DualContouringJob2Phase2 : IJob
    {
        public int VoxelSide;
        public int Scale;
        public int3 Coords;
        public Mesh.MeshData OutputMesh;
        // [WriteOnly]
        public NativeArray<int> IndexVertexCounts;
        [ReadOnly]
        public NativeArray<EdgeCase> edgeMasks;
        [ReadOnly]
        public EvalGraph EvalGraph;

        public NativeArray<float3> outputVerts;
        public NativeArray<int> vertIndices;
        
        public unsafe void Execute()
        {
            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;
            var delta = Scale / ((float)VoxelSide);
            
            EvalState evalState = new EvalState();
            var outputNormals = OutputMesh.GetVertexData<float3>(stream:1);
            var outputTris = OutputMesh.GetIndexData<int>();
            // var vertIndices = new NativeArray<int>(edgeMasks.Length, Allocator.Temp);
            
            int nextVertexIndex = 0;

            int outputIndexIndex = 0;


            // delta = Scale / (VoxelSide - 1f);
            for (int x = 0; x < VoxelSide; x++)
            {
                for (int y = 0; y < VoxelSide; y++)
                {
                    for (int z = 0; z < VoxelSide; z++)
                    {
                        var localCoords = new int3(x, y, z);
                        var vertIndex = MeshGen.CoordsToIndex(localCoords, v1);
                        var edgeCase = edgeMasks[vertIndex];
                        // Debug.Log($"{localCoords} mask verts {edgeCase.VertexCount}");
                        if(edgeCase.VertexCount == 0)
                            continue;
                        for (int i = 0; i < edgeCase.VertexCount/4; i++)
                        {
                            ProcessEdge(edgeCase.Vertices+i*4, edgeCase.Flipped == 1, 
                                x, y, z,
                                v1,
                                ref vertIndices,
                                delta,
                                ref outputVerts,
                                ref nextVertexIndex, 
                                ref outputNormals,
                                ref evalState,
                                ref outputTris,
                                ref outputIndexIndex);
                        }
                    }
                }
            }

            IndexVertexCounts[0] = outputIndexIndex;
            IndexVertexCounts[1] = nextVertexIndex;
            UnsafeUtility.MemClear((int*)outputTris.GetUnsafePtr()+outputIndexIndex, UnsafeUtility.SizeOf<int>()* (outputTris.Length - outputIndexIndex));

        }
        
        float3 SampleNormal(ref EvalState evalState, float3 pos)
        {
            // var v3 = VoxelSide + 3;

            unsafe float d(ref EvalState evalState, in EvalGraph g, float3 delta)
            {
                const float e = 0.01f;
                var p = pos + e * delta;
                return evalState.Run(g, &p).x;
            }
            return
                new float3(
                    d(ref evalState, EvalGraph, new float3(-1, 0, 0)) -
                    d(ref evalState, EvalGraph, new float3(1, 0, 0)),
                    d(ref evalState, EvalGraph, new float3(0, -1, 0)) -
                    d(ref evalState, EvalGraph, new float3(0, 1, 0)),
                    d(ref evalState, EvalGraph, new float3(0, 0, -1)) -
                    d(ref evalState, EvalGraph, new float3(0, 0, 1))
                );
        }

        private void AddTriIndex(int3 c, int v1, ref NativeArray<int> vertIndices, float delta,
            ref NativeArray<float3> outputVerts, 
            ref int nextVertexIndex, 
            ref NativeArray<float3> outputNormals, 
            ref EvalState evalState, 
            ref NativeArray<int> outputTris,
            ref int outputIndexIndex)
        {
            var coordsToIndex = MeshGen.CoordsToIndex(c, v1);
            // Debug.Log($"Add {c} vi {vertIndices[coordsToIndex]-1} at {ind}");
            // indices in BASE 1

            // vertex doesn't exist yet
            if (vertIndices[coordsToIndex] == 0)
            {
                var pos = (float3) c * delta + new float3(.5f,-.5f,.5f) * delta;
                outputVerts[nextVertexIndex] = pos;
                outputNormals[nextVertexIndex] = SampleNormal(ref evalState, Coords + pos - 0.5f * delta);

                // Debug.Log($"create vertex at {c} vi {nextVertexIndex} index {coordsToIndex}");
                vertIndices[coordsToIndex] = nextVertexIndex+1;
                nextVertexIndex++;
            }

            outputTris[outputIndexIndex++] = vertIndices[coordsToIndex] - 1;
        }

        private unsafe void ProcessEdge(in byte* vertices, bool flipped, int x, int y, int z, int v1,
            ref NativeArray<int> vertIndices,
            float delta,
            ref NativeArray<float3> outputVerts,
            ref int nextVertexIndex,
            ref NativeArray<float3> outputNormals,
            ref EvalState evalState,
            ref NativeArray<int> outputTris, 
            ref int outputIndexIndex)
        {
            int3 getV(byte vIndex) =>
                vIndex switch
                {
                    0 => new int3(x, y, z),
                    1 => new int3(x + 1, y, z),
                    2 => new int3(x + 1, y, z + 1),
                    3 => new int3(x, y, z + 1),
                    4 => new int3(x, y + 1, z),
                    5 => new int3(x + 1, y + 1, z),
                    6 => new int3(x + 1, y + 1, z + 1),
                    7 => new int3(x, y + 1, z + 1),
                    // 0 => new int3(x -1, y-1, z-1),
                    // 1 => new int3(x , y-1, z-1),
                    // 2 => new int3(x , y-1, z ),
                    // 3 => new int3(x-1, y-1, z ),
                    // 4 => new int3(x-1, y , z-1),
                    // 5 => new int3(x , y , z-1),
                    // 6 => new int3(x , y , z ),
                    // 7 => new int3(x, y , z ),
                    _ => throw new System.NotImplementedException(),
                };// - new int3(1);

            if (!flipped)
            {
                AddTriIndex(getV(vertices[0]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[2]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[1]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);

                AddTriIndex(getV(vertices[0]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[3]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[2]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
            }
            else
            {
                AddTriIndex(getV(vertices[0]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[1]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[2]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);

                AddTriIndex(getV(vertices[0]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[2]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
                AddTriIndex(getV(vertices[3]), v1, ref vertIndices, delta, ref outputVerts, ref nextVertexIndex, ref outputNormals, ref evalState,  ref outputTris, ref outputIndexIndex);
            }
        }
    }

    public struct DualContouringJob2Smooth : IJob
    {
        public int VoxelSide;
        public float Isolevel;
        public int Scale;
        [ReadOnly]
        public NativeArray<float> Densities;
        [ReadOnly]
        public NativeArray<ushort> EdgeTable;
        [ReadOnly]
        public NativeArray<Marching.byte2> EdgeConnection;
        [ReadOnly]
        public NativeArray<float3> EdgeDirection;

        public NativeArray<int> vertIndices;
        public NativeArray<float3> outputVerts;
        
        public unsafe void Execute()
        {
            int voxelSide = VoxelSide;
            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;

            var delta = Scale / ((float)VoxelSide);
            
            
            Marching.byte3* vertexOffsets = stackalloc Marching.byte3[8];
            vertexOffsets[0] = new Marching.byte3(0, 0, 0);
            vertexOffsets[1] = new Marching.byte3(1, 0, 0);
            vertexOffsets[2] = new Marching.byte3(1, 0, 1);
            vertexOffsets[3] = new Marching.byte3(0, 0, 1);
            vertexOffsets[4] = new Marching.byte3(0, 1, 0);
            vertexOffsets[5] = new Marching.byte3(1, 1, 0);
            vertexOffsets[6] = new Marching.byte3(1, 1, 1);
            vertexOffsets[7] = new Marching.byte3(0, 1, 1);
            
            // if(Smooth)
            for (int x = 0; x <= voxelSide; x++)
            {
                for (int y = 0; y <= voxelSide; y++)
                {
                    for (int z = 0; z <= voxelSide; z++)
                    {
                        var localCoords = new int3(x, y, z);
                        
                        float3 mean = float3.zero;
                        int meanCount = 0;

                        MeshGen.GetCornerCoords(localCoords, v3, out var corners);

                        // Debug.Log($"Smooth from {localCoords}: {string.Join(",", corners.AsArray().Select(c => MeshGen.IndexToCoords(c, v3)))}");

                        MeshGen.OctFloat voxelDensities;
                        for (int j = 0; j < 8; j++) voxelDensities[j] = Densities[corners[j]];

                        var edgeMask = EdgeCase.GetEdgeMask(EdgeTable, Isolevel, voxelDensities);
                        if(edgeMask == 0)
                            continue;
                        for (int i = 0; i < 12; i++)
                        {
                            //if there is an intersection on this edge
                            if ((edgeMask & (1 << i)) != 0)
                            {
                                meanCount++;
                                Marching.byte2 conn = EdgeConnection[i];
                                var offset =
                                        // 0.5f
                                        (Isolevel - voxelDensities[conn.x]) /
                                        (voxelDensities[conn.y] - voxelDensities[conn.x])
                                    ;

                                // compute the two normals at x,y,z and x',y',z'
                                // 'coords are xyz+edgeDirection as int
                                // n = normalize(n1+n2)
                                // n1, n2: gradient on 3 axis
                                // cheap gradient if the density grid has a padding of 1 


                                var edgePoint = new float3(
                                    (vertexOffsets[conn.x].x) * delta + offset * delta * EdgeDirection[i].x,
                                    (vertexOffsets[conn.x].y) * delta + offset * delta * EdgeDirection[i].y,
                                    (vertexOffsets[conn.x].z) * delta + offset * delta * EdgeDirection[i].z
                                );
                                mean += math.clamp(edgePoint, float3.zero, 1);
                                // edgePoints[i]
                            }
                        }
// if(meanCount == 0)
                        mean /= meanCount;
                        
                        var coordsToIndex = MeshGen.CoordsToIndex(localCoords , v1);
                        var index = vertIndices[coordsToIndex] - 1;
                        // Debug.Log($"{localCoords} {coordsToIndex} m {Convert.ToString(edgeMask, 2)} {index} offset {mean}");
                        if (index >= 0)
                        {
                            // Assert.IsTrue(index >= 0, $"{localCoords} index {coordsToIndex}");
                            outputVerts[index] += mean - 0.5f * delta;
                        }
                        // else
                        // Debug.LogError($"No index for {localCoords} at {coordsToIndex} {Convert.ToString(edgeMask, 2)}");
                    }
                }
            }
        }
    }
}