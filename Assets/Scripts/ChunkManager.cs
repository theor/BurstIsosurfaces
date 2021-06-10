using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

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
            _chunk.Setup(_meshGen);
        }

        private void Update()
        {
            int2 cur = new int2((int) math.floor(Target.position.x), (int) math.floor(Target.position.z));
            if (!_chunk.Coords.Equals(cur))
            {
                _meshGen.RequestChunk(_chunk, cur);
            }
        }

        // private void OnDrawGizmos()
        // {
            // if(_chunk && _chunk.den)
            // #if UNITY_EDITOR
            // if (!UnityEditor.EditorApplication.isPlaying && !_meshGen)
            //     _meshGen = GetComponent<MeshGen>();
            // #endif
            // var v1 = _meshGen.VoxelSide+1;
            // for (int i = 0; i < v1*v1*v1; i++)
            // {
            //     var coords = MeshGen.IndexToCoords(i, v1);
            //     Assert.AreEqual(i, MeshGen.CoordsToIndex(coords, v1));
            //     Handles.Label((float3)coords, i + " " + coords.ToString());
            // }
        // }
    }
}