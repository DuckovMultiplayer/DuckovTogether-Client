















using EscapeFromDuckovCoopMod.Utils;

namespace EscapeFromDuckovCoopMod;




public static class SortedListExtensions
{
    
    
    
    public static void RemoveRange<T, U>(this SortedList<T, U> list, int amount)
    {
        for (int i = 0; i < amount && i < list.Count; ++i)
        {
            list.RemoveAt(0);
        }
    }
}

public class NetInterpolator : MonoBehaviour
{
    [Tooltip("渲染回看时间；越大越稳，越小越跟手")] public float interpolationBackTime = 0.12f;

    [Tooltip("缺帧时最多允许预测多久")] public float maxExtrapolate = 0.05f;

    [Tooltip("误差过大时直接硬对齐距离")] public float hardSnapDistance = 6f; 

    [Tooltip("位置平滑插值的瞬时权重")] public float posLerpFactor = 0.9f;

    [Tooltip("朝向平滑插值的瞬时权重")] public float rotLerpFactor = 0.9f;

    [Header("跑步反超护栏")] public bool extrapolateWhenRunning; 

    public float runSpeedThreshold = 3.0f; 

    [Header("EMA 平滑配置 (高级)")]
    [Tooltip("启用 EMA 平滑网络抖动")]
    public bool enableEmaSmoothing = true;

    [Tooltip("动态调整延迟（根据网络质量自动优化）")]
    public bool enableDynamicDelay = true;

    [Tooltip("最小延迟时间（秒）")]
    public float minInterpolationDelay = 0.05f;

    [Tooltip("最大延迟时间（秒）")]
    public float maxInterpolationDelay = 0.25f;

    [Tooltip("延迟安全倍数（增加此值可提高稳定性但增加延迟）")]
    public float delaySafetyMultiplier = 2.0f;

    [Header("网络配置")]
    [Tooltip("服务器发送频率 (Hz)，用于计算追赶/减速阈值")]
    public int sendRate = 60;

    [Header("追赶/减速配置 (高级)")]
    [Tooltip("启用自动追赶/减速机制（推荐）")]
    public bool enableCatchupSlowdown = true;

    [Tooltip("追赶加速比例（0-1，推荐0.05 = 5%加速）")]
    [Range(0f, 0.5f)]
    public float catchupSpeed = 0.05f;

    [Tooltip("减速比例（0-1，推荐0.04 = 4%减速）")]
    [Range(0f, 0.5f)]
    public float slowdownSpeed = 0.04f;

    [Tooltip("追赶阈值（帧数倍数，推荐1.0，即落后1帧时开始追赶）")]
    public float catchupPositiveThreshold = 1.0f;

    [Tooltip("减速阈值（帧数倍数，推荐-1.0，即超前1帧时开始减速）")]
    public float catchupNegativeThreshold = -1.0f;

    [Header("调试信息")]
    [Tooltip("显示插值调试信息")]
    public bool showDebugInfo = false;

    [Tooltip("启用二分查找优化（推荐）")]
    public bool enableBinarySearch = true;

    private readonly SortedList<double, Snap> _buf = new(64); 
    private readonly object _bufferLock = new();               
    private Vector3 _lastVel = Vector3.zero;
    private Transform modelRoot; 
    private Transform root; 

    
    private ExponentialMovingAverage _delayEma = new(60);           
    private ExponentialMovingAverage _intervalEma = new(60);        
    private ExponentialMovingAverage _driftEma = new(60);           
    private ExponentialMovingAverage _deliveryTimeEma = new(120);   
    private double _lastPushTime = -1;                              
    private double _baseInterpolationDelay;                         

    
    private double _localTimeline = 0;                           
    private double _localTimeScale = 1.0;                        
    private bool _timelineInitialized = false;                   

    
    private float SendInterval => 1f / Mathf.Max(1, sendRate);   

