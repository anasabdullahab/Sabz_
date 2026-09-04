# =============================================================================
# SABZ Prompt 7 - Smart Crop Monitoring Schedule & Farmer Reminder Foundation
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# NO AI provider key needed, NO live AI inference: a "SomethingSuspicious"
# observation only RECOMMENDS the existing Prompt 6 photo workflow.
#
# Idempotency strategy: every run deletes leftover "MON " fixture crops first
# (monitoring checks cascade-delete with crops), then recreates fixtures with
# planting dates relative to today, so scheduled/due/upcoming states are
# deterministic on every run. Seed/reference data is never touched.
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
    $tmp = Join-Path $env:TEMP ('sazbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# Invoke-WebRequest-based call: returns @{ Status; Data; Error }
function ApiCall([string]$method, [string]$url, $headers = @{}, $body = $null, $contentType = $null) {
    try {
        $params = @{ Uri = $url; Method = $method; UseBasicParsing = $true; Headers = $headers }
        if ($null -ne $body) { $params.Body = $body }
        if ($contentType) { $params.ContentType = $contentType }
        $resp = Invoke-WebRequest @params
        $data = $null
        if ($resp.Content) { try { $data = $resp.Content | ConvertFrom-Json } catch { $data = $resp.Content } }
        return @{ Status = [int]$resp.StatusCode; Data = $data; Error = $null }
    } catch {
        $status = 0
        $data = $null
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $raw = $reader.ReadToEnd()
                if ($raw) { $data = $raw | ConvertFrom-Json }
            } catch { }
        }
        return @{ Status = $status; Data = $data; Error = $_.Exception.Message }
    }
}

# multipart POST via curl.exe (reliable for file uploads in PS 5.1)
function MultipartPost([string]$url, [string]$token, [string]$filePath, [string]$fileContentType) {
    $cargs = @('-s', '-o', '-', '-w', '%{http_code}', '-X', 'POST', $url)
    if ($token) { $cargs += @('-H', "Authorization: Bearer $token") }
    $cargs += @('-F', "image=@${filePath};type=${fileContentType}")
    $raw = (& curl.exe @cargs) -join ''
    if ($raw.Length -lt 3) { return @{ Status = 0; Raw = $raw } }
    $status = [int]$raw.Substring($raw.Length - 3)
    return @{ Status = $status; Raw = $raw.Substring(0, $raw.Length - 3) }
}

# GET returning JSON - uses Invoke-WebRequest + ConvertFrom-Json so arrays are
# never silently unwrapped (Invoke-RestMethod's unwrapping corrupts list loops).
function GetJson([string]$url, $headers) {
    $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing
    return ($resp.Content | ConvertFrom-Json)
}

function EnsureFarm($token, $name) {
    $farms = @(GetJson "$base/api/farms" @{ Authorization = "Bearer $token" })
    $existing = @($farms | Where-Object { $_.farmName -eq $name }) | Select-Object -First 1
    if ($existing) { return $existing.id }
    $body = @{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        FarmSize = 5; FarmSizeUnit = 'Acres'; SoilType = 'Loamy'; IrrigationType = 'Canal'
    }
    $created = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    return $created.id
}

function CreateCrop($token, $farmId, $name, $catalogId, $plantingDateIso) {
    $body = @{ CropName = $name; Season = 'Rabi'; CropCatalogId = $catalogId }
    if ($plantingDateIso) { $body.PlantingDate = $plantingDateIso }
    return Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
}

function DeleteMonCrops($token, $farmId) {
    try { $crops = @(GetJson "$base/api/farms/$farmId/crops" @{ Authorization = "Bearer $token" }) } catch { return }
    # Guard against a nested array (member-enumeration artefact on some hosts).
    if ($crops.Count -eq 1 -and $crops[0] -is [System.Array]) { $crops = @($crops[0]) }
    $mons = @($crops | Where-Object { $_.cropName -like 'MON *' })
    foreach ($c in $mons) {
        try {
            Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing | Out-Null
        } catch {
            # 404 = already gone (e.g. removed by an earlier test); anything else is reported.
            $code = 0
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            if ($code -ne 404) { Write-Host "  WARN  cleanup delete '$($c.cropName)' -> $code" -ForegroundColor Yellow }
        }
    }
}

