using BepInEx;
using BepInEx.Configuration;
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
            NoPVP = Config.Bind("General", "NoPVP", false, "Disable PVP completely when you hosting");
            DisablePvpInShip_StandaloneServerReceiveRpcs.Instance = new DisablePvpInShip_StandaloneServerReceiveRpcs();
            harmony.PatchAll();
            Logger.LogInfo("DisablePvpInShip Standalone is loaded!");
        }

        public const string PLUGIN_GUID = "DisablePvpInShip_Standalone";
        public const string PLUGIN_NAME = "DisablePvpInShip_Standalone";
        public const string PLUGIN_VERSION = "1.0.2";
        public const string PLUGIN_VERSION_FULL = PLUGIN_VERSION + ".0";

        Harmony harmony = new Harmony(PLUGIN_GUID);

        public static ManualLogSource logger;
        public static ConfigEntry<bool> NoPVP;
        public static DisablePvpInShip_StandalonePlugin Instance;
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
            if (DisablePvpInShip_StandalonePlugin.NoPVP.Value)
            {
                DisablePvpInShip_StandalonePlugin.logger.LogInfo($"Player #{senderPlayerId} ({username}) tried to damage ({instance.playerUsername}) for ({damageAmount}) damage.");
                return;
            }
            if (StartOfRound.Instance.shipInnerRoomBounds != null &&
                StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(instance.transform.position))
            {
                DisablePvpInShip_StandalonePlugin.logger.LogInfo($"Player #{senderPlayerId} ({username}) tried to damage ({instance.playerUsername}) inside the ship.");
                return;
            }
            /*
            if (damageAmount > 40)
            {
                DisablePvpInShip_StandalonePlugin.logger.LogInfo($"Player #{senderPlayerId} ({username}) tried to damage ({instance.playerUsername}) for excessive damage ({damageAmount})");
                damageAmount = 40;
            }
            */
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

        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.SetSingleton))]
        private static void RegisterPrefab()
        {
            if (!DisablePvpInShip_StandalonePlugin.NoPVP.Value)
                return;
            var prefab = new GameObject(DisablePvpInShip_StandalonePlugin.PLUGIN_NAME + " Prefab");
            prefab.hideFlags |= HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(prefab);
            var networkObject = prefab.AddComponent<NetworkObject>();
            try
            {
                var field = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    uint hash = (uint)DisablePvpInShip_StandalonePlugin.PLUGIN_GUID.GetHashCode();
                    field.SetValue(networkObject, hash);
                }
            }
            catch { }
            NetworkManager.Singleton.PrefabHandler.AddNetworkPrefab(prefab);
        }
        */
    }
}
