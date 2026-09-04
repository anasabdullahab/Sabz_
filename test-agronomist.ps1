# =============================================================================
# SABZ Prompt 13 - Voice-First AI Agronomist Assistant test suite
# Idempotent. Requires: API running on http://localhost:5073, LocalDB SabzDB.
#
# PROVIDER MODE: this environment has NO AI provider API key configured
# (by design - the agronomist reuses the shared DashScope key under
# DiseaseDetection:ApiKey). Tests therefore assert the full LOCAL pipeline:
# authentication, ownership, text/audio validation, and the graceful 502
# "not configured" behaviour for both the text answer and the voice
# transcription (never a fake answer or a fake transcription).
# Checks tagged [LIVE-PROVIDER] document what must be verified once a real
# DashScope API key is supplied locally.
#
# READ-ONLY: the assistant must never create/modify farms, crops, transactions,
# monitoring checks, notifications or users, and must not persist chat history.
# Idempotency: every run deletes leftover "AG " fixture crops and farms through
# the public API, then recreates fixtures. Seed/reference data is never touched.
# =============================================================================
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5073'
$pass = 0
$fail = 0

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { $script:pass++; Write-Host "  PASS  $name" -ForegroundColor Green }
    else            { $script:fail++; Write-Host "  FAIL  $name  $detail" -ForegroundColor Red }
}

function SqlQuery([string]$sql) {
    $tmp = Join-Path $env:TEMP ('agq_' + [Guid]::NewGuid().ToString('N') + '.sql')
    Set-Content -Path $tmp -Value ("SET NOCOUNT ON;`n" + $sql) -Encoding UTF8
    try { return (& sqlcmd -I -S "(localdb)\mssqllocaldb" -d SabzDB -E -i $tmp -W -s"|" -h -1) }
    finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
}

function Login([string]$identifier, [string]$password) {
    try {
        $body = @{ Identifier = $identifier; Password = $password } | ConvertTo-Json
        $resp = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $body
        return $resp.token
    } catch { return $null }
}

# JSON POST returning status + raw body (for leak/message checks).
function JsonPost([string]$url, $headers, [string]$jsonBody) {
    try {
        $resp = Invoke-WebRequest -Uri $url -Method Post -Headers $headers -ContentType 'application/json' -Body $jsonBody -UseBasicParsing
        return @{ Status = [int]$resp.StatusCode; Raw = $resp.Content }
    } catch {
        $status = 0; $raw = $null
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try { $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); $raw = $reader.ReadToEnd() } catch { }
        }
        return @{ Status = $status; Raw = $raw }
    }
}

# multipart POST via curl.exe (reliable for file uploads in PS 5.1)
function MultipartPost([string]$url, [string]$token, [string]$filePath, [string]$fileContentType, [string]$fieldName = 'audio') {
    $cargs = @('-s', '-o', '-', '-w', '%{http_code}', '-X', 'POST', $url)
    if ($token) { $cargs += @('-H', "Authorization: Bearer $token") }
    $cargs += @('-F', "${fieldName}=@${filePath};type=${fileContentType}")
    $raw = (& curl.exe @cargs) -join ''
    if ($raw.Length -lt 3) { return @{ Status = 0; Raw = $raw } }
    $status = [int]$raw.Substring($raw.Length - 3)
    return @{ Status = $status; Raw = $raw.Substring(0, $raw.Length - 3) }
}

# multipart POST without the file field
function MultipartPostNoFile([string]$url, [string]$token, [hashtable]$fields) {
    $cargs = @('-s', '-o', '-', '-w', '%{http_code}', '-X', 'POST', $url)
    if ($token) { $cargs += @('-H', "Authorization: Bearer $token") }
    foreach ($k in $fields.Keys) { $cargs += @('-F', "$k=$($fields[$k])") }
    $raw = (& curl.exe @cargs) -join ''
    if ($raw.Length -lt 3) { return @{ Status = 0; Raw = $raw } }
    $status = [int]$raw.Substring($raw.Length - 3)
    return @{ Status = $status; Raw = $raw.Substring(0, $raw.Length - 3) }
}

