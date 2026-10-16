// QuickLook.Plugin.StyledMD: restyles QuickLook's Markdown preview.
// Copyright (C) 2026 No3371
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using QuickLook.Common.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.StyledMD
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
            catch (Exception)
            {
                // never break the built-in viewer
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
                if (res == null) { _gaveUp = true; return; }
                if (!res.TryGetValue(TemplateKey, out var bytes))
                {
                    _gaveUp = true;
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
                    if (i < 0) { _gaveUp = true; return; }

                    html = html.Insert(i, "\n" + LinkTag);
                    res[TemplateKey] = new UTF8Encoding(false).GetBytes(html);
                }

                ForceLightTheme(res);

                _resources = res;
                EnsureCssFile();
                _patched = true;
                ReloadCssIfChanged(force: true);
            }
            catch (Exception)
            {
                _gaveUp = true;
            }
        }

        // GitHub's CSS and the template pick a dark theme through "prefers-color-scheme". Make the dark
        // queries never match and the light ones always match, so the page stays light on a dark system.
        private static void ForceLightTheme(Dictionary<string, byte[]> res)
        {
            foreach (var key in res.Keys.ToList())
            {
                if (key != TemplateKey && !key.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) continue;

                var text = Encoding.UTF8.GetString(res[key]);
                var patched = Regex.Replace(text, @"prefers-color-scheme:\s*dark", "min-width: 99999px");
                patched = Regex.Replace(patched, @"prefers-color-scheme:\s*light", "min-width: 0px");
                if (patched == text) continue;

                res[key] = new UTF8Encoding(false).GetBytes(patched);
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
        }
    }
}
