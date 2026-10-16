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
Target: `net462`. Check out `QuickLook.Common` beside this folder (as `QuickLook.Plugin.HelloWorld` does).
Put `QuickLook.Plugin.MarkdownStyle.dll` in a `QuickLook.Plugin.MarkdownStyle` folder under `QuickLook.Plugin`.

## Not tested
Written on Linux with no .NET toolchain: it was never compiled or run. Test on Windows against your QuickLook version.
It relies on private names: `MarkdownPanel._resources`, the key `/md2html.html`, and a literal `</head>`.
