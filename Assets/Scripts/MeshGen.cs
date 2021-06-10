using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class MeshGen : MonoBehaviour
    {
        public int VoxelSide = 1;
        public float ScaleFactor = 1;

        public NativeArray<ushort> EdgeTable;
        public NativeArray<int> TriTable;
        public NativeArray<Marching.byte2> EdgeConnection;
        public NativeArray<float3> EdgeDirection;

        private void Start()
        {
            EdgeTable = Marching.EdgeTable(Allocator.Persistent);
            TriTable = Marching.TriTable(Allocator.Persistent);
            EdgeConnection = Marching.EdgeConnection(Allocator.Persistent);
            EdgeDirection = Marching.EdgeDirection(Allocator.Persistent);
        }

        private void OnDestroy()
        {
            EdgeTable.Dispose();
            TriTable.Dispose();
            EdgeDirection.Dispose();
            EdgeConnection.Dispose();
        }

        public  struct OctInt
        {
            public unsafe fixed int Value[8];
            public unsafe int this[int i] => Value[i];
        }
        
        public unsafe struct OctFloat
        {
            public fixed float Value[8];
            public float this[int i]
            {
                get { return Value[i]; }
                set { Value[i] = value; }
            }
        }
        
        public void RequestChunk(Chunk chunk, int2 coords)
        {
            chunk.Generating = true;
            chunk.Coords = coords;
            
            chunk.OutputMeshData = Mesh.AllocateWritableMeshData(1);
            
            
            chunk.RequestGeneration(coords);
        }

        public static unsafe bool GetCornerCoords(int3 voxelCoords, int v1, out OctInt coords)
        {
            coords.Value[0] = CoordsToIndex(voxelCoords, v1);
            coords.Value[1] = coords.Value[0] + 1; // +x
            coords.Value[2] = coords.Value[1] + v1; // +z
            coords.Value[3] = coords.Value[2] - 1; // -x
            coords.Value[4] = coords.Value[0] + v1*v1; // +y
            coords.Value[5] = coords.Value[1] + v1*v1; // +y
            coords.Value[6] = coords.Value[2] + v1*v1; // +y
            coords.Value[7] = coords.Value[3] + v1*v1; // +y
            return true;
        }
        
        

        [BurstCompile]
        public struct DensityJob : IJobParallelFor
        {
            public int VoxelSide;
            public int2 Coords;
            [WriteOnly]
            public NativeArray<float> Densities;
            public void Execute(int index)
            {
                var delta = 1f / VoxelSide;
                var v1 = VoxelSide + 1;
                // array is xxx zzz yyy
                float3 coords = (float3)IndexToCoords(index, v1) * delta + new float3(Coords.x, 0, Coords.y);
                float d = Density(coords);
                Densities[index] = d;
            }
        }

        [BurstCompile]
        public struct GenJob : IJob
        {
            public Mesh.MeshData OutputMesh;
            public int2 Coords;
            public NativeArray<int> IndexVertexCounts;
            public int VoxelSide;
            public NativeArray<float> Densities;
            public float Isolevel;
            public NativeArray<ushort> EdgeTable;
            public NativeArray<int> TriTable;
            public NativeArray<Marching.byte2> EdgeConnection;
            public NativeArray<float3> EdgeDirection;

            public unsafe void Execute()
            {
                var outputVerts = OutputMesh.GetVertexData<float3>();
                var outputNormals = OutputMesh.GetVertexData<float3>(stream:1);
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
                        var coordsY = (y*delta);
                        for (int z = 0; z < VoxelSide; z++)
                        {
                            var coordsZ = (Coords.y + z * delta);
                            var coords = new int3(x, y, z);

                            GetCornerCoords(coords, v1, out var corners);

                            OctFloat voxelDentities;
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
                                    outputTris[ind++] = v;
                                    outputVerts[v] = edgePoints[vert];
                                    outputNormals[v] = new float3(0, 1, 0);
                                    v++;
                                }
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

                void AddQuad(ref int v, ref int i, float3 tl, float3 tr, float3 bl, float3 br)
                {
                    var n = math.cross(tr - tl, bl - tl);
                    outputTris[i++] = v;
                    outputTris[i++] = v+1;
                    outputTris[i++] = v+2;

                    outputTris[i++] = v+2;
                    outputTris[i++] = v+1;
                    outputTris[i++] = v+3;

                    outputNormals[v] = n;
                    outputVerts[v++] = tl;
                    
                    outputNormals[v] = n;
                    outputVerts[v++] = tr;
                    
                    outputNormals[v] = n;
                    outputVerts[v++] = bl;
                    
                    outputNormals[v] = n;
                    outputVerts[v++] = br;
                }
                
                void CreateCube(ref int v, ref int i, int x, int y, int z)
                {
                    float3 a = delta*new float3(x+0, y+1 , z+1);
                    float3 b = delta*new float3(x+1, y+1 , z+1);
                    float3 c = delta*new float3(x+0, y+1 , z+0);
                    float3 d = delta*new float3(x+1, y+1 , z+0);
                    float3 e = delta*new float3(x+0, y+0 , z+1);
                    float3 f = delta*new float3(x+1, y+0 , z+1);
                    float3 g = delta*new float3(x+0, y+0 , z+0);
                    float3 h = delta*new float3(x+1, y+0 , z+0);
                    
                    // a b
                    // c d
                    
                    // e f
                    // g h
                    AddQuad(ref v, ref i, a,b,c,d);
                    AddQuad(ref v, ref i, f,e,h,g);
                    AddQuad(ref v, ref i, c,d,g,h);
                    AddQuad(ref v, ref i, b,a,f,e);
                    AddQuad(ref v, ref i, d,b,h,f);
                    AddQuad(ref v, ref i, a,c,e,g);
                }
            }
        }

        /// <summary>
        /// IndexToCoords
        /// </summary>
        /// <param name="index"></param>
        /// <param name="v1">VoxelSide + 1</param>
        /// <returns></returns>
        public static int3 IndexToCoords(int index, int v1)
        {
            return new int3(
                index % v1,
                index / (v1 * v1),
                ((index % (v1 * v1)) / v1)
            );
        }

        public static int CoordsToIndex(int3 coords, int v1)
        {
            return coords.x + coords.y * v1 * v1 + coords.z * v1;
        }

        private static float Density(float3 coords)
        {
            // return .25f - coords.z; // plane
            
            float3 warp = noise.srdnoise(coords.xy * .008f, coords.z* .008f).xyz;
            coords += warp * .08f;
            return noise.snoise(coords * 4.03f) * .25f +
                   noise.snoise(coords * 1.96f) * .5f +
                   noise.snoise(coords * 1.01f) * 1f;

            return coords.y - ((math.sin(coords.x * 10) + math.cos(coords.z * 10)) * .1f + .25f);

            // return math.distance(coords, new float3(.5f)) - .25f; // sphere
            // return .5f - math.distance(coords, new float3(.5f,.5f,.5f));
        }
    }
}