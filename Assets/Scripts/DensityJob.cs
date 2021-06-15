using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct DensityJob : IJobFor
    {
        public int VoxelSide;
        public int3 Coords;
        public int Scale;
        [WriteOnly]
        public NativeArray<float> Densities;

        // public bool Code;

        [ReadOnly]
        public EvalGraph EvalGraph;


        public unsafe void Execute(int index)
        {
            var delta = Scale / (float)VoxelSide;
            var v1 = VoxelSide + 1;
            var v3 = VoxelSide + 3;
            // array is xxx zzz yyy
            // world space
            var local = MeshGen.IndexToCoords(index, v3);
            Assert.AreEqual(index, MeshGen.CoordsToIndex(local, v3));
            float3 coords = (float3)local * delta + Coords;
            float d =
                new EvalState().Run(EvalGraph, &coords).x;
                // MeshGen.Density(coords);
            Densities[index] = d;
            // Debug.Log(string.Format("{0} at {1}: {2}", index, coords, d));
        }
    }
}