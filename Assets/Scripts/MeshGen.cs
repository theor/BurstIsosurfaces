using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTemplateProjects
{
    public class MeshGen : MonoBehaviour
    {
        public  struct OctInt
        {
            public unsafe fixed int Value[8];
            public unsafe int this[int i] => Value[i];
        }
        
        public unsafe struct OctFloat
        {
            public fixed float Value[8];
            public float this[int i]
            {
                get { return Value[i]; }
                set { Value[i] = value; }
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
        public Eval DensityFormulaEvaluator { get; private set; }

        private void Reset()
        {
            if (!DensityFormula)
                DensityFormula = Formula.CreateInstance<Formula>();
        }

        private void Start()
        {
            EdgeTable = Marching.EdgeTable(Allocator.Persistent);
            TriTable = Marching.TriTable(Allocator.Persistent);
            EdgeConnection = Marching.EdgeConnection(Allocator.Persistent);
            EdgeDirection = Marching.EdgeDirection(Allocator.Persistent);
            _queue = new Queue<Chunk>((2 * VoxelSide + 1)*(2 * VoxelSide + 1));
            DensityFormulaEvaluator = DensityFormula.MakeEval();
        }

        private void OnDestroy()
        {
            DensityFormulaEvaluator.Dispose();
            EdgeTable.Dispose();
            TriTable.Dispose();
            EdgeDirection.Dispose();
            EdgeConnection.Dispose();
        }
        
        public void RequestChunk(Chunk chunk, int3 coords)
        {
            chunk.Coords = coords;
            _queue.Enqueue(chunk);
        }

        private void Update()
        {
            if (_currentHandle.IsCompleted && _queue.Count > 0)
            {
                var chunk = _queue.Dequeue();
                chunk.Generating = true;

                chunk.OutputMeshData = Mesh.AllocateWritableMeshData(1);
            
                _currentHandle = chunk.RequestGeneration();
            }
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

        private static float fbm(float3 pos, float persistence, int octaves, float lacunarity)
        {
            float g = math.exp2(-persistence);
            float f = 1.0f;
            float a = 1.0f;
            float t = 0.0f;
            for (int i = 0; i < octaves; i++)
            {
                t += a * noise.snoise(f * pos);
                f *= lacunarity;
                a *= g;
            }

            return t;
        }

        public static float Density(float3 coords)
        {
            return coords.y - 0.25f;
            // return .25f - coords.z; // plane
            float persistence = 1;
            int octaves = 5;
            float lacunarity = .5f;
            coords *= .5f;
            return fbm(coords + fbm(coords, persistence, octaves, lacunarity), persistence, octaves, lacunarity);
            
            
            float3 warp = noise.srdnoise(coords.xy * .008f, coords.z* .008f).xyz;
            coords += warp * .08f;
            return 
                math.min(coords.y - .5f, noise.snoise(coords * 4.03f) * .25f +
                                                                     noise.snoise(coords * 1.96f) * .5f +
                                                                     noise.snoise(coords * .601f) * 1f);

            // return coords.y - ((math.sin(coords.x * 10) + math.cos(coords.z * 10)) * .1f + .25f);

            // return math.distance(coords, new float3(.5f)) - .25f; // sphere
            // return .5f - math.distance(coords, new float3(.5f,.5f,.5f));
        }
    }
}