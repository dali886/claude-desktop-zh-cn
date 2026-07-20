#Requires -Version 5.1
# Claude Desktop zh-CN installer - no Python required
# Save as UTF-8 with BOM for Windows PowerShell 5.1
$ErrorActionPreference = "Stop"
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WindowsApps = "C:\Program Files\WindowsApps"
$MemPath = Join-Path $ScriptDir "translation-memory.json"
if (-not (Test-Path $MemPath)) {
    $MemPath = Join-Path (Split-Path $ScriptDir -Parent) "shared	ranslation-memory.json"
}
if (-not (Test-Path $MemPath)) {
    $MemPath = Join-Path (Split-Path $ScriptDir -Parent) "mac	ranslation-memory.json"
}
$StatePath = Join-Path $ScriptDir "install-state.json"
$LogPath = Join-Path $ScriptDir "install-no-python.log"

function Write-Log([string]$Msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Msg
    Write-Host $line
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
}

function Get-JsonSerializer {
    Add-Type -AssemblyName System.Web.Extensions -ErrorAction SilentlyContinue
    $ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $ser.MaxJsonLength = [int]::MaxValue
    $ser.RecursionLimit = 100
    return $ser
}

function Read-JsonFile([string]$Path) {
    $ser = Get-JsonSerializer
    $raw = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    return $ser.DeserializeObject($raw)
}

# PowerShell 5.1: foreach ($k in $genericDict.Keys) only yields ONE entry.
# Always walk GetEnumerator() for Dictionary / Hashtable.
function New-StringMap {
    # Case-sensitive map (default @{} merges "Cancel"/"cancel")
    return New-Object 'System.Collections.Hashtable' ([System.StringComparer]::Ordinal)
}

function Convert-IDictToHashtable($obj) {
    $h = New-StringMap
    if ($null -eq $obj) { return $h }
    $e = $obj.GetEnumerator()
    while ($e.MoveNext()) {
        $k = [string]$e.Current.Key
        $v = $e.Current.Value
        if ($null -eq $v) {
            $h[$k] = ""
        } else {
            $h[$k] = [string]$v
        }
    }
    return $h
}

function Escape-JsonString([string]$s) {
    if ($null -eq $s) { return "" }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        switch ($ch) {
            '"'  { [void]$sb.Append('\"') }
            '\'  { [void]$sb.Append('\\') }
            "`n" { [void]$sb.Append('\n') }
            "`r" { [void]$sb.Append('\r') }
            "`t" { [void]$sb.Append('\t') }
            default {
                $code = [int][char]$ch
                if ($code -lt 0x20) {
                    [void]$sb.AppendFormat('\u{0:x4}', $code)
                } else {
                    [void]$sb.Append($ch)
                }
            }
        }
    }
    return $sb.ToString()
}

function Write-DictJson([string]$Path, [hashtable]$Dict) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("{")
    $keys = New-Object 'System.Collections.Generic.List[string]'
    foreach ($k in $Dict.Keys) { [void]$keys.Add([string]$k) }
    $n = $keys.Count
    for ($i = 0; $i -lt $n; $i++) {
        $k = $keys[$i]
        $v = [string]$Dict[$k]
        $ks = Escape-JsonString $k
        $vs = Escape-JsonString $v
        if ($i -lt $n - 1) {
            [void]$sb.AppendLine(('  "{0}": "{1}",' -f $ks, $vs))
        } else {
            [void]$sb.AppendLine(('  "{0}": "{1}"' -f $ks, $vs))
        }
    }
    [void]$sb.AppendLine("}")
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $sb.ToString(), $utf8)
}

