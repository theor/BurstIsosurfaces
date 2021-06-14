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

        float3 SampleNormal(int3 pos)
        {
            var v3 = VoxelSide + 3;

            float d(NativeArray<float> array, int3 delta)
            {
                var coordsToIndex = MeshGen.CoordsToIndex(pos + delta, v3);
                Assert.AreEqual(pos+delta, MeshGen.IndexToCoords(coordsToIndex, v3));
                var f = array[coordsToIndex];
                // Debug.Log($"{pos+delta} {delta} {f:F2}");
                return f;
            }
            // var f = 1f;
            return
                new float3(
                    d(Densities, new int3(-1, 0, 0)) -
                        d(Densities, new int3(1, 0, 0)),
                    d(Densities, new int3(0, -1, 0)) -
                    d(Densities, new int3(0, 1, 0)),
                    d(Densities, new int3(0, 0, -1)) -
                    d(Densities, new int3(0, 0, 1))
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
        public unsafe void Execute()
        {
            var outputVerts = OutputMesh.GetVertexData<float3>();
            var outputNormals = OutputMesh.GetVertexData<float3>(stream:1);
            var outputTris = OutputMesh.GetIndexData<int>();

            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;

            int v = 0;
            int ind = 0;
            var delta = 1f / VoxelSide;
                
            float3* edgePoints = stackalloc float3[12];
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
                
            var coords = Coords*VoxelSide;
            for (int x = 0; x < VoxelSide; x++)
            {
                var coordsX = (Coords.x + x*delta);
                for (int y = 0; y < VoxelSide; y++)
                {
                    var coordsY = (Coords.y + y*delta);
                    for (int z = 0; z < VoxelSide; z++)
                    {
                        var coordsZ = (Coords.z + z * delta);
                        var localCoords = new int3(x, y, z);

                        MeshGen.GetCornerCoords(localCoords, v3, out var corners);

                        MeshGen.OctFloat voxelDensities;
                        MeshGen.OctFloat voxelDensitiesBool;
                        for (int j = 0; j < 8; j++)
                        {
                            voxelDensities[j] = Densities[corners[j]];
                            voxelDensitiesBool[j] = voxelDensities[j] < Isolevel ? -1 : 1;
                        }
                            
                        // Debug.Log($"voxel {coords} {localCoords} = {coords+localCoords}\ncorners {corners}\ndensities {voxelDensities}\nbool {voxelDensitiesBool}");
                        byte cubeindex = 0;
                        if (voxelDensities[0] < Isolevel) cubeindex |= 1;
                        if (voxelDensities[1] < Isolevel) cubeindex |= 2;
                        if (voxelDensities[2] < Isolevel) cubeindex |= 4;
                        if (voxelDensities[3] < Isolevel) cubeindex |= 8;
                        if (voxelDensities[4] < Isolevel) cubeindex |= 16;
                        if (voxelDensities[5] < Isolevel) cubeindex |= 32;
                        if (voxelDensities[6] < Isolevel) cubeindex |= 64;
                        if (voxelDensities[7] < Isolevel) cubeindex |= 128;

                        ushort edgeMask = EdgeTable[cubeindex];

                        // Debug.Log($"{x} {y} {z} index {cubeindex:X} mask {edgeMask:X}");
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
                                    (Isolevel - voxelDensities[conn.x])/(voxelDensities[conn.y] - voxelDensities[conn.x])
                                    ;
                                
                                // compute the two normals at x,y,z and x',y',z'
                                // 'coords are xyz+edgeDirection as int
                                // n = normalize(n1+n2)
                                // n1, n2: gradient on 3 axis
                                // cheap gradient if the density grid has a padding of 1 

                                
                                edgePoints[i] = new float3(
                                    (x + vertexOffsets[conn.x].x)*delta + offset* delta * EdgeDirection[i].x,  
                                    (y + vertexOffsets[conn.x].y)*delta + offset* delta * EdgeDirection[i].y,  
                                    (z + vertexOffsets[conn.x].z)*delta + offset* delta * EdgeDirection[i].z  
                                );
                                
                                var a = new int3(x + vertexOffsets[conn.x].x, y + vertexOffsets[conn.x].y, z + vertexOffsets[conn.x].z);
                                var b = new int3(x + vertexOffsets[conn.y].x, y + vertexOffsets[conn.y].y, z + vertexOffsets[conn.y].z);
                                var na = SampleNormal(a);
                                var nb = SampleNormal(b);
                                normals[i] =
                                    math.normalize(na * (1-offset) + nb * (offset));
                                var t1 = new int3(1,0,0);
                                var t2 = new int3(1,0,1);
                                // if(a.Equals(t1) && b.Equals(t2) || a.Equals(t2) && b.Equals(t1))
                                    // Debug.Log($"{a} {na:F2}\n{b} {nb:F2}\n{normals[i]:F2}\n{edgePoints[i]:F2}");
                                // normals[i] = new half4(new float4( math.normalize(SampleNormal(Coords + edgePoints[i])), 1));
                            }
                            else
                            {
                                edgePoints[i] = default;
                                normals[i] = default;
                            }
                        }

                        for (int i = 0; i < 5; i++)
                        {
                            if(TriTable[cubeindex*16 + 3*i] < 0)
                                break;
                            for (int j = 0; j < 3; j++)
                            {
                                var vert = TriTable[cubeindex * 16 + 3 * i + 2-j];
                                outputTris[ind++] = (ushort) (v+j);
                                outputVerts[v+j] = edgePoints[vert];
                                outputNormals[v+j] = normals[vert];
                            }

                            // var n = new half4((half3)math.cross(outputVerts[v + 1] - outputVerts[v],
                            //     outputVerts[v + 2] - outputVerts[v]), (half)1f);
                            // outputNormals[v] =  n;
                            // outputNormals[v+1] = n;
                            // outputNormals[v+2] = n;

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