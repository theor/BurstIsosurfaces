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

        public  struct OctInt
        {
            public unsafe fixed int Value[8];
            public unsafe int this[int i] => Value[i];
        }
        
        public unsafe struct OctFloat
        {
            public fixed float Value[8];
            public float this[int i] => Value[i];
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
            public float ScaleFactor;

            public unsafe void Execute()
            {
                var outputVerts = OutputMesh.GetVertexData<float3>();
                var outputNormals = OutputMesh.GetVertexData<float3>(stream:1);
                var outputTris = OutputMesh.GetIndexData<int>();

                int v = 0;
                int i = 0;
                var delta = 1f / VoxelSide;
                
                for (int x = 0; x < VoxelSide; x++)
                {
                    var coordsX = ScaleFactor * (Coords.x + x*delta);
                    for (int y = 0; y < VoxelSide; y++)
                    {
                        var coordsY = ScaleFactor * (y*delta);
                        for (int z = 0; z < VoxelSide; z++)
                        {
                            var coordsZ = ScaleFactor * (Coords.y + z * delta);
                            var coords = new float3(coordsX, coordsY, coordsZ);
                            if(coordsY < math.sin(coordsX) + math.cos(coordsZ))
                                CreateCube(ref v, ref i, x, y, z);
                        }
                    }
                }

                IndexVertexCounts[0] = i;
                IndexVertexCounts[1] = v;
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
            return .5f - math.distance(coords, new float3(.5f,.5f,.5f));
        }
    }
}