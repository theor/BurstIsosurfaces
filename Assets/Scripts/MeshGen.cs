using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class MeshGen : MonoBehaviour
    {
        public void RequestChunk(Chunk chunk, int2 coords)
        {
            Debug.Log($"Generate {coords}");
            chunk.Generating = true;
            chunk.Coords = coords;
            
            chunk.OutputMeshData = Mesh.AllocateWritableMeshData(1);
            
            
            var job = new GenJob
            {
                outputMesh = chunk.OutputMeshData[0],
                coords = coords,
            };
            job.outputMesh.SetIndexBufferParams(3, IndexFormat.UInt32);
            job.outputMesh.SetVertexBufferParams(3,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal, stream:1));
            chunk.Handle = job.Schedule();
        }

        public struct GenJob : IJob
        {
            public Mesh.MeshData outputMesh;
            public int2 coords;

            public void Execute()
            {
                var outputVerts = outputMesh.GetVertexData<Vector3>();
                var outputNormals = outputMesh.GetVertexData<Vector3>(stream:1);
                var outputTris = outputMesh.GetIndexData<int>();
                outputVerts[0] = new Vector3(0, 0 , 0);
                outputVerts[1] = new Vector3(0, 0 , 1);
                outputVerts[2] = new Vector3(1, 0 , 0);
                
                outputNormals[0] = Vector3.up;
                outputNormals[1] = Vector3.up;
                outputNormals[2] = Vector3.up;

                outputTris[0] = 0;
                outputTris[1] = 1;
                outputTris[2] = 2;
            }
        }
    }
}