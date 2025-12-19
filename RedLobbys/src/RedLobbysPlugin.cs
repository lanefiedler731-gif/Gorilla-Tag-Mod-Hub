using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;

namespace RedLobbys
{
    [BepInPlugin("com.lane.redlobbys", "RedLobbys", "1.0.0")]
    public class RedLobbysPlugin : BaseUnityPlugin
    {
        public static string RedLobbyAppId = "[redacted]";

        private void Start()
        {
            DontDestroyOnLoad(this);
            gameObject.AddComponent<RedLobbysNetworkController>();
            ApplyHarmonyPatches();
        }

        private void ApplyHarmonyPatches()
        {
            var harmony = new Harmony("com.lane.redlobbys");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public class RedLobbysNetworkController : MonoBehaviourPunCallbacks
    {
        public override void OnJoinedRoom()
        {
            if (PhotonNetwork.CurrentRoom != null)
            {
                Debug.Log($"RedLobbys: Joined room {PhotonNetwork.CurrentRoom.Name}");
                
                if (GorillaComputer.instance != null)
                {
                    GorillaComputer.instance.currentGameModeText.Value = "CURRENT MODE\nRED LOBBY";
                }
            }
        }

        public override void OnLeftRoom()
        {
            if (GorillaComputer.instance != null)
            {
                GorillaComputer.instance.currentGameModeText.Value = "CURRENT MODE\n-NOT IN ROOM-";
            }
        }
    }

    [HarmonyPatch(typeof(GorillaComputer), "GeneralFailureMessage")]
    internal class ComputerPatch
    {
        static bool Prefix(string failMessage)
        {
            if (failMessage.Contains("BANNED"))
            {
                Debug.Log("RedLobbys: Ban detected, switching to Red Lobby AppID");
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = RedLobbysPlugin.RedLobbyAppId;
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = RedLobbysPlugin.RedLobbyAppId;
                PhotonNetwork.ConnectUsingSettings();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PhotonNetworkController), "AttemptToJoinPublicRoom")]
    internal class CustomJoinPatch
    {
        static void Prefix(PhotonNetworkController __instance, out string[] __state)
        {
            __state = GorillaComputer.instance.allowedMapsToJoin;
            string[] array = new string[__state.Length + 1];
            GorillaComputer.instance.allowedMapsToJoin.CopyTo(array, 0);
            array[^1] = "RedLobby";
            GorillaComputer.instance.allowedMapsToJoin = array;
            
            __instance.currentGameType = "RedLobby";
        }

        static void Postfix(string[] __state)
        {
            GorillaComputer.instance.allowedMapsToJoin = __state;
        }
    }

    [HarmonyPatch(typeof(RoomOptions))]
    internal class RoomOptionsPatch
    {
        // Patch the RoomOptions creation to ensure IsVisible and IsOpen are true for Red Lobbies
        [HarmonyPatch(MethodType.Constructor, new Type[] { })]
        [HarmonyPostfix]
        static void Postfix(RoomOptions __instance)
        {
            // Only modify if we're connected to the Red Lobby AppID
            if (PhotonNetwork.PhotonServerSettings?.AppSettings?.AppIdRealtime == RedLobbysPlugin.RedLobbyAppId)
            {
                // Ensure rooms are visible and joinable ONLY if not already set
                if (!__instance.IsVisible)
                {
                    __instance.IsVisible = true;
                    Debug.Log("RedLobbys: Set IsVisible = true for Red Lobby room");
                }
                
                if (!__instance.IsOpen)
                {
                    __instance.IsOpen = true;
                    Debug.Log("RedLobbys: Set IsOpen = true for Red Lobby room");
                }
            }
        }
    }
}
