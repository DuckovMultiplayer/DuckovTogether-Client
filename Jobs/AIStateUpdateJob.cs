















using Unity.Jobs;
using Unity.Collections;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Jobs
{
    
    
    
    
    public struct AIStateUpdateJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int> aiIds;

        [ReadOnly]
        public NativeArray<Vector3> positions;

        [ReadOnly]
        public NativeArray<Quaternion> rotations;

        [ReadOnly]
        public float deltaTime;

        public void Execute(int index)
        {
            
            

            
            
            
            

            
            
        }
    }

    
    
    
    public struct AIPathCalculationJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> startPositions;

        [ReadOnly]
        public NativeArray<Vector3> targetPositions;

        [WriteOnly]
        public NativeArray<Vector3> calculatedDirections;

        public void Execute(int index)
        {
            
            Vector3 direction = (targetPositions[index] - startPositions[index]).normalized;
            calculatedDirections[index] = direction;
        }
    }

    
    
    
    public struct NetworkPacketDeserializeJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<byte> packetData;

        [ReadOnly]
        public NativeArray<int> packetOffsets;

        [ReadOnly]
        public NativeArray<int> packetLengths;

        
        [WriteOnly]
        public NativeArray<int> decodedIds;

        public void Execute(int index)
        {
            
            int offset = packetOffsets[index];
            int length = packetLengths[index];

            
            if (length >= 4)
            {
                int id = (packetData[offset] << 0) |
                        (packetData[offset + 1] << 8) |
                        (packetData[offset + 2] << 16) |
                        (packetData[offset + 3] << 24);

                decodedIds[index] = id;
            }
        }
    }
}

