using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>Clone Alkyon AB-4 afterburner flame / glow onto the MiG-15S nozzle.</summary>
    internal static class Ab4Fx
    {
        private const string FlameName = "MIG15S_AB4Flame";
        private const string GlowName = "MIG15S_AB4Glow";
        private const float VisualScale = 2.4f;

        private static readonly FieldInfo NozzleAfterburners =
            AccessTools.Field(typeof(JetNozzle), "afterburners");
        private static readonly FieldInfo NozzleThrustXf =
            AccessTools.Field(typeof(JetNozzle), "thrustTransform");
        private static readonly FieldInfo NozzleGlow =
            AccessTools.Field(typeof(JetNozzle), "glow");
        private static readonly FieldInfo NozzleEngine =
            AccessTools.Field(typeof(JetNozzle), "engine");
        private static readonly Type AfterburnerType =
            AccessTools.Inner(typeof(JetNozzle), "Afterburner");
        private static readonly FieldInfo AbFlameRenderer =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "flameRenderer") : null;
        private static readonly FieldInfo AbGlowRenderer =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "nozzleGlowRenderer") : null;
        private static readonly FieldInfo AbFlameBright =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "flameBrightness") : null;
        private static readonly FieldInfo AbGlowBright =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "nozzleGlowBrightness") : null;

        private static readonly HashSet<int> Done = new HashSet<int>();
        private static readonly HashSet<int> AcDone = new HashSet<int>();
        private static JetNozzle _donor;
        private static Material _nozzleFixMat;
        private static float _nextDonor;

        internal static void Ensure(Aircraft ac)
        {
            if (ac == null || !Service.IsOurs(ac) || Service.IsOaFamilyClone(ac))
                return;
            if (AcDone.Contains(ac.GetInstanceID()))
                return;
            JetNozzle[] nozzles = null;
            try { nozzles = ac.GetComponentsInChildren<JetNozzle>(true); }
            catch { nozzles = null; }
            if (nozzles == null)
                return;
            for (int i = 0; i < nozzles.Length; i++)
            {
                if (nozzles[i] != null)
                    DestroyBadClones(nozzles[i]);
            }
            RepairPinkNozzles(ac);
            for (int i = 0; i < nozzles.Length; i++)
            {
                JetNozzle n = nozzles[i];
                if (n == null)
                    continue;
                if (HasValidClone(n))
                {
                    Done.Add(n.GetInstanceID());
                    continue;
                }
                if (!Done.Add(n.GetInstanceID()))
                    continue;
                ApplyToNozzle(n, ac);
                if (!HasValidClone(n))
                    Done.Remove(n.GetInstanceID());
            }
            bool ready = true;
            for (int i = 0; i < nozzles.Length; i++)
            {
                if (nozzles[i] != null && !HasValidClone(nozzles[i]))
                {
                    ready = false;
                    break;
                }
            }
            if (ready)
                AcDone.Add(ac.GetInstanceID());
        }

        internal static void SetVisible(Aircraft ac, bool on)
        {
            if (ac == null)
                return;
            Transform[] xs = null;
            try { xs = ac.GetComponentsInChildren<Transform>(true); }
            catch { xs = null; }
            if (xs == null)
                return;
            for (int i = 0; i < xs.Length; i++)
            {
                Transform t = xs[i];
                if (t == null)
                    continue;
                string n = t.name;
                if (n != FlameName && n != GlowName && n != GlowName + "PS")
                    continue;
                Renderer r = t.GetComponent<Renderer>();
                if (MissingMaterial(r))
                {
                    UnityEngine.Object.Destroy(t.gameObject);
                    continue;
                }
                if (t.gameObject.activeSelf != on)
                    t.gameObject.SetActive(on);
                if (r != null)
                    r.enabled = on;
                ParticleSystem ps = t.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    if (on)
                    {
                        if (!ps.isPlaying)
                            ps.Play(true);
                    }
                    else if (ps.isPlaying)
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        internal static bool MissingMaterial(Renderer r)
        {
            if (r == null)
                return true;
            Material[] mats = null;
            try { mats = r.sharedMaterials; }
            catch { mats = null; }
            if (mats == null || mats.Length == 0)
                return true;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || m.shader == null)
                    return true;
                string sn = m.shader.name;
                if (sn == null)
                    return true;
                if (sn.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (sn.IndexOf("Hidden/Internal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void ApplyToNozzle(JetNozzle nozzle, Aircraft ac)
        {
            JetNozzle donor = GetDonor();
            Transform parent = nozzle.transform;
            try
            {
                if (NozzleThrustXf != null)
                {
                    Transform xf = NozzleThrustXf.GetValue(nozzle) as Transform;
                    if (xf != null)
                        parent = xf;
                }
            }
            catch { }

            Renderer flame = CloneNamedRenderer(donor, parent, ac, true);
            Renderer glow = CloneNamedRenderer(donor, parent, ac, false);
            ParticleSystem glowPs = CloneGlowParticles(donor, parent, ac);
            if (glowPs != null && glowPs.isPlaying)
                glowPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            object ab = GetOrCreateAfterburner(nozzle);
            if (ab != null)
            {
                if (AbFlameRenderer != null)
                {
                    try { AbFlameRenderer.SetValue(ab, MissingMaterial(flame) ? null : flame); }
                    catch { }
                }
                if (AbGlowRenderer != null)
                {
                    try { AbGlowRenderer.SetValue(ab, MissingMaterial(glow) ? null : glow); }
                    catch { }
                }
                if (flame != null || glow != null)
                    CopyBright(donor, ab);
            }
            DestroyNamedUnder(nozzle, "MIG15S_ABFlame");
            SetVisible(ac, false);
        }

        private static object GetOrCreateAfterburner(JetNozzle nozzle)
        {
            if (nozzle == null || AfterburnerType == null || NozzleAfterburners == null)
                return null;
            object arrObj = null;
            try { arrObj = NozzleAfterburners.GetValue(nozzle); }
            catch { arrObj = null; }
            Array arr = arrObj as Array;
            object ab = null;
            if (arr != null && arr.Length > 0)
                ab = arr.GetValue(0);
            if (ab != null)
                return ab;
            try { ab = AccessTools.CreateInstance(AfterburnerType); }
            catch { ab = null; }
            if (ab == null)
                return null;
            Array created = Array.CreateInstance(AfterburnerType, 1);
            created.SetValue(ab, 0);
            try { NozzleAfterburners.SetValue(nozzle, created); }
            catch { return null; }
            return ab;
        }

        private static void CopyBright(JetNozzle donor, object ab)
        {
            if (donor == null || ab == null)
                return;
            object srcAb = FirstAfterburner(donor);
            if (srcAb == null)
                return;
            CopyFloat(AbFlameBright, srcAb, ab);
            CopyFloat(AbGlowBright, srcAb, ab);
        }

        private static void CopyFloat(FieldInfo f, object src, object dst)
        {
            if (f == null || src == null || dst == null)
                return;
            try { f.SetValue(dst, f.GetValue(src)); }
            catch { }
        }

        private static object FirstAfterburner(JetNozzle nozzle)
        {
            if (nozzle == null || NozzleAfterburners == null)
                return null;
            try
            {
                Array arr = NozzleAfterburners.GetValue(nozzle) as Array;
                if (arr != null && arr.Length > 0)
                    return arr.GetValue(0);
            }
            catch { }
            return null;
        }

        private static Renderer CloneNamedRenderer(JetNozzle donor, Transform parent, Aircraft ac, bool flame)
        {
            Renderer src = flame ? FindDonorFlame(donor) : FindDonorGlow(donor);
            if (src == null || MissingMaterial(src) || parent == null)
                return null;
            string want = flame ? FlameName : GlowName;
            Transform existing = parent.Find(want);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = UnityEngine.Object.Instantiate(src.gameObject, parent);
                go.name = want;
                StripPhysics(go);
                go.transform.localPosition = flame ? new Vector3(0f, 0f, 0.15f) : Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * VisualScale;
                go.layer = ac.gameObject.layer;
                CopyMaterials(src, go.GetComponent<Renderer>());
            }
            go.SetActive(false);
            Renderer dst = go.GetComponent<Renderer>();
            if (MissingMaterial(dst))
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            return dst;
        }

        private static ParticleSystem CloneGlowParticles(JetNozzle donor, Transform parent, Aircraft ac)
        {
            if (donor == null || parent == null || NozzleGlow == null)
                return null;
            ParticleSystem src = null;
            try { src = NozzleGlow.GetValue(donor) as ParticleSystem; }
            catch { src = null; }
            if (src == null)
                return null;
            Renderer srcR = src.GetComponent<Renderer>();
            if (MissingMaterial(srcR) && srcR != null)
                return null;
            Transform existing = parent.Find(GlowName + "PS");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = UnityEngine.Object.Instantiate(src.gameObject, parent);
                go.name = GlowName + "PS";
                StripPhysics(go);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * VisualScale;
                go.layer = ac.gameObject.layer;
            }
            go.SetActive(false);
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Renderer dstR = go.GetComponent<Renderer>();
            if (MissingMaterial(dstR))
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            if (ps != null && ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static Renderer FindDonorFlame(JetNozzle donor)
        {
            object ab = FirstAfterburner(donor);
            if (ab != null && AbFlameRenderer != null)
            {
                try
                {
                    Renderer r = AbFlameRenderer.GetValue(ab) as Renderer;
                    if (r != null && !MissingMaterial(r))
                        return r;
                }
                catch { }
            }
            return null;
        }

        private static Renderer FindDonorGlow(JetNozzle donor)
        {
            object ab = FirstAfterburner(donor);
            if (ab != null && AbGlowRenderer != null)
            {
                try
                {
                    Renderer r = AbGlowRenderer.GetValue(ab) as Renderer;
                    if (r != null && !MissingMaterial(r))
                        return r;
                }
                catch { }
            }
            return null;
        }

        private static void StripPhysics(GameObject go)
        {
            if (go == null)
                return;
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    UnityEngine.Object.Destroy(cols[i]);
            }
            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                    UnityEngine.Object.Destroy(rbs[i]);
            }
        }

        private static void CopyMaterials(Renderer src, Renderer dst)
        {
            if (src == null || dst == null)
                return;
            try { dst.sharedMaterials = src.sharedMaterials; }
            catch { }
        }

        private static bool HasValidClone(JetNozzle nozzle)
        {
            if (nozzle == null)
                return false;
            Transform[] xs = nozzle.GetComponentsInChildren<Transform>(true);
            if (xs == null)
                return false;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] == null || xs[i].name != FlameName)
                    continue;
                Renderer r = xs[i].GetComponent<Renderer>();
                if (!MissingMaterial(r))
                    return true;
            }
            return false;
        }

        private static void DestroyBadClones(JetNozzle nozzle)
        {
            if (nozzle == null)
                return;
            Transform[] xs = nozzle.GetComponentsInChildren<Transform>(true);
            if (xs == null)
                return;
            for (int i = 0; i < xs.Length; i++)
            {
                Transform t = xs[i];
                if (t == null)
                    continue;
                string n = t.name;
                if (n != FlameName && n != GlowName && n != GlowName + "PS" && n != "MIG15S_ABFlame")
                    continue;
                Renderer r = t.GetComponent<Renderer>();
                if (n == "MIG15S_ABFlame" || MissingMaterial(r))
                    UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        private static void DestroyNamedUnder(JetNozzle nozzle, string name)
        {
            if (nozzle == null || string.IsNullOrEmpty(name))
                return;
            Transform[] xs = nozzle.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] == null || xs[i].name != name)
                    continue;
                UnityEngine.Object.Destroy(xs[i].gameObject);
            }
        }

        private static void RepairPinkNozzles(Aircraft ac)
        {
            if (ac == null)
                return;
            JetNozzle[] nozzles = null;
            try { nozzles = ac.GetComponentsInChildren<JetNozzle>(true); }
            catch { nozzles = null; }
            if (nozzles == null)
                return;
            Renderer donorMat = FindLoadedEngineRenderer();
            for (int n = 0; n < nozzles.Length; n++)
            {
                JetNozzle nozzle = nozzles[n];
                if (nozzle == null)
                    continue;
                Renderer[] rs = nozzle.GetComponentsInChildren<Renderer>(true);
                if (rs == null)
                    continue;
                for (int i = 0; i < rs.Length; i++)
                {
                    Renderer r = rs[i];
                    if (r == null || !MissingMaterial(r))
                        continue;
                    if (r is ParticleSystemRenderer)
                        continue;
                    string nm = r.gameObject.name;
                    if (nm == FlameName || nm == GlowName || nm == GlowName + "PS" || nm == "MIG15S_ABFlame")
                        continue;
                    if (donorMat != null)
                        CopyMaterials(donorMat, r);
                    if (MissingMaterial(r))
                        ApplyNozzleFixMat(r);
                }
            }
        }

        private static void ApplyNozzleFixMat(Renderer r)
        {
            if (r == null)
                return;
            if (_nozzleFixMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null)
                    sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh == null)
                    sh = Shader.Find("Sprites/Default");
                if (sh == null)
                    return;
                _nozzleFixMat = new Material(sh);
                _nozzleFixMat.name = "MIG15S_NozzleFix";
                _nozzleFixMat.color = new Color(0.07f, 0.07f, 0.08f, 1f);
            }
            try { r.sharedMaterial = _nozzleFixMat; }
            catch { }
        }

        private static Renderer FindLoadedEngineRenderer()
        {
            JetNozzle donor = GetDonor();
            if (donor != null)
            {
                Renderer fromDonor = RendererOnEngine(donor);
                if (fromDonor != null)
                    return fromDonor;
            }
            JetNozzle[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<JetNozzle>(); }
            catch { all = null; }
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = RendererOnEngine(all[i]);
                if (r != null)
                    return r;
            }
            return null;
        }

        private static Renderer RendererOnEngine(JetNozzle nozzle)
        {
            if (nozzle == null)
                return null;
            GameObject engine = null;
            try
            {
                if (NozzleEngine != null)
                    engine = NozzleEngine.GetValue(nozzle) as GameObject;
            }
            catch { engine = null; }
            if (engine != null)
            {
                Renderer[] rs = engine.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] != null && !(rs[i] is ParticleSystemRenderer) && !MissingMaterial(rs[i]))
                        return rs[i];
                }
            }
            Renderer[] all = nozzle.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i] is ParticleSystemRenderer)
                    continue;
                string nm = all[i].gameObject.name;
                if (nm == FlameName || nm == GlowName)
                    continue;
                if (!MissingMaterial(all[i]))
                    return all[i];
            }
            return null;
        }

        private static JetNozzle GetDonor()
        {
            if (_donor != null && DonorUsable(_donor))
                return _donor;
            if (Time.unscaledTime < _nextDonor && _donor == null)
                return null;
            _nextDonor = Time.unscaledTime + 15f;
            JetNozzle best = null;
            JetNozzle[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<JetNozzle>(); }
            catch { all = null; }
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    JetNozzle n = all[i];
                    if (!DonorUsable(n))
                        continue;
                    Aircraft ac = null;
                    try { ac = n.GetComponentInParent<Aircraft>(); }
                    catch { ac = null; }
                    if (ac != null && Service.IsOurs(ac))
                        continue;
                    if (IsAb4Blob(ac, n))
                    {
                        _donor = n;
                        if (Plugin.Log != null)
                            Plugin.Log.LogInfo("MiG-15S AB FX from live nozzle");
                        return _donor;
                    }
                    if (best == null)
                        best = n;
                }
            }
            if (best != null)
            {
                _donor = best;
                return _donor;
            }
            AircraftDefinition[] defs = null;
            try { defs = Resources.FindObjectsOfTypeAll<AircraftDefinition>(); }
            catch { defs = null; }
            if (defs == null)
                return null;
            for (int i = 0; i < defs.Length; i++)
            {
                AircraftDefinition def = defs[i];
                if (def == null || !IsAb4(def))
                    continue;
                GameObject prefab = def.unitPrefab;
                if (prefab == null)
                    continue;
                JetNozzle[] ns = prefab.GetComponentsInChildren<JetNozzle>(true);
                if (ns == null || ns.Length == 0 || !DonorUsable(ns[0]))
                    continue;
                _donor = ns[0];
                if (Plugin.Log != null)
                    Plugin.Log.LogInfo("MiG-15S AB FX from " + def.jsonKey);
                return _donor;
            }
            return null;
        }

        private static bool DonorUsable(JetNozzle n)
        {
            if (n == null)
                return false;
            Renderer flame = FindDonorFlame(n);
            return flame != null && !MissingMaterial(flame);
        }

        private static bool IsAb4Blob(Aircraft ac, JetNozzle n)
        {
            string blob = string.Empty;
            if (ac != null)
            {
                try
                {
                    AircraftDefinition def = ac.definition;
                    if (def != null)
                        blob = ((def.jsonKey != null ? def.jsonKey : string.Empty) + " "
                            + (def.unitName != null ? def.unitName : string.Empty) + " "
                            + (def.code != null ? def.code : string.Empty));
                }
                catch { blob = string.Empty; }
            }
            if (blob.IndexOf("AB-4", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("AB4", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("Alkyon", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n != null && n.gameObject != null && n.gameObject.name != null)
            {
                string gn = n.gameObject.name;
                if (gn.IndexOf("AB-4", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsAb4(AircraftDefinition def)
        {
            string blob = ((def.jsonKey != null ? def.jsonKey : string.Empty) + " "
                + (def.unitName != null ? def.unitName : string.Empty) + " "
                + (def.code != null ? def.code : string.Empty));
            if (blob.IndexOf("AB-4", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("AB4", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (blob.IndexOf("Alkyon", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }
    }
}
