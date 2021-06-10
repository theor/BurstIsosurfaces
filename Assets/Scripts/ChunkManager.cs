using System;
using System.Collections.Generic;
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
        private List<Chunk> _chunks;
        private List<Chunk> _freeChunks;
        private int2 Coords;

        public int ChunkDist = 1;

        private void Start()
        {
            Coords = new int2(Int32.MaxValue);
            _meshGen = GetComponent<MeshGen>();

            var chunkCount = 2 * ChunkDist + 1;
            chunkCount *= chunkCount;
            _freeChunks = new List<Chunk>(chunkCount);
            _chunks = new List<Chunk>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = new GameObject("Chunk"+i, typeof(Chunk),typeof(MeshFilter), typeof(MeshRenderer)).GetComponent<Chunk>();
                chunk.transform.SetParent(transform);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = Material;
                chunk.Setup(_meshGen);
                _freeChunks.Add(chunk);
            }
            
        }

        private void Update()
        {
            int2 cur = new int2((int) math.floor(Target.position.x), (int) math.floor(Target.position.z));
            if (!Coords.Equals(cur))
            {
                Coords = cur;
                for (var index = 0; index < _chunks.Count; index++)
                {
                    var chunk = _chunks[index];
                    var d = math.abs(chunk.Coords - Coords);
                    if (d.x > ChunkDist || d.y > ChunkDist)
                    {
                        _freeChunks.Add(chunk);
                        _chunks.RemoveAt(index);
                        index--;
                    }
                }

                for (var x = -ChunkDist; x <= ChunkDist; x++)
                for (var y = -ChunkDist; y <= ChunkDist; y++)
                {
                    var offset = new int2(x,y);
                    bool found = false;
                    for (var index = 0; index < _chunks.Count; index++)
                    {
                        var chunk = _chunks[index];
                        if (chunk.Coords.Equals(Coords + offset))
                        {
                            found = true;
                            break;
                        }
                    }

                    if(found)
                        continue;
                    var freeChunk = _freeChunks[_freeChunks.Count - 1];
                    _freeChunks.RemoveAt(_freeChunks.Count - 1);
                    _meshGen.RequestChunk(freeChunk, Coords + offset);
                    _chunks.Add(freeChunk);
                }
                
                
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