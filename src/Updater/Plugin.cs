using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace MistX.Updater
{
    [BepInPlugin(GUID, "MistX Updater", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "mistx.updater";
        private const string Owner = "fluffy803";
        private const string Repo  = "MistTrainGirlsX-Korean-Patch";
        private const string Branch = "dist";
        private static string Base => "https://raw.githubusercontent.com/" + Owner + "/" + Repo + "/" + Branch + "/";

        internal static ManualLogSource L;

        private void Awake()
        {
            L = Logger;
            var enabled = Config.Bind("General", "AutoUpdate", true,
                "게임 시작 시 GitHub 최신 번역을 확인해 바뀐 파일만 받아 둡니다(게임 재시작 시 반영). 끄려면 false.");
            if (!enabled.Value) { L.LogInfo("auto-update: disabled by config"); return; }
            var t = new Thread(Run) { IsBackground = true, Name = "MistXUpdater" };
            t.Start();
        }

        private static string Root => Paths.GameRootPath;
        private static string KoDir => Path.Combine(Paths.BepInExRootPath, "Translation", "ko");
        private static string VersionFile => Path.Combine(KoDir, ".patch_version");

        private static void Run()
        {
            try
            {
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; } catch { }
                ServicePointManager.Expect100Continue = false;

                string manifest = DownloadString(Base + "manifest.txt", 15000);
                if (manifest == null) { L.LogInfo("auto-update: manifest 조회 실패(오프라인/비공개 릴리스?) — 건너뜀"); return; }

                string remoteVer = null, reinstall = null;
                var files = new List<string[]>();
                foreach (var raw in manifest.Replace("\r", "").Split('\n'))
                {
                    var ln = raw.Trim();
                    if (ln.Length == 0 || ln[0] == '#') continue;
                    if (ln.StartsWith("version=")) remoteVer = ln.Substring(8).Trim();
                    else if (ln.StartsWith("reinstall=")) reinstall = ln.Substring(10).Trim();
                    else if (ln.StartsWith("file="))
                    {
                        var p = ln.Substring(5).Split('|');
                        if (p.Length == 3) files.Add(new[] { p[0].Trim(), p[1].Trim().ToLowerInvariant(), p[2].Trim() });
                    }
                }

                string localVer = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "";
                if (!string.IsNullOrEmpty(remoteVer) && remoteVer == localVer)
                { L.LogInfo("auto-update: 이미 최신 (v" + localVer + ")"); return; }
                L.LogInfo("auto-update: 확인 (로컬 v" + (localVer == "" ? "?" : localVer) + " -> 원격 v" + remoteVer + ")");

                int changed = 0, failed = 0;
                foreach (var f in files)
                {
                    string rel = f[0], wantHash = f[1], asset = f[2];
                    string target = Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
                    try
                    {
                        if (File.Exists(target) && Sha256File(target) == wantHash) continue;
                        byte[] data = DownloadBytes(Base + asset, 180000);
                        if (data == null) { failed++; L.LogWarning("auto-update: 다운로드 실패 " + asset); continue; }
                        if (!string.IsNullOrEmpty(wantHash) && Sha256Bytes(data) != wantHash)
                        { failed++; L.LogWarning("auto-update: 해시 불일치 " + asset + " — 건너뜀"); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        string tmp = target + ".tmp";
                        File.WriteAllBytes(tmp, data);
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(tmp, target);
                        changed++;
                        L.LogInfo("auto-update: 적용 " + rel + " (" + (data.Length / 1024) + "KB)");
                    }
                    catch (Exception fe) { failed++; L.LogWarning("auto-update: " + asset + " 처리 오류 " + fe.Message); }
                }

                if (failed == 0 && !string.IsNullOrEmpty(remoteVer)) File.WriteAllText(VersionFile, remoteVer);
                if (changed > 0)
                    L.LogMessage("★ 번역 업데이트 " + changed + "개 적용됨 (v" + remoteVer + "). 게임을 재시작하면 반영됩니다.");
                else if (failed == 0)
                    L.LogInfo("auto-update: 변경 파일 없음(버전만 갱신)");

                if (!string.IsNullOrEmpty(reinstall) && !string.IsNullOrEmpty(localVer) && CompareVer(localVer, reinstall) < 0)
                    L.LogMessage("★ 플러그인/폰트 대형 업데이트(v" + reinstall + "+)가 있습니다. 최신 패치 zip을 받아 재설치하세요.");
            }
            catch (Exception e) { L.LogWarning("auto-update: 건너뜀 (" + e.Message + ")"); }
        }

        private static int CompareVer(string a, string b)
        {
            string[] pa = a.Split('.'), pb = b.Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out var xi) ? xi : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out var yi) ? yi : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        private static HttpWebRequest Req(string url)
        {
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "MistX-Updater/1.0 (+github.com/" + Owner + "/" + Repo + ")";
            r.AllowAutoRedirect = true;
            r.KeepAlive = false;
            r.Accept = "*/*";
            return r;
        }

        private static string DownloadString(string url, int timeoutMs)
        {
            try
            {
                var r = Req(url); r.Timeout = timeoutMs; r.ReadWriteTimeout = timeoutMs;
                using (var resp = (HttpWebResponse)r.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                { s.CopyTo(ms); return Encoding.UTF8.GetString(ms.ToArray()); }
            }
            catch { return null; }
        }

        private static byte[] DownloadBytes(string url, int timeoutMs)
        {
            try
            {
                var r = Req(url); r.Timeout = 30000; r.ReadWriteTimeout = timeoutMs;
                using (var resp = (HttpWebResponse)r.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                { s.CopyTo(ms); return ms.ToArray(); }
            }
            catch { return null; }
        }

        private static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return ToHex(sha.ComputeHash(fs));
        }
        private static string Sha256Bytes(byte[] b)
        {
            using (var sha = SHA256.Create()) return ToHex(sha.ComputeHash(b));
        }
        private static string ToHex(byte[] h)
        {
            var sb = new StringBuilder(h.Length * 2);
            foreach (var x in h) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
