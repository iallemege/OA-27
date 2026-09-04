using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OA27Variant
{
    internal static class PluginInfo
    {
        public const string GUID = "com.ial.oa27variant";
        public const string Name = "OA-27Variant";
        public const string Version = "1.1.4";
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _harmony = new Harmony(PluginInfo.GUID);
            _harmony.PatchAll();
            try { Service.EnsureClones(); }
            catch { }
            try { Service.StampAllDefs(); }
            catch { }
            try { HangarInject.EnsureAiSupply(); }
            catch { }
            Log.LogInfo(PluginInfo.Name + " v" + PluginInfo.Version
                + " (GUID " + PluginInfo.GUID
                + "). Independent OA WSO (Y/N flares and lock). C stealth/STOL, D BDF CAS, E PALA dash. Rank 1.");
        }

        private void Update()
        {
            Service.Tick();
            Service.TickRearEject();
            HangarInject.Tick();
            GunpodInject.Tick();
            RearLure.Tick();
        }

        private void OnGUI()
        {
            Service.Draw();
            OaWso.Draw();
        }

        internal static bool IsRuntime(Component c)
        {
            if (c == null || c.gameObject == null)
                return false;
            try { return c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded; }
            catch { return false; }
        }
    }
}
