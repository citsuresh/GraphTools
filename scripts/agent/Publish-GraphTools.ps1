<#
.SYNOPSIS
Builds and copies a portable Windows x64 GraphTools bundle beside this script.
.DESCRIPTION
Requires the GraphTools source checkout and a compatible .NET SDK.
Re-running refreshes files in GraphTools-win-x64 without adding a timestamp.
Existing files are overwritten; unrelated files in that folder are not removed.
Runs Builder and Query smoke checks before reporting success.
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Publish-GraphTools.ps1
.EXAMPLE
.\Publish-GraphTools.ps1 -SourceRoot 'D:\Repos\GraphTools'
#>
[CmdletBinding()]
param(
    [string]$SourceRoot = 'C:\MyFiles\Git\GraphTools',
    [string]$OutputRoot = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
$bundle = Join-Path $OutputRoot 'GraphTools-win-x64'

Get-Command dotnet -CommandType Application -ErrorAction Stop | Out-Null
foreach ($relativePath in @(
    'GraphTools.slnx',
    'GraphTools.Builder\GraphTools.Builder.csproj',
    'GraphTools.Query\GraphTools.Query.csproj',
    'docs\graph-viewer.html',
    'README.md'
)) {
    $path = Join-Path $SourceRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required source file not found: $path"
    }
}

New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($tool in @('Builder', 'Query')) {
    $project = Join-Path $SourceRoot "GraphTools.$tool\GraphTools.$tool.csproj"
    $output = Join-Path $bundle $tool
    & dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
    if ($LASTEXITCODE -ne 0) {
        throw "GraphTools.$tool publish failed (exit code $LASTEXITCODE). Bundle may be incomplete."
    }
    foreach ($artifact in @("GraphTools.$tool.exe", 'coreclr.dll', 'BuildHost-netcore')) {
        if (-not (Test-Path -LiteralPath (Join-Path $output $artifact))) {
            throw "Missing published artifact in ${output}: $artifact"
        }
    }
}

Copy-Item -LiteralPath (Join-Path $SourceRoot 'docs\graph-viewer.html') -Destination (Join-Path $bundle 'graph-viewer.html') -Force
Copy-Item -LiteralPath (Join-Path $SourceRoot 'README.md') -Destination (Join-Path $bundle 'README.md') -Force

$instructions = @'
GraphTools - Windows x64 portable Release bundle

Copy this entire folder to the other machine. Keep all files and subfolders
inside Builder and Query together, including the BuildHost folders.
No GraphTools source checkout or build is required.

Destination requirements:
- Windows x64 and a compatible .NET SDK installed machine-wide.
- Both tools initialize MSBuildLocator at startup, including Query/diff mode.
- Builder needs the analyzed solution's SDKs, targeting packs and workloads.
  Restore that solution's dependencies before analyzing it.
- The .NET 8 application runtime is bundled; no separate runtime is needed.

Run from PowerShell in this folder:

Build a graph:
  .\Builder\GraphTools.Builder.exe --solution "C:\Repo\App.slnx" --output "C:\Repo\docs\full-graph.json" --mode full

List symbols:
  .\Query\GraphTools.Query.exe --graph "C:\Repo\docs\full-graph.json" --list-symbols --project "MyProject"

Query callers:
  .\Query\GraphTools.Query.exe --graph "C:\Repo\docs\full-graph.json" --symbol "MyNamespace.MyClass.MyMethod()" --direction callers

Open graph-viewer.html in Edge/Chrome and select the generated graph JSON files.
The viewer uses D3.js from a CDN, requiring network access unless cached.

README.md describes additional commands. Use the portable paths above instead
of its source-build paths. The Debug-path wrapper script is not required.
No repository source files or project graph data are included by this script.
'@
Set-Content -LiteralPath (Join-Path $bundle 'START-HERE.txt') -Value $instructions -Encoding ASCII

$smoke = Join-Path ([System.IO.Path]::GetTempPath()) ('GraphTools-share-smoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smoke | Out-Null
try {
    $graphPath = Join-Path $smoke 'full-graph.json'
    & (Join-Path $bundle 'Builder\GraphTools.Builder.exe') --solution (Join-Path $SourceRoot 'GraphTools.slnx') --output $graphPath --mode full
    if ($LASTEXITCODE -ne 0) {
        throw "Published Builder smoke check failed (exit code $LASTEXITCODE)."
    }
    $graph = Get-Content -LiteralPath $graphPath -Raw | ConvertFrom-Json
    if ($graph.nodes.Count -eq 0 -or $graph.edges.Count -eq 0) {
        throw 'Published Builder produced an empty graph.'
    }
    $queryOutput = & (Join-Path $bundle 'Query\GraphTools.Query.exe') --graph $graphPath --list-symbols --project GraphTools.Core
    if ($LASTEXITCODE -ne 0) {
        throw "Published Query smoke check failed (exit code $LASTEXITCODE)."
    }
    $symbols = ($queryOutput -join [Environment]::NewLine) | ConvertFrom-Json
    if (@($symbols).Count -eq 0) {
        throw 'Published Query returned no Core symbols.'
    }
    Write-Host "Verified: $($graph.nodes.Count) nodes, $($graph.edges.Count) edges, $(@($symbols).Count) Core symbols."
}
finally {
    foreach ($name in @('full-graph.json', 'project-dependencies.json')) {
        $path = Join-Path $smoke $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path
        }
    }
    Remove-Item -LiteralPath $smoke
}

$files = Get-ChildItem -LiteralPath $bundle -Recurse -File
$sizeMB = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Ready: $bundle ($sizeMB MB)"
