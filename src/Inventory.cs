using System;
using System.Collections.Generic;
using HarmonyLib;
using NuclearOption;
using NuclearOption.Networking;

namespace OA27Variant
{
    /// <summary>
    /// OA-27C shares the donor prefab. Oritasy SameAirframe treats that as one
    /// airframe, so buying the clone also increments OA-27 owned/faction counts.
    /// Count and consume by jsonKey only.
    /// </summary>
    internal static class InventorySplit
    {
        internal static AircraftDefinition _flyWant;

        internal static bool IsTracked(AircraftDefinition def)
        {
            return Service.IsOaFamilyDef(def) || Service.IsDonorDef(def);
        }

        internal static bool SameKey(AircraftDefinition a, AircraftDefinition b)
        {
            if (a == null || b == null)
                return false;
            if (object.ReferenceEquals(a, b))
                return true;
            string ka = a.jsonKey;
            string kb = b.jsonKey;
            if (string.IsNullOrEmpty(ka) || string.IsNullOrEmpty(kb))
                return false;
            return string.Equals(ka, kb, StringComparison.OrdinalIgnoreCase);
        }

        internal static int CountOwned(Player player, AircraftDefinition def, bool includeReserved)
        {
            int n = 0;
            if (player == null || def == null)
                return 0;
            try
            {
                foreach (OwnedAirframe owned in player.OwnedAirframes)
                {
                    if (!SameKey(owned.Definition, def))
                        continue;
                    if (includeReserved || !owned.Reserved)
                        n++;
                }
            }
            catch { }
            return n;
        }
    }

    [HarmonyPatch(typeof(Player), "OwnedAirframeTypeCount")]
    internal static class Patch_OA27C_OwnedCount
    {
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(
            Player __instance,
            AircraftDefinition aircraftDef,
            bool includeReserved,
            ref int __result)
        {
            if (!InventorySplit.IsTracked(aircraftDef))
                return;
            __result = InventorySplit.CountOwned(__instance, aircraftDef, includeReserved);
        }
    }

    [HarmonyPatch(typeof(Player), "OwnsAirframe")]
    internal static class Patch_OA27C_Owns
    {
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(
            Player __instance,
            AircraftDefinition aircraftDef,
            bool includeReserved,
            ref bool __result)
        {
            if (!InventorySplit.IsTracked(aircraftDef))
                return;
            __result = InventorySplit.CountOwned(__instance, aircraftDef, includeReserved) > 0;
        }
    }

    [HarmonyPatch(typeof(Player), "PossessesReservedAirframe", new Type[] { typeof(AircraftDefinition) })]
    internal static class Patch_OA27C_Reserved
    {
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(
            Player __instance,
            AircraftDefinition aircraftDef,
            ref bool __result)
        {
            if (!InventorySplit.IsTracked(aircraftDef) || __instance == null)
                return;
            try
            {
                foreach (OwnedAirframe owned in __instance.OwnedAirframes)
                {
                    if (owned.Reserved && InventorySplit.SameKey(owned.Definition, aircraftDef))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            catch { }
            __result = false;
        }
    }

    [HarmonyPatch(typeof(Player), "FlyOwnedAirframe")]
    internal static class Patch_OA27C_FlyCapture
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(AircraftDefinition airframe)
        {
            InventorySplit._flyWant = airframe;
        }
    }

    [HarmonyPatch(typeof(Player), "FlyOwnedAirframe")]
    internal static class Patch_OA27C_FlyKeepKey
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref AircraftDefinition airframe)
        {
            AircraftDefinition want = InventorySplit._flyWant;
            InventorySplit._flyWant = null;
            if (want == null || !InventorySplit.IsTracked(want))
                return;
            if (airframe == null || !InventorySplit.SameKey(want, airframe))
                airframe = want;
        }
    }

    [HarmonyPatch(typeof(FactionHQ), "GetUnitSupply")]
    internal static class Patch_OA27C_HqSupply
    {
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(FactionHQ __instance, UnitDefinition unitDefinition, ref int __result)
        {
            AircraftDefinition def = unitDefinition as AircraftDefinition;
            if (!InventorySplit.IsTracked(def) || __instance == null)
                return;
            int n = 0;
            try
            {
                foreach (KeyValuePair<AircraftDefinition, FactionHQ.RuntimeSupply> kv in __instance.AircraftSupply)
                {
                    if (InventorySplit.SameKey(kv.Key, def))
                        n += kv.Value.Count;
                }
            }
            catch
            {
                return;
            }
            __result = n;
        }
    }

    [HarmonyPatch(typeof(Player), "CreditAirframe")]
    internal static class Patch_OA27C_Credit
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(AircraftDefinition aircraftDef)
        {
            HangarInject.RegisterNetwork(aircraftDef);
        }
    }

    [HarmonyPatch(typeof(DefinitionWriters), "WriteAircraftDefinition")]
    internal static class Patch_OA27C_WriteDef
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(AircraftDefinition definition)
        {
            HangarInject.RegisterNetwork(definition);
        }
    }
}
