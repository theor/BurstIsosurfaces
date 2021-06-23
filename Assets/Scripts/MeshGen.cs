using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    public class MeshGen : MonoBehaviour
    {
        public unsafe struct OctInt
        {
            public fixed int Value[8];
            public int this[int i] => Value[i];

            public override string ToString()
            {
                return $"{Value[0]} {Value[1]} {Value[2]} {Value[3]} {Value[4]} {Value[5]} {Value[6]} {Value[7]}";
            }
        }
        
        public unsafe struct OctFloat
        {
            public fixed float Value[8];
            public float this[int i]
            {
                get { return Value[i]; }
                set { Value[i] = value; }
            }

            public override string ToString()
            {
                return $"{Value[0]:F2} {Value[1]:F2} {Value[2]:F2} {Value[3]:F2} {Value[4]:F2} {Value[5]:F2} {Value[6]:F2} {Value[7]:F2}";
            }
        }
        
        [Range(1, 16)]
        public int VoxelSide = 1;

        public Formula DensityFormula;

        public NativeArray<ushort> EdgeTable;
        public NativeArray<int> TriTable;
        public NativeArray<Marching.byte2> EdgeConnection;
        public NativeArray<float3> EdgeDirection;

        private Queue<Chunk> _queue;
        private JobHandle _currentHandle;
        private uint4 _lastHash;
        [NonSerialized]
        public EvalGraph DensityFormulaEvaluator;

        public Algorithm Algorithm;
        public bool Smooth;


        private void Reset()
        {
            if (DensityFormula == null)
            {
                DensityFormula = new Formula();
            }
            DensityFormula.SetParameters("coords");
        }

        private void Start()
        {
            DensityFormula.Compile(out DensityFormulaEvaluator);
            EdgeTable = Marching.EdgeTable(Allocator.Persistent);
            TriTable = Marching.TriTable(Allocator.Persistent);
            EdgeConnection = Marching.EdgeConnection(Allocator.Persistent);
            EdgeDirection = Marching.EdgeDirection(Allocator.Persistent);
            _queue = new Queue<Chunk>((2 * VoxelSide + 1)*(2 * VoxelSide + 1));
            // DensityFormula.Compile(ref _lastHash, out DensityFormulaEvaluator);
        }

        private void OnDestroy()
        {
            DensityFormulaEvaluator.Dispose();
            EdgeTable.Dispose();
            TriTable.Dispose();
            EdgeDirection.Dispose();
            EdgeConnection.Dispose();
        }

        public void ForceComplete() => _currentHandle.Complete();
        public void RequestChunk(Chunk chunk, int3 coords, int scale, bool forceImmediate = false, bool cancelPrevious = false)
        {
            chunk.Coords = coords;
            chunk.Scale = scale;
            if (forceImmediate)
            {
                _currentHandle = JobHandle.CombineDependencies(_currentHandle, GenerateChunk(chunk));
            }
            else
            {
                if (!chunk.InQueue)
                {
                    chunk.InQueue = true;
                    _queue.Enqueue(chunk);
                }
            }
        }

        private void Update()
        {
            if (_currentHandle.IsCompleted && _queue.Count > 0)
            {
                // Stopwatch sw = Stopwatch.StartNew();

                // while (_queue.Count > 0 && sw.ElapsedMilliseconds < 8)
                {
                    var chunk = _queue.Dequeue();
                    _currentHandle = GenerateChunk(chunk);
                    // _currentHandle = JobHandle.CombineDependencies(_currentHandle, chunk.RequestGeneration());
                }
            }
        }

        private static JobHandle GenerateChunk(Chunk chunk)
        {
            chunk.Generating = true;

            return chunk.RequestGeneration();
        }

        public static unsafe bool GetCornerCoords(int3 voxelCoords, int v2, out OctInt coords)
        {
            coords.Value[0] = CoordsToIndex(voxelCoords, v2);
            coords.Value[1] = coords.Value[0] + 1; // +x
            coords.Value[2] = coords.Value[1] + v2; // +z
            coords.Value[3] = coords.Value[2] - 1; // -x
            coords.Value[4] = coords.Value[0] + v2*v2; // +y
            coords.Value[5] = coords.Value[1] + v2*v2; // +y
            coords.Value[6] = coords.Value[2] + v2*v2; // +y
            coords.Value[7] = coords.Value[3] + v2*v2; // +y
            return true;
        }


        /// <summary>
        /// IndexToCoords
        /// </summary>
        /// <param name="index"></param>
        /// <param name="v3">VoxelSide + 1</param>
        /// <returns></returns>
        public static int3 IndexToCoords(int index, int v3)
        {
            return new int3(
                (index % v3) - 1,
                index / (v3 * v3) - 1,
                ((index % (v3 * v3)) / v3) - 1
            );
        }
        public static int3 IndexToCoordsNoPadding(int index, int v3)
        {
            return new int3(
                (index % v3),
                index / (v3 * v3),
                ((index % (v3 * v3)) / v3)
            );
        }

        public static int CoordsToIndex(int3 coords, int v1)
        {
            return (coords.x + 1) + (coords.y + 1) * v1 * v1 + (coords.z + 1) * v1;
        }
        public static int CoordsToIndexNoPadding(int3 coords, int v1)
        {
            var coordsToIndexNoPadding = coords.x + coords.y * v1 * v1 + coords.z * v1;
            #if true
            Assert.AreEqual(coords, IndexToCoordsNoPadding(coordsToIndexNoPadding, v1));
            #endif
            return coordsToIndexNoPadding;
        }


        public static float Density(float3 coords)
        {
            // return noise.srdnoise(coords.xy * 0.5f, coords.z * 0.5f).x;
            // return coords.y - 0.25f;
            // return .25f - coords.z; // plane
            // float persistence = 1;
            // int octaves = 5;
            // float lacunarity = .5f;
            // coords *= .5f;
            // return Fbm.fbm(coords + Fbm.fbm(coords, persistence, octaves, lacunarity), persistence, octaves, lacunarity);
            //
            //
            // float3 warp = noise.srdnoise(coords.xy * .008f, coords.z* .008f).xyz;
            // coords += warp * .08f;
            // return 
            //     math.min(coords.y - .5f, noise.snoise(coords * 4.03f) * .25f +
            //                                                          noise.snoise(coords * 1.96f) * .5f +
            //                                                          noise.snoise(coords * .601f) * 1f);

            // return coords.y - ((math.sin(coords.x * 10) + math.cos(coords.z * 10)) * .1f + .25f);

            return 1.25f - math.distance(coords, new float3(.5f)); // sphere
            // return 0.5f - math.distance(coords, new float3(.5f,.5f,.5f));
        }

        public void ClearQueue()
        {
            while (_queue.Count > 0)
                _queue.Dequeue().InQueue = false;
            _currentHandle.Complete();
        }
    }

    public enum Algorithm
    {
        MarchingCube,
        DualContouring,
    }
}