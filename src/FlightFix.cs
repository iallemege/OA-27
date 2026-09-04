using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// Hands-off pitch (no auto pull-up) and FBW/trim quieting.
    /// Airframe joints/bodies stay stock; speed is capped in Service.
    /// </summary>
    internal static class FlightFix
    {
        internal const float StickDead = 0.08f;
        internal const float PitchRateDamp = 0.55f;
        internal const float ExternalHitMps = 22f;
        internal const float FlutterKmh = 650f;
        internal const float FlutterMps = 650f / 3.6f;

        private static readonly FieldInfo CfAircraft =
            AccessTools.Field(typeof(ControlsFilter), "aircraft");
        private static readonly FieldInfo CfFlyByWire =
            AccessTools.Field(typeof(ControlsFilter), "flyByWire");
        private static readonly Type FbwType =
            AccessTools.Inner(typeof(ControlsFilter), "FlyByWire");
        private static readonly FieldInfo FbwEnabled =
            FbwType != null ? AccessTools.Field(FbwType, "Enabled") : null;
        private static readonly FieldInfo FbwI =
            FbwType != null ? AccessTools.Field(FbwType, "iFactor") : null;
        private static readonly FieldInfo FbwIState =
            FbwType != null ? AccessTools.Field(FbwType, "i") : null;
        private static readonly FieldInfo FbwGLimit =
            FbwType != null ? AccessTools.Field(FbwType, "gLimitPositive") : null;
        private static readonly FieldInfo FbwCorner =
            FbwType != null ? AccessTools.Field(FbwType, "cornerSpeed") : null;
        private static readonly FieldInfo FbwPitchVel =
            FbwType != null ? AccessTools.Field(FbwType, "maxPitchAngularVel") : null;
        private static readonly FieldInfo FbwRollVel =
            FbwType != null ? AccessTools.Field(FbwType, "maxRollAngularVel") : null;
        private static readonly FieldInfo FbwAlpha =
            FbwType != null ? AccessTools.Field(FbwType, "alphaLimiter") : null;
        private static readonly FieldInfo FbwSmooth =
            FbwType != null ? AccessTools.Field(FbwType, "inputSmoothing") : null;
        private static readonly FieldInfo FbwDFast =
            FbwType != null ? AccessTools.Field(FbwType, "dFactorFast") : null;
        private static readonly FieldInfo NozzlePitchThrust =
            AccessTools.Field(typeof(JetNozzle), "pitchThrust");
        private static readonly FieldInfo NozzleRollThrust =
            AccessTools.Field(typeof(JetNozzle), "rollThrust");
        private static readonly FieldInfo AeroJoints =
            AccessTools.Field(typeof(AeroPart), "joints");
        private static readonly FieldInfo UnitStructural =
            AccessTools.Field(typeof(UnitPart), "structuralThreshold");
        private static readonly FieldInfo UnitImpact =
            AccessTools.Field(typeof(UnitPart), "impactDamage");
        private static readonly FieldInfo ImpactThreshold =
            AccessTools.Field(typeof(ImpactDamage), "threshold");
        private static readonly FieldInfo ImpactMultiplier =
            AccessTools.Field(typeof(ImpactDamage), "multiplier");
        private static readonly FieldInfo AutopilotFwd =
            AccessTools.Field(typeof(Autopilot), "forwardFlight")
            ?? AccessTools.Field(typeof(Autopilot), "forwardFlightController");
        private static readonly FieldInfo CsSpring =
            AccessTools.Field(typeof(ControlSurfacePhysics), "spring");
        private static readonly FieldInfo CsDamp =
            AccessTools.Field(typeof(ControlSurfacePhysics), "damp");
        private static readonly FieldInfo CsBreak =
            AccessTools.Field(typeof(ControlSurfacePhysics), "breakStrength");
        private static readonly FieldInfo AircraftParts =
            AccessTools.Field(typeof(Aircraft), "parts");
        private static readonly FieldInfo AircraftAeroParts =
            AccessTools.Field(typeof(Aircraft), "partsWithAero");

        private static readonly HashSet<int> FlightDone = new HashSet<int>();
        private static readonly HashSet<int> GroundDone = new HashSet<int>();
        private static readonly Dictionary<int, LandingGear.GearState> GearSeen =
            new Dictionary<int, LandingGear.GearState>();
        private static readonly Dictionary<int, float> ExternalHitAt = new Dictionary<int, float>();

        internal static void Apply(Aircraft ac)
        {
            if (ac == null || !Service.IsOurs(ac) || !Plugin.IsRuntime(ac))
                return;
            if (!Service.IsLiveAircraft(ac))
                return;
            TuneParameters(ac);
            int id = ac.GetInstanceID();
            if (GroundDone.Add(id))
                IgnoreSelfCollisions(ac);
            RefreshGearIgnores(ac, id);
        }

        internal static void MarkExternalHit(Aircraft ac)
        {
            if (ac == null)
                return;
            ExternalHitAt[ac.GetInstanceID()] = Time.unscaledTime;
        }

        internal static bool RecentExternalHit(Aircraft ac)
        {
            if (ac == null)
                return false;
            float t;
            if (!ExternalHitAt.TryGetValue(ac.GetInstanceID(), out t))
                return false;
            return (Time.unscaledTime - t) < 1.5f;
        }

        internal static bool IsRealImpact(Aircraft ac, Collision collision)
        {
            if (ac == null || collision == null)
                return false;
            if (IsOwnPart(ac, collision))
                return false;
            float rel = 0f;
            try { rel = collision.relativeVelocity.magnitude; }
            catch { rel = 0f; }
            if (rel < ExternalHitMps)
                return false;
            MarkExternalHit(ac);
            return true;
        }

        internal static bool IsOwnPart(Aircraft ac, Collision collision)
        {
            if (ac == null || collision == null)
                return true;
            Transform other = null;
            try
            {
                if (collision.collider != null)
                    other = collision.collider.transform;
                else if (collision.rigidbody != null)
                    other = collision.rigidbody.transform;
            }
            catch { other = null; }
            if (other == null)
                return true;
            if (other.IsChildOf(ac.transform))
                return true;
            Aircraft otherAc = other.GetComponentInParent<Aircraft>();
            return otherAc != null && object.ReferenceEquals(otherAc, ac);
        }

        internal static void HandsOffPitch(ControlsFilter filter, ControlInputs inputs, float stickPitch, Rigidbody rb)
        {
            if (inputs == null)
                return;
            Aircraft ac = AircraftOfFilter(filter);
            if (ac == null || !Service.IsOurs(ac) || !Plugin.IsRuntime(ac))
                return;
            if (Service.IsOaFamilyClone(ac))
                return;
            if (!Service.IsLiveAircraft(ac))
                return;
            if (Mathf.Abs(stickPitch) >= StickDead)
                return;
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            if (alt < 6f)
                return;
            if (Service.GearOnGround(ac))
                return;
            float rate = 0f;
            try
            {
                if (rb != null)
                    rate = Vector3.Dot(rb.angularVelocity, ac.transform.right);
            }
            catch { rate = 0f; }
            float damp = PitchRateDamp;
            try
            {
                if (ac.speed >= FlutterMps)
                    damp = 1.15f;
            }
            catch { }
            inputs.pitch = Mathf.Clamp(-rate * damp, -0.45f, 0.45f);
            ZeroFbwIntegrator(filter);
        }

        internal static void ScaleHighSpeedStick(ControlsFilter filter, ControlInputs inputs)
        {
            if (inputs == null)
                return;
            Aircraft ac = AircraftOfFilter(filter);
            if (ac == null || !Service.IsOurs(ac) || !Service.IsLiveAircraft(ac))
                return;
            if (Service.IsOaFamilyClone(ac))
                return;
            float spd = 0f;
            try { spd = ac.speed; }
            catch { spd = 0f; }
            if (spd < FlutterMps)
                return;
            float t = (spd - FlutterMps) / Mathf.Max(1f, Service.SpeedCapMps - FlutterMps);
            if (t > 1f)
                t = 1f;
            float scale = Mathf.Lerp(1f, 0.42f, t);
            inputs.pitch *= scale;
            inputs.roll *= scale;
            inputs.yaw *= scale;
        }

        internal static bool ShareRigidbody(AeroPart part)
        {
            return false;
        }

        internal static bool SkipCreateJoints(AeroPart part)
        {
            return false;
        }

        internal static Aircraft AircraftOfPart(UnitPart part)
        {
            if (part == null)
                return null;
            Aircraft ac = part.parentUnit as Aircraft;
            if (ac != null)
                return ac;
            try { return part.GetComponentInParent<Aircraft>(); }
            catch { return null; }
        }

        internal static bool BlockJointBreak(UnitPart part)
        {
            return false;
        }

        internal static Aircraft AircraftOfFilter(ControlsFilter filter)
        {
            if (filter == null)
                return null;
            if (CfAircraft != null)
            {
                try
                {
                    Aircraft ac = CfAircraft.GetValue(filter) as Aircraft;
                    if (ac != null)
                        return ac;
                }
                catch { }
            }
            try { return filter.GetComponentInParent<Aircraft>(); }
            catch { return null; }
        }

        private static void QuietNozzlePitch(Aircraft ac)
        {
            JetNozzle[] nozzles = null;
            try { nozzles = ac.GetComponentsInChildren<JetNozzle>(true); }
            catch { nozzles = null; }
            if (nozzles == null)
                return;
            for (int i = 0; i < nozzles.Length; i++)
            {
                JetNozzle n = nozzles[i];
                if (n == null)
                    continue;
                if (NozzlePitchThrust != null)
                {
                    try { NozzlePitchThrust.SetValue(n, 0f); }
                    catch { }
                }
                if (NozzleRollThrust != null)
                {
                    try { NozzleRollThrust.SetValue(n, 0f); }
                    catch { }
                }
            }
        }

        private static void TuneParameters(Aircraft ac)
        {
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (def == null || def.aircraftParameters == null)
                return;
            AircraftParameters p = def.aircraftParameters;
            p.levelBias = 0f;
            if (p.AoAEffects != null)
                p.AoAEffects.ShakeFactor = 0f;
            ControlsFilter cf = null;
            try { cf = ac.GetControlsFilter(); }
            catch { cf = null; }
            if (cf == null)
                return;
            try
            {
                FieldInfo fp = AccessTools.Field(typeof(ControlsFilter), "aircraftParameters");
                if (fp != null)
                {
                    AircraftParameters live = fp.GetValue(cf) as AircraftParameters;
                    if (live != null)
                    {
                        live.levelBias = 0f;
                        if (live.AoAEffects != null)
                            live.AoAEffects.ShakeFactor = 0f;
                    }
                }
            }
            catch { }
        }

        private static void QuietFlyByWire(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            ControlsFilter cf = null;
            try { cf = ac.GetControlsFilter(); }
            catch { cf = null; }
            if (cf != null && CfFlyByWire != null)
            {
                object fbw = null;
                try { fbw = CfFlyByWire.GetValue(cf); }
                catch { fbw = null; }
                if (fbw != null)
                {
                    // Stick-center FBW G-command is what pitches the jet up in level flight.
                    SetBool(FbwEnabled, fbw, false);
                    SetFloat(FbwI, fbw, 0f);
                    SetFloat(FbwIState, fbw, 0f);
                    SetFloat(FbwGLimit, fbw, 9f);
                    SetFloat(FbwCorner, fbw, 240f);
                    SetFloat(FbwPitchVel, fbw, 1.6f);
                    SetFloat(FbwRollVel, fbw, 3.2f);
                    SetFloat(FbwAlpha, fbw, 16f);
                    SetFloat(FbwDFast, fbw, 0.35f);
                    if (FbwSmooth != null)
                    {
                        try { FbwSmooth.SetValue(fbw, new Vector3(0.12f, 0.12f, 0.16f)); }
                        catch { }
                    }
                }
            }
            if (!FlightDone.Add(id))
                return;
            Autopilot ap = null;
            try { ap = ac.GetComponentInChildren<Autopilot>(true); }
            catch { ap = null; }
            if (ap != null && AutopilotFwd != null)
            {
                try
                {
                    object fwd = AutopilotFwd.GetValue(ap);
                    if (fwd != null)
                    {
                        FieldInfo en = AccessTools.Field(fwd.GetType(), "Enabled");
                        if (en != null)
                            en.SetValue(fwd, false);
                    }
                }
                catch { }
            }
        }

        private static void ZeroFbwIntegrator(ControlsFilter filter)
        {
            if (filter == null || CfFlyByWire == null || FbwIState == null)
                return;
            try
            {
                object fbw = CfFlyByWire.GetValue(filter);
                if (fbw != null)
                    FbwIState.SetValue(fbw, 0f);
            }
            catch { }
        }

        private static void WeldAirframe(Aircraft ac)
        {
            AeroPart[] aeros = ac.GetComponentsInChildren<AeroPart>(true);
            for (int i = 0; i < aeros.Length; i++)
                WeldAero(aeros[i]);
            UnitPart[] parts = ac.GetComponentsInChildren<UnitPart>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                UnitPart p = parts[i];
                if (p == null)
                    continue;
                HardenPart(p);
                Rigidbody rb = null;
                try { rb = p.rb; }
                catch { rb = null; }
                if (rb == null)
                    continue;
                try
                {
                    rb.solverIterations = 16;
                    rb.solverVelocityIterations = 8;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                catch { }
            }
            if (ac.rb != null)
            {
                try
                {
                    ac.rb.solverIterations = 16;
                    ac.rb.solverVelocityIterations = 8;
                    ac.rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                catch { }
            }
        }

        private static void WeldAero(AeroPart part)
        {
            if (part == null || AeroJoints == null)
                return;
            PartJoint[] joints = null;
            try { joints = AeroJoints.GetValue(part) as PartJoint[]; }
            catch { return; }
            if (joints == null)
                return;
            for (int i = 0; i < joints.Length; i++)
            {
                PartJoint pj = joints[i];
                if (pj == null)
                    continue;
                pj.breakForce = float.PositiveInfinity;
                pj.breakTorque = float.PositiveInfinity;
                pj.solverIterations = 20;
                Joint j = pj.joint;
                if (j == null)
                    continue;
                j.breakForce = float.PositiveInfinity;
                j.breakTorque = float.PositiveInfinity;
                ConfigurableJoint cj = j as ConfigurableJoint;
                if (cj != null)
                {
                    try
                    {
                        cj.projectionMode = JointProjectionMode.PositionAndRotation;
                        cj.projectionDistance = 0.02f;
                        cj.projectionAngle = 2f;
                    }
                    catch { }
                }
            }
        }

        private static void HardenPart(UnitPart part)
        {
            if (part == null)
                return;
            if (UnitStructural != null)
            {
                try
                {
                    float th = (float)UnitStructural.GetValue(part);
                    if (th > 0.001f)
                        UnitStructural.SetValue(part, th * 0.02f);
                }
                catch { }
            }
            if (UnitImpact == null || ImpactThreshold == null)
                return;
            try
            {
                object impact = UnitImpact.GetValue(part);
                if (impact == null)
                    return;
                float th = (float)ImpactThreshold.GetValue(impact);
                if (th > 0.01f)
                    ImpactThreshold.SetValue(impact, th * 24f);
                if (ImpactMultiplier != null)
                {
                    float m = (float)ImpactMultiplier.GetValue(impact);
                    if (m > 0.01f)
                        ImpactMultiplier.SetValue(impact, m * 0.04f);
                }
            }
            catch { }
        }

        private static void CollapseExtraBodies(Aircraft ac)
        {
            if (ac == null || ac.rb == null)
                return;
            List<UnitPart> parts = CollectAllParts(ac);
            for (int i = 0; i < parts.Count; i++)
            {
                UnitPart p = parts[i];
                if (p == null || p.transform == null)
                    continue;
                if (KeepSeparateBody(p.gameObject, ac))
                    continue;
                try
                {
                    if (ac.transform != null && !p.transform.IsChildOf(ac.transform))
                        p.transform.SetParent(ac.transform, true);
                }
                catch { }
            }
            HashSet<int> seenJoint = new HashSet<int>();
            for (int i = 0; i < parts.Count; i++)
            {
                UnitPart p = parts[i];
                if (p == null)
                    continue;
                Joint[] joints = null;
                try { joints = p.GetComponents<Joint>(); }
                catch { joints = null; }
                if (joints == null)
                    continue;
                for (int j = 0; j < joints.Length; j++)
                {
                    Joint joint = joints[j];
                    if (joint == null)
                        continue;
                    if (!seenJoint.Add(joint.GetInstanceID()))
                        continue;
                    if (KeepSeparateBody(joint.gameObject, ac))
                        continue;
                    try
                    {
                        if (joint.connectedBody != null
                            && KeepSeparateBody(joint.connectedBody.gameObject, ac))
                            continue;
                    }
                    catch { }
                    try { UnityEngine.Object.Destroy(joint); }
                    catch { }
                }
            }
            HashSet<int> seenRb = new HashSet<int>();
            float extraMass = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                UnitPart p = parts[i];
                if (p == null || KeepSeparateBody(p.gameObject, ac))
                    continue;
                Rigidbody rb = null;
                try { rb = p.rb; }
                catch { rb = null; }
                if (rb == null)
                {
                    try { rb = p.GetComponent<Rigidbody>(); }
                    catch { rb = null; }
                }
                if (rb == null || object.ReferenceEquals(rb, ac.rb))
                    continue;
                if (!seenRb.Add(rb.GetInstanceID()))
                    continue;
                try
                {
                    if (rb.mass > 0.02f)
                        extraMass += rb.mass;
                }
                catch { }
                try
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                }
                catch { }
                try { UnityEngine.Object.Destroy(rb); }
                catch { }
            }
            if (extraMass > 0.02f)
            {
                try { ac.rb.mass += extraMass; }
                catch { }
            }
            for (int i = 0; i < parts.Count; i++)
            {
                UnitPart p = parts[i];
                if (p == null || KeepSeparateBody(p.gameObject, ac))
                    continue;
                try { p.rb = ac.rb; }
                catch { }
            }
        }

        private static List<UnitPart> CollectAllParts(Aircraft ac)
        {
            List<UnitPart> list = new List<UnitPart>();
            HashSet<int> seen = new HashSet<int>();
            AddParts(list, seen, ac.GetComponentsInChildren<UnitPart>(true));
            try
            {
                List<UnitPart> all = ac.GetAllParts();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                        AddPart(list, seen, all[i]);
                }
            }
            catch { }
            try
            {
                if (ac.partLookup != null)
                {
                    for (int i = 0; i < ac.partLookup.Count; i++)
                        AddPart(list, seen, ac.partLookup[i]);
                }
            }
            catch { }
            AddReflectedPartList(list, seen, ac, AircraftParts);
            AddReflectedPartList(list, seen, ac, AircraftAeroParts);
            return list;
        }

        private static void AddReflectedPartList(List<UnitPart> list, HashSet<int> seen, Aircraft ac, FieldInfo field)
        {
            if (field == null || ac == null)
                return;
            object raw = null;
            try { raw = field.GetValue(ac); }
            catch { return; }
            IEnumerable e = raw as IEnumerable;
            if (e == null)
                return;
            foreach (object o in e)
                AddPart(list, seen, o as UnitPart);
        }

        private static void AddParts(List<UnitPart> list, HashSet<int> seen, UnitPart[] parts)
        {
            if (parts == null)
                return;
            for (int i = 0; i < parts.Length; i++)
                AddPart(list, seen, parts[i]);
        }

        private static void AddPart(List<UnitPart> list, HashSet<int> seen, UnitPart p)
        {
            if (p == null)
                return;
            if (!seen.Add(p.GetInstanceID()))
                return;
            list.Add(p);
        }

        private static void QuietControlSurfacePhysics(Aircraft ac)
        {
            ControlSurfacePhysics[] css = null;
            try { css = ac.GetComponentsInChildren<ControlSurfacePhysics>(true); }
            catch { css = null; }
            if (css == null)
                return;
            for (int i = 0; i < css.Length; i++)
            {
                ControlSurfacePhysics cs = css[i];
                if (cs == null)
                    continue;
                if (CsSpring != null)
                {
                    try { CsSpring.SetValue(cs, 0f); }
                    catch { }
                }
                if (CsDamp != null)
                {
                    try { CsDamp.SetValue(cs, 250f); }
                    catch { }
                }
                if (CsBreak != null)
                {
                    try { CsBreak.SetValue(cs, float.PositiveInfinity); }
                    catch { }
                }
                try { cs.enabled = false; }
                catch { }
            }
        }

        private static void KillFlutter(Aircraft ac)
        {
            if (ac == null || ac.rb == null)
                return;
            Rigidbody[] rbs = null;
            try { rbs = ac.GetComponentsInChildren<Rigidbody>(true); }
            catch { rbs = null; }
            if (rbs == null)
                return;
            Vector3 w = ac.rb.angularVelocity;
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody rb = rbs[i];
                if (rb == null || object.ReferenceEquals(rb, ac.rb))
                    continue;
                if (KeepSeparateBody(rb.gameObject, ac))
                    continue;
                try
                {
                    rb.velocity = ac.rb.GetPointVelocity(rb.worldCenterOfMass);
                    rb.angularVelocity = w;
                }
                catch { }
            }
        }

        private static bool KeepSeparateBody(GameObject go, Aircraft ac)
        {
            if (go == null)
                return false;
            if (ac != null && object.ReferenceEquals(go, ac.gameObject))
                return false;
            try
            {
                if (go.GetComponent<LandingGear>() != null)
                    return true;
                if (go.GetComponent<Pilot>() != null)
                    return true;
                if (go.GetComponent<Missile>() != null)
                    return true;
            }
            catch { }
            string n = go.name != null ? go.name : string.Empty;
            if (n.IndexOf("Gear", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Tire", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Strut", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Oleo", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Missile", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Bomb", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Rocket", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("KH38", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static void RefreshGearIgnores(Aircraft ac, int id)
        {
            if (ac == null)
                return;
            LandingGear.GearState gear = LandingGear.GearState.Uninitialized;
            try { gear = ac.gearState; }
            catch { return; }
            LandingGear.GearState prev;
            if (GearSeen.TryGetValue(id, out prev) && prev == gear)
                return;
            GearSeen[id] = gear;
            IgnoreSelfCollisions(ac);
        }

        private static void IgnoreSelfCollisions(Aircraft ac)
        {
            Collider[] cols = null;
            try { cols = ac.GetComponentsInChildren<Collider>(true); }
            catch { cols = null; }
            if (cols == null)
                return;
            for (int i = 0; i < cols.Length; i++)
            {
                Collider a = cols[i];
                if (a == null)
                    continue;
                for (int j = i + 1; j < cols.Length; j++)
                {
                    Collider b = cols[j];
                    if (b == null)
                        continue;
                    try { Physics.IgnoreCollision(a, b, true); }
                    catch { }
                }
            }
        }

        private static void SetBool(FieldInfo f, object obj, bool v)
        {
            if (f == null || obj == null)
                return;
            try { f.SetValue(obj, v); }
            catch { }
        }

        private static void SetFloat(FieldInfo f, object obj, float v)
        {
            if (f == null || obj == null)
                return;
            try { f.SetValue(obj, v); }
            catch { }
        }
    }
}
