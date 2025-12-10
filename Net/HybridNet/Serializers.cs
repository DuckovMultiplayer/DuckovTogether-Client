using System;
using System.Text;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net.HybridNet;

public static class JsonSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };
    
    public static byte[] Serialize<T>(T obj)
    {
        var json = JsonConvert.SerializeObject(obj, Settings);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public static T Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json);
    }
}

public static class BinarySerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };
    
    public static byte[] Serialize<T>(T obj)
    {
        if (obj is IAITransformData transform)
            return SerializeAITransform(transform);
        
        if (obj is IAIAnimationData anim)
            return SerializeAIAnimation(anim);
        
        var json = JsonConvert.SerializeObject(obj, Settings);
        return Encoding.UTF8.GetBytes(json);
    }
    
    public static T Deserialize<T>(byte[] data)
    {
        if (typeof(T).GetInterface(nameof(IAITransformData)) != null)
            return (T)DeserializeAITransform(data);
        
        if (typeof(T).GetInterface(nameof(IAIAnimationData)) != null)
            return (T)DeserializeAIAnimation(data);
        
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json);
    }
    
    private static byte[] SerializeAITransform(IAITransformData transform)
    {
        var buffer = new byte[28];
        int offset = 0;
        
        BitConverter.GetBytes(transform.EntityId).CopyTo(buffer, offset);
        offset += 4;
        
        BitConverter.GetBytes(transform.PosX).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(transform.PosY).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(transform.PosZ).CopyTo(buffer, offset);
        offset += 4;
        
        BitConverter.GetBytes(transform.ForwardX).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(transform.ForwardY).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(transform.ForwardZ).CopyTo(buffer, offset);
        offset += 4;
        
        BitConverter.GetBytes(transform.Timestamp).CopyTo(buffer, offset);
        
        return buffer;
    }
    
    private static object DeserializeAITransform(byte[] data)
    {
        int offset = 0;
        
        var entityId = BitConverter.ToInt32(data, offset);
        offset += 4;
        
        var posX = BitConverter.ToSingle(data, offset);
        offset += 4;
        var posY = BitConverter.ToSingle(data, offset);
        offset += 4;
        var posZ = BitConverter.ToSingle(data, offset);
        offset += 4;
        
        var fwdX = BitConverter.ToSingle(data, offset);
        offset += 4;
        var fwdY = BitConverter.ToSingle(data, offset);
        offset += 4;
        var fwdZ = BitConverter.ToSingle(data, offset);
        offset += 4;
        
        var timestamp = BitConverter.ToSingle(data, offset);
        
        return new AITransformMessage
        {
            EntityId = entityId,
            PosX = posX,
            PosY = posY,
            PosZ = posZ,
            ForwardX = fwdX,
            ForwardY = fwdY,
            ForwardZ = fwdZ,
            Timestamp = timestamp
        };
    }
    
    private static byte[] SerializeAIAnimation(IAIAnimationData anim)
    {
        var buffer = new byte[23];
        int offset = 0;
        
        BitConverter.GetBytes(anim.EntityId).CopyTo(buffer, offset);
        offset += 4;
        
        BitConverter.GetBytes(anim.Speed).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(anim.DirX).CopyTo(buffer, offset);
        offset += 4;
        BitConverter.GetBytes(anim.DirY).CopyTo(buffer, offset);
        offset += 4;
        
        BitConverter.GetBytes(anim.HandState).CopyTo(buffer, offset);
        offset += 4;
        
        buffer[offset++] = (byte)(anim.GunReady ? 1 : 0);
        buffer[offset++] = (byte)(anim.Dashing ? 1 : 0);
        buffer[offset] = (byte)(anim.Attacking ? 1 : 0);
        
        return buffer;
    }
    
    private static object DeserializeAIAnimation(byte[] data)
    {
        int offset = 0;
        
        var entityId = BitConverter.ToInt32(data, offset);
        offset += 4;
        
        var speed = BitConverter.ToSingle(data, offset);
        offset += 4;
        var dirX = BitConverter.ToSingle(data, offset);
        offset += 4;
        var dirY = BitConverter.ToSingle(data, offset);
        offset += 4;
        
        var handState = BitConverter.ToInt32(data, offset);
        offset += 4;
        
        var gunReady = data[offset++] == 1;
        var dashing = data[offset++] == 1;
        var attacking = data[offset] == 1;
        
        return new AIAnimationMessage
        {
            EntityId = entityId,
            Speed = speed,
            DirX = dirX,
            DirY = dirY,
            HandState = handState,
            GunReady = gunReady,
            Dashing = dashing,
            Attacking = attacking
        };
    }
}

public interface IAITransformData
{
    int EntityId { get; }
    float PosX { get; }
    float PosY { get; }
    float PosZ { get; }
    float ForwardX { get; }
    float ForwardY { get; }
    float ForwardZ { get; }
    float Timestamp { get; }
}

public interface IAIAnimationData
{
    int EntityId { get; }
    float Speed { get; }
    float DirX { get; }
    float DirY { get; }
    int HandState { get; }
    bool GunReady { get; }
    bool Dashing { get; }
    bool Attacking { get; }
}