function GetRaw([string]$url, $headers = @{}) {
    try { return (Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing).Content } catch { return $null }
}

function AsArray($x) {
    if ($null -eq $x) { return , @() }
    if ($x -is [System.Object[]]) {
        if ($x.Count -eq 1 -and $null -eq $x[0]) { return , @() }
        return , $x
    }
    return , @($x)
}

function EnsureFarm($token, $name, $withGps) {
    $raw = GetRaw "$base/api/farms" @{ Authorization = "Bearer $token" }
    $farms = AsArray ($raw | ConvertFrom-Json)
    $existing = @($farms | Where-Object { $_.farmName -eq $name }) | Select-Object -First 1
    if ($existing) { return $existing.id }
    $body = @{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        FarmSize = 5; FarmSizeUnit = 'Acres'; SoilType = 'Loamy'; IrrigationType = 'Canal'
    }
    if ($withGps) { $body.Latitude = 31.5204; $body.Longitude = 74.3587 }
    $created = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    return $created.id
}

function CreateCrop($token, $farmId, $name, $catalogId) {
    $body = @{ CropName = $name; Season = 'Rabi'; CropCatalogId = $catalogId; GrowthStage = 'Vegetative' }
    return Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
}

function DeleteAgCrops($headers, $farmId) {
    $raw = GetRaw "$base/api/farms/$farmId/crops" $headers
    if (-not $raw) { return }
    foreach ($c in AsArray ($raw | ConvertFrom-Json)) {
        if ($c.cropName -like 'AG *') {
            try { Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers $headers -UseBasicParsing | Out-Null } catch { }
        }
    }
}

# Generate a small valid WAV file (mono 16-bit PCM, ~1 second).
function MakeWav([string]$path) {
    $sampleRate = 8000; $samples = 8000; $dataSize = $samples * 2
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
    $bw.Write([int](36 + $dataSize))
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('WAVE'))
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('fmt '))
    $bw.Write([int]16); $bw.Write([int16]1); $bw.Write([int16]1)
    $bw.Write([int]$sampleRate); $bw.Write([int]($sampleRate * 2))
    $bw.Write([int16]2); $bw.Write([int16]16)
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
    $bw.Write([int]$dataSize)
    for ($i = 0; $i -lt $samples; $i++) { $bw.Write([int16]([Math]::Sin($i / 8.0) * 3000)) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
}

Write-Host "`n=== SABZ Prompt 13: Voice-First AI Agronomist Tests ===" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Setup
# -----------------------------------------------------------------------------
Write-Host "`n--- Setup ---"
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
Check 'SETUP.1 User A login' ([bool]$tokenA)
Check 'SETUP.2 User B login' ([bool]$tokenB)
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }

$farmMain  = EnsureFarm $tokenA 'AG Main Farm' $true
$farmNoGps = EnsureFarm $tokenA 'AG No-GPS Farm' $false
$farmB     = EnsureFarm $tokenB 'AG User-B Farm' $true
Check 'SETUP.3 AG farms ready' ($farmMain -and $farmNoGps -and $farmB)

# Ahmed seed-farm guard snapshot (must remain untouched).
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: remove leftover AG crops/farms from any previous run, recreate.
foreach ($f in @($farmMain, $farmNoGps)) { DeleteAgCrops $hdrA $f; Invoke-WebRequest -Uri "$base/api/farms/$f" -Method Delete -Headers $hdrA -UseBasicParsing -ErrorAction SilentlyContinue | Out-Null }
DeleteAgCrops $hdrB $farmB; Invoke-WebRequest -Uri "$base/api/farms/$farmB" -Method Delete -Headers $hdrB -UseBasicParsing -ErrorAction SilentlyContinue | Out-Null

