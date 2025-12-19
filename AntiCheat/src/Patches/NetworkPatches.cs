using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using GorillaNetworking;

namespace GorillaAntiCheat.Patches
{
    /*
    /// <summary>
    /// Patches for network-level protection including anti-kick and stealth
    /// </summary>
    [HarmonyPatch]
    public static class NetworkProtectionPatches
    {
        /// <summary>
        /// Block kick attempts from NetworkSystemPUN
        /// </summary>
        [HarmonyPatch(typeof(NetworkSystemPUN), "OnEvent")]
        [HarmonyPrefix]
        public static bool OnEventPrefix(EventData photonEvent)
        {
            if (!AntiCheatPlugin.ProtectionEnabled)
                return true;
            
            // Event codes that might be used for kicks/disconnects
            byte[] suspiciousEvents = { 199, 254, 255 };
            
            if (AntiCheatPlugin.AntiKick && Array.IndexOf(suspiciousEvents, photonEvent.Code) >= 0)
            {
                // Check if this event is targeting us
                try
                {
                    if (photonEvent.Sender != PhotonNetwork.LocalPlayer?.ActorNumber)
                    {
                        AntiCheatPlugin.BlockedKickAttempts++;
                        AntiCheatPlugin.AddLog($"[BLOCK] Suspicious event {photonEvent.Code} from player {photonEvent.Sender}");
                        return false;
                    }
                }
                catch { }
            }
            
            // Block report events
            if (AntiCheatPlugin.BlockNetworkEvents && (photonEvent.Code == 8 || photonEvent.Code == 50))
            {
                try
                {
                    if (photonEvent.CustomData is object[] data && data.Length > 3)
                    {
                        string targetId = data[3]?.ToString() ?? "";
                        string localId = PhotonNetwork.LocalPlayer?.UserId ?? "";
                        
                        if (targetId == localId)
                        {
                            AntiCheatPlugin.BlockedNetEvents++;
                            return false;
                        }
                    }
                }
                catch { }
            }
            
            return true;
        }
    }
    */

    /*
    /// <summary>
    /// Patches for PhotonNetworkController to prevent disconnections and kicks
    /// </summary>
    [HarmonyPatch(typeof(PhotonNetworkController))]
    public static class PhotonControllerPatches
    {
        /// <summary>
        /// Block any suspicious sends that target us
        /// </summary>
        [HarmonyPatch("OnEvent")]
        [HarmonyPrefix]
        public static bool OnEventPrefix(EventData photonEvent)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.AntiKick)
                return true;
            
            try
            {
                // Block master client override attempts
                if (photonEvent.Code == 254)  // Room event
                {
                    AntiCheatPlugin.BlockedKickAttempts++;
                    AntiCheatPlugin.AddLog("[BLOCK] Master client override blocked");
                    return false;
                }
            }
            catch { }
            
            return true;
        }
    }
    */

    /// <summary>
    /// Patches for VRRig to prevent detection through rig state
    /// </summary>
    [HarmonyPatch(typeof(VRRig))]
    public static class VRRigPatches
    {
        /// <summary>
        /// Spoof our player state to look normal
        /// </summary>
        [HarmonyPatch("OnDisable")]
        [HarmonyPrefix]
        public static bool OnDisablePrefix(VRRig __instance)
        {
            // Don't let our rig report errors on disable
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.StealthMode)
                return true;
            
            try
            {
                if (__instance.isOfflineVRRig)
                {
                    return true;  // Always allow offline rig disable
                }
            }
            catch { }
            
