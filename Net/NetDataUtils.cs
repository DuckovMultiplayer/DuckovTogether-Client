















using System.Runtime.CompilerServices;

namespace EscapeFromDuckovCoopMod;

public static class NetDataExtensions
{
    public static void PutVector3(this NetDataWriter writer, Vector3 vector)
    {
        writer.Put(vector.x);
        writer.Put(vector.y);
        writer.Put(vector.z);
    }

    public static Vector3 GetVector3(this NetDataReader reader)
    {
        return new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Finite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quaternion NormalizeSafe(Quaternion q)
    {
        if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w))
            return Quaternion.identity;

        
        var mag2 = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (mag2 < 1e-12f) return Quaternion.identity;

        var inv = 1.0f / Mathf.Sqrt(mag2);
        q.x *= inv;
        q.y *= inv;
        q.z *= inv;
        q.w *= inv;
        return q;
    }

    public static void PutQuaternion(this NetDataWriter writer, Quaternion q)
    {
        q = NormalizeSafe(q);
        writer.Put(q.x);
        writer.Put(q.y);
        writer.Put(q.z);
        writer.Put(q.w);
    }

    public static Quaternion GetQuaternion(this NetDataReader reader)
    {
        var q = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        return NormalizeSafe(q);
    }
}