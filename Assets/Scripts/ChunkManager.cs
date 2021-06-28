using System;
using System.Collections.Generic;
using Eval.Runtime;
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
        private int3 Coords;
        public int Scale;

        public int ChunkDist = 1;

        private void Start()
        {
            Coords = new int3(Int32.MaxValue);
            _meshGen = GetComponent<MeshGen>();

            var chunkCount = 2 * ChunkDist + 1;
            chunkCount = chunkCount * chunkCount * chunkCount;
            _freeChunks = new List<Chunk>(chunkCount);
            _chunks = new List<Chunk>(chunkCount);
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = new GameObject("Chunk"+i, typeof(Chunk),typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider)).GetComponent<Chunk>();
                var c = chunk.GetComponent<BoxCollider>();
                c.center = new Vector3(0.5f, 0.5f, 0.5f)*Scale; 
                c.size = Vector3.one*Scale; 
                chunk.transform.SetParent(transform);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = Material;
                chunk.Setup(_meshGen);
                _freeChunks.Add(chunk);
            }
            
        }

#if UNITY_EDITOR
        private void OnFormulaChanged(EvalGraph oldgraph, EvalGraph newgraph)
        {
            Debug.Log("Changed");
            
            _meshGen.ClearQueue();
            for (var i = 0; i < _chunks.Count; i++)
            {
                _meshGen.RequestChunk(_chunks[i], _chunks[i].Coords, Scale, false);
            }
        }

        public void ForceRegen()
        {
            _meshGen.DensityFormula.SetDirty();
            // _meshGen.DensityFormula.LiveEdit(ref _meshGen.DensityFormulaEvaluator, OnFormulaChanged);
        }
#endif

        private void Update()
        {
#if UNITY_EDITOR
            _meshGen.DensityFormula.LiveEdit(ref _meshGen.DensityFormulaEvaluator, OnFormulaChanged);
#endif
            
            var position = Target.position;
            int3 cur = new int3((int) math.floor(position.x - Scale/2), (int) math.floor(position.y - Scale/2), (int) math.floor(position.z - Scale/2));
            if (!Coords.Equals(cur))
            {
                Coords = cur;
                for (var index = 0; index < _chunks.Count; index++)
                {
                    var chunk = _chunks[index];
                    var d = math.abs(chunk.Coords - Coords);
                    if (d.x > ChunkDist || d.y > ChunkDist || d.z > ChunkDist)
                    {
                        _freeChunks.Add(chunk);
                        _chunks.RemoveAt(index);
                        index--;
                    }
                }

                for (var x = -ChunkDist; x <= ChunkDist; x++)
                for (var y = -ChunkDist; y <= ChunkDist; y++)
                for (var z = -ChunkDist; z <= ChunkDist; z++)
                {
                    var offset = new int3(x,y,z);
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
                    _meshGen.RequestChunk(freeChunk, Coords + offset, Scale);
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