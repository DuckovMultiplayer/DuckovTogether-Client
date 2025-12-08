namespace EscapeFromDuckovCoopMod;

public class EquipmentSyncData
{
    public string ItemId;
    public int SlotHash;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SlotHash);
        writer.Put(ItemId ?? "");
    }

    public static EquipmentSyncData Deserialize(NetDataReader reader)
    {
        return new EquipmentSyncData
        {
            SlotHash = reader.GetInt(),
            ItemId = reader.GetString()
        };
    }
}

public class WeaponSyncData
{
    public string ItemId;
    public int SlotHash;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(SlotHash);
        writer.Put(ItemId ?? "");
    }

    public static WeaponSyncData Deserialize(NetDataReader reader)
    {
        return new WeaponSyncData
        {
            SlotHash = reader.GetInt(),
            ItemId = reader.GetString()
        };
    }
}

public static class SyncDataManger
{
}