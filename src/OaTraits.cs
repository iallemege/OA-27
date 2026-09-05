using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// Airframe traits (not WSO): C RCS 0 + STOL/low-G, D fuel, E ECM + ground-gun immunity.
    /// </summary>
    internal static class OaTraits
    {
        private const float StolCeilM = 28f;
        private const float LowGCeilM = 80f;
        private const float EcmKeep = 6.5f;
        private static int GroundGunBlast;

        private static readonly FieldInfo EcmField =
            AccessTools.Field(typeof(Aircraft), "ecmIntensity");
        private static readonly FieldInfo FbwGLimit =
            AccessTools.Field(typeof(ControlsFilter.FlyByWire), "gLimitPositive");
        private static readonly FieldInfo FbwTakeoff =
            AccessTools.Field(typeof(ControlsFilter.FlyByWire), "takeoffSpeed");
        private static readonly FieldInfo FbwAlpha =
            AccessTools.Field(typeof(ControlsFilter.FlyByWire), "alphaLimiter");
        private static readonly FieldInfo FuelCapField =
            AccessTools.Field(typeof(FuelTank), "fuelCapacity");
        private static readonly HashSet<int> FuelDone = new HashSet<int>();
        private static readonly HashSet<int> FbwOnce = new HashSet<int>();
        private static readonly Dictionary<int, byte> GBand = new Dictionary<int, byte>(8);

        internal static void OnSpawn(Aircraft ac)
        {
            if (ac == null || !Service.IsOaFamilyClone(ac))
                return;
            if (Service.IsOaDClone(ac))
                BuffFuel(ac);
            KeepRcs(ac);
            KeepEcm(ac);
        }

        internal static void Tick(Aircraft ac)
        {
            if (ac == null || !Service.IsOaFamilyClone(ac))
                return;
            if (!Service.IsLiveAircraft(ac) || !Plugin.IsRuntime(ac))
                return;
            KeepRcs(ac);
            KeepEcm(ac);
            if (Service.IsOaClone(ac))
            {
                StolLift(ac);
                LowAltG(ac);
            }
        }

        internal static void KeepRcs(Aircraft ac)
        {
            if (ac == null || !Service.IsOaClone(ac))
                return;
            try { ac.RCS = 0f; }
            catch { }
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (def != null)
                def.radarSize = 0f;
        }

        internal static bool IsGroundGunDealer(Unit dealer)
        {
            if (dealer == null)
                return false;
            if (dealer is Aircraft)
                return false;
            if (dealer is Missile)
                return false;
            return true;
        }

        internal static bool HitIsOaE(Unit hit)
        {
            Aircraft ac = hit as Aircraft;
            return ac != null && Service.IsOaEClone(ac);
        }

        internal static bool BlockGroundGunOnE(Unit victim, PersistentID dealerID)
        {
            if (!HitIsOaE(victim))
                return false;
            return GroundGunBlast > 0;
        }

        internal static bool BlockGroundGunOnE(UnitPart part, PersistentID dealerID)
        {
            if (part == null)
                return false;
            Unit u = null;
            try { u = part.parentUnit; }
            catch { u = null; }
            if (u == null)
            {
                try { u = part.GetUnit(); }
                catch { u = null; }
            }
            return BlockGroundGunOnE(u, dealerID);
        }

        internal static bool BlockGroundGunOnE(Turbofan fan, PersistentID dealerID)
        {
            if (fan == null)
                return false;
            Unit u = null;
            try { u = fan.GetUnit(); }
            catch { u = null; }
            return BlockGroundGunOnE(u, dealerID);
        }

        internal static void BeginGroundGunBlast(PersistentID dealerID, PersistentID missileID, out bool marked)
        {
            marked = false;
            if (missileID.IsValid)
                return;
            Unit dealer = null;
            if (dealerID.IsValid)
            {
                try { dealerID.TryGetUnit(out dealer); }
                catch { dealer = null; }
            }
            if (!IsGroundGunDealer(dealer))
                return;
            GroundGunBlast++;
            marked = true;
        }

        internal static void EndGroundGunBlast(bool marked)
        {
            if (!marked)
                return;
            if (GroundGunBlast > 0)
                GroundGunBlast--;
        }

        private static void KeepEcm(Aircraft ac)
        {
            if (ac == null || !Service.IsOaEClone(ac) || EcmField == null)
                return;
            try
            {
                float now = (float)EcmField.GetValue(ac);
                if (now < EcmKeep * 0.95f)
                    EcmField.SetValue(ac, EcmKeep);
            }
            catch { }
        }

        private static void StolLift(Aircraft ac)
        {
            if (!Service.HasSimAuthority(ac))
                return;
            Rigidbody rb = null;
            try { rb = ac.rb; }
            catch { rb = null; }
            if (rb == null || rb.isKinematic)
                return;
            float alt = 0f;
            float spd = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            try { spd = ac.speed; }
            catch { spd = 0f; }
            if (alt > StolCeilM || spd < 6f || spd > 95f)
                return;
            try
            {
                if (ac.gearDeployed)
                    return;
            }
            catch { }
            try
            {
                if (ac.IsLanded())
                    return;
            }
            catch { }
            if (Service.InHangarHold(ac))
                return;
            float ge = 1f - Mathf.Clamp01(alt / StolCeilM);
            float slow = 1f - Mathf.Clamp01((spd - 18f) / 70f);
            if (slow < 0.15f)
                slow = 0.15f;
            float lift = rb.mass * 9.81f * (0.28f + 0.62f * ge) * slow;
            try { rb.AddForce(Vector3.up * lift, ForceMode.Force); }
            catch { }
        }

        private static void LowAltG(Aircraft ac)
        {
            ControlsFilter cf = null;
            try { cf = ac.GetControlsFilter(); }
            catch { cf = null; }
            if (cf == null)
                return;
            ControlsFilter.FlyByWire fbw = null;
            try { fbw = cf.GetFlyByWire(); }
            catch { fbw = null; }
            if (fbw == null)
                return;
            int id = ac.GetInstanceID();
            if (FbwOnce.Add(id) && FbwTakeoff != null)
            {
                try { FbwTakeoff.SetValue(fbw, 22f); }
                catch { }
            }
            float alt = 80f;
            try { alt = ac.radarAlt; }
            catch { alt = 80f; }
            byte want = alt < LowGCeilM ? (byte)1 : (byte)0;
            byte had;
            if (GBand.TryGetValue(id, out had) && had == want)
                return;
            GBand[id] = want;
            bool low = want == 1;
            if (FbwGLimit != null)
            {
                try { FbwGLimit.SetValue(fbw, low ? 16f : 9f); }
                catch { }
            }
            if (FbwAlpha != null && low)
            {
                try { FbwAlpha.SetValue(fbw, 32f); }
                catch { }
            }
        }

        private static void BuffFuel(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            if (!FuelDone.Add(id))
                return;
            FuelTank[] tanks = null;
            try { tanks = ac.GetComponentsInChildren<FuelTank>(true); }
            catch { tanks = null; }
            if (tanks == null)
                return;
            FieldInfo capField = FuelCapField;
            for (int i = 0; i < tanks.Length; i++)
            {
                FuelTank t = tanks[i];
                if (t == null)
                    continue;
                if (capField != null)
                {
                    try
                    {
                        float cap = (float)capField.GetValue(t);
                        if (cap > 0.01f)
                            capField.SetValue(t, cap * 1.6f);
                    }
                    catch { }
                }
                try { t.fuelMass = t.fuelMass * 1.6f; }
                catch { }
            }
        }
    }

    [HarmonyPatch(typeof(Unit), "ModifyRCS")]
    internal static class Patch_OaC_RcsZero
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Unit __instance)
        {
            Aircraft ac = __instance as Aircraft;
            if (ac == null)
                return;
            OaTraits.KeepRcs(ac);
        }
    }

    [HarmonyPatch(typeof(Unit), "RegisterHit")]
    internal static class Patch_OaE_GroundGunHit
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Unit __instance, Unit hitUnit)
        {
            if (!OaTraits.HitIsOaE(hitUnit))
                return true;
            if (!OaTraits.IsGroundGunDealer(__instance))
                return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(DamageEffects), "BlastFrag")]
    internal static class Patch_OaE_GroundGunBlast
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(PersistentID dealerID, PersistentID missileID, ref bool __state)
        {
            bool marked;
            OaTraits.BeginGroundGunBlast(dealerID, missileID, out marked);
            __state = marked;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(bool __state)
        {
            OaTraits.EndGroundGunBlast(__state);
        }
    }

    [HarmonyPatch(typeof(UnitPart), "TakeDamage")]
    internal static class Patch_OaE_PartGun
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(UnitPart __instance, PersistentID dealerID)
        {
            return !OaTraits.BlockGroundGunOnE(__instance, dealerID);
        }
    }

    [HarmonyPatch(typeof(Turbofan), "TakeDamage")]
    internal static class Patch_OaE_FanGun
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Turbofan __instance, PersistentID dealerID)
        {
            return !OaTraits.BlockGroundGunOnE(__instance, dealerID);
        }
    }
}
