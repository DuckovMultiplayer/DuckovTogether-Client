















using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod;




public class MModUIComponents
{
    
    public GameObject MainPanel;
    public GameObject PlayerStatusPanel;
    public GameObject VoiceSettingsPanel;
    public Transform ActiveSpeakersContent;
    public GameObject VotePanel;
    public GameObject SpectatorPanel;

    
    public TMP_InputField IpInputField;
    public TMP_InputField PortInputField;
    public TMP_InputField JsonInputField;  

    
    public TMP_Text StatusText;
    public TMP_Text SteamStatusText;  
    public TMP_Text ServerPortText;
    public TMP_Text ConnectionCountText;
    public TMP_Text ModeToggleButtonText;
    public TMP_Text ModeInfoText;
    public TMP_Text ModeText;
    public TMP_Text SteamMaxPlayersText;

    
    public Image ModeIndicator;

    
    public Transform HostListContent;
    public Transform PlayerListContent;
    public Transform SteamLobbyListContent;

    
    public Button ModeToggleButton;
    public Button SteamCreateLeaveButton;
    public TMP_Text SteamCreateLeaveButtonText;

    
    public GameObject DirectModePanel;
    public GameObject SteamModePanel;

    
    public GameObject DirectServerListArea;
    public GameObject SteamServerListArea;
    
    
    public GameObject OnlineLobbyPanel;
    public Transform OnlineRoomListContent;
    public GameObject NodeSelectorPanel;
    public Transform NodeListContent;
    public TMP_Text SelectedNodeText;
    public TMP_Text OnlineRoomCountText;
}