Write-Host "`n=== SABZ Prompt 7: Crop Monitoring Tests ===" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Setup: logins, farms, fixture crops (dates relative to today => deterministic)
# -----------------------------------------------------------------------------
Write-Host "`n--- Setup ---"
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
Check 'SETUP.1 User A login' ([bool]$tokenA)
Check 'SETUP.2 User B login' ([bool]$tokenB)
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }

$farmA = EnsureFarm $tokenA 'Monitoring Test Farm'
$farmB = EnsureFarm $tokenB 'MON User-B Test Farm'
Check 'SETUP.3 Farm A ready' ([bool]$farmA) "farmA=$farmA"
Check 'SETUP.4 Farm B ready' ([bool]$farmB)

# Ahmed Farm guard snapshot (must remain untouched)
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: remove leftover fixtures from previous runs (checks cascade-delete)
DeleteMonCrops $tokenA $farmA
DeleteMonCrops $tokenB $farmB

$today = (Get-Date).ToUniversalTime().Date
$pdWheat = $today.AddDays(-40).ToString('yyyy-MM-dd')   # wheat offsets 14/30 due, 60 upcoming
$pdFuture = $today.AddDays(30).ToString('yyyy-MM-dd')   # all wheat checks upcoming
$pdSugarcane = $today.AddDays(-10).ToString('yyyy-MM-dd')
$pdUserB = $today.AddDays(-20).ToString('yyyy-MM-dd')   # offset 14 due, 30/60 upcoming

$cropWheat   = CreateCrop $tokenA $farmA 'MON Test Wheat' 1 $pdWheat
$cropFuture  = CreateCrop $tokenA $farmA 'MON Future Wheat' 1 $pdFuture
$cropNoDate  = CreateCrop $tokenA $farmA 'MON Wheat No Date' 1 $null
$cropSugar   = CreateCrop $tokenA $farmA 'MON Sugarcane No Rules' 4 $pdSugarcane
$cropB       = CreateCrop $tokenB $farmB 'MON Wheat User-B' 1 $pdUserB

Check 'SETUP.5 wheat crop created (with planting date)' ([bool]$cropWheat.id)
Check 'SETUP.6 future wheat crop created' ([bool]$cropFuture.id)
Check 'SETUP.7 no-planting-date crop created (creation not broken)' ([bool]$cropNoDate.id)
Check 'SETUP.8 no-rules crop created' ([bool]$cropSugar.id)
Check 'SETUP.9 user B crop created' ([bool]$cropB.id)
Check 'SETUP.10 planting date persisted' (([datetime]$cropWheat.plantingDate).Date -eq $today.AddDays(-40)) "got $($cropWheat.plantingDate)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: automatic generation on crop creation ---"
$r = ApiCall 'GET' "$base/api/crops/$($cropWheat.id)/monitoring" $hdrA
$wChecks = @($r.Data) | Sort-Object -Property scheduledDate
Check 'T1.1 crop creation auto-generated 3 checks (wheat has 3 rules)' ($r.Status -eq 200 -and $wChecks.Count -eq 3) "status=$($r.Status) count=$($wChecks.Count)"
$expDates = @($today.AddDays(-26), $today.AddDays(-10), $today.AddDays(20))
$datesOk = $true
for ($i = 0; $i -lt 3; $i++) {
    if (([datetime]$wChecks[$i].scheduledDate).Date -ne $expDates[$i]) { $datesOk = $false }
}
Check 'T1.2 scheduled dates = planting date + rule offsets (14/30/60)' $datesOk
Check 'T1.3 rule content snapshotted (title from seed rule)' ($wChecks[0].title -eq 'Early growth and emergence check') "got '$($wChecks[0].title)'"
Check 'T1.4 computed statuses: two Due + one Upcoming' (($wChecks[0].status -eq 'Due') -and ($wChecks[1].status -eq 'Due') -and ($wChecks[2].status -eq 'Upcoming'))
Check 'T1.5 inspection items are a list' (@($wChecks[0].inspectionItems).Count -gt 1)
Check 'T1.6 checks carry crop + farm context' (($wChecks[0].cropName -eq 'MON Test Wheat') -and ($wChecks[0].farmId -eq $farmA))

