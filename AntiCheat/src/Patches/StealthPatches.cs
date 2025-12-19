using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;

namespace GorillaAntiCheat.Patches
{
    /// <summary>
    /// Stealth patches to hide mod presence and prevent detection signatures
    /// </summary>
    [HarmonyPatch]
    public static class StealthPatches
    {
        // Cached values for spoofing
        private static Dictionary<string, object> cachedPlayerData = new Dictionary<string, object>();

        /// <summary>
        /// Prevent Unity debug logging from revealing mod activity
        /// </summary>
        [HarmonyPatch(typeof(Debug), "Log", new Type[] { typeof(object) })]
        [HarmonyPrefix]
        public static bool DebugLogPrefix(object message)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.StealthMode)
                return true;
            
            try
            {
                string msg = message?.ToString() ?? "";
                
                // Block logs that might reveal mod activity
                if (msg.Contains("BepInEx") || 
                    msg.Contains("Harmony") || 
                    msg.Contains("Patch") ||
                    msg.Contains("AntiCheat") ||
                    msg.Contains("[MOD]") ||
                    msg.Contains("WalkSim") ||
                    msg.Contains("RedLobb"))
                {
                    return false;  // Suppress the log
                }
            }
            catch { }
            
            return true;
        }

        /// <summary>
        /// Prevent error logs that could indicate mod issues
        /// </summary>
        [HarmonyPatch(typeof(Debug), "LogError", new Type[] { typeof(object) })]
        [HarmonyPrefix]
        public static bool DebugLogErrorPrefix(object message)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.StealthMode)
                return true;
            
            try
            {
                string msg = message?.ToString() ?? "";
                
                // Block mod-related error logs
                if (msg.Contains("BepInEx") || 
                    msg.Contains("Harmony") || 
                    msg.Contains("Patch") ||
                    msg.Contains("NullReference") ||  // Common mod errors
                    msg.Contains("MissingMethod"))
                {
                    return false;
                }
            }
            catch { }
            
            return true;
        }
    }

    /// <summary>
    /// Patches to spoof player data to look legitimate
    /// </summary>
    public static class PlayerDataSpoofing
    {
        // Legitimate-looking data ranges
        private static readonly float[] NormalHeadHeights = { 1.4f, 1.5f, 1.6f, 1.7f, 1.8f };
        private static readonly float[] NormalHandDistances = { 0.3f, 0.4f, 0.5f, 0.6f };
        
        /// <summary>
        /// Get a spoofed value that looks normal
        /// </summary>
        public static float GetSpoofedFloat(string key, float original, float min, float max)
        {
            // Return a value within normal range
            if (original < min) return min;
            if (original > max) return max;
            return original;
        }

        /// <summary>
        /// Normalize a velocity to look legitimate
        /// </summary>
        public static Vector3 NormalizeVelocity(Vector3 velocity, float maxSpeed)
        {
            if (velocity.magnitude > maxSpeed)
            {
                return velocity.normalized * maxSpeed;
            }
            return velocity;
        }
    }

    /*
    /// <summary>
    /// Patches for GorillaTagger to prevent detection through player state
    /// </summary>
    [HarmonyPatch(typeof(GorillaTagger))]
    public static class TaggerStealthPatches
    {
        /// <summary>
        /// Ensure our tagging looks legitimate
        /// </summary>
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(GorillaTagger __instance)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.SpoofPlayerData)
                return;
            
            // No modifications needed currently
            // This is a hook point for future state normalization
        }
    }
    */

    /*
    /// <summary>
    /// Patches for Application to prevent crash reports and telemetry
    /// </summary>
    [HarmonyPatch(typeof(Application))]
    public static class ApplicationPatches
    {
        /// <summary>
        /// Block log message callbacks that might report mod activity
        /// </summary>
        [HarmonyPatch("logMessageReceived", MethodType.Getter)]
        [HarmonyPostfix]
        public static void LogMessageReceivedPostfix()
        {
            // Note: This is informational - actual blocking happens in Debug patches
        }
    }
    */

    /// <summary>
    /// Time-based detection prevention
    /// </summary>
    public static class TimeProtection
    {
        private static float lastValidTime = 0f;
        private static float timeOffset = 0f;

        /// <summary>
        /// Get a normalized time value that won't trigger time-based detection
        /// </summary>
        public static float GetNormalizedTime()
        {
            float realTime = Time.realtimeSinceStartup;
            
            // Ensure time progresses normally (no time manipulation detection)
            if (lastValidTime > 0 && realTime < lastValidTime)
            {
                // Time went backwards - adjust offset
                timeOffset += lastValidTime - realTime;
            }
            
            lastValidTime = realTime;
            return realTime + timeOffset;
        }
    }

    /// <summary>
    /// Memory protection to prevent memory scanning
    /// </summary>
    public static class MemoryProtection
    {
        // Obfuscated storage for sensitive values
        private static Dictionary<int, byte[]> protectedValues = new Dictionary<int, byte[]>();
        private static byte obfuscationKey = 0xAB;

        /// <summary>
        /// Store a value in protected memory
        /// </summary>
        public static void StoreProtected(int key, string value)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] protected_data = new byte[data.Length];
            
            for (int i = 0; i < data.Length; i++)
            {
                protected_data[i] = (byte)(data[i] ^ obfuscationKey ^ (i % 256));
            }
            
            protectedValues[key] = protected_data;
        }

        /// <summary>
        /// Retrieve a protected value
        /// </summary>
        public static string RetrieveProtected(int key)
        {
            if (!protectedValues.ContainsKey(key))
                return "";
            
            byte[] protected_data = protectedValues[key];
            byte[] data = new byte[protected_data.Length];
            
            for (int i = 0; i < protected_data.Length; i++)
            {
                data[i] = (byte)(protected_data[i] ^ obfuscationKey ^ (i % 256));
            }
            
            return System.Text.Encoding.UTF8.GetString(data);
        }
    }

    /*
    /// <summary>
    /// Assembly protection to hide loaded mods
    /// </summary>
    [HarmonyPatch]
    public static class AssemblyProtection
    {
        // List of assemblies to hide
        private static readonly HashSet<string> HiddenAssemblies = new HashSet<string>
        {
            "GorillaAntiCheat",
            "WalkSimModern",
            "RedLobbys",
            "SoundBoard",
            "0Harmony"
        };

        /// <summary>
        /// Attempt to hide assemblies from reflection
        /// Note: This has limited effectiveness but adds a layer of protection
        /// </summary>
        [HarmonyPatch(typeof(AppDomain), "GetAssemblies")]
        [HarmonyPostfix]
        public static void GetAssembliesPostfix(ref Assembly[] __result)
        {
            if (!AntiCheatPlugin.ProtectionEnabled || !AntiCheatPlugin.StealthMode)
                return;
            
            try
            {
                // Filter out hidden assemblies
                var filteredList = new List<Assembly>();
                foreach (var assembly in __result)
                {
                    string name = assembly.GetName().Name;
                    bool shouldHide = false;
                    
                    foreach (var hidden in HiddenAssemblies)
                    {
                        if (name.Contains(hidden))
                        {
                            shouldHide = true;
                            break;
                        }
                    }
                    
                    if (!shouldHide)
                    {
                        filteredList.Add(assembly);
                    }
                }
                
                __result = filteredList.ToArray();
            }
            catch { }
        }
    }
    */
}
