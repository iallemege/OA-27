using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// After the OA-27C WSO is punched, nearby live missiles are pulled onto
    /// the ejected rear seat (PilotDismounted dummy + hot IR).
    /// </summary>
    internal static class RearLure
    {
        private const float RadiusM = 7500f;
        private const float Interval = 0.2f;
        private const float Duration = 18f;
        private const float IrIntensity = 72f;
        private const float RcsBoost = 80f;
        private const float AdoptRangeM = 90f;

        private static readonly FieldInfo MissileTargetField =
            AccessTools.Field(typeof(Missile), "target");
        private static readonly FieldInfo SeekerField =
            AccessTools.Field(typeof(Missile), "seeker");
        private static readonly FieldInfo SeekerTargetField =
            AccessTools.Field(typeof(MissileSeeker), "targetUnit");
        private static readonly FieldInfo IrTargetField =
            AccessTools.Field(typeof(IRSeeker), "IRTarget");
        private static readonly FieldInfo IrGuidanceField =
            AccessTools.Field(typeof(IRSeeker), "guidance");
        private static readonly FieldInfo IrKnownPosField =
            AccessTools.Field(typeof(IRSeeker), "knownPos");
        private static readonly FieldInfo IrKnownVelField =
            AccessTools.Field(typeof(IRSeeker), "knownVel");
        private static readonly FieldInfo SarhTargetTransform =
            AccessTools.Field(typeof(SARHSeeker), "targetTransform");
        private static readonly FieldInfo SarhKnownPos =
            AccessTools.Field(typeof(SARHSeeker), "knownPos");
        private static readonly FieldInfo SarhKnownVel =
            AccessTools.Field(typeof(SARHSeeker), "knownVel");
        private static readonly FieldInfo SarhTimeWithoutTrack =
            AccessTools.Field(typeof(SARHSeeker), "timeWithoutTrack");
        private static readonly FieldInfo SarhGuidance =
            AccessTools.Field(typeof(SARHSeeker), "guidance");
        private static readonly FieldInfo ArhKnownPos =
            AccessTools.Field(typeof(ARHSeeker), "knownPos");
        private static readonly FieldInfo ArhKnownVel =
            AccessTools.Field(typeof(ARHSeeker), "knownVel");
        private static readonly FieldInfo ArhGuidance =
            AccessTools.Field(typeof(ARHSeeker), "guidance");
        private static readonly FieldInfo ArhTimeWithoutReturn =
            AccessTools.Field(typeof(ARHSeeker), "timeWithoutReturn");
        private static readonly FieldInfo ArhRadarLock =
            AccessTools.Field(typeof(ARHSeeker), "radarLockEstablished");
        private static readonly FieldInfo ArhAchievedLock =
            AccessTools.Field(typeof(ARHSeeker), "achievedLock");

        private static bool _active;
        private static Aircraft _ac;
        private static uint _acPid;
        private static int _rearNum;
        private static Transform _baitXf;
        private static Rigidbody _baitRb;
        private static Unit _baitUnit;
        private static IRSource _irOnAc;
        private static IRSource _irOnBait;
        private static float _until;
        private static float _nextPulse;
        private static bool _rcsBoosted;
        private static readonly List<Missile> _scratch = new List<Missile>(64);

        internal static void Arm(Aircraft ac, Pilot rear, EjectionSeat seat, int rearNum)
        {
            Arm(ac, rear, seat, rearNum, null);
        }

        internal static void Arm(Aircraft ac, Pilot rear, EjectionSeat seat, int rearNum, Unit bait)
        {
            if (ac == null)
                return;
            _active = true;
            _ac = ac;
            _acPid = 0u;
            try { _acPid = ac.persistentID.Id; }
            catch { }
            _rearNum = rearNum > 0 ? rearNum : 1;
            _baitXf = null;
            _baitUnit = bait;
            if (_baitUnit != null)
                _baitXf = _baitUnit.transform;
            if (_baitXf == null && seat != null)
                _baitXf = seat.transform;
            if (_baitXf == null && rear != null)
                _baitXf = rear.transform;
            if (_baitXf == null)
                _baitXf = ac.transform;
            _baitRb = null;
            if (_baitUnit != null)
            {
                try { _baitRb = _baitUnit.rb; }
                catch { _baitRb = null; }
            }
            if (_baitRb == null)
                _baitRb = RbOf(_baitXf);
            _until = Time.time + Duration;
            _nextPulse = 0f;
            _rcsBoosted = false;
            if (_baitUnit != null)
            {
                try { _baitUnit.ModifyRCS(RcsBoost); }
                catch { }
                _rcsBoosted = true;
            }
            ResolveBait();
            AttachIr();
        }

        internal static void Tick()
        {
            if (!_active)
                return;
            if (_ac == null)
            {
                Stop();
                return;
            }
            try
            {
                if (_ac.disabled)
                {
                    Stop();
                    return;
                }
            }
            catch { }
            if (Time.time > _until)
            {
                Stop();
                return;
            }
            if (!Service.IsOaConventionalClone(_ac))
                Service.KeepPlayerFlying(_ac);
            if (_baitUnit == null || _baitXf == null || BaitGone(_baitUnit))
                ResolveBait();
            if (Time.time < _nextPulse)
                return;
            _nextPulse = Time.time + Interval;
            Pulse();
        }

        private static void Stop()
        {
            if (_irOnAc != null && _ac != null)
            {
                try { _ac.RemoveIRSource(_irOnAc); }
                catch { }
            }
            _irOnAc = null;
            _irOnBait = null;
            _active = false;
            _ac = null;
            _baitXf = null;
            _baitRb = null;
            _baitUnit = null;
        }

        private static bool BaitGone(Unit u)
        {
            if (u == null)
                return true;
            try
            {
                if (u.disabled)
                    return true;
            }
            catch { }
            return false;
        }

        private static void ResolveBait()
        {
            if (_baitUnit != null && !BaitGone(_baitUnit))
            {
                if (_baitXf == null)
                    _baitXf = _baitUnit.transform;
                if (_baitRb == null)
                {
                    try { _baitRb = _baitUnit.rb; }
                    catch { _baitRb = RbOf(_baitXf); }
                }
                return;
            }
            PilotDismounted best = null;
            float bestSqr = AdoptRangeM * AdoptRangeM;
            Vector3 origin = Vector3.zero;
            bool haveOrigin = false;
            if (_baitXf != null)
            {
                origin = _baitXf.position;
                haveOrigin = true;
            }
            else if (_ac != null)
            {
                origin = _ac.transform.position;
                haveOrigin = true;
            }
            List<Unit> units = null;
            try { units = UnitRegistry.allUnits; }
            catch { units = null; }
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    PilotDismounted pd = units[i] as PilotDismounted;
                    if (ScoreDummy(pd, origin, haveOrigin, ref bestSqr))
                        best = pd;
                }
            }
            if (best == null)
            {
                PilotDismounted[] all = null;
                try { all = Object.FindObjectsOfType<PilotDismounted>(); }
                catch { all = null; }
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (ScoreDummy(all[i], origin, haveOrigin, ref bestSqr))
                            best = all[i];
                    }
                }
            }
            if (best == null && units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    Unit u = units[i];
                    if (u == null || u.gameObject == null)
                        continue;
                    if (u.gameObject.name != null
                        && u.gameObject.name.IndexOf("OA27C_RearBait", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _baitUnit = u;
                        if (u.transform != null)
                            _baitXf = u.transform;
                        _baitRb = u.rb != null ? u.rb : RbOf(_baitXf);
                        if (!_rcsBoosted)
                        {
                            try { u.ModifyRCS(RcsBoost); }
                            catch { }
                            _rcsBoosted = true;
                        }
                        AttachIr();
                        return;
                    }
                }
            }
            if (best == null)
                return;
            _baitUnit = best;
            if (best.transform != null)
                _baitXf = best.transform;
            if (best.rb != null)
                _baitRb = best.rb;
            else
                _baitRb = RbOf(_baitXf);
            if (!_rcsBoosted)
            {
                try { best.ModifyRCS(RcsBoost); }
                catch { }
                _rcsBoosted = true;
            }
            AttachIr();
        }

        private static bool ScoreDummy(PilotDismounted pd, Vector3 origin, bool haveOrigin, ref float bestSqr)
        {
            if (pd == null)
                return false;
            try
            {
                if (pd.disabled)
                    return false;
            }
            catch { }
            try
            {
                if (!Plugin.IsRuntime(pd))
                    return false;
            }
            catch { }
            bool ours = false;
            try
            {
                if (_acPid != 0u && pd.parentUnit.Id == _acPid)
                    ours = true;
            }
            catch { }
            try
            {
                if ((int)pd.pilotNumber == _rearNum)
                    ours = true;
            }
            catch { }
            float sqr = 0f;
            if (haveOrigin)
            {
                try { sqr = (pd.transform.position - origin).sqrMagnitude; }
                catch { return false; }
                if (!ours && sqr > bestSqr)
                    return false;
            }
            else if (!ours)
                return false;
            if (ours)
                bestSqr = 0f;
            else
                bestSqr = sqr;
            return true;
        }

        private static void AttachIr()
        {
            Transform xf = _baitXf;
            if (xf == null && _ac != null)
                xf = _ac.transform;
            if (xf == null)
                return;
            if (_irOnBait == null)
            {
                _irOnBait = MakeIr(xf);
                if (_baitUnit != null && _irOnBait != null)
                {
                    try { _baitUnit.AddIRSource(_irOnBait); }
                    catch { }
                }
            }
            else
            {
                _irOnBait.transform = xf;
                _irOnBait.intensity = IrIntensity;
                _irOnBait.flare = true;
            }
            if (_irOnAc == null && _ac != null)
            {
                _irOnAc = MakeIr(xf);
                if (_irOnAc != null)
                {
                    try { _ac.AddIRSource(_irOnAc); }
                    catch { }
                }
            }
            else if (_irOnAc != null)
            {
                _irOnAc.transform = xf;
                _irOnAc.intensity = IrIntensity;
                _irOnAc.flare = true;
            }
        }

        private static IRSource MakeIr(Transform xf)
        {
            IRSource ir = new IRSource(xf, IrIntensity, true);
            if (ir == null)
                return null;
            ir.transform = xf;
            ir.intensity = IrIntensity;
            ir.flare = true;
            return ir;
        }

        private static void Pulse()
        {
            if (_baitXf == null && _baitUnit == null)
                return;
            Vector3 origin = _ac != null ? _ac.transform.position : _baitXf.position;
            if (_baitXf != null)
                origin = _baitXf.position;
            float r2 = RadiusM * RadiusM;
            CollectMissiles();
            for (int i = 0; i < _scratch.Count; i++)
            {
                Missile m = _scratch[i];
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
                    if (IsOurShot(m))
                        continue;
                    if (IsStillOnPylon(m))
                        continue;
                    Retarget(m);
                }
                catch { }
            }
        }

        private static void CollectMissiles()
        {
            _scratch.Clear();
            List<Unit> units = null;
            try { units = UnitRegistry.allUnits; }
            catch { units = null; }
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    Missile m = units[i] as Missile;
                    if (m != null)
                        _scratch.Add(m);
                }
            }
            if (units != null)
                return;
            Missile[] all = null;
            try { all = Object.FindObjectsOfType<Missile>(); }
            catch { all = null; }
            if (all == null)
                return;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    _scratch.Add(all[i]);
            }
        }

        private static bool IsOurShot(Missile m)
        {
            if (m == null || _ac == null)
                return false;
            try
            {
                if (object.ReferenceEquals(m.owner, _ac))
                    return true;
            }
            catch { }
            try
            {
                if (_acPid != 0u && m.ownerID.Id == _acPid)
                    return true;
            }
            catch { }
            return false;
        }

        private static bool IsStillOnPylon(Missile m)
        {
            if (m == null)
                return false;
            try
            {
                Aircraft parent = m.GetComponentInParent<Aircraft>();
                if (parent != null)
                    return true;
            }
            catch { }
            return false;
        }

        private static void Retarget(Missile hostile)
        {
            if (hostile == null)
                return;
            Transform xf = _baitXf;
            if (xf == null && _baitUnit != null)
                xf = _baitUnit.transform;
            Rigidbody rb = _baitRb;
            if (rb == null && _baitUnit != null)
                rb = _baitUnit.rb;
            if (rb == null)
                rb = RbOf(xf);

            if (_baitUnit != null)
            {
                try { hostile.SetTarget(_baitUnit); }
                catch { }
                try
                {
                    if (MissileTargetField != null)
                        MissileTargetField.SetValue(hostile, _baitUnit);
                }
                catch { }
            }

            if (xf != null)
            {
                try { hostile.SetProxyFuse(xf, rb); }
                catch { }
            }

            GlobalPosition gp = default(GlobalPosition);
            Vector3 vel = Vector3.zero;
            bool haveGp = false;
            if (_baitUnit != null)
            {
                try
                {
                    gp = _baitUnit.GlobalPosition();
                    haveGp = true;
                }
                catch { }
            }
            if (!haveGp && xf != null)
            {
                try
                {
                    gp = xf.position.ToGlobalPosition();
                    haveGp = true;
                }
                catch { }
            }
            if (rb != null)
            {
                try { vel = rb.velocity; }
                catch { }
            }
            if (haveGp)
            {
                try { hostile.SetAimpoint(gp, vel); }
                catch { }
            }

            MissileSeeker seeker = null;
            try
            {
                if (SeekerField != null)
                    seeker = SeekerField.GetValue(hostile) as MissileSeeker;
            }
            catch { }
            if (seeker == null)
                return;

            if (_baitUnit != null && SeekerTargetField != null)
            {
                try { SeekerTargetField.SetValue(seeker, _baitUnit); }
                catch { }
            }

            IRSeeker ir = seeker as IRSeeker;
            if (ir != null)
            {
                if (_irOnBait != null && IrTargetField != null)
                {
                    try { IrTargetField.SetValue(ir, _irOnBait); }
                    catch { }
                }
                if (IrGuidanceField != null)
                {
                    try { IrGuidanceField.SetValue(ir, true); }
                    catch { }
                }
                if (haveGp && IrKnownPosField != null)
                {
                    try { IrKnownPosField.SetValue(ir, gp); }
                    catch { }
                }
                if (IrKnownVelField != null)
                {
                    try { IrKnownVelField.SetValue(ir, vel); }
                    catch { }
                }
            }

            SARHSeeker sarh = seeker as SARHSeeker;
            if (sarh != null)
            {
                if (xf != null && SarhTargetTransform != null)
                {
                    try { SarhTargetTransform.SetValue(sarh, xf); }
                    catch { }
                }
                if (SarhTimeWithoutTrack != null)
                {
                    try { SarhTimeWithoutTrack.SetValue(sarh, 0f); }
                    catch { }
                }
                if (haveGp && SarhKnownPos != null)
                {
                    try { SarhKnownPos.SetValue(sarh, gp); }
                    catch { }
                }
                if (SarhKnownVel != null)
                {
                    try { SarhKnownVel.SetValue(sarh, vel); }
                    catch { }
                }
                if (SarhGuidance != null)
                {
                    try { SarhGuidance.SetValue(sarh, true); }
                    catch { }
                }
            }

            ARHSeeker arh = seeker as ARHSeeker;
            if (arh != null)
            {
                if (haveGp && ArhKnownPos != null)
                {
                    try { ArhKnownPos.SetValue(arh, gp); }
                    catch { }
                }
                if (ArhKnownVel != null)
                {
                    try { ArhKnownVel.SetValue(arh, vel); }
                    catch { }
                }
                if (ArhGuidance != null)
                {
                    try { ArhGuidance.SetValue(arh, true); }
                    catch { }
                }
                if (ArhTimeWithoutReturn != null)
                {
                    try { ArhTimeWithoutReturn.SetValue(arh, 0f); }
                    catch { }
                }
                if (ArhRadarLock != null)
                {
                    try { ArhRadarLock.SetValue(arh, true); }
                    catch { }
                }
                if (ArhAchievedLock != null)
                {
                    try { ArhAchievedLock.SetValue(arh, true); }
                    catch { }
                }
            }

            OpticalSeeker opt = seeker as OpticalSeeker;
            if (opt != null)
                BindNamed(opt, xf, gp, vel, haveGp);
        }

        private static void BindNamed(object seeker, Transform xf, GlobalPosition gp, Vector3 vel, bool haveGp)
        {
            if (seeker == null)
                return;
            SetField(seeker, "targetTransform", xf);
            SetField(seeker, "guidance", true);
            SetField(seeker, "hasVisual", true);
            if (haveGp)
                SetField(seeker, "knownPos", gp);
            SetField(seeker, "knownVel", vel);
        }

        private static void SetField(object obj, string name, object value)
        {
            if (obj == null)
                return;
            FieldInfo f = AccessTools.Field(obj.GetType(), name);
            if (f == null)
                return;
            try { f.SetValue(obj, value); }
            catch { }
        }

        private static Rigidbody RbOf(Transform xf)
        {
            if (xf == null)
                return null;
            Rigidbody rb = null;
            try { rb = xf.GetComponent<Rigidbody>(); }
            catch { }
            if (rb == null)
            {
                try { rb = xf.GetComponentInParent<Rigidbody>(); }
                catch { }
            }
            return rb;
        }
    }
}
