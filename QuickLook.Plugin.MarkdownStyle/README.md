# QuickLook.Plugin.MarkdownStyle

Restyles QuickLook's built-in Markdown preview with your own CSS. It handles no file itself.

## How it works
On the first `CanHandle` call, the plugin finds the loaded `QuickLook.Plugin.MarkdownViewer`
assembly and runs the static constructor of `MarkdownPanel`. It reflects the static
`_resources` dictionary and does two things:

1. Inserts `<link rel="stylesheet" href="user.css" />` before `</head>` in `/md2html.html`.
2. Stores the built-in default style (embedded in the DLL), followed by `%APPDATA%\pooi.moe\QuickLook\user-markdown.css`, under `/user.css`.

The built-in viewer already serves any key in `_resources`. The plugin re-reads the CSS
file when its timestamp changes. Any failure is caught and leaves the built-in viewer unchanged.

The default style updates with the plugin. `user-markdown.css` holds only your overrides and loads last.
If the file is missing, the plugin writes an empty template. A file from an older version that holds a full copy of the default is renamed to `user-markdown.css.old`.

## Build
Same layout as `QuickLook.Plugin.HelloWorld`. Needs Windows with the .NET SDK or Visual Studio 2022.

```powershell
git submodule update --init            # fetches QuickLook.Common
dotnet build -c Release                # output: bin\Release\
powershell Scripts\pack-zip.ps1        # output: QuickLook.Plugin.MarkdownStyle.qlplugin
```

A `.qlplugin` file is a zip of `bin\Release\` (without `*.pdb` and `*.xml`) with the extension renamed.
Install (not verified): open the `.qlplugin` file with QuickLook, or unzip it into a `QuickLook.Plugin\QuickLook.Plugin.MarkdownStyle` folder.

## Not tested
Written on Linux with no .NET toolchain: it was never compiled or run. Test on Windows against your QuickLook version.
It relies on private names: `MarkdownPanel._resources`, the key `/md2html.html`, and a literal `</head>`.

## Debugging
The plugin writes `%APPDATA%\pooi.moe\QuickLook\markdownstyle.log`. If the file does not exist after you preview
a file, QuickLook did not load the plugin. If it ends with `GAVE UP: ...`, the line says which assumption failed.

## Requirements
Needs a QuickLook whose Markdown viewer has `MarkdownPanel._resources`. Checked in source: QuickLook 4.5.0 and master.
QuickLook 3.6.4 does not have it (it loads the template from a .NET resource and calls `WebpagePanel.LoadHtml`),
so the log shows `GAVE UP: type or field not found`.

## Fonts and theme
- h1 and h2 use Bebas Neue, embedded in the DLL as a Latin-subset WOFF2 (SIL OFL 1.1, license in `Resources/fonts/`). An installed Bebas Neue is used first.
- The page is light only. The plugin rewrites `prefers-color-scheme` queries in the viewer's CSS and template in memory, so a dark system theme has no effect.