$r = ApiCall 'GET' "$base/api/crops/$($cropFuture.id)/monitoring" $hdrA
$fChecks = @($r.Data)
Check 'T1.7 future crop: 3 checks, all Upcoming' ($fChecks.Count -eq 3 -and @($fChecks | Where-Object { $_.status -ne 'Upcoming' }).Count -eq 0) "count=$($fChecks.Count)"

$r = ApiCall 'GET' "$base/api/crops/$($cropNoDate.id)/monitoring" $hdrA
Check 'T1.8 crop without planting date: creation OK, zero checks' ($r.Status -eq 200 -and @($r.Data).Count -eq 0) "status=$($r.Status)"

$r = ApiCall 'GET' "$base/api/crops/$($cropB.id)/monitoring" $hdrB
$bChecks = @($r.Data) | Sort-Object -Property scheduledDate
Check 'T1.9 user B crop auto-generated 3 checks' ($bChecks.Count -eq 3) "count=$($bChecks.Count)"
Check 'T1.10 user B first check is Due (planted 20 days ago, offset 14)' ($bChecks[0].status -eq 'Due') "got $($bChecks[0].status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: idempotent generation endpoint (safe for existing crops) ---"
$r = ApiCall 'POST' "$base/api/crops/$($cropWheat.id)/monitoring/generate" $hdrA
Check 'T2.1 generate on already-generated crop creates nothing' ($r.Status -eq 200 -and $r.Data.checksCreated -eq 0) "status=$($r.Status) created=$($r.Data.checksCreated)"
Check 'T2.2 reports existing checks (3)' ($r.Data.existingChecks -eq 3) "existing=$($r.Data.existingChecks)"
Check 'T2.3 note explains nothing was duplicated' (($r.Data.notes -join ' ') -match 'nothing was duplicated')

$r = ApiCall 'POST' "$base/api/crops/$($cropWheat.id)/monitoring/generate" $hdrA
$still = ApiCall 'GET' "$base/api/crops/$($cropWheat.id)/monitoring" $hdrA
Check 'T2.4 double-run stays at 3 checks (no duplicates)' ($r.Data.checksCreated -eq 0 -and @($still.Data).Count -eq 3) "count=$(@($still.Data).Count)"

$r = ApiCall 'POST' "$base/api/crops/$($cropNoDate.id)/monitoring/generate" $hdrA
Check 'T2.5 no planting date: honest note, zero checks' ($r.Status -eq 200 -and $r.Data.hasPlantingDate -eq $false -and $r.Data.checksCreated -eq 0 -and (($r.Data.notes -join ' ') -match 'no planting date')) "notes=$($r.Data.notes -join ' ')"

$r = ApiCall 'POST' "$base/api/crops/$($cropSugar.id)/monitoring/generate" $hdrA
Check 'T2.6 crop with no rules: rulesApplied=0, honest note' ($r.Status -eq 200 -and $r.Data.rulesApplied -eq 0 -and $r.Data.checksCreated -eq 0 -and (($r.Data.notes -join ' ') -match 'No active monitoring rules')) "notes=$($r.Data.notes -join ' ')"

$r = ApiCall 'POST' "$base/api/crops/$($cropB.id)/monitoring/generate" $hdrB
Check 'T2.7 generation idempotent for user B too' ($r.Status -eq 200 -and $r.Data.checksCreated -eq 0 -and $r.Data.existingChecks -eq 3) "created=$($r.Data.checksCreated)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: due / upcoming lists (UTC-computed, user-scoped) ---"
$nowUtc = [DateTime]::UtcNow
$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
$dueA = @($r.Data)
Check 'T3.1 due list has exactly the 2 overdue wheat checks' ($r.Status -eq 200 -and $dueA.Count -eq 2) "status=$($r.Status) count=$($dueA.Count)"
Check 'T3.2 every due item is status Due with scheduledDate <= now' (@($dueA | Where-Object { $_.status -ne 'Due' -or ([datetime]$_.scheduledDate) -gt $nowUtc }).Count -eq 0)
Check 'T3.3 due list sorted soonest first' (([datetime]$dueA[0].scheduledDate) -le ([datetime]$dueA[1].scheduledDate))

