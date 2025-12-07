















using System.Collections;

namespace EscapeFromDuckovCoopMod;




public class SceneInitManager : MonoBehaviour
{
    public static SceneInitManager Instance { get; private set; }

    private readonly Queue<Action> _taskQueue = new();
    private bool _isProcessing = false;
    private const float MAX_FRAME_TIME_MS = 3f; 

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

    
    
    
    public void EnqueueTask(Action task, string taskName = "Unknown")
    {
        if (task == null) return;

        _taskQueue.Enqueue(() =>
        {
            try
            {
                task();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneInit] Task '{taskName}' failed: {e}");
            }
        });

        
        if (!_isProcessing)
        {
            StartCoroutine(ProcessTaskQueue());
        }
    }

    
    
    
    public void EnqueueDelayedTask(Action task, float delaySeconds, string taskName = "Unknown")
    {
        StartCoroutine(DelayedEnqueue(task, delaySeconds, taskName));
    }

    private IEnumerator DelayedEnqueue(Action task, float delaySeconds, string taskName)
    {
        yield return new WaitForSeconds(delaySeconds);
        EnqueueTask(task, taskName);
    }

    
    
    
    public void EnqueueBatch(IEnumerable<Action> tasks, string batchName = "Batch")
    {
        int count = 0;
        foreach (var task in tasks)
        {
            var taskIndex = count++;
            EnqueueTask(task, $"{batchName}_{taskIndex}");
        }
    }

    
    
    
    private IEnumerator ProcessTaskQueue()
    {
        _isProcessing = true;

        while (_taskQueue.Count > 0)
        {
            var frameStartTime = Time.realtimeSinceStartup;

            
            while (_taskQueue.Count > 0)
            {
                var elapsed = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
                if (elapsed > MAX_FRAME_TIME_MS) break;

                var task = _taskQueue.Dequeue();
                task?.Invoke();
            }

            yield return null; 
        }

        _isProcessing = false;
    }

    
    
    
    public void ClearQueue()
    {
        _taskQueue.Clear();
        _isProcessing = false;
    }

    
    
    
    public int PendingTaskCount => _taskQueue.Count;

    
    
    
    public bool IsProcessing => _isProcessing;
}