$farmMain  = EnsureFarm $tokenA 'AG Main Farm' $true
$farmNoGps = EnsureFarm $tokenA 'AG No-GPS Farm' $false
$farmB     = EnsureFarm $tokenB 'AG User-B Farm' $true
Check 'SETUP.4 AG farms recreated fresh' ($farmMain -and $farmNoGps -and $farmB)

$cropA = CreateCrop $tokenA $farmMain 'AG Wheat' 1
Check 'SETUP.5 crop A created' ([bool]$cropA.id)

# Audio fixtures.
$tmpDir = $env:TEMP
$wavAudio    = Join-Path $tmpDir 'ag-test-voice.wav'
$emptyAudio  = Join-Path $tmpDir 'ag-test-empty.wav'
$txtFile     = Join-Path $tmpDir 'ag-test-notes.txt'
$oversized   = Join-Path $tmpDir 'ag-test-oversized.wav'
MakeWav $wavAudio
[System.IO.File]::WriteAllBytes($emptyAudio, [byte[]]@())
Set-Content -Path $txtFile -Value 'this is not audio'
$big = New-Object byte[] 10550000
$sig = [System.Text.Encoding]::ASCII.GetBytes('RIFF____WAVE')
[Array]::Copy($sig, $big, $sig.Length)
[System.IO.File]::WriteAllBytes($oversized, $big)
Check 'SETUP.6 audio fixtures generated' ((Test-Path $wavAudio) -and (Test-Path $oversized))

