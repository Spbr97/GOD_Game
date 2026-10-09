<#
.SYNOPSIS
    Builds, smoke tests and packages a Windows x64 player for
    "The God Who Was Forgotten". STANDALONE_RELEASE_ROADMAP.md TASK 039.

.DESCRIPTION
    The one command TASK 039 asks for. From a clean checkout, with nothing open:

        .\Tools\build-windows.ps1                 # Development player, smoke tested, zipped
        .\Tools\build-windows.ps1 -Variant Release
        .\Tools\build-windows.ps1 -Variant Both

    The Editor version is read from ProjectSettings/ProjectVersion.txt rather than
    written here, so this script cannot silently build with a different Unity than
    the project is pinned to -- which is the failure that makes a "reproducible"
    build not reproducible.

    Only the Development variant can be smoke tested automatically. The harness that
    drives it is compiled out of a Release player on purpose (SPEC.md section 52), so
    a Release build is launch-checked here and hand-verified against the manual pass
    in TEST_PLAN.md.

.PARAMETER Variant
    Development (default), Release, or Both.

.PARAMETER SkipSmokeTest
    Build and package only. Use when iterating on the build itself.

.PARAMETER NoZip
    Leave the player directory unpackaged.

.PARAMETER UnityPath
    An explicit Unity.exe, for a machine that does not install via Unity Hub.

.PARAMETER SmokeTestTimeoutSeconds
    How long to let the player run before treating it as hung. Default 600.