$r = ApiCall 'GET' "$base/api/monitoring/upcoming" $hdrA
$upA = @($r.Data)
Check 'T3.4 upcoming list has 4 scheduled-future checks' ($upA.Count -eq 4) "count=$($upA.Count)"
Check 'T3.5 every upcoming item is Upcoming with scheduledDate > now' (@($upA | Where-Object { $_.status -ne 'Upcoming' -or ([datetime]$_.scheduledDate) -le $nowUtc }).Count -eq 0)
$futureCropIds = @($upA | Where-Object { $_.cropId -eq $cropFuture.id })
Check 'T3.6 future crop contributes its 3 upcoming checks' ($futureCropIds.Count -eq 3)

$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrB
$dueB = @($r.Data)
Check 'T3.7 user B due list isolated (only B crop, none of A)' ($dueB.Count -eq 1 -and $dueB[0].cropId -eq $cropB.id -and @($dueB | Where-Object { $_.cropId -eq $cropWheat.id }).Count -eq 0) "count=$($dueB.Count)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: authentication and ownership (401 / 404 / 403) ---"
$anon = @{}
$r = ApiCall 'GET' "$base/api/crops/$($cropWheat.id)/monitoring" $anon
Check 'T4.1 crop monitoring without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/monitoring/due" $anon
Check 'T4.2 due list without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/monitoring/upcoming" $anon
Check 'T4.3 upcoming list without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$([Guid]::NewGuid())/complete" $anon (@{ Observation = 'Normal' } | ConvertTo-Json) 'application/json'
Check 'T4.4 complete without token -> 401' ($r.Status -eq 401) "got $($r.Status)"

$r = ApiCall 'GET' "$base/api/crops/$([Guid]::NewGuid())/monitoring" $hdrA
Check 'T4.5 unknown crop -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/crops/$([Guid]::NewGuid())/monitoring/generate" $hdrA
Check 'T4.6 generate for unknown crop -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/crops/$($cropB.id)/monitoring" $hdrA
Check 'T4.7 another farmer''s crop -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/crops/$($cropB.id)/monitoring/generate" $hdrA
Check 'T4.8 generate on another farmer''s crop -> 403' ($r.Status -eq 403) "got $($r.Status)"

$someCheckId = $wChecks[0].id
$r = ApiCall 'POST' "$base/api/monitoring/$([Guid]::NewGuid())/complete" $hdrA (@{ Observation = 'Normal' } | ConvertTo-Json) 'application/json'
Check 'T4.9 complete unknown check -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$([Guid]::NewGuid())/skip" $hdrA (@{ } | ConvertTo-Json) 'application/json'
Check 'T4.10 skip unknown check -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$someCheckId/complete" $hdrB (@{ Observation = 'Normal' } | ConvertTo-Json) 'application/json'
Check 'T4.11 complete another farmer''s check -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$someCheckId/skip" $hdrB (@{ } | ConvertTo-Json) 'application/json'
Check 'T4.12 skip another farmer''s check -> 403' ($r.Status -eq 403) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: complete with observation Normal ---"
$c1 = $wChecks[0]   # due, offset 14
$r = ApiCall 'POST' "$base/api/monitoring/$($c1.id)/complete" $hdrA (@{ Observation = 'Normal'; Notes = 'even emergence, no issues' } | ConvertTo-Json) 'application/json'
Check 'T5.1 complete Normal -> 200' ($r.Status -eq 200) "got $($r.Status)"
Check 'T5.2 Normal: photo analysis NOT recommended' ($r.Data.photoAnalysisRecommended -eq $false)
Check 'T5.3 Normal: next action says continue schedule' ($r.Data.nextAction -match 'No further action') "got '$($r.Data.nextAction)'"
Check 'T5.4 observation note states it is not a diagnosis' ($r.Data.observationNote -match 'not a disease diagnosis')
Check 'T5.5 check marked Completed with observation + notes + timestamp' (($r.Data.check.status -eq 'Completed') -and ($r.Data.check.observation -eq 'Normal') -and ($r.Data.check.farmerNotes -eq 'even emergence, no issues') -and ($null -ne $r.Data.check.completedAt))

