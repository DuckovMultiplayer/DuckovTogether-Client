















using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace EscapeFromDuckovCoopMod.Jobs
{
    
    
    
    
    [BurstCompile] 
    public struct AISeedCalculationJob : IJobParallelFor
    {
        [ReadOnly]
        public int sceneSeed;

        [ReadOnly]
        public NativeArray<int> rootIds;

        [WriteOnly]
        public NativeArray<int> calculatedSeeds;

        
        
        
        public void Execute(int index)
        {
            
            int rootId = rootIds[index];

            
            
            int seed = sceneSeed ^ rootId;
            seed = seed * 1103515245 + 12345; 
            seed = (seed / 65536) % 32768;

            calculatedSeeds[index] = seed;
        }
    }

    
    
    
    [BurstCompile]
    public struct AISeedPairCalculationJob : IJobParallelFor
    {
        [ReadOnly]
        public int sceneSeed;

        [ReadOnly]
        public NativeArray<int> rootIdsA; 

        [ReadOnly]
        public NativeArray<int> rootIdsB; 

        [WriteOnly]
        public NativeArray<int> calculatedSeedsA;

        [WriteOnly]
        public NativeArray<int> calculatedSeedsB;

        public void Execute(int index)
        {
            int idA = rootIdsA[index];
            int idB = rootIdsB[index];

            
            int seedA = sceneSeed ^ idA;
            seedA = seedA * 1103515245 + 12345;
            seedA = (seedA / 65536) % 32768;

            int seedB = sceneSeed ^ idB;
            seedB = seedB * 1103515245 + 12345;
            seedB = (seedB / 65536) % 32768;

            calculatedSeedsA[index] = seedA;
            calculatedSeedsB[index] = seedB;
        }
    }
}

