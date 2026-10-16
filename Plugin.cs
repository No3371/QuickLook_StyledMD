using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.MarkdownStyle
{
    /// <summary>
    /// Handles no file. Patches the built-in MarkdownViewer's in-memory resources so its
    /// template links a user CSS file. Any failure leaves the built-in viewer untouched.
    /// </summary>
    public class Plugin : IViewer
    {
        private const string ViewerAssemblyName = "QuickLook.Plugin.MarkdownViewer";
        private const string PanelTypeName = "QuickLook.Plugin.MarkdownViewer.MarkdownPanel";
        private const string ResourcesFieldName = "_resources";
        private const string TemplateKey = "/md2html.html";
        private const string CssKey = "/user.css";
        private const string LinkTag = "<link rel=\"stylesheet\" href=\"user.css\" />\n";

        private static readonly object Gate = new object();
        private static Dictionary<string, byte[]> _resources;
        private static bool _patched;
        private static bool _gaveUp;
        private static DateTime _cssStamp = DateTime.MinValue;

        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "pooi.moe", "QuickLook");

        private static string CssPath => Path.Combine(DataDir, "user-markdown.css");
        private static string LogPath => Path.Combine(DataDir, "markdownstyle.log");

        private static int _logLines;

        // Above the built-in viewer (0), so our CanHandle runs before its CanHandle returns true.
        public int Priority => 1;

        public void Init()
        {
            // Patching is lazy (first CanHandle): the target assembly must be loaded first.
            Log("Init " + typeof(Plugin).Assembly.Location);
        }

        public bool CanHandle(string path)
        {
            try
            {
                lock (Gate)
                {
                    if (_logLines < 50)
                        Log("CanHandle " + Path.GetFileName(path) + " patched=" + _patched + " gaveUp=" + _gaveUp);
                    if (_gaveUp) return false;
                    if (!_patched) TryPatch();
                    if (_patched) ReloadCssIfChanged();
                }
            }
            catch (Exception e)
            {
                Log("CanHandle error: " + e);
            }

            return false;
        }

        public void Prepare(string path, ContextObject context)
        {
        }

        public void View(string path, ContextObject context)
        {
        }

        public void Cleanup()
        {
        }

        private static void TryPatch()
        {
            try
            {
                // Plugins are loaded with Assembly.LoadFrom, so look in the AppDomain first.
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == ViewerAssemblyName);
                if (asm == null) { Log("viewer assembly not loaded (will retry)"); return; }
                Log("viewer assembly " + asm.GetName().Version + " at " + asm.Location);

                var panel = asm.GetType(PanelTypeName, throwOnError: false);
                var field = panel?.GetField(ResourcesFieldName,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) { GiveUp("type or field not found: " + PanelTypeName + "." + ResourcesFieldName); return; }

                // Fill the dictionary before we touch it.
                RuntimeHelpers.RunClassConstructor(panel.TypeHandle);

                var res = field.GetValue(null) as Dictionary<string, byte[]>;
                if (res == null) { GiveUp("_resources is not a Dictionary<string, byte[]>"); return; }
                if (!res.TryGetValue(TemplateKey, out var bytes))
                {
                    GiveUp(TemplateKey + " not in _resources; keys: " + string.Join(", ", res.Keys.Take(30)));
                    return;
                }

                var html = Encoding.UTF8.GetString(bytes);
                if (html.IndexOf("user.css", StringComparison.Ordinal) < 0)
                {
                    // github-markdown.css is linked inside <body>, after </head>. Our sheet must come after it.
                    var i = -1;
                    var m = html.IndexOf("github-markdown.css", StringComparison.Ordinal);
                    if (m >= 0)
                    {
                        var close = html.IndexOf('>', m);
                        if (close >= 0) i = close + 1;
                    }
                    if (i < 0) i = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (i < 0) { GiveUp("no insertion point in template"); return; }

                    html = html.Insert(i, "\n" + LinkTag);
                    res[TemplateKey] = new UTF8Encoding(false).GetBytes(html);
                    Log("template patched at offset " + i);
                }

                ForceLightTheme(res);

                _resources = res;
                EnsureCssFile();
                _patched = true;
                ReloadCssIfChanged(force: true);
                
            }
            catch (Exception e)
            {
                GiveUp("TryPatch error: " + e);
            }
        }

        // GitHub's CSS and the template pick a dark theme through "prefers-color-scheme". Make the dark
        // queries never match and the light ones always match, so the page stays light on a dark system.
        private static void ForceLightTheme(Dictionary<string, byte[]> res)
        {
            var changed = 0;
            foreach (var key in res.Keys.ToList())
            {
                if (key != TemplateKey && !key.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) continue;

                var text = Encoding.UTF8.GetString(res[key]);
                var patched = Regex.Replace(text, @"prefers-color-scheme:\s*dark", "min-width: 99999px");
                patched = Regex.Replace(patched, @"prefers-color-scheme:\s*light", "min-width: 0px");
                if (patched == text) continue;

                res[key] = new UTF8Encoding(false).GetBytes(patched);
                changed++;
            }

            Log("forced light theme in " + changed + " resources");
        }

        private static void GiveUp(string reason)
        {
            _gaveUp = true;
            Log("GAVE UP: " + reason);
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                if (_logLines == 0 && File.Exists(LogPath) && new FileInfo(LogPath).Length > 100 * 1024)
                    File.Delete(LogPath);
                _logLines++;
                File.AppendAllText(LogPath, DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch
            {
                // logging must never break the viewer
            }
        }

        private const string LegacyMarker = "/* QuickLook Markdown style (user file).";
        private const string UserTemplate =
            "/* QuickLook Markdown style: your overrides.\r\n" +
            "   The built-in default style loads first, then this file. Put only what you want to change here.\r\n" +
            "   Changes load on the next preview. Example:\r\n" +
            "     #preview { max-width: 900px; }\r\n" +
            "*/\r\n";

        private static void EnsureCssFile()
        {
            var path = CssPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Older versions copied the whole default into this file, which then never got updates.
            if (File.Exists(path))
            {
                string head;
                using (var r = new StreamReader(path, Encoding.UTF8))
                    head = r.ReadLine() ?? "";
                if (head.StartsWith(LegacyMarker, StringComparison.Ordinal))
                {
                    var old = path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                    Log("moved old full copy to " + old);
                }
            }

            if (!File.Exists(path))
                File.WriteAllText(path, UserTemplate, new UTF8Encoding(false));
        }

        // Bebas Neue (SIL OFL 1.1), Latin subset. A font installed on the PC is used first.
        private static string BuildFontFaceCss()
        {
            using (var s = typeof(Plugin).Assembly.GetManifestResourceStream("bebas-neue-latin-400.woff2"))
            {
                if (s == null) return "";
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return "@font-face{font-family:\"Bebas Neue\";font-style:normal;font-weight:400;" +
                           "src:local(\"Bebas Neue\"),url(data:font/woff2;base64," + Convert.ToBase64String(ms.ToArray()) + ") format(\"woff2\");" +
                           "unicode-range:U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329," +
                           "U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD}\n";
                }
            }
        }

        private static byte[] ReadDefaultCss()
        {
            using (var s = typeof(Plugin).Assembly.GetManifestResourceStream("default-markdown.css"))
            using (var ms = new MemoryStream())
            {
                s?.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static void ReloadCssIfChanged(bool force = false)
        {
            var path = CssPath;
            var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (!force && stamp == _cssStamp) return;

            // Embedded font, then the built-in default, then the user's file, so the user's rules win.
            var css = new List<byte>(Encoding.UTF8.GetBytes(BuildFontFaceCss()));
            css.AddRange(ReadDefaultCss());
            css.AddRange(Encoding.UTF8.GetBytes("\n"));
            if (File.Exists(path))
                css.AddRange(File.ReadAllBytes(path));

            _resources[CssKey] = css.ToArray();
            _cssStamp = stamp;
            Log("css built: " + css.Count + " bytes");
        }
    }
}