function Find-ClaudeRoot {
    if (-not (Test-Path $WindowsApps)) {
        throw "WindowsApps not found: $WindowsApps"
    }
    $cands = @()
    Get-ChildItem -Path $WindowsApps -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue | ForEach-Object {
        $en = Join-Path $_.FullName "app\resources\en-US.json"
        if (Test-Path $en) {
            $mtime = (Get-Item $en).LastWriteTimeUtc
            $cands += [pscustomobject]@{ Path = $_.FullName; Mtime = $mtime; Name = $_.Name }
        }
    }
    if ($cands.Count -eq 0) {
        throw "Claude not installed (no app\resources\en-US.json). Install Claude Desktop first."
    }
    $sorted = $cands | Sort-Object Mtime -Descending
    Write-Log ("Found Claude packages: {0}" -f $sorted.Count)
    foreach ($c in $sorted) {
        $mark = ""
        if ($c.Path -eq $sorted[0].Path) { $mark = " <-- selected" }
        Write-Log ("  {0}{1}" -f $c.Name, $mark)
    }
    return $sorted[0].Path
}

function Ensure-WriteAccess([string]$Resources) {
    $test = Join-Path $Resources ".zh-cn-write-test"
    try {
        [System.IO.File]::WriteAllText($test, "ok")
        Remove-Item $test -Force -ErrorAction SilentlyContinue
        Write-Log "Write access OK"
        return
    } catch {
        Write-Log "Write denied; running takeown/icacls..."
    }
    $root = Split-Path -Parent (Split-Path -Parent $Resources)
    $null = & takeown.exe /F $root /A /R /D Y 2>&1
    $null = & icacls.exe $root /grant "Administrators:(OI)(CI)F" /T /C /Q 2>&1
    try {
        [System.IO.File]::WriteAllText($test, "ok")
        Remove-Item $test -Force -ErrorAction SilentlyContinue
        Write-Log "Write access OK after ACL fix"
    } catch {
        throw "Still cannot write to $Resources. Right-click bat -> Run as administrator. $($_.Exception.Message)"
    }
}

function Load-Memory {
    if (-not (Test-Path $MemPath)) {
        throw "Missing translation-memory.json next to this script. Copy the full folder."
    }
    Write-Log "Loading translation-memory.json ..."
    $obj = Read-JsonFile $MemPath
    $mem = Convert-IDictToHashtable $obj
    Write-Log ("Memory entries: {0}" -f $mem.Count)
    if ($mem.Count -lt 100) {
        throw ("translation-memory.json loaded only {0} entries (expected thousands). Abort to avoid overwriting Chinese with English." -f $mem.Count)
    }
    return $mem
}

function Build-ZhFromEn([hashtable]$EnDict, [hashtable]$Memory, [string]$Label) {
    $out = New-StringMap
    $miss = 0
    $hit = 0
    foreach ($k in $EnDict.Keys) {
        $en = [string]$EnDict[$k]
        if ($Memory.ContainsKey($en)) {
            $out[[string]$k] = [string]$Memory[$en]
            $hit++
        } else {
            $out[[string]$k] = $en
            $miss++
        }
    }
    Write-Log ("[{0}] keys={1} hit={2} miss={3}" -f $Label, $out.Count, $hit, $miss)
    return @{ Dict = $out; Miss = $miss; Hit = $hit }
}

