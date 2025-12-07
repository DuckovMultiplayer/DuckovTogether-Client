















using UnityEngine;
using EscapeFromDuckovCoopMod.Net;

namespace EscapeFromDuckovCoopMod;




public static class SceneNetVoteHelper
{
    
    
    
    public static void Host_StartJsonVote(string targetSceneId, string curtainGuid,
        bool notifyEvac, bool saveToFile, bool useLocation, string locationName)
    {
        var sceneNet = SceneNet.Instance;
        if (sceneNet == null)
        {
            Debug.LogWarning("[SceneVote] SceneNet.Instance 为空");
            return;
        }

        
        sceneNet.sceneTargetId = targetSceneId ?? "";
        sceneNet.sceneCurtainGuid = string.IsNullOrEmpty(curtainGuid) ? null : curtainGuid;
        sceneNet.sceneNotifyEvac = notifyEvac;
        sceneNet.sceneSaveToFile = saveToFile;
        sceneNet.sceneUseLocation = useLocation;
        sceneNet.sceneLocationName = locationName ?? "";

        
        sceneNet._srvSceneGateOpen = false;
        sceneNet._srvGateReadyPids.Clear();
        Debug.Log("[GATE] 投票开始，重置场景门控状态");

        
        SceneVoteMessage.Host_StartVote(targetSceneId, curtainGuid, notifyEvac, saveToFile, useLocation, locationName);
        
        Debug.Log($"[SCENE] 投票开始 (JSON): target='{targetSceneId}', loc='{locationName}'");
    }

    
    
    
    public static void Client_RequestJsonVote(string targetId, string curtainGuid,
        bool notifyEvac, bool saveToFile, bool useLocation, string locationName)
    {
        SceneVoteMessage.Client_RequestVote(targetId, curtainGuid, notifyEvac, saveToFile, useLocation, locationName);
    }
}