#>
[CmdletBinding()]
param(
    [ValidateSet('Development', 'Release', 'Both')]
    [string] $Variant = 'Development',

    [switch] $SkipSmokeTest,
    [switch] $NoZip,
    [string] $UnityPath,
    [int]    $SmokeTestTimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'

# Repo root is this script's parent's parent, so the script works from any working
# directory -- including a double-click, where the working directory is not the repo.
$RepoRoot    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$BuildRoot   = Join-Path $RepoRoot 'Build\Windows'
$LogRoot     = Join-Path $BuildRoot '_logs'
$Executable  = 'TheGodWhoWasForgotten.exe'

function Write-Step([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok([string] $Message)   { Write-Host "    OK   $Message" -ForegroundColor Green }
function Write-Bad([string] $Message)  { Write-Host "    FAIL $Message" -ForegroundColor Red }
function Write-Info([string] $Message) { Write-Host "         $Message" -ForegroundColor DarkGray }

function Get-PinnedEditorVersion {
    $versionFile = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) {
        throw "Cannot find $versionFile. Run this from inside the repository."
    }

    $line = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if ($null -eq $line) {
        throw "No m_EditorVersion in $versionFile."
    }

    return $line.Matches[0].Groups[1].Value.Trim()
}

function Resolve-Unity([string] $Version) {
    if ($UnityPath) {
        if (-not (Test-Path $UnityPath)) { throw "-UnityPath '$UnityPath' does not exist." }
        return $UnityPath
    }

    if ($env:UNITY_EDITOR_PATH -and (Test-Path $env:UNITY_EDITOR_PATH)) {
        return $env:UNITY_EDITOR_PATH
    }

    $candidates = @(
        "$env:ProgramFiles\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Editor\Unity.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    throw ("Unity $Version is not installed where this script looks. Install it from Unity Hub, " +
           "or pass -UnityPath, or set UNITY_EDITOR_PATH. Building with a different Editor version " +
           "would not be a reproducible build.")
}

function Invoke-UnityBuild([string] $Unity, [string] $VariantName) {
    $method = "WindowsBuild.$VariantName"
    $log    = Join-Path $LogRoot "build-$($VariantName.ToLower()).log"

    New-Item -ItemType Directory -Force -Path $LogRoot | Out-Null

    Write-Info "method   $method"
    Write-Info "log      $log"
    Write-Info 'Unity runs headless here; a first build on a cold Library can take several minutes.'

    # No -quit: WindowsBuild calls EditorApplication.Exit itself so a failed
    # BuildPipeline call produces a non-zero code. -quit would mask that with 0.
    $arguments = @(
        '-batchmode',
        '-projectPath', $RepoRoot,
        '-buildTarget', 'Win64',
        '-executeMethod', $method,
        '-logFile', $log
    )

    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru -Wait -NoNewWindow
    return @{ ExitCode = $process.ExitCode; Log = $log }
}

function Show-BuildLogErrors([string] $Log) {
    if (-not (Test-Path $Log)) { return }

    $errors = Select-String -Path $Log -Pattern 'error CS|\[BUILD\]|BuildFailedException|Exception:' |
              Select-Object -Last 25
    foreach ($entry in $errors) {
        Write-Info $entry.Line.Trim()
    }
}

function Wait-ForUnlocked([string] $Path, [int] $TimeoutSeconds = 30) {
    # "The process has exited" and "Windows has released its file handles" are not the
    # same moment, and nothing in .NET lets you wait for the second one. Opening the
    # file for reading is the only reliable test, so poll that.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        try {
            $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'None')
            $stream.Close()
            return $true
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Info "'$Path' is still locked after $TimeoutSeconds s."
    return $false
}

function Invoke-SmokeTest([string] $PlayerDirectory, [string] $VariantName) {
    $exe = Join-Path $PlayerDirectory $Executable
    if (-not (Test-Path $exe)) {
        Write-Bad "no $Executable in $PlayerDirectory"
        return $false
    }

    $report = Join-Path $LogRoot "smoke-test-$($VariantName.ToLower()).txt"
    if (Test-Path $report) { Remove-Item $report -Force }

    $playerLog = Join-Path $LogRoot "player-$($VariantName.ToLower()).log"

    # Windowed and small on purpose. -batchmode in a player suppresses rendering, and
    # this test walks through three scenes' worth of canvases and cameras; running it
    # the way a person runs it keeps the test honest about what it verified.
    $arguments = @(
        '-smokeTest',
        '-smokeTestReport', $report,
        '-logFile', $playerLog,
        '-screen-width', '1280',
        '-screen-height', '720',
        '-screen-fullscreen', '0'
    )

    Write-Info "launching $exe -smokeTest"
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru

    if (-not $process.WaitForExit($SmokeTestTimeoutSeconds * 1000)) {
        Write-Bad "the player did not exit within $SmokeTestTimeoutSeconds s; killing it"
        try { $process.Kill() } catch { }
        Write-Info "player log: $playerLog"
        return $false
    }

    # The timed WaitForExit returns as soon as the process signals, which on Windows is
    # before its file handles are necessarily released -- the first run of this script
    # got as far as a passing smoke test and then failed to zip its own .exe. Wait
    # again without a timeout to flush, then confirm the executable is actually
    # unlocked before anyone tries to read it.
    $process.WaitForExit()
    Wait-ForUnlocked -Path $exe -TimeoutSeconds 30 | Out-Null

    $reportPassed = (Test-Path $report) -and
        [bool](Select-String -Path $report -Pattern '^RESULT: PASS -' -Quiet)
    if (Test-Path $report) {
        Write-Host ''
        Get-Content $report -Encoding utf8 | ForEach-Object { Write-Host "    $_" }
        Write-Host ''
    }
    else {
        Write-Info "no report at $report; see $playerLog"
    }

    if ($process.ExitCode -eq 0 -and $reportPassed) {
        Write-Ok 'first smoke-test process passed in the built player'
        return (Invoke-SmokeResume -PlayerDirectory $PlayerDirectory)
    }

    Write-Bad "smoke test exited $($process.ExitCode); passing report present: $reportPassed"
    Write-Info "player log: $playerLog"
    return $false
}

function Invoke-SmokeResume([string] $PlayerDirectory) {
    # The second launch proves that the first process's save survives a cold start.
    # SaveStorage keeps both launches in the isolated SmokeTestSaves directory.
    $exe = Join-Path $PlayerDirectory $Executable
    $report = Join-Path $LogRoot 'smoke-test-resume.txt'
    $playerLog = Join-Path $LogRoot 'player-smoke-resume.log'
    if (Test-Path $report) { Remove-Item $report -Force }

    $arguments = @(
        '-smokeTestResume',
        '-smokeTestReport', $report,
        '-logFile', $playerLog,
        '-screen-width', '1280',
        '-screen-height', '720',
        '-screen-fullscreen', '0'
    )

    Write-Info 'relaunching the player to load the previous process save'
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit($SmokeTestTimeoutSeconds * 1000)) {
        Write-Bad "the resume player did not exit within $SmokeTestTimeoutSeconds s; killing it"
        try { $process.Kill() } catch { }
        Write-Info "player log: $playerLog"
        return $false
    }

    $process.WaitForExit()
    Wait-ForUnlocked -Path $exe -TimeoutSeconds 30 | Out-Null

    $reportPassed = (Test-Path $report) -and
        [bool](Select-String -Path $report -Pattern '^RESULT: PASS -' -Quiet)
    if (Test-Path $report) {
        Write-Host ''
        Get-Content $report -Encoding utf8 | ForEach-Object { Write-Host "    $_" }
        Write-Host ''
    }

    if ($process.ExitCode -eq 0 -and $reportPassed) {
        Write-Ok 'save loaded successfully in a second player process'
        return $true
    }

    Write-Bad "resume smoke test exited $($process.ExitCode); passing report present: $reportPassed"
    Write-Info "player log: $playerLog"
    return $false
}