function Patch-JsFiles([string]$AssetsDir, [string]$BackupDir) {
    if (-not (Test-Path $AssetsDir)) {
        Write-Log "No assets/v1; skip JS patch"
        return @()
    }
    if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null }

    $oldLocales = '["en-US","de-DE","fr-FR","ko-KR","ja-JP","es-419","es-ES","it-IT","hi-IN","pt-BR","id-ID"]'
    $newLocales = '["en-US","de-DE","fr-FR","ko-KR","ja-JP","es-419","es-ES","it-IT","hi-IN","pt-BR","id-ID","zh-CN"]'
    $oldMap = '{"en-US":"en","de-DE":"de","fr-FR":"fr","ko-KR":"ko","ja-JP":"ja","es-419":"es","es-ES":"es","it-IT":"it","hi-IN":"en","pt-BR":"pt_BR","id-ID":"id"}'
    $newMap = '{"en-US":"en","de-DE":"de","fr-FR":"fr","ko-KR":"ko","ja-JP":"ja","es-419":"es","es-ES":"es","it-IT":"it","hi-IN":"en","pt-BR":"pt_BR","id-ID":"id","zh-CN":"zh_CN"}'
    $oldPersona = 'case"ja-JP":return["language","ja"];case"es-419"'
    $newPersona = 'case"ja-JP":return["language","ja"];case"zh-CN":return["language","zh"];case"es-419"'

    $patched = New-Object System.Collections.Generic.List[string]
    Get-ChildItem -Path $AssetsDir -Filter "*.js" -File | ForEach-Object {
        $path = $_.FullName
        $len = $_.Length
        if ($len -lt 500 -or $len -gt 50MB) { return }
        try {
            $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        } catch { return }
        if ($text -notmatch 'en-US') { return }
        if ($text -notmatch 'de-DE|ja-JP|pt-BR') { return }

        $orig = $text
        $changed = $false

        if ($text.Contains($oldLocales) -and -not $text.Contains($newLocales)) {
            $text = $text.Replace($oldLocales, $newLocales)
            $changed = $true
        }
        if ($text.Contains($oldMap) -and -not $text.Contains('"zh-CN":"zh_CN"')) {
            $text = $text.Replace($oldMap, $newMap)
            $changed = $true
        }
        if ($text.Contains($oldPersona) -and -not $text.Contains('case"zh-CN":return["language","zh"]')) {
            $text = $text.Replace($oldPersona, $newPersona)
            $changed = $true
        }

        if ($changed -and $text -ne $orig) {
            $bak = Join-Path $BackupDir $_.Name
            if (-not (Test-Path $bak)) {
                Copy-Item -Path $path -Destination $bak -Force
            }
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
            Write-Log ("Patched JS: {0}" -f $_.Name)
            [void]$patched.Add($path)
        }
    }
    return ,$patched.ToArray()
}

# ---- main ----
"" | Set-Content -Path $LogPath -Encoding UTF8
Write-Log "=== Claude zh-CN install (no Python) ==="
Write-Log ("ScriptDir: {0}" -f $ScriptDir)

