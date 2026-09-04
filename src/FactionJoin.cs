using System;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// Host-side faction join for vanilla / MiG-15S-only clients.
    /// Other players must not need Oritasy: Oritasy only clears HQ while the
    /// HOST join menu is open, so late joiners fail ValidateFactionChange after
    /// the host has already picked a side.
    /// Does not touch Steam, chat, or lobby create.
    /// </summary>
    internal static class FactionJoin
    {
        private static readonly MethodInfo HqSetter =
            AccessTools.PropertySetter(typeof(Player), "HQ");
        private static readonly MethodInfo GetAuthData =
            AccessTools.Method(typeof(BasePlayer), "GetAuthData");

        internal static bool Grounded(Player player)
        {
            if (player == null)
                return false;
            try
            {
                if (player.Aircraft != null)
                    return false;
            }
            catch { }
            return true;
        }

        internal static void ClearHqForPick(Player player, FactionHQ newHQ)
        {
            if (player == null || newHQ == null)
                return;
            if (!Grounded(player))
                return;
            FactionHQ cur = null;
            try { cur = player.HQ; }
            catch { }
            if (cur == null || object.ReferenceEquals(cur, newHQ))
                return;
            try { cur.RemovePlayer(player); }
            catch { }
            try
            {
                if (HqSetter != null)
                    HqSetter.Invoke(player, new object[] { null });
            }
            catch { }
            ClearSavedFaction(player);
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("FactionJoin: cleared HQ so a grounded player can pick a side.");
        }

        internal static void ClearSavedFaction(Player player)
        {
            if (GetAuthData == null || player == null)
                return;
            try
            {
                object auth = GetAuthData.Invoke(player, null);
                if (auth == null)
                    return;
                FieldInfo saveField = AccessTools.Field(auth.GetType(), "SaveData");
                if (saveField == null)
                    saveField = AccessTools.Field(auth.GetType(), "saveData");
                if (saveField == null)
                    return;
                SavedPlayerData data = saveField.GetValue(auth) as SavedPlayerData;
                if (data != null)
                    data.Faction = null;
            }
            catch { }
        }

        internal static bool FreshJoin(Player player)
        {
            if (GetAuthData == null || player == null)
                return true;
            try
            {
                object auth = GetAuthData.Invoke(player, null);
                if (auth == null)
                    return true;
                FieldInfo saveField = AccessTools.Field(auth.GetType(), "SaveData");
                if (saveField == null)
                    return true;
                SavedPlayerData data = saveField.GetValue(auth) as SavedPlayerData;
                if (data == null)
                    return true;
                if (data.Rejoined && data.Faction != null)
                    return false;
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class Patch_MiG15S_ValidateFaction
    {
        private static MethodBase TargetMethod()
        {
            MethodInfo two = AccessTools.Method(typeof(Player), "ValidateFactionChange",
                new Type[] { typeof(FactionHQ), typeof(bool) });
            if (two != null)
                return two;
            return AccessTools.Method(typeof(Player), "ValidateFactionChange",
                new Type[] { typeof(FactionHQ) });
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, FactionHQ newHQ)
        {
            FactionJoin.ClearHqForPick(__instance, newHQ);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Player __instance, FactionHQ newHQ, ref bool __result)
        {
            if (__result)
                return;
            if (!FactionJoin.Grounded(__instance) || newHQ == null)
                return;
            try
            {
                if (MissionManager.CurrentMission == null)
                    return;
            }
            catch { return; }
            try
            {
                if (newHQ.preventJoin)
                    return;
            }
            catch { return; }
            FactionHQ cur = null;
            try { cur = __instance.HQ; }
            catch { }
            if (cur != null && !object.ReferenceEquals(cur, newHQ))
                FactionJoin.ClearHqForPick(__instance, newHQ);
            try { cur = __instance.HQ; }
            catch { cur = null; }
            if (cur != null && !object.ReferenceEquals(cur, newHQ))
                return;
            __result = true;
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("FactionJoin: allowed grounded faction pick.");
        }
    }

    [HarmonyPatch(typeof(Player), "OnStartServer")]
    internal static class Patch_MiG15S_NoAutoFaction
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Player __instance)
        {
            if (FactionJoin.FreshJoin(__instance))
            {
                FactionJoin.ClearSavedFaction(__instance);
                return false;
            }
            return true;
        }
    }
}
