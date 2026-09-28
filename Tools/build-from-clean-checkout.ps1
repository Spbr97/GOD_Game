<#
.SYNOPSIS
    Clones this repository at a given commit into a scratch directory and builds a
    Windows player there, with nothing from the working tree.
    STANDALONE_RELEASE_ROADMAP.md TASK 039's clean-checkout clause.

.DESCRIPTION
    TASK 039 claims the release is reproducible from a clean checkout. Every build so
    far has been made in the working tree, which is not the same claim and cannot test
    it. A working tree carries a Library folder full of imported assets, generated
    .csproj files, local Editor preferences, and -- the failure this is actually looking
    for -- files that are on disk but were never added to Git. A build that depends on
    one of those succeeds here and fails for everybody else, and nothing in the project
    notices until somebody clones it.

    So this clones into a fresh directory, checks nothing else leaked in, and builds.
    Only the things Git tracks come along, and Unity reimports everything from scratch
    on first open, which is why this takes appreciably longer than an ordinary build.

    It builds by calling Tools/build-windows.ps1 inside the clone, so it tests the
    committed build script and not this machine's copy of it.

    By default it builds HEAD. HEAD must be committed for that to mean anything: this
    refuses to run with a dirty working tree unless -AllowDirty is given, because a
    clean-checkout build of a commit that does not contain your changes is a test that
    passes while telling you nothing.

.PARAMETER Commit
    What to check out. Default HEAD.

.PARAMETER Variant
    Passed through: Development (default), Release or Both.

.PARAMETER WorkRoot
    Where to put the clone. Default is a timestamped directory under the system
    temporary folder.

.PARAMETER Keep
    Leave the clone behind for inspection. Otherwise it is deleted on success and kept
    on failure, which is when you want to look at it.

.PARAMETER AllowDirty
    Build anyway with uncommitted changes, accepting that they are not in the clone.

.PARAMETER SkipSmokeTest
    Passed through to the inner build.

.EXAMPLE
    .\Tools\build-from-clean-checkout.ps1 -Variant Both
#>
[CmdletBinding()]
param(
    [string] $Commit = 'HEAD',

    [ValidateSet('Development', 'Release', 'Both')]
    [string] $Variant = 'Development',

    [string] $WorkRoot,
    [switch] $Keep,
    [switch] $AllowDirty,
    [switch] $SkipSmokeTest
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

function Write-Step([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok  ([string] $Message) { Write-Host "    [ok]   $Message" -ForegroundColor Green }
function Write-Bad ([string] $Message) { Write-Host "    [fail] $Message" -ForegroundColor Red }
function Write-Info([string] $Message) { Write-Host "    $Message" -ForegroundColor DarkGray }

Write-Step "Clean-checkout build of $Commit from $RepoRoot"

# ---------------------------------------------------------------- preconditions

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is not on PATH; this script cannot make a clean checkout without it.'
}

Push-Location $RepoRoot
try {
    $status = & git status --porcelain
    $resolved = (& git rev-parse $Commit).Trim()
}
finally {
    Pop-Location
}

if ($status) {
    $count = ($status | Measure-Object -Line).Lines

    if (-not $AllowDirty) {
        Write-Bad "$count uncommitted path(s) in the working tree."
        Write-Info 'A clean checkout contains only what is committed, so those changes would'
        Write-Info 'not be in this build and the result would say nothing about them.'
        Write-Info 'Commit them first, or pass -AllowDirty if you meant to build an older state.'
        exit 2
    }

    Write-Info "$count uncommitted path(s) ignored, as -AllowDirty was given."
}

Write-Ok "building commit $resolved"

if (-not $WorkRoot) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) "godgame-clean-$stamp"
}

$clone = Join-Path $WorkRoot 'repo'

# ---------------------------------------------------------------------- clone

Write-Step "Cloning into $clone"

New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null

# Cloned from the local path rather than from the remote on purpose: this is testing
# whether the *commit* is self-sufficient, not whether the network works, and it keeps
# the check runnable with no remote configured at all.
& git clone --quiet --no-hardlinks $RepoRoot $clone
if ($LASTEXITCODE -ne 0) { throw "git clone failed with exit code $LASTEXITCODE" }

Push-Location $clone
try {
    & git checkout --quiet --detach $resolved
    if ($LASTEXITCODE -ne 0) { throw "git checkout $resolved failed" }
}
finally {
    Pop-Location
}

Write-Ok 'clone created'

# ------------------------------------------------------- what should not be there

Write-Step 'Checking the clone is genuinely clean'

$mustBeAbsent = @('Library', 'Temp', 'Logs', 'obj', 'Build', 'UserSettings')
$leaked = @()

foreach ($name in $mustBeAbsent) {
    if (Test-Path (Join-Path $clone $name)) { $leaked += $name }
}

if ($leaked.Count -gt 0) {
    Write-Bad "the clone contains generated directories that should not be tracked: $($leaked -join ', ')"
    Write-Info 'Add them to .gitignore and remove them from the index; a build here would'
    Write-Info 'be reusing this machine''s state and would not be a clean-checkout build.'
    exit 3
}

Write-Ok 'no generated directories came along'

$mustBePresent = @(
    'Assets',
    'Packages\manifest.json',
    'ProjectSettings\ProjectVersion.txt',
    'ProjectSettings\EditorBuildSettings.asset',
    'Tools\build-windows.ps1'
)
$missing = @()

foreach ($name in $mustBePresent) {
    if (-not (Test-Path (Join-Path $clone $name))) { $missing += $name }
}

if ($missing.Count -gt 0) {
    Write-Bad "the commit is missing files a build needs: $($missing -join ', ')"
    exit 4
}

Write-Ok 'everything a build needs is committed'

# ---------------------------------------------------------------------- build

Write-Step "Building the $Variant player inside the clone"
Write-Info 'Unity reimports every asset on first open, so this is slower than a working-tree build.'

$inner = Join-Path $clone 'Tools\build-windows.ps1'
$arguments = @('-Variant', $Variant)
if ($SkipSmokeTest) { $arguments += '-SkipSmokeTest' }

& $inner @arguments
$buildExit = $LASTEXITCODE

# ---------------------------------------------------------------------- result

Write-Step 'Result'

if ($buildExit -eq 0) {
    Write-Ok "the $Variant player built from a clean checkout of $resolved"
    Write-Info "player: $(Join-Path $clone 'Build\Windows')"

    if ($Keep) {
        Write-Info "clone kept at $clone"
    }
    else {
        Write-Info 'removing the clone; pass -Keep to inspect it'
        Remove-Item -Recurse -Force $WorkRoot -ErrorAction SilentlyContinue
    }
}
else {
    Write-Bad "the build failed inside the clean checkout with exit code $buildExit"
    Write-Info "the clone has been left at $clone so the failure can be looked at"
    Write-Info 'A failure here that does not reproduce in the working tree means the build'
    Write-Info 'depends on something that was never committed.'
}

exit $buildExit
