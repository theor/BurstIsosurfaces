using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class Chunk : MonoBehaviour
    {
        public int2 Coords;
        public Mesh Mesh;
        public JobHandle Handle;
        public bool Generating;
        public Mesh.MeshDataArray OutputMeshData;

        private void Start()
        {
            Coords = new int2(int.MaxValue, int.MaxValue);
            Mesh = new Mesh();
            GetComponent<MeshFilter>().sharedMesh = Mesh;
        }

        private void Update()
        {
            if (Generating && Handle.IsCompleted)
            {
                Debug.Log("Complete Chunk");
                Generating = false;
                transform.position = new Vector3(Coords.x, 0, Coords.y);
                
                var sm = new SubMeshDescriptor(0, 3, MeshTopology.Triangles)
                {
                    firstVertex = 0,
                    vertexCount = 3,
                };


                var meshData = OutputMeshData[0];
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, sm);
                Mesh.ApplyAndDisposeWritableMeshData(OutputMeshData, Mesh);
            }
        }
    }
}