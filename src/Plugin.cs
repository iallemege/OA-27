using System;
using System.Reflection;
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
        public const string Version = "1.1.18";
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
            PatchAllSafe();
            try { Service.EnsureClones(); }
            catch { }
            try { Service.StampAllDefs(); }
            catch { }
            try { HangarInject.EnsureAiSupply(); }
            catch { }
            LoadScreen.Arm();
            Log.LogInfo(PluginInfo.Name + " v" + PluginInfo.Version
                + " (GUID " + PluginInfo.GUID
                + "). Standalone hangar select. Spectre / Anvil / Wraith.");
        }

        private void PatchAllSafe()
        {
            Type[] types = null;
            try { types = Assembly.GetExecutingAssembly().GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch { types = null; }
            if (types == null)
                return;
            int ok = 0;
            int skip = 0;
            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || !t.IsClass)
                    continue;
                if (!HasHarmonyPatch(t))
                    continue;
                try
                {
                    _harmony.CreateClassProcessor(t).Patch();
                    ok++;
                }
                catch (Exception ex)
                {
                    skip++;
                    Log.LogWarning("Harmony skip " + t.Name + ": " + ex.Message);
                }
            }
            Log.LogInfo("Harmony patches applied " + ok + ", skipped " + skip);
        }

        private static bool HasHarmonyPatch(Type t)
        {
            object[] attrs = t.GetCustomAttributes(true);
            if (attrs == null)
                return false;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i] == null)
                    continue;
                if (attrs[i] is HarmonyPatch)
                    return true;
                string n = attrs[i].GetType().Name;
                if (n.IndexOf("HarmonyPatch", StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
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
            LoadScreen.Draw();
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
