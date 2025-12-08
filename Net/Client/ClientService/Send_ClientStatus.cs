namespace EscapeFromDuckovCoopMod;

public class Send_ClientStatus : MonoBehaviour
{
    public static Send_ClientStatus Instance { get; private set; }
    private ModBehaviourF Service => ModBehaviourF.Instance;
    
    public void Init()
    {
        Instance = this;
    }
    
    public void SendClientStatusUpdate()
    {
        if (Service == null || Service.connectedPeer == null || Service.IsServer)
            return;

        Net.ClientStatusMessage.Client_SendStatusUpdate();
    }
}
