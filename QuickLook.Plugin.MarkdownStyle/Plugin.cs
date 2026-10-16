using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private static string CssPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "pooi.moe", "QuickLook", "user-markdown.css");

        // Above the built-in viewer (0), so our CanHandle runs before its CanHandle returns true.
        public int Priority => 1;

        public void Init()
        {
            // Patching is lazy (first CanHandle): the target assembly must be loaded first.
        }

        public bool CanHandle(string path)
        {
            try
            {
                lock (Gate)
                {
                    if (_gaveUp) return false;
                    if (!_patched) TryPatch();
                    if (_patched) ReloadCssIfChanged();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
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
                if (asm == null) return; // not loaded (yet); retry on the next call

                var panel = asm.GetType(PanelTypeName, throwOnError: false);
                var field = panel?.GetField(ResourcesFieldName,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) { _gaveUp = true; return; }

                // Fill the dictionary before we touch it.
                RuntimeHelpers.RunClassConstructor(panel.TypeHandle);

                var res = field.GetValue(null) as Dictionary<string, byte[]>;
                if (res == null || !res.TryGetValue(TemplateKey, out var bytes)) { _gaveUp = true; return; }

                var html = Encoding.UTF8.GetString(bytes);
                if (html.IndexOf("user.css", StringComparison.Ordinal) < 0)
                {
                    var i = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                    if (i < 0) { _gaveUp = true; return; }
                    html = html.Insert(i, LinkTag);
                    res[TemplateKey] = new UTF8Encoding(false).GetBytes(html);
                }

                _resources = res;
                EnsureCssFile();
                _patched = true;
                ReloadCssIfChanged(force: true);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                _gaveUp = true;
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
