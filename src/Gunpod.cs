using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// KH38MT single and 30mm swivel pod on every aircraft hardpoint.
    /// OA-27C still restores its stock catalog on top of those extras.
    /// </summary>
    internal static class GunpodInject
    {
        internal const string DonorKey = "Aryx_PropAttacker1_SwivelGunpod";
        internal const string CloneKey = "IAL_OA27_30mm_WingPod";
        internal const string Display = "30mm Swivel Gunpod";
        internal const string Kh38Key = "KH38MT";

        private static WeaponMount _pod;
        private static WeaponMount _kh38;
        private static float _nextFind;
        private static float _nextInject;

        internal static void Tick()
        {
            if (_pod == null || _kh38 == null)
                FindOrClone();
            if (_pod == null && _kh38 == null)
                return;
            if (Time.unscaledTime < _nextInject)
                return;
            InjectLive();
        }

        internal static WeaponMount Pod
        {
            get
            {
                if (_pod == null)
                    FindOrClone();
                return _pod;
            }
        }

        internal static WeaponMount Kh38
        {
            get
            {
                if (_kh38 == null)
                    FindOrClone();
                return _kh38;
            }
        }

        private static void FindOrClone()
        {
            if (Time.unscaledTime < _nextFind && _pod == null && _kh38 == null)
                return;
            if (Time.unscaledTime < _nextFind && _pod != null && _kh38 != null)
                return;
            _nextFind = Time.unscaledTime + 6f;
            try { NobpDonor.Ensure(); }
            catch { }
            try { Service.EnsureClones(); }
            catch { }
            if (_kh38 == null)
            {
                _kh38 = FindMount(Kh38Key);
                if (_kh38 == null)
                    _kh38 = FindMountByName("KH38MT");
            }
            if (_pod != null)
                return;
            WeaponMount donor = FindMount(DonorKey);
            if (donor == null)
                donor = FindMountByName("30mm Swivel Gunpod");
            if (donor == null)
                return;
            WeaponMount existing = FindMount(CloneKey);
            if (existing != null && Service.MountIsNetworked(existing))
            {
                _pod = existing;
                return;
            }
            _pod = donor;
            Service.RegisterNetworkMount(donor);
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("30mm swivel gun pod uses encyclopedia " + DonorKey);
        }

        private static WeaponMount FindMount(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            WeaponMount[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<WeaponMount>(); }
            catch { all = null; }
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                WeaponMount m = all[i];
                if (m != null
                    && string.Equals(m.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
            return null;
        }

        private static WeaponMount FindMountByName(string name)
        {
            WeaponMount[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<WeaponMount>(); }
            catch { all = null; }
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                WeaponMount m = all[i];
                if (m == null)
                    continue;
                if (!string.IsNullOrEmpty(m.mountName)
                    && string.Equals(m.mountName, name, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
            return null;
        }

        private static void InjectLive()
        {
            _nextInject = Time.unscaledTime + 45f;
            Service.SnapshotOaDonorLoadouts();
            List<Aircraft> all = null;
            try { all = UnitRegistry.allAircraft; }
            catch { all = null; }
            if (all == null)
                return;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i];
                if (ac == null || ac.weaponManager == null)
                    continue;
                if (!Service.IsOaFamilyClone(ac) && !Service.IsOaHangarPreview(ac)
                    && !Service.IsOaPowered(ac))
                    continue;
                InjectManager(ac.weaponManager);
            }
        }

        internal static void InjectManager(WeaponManager wm)
        {
            if (wm == null || wm.hardpointSets == null)
                return;
            if (_pod == null && _kh38 == null)
                return;
            Aircraft ac = null;
            try { ac = wm.GetComponentInParent<Aircraft>(); }
            catch { ac = null; }
            if (ac == null)
                return;
            if (!Service.IsOaFamilyClone(ac) && !Service.IsOaHangarPreview(ac)
                && !Service.IsOaPowered(ac)
                && !Service.IsOaFamilyDef(LoadoutLock.ActiveSpawnDef()))
                return;
            Service.DetachOaHardpoints(ac);
            for (int i = 0; i < wm.hardpointSets.Length; i++)
                OfferOnSet(wm.hardpointSets[i], ac);
        }

        internal static void OfferOnSet(HardpointSet hs)
        {
            OfferOnSet(hs, null);
        }

        internal static void OfferOnSet(HardpointSet hs, Aircraft ac)
        {
            if (hs == null || IsNavalHardpoint(hs))
                return;
            if (!Service.IsOaLoadoutContext(ac, hs))
                return;
            Service.MergeOaStock(hs, ac);
            if (hs.weaponOptions == null)
                hs.weaponOptions = new List<WeaponMount>(8);
            OfferMount(hs.weaponOptions, Pod);
            OfferMount(hs.weaponOptions, Kh38);
        }

        internal static void RestoreStockPlusPod(HardpointSet hs, List<WeaponMount> list)
        {
            if (list == null || hs == null)
                return;
            Aircraft ac = LoadoutLock.FindAircraft(hs);
            if (ac == null)
                ac = LoadoutLock.SelectorAircraft;
            if (!Service.IsOaLoadoutContext(ac, hs))
            {
                OfferMount(list, Pod);
                OfferMount(list, Kh38);
                return;
            }
            Service.MergeOaStock(hs, ac);
            Service.MergeLoadoutSlotsIntoHardpoints(ac);
            if (hs.weaponOptions == null)
                hs.weaponOptions = new List<WeaponMount>(8);
            int i;
            for (i = 0; i < hs.weaponOptions.Count; i++)
            {
                WeaponMount m = hs.weaponOptions[i];
                if (m == null)
                    continue;
                Service.PrepareStockMount(m);
                if (string.IsNullOrEmpty(m.mountName) && !string.IsNullOrEmpty(m.jsonKey))
                    m.mountName = m.jsonKey;
                if (!string.IsNullOrEmpty(m.mountName) && !ListHas(list, m))
                    list.Add(m);
            }
            OfferOnSet(hs, ac);
            OfferMount(list, Pod);
            OfferMount(list, Kh38);
        }

        internal static bool IsNavalHardpoint(HardpointSet hs)
        {
            if (hs == null || string.IsNullOrEmpty(hs.name))
                return false;
            string n = hs.name;
            if (n.IndexOf("VLS", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Ship", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Naval", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Cell", StringComparison.OrdinalIgnoreCase) >= 0
                && (n.IndexOf("Launch", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("VLS", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Mk", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            return false;
        }

        private static void OfferMount(List<WeaponMount> list, WeaponMount mount)
        {
            if (list == null || mount == null)
                return;
            mount = Service.ResolveNetworkMount(mount);
            if (mount == null)
                return;
            if (string.IsNullOrEmpty(mount.mountName) && !string.IsNullOrEmpty(mount.jsonKey))
                mount.mountName = mount.jsonKey;
            if (ListHas(list, mount))
                return;
            list.Add(mount);
        }

        internal static bool ListHas(List<WeaponMount> list, WeaponMount mount)
        {
            if (list == null || mount == null)
                return false;
            string key = mount.jsonKey;
            for (int i = 0; i < list.Count; i++)
            {
                WeaponMount cur = list[i];
                if (cur == null)
                    continue;
                if (object.ReferenceEquals(cur, mount))
                    return true;
                if (!string.IsNullOrEmpty(key)
                    && string.Equals(cur.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrEmpty(key)
                    && string.Equals(cur.jsonKey, DonorKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(mount.jsonKey, CloneKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static bool IsOurExtra(WeaponMount mount)
        {
            if (mount == null)
                return false;
            if (IsPod(mount) || IsKh38(mount))
                return true;
            return false;
        }

        internal static bool IsPod(WeaponMount mount)
        {
            if (mount == null)
                return false;
            if (_pod != null && object.ReferenceEquals(_pod, mount))
                return true;
            string key = mount.jsonKey;
            return string.Equals(key, CloneKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, DonorKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsKh38(WeaponMount mount)
        {
            if (mount == null)
                return false;
            if (_kh38 != null && object.ReferenceEquals(_kh38, mount))
                return true;
            return LoadoutLock.IsKh38(mount);
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "GetAvailableWeaponsNonAlloc")]
    internal static class Patch_OA27C_KeepStockCatalog
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(HardpointSet hardpointSet)
        {
            GunpodInject.OfferOnSet(hardpointSet, LoadoutLock.SelectorAircraft);
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(HardpointSet hardpointSet, List<WeaponMount> outAvailable)
        {
            GunpodInject.RestoreStockPlusPod(hardpointSet, outAvailable);
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "VetWeapon")]
    internal static class Patch_OA27C_VetAllowStock
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)]
        private static bool Prefix(
            WeaponMount requestedMount,
            HardpointSet hardpointSet,
            ref bool __result,
            ref string failReason,
            ref int failCost)
        {
            if (!GunpodInject.IsOurExtra(requestedMount))
                return true;
            if (GunpodInject.IsNavalHardpoint(hardpointSet))
                return true;
            GunpodInject.OfferOnSet(hardpointSet, LoadoutLock.SelectorAircraft);
            __result = true;
            failReason = null;
            failCost = 0;
            return false;
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(
            WeaponMount requestedMount,
            HardpointSet hardpointSet,
            ref bool __result)
        {
            if (__result || requestedMount == null)
                return;
            if (GunpodInject.IsNavalHardpoint(hardpointSet))
                return;
            if (GunpodInject.IsOurExtra(requestedMount))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "MountAllowedHardpoint")]
    internal static class Patch_OA27C_MountAllowStock
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.VeryHigh)]
        private static bool Prefix(WeaponMount mount, HardpointSet hardpointSet, ref bool __result)
        {
            if (!GunpodInject.IsOurExtra(mount))
                return true;
            if (GunpodInject.IsNavalHardpoint(hardpointSet))
                return true;
            GunpodInject.OfferOnSet(hardpointSet, LoadoutLock.SelectorAircraft);
            __result = true;
            return false;
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(WeaponMount mount, HardpointSet hardpointSet, ref bool __result)
        {
            if (__result || mount == null)
                return;
            if (GunpodInject.IsNavalHardpoint(hardpointSet))
                return;
            if (GunpodInject.IsOurExtra(mount))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WeaponSelector), "Initialize", new Type[] { typeof(Aircraft), typeof(HardpointSet), typeof(FactionHQ), typeof(Airbase) })]
    internal static class Patch_OA27C_WingPodSelector
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Aircraft aircraft, HardpointSet hardpointSet)
        {
            LoadoutLock.NoteSelectorAircraft(aircraft);
            if (aircraft != null && aircraft.weaponManager != null)
                GunpodInject.InjectManager(aircraft.weaponManager);
            GunpodInject.OfferOnSet(hardpointSet, aircraft);
        }
    }

    [HarmonyPatch(typeof(WeaponSelector), "SetValue")]
    internal static class Patch_OA27C_SetValueByKey
    {
        private static readonly FieldInfo DropdownOptionsField =
            AccessTools.Field(typeof(WeaponSelector), "dropdownOptions");

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(WeaponSelector __instance, ref WeaponMount weaponMount)
        {
            if (weaponMount == null || __instance == null || DropdownOptionsField == null)
                return;
            object raw = null;
            try { raw = DropdownOptionsField.GetValue(__instance); }
            catch { raw = null; }
            IList list = raw as IList;
            if (list == null)
                return;
            string key = weaponMount.jsonKey;
            int i;
            for (i = 0; i < list.Count; i++)
            {
                object item = list[i];
                if (item == null)
                    continue;
                FieldInfo item2 = item.GetType().GetField("Item2");
                object boxed = null;
                if (item2 != null)
                {
                    try { boxed = item2.GetValue(item); }
                    catch { boxed = null; }
                }
                else
                {
                    PropertyInfo prop = item.GetType().GetProperty("Item2");
                    if (prop != null)
                    {
                        try { boxed = prop.GetValue(item, null); }
                        catch { boxed = null; }
                    }
                }
                WeaponMount m = boxed as WeaponMount;
                if (m == null)
                    continue;
                if (object.ReferenceEquals(m, weaponMount))
                    return;
                if (!string.IsNullOrEmpty(key)
                    && string.Equals(m.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    weaponMount = m;
                    return;
                }
                if (Service.IsInternal20(weaponMount) && Service.IsInternal20(m))
                {
                    weaponMount = m;
                    return;
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_OA27C_PodMatchesPylon
    {
        private static readonly FieldInfo BoundMountField;

        static Patch_OA27C_PodMatchesPylon()
        {
            Type nested = AccessTools.Inner(typeof(Hardpoint), "HardpointPylon");
            BoundMountField = nested != null ? AccessTools.Field(nested, "mount") : null;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            Type nested = AccessTools.Inner(typeof(Hardpoint), "HardpointPylon");
            if (nested == null)
                return null;
            return AccessTools.Method(nested, "MatchesMount", new Type[] { typeof(WeaponMount) });
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, WeaponMount mount, ref bool __result)
        {
            if (__result || mount == null || __instance == null || BoundMountField == null)
                return;
            if (!GunpodInject.IsOurExtra(mount))
                return;
            WeaponMount bound = null;
            try { bound = BoundMountField.GetValue(__instance) as WeaponMount; }
            catch { return; }
            if (bound == null)
                return;
            __result = true;
        }
    }
}