$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
Check 'T5.6 completed check no longer due' (@(@($r.Data) | Where-Object { $_.id -eq $c1.id }).Count -eq 0)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: complete with SomethingSuspicious (Prompt 6 hand-off, no AI call) ---"
$c2 = $wChecks[1]   # due, offset 30
$r = ApiCall 'POST' "$base/api/monitoring/$($c2.id)/complete" $hdrA (@{ Observation = 'SomethingSuspicious'; Notes = 'yellow spots on lower leaves' } | ConvertTo-Json) 'application/json'
Check 'T6.1 complete SomethingSuspicious -> 200' ($r.Status -eq 200) "got $($r.Status)"
Check 'T6.2 suspicious: photoAnalysisRecommended=true' ($r.Data.photoAnalysisRecommended -eq $true)
Check 'T6.3 next action points to existing photo/disease-detection workflow' (($r.Data.nextAction -match 'photo') -and ($r.Data.nextAction -match 'disease-detection')) "got '$($r.Data.nextAction)'"
Check 'T6.4 check dto itself flags photo recommendation' ($r.Data.check.photoAnalysisRecommended -eq $true)
Check 'T6.5 observation recorded verbatim (not a diagnosis)' (($r.Data.check.observation -eq 'SomethingSuspicious') -and ($r.Data.observationNote -match 'not a disease diagnosis'))

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: validation, conflicts, skip lifecycle ---"
$f1 = ($fChecks | Sort-Object -Property scheduledDate)[0]
$f2 = ($fChecks | Sort-Object -Property scheduledDate)[1]
$f3 = ($fChecks | Sort-Object -Property scheduledDate)[2]

$r = ApiCall 'POST' "$base/api/monitoring/$($f1.id)/complete" $hdrA (@{ Observation = 'Very Good' } | ConvertTo-Json) 'application/json'
Check 'T7.1 invalid observation -> 400' ($r.Status -eq 400) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$($f1.id)/complete" $hdrA (@{ Observation = '' } | ConvertTo-Json) 'application/json'
Check 'T7.2 empty observation -> 400' ($r.Status -eq 400) "got $($r.Status)"
$longNotes = 'x' * 1001
$r = ApiCall 'POST' "$base/api/monitoring/$($f1.id)/complete" $hdrA (@{ Observation = 'Normal'; Notes = $longNotes } | ConvertTo-Json) 'application/json'
Check 'T7.3 notes over 1000 chars -> 400' ($r.Status -eq 400) "got $($r.Status)"

$r = ApiCall 'POST' "$base/api/monitoring/$($c1.id)/complete" $hdrA (@{ Observation = 'Normal' } | ConvertTo-Json) 'application/json'
Check 'T7.4 duplicate completion rejected -> 409' ($r.Status -eq 409) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$($c1.id)/skip" $hdrA (@{ } | ConvertTo-Json) 'application/json'
Check 'T7.5 skip after complete rejected -> 409' ($r.Status -eq 409) "got $($r.Status)"

$r = ApiCall 'POST' "$base/api/monitoring/$($f2.id)/skip" $hdrA (@{ Notes = 'out of town this week' } | ConvertTo-Json) 'application/json'
Check 'T7.6 skip scheduled check -> 200 with Skipped status + timestamp' ($r.Status -eq 200 -and $r.Data.status -eq 'Skipped' -and ($null -ne $r.Data.skippedAt) -and ($r.Data.farmerNotes -eq 'out of town this week')) "status=$($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$($f2.id)/skip" $hdrA (@{ } | ConvertTo-Json) 'application/json'
Check 'T7.7 skip after skip rejected -> 409' ($r.Status -eq 409) "got $($r.Status)"
$r = ApiCall 'POST' "$base/api/monitoring/$($f2.id)/complete" $hdrA (@{ Observation = 'Normal' } | ConvertTo-Json) 'application/json'
Check 'T7.8 complete after skip rejected -> 409' ($r.Status -eq 409) "got $($r.Status)"

