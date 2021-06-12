using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct GenJob : IJob
    {
        public Mesh.MeshData OutputMesh;
        public int3 Coords;
        public int VoxelSide;
        public float Isolevel;
        [WriteOnly]
        public NativeArray<int> IndexVertexCounts;

        [ReadOnly]
        public NativeArray<float> Densities;

        [ReadOnly]
        public NativeArray<ushort> EdgeTable;
        [ReadOnly]
        public NativeArray<int> TriTable;
        [ReadOnly]
        public NativeArray<Marching.byte2> EdgeConnection;
        [ReadOnly]
        public NativeArray<float3> EdgeDirection;

        public unsafe void Execute()
        {
            var outputVerts = OutputMesh.GetVertexData<float3>();
            var outputNormals = OutputMesh.GetVertexData<half4>(stream:1);
            var outputTris = OutputMesh.GetIndexData<int>();

            var v1 = VoxelSide + 1;

            int v = 0;
            int ind = 0;
            var delta = 1f / VoxelSide;
            var delta1 = 1f / v1;
                
            float3* edgePoints = stackalloc float3[12];
                     
            Marching.byte3* vertexOffsets = stackalloc Marching.byte3[8];
            vertexOffsets[0] = new Marching.byte3(0, 0, 0);
            vertexOffsets[1] = new Marching.byte3(1, 0, 0);
            vertexOffsets[2] = new Marching.byte3(1, 0, 1);
            vertexOffsets[3] = new Marching.byte3(0, 0, 1);
            vertexOffsets[4] = new Marching.byte3(0, 1, 0);
            vertexOffsets[5] = new Marching.byte3(1, 1, 0);
            vertexOffsets[6] = new Marching.byte3(1, 1, 1);
            vertexOffsets[7] = new Marching.byte3(0, 1, 1);
                
            for (int x = 0; x < VoxelSide; x++)
            {
                var coordsX = (Coords.x + x*delta);
                for (int y = 0; y < VoxelSide; y++)
                {
                    var coordsY = (Coords.y + y*delta);
                    for (int z = 0; z < VoxelSide; z++)
                    {
                        var coordsZ = (Coords.z + z * delta);
                        var coords = new int3(x, y, z);

                        MeshGen.GetCornerCoords(coords, v1, out var corners);

                        MeshGen.OctFloat voxelDentities;
                        for (int j = 0; j < 8; j++) voxelDentities[j] = Densities[corners[j]];
                            
                        byte cubeindex = 0;
                        if (voxelDentities[0] < Isolevel) cubeindex |= 1;
                        if (voxelDentities[1] < Isolevel) cubeindex |= 2;
                        if (voxelDentities[2] < Isolevel) cubeindex |= 4;
                        if (voxelDentities[3] < Isolevel) cubeindex |= 8;
                        if (voxelDentities[4] < Isolevel) cubeindex |= 16;
                        if (voxelDentities[5] < Isolevel) cubeindex |= 32;
                        if (voxelDentities[6] < Isolevel) cubeindex |= 64;
                        if (voxelDentities[7] < Isolevel) cubeindex |= 128;

                        ushort edgeMask = EdgeTable[cubeindex];

                        // Debug.Log($"{x} {y} {z} mask {edgeMask:X}");
                        if(edgeMask == 0)
                            continue;

                        for (int i = 0; i < 12; i++)
                        {
                            //if there is an intersection on this edge
                            if ((edgeMask & (1 << i)) != 0)
                            {
                                Marching.byte2 conn = EdgeConnection[i];
                                var offset = 
                                    // 0.5f
                                    (Isolevel - voxelDentities[conn.x])/(voxelDentities[conn.y] - voxelDentities[conn.x])  
                                    * delta;
                                
                                // compute the two normals at x,y,z and x',y',z'
                                // 'coords are xyz+edgeDirection as int
                                // n = normalize(n1+n2)
                                // n1, n2: gradient on 3 axis
                                // cheap gradient if the density grid has a padding of 1 
                                
                                
                                edgePoints[i] = new float3(
                                    (x + vertexOffsets[conn.x].x)*delta + offset * EdgeDirection[i].x,  
                                    (y + vertexOffsets[conn.x].y)*delta + offset * EdgeDirection[i].y,  
                                    (z + vertexOffsets[conn.x].z)*delta + offset * EdgeDirection[i].z  
                                );
                            }
                            else
                                edgePoints[i] = default;
                        }

                        for (int i = 0; i < 5; i++)
                        {
                            if(TriTable[cubeindex*16 + 3*i] < 0)
                                break;
                            for (int j = 0; j < 3; j++)
                            {
                                var vert = TriTable[cubeindex * 16 + 3 * i + j];
                                outputTris[ind++] = (ushort) (v+j);
                                outputVerts[v+j] = edgePoints[vert];
                            }

                            var n = new half4((half3)math.cross(outputVerts[v + 1] - outputVerts[v],
                                outputVerts[v + 2] - outputVerts[v]), (half)1f);
                            outputNormals[v] =  n;
                            outputNormals[v+1] = n;
                            outputNormals[v+2] = n;

                            v += 3;
                        }

                        // if(coordsY < math.sin(coordsX) + math.cos(coordsZ))
                        // CreateCube(ref v, ref ind, x, y, z);
                    }
                }
            }

            IndexVertexCounts[0] = ind;
            IndexVertexCounts[1] = v;
            // for (int j = i; j < outputTris.Length; j++)
            // {
            //     outputTris[j] = 0;
            // }
            UnsafeUtility.MemClear((int*)outputTris.GetUnsafePtr()+ind, UnsafeUtility.SizeOf<int>()* (outputTris.Length - ind));
        }
    }
}