function Test-DeveloperCodeGating([string] $PlayerDirectory, [string] $VariantName) {
    # TASK 039 asks us to confirm the Release player excludes developer-only controls,
    # and the honest way to confirm that is to look in the binary rather than to trust
    # that a #if did its job. StandaloneSmokeTest and MainMenuController's seam are both
    # wholly inside UNITY_EDITOR || GAME_DEVELOPER_TOOLS, so in a Release player those
    # names should not exist in the managed assembly's metadata at all.
    #
    # Searching a DLL for a UTF-8 type name is crude, and it is crude in the safe
    # direction: a false positive says "still present" and fails the check, which gets
    # looked at. It cannot report absent something that is there.
    $managed = Join-Path $PlayerDirectory 'TheGodWhoWasForgotten_Data\Managed'
    $assembly = Join-Path $managed 'Game.Runtime.dll'

    if (-not (Test-Path $assembly)) {
        Write-Bad "no Game.Runtime.dll under $managed; cannot verify developer-code gating"
        return $false
    }

    $markers = @('StandaloneSmokeTest', 'BeginNewGameForSmokeTest')
    $found = @()

    foreach ($marker in $markers) {
        if (Select-String -Path $assembly -Pattern $marker -Encoding utf8 -Quiet) {
            $found += $marker
        }
    }

    if ($VariantName -eq 'Release') {
        if ($found.Count -eq 0) {
            Write-Ok 'no developer-only smoke-test code in the Release assembly'
            return $true
        }

        Write-Bad "the Release assembly still contains: $($found -join ', ')"
        Write-Info 'GAME_DEVELOPER_TOOLS is leaking into the Release build; SPEC.md section 52 is not satisfied.'
        return $false
    }

    if ($found.Count -eq $markers.Count) {
        Write-Ok 'developer-only code is present in the Development assembly, as intended'
        return $true
    }

    Write-Bad "the Development assembly is missing: $(($markers | Where-Object { $found -notcontains $_ }) -join ', ')"
    Write-Info 'GAME_DEVELOPER_TOOLS was not defined, so the smoke test cannot run. Check WindowsBuild.DeveloperToolsDefine.'
    return $false
}

function Test-ReleasePlayerLaunches([string] $PlayerDirectory) {
    # A Release player has no harness in it, so the most a script can assert is that
    # the process starts, initializes the engine and reaches the first scene without
    # dying. Everything past that is the manual pass. Stated rather than dressed up as
    # a full test.
    $exe = Join-Path $PlayerDirectory $Executable
    $playerLog = Join-Path $LogRoot 'player-release-launch.log'
    if (Test-Path $playerLog) { Remove-Item $playerLog -Force }

    Write-Info "launching $exe for a 20 s start-up check"
    $arguments = @('-logFile', $playerLog, '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0')
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru

    $exited = $process.WaitForExit(20000)
    if ($exited) {
        Write-Bad "the Release player exited before the 20 s start-up check ended (code $($process.ExitCode))"
        Write-Info "player log: $playerLog"
        return $false
    }

    if (-not $exited) {
        try { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 1500 } catch { }
        if (-not $process.HasExited) { try { $process.Kill() } catch { } }
        try { $process.WaitForExit() } catch { }
    }

    # Same reason as in the smoke test: packaging comes next and cannot read a locked .exe.
    Wait-ForUnlocked -Path $exe -TimeoutSeconds 30 | Out-Null

    if (-not (Test-Path $playerLog)) {
        Write-Bad 'the Release player produced no log for this launch'
        return $false
    }

    $fatal = Select-String -Path $playerLog -Pattern 'Fatal error|Unhandled Exception|Crash!!!' |
             Select-Object -First 5
    if ($fatal) {
        Write-Bad 'the Release player log contains a fatal error'
        foreach ($entry in $fatal) { Write-Info $entry.Line.Trim() }
        return $false
    }

    if (-not (Select-String -Path $playerLog -Pattern '\[GAME\] GameManager initialized\.' -Quiet) -or
        -not (Select-String -Path $playerLog -Pattern '\[GAME\] State changed: Boot -> Playing' -Quiet)) {
        Write-Bad 'the Release player did not initialize the game and reach the Main Menu'
        Write-Info "player log: $playerLog"
        return $false
    }

    Write-Ok 'the Release player starts and stays up (manual pass still required)'
    return $true
}

