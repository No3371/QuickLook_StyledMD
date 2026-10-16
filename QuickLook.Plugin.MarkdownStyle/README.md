# QuickLook.Plugin.MarkdownStyle

Restyles QuickLook's built-in Markdown preview with your own CSS. It handles no file itself.

## How it works
On the first `CanHandle` call, the plugin finds the loaded `QuickLook.Plugin.MarkdownViewer`
assembly and runs the static constructor of `MarkdownPanel`. It reflects the static
`_resources` dictionary and does two things:

1. Inserts `<link rel="stylesheet" href="user.css" />` before `</head>` in `/md2html.html`.
2. Stores `%APPDATA%\pooi.moe\QuickLook\user-markdown.css` under `/user.css`.

The built-in viewer already serves any key in `_resources`. The plugin re-reads the CSS
file when its timestamp changes. Any failure is caught and leaves the built-in viewer unchanged.

If the CSS file is missing, the plugin writes `Resources/default-markdown.css` there.

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
