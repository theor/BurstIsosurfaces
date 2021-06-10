using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityTemplateProjects
{
    [BurstCompile]
    public struct DensityJob : IJobParallelFor
    {
        public int VoxelSide;
        public int2 Coords;
        [WriteOnly]
        public NativeArray<float> Densities;
        public void Execute(int index)
        {
            var delta = 1f / VoxelSide;
            var v1 = VoxelSide + 1;
            // array is xxx zzz yyy
            float3 coords = (float3)MeshGen.IndexToCoords(index, v1) * delta + new float3(Coords.x, 0, Coords.y);
            float d = MeshGen.Density(coords);
            Densities[index] = d;
        }
    }
}