using System.Reflection;

namespace EscapeFromDuckovCoopMod.DuckovNet.Core;

internal sealed class DuckovRpcMethodInfo
{
    public ushort MethodId { get; }
    public MethodInfo Method { get; }
    public DuckovRpcAttribute Attribute { get; }
    public Type DeclaringType { get; }
    public ParameterInfo[] Parameters { get; }
    public string FullMethodName { get; }

    public DuckovRpcMethodInfo(ushort methodId, MethodInfo method, DuckovRpcAttribute attribute)
    {
        MethodId = methodId;
        Method = method;
        Attribute = attribute;
        DeclaringType = method.DeclaringType;
        Parameters = method.GetParameters();
        FullMethodName = $"{DeclaringType.FullName}.{method.Name}";
    }

    public DeliveryMethod GetDeliveryMethod()
    {
        if (!Attribute.Reliable)
        {
            return Attribute.Ordered ? DeliveryMethod.Sequenced : DeliveryMethod.Unreliable;
        }
        return Attribute.Ordered ? DeliveryMethod.ReliableOrdered : DeliveryMethod.ReliableUnordered;
    }

    public byte GetChannel()
    {
        return (byte)Mathf.Clamp(Attribute.Channel, 0, 255);
    }
}
