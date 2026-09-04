using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace OA27Variant
{
    /// <summary>
    /// Blueprinter hangar ids (revetment1__revetment1) do not match 0.34 instance
    /// names, so the definition shows up greyed-out. Put MiG-15S on every live hangar.
    /// </summary>
    internal static class HangarInject
    {
        private static readonly FieldInfo AvailableAircraftField =
            AccessTools.Field(typeof(Hangar), "availableAircraft");
        private static readonly FieldInfo SelectionField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "aircraftSelection");
        private static readonly FieldInfo MenuAirbaseField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "airbase");
        private static readonly FieldInfo ButtonLabelField =
            AccessTools.Field(typeof(AircraftSelectionButton), "label");
        private static readonly FieldInfo SpawnedObjectField =
            AccessTools.Field(typeof(Hangar), "spawnedObject");
        private static readonly FieldInfo ClearDistanceField =
            AccessTools.Field(typeof(Hangar), "clearDistance");
        private static readonly Dictionary<int, float> ReservedUntil =
            new Dictionary<int, float>(32);
        private static AircraftDefinition _cachedDef;
        private static FactionHQ[] _cachedHqs;
        private static float _nextHqScan;
        private static bool _encOnce;

        internal static void ApplyShortIcon(AircraftSelectionButton btn)
        {
            if (btn == null)
                return;
            AircraftDefinition def = null;
            try { def = btn.definition; }
            catch { def = null; }
            if (!Service.IsOursDef(def))
                return;
            if (ButtonLabelField == null)
                return;
            Text label = ButtonLabelField.GetValue(btn) as Text;
            if (label != null)
            {
                if (Service.IsOaEDef(def))
                    label.text = Service.OaEShortName;
                else if (Service.IsOaDDef(def))
                    label.text = Service.OaDShortName;
                else if (Service.IsOaDef(def))
                    label.text = Service.OaShortName;
                else
                    label.text = Service.ShortName;
            }
        }

        internal static void ApplyOaDVisibility(AircraftSelectionButton btn)
        {
            if (btn == null || btn.gameObject == null)
                return;
            AircraftDefinition def = null;
            try { def = btn.definition; }
            catch { def = null; }
            if (def == null)
                return;
            if (!Service.IsOaDDef(def) && !Service.IsOaEDef(def))
                return;
            if (!Service.LocalPlayerMaySelectExclusive(def))
            {
                if (btn.gameObject.activeSelf)
                {
                    try { btn.gameObject.SetActive(false); }
                    catch { }
                }
                return;
            }
            if (!btn.gameObject.activeSelf)
            {
                try { btn.gameObject.SetActive(true); }
                catch { }
            }
        }

        internal static AircraftDefinition FindDef()
        {
            Service.EnsureClones();
            if (Service.OaClone != null)
            {
                _cachedDef = Service.OaClone;
                return _cachedDef;
            }
            return Service.OaClone;
        }

        internal static AircraftDefinition FindOaDef()
        {
            return FindDef();
        }

        internal static AircraftDefinition FindOaDDef()
        {
            Service.EnsureClones();
            return Service.OaDClone;
        }

        internal static AircraftDefinition FindOaEDef()
        {
            Service.EnsureClones();
            return Service.OaEClone;
        }

        internal static void RegisterNetwork(AircraftDefinition def)
        {
            if (def == null)
                return;
            try
            {
                if (!string.IsNullOrEmpty(def.jsonKey) && Encyclopedia.Lookup != null)
                    Encyclopedia.Lookup[def.jsonKey] = def;
            }
            catch { }
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc == null || enc.IndexLookup == null)
                return;
            INetworkDefinition nd = def;
            try
            {
                int? existing = nd.LookupIndex;
                if (existing.HasValue
                    && existing.Value >= 0
                    && existing.Value < enc.IndexLookup.Count)
                {
                    if (object.ReferenceEquals(enc.IndexLookup[existing.Value], nd))
                        return;
                    nd.LookupIndex = null;
                }
                int idx = enc.IndexLookup.IndexOf(nd);
                if (idx >= 0)
                {
                    nd.LookupIndex = idx;
                    return;
                }
                string key = def.jsonKey;
                int exact = -1;
                for (int i = 0; i < enc.IndexLookup.Count; i++)
                {
                    UnitDefinition ud = enc.IndexLookup[i] as UnitDefinition;
                    if (ud == null || string.IsNullOrEmpty(ud.jsonKey))
                        continue;
                    if (!string.IsNullOrEmpty(key)
                        && string.Equals(ud.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        exact = i;
                        break;
                    }
                }
                if (exact >= 0)
                {
                    nd.LookupIndex = exact;
                    return;
                }
                enc.IndexLookup.Add(nd);
                nd.LookupIndex = enc.IndexLookup.Count - 1;
            }
            catch { }
        }

        internal static void EnsureOnHangar(Hangar hangar)
        {
            if (hangar == null || AvailableAircraftField == null)
                return;
            if (IsRubble(hangar.name))
                return;
            if (IsHelipad(hangar))
            {
                StripFromHangar(hangar);
                return;
            }
            try
            {
                if (hangar.Disabled)
                    return;
            }
            catch { }
            AddCloneToHangar(hangar, FindOaDef());
            if (Service.LocalPlayerMaySelectOaD())
                AddCloneToHangar(hangar, FindOaDDef());
            else
                StripKeyFromHangar(hangar, Service.OaDJsonKey);
            if (Service.LocalPlayerMaySelectOaE())
                AddCloneToHangar(hangar, FindOaEDef());
            else
                StripKeyFromHangar(hangar, Service.OaEJsonKey);
        }

        private static void AddCloneToHangar(Hangar hangar, AircraftDefinition def)
        {
            if (hangar == null || def == null || AvailableAircraftField == null)
                return;
            if (Service.IsDonorDef(def))
                return;
            AircraftDefinition[] cur = AvailableAircraftField.GetValue(hangar) as AircraftDefinition[];
            if (ArrayContains(cur, def))
                return;
            Service.ApplyEncyclopedia(def);
            RegisterNetwork(def);
            int n = cur != null ? cur.Length : 0;
            AircraftDefinition[] next = new AircraftDefinition[n + 1];
            if (n > 0)
                Array.Copy(cur, next, n);
            next[n] = def;
            AvailableAircraftField.SetValue(hangar, next);
        }

        private static void StripKeyFromHangar(Hangar hangar, string key)
        {
            if (hangar == null || AvailableAircraftField == null || string.IsNullOrEmpty(key))
                return;
            AircraftDefinition[] cur = AvailableAircraftField.GetValue(hangar) as AircraftDefinition[];
            if (cur == null || cur.Length == 0)
                return;
            int keep = 0;
            for (int i = 0; i < cur.Length; i++)
            {
                if (cur[i] == null)
                    continue;
                if (string.Equals(cur[i].jsonKey, key, StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(key, Service.OaDJsonKey, StringComparison.OrdinalIgnoreCase)
                        && Service.IsOaDDef(cur[i]))
                    || (string.Equals(key, Service.OaEJsonKey, StringComparison.OrdinalIgnoreCase)
                        && Service.IsOaEDef(cur[i])))
                    continue;
                cur[keep] = cur[i];
                keep++;
            }
            if (keep == cur.Length)
                return;
            AircraftDefinition[] next = new AircraftDefinition[keep];
            if (keep > 0)
                Array.Copy(cur, next, keep);
            AvailableAircraftField.SetValue(hangar, next);
        }

        private static void StripKeyFromList(List<AircraftDefinition> dest, string key)
        {
            if (dest == null || dest.Count == 0)
                return;
            for (int i = dest.Count - 1; i >= 0; i--)
            {
                AircraftDefinition d = dest[i];
                if (d == null)
                    continue;
                if ((!string.IsNullOrEmpty(key)
                        && string.Equals(d.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    || (string.Equals(key, Service.OaDJsonKey, StringComparison.OrdinalIgnoreCase)
                        && Service.IsOaDDef(d))
                    || (string.Equals(key, Service.OaEJsonKey, StringComparison.OrdinalIgnoreCase)
                        && Service.IsOaEDef(d)))
                    dest.RemoveAt(i);
            }
        }

        private static float _nextScan;

        internal static void Tick()
        {
            Aircraft ac;
            try
            {
                if (GameManager.GetLocalAircraft(out ac) && ac != null && Plugin.IsRuntime(ac))
                    return;
            }
            catch { }
            if (Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 8f;
            EnsureAiSupply();
        }

        internal static AircraftDefinition ListedDef()
        {
            Service.EnsureClones();
            AircraftDefinition def = FindDef();
            if (def == null)
                def = FindOaDef();
            if (def == null)
                return null;
            Service.ApplyEncyclopedia(def);
            RegisterNetwork(def);
            return def;
        }

        internal static void EnsureAiSupply()
        {
            Service.EnsureClones();
            AircraftDefinition mig = FindDef();
            AircraftDefinition oa = FindOaDef();
            AircraftDefinition oaD = FindOaDDef();
            AircraftDefinition oaE = FindOaEDef();
            if (mig != null)
            {
                RegisterNetwork(mig);
                if (!_encOnce)
                    Service.ApplyEncyclopedia(mig);
            }
            if (oa != null)
            {
                RegisterNetwork(oa);
                if (!_encOnce)
                    Service.ApplyEncyclopedia(oa);
            }
            if (oaD != null)
            {
                RegisterNetwork(oaD);
                if (!_encOnce)
                    Service.ApplyEncyclopedia(oaD);
            }
            if (oaE != null)
            {
                RegisterNetwork(oaE);
                if (!_encOnce)
                    Service.ApplyEncyclopedia(oaE);
            }
            _encOnce = mig != null || oa != null || oaD != null || oaE != null || _encOnce;
            FactionHQ[] hqs = CachedHqs();
            if (hqs == null)
                return;
            for (int i = 0; i < hqs.Length; i++)
            {
                if (mig != null)
                    EnsureHqSupply(hqs[i], mig);
                if (oa != null)
                    EnsureHqSupply(hqs[i], oa);
                if (oaD != null)
                    EnsureHqSupplyOaD(hqs[i], oaD);
                if (oaE != null)
                    EnsureHqSupplyOaE(hqs[i], oaE);
            }
        }

        internal static void EnsureHq(FactionHQ hq)
        {
            Service.EnsureClones();
            AircraftDefinition mig = FindDef();
            AircraftDefinition oa = FindOaDef();
            AircraftDefinition oaD = FindOaDDef();
            AircraftDefinition oaE = FindOaEDef();
            if (mig != null)
            {
                RegisterNetwork(mig);
                EnsureHqSupply(hq, mig);
            }
            if (oa != null)
            {
                RegisterNetwork(oa);
                EnsureHqSupply(hq, oa);
            }
            if (oaD != null)
            {
                RegisterNetwork(oaD);
                EnsureHqSupplyOaD(hq, oaD);
            }
            if (oaE != null)
            {
                RegisterNetwork(oaE);
                EnsureHqSupplyOaE(hq, oaE);
            }
        }

        private static FactionHQ[] CachedHqs()
        {
            if (_cachedHqs != null && _cachedHqs.Length > 0)
                return _cachedHqs;
            if (Time.unscaledTime < _nextHqScan)
                return _cachedHqs;
            _nextHqScan = Time.unscaledTime + 15f;
            try { _cachedHqs = Resources.FindObjectsOfTypeAll<FactionHQ>(); }
            catch { _cachedHqs = null; }
            return _cachedHqs;
        }

        private static void EnsureHqSupply(FactionHQ hq, AircraftDefinition def)
        {
            if (hq == null || def == null)
                return;
            if (!Plugin.IsRuntime(hq))
                return;
            try
            {
                if (!hq.IsServer)
                    return;
            }
            catch
            {
                return;
            }
            UnrestrictHq(hq);
            int reserved = 0;
            try { reserved = hq.reserveAirframes; }
            catch { reserved = 0; }
            int players = 0;
            try
            {
                if (hq.factionPlayers != null)
                    players = hq.factionPlayers.Count;
            }
            catch { players = 0; }
            try { reserved += hq.extraReservesPerPlayer * players; }
            catch { }
            int want = reserved + 8;
            if (want < 8)
                want = 8;
            int have = 0;
            try { have = hq.GetUnitSupply(def); }
            catch { have = 0; }
            if (have >= want)
                return;
            try { hq.ModifyUnitSupply(def, want - have); }
            catch { }
        }

        private static void EnsureHqSupplyOaD(FactionHQ hq, AircraftDefinition def)
        {
            if (hq == null || def == null)
                return;
            if (Service.HqIsPala(hq))
            {
                RestrictOaD(hq);
                return;
            }
            UnrestrictOaD(hq);
            if (!Service.HqIsBdf(hq))
                return;
            EnsureHqSupply(hq, def);
        }

        private static void EnsureHqSupplyOaE(FactionHQ hq, AircraftDefinition def)
        {
            if (hq == null || def == null)
                return;
            if (Service.HqIsBdf(hq))
            {
                RestrictOaE(hq);
                return;
            }
            UnrestrictOaE(hq);
            if (!Service.HqIsPala(hq))
                return;
            EnsureHqSupply(hq, def);
        }

        private static void RestrictOaD(FactionHQ hq)
        {
            if (hq == null)
                return;
            AddRestricted(hq.restrictedAircraft, Service.OaDJsonKey);
            try { AddRestricted(hq.NetworkrestrictedAircraft, Service.OaDJsonKey); }
            catch { }
        }

        private static void UnrestrictOaD(FactionHQ hq)
        {
            if (hq == null)
                return;
            RemoveKey(hq.restrictedAircraft, Service.OaDJsonKey);
            try { RemoveKey(hq.NetworkrestrictedAircraft, Service.OaDJsonKey); }
            catch { }
        }

        private static void RestrictOaE(FactionHQ hq)
        {
            if (hq == null)
                return;
            AddRestricted(hq.restrictedAircraft, Service.OaEJsonKey);
            try { AddRestricted(hq.NetworkrestrictedAircraft, Service.OaEJsonKey); }
            catch { }
        }

        private static void UnrestrictOaE(FactionHQ hq)
        {
            if (hq == null)
                return;
            RemoveKey(hq.restrictedAircraft, Service.OaEJsonKey);
            try { RemoveKey(hq.NetworkrestrictedAircraft, Service.OaEJsonKey); }
            catch { }
        }

        private static void RemoveKey(List<string> keys, string key)
        {
            if (keys == null || string.IsNullOrEmpty(key))
                return;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(key, Service.OaDJsonKey, StringComparison.OrdinalIgnoreCase)
                        && keys[i] != null
                        && keys[i].IndexOf("OA-27D", StringComparison.OrdinalIgnoreCase) >= 0)
                    || (string.Equals(key, Service.OaEJsonKey, StringComparison.OrdinalIgnoreCase)
                        && keys[i] != null
                        && keys[i].IndexOf("OA-27E", StringComparison.OrdinalIgnoreCase) >= 0))
                    keys.RemoveAt(i);
            }
        }

        private static void AddRestricted(List<string> keys, string key)
        {
            if (keys == null || string.IsNullOrEmpty(key))
                return;
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            keys.Add(key);
        }

        private static void UnrestrictHq(FactionHQ hq)
        {
            if (hq == null)
                return;
            RemoveRestricted(hq.restrictedAircraft, hq);
            try { RemoveRestricted(hq.NetworkrestrictedAircraft, hq); }
            catch { }
        }

        private static void RemoveRestricted(List<string> keys, FactionHQ hq)
        {
            if (keys == null || keys.Count == 0)
                return;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                string k = keys[i];
                if (string.IsNullOrEmpty(k))
                    continue;
                if (string.Equals(k, Service.JsonKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, Service.OaJsonKey, StringComparison.OrdinalIgnoreCase)
                    || k.IndexOf("MiG-15S", StringComparison.OrdinalIgnoreCase) >= 0
                    || k.IndexOf("MIG-15S", StringComparison.OrdinalIgnoreCase) >= 0
                    || k.IndexOf("OA-27C", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    keys.RemoveAt(i);
                    continue;
                }
                if (!Service.HqIsPala(hq)
                    && (string.Equals(k, Service.OaDJsonKey, StringComparison.OrdinalIgnoreCase)
                        || k.IndexOf("OA-27D", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    keys.RemoveAt(i);
                    continue;
                }
                if (!Service.HqIsBdf(hq)
                    && (string.Equals(k, Service.OaEJsonKey, StringComparison.OrdinalIgnoreCase)
                        || k.IndexOf("OA-27E", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    keys.RemoveAt(i);
                }
            }
        }

        internal static void EnsureAllHangars()
        {
            Hangar[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<Hangar>(); }
            catch { all = null; }
            if (all == null)
                return;
            for (int i = 0; i < all.Length; i++)
                EnsureOnHangar(all[i]);
        }

        internal static void MergeInto(List<AircraftDefinition> dest, Airbase airbase)
        {
            if (dest == null || airbase == null)
                return;
            List<Hangar> hangars = null;
            try { hangars = airbase.hangars; }
            catch { hangars = null; }
            if (hangars != null)
            {
                for (int i = 0; i < hangars.Count; i++)
                    EnsureOnHangar(hangars[i]);
            }
            if (!HasFixedWingPad(airbase))
            {
                RemoveOursFromList(dest);
                return;
            }
            AddCloneToList(dest, FindOaDef());
            if (Service.LocalPlayerMaySelectOaD())
                AddCloneToList(dest, FindOaDDef());
            else
                StripKeyFromList(dest, Service.OaDJsonKey);
            if (Service.LocalPlayerMaySelectOaE())
                AddCloneToList(dest, FindOaEDef());
            else
                StripKeyFromList(dest, Service.OaEJsonKey);
        }

        private static void AddCloneToList(List<AircraftDefinition> dest, AircraftDefinition def)
        {
            if (dest == null || def == null || Service.IsDonorDef(def))
                return;
            if (ListContains(dest, def))
                return;
            dest.Add(def);
        }

        internal static void InjectMenu(AircraftSelectionMenu menu, Airbase airbase)
        {
            LoadoutLock.NoteMenu(menu);
            if (menu == null || SelectionField == null)
                return;
            List<AircraftDefinition> sel = SelectionField.GetValue(menu) as List<AircraftDefinition>;
            if (sel == null)
            {
                sel = new List<AircraftDefinition>(8);
                SelectionField.SetValue(menu, sel);
            }
            if (airbase != null)
                MergeInto(sel, airbase);
            else
            {
                AddCloneToList(sel, FindOaDef());
                if (Service.LocalPlayerMaySelectOaD())
                    AddCloneToList(sel, FindOaDDef());
                else
                    StripKeyFromList(sel, Service.OaDJsonKey);
                if (Service.LocalPlayerMaySelectOaE())
                    AddCloneToList(sel, FindOaEDef());
                else
                    StripKeyFromList(sel, Service.OaEJsonKey);
            }
        }

        internal static bool HangarHasOurs(Hangar hangar)
        {
            if (hangar == null || AvailableAircraftField == null)
                return false;
            AircraftDefinition[] arr = AvailableAircraftField.GetValue(hangar) as AircraftDefinition[];
            if (arr == null)
                return false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (Service.IsOursDef(arr[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Occupied only by a live networked aircraft on this pad, or a short
        /// reserve after a successful TrySpawn. Hangar.Available and leftover
        /// spawnedObject (preview / dummy) are not occupancy — empty pads start
        /// Available=false on clients and still show the no-hangars panel.
        /// </summary>
        internal static bool IsPadFree(Hangar hangar)
        {
            if (hangar == null)
                return false;
            if (IsRubble(hangar.name))
                return false;
            if (IsHelipad(hangar))
                return false;
            try
            {
                if (hangar.Disabled)
                    return false;
            }
            catch { }
            if (IsReserved(hangar))
                return false;
            return !HasLiveOccupant(hangar);
        }

        private static bool HasLiveOccupant(Hangar hangar)
        {
            if (hangar == null)
                return false;
            if (SpawnedObjectField != null)
            {
                try
                {
                    GameObject go = SpawnedObjectField.GetValue(hangar) as GameObject;
                    Aircraft spawned = AircraftOn(go);
                    if (spawned != null && Service.IsLiveAircraft(spawned))
                        return true;
                }
                catch { }
            }
            try
            {
                Unit u = hangar.GetUnit();
                if (u != null && u)
                {
                    Aircraft parked = u as Aircraft;
                    if (parked != null && Service.IsLiveAircraft(parked))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static Aircraft AircraftOn(GameObject go)
        {
            if (go == null || !go)
                return null;
            Aircraft ac = go.GetComponent<Aircraft>();
            if (ac != null)
                return ac;
            try
            {
                ac = go.GetComponentInParent<Aircraft>();
                if (ac != null)
                    return ac;
            }
            catch { }
            try { return go.GetComponentInChildren<Aircraft>(true); }
            catch { return null; }
        }

        internal static bool AnyFreeHangar(Airbase airbase)
        {
            if (airbase == null)
                return true;
            List<Hangar> hangars = null;
            try { hangars = airbase.hangars; }
            catch { hangars = null; }
            if (hangars == null || hangars.Count == 0)
                return true;
            bool sawFixed = false;
            bool sawHeliOnly = false;
            for (int i = 0; i < hangars.Count; i++)
            {
                Hangar h = hangars[i];
                if (h == null)
                    continue;
                if (IsRubble(h.name))
                    continue;
                try
                {
                    if (h.Disabled)
                        continue;
                }
                catch { }
                if (IsHelipad(h))
                {
                    sawHeliOnly = true;
                    EnsureOnHangar(h);
                    continue;
                }
                EnsureOnHangar(h);
                sawFixed = true;
                if (IsPadFree(h))
                    return true;
            }
            if (sawFixed)
                return false;
            return !sawHeliOnly;
        }

        internal static void Reserve(Hangar hangar)
        {
            if (hangar == null)
                return;
            ReservedUntil[hangar.GetInstanceID()] = Time.unscaledTime + 2.5f;
        }

        private static bool IsReserved(Hangar hangar)
        {
            if (hangar == null)
                return false;
            int id = hangar.GetInstanceID();
            float until;
            if (!ReservedUntil.TryGetValue(id, out until))
                return false;
            if (Time.unscaledTime < until)
                return true;
            ReservedUntil.Remove(id);
            return false;
        }

        internal static bool PadBlockedByOurs(Hangar hangar)
        {
            Transform spawn = null;
            try { spawn = hangar.GetSpawnTransform(); }
            catch { spawn = null; }
            if (spawn == null)
                return false;
            float radius = 12f;
            if (ClearDistanceField != null)
            {
                try
                {
                    float c = (float)ClearDistanceField.GetValue(hangar);
                    if (c > 8f)
                        radius = c;
                }
                catch { }
            }
            float r2 = radius * radius;
            Vector3 at = spawn.position;
            List<Aircraft> all = null;
            try { all = UnitRegistry.allAircraft; }
            catch { all = null; }
            if (all == null)
                return false;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i];
                if (ac == null || !Service.IsLiveAircraft(ac))
                    continue;
                if (!Service.InHangarHold(ac) && !NearPadSlow(ac))
                    continue;
                try
                {
                    if ((ac.transform.position - at).sqrMagnitude <= r2)
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static bool NearPadSlow(Aircraft ac)
        {
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            if (alt >= 18f)
                return false;
            return Service.GroundSpeed(ac) < 10f;
        }

        internal static bool ListContains(List<AircraftDefinition> list, AircraftDefinition def)
        {
            if (list == null || def == null)
                return false;
            string key = def.jsonKey;
            for (int i = 0; i < list.Count; i++)
            {
                AircraftDefinition cur = list[i];
                if (cur == null)
                    continue;
                if (object.ReferenceEquals(cur, def))
                    return true;
                if (!string.IsNullOrEmpty(key) && string.Equals(cur.jsonKey, key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ArrayContains(AircraftDefinition[] arr, AircraftDefinition def)
        {
            if (arr == null || def == null)
                return false;
            string key = def.jsonKey;
            for (int i = 0; i < arr.Length; i++)
            {
                AircraftDefinition cur = arr[i];
                if (cur == null)
                    continue;
                if (object.ReferenceEquals(cur, def))
                    return true;
                if (!string.IsNullOrEmpty(key) && string.Equals(cur.jsonKey, key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static bool IsHelipad(Hangar hangar)
        {
            if (hangar == null)
                return false;
            if (NameLooksHelipad(hangar.name))
                return true;
            try
            {
                Transform t = hangar.transform;
                int guard = 0;
                while (t != null && guard < 6)
                {
                    if (NameLooksHelipad(t.name))
                        return true;
                    t = t.parent;
                    guard++;
                }
            }
            catch { }
            return false;
        }

        private static bool NameLooksHelipad(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string n = name.ToLowerInvariant();
            return n.IndexOf("helipad", StringComparison.Ordinal) >= 0
                || n.IndexOf("heli_pad", StringComparison.Ordinal) >= 0
                || n.IndexOf("heli-pad", StringComparison.Ordinal) >= 0
                || n.IndexOf("helicopterpad", StringComparison.Ordinal) >= 0
                || n.IndexOf("直升机坪", StringComparison.Ordinal) >= 0;
        }

        internal static bool HasFixedWingPad(Airbase airbase)
        {
            if (airbase == null)
                return false;
            List<Hangar> hangars = null;
            try { hangars = airbase.hangars; }
            catch { hangars = null; }
            if (hangars == null)
                return false;
            for (int i = 0; i < hangars.Count; i++)
            {
                Hangar h = hangars[i];
                if (h == null || IsRubble(h.name) || IsHelipad(h))
                    continue;
                try
                {
                    if (h.Disabled)
                        continue;
                }
                catch { }
                return true;
            }
            return false;
        }

        private static void StripFromHangar(Hangar hangar)
        {
            if (hangar == null || AvailableAircraftField == null)
                return;
            AircraftDefinition[] cur = AvailableAircraftField.GetValue(hangar) as AircraftDefinition[];
            if (cur == null || cur.Length == 0)
                return;
            int keep = 0;
            for (int i = 0; i < cur.Length; i++)
            {
                if (cur[i] == null)
                    continue;
                if (Service.IsOursDef(cur[i]) && !Service.IsDonorDef(cur[i]))
                    continue;
                cur[keep] = cur[i];
                keep++;
            }
            if (keep == cur.Length)
                return;
            AircraftDefinition[] next = new AircraftDefinition[keep];
            if (keep > 0)
                Array.Copy(cur, next, keep);
            AvailableAircraftField.SetValue(hangar, next);
        }

        private static void RemoveOursFromList(List<AircraftDefinition> dest)
        {
            if (dest == null || dest.Count == 0)
                return;
            for (int i = dest.Count - 1; i >= 0; i--)
            {
                if (Service.IsOursDef(dest[i]) && !Service.IsDonorDef(dest[i]))
                    dest.RemoveAt(i);
            }
        }

        private static bool IsRubble(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string n = name.ToLowerInvariant();
            return n.IndexOf("rubble", StringComparison.Ordinal) >= 0
                || n.IndexOf("destroy", StringComparison.Ordinal) >= 0
                || n.IndexOf("wreck", StringComparison.Ordinal) >= 0;
        }

        internal static Airbase MenuAirbase(AircraftSelectionMenu menu)
        {
            if (menu == null || MenuAirbaseField == null)
                return null;
            try { return MenuAirbaseField.GetValue(menu) as Airbase; }
            catch { return null; }
        }
    }

    /// <summary>
    /// Vanilla Fly uses OwnsAirframe + loadoutUnaffordable + insufficientWarheads
    /// (Update / UpdateReadouts), not CanFlyAircraft. Shared donor prefab leaves
    /// selectedType on Aryx_PropAttacker1 unless we re-bind after SpawnPreview.
    /// UpdateReadouts also forces fly off when preview is null — refresh after that.
    /// </summary>
    internal static class OaFlyButton
    {
        private static readonly FieldInfo PreviewField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "previewAircraft");
        private static readonly FieldInfo SelectedTypeField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "selectedType");
        private static readonly FieldInfo SelectionField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "aircraftSelection");
        private static readonly FieldInfo IndexField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "selectionIndex");
        private static readonly FieldInfo FlyButtonField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "flyButton");
        private static readonly FieldInfo LocalPlayerField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "localPlayer");
        private static readonly FieldInfo LoadoutUnaffField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "loadoutUnaffordable");
        private static readonly FieldInfo InsuffWarField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "insufficientWarheads");

        internal static void BindPreviewDefinition(AircraftSelectionMenu menu)
        {
            if (menu == null)
                return;
            AircraftDefinition intended = ResolveListSelection(menu);
            if (intended == null || !Service.IsOursDef(intended))
                return;
            Aircraft preview = null;
            try
            {
                if (PreviewField != null)
                    preview = PreviewField.GetValue(menu) as Aircraft;
            }
            catch { preview = null; }
            LoadoutLock.NoteHangarDef(intended);
            if (preview != null)
            {
                try
                {
                    Unit u = preview;
                    u.definition = intended;
                }
                catch { }
                LoadoutLock.NoteSelectorAircraft(preview);
            }
            if (SelectedTypeField != null)
            {
                try { SelectedTypeField.SetValue(menu, intended); }
                catch { }
            }
        }

        internal static void Refresh(AircraftSelectionMenu menu)
        {
            if (menu == null || FlyButtonField == null)
                return;
            AircraftDefinition sel = ResolveSelection(menu);
            if (sel == null || !Service.IsOursDef(sel))
                return;
            Button fly = FlyButtonField.GetValue(menu) as Button;
            if (fly == null)
                return;
            Player player = LocalPlayerField != null
                ? LocalPlayerField.GetValue(menu) as Player
                : null;
            if (player == null)
                return;
            bool owns = InventorySplit.CountOwned(player, sel, true) > 0;
            bool unaff = false;
            bool warheads = false;
            try
            {
                Behaviour unaffImg = LoadoutUnaffField != null
                    ? LoadoutUnaffField.GetValue(menu) as Behaviour
                    : null;
                Behaviour warImg = InsuffWarField != null
                    ? InsuffWarField.GetValue(menu) as Behaviour
                    : null;
                if (unaffImg != null)
                    unaff = unaffImg.enabled;
                if (warImg != null)
                    warheads = warImg.enabled;
            }
            catch { }
            fly.interactable = owns && !unaff && !warheads;
        }

        private static AircraftDefinition ResolveSelection(AircraftSelectionMenu menu)
        {
            AircraftDefinition sel = null;
            if (SelectedTypeField != null)
            {
                try { sel = SelectedTypeField.GetValue(menu) as AircraftDefinition; }
                catch { sel = null; }
            }
            if (sel != null && Service.IsOursDef(sel))
                return sel;
            return ResolveListSelection(menu);
        }

        private static AircraftDefinition ResolveListSelection(AircraftSelectionMenu menu)
        {
            if (SelectionField == null || IndexField == null)
                return null;
            try
            {
                List<AircraftDefinition> list =
                    SelectionField.GetValue(menu) as List<AircraftDefinition>;
                int idx = (int)IndexField.GetValue(menu);
                if (list != null && idx >= 0 && idx < list.Count)
                    return list[idx];
            }
            catch { }
            return null;
        }
    }

    [HarmonyPatch(typeof(Airbase), "AddHangar")]
    internal static class Patch_MiG15S_AddHangar
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Hangar hangar)
        {
            HangarInject.EnsureOnHangar(hangar);
        }
    }

    [HarmonyPatch(typeof(Hangar), "GetAvailableAircraft")]
    internal static class Patch_MiG15S_HangarAvailable
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Hangar __instance)
        {
            HangarInject.EnsureOnHangar(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Hangar __instance, ref AircraftDefinition[] __result)
        {
            HangarInject.EnsureOnHangar(__instance);
            if (AvailableAircraftField() == null)
                return;
            AircraftDefinition[] field = AvailableAircraftField().GetValue(__instance) as AircraftDefinition[];
            if (field != null && field.Length > 0)
                __result = field;
        }

        private static FieldInfo AvailableAircraftField()
        {
            return AccessTools.Field(typeof(Hangar), "availableAircraft");
        }
    }

    [HarmonyPatch(typeof(Hangar), "CanSpawnAircraft")]
    internal static class Patch_MiG15S_HangarCanSpawn
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Hangar __instance)
        {
            HangarInject.EnsureOnHangar(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Hangar __instance, AircraftDefinition definition, ref bool __result)
        {
            if (definition == null || __instance == null)
                return;
            if (!Service.LocalPlayerMaySelectExclusive(definition))
            {
                __result = false;
                return;
            }
            if (!Service.IsOursDef(definition))
                return;
            if (HangarInject.IsHelipad(__instance))
            {
                __result = false;
                return;
            }
            HangarInject.EnsureOnHangar(__instance);
            if (!HangarInject.HangarHasOurs(__instance))
                return;
            if (!HangarInject.IsPadFree(__instance))
            {
                __result = false;
                return;
            }
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Airbase), "GetAvailableAircraft")]
    internal static class Patch_MiG15S_AirbaseAvailable
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Airbase __instance, List<AircraftDefinition> __result)
        {
            if (__instance == null)
                return;
            try
            {
                if (__instance.disabled)
                    return;
            }
            catch { }
            HangarInject.MergeInto(__result, __instance);
        }
    }

    [HarmonyPatch(typeof(Airbase), "TrySpawnAircraft")]
    internal static class Patch_MiG15S_AirbaseTrySpawn
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(AircraftDefinition definition)
        {
            if (definition == null)
                return;
            LoadoutLock.BeginVet(definition);
            LoadoutLock.NoteHangarDef(definition);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            LoadoutLock.EndVet();
        }
    }

    [HarmonyPatch(typeof(Airbase), "CanSpawnAircraft")]
    internal static class Patch_MiG15S_AirbaseCanSpawn
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Airbase __instance, AircraftDefinition definition, ref bool __result)
        {
            if (__instance == null || definition == null)
                return;
            if (!Service.LocalPlayerMaySelectExclusive(definition))
            {
                __result = false;
                return;
            }
            if (!Service.IsOursDef(definition))
                return;
            __result = HangarInject.AnyFreeHangar(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "Refresh")]
    internal static class Patch_MiG15S_MenuRefresh
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionMenu __instance, Airbase airbase)
        {
            HangarInject.InjectMenu(__instance, airbase);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "SpawnPreview")]
    internal static class Patch_MiG15S_SpawnPreview
    {
        private static readonly FieldInfo PreviewField =
            AccessTools.Field(typeof(AircraftSelectionMenu), "previewAircraft");

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            Service.BeginHangarPreview();
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(AircraftSelectionMenu __instance)
        {
            Aircraft preview = null;
            try
            {
                if (PreviewField != null && __instance != null)
                    preview = PreviewField.GetValue(__instance) as Aircraft;
            }
            catch { preview = null; }
            Service.EndHangarPreview(preview);
            if (__instance != null)
            {
                OaFlyButton.BindPreviewDefinition(__instance);
                OaFlyButton.Refresh(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "DestroyPreviewAircraft")]
    internal static class Patch_MiG15S_DestroyPreview
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Service.ClearHangarPreview();
        }
    }

    [HarmonyPatch(typeof(Hangar), "TrySpawnAircraft")]
    internal static class Patch_MiG15S_HangarTrySpawn
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(
            Hangar __instance,
            AircraftDefinition definition,
            ref Airbase.TrySpawnResult __result)
        {
            if (__instance == null || definition == null)
                return true;
            LoadoutLock.BeginVet(definition);
            LoadoutLock.NoteHangarDef(definition);
            if (!Service.IsOursDef(definition))
                return true;
            if (!Service.LocalPlayerMaySelectExclusive(definition))
            {
                __result = default(Airbase.TrySpawnResult);
                return false;
            }
            if (HangarInject.IsHelipad(__instance))
            {
                __result = default(Airbase.TrySpawnResult);
                return false;
            }
            HangarInject.EnsureOnHangar(__instance);
            if (HangarInject.IsPadFree(__instance) && !HangarInject.PadBlockedByOurs(__instance))
                return true;
            __result = default(Airbase.TrySpawnResult);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            Hangar __instance,
            AircraftDefinition definition,
            Airbase.TrySpawnResult __result)
        {
            if (__instance == null || definition == null)
                return;
            LoadoutLock.EndVet();
            if (!Service.IsOursDef(definition))
                return;
            if (__result.Allowed)
                HangarInject.Reserve(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "CanFlyAircraft")]
    internal static class Patch_MiG15S_CanFly
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionMenu __instance, AircraftDefinition definition, ref bool __result)
        {
            if (definition == null)
                return;
            if (!Service.IsOursDef(definition))
                return;
            if (!Service.LocalPlayerMaySelectExclusive(definition))
            {
                __result = false;
                return;
            }
            if (!Service.MeetsRank(definition))
            {
                __result = false;
                return;
            }
            Airbase airbase = HangarInject.MenuAirbase(__instance);
            HangarInject.InjectMenu(__instance, airbase);
            if (airbase == null)
            {
                if (!__result)
                    __result = true;
                return;
            }
            __result = HangarInject.AnyFreeHangar(airbase);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionButton), "CheckAvailable")]
    internal static class Patch_MiG15S_ButtonAvailable
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionButton __instance, ref bool __result)
        {
            if (__instance == null || !Service.IsOursDef(__instance.definition))
                return;
            if (!Service.LocalPlayerMaySelectExclusive(__instance.definition))
            {
                __result = false;
                return;
            }
            if (!Service.MeetsRank(__instance.definition))
            {
                __result = false;
                return;
            }
            if (__result)
                return;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(UnitDefinition), "IsAllowed")]
    internal static class Patch_MiG15S_IsAllowed
    {
        [HarmonyPostfix]
        private static void Postfix(UnitDefinition __instance, ref bool __result)
        {
            if (__instance == null)
                return;
            AircraftDefinition def = __instance as AircraftDefinition;
            if (Service.IsOaConventionalDef(__instance))
            {
                __result = Service.LocalPlayerMaySelectExclusive(__instance) && Service.MeetsRank(def);
                return;
            }
            if (__result)
                return;
            if (Service.IsOursUnit(__instance) && Service.MeetsRank(def))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(UnitDefinition), "NotAllowed")]
    internal static class Patch_MiG15S_NotAllowed
    {
        [HarmonyPostfix]
        private static void Postfix(UnitDefinition __instance, ref bool __result)
        {
            if (__instance == null)
                return;
            AircraftDefinition def = __instance as AircraftDefinition;
            if (Service.IsOaConventionalDef(__instance))
            {
                __result = !(Service.LocalPlayerMaySelectExclusive(__instance) && Service.MeetsRank(def));
                return;
            }
            if (!__result)
                return;
            if (Service.IsOursUnit(__instance) && Service.MeetsRank(def))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionButton), "Setup")]
    internal static class Patch_MiG15S_HangarIconSetup
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(AircraftSelectionButton __instance, ref bool owned, ref bool available)
        {
            if (__instance == null || !Service.IsOursDef(__instance.definition))
                return;
            if (!Service.LocalPlayerMaySelectExclusive(__instance.definition))
            {
                available = false;
                return;
            }
            Player p = null;
            try { GameManager.GetLocalPlayer(out p); }
            catch { p = null; }
            if (p != null)
                owned = InventorySplit.CountOwned(p, __instance.definition, true) > 0;
            if (!Service.MeetsRank(__instance.definition))
            {
                available = false;
                return;
            }
            available = true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionButton __instance)
        {
            HangarInject.ApplyShortIcon(__instance);
            HangarInject.ApplyOaDVisibility(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionButton), "Update")]
    internal static class Patch_MiG15S_HangarIconUpdate
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionButton __instance)
        {
            HangarInject.ApplyShortIcon(__instance);
            HangarInject.ApplyOaDVisibility(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "UpdateReadouts")]
    internal static class Patch_Oa_FlyAfterReadouts
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionMenu __instance)
        {
            OaFlyButton.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), "Update")]
    internal static class Patch_Oa_FlyAfterUpdate
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftSelectionMenu __instance)
        {
            OaFlyButton.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(FactionHQ), "OnMissionLoad")]
    internal static class Patch_MiG15S_HqMissionLoad
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            HangarInject.EnsureAiSupply();
        }
    }

    [HarmonyPatch(typeof(FactionHQ), "DeployAIAircraft")]
    internal static class Patch_MiG15S_DeployAi
    {
        [HarmonyPrefix]
        private static void Prefix(FactionHQ __instance)
        {
            HangarInject.EnsureHq(__instance);
        }
    }

    [HarmonyPatch(typeof(AircraftParameters), "GetRandomStandardLoadout")]
    internal static class Patch_MiG15S_AiLoadout
    {
        [HarmonyPrefix]
        private static bool Prefix(AircraftParameters __instance, AircraftDefinition definition, ref StandardLoadout __result)
        {
            if (!Service.IsOursDef(definition))
                return true;
            if (definition.unitPrefab != null)
                return true;
            __result = FirstEnabledLoadout(__instance);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AircraftParameters __instance, AircraftDefinition definition, ref StandardLoadout __result)
        {
            if (__result != null || !Service.IsOursDef(definition))
                return;
            __result = FirstEnabledLoadout(__instance);
        }

        private static StandardLoadout FirstEnabledLoadout(AircraftParameters p)
        {
            if (p == null || p.StandardLoadouts == null)
                return null;
            for (int i = 0; i < p.StandardLoadouts.Length; i++)
            {
                StandardLoadout sl = p.StandardLoadouts[i];
                if (sl != null && !sl.disabled)
                    return sl;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(StandardLoadout), "AllowedByHQ")]
    internal static class Patch_MiG15S_AllowedByHq
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(WeaponManager weaponManager, ref bool __result)
        {
            if (__result || weaponManager == null)
                return;
            Aircraft ac = weaponManager.GetComponentInParent<Aircraft>();
            if (Service.IsOurs(ac))
                __result = true;
        }
    }
}
