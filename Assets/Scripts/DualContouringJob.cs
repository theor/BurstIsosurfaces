using System;
using System.Linq;
using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct DualContouringJob : IJob
    {
        public bool Smooth;
        [ReadOnly]
        public EvalGraph EvalGraph;
        
        public Mesh.MeshData OutputMesh;
        public int3 Coords;
        public int Scale;
        public int VoxelSide;
        public float Isolevel;
        // [WriteOnly]
        public NativeArray<int> IndexVertexCounts;

        [ReadOnly]
        public NativeArray<float> Densities;
        [ReadOnly]
        public NativeArray<ushort> EdgeTable;

        [ReadOnly]
        public NativeArray<Marching.byte2> EdgeConnection;
        [ReadOnly]
        public NativeArray<float3> EdgeDirection;

        struct EdgeCase
        {
            public byte VertexCount;
            public byte Flipped;
            public byte Mask;
            public unsafe fixed byte Vertices[12];

            public EdgeCase(byte vertexCount) : this()
            {
                VertexCount = vertexCount;
                Flipped = 0;
            }

            public unsafe EdgeCase Quad0(byte va, byte vb, byte vc, byte vd) => Quad(0, va, vb, vc, vd);
            public unsafe EdgeCase Quad1(byte va, byte vb, byte vc, byte vd) => Quad(1, va, vb, vc, vd);
            public unsafe EdgeCase Quad2(byte va, byte vb, byte vc, byte vd) => Quad(2, va, vb, vc, vd);
            public unsafe EdgeCase Quad(byte quadIndex, byte va, byte vb, byte vc, byte vd)
            {
                Assert.IsTrue(quadIndex < VertexCount/4);
                Vertices[0+4*quadIndex] = va;
                Vertices[1+4*quadIndex] = vb;
                Vertices[2+4*quadIndex] = vc;
                Vertices[3+4*quadIndex] = vd;
                return this;
            }
        }

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
        static unsafe NativeArray<EdgeCase> EdgeCases(Allocator allocator)
        {
            var a = new NativeArray<EdgeCase>(16, allocator);
            a[0b0000] = new EdgeCase(0);
            a[0b0001] = new EdgeCase(4).Quad0(0,1,5,4);
            a[0b0010] = new EdgeCase(4).Quad0(4,5,6,7);
            a[0b0011] = new EdgeCase(8).Quad0(0,1,5,4).Quad1(4,5,6,7);
            a[0b0100] = new EdgeCase(4).Quad0(0,4,7,3);
            a[0b0101] = new EdgeCase(8).Quad0(0,1,5,4).Quad1(7,3,0,4);
            a[0b0110] = new EdgeCase(8).Quad0(4,5,6,7).Quad1(7,3,0,4);
            a[0b0111] = new EdgeCase(12).Quad0(0,1,5,4).Quad1(4,5,6,7).Quad2(7,3,0,4);
            for (int i = 0b1000; i <= 0b1111; i++)
            {
                var edgeCase = a[(~i) & 0b00001111];
                edgeCase.Flipped = 1;
                a[i] = edgeCase;
            }

            for (int i = 0; i <= 0b1111; i++)
            {
                var c = a[i];
                c.Mask = (byte) i;
                a[i] = c;
            }

            // for (int i = 0; i < 16; i++)
            // {
            //     var edgeCase = a[i];
            //     var verts = String.Join(",", Enumerable.Repeat(0ul, edgeCase.VertexCount).Select((_, j) =>
            //     {
            //         var edgeCase = a[i];
            //         return edgeCase.Vertices[j];
            //     }));
            //     Debug.Log($"{i:X} {edgeCase.VertexCount} {verts}");
            // }
            return a;
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

            // float d(NativeArray<float> array, int3 delta)
            // {
            //     var coordsToIndex = MeshGen.CoordsToIndex(pos + delta, v3);
            //     Assert.AreEqual(pos+delta, MeshGen.IndexToCoords(coordsToIndex, v3));
            //     var f = array[coordsToIndex];
            //     // Debug.Log($"{pos+delta} {delta} {f:F2}");
            //     return f;
            // }
            // var f = 1f;
            return
                new float3(
                    d(ref evalState, EvalGraph, new float3(-1, 0, 0)) -
                        d(ref evalState, EvalGraph, new float3(1, 0, 0)),
                    d(ref evalState, EvalGraph, new float3(0, -1, 0)) -
                    d(ref evalState, EvalGraph, new float3(0, 1, 0)),
                    d(ref evalState, EvalGraph, new float3(0, 0, -1)) -
                    d(ref evalState, EvalGraph, new float3(0, 0, 1))
                );
            // new float3(
            //         MeshGen.Density(pos + f*new float3(-1, 0, 0)) -
            //             MeshGen.Density(pos + f*new float3(1, 0, 0)),
            //         MeshGen.Density(pos + f*new float3(0, -1, 0)) -
            //         MeshGen.Density(pos + f*new float3(0, 1, 0)),
            //         MeshGen.Density(pos + f*new float3(0, 0, -1)) -
            //         MeshGen.Density(pos + f*new float3(0, 0, 1))
            //     );
        }

        ushort GetEdgeMask(in MeshGen.OctFloat voxelDensities)
        {
            byte cubeIndex = 0;
            if (voxelDensities[0] < Isolevel) cubeIndex |= 1;
            if (voxelDensities[1] < Isolevel) cubeIndex |= 2;
            if (voxelDensities[2] < Isolevel) cubeIndex |= 4;
            if (voxelDensities[3] < Isolevel) cubeIndex |= 8;
            if (voxelDensities[4] < Isolevel) cubeIndex |= 16;
            if (voxelDensities[5] < Isolevel) cubeIndex |= 32;
            if (voxelDensities[6] < Isolevel) cubeIndex |= 64;
            if (voxelDensities[7] < Isolevel) cubeIndex |= 128;

            ushort edgeMask = EdgeTable[cubeIndex];
            return edgeMask;
        }
        public unsafe void Execute()
        {
            int voxelSide = VoxelSide;
            EvalState evalState = new EvalState();
            
            var outputVerts = OutputMesh.GetVertexData<float3>();
            var outputNormals = OutputMesh.GetVertexData<float3>(stream:1);
            var outputTris = OutputMesh.GetIndexData<int>();

            var vertIndices = new NativeArray<int>(outputVerts.Length, Allocator.Temp);
            var edgeMasks = new NativeArray<EdgeCase>(outputVerts.Length, Allocator.Temp);
            
            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;

            var delta = Scale / ((float)VoxelSide);
                
            // float3* edgePoints = stackalloc float3[12];
            float3* normals = stackalloc float3[12];
                     
            Marching.byte3* vertexOffsets = stackalloc Marching.byte3[8];
            vertexOffsets[0] = new Marching.byte3(0, 0, 0);
            vertexOffsets[1] = new Marching.byte3(1, 0, 0);
            vertexOffsets[2] = new Marching.byte3(1, 0, 1);
            vertexOffsets[3] = new Marching.byte3(0, 0, 1);
            vertexOffsets[4] = new Marching.byte3(0, 1, 0);
            vertexOffsets[5] = new Marching.byte3(1, 1, 0);
            vertexOffsets[6] = new Marching.byte3(1, 1, 1);
            vertexOffsets[7] = new Marching.byte3(0, 1, 1);


            //0.25 - dist(coords, 0.5)
            // 0.25 - y(coords)
            var edgeCases = EdgeCases(Allocator.Temp);
                
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
                        if (voxelDensities[1] < Isolevel) cubeindex |= 1;
                        if (voxelDensities[6] < Isolevel) cubeindex |= 2;
                        if (voxelDensities[3] < Isolevel) cubeindex |= 4;
                        if (voxelDensities[2] < Isolevel) cubeindex |= 8;

                        // cubeindex = 0b0111;
                        // cubeindex = 0b1000;
                        var edgeCase = edgeCases[cubeindex];

                        // Debug.Log($"{x} {y} {z} index {cubeindex:X} mask {edgeCase.VertexCount}");
                        
                        

                        // alloc vertex and index
                        // index is in base 1 (so 0 is invalid)
                        var vertIndex = MeshGen.CoordsToIndexNoPadding(localCoords, v1);
                        
                        edgeMasks[vertIndex] = edgeCase;
                    }
                }
            }

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
                        var vertIndex = MeshGen.CoordsToIndexNoPadding(localCoords, v1);
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

            // float3* edgePoints = stackalloc float3[12];
            if(Smooth)
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

                            MeshGen.OctFloat voxelDensities;
                            for (int j = 0; j < 8; j++) voxelDensities[j] = Densities[corners[j]];

                            var edgeMask = GetEdgeMask(voxelDensities);
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
                        
                            var coordsToIndex = MeshGen.CoordsToIndexNoPadding(localCoords + new int3(0,1,0), v1);
                            var index = vertIndices[coordsToIndex] - 1;
                            Debug.Log($"{localCoords} {coordsToIndex} m {edgeMask:X} {index} offset {mean}");
                            Assert.IsTrue(index >= 0, $"{localCoords} index {coordsToIndex}");
                            outputVerts[index] += mean - 0.5f*delta;
                        }
                    }
                }

            IndexVertexCounts[0] = outputIndexIndex;
            IndexVertexCounts[1] = nextVertexIndex;
            Debug.Log($"v {nextVertexIndex} i {outputIndexIndex}");
            // for (int i = 0; i < v; i++)
            // {
            //     Debug.Log(outputVerts[i]);
            // }
            // for (int i = 0; i < ind; i++)
            // {
            //     Debug.Log(outputTris[i]);
            // }
            
            // for (int j = i; j < outputTris.Length; j++)
            // {
            //     outputTris[j] = 0;
            // }
            UnsafeUtility.MemClear((int*)outputTris.GetUnsafePtr()+outputIndexIndex, UnsafeUtility.SizeOf<int>()* (outputTris.Length - outputIndexIndex));
        }

        private void AddTriIndex(int3 c, int v1, ref NativeArray<int> vertIndices, float delta,
            ref NativeArray<float3> outputVerts, 
            ref int nextVertexIndex, 
            ref NativeArray<float3> outputNormals, 
            ref EvalState evalState, 
            ref NativeArray<int> outputTris,
            ref int outputIndexIndex)
        {
            var coordsToIndex = MeshGen.CoordsToIndexNoPadding(c, v1);
            // Debug.Log($"Add {c} vi {vertIndices[coordsToIndex]-1} at {ind}");
            // indices in BASE 1

            // vertex doesn't exist yet
            if (vertIndices[coordsToIndex] == 0)
            {
                var pos = (float3) c * delta + new float3(.5f,-.5f,.5f) * delta;
                outputVerts[nextVertexIndex] = pos;
                outputNormals[nextVertexIndex] = SampleNormal(ref evalState, Coords + pos - 0.5f * delta);

                Debug.Log($"create vertex at {c} vi {nextVertexIndex} index {coordsToIndex}");
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
}