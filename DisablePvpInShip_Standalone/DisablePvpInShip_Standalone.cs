using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace DisablePvpInShip_Standalone
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class DisablePvpInShip_StandalonePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Instance = this;
            logger = base.Logger;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DisablePvpInShip_StandaloneServerReceiveRpcs.Instance = new DisablePvpInShip_StandaloneServerReceiveRpcs();
            harmony.PatchAll();
            Logger.LogInfo("DisablePvpInShip Standalone is loaded!");
        }

        public const string PLUGIN_GUID = "HostFixes.DisablePvpInShip_Standalone";
        public const string PLUGIN_NAME = "DisablePvpInShip_Standalone";
        public const string PLUGIN_VERSION = "1.0.0";
        public const string PLUGIN_VERSION_FULL = PLUGIN_VERSION + ".0";

        Harmony harmony = new Harmony(PLUGIN_GUID);

        public static DisablePvpInShip_StandalonePlugin Instance;
        public static ManualLogSource logger;
    }

    internal class DisablePvpInShip_StandaloneServerReceiveRpcs
    {
        public static DisablePvpInShip_StandaloneServerReceiveRpcs Instance;

        public void DamagePlayerFromOtherClientServerRpc(
            int damageAmount,
            Vector3 hitDirection,
            int playerWhoHit,
            PlayerControllerB instance,
            __RpcParams RpcParams)
        {
            ulong senderClientId = RpcParams.Server.Receive.SenderClientId;
            if (!StartOfRound.Instance.ClientPlayerList.TryGetValue(senderClientId, out int senderPlayerId))
            {
                DisablePvpInShip_StandalonePlugin.logger.LogError($"[DamagePlayerFromOtherClientServerRpc] Failed to get the playerId from senderClientId: {senderClientId}");
                return;
            }
            string username = StartOfRound.Instance.allPlayerScripts[senderPlayerId].playerUsername;

            if (StartOfRound.Instance.shipInnerRoomBounds != null &&
                StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(instance.transform.position))
            {
                DisablePvpInShip_StandalonePlugin.logger.LogInfo($"Player #{senderPlayerId} ({username}) tried to damage ({instance.playerUsername}) inside the ship.");
                return;
            }

            // DisablePvpInShip_StandalonePlugin.logger.LogInfo($"Player #{senderPlayerId} ({username}) damaged ({instance.playerUsername}) for ({damageAmount}) damage.");
            instance.DamagePlayerFromOtherClientServerRpc(damageAmount, hitDirection, playerWhoHit);
        }
    }

    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyPatch(typeof(PlayerControllerB), "__rpc_handler_638895557")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RedirectDamageRpc(IEnumerable<CodeInstruction> instructions)
        {
            var found = false;
            var callLocation = -1;
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo { Name: "DamagePlayerFromOtherClientServerRpc" })
                {
                    callLocation = i;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                codes.Insert(callLocation, new CodeInstruction(OpCodes.Ldarg_0));
                codes.Insert(callLocation + 1, new CodeInstruction(OpCodes.Ldarg_2));
                codes[callLocation + 2].operand = AccessTools.Method(typeof(DisablePvpInShip_StandaloneServerReceiveRpcs), "DamagePlayerFromOtherClientServerRpc");
            }
            else
            {
                DisablePvpInShip_StandalonePlugin.logger.LogError("Could not patch DamagePlayerFromOtherClientServerRpc");
            }

            return codes.AsEnumerable();
        }
    }
}