$chat  = "$base/api/farms/$farmMain/agronomist/chat"
$voice = "$base/api/farms/$farmMain/agronomist/voice"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: authentication ---"
$r = JsonPost $chat @{} (@{ message = 'How much water does wheat need?' } | ConvertTo-Json)
Check 'T1.1 chat without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = JsonPost $chat @{ Authorization = 'Bearer not.a.real.token' } (@{ message = 'test' } | ConvertTo-Json)
Check 'T1.2 chat malformed token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = MultipartPost $voice $null $wavAudio 'audio/wav'
Check 'T1.3 voice without token -> 401' ($r.Status -eq 401) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: ownership ---"
$ghost = [Guid]::NewGuid()
$r = JsonPost "$base/api/farms/$ghost/agronomist/chat" $hdrA (@{ message = 'test' } | ConvertTo-Json)
Check 'T2.1 chat unknown farm -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = JsonPost "$base/api/farms/$farmB/agronomist/chat" $hdrA (@{ message = 'test' } | ConvertTo-Json)
Check 'T2.2 chat another user''s farm -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = MultipartPost "$base/api/farms/$ghost/agronomist/voice" $tokenA $wavAudio 'audio/wav'
Check 'T2.3 voice unknown farm -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = MultipartPost "$base/api/farms/$farmB/agronomist/voice" $tokenA $wavAudio 'audio/wav'
Check 'T2.4 voice another user''s farm -> 403' ($r.Status -eq 403) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: text validation ---"
$r = JsonPost $chat $hdrA (@{ message = '' } | ConvertTo-Json)
Check 'T3.1 empty question -> 400' ($r.Status -eq 400) "got $($r.Status)"
$r = JsonPost $chat $hdrA (@{ message = '    ' } | ConvertTo-Json)
Check 'T3.2 whitespace-only question -> 400' ($r.Status -eq 400) "got $($r.Status)"
$r = JsonPost $chat $hdrA (@{ } | ConvertTo-Json)
Check 'T3.3 missing message field -> 400' ($r.Status -eq 400) "got $($r.Status)"
$longQ = 'a' * 1500
$r = JsonPost $chat $hdrA (@{ message = $longQ } | ConvertTo-Json)
Check 'T3.4 overlong question (>1000) -> 400' ($r.Status -eq 400) "got $($r.Status)"
Check 'T3.5 overlong message mentions length' ($r.Raw -match 'too long|character')

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: valid text question (NOT-CONFIGURED provider mode) ---"
$r = JsonPost $chat $hdrA (@{ message = 'How much water does wheat need in Rabi season?' } | ConvertTo-Json)
Check 'T4.1 valid question reaches provider gate -> 502' ($r.Status -eq 502) "got $($r.Status) $($r.Raw)"
Check 'T4.2 message explains not configured' ($r.Raw -match 'not configured')
Check 'T4.3 no fabricated answer in failure path' ($r.Raw -notmatch '"answer"\s*:\s*"[A-Za-z0-9]')
Check 'T4.4 no userId leakage' ($r.Raw -notmatch '(?i)"userId"')
Check 'T4.5 no ownerId leakage' ($r.Raw -notmatch '(?i)"ownerId"')
# Urdu question is accepted and also reaches the provider gate (language detected server-side).
$r = JsonPost $chat $hdrA (@{ message = 'گندم کو کتنی پانی کی ضرورت ہوتی ہے؟' } | ConvertTo-Json)
Check 'T4.6 Urdu question accepted (reaches provider gate 502)' ($r.Status -eq 502) "got $($r.Status)"
# [LIVE-PROVIDER] with a real key this must return 200 with question, answer, language,
# farmContextUsed, limitations, disclaimer and generatedAt; language must be "ur" for the
# Urdu question and "en" for the English one.

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: voice validation ---"
$r = MultipartPostNoFile $voice $tokenA @{ note = 'no audio attached' }
Check 'T5.1 missing audio -> 400' ($r.Status -eq 400) "got $($r.Status) $($r.Raw)"
$r = MultipartPost $voice $tokenA $emptyAudio 'audio/wav'
Check 'T5.2 empty audio -> 400' ($r.Status -eq 400) "got $($r.Status)"
$r = MultipartPost $voice $tokenA $txtFile 'text/plain'
Check 'T5.3 unsupported type -> 400' ($r.Status -eq 400) "got $($r.Status)"
Check 'T5.4 unsupported message mentions format' ($r.Raw -match 'format|audio')
try {
    $r = MultipartPost $voice $tokenA $oversized 'audio/wav'
    Check 'T5.5 oversized audio rejected (400 or 413)' ($r.Status -eq 400 -or $r.Status -eq 413) "got $($r.Status)"
} catch {
    Check 'T5.5 oversized rejected (connection refused by server limits)' $true
}

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: valid voice question (NOT-CONFIGURED provider mode) ---"
$r = MultipartPost $voice $tokenA $wavAudio 'audio/wav'
Check 'T6.1 valid audio reaches STT gate -> 502' ($r.Status -eq 502) "got $($r.Status) $($r.Raw)"
Check 'T6.2 message explains not configured' ($r.Raw -match 'not configured')
Check 'T6.3 no fabricated transcription' ($r.Raw -notmatch '"transcription"\s*:\s*"[A-Za-z0-9]')
# [LIVE-PROVIDER] with a real key this must return 200 with transcription + answer;
# the transcription is used as the question, and the farm context belongs to the caller.

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: read-only guarantee (no side effects) ---"
$cropsBefore  = (SqlQuery "SELECT COUNT(*) FROM Crops") -join ''
$txBefore     = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions") -join ''
$chkBefore    = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks") -join ''
$notifBefore  = (SqlQuery "SELECT COUNT(*) FROM Notifications") -join ''
$farmBefore   = (SqlQuery "SELECT COUNT(*) FROM Farms") -join ''

# Hit the assistant repeatedly (text + voice) - counts must not change.
JsonPost $chat $hdrA (@{ message = 'What fertilizer is best for wheat?' } | ConvertTo-Json) | Out-Null
JsonPost $chat $hdrA (@{ message = 'گندم کی بیماری' } | ConvertTo-Json) | Out-Null
MultipartPost $voice $tokenA $wavAudio 'audio/wav' | Out-Null
JsonPost $chat $hdrA (@{ message = 'Repeat question one' } | ConvertTo-Json) | Out-Null

$cropsAfter = (SqlQuery "SELECT COUNT(*) FROM Crops") -join ''
$txAfter    = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions") -join ''
$chkAfter   = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks") -join ''
$notifAfter = (SqlQuery "SELECT COUNT(*) FROM Notifications") -join ''
$farmAfter  = (SqlQuery "SELECT COUNT(*) FROM Farms") -join ''

