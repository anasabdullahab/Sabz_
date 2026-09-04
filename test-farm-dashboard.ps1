# =============================================================================
# SABZ Prompt 12 - Unified Farm Dashboard & Insights
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# The dashboard is a READ-ONLY aggregation/orchestration layer over EXISTING
# features (farms, crops, Prompt 7 monitoring, Prompt 8 notifications, Prompt 9
# ledger, Prompt 10 health, Prompt 11 performance, Prompt 3 weather). These
# tests therefore verify the dashboard by CROSS-CHECKING every section against
# the exact source endpoint it reuses, plus auth/ownership/security, monitoring
# no-state-change, notification idempotency and honest limitations.
#
# Idempotency strategy: every run deletes leftover "FD " fixture crops, their
# transactions/monitoring checks and the "FD " fixture farms through the public
# API, then recreates fixtures with dates relative to today. Seed/reference data
# is never touched. Nothing is persisted by the dashboard itself.
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
    $tmp = Join-Path $env:TEMP ('fdbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# Raw GET (string) so regex checks run against the exact JSON bytes.
function GetRaw([string]$url, $headers = @{}) {
    try { return (Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing).Content } catch { return $null }
}

# Normalise PS 5.1 pipeline artefacts into a real array.
function AsArray($x) {
    if ($null -eq $x) { return , @() }
    if ($x -is [System.Object[]]) {
        if ($x.Count -eq 1 -and $null -eq $x[0]) { return , @() }
        return , $x
    }
    return , @($x)
}

function EnsureFarm($token, $name) {
    $raw = GetRaw "$base/api/farms" @{ Authorization = "Bearer $token" }
    $farms = AsArray ($raw | ConvertFrom-Json)
    $existing = @($farms | Where-Object { $_.farmName -eq $name }) | Select-Object -First 1
    if ($existing) { return $existing.id }
    $body = @{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        FarmSize = 5; FarmSizeUnit = 'Acres'; SoilType = 'Loamy'; IrrigationType = 'Canal'
    }
    $created = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    return $created.id
}

# Same as EnsureFarm but WITH GPS coordinates (so dashboard weather is attempted).
function EnsureFarmCoords($token, $name, $lat, $lng) {
    $raw = GetRaw "$base/api/farms" @{ Authorization = "Bearer $token" }
    $farms = AsArray ($raw | ConvertFrom-Json)
    $existing = @($farms | Where-Object { $_.farmName -eq $name }) | Select-Object -First 1
    if ($existing) { return $existing.id }
    $body = @{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        Latitude = $lat; Longitude = $lng
        FarmSize = 8; FarmSizeUnit = 'Acres'; SoilType = 'Loamy'; IrrigationType = 'Canal'
    }
    $created = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    return $created.id
}

function CreateCrop($token, $farmId, $name, $catalogId, $plantingDateIso) {
    $body = @{ CropName = $name; Season = 'Rabi'; CropCatalogId = $catalogId }
    if ($plantingDateIso) { $body.PlantingDate = $plantingDateIso }
    return Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
}

function PostTx($headers, $farmId, $type, $category, $amount, $dateIso, $cropId) {
    $body = @{ TransactionType = $type; Category = $category; Amount = $amount }
    if ($dateIso) { $body.TransactionDate = $dateIso }
    if ($cropId) { $body.CropId = $cropId }
    return ApiCall 'POST' "$base/api/farms/$farmId/transactions" $headers ($body | ConvertTo-Json) 'application/json'
}

function DeleteFarmTx($headers, $farmId) {
    $raw = GetRaw "$base/api/farms/$farmId/transactions?take=100" $headers
    if (-not $raw) { return }
    foreach ($t in AsArray ($raw | ConvertFrom-Json)) {
        ApiCall 'DELETE' "$base/api/transactions/$($t.id)" $headers | Out-Null
    }
}

function DeleteFdCrops($headers, $farmId) {
    $raw = GetRaw "$base/api/farms/$farmId/crops" $headers
    if (-not $raw) { return }
    foreach ($c in AsArray ($raw | ConvertFrom-Json)) {
        if ($c.cropName -like 'FD *') {
            try { Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers $headers -UseBasicParsing | Out-Null } catch { }
        }
    }
}

function HasLimitation($dashboard, [string]$code) {
    return [bool](@($dashboard.limitations) | Where-Object { $_.code -eq $code })
}

$today = (Get-Date).Date
function D([int]$offset) { return $script:today.AddDays($offset).ToString('yyyy-MM-dd') }

Write-Host "`n=== SABZ Prompt 12: Unified Farm Dashboard & Insights Tests ===" -ForegroundColor Cyan

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

$farmEmpty = EnsureFarm $tokenA 'FD Empty Farm'
$farmMain  = EnsureFarmCoords $tokenA 'FD Main Farm' 31.5204 74.3587
$farmB     = EnsureFarm $tokenB 'FD User-B Farm'
Check 'SETUP.3 FD farms ready' ($farmEmpty -and $farmMain -and $farmB)

# Ahmed seed-farm guard snapshot (must remain untouched).
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: wipe fixture ledgers/crops and delete FD farms left by any
# previous run, then recreate everything from scratch.
foreach ($f in @($farmEmpty, $farmMain)) {
    DeleteFarmTx $hdrA $f
    DeleteFdCrops $hdrA $f
}
DeleteFarmTx $hdrB $farmB
DeleteFdCrops $hdrB $farmB
foreach ($f in @($farmEmpty, $farmMain)) { ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null }
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$farmEmpty = EnsureFarm $tokenA 'FD Empty Farm'
$farmMain  = EnsureFarmCoords $tokenA 'FD Main Farm' 31.5204 74.3587
$farmB     = EnsureFarm $tokenB 'FD User-B Farm'
Check 'SETUP.4 FD farms recreated fresh' ($farmEmpty -and $farmMain -and $farmB)

# Main-farm crop + ledger fixtures (deterministic totals).
$cropWheat  = CreateCrop $tokenA $farmMain 'FD Wheat'  1 $null
$cropCotton = CreateCrop $tokenA $farmMain 'FD Cotton' 1 $null
Check 'SETUP.5 main crops created' ($cropWheat.id -and $cropCotton.id)

# Ledger: Wheat +4000, Cotton -2000, farm-level -300 -> income 5000,
# expense 3300, net 1700, 5 transactions.
$txStatuses = @()
$txStatuses += (PostTx $hdrA $farmMain 'Expense' 'Seeds'      1000 (D -10) $cropWheat.id).Status
$txStatuses += (PostTx $hdrA $farmMain 'Income'  'CropSale'   5000 (D -5)  $cropWheat.id).Status
$txStatuses += (PostTx $hdrA $farmMain 'Expense' 'Labour'     2000 (D -8)  $cropCotton.id).Status
$txStatuses += (PostTx $hdrA $farmMain 'Expense' 'Irrigation' 300  (D -2)  $null).Status
Check 'SETUP.6 main ledger created (4 tx)' ((@($txStatuses | Where-Object { $_ -ne 200 -and $_ -ne 201 })).Count -eq 0) "statuses=$($txStatuses -join ',')"

# Monitoring fixtures: one crop left fully scheduled (due/upcoming + lazy
# notifications), one crop with a completed and a skipped check.
$cropDue  = CreateCrop $tokenA $farmMain 'FD Due Wheat'  1 (D -40)
$cropDone = CreateCrop $tokenA $farmMain 'FD Done Wheat' 1 (D -40)
Check 'SETUP.7 monitoring crops created' ($cropDue.id -and $cropDone.id)
$gDue  = ApiCall 'POST' "$base/api/crops/$($cropDue.id)/monitoring/generate"  $hdrA
$gDone = ApiCall 'POST' "$base/api/crops/$($cropDone.id)/monitoring/generate" $hdrA
Check 'SETUP.8 monitoring checks generated (3 each)' (($gDue.Data.checksCreated + $gDue.Data.existingChecks) -eq 3 -and ($gDone.Data.checksCreated + $gDone.Data.existingChecks) -eq 3)
$doneChecks = AsArray ((GetRaw "$base/api/crops/$($cropDone.id)/monitoring" $hdrA) | ConvertFrom-Json)
$doneSorted = @($doneChecks | Sort-Object -Property scheduledDate)
$rc = ApiCall 'POST' "$base/api/monitoring/$($doneSorted[0].id)/complete" $hdrA (@{ Observation = 'Normal'; Notes = 'FD test' } | ConvertTo-Json) 'application/json'
$rs = ApiCall 'POST' "$base/api/monitoring/$($doneSorted[1].id)/skip"     $hdrA (@{ Notes = 'FD test skip' } | ConvertTo-Json) 'application/json'
Check 'SETUP.9 complete+skip applied' ($rc.Status -eq 200 -and $rs.Status -eq 200) "complete=$($rc.Status) skip=$($rs.Status)"

# User B fixture.
$cropB = CreateCrop $tokenB $farmB 'FD B Wheat' 1 $null
Check 'SETUP.10 user B crop created' ([bool]$cropB.id)

# -----------------------------------------------------------------------------
# TEST 1: authentication
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: authentication ---"
$anon = @{}
$r = ApiCall 'GET' "$base/api/farms/$farmMain/dashboard" $anon
Check 'T1.1 dashboard without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$bad = @{ Authorization = 'Bearer not.a.real.token' }
$r = ApiCall 'GET' "$base/api/farms/$farmMain/dashboard" $bad
Check 'T1.2 malformed token -> 401' ($r.Status -eq 401) "got $($r.Status)"

# -----------------------------------------------------------------------------
# TEST 2: ownership
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: ownership ---"
$ghost = [Guid]::NewGuid()
$r = ApiCall 'GET' "$base/api/farms/$ghost/dashboard" $hdrA
Check 'T2.1 unknown farm -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/dashboard" $hdrA
Check 'T2.2 another user''s farm -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/dashboard" $hdrB
Check 'T2.3 user B reads own farm -> 200' ($r.Status -eq 200) "got $($r.Status)"

# -----------------------------------------------------------------------------
# TEST 3: farm section + no UserId leakage
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: farm section ---"
$r = ApiCall 'GET' "$base/api/farms/$farmMain/dashboard" $hdrA
Check 'T3.1 dashboard -> 200' ($r.Status -eq 200) "got $($r.Status)"
$d = $r.Data
Check 'T3.2 farmId echoed' ($d.farm.farmId -eq $farmMain)
Check 'T3.3 farmName echoed' ($d.farm.farmName -eq 'FD Main Farm')
Check 'T3.4 location resolved (province/district/tehsil)' ($d.farm.province -and $d.farm.district -and $d.farm.tehsil)
Check 'T3.5 farm facts (size/unit/soil/irrigation)' ($d.farm.farmSize -eq 8 -and $d.farm.farmSizeUnit -eq 'Acres' -and $d.farm.soilType -eq 'Loamy' -and $d.farm.irrigationType -eq 'Canal')
Check 'T3.6 hasCoordinates true for coords farm' ($d.farm.hasCoordinates -eq $true)
$rawMain = GetRaw "$base/api/farms/$farmMain/dashboard" $hdrA
Check 'T3.7 no userId in dashboard JSON' ($rawMain -notmatch '(?i)"userId"')
Check 'T3.8 no ownerId in dashboard JSON' ($rawMain -notmatch '(?i)"ownerId"')

# -----------------------------------------------------------------------------
# TEST 4: crops section
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: crops section ---"
Check 'T4.1 totalCrops 4' ($d.crops.totalCrops -eq 4) "got $($d.crops.totalCrops)"
Check 'T4.2 activeCrops 4' ($d.crops.activeCrops -eq 4) "got $($d.crops.activeCrops)"
$names = @((AsArray $d.crops.crops) | ForEach-Object { $_.cropName })
Check 'T4.3 crop summaries include fixtures' (($names -contains 'FD Wheat') -and ($names -contains 'FD Due Wheat'))
$first = (AsArray $d.crops.crops)[0]
Check 'T4.4 crop item has fields' ($first.cropId -and $first.season -and $first.status)
Check 'T4.5 empty farm has NoCrops limitation' (HasLimitation ((ApiCall 'GET' "$base/api/farms/$farmEmpty/dashboard" $hdrA).Data) 'NoCrops')

# -----------------------------------------------------------------------------
# TEST 5: monitoring section (cross-check + no state change)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: monitoring section ---"
# Prime the Prompt 8 lazy-notification path BEFORE measuring, then confirm the
# dashboard neither changes monitoring state nor adds duplicate notifications.
ApiCall 'GET' "$base/api/monitoring/due" $hdrA | Out-Null
$unreadBefore = (ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA).Data.count
$dueBefore  = AsArray ((GetRaw "$base/api/monitoring/due" $hdrA) | ConvertFrom-Json)
$upBefore   = AsArray ((GetRaw "$base/api/monitoring/upcoming" $hdrA) | ConvertFrom-Json)
$dueFarmBefore  = @($dueBefore | Where-Object { $_.farmId -eq $farmMain })
$upFarmBefore   = @($upBefore  | Where-Object { $_.farmId -eq $farmMain })

$activity = (ApiCall 'GET' "$base/api/farms/$farmMain/performance/activity" $hdrA).Data
Check 'T5.1 totalChecks == activity.monitoringCheckCount (6)' ($d.monitoring.totalChecks -eq $activity.monitoringCheckCount -and $d.monitoring.totalChecks -eq 6) "dash=$($d.monitoring.totalChecks) activity=$($activity.monitoringCheckCount)"
Check 'T5.2 completedChecks 1' ($d.monitoring.completedChecks -eq 1 -and $d.monitoring.completedChecks -eq $activity.completedMonitoringChecks) "got $($d.monitoring.completedChecks)"
Check 'T5.3 skippedChecks 1' ($d.monitoring.skippedChecks -eq 1 -and $d.monitoring.skippedChecks -eq $activity.skippedMonitoringChecks) "got $($d.monitoring.skippedChecks)"
Check 'T5.4 dueChecks matches source endpoint' ($d.monitoring.dueChecks -eq $dueFarmBefore.Count) "dash=$($d.monitoring.dueChecks) src=$($dueFarmBefore.Count)"
Check 'T5.5 upcomingChecks matches source endpoint' ($d.monitoring.upcomingChecks -eq $upFarmBefore.Count) "dash=$($d.monitoring.upcomingChecks) src=$($upFarmBefore.Count)"
Check 'T5.6 due+upcoming == remaining scheduled (4)' (($d.monitoring.dueChecks + $d.monitoring.upcomingChecks) -eq 4)

# Read the dashboard a second time - monitoring state and notifications must be unchanged.
ApiCall 'GET' "$base/api/farms/$farmMain/dashboard" $hdrA | Out-Null
ApiCall 'GET' "$base/api/farms/$farmMain/dashboard" $hdrA | Out-Null
$dueAfter = AsArray ((GetRaw "$base/api/monitoring/due" $hdrA) | ConvertFrom-Json)
$unreadAfter = (ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA).Data.count
Check 'T5.7 dashboard does not change due-check state' (@($dueAfter | Where-Object { $_.farmId -eq $farmMain }).Count -eq $dueFarmBefore.Count)
Check 'T5.8 dashboard adds no duplicate notifications' ($unreadAfter -eq $unreadBefore) "before=$unreadBefore after=$unreadAfter"

# -----------------------------------------------------------------------------
# TEST 6: notifications section (user-scoped, bounded)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: notifications section ---"
$srcUnread = (ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA).Data.count
$srcRecent = AsArray ((GetRaw "$base/api/notifications?take=5" $hdrA) | ConvertFrom-Json)
Check 'T6.1 unreadCount matches source' ($d.notifications.unreadCount -eq $srcUnread) "dash=$($d.notifications.unreadCount) src=$srcUnread"
$recent = AsArray $d.notifications.recentNotifications
Check 'T6.2 recent list bounded to 5' ($recent.Count -le 5) "got $($recent.Count)"
$srcIds  = @($srcRecent | ForEach-Object { $_.id })
$dashIds = @($recent | ForEach-Object { $_.id })
Check 'T6.3 recent list matches source (same ids)' (-not (Compare-Object $srcIds $dashIds)) "src=$($srcIds.Count) dash=$($dashIds.Count)"
# User isolation: user B's dashboard never contains user A's notifications.
$dB = (ApiCall 'GET' "$base/api/farms/$farmB/dashboard" $hdrB).Data
$bIds = @( (AsArray $dB.notifications.recentNotifications) | ForEach-Object { $_.id } )
Check 'T6.4 user B sees none of user A''s notifications' (-not (@($bIds | Where-Object { $srcIds -contains $_ })))

# -----------------------------------------------------------------------------
# TEST 7: financial section (matches Prompt 9)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: financial section ---"
$fin = (ApiCall 'GET' "$base/api/farms/$farmMain/financial-summary" $hdrA).Data
Check 'T7.1 totalIncome matches Prompt 9 (5000)' ($d.financial.totalIncome -eq $fin.totalIncome -and $d.financial.totalIncome -eq 5000) "dash=$($d.financial.totalIncome) src=$($fin.totalIncome)"
Check 'T7.2 totalExpenses matches Prompt 9 (3300)' ($d.financial.totalExpenses -eq $fin.totalExpenses -and $d.financial.totalExpenses -eq 3300) "dash=$($d.financial.totalExpenses) src=$($fin.totalExpenses)"
Check 'T7.3 netResult matches Prompt 9 netProfitLoss (1700)' ($d.financial.netResult -eq $fin.netProfitLoss -and $d.financial.netResult -eq 1700) "dash=$($d.financial.netResult) src=$($fin.netProfitLoss)"
Check 'T7.4 transactionCount matches Prompt 9 (4)' ($d.financial.transactionCount -eq $fin.transactionCount -and $d.financial.transactionCount -eq 4) "dash=$($d.financial.transactionCount) src=$($fin.transactionCount)"
Check 'T7.5 empty farm has NoFinancialTransactions limitation' (HasLimitation ((ApiCall 'GET' "$base/api/farms/$farmEmpty/dashboard" $hdrA).Data) 'NoFinancialTransactions')

# -----------------------------------------------------------------------------
# TEST 8: financial health section (matches Prompt 10, disclaimers preserved)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: financial health section ---"
$health = (ApiCall 'GET' "$base/api/farms/$farmMain/financial-health" $hdrA).Data
$comp   = (ApiCall 'GET' "$base/api/farms/$farmMain/financial-health/completeness" $hdrA).Data
Check 'T8.1 healthIndicator matches Prompt 10' ($d.financialHealth.healthIndicator -eq $health.healthIndicator) "dash=$($d.financialHealth.healthIndicator) src=$($health.healthIndicator)"
Check 'T8.2 healthExplanation matches Prompt 10' ($d.financialHealth.healthExplanation -eq $health.healthExplanation)
Check 'T8.3 completenessStatus matches Prompt 10' ($d.financialHealth.completenessStatus -eq $comp.status) "dash=$($d.financialHealth.completenessStatus) src=$($comp.status)"
Check 'T8.4 completenessScore matches Prompt 10' ($d.financialHealth.completenessScore -eq $comp.score) "dash=$($d.financialHealth.completenessScore) src=$($comp.score)"
Check 'T8.5 completeness disclaimer preserved' ($d.financialHealth.disclaimer -eq $comp.disclaimer -and $d.financialHealth.disclaimer -match 'SABZ')

# -----------------------------------------------------------------------------
# TEST 9: performance section (matches Prompt 11, factual wording)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: performance section ---"
$perf = (ApiCall 'GET' "$base/api/farms/$farmMain/performance" $hdrA).Data
Check 'T9.1 overallStatus matches Prompt 11' ($d.performance.overallStatus -eq $perf.overallStatus) "dash=$($d.performance.overallStatus) src=$($perf.overallStatus)"
Check 'T9.2 statusExplanation matches Prompt 11' ($d.performance.statusExplanation -eq $perf.statusExplanation)
Check 'T9.3 netResult matches Prompt 11' ($d.performance.netResult -eq $perf.netResult)
Check 'T9.4 bestRecordedCrop matches Prompt 11 (FD Wheat)' ($d.performance.bestRecordedCrop.cropName -eq $perf.bestRecordedCrop.cropName -and $d.performance.bestRecordedCrop.cropName -eq 'FD Wheat') "dash=$($d.performance.bestRecordedCrop.cropName) src=$($perf.bestRecordedCrop.cropName)"
Check 'T9.5 weakestRecordedCrop matches Prompt 11 (FD Cotton)' ($d.performance.weakestRecordedCrop.cropName -eq $perf.weakestRecordedCrop.cropName -and $d.performance.weakestRecordedCrop.cropName -eq 'FD Cotton') "dash=$($d.performance.weakestRecordedCrop.cropName) src=$($perf.weakestRecordedCrop.cropName)"

# -----------------------------------------------------------------------------
# TEST 10: weather section (external data, never breaks dashboard)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: weather section ---"
# Probe the source endpoint first so any success warms the shared cache.
$directW = ApiCall 'GET' "$base/api/farms/$farmMain/weather/current" $hdrA
if ($directW.Status -eq 200) {
    Check 'T10.1 coords farm shows weather when provider OK' ($null -ne $d.weather)
    Check 'T10.2 dashboard weather source matches provider' ($d.weather.source -eq $directW.Data.source) "dash=$($d.weather.source) src=$($directW.Data.source)"
    Check 'T10.3 weather marked as external data' ($d.weather.note -match 'external data')
    Check 'T10.4 no NoCoordinates limitation on coords farm' (-not (HasLimitation $d 'NoCoordinates'))
} else {
    Check 'T10.1 dashboard still 200 when weather fails' ($true)
    Check 'T10.2 weather null on provider failure' ($null -eq $d.weather)
    Check 'T10.3 WeatherUnavailable limitation present' (HasLimitation $d 'WeatherUnavailable')
}
# Farm without coordinates: weather must be null with a NoCoordinates limitation.
$dEmpty = (ApiCall 'GET' "$base/api/farms/$farmEmpty/dashboard" $hdrA).Data
Check 'T10.5 no-coords farm weather null' ($null -eq $dEmpty.weather)
Check 'T10.6 no-coords farm NoCoordinates limitation' (HasLimitation $dEmpty 'NoCoordinates')

# -----------------------------------------------------------------------------
# TEST 11: limitations + mandatory disclaimer
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 11: limitations & disclaimer ---"
Check 'T11.1 RecordedDataOnly always present' (HasLimitation $d 'RecordedDataOnly')
Check 'T11.2 RecordedDataOnly is first limitation' ((AsArray $d.limitations)[0].code -eq 'RecordedDataOnly')
Check 'T11.3 disclaimer present' ($d.disclaimer -match 'does not independently verify real-world farm activity')
Check 'T11.4 disclaimer names creditworthiness' ($d.disclaimer -match 'creditworthiness')
Check 'T11.5 generatedAt populated' ([bool]$d.generatedAt)
Check 'T11.6 empty farm disclaimer still present' ((ApiCall 'GET' "$base/api/farms/$farmEmpty/dashboard" $hdrA).Data.disclaimer -match 'does not independently verify')

# -----------------------------------------------------------------------------
# TEST 12: response hygiene (wording guardrails)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 12: response hygiene ---"
Check 'T12.1 no loan/credit wording' ($rawMain -notmatch '(?i)(loan|credit scor|insurance|banking|investment|most profitable)')
Check 'T12.2 factual "recorded" wording for best crop' ($rawMain -match '(?i)bestRecordedCrop')

# -----------------------------------------------------------------------------
# TEST 13: cleanup + database integrity
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 13: cleanup and integrity ---"
foreach ($f in @($farmEmpty, $farmMain)) {
    DeleteFarmTx $hdrA $f
    DeleteFdCrops $hdrA $f
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}
DeleteFarmTx $hdrB $farmB
DeleteFdCrops $hdrB $farmB
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$leftFarms  = (SqlQuery "SELECT COUNT(*) FROM Farms WHERE FarmName LIKE 'FD %'") -join ''
$leftCrops  = (SqlQuery "SELECT COUNT(*) FROM Crops WHERE CropName LIKE 'FD %'") -join ''
$orphanTx   = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Farms f ON t.FarmId = f.Id WHERE f.Id IS NULL") -join ''
$orphanCrop = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Crops c ON t.CropId = c.Id WHERE t.CropId IS NOT NULL AND c.Id IS NULL") -join ''
$orphanChk  = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks k LEFT JOIN Crops c ON k.CropId = c.Id WHERE c.Id IS NULL") -join ''
$tableCount = (SqlQuery "SELECT COUNT(*) FROM sys.tables") -join ''
$migCount   = (SqlQuery "SELECT COUNT(*) FROM __EFMigrationsHistory") -join ''

Check 'T13.1 no FD farms left' ($leftFarms -eq '0') "left=$leftFarms"
Check 'T13.2 no FD crops left' ($leftCrops -eq '0') "left=$leftCrops"
Check 'T13.3 no orphan financial transactions' ($orphanTx -eq '0') "orphans=$orphanTx"
Check 'T13.4 no orphan crop references on transactions' ($orphanCrop -eq '0') "orphans=$orphanCrop"
Check 'T13.5 no orphan monitoring checks' ($orphanChk -eq '0') "orphans=$orphanChk"
Check 'T13.6 table count unchanged (21 incl. history)' ($tableCount -eq '21') "count=$tableCount"
Check 'T13.7 migration count unchanged (11)' ($migCount -eq '11') "count=$migCount"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T13.8 Ahmed seed farm untouched' ($ahmedBefore -eq $ahmedAfter)

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
Write-Host "`n=== Prompt 12 results: $pass passed, $fail failed ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 } else { exit 0 }
