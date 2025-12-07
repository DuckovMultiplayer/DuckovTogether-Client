using Duckov.UI;
using EscapeFromDuckovCoopMod.Utils;
using System.Reflection;
using TMPro;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod;

[HarmonyPatch(typeof(HealthBar), "Refresh")]
internal static class Patch_HealthBar_Refresh_ApplyPlayerColor
{
    private static readonly MethodInfo MI_GetActiveHealthBar = 
        AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
    
    private static readonly FieldInfo FI_Fill = 
        AccessTools.Field(typeof(HealthBar), "fill");
    
    private static readonly FieldInfo FI_NameText = 
        AccessTools.Field(typeof(HealthBar), "nameText");

    private static void Postfix(HealthBar __instance)
    {
        try
        {
            var health = __instance.target;
            if (!health) return;

            var cmc = health.TryGetCharacter();
            if (!cmc) return;

            var mod = ModBehaviourF.Instance;
            if (mod == null || !mod.networkStarted) return;

            var colorManager = PlayerColorManager.Instance;
            if (colorManager == null) return;

            string playerId = null;
            string steamName = null;
            bool isLocal = false;

            if (cmc == CharacterMainControl.Main)
            {
                isLocal = true;
                playerId = "local";
                if (SteamManager.Initialized)
                {
                    steamName = Steamworks.SteamFriends.GetPersonaName();
                }
            }
            else if (!mod.IsServer)
            {
                foreach (var kvp in mod.clientPlayerStatuses)
                {
                    if (mod.clientRemoteCharacters.TryGetValue(kvp.Key, out var remoteChar) && remoteChar == cmc.gameObject)
                    {
                        playerId = kvp.Key;
                        steamName = kvp.Value.PlayerName;
                        break;
                    }
                }
            }
            else
            {
                foreach (var kvp in mod.playerStatuses)
                {
                    if (mod.remoteCharacters.TryGetValue(kvp.Key, out var remoteChar) && remoteChar == cmc.gameObject)
                    {
                        playerId = kvp.Key.EndPoint.ToString();
                        steamName = kvp.Value.PlayerName;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(playerId)) return;

            var color = colorManager.GetOrAssignColor(playerId, isLocal);

            var fillImage = FI_Fill?.GetValue(__instance) as Image;
            if (fillImage != null)
            {
                fillImage.color = color;
            }

            if (!string.IsNullOrEmpty(steamName))
            {
                var nameText = FI_NameText?.GetValue(__instance) as TextMeshProUGUI;
                if (nameText != null)
                {
                    nameText.text = steamName;
                    nameText.gameObject.SetActive(true);
                }
            }
        }
        catch
        {
        }
    }
}
