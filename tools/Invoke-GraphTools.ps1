<#
.SYNOPSIS
    Runs a GraphTools executable (Builder or Query) with a clean environment, working around
    the case where a calling shell has inherited a DOTNET_ROOT override (e.g. set by Visual
    Studio Insiders to pin its own bundled .NET runtime) that hides the machine-wide net8.0
    runtime GraphTools targets. See docs/DESIGN_DECISIONS.md for the full story.

.PARAMETER Tool
    Which GraphTools executable to run: 'Builder' or 'Query'.

.PARAMETER Args
    Remaining arguments are passed through as-is to the underlying executable.

.EXAMPLE
    .\tools\Invoke-GraphTools.ps1 -Tool Builder -- --solution "C:\Repo\App.slnx" --output "C:\Repo\docs\full-graph.json" --mode full
    .\tools\Invoke-GraphTools.ps1 -Tool Query -- --graph "C:\Repo\docs\full-graph.json" --symbol "MyNamespace.MyClass"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Builder', 'Query')]
    [string]$Tool,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "GraphTools.$Tool\bin\Debug\net8.0\GraphTools.$Tool.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "GraphTools.$Tool.exe not found at '$exePath'. Build the solution first (dotnet build)."
}

# Clear any inherited DOTNET_ROOT override so the apphost resolves the machine-wide net8.0
# shared runtime instead of whatever a host IDE/tool pinned it to.
Remove-Item -Path Env:\DOTNET_ROOT -ErrorAction SilentlyContinue

& $exePath @Args
exit $LASTEXITCODE
