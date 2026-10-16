using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

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

                _resources = res;
                EnsureCssFile();
                _patched = true;
                ReloadCssIfChanged(force: true);
                Log("css loaded: " + res[CssKey].Length + " bytes");
            }
            catch (Exception e)
            {
                GiveUp("TryPatch error: " + e);
            }
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

        private static void EnsureCssFile()
        {
            var path = CssPath;
            if (File.Exists(path)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var s = typeof(Plugin).Assembly.GetManifestResourceStream("default-markdown.css"))
            using (var f = File.Create(path))
            {
                s?.CopyTo(f);
            }
        }

        private static void ReloadCssIfChanged(bool force = false)
        {
            var path = CssPath;
            if (!File.Exists(path)) return;

            var stamp = File.GetLastWriteTimeUtc(path);
            if (!force && stamp == _cssStamp) return;

            _resources[CssKey] = File.ReadAllBytes(path);
            _cssStamp = stamp;
        }
    }
}
