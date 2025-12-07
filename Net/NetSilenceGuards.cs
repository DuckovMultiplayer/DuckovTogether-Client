















namespace EscapeFromDuckovCoopMod;

public static class NetSilenceGuards
{
    
    [ThreadStatic] public static bool InPickupItem; 
    [ThreadStatic] public static bool InCapacityShrinkCleanup; 
}