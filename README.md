# StyledMD

A QuickLook plugin (`QuickLook.Plugin.StyledMD`).

A small plugin that gives QuickLook's built-in Markdown preview a calmer, more readable style:
a soft sage page, soft black text, a wide reading column, and a steep step between small body text
and large headings.

It does not open or render any file. QuickLook's own Markdown viewer still does all of that.
This plugin only adds a style sheet to it.

> **Status:** not an official QuickLook plugin, and not affiliated with QuickLook or Typesafe.
> See [Credits](#credits) and [Limits](#limits).

## What you get

- **Light only.** The page stays light when Windows is in dark mode.
- **Headings.** h1 and h2 use Bebas Neue (capitals only). h3 is Roboto Bold. Body text is 17px, weight 400.
- **Body text.** Roboto first, then Noto Sans. Characters those fonts lack (for example Chinese) use the next installed font.
- **Code.** Weight 400 with a thin stroke, so it does not look faint.
- **Tables, quotes, links.** Rounded table border, an accent bar on quotes, a pink link underline.
- **Your own CSS.** One file of overrides that loads after the built-in style.

## Names

The repository is StyledMD and the plugin is `QuickLook.Plugin.StyledMD`. QuickLook only loads files named `QuickLook.Plugin.*.dll`,
so the plugin name has to start with `QuickLook.Plugin.`.

**Upgrading from the earlier name `QuickLook.Plugin.MarkdownStyle`:** quit QuickLook and delete the folder
`%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.MarkdownStyle\` before you install this one. Two copies would both patch the viewer.
Your `user-markdown.css` is kept. You can delete the old `markdownstyle.log`, which the earlier name wrote.

## Requirements

| Item | Needs |
|---|---|
| OS | Windows |
| QuickLook | A version whose Markdown viewer has `MarkdownPanel._resources` |

Checked in the source of QuickLook 4.5.0 and master, and seen working on a real Windows install.
**QuickLook 3.6.4 does not work.** Its Markdown viewer has no `MarkdownPanel`, so the plugin does nothing there. Upgrade QuickLook first.

## Install

1. Get `QuickLook.Plugin.StyledMD.qlplugin` from the latest build or release.
   (Builds: the **Actions** tab, then the newest green run, then **Artifacts**.)
2. A `.qlplugin` file is a zip. Unzip it into
   `%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.StyledMD\`
   so that `QuickLook.Plugin.StyledMD.dll` sits directly in that folder.
3. Restart QuickLook (quit it from the tray icon, then start it again).
4. Select a `.md` file and press Space.

To remove it, quit QuickLook and delete that folder. Optionally delete the files listed in
[Files the plugin writes](#files-the-plugin-writes).

## Change the style

Open `%APPDATA%\pooi.moe\QuickLook\user-markdown.css`. The plugin creates it on first run.
Put only the rules you want to change in it. It loads after the built-in style, so your rules win,
and it is re-read on the next preview. If a change does not show, restart QuickLook.

```css
/* wider column */
#preview { max-width: 900px; }

/* your own colors */
:root {
  --ts-bg: #f4f1ea;
  --ts-accent: #2a7de1;
}
```

Useful variables: `--ts-bg`, `--ts-text`, `--ts-text-soft`, `--ts-code-bg`, `--ts-rule`, `--ts-th-bg`,
`--ts-accent`, `--ts-sans`, `--ts-mono`, `--ts-display`.
Useful selectors: `body.markdown-body`, `#preview` (the rendered document), `#toc` (the contents sidebar).

The built-in style is replaced on every plugin update. Keep your changes in `user-markdown.css`.
A file from an older version that holds a full copy of the style is renamed to `user-markdown.css.old`.

## If nothing changes

The plugin writes no log and shows no message. If it cannot patch the viewer, it does nothing, and QuickLook shows its normal Markdown preview.
Check these in order:

1. **Install folder.** `QuickLook.Plugin.StyledMD.dll` must sit directly in `%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.StyledMD\`. Restart QuickLook after you copy it.
2. **Old copy.** Delete the folder of the earlier name `QuickLook.Plugin.MarkdownStyle` (see [Names](#names)).
3. **QuickLook version.** See [Requirements](#requirements). A QuickLook update can also break the plugin (see [Limits](#limits)).
4. **Your overrides.** Rename `user-markdown.css` and preview again. A wrong rule in it can hide the built-in style.

## Privacy and safety

- The plugin makes no network requests.
- It changes only QuickLook's in-memory copy of the viewer's template and style sheets. It does not edit any QuickLook file.
- It writes no log and records nothing about the files you preview.

## Files the plugin writes

All in `%APPDATA%\pooi.moe\QuickLook\`:

- `user-markdown.css`: your overrides.
- `user-markdown.css.old`: only after migrating from an older version.

## How it works

1. QuickLook loads the plugin. On the first `CanHandle` call, the plugin finds the loaded
   `QuickLook.Plugin.MarkdownViewer` assembly and reads the private static `MarkdownPanel._resources` dictionary.
   The plugin has priority 1 and always answers "cannot handle", so the built-in viewer opens every file.
2. It inserts `<link rel="stylesheet" href="user.css" />` into `/md2html.html`, after the `github-markdown.css` link
   (that link is inside `<body>`, so a link in `<head>` would load too early).
3. It rewrites the `prefers-color-scheme` queries in the viewer's template and style sheets so dark mode never applies.
4. It stores `/user.css` in the same dictionary: the embedded Bebas Neue font, the built-in style, then your file.
   The viewer already serves every key of that dictionary to its web view.

## Limits

- **It relies on private names:** `MarkdownPanel._resources`, the key `/md2html.html`, and the order of links in the template.
  A QuickLook update can change them and the plugin then does nothing. Test after each QuickLook update.
- Mermaid previews use the same template, so they get the style. AsciiDoc, reStructuredText and Jupyter previews use other templates and are not styled.
- Bebas Neue is capitals only and covers Latin characters. Other scripts fall back to your other fonts.
- Roboto and Noto Sans are not bundled. Without them on your PC, headings and body text fall back to Segoe UI or another system font.

## Build

Needs Windows with the .NET SDK or Visual Studio 2022. The project targets `net462` with WPF, so it does not build on Linux or macOS.

```powershell
git clone --recurse-submodules <this repository>
cd StyledMD
dotnet build -c Release
powershell Scripts\pack-zip.ps1     # creates QuickLook.Plugin.StyledMD.qlplugin
```

`QuickLook.Common` is a git submodule. `Directory.Build.props` and `Scripts\update-version.ps1` exist only because
`QuickLook.Common` runs a pre-build script that expects a solution folder.

The GitHub Actions workflow `build-markdown-style.yml` does the same on a Windows runner and uploads the `.qlplugin` as an artifact.
Push a tag that starts with `v` (for example `v0.2.0`), or run the workflow by hand with a tag, to also create a release.

## Reporting problems

Please report problems with this plugin here, not to QuickLook. Include:

- your QuickLook version,
- what you did and what you expected, with the file you previewed if it is not private,
- a screenshot, if the problem is how the page looks.

## Credits

Thank you to:

- **QuickLook** and its contributors (QL-Win), for the viewer, the plugin interface and the `QuickLook.Plugin.HelloWorld` starting point this project follows. The QuickLook source files carry GPL-3.0-or-later notices.
- **Typesafe** (typesafe.ai), whose blog gave the look: the palette, the table and inline-code values, and the idea of a steep size and weight scale.
  This project is not affiliated with or endorsed by Typesafe. It copies no stylesheet, image or font from them.
  Their font, Die Grotesk C, is commercial and is never bundled. It is used only if you have it installed.
- **Bebas Neue** by the Bebas Neue Project Authors (Dharma Type), embedded under the SIL Open Font License 1.1.
  The license text is in `Resources/fonts/BebasNeue-OFL-LICENSE.txt`.
- **Roboto** and **Noto Sans**, named in the font list and used only if installed.
- **GitHub's Markdown CSS**, which QuickLook's viewer uses as its base style. This plugin only adds rules on top of it.
- Built with the help of **Claude Code**.

## License

Copyright (C) 2026 No3371.

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
or (at your option) any later version. It is distributed without any warranty. See [LICENSE](LICENSE).

GPL-3.0-or-later fits the QuickLook source files this plugin links to, which carry that notice.
(The `QuickLook.Common` repository also contains a `LICENSE` file that says MIT. If this matters for your use, ask its authors.)

Bebas Neue stays under its own license, the SIL Open Font License 1.1, in `Resources/fonts/`.