            return true;
        }
    }

    /// <summary>
    /// Patches for GorillaTagManager to prevent tag-related detections
    /// </summary>
    [HarmonyPatch(typeof(GorillaTagManager))]
    public static class TagManagerPatches
    {
        /// <summary>
        /// Block suspicious tag reports about us
        /// </summary>
        [HarmonyPatch("ReportTag")]
        [HarmonyPrefix]
        public static bool ReportTagPrefix(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.SpoofPlayerData)
                return true;
            
            try
            {
                // Allow normal tags, but track for suspicious activity
                string localId = PhotonNetwork.LocalPlayer?.UserId ?? "";
                
                if (taggingPlayer?.UserId == localId || taggedPlayer?.UserId == localId)
                {
                    // Log but allow - tagging is normal gameplay
                    // Just make sure our data looks legitimate
                }
            }
            catch { }
            
            return true;
        }
    }

    /// <summary>
    /// Patches for NetworkSystem base class
    /// </summary>
    [HarmonyPatch(typeof(NetworkSystem))]
    public static class NetworkSystemPatches
    {
        /// <summary>
        /// Block the OnRaiseEvent that could flag us
        /// </summary>
        [HarmonyPatch("RaiseEvent")]
        [HarmonyPrefix]
        public static bool RaiseEventPrefix(byte eventCode, object data, int source)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.BlockNetworkEvents)
                return true;
            
            // Event codes 8 and 50 are report events
            if (eventCode == 8 || eventCode == 50)
            {
                try
                {
                    if (data is object[] dataArray && dataArray.Length > 3)
                    {
                        string targetId = dataArray[3]?.ToString() ?? "";
                        string localId = PhotonNetwork.LocalPlayer?.UserId ?? "";
                        
                        if (targetId == localId)
                        {
                            AntiCheatPlugin.BlockedNetEvents++;
                            AntiCheatPlugin.AddLog($"[BLOCK] NetworkSystem event {eventCode} blocked");
                            return false;
                        }
                    }
                }
                catch { }
            }
            
            return true;
        }
    }

    /// <summary>
    /// Patches for NetworkSystemPUN implementation
    /// </summary>
    [HarmonyPatch(typeof(NetworkSystemPUN))]
    public static class NetworkSystemPUNPatches
    {
        /// <summary>
        /// Block forced disconnects
        /// </summary>
        [HarmonyPatch("ReturnToSinglePlayer")]
        [HarmonyPrefix]
        public static bool ReturnToSinglePlayerPrefix()
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.AntiKick)
                return true;
            
            // Check if this was triggered by anti-cheat
            try
            {
                // Check the call stack to see if GorillaNot triggered this
                var stackTrace = new System.Diagnostics.StackTrace();
                foreach (var frame in stackTrace.GetFrames())
                {
                    string methodName = frame.GetMethod()?.Name ?? "";
                    string typeName = frame.GetMethod()?.DeclaringType?.Name ?? "";
                    
                    if (typeName == "GorillaNot" || methodName.Contains("QuitDelay"))
                    {
                        AntiCheatPlugin.BlockedKickAttempts++;
                        AntiCheatPlugin.AddLog("[BLOCK] Forced disconnect blocked (anti-cheat triggered)");
                        return false;
                    }
                }
            }
            catch { }
            
            return true;
        }
    }

    /// <summary>
    /// Patches for GorillaComputer to handle ban messages
    /// </summary>
    [HarmonyPatch(typeof(GorillaComputer))]
    public static class ComputerPatches
    {
        /// <summary>
        /// Don't let general failure messages disconnect us
        /// </summary>
        [HarmonyPatch("GeneralFailureMessage")]
        [HarmonyPrefix]
        public static bool GeneralFailureMessagePrefix(string failMessage)
        {
            if (!AntiCheatPlugin.ProtectionEnabled)
                return true;
            
            // Log what's happening
            AntiCheatPlugin.AddLog($"[INFO] General failure: {failMessage}");
            
            // Don't block BANNED messages - those need special handling
            if (failMessage.Contains("BANNED"))
            {
                return true;
            }
            
            return true;
        }
    }

    /// <summary>
    /// Patches for Room callbacks to prevent room-based kicks
    /// </summary>
    [HarmonyPatch]
    public static class RoomCallbackPatches
    {
        /// <summary>
        /// Prevent closing/hiding the room due to anti-cheat
        /// </summary>
        [HarmonyPatch(typeof(Room), "IsOpen", MethodType.Setter)]
        [HarmonyPrefix]
        public static bool IsOpenSetterPrefix(Room __instance, ref bool value)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.AntiKick)
                return true;
            
            // Check if GorillaNot is trying to close the room
            try
            {
                var stackTrace = new System.Diagnostics.StackTrace();
                foreach (var frame in stackTrace.GetFrames())
                {
                    string typeName = frame.GetMethod()?.DeclaringType?.Name ?? "";
                    
                    if (typeName == "GorillaNot")
                    {
                        // Block room closure from anti-cheat
                        AntiCheatPlugin.AddLog("[BLOCK] Room closure blocked");
                        return false;
                    }
                }
            }
            catch { }
            
            return true;
        }

        /// <summary>
        /// Prevent making room invisible due to anti-cheat
        /// </summary>
        [HarmonyPatch(typeof(Room), "IsVisible", MethodType.Setter)]
        [HarmonyPrefix]
        public static bool IsVisibleSetterPrefix(Room __instance, ref bool value)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.AntiKick)
                return true;
            
            // Check if GorillaNot is trying to hide the room
            try
            {
                var stackTrace = new System.Diagnostics.StackTrace();
                foreach (var frame in stackTrace.GetFrames())
                {
                    string typeName = frame.GetMethod()?.DeclaringType?.Name ?? "";
                    
                    if (typeName == "GorillaNot")
                    {
                        // Block room hiding from anti-cheat
                        AntiCheatPlugin.AddLog("[BLOCK] Room visibility change blocked");
                        return false;
                    }
                }
            }
            catch { }
            
            return true;
        }
    }
}
