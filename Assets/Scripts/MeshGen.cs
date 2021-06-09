using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class MeshGen : MonoBehaviour
    {
        public int VoxelSide = 1;
        public void RequestChunk(Chunk chunk, int2 coords)
        {
            Debug.Log($"Generate {coords}");
            chunk.Generating = true;
            chunk.Coords = coords;
            
            chunk.OutputMeshData = Mesh.AllocateWritableMeshData(1);
            
            
            var job = new GenJob
            {
                voxelSide = VoxelSide,
                outputMesh = chunk.OutputMeshData[0],
                coords = coords,
                indexVertexCounts = chunk.IndexVertexCounts,
            };
            var maxCubeCount = VoxelSide * VoxelSide * VoxelSide;
            var maxTriCount = maxCubeCount *6/*faces*/*2/*tri per face*/;
            job.outputMesh.SetIndexBufferParams(maxTriCount*3, IndexFormat.UInt32);
            job.outputMesh.SetVertexBufferParams(maxCubeCount*8,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream:1));
            chunk.Handle = job.Schedule();
        }

        public struct GenJob : IJob
        {
            public Mesh.MeshData outputMesh;
            public int2 coords;
            public NativeArray<int> indexVertexCounts;
            public int voxelSide;

            public void Execute()
            {
                var outputVerts = outputMesh.GetVertexData<Vector3>();
                var outputNormals = outputMesh.GetVertexData<Vector3>(stream:1);
                var outputTris = outputMesh.GetIndexData<int>();

                for (int x = 0; x < voxelSide; x++)
                {
                    for (int y = 0; y < voxelSide; y++)
                    {
                        for (int z = 0; z < voxelSide; z++)
                        {
                            CreateCube(x, y, z);
                        }
                    }
                }
                
                // outputVerts[0] = new Vector3(0, 0 , 0);
                // outputVerts[1] = new Vector3(0, 0 , 1);
                // outputVerts[2] = new Vector3(1, 0 , 0);
                //
                // outputNormals[0] = Vector3.up;
                // outputNormals[1] = Vector3.up;
                // outputNormals[2] = Vector3.up;
                //
                // outputTris[0] = 0;
                // outputTris[1] = 1;
                // outputTris[2] = 2;

                indexVertexCounts[0] = voxelSide * voxelSide * voxelSide * 6 * 2 * 3;
                indexVertexCounts[1] = voxelSide * voxelSide * voxelSide * 8;
                // for (int i = indexVertexCounts[0]; i < outputTris.Length; i++)
                // {
                //     outputTris[i] = 0;
                // }

                void AddTri(ref int indice, int v1, int v2, int v3)
                {
                    outputTris[indice++] = v1;
                    outputTris[indice++] = v2;
                    outputTris[indice++] = v3;
                }
                void CreateCube(int x, int y, int z)
                {
                    // 5 7
                    // 4 6 
                    // 1 3
                    // 0 2 
                    
                    outputVerts[0] = new Vector3(x+0, y+0 , z+0);
                    outputVerts[1] = new Vector3(x+0, y+0 , z+1);
                    outputVerts[2] = new Vector3(x+1, y+0 , z+0);
                    outputVerts[3] = new Vector3(x+1, y+0 , z+1);
                    
                    outputVerts[4] = new Vector3(x+0, y+1 , z+0);
                    outputVerts[5] = new Vector3(x+0, y+1 , z+1);
                    outputVerts[6] = new Vector3(x+1, y+1 , z+0);
                    outputVerts[7] = new Vector3(x+1, y+1 , z+1);

                    int indice = 0;
                    AddTri(ref indice, 0, 2, 1);
                    AddTri(ref indice, 2, 3, 1);
                    AddTri(ref indice, 4, 5, 6);
                    AddTri(ref indice, 6, 5, 7);
                    
                    // 4 6
                    // 0 2
                    AddTri(ref indice, 0, 4, 2);
                    AddTri(ref indice, 2, 4, 6);
                    // 5 7
                    // 1 3
                    AddTri(ref indice, 1, 3, 5);
                    AddTri(ref indice, 5, 3, 7);
 

                    // 5 4
                    // 1 0
                    AddTri(ref indice, 0, 1, 5);
                    AddTri(ref indice, 0, 5, 4);
                    // 7 6
                    // 3 2
                    AddTri(ref indice, 3, 2, 7);
                    AddTri(ref indice, 7, 2, 6);
                
                    outputNormals[0] = Vector3.down;
                    outputNormals[1] = Vector3.down;
                    outputNormals[2] = Vector3.down;
                    outputNormals[3] = Vector3.down;
                    outputNormals[4] = Vector3.up;
                    outputNormals[5] = Vector3.up;
                    outputNormals[6] = Vector3.up;
                    outputNormals[7] = Vector3.up;
                }
            }
        }
    }
}