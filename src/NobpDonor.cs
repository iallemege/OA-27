using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// Loads Aryx OA-27 Cavalier .nobp without Blueprinter or BIA.
    /// If Blueprinter is already in the process, leave the file to it.
    /// AssetBundle is invoked by reflection so this DLL does not reference BIA or Blueprinter.
    /// </summary>
    internal static class NobpDonor
    {
        private static object _held;
        private static bool _tried;
        private static float _nextTry;
        private static Type _bundleType;
        private static MethodInfo _loadFromFile;
        private static MethodInfo _loadAll;
        private static MethodInfo _getAll;
        private static PropertyInfo _bundleName;

        internal static void Ensure()
        {
            if (Service.FindDefByKey(Service.OaDonorKey) != null)
                return;
            if (Time.unscaledTime < _nextTry)
                return;
            _nextTry = Time.unscaledTime + 2f;
            if (BlueprinterLoaded())
                return;
            if (_tried && _held != null)
                return;
            if (!ResolveApi())
                return;
            string path = FindNobp();
            if (string.IsNullOrEmpty(path))
                return;
            if (BundleAlreadyOpen(path))
                return;
            try
            {
                _held = _loadFromFile.Invoke(null, new object[] { path });
            }
            catch (Exception ex)
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("OA-27C nobp open: " + ex.Message);
                _tried = true;
                return;
            }
            _tried = true;
            if (_held == null)
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("OA-27C nobp LoadFromFile returned null: " + path);
                return;
            }
            try
            {
                if (_loadAll != null)
                    _loadAll.Invoke(_held, null);
            }
            catch (Exception ex)
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("OA-27C nobp assets: " + ex.Message);
            }
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("OA-27C loaded donor bundle " + Path.GetFileName(path)
                    + " without Blueprinter / BIA");
        }

        private static bool ResolveApi()
        {
            if (_loadFromFile != null)
                return true;
            _bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule", false);
            if (_bundleType == null)
                _bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.CoreModule", false);
            if (_bundleType == null)
                _bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine", false);
            if (_bundleType == null)
                return false;
            _loadFromFile = _bundleType.GetMethod("LoadFromFile",
                BindingFlags.Public | BindingFlags.Static, null,
                new Type[] { typeof(string) }, null);
            _loadAll = _bundleType.GetMethod("LoadAllAssets",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            _getAll = _bundleType.GetMethod("GetAllLoadedAssetBundles",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            _bundleName = _bundleType.GetProperty("name",
                BindingFlags.Public | BindingFlags.Instance);
            return _loadFromFile != null;
        }

        private static bool BlueprinterLoaded()
        {
            Assembly[] all = null;
            try { all = AppDomain.CurrentDomain.GetAssemblies(); }
            catch { return false; }
            if (all == null)
                return false;
            for (int i = 0; i < all.Length; i++)
            {
                string n = null;
                try { n = all[i].GetName().Name; }
                catch { n = null; }
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.IndexOf("Blueprinter", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool BundleAlreadyOpen(string path)
        {
            if (_getAll == null)
                return false;
            string file = Path.GetFileNameWithoutExtension(path);
            object listed = null;
            try { listed = _getAll.Invoke(null, null); }
            catch { return false; }
            System.Collections.IEnumerable open = listed as System.Collections.IEnumerable;
            if (open == null)
                return false;
            foreach (object item in open)
            {
                if (item == null)
                    continue;
                string n = null;
                try
                {
                    if (_bundleName != null)
                        n = _bundleName.GetValue(item, null) as string;
                }
                catch { n = null; }
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.IndexOf(file, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (n.IndexOf("cavalier", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string FindNobp()
        {
            string plugins = null;
            try { plugins = Paths.PluginPath; }
            catch { plugins = null; }
            string hit = FirstNobp(plugins);
            if (!string.IsNullOrEmpty(hit))
                return hit;
            try
            {
                string asm = typeof(NobpDonor).Assembly.Location;
                if (!string.IsNullOrEmpty(asm))
                {
                    hit = FirstNobp(Path.GetDirectoryName(asm));
                    if (!string.IsNullOrEmpty(hit))
                        return hit;
                    hit = FirstNobp(Path.Combine(Path.GetDirectoryName(asm), "OA-27Variant"));
                    if (!string.IsNullOrEmpty(hit))
                        return hit;
                    hit = FirstNobp(Path.Combine(Path.GetDirectoryName(asm), "OA27C"));
                    if (!string.IsNullOrEmpty(hit))
                        return hit;
                }
            }
            catch { }
            return null;
        }

        private static string FirstNobp(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return null;
            string exact = Path.Combine(dir, "Aryx.OA-27.Cavalier_1.0.1.nobp");
            if (File.Exists(exact))
                return exact;
            string[] files = null;
            try { files = Directory.GetFiles(dir, "*.nobp"); }
            catch { return null; }
            if (files == null)
                return null;
            for (int i = 0; i < files.Length; i++)
            {
                string n = Path.GetFileName(files[i]);
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.IndexOf("OA-27", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Cavalier", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("PropAttacker", StringComparison.OrdinalIgnoreCase) >= 0)
                    return files[i];
            }
            return null;
        }
    }
}
