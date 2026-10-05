using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Mist.Api;
using Mist.Scenario;

namespace MistX.StoryKO
{
    [BepInPlugin(GUID, "MistX Story KO", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "mistx.storyko";
        internal static ManualLogSource Log;
        internal static readonly Dictionary<string, string> Dict = new Dictionary<string, string>();

        private void Awake()
        {
            Log = Logger;
            LoadDicts();

            var h = new Harmony(GUID);
            h.PatchAll(typeof(GetPhraseDataPatch));
            h.PatchAll(typeof(PhraseColorPatch));
            h.PatchAll(typeof(PhraseShowPatch));
            h.PatchAll(typeof(PhraseFontSizePatch));

            Log.LogInfo($"StoryKO ready: {Dict.Count} phrase translations loaded.");
        }

        internal static TMPro.TMP_FontAsset KrTmp;
        internal static Font KrFont;
        internal static byte[] KrTtf;
        internal static bool KrLoadActive;
        internal static bool EnsureKr()
        {
            if (KrTmp != null) return true;
            try
            {
                if (KrTtf == null)
                {
                    string p1 = Path.Combine(Paths.BepInExRootPath, "Translation", "ko", "MistX_KR.ttf");
                    string p2 = @"C:\Windows\Fonts\MistX_KR.ttf";
                    string path = File.Exists(p1) ? p1 : (File.Exists(p2) ? p2 : null);
                    if (path == null) { Log.LogWarning("TMPFallback: MistX_KR.ttf not found"); return false; }
                    KrTtf = File.ReadAllBytes(path);
                }
                KrLoadActive = true;
                try
                {
                    KrFont = Font.CreateDynamicFontFromOSFont("MistX KR", 90);
                    var kr = TMPro.TMP_FontAsset.CreateFontAsset(KrFont, 90, 9,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                        TMPro.AtlasPopulationMode.Dynamic, true);
                    if (kr == null) { Log.LogWarning("TMPFallback: CreateFontAsset null"); return false; }
                    kr.name = "MistX KR (TMP FB)";
                    UnityEngine.Object.DontDestroyOnLoad(kr);
                    UnityEngine.Object.DontDestroyOnLoad(KrFont);
                    KrTmp = kr;

                    string cf = Path.Combine(Paths.BepInExRootPath, "Translation", "ko", "ko_charset.txt");
                    if (File.Exists(cf))
                    {
                        var raw = File.ReadAllText(cf, Encoding.UTF8);
                        var sb = new StringBuilder(raw.Length);
                        var seen = new System.Collections.Generic.HashSet<char>();
                        foreach (var ch in raw) { if (ch == '\n' || ch == '\r' || ch == '\t' || ch == ' ') continue; if (seen.Add(ch)) sb.Append(ch); }
                        string charset = sb.ToString();
                        string missing;
                        KrTmp.TryAddCharacters(charset, out missing, false);
                        Log.LogInfo("TMPFallback: pre-rasterized " + charset.Length + " chars, missing=" + (missing == null ? 0 : missing.Length));
                    }
                    else Log.LogWarning("TMPFallback: ko_charset.txt 없음 — KO 일부 두부 가능(크래시는 아님)");

                    KrTmp.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
                }
                finally { KrLoadActive = false; }
                Log.LogInfo("TMPFallback: KR asset ready (static)");
            }
            catch (Exception e) { KrLoadActive = false; Log.LogWarning("TMPFallback create err: " + e.Message); return false; }
            return KrTmp != null;
        }

        private void LoadDicts()
        {
            try
            {

                string dir = Path.Combine(Paths.BepInExRootPath, "Translation", "ko", "Story");
                if (!Directory.Exists(dir)) { Log.LogWarning("Story dir not found: " + dir); return; }
                foreach (var f in Directory.GetFiles(dir, "*.b64"))
                {
                    int n = 0;
                    foreach (var line in File.ReadAllLines(f, Encoding.ASCII))
                    {
                        if (line.Length == 0) continue;
                        int t = line.IndexOf('\t');
                        if (t <= 0) continue;
                        try
                        {
                            string jp = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, t)));
                            string ko = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(t + 1)));
                            if (!string.IsNullOrEmpty(jp)) { Dict[jp] = ko; n++; }
                        }
                        catch { }
                    }
                    Log.LogInfo($"  loaded {n} from {Path.GetFileName(f)}");
                }
            }
            catch (Exception e) { Log.LogError("LoadDicts failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(ScenarioSceneBase), "GetPhraseData")]
    internal static class GetPhraseDataPatch
    {
        private static int _hits;
        private static void Prefix(MSceneDetailViewModel sceneDetail)
        {
            if (sceneDetail == null) return;
            var p = sceneDetail.Phrase;
            if (!string.IsNullOrEmpty(p) && Plugin.Dict.TryGetValue(p, out var ko))
            {
                sceneDetail.Phrase = ko;
                if (_hits < 3) { _hits++; Plugin.Log.LogInfo("Phrase translated -> KO (hit " + _hits + ")"); }
            }
        }
    }

    internal static class PhraseStyle
    {
        internal static float LineSpacing = 1.15f;
        internal static float OutlineDist = 1.0f;
        private static bool _logged;
        internal static void Apply(ScenarioPhraseData inst)
        {
            if (inst == null) return;
            var tr = Traverse.Create(inst);
            var lbl = tr.Field("_phraseLabel").GetValue() as Text;
            var name = tr.Field("_characterNameLabel").GetValue() as Text;
            var outline = tr.Field("_outline").GetValue() as Outline;
            if (lbl != null) { lbl.color = Color.white; lbl.lineSpacing = LineSpacing; EnsureOutline(lbl); }
            if (name != null) { name.color = Color.white; EnsureOutline(name); }
            if (outline != null) { outline.enabled = true; outline.effectColor = Color.black; outline.effectDistance = new Vector2(OutlineDist, -OutlineDist); outline.useGraphicAlpha = true; }
            if (!_logged) { _logged = true; Plugin.Log.LogInfo("PhraseStyle applied (Show+ColorChange hooks, white+outline, lineSpacing=" + LineSpacing + ")"); }
        }
        private static void EnsureOutline(Text t)
        {
            var ol = t.GetComponent<Outline>();
            if (ol == null) ol = t.gameObject.AddComponent<Outline>();
            ol.enabled = true; ol.effectColor = Color.black;
            ol.effectDistance = new Vector2(OutlineDist, -OutlineDist); ol.useGraphicAlpha = true;
        }
    }

    [HarmonyPatch(typeof(ScenarioPhraseData), "ChangePhraseLabelColor")]
    internal static class PhraseColorPatch
    { private static void Postfix(ScenarioPhraseData __instance) => PhraseStyle.Apply(__instance); }

    [HarmonyPatch(typeof(ScenarioPhraseData), "Show")]
    internal static class PhraseShowPatch
    { private static void Postfix(ScenarioPhraseData __instance) => PhraseStyle.Apply(__instance); }

    [HarmonyPatch(typeof(ScenarioPhraseData), "ChangeFontSize")]
    internal static class PhraseFontSizePatch
    { private static void Postfix(ScenarioPhraseData __instance) => PhraseStyle.Apply(__instance); }

    [HarmonyPatch]
    internal static class FontEngineLoadFacePatch
    {
        private static System.Reflection.MethodBase TargetMethod()
            => HarmonyLib.AccessTools.Method(typeof(UnityEngine.TextCore.LowLevel.FontEngine), "LoadFontFace",
                new[] { typeof(Font), typeof(int) });
        private static bool Prefix(Font font, int pointSize, ref UnityEngine.TextCore.LowLevel.FontEngineError __result)
        {

            if (Plugin.KrLoadActive && Plugin.KrTtf != null && font != null && ReferenceEquals(font, Plugin.KrFont))
            {
                __result = UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(Plugin.KrTtf, pointSize);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class TmpOnEnablePatch
    {
        private static bool _init, _errLogged;
        private static System.Reflection.MethodBase TargetMethod()
            => HarmonyLib.AccessTools.Method(typeof(TMPro.TMP_Text), "OnEnable")
               ?? HarmonyLib.AccessTools.Method(typeof(TMPro.TextMeshProUGUI), "OnEnable");
        private static void Postfix(TMPro.TMP_Text __instance)
        {
            try
            {
                if (!_init)
                {
                    _init = true;
                    if (Plugin.EnsureKr())
                    {
                        var gfb = TMPro.TMP_Settings.fallbackFontAssets;
                        if (gfb != null && !gfb.Contains(Plugin.KrTmp)) gfb.Add(Plugin.KrTmp);
                        Plugin.Log.LogInfo("TMPFallback: global fallback set");
                    }
                }
                if (Plugin.KrTmp == null || __instance == null) return;
                var fa = __instance.font;
                if (fa != null && fa != Plugin.KrTmp)
                {
                    if (fa.fallbackFontAssetTable == null)
                        fa.fallbackFontAssetTable = new System.Collections.Generic.List<TMPro.TMP_FontAsset>();
                    if (!fa.fallbackFontAssetTable.Contains(Plugin.KrTmp))
                        fa.fallbackFontAssetTable.Add(Plugin.KrTmp);
                }

            }
            catch (Exception e) { if (!_errLogged) { _errLogged = true; Plugin.Log.LogWarning("TMPFallback OnEnable err: " + e.Message); } }
        }
    }
}
