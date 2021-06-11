using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct DensityJob : IJobFor
    {
        public int VoxelSide;
        public int3 Coords;
        [WriteOnly]
        public NativeArray<float> Densities;

        public Eval Eval;
        public void Execute(int index)
        {
            var delta = 1f / VoxelSide;
            var v1 = VoxelSide + 1;
            // array is xxx zzz yyy
            float3 coords = (float3)MeshGen.IndexToCoords(index, v1) * delta + Coords;
            Eval.Params[0] = coords;
            float d = 
                Eval.Run().x;
                // MeshGen.Density(coords);
            Densities[index] = d;
            Debug.Log(string.Format("{0}: {1}", index, d));
        }
    }
}