namespace EscapeFromDuckovCoopMod.DuckovNet.Core;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DuckovRpcAttribute : Attribute
{
    public RpcTarget Target { get; set; }
    public RpcPriority Priority { get; set; }
    public bool Reliable { get; set; }
    public bool Ordered { get; set; }
    public int Channel { get; set; }

    public DuckovRpcAttribute(RpcTarget target = RpcTarget.All)
    {
        Target = target;
        Priority = RpcPriority.Normal;
        Reliable = true;
        Ordered = true;
        Channel = 0;
    }
}

public enum RpcTarget
{
    Server,
    Client,
    All,
    Others,
    Owner
}

public enum RpcPriority
{
    Low,
    Normal,
    High,
    Critical
}
