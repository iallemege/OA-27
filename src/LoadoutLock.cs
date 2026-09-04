using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// MIG-15S stays on KH38MT + its stripped catalog (tailhook) even when
    /// UnrestrictedWeapons / Oritasy unrestricted dumps every mount onto pylons.
    /// Lock is by current aircraft definition only — hangar preview reuses one
    /// Aircraft instance, so a HardpointSet catalog must never leak to other jets.
    /// </summary>
    internal static class LoadoutLock
    {
        internal const string Kh38Key = "KH38MT";

        private static readonly Dictionary<HardpointSet, HashSet<string>> Catalog =
            new Dictionary<HardpointSet, HashSet<string>>();
        private static readonly FieldInfo AircraftOnWm =
            AccessTools.Field(typeof(WeaponManager), "aircraft");
        private static readonly FieldInfo SelectedTypeField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "selectedType");

        private static AircraftDefinition _vettingDef;
        private static AircraftDefinition _hangarDef;
        private static Aircraft _selectorAircraft;
        private static readonly Dictionary<HardpointSet, Aircraft> SetOwner =
            new Dictionary<HardpointSet, Aircraft>(32);

        internal static void NoteHangarDef(AircraftDefinition def)
        {
            _hangarDef = def;
        }

        internal static void NoteSelectorAircraft(Aircraft ac)
        {
            _selectorAircraft = ac;
            if (ac == null || ac.weaponManager == null || ac.weaponManager.hardpointSets == null)
                return;
            HardpointSet[] sets = ac.weaponManager.hardpointSets;
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] != null)
                    SetOwner[sets[i]] = ac;
            }
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (Service.IsOaFamilyDef(def) || Service.IsDonorDef(def))
                _hangarDef = def;
        }

        internal static Aircraft SelectorAircraft
        {
            get { return _selectorAircraft; }
        }

        internal static void NoteMenu(AircraftSelectionMenu menu)
        {
            if (menu == null || SelectedTypeField == null)
                return;
            try { _hangarDef = SelectedTypeField.GetValue(menu) as AircraftDefinition; }
            catch { }
        }

        internal static void BeginVet(AircraftDefinition def)
        {
            _vettingDef = def;
        }

        internal static void EndVet()
        {
            _vettingDef = null;
        }

        private static AircraftDefinition ActiveDef()
        {
            if (_vettingDef != null)
                return _vettingDef;
            return _hangarDef;
        }

        internal static AircraftDefinition ActiveSpawnDef()
        {
            return ActiveDef();
        }

        internal static void RememberAircraft(Aircraft ac)
        {
        }

        internal static void RememberCatalog(HardpointSet hs)
        {
            if (hs == null)
                return;
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            keys.Add(Kh38Key);
            if (hs.weaponOptions != null)
            {
                for (int i = 0; i < hs.weaponOptions.Count; i++)
                    AddKey(keys, hs.weaponOptions[i]);
            }
            Catalog[hs] = keys;
        }

        internal static bool IsLockedAircraft(Aircraft ac)
        {
            return Service.IsMigClone(ac);
        }

        internal static bool IsLockedDef(AircraftDefinition def)
        {
            return Service.IsMigCloneDef(def);
        }

        /// <summary>
        /// True only when the aircraft being configured / spawned is MiG-15S.
        /// Never lock just because this HardpointSet was seen on a MiG earlier.
        /// </summary>
        internal static bool ShouldLock(HardpointSet hs)
        {
            return false;
        }

        internal static bool IsLockedSet(HardpointSet hs)
        {
            if (hs == null)
                return false;
            Aircraft ac = FindAircraft(hs);
            if (ac == null)
                return false;
            if (!Service.IsMigClone(ac))
                return false;
            RememberCatalog(hs);
            return true;
        }

        internal static bool IsAllowedMount(WeaponMount mount, HardpointSet hs)
        {
            if (mount == null)
                return true;
            if (Service.IsGunMount(mount))
                return false;
            if (IsKh38(mount))
                return true;
            if (IsVeyrnPackMount(mount))
                return true;
            if (mount.tailHook)
                return true;

            HashSet<string> keys;
            if (hs != null && Catalog.TryGetValue(hs, out keys) && keys != null && KeyMatches(keys, mount))
                return true;

            if (hs != null && hs.weaponOptions != null)
            {
                for (int i = 0; i < hs.weaponOptions.Count; i++)
                {
                    WeaponMount opt = hs.weaponOptions[i];
                    if (opt == mount)
                        return true;
                    if (SameKey(opt, mount))
                        return true;
                }
            }
            return false;
        }

        internal static void FilterList(HardpointSet hs, List<WeaponMount> list)
        {
            if (list == null || !ShouldLock(hs))
                return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                WeaponMount m = list[i];
                if (m != null && !IsAllowedMount(m, hs))
                    list.RemoveAt(i);
            }
        }

        internal static bool IsVeyrnPackMount(WeaponMount m)
        {
            if (m == null)
                return false;
            string blob = (m.jsonKey != null ? m.jsonKey : string.Empty) + " "
                + (m.mountName != null ? m.mountName : string.Empty);
            if (m.info != null)
            {
                blob = blob + " " + (m.info.weaponName != null ? m.info.weaponName : string.Empty)
                    + " " + (m.info.shortName != null ? m.info.shortName : string.Empty);
            }
            if (blob.IndexOf("AAM-2CV", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("AAM_2CV", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("AAM2CV", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("ACM-119", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("ACM_119", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("ACNM-118", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("ACNM_118", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("TGM-85", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("TGM_85", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("TGM85", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("Kh-85", StringComparison.OrdinalIgnoreCase) >= 0
                || blob.IndexOf("KH85", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        internal static bool IsKh38(WeaponMount m)
        {
            if (m == null)
                return false;
            if (string.Equals(m.jsonKey, Kh38Key, StringComparison.OrdinalIgnoreCase))
                return true;
            if (NameHasKh38(m.mountName))
                return true;
            if (m.info != null && (NameHasKh38(m.info.weaponName) || NameHasKh38(m.info.shortName)))
                return true;
            return false;
        }

        private static bool NameHasKh38(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            return s.IndexOf("KH38", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Kh-38", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddKey(HashSet<string> keys, WeaponMount m)
        {
            if (m == null || keys == null)
                return;
            if (!string.IsNullOrEmpty(m.jsonKey))
                keys.Add(m.jsonKey);
            if (!string.IsNullOrEmpty(m.mountName))
                keys.Add(m.mountName);
            if (m.info != null)
            {
                if (!string.IsNullOrEmpty(m.info.weaponName))
                    keys.Add(m.info.weaponName);
                if (!string.IsNullOrEmpty(m.info.shortName))
                    keys.Add(m.info.shortName);
            }
        }

        private static bool KeyMatches(HashSet<string> keys, WeaponMount m)
        {
            if (keys == null || m == null)
                return false;
            if (!string.IsNullOrEmpty(m.jsonKey) && keys.Contains(m.jsonKey))
                return true;
            if (!string.IsNullOrEmpty(m.mountName) && keys.Contains(m.mountName))
                return true;
            if (m.info != null)
            {
                if (!string.IsNullOrEmpty(m.info.weaponName) && keys.Contains(m.info.weaponName))
                    return true;
                if (!string.IsNullOrEmpty(m.info.shortName) && keys.Contains(m.info.shortName))
                    return true;
            }
            return false;
        }

        private static bool SameKey(WeaponMount a, WeaponMount b)
        {
            if (a == null || b == null)
                return false;
            if (!string.IsNullOrEmpty(a.jsonKey) && string.Equals(a.jsonKey, b.jsonKey, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        internal static Aircraft FindAircraft(HardpointSet hs)
        {
            if (hs == null)
                return null;
            Aircraft cached;
            if (SetOwner.TryGetValue(hs, out cached) && cached != null)
                return cached;
            if (_selectorAircraft != null
                && _selectorAircraft.weaponManager != null
                && _selectorAircraft.weaponManager.hardpointSets != null)
            {
                HardpointSet[] own = _selectorAircraft.weaponManager.hardpointSets;
                for (int i = 0; i < own.Length; i++)
                {
                    if (object.ReferenceEquals(own[i], hs))
                    {
                        SetOwner[hs] = _selectorAircraft;
                        return _selectorAircraft;
                    }
                }
            }
            Aircraft ac = null;
            List<Aircraft> live = null;
            try { live = UnitRegistry.allAircraft; }
            catch { live = null; }
            if (live != null)
            {
                for (int i = 0; i < live.Count; i++)
                {
                    Aircraft cur = live[i];
                    if (cur == null || cur.weaponManager == null
                        || cur.weaponManager.hardpointSets == null)
                        continue;
                    HardpointSet[] sets = cur.weaponManager.hardpointSets;
                    for (int h = 0; h < sets.Length; h++)
                    {
                        if (object.ReferenceEquals(sets[h], hs))
                        {
                            ac = cur;
                            break;
                        }
                    }
                    if (ac != null)
                        break;
                }
            }
            if (ac == null && AircraftOnWm != null)
            {
                WeaponManager[] wms = null;
                try { wms = UnityEngine.Object.FindObjectsOfType<WeaponManager>(); }
                catch { wms = null; }
                if (wms != null)
                {
                    for (int i = 0; i < wms.Length; i++)
                    {
                        WeaponManager wm = wms[i];
                        if (wm == null || wm.hardpointSets == null)
                            continue;
                        bool hit = false;
                        for (int h = 0; h < wm.hardpointSets.Length; h++)
                        {
                            if (object.ReferenceEquals(wm.hardpointSets[h], hs))
                            {
                                hit = true;
                                break;
                            }
                        }
                        if (!hit)
                            continue;
                        try { ac = AircraftOnWm.GetValue(wm) as Aircraft; }
                        catch { ac = null; }
                        if (ac == null)
                        {
                            try { ac = wm.GetComponentInParent<Aircraft>(); }
                            catch { ac = null; }
                        }
                        if (ac != null)
                            break;
                    }
                }
            }
            if (ac != null)
                SetOwner[hs] = ac;
            return ac;
        }
    }

    [HarmonyPatch(typeof(WeaponSelector), "Initialize")]
    [HarmonyPatch(new Type[] { typeof(Aircraft), typeof(HardpointSet), typeof(FactionHQ), typeof(Airbase) })]
    internal static class Patch_WeaponSelector_LockCatalog
    {
        private static void Prefix(Aircraft aircraft, HardpointSet hardpointSet)
        {
            if (!LoadoutLock.IsLockedAircraft(aircraft))
                return;
            LoadoutLock.RememberCatalog(hardpointSet);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "SetSelectedType")]
    internal static class Patch_MiG15S_HangarSelectedType
    {
        [HarmonyPostfix]
        private static void Postfix(AircraftDefinition definition)
        {
            LoadoutLock.NoteHangarDef(definition);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "Refresh")]
    internal static class Patch_MiG15S_HangarRefreshLock
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionMenu __instance)
        {
            LoadoutLock.NoteMenu(__instance);
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "VetLoadout")]
    internal static class Patch_VetLoadout_LockScope
    {
        [HarmonyPrefix]
        private static void Prefix(AircraftDefinition definition)
        {
            LoadoutLock.BeginVet(definition);
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            LoadoutLock.EndVet();
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "GetAvailableWeaponsNonAlloc")]
    internal static class Patch_GetAvailableWeapons_Lock
    {
        [HarmonyFinalizer]
        private static void Finalizer(HardpointSet hardpointSet, List<WeaponMount> outAvailable)
        {
            LoadoutLock.FilterList(hardpointSet, outAvailable);
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "VetWeapon")]
    internal static class Patch_VetWeapon_Lock
    {
        [HarmonyFinalizer]
        private static void Finalizer(
            WeaponMount requestedMount,
            HardpointSet hardpointSet,
            ref bool __result,
            ref string failReason,
            ref int failCost)
        {
            if (!__result || !LoadoutLock.ShouldLock(hardpointSet))
                return;
            if (LoadoutLock.IsAllowedMount(requestedMount, hardpointSet))
                return;
            __result = false;
            failReason = "MIG-15S locked loadout";
            failCost = 0;
        }
    }

    [HarmonyPatch(typeof(WeaponChecker), "MountAllowedHardpoint")]
    internal static class Patch_MountAllowedHardpoint_Lock
    {
        [HarmonyFinalizer]
        private static void Finalizer(WeaponMount mount, HardpointSet hardpointSet, ref bool __result)
        {
            if (!__result || !LoadoutLock.ShouldLock(hardpointSet))
                return;
            if (LoadoutLock.IsAllowedMount(mount, hardpointSet))
                return;
            __result = false;
        }
    }
}