function Copy-PlayerDocs([string] $PlayerDirectory, [string] $Version) {
    $documents = @(
        @{ Source = 'Release\PLAYER_README.txt'; Destination = 'PLAYER_README.txt' },
        @{ Source = 'Release\CONTROLS.txt'; Destination = 'CONTROLS.txt' },
        @{ Source = 'Release\RELEASE_NOTES.txt'; Destination = 'RELEASE_NOTES.txt' },
        @{ Source = 'Release\CREDITS.txt'; Destination = 'CREDITS.txt' },
        @{ Source = 'ASSET_LICENSES.md'; Destination = 'ASSET_LICENSES.md' }
    )

    foreach ($document in $documents) {
        $source = Join-Path $RepoRoot $document.Source
        if (-not (Test-Path -LiteralPath $source)) {
            Write-Bad "player document is missing: $source"
            return $false
        }

        try {
            $destination = Join-Path $PlayerDirectory $document.Destination
            if ($document.Destination -eq 'ASSET_LICENSES.md') {
                Copy-Item -LiteralPath $source -Destination $destination -ErrorAction Stop
            }
            else {
                $contents = [System.IO.File]::ReadAllText($source).Replace('{{VERSION}}', $Version)
                [System.IO.File]::WriteAllText($destination, $contents,
                    [System.Text.UTF8Encoding]::new($false))
            }
        }
        catch {
            Write-Bad "could not copy player document '$source': $($_.Exception.Message)"
            return $false
        }
    }

    Write-Ok 'player instructions, controls, notes, credits and asset ledger copied'
    return $true
}

function New-Package([string] $PlayerDirectory, [string] $VariantName, [string] $Version) {
    $zip = Join-Path $BuildRoot "TheGodWhoWasForgotten-$Version-windows-x64-$($VariantName.ToLower()).zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }

    # A retry rather than one attempt: a virus scanner or an indexer touching a
    # freshly written 176 MB player is ordinary on Windows, and losing a good build to
    # it would be silly. Errors are made terminating first so the catch sees them --
    # Compress-Archive reports a locked file non-terminating.
    $attempts = 3
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            Compress-Archive -Path (Join-Path $PlayerDirectory '*') -DestinationPath $zip -ErrorAction Stop
            break
        }
        catch {
            if (Test-Path $zip) { Remove-Item $zip -Force -ErrorAction SilentlyContinue }

            if ($attempt -eq $attempts) {
                throw "Could not package $PlayerDirectory after $attempts attempts: $($_.Exception.Message)"
            }

            Write-Info "packaging attempt $attempt failed ($($_.Exception.Message.Trim())); retrying"
            Start-Sleep -Seconds 5
        }
    }

    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)

    # TASK 057 ships a checksum with the download; produce it from the first build so
    # the habit and the tooling already exist by then.
    $hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash
    Set-Content -Path "$zip.sha256" -Value "$hash  $(Split-Path -Leaf $zip)" -Encoding utf8

    Write-Ok "packaged $(Split-Path -Leaf $zip) ($sizeMb MB)"
    Write-Info "sha256 $hash"
    return $zip
}

