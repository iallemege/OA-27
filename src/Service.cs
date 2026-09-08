using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace OA27Variant
{
    internal static class Service
    {
        internal const string JsonKey = "Aryx_MiG15_KAM";
        internal const string DonorJsonKey = "Aryx_MiG-15";
        internal const string DisplayName = "MIG-15S Kamikaze Drone";
        internal const string ShortName = "MIG-15S";
        internal const string EncDescription =
            "MiG-15S is a fighter that have modernization to work perfect in the 2080s, it have upgrade  all parts from the original version , because of IAL Design Bureau's modify,the engine of MIG-15 has change to IES-200 and add suicide nuclear bomb to do the Kamikaze work. Drone means cockpit canopy was welded shut.";
        internal const string OaDonorKey = "Aryx_PropAttacker1";
        internal const string Internal20Key = "Aryx_PropAttacker1_20mm_Internal";
        internal const string OaJsonKey = "Aryx_OA27_C";
        internal const string OaDisplayName = "OA-27C Spectre";
        internal const string OaShortName = "OA-27C";
        internal const string OaEncDescription =
            "OA-27C Spectre is the IAL stealth Cavalier. Own WSO station: dumps incoming missiles, Y/N flares, Y/N target lock, gunsight lead, GLOC brace. RCS is held at zero. Eject punches the WSO into a decoy — you stay and cannot bail. Short-field lift and a raised G limit near the ground. 1 kt suicide fuze. Gun pods stay.";
        internal const string OaDJsonKey = "Aryx_OA27_D";
        internal const string OaDDisplayName = "OA-27D Anvil";
        internal const string OaDShortName = "OA-27D";
        internal const string OaDEncDescription =
            "OA-27D Anvil is the Boscali CAS Cavalier: extra airframe toughness and fuel, doubled turbine, no suicide kit. Same OA WSO station as C while he is aboard (Y/N flares and locks); first eject punches him, second bails you out. PALA hangars do not list it.";
        internal const string OaEJsonKey = "Aryx_OA27_E";
        internal const string OaEDisplayName = "OA-27E Wraith";
        internal const string OaEShortName = "OA-27E";
        internal const string OaEEncDescription =
            "OA-27E Wraith is the PALA dash Cavalier: triple turbine, standing ECM, and ground autocannons cannot damage it. No stealth kit and no suicide charge. Same OA WSO station as C/D while he is aboard (Y/N flares and locks); first eject punches him, second bails you out. Boscali hangars do not list it.";
        internal const string FuzeHud = "[Suicide fuze armed]";
        internal const string GearUpHint = "[Press \\ to set fuze of suicide bomb]";
        internal const string EjectDenyHud = "you cannot eject because this is kamikaze drone";
        private const int HintFlashCount = 5;
        private const float HintOnSec = 0.4f;
        private const float HintOffSec = 0.25f;
        internal const float SpeedCapKmh = 900f;
        internal const float SpeedCapMps = 900f / 3.6f;
        internal const float ImpactKmh = 150f;
        internal const float ImpactMps = 150f / 3.6f;
        internal const float YieldKt = 1f;
        internal const float BlastYield1kt = 1000000f;
        internal const float DemolishRadius = 15f;
        internal const float TargetTwR = 1f;
        /// <summary>OA-27 family turboprop: keep native engine/prop units, times this.</summary>
        internal const float PropPowerMul = 2f;
        internal const float PropPowerMulE = 3f;
        internal const float StrengthMul = 8f;
        internal const float StrengthMulD = 16f;
        // Local +Z is the nose. Encyclopedia pose only — live CG stays stock MiG-15.
        internal const float CgLocalZ = 0.85f;
        internal const float EncCgLocalZ = 1.2f;
        internal const float IrMin = 1.2f;
        internal const float IrMax = 3f;
        internal const float AbThrottleStart = 0.90f;
        internal const float AbThrustMul = 0f;
        /// <summary>Hangar rank. OA-27C unlocks at rank 1.</summary>
        internal const int DisplayRank = 1;
        internal const int ActualRank = 1;
        /// <summary>Fallback if donor OA-27 has no value yet. Millions.</summary>
        internal const float CostFallback = 0.006f;
        internal const float CostOfDonor = 0.70f;

        private static readonly FieldInfo ShockYieldField =
            AccessTools.Field(typeof(Shockwave), "yieldKilotons");
        private static readonly FieldInfo BlastYieldField =
            AccessTools.Field(typeof(Missile), "blastYield");
        private static readonly FieldInfo AeroJoints =
            AccessTools.Field(typeof(AeroPart), "joints");
        private static readonly FieldInfo UnitImpact =
            AccessTools.Field(typeof(UnitPart), "impactDamage");
        private static readonly FieldInfo UnitStructural =
            AccessTools.Field(typeof(UnitPart), "structuralThreshold");
        private static readonly FieldInfo MountDisabledField =
            AccessTools.Field(typeof(WeaponMount), "disabled");
        private static readonly FieldInfo ImpactThreshold =
            AccessTools.Field(typeof(ImpactDamage), "threshold");
        private static readonly FieldInfo ImpactMultiplier =
            AccessTools.Field(typeof(ImpactDamage), "multiplier");
        private static readonly FieldInfo FilterParams =
            AccessTools.Field(typeof(ControlsFilter), "aircraftParameters");
        private static readonly FieldInfo AircraftEjected =
            AccessTools.Field(typeof(Aircraft), "ejected");
        private static readonly FieldInfo PilotNumberField =
            AccessTools.Field(typeof(Pilot), "pilotNumber");
        private static readonly FieldInfo PlayerStatePilot =
            AccessTools.Field(typeof(PilotBaseState), "pilot");
        private static readonly FieldInfo PlayerStateRewired =
            AccessTools.Field(typeof(PilotPlayerState), "player");
        private static readonly FieldInfo GameManagerPlayerInput =
            AccessTools.Field(typeof(GameManager), "playerInput");
        private static readonly PropertyInfo GameManagerPlayerInputProp =
            AccessTools.Property(typeof(GameManager), "playerInput");
        private static MethodInfo _rewiredButtonDown;
        private static readonly FieldInfo PilotSeatField =
            AccessTools.Field(typeof(Pilot), "ejectionSeat");
        private static readonly FieldInfo MapBuildingSetField =
            AccessTools.Field(typeof(MapBuilding), "buildingSet");
        private static readonly FieldInfo MapBuildingIndexField =
            AccessTools.Field(typeof(MapBuilding), "index");
        private static readonly FieldInfo JetAircraft =
            AccessTools.Field(typeof(Turbojet), "aircraft");
        private static readonly FieldInfo JetThrustNow =
            AccessTools.Field(typeof(Turbojet), "thrust");
        private static readonly FieldInfo JetMinDensity =
            AccessTools.Field(typeof(Turbojet), "minDensity");
        private static readonly FieldInfo JetAltThrust =
            AccessTools.Field(typeof(Turbojet), "altitudeThrust");
        private static readonly FieldInfo JetMaxSpeed =
            AccessTools.Field(typeof(Turbojet), "maxSpeed");
        private static readonly FieldInfo JetOperable =
            AccessTools.Field(typeof(Turbojet), "operable");
        private static readonly FieldInfo TankCapacity =
            AccessTools.Field(typeof(FuelTank), "fuelCapacity");
        private static readonly FieldInfo NozzleAircraft =
            AccessTools.Field(typeof(JetNozzle), "aircraft");
        private static readonly FieldInfo NozzleIRMin =
            AccessTools.Field(typeof(JetNozzle), "IRMin");
        private static readonly FieldInfo NozzleIRMax =
            AccessTools.Field(typeof(JetNozzle), "IRMax");
        private static readonly FieldInfo NozzleAfterburners =
            AccessTools.Field(typeof(JetNozzle), "afterburners");
        private static readonly FieldInfo NozzleGlow =
            AccessTools.Field(typeof(JetNozzle), "glow");
        private static readonly FieldInfo NozzleThrustAudio =
            AccessTools.Field(typeof(JetNozzle), "thrustAudio");
        private static readonly FieldInfo NozzleIrSource =
            AccessTools.Field(typeof(JetNozzle), "irSource");
        private static readonly FieldInfo NozzleThrustXf =
            AccessTools.Field(typeof(JetNozzle), "thrustTransform");
        private static readonly FieldInfo EncCostText =
            AccessTools.Field(typeof(EncyclopediaBrowser), "cost");
        private static readonly FieldInfo PropNominalPower =
            AccessTools.Field(typeof(ConstantSpeedProp), "nominalPower");
        private static readonly FieldInfo PropFanNominalPower =
            AccessTools.Field(typeof(PropFan), "nominalPower");
        private static readonly FieldInfo TransMaxPower =
            AccessTools.Field(typeof(Transmission), "maxPowerOutput");
        private static readonly FieldInfo TurbojetAbOn =
            AccessTools.Field(typeof(Turbojet), "afterburnerOn");
        private static readonly Type AfterburnerType =
            AccessTools.Inner(typeof(JetNozzle), "Afterburner");
        private static readonly FieldInfo AbFlameRenderer =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "flameRenderer") : null;
        private static readonly FieldInfo AbGlowRenderer =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "nozzleGlowRenderer") : null;
        private static readonly FieldInfo AbThrottleStartField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "throttleStart") : null;
        private static readonly FieldInfo AbThrottleEndField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "throttleEnd") : null;
        private static readonly FieldInfo AbThrustField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "thrust") : null;
        private static readonly FieldInfo AbFuelField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "fuelConsumption") : null;
        private static readonly FieldInfo AbFlameBrightField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "flameBrightness") : null;
        private static readonly FieldInfo AbGlowBrightField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "nozzleGlowBrightness") : null;
        private static readonly FieldInfo AbIRField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "IRIntensity") : null;
        private static readonly FieldInfo AbSmoothingField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "smoothing") : null;
        private static readonly FieldInfo AbSourceField =
            AfterburnerType != null ? AccessTools.Field(AfterburnerType, "source") : null;
        private static readonly Collider[] DemolishHits = new Collider[1024];
        private const float ShipWreckSettleSec = 8f;
        private const float ShipWreckMaxHoriz = 6f;
        private static readonly List<Ship> ShipWrecks = new List<Ship>(8);
        private static readonly Dictionary<int, float> ShipWreckUntil =
            new Dictionary<int, float>(8);

        private static readonly HashSet<int> Detonated = new HashSet<int>();
        private static readonly HashSet<int> FuzeOn = new HashSet<int>();
        private static readonly Dictionary<int, byte> OursCache = new Dictionary<int, byte>(64);
        private static readonly HashSet<int> CgMassDone = new HashSet<int>();
        private static readonly HashSet<int> PilotHidden = new HashSet<int>();
        private static readonly HashSet<int> EngineTuned = new HashSet<int>();
        private static bool _donorLiveriesCopied;
        private static readonly HashSet<int> GunsStripped = new HashSet<int>();
        private static readonly Dictionary<int, float> Mtow = new Dictionary<int, float>();
        private static readonly Dictionary<int, Rigidbody[]> ClampBodies = new Dictionary<int, Rigidbody[]>();
        private static readonly Dictionary<int, FlightMem> Flight = new Dictionary<int, FlightMem>();
        private static readonly Dictionary<int, float> SpawnedAt = new Dictionary<int, float>();
        private static bool _skipDisableBoom;
        internal static bool SkipDisableBoom
        {
            get { return _skipDisableBoom; }
        }
        private static GUIStyle _prompt;
        private static GUIStyle _hintStyle;
        private static GameObject _nukeFx;
        private static int _boundId;
        private static LandingGear.GearState _prevGear = LandingGear.GearState.Uninitialized;
        private static bool _hintUsedThisUp;
        private static float _hintStart;
        private static float _nextEnc;
        private static readonly HashSet<int> EncOnce = new HashSet<int>();
        private static readonly HashSet<int> OaRearEjected = new HashSet<int>();
        private static readonly HashSet<int> CatalogDetached = new HashSet<int>();
        private static readonly HashSet<int> PowerDone = new HashSet<int>();
        private static readonly HashSet<int> FlyHudOnce = new HashSet<int>();
        private static readonly HashSet<int> CrewShownOnce = new HashSet<int>();
        private static readonly Dictionary<int, float> EngineWantKw = new Dictionary<int, float>(8);
        private static readonly Dictionary<int, float> CachedPowerKw = new Dictionary<int, float>(8);
        private static readonly Dictionary<int, float> CrewShownAt = new Dictionary<int, float>(8);
        private static readonly List<PendingYank> SeatYanks = new List<PendingYank>(4);
        private static GameObject _dismountPrefab;
        private static bool _dismountTried;

        private struct PendingYank
        {
            public Aircraft ac;
            public Transform xf;
            public float at;
        }
        private static readonly HashSet<int> CStallCut = new HashSet<int>();
        private static AircraftDefinition _donorDef;
        private static AircraftDefinition _migClone;
        private static AircraftDefinition _oaClone;
        private static AircraftDefinition _oaDClone;
        private static AircraftDefinition _oaEClone;
        private static readonly Dictionary<int, float> PropBasePower =
            new Dictionary<int, float>(8);
        private static float _nextDonorScan;
        private static float _ejectDenyUntil;
        private static GUIStyle _ejectDenyStyle;
        private static float _fuzeArmedAt;
        private static bool _hangarPreviewSpawn;
        private static Aircraft _hangarPreview;
        private static bool _spawnBind;
        private static Aircraft _ejectIntentAc;
        private static float _ejectIntentUntil;
        private static Aircraft _switchAway;
        private static List<WeaponMount>[] _oaStockSets;
        private static string[] _oaStockNames;
        private static int _oaStockMounts;
        private static bool _oaStockLogged;
        private static float _nextStockScan;

        internal static bool IsOurs(Aircraft ac)
        {
            if (ac == null)
                return false;
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (def == null)
                return false;
            int id = ac.GetInstanceID();
            byte cached;
            if (OursCache.TryGetValue(id, out cached))
                return cached == 1;
            bool ours = IsOursDef(def);
            if (ours)
                OursCache[id] = 1;
            else if (!IsDonorDef(def))
                OursCache[id] = 2;
            return ours;
        }

        internal static bool IsOursDef(AircraftDefinition def)
        {
            return IsOursUnit(def);
        }

        internal static bool IsDonorDef(UnitDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.jsonKey))
                return false;
            return string.Equals(def.jsonKey, OaDonorKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOaDef(UnitDefinition def)
        {
            if (def == null)
                return false;
            string key = def.jsonKey;
            if (!string.IsNullOrEmpty(key)
                && string.Equals(key, OaJsonKey, StringComparison.OrdinalIgnoreCase))
                return true;
            string n = def.unitName != null ? def.unitName : string.Empty;
            return n.IndexOf("OA-27C", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(n, OaDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOaDDef(UnitDefinition def)
        {
            if (def == null)
                return false;
            string key = def.jsonKey;
            if (!string.IsNullOrEmpty(key)
                && string.Equals(key, OaDJsonKey, StringComparison.OrdinalIgnoreCase))
                return true;
            string n = def.unitName != null ? def.unitName : string.Empty;
            return n.IndexOf("OA-27D", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(n, OaDDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOaEDef(UnitDefinition def)
        {
            if (def == null)
                return false;
            string key = def.jsonKey;
            if (!string.IsNullOrEmpty(key)
                && string.Equals(key, OaEJsonKey, StringComparison.OrdinalIgnoreCase))
                return true;
            string n = def.unitName != null ? def.unitName : string.Empty;
            return n.IndexOf("OA-27E", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(n, OaEDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOaConventionalDef(UnitDefinition def)
        {
            return IsOaDDef(def) || IsOaEDef(def);
        }

        internal static bool IsBdfExclusiveDef(UnitDefinition def)
        {
            return IsOaDDef(def);
        }

        internal static bool IsPalaExclusiveDef(UnitDefinition def)
        {
            return IsOaEDef(def);
        }

        internal static bool IsOaFamilyDef(UnitDefinition def)
        {
            return IsOaDef(def) || IsOaDDef(def) || IsOaEDef(def);
        }

        internal static bool IsOaClone(Aircraft ac)
        {
            if (ac == null)
                return false;
            return IsOaDef(ac.definition as AircraftDefinition);
        }

        internal static bool IsOaDClone(Aircraft ac)
        {
            if (ac == null)
                return false;
            return IsOaDDef(ac.definition as AircraftDefinition);
        }

        internal static bool IsOaEClone(Aircraft ac)
        {
            if (ac == null)
                return false;
            return IsOaEDef(ac.definition as AircraftDefinition);
        }

        internal static bool IsOaConventionalClone(Aircraft ac)
        {
            return IsOaDClone(ac) || IsOaEClone(ac);
        }

        internal static bool IsOaFamilyClone(Aircraft ac)
        {
            return IsOaClone(ac) || IsOaDClone(ac) || IsOaEClone(ac);
        }

        internal static bool IsOaPowered(Aircraft ac)
        {
            if (ac == null)
                return false;
            if (IsOaFamilyClone(ac))
                return true;
            AircraftDefinition cur = ac.definition as AircraftDefinition;
            if (!IsDonorDef(cur))
                return false;
            if (!MayRetargetDonorHull(ac))
                return false;
            return IsOaFamilyDef(LoadoutLock.ActiveSpawnDef());
        }

        internal static bool HasOaWso(Aircraft ac)
        {
            if (ac == null || !IsOaFamilyClone(ac))
                return false;
            return !OaRearEjected.Contains(ac.GetInstanceID());
        }

        internal static float PowerMulOf(Aircraft ac)
        {
            if (IsOaEClone(ac))
                return PropPowerMulE;
            AircraftDefinition want = LoadoutLock.ActiveSpawnDef();
            if (IsOaEDef(want))
                return PropPowerMulE;
            return PropPowerMul;
        }

        internal static float StrengthMulOf(Aircraft ac)
        {
            if (IsOaDClone(ac))
                return StrengthMulD;
            return StrengthMul;
        }

        internal static bool IsMigClone(Aircraft ac)
        {
            if (ac == null)
                return false;
            return IsMigCloneDef(ac.definition as AircraftDefinition);
        }

        internal static bool IsMigCloneDef(UnitDefinition def)
        {
            if (def == null || IsOaFamilyDef(def) || IsDonorDef(def))
                return false;
            return IsOursUnit(def);
        }

        internal static AircraftDefinition MigClone
        {
            get { return _migClone; }
        }

        internal static AircraftDefinition OaClone
        {
            get { return _oaClone; }
        }

        internal static AircraftDefinition OaDClone
        {
            get { return _oaDClone; }
        }

        internal static AircraftDefinition OaEClone
        {
            get { return _oaEClone; }
        }

        internal static bool IsOursUnit(UnitDefinition def)
        {
            return IsOaFamilyDef(def);
        }

        internal static bool LocalPlayerMaySelectOaD()
        {
            if (LocalPlayerIsPala())
                return false;
            return true;
        }

        internal static bool LocalPlayerMaySelectOaE()
        {
            if (LocalPlayerIsBdf())
                return false;
            return true;
        }

        internal static bool LocalPlayerMaySelectExclusive(UnitDefinition def)
        {
            if (IsOaDDef(def))
                return LocalPlayerMaySelectOaD();
            if (IsOaEDef(def))
                return LocalPlayerMaySelectOaE();
            return true;
        }

        internal static bool LocalPlayerIsPala()
        {
            return HqIsPala(LocalPlayerHq());
        }

        internal static bool LocalPlayerIsBdf()
        {
            return HqIsBdf(LocalPlayerHq());
        }

        private static FactionHQ LocalPlayerHq()
        {
            FactionHQ hq = null;
            try
            {
                Player p;
                if (GameManager.GetLocalPlayer(out p) && p != null)
                    hq = p.HQ;
            }
            catch { hq = null; }
            if (hq == null)
            {
                try
                {
                    FactionHQ localHq;
                    if (GameManager.GetLocalHQ(out localHq))
                        hq = localHq;
                }
                catch { hq = null; }
            }
            return hq;
        }

        internal static bool HqIsBdf(FactionHQ hq)
        {
            if (hq == null)
                return false;
            if (HqIsPala(hq))
                return false;
            return FactionLooksBdf(FactionLabel(hq));
        }

        internal static bool HqIsPala(FactionHQ hq)
        {
            if (hq == null)
                return false;
            return FactionLooksPala(FactionLabel(hq));
        }

        private static string FactionLabel(FactionHQ hq)
        {
            string n = "";
            try
            {
                if (hq.faction != null && !string.IsNullOrEmpty(hq.faction.factionName))
                    n = hq.faction.factionName;
            }
            catch { }
            if (string.IsNullOrEmpty(n))
            {
                try { n = hq.name; }
                catch { n = ""; }
            }
            return n != null ? n : "";
        }

        private static bool FactionLooksPala(string n)
        {
            if (string.IsNullOrEmpty(n))
                return false;
            string u = n.ToUpperInvariant();
            return u.IndexOf("PALA", StringComparison.Ordinal) >= 0
                || u.IndexOf("PRIMEVA", StringComparison.Ordinal) >= 0;
        }

        private static bool FactionLooksBdf(string n)
        {
            if (string.IsNullOrEmpty(n))
                return false;
            string u = n.ToUpperInvariant();
            return u.IndexOf("BDF", StringComparison.Ordinal) >= 0
                || u.IndexOf("BOSCALI", StringComparison.Ordinal) >= 0;
        }

        internal static float OaPrice()
        {
            AircraftDefinition donor = FindDefByKey(OaDonorKey);
            if (donor != null && donor.value > 0.0001f)
            {
                float price = donor.value * CostOfDonor;
                if (price >= donor.value)
                    price = donor.value * 0.90f;
                if (price < 0.0001f)
                    price = donor.value * 0.50f;
                return price;
            }
            return CostFallback;
        }

        internal static bool MeetsRank(AircraftDefinition def)
        {
            int need = ActualRank;
            if (def != null && def.aircraftParameters != null)
                need = def.aircraftParameters.rankRequired;
            if (need <= 0)
                return true;
            int have = 0;
            try
            {
                Player p;
                if (GameManager.GetLocalPlayer(out p) && p != null)
                    have = p.PlayerRank;
            }
            catch { }
            return have >= need;
        }

        internal static bool IsOaLoadoutContext(Aircraft ac, HardpointSet hs)
        {
            if (IsOaFamilyClone(ac) || IsOaHangarPreview(ac))
                return true;
            Aircraft owner = ac != null ? ac : LoadoutLock.FindAircraft(hs);
            if (owner == null)
                owner = LoadoutLock.SelectorAircraft;
            if (IsOaFamilyClone(owner) || IsOaHangarPreview(owner))
                return true;
            AircraftDefinition sel = LoadoutLock.ActiveSpawnDef();
            if (!IsOaFamilyDef(sel))
                return false;
            if (owner == null)
                return false;
            if (IsDonorDef(owner.definition as AircraftDefinition))
                return true;
            return IsOaFamilyClone(owner);
        }

        internal static bool IsOaHangarPreview(Aircraft ac)
        {
            if (ac == null)
                return false;
            if (_hangarPreviewSpawn)
            {
                try
                {
                    if (!ac.networked)
                    {
                        return IsOaFamilyClone(ac)
                            || IsDonorDef(ac.definition as AircraftDefinition);
                    }
                }
                catch { }
                return false;
            }
            if (_hangarPreview != null && object.ReferenceEquals(ac, _hangarPreview))
                return true;
            if (IsLiveAircraft(ac))
                return false;
            if (IsOaFamilyClone(ac))
                return true;
            AircraftDefinition sel = LoadoutLock.ActiveSpawnDef();
            return IsDonorDef(ac.definition as AircraftDefinition) && IsOaFamilyDef(sel);
        }

        internal static bool HasSimAuthority(Aircraft ac)
        {
            if (ac == null)
                return false;
            try
            {
                if (!ac.networked)
                    return true;
            }
            catch { }
            try
            {
                if (ac.IsServer)
                    return true;
            }
            catch { }
            return IsLocalPlayerAircraft(ac);
        }

        private sealed class FlightMem
        {
            public float LastFastMps;
            public float LastFastAt;
            public bool GearUp;
            public bool Airborne;
        }

        private static FlightMem Mem(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            FlightMem m;
            if (!Flight.TryGetValue(id, out m) || m == null)
            {
                m = new FlightMem();
                Flight[id] = m;
            }
            return m;
        }

        internal static void RememberFlight(Aircraft ac)
        {
            if (ac == null)
                return;
            FlightMem m = Mem(ac);
            float spd = 0f;
            try { spd = ac.speed; }
            catch { spd = 0f; }
            if (spd >= ImpactMps)
            {
                m.LastFastMps = spd;
                m.LastFastAt = Time.unscaledTime;
            }
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            LandingGear.GearState gear = LandingGear.GearState.Uninitialized;
            try { gear = ac.gearState; }
            catch { gear = LandingGear.GearState.Uninitialized; }
            bool gearUp = gear == LandingGear.GearState.Retracting
                || gear == LandingGear.GearState.LockedRetracted;
            if (gearUp)
                m.GearUp = true;
            if (alt > 8f)
                m.Airborne = true;
            if (!gearUp && alt < 12f)
            {
                m.GearUp = false;
                m.Airborne = false;
                if (spd < ImpactMps)
                    m.LastFastAt = 0f;
            }
        }

        private static bool GearIsUp(Aircraft ac)
        {
            if (ac == null)
                return false;
            LandingGear.GearState gear = LandingGear.GearState.Uninitialized;
            try { gear = ac.gearState; }
            catch { return false; }
            return gear == LandingGear.GearState.Retracting
                || gear == LandingGear.GearState.LockedRetracted;
        }

        private static bool FuzeSettled(Aircraft ac)
        {
            if (!FuzeArmed(ac))
                return false;
            if (_fuzeArmedAt <= 0.01f)
                return false;
            return (Time.unscaledTime - _fuzeArmedAt) >= 1f;
        }

        private static bool RecentFast(Aircraft ac)
        {
            if (ac == null)
                return false;
            FlightMem m = Mem(ac);
            if (m.LastFastMps >= ImpactMps && (Time.unscaledTime - m.LastFastAt) < 2f)
                return true;
            float spd = 0f;
            try { spd = ac.speed; }
            catch { spd = 0f; }
            return spd >= ImpactMps;
        }

        private static bool WasFlying(Aircraft ac)
        {
            if (ac == null)
                return false;
            FlightMem m = Mem(ac);
            return m.GearUp || m.Airborne;
        }

        internal static void Watchdog(Aircraft ac)
        {
            if (ac == null || !IsOurs(ac) || !FuzeArmed(ac) || !FuzeSettled(ac))
                return;
            if (!GearIsUp(ac))
                return;
            if (!WasFlying(ac) || !RecentFast(ac))
                return;
            float spd = 0f;
            try { spd = ac.speed; }
            catch { spd = 0f; }
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            bool dead = false;
            try { dead = ac.disabled; }
            catch { dead = false; }
            if (dead)
            {
                if (!FlightFix.RecentExternalHit(ac))
                    return;
                TryDetonate(ac, "watchdog-dead");
                return;
            }
            if (spd < 12f && alt < 40f)
                TryDetonate(ac, "watchdog-impact");
        }

        internal static void NoteHit(Aircraft ac, Collision collision)
        {
            if (!CollisionShouldBoom(ac, collision))
                return;
            TryDetonate(ac, "impact");
        }

        private static bool IsSelfHit(Aircraft ac, Collision collision)
        {
            if (ac == null || collision == null)
                return false;
            Transform other = null;
            try { other = collision.collider != null ? collision.collider.transform : null; }
            catch { other = null; }
            if (other == null)
                return false;
            Aircraft otherAc = other.GetComponentInParent<Aircraft>();
            return otherAc != null && object.ReferenceEquals(otherAc, ac);
        }

        internal static bool FuzeArmed(Aircraft ac)
        {
            if (ac == null || IsOaConventionalClone(ac))
                return false;
            return IsLocalPlayerAircraft(ac) && FuzeOn.Contains(ac.GetInstanceID());
        }

        internal static bool IsLocalPlayerAircraft(Aircraft ac)
        {
            if (ac == null)
                return false;
            try { return GameManager.IsLocalAircraft(ac); }
            catch { return false; }
        }

        internal static bool LocalHudOk(Aircraft ac)
        {
            if (ac == null)
                return false;
            if (!IsOurs(ac) || !IsLiveAircraft(ac))
                return false;
            if (IsOaConventionalClone(ac))
                return false;
            try
            {
                if (ac.disabled)
                    return false;
            }
            catch { }
            if (!IsLocalPlayerAircraft(ac))
                return false;
            try
            {
                Player p;
                if (GameManager.GetLocalPlayer(out p) && p != null)
                {
                    if (p.Aircraft == null || !object.ReferenceEquals(p.Aircraft, ac))
                        return false;
                }
            }
            catch { }
            try
            {
                if (GameplayUI.ShouldShowSpectatorPanel())
                    return false;
            }
            catch { }
            return true;
        }

        internal static void Tick()
        {
            TickShipWrecks();
            Aircraft ac;
            if (!GameManager.GetLocalAircraft(out ac) || ac == null)
            {
                ResetHintLocal();
                MaybeStamp();
                return;
            }
            if (!LocalHudOk(ac))
            {
                ResetHintLocal();
                MaybeStamp();
                return;
            }
            if (IsOaConventionalClone(ac))
            {
                ResetHintLocal();
                MaybeStamp();
                return;
            }
            PollGearHint(ac);
            if (!InEncyclopedia() && Input.GetKeyDown(KeyCode.BackQuote))
                TryDetonate(ac, "manual", false);
            if (Input.GetKeyDown(KeyCode.Backslash))
                ToggleFuze(ac);
        }

        private static void ToggleFuze(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            if (FuzeOn.Contains(id))
            {
                FuzeOn.Remove(id);
                _fuzeArmedAt = 0f;
            }
            else
            {
                FuzeOn.Add(id);
                _fuzeArmedAt = Time.unscaledTime;
                _hintStart = 0f;
            }
        }

        private static void MaybeStamp()
        {
            if (Time.unscaledTime < _nextEnc)
                return;
            _nextEnc = Time.unscaledTime + 8f;
            StampAllDefs();
        }

        internal static void StampAllDefs()
        {
            EnsureClones();
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc != null && enc.aircraft != null)
            {
                for (int i = 0; i < enc.aircraft.Count; i++)
                    ApplyEncyclopedia(enc.aircraft[i]);
            }
            else
            {
                ApplyEncyclopedia(_migClone);
                ApplyEncyclopedia(_oaClone);
                ApplyEncyclopedia(_oaDClone);
                ApplyEncyclopedia(_oaEClone);
            }
            if (_oaClone != null)
                HangarInject.RegisterNetwork(_oaClone);
            if (_oaDClone != null)
                HangarInject.RegisterNetwork(_oaDClone);
            if (_oaEClone != null)
                HangarInject.RegisterNetwork(_oaEClone);
        }

        private static void ResetHintLocal()
        {
            _boundId = 0;
            _prevGear = LandingGear.GearState.Uninitialized;
            _hintUsedThisUp = false;
            _hintStart = 0f;
        }

        private static void PollGearHint(Aircraft ac)
        {
            int id = ac.GetInstanceID();
            if (id != _boundId)
            {
                _boundId = id;
                _prevGear = LandingGear.GearState.Uninitialized;
                _hintUsedThisUp = false;
                _hintStart = 0f;
            }
            LandingGear.GearState now = LandingGear.GearState.Uninitialized;
            try { now = ac.gearState; }
            catch { now = LandingGear.GearState.Uninitialized; }
            LandingGear.GearState prev = _prevGear;
            _prevGear = now;
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            bool down = now == LandingGear.GearState.LockedExtended
                || now == LandingGear.GearState.Extending;
            if (down && alt < 12f)
            {
                _hintUsedThisUp = false;
                return;
            }
            if (_hintUsedThisUp)
                return;
            if (FuzeArmed(ac))
                return;
            bool wasDown = prev == LandingGear.GearState.LockedExtended
                || prev == LandingGear.GearState.Extending;
            bool goingUp = now == LandingGear.GearState.Retracting
                || now == LandingGear.GearState.LockedRetracted;
            if (!wasDown || !goingUp)
                return;
            if (alt < 3f)
            {
                float spd = 0f;
                try { spd = ac.speed; }
                catch { spd = 0f; }
                if (spd < 40f)
                    return;
            }
            _hintUsedThisUp = true;
            _hintStart = Time.unscaledTime;
        }

        private static bool HintFlashVisible()
        {
            if (_hintStart <= 0.01f)
                return false;
            float cycle = HintOnSec + HintOffSec;
            float elapsed = Time.unscaledTime - _hintStart;
            if (elapsed < 0f)
                return false;
            int n = (int)(elapsed / cycle);
            if (n >= HintFlashCount)
                return false;
            float phase = elapsed - (n * cycle);
            return phase < HintOnSec;
        }

        internal static void Draw()
        {
            Aircraft ac;
            if (!GameManager.GetLocalAircraft(out ac) || ac == null)
                return;
            if (!LocalHudOk(ac))
                return;
            string line = null;
            if (Time.unscaledTime < _ejectDenyUntil)
                line = EjectDenyHud;
            else if (FuzeArmed(ac))
                line = FuzeHud;
            else if (HintFlashVisible())
                line = GearUpHint;
            if (line == null)
                return;
            if (_ejectDenyStyle == null)
            {
                _ejectDenyStyle = new GUIStyle(GUI.skin.label);
                _ejectDenyStyle.alignment = TextAnchor.MiddleCenter;
                _ejectDenyStyle.fontSize = 26;
                _ejectDenyStyle.fontStyle = FontStyle.Bold;
                _ejectDenyStyle.normal.textColor = new Color(1f, 0.2f, 0.15f, 1f);
            }
            if (_prompt == null)
            {
                _prompt = new GUIStyle(GUI.skin.label);
                _prompt.alignment = TextAnchor.MiddleCenter;
                _prompt.fontSize = 22;
                _prompt.fontStyle = FontStyle.Bold;
                _prompt.normal.textColor = new Color(1f, 0.85f, 0.15f, 1f);
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label);
                _hintStyle.alignment = TextAnchor.MiddleCenter;
                _hintStyle.fontSize = 22;
                _hintStyle.fontStyle = FontStyle.Bold;
                _hintStyle.normal.textColor = new Color(1f, 0.92f, 0.35f, 1f);
            }
            float w = 720f;
            Rect r = new Rect((Screen.width - w) * 0.5f, 36f, w, 40f);
            GUIStyle st = _hintStyle;
            if (line == EjectDenyHud)
                st = _ejectDenyStyle;
            else if (FuzeArmed(ac))
                st = _prompt;
            GUI.Label(r, line, st);
        }

        internal static void ApplyEncyclopedia(AircraftDefinition def)
        {
            if (def == null)
                return;
            if (IsDonorDef(def))
                return;
            if (!IsOursDef(def))
                return;
            bool oaE = IsOaEDef(def);
            bool oaD = IsOaDDef(def);
            bool oaC = IsOaDef(def);
            bool oa = oaC || oaD || oaE;
            string display = oaE ? OaEDisplayName : (oaD ? OaDDisplayName : (oaC ? OaDisplayName : DisplayName));
            string code = oaE ? OaEShortName : (oaD ? OaDShortName : (oaC ? OaShortName : ShortName));
            string desc = oaE ? OaEEncDescription : (oaD ? OaDEncDescription : (oaC ? OaEncDescription : EncDescription));
            string key = oaE ? OaEJsonKey : (oaD ? OaDJsonKey : (oaC ? OaJsonKey : JsonKey));
            def.jsonKey = key;
            def.unitName = display;
            def.code = code;
            def.bogeyName = code;
            def.dontAutomaticallyAddToEncyclopedia = false;
            TypeIdentity type = def.typeIdentity;
            if (type.air < 0.5f)
            {
                type.air = 1f;
                def.typeIdentity = type;
            }
            RoleIdentity role = def.roleIdentity;
            if (role.antiAir < 0.2f)
            {
                role.antiAir = 1f;
                if (role.antiSurface < 0.1f)
                    role.antiSurface = 0.35f;
                def.roleIdentity = role;
            }
            if (def.aircraftParameters != null)
            {
                def.aircraftParameters.aircraftName = display;
                def.aircraftParameters.rankRequired = DisplayRank;
                if (!oa)
                    def.aircraftParameters.maxSpeed = SpeedCapMps;
            }
            if (!oa && def.aircraftInfo != null)
                def.aircraftInfo.maxSpeed = SpeedCapMps;
            if (oa)
                def.value = OaPrice();
            else
                def.value = CostFallback;
            def.description = desc;
            if (oaC)
            {
                def.radarSize = 0f;
                if (def.aircraftInfo != null && CStallCut.Add(def.GetInstanceID()))
                    def.aircraftInfo.stallSpeed = def.aircraftInfo.stallSpeed * 0.72f;
            }
            if (Encyclopedia.Lookup != null)
                Encyclopedia.Lookup[key] = def;
            if (oa)
            {
                AdoptOaLiveries(def);
                AdoptOaStandardLoadouts(def);
            }
            else
            {
                StripGunsFromDefinition(def);
                AdoptDonorLiveries(def);
            }
        }

        internal static bool IsKamDef(AircraftDefinition def)
        {
            if (def == null || IsOaFamilyDef(def) || IsDonorDef(def))
                return false;
            return IsMigCloneDef(def);
        }

        internal static AircraftDefinition FindDefByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            if (Encyclopedia.Lookup != null)
            {
                UnitDefinition u;
                if (Encyclopedia.Lookup.TryGetValue(key, out u))
                {
                    AircraftDefinition hit = u as AircraftDefinition;
                    if (hit != null
                        && string.Equals(hit.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                        return hit;
                }
            }
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc != null && enc.aircraft != null)
            {
                for (int i = 0; i < enc.aircraft.Count; i++)
                {
                    AircraftDefinition d = enc.aircraft[i];
                    if (d != null
                        && string.Equals(d.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                        return d;
                }
            }
            if (Time.unscaledTime < _nextDonorScan)
                return null;
            _nextDonorScan = Time.unscaledTime + 8f;
            AircraftDefinition[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<AircraftDefinition>(); }
            catch { all = null; }
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                AircraftDefinition d = all[i];
                if (d != null
                    && string.Equals(d.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }

        internal static AircraftDefinition FindDonorDef()
        {
            if (_donorDef != null)
                return _donorDef;
            AircraftDefinition hit = FindDefByKey(OaDonorKey);
            if (hit != null)
                _donorDef = hit;
            return _donorDef;
        }

        private static bool _cloneReadyLogged;
        private static bool _oaLoadoutsAdopted;
        private static float _nextDonorWarn;

        internal static void EnsureClones()
        {
            if (_oaClone != null && _oaDClone != null && _oaEClone != null)
            {
                if (!_oaLoadoutsAdopted)
                {
                    SnapshotOaDonorLoadouts();
                    AdoptOaStandardLoadouts(_oaClone);
                    AdoptOaStandardLoadouts(_oaDClone);
                    AdoptOaStandardLoadouts(_oaEClone);
                    _oaLoadoutsAdopted = true;
                }
                if (!_cloneReadyLogged && Plugin.Log != null)
                {
                    _cloneReadyLogged = true;
                    Plugin.Log.LogInfo("OA-27C / OA-27D / OA-27E ready (cloned from "
                        + OaDonorKey + ")");
                }
                return;
            }
            NobpDonor.Ensure();
            if (_oaClone == null)
                _oaClone = MakeClone(
                    FindDefByKey(OaDonorKey),
                    OaJsonKey,
                    OaDisplayName,
                    OaShortName,
                    OaEncDescription,
                    false);
            if (_oaDClone == null)
                _oaDClone = MakeClone(
                    FindDefByKey(OaDonorKey),
                    OaDJsonKey,
                    OaDDisplayName,
                    OaDShortName,
                    OaDEncDescription,
                    false);
            if (_oaEClone == null)
                _oaEClone = MakeClone(
                    FindDefByKey(OaDonorKey),
                    OaEJsonKey,
                    OaEDisplayName,
                    OaEShortName,
                    OaEEncDescription,
                    false);
            if (_oaClone != null && _oaDClone != null && _oaEClone != null)
            {
                if (!_oaLoadoutsAdopted)
                {
                    SnapshotOaDonorLoadouts();
                    AdoptOaStandardLoadouts(_oaClone);
                    AdoptOaStandardLoadouts(_oaDClone);
                    AdoptOaStandardLoadouts(_oaEClone);
                    _oaLoadoutsAdopted = true;
                }
                if (!_cloneReadyLogged && Plugin.Log != null)
                {
                    _cloneReadyLogged = true;
                    Plugin.Log.LogInfo("OA-27C / OA-27D / OA-27E ready (cloned from "
                        + OaDonorKey + ")");
                }
                return;
            }
            AircraftDefinition donor = FindDefByKey(OaDonorKey);
            if (donor == null && Plugin.Log != null && Time.unscaledTime >= _nextDonorWarn)
            {
                _nextDonorWarn = Time.unscaledTime + 8f;
                Plugin.Log.LogWarning("OA-27C/D/E waiting for Aryx donor " + OaDonorKey);
            }
        }

        private static AircraftDefinition MakeClone(
            AircraftDefinition donor,
            string key,
            string display,
            string code,
            string desc,
            bool mig)
        {
            if (donor == null || string.IsNullOrEmpty(key))
                return null;
            if (IsOursDef(donor) && !IsDonorDef(donor)
                && string.Equals(donor.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                return donor;
            AircraftDefinition existing = FindDefByKey(key);
            if (existing != null && !IsDonorDef(existing))
            {
                ApplyEncyclopedia(existing);
                RegisterClone(existing);
                return existing;
            }
            AircraftDefinition clone = UnityEngine.Object.Instantiate(donor);
            if (clone == null)
                return null;
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                INetworkDefinition nd = clone;
                nd.LookupIndex = null;
            }
            catch { }
            clone.jsonKey = key;
            clone.unitName = display;
            clone.code = code;
            clone.bogeyName = code;
            clone.description = desc;
            clone.value = mig ? CostFallback : OaPrice();
            clone.dontAutomaticallyAddToEncyclopedia = false;
            if (donor.aircraftParameters != null)
            {
                AircraftParameters p = UnityEngine.Object.Instantiate(donor.aircraftParameters);
                if (p != null)
                {
                    p.hideFlags = HideFlags.HideAndDontSave;
                    p.aircraftName = display;
                    p.rankRequired = DisplayRank;
                    if (mig)
                        p.maxSpeed = SpeedCapMps;
                    clone.aircraftParameters = p;
                }
            }
            clone.aircraftInfo = CopyAircraftInfo(donor.aircraftInfo);
            if (mig && clone.aircraftInfo != null)
                clone.aircraftInfo.maxSpeed = SpeedCapMps;
            ApplyEncyclopedia(clone);
            RegisterClone(clone);
            SnapshotOaDonorLoadouts();
            AdoptOaStandardLoadouts(clone);
            if (Plugin.Log != null)
            {
                string tag = mig ? "MiG-15S" : "OA-27C";
                if (string.Equals(key, OaDJsonKey, StringComparison.OrdinalIgnoreCase))
                    tag = "OA-27D";
                else if (string.Equals(key, OaEJsonKey, StringComparison.OrdinalIgnoreCase))
                    tag = "OA-27E";
                Plugin.Log.LogInfo(tag + " cloned from " + donor.jsonKey + " as " + key);
            }
            return clone;
        }

        private static AircraftInfo CopyAircraftInfo(AircraftInfo src)
        {
            if (src == null)
                return null;
            AircraftInfo n = new AircraftInfo();
            n.emptyWeight = src.emptyWeight;
            n.maxSpeed = src.maxSpeed;
            n.stallSpeed = src.stallSpeed;
            n.maneuverability = src.maneuverability;
            n.maxWeight = src.maxWeight;
            return n;
        }

        private static void RegisterClone(AircraftDefinition clone)
        {
            if (clone == null || string.IsNullOrEmpty(clone.jsonKey))
                return;
            try
            {
                if (Encyclopedia.Lookup != null)
                    Encyclopedia.Lookup[clone.jsonKey] = clone;
            }
            catch { }
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc != null && enc.aircraft != null)
            {
                bool found = false;
                for (int i = 0; i < enc.aircraft.Count; i++)
                {
                    AircraftDefinition cur = enc.aircraft[i];
                    if (cur == null)
                        continue;
                    if (object.ReferenceEquals(cur, clone)
                        || string.Equals(cur.jsonKey, clone.jsonKey, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        if (!object.ReferenceEquals(cur, clone))
                            enc.aircraft[i] = clone;
                        break;
                    }
                }
                if (!found)
                    enc.aircraft.Add(clone);
            }
            HangarInject.RegisterNetwork(clone);
        }

        internal static void BindCloneDefinition(Aircraft ac)
        {
            if (ac == null)
                return;
            AircraftDefinition cur = ac.definition as AircraftDefinition;
            if (!IsDonorDef(cur))
                return;
            if (!MayRetargetDonorHull(ac))
                return;
            AircraftDefinition want = LoadoutLock.ActiveSpawnDef();
            if (want == null || !IsOursDef(want) || IsDonorDef(want))
                return;
            Unit unit = ac;
            if (string.Equals(cur.jsonKey, DonorJsonKey, StringComparison.OrdinalIgnoreCase)
                && IsMigCloneDef(want))
                unit.definition = want;
            else if (string.Equals(cur.jsonKey, OaDonorKey, StringComparison.OrdinalIgnoreCase)
                && IsOaFamilyDef(want))
                unit.definition = want;
            ApplyEncyclopedia(want);
            try { unit.NetworkunitName = want.unitName; }
            catch { }
        }

        private static bool MayRetargetDonorHull(Aircraft ac)
        {
            if (ac == null)
                return false;
            if (_hangarPreviewSpawn)
            {
                try { return !ac.networked; }
                catch { return true; }
            }
            if (_hangarPreview != null && object.ReferenceEquals(ac, _hangarPreview))
                return true;
            if (_spawnBind)
                return true;
            return IsLocalPlayerAircraft(ac);
        }

        internal static void BeginSpawnBind()
        {
            _spawnBind = true;
        }

        internal static void EndSpawnBind()
        {
            _spawnBind = false;
        }

        internal static void NotePlayerEjectIntent(Aircraft ac)
        {
            if (ac == null)
                return;
            _ejectIntentAc = ac;
            _ejectIntentUntil = Time.unscaledTime + 0.35f;
        }

        internal static void NoteSwitchAway(Aircraft ac)
        {
            _switchAway = ac;
        }

        internal static void ClearSwitchAway()
        {
            _switchAway = null;
        }

        private static bool ConsumePlayerEjectIntent(Aircraft ac)
        {
            if (ac == null || _ejectIntentAc == null)
                return false;
            if (!object.ReferenceEquals(ac, _ejectIntentAc))
                return false;
            if (Time.unscaledTime > _ejectIntentUntil)
                return false;
            _ejectIntentAc = null;
            return true;
        }

        internal static void NoteEjectIfPressed(object playerState)
        {
            if (playerState == null)
                return;
            Aircraft ac = AircraftOfPlayerState(playerState);
            if (ac == null)
                return;
            if (!RewiredButtonDown(playerState, "Eject"))
                return;
            NotePlayerEjectIntent(ac);
        }

        private static Aircraft AircraftOfPlayerState(object playerState)
        {
            if (playerState == null || PlayerStatePilot == null)
                return null;
            Pilot p = null;
            try { p = PlayerStatePilot.GetValue(playerState) as Pilot; }
            catch { p = null; }
            if (p == null)
                return null;
            try { return p.aircraft; }
            catch { return null; }
        }

        private static bool RewiredButtonDown(object playerState, string action)
        {
            if (playerState == null || string.IsNullOrEmpty(action) || PlayerStateRewired == null)
                return false;
            object pi = null;
            try { pi = PlayerStateRewired.GetValue(playerState); }
            catch { pi = null; }
            return RewiredButtonDownOn(pi, action);
        }

        private static bool PlayerEjectHeldNow()
        {
            object pi = null;
            if (GameManagerPlayerInput != null)
            {
                try { pi = GameManagerPlayerInput.GetValue(null); }
                catch { pi = null; }
            }
            if (pi == null && GameManagerPlayerInputProp != null)
            {
                try { pi = GameManagerPlayerInputProp.GetValue(null, null); }
                catch { pi = null; }
            }
            return RewiredButtonDownOn(pi, "Eject");
        }

        private static bool RewiredButtonDownOn(object pi, string action)
        {
            if (pi == null || string.IsNullOrEmpty(action))
                return false;
            if (_rewiredButtonDown == null)
            {
                try
                {
                    _rewiredButtonDown = AccessTools.Method(
                        pi.GetType(),
                        "GetButtonDown",
                        new Type[] { typeof(string) });
                }
                catch { _rewiredButtonDown = null; }
            }
            if (_rewiredButtonDown == null)
                return false;
            try
            {
                object v = _rewiredButtonDown.Invoke(pi, new object[] { action });
                return v is bool && (bool)v;
            }
            catch
            {
                return false;
            }
        }

        internal static bool MountIsNetworked(WeaponMount mount)
        {
            if (mount == null)
                return false;
            try
            {
                INetworkDefinition nd = mount;
                if (nd.LookupIndex.HasValue)
                    return true;
            }
            catch { }
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc == null || enc.IndexLookup == null)
                return false;
            try { return enc.IndexLookup.Contains(mount); }
            catch { return false; }
        }

        internal static void RegisterNetworkMount(WeaponMount mount)
        {
            if (mount == null)
                return;
            Encyclopedia enc = null;
            try { enc = Encyclopedia.i; }
            catch { enc = null; }
            if (enc != null && enc.weaponMounts != null && !enc.weaponMounts.Contains(mount))
                enc.weaponMounts.Add(mount);
            try
            {
                if (Encyclopedia.WeaponLookup != null && !string.IsNullOrEmpty(mount.jsonKey))
                    Encyclopedia.WeaponLookup[mount.jsonKey] = mount;
            }
            catch { }
            if (enc == null || enc.IndexLookup == null)
                return;
            try
            {
                if (!enc.IndexLookup.Contains(mount))
                {
                    enc.IndexLookup.Add(mount);
                    INetworkDefinition nd = mount;
                    nd.LookupIndex = enc.IndexLookup.Count - 1;
                }
                else
                {
                    INetworkDefinition nd = mount;
                    if (!nd.LookupIndex.HasValue)
                    {
                        int idx = enc.IndexLookup.IndexOf(mount);
                        if (idx >= 0)
                            nd.LookupIndex = idx;
                    }
                }
            }
            catch { }
        }

        internal static WeaponMount ResolveNetworkMount(WeaponMount mount)
        {
            if (mount == null)
                return null;
            if (MountIsNetworked(mount))
                return mount;
            string key = mount.jsonKey;
            if (string.Equals(key, GunpodInject.CloneKey, StringComparison.OrdinalIgnoreCase)
                || GunpodInject.IsPod(mount))
                key = GunpodInject.DonorKey;
            WeaponMount byKey = FindMountByKey(key);
            if (byKey != null && MountIsNetworked(byKey))
                return byKey;
            if (!string.Equals(key, GunpodInject.DonorKey, StringComparison.OrdinalIgnoreCase))
            {
                WeaponMount donorPod = FindMountByKey(GunpodInject.DonorKey);
                if (donorPod != null && GunpodInject.IsPod(mount) && MountIsNetworked(donorPod))
                    return donorPod;
            }
            RegisterNetworkMount(mount);
            if (MountIsNetworked(mount))
                return mount;
            return byKey != null ? byKey : mount;
        }

        internal static void SanitizeLoadout(NuclearOption.SavedMission.Loadout loadout)
        {
            if (loadout == null || loadout.weapons == null)
                return;
            int i;
            for (i = 0; i < loadout.weapons.Count; i++)
            {
                WeaponMount m = loadout.weapons[i];
                if (m == null)
                    continue;
                loadout.weapons[i] = ResolveNetworkMount(m);
            }
        }

        internal static void AdoptDonorLiveries(AircraftDefinition kam)
        {
            if (!IsKamDef(kam) || kam.aircraftParameters == null)
                return;
            AircraftDefinition donor = FindDonorDef();
            if (donor == null || object.ReferenceEquals(donor, kam)
                || donor.aircraftParameters == null)
                return;
            List<AircraftParameters.Livery> src = donor.aircraftParameters.liveries;
            if (src == null || src.Count == 0)
                return;
            if (object.ReferenceEquals(kam.aircraftParameters.liveries, src))
                return;
            kam.aircraftParameters.liveries = src;
            if (!_donorLiveriesCopied && Plugin.Log != null)
                Plugin.Log.LogInfo("MiG-15S adopted " + src.Count.ToString() + " donor liveries.");
            _donorLiveriesCopied = true;
        }

        internal static void AdoptOaLiveries(AircraftDefinition clone)
        {
            if (!IsOaFamilyDef(clone) || clone.aircraftParameters == null)
                return;
            AircraftDefinition donor = FindDefByKey(OaDonorKey);
            if (donor == null || object.ReferenceEquals(donor, clone)
                || donor.aircraftParameters == null)
                return;
            List<AircraftParameters.Livery> src = donor.aircraftParameters.liveries;
            if (src == null || src.Count == 0)
                return;
            if (object.ReferenceEquals(clone.aircraftParameters.liveries, src))
                return;
            clone.aircraftParameters.liveries = src;
        }

        internal static void ApplyEncyclopediaCostLabel(EncyclopediaBrowser browser, UnitDefinition def)
        {
            if (browser == null || !IsOursDef(def as AircraftDefinition))
                return;
            if (EncCostText == null)
                return;
            object tmp = null;
            try { tmp = EncCostText.GetValue(browser); }
            catch { tmp = null; }
            if (tmp == null)
                return;
            string shown = null;
            float cost = IsOaFamilyDef(def as AircraftDefinition) ? OaPrice() : CostFallback;
            try { shown = UnitConverter.ValueReading(cost); }
            catch { shown = null; }
            if (string.IsNullOrEmpty(shown)
                || shown.IndexOf("0.00", StringComparison.Ordinal) >= 0)
                shown = "$6.0K";
            try
            {
                PropertyInfo pi = tmp.GetType().GetProperty("text");
                if (pi != null)
                    pi.SetValue(tmp, shown, null);
            }
            catch { }
        }

        internal static void ApplyPerformance(Aircraft ac)
        {
            BindCloneDefinition(ac);
            bool oa = IsOaPowered(ac);
            if ((!IsOurs(ac) && !oa) || !Plugin.IsRuntime(ac))
                return;
            int id = ac.GetInstanceID();
            if (EncOnce.Add(id))
            {
                ApplyEncyclopedia(ac.definition as AircraftDefinition);
            if (oa)
            {
                ShowOaCrew(ac);
                ApplyPropPower(ac);
                DetachOaHardpoints(ac);
                OaTraits.OnSpawn(ac);
                try
                {
                    AircraftDefinition named = ac.definition as AircraftDefinition;
                    if (named != null && !string.IsNullOrEmpty(named.unitName))
                        ac.NetworkunitName = named.unitName;
                }
                catch { }
            }
                else
                {
                    HidePilotModel(ac);
                    StripGuns(ac);
                }
                BuffAirframe(ac);
            }
            if (InEncyclopedia())
            {
                if (!oa)
                    ApplyNoseBallast(ac);
                PoseEncyclopediaAircraft(ac);
                return;
            }
            if (!IsLiveAircraft(ac))
                return;
            if (!oa)
                AdoptDonorLiveries(ac.definition as AircraftDefinition);
            if (!oa)
                Ab4Fx.Ensure(ac);
            if (!oa && HasSimAuthority(ac))
                FlightFix.Apply(ac);
            if (oa)
            {
                if (!PowerDone.Contains(id))
                    ApplyPropPower(ac);
                OaTraits.Tick(ac);
                OaWso.Tick(ac);
                if (OaRearEjected.Contains(id) && IsOaClone(ac) && !IsOaConventionalClone(ac))
                    KeepPlayerFlying(ac, false);
                return;
            }
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (def != null && def.aircraftParameters != null)
                def.aircraftParameters.maxSpeed = SpeedCapMps;
            if (def != null && def.aircraftInfo != null)
                def.aircraftInfo.maxSpeed = SpeedCapMps;
            if (FilterParams != null)
            {
                try
                {
                    ControlsFilter cf = ac.GetControlsFilter();
                    if (cf != null)
                    {
                        AircraftParameters p = FilterParams.GetValue(cf) as AircraftParameters;
                        if (p != null)
                            p.maxSpeed = SpeedCapMps;
                    }
                }
                catch { }
            }
            ApplyIrAndAfterburner(ac);
            if (!HasSimAuthority(ac))
                return;
            ApplyThrust(ac);
            ClampAirspeed(ac);
        }

        internal static void ApplyPropPower(Aircraft ac)
        {
            if (ac == null)
                return;
            BindCloneDefinition(ac);
            if (!IsOaPowered(ac))
                return;
            TurbineEngine[] tes = null;
            try { tes = ac.GetComponentsInChildren<TurbineEngine>(true); }
            catch { tes = null; }
            float total = 0f;
            if (tes != null)
            {
                for (int i = 0; i < tes.Length; i++)
                {
                    TurbineEngine te = tes[i];
                    if (te == null)
                        continue;
                    DoubleTurbine(te, ac);
                    total += te.maxPower;
                }
            }
            DoubleFieldPower(ac, PropNominalPower, typeof(ConstantSpeedProp), total);
            DoubleFieldPower(ac, PropFanNominalPower, typeof(PropFan), total);
            if (TransMaxPower != null)
            {
                Transmission[] boxes = null;
                try { boxes = ac.GetComponentsInChildren<Transmission>(true); }
                catch { boxes = null; }
                if (boxes != null)
                {
                    for (int i = 0; i < boxes.Length; i++)
                    {
                        Transmission box = boxes[i];
                        if (box == null)
                            continue;
                        float cur = 0f;
                        try { cur = (float)TransMaxPower.GetValue(box); }
                        catch { cur = 0f; }
                        float want = total;
                        if (want < 0.01f)
                            want = DoubledFromStock(box.GetInstanceID(), cur, PowerMulOf(ac));
                        else
                            PropBasePower[box.GetInstanceID()] = want / PowerMulOf(ac);
                        try { TransMaxPower.SetValue(box, want); }
                        catch { }
                    }
                }
            }
            if (total >= 1f)
                CachedPowerKw[ac.GetInstanceID()] = total;
            PowerDone.Add(ac.GetInstanceID());
        }

        internal static void KeepPropPower(TurbineEngine te)
        {
            if (te == null)
                return;
            Aircraft ac = te.aircraft;
            if (ac == null)
                return;
            if (!IsOaPowered(ac))
                return;
            DoubleTurbine(te, ac);
        }

        internal static float FullLoadPowerKw(Aircraft ac)
        {
            if (ac == null)
                return 0f;
            int id = ac.GetInstanceID();
            float cached;
            if (CachedPowerKw.TryGetValue(id, out cached) && cached >= 1f)
                return cached;
            ApplyPropPower(ac);
            if (CachedPowerKw.TryGetValue(id, out cached) && cached >= 1f)
                return cached;
            return 0f;
        }

        private static void DoubleTurbine(TurbineEngine te, Aircraft ac)
        {
            if (te == null)
                return;
            float want = DoubledFromStock(te.GetInstanceID(), te.maxPower, PowerMulOf(ac));
            EngineWantKw[te.GetInstanceID()] = want;
            te.maxPower = want;
        }

        private static float DoubledFromStock(int id, float current, float mul)
        {
            float stock;
            if (!PropBasePower.TryGetValue(id, out stock) || stock < 0.01f)
            {
                stock = current;
                if (stock < 0.01f)
                    return current;
                PropBasePower[id] = stock;
            }
            if (mul < 1f)
                mul = PropPowerMul;
            return stock * mul;
        }

        private static void DoubleFieldPower(Aircraft ac, FieldInfo field, Type type, float total)
        {
            if (ac == null || field == null || type == null)
                return;
            Component[] comps = null;
            try { comps = ac.GetComponentsInChildren(type, true); }
            catch { comps = null; }
            if (comps == null || comps.Length == 0)
                return;
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                    continue;
                try
                {
                    float want = total;
                    if (want < 0.01f)
                    {
                        float cur = (float)field.GetValue(c);
                        want = DoubledFromStock(c.GetInstanceID(), cur, PowerMulOf(ac));
                    }
                    field.SetValue(c, want);
                }
                catch { }
            }
        }

        private static void ClampAirspeed(Aircraft ac)
        {
            if (ac == null)
                return;
            float cap = SpeedCapMps;
            float capSq = cap * cap;
            int id = ac.GetInstanceID();
            Rigidbody[] rbs;
            if (!ClampBodies.TryGetValue(id, out rbs) || rbs == null)
            {
                try { rbs = ac.GetComponentsInChildren<Rigidbody>(true); }
                catch { rbs = null; }
                if (rbs != null)
                    ClampBodies[id] = rbs;
            }
            if (rbs == null)
                return;
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody rb = rbs[i];
                if (rb == null || rb.isKinematic)
                    continue;
                try
                {
                    Vector3 v = rb.velocity;
                    if (v.sqrMagnitude > capSq)
                        rb.velocity = v.normalized * cap;
                }
                catch { }
            }
        }

        internal static bool IsGunMount(WeaponMount m)
        {
            if (m == null)
                return false;
            if (GunpodInject.IsPod(m))
                return false;
            if (IsGunKey(m.jsonKey))
                return true;
            string n = m.mountName != null ? m.mountName : string.Empty;
            if (n.IndexOf("23mm", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("37mm", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("57mm", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("27mm", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (m.info != null && m.info.gun)
                return true;
            return false;
        }

        private static bool IsGunKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (key.StartsWith("Aryx_MiG15_Gun", StringComparison.OrdinalIgnoreCase))
                return true;
            if (key.IndexOf("WeaponMount_23mm", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("WeaponMount_37mm", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("WeaponMount_57mm", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static void StripWeaponList(List<WeaponMount> list)
        {
            if (list == null)
                return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (IsGunMount(list[i]))
                    list.RemoveAt(i);
            }
        }

        private static void StripGunsFromDefinition(AircraftDefinition def)
        {
            if (def == null || def.aircraftParameters == null)
                return;
            AircraftParameters p = def.aircraftParameters;
            if (p.loadouts != null)
            {
                for (int i = 0; i < p.loadouts.Count; i++)
                {
                    NuclearOption.SavedMission.Loadout lo = p.loadouts[i];
                    if (lo != null)
                        StripWeaponList(lo.weapons);
                }
            }
            if (p.StandardLoadouts != null)
            {
                for (int i = 0; i < p.StandardLoadouts.Length; i++)
                {
                    StandardLoadout sl = p.StandardLoadouts[i];
                    if (sl == null || sl.loadout == null)
                        continue;
                    StripWeaponList(sl.loadout.weapons);
                    sl.Name = "Kamikaze";
                }
            }
        }

        private static void StripGuns(Aircraft ac)
        {
            if (ac == null)
                return;
            int id = ac.GetInstanceID();
            if (!GunsStripped.Add(id))
                return;
            StripGunsFromDefinition(ac.definition as AircraftDefinition);
            WeaponManager wm = ac.weaponManager;
            if (wm == null || wm.hardpointSets == null)
                return;
            for (int h = 0; h < wm.hardpointSets.Length; h++)
            {
                HardpointSet hs = wm.hardpointSets[h];
                if (hs == null)
                    continue;
                StripWeaponList(hs.weaponOptions);
                LoadoutLock.RememberCatalog(hs);
            }
            LoadoutLock.RememberAircraft(ac);
        }

        internal static void HidePilotModel(Aircraft ac)
        {
            if (ac == null || IsOaFamilyClone(ac))
                return;
            int id = ac.GetInstanceID();
            if (!PilotHidden.Add(id))
                return;
            Renderer[] rs = null;
            try { rs = ac.GetComponentsInChildren<Renderer>(true); }
            catch { rs = null; }
            if (rs == null)
                return;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                if (!UnderPilotVisual(rs[i].transform))
                    continue;
                try { rs[i].enabled = false; }
                catch { }
            }
        }

        private static bool UnderPilotVisual(Transform t)
        {
            Transform c = t;
            int guard = 0;
            while (c != null && guard < 16)
            {
                guard++;
                string n = c.name != null ? c.name : string.Empty;
                if (string.Equals(n, "pilot", StringComparison.OrdinalIgnoreCase)
                    || n.IndexOf("pilot_armature", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("PilotMesh", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("pilotMesh", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                try { c = c.parent; }
                catch { break; }
            }
            return false;
        }

        internal static bool BlockEjection(Aircraft ac)
        {
            return ac != null && IsMigClone(ac);
        }

        internal static bool HandleOaPartialEject(Aircraft ac)
        {
            if (ac == null)
                return false;
            if (!IsOaFamilyClone(ac) && !IsOaPowered(ac))
                return false;
            if (!IsLiveAircraft(ac))
                return true;
            if (_switchAway != null && object.ReferenceEquals(ac, _switchAway))
                return false;
            if (!IsLocalPlayerAircraft(ac))
                return false;
            bool asked = ConsumePlayerEjectIntent(ac);
            if (!asked)
                asked = PlayerEjectHeldNow();
            if (!asked)
                return true;
            if (IsOaConventionalClone(ac))
            {
                if (OaRearEjected.Contains(ac.GetInstanceID()))
                    return false;
                DoOaRearEject(ac);
                return true;
            }
            DoOaRearEject(ac);
            NotifyCannotEject(ac);
            return true;
        }

        internal static void TickRearEject()
        {
            YankStuckSeats();
            Aircraft local;
            if (!GameManager.GetLocalAircraft(out local) || local == null)
                return;
            if (!IsOaFamilyClone(local))
                return;
            if (OaRearEjected.Contains(local.GetInstanceID()))
                return;
            if (CrewShownOnce.Contains(local.GetInstanceID()))
                return;
            ShowOaCrew(local);
        }

        private static void MaybeShowOaCrew(Aircraft ac)
        {
            if (ac == null || !IsOaFamilyClone(ac))
                return;
            int id = ac.GetInstanceID();
            if (OaRearEjected.Contains(id) || CrewShownOnce.Contains(id))
                return;
            ShowOaCrew(ac);
        }

        internal static void ShowOaCrew(Aircraft ac)
        {
            if (ac == null || !IsOaFamilyClone(ac))
                return;
            CrewShownOnce.Add(ac.GetInstanceID());
            Pilot player = FindPlayerPilot(ac);
            Pilot[] pilots = AllPilots(ac);
            if (pilots != null)
            {
                for (int i = 0; i < pilots.Length; i++)
                {
                    Pilot p = pilots[i];
                    if (p == null || object.ReferenceEquals(p, player) || p.playerControlled)
                        continue;
                    try { p.TogglePilotVisibility(true); }
                    catch { }
                    SetSubtreeVisible(p.transform, true);
                }
            }
            Renderer[] rs = null;
            try { rs = ac.GetComponentsInChildren<Renderer>(true); }
            catch { rs = null; }
            if (rs == null)
                return;
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null)
                    continue;
                if (player != null && UnderRoot(r.transform, player.transform))
                    continue;
                if (!LooksLikeCrewVisual(r.transform))
                    continue;
                try { r.enabled = true; }
                catch { }
            }
        }

        private static void DoOaRearEject(Aircraft ac)
        {
            if (ac == null)
                return;
            int id = ac.GetInstanceID();
            if (!OaRearEjected.Add(id))
            {
                KeepPlayerFlying(ac);
                return;
            }
            JettisonOaCanopy(ac);
            Pilot rear = FindOaRearPilot(ac);
            EjectionSeat seat = FindOaRearSeat(ac, rear);
            Transform visual = FindOaRearVisual(ac, rear, seat);
            PunchOaRear(ac, rear, seat, visual);
            KeepPlayerFlying(ac);
        }

        private static void JettisonOaCanopy(Aircraft ac)
        {
            if (ac == null)
                return;
            Pilot player = FindPlayerPilot(ac);
            Canopy[] cans = null;
            try { cans = ac.GetComponentsInChildren<Canopy>(true); }
            catch { cans = null; }
            if (cans == null)
                return;
            for (int i = 0; i < cans.Length; i++)
            {
                Canopy c = cans[i];
                if (c == null || c.transform == null)
                    continue;
                DetachCrewFromCanopy(ac, player, c.transform);
                try { c.Eject(); }
                catch { }
            }
        }

        private static void DetachCrewFromCanopy(Aircraft ac, Pilot player, Transform canopy)
        {
            if (ac == null || canopy == null)
                return;
            Transform dest = ac.transform;
            if (player != null && player.transform != null
                && UnderRoot(player.transform, canopy)
                && !object.ReferenceEquals(player.transform, dest))
            {
                try { player.transform.SetParent(dest, true); }
                catch { }
            }
            Camera[] cams = null;
            try { cams = ac.GetComponentsInChildren<Camera>(true); }
            catch { cams = null; }
            if (cams == null)
                return;
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] == null || cams[i].transform == null)
                    continue;
                if (!UnderRoot(cams[i].transform, canopy))
                    continue;
                try { cams[i].transform.SetParent(dest, true); }
                catch { }
            }
        }

        private static Pilot[] AllPilots(Aircraft ac)
        {
            if (ac == null)
                return null;
            Pilot[] pilots = null;
            try { pilots = ac.pilots; }
            catch { pilots = null; }
            if (pilots != null && pilots.Length > 0)
                return pilots;
            try { return ac.GetComponentsInChildren<Pilot>(true); }
            catch { return null; }
        }

        private static Pilot FindPlayerPilot(Aircraft ac)
        {
            Pilot[] pilots = AllPilots(ac);
            if (pilots == null)
                return null;
            for (int i = 0; i < pilots.Length; i++)
            {
                if (pilots[i] != null && pilots[i].playerControlled)
                    return pilots[i];
            }
            if (pilots.Length > 0)
                return pilots[0];
            return null;
        }

        internal static Pilot FindOaWsoPilot(Aircraft ac)
        {
            return FindOaRearPilot(ac);
        }

        private static Pilot FindOaRearPilot(Aircraft ac)
        {
            Pilot[] pilots = AllPilots(ac);
            if (pilots == null)
                return null;
            Pilot player = FindPlayerPilot(ac);
            Pilot named = null;
            Pilot highest = null;
            int bestNum = -1;
            for (int i = 0; i < pilots.Length; i++)
            {
                Pilot p = pilots[i];
                if (p == null || object.ReferenceEquals(p, player) || p.playerControlled)
                    continue;
                string n = p.gameObject != null ? p.gameObject.name : string.Empty;
                if (CrewNameHit(n))
                    named = p;
                int num = PilotIndex(p, i);
                if (num > bestNum)
                {
                    bestNum = num;
                    highest = p;
                }
            }
            if (named != null)
                return named;
            if (highest != null)
                return highest;
            if (player != null && pilots.Length >= 2)
            {
                for (int i = 0; i < pilots.Length; i++)
                {
                    if (pilots[i] != null && !object.ReferenceEquals(pilots[i], player))
                        return pilots[i];
                }
            }
            return null;
        }

        private static EjectionSeat[] AllSeats(Aircraft ac)
        {
            if (ac == null)
                return null;
            try { return ac.GetComponentsInChildren<EjectionSeat>(true); }
            catch { return null; }
        }

        private static EjectionSeat SeatOn(Pilot p)
        {
            if (p == null)
                return null;
            try { return p.GetComponentInChildren<EjectionSeat>(true); }
            catch { return null; }
        }

        private static EjectionSeat SeatNear(Transform t, EjectionSeat[] seats, float maxM)
        {
            if (t == null || seats == null)
                return null;
            EjectionSeat best = null;
            float bestSqr = maxM * maxM;
            for (int i = 0; i < seats.Length; i++)
            {
                EjectionSeat s = seats[i];
                if (s == null || s.transform == null)
                    continue;
                float sqr = (s.transform.position - t.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = s;
                }
            }
            return best;
        }

        private static EjectionSeat FindOaRearSeat(Aircraft ac, Pilot rear)
        {
            EjectionSeat[] seats = AllSeats(ac);
            if (seats == null || seats.Length == 0)
                return SeatOn(rear);
            if (rear != null)
            {
                EjectionSeat onRear = SeatOn(rear);
                if (onRear != null)
                    return onRear;
                EjectionSeat nearRear = SeatNear(rear.transform, seats, 1.6f);
                if (nearRear != null && !IsPlayerSeat(ac, nearRear))
                    return nearRear;
            }
            EjectionSeat aft = null;
            float bestZ = float.MaxValue;
            for (int i = 0; i < seats.Length; i++)
            {
                EjectionSeat s = seats[i];
                if (s == null || IsPlayerSeat(ac, s))
                    continue;
                float z = LocalZ(ac, s.transform);
                if (z < bestZ)
                {
                    bestZ = z;
                    aft = s;
                }
            }
            return aft;
        }

        private static bool IsPlayerSeat(Aircraft ac, EjectionSeat seat)
        {
            if (ac == null || seat == null)
                return false;
            Pilot player = FindPlayerPilot(ac);
            if (player == null)
                return false;
            EjectionSeat front = SeatOn(player);
            if (front != null && object.ReferenceEquals(front, seat))
                return true;
            if (player.transform != null
                && (seat.transform.position - player.transform.position).sqrMagnitude < 0.35f * 0.35f)
                return true;
            return false;
        }

        private static Transform FindOaRearVisual(Aircraft ac, Pilot rear, EjectionSeat seat)
        {
            if (seat != null)
                return seat.transform;
            if (rear != null)
                return rear.transform;
            Pilot player = FindPlayerPilot(ac);
            Transform[] xs = null;
            try { xs = ac.GetComponentsInChildren<Transform>(true); }
            catch { xs = null; }
            if (xs == null)
                return null;
            Transform best = null;
            float bestZ = float.MaxValue;
            for (int i = 0; i < xs.Length; i++)
            {
                Transform t = xs[i];
                if (t == null)
                    continue;
                if (player != null && (object.ReferenceEquals(t, player.transform)
                    || UnderRoot(t, player.transform)))
                    continue;
                if (IsProtectedFlightHardware(t, ac, player))
                    continue;
                if (!CrewNameHit(t.name))
                    continue;
                if (t.childCount > 48)
                    continue;
                float z = LocalZ(ac, t);
                if (z < bestZ)
                {
                    bestZ = z;
                    best = t;
                }
            }
            return best;
        }

        private static bool CrewNameHit(string n)
        {
            if (string.IsNullOrEmpty(n))
                return false;
            if (n.IndexOf("wso", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("copilot", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("co-pilot", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("rio", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("seat2", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("seat_2", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("seat 2", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pilot (1)", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pilot_1", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pilot1", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            bool rear = n.IndexOf("rear", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("aft", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!rear)
                return false;
            return n.IndexOf("seat", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pilot", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("crew", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeCrewVisual(Transform t)
        {
            if (t == null)
                return false;
            if (UnderPilotVisual(t))
                return true;
            Transform c = t;
            int guard = 0;
            while (c != null && guard < 12)
            {
                guard++;
                if (CrewNameHit(c.name))
                    return true;
                try { c = c.parent; }
                catch { break; }
            }
            return false;
        }

        private static float LocalZ(Aircraft ac, Transform t)
        {
            if (ac == null || t == null)
                return 0f;
            return ac.transform.InverseTransformPoint(t.position).z;
        }

        private static bool UnderRoot(Transform t, Transform root)
        {
            if (t == null || root == null)
                return false;
            Transform c = t;
            int guard = 0;
            while (c != null && guard < 24)
            {
                guard++;
                if (object.ReferenceEquals(c, root))
                    return true;
                try { c = c.parent; }
                catch { break; }
            }
            return false;
        }

        private static int PilotIndex(Pilot p, int fallback)
        {
            if (p != null && PilotNumberField != null)
            {
                try
                {
                    return (int)((byte)PilotNumberField.GetValue(p));
                }
                catch { }
            }
            return fallback;
        }

        private static Vector3 EjectVelocity(Aircraft ac)
        {
            Vector3 vel = Vector3.up * 24f;
            try
            {
                if (ac != null && ac.rb != null)
                    vel = ac.rb.velocity + ac.transform.up * 24f + ac.transform.forward * 5f;
            }
            catch { }
            return vel;
        }

        private static void PunchOaRear(Aircraft ac, Pilot rear, EjectionSeat seat, Transform visual)
        {
            if (ac == null)
                return;
            Pilot player = FindPlayerPilot(ac);
            if (seat != null && IsPlayerSeat(ac, seat))
                seat = null;
            if (visual != null && player != null && UnderRoot(visual, player.transform))
                visual = seat != null ? seat.transform : null;
            if (visual != null && IsProtectedFlightHardware(visual, ac, player))
                visual = seat != null && !IsProtectedFlightHardware(seat.transform, ac, player)
                    ? seat.transform
                    : null;
            if (visual == null && seat != null)
                visual = seat.transform;
            if (visual == null && rear != null)
                visual = rear.transform;

            SetSubtreeVisible(visual, true);
            HideWsoMeshes(visual);

            UnitPart part = null;
            if (rear != null)
            {
                try { part = rear.GetUnitPart(); }
                catch { part = null; }
            }
            if (part == null && seat != null)
            {
                try { part = seat.GetComponentInParent<UnitPart>(); }
                catch { part = null; }
            }

            if (seat != null)
            {
                try { seat.Fire(part); }
                catch { }
                try { seat.Detach(); }
                catch { }
            }

            Vector3 vel = EjectVelocity(ac);
            if (rear != null && !rear.playerControlled)
            {
                try { rear.SetEjected(); }
                catch { rear.ejected = true; }
                try { rear.Detach(vel, rear.transform.localPosition); }
                catch { }
                try { rear.TogglePilotVisibility(false); }
                catch { }
                HideWsoMeshes(rear.transform);
            }

            ScheduleYank(ac, visual, 0.25f);
            ScheduleYank(ac, visual, 1.4f);
            if (visual != null && StillOnAircraft(ac, visual))
                LaunchLoose(ac, visual, vel, player);

            HideWsoMeshes(visual);
            if (seat != null)
                HideWsoMeshes(seat.transform);
            HideLeftoverRearCrew(ac, player);

            int num = PilotIndex(rear, 1);
            Unit bait = SpawnLockableBait(ac, visual, seat, vel, num);
            if (bait != null)
                HideWsoMeshes(bait.transform);
            KeepPlayerFlying(ac, true);
            RearLure.Arm(ac, rear, seat, num, bait);
        }

        private static void ScheduleYank(Aircraft ac, Transform xf, float delay)
        {
            if (ac == null || xf == null)
                return;
            PendingYank y;
            y.ac = ac;
            y.xf = xf;
            y.at = Time.time + delay;
            SeatYanks.Add(y);
        }

        private static void YankStuckSeats()
        {
            if (SeatYanks.Count == 0)
                return;
            for (int i = SeatYanks.Count - 1; i >= 0; i--)
            {
                PendingYank y = SeatYanks[i];
                if (Time.time < y.at)
                    continue;
                SeatYanks.RemoveAt(i);
                if (y.ac == null || y.xf == null)
                    continue;
                if (!StillOnAircraft(y.ac, y.xf))
                    continue;
                LaunchLoose(y.ac, y.xf, EjectVelocity(y.ac), FindPlayerPilot(y.ac));
            }
        }

        private static bool StillOnAircraft(Aircraft ac, Transform xf)
        {
            if (ac == null || xf == null)
                return false;
            return UnderRoot(xf, ac.transform);
        }

        private static void LaunchLoose(Aircraft ac, Transform xf, Vector3 vel, Pilot player)
        {
            if (ac == null || xf == null)
                return;
            if (IsProtectedFlightHardware(xf, ac, player))
                return;
            if (player != null && (object.ReferenceEquals(xf, player.transform)
                || UnderRoot(player.transform, xf)))
                return;
            if (object.ReferenceEquals(xf, ac.transform))
                return;
            try { xf.SetParent(null, true); }
            catch { return; }
            SetSubtreeVisible(xf, true);
            HideWsoMeshes(xf);
            Rigidbody rb = null;
            try { rb = xf.GetComponent<Rigidbody>(); }
            catch { rb = null; }
            if (rb == null)
            {
                try { rb = xf.gameObject.AddComponent<Rigidbody>(); }
                catch { rb = null; }
            }
            if (rb == null)
                return;
            try
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                if (rb.mass < 40f)
                    rb.mass = 80f;
                rb.velocity = vel;
                if (ac.transform != null)
                    rb.angularVelocity = ac.transform.right * 0.35f;
            }
            catch { }
        }

        private static void SetSubtreeVisible(Transform root, bool on)
        {
            if (root == null)
                return;
            Renderer[] rs = null;
            try { rs = root.GetComponentsInChildren<Renderer>(true); }
            catch { rs = null; }
            if (rs == null)
                return;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                try { rs[i].enabled = on; }
                catch { }
            }
        }

        private static void HideLeftoverRearCrew(Aircraft ac, Pilot player)
        {
            if (ac == null)
                return;
            Pilot[] pilots = AllPilots(ac);
            if (pilots != null)
            {
                for (int i = 0; i < pilots.Length; i++)
                {
                    Pilot p = pilots[i];
                    if (p == null || object.ReferenceEquals(p, player) || p.playerControlled)
                        continue;
                    try { p.TogglePilotVisibility(false); }
                    catch { }
                    HideWsoMeshes(p.transform);
                }
            }
            HideWsoMeshes(ac.transform, player);
        }

        private static void HideWsoMeshes(Transform root)
        {
            HideWsoMeshes(root, null);
        }

        private static void HideWsoMeshes(Transform root, Pilot keep)
        {
            if (root == null)
                return;
            Renderer[] rs = null;
            try { rs = root.GetComponentsInChildren<Renderer>(true); }
            catch { rs = null; }
            if (rs == null)
                return;
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null)
                    continue;
                if (keep != null && UnderRoot(r.transform, keep.transform))
                    continue;
                if (!IsCrewBodyMesh(r))
                    continue;
                try { r.enabled = false; }
                catch { }
            }
        }

        private static bool IsCrewBodyMesh(Renderer r)
        {
            if (r == null)
                return false;
            if (r is SkinnedMeshRenderer)
            {
                string sn = r.gameObject != null ? r.gameObject.name : string.Empty;
                if (sn.IndexOf("seat", StringComparison.OrdinalIgnoreCase) >= 0
                    || sn.IndexOf("canopy", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
                return true;
            }
            Transform t = r.transform;
            int guard = 0;
            while (t != null && guard < 10)
            {
                guard++;
                string n = t.name != null ? t.name : string.Empty;
                if (n.IndexOf("seat", StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("pilot", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                if (n.IndexOf("wso", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("armature", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("PilotMesh", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("pilotMesh", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(n, "pilot", StringComparison.OrdinalIgnoreCase)
                    || n.IndexOf("copilot", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("gunner", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                try { t = t.parent; }
                catch { break; }
            }
            return false;
        }

        private static Unit SpawnLockableBait(Aircraft ac, Transform at, EjectionSeat seat, Vector3 vel, int rearNum)
        {
            GameObject prefab = FindDismountPrefab();
            if (prefab == null || ac == null)
                return null;
            Vector3 pos = ac.transform.position;
            Quaternion rot = ac.transform.rotation;
            if (at != null)
            {
                pos = at.position;
                rot = at.rotation;
            }
            else
                pos = pos + ac.transform.up * 1.2f - ac.transform.forward * 0.8f;

            GameObject go = null;
            bool srcOn = prefab.activeSelf;
            try
            {
                if (srcOn)
                    prefab.SetActive(false);
                go = UnityEngine.Object.Instantiate(prefab, pos, rot);
                if (srcOn)
                    prefab.SetActive(true);
            }
            catch
            {
                try
                {
                    if (srcOn && prefab != null)
                        prefab.SetActive(true);
                }
                catch { }
                return null;
            }
            if (go == null)
                return null;
            go.name = "OA27C_RearBait";
            go.SetActive(true);
            SetSubtreeVisible(go.transform, true);

            PilotDismounted pd = null;
            try { pd = go.GetComponent<PilotDismounted>(); }
            catch { pd = null; }
            Unit unit = pd;
            if (unit == null)
            {
                try { unit = go.GetComponent<Unit>(); }
                catch { unit = null; }
            }
            if (unit == null)
            {
                try { UnityEngine.Object.Destroy(go); }
                catch { }
                return null;
            }

            try { unit.networked = false; }
            catch { }
            try { unit.NetworkunitName = "WSO"; }
            catch { }
            try { unit.NetworkHQ = ac.NetworkHQ; }
            catch { }
            try { unit.MapHQ = ac.MapHQ; }
            catch { }
            if (pd != null)
            {
                try { pd.NetworkparentUnit = ac.persistentID; }
                catch { }
                try { pd.NetworkpilotNumber = (byte)rearNum; }
                catch { }
                try { pd.SetPilotState(PilotDismounted.PilotState.ejecting); }
                catch { }
                if (seat != null)
                {
                    try { seat.LinkToPilot(pd); }
                    catch { }
                }
            }

            try
            {
                PersistentID pid = UnitRegistry.GetNextIndex();
                UnitRegistry.RegisterUnit(unit, pid);
            }
            catch { }
            try { unit.RegisterUnit(null); }
            catch { }
            try { unit.InitializeUnit(); }
            catch { }
            try
            {
                if (UnitRegistry.allUnits != null && !UnitRegistry.allUnits.Contains(unit))
                    UnitRegistry.allUnits.Add(unit);
            }
            catch { }

            try { unit.RCS = 12f; }
            catch { }
            try { unit.ModifyRCS(80f); }
            catch { }

            Rigidbody rb = null;
            try { rb = go.GetComponent<Rigidbody>(); }
            catch { rb = null; }
            if (rb == null)
            {
                try { rb = go.AddComponent<Rigidbody>(); }
                catch { rb = null; }
            }
            if (rb != null)
            {
                try
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    if (rb.mass < 40f)
                        rb.mass = 90f;
                    rb.velocity = vel;
                }
                catch { }
            }

            HideWsoMeshes(at);
            if (at != null && !StillOnAircraft(ac, at))
            {
                try { go.transform.SetParent(at, true); }
                catch { }
            }
            else if (at != null && StillOnAircraft(ac, at))
            {
                LaunchLoose(ac, at, vel, FindPlayerPilot(ac));
                try { go.transform.SetParent(at, true); }
                catch { }
            }
            HideWsoMeshes(at);
            HideWsoMeshes(go.transform);

            if (Plugin.Log != null)
                Plugin.Log.LogInfo("OA-27C rear bait spawned for lock / missile lure");
            return unit;
        }

        private static GameObject FindDismountPrefab()
        {
            if (_dismountPrefab != null)
                return _dismountPrefab;
            if (_dismountTried)
                return null;
            _dismountTried = true;
            PilotDismounted[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<PilotDismounted>(); }
            catch { return null; }
            if (all == null)
                return null;
            int bestScore = -1;
            GameObject best = null;
            for (int i = 0; i < all.Length; i++)
            {
                PilotDismounted pd = all[i];
                if (pd == null || pd.gameObject == null)
                    continue;
                GameObject go = pd.gameObject;
                string nm = go.name != null ? go.name : string.Empty;
                if (nm.IndexOf("OA27C_RearBait", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (nm.IndexOf("VanillaPilotBody", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
                int n = rs != null ? rs.Length : 0;
                if (n <= 0)
                    continue;
                bool prefab = false;
                try { prefab = !go.scene.IsValid() || !go.scene.isLoaded; }
                catch { }
                int score = n + (prefab ? 80 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = go;
                }
            }
            _dismountPrefab = best;
            return _dismountPrefab;
        }

        private static bool IsProtectedFlightHardware(Transform xf, Aircraft ac, Pilot player)
        {
            if (xf == null)
                return true;
            if (ac != null && object.ReferenceEquals(xf, ac.transform))
                return true;
            if (player != null)
            {
                if (object.ReferenceEquals(xf, player.transform)
                    || UnderRoot(xf, player.transform)
                    || UnderRoot(player.transform, xf))
                    return true;
            }
            string n = xf.name != null ? xf.name : string.Empty;
            if (n.IndexOf("hud", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("mfd", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("cockpit", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("gunsight", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("boresight", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("combiner", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("canvas", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            try
            {
                if (xf.GetComponent<Camera>() != null)
                    return true;
                if (xf.GetComponent<WeaponManager>() != null)
                    return true;
                if (xf.GetComponent<WeaponStation>() != null)
                    return true;
                if (xf.GetComponent<Turret>() != null)
                    return true;
                if (xf.GetComponent<Cockpit>() != null)
                    return true;
            }
            catch { }
            return false;
        }

        internal static void KeepPlayerFlying(Aircraft ac)
        {
            KeepPlayerFlying(ac, false);
        }

        internal static void KeepPlayerFlying(Aircraft ac, bool full)
        {
            if (ac == null)
                return;
            if (AircraftEjected != null)
            {
                try { AircraftEjected.SetValue(ac, false); }
                catch { }
            }
            try { GameManager.flightControlsEnabled = true; }
            catch { }
            Pilot[] pilots = null;
            try { pilots = ac.pilots; }
            catch { pilots = null; }
            if (pilots != null)
            {
                for (int i = 0; i < pilots.Length; i++)
                {
                    Pilot p = pilots[i];
                    if (p == null || !p.playerControlled)
                        continue;
                    try { p.ejected = false; }
                    catch { }
                    try { p.dead = false; }
                    catch { }
                }
            }
        }

        private static void RestoreWeapons(Aircraft ac)
        {
            if (ac == null || ac.weaponManager == null)
                return;
            WeaponManager wm = ac.weaponManager;
            bool empty = false;
            try
            {
                empty = ac.weaponStations == null || ac.weaponStations.Count == 0;
            }
            catch { empty = false; }
            if (empty)
            {
                try { wm.SpawnWeapons(); }
                catch { }
            }
            try
            {
                if (wm.currentWeaponStation == null)
                    wm.NextWeaponStation();
            }
            catch { }
        }

        internal static void DetachOaHardpoints(Aircraft ac)
        {
            if (ac == null)
                return;
            if (!IsOaFamilyClone(ac) && !IsOaHangarPreview(ac) && !IsOaPowered(ac))
                return;
            int id = ac.GetInstanceID();
            bool first = CatalogDetached.Add(id);
            SnapshotOaDonorLoadouts();
            WeaponManager wm = ac.weaponManager;
            if (wm == null || wm.hardpointSets == null)
                return;
            for (int i = 0; i < wm.hardpointSets.Length; i++)
            {
                HardpointSet hs = wm.hardpointSets[i];
                if (hs == null)
                    continue;
                if (first)
                    DetachHardpointList(hs);
                MergeOaStock(hs, ac);
            }
            MergeLoadoutSlotsIntoHardpoints(ac);
        }

        private static void DetachHardpointList(HardpointSet hs)
        {
            if (hs == null)
                return;
            if (hs.weaponOptions == null)
                hs.weaponOptions = new List<WeaponMount>(8);
            else
                hs.weaponOptions = new List<WeaponMount>(hs.weaponOptions);
        }

        internal static void SnapshotOaDonorLoadouts()
        {
            if (_oaStockMounts > 0)
                return;
            if (Time.unscaledTime < _nextStockScan)
                return;
            _nextStockScan = Time.unscaledTime + 4f;
            AircraftDefinition donor = FindDonorDef();
            if (donor != null)
                TrySnapshotWeaponManager(WeaponManagerOf(donor));
            List<Aircraft> live = null;
            try { live = UnitRegistry.allAircraft; }
            catch { live = null; }
            if (live == null)
                return;
            for (int i = 0; i < live.Count; i++)
            {
                Aircraft ac = live[i];
                if (ac == null || ac.weaponManager == null)
                    continue;
                if (!IsDonorDef(ac.definition as AircraftDefinition))
                    continue;
                TrySnapshotWeaponManager(ac.weaponManager);
            }
        }

        private static WeaponManager WeaponManagerOf(AircraftDefinition def)
        {
            if (def == null || def.unitPrefab == null)
                return null;
            WeaponManager wm = null;
            try { wm = def.unitPrefab.GetComponentInChildren<WeaponManager>(true); }
            catch { wm = null; }
            return wm;
        }

        private static void TrySnapshotWeaponManager(WeaponManager wm)
        {
            if (wm == null || wm.hardpointSets == null || wm.hardpointSets.Length == 0)
                return;
            int n = 0;
            int i;
            for (i = 0; i < wm.hardpointSets.Length; i++)
                n += CountStockMounts(wm.hardpointSets[i]);
            if (n <= _oaStockMounts)
                return;
            List<WeaponMount>[] sets = new List<WeaponMount>[wm.hardpointSets.Length];
            string[] names = new string[wm.hardpointSets.Length];
            for (i = 0; i < wm.hardpointSets.Length; i++)
            {
                HardpointSet hs = wm.hardpointSets[i];
                names[i] = hs != null ? hs.name : null;
                sets[i] = CopyStockMounts(hs);
            }
            _oaStockSets = sets;
            _oaStockNames = names;
            _oaStockMounts = n;
            if (!_oaStockLogged && Plugin.Log != null)
            {
                _oaStockLogged = true;
                Plugin.Log.LogInfo("OA-27 stock catalog snapshot "
                    + n.ToString() + " mounts across "
                    + wm.hardpointSets.Length.ToString() + " hardpoints");
            }
            AdoptOaStandardLoadouts(_oaClone);
            AdoptOaStandardLoadouts(_oaDClone);
            AdoptOaStandardLoadouts(_oaEClone);
        }

        private static int CountStockMounts(HardpointSet hs)
        {
            if (hs == null || hs.weaponOptions == null)
                return 0;
            int n = 0;
            int i;
            for (i = 0; i < hs.weaponOptions.Count; i++)
            {
                WeaponMount m = hs.weaponOptions[i];
                if (m == null || GunpodInject.IsOurExtra(m))
                    continue;
                n++;
            }
            return n;
        }

        private static List<WeaponMount> CopyStockMounts(HardpointSet hs)
        {
            List<WeaponMount> copy = new List<WeaponMount>(8);
            if (hs == null || hs.weaponOptions == null)
                return copy;
            int i;
            for (i = 0; i < hs.weaponOptions.Count; i++)
            {
                WeaponMount m = hs.weaponOptions[i];
                if (m == null || GunpodInject.IsOurExtra(m))
                    continue;
                copy.Add(m);
            }
            return copy;
        }

        internal static void MergeOaStock(HardpointSet hs, Aircraft ac)
        {
            if (hs == null)
                return;
            if (!IsOaLoadoutContext(ac, hs))
                return;
            SnapshotOaDonorLoadouts();
            List<WeaponMount> stock = StockForSet(hs, ac);
            if (stock == null || stock.Count == 0)
                return;
            if (hs.weaponOptions == null)
                hs.weaponOptions = new List<WeaponMount>(stock.Count + 4);
            int i;
            for (i = 0; i < stock.Count; i++)
            {
                WeaponMount m = stock[i];
                if (m == null)
                    continue;
                if (ListHasMount(hs.weaponOptions, m))
                    continue;
                hs.weaponOptions.Add(m);
            }
        }

        private static List<WeaponMount> StockForSet(HardpointSet hs, Aircraft ac)
        {
            if (_oaStockSets == null || _oaStockSets.Length == 0)
                return null;
            int idx = IndexOfHardpoint(ac, hs);
            if (idx >= 0 && idx < _oaStockSets.Length && _oaStockSets[idx] != null)
                return _oaStockSets[idx];
            if (hs == null || string.IsNullOrEmpty(hs.name) || _oaStockNames == null)
                return null;
            int i;
            for (i = 0; i < _oaStockNames.Length; i++)
            {
                if (string.Equals(_oaStockNames[i], hs.name, StringComparison.OrdinalIgnoreCase)
                    && _oaStockSets[i] != null)
                    return _oaStockSets[i];
            }
            return null;
        }

        private static int IndexOfHardpoint(Aircraft ac, HardpointSet hs)
        {
            if (ac == null)
                ac = LoadoutLock.FindAircraft(hs);
            if (ac == null)
                ac = LoadoutLock.SelectorAircraft;
            if (ac == null || ac.weaponManager == null || ac.weaponManager.hardpointSets == null
                || hs == null)
                return -1;
            HardpointSet[] sets = ac.weaponManager.hardpointSets;
            int i;
            for (i = 0; i < sets.Length; i++)
            {
                if (object.ReferenceEquals(sets[i], hs))
                    return i;
            }
            return -1;
        }

        internal static bool ListHasMount(List<WeaponMount> list, WeaponMount mount)
        {
            if (list == null || mount == null)
                return false;
            string key = mount.jsonKey;
            int i;
            for (i = 0; i < list.Count; i++)
            {
                WeaponMount cur = list[i];
                if (cur == null)
                    continue;
                if (object.ReferenceEquals(cur, mount))
                    return true;
                if (!string.IsNullOrEmpty(key)
                    && string.Equals(cur.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static void AdoptOaStandardLoadouts(AircraftDefinition clone)
        {
            if (!IsOaFamilyDef(clone) || clone.aircraftParameters == null)
                return;
            AircraftDefinition donor = FindDonorDef();
            if (donor == null || donor.aircraftParameters == null
                || object.ReferenceEquals(donor, clone))
                return;
            StandardLoadout[] src = donor.aircraftParameters.StandardLoadouts;
            if (src == null || src.Length == 0)
                return;
            int srcN = CountPresetWeapons(src);
            if (srcN <= 0)
                return;
            int dstN = CountPresetWeapons(clone.aircraftParameters.StandardLoadouts);
            if (dstN < srcN)
                clone.aircraftParameters.StandardLoadouts = src;
            if (donor.aircraftParameters.loadouts != null
                && (clone.aircraftParameters.loadouts == null
                    || clone.aircraftParameters.loadouts.Count == 0
                    || !ParamsHaveInternal20(clone.aircraftParameters)))
                clone.aircraftParameters.loadouts = donor.aircraftParameters.loadouts;
        }

        internal static void MergeLoadoutSlotsIntoHardpoints(Aircraft ac)
        {
            if (ac == null || ac.weaponManager == null)
                return;
            AbsorbParamsOntoAircraft(ac, ac.definition as AircraftDefinition);
            AbsorbParamsOntoAircraft(ac, FindDonorDef());
            AbsorbParamsOntoAircraft(ac, LoadoutLock.ActiveSpawnDef());
            PlaceInternal20(ac);
        }

        private static void AbsorbParamsOntoAircraft(Aircraft ac, AircraftDefinition def)
        {
            if (ac == null || def == null || def.aircraftParameters == null)
                return;
            AircraftParameters p = def.aircraftParameters;
            if (p.loadouts != null)
            {
                int i;
                for (i = 0; i < p.loadouts.Count; i++)
                {
                    NuclearOption.SavedMission.Loadout lo = p.loadouts[i];
                    if (lo != null)
                        PutWeaponsOnSets(ac.weaponManager, lo.weapons);
                }
            }
            if (p.StandardLoadouts == null)
                return;
            int s;
            for (s = 0; s < p.StandardLoadouts.Length; s++)
            {
                StandardLoadout sl = p.StandardLoadouts[s];
                if (sl != null && sl.loadout != null)
                    PutWeaponsOnSets(ac.weaponManager, sl.loadout.weapons);
            }
        }

        private static void PutWeaponsOnSets(WeaponManager wm, List<WeaponMount> weapons)
        {
            if (wm == null || wm.hardpointSets == null || weapons == null)
                return;
            int n = weapons.Count;
            if (n > wm.hardpointSets.Length)
                n = wm.hardpointSets.Length;
            int i;
            for (i = 0; i < n; i++)
            {
                WeaponMount m = weapons[i];
                if (m == null)
                    continue;
                m = ResolveNetworkMount(m);
                if (m == null)
                    continue;
                HardpointSet hs = wm.hardpointSets[i];
                if (hs == null)
                    continue;
                PrepareStockMount(m);
                if (hs.weaponOptions == null)
                    hs.weaponOptions = new List<WeaponMount>(8);
                if (!ListHasMount(hs.weaponOptions, m))
                    hs.weaponOptions.Add(m);
            }
        }

        internal static void PlaceInternal20(Aircraft ac)
        {
            if (ac == null || ac.weaponManager == null || ac.weaponManager.hardpointSets == null)
                return;
            WeaponMount gun = FindInternal20();
            if (gun == null)
                return;
            PrepareStockMount(gun);
            HardpointSet[] sets = ac.weaponManager.hardpointSets;
            bool placed = false;
            int i;
            for (i = 0; i < sets.Length; i++)
            {
                HardpointSet hs = sets[i];
                if (hs == null)
                    continue;
                if (!SetWantsInternal20(hs, i, ac))
                    continue;
                if (hs.weaponOptions == null)
                    hs.weaponOptions = new List<WeaponMount>(8);
                if (!ListHasMount(hs.weaponOptions, gun))
                    hs.weaponOptions.Add(gun);
                placed = true;
            }
            if (placed)
                return;
            for (i = 0; i < sets.Length; i++)
            {
                HardpointSet hs = sets[i];
                if (hs == null)
                    continue;
                if (hs.weaponOptions == null)
                    hs.weaponOptions = new List<WeaponMount>(8);
                if (!ListHasMount(hs.weaponOptions, gun))
                    hs.weaponOptions.Add(gun);
                break;
            }
        }

        private static bool SetWantsInternal20(HardpointSet hs, int idx, Aircraft ac)
        {
            if (hs == null)
                return false;
            if (NameLooksInternalGun(hs.name))
                return true;
            if (hs.weaponOptions != null)
            {
                int i;
                for (i = 0; i < hs.weaponOptions.Count; i++)
                {
                    if (IsInternal20(hs.weaponOptions[i]) || IsGunMount(hs.weaponOptions[i]))
                        return true;
                }
            }
            List<WeaponMount> stock = StockForSet(hs, ac);
            if (stock != null)
            {
                int i;
                for (i = 0; i < stock.Count; i++)
                {
                    if (IsInternal20(stock[i]) || IsGunMount(stock[i]))
                        return true;
                }
            }
            if (idx == 0)
                return true;
            return false;
        }

        internal static bool IsInternal20(WeaponMount m)
        {
            if (m == null)
                return false;
            string key = m.jsonKey != null ? m.jsonKey : string.Empty;
            if (string.Equals(key, Internal20Key, StringComparison.OrdinalIgnoreCase))
                return true;
            if (key.IndexOf("20mm_Internal", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string n = ((m.mountName != null ? m.mountName : string.Empty) + " "
                + (m.name != null ? m.name : string.Empty));
            return n.IndexOf("20mm", StringComparison.OrdinalIgnoreCase) >= 0
                && n.IndexOf("Internal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool NameLooksInternalGun(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.IndexOf("20mm", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Internal", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Cannon", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("Gunpod", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Pod", StringComparison.OrdinalIgnoreCase) < 0)
                return true;
            return false;
        }

        internal static WeaponMount FindInternal20()
        {
            WeaponMount hit = FindMountByKey(Internal20Key);
            if (hit != null)
                return hit;
            WeaponMount[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<WeaponMount>(); }
            catch { all = null; }
            if (all == null)
                return null;
            int i;
            for (i = 0; i < all.Length; i++)
            {
                if (IsInternal20(all[i]))
                    return all[i];
            }
            return null;
        }

        private static WeaponMount FindMountByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            try
            {
                if (Encyclopedia.WeaponLookup != null)
                {
                    WeaponMount lookup;
                    if (Encyclopedia.WeaponLookup.TryGetValue(key, out lookup) && lookup != null)
                        return lookup;
                }
            }
            catch { }
            WeaponMount[] all = null;
            try { all = Resources.FindObjectsOfTypeAll<WeaponMount>(); }
            catch { all = null; }
            if (all == null)
                return null;
            int i;
            for (i = 0; i < all.Length; i++)
            {
                WeaponMount m = all[i];
                if (m != null
                    && string.Equals(m.jsonKey, key, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
            return null;
        }

        internal static void PrepareStockMount(WeaponMount m)
        {
            if (m == null)
                return;
            if (string.IsNullOrEmpty(m.mountName))
            {
                if (IsInternal20(m))
                    m.mountName = "20mm Internal";
                else if (!string.IsNullOrEmpty(m.jsonKey))
                    m.mountName = m.jsonKey;
            }
            if (MountDisabledField == null)
                return;
            try
            {
                object cur = MountDisabledField.GetValue(m);
                if (cur is bool && (bool)cur)
                    MountDisabledField.SetValue(m, false);
            }
            catch { }
        }

        private static bool ParamsHaveInternal20(AircraftParameters p)
        {
            if (p == null)
                return false;
            if (p.loadouts != null)
            {
                int i;
                for (i = 0; i < p.loadouts.Count; i++)
                {
                    NuclearOption.SavedMission.Loadout lo = p.loadouts[i];
                    if (lo != null && ListHasInternal20(lo.weapons))
                        return true;
                }
            }
            return CountPresetWeapons(p.StandardLoadouts) > 0
                && StandardLoadoutsHaveInternal20(p.StandardLoadouts);
        }

        private static bool StandardLoadoutsHaveInternal20(StandardLoadout[] presets)
        {
            if (presets == null)
                return false;
            int i;
            for (i = 0; i < presets.Length; i++)
            {
                StandardLoadout sl = presets[i];
                if (sl != null && sl.loadout != null && ListHasInternal20(sl.loadout.weapons))
                    return true;
            }
            return false;
        }

        private static bool ListHasInternal20(List<WeaponMount> list)
        {
            if (list == null)
                return false;
            int i;
            for (i = 0; i < list.Count; i++)
            {
                if (IsInternal20(list[i]))
                    return true;
            }
            return false;
        }

        private static int CountPresetWeapons(StandardLoadout[] presets)
        {
            if (presets == null)
                return 0;
            int n = 0;
            int i;
            for (i = 0; i < presets.Length; i++)
            {
                StandardLoadout sl = presets[i];
                if (sl == null || sl.disabled || sl.loadout == null || sl.loadout.weapons == null)
                    continue;
                int w;
                for (w = 0; w < sl.loadout.weapons.Count; w++)
                {
                    if (sl.loadout.weapons[w] != null)
                        n++;
                }
            }
            return n;
        }

        internal static void NotifyCannotEject(Aircraft ac)
        {
            if (ac == null || !IsOurs(ac))
                return;
            bool local = false;
            try { local = GameManager.IsLocalAircraft(ac); }
            catch { local = true; }
            if (!local)
            {
                try
                {
                    Aircraft mine;
                    if (GameManager.GetLocalAircraft(out mine) && mine != null
                        && object.ReferenceEquals(mine, ac))
                        local = true;
                }
                catch { }
            }
            if (!local)
                return;
            _ejectDenyUntil = Time.unscaledTime + 2.5f;
        }

        internal static void ApplyThrust(Aircraft ac)
        {
            if (ac == null)
                return;
            float thrust = FullLoadThrust(ac);
            Turbojet[] jets = null;
            try { jets = ac.GetComponentsInChildren<Turbojet>(true); }
            catch { jets = null; }
            if (jets == null)
                return;
            for (int i = 0; i < jets.Length; i++)
                ForceJetThrust(jets[i], ac, thrust);
            ApplyIrAndAfterburner(ac);
        }

        internal static Aircraft AircraftOfNozzle(JetNozzle nozzle)
        {
            if (nozzle == null)
                return null;
            Aircraft ac = null;
            try
            {
                if (NozzleAircraft != null)
                    ac = NozzleAircraft.GetValue(nozzle) as Aircraft;
            }
            catch { ac = null; }
            if (ac == null)
            {
                try { ac = nozzle.GetComponentInParent<Aircraft>(); }
                catch { ac = null; }
            }
            return ac;
        }

        internal static bool OursNozzle(JetNozzle nozzle)
        {
            return IsOurs(AircraftOfNozzle(nozzle));
        }

        internal static void ForceAllowAfterburner(JetNozzle nozzle, ref bool allowAfterburner)
        {
            Aircraft ac = AircraftOfNozzle(nozzle);
            if (!IsOurs(ac) || !IsLiveAircraft(ac))
                return;
            allowAfterburner = AfterburnerRequested(ac);
        }

        private static bool AfterburnerRequested(Aircraft ac)
        {
            if (ac == null)
                return false;
            try
            {
                ControlInputs inputs = ac.GetInputs();
                if (inputs != null && inputs.throttle >= AbThrottleStart)
                    return true;
            }
            catch { }
            return false;
        }

        internal static void ClampNozzleIR(JetNozzle nozzle, bool max, ref float result)
        {
            if (!OursNozzle(nozzle))
                return;
            result = max ? IrMax : IrMin;
        }

        internal static void ApplyIrAndAfterburner(Aircraft ac)
        {
            if (ac == null)
                return;
            JetNozzle[] nozzles = null;
            try { nozzles = ac.GetComponentsInChildren<JetNozzle>(true); }
            catch { nozzles = null; }
            if (nozzles == null)
                return;
            float extra = FullLoadThrust(ac) * AbThrustMul;
            bool abOn = AfterburnerRequested(ac);
            for (int i = 0; i < nozzles.Length; i++)
            {
                JetNozzle n = nozzles[i];
                if (n == null)
                    continue;
                if (NozzleIRMin != null)
                {
                    try { NozzleIRMin.SetValue(n, IrMin); }
                    catch { }
                }
                if (NozzleIRMax != null)
                {
                    try { NozzleIRMax.SetValue(n, IrMax); }
                    catch { }
                }
                if (NozzleIrSource != null)
                {
                    try
                    {
                        IRSource src = NozzleIrSource.GetValue(n) as IRSource;
                        if (src != null && src.intensity > IrMax)
                            src.intensity = IrMax;
                    }
                    catch { }
                }
                EnsureAfterburner(n, ac, extra, abOn);
            }
            Turbojet[] jets = null;
            try { jets = ac.GetComponentsInChildren<Turbojet>(true); }
            catch { jets = null; }
            if (jets != null && TurbojetAbOn != null)
            {
                for (int i = 0; i < jets.Length; i++)
                {
                    if (jets[i] == null)
                        continue;
                    try { TurbojetAbOn.SetValue(jets[i], abOn); }
                    catch { }
                }
            }
            DestroyAbFlamePrimitive(ac);
            Ab4Fx.SetVisible(ac, abOn);
        }

        private static void EnsureAfterburner(JetNozzle nozzle, Aircraft ac, float extraThrust, bool abOn)
        {
            if (nozzle == null || AfterburnerType == null || NozzleAfterburners == null)
                return;
            object arrObj = null;
            try { arrObj = NozzleAfterburners.GetValue(nozzle); }
            catch { arrObj = null; }
            Array arr = arrObj as Array;
            object ab = null;
            if (arr != null && arr.Length > 0)
                ab = arr.GetValue(0);
            if (ab == null)
            {
                try { ab = AccessTools.CreateInstance(AfterburnerType); }
                catch { ab = null; }
                if (ab == null)
                    return;
                Array created = Array.CreateInstance(AfterburnerType, 1);
                created.SetValue(ab, 0);
                try { NozzleAfterburners.SetValue(nozzle, created); }
                catch { return; }
            }
            if (AbThrottleStartField != null)
            {
                try { AbThrottleStartField.SetValue(ab, AbThrottleStart); }
                catch { }
            }
            if (AbThrottleEndField != null)
            {
                try { AbThrottleEndField.SetValue(ab, 1f); }
                catch { }
            }
            if (AbThrustField != null)
            {
                try { AbThrustField.SetValue(ab, extraThrust); }
                catch { }
            }
            if (AbFuelField != null)
            {
                try { AbFuelField.SetValue(ab, extraThrust * 0.00008f); }
                catch { }
            }
            if (AbFlameBrightField != null)
            {
                try { AbFlameBrightField.SetValue(ab, abOn ? 4.5f : 0f); }
                catch { }
            }
            if (AbGlowBrightField != null)
            {
                try { AbGlowBrightField.SetValue(ab, abOn ? 3.5f : 0f); }
                catch { }
            }
            if (AbIRField != null)
            {
                try { AbIRField.SetValue(ab, IrMax); }
                catch { }
            }
            if (AbSmoothingField != null)
            {
                try { AbSmoothingField.SetValue(ab, 0.12f); }
                catch { }
            }
            DestroyAbFlamePrimitive(ac);
            Renderer flame = FindNamedRenderer(ac, "MIG15S_AB4Flame");
            Renderer haze = FindNamedRenderer(ac, "MIG15S_AB4Glow");
            if (Ab4Fx.MissingMaterial(flame))
                flame = null;
            if (Ab4Fx.MissingMaterial(haze))
                haze = null;
            if (AbFlameRenderer != null)
            {
                try { AbFlameRenderer.SetValue(ab, flame); }
                catch { }
            }
            if (AbGlowRenderer != null)
            {
                try { AbGlowRenderer.SetValue(ab, haze); }
                catch { }
            }
            if (AbSourceField != null && NozzleThrustAudio != null)
            {
                try
                {
                    object src = NozzleThrustAudio.GetValue(nozzle);
                    if (src != null)
                        AbSourceField.SetValue(ab, src);
                }
                catch { }
            }
        }

        private static Renderer FindNamedRenderer(Aircraft ac, string part)
        {
            if (ac == null || string.IsNullOrEmpty(part))
                return null;
            Renderer[] rs = null;
            try { rs = ac.GetComponentsInChildren<Renderer>(true); }
            catch { rs = null; }
            if (rs == null)
                return null;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                string n = rs[i].gameObject.name;
                if (n != null && n.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return rs[i];
            }
            return null;
        }

        private static void DestroyAbFlamePrimitive(Aircraft ac)
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
                if (t == null || t.name != "MIG15S_ABFlame")
                    continue;
                UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        internal static void ForceJetThrust(Turbojet jet)
        {
            if (jet == null)
                return;
            Aircraft ac = null;
            try
            {
                if (JetAircraft != null)
                    ac = JetAircraft.GetValue(jet) as Aircraft;
            }
            catch { ac = null; }
            if (ac == null)
            {
                try { ac = jet.GetComponentInParent<Aircraft>(); }
                catch { ac = null; }
            }
            if (ac == null || !IsMigClone(ac))
                return;
            if (InEncyclopedia() || !IsLiveAircraft(ac))
                return;
            if (!HasSimAuthority(ac))
                return;
            ForceJetThrust(jet, ac, FullLoadThrust(ac));
        }

        internal static bool TryOursMaxThrust(Turbojet jet, out float thrust)
        {
            thrust = 0f;
            if (jet == null)
                return false;
            Aircraft ac = null;
            try
            {
                if (JetAircraft != null)
                    ac = JetAircraft.GetValue(jet) as Aircraft;
            }
            catch { ac = null; }
            if (ac == null)
            {
                try { ac = jet.GetComponentInParent<Aircraft>(); }
                catch { ac = null; }
            }
            if (ac == null || !IsMigClone(ac) || !IsLiveAircraft(ac))
                return false;
            thrust = FullLoadThrust(ac);
            return thrust > 1f;
        }

        private static void ForceJetThrust(Turbojet jet, Aircraft ac, float thrust)
        {
            if (jet == null || thrust < 1f)
                return;
            jet.maxThrust = thrust;
            if (JetMaxSpeed != null)
            {
                try { JetMaxSpeed.SetValue(jet, SpeedCapMps); }
                catch { }
            }
            int id = jet.GetInstanceID();
            if (EngineTuned.Add(id))
            {
                if (JetMinDensity != null)
                {
                    try { JetMinDensity.SetValue(jet, 0.01f); }
                    catch { }
                }
                if (JetAltThrust != null)
                {
                    try { JetAltThrust.SetValue(jet, AnimationCurve.Linear(0f, 1f, 30000f, 1f)); }
                    catch { }
                }
            }
            if (JetOperable != null)
            {
                try { JetOperable.SetValue(jet, true); }
                catch { }
            }
        }

        private static float FullLoadThrust(Aircraft ac)
        {
            float mass = FullLoadMass(ac);
            float thrust = TargetTwR * mass * 9.81f;
            if (thrust < 1f)
                thrust = 1f;
            return thrust;
        }

        private static float FullLoadMass(Aircraft ac)
        {
            if (ac == null)
                return 5055f;
            int id = ac.GetInstanceID();
            float cached;
            if (Mtow.TryGetValue(id, out cached) && cached >= 4000f)
                return cached;
            float parts = 0f;
            UnitPart[] up = null;
            try { up = ac.GetComponentsInChildren<UnitPart>(true); }
            catch { up = null; }
            if (up != null)
            {
                for (int i = 0; i < up.Length; i++)
                {
                    if (up[i] == null)
                        continue;
                    if (up[i].mass > 0f)
                        parts += up[i].mass;
                }
            }
            float fuel = 0f;
            FuelTank[] tanks = null;
            try { tanks = ac.GetComponentsInChildren<FuelTank>(true); }
            catch { tanks = null; }
            if (tanks != null)
            {
                for (int i = 0; i < tanks.Length; i++)
                {
                    if (tanks[i] == null)
                        continue;
                    float cap = 0f;
                    try { cap = tanks[i].GetCapacity(); }
                    catch { cap = 0f; }
                    if (cap < 1f && TankCapacity != null)
                    {
                        try { cap = (float)TankCapacity.GetValue(tanks[i]); }
                        catch { cap = 0f; }
                    }
                    if (cap >= 1f && cap < 1200f)
                        fuel += cap;
                }
            }
            float mass = parts + fuel;
            float live = 0f;
            try
            {
                if (ac.rb != null)
                    live = ac.rb.mass;
            }
            catch { live = 0f; }
            if (live > mass)
                mass = live;
            AircraftDefinition def = ac.definition as AircraftDefinition;
            if (def != null && def.aircraftInfo != null)
            {
                if (def.aircraftInfo.maxWeight > mass)
                    mass = def.aircraftInfo.maxWeight;
            }
            if (IsOaFamilyClone(ac))
            {
                if (mass < 200f)
                    mass = 5055f;
            }
            else if (mass < 4000f)
                mass = 5055f;
            Mtow[id] = mass;
            return mass;
        }

        internal static bool InEncyclopedia()
        {
            try
            {
                return GameManager.gameState == GameState.Encyclopedia;
            }
            catch
            {
                return false;
            }
        }

        internal static void BeginHangarPreview()
        {
            _hangarPreviewSpawn = true;
        }

        internal static void EndHangarPreview(Aircraft preview)
        {
            _hangarPreviewSpawn = false;
            _hangarPreview = preview;
        }

        internal static void ClearHangarPreview()
        {
            _hangarPreviewSpawn = false;
            _hangarPreview = null;
        }

        /// <summary>
        /// Hangar SpawnPreview instantiates a display jet (networked=false) then
        /// SetComplexPhysics. Live thrust / joint welding in Awake destroys extra
        /// rigidbodies before the hangar collects them — twitch then PhysX abort.
        /// </summary>
        internal static bool IsLiveAircraft(Aircraft ac)
        {
            if (ac == null || !Plugin.IsRuntime(ac))
                return false;
            if (InEncyclopedia())
                return false;
            if (_hangarPreviewSpawn)
                return false;
            if (_hangarPreview != null && object.ReferenceEquals(ac, _hangarPreview))
                return false;
            try
            {
                if (!ac.networked)
                    return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Still in ground effect. Ignore the gear handle.
        /// </summary>
        internal static bool GearOnGround(Aircraft ac)
        {
            if (ac == null || !IsLiveAircraft(ac))
                return false;
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            return alt < 25f;
        }

        internal static float HangarAge(Aircraft ac)
        {
            if (ac == null)
                return 0f;
            int id = ac.GetInstanceID();
            float t;
            if (!SpawnedAt.TryGetValue(id, out t))
            {
                t = Time.unscaledTime;
                SpawnedAt[id] = t;
            }
            return Time.unscaledTime - t;
        }

        internal static float GroundSpeed(Aircraft ac)
        {
            if (ac == null || ac.rb == null)
                return 0f;
            try
            {
                Vector3 v = ac.rb.velocity;
                v.y = 0f;
                return v.magnitude;
            }
            catch { return 0f; }
        }

        /// <summary>
        /// True while the jet is still sitting in the hangar. Horizontal speed
        /// only — oleo bounce and the gear handle must not count as airborne.
        /// </summary>
        internal static bool InHangarHold(Aircraft ac)
        {
            if (ac == null || !IsLiveAircraft(ac))
                return false;
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            if (alt >= 20f)
                return false;
            if (GroundSpeed(ac) >= 8f)
                return false;
            return true;
        }

        internal static bool BlockHangarCrash(Aircraft ac)
        {
            if (ac == null || !IsOurs(ac) || !IsLiveAircraft(ac))
                return false;
            if (FuzeArmed(ac))
                return false;
            if (HangarAge(ac) >= 15f)
                return false;
            float alt = 0f;
            try { alt = ac.radarAlt; }
            catch { alt = 0f; }
            if (alt >= 25f)
                return false;
            return GroundSpeed(ac) < 12f;
        }

        internal static void PoseEncyclopediaAircraft(Aircraft ac)
        {
            if (ac == null || !IsOurs(ac))
                return;
            if (!IsOaFamilyClone(ac))
                HidePilotModel(ac);
            if (IsOaFamilyClone(ac))
                return;
            ApplyNoseBallast(ac);
            ApplyIrAndAfterburner(ac);
            ApplyCg(ac, EncCgLocalZ);
            try { ac.SetGear(true); }
            catch { }
            Turbojet[] jets = null;
            try { jets = ac.GetComponentsInChildren<Turbojet>(true); }
            catch { jets = null; }
            if (jets != null)
            {
                for (int i = 0; i < jets.Length; i++)
                {
                    if (jets[i] == null)
                        continue;
                    jets[i].maxThrust = 0f;
                }
            }
            try
            {
                ControlInputs inputs = ac.GetInputs();
                if (inputs != null)
                    inputs.throttle = 0f;
            }
            catch { }
            if (ac.rb != null)
            {
                try
                {
                    ac.rb.automaticCenterOfMass = false;
                    Vector3 c = ac.rb.centerOfMass;
                    ac.rb.centerOfMass = new Vector3(0f, c.y, EncCgLocalZ);
                    ac.rb.velocity = Vector3.zero;
                    ac.rb.angularVelocity = Vector3.zero;
                    ac.rb.constraints = RigidbodyConstraints.FreezeRotation;
                }
                catch { }
            }
            try
            {
                Vector3 e = ac.transform.eulerAngles;
                ac.transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            }
            catch { }
        }

        private static void ApplyNoseBallast(Aircraft ac)
        {
            if (ac == null)
                return;
            if (!CgMassDone.Add(ac.GetInstanceID()))
                return;
            UnitPart[] parts = null;
            try { parts = ac.GetComponentsInChildren<UnitPart>(true); }
            catch { parts = null; }
            if (parts == null)
                return;
            for (int i = 0; i < parts.Length; i++)
            {
                UnitPart p = parts[i];
                if (p == null)
                    continue;
                string n = p.name != null ? p.name : string.Empty;
                if (n.IndexOf("Intake", StringComparison.OrdinalIgnoreCase) >= 0)
                    p.mass += 650f;
                else if (n.IndexOf("Cockpit", StringComparison.OrdinalIgnoreCase) >= 0)
                    p.mass += 250f;
                else if (n.IndexOf("Fuselage_Tailpipe", StringComparison.OrdinalIgnoreCase) >= 0)
                    p.mass *= 0.4f;
                else if (n.IndexOf("Wing_Elevator", StringComparison.OrdinalIgnoreCase) >= 0)
                    p.mass *= 0.45f;
                else if (n.IndexOf("Aryx_MiG15_Tail", StringComparison.OrdinalIgnoreCase) >= 0)
                    p.mass *= 0.45f;
            }
        }

        private static void ApplyCg(Aircraft ac)
        {
            ApplyCg(ac, CgLocalZ);
        }

        private static void ApplyCg(Aircraft ac, float localZ)
        {
            if (ac == null || ac.rb == null)
                return;
            try
            {
                ac.rb.automaticCenterOfMass = false;
                Vector3 c = ac.rb.centerOfMass;
                if (c.z < localZ - 0.02f || c.z > localZ + 0.02f)
                    ac.rb.centerOfMass = new Vector3(0f, c.y, localZ);
            }
            catch
            {
                try
                {
                    Vector3 c = ac.rb.centerOfMass;
                    ac.rb.centerOfMass = new Vector3(0f, c.y, localZ);
                }
                catch { }
            }
        }

        private static float ReadFullMass(Aircraft ac)
        {
            float mass = 0f;
            try
            {
                if (ac.rb != null)
                    mass = ac.rb.mass;
            }
            catch { mass = 0f; }
            if (mass >= 1500f)
                return mass;
            float sum = 0f;
            UnitPart[] parts = null;
            try { parts = ac.GetComponentsInChildren<UnitPart>(true); }
            catch { parts = null; }
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null)
                        continue;
                    if (parts[i].mass > 0f)
                        sum += parts[i].mass;
                }
            }
            FuelTank[] tanks = null;
            try { tanks = ac.GetComponentsInChildren<FuelTank>(true); }
            catch { tanks = null; }
            if (tanks != null)
            {
                for (int i = 0; i < tanks.Length; i++)
                {
                    if (tanks[i] == null)
                        continue;
                    if (tanks[i].fuelMass > 0f)
                        sum += tanks[i].fuelMass;
                }
            }
            if (sum < 400f)
                sum = 5055f;
            return sum;
        }

        private static void BuffAirframe(Aircraft aircraft)
        {
            if (aircraft == null)
                return;
            AeroPart[] aeros = aircraft.GetComponentsInChildren<AeroPart>(true);
            float mul = StrengthMulOf(aircraft);
            for (int i = 0; i < aeros.Length; i++)
                BuffAeroPart(aeros[i], mul);
            UnitPart[] parts = aircraft.GetComponentsInChildren<UnitPart>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                UnitPart part = parts[i];
                if (part == null || part is AeroPart)
                    continue;
                BuffUnitPartDurability(part, mul);
            }
        }

        private static void BuffAeroPart(AeroPart part, float mul)
        {
            if (part == null)
                return;
            BuffUnitPartDurability(part, mul);
            if (AeroJoints == null)
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
                if (pj.breakForce > 0f && !float.IsInfinity(pj.breakForce))
                    pj.breakForce *= mul;
                if (pj.breakTorque > 0f && !float.IsInfinity(pj.breakTorque))
                    pj.breakTorque *= mul;
                Joint j = pj.joint;
                if (j == null)
                    continue;
                if (j.breakForce > 0f && !float.IsInfinity(j.breakForce))
                    j.breakForce *= mul;
                if (j.breakTorque > 0f && !float.IsInfinity(j.breakTorque))
                    j.breakTorque *= mul;
            }
        }

        private static void BuffUnitPartDurability(UnitPart part, float mul)
        {
            if (part == null)
                return;
            if (UnitStructural != null)
            {
                try
                {
                    float th = (float)UnitStructural.GetValue(part);
                    if (th > 0.01f)
                        UnitStructural.SetValue(part, th / mul);
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
                    ImpactThreshold.SetValue(impact, th * mul);
                if (ImpactMultiplier != null)
                {
                    float m = (float)ImpactMultiplier.GetValue(impact);
                    if (m > 0.01f)
                        ImpactMultiplier.SetValue(impact, m / mul);
                }
            }
            catch { }
        }

        internal static bool TryDetonate(Aircraft ac, string reason)
        {
            return TryDetonate(ac, reason, true);
        }

        internal static bool TryDetonate(Aircraft ac, string reason, bool requireFuze)
        {
            if (ac == null || ac.transform == null)
                return false;
            if (!IsOurs(ac))
                return false;
            if (IsOaConventionalClone(ac))
                return false;
            if (InEncyclopedia())
                return false;
            if (requireFuze)
            {
                if (!FuzeArmed(ac))
                    return false;
                if (!FuzeSettled(ac))
                    return false;
                if (!GearIsUp(ac))
                    return false;
            }
            int id = ac.GetInstanceID();
            if (Detonated.Contains(id))
                return false;
            Detonated.Add(id);
            Vector3 pos = ac.transform.position;
            CrashAsPilot(ac);
            DemolishAround(pos, ac);
            SpawnTenKt(pos, ac);
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("MiG-15S 1kt (" + reason + ")");
            return true;
        }

        internal static void ReplayRemoteBoom(uint persistentId, Vector3 pos)
        {
            Aircraft ac = FindByPersistent(persistentId);
            if (ac != null)
            {
                int iid = ac.GetInstanceID();
                if (Detonated.Contains(iid))
                    return;
                Detonated.Add(iid);
                try
                {
                    if (!ac.disabled)
                    {
                        _skipDisableBoom = true;
                        ac.DisableUnit();
                        _skipDisableBoom = false;
                    }
                }
                catch { _skipDisableBoom = false; }
            }
            SpawnTenKt(pos, ac);
        }

        internal static Aircraft FindByPersistent(uint persistentId)
        {
            if (persistentId == 0)
                return null;
            List<Aircraft> all = null;
            try { all = UnitRegistry.allAircraft; }
            catch { all = null; }
            if (all == null)
                return null;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft ac = all[i];
                if (ac == null)
                    continue;
                try
                {
                    if (ac.persistentID.Id == persistentId)
                        return ac;
                }
                catch { }
            }
            return null;
        }

        private static void CrashAsPilot(Aircraft ac)
        {
            if (ac == null)
                return;
            if (AircraftEjected != null)
            {
                try { AircraftEjected.SetValue(ac, false); }
                catch { }
            }
            Pilot[] pilots = null;
            try { pilots = ac.pilots; }
            catch { pilots = null; }
            if (pilots != null)
            {
                PersistentID none = default(PersistentID);
                for (int i = 0; i < pilots.Length; i++)
                {
                    Pilot p = pilots[i];
                    if (p == null)
                        continue;
                    try { p.ejected = false; }
                    catch { }
                    try { p.TakeDamage(0f, 0f, 1f, 0f, 1e9f, none); }
                    catch
                    {
                        try { p.ApplyDamage(0f, 0f, 0f, 1e9f); }
                        catch { }
                    }
                }
            }
            try
            {
                if (!ac.disabled)
                {
                    _skipDisableBoom = true;
                    ac.DisableUnit();
                    _skipDisableBoom = false;
                }
            }
            catch { _skipDisableBoom = false; }
            try { ac.CmdDisableUnit(); }
            catch { }
        }

        private static void DemolishAround(Vector3 pos, Aircraft self)
        {
            Collider[] hits = null;
            int n = 0;
            try { n = Physics.OverlapSphereNonAlloc(pos, DemolishRadius, DemolishHits); }
            catch { n = 0; }
            if (n > 0 && n < DemolishHits.Length)
            {
                for (int i = 0; i < n; i++)
                    DemolishCollider(DemolishHits[i], self);
                return;
            }
            try { hits = Physics.OverlapSphere(pos, DemolishRadius); }
            catch { hits = null; }
            if (hits == null)
            {
                for (int i = 0; i < n && i < DemolishHits.Length; i++)
                    DemolishCollider(DemolishHits[i], self);
                return;
            }
            for (int i = 0; i < hits.Length; i++)
                DemolishCollider(hits[i], self);
        }

        private static void DemolishCollider(Collider col, Aircraft self)
        {
            if (col == null)
                return;
            Transform t = col.transform;
            if (t == null)
                return;
            if (self != null && t.IsChildOf(self.transform))
                return;
            MapBuilding mb = t.GetComponentInParent<MapBuilding>();
            if (mb != null)
                DemolishMapBuilding(mb);
            Unit u = t.GetComponentInParent<Unit>();
            if (u != null && (self == null || !object.ReferenceEquals(u, self)))
                DemolishUnit(u);
        }

        private static void DemolishMapBuilding(MapBuilding mb)
        {
            if (mb == null)
                return;
            PersistentID none = default(PersistentID);
            try { mb.TakeDamage(0f, 0f, 1f, 0f, 1e9f, none); }
            catch { }
            try { mb.ApplyDamage(0f, 0f, 0f, 1e9f); }
            catch { }
            if (MapBuildingSetField == null || MapBuildingIndexField == null)
                return;
            try
            {
                MapBuildingSet set = MapBuildingSetField.GetValue(mb) as MapBuildingSet;
                if (set == null)
                    return;
                int idx = (int)MapBuildingIndexField.GetValue(mb);
                set.DestroyBuilding(idx);
            }
            catch { }
        }

        private static void DemolishUnit(Unit u)
        {
            if (u == null)
                return;
            Ship ship = u as Ship;
            if (ship != null)
            {
                KillShipQuiet(ship);
                return;
            }
            try
            {
                if (u.disabled)
                    return;
            }
            catch { }
            PersistentID none = default(PersistentID);
            UnitPart[] parts = null;
            try { parts = u.GetComponentsInChildren<UnitPart>(true); }
            catch { parts = null; }
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    UnitPart p = parts[i];
                    if (p == null)
                        continue;
                    try { p.TakeDamage(0f, 0f, 1f, 0f, 1e9f, none); }
                    catch { }
                    try { p.SpawnFragments(); }
                    catch { }
                    try { p.Detach(Vector3.zero, Vector3.zero); }
                    catch { }
                }
            }
            Aircraft other = u as Aircraft;
            if (other != null && other.pilots != null)
            {
                for (int i = 0; i < other.pilots.Length; i++)
                {
                    Pilot pl = other.pilots[i];
                    if (pl == null)
                        continue;
                    try { pl.TakeDamage(0f, 0f, 1f, 0f, 1e9f, none); }
                    catch
                    {
                        try { pl.ApplyDamage(0f, 0f, 0f, 1e9f); }
                        catch { }
                    }
                }
            }
            try { u.DisableUnit(); }
            catch { }
            try { u.CmdDisableUnit(); }
            catch { }
        }

        /// <summary>
        /// Wreck the ship in place. Detach superstructure, but keep the hull
        /// so leftover buoyancy cannot spin the wreck into the sky.
        /// </summary>
        private static void KillShipQuiet(Ship ship)
        {
            if (ship == null)
                return;
            PersistentID none = default(PersistentID);
            UnitPart[] parts = null;
            try { parts = ship.GetComponentsInChildren<UnitPart>(true); }
            catch { parts = null; }
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    UnitPart p = parts[i];
                    if (p == null)
                        continue;
                    try { p.TakeDamage(0f, 0f, 1f, 0f, 1e9f, none); }
                    catch { }
                    try { p.SpawnFragments(); }
                    catch { }
                    if (KeepShipHull(ship, p))
                        continue;
                    try { p.Detach(Vector3.zero, Vector3.zero); }
                    catch { }
                }
            }
            try
            {
                if (!ship.disabled)
                    ship.DisableUnit();
            }
            catch { }
            try { ship.CmdDisableUnit(); }
            catch { }
            PinShipWreck(ship);
        }

        private static bool KeepShipHull(Ship ship, UnitPart part)
        {
            if (ship == null || part == null)
                return true;
            try
            {
                if (object.ReferenceEquals(part.gameObject, ship.gameObject))
                    return true;
                if (object.ReferenceEquals(part.transform, ship.transform))
                    return true;
            }
            catch { return true; }
            Rigidbody shipRb = null;
            try { shipRb = ship.GetComponent<Rigidbody>(); }
            catch { shipRb = null; }
            if (shipRb == null)
                return false;
            Rigidbody partRb = null;
            try { partRb = part.GetComponent<Rigidbody>(); }
            catch { partRb = null; }
            if (partRb != null && object.ReferenceEquals(partRb, shipRb))
                return true;
            try
            {
                if (object.ReferenceEquals(part.transform, shipRb.transform))
                    return true;
            }
            catch { }
            return false;
        }

        internal static void PinShipWreck(Ship ship)
        {
            if (ship == null)
                return;
            int id = ship.GetInstanceID();
            ShipWreckUntil[id] = Time.unscaledTime + ShipWreckSettleSec;
            bool listed = false;
            for (int i = 0; i < ShipWrecks.Count; i++)
            {
                if (object.ReferenceEquals(ShipWrecks[i], ship))
                {
                    listed = true;
                    break;
                }
            }
            if (!listed)
                ShipWrecks.Add(ship);
            SettleUnitBodies(ship);
        }

        internal static bool IsSettlingShip(Ship ship)
        {
            if (ship == null)
                return false;
            float until;
            if (!ShipWreckUntil.TryGetValue(ship.GetInstanceID(), out until))
                return false;
            return Time.unscaledTime < until;
        }

        internal static void TickShipWrecks()
        {
            if (ShipWrecks.Count == 0)
                return;
            float now = Time.unscaledTime;
            for (int i = ShipWrecks.Count - 1; i >= 0; i--)
            {
                Ship ship = ShipWrecks[i];
                if (ship == null)
                {
                    ShipWrecks.RemoveAt(i);
                    continue;
                }
                int id = ship.GetInstanceID();
                float until;
                if (!ShipWreckUntil.TryGetValue(id, out until) || now >= until)
                {
                    ShipWreckUntil.Remove(id);
                    ShipWrecks.RemoveAt(i);
                    continue;
                }
                SettleUnitBodies(ship);
            }
        }

        internal static void SettleUnitBodies(Unit u)
        {
            if (u == null)
                return;
            Rigidbody[] rbs = null;
            try { rbs = u.GetComponentsInChildren<Rigidbody>(true); }
            catch { rbs = null; }
            if (rbs == null)
                return;
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody rb = rbs[i];
                if (rb == null || rb.isKinematic)
                    continue;
                Vector3 v = Vector3.zero;
                try { v = rb.velocity; }
                catch { continue; }
                if (v.y > 0f)
                    v.y = 0f;
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                if (flat.sqrMagnitude > ShipWreckMaxHoriz * ShipWreckMaxHoriz)
                {
                    flat = flat.normalized * ShipWreckMaxHoriz;
                    v.x = flat.x;
                    v.z = flat.z;
                }
                try
                {
                    rb.velocity = v;
                    rb.angularVelocity = Vector3.zero;
                }
                catch { }
            }
        }

        private static void SpawnTenKt(Vector3 pos, Aircraft ac)
        {
            PersistentID owner = default(PersistentID);
            try
            {
                if (ac != null)
                    owner = ac.persistentID;
            }
            catch { }
            Quaternion rot = Quaternion.identity;
            try
            {
                if (ac != null && ac.transform != null)
                    rot = ac.transform.rotation;
            }
            catch { }
            GameObject fxPrefab = ResolveNukeFx();
            if (fxPrefab == null)
                return;
            GameObject fx = null;
            try { fx = UnityEngine.Object.Instantiate(fxPrefab, pos, Quaternion.identity); }
            catch { fx = null; }
            PaintTenKt(fx, owner);
        }

        private static void PaintTenKt(GameObject root, PersistentID owner)
        {
            if (root == null)
                return;
            try
            {
                MushroomCloud[] clouds = root.GetComponentsInChildren<MushroomCloud>(true);
                if (clouds != null)
                {
                    for (int i = 0; i < clouds.Length; i++)
                    {
                        if (clouds[i] != null)
                            clouds[i].yield = YieldKt;
                    }
                }
            }
            catch { }
            if (ShockYieldField == null)
                return;
            try
            {
                Shockwave[] waves = root.GetComponentsInChildren<Shockwave>(true);
                if (waves == null)
                    return;
                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] == null)
                        continue;
                    waves[i].enabled = true;
                    ShockYieldField.SetValue(waves[i], YieldKt);
                    try { waves[i].SetOwner(owner, YieldKt); }
                    catch { }
                }
            }
            catch { }
        }

        private static GameObject ResolveNukeFx()
        {
            if (_nukeFx != null)
                return _nukeFx;
            GameObject fromLookup = PrefabFromLookup("explosion_1kt");
            if (fromLookup == null)
                fromLookup = PrefabFromLookup("explosion_20kt");
            if (fromLookup != null)
            {
                _nukeFx = fromLookup;
                return _nukeFx;
            }
            MushroomCloud[] clouds = null;
            try { clouds = Resources.FindObjectsOfTypeAll<MushroomCloud>(); }
            catch { clouds = null; }
            if (clouds == null)
                return null;
            GameObject oneKt = null;
            GameObject twenty = null;
            for (int i = 0; i < clouds.Length; i++)
            {
                GameObject go = RootNukeFx(clouds[i]);
                if (go == null)
                    continue;
                string n = go.name;
                bool isOne = n == "explosion_1kt" || n == "explosion_1kt(Clone)";
                bool isTwenty = n == "explosion_20kt" || n == "explosion_20kt(Clone)";
                if (!isOne && !isTwenty)
                    continue;
                if (!go.scene.IsValid())
                {
                    if (isOne)
                    {
                        _nukeFx = go;
                        return _nukeFx;
                    }
                    if (twenty == null)
                        twenty = go;
                }
                else if (isOne && oneKt == null)
                    oneKt = go;
                else if (isTwenty && twenty == null)
                    twenty = go;
            }
            if (oneKt != null)
                _nukeFx = oneKt;
            else
                _nukeFx = twenty;
            return _nukeFx;
        }

        private static GameObject RootNukeFx(MushroomCloud cloud)
        {
            if (cloud == null)
                return null;
            Transform t = cloud.transform;
            if (t == null)
                return cloud.gameObject;
            Transform p = t.parent;
            if (p != null)
            {
                string n = p.name;
                if (n == "explosion_1kt" || n == "explosion_1kt(Clone)"
                    || n == "explosion_20kt" || n == "explosion_20kt(Clone)")
                    return p.gameObject;
            }
            return cloud.gameObject;
        }

        private static GameObject PrefabFromLookup(string key)
        {
            if (string.IsNullOrEmpty(key) || Encyclopedia.Lookup == null)
                return null;
            UnitDefinition def;
            if (!Encyclopedia.Lookup.TryGetValue(key, out def) || def == null)
                return null;
            return def.unitPrefab;
        }

        internal static bool CollisionShouldBoom(Aircraft ac, Collision collision)
        {
            if (ac == null)
                return false;
            if (!FuzeArmed(ac))
                return false;
            if (!FuzeSettled(ac))
                return false;
            if (!GearIsUp(ac))
                return false;
            if (!WasFlying(ac) && !RecentFast(ac))
                return false;
            if (!RecentFast(ac))
                return false;
            if (collision == null)
                return FlightFix.RecentExternalHit(ac);
            return FlightFix.IsRealImpact(ac, collision);
        }
    }

    [HarmonyPatch(typeof(LiveryMetaData), "CheckAircraft")]
    internal static class Patch_MiG15S_LiveryMeta
    {
        [HarmonyPostfix]
        private static void Postfix(LiveryMetaData __instance, AircraftDefinition aircraft, ref bool __result)
        {
            if (__result)
                return;
            if (Service.IsOaFamilyDef(aircraft))
            {
                string oaKey = __instance.AircraftKey;
                string oaName = __instance.Aircraft;
                if (!string.IsNullOrEmpty(oaKey)
                    && (string.Equals(oaKey, Service.OaDonorKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(oaKey, Service.OaJsonKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(oaKey, Service.OaDJsonKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(oaKey, Service.OaEJsonKey, StringComparison.OrdinalIgnoreCase)))
                {
                    __result = true;
                    return;
                }
                if (!string.IsNullOrEmpty(oaName)
                    && oaName.IndexOf("OA-27", StringComparison.OrdinalIgnoreCase) >= 0)
                    __result = true;
                return;
            }
            if (!Service.IsKamDef(aircraft))
                return;
            string key = __instance.AircraftKey;
            string name = __instance.Aircraft;
            if (!string.IsNullOrEmpty(key)
                && (string.Equals(key, Service.DonorJsonKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, Service.JsonKey, StringComparison.OrdinalIgnoreCase)))
            {
                __result = true;
                return;
            }
            if (string.IsNullOrEmpty(name))
                return;
            if (name.IndexOf("MiG-15", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            if (name.IndexOf("15S", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                __result = true;
                return;
            }
            __result = true;
        }
    }

    [HarmonyPatch(typeof(UnitPart), "SetLivery")]
    internal static class Patch_MiG15S_SetLivery
    {
        [HarmonyPrefix]
        private static bool Prefix(UnitPart __instance, LiveryData livery)
        {
            if (livery != null && livery.Texture != null)
                return true;
            if (__instance == null)
                return true;
            Aircraft ac = null;
            try { ac = __instance.GetComponentInParent<Aircraft>(); }
            catch { ac = null; }
            if (!Service.IsOurs(ac))
                return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Aircraft), "Awake")]
    internal static class Patch_MiG15S_Awake
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Aircraft __instance)
        {
            Service.BindCloneDefinition(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(Aircraft __instance)
        {
            Service.BindCloneDefinition(__instance);
            Service.ApplyPerformance(__instance);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "FixedUpdate")]
    internal static class Patch_MiG15S_FixedUpdate
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Aircraft __instance)
        {
            if (!Service.IsOurs(__instance) || !Service.IsLiveAircraft(__instance))
                return;
            Service.RememberFlight(__instance);
            Service.Watchdog(__instance);
            Service.ApplyPerformance(__instance);
        }
    }

    [HarmonyPatch(typeof(TurbineEngine), "Update")]
    internal static class Patch_OA27C_TurbineUpdate
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(TurbineEngine __instance)
        {
            Service.KeepPropPower(__instance);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "GetMaxPower")]
    internal static class Patch_OA27C_GetMaxPower
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Aircraft __instance, ref bool __result, ref float maxPower)
        {
            if (!Service.IsOaPowered(__instance))
                return;
            float want = Service.FullLoadPowerKw(__instance);
            if (want < 1f)
                return;
            if (maxPower < want * 0.99f)
                maxPower = want;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Transmission), "Awake")]
    internal static class Patch_OA27V_TransAwake
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Transmission __instance)
        {
            if (__instance == null)
                return;
            Aircraft ac = null;
            try { ac = __instance.GetComponentInParent<Aircraft>(); }
            catch { ac = null; }
            Service.ApplyPropPower(ac);
        }
    }

    [HarmonyPatch(typeof(PropFan), "Start")]
    internal static class Patch_OA27V_PropFanStart
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PropFan __instance)
        {
            if (__instance == null)
                return;
            Aircraft ac = null;
            try { ac = __instance.GetComponentInParent<Aircraft>(); }
            catch { ac = null; }
            Service.ApplyPropPower(ac);
        }
    }

    [HarmonyPatch(typeof(JetNozzle), "Thrust")]
    internal static class Patch_MiG15S_NozzleThrust
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(JetNozzle __instance, ref bool allowAfterburner)
        {
            Service.ForceAllowAfterburner(__instance, ref allowAfterburner);
        }
    }

    [HarmonyPatch(typeof(JetNozzle), "GetIRMax")]
    internal static class Patch_MiG15S_IRMax
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(JetNozzle __instance, ref float __result)
        {
            Service.ClampNozzleIR(__instance, true, ref __result);
        }
    }

    [HarmonyPatch(typeof(JetNozzle), "GetIRMin")]
    internal static class Patch_MiG15S_IRMin
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(JetNozzle __instance, ref float __result)
        {
            Service.ClampNozzleIR(__instance, false, ref __result);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "StartEjectionSequence")]
    internal static class Patch_MiG15S_NoEject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Aircraft __instance)
        {
            if (Service.HandleOaPartialEject(__instance))
                return false;
            if (!Service.BlockEjection(__instance))
                return true;
            Service.NotifyCannotEject(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Aircraft), "CmdStartEjectionSequence")]
    internal static class Patch_MiG15S_NoEjectCmd
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Aircraft __instance)
        {
            if (Service.HandleOaPartialEject(__instance))
                return false;
            if (!Service.BlockEjection(__instance))
                return true;
            Service.NotifyCannotEject(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pilot), "CommandEjection")]
    internal static class Patch_MiG15S_NoPilotEject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Pilot __instance)
        {
            Aircraft ac = null;
            try
            {
                if (__instance != null)
                    ac = __instance.aircraft;
            }
            catch { ac = null; }
            if (ac == null && __instance != null)
                ac = __instance.GetComponentInParent<Aircraft>();
            if (Service.HandleOaPartialEject(ac))
                return false;
            if (!Service.BlockEjection(ac))
                return true;
            Service.NotifyCannotEject(ac);
            return false;
        }
    }

    [HarmonyPatch(typeof(RadialMenuAction), "TriggerAction")]
    internal static class Patch_MiG15S_NoRadialEject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(RadialMenuAction __instance, Aircraft aircraft)
        {
            if (__instance == null)
                return true;
            RadialMenuAction.ActionType t = RadialMenuAction.ActionType.Gear;
            try { t = __instance.GetActionType(); }
            catch { return true; }
            if (t != RadialMenuAction.ActionType.Eject)
                return true;
            Service.NotePlayerEjectIntent(aircraft);
            if (Service.HandleOaPartialEject(aircraft))
                return false;
            if (!Service.BlockEjection(aircraft))
                return true;
            Service.NotifyCannotEject(aircraft);
            return false;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
    internal static class Patch_OA27_PlayerEjectIntent
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(PilotPlayerState __instance)
        {
            Service.NoteEjectIfPressed(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "SetAircraft")]
    internal static class Patch_OA27_SetAircraftEject
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, Aircraft aircraft)
        {
            if (__instance == null || aircraft == null)
                return;
            Aircraft old = null;
            try { old = __instance.Aircraft; }
            catch { old = null; }
            if (old == null || object.ReferenceEquals(old, aircraft))
                return;
            Service.NoteSwitchAway(old);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            Service.ClearSwitchAway();
        }
    }

    [HarmonyPatch(typeof(Aircraft), "SpawnEjectingPilot")]
    internal static class Patch_MiG15S_NoEjectPilot
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Aircraft __instance)
        {
            if (Service.IsOaClone(__instance))
                return false;
            return !Service.BlockEjection(__instance);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "RpcEscapeCapsule")]
    internal static class Patch_MiG15S_NoCapsule
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Aircraft __instance)
        {
            if (Service.IsOaClone(__instance))
                return false;
            return !Service.BlockEjection(__instance);
        }
    }

    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { })]
    internal static class Patch_MiG15S_Encyclopedia
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Service.StampAllDefs();
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "SpawnUnit")]
    internal static class Patch_MiG15S_EncSpawnUnit
    {
        [HarmonyPrefix]
        private static void Prefix(UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
        }

        [HarmonyPostfix]
        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
            Unit u = null;
            try { u = __instance.GetSpawnedUnit(); }
            catch { u = null; }
            Aircraft ac = u as Aircraft;
            if (ac == null && u != null)
                ac = u.GetComponent<Aircraft>();
            Service.PoseEncyclopediaAircraft(ac);
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "SpawnAircraft")]
    internal static class Patch_MiG15S_EncSpawnAircraft
    {
        [HarmonyPrefix]
        private static void Prefix(UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
        }

        [HarmonyPostfix]
        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
            Unit u = null;
            try { u = __instance.GetSpawnedUnit(); }
            catch { u = null; }
            Aircraft ac = u as Aircraft;
            if (ac == null && u != null)
                ac = u.GetComponent<Aircraft>();
            Service.PoseEncyclopediaAircraft(ac);
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "Update")]
    internal static class Patch_MiG15S_EncUpdate
    {
        [HarmonyPostfix]
        private static void Postfix(EncyclopediaBrowser __instance)
        {
            Unit u = null;
            try { u = __instance.GetSpawnedUnit(); }
            catch { u = null; }
            Aircraft ac = u as Aircraft;
            if (ac == null && u != null)
                ac = u.GetComponent<Aircraft>();
            if (ac != null)
                Service.PoseEncyclopediaAircraft(ac);
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "DisplayUnitInfo")]
    internal static class Patch_MiG15S_EncInfo
    {
        [HarmonyPrefix]
        private static void Prefix(UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
        }

        [HarmonyPostfix]
        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            Service.ApplyEncyclopedia(definition as AircraftDefinition);
            Service.ApplyEncyclopediaCostLabel(__instance, definition);
        }
    }

    [HarmonyPatch(typeof(AeroPart), "OnCollisionEnter")]
    internal static class Patch_MiG15S_AeroCollision
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AeroPart __instance, Collision collision)
        {
            if (__instance == null)
                return;
            Aircraft ac = __instance.parentUnit as Aircraft;
            if (ac == null)
                ac = __instance.GetComponentInParent<Aircraft>();
            Service.NoteHit(ac, collision);
        }
    }

    [HarmonyPatch(typeof(FuelTank), "OnCollisionEnter")]
    internal static class Patch_MiG15S_FuelCollision
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(FuelTank __instance, Collision collision)
        {
            if (__instance == null)
                return;
            Aircraft ac = __instance.GetComponentInParent<Aircraft>();
            Service.NoteHit(ac, collision);
        }
    }

    [HarmonyPatch(typeof(Unit), "DisableUnit")]
    internal static class Patch_MiG15S_DisableUnit
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Unit __instance)
        {
            Aircraft ac = __instance as Aircraft;
            if (ac == null)
                return true;
            if (Service.BlockHangarCrash(ac))
                return false;
            if (Service.SkipDisableBoom)
                return true;
            Service.RememberFlight(ac);
            if (!Service.IsOurs(ac) || !Service.FuzeArmed(ac))
                return true;
            if (!Service.CollisionShouldBoom(ac, null))
                return true;
            Service.TryDetonate(ac, "disabled");
            return true;
        }
    }

    [HarmonyPatch(typeof(UnitPart), "OnJointBreak")]
    internal static class Patch_MiG15S_JointBreak
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(UnitPart __instance)
        {
            return !FlightFix.BlockJointBreak(__instance);
        }
    }

    [HarmonyPatch(typeof(AeroPart), "CreateRB")]
    internal static class Patch_MiG15S_CreateRB
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(AeroPart __instance)
        {
            return !FlightFix.ShareRigidbody(__instance);
        }
    }

    [HarmonyPatch(typeof(AeroPart), "CreateJoints")]
    internal static class Patch_MiG15S_CreateJoints
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(AeroPart __instance)
        {
            return !FlightFix.SkipCreateJoints(__instance);
        }
    }

    [HarmonyPatch(typeof(Ship), "CheckShipBuoyancy")]
    internal static class Patch_MiG15S_ShipBuoyancy
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Ship __instance)
        {
            if (__instance == null || !Service.IsSettlingShip(__instance))
                return;
            Service.SettleUnitBodies(__instance);
        }
    }

    [HarmonyPatch(typeof(Ship), "ApplyPartsForce")]
    internal static class Patch_MiG15S_ShipPartsForce
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Ship __instance)
        {
            if (__instance == null || !Service.IsSettlingShip(__instance))
                return;
            Service.SettleUnitBodies(__instance);
        }
    }

    [HarmonyPatch(typeof(Ship), "OnCollisionEnter")]
    internal static class Patch_MiG15S_ShipCollision
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Ship __instance, Collision collision)
        {
            if (__instance == null || collision == null)
                return;
            Transform other = null;
            try
            {
                if (collision.collider != null)
                    other = collision.collider.transform;
            }
            catch { other = null; }
            if (other == null)
                return;
            Aircraft ac = other.GetComponentInParent<Aircraft>();
            if (!Service.IsOurs(ac))
                return;
            bool dead = false;
            try { dead = __instance.disabled; }
            catch { dead = false; }
            if (dead)
                Service.PinShipWreck(__instance);
        }
    }
}