    private void LateUpdate()
    {
        
        if (!root)
        {
            var cmc = GetComponentInChildren<CharacterMainControl>();
            if (cmc)
            {
                root = cmc.transform;
                modelRoot = cmc.modelRoot ? cmc.modelRoot.transform : cmc.transform;
            }
            else
            {
                root = transform;
            }
        }

        if (!modelRoot) modelRoot = root;

        
        lock (_bufferLock)
        {
            if (_buf.Count == 0) return;

            
            if (enableCatchupSlowdown && _timelineInitialized)
            {
                
                _localTimeline += Time.unscaledDeltaTime * _localTimeScale;
            }
            else
            {
                
                _localTimeline = Time.unscaledTimeAsDouble - interpolationBackTime;
            }

            double renderT = _localTimeline;

            
            int i;
            if (enableBinarySearch && _buf.Count > 10)
            {
                
                i = BinarySearchSnapshot(renderT);
            }
            else
            {
                
                i = 0;
                var keys = _buf.Keys;
                while (i < _buf.Count && keys[i] < renderT) i++;
            }

            if (i == 0)
            {
                
                var first = _buf.Values[0];
                Apply(first.pos, first.rot, true);
                return;
            }

            if (i < _buf.Count)
            {
                
                var a = _buf.Values[i - 1];
                var b = _buf.Values[i];
                var t = (float)((renderT - a.t) / Math.Max(1e-6, b.t - a.t));
                var pos = Vector3.LerpUnclamped(a.pos, b.pos, t);
                var rot = Quaternion.Slerp(a.rot, b.rot, t);
                Apply(pos, rot);

                
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[NetInterp] 时间轴={_localTimeline:F3}s, " +
                        $"时间缩放={_localTimeScale:F3}x, " +
                        $"插值进度={t:F2}, " +
                        $"缓冲区={_buf.Count}, " +
                        $"查找方式={(enableBinarySearch && _buf.Count > 10 ? "二分" : "线性")}");
                }

                
                if (i > 1)
                {
                    _buf.RemoveRange(i - 1);
                }
            }
            else
            {
                
                var last = _buf.Values[_buf.Count - 1];
                var dt = renderT - last.t;

                
                var allow = dt <= maxExtrapolate;
                if (!extrapolateWhenRunning)
                {
                    var speed = _lastVel.magnitude;
                    if (speed > runSpeedThreshold) allow = false; 
                }

                if (allow)
                    Apply(last.pos + _lastVel * (float)dt, last.rot);
                else
                    Apply(last.pos, last.rot);

                
                if (_buf.Count > 2)
                {
                    _buf.RemoveRange(_buf.Count - 2);
                }
            }
        } 
    }

    
    
    
    private int BinarySearchSnapshot(double targetTime)
    {
        var keys = _buf.Keys;
        int left = 0;
        int right = _buf.Count - 1;
        int result = _buf.Count; 

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (keys[mid] < targetTime)
            {
                left = mid + 1;
            }
            else
            {
                result = mid;
                right = mid - 1;
            }
        }

        return result;
    }

    public void Init(Transform rootT, Transform modelRootT)
    {
        root = rootT;
        modelRoot = modelRootT ? modelRootT : rootT;
    }

    
    
    
    
    
    public void Push(Vector3 pos, Quaternion rot, double when = -1)
    {
        double now = Time.unscaledTimeAsDouble;
        if (when < 0) when = now;

        lock (_bufferLock) 
        {
            
            if (_buf.Count > 0)
            {
                var prev = _buf.Values[_buf.Count - 1];
                var dt = when - prev.t;
                if (dt > 1e-6) _lastVel = (pos - prev.pos) / (float)dt;

                
                float distSqr = (pos - prev.pos).sqrMagnitude;
                if (distSqr > hardSnapDistance * hardSnapDistance)
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning($"[NetInterp] 异常跳变: {Mathf.Sqrt(distSqr):F2}m, 清空缓冲");
                    }
                    _buf.Clear();
                    _delayEma.Reset();
                    _intervalEma.Reset();
                    _driftEma.Reset();
                    _deliveryTimeEma.Reset();
                    _timelineInitialized = false;
                    _localTimeScale = 1.0;
                }
            }

            

            
            if (_buf.Count == 0)
            {
                _localTimeline = when - interpolationBackTime;
                _timelineInitialized = true;
            }

            
            int beforeCount = _buf.Count;
            var newSnap = new Snap { t = when, localTime = now, pos = pos, rot = rot };

            
            if (_buf.Count >= 64)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[NetInterp] 缓冲区已满(64)，清空");
                }
                _buf.Clear();
                _localTimeline = when - interpolationBackTime;
            }

            _buf[when] = newSnap;
            bool wasInserted = _buf.Count > beforeCount;

            
            if (wasInserted && _buf.Count >= 2)
            {
                
                var secondLast = _buf.Values[_buf.Count - 2];
                var latest = _buf.Values[_buf.Count - 1];

                double localDeliveryTime = latest.localTime - secondLast.localTime;
                _deliveryTimeEma.Add(localDeliveryTime);

                
                if (enableDynamicDelay && _deliveryTimeEma.IsInitialized)
                {
                    
                    
                    double jitterStdDev = _deliveryTimeEma.StandardDeviation;
                    double dynamicMultiplier = ((SendInterval + jitterStdDev) / SendInterval) +
                                              delaySafetyMultiplier; 

                    
                    dynamicMultiplier = Math.Clamp(dynamicMultiplier, 0, 5);

                    
                    float oldDelay = interpolationBackTime;
                    interpolationBackTime = (float)(SendInterval * dynamicMultiplier);
                    interpolationBackTime = Math.Clamp(interpolationBackTime,
                        minInterpolationDelay, maxInterpolationDelay);

                    if (showDebugInfo && Time.frameCount % 60 == 0)
                    {
                        Debug.Log($"[NetInterp] 动态调整: jitter={jitterStdDev:F4}s, " +
                            $"倍数={dynamicMultiplier:F2}x, " +
                            $"延迟: {oldDelay:F3}s→{interpolationBackTime:F3}s");
                    }
                }

                
                double latestRemoteTime = when;
                double bufferTime = interpolationBackTime;
                double targetTime = latestRemoteTime - bufferTime;
                double lowerBound = targetTime - bufferTime;
                double upperBound = targetTime + bufferTime;
                _localTimeline = Math.Clamp(_localTimeline, lowerBound, upperBound);

                
                double timeDiff = latestRemoteTime - _localTimeline;
                _driftEma.Add(timeDiff);

                
                double drift = _driftEma.Value - bufferTime;

                
                double absoluteNegativeThreshold = SendInterval * catchupNegativeThreshold;
                double absolutePositiveThreshold = SendInterval * catchupPositiveThreshold;

                
                if (drift > absolutePositiveThreshold)
                {
                    _localTimeScale = 1.0 + catchupSpeed; 
                }
                else if (drift < absoluteNegativeThreshold)
                {
                    _localTimeScale = 1.0 - slowdownSpeed; 
                }
                else
                {
                    _localTimeScale = 1.0; 
                }

                
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    string mode = _localTimeScale > 1.0 ? "追赶" :
                                 _localTimeScale < 1.0 ? "减速" : "正常";
                    Debug.Log($"[NetInterp] {mode}: drift={drift:F4}s, " +
                        $"timeDiff={timeDiff:F4}s, " +
                        $"缩放={_localTimeScale:F3}x, " +
                        $"缓冲={_buf.Count}");
                }
            }
        } 
    }

    
    
    
    public NetworkQualityStats GetNetworkStats()
    {
        return new NetworkQualityStats
        {
            AverageDelay = _delayEma.Value,
            DelayStandardDeviation = _delayEma.StandardDeviation,
            AveragePacketInterval = _intervalEma.Value,
            PacketIntervalJitter = _intervalEma.StandardDeviation,
            CurrentInterpolationDelay = interpolationBackTime,
            BufferSize = _buf.Count,
            IsEmaInitialized = _delayEma.IsInitialized && _intervalEma.IsInitialized,
            
            LocalTimeline = _localTimeline,
            LocalTimeScale = _localTimeScale,
            Drift = _driftEma.Value,
            DriftStandardDeviation = _driftEma.StandardDeviation,
            IsTimelineInitialized = _timelineInitialized,
            
            AverageDeliveryTime = _deliveryTimeEma.Value,
            DeliveryTimeJitter = _deliveryTimeEma.StandardDeviation
        };
    }

    
    
    
    public struct NetworkQualityStats
    {
        public double AverageDelay;              
        public double DelayStandardDeviation;    
        public double AveragePacketInterval;     
        public double PacketIntervalJitter;      
        public float CurrentInterpolationDelay;  
        public int BufferSize;                   
        public bool IsEmaInitialized;            

        
        public double LocalTimeline;             
        public double LocalTimeScale;            
        public double Drift;                     
        public double DriftStandardDeviation;    
        public bool IsTimelineInitialized;       

        
        public double AverageDeliveryTime;       
        public double DeliveryTimeJitter;        

        
        
        
        public readonly int GetQualityScore()
        {
            if (!IsEmaInitialized) return 0;

            
            double latencyScore = Math.Max(0, 100 - AverageDelay * 1000); 
            double jitterScore = Math.Max(0, 100 - PacketIntervalJitter * 2000); 
            double driftScore = Math.Max(0, 100 - Math.Abs(Drift) * 500); 

            return (int)((latencyScore + jitterScore + driftScore) / 3);
        }

        
        
        
        public readonly string GetStatusDescription()
        {
            int score = GetQualityScore();
            if (score >= 80) return "优秀";
            if (score >= 60) return "良好";
            if (score >= 40) return "一般";
            if (score >= 20) return "较差";
            return "很差";
        }
    }

    private void Apply(Vector3 pos, Quaternion rot, bool hardSnap = false)
    {
        if (!root) return;

        
        if (hardSnap || (root.position - pos).sqrMagnitude > hardSnapDistance * hardSnapDistance)
        {
            root.SetPositionAndRotation(pos, rot);
            if (modelRoot && modelRoot != root) modelRoot.rotation = rot;
            return;
        }

        
        
        
        if (enableCatchupSlowdown && _timelineInitialized)
        {
            
            root.position = pos;
            if (modelRoot)
                modelRoot.rotation = rot;
            else
                root.rotation = rot;
        }
        else
        {
            
            root.position = Vector3.Lerp(root.position, pos, posLerpFactor);
            if (modelRoot)
                modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, rot, rotLerpFactor);
        }
    }

    private struct Snap
    {
        public double t;           
        public double localTime;   
        public Vector3 pos;
        public Quaternion rot;
    }
}


public static class NetInterpUtil
{
    public static NetInterpolator Attach(GameObject go)
    {
        if (!go) return null;
        var ni = go.GetComponent<NetInterpolator>();
        if (!ni) ni = go.AddComponent<NetInterpolator>();
        var cmc = go.GetComponent<CharacterMainControl>();
        if (cmc) ni.Init(cmc.transform, cmc.modelRoot ? cmc.modelRoot.transform : cmc.transform);
        return ni;
    }
}