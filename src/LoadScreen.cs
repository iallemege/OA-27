using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace OA27Variant
{
    /// <summary>
    /// Left boot overlay in the BIA load-bar style. Holds 3 seconds so the
    /// plugin is visibly confirmed on the title screen.
    /// </summary>
    internal static class LoadScreen
    {
        private sealed class Row
        {
            internal string Name;
            internal int Current;
            internal int Total;
            internal bool Failed;
        }

        private const float HoldSec = 3f;
        private static readonly List<Row> Rows = new List<Row>(16);
        private static bool _armed;
        private static bool _hidden;
        private static bool _started;
        private static bool _modsScanned;
        private static float _hideAt = -1f;
        private static string _status = "";
        private static GUIStyle _header;
        private static GUIStyle _body;
        private static GUIStyle _count;
        private static Texture2D _px;
        private static bool _styles;

        internal static void Arm()
        {
            _armed = true;
            _hidden = false;
            _started = false;
            _modsScanned = false;
            _hideAt = -1f;
            _status = "Loading OA-27Variant…";
            Rows.Clear();
            SetRow("OA-27Variant  " + PluginInfo.Version, 1, 1, false);
            SetRow(Service.OaDisplayName, 0, 1, false);
            SetRow(Service.OaDDisplayName, 0, 1, false);
            SetRow(Service.OaEDisplayName, 0, 1, false);
            SetRow("Aryx donor", 0, 1, false);
        }

        internal static void Draw()
        {
            if (!_armed || _hidden)
                return;
            if (!_started
                && Event.current != null
                && Event.current.type == EventType.Repaint
                && Screen.width > 64 && Screen.height > 64)
            {
                _started = true;
                _hideAt = Time.unscaledTime + HoldSec;
                if (Plugin.Log != null)
                    Plugin.Log.LogInfo("OA-27Variant load screen (" + HoldSec.ToString("0") + "s)");
            }
            try { Service.EnsureClones(); }
            catch { }
            RefreshRows();
            if (!_modsScanned)
                ScanLoadedMods();
            if (_started && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                _hidden = true;
                return;
            }
            Paint();
        }

        private static void RefreshRows()
        {
            bool c = Service.OaClone != null;
            bool d = Service.OaDClone != null;
            bool e = Service.OaEClone != null;
            bool donor = Service.FindDefByKey(Service.OaDonorKey) != null;
            SetRow(Service.OaDisplayName, c ? 1 : 0, 1, false);
            SetRow(Service.OaDDisplayName, d ? 1 : 0, 1, false);
            SetRow(Service.OaEDisplayName, e ? 1 : 0, 1, false);
            SetRow("Aryx donor", donor ? 1 : 0, 1, !donor && _started);
            if (c && d && e)
                _status = "OA-27Variant loaded — Spectre / Anvil / Wraith";
            else if (donor)
                _status = "Donor found — cloning C/D/E…";
            else
                _status = "Waiting for Aryx OA-27 Cavalier…";
        }

        private static void ScanLoadedMods()
        {
            _modsScanned = true;
            try
            {
                Dictionary<string, BepInEx.PluginInfo> infos = Chainloader.PluginInfos;
                if (infos == null)
                {
                    _modsScanned = false;
                    return;
                }
                foreach (KeyValuePair<string, BepInEx.PluginInfo> kv in infos)
                {
                    BepInEx.PluginInfo info = kv.Value;
                    if (info == null || info.Metadata == null)
                        continue;
                    if (info.Metadata.GUID == PluginInfo.GUID)
                        continue;
                    string label = info.Metadata.Name;
                    if (string.IsNullOrEmpty(label))
                        label = info.Metadata.GUID;
                    if (info.Metadata.Version != null)
                        label = label + "  " + info.Metadata.Version.ToString();
                    SetRow(label, 1, 1, false);
                }
            }
            catch
            {
                _modsScanned = false;
            }
        }

        private static void SetRow(string name, int current, int total, bool failed)
        {
            if (string.IsNullOrEmpty(name))
                return;
            if (total < 1)
                total = 1;
            if (current < 0)
                current = 0;
            if (current > total)
                current = total;
            Row row = Find(name);
            if (row == null)
            {
                row = new Row();
                row.Name = name;
                Rows.Add(row);
            }
            row.Current = current;
            row.Total = total;
            row.Failed = failed;
        }

        private static void Paint()
        {
            EnsureStyles();
            int prevDepth = GUI.depth;
            GUI.depth = -1100;
            float left = 28f;
            float top = 28f;
            float nameW = 240f;
            float gap = 10f;
            float barW = 300f;
            float countW = 78f;
            float rowH = 22f;
            float panelW = nameW + gap + barW + gap + countW + 36f;
            int n = Rows.Count;
            if (n < 1)
                n = 1;
            float panelH = 64f + n * rowH + 18f;
            if (panelH > Screen.height - 40f)
                panelH = Screen.height - 40f;

            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.88f);
            GUI.DrawTexture(new Rect(left - 12f, top - 10f, panelW, panelH), _px);
            DrawRgbStrip(left - 12f, top - 10f, 3f, panelH, true);
            _header.normal.textColor = HueRgb(Time.unscaledTime * 0.35f);
            GUI.color = Color.white;

            GUI.Label(new Rect(left, top, panelW - 24f, 22f),
                "OA-27Variant  " + PluginInfo.Version, _header);
            string st = _status;
            if (string.IsNullOrEmpty(st))
                st = "…";
            GUI.Label(new Rect(left, top + 22f, panelW - 24f, 18f), st, _body);

            float y = top + 46f;
            float maxY = top - 10f + panelH - 10f;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (y + rowH > maxY)
                    break;
                DrawRow(Rows[i], left, y, nameW, gap, barW, countW, rowH);
                y += rowH;
            }
            GUI.color = prev;
            GUI.depth = prevDepth;
        }

        private static void DrawRow(Row row, float x, float y, float nameW, float gap,
            float barW, float countW, float rowH)
        {
            if (row == null)
                return;
            Color prev = GUI.color;
            if (row.Failed)
                GUI.color = new Color(1f, 0.35f, 0.28f, 0.95f);
            else
                GUI.color = HueRgb(Time.unscaledTime * 0.45f + y * 0.01f);
            GUI.Label(new Rect(x, y, nameW, rowH), row.Name, _body);
            GUI.color = prev;

            float bx = x + nameW + gap;
            float by = y + 8f;
            float bh = 10f;
            float fill = 0f;
            if (row.Total > 0)
                fill = (float)row.Current / (float)row.Total;
            if (fill < 0f)
                fill = 0f;
            if (fill > 1f)
                fill = 1f;
            DrawRgbBar(bx, by, barW, bh, fill, row.Failed);
            string c = row.Failed
                ? "FAIL"
                : (row.Current.ToString() + "/" + row.Total.ToString());
            GUI.Label(new Rect(bx + barW + gap, y, countW, rowH), c, _count);
        }

        private static Row Find(string name)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null && Rows[i].Name == name)
                    return Rows[i];
            }
            return null;
        }

        private static void EnsureStyles()
        {
            if (_styles)
                return;
            _styles = true;
            if (_px == null)
            {
                _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }
            _header = new GUIStyle(GUI.skin.label);
            _header.fontSize = 18;
            _header.fontStyle = FontStyle.Bold;
            _header.alignment = TextAnchor.MiddleLeft;
            _header.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label);
            _body.fontSize = 12;
            _body.alignment = TextAnchor.MiddleLeft;
            _body.clipping = TextClipping.Clip;
            _body.wordWrap = false;
            _body.normal.textColor = new Color(0.82f, 1f, 0.88f, 1f);
            _count = new GUIStyle(_body);
            _count.alignment = TextAnchor.MiddleRight;
            _count.fontSize = 12;
        }

        private static void DrawRgbBar(float x, float y, float w, float h, float fill, bool failed)
        {
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), _px);
            if (failed)
            {
                GUI.color = new Color(0.85f, 0.22f, 0.18f, 0.95f);
                GUI.DrawTexture(new Rect(x, y, w, h), _px);
                GUI.color = Color.white;
                return;
            }
            float t = Time.unscaledTime * 0.7f;
            if (fill < 0.001f)
            {
                float shown = 0.22f;
                float slide = Mathf.Repeat(t * 1.35f, 1.22f) - 0.11f;
                float sx = x + w * slide;
                float sw = w * shown;
                float right = x + w;
                if (sx < x)
                {
                    sw -= x - sx;
                    sx = x;
                }
                if (sx + sw > right)
                    sw = right - sx;
                DrawRgbSpan(sx, y, sw, h, t, false);
            }
            else
                DrawRgbSpan(x, y, w * fill, h, t, false);
            GUI.color = Color.white;
        }

        private static void DrawRgbStrip(float x, float y, float w, float h, bool vertical)
        {
            DrawRgbSpan(x, y, w, h, Time.unscaledTime * 0.55f, vertical);
        }

        private static void DrawRgbSpan(float x, float y, float w, float h, float t, bool vertical)
        {
            if (w < 0.5f || h < 0.5f)
                return;
            int slices = vertical ? 18 : 28;
            float step = vertical ? (h / slices) : (w / slices);
            for (int i = 0; i < slices; i++)
            {
                GUI.color = HueRgb(t - i * 0.045f);
                if (vertical)
                    GUI.DrawTexture(new Rect(x, y + i * step, w, step + 0.5f), _px);
                else
                    GUI.DrawTexture(new Rect(x + i * step, y, step + 0.5f, h), _px);
            }
        }

        private static Color HueRgb(float t)
        {
            float h = t - Mathf.Floor(t);
            if (h < 0f)
                h += 1f;
            return Color.HSVToRGB(h, 1f, 1f);
        }
    }
}
