















namespace EscapeFromDuckovCoopMod;

public static class PackFlag
{
    
    public static byte PackFlags(bool hasCurtain, bool useLoc, bool notifyEvac, bool saveToFile)
    {
        byte f = 0;
        if (hasCurtain) f |= 1 << 0;
        if (useLoc) f |= 1 << 1;
        if (notifyEvac) f |= 1 << 2;
        if (saveToFile) f |= 1 << 3;
        return f;
    }

    public static void UnpackFlags(byte f, out bool hasCurtain, out bool useLoc, out bool notifyEvac, out bool saveToFile)
    {
        hasCurtain = (f & (1 << 0)) != 0;
        useLoc = (f & (1 << 1)) != 0;
        notifyEvac = (f & (1 << 2)) != 0;
        saveToFile = (f & (1 << 3)) != 0;
    }
}