Check 'T7.1 assistant creates no crops'        ($cropsBefore -eq $cropsAfter) "before=$cropsBefore after=$cropsAfter"
Check 'T7.2 assistant creates no transactions' ($txBefore -eq $txAfter)       "before=$txBefore after=$txAfter"
Check 'T7.3 assistant changes no monitoring'   ($chkBefore -eq $chkAfter)     "before=$chkBefore after=$chkAfter"
Check 'T7.4 assistant creates no notifications' ($notifBefore -eq $notifAfter) "before=$notifBefore after=$notifAfter"
Check 'T7.5 assistant creates no farms'        ($farmBefore -eq $farmAfter)   "before=$farmBefore after=$farmAfter"
# No chat history table exists (read-only, no persisted conversations).
# Marketplace tables (Prompt 15) contain 'Conversations' but belong to the
# marketplace inbox, not to the agronomist assistant.
$chatTable = (SqlQuery "SELECT COUNT(*) FROM sys.tables WHERE (name LIKE '%Chat%' OR name LIKE '%Conversation%' OR name LIKE '%Assistant%') AND name NOT LIKE 'Marketplace%'") -join ''
Check 'T7.6 no chat-history/assistant table added' ($chatTable -eq '0') "found=$chatTable"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: farm-context ownership isolation ---"
# User B asks about their OWN farm: only user B's farm context may be used. In
# provider mode this still gates at 502, but ownership must resolve to B's farm.
$r = JsonPost "$base/api/farms/$farmB/agronomist/chat" $hdrB (@{ message = 'How do I improve my soil?' } | ConvertTo-Json)
Check 'T8.1 user B reaches provider gate on own farm -> 502' ($r.Status -eq 502) "got $($r.Status)"
# User A can never touch user B's farm context (403 above in T2.2).
# [LIVE-PROVIDER] with a real key the farmContextUsed.farmId must equal the caller's
# farmId and never another user's farm.

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: cleanup and integrity ---"
foreach ($f in @($farmMain, $farmNoGps)) {
    DeleteAgCrops $hdrA $f
    Invoke-WebRequest -Uri "$base/api/farms/$f" -Method Delete -Headers $hdrA -UseBasicParsing -ErrorAction SilentlyContinue | Out-Null
}
DeleteAgCrops $hdrB $farmB
Invoke-WebRequest -Uri "$base/api/farms/$farmB" -Method Delete -Headers $hdrB -UseBasicParsing -ErrorAction SilentlyContinue | Out-Null

$leftFarms  = (SqlQuery "SELECT COUNT(*) FROM Farms WHERE FarmName LIKE 'AG %'") -join ''
$leftCrops  = (SqlQuery "SELECT COUNT(*) FROM Crops WHERE CropName LIKE 'AG %'") -join ''
$tableCount = (SqlQuery "SELECT COUNT(*) FROM sys.tables") -join ''
$migCount   = (SqlQuery "SELECT COUNT(*) FROM __EFMigrationsHistory") -join ''

Check 'T9.1 no AG farms left' ($leftFarms -eq '0') "left=$leftFarms"
Check 'T9.2 no AG crops left' ($leftCrops -eq '0') "left=$leftCrops"
Check 'T9.3 table count unchanged (21 incl. history)' ($tableCount -eq '21') "count=$tableCount"
Check 'T9.4 migration count unchanged (11)' ($migCount -eq '11') "count=$migCount"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T9.5 Ahmed seed farm untouched' ($ahmedBefore -eq $ahmedAfter)

# -----------------------------------------------------------------------------
Write-Host "`n=== Prompt 13 results: $pass passed, $fail failed (total $($pass + $fail)) ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Remove-Item $wavAudio, $emptyAudio, $txtFile, $oversized -Force -ErrorAction SilentlyContinue
if ($fail -gt 0) { exit 1 } else { exit 0 }
