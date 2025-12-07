using System.Reflection;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod.DuckovNet.Core;

public sealed class DuckovNetRuntime : MonoBehaviour
{
    public static DuckovNetRuntime Instance { get; private set; }

    private const ushort PROTOCOL_ID = 0xDC17;
    private readonly Dictionary<ushort, DuckovRpcMethodInfo> _methodRegistry = new();
    private readonly Dictionary<string, ushort> _methodNameToId = new();
    private readonly Dictionary<Type, object> _serviceInstances = new();
    private ushort _nextMethodId = 1;

    private NetService _netService;
    private bool _isInitialized;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialize(NetService netService)
    {
        if (_isInitialized) return;

        _netService = netService;
        ScanAndRegisterRpcMethods();
        _isInitialized = true;

        LoggerHelper.LogInfo($"[DuckovNet] Initialized with {_methodRegistry.Count} RPC methods");
    }

    private void ScanAndRegisterRpcMethods()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    ScanType(type);
                }
            }
            catch
            {
            }
        }
    }

    private void ScanType(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<DuckovRpcAttribute>();
            if (attr != null)
            {
                RegisterMethod(method, attr);
            }
        }
    }

    private void RegisterMethod(MethodInfo method, DuckovRpcAttribute attribute)
    {
        var methodId = _nextMethodId++;
        var methodInfo = new DuckovRpcMethodInfo(methodId, method, attribute);

        _methodRegistry[methodId] = methodInfo;
        _methodNameToId[methodInfo.FullMethodName] = methodId;

        if (!method.IsStatic && !_serviceInstances.ContainsKey(method.DeclaringType))
        {
            var instance = FindObjectOfType(method.DeclaringType);
            if (instance != null)
            {
                _serviceInstances[method.DeclaringType] = instance;
            }
        }
    }

    public void InvokeRpc(string methodName, params object[] args)
    {
        if (!_methodNameToId.TryGetValue(methodName, out var methodId))
        {
            LoggerHelper.LogError($"[DuckovNet] RPC method not found: {methodName}");
            return;
        }

        InvokeRpc(methodId, args);
    }

    public void InvokeRpc(ushort methodId, params object[] args)
    {
        if (!_methodRegistry.TryGetValue(methodId, out var methodInfo))
        {
            LoggerHelper.LogError($"[DuckovNet] RPC method ID not found: {methodId}");
            return;
        }

        var writer = new NetDataWriter();
        writer.Put(PROTOCOL_ID);
        writer.Put(methodId);

        for (int i = 0; i < args.Length; i++)
        {
            var paramType = methodInfo.Parameters[i].ParameterType;
            DuckovNetSerializer.Serialize(writer, paramType, args[i]);
        }

        SendRpc(methodInfo, writer);
    }

    private void SendRpc(DuckovRpcMethodInfo methodInfo, NetDataWriter writer)
    {
        var deliveryMethod = methodInfo.GetDeliveryMethod();
        var channel = methodInfo.GetChannel();

        if (_netService.IsServer)
        {
            switch (methodInfo.Attribute.Target)
            {
                case RpcTarget.Client:
                case RpcTarget.All:
                case RpcTarget.Others:
                    foreach (var peer in _netService.netManager.ConnectedPeerList)
                    {
                        peer.Send(writer, channel, deliveryMethod);
                    }
                    break;
            }
        }
        else
        {
            if (_netService.connectedPeer != null)
            {
                _netService.connectedPeer.Send(writer, channel, deliveryMethod);
            }
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
    {
        var protocolId = reader.GetUShort();
        if (protocolId != PROTOCOL_ID)
        {
            LoggerHelper.LogWarning($"[DuckovNet] Invalid protocol ID: {protocolId}");
            return;
        }

        var methodId = reader.GetUShort();
        if (!_methodRegistry.TryGetValue(methodId, out var methodInfo))
        {
            LoggerHelper.LogWarning($"[DuckovNet] Unknown RPC method ID: {methodId}");
            return;
        }

        try
        {
            var args = new object[methodInfo.Parameters.Length];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = DuckovNetSerializer.Deserialize(reader, methodInfo.Parameters[i].ParameterType);
            }

            ExecuteRpc(methodInfo, args, peer);
        }
        catch (Exception ex)
        {
            LoggerHelper.LogError($"[DuckovNet] RPC execution failed: {methodInfo.FullMethodName} - {ex.Message}");
        }
    }

    private void ExecuteRpc(DuckovRpcMethodInfo methodInfo, object[] args, NetPeer peer)
    {
        object instance = null;
        if (!methodInfo.Method.IsStatic)
        {
            if (!_serviceInstances.TryGetValue(methodInfo.DeclaringType, out instance))
            {
                instance = FindObjectOfType(methodInfo.DeclaringType);
                if (instance != null)
                {
                    _serviceInstances[methodInfo.DeclaringType] = instance;
                }
            }

            if (instance == null)
            {
                LoggerHelper.LogError($"[DuckovNet] Service instance not found: {methodInfo.DeclaringType.Name}");
                return;
            }
        }

        methodInfo.Method.Invoke(instance, args);
    }

    public T GetService<T>() where T : class
    {
        if (_serviceInstances.TryGetValue(typeof(T), out var instance))
        {
            return instance as T;
        }
        return null;
    }
}
