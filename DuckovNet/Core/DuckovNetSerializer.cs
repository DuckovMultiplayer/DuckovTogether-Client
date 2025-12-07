namespace EscapeFromDuckovCoopMod.DuckovNet.Core;

internal static class DuckovNetSerializer
{
    private static readonly Dictionary<Type, Action<NetDataWriter, object>> _writers = new();
    private static readonly Dictionary<Type, Func<NetDataReader, object>> _readers = new();

    static DuckovNetSerializer()
    {
        RegisterPrimitives();
        RegisterUnityTypes();
    }

    private static void RegisterPrimitives()
    {
        Register<bool>((w, v) => w.Put(v), r => r.GetBool());
        Register<byte>((w, v) => w.Put(v), r => r.GetByte());
        Register<short>((w, v) => w.Put(v), r => r.GetShort());
        Register<ushort>((w, v) => w.Put(v), r => r.GetUShort());
        Register<int>((w, v) => w.Put(v), r => r.GetInt());
        Register<uint>((w, v) => w.Put(v), r => r.GetUInt());
        Register<long>((w, v) => w.Put(v), r => r.GetLong());
        Register<ulong>((w, v) => w.Put(v), r => r.GetULong());
        Register<float>((w, v) => w.Put(v), r => r.GetFloat());
        Register<double>((w, v) => w.Put(v), r => r.GetDouble());
        Register<string>((w, v) => w.Put(v ?? string.Empty), r => r.GetString());
    }

    private static void RegisterUnityTypes()
    {
        Register<Vector2>((w, v) => { w.Put(v.x); w.Put(v.y); }, 
            r => new Vector2(r.GetFloat(), r.GetFloat()));
        Register<Vector3>((w, v) => { w.Put(v.x); w.Put(v.y); w.Put(v.z); }, 
            r => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat()));
        Register<Quaternion>((w, v) => { w.Put(v.x); w.Put(v.y); w.Put(v.z); w.Put(v.w); }, 
            r => new Quaternion(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat()));
        Register<Color>((w, v) => { w.Put(v.r); w.Put(v.g); w.Put(v.b); w.Put(v.a); }, 
            r => new Color(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat()));
    }

    public static void Register<T>(Action<NetDataWriter, T> writer, Func<NetDataReader, T> reader)
    {
        _writers[typeof(T)] = (w, v) => writer(w, (T)v);
        _readers[typeof(T)] = r => reader(r);
    }

    public static void Serialize(NetDataWriter writer, Type type, object value)
    {
        if (_writers.TryGetValue(type, out var writeAction))
        {
            writeAction(writer, value);
        }
        else if (type.IsEnum)
        {
            writer.Put(Convert.ToInt32(value));
        }
        else if (type.IsArray)
        {
            SerializeArray(writer, (Array)value);
        }
        else if (typeof(IDuckovSerializable).IsAssignableFrom(type))
        {
            ((IDuckovSerializable)value).Serialize(writer);
        }
        else
        {
            throw new NotSupportedException($"Type {type.Name} not supported for serialization");
        }
    }

    public static object Deserialize(NetDataReader reader, Type type)
    {
        if (_readers.TryGetValue(type, out var readFunc))
        {
            return readFunc(reader);
        }
        else if (type.IsEnum)
        {
            return Enum.ToObject(type, reader.GetInt());
        }
        else if (type.IsArray)
        {
            return DeserializeArray(reader, type);
        }
        else if (typeof(IDuckovSerializable).IsAssignableFrom(type))
        {
            var instance = (IDuckovSerializable)Activator.CreateInstance(type);
            instance.Deserialize(reader);
            return instance;
        }
        else
        {
            throw new NotSupportedException($"Type {type.Name} not supported for deserialization");
        }
    }

    private static void SerializeArray(NetDataWriter writer, Array array)
    {
        writer.Put(array.Length);
        var elementType = array.GetType().GetElementType();
        foreach (var item in array)
        {
            Serialize(writer, elementType, item);
        }
    }

    private static Array DeserializeArray(NetDataReader reader, Type arrayType)
    {
        var length = reader.GetInt();
        var elementType = arrayType.GetElementType();
        var array = Array.CreateInstance(elementType, length);
        for (int i = 0; i < length; i++)
        {
            array.SetValue(Deserialize(reader, elementType), i);
        }
        return array;
    }
}

public interface IDuckovSerializable
{
    void Serialize(NetDataWriter writer);
    void Deserialize(NetDataReader reader);
}
