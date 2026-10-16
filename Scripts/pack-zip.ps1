# Run after a Release build: dotnet build -c Release
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "QuickLook.Plugin.MarkdownStyle.qlplugin"
$zip = Join-Path $root "QuickLook.Plugin.MarkdownStyle.zip"

Remove-Item $out, $zip -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path (Join-Path $root "bin\Release") -Exclude *.pdb,*.xml
Compress-Archive $files $zip
Move-Item $zip $out
