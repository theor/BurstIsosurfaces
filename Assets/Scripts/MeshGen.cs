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

        public void RequestChunk(Chunk chunk, int2 coords)
        {
            chunk.Generating = true;
            chunk.Coords = coords;
            
            chunk.OutputMeshData = Mesh.AllocateWritableMeshData(1);
            
            
            chunk.RequestGeneration(coords, this);
        }

        [BurstCompile]
        public struct GenJob : IJob
        {
            public Mesh.MeshData outputMesh;
            public int2 coords;
            public NativeArray<int> indexVertexCounts;
            public int voxelSide;
            public float scaleFactor;

            public unsafe void Execute()
            {
                var outputVerts = outputMesh.GetVertexData<float3>();
                var outputNormals = outputMesh.GetVertexData<float3>(stream:1);
                var outputTris = outputMesh.GetIndexData<int>();

                int v = 0;
                int i = 0;
                var delta = 1f / voxelSide;
                
                for (int x = 0; x < voxelSide; x++)
                {
                    var coordsX = scaleFactor * (coords.x + x*delta);
                    for (int y = 0; y < voxelSide; y++)
                    {
                        var coordsY = scaleFactor * (y*delta);
                        for (int z = 0; z < voxelSide; z++)
                        {
                            var coordsZ = scaleFactor * (coords.y + z * delta);
                            if(coordsY < math.sin(coordsX) + math.cos(coordsZ))
                                CreateCube(ref v, ref i, x, y, z);
                        }
                    }
                }

                indexVertexCounts[0] = i;
                indexVertexCounts[1] = v;
                // for (int j = i; j < outputTris.Length; j++)
                // {
                //     outputTris[j] = 0;
                // }
                UnsafeUtility.MemClear((int*)outputTris.GetUnsafePtr()+i, UnsafeUtility.SizeOf<int>()* (outputTris.Length - i));

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
    }
}