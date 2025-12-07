















using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Jobs
{
    
    
    
    
    public class JobSystemManager : MonoBehaviour
    {
        public static JobSystemManager Instance { get; private set; }

        
        private readonly List<JobHandle> _activeJobHandles = new();

        
        public static bool EnableJobSystem = true;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            
            CompleteAllJobs();
        }

        
        
        
        public JobHandle ScheduleAIPathCalculation(
            NativeArray<Vector3> startPositions,
            NativeArray<Vector3> targetPositions,
            NativeArray<Vector3> resultDirections,
            int innerloopBatchCount = 64)
        {
            if (!EnableJobSystem || startPositions.Length == 0)
                return default;

            var job = new AIPathCalculationJob
            {
                startPositions = startPositions,
                targetPositions = targetPositions,
                calculatedDirections = resultDirections
            };

            
            var handle = job.Schedule(startPositions.Length, innerloopBatchCount, default);
            _activeJobHandles.Add(handle);

            return handle;
        }

        
        
        
        public JobHandle ScheduleNetworkDeserialize(
            NativeArray<byte> packetData,
            NativeArray<int> offsets,
            NativeArray<int> lengths,
            NativeArray<int> resultIds,
            int innerloopBatchCount = 64)
        {
            if (!EnableJobSystem || offsets.Length == 0)
                return default;

            var job = new NetworkPacketDeserializeJob
            {
                packetData = packetData,
                packetOffsets = offsets,
                packetLengths = lengths,
                decodedIds = resultIds
            };

            
            var handle = job.Schedule(offsets.Length, innerloopBatchCount, default);
            _activeJobHandles.Add(handle);

            return handle;
        }

        
        
        
        public void CompleteAllJobs()
        {
            foreach (var handle in _activeJobHandles)
            {
                if (!handle.IsCompleted)
                {
                    handle.Complete();
                }
            }
            _activeJobHandles.Clear();
        }

        
        
        
        public void CompleteJob(JobHandle handle)
        {
            if (!handle.IsCompleted)
            {
                handle.Complete();
            }
        }

        private void OnDestroy()
        {
            
            CompleteAllJobs();
        }
    }
}

