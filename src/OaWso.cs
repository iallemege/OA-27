using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// OA-27 family WSO. No Oritasy types.
    /// Aboard: dump incoming missiles, Y/N flares, Y/N target lock, gunsight lead, GLOC brace.
    /// Eject: C punches forever; D/E punch once then real bail.
    /// </summary>
    internal static class OaWso
    {
        private const float DumpSec = 20f;
        private const float DumpRadiusM = 10000f;
        private const float FlareGap = 1.05f;
        private const float CueGap = 2.5f;
        private const float CueRangeM = 12000f;
        private const float CueDot = 0.35f;
        private const float AskTimeout = 8f;
        private const float AskDenyGap = 12f;

        private enum AskKind
        {
            None = 0,
            Lock = 1,
            Flares = 2
        }

        private static readonly FieldInfo MissileTargetField =
            AccessTools.Field(typeof(Missile), "target");
        private static readonly FieldInfo SeekerField =
            AccessTools.Field(typeof(Missile), "seeker");
        private static readonly FieldInfo SeekerTargetField =
            AccessTools.Field(typeof(MissileSeeker), "targetUnit");
        private static readonly FieldInfo IrTargetField =
            AccessTools.Field(typeof(IRSeeker), "IRTarget");
        private static readonly FieldInfo ArhLock =
            AccessTools.Field(typeof(ARHSeeker), "radarLockEstablished");
        private static readonly FieldInfo ArhAchieved =
            AccessTools.Field(typeof(ARHSeeker), "achievedLock");

        private static readonly Dictionary<int, float> NextDump =
            new Dictionary<int, float>(8);
        private static readonly Dictionary<int, float> NextFlare =
            new Dictionary<int, float>(8);
        private static readonly Dictionary<int, float> NextCue =
            new Dictionary<int, float>(8);
        private static readonly List<Missile> Scratch = new List<Missile>(64);
        private static GUIStyle _askStyle;
        private static GUIStyle _askSub;
        private static AskKind _ask;
        private static Unit _askUnit;
        private static Aircraft _askAc;
        private static float _askUntil;
        private static float _lockDenyUntil;
        private static float _flareDenyUntil;

        internal static bool Aboard(Aircraft ac)
        {
            return Service.HasOaWso(ac);
        }

        private static int _aboardFrame = -1;
        private static bool _aboardCached;

        internal static bool CachedLocalAboard()
        {
            int f = Time.frameCount;
            if (f == _aboardFrame)
                return _aboardCached;
            _aboardFrame = f;
            _aboardCached = LocalAboard();
            return _aboardCached;
        }

        internal static bool LocalAboard()
        {
            Aircraft ac;
            if (!GameManager.GetLocalAircraft(out ac) || ac == null)
                return false;
            if (!Service.IsLocalPlayerAircraft(ac))
                return false;
            return Aboard(ac);
        }

        internal static void Tick(Aircraft ac)
        {
            if (ac == null || !Service.IsOaFamilyClone(ac))
                return;
            if (!Service.IsLiveAircraft(ac) || !Plugin.IsRuntime(ac))
                return;
            if (!Service.IsLocalPlayerAircraft(ac))
                return;
            if (!Aboard(ac))
            {
                ClearAsk();
                return;
            }
            TickPendingAsk();
            TickDump(ac);
            TickFlares(ac);
            TickCue(ac);
        }

        internal static void Draw()
        {
            Aircraft ac;
            if (!GameManager.GetLocalAircraft(out ac) || ac == null)
                return;
            if (!Service.IsOaFamilyClone(ac) || !Service.IsLiveAircraft(ac))
                return;
            DrawAsk();
        }

        private static void TickDump(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            float due;
            if (!NextDump.TryGetValue(id, out due))
            {
                NextDump[id] = Time.unscaledTime + 2f;
                return;
            }
            if (Time.unscaledTime < due)
                return;
            NextDump[id] = Time.unscaledTime + DumpSec;
            DumpHostiles(ac);
        }

        private static void TickPendingAsk()
        {
            if (_ask == AskKind.None)
                return;
            float now = Time.unscaledTime;
            if (now > _askUntil)
            {
                DenyAsk();
                return;
            }
            try
            {
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    AcceptAsk();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.N))
                {
                    DenyAsk();
                    return;
                }
            }
            catch { }
        }

        private static void StartAsk(AskKind kind, Aircraft ac, Unit unit)
        {
            _ask = kind;
            _askAc = ac;
            _askUnit = unit;
            _askUntil = Time.unscaledTime + AskTimeout;
        }

        private static void AcceptAsk()
        {
            Aircraft ac = _askAc;
            Unit unit = _askUnit;
            AskKind kind = _ask;
            ClearAsk();
            if (kind == AskKind.Lock)
                ApplyLock(ac, unit);
            else if (kind == AskKind.Flares)
                PopFlaresKeepStation(ac);
        }

        private static void DenyAsk()
        {
            float now = Time.unscaledTime;
            if (_ask == AskKind.Lock)
                _lockDenyUntil = now + AskDenyGap;
            else if (_ask == AskKind.Flares)
                _flareDenyUntil = now + AskDenyGap;
            ClearAsk();
        }

        private static void ClearAsk()
        {
            _ask = AskKind.None;
            _askAc = null;
            _askUnit = null;
            _askUntil = 0f;
        }

        private static void ApplyLock(Aircraft ac, Unit pick)
        {
            if (ac == null || pick == null)
                return;
            try
            {
                if (ac.weaponManager != null)
                    ac.weaponManager.AddTargetList(pick);
            }
            catch { }
            Pilot wso = Service.FindOaWsoPilot(ac);
            if (wso != null)
            {
                try { wso.SetPrimaryTarget(pick); }
                catch { }
            }
        }

        private static void DrawAsk()
        {
            if (_ask == AskKind.None)
                return;
            if (Event.current != null && Event.current.type != EventType.Repaint)
                return;
            EnsureAskStyles();
            string title;
            string sub;
            if (_ask == AskKind.Lock)
            {
                title = "WSO: lock this target?";
                string name = "";
                try
                {
                    if (_askUnit != null && _askUnit.definition != null)
                        name = _askUnit.definition.unitName;
                }
                catch { }
                if (string.IsNullOrEmpty(name))
                    name = "Hostile";
                sub = name + "   Y yes   N no";
            }
            else
            {
                title = "WSO: dump flares?";
                sub = "Will not switch your selected countermeasure.  Y yes   N no";
            }
            float w = Mathf.Min(720f, Screen.width * 0.88f);
            Rect box = new Rect((Screen.width - w) * 0.5f, 108f, w, 56f);
            int prevDepth = GUI.depth;
            GUI.depth = -997;
            Color prev = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.04f, 0.82f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 1f, 0.55f, 0.95f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 10f, box.y + 4f, box.width - 20f, 24f), title, _askStyle);
            GUI.Label(new Rect(box.x + 10f, box.y + 28f, box.width - 20f, 22f), sub, _askSub);
            GUI.color = prev;
            GUI.depth = prevDepth;
        }

        private static void EnsureAskStyles()
        {
            if (_askStyle != null)
                return;
            _askStyle = new GUIStyle(GUI.skin.label);
            _askStyle.fontSize = 16;
            _askStyle.fontStyle = FontStyle.Bold;
            _askStyle.alignment = TextAnchor.MiddleCenter;
            _askStyle.normal.textColor = new Color(0.85f, 1f, 0.7f, 1f);
            _askSub = new GUIStyle(GUI.skin.label);
            _askSub.fontSize = 13;
            _askSub.alignment = TextAnchor.MiddleCenter;
            _askSub.normal.textColor = new Color(0.92f, 0.95f, 0.82f, 1f);
        }

        private static void TickFlares(Aircraft ac)
        {
            if (_ask != AskKind.None)
                return;
            int id = ac.GetInstanceID();
            float now = Time.unscaledTime;
            float due;
            if (!NextFlare.TryGetValue(id, out due))
                due = 0f;
            if (now < due)
                return;
            NextFlare[id] = now + FlareGap;
            if (now < _flareDenyUntil)
                return;
            if (!InboundMissile(ac))
                return;
            StartAsk(AskKind.Flares, ac, null);
        }

        private static void TickCue(Aircraft ac)
        {
            if (_ask != AskKind.None)
                return;
            int id = ac.GetInstanceID();
            float now = Time.unscaledTime;
            float due;
            if (!NextCue.TryGetValue(id, out due))
                due = 0f;
            if (now < due)
                return;
            NextCue[id] = now + CueGap;
            if (now < _lockDenyUntil)
                return;
            if (HasPlayerLock(ac))
                return;
            Unit pick = NearestHostile(ac);
            if (pick == null)
                return;
            StartAsk(AskKind.Lock, ac, pick);
        }

        private static bool HasPlayerLock(Aircraft ac)
        {
            if (ac == null || ac.weaponManager == null)
                return false;
            try
            {
                List<Unit> list = ac.weaponManager.GetTargetList();
                if (list != null && list.Count > 0)
                    return true;
            }
            catch { }
            return false;
        }

        private static Unit NearestHostile(Aircraft ac)
        {
            List<Unit> units = null;
            try { units = UnitRegistry.allUnits; }
            catch { units = null; }
            if (units == null)
                return null;
            Vector3 origin = ac.transform.position;
            Vector3 fwd = ac.transform.forward;
            float r2 = CueRangeM * CueRangeM;
            Unit best = null;
            float bestSqr = r2;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null || object.ReferenceEquals(u, ac))
                    continue;
                try
                {
                    if (u.disabled)
                        continue;
                }
                catch { continue; }
                if (u is Missile)
                    continue;
                if (!IsHostile(ac, u))
                    continue;
                Vector3 d = u.transform.position - origin;
                float sqr = d.sqrMagnitude;
                if (sqr > r2 || sqr < 80f)
                    continue;
                if (Vector3.Dot(fwd, d.normalized) < CueDot)
                    continue;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = u;
                }
            }
            return best;
        }

        private static bool IsHostile(Aircraft ac, Unit u)
        {
            if (ac == null || u == null)
                return false;
            FactionHQ ah = null;
            FactionHQ uh = null;
            try { ah = ac.NetworkHQ; }
            catch { ah = null; }
            try { uh = u.NetworkHQ; }
            catch { uh = null; }
            if (ah == null || uh == null)
                return false;
            return !object.ReferenceEquals(ah, uh);
        }

        private static void PopFlaresKeepStation(Aircraft ac)
        {
            CountermeasureManager mgr = null;
            byte saved = 0;
            bool have = false;
            try
            {
                mgr = ac.countermeasureManager;
                if (mgr != null)
                {
                    saved = mgr.activeIndex;
                    have = true;
                }
            }
            catch { }
            bool fired = FireNamedCm(ac, "flare");
            if (!fired)
                fired = FireNamedCm(ac, "chaff");
            if (!fired)
            {
                try
                {
                    if (mgr != null)
                        mgr.PopFlares();
                    else
                        ac.Countermeasures(true, 0);
                }
                catch { }
            }
            if (have && mgr != null)
            {
                try { mgr.activeIndex = saved; }
                catch { }
                try { mgr.UpdateHUD(); }
                catch { }
            }
        }

        private static bool FireNamedCm(Aircraft ac, string needle)
        {
            Countermeasure[] cms = null;
            try { cms = ac.GetComponentsInChildren<Countermeasure>(true); }
            catch { cms = null; }
            if (cms == null)
                return false;
            bool any = false;
            for (int i = 0; i < cms.Length; i++)
            {
                Countermeasure c = cms[i];
                if (c == null)
                    continue;
                string n = c.displayName != null ? c.displayName : string.Empty;
                if (n.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                try
                {
                    if (c.ammo <= 0)
                        continue;
                    c.Fire();
                    any = true;
                }
                catch { }
            }
            return any;
        }

        private static bool InboundMissile(Aircraft ac)
        {
            CollectMissiles();
            Vector3 origin = ac.transform.position;
            float r2 = DumpRadiusM * DumpRadiusM;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Missile m = Scratch[i];
                if (m == null)
                    continue;
                try
                {
                    if (m.disabled)
                        continue;
                    if (!Plugin.IsRuntime(m))
                        continue;
                    if ((m.transform.position - origin).sqrMagnitude > r2)
                        continue;
                    if (IsOurShot(m, ac))
                        continue;
                    if (MissileStillOnPylon(m))
                        continue;
                    if (AimedAt(m, ac))
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static int DumpHostiles(Aircraft ac)
        {
            CollectMissiles();
            Vector3 origin = ac.transform.position;
            float r2 = DumpRadiusM * DumpRadiusM;
            int n = 0;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Missile m = Scratch[i];
                if (m == null)
                    continue;
                try
                {
                    if (m.disabled)
                        continue;
                    if (!Plugin.IsRuntime(m))
                        continue;
                    if ((m.transform.position - origin).sqrMagnitude > r2)
                        continue;
                    if (IsOurShot(m, ac))
                        continue;
                    if (MissileStillOnPylon(m))
                        continue;
                    if (!AimedAt(m, ac))
                        continue;
                    BreakLock(m);
                    n++;
                }
                catch { }
            }
            return n;
        }

        private static void CollectMissiles()
        {
            Scratch.Clear();
            List<Unit> units = null;
            try { units = UnitRegistry.allUnits; }
            catch { units = null; }
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    Missile m = units[i] as Missile;
                    if (m != null)
                        Scratch.Add(m);
                }
            }
            if (units != null)
                return;
            Missile[] all = null;
            try { all = UnityEngine.Object.FindObjectsOfType<Missile>(); }
            catch { all = null; }
            if (all == null)
                return;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    Scratch.Add(all[i]);
            }
        }

        private static bool MissileStillOnPylon(Missile m)
        {
            if (m == null)
                return true;
            try
            {
                Transform xf = m.transform;
                if (xf == null || xf.parent == null)
                    return false;
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsOurShot(Missile m, Aircraft ac)
        {
            try
            {
                if (object.ReferenceEquals(m.owner, ac))
                    return true;
            }
            catch { }
            return false;
        }

        private static bool AimedAt(Missile m, Aircraft ac)
        {
            Unit t = null;
            try
            {
                if (MissileTargetField != null)
                    t = MissileTargetField.GetValue(m) as Unit;
            }
            catch { t = null; }
            if (t == null)
                return false;
            if (object.ReferenceEquals(t, ac))
                return true;
            Aircraft other = t as Aircraft;
            if (other == null)
            {
                try { other = t.GetComponentInParent<Aircraft>(); }
                catch { other = null; }
            }
            return object.ReferenceEquals(other, ac);
        }

        private static void BreakLock(Missile m)
        {
            try { m.SetTarget(null); }
            catch { }
            if (MissileTargetField != null)
            {
                try { MissileTargetField.SetValue(m, null); }
                catch { }
            }
            MissileSeeker seeker = null;
            try
            {
                if (SeekerField != null)
                    seeker = SeekerField.GetValue(m) as MissileSeeker;
            }
            catch { seeker = null; }
            if (seeker == null)
                return;
            if (SeekerTargetField != null)
            {
                try { SeekerTargetField.SetValue(seeker, null); }
                catch { }
            }
            IRSeeker ir = seeker as IRSeeker;
            if (ir != null && IrTargetField != null)
            {
                try { IrTargetField.SetValue(ir, null); }
                catch { }
            }
            ARHSeeker arh = seeker as ARHSeeker;
            if (arh != null)
            {
                if (ArhLock != null)
                {
                    try { ArhLock.SetValue(arh, false); }
                    catch { }
                }
                if (ArhAchieved != null)
                {
                    try { ArhAchieved.SetValue(arh, false); }
                    catch { }
                }
            }
        }
    }

    [HarmonyPatch(typeof(TargetCalc), "TargetLeadTime")]
    internal static class Patch_OaWso_Lead
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(GameObject gun, ref int iterations)
        {
            if (gun == null || iterations >= LeadItersHeld)
                return;
            if (!OaWso.CachedLocalAboard())
                return;
            Aircraft local;
            if (!GameManager.GetLocalAircraft(out local) || local == null)
                return;
            if (!object.ReferenceEquals(gun, local.gameObject))
                return;
            iterations = LeadItersHeld;
        }

        private const int LeadItersHeld = 5;
    }

    [HarmonyPatch(typeof(GLOC), "SimulateGLOC")]
    internal static class Patch_OaWso_Gloc
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(GLOC __instance, ref float __result)
        {
            if (__instance == null || __result >= 0.45f)
                return;
            if (!OaWso.CachedLocalAboard())
                return;
            Pilot p = null;
            try { p = __instance.GetComponent<Pilot>(); }
            catch { p = null; }
            if (p == null)
            {
                try { p = __instance.GetComponentInParent<Pilot>(); }
                catch { p = null; }
            }
            Aircraft ac = null;
            try
            {
                if (p != null)
                    ac = p.aircraft;
            }
            catch { ac = null; }
            if (ac == null || !Service.IsLocalPlayerAircraft(ac) || !OaWso.Aboard(ac))
                return;
            if (__result < 0.45f)
                __result = 0.45f;
        }
    }
}