function Get-BundleVersion {
    $settings = Join-Path $RepoRoot 'ProjectSettings\ProjectSettings.asset'
    $line = Select-String -Path $settings -Pattern '^\s*bundleVersion:\s*(.+)$' | Select-Object -First 1
    if ($null -eq $line) { return '0.0.0' }
    return $line.Matches[0].Groups[1].Value.Trim()
}

# ------------------------------------------------------------------------------ main

$editorVersion = Get-PinnedEditorVersion
$unity         = Resolve-Unity $editorVersion
$bundleVersion = Get-BundleVersion

Write-Step 'Windows x64 build'
Write-Info "repo           $RepoRoot"
Write-Info "pinned Editor  $editorVersion"
Write-Info "Unity.exe      $unity"
Write-Info "game version   $bundleVersion"
Write-Info "output         $BuildRoot"

$gitStatus = & git -C $RepoRoot status --porcelain 2>$null
if ($LASTEXITCODE -eq 0 -and $gitStatus) {
    Write-Info 'note: the working tree has uncommitted changes; build-info.txt will say so.'
}

if ($Variant -eq 'Both') { $variants = @('Development', 'Release') } else { $variants = @($Variant) }

$failed = @()

foreach ($current in $variants) {
    Write-Step "Building the $current player"

    $result = Invoke-UnityBuild -Unity $unity -VariantName $current
    $playerDirectory = Join-Path $BuildRoot $current

    if ($result.ExitCode -ne 0 -or -not (Test-Path (Join-Path $playerDirectory $Executable))) {
        Write-Bad "the $current build failed (Unity exited $($result.ExitCode))"
        Show-BuildLogErrors $result.Log
        $failed += "$current build"
        continue
    }

    Write-Ok "$current player at $playerDirectory"
    $info = Join-Path $playerDirectory 'build-info.txt'
    if (Test-Path $info) { Get-Content $info | ForEach-Object { Write-Info $_ } }

    $docsReady = Copy-PlayerDocs -PlayerDirectory $playerDirectory -Version $bundleVersion
    if (-not $docsReady) { $failed += "$current player documents" }

    if (-not $SkipSmokeTest) {
        Write-Step "Verifying the $current player outside the Editor"

        if (-not (Test-DeveloperCodeGating -PlayerDirectory $playerDirectory -VariantName $current)) {
            $failed += "$current developer-code gating"
        }

        if ($current -eq 'Development') {
            if (-not (Invoke-SmokeTest -PlayerDirectory $playerDirectory -VariantName $current)) {
                $failed += "$current smoke test"
            }
        }
        else {
            if (-not (Test-ReleasePlayerLaunches -PlayerDirectory $playerDirectory)) {
                $failed += 'Release launch check'
            }
        }
    }

    if (-not $NoZip) {
        if (-not $docsReady) {
            Write-Bad "skipping $current packaging because player documents are missing"
            continue
        }

        Write-Step "Packaging the $current player"

        # Contained on purpose. A packaging problem must not abandon the variants after
        # this one, and it must not discard a player that already built and passed its
        # smoke test -- the first run of this script threw away a verified Release build
        # because zipping the Development one failed.
        try {
            New-Package -PlayerDirectory $playerDirectory -VariantName $current -Version $bundleVersion | Out-Null
        }
        catch {
            Write-Bad "could not package the $current player: $($_.Exception.Message)"
            Write-Info "the player itself is intact at $playerDirectory"
            $failed += "$current packaging"
        }
    }
}

Write-Step 'Result'

# URP can write shader-keyword prefiltering results into source assets. Compare
# content to HEAD: a fresh Unity import can make git status report modified files
# whose normalized content is unchanged, and that is not a change to review.
$churn = & git -C $RepoRoot diff --name-only HEAD -- 'Assets/Settings' 'ProjectSettings/GraphicsSettings.asset' 2>$null
if ($LASTEXITCODE -eq 0 -and $churn) {
    Write-Info 'Graphics settings differ from HEAD. Inspect these files before committing:'
    $churn | ForEach-Object { Write-Info "  $_" }
}

if ($failed.Count -eq 0) {
    Write-Ok "all requested variants built and verified: $($variants -join ', ')"
    Write-Info 'A passing smoke test is not a passing release. TEST_PLAN.md has the manual pass.'
    exit 0
}

Write-Bad "problems: $($failed -join ', ')"
Write-Info "logs in $LogRoot"
exit 1