$r = ApiCall 'POST' "$base/api/monitoring/$($f3.id)/complete" $hdrA (@{ Observation = 'normal' } | ConvertTo-Json) 'application/json'
Check 'T7.9 observation is case-insensitive ("normal" accepted)' ($r.Status -eq 200 -and $r.Data.check.observation -eq 'Normal') "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: final state - due/upcoming correctness after farmer actions ---"
$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
Check 'T8.1 user A due list now empty (completed/skipped never due)' ($r.Status -eq 200 -and @($r.Data).Count -eq 0) "count=$(@($r.Data).Count)"
$r = ApiCall 'GET' "$base/api/monitoring/upcoming" $hdrA
$upFinal = @($r.Data)
Check 'T8.2 upcoming now exactly 2 (wheat offset-60 + future offset-14)' ($upFinal.Count -eq 2) "count=$($upFinal.Count)"
Check 'T8.3 skipped check absent from upcoming and due' (@($upFinal | Where-Object { $_.id -eq $f2.id }).Count -eq 0)

$r = ApiCall 'GET' "$base/api/crops/$($cropWheat.id)/monitoring" $hdrA
$statesW = @(@($r.Data) | Sort-Object -Property scheduledDate | ForEach-Object { $_.status }) -join ','
Check 'T8.4 wheat crop history readable: Completed,Completed,Upcoming' ($statesW -eq 'Completed,Completed,Upcoming') "got $statesW"
$r = ApiCall 'GET' "$base/api/crops/$($cropFuture.id)/monitoring" $hdrA
$statesF = @(@($r.Data) | Sort-Object -Property scheduledDate | ForEach-Object { $_.status }) -join ','
Check 'T8.5 future crop states: Upcoming,Skipped,Completed' ($statesF -eq 'Upcoming,Skipped,Completed') "got $statesF"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: Prompt 6 regression (disease detection, NOT-CONFIGURED mode) ---"
Add-Type -AssemblyName System.Drawing
$leafImg = Join-Path $env:TEMP 'mon-test-leaf.jpg'
$bmp = New-Object System.Drawing.Bitmap(320, 240)
$rnd = New-Object System.Random(42)
for ($y = 0; $y -lt 240; $y += 2) {
    for ($x = 0; $x -lt 320; $x += 2) {
        $c = [System.Drawing.Color]::FromArgb(20 + $rnd.Next(40), 110 + $rnd.Next(60), 30 + $rnd.Next(30))
        $bmp.SetPixel($x, $y, $c)
        $bmp.SetPixel($x + 1, $y, $c)
        $bmp.SetPixel($x, $y + 1, $c)
    }
}
$bmp.Save($leafImg, [System.Drawing.Imaging.ImageFormat]::Jpeg)
$bmp.Dispose()
$txtFile = Join-Path $env:TEMP 'mon-test-notes.txt'
Set-Content -Path $txtFile -Value 'not an image'

$ddEndpoint = "$base/api/farms/$farmA/disease-detection"
$r = MultipartPost $ddEndpoint $tokenA $leafImg 'image/jpeg'
Check 'T9.1 valid image still reaches provider gate 502 (no key configured)' ($r.Status -eq 502) "got $($r.Status)"
Check 'T9.2 no fake AI result (message says not configured)' ($r.Raw -match 'not configured')
$r = MultipartPost $ddEndpoint $tokenA $txtFile 'image/png'
Check 'T9.3 non-image still rejected -> 400' ($r.Status -eq 400) "got $($r.Status)"
Remove-Item $leafImg, $txtFile -Force -ErrorAction SilentlyContinue

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: Prompt 4/5 regressions (suitability + recommendations) ---"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Rabi" $hdrA
Check 'T10.1 suitability 200' ($r.Status -eq 200) "got $($r.Status)"
$suitCrops = @($r.Data.crops)
Check 'T10.2 suitability returns crops' ($suitCrops.Count -gt 0) "count=$($suitCrops.Count)"
$top = ($suitCrops | Sort-Object -Property suitabilityScore -Descending | Select-Object -First 1)
Check 'T10.3 top suitability score sane (>=70)' ($top.suitabilityScore -ge 70) "top=$($top.suitabilityScore)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Invalid" $hdrA
Check 'T10.4 invalid season still 400' ($r.Status -eq 400) "got $($r.Status)"

