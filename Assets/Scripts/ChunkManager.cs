using System;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    public class ChunkManager : MonoBehaviour
    {
        public Transform Target;
        public Material Material;
        private MeshGen _meshGen;
        private Chunk _chunk;

        private void Start()
        {
            _meshGen = GetComponent<MeshGen>();
            
            
            _chunk = new GameObject("New", typeof(Chunk),typeof(MeshFilter), typeof(MeshRenderer)).GetComponent<Chunk>();
            _chunk.transform.SetParent(transform);
            _chunk.GetComponent<MeshRenderer>().sharedMaterial = Material;
        }

        private void Update()
        {
            int2 cur = new int2((int) math.floor(Target.position.x), (int) math.floor(Target.position.z));
            if (!_chunk.Coords.Equals(cur))
            {
                _meshGen.RequestChunk(_chunk, cur);
            }
        }
    }
}