try {
    $appRoot = Find-ClaudeRoot
    $resources = Join-Path $appRoot "app\resources"
    if ($appRoot -match 'Claude_([^_]+)_') {
        $version = $Matches[1]
    } else {
        $version = Split-Path $appRoot -Leaf
    }
    $packDir = Join-Path $ScriptDir ("zh-CN-pack-{0}" -f $version)
    if (-not (Test-Path $packDir)) { New-Item -ItemType Directory -Path $packDir -Force | Out-Null }
    Write-Log ("Target: {0}" -f $appRoot)
    Write-Log ("Version: {0}" -f $version)

    Ensure-WriteAccess $resources

    $shellEnPath = Join-Path $resources "en-US.json"
    $ionEnPath = Join-Path $resources "ion-dist\i18n\en-US.json"
    $dynEnPath = Join-Path $resources "ion-dist\i18n\dynamic\en-US.json"
    foreach ($p in @($shellEnPath, $ionEnPath, $dynEnPath)) {
        if (-not (Test-Path $p)) { throw "Missing source: $p" }
    }

    $memory = Load-Memory

    Write-Log "Reading en-US files..."
    $shellEn = Convert-IDictToHashtable (Read-JsonFile $shellEnPath)
    $ionEn = Convert-IDictToHashtable (Read-JsonFile $ionEnPath)
    $dynEn = Convert-IDictToHashtable (Read-JsonFile $dynEnPath)
    Write-Log ("shell={0} ion={1} dynamic={2}" -f $shellEn.Count, $ionEn.Count, $dynEn.Count)
    if ($shellEn.Count -lt 10 -or $ionEn.Count -lt 100) {
        throw ("en-US parse failed (shell={0} ion={1}). Abort." -f $shellEn.Count, $ionEn.Count)
    }

    $r1 = Build-ZhFromEn $shellEn $memory "shell"
    $r2 = Build-ZhFromEn $dynEn $memory "dynamic"
    $r3 = Build-ZhFromEn $ionEn $memory "ion"
    $totalMiss = [int]$r1.Miss + [int]$r2.Miss + [int]$r3.Miss
    $totalHit = [int]$r1.Hit + [int]$r2.Hit + [int]$r3.Hit
    if ($totalHit -lt 100) {
        throw ("Only {0} cache hits (miss={1}). Abort to avoid writing mostly-English zh-CN." -f $totalHit, $totalMiss)
    }

    $shellDst = Join-Path $resources "zh-CN.json"
    $ionDst = Join-Path $resources "ion-dist\i18n\zh-CN.json"
    $ovrDst = Join-Path $resources "ion-dist\i18n\zh-CN.overrides.json"
    $dynDst = Join-Path $resources "ion-dist\i18n\dynamic\zh-CN.json"

    Write-Log "Writing zh-CN files..."
    Write-DictJson $shellDst $r1.Dict
    Write-DictJson $dynDst $r2.Dict
    Write-DictJson $ionDst $r3.Dict
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($ovrDst, "{}" + [Environment]::NewLine, $utf8NoBom)

    Write-DictJson (Join-Path $packDir "zh-CN.json") $r1.Dict
    Write-DictJson (Join-Path $packDir "ion-dist\i18n\zh-CN.json") $r3.Dict
    Write-DictJson (Join-Path $packDir "ion-dist\i18n\dynamic\zh-CN.json") $r2.Dict
    [System.IO.File]::WriteAllText((Join-Path $packDir "ion-dist\i18n\zh-CN.overrides.json"), "{}" + [Environment]::NewLine, $utf8NoBom)

    $assets = Join-Path $resources "ion-dist\assets\v1"
    $bakDir = Join-Path $packDir "patched-js-backups"
    $patched = @(Patch-JsFiles $assets $bakDir)

    $stateObj = [ordered]@{
        installedAt     = (Get-Date -Format "yyyy-MM-ddTHH:mm:sszzz")
        pluginInstalled = $false
        resources       = $resources
        claudeRoot      = $appRoot
        mode            = "add-zh-CN-pack"
        locale          = "zh-CN"
        uiInstalled     = $true
        overlaid        = @()
        added           = @($shellDst, $ionDst, $ovrDst, $dynDst)
        patchedJs       = @($patched)
        packDir         = $packDir
        appVersion      = $version
        engine          = "powershell-no-python"
        cacheHits       = $totalHit
        cacheMisses     = $totalMiss
        note            = "Dedicated zh-CN pack without Python; existing language files were not overwritten."
    }
    ($stateObj | ConvertTo-Json -Depth 6) | Set-Content -Path $StatePath -Encoding UTF8
    Copy-Item $StatePath (Join-Path $packDir "install-state.json") -Force

    Write-Log ("Installed shell={0} ion={1} dynamic={2}" -f $r1.Dict.Count, $r3.Dict.Count, $r2.Dict.Count)
    Write-Log ("Patched JS count: {0}" -f $patched.Count)
    if ($totalMiss -gt 0) {
        Write-Log ("NOTE: {0} strings missing from cache (kept English)." -f $totalMiss)
    }
    Write-Log "DONE - fully quit Claude and reopen"
    Write-Host ""
    Write-Host "OK. Quit Claude completely, then reopen." -ForegroundColor Green
    if ($totalMiss -gt 0) {
        Write-Host ("Cache miss: {0} strings may stay English." -f $totalMiss) -ForegroundColor Yellow
    }
    exit 0
}
catch {
    Write-Log ("ERROR: {0}" -f $_.Exception.Message)
    Write-Host ""
    Write-Host ("FAILED: {0}" -f $_.Exception.Message) -ForegroundColor Red
    Write-Host "If access denied: right-click bat -> Run as administrator." -ForegroundColor Yellow
    exit 1
}