$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-recommendations?season=Rabi" $hdrA
Check 'T10.5 recommendations 200' ($r.Status -eq 200) "got $($r.Status)"
$recs = @($r.Data.recommendations)
$validLevels = @('Highly Recommended', 'Recommended', 'Consider', 'Not Recommended')
$badLevels = @($recs | Where-Object { $validLevels -notcontains $_.recommendation })
Check 'T10.6 recommendations returned with valid levels' ($recs.Count -gt 0 -and @($badLevels).Count -eq 0)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 11: crop CRUD regression (create/update/delete + check cascade) ---"
$crud = CreateCrop $tokenA $farmA 'MON CRUD Temp' 1 $today.AddDays(-5).ToString('yyyy-MM-dd')
Check 'T11.1 crop create with planting date 200/201' ([bool]$crud.id)
$r = ApiCall 'GET' "$base/api/crops/$($crud.id)/monitoring" $hdrA
Check 'T11.2 CRUD-created crop also auto-generates 3 checks' (@($r.Data).Count -eq 3) "count=$(@($r.Data).Count)"
$updBody = @{ CropName = 'MON CRUD Temp'; Season = 'Rabi'; CropCatalogId = 1; GrowthStage = 'Vegetative' } | ConvertTo-Json
$r = ApiCall 'PUT' "$base/api/crops/$($crud.id)" $hdrA $updBody 'application/json'
Check 'T11.3 crop update 200' ($r.Status -eq 200) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/crops/$($crud.id)" $hdrA
Check 'T11.4 crop get-by-id reflects update' ($r.Status -eq 200 -and $r.Data.growthStage -eq 'Vegetative') "got $($r.Status)"
$r = ApiCall 'DELETE' "$base/api/crops/$($crud.id)" $hdrA
Check 'T11.5 crop delete 204' ($r.Status -eq 204) "got $($r.Status)"
$cascadeCount = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks WHERE CropId = '$($crud.id)'") -join ''
Check 'T11.6 deleting a crop cascade-deletes its checks' ($cascadeCount -eq '0') "remaining=$cascadeCount"
$r = ApiCall 'GET' "$base/api/crops/$($crud.id)" $hdrA
Check 'T11.7 deleted crop -> 404' ($r.Status -eq 404) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 12: data integrity guards ---"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T12.1 Ahmed Farm untouched' ($ahmedBefore -eq $ahmedAfter) "before=$ahmedBefore after=$ahmedAfter"
$ruleCount = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringRules") -join ''
Check 'T12.2 seed monitoring rules intact (15)' ($ruleCount -eq '15') "count=$ruleCount"
$catalogCount = (SqlQuery "SELECT COUNT(*) FROM CropCatalog") -join ''
Check 'T12.3 crop catalog intact (22)' ($catalogCount -eq '22') "count=$catalogCount"

# -----------------------------------------------------------------------------
Write-Host "`n--- Cleanup: remove fixtures so the script is safe to rerun ---"
DeleteMonCrops $tokenA $farmA
DeleteMonCrops $tokenB $farmB
$orphans = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks WHERE CropId NOT IN (SELECT Id FROM Crops)") -join ''
$leftover = (SqlQuery "SELECT COUNT(*) FROM Crops WHERE CropName LIKE 'MON %'") -join ''
Check 'CLEANUP.1 all fixture crops removed' ($leftover -eq '0') "leftover=$leftover"
Check 'CLEANUP.2 no orphaned monitoring checks' ($orphans -eq '0') "orphans=$orphans"

# -----------------------------------------------------------------------------
Write-Host "`n=== RESULTS: $pass passed, $fail failed (total $($pass + $fail)) ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 } else { exit 0 }
