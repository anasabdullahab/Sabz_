# =============================================================================
# SABZ Prompt 11 - Farm Performance Dashboard & Decision Intelligence
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Read-only intelligence over crops, the Prompt 9 ledger and Prompt 7
# monitoring checks. No scoring, no AI, no background jobs, no new tables,
# nothing persisted.
#
# Idempotency strategy: every run deletes leftover "FP " fixture crops, their
# transactions/monitoring checks and the "FP " fixture farms through the
# public API, then recreates fixtures with dates relative to today.
# Seed/reference data is never touched.
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
    $tmp = Join-Path $env:TEMP ('fpbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# Normalise PS 5.1 pipeline artefacts into a real array (see test-financial-health.ps1).
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

function DeleteFpCrops($headers, $farmId) {
    $raw = GetRaw "$base/api/farms/$farmId/crops" $headers
    if (-not $raw) { return }
    foreach ($c in AsArray ($raw | ConvertFrom-Json)) {
        if ($c.cropName -like 'FP *') {
            try { Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers $headers -UseBasicParsing | Out-Null } catch { }
        }
    }
}

function HasLimitation($summary, [string]$code) {
    return [bool](@($summary.limitations) | Where-Object { $_.code -eq $code })
}

$today = (Get-Date).Date
function D([int]$offset) { return $script:today.AddDays($offset).ToString('yyyy-MM-dd') }

Write-Host "`n=== SABZ Prompt 11: Farm Performance Dashboard & Decision Intelligence Tests ===" -ForegroundColor Cyan

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

$farmEmpty    = EnsureFarm $tokenA 'FP Empty Farm'
$farmOverview = EnsureFarm $tokenA 'FP Overview Farm'
$farmLimited  = EnsureFarm $tokenA 'FP Limited Farm'
$farmActivity = EnsureFarm $tokenA 'FP Activity Farm'
$farmB        = EnsureFarm $tokenB 'FP User-B Farm'
Check 'SETUP.3 FP farms ready' ($farmEmpty -and $farmOverview -and $farmLimited -and $farmActivity -and $farmB)

# Ahmed Farm guard snapshot (must remain untouched).
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: wipe fixture ledgers and crops, delete FP farms left by any
# previous run, and recreate everything from scratch.
foreach ($f in @($farmEmpty, $farmOverview, $farmLimited, $farmActivity)) {
    DeleteFarmTx $hdrA $f
    DeleteFpCrops $hdrA $f
}
DeleteFarmTx $hdrB $farmB
DeleteFpCrops $hdrB $farmB
foreach ($f in @($farmEmpty, $farmOverview, $farmLimited, $farmActivity)) {
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$farmEmpty    = EnsureFarm $tokenA 'FP Empty Farm'
$farmOverview = EnsureFarm $tokenA 'FP Overview Farm'
$farmLimited  = EnsureFarm $tokenA 'FP Limited Farm'
$farmActivity = EnsureFarm $tokenA 'FP Activity Farm'
$farmB        = EnsureFarm $tokenB 'FP User-B Farm'
Check 'SETUP.4 FP farms recreated fresh' ($farmEmpty -and $farmOverview -and $farmLimited -and $farmActivity -and $farmB)

# Overview fixtures: 6 crops, deterministic nets, tie at the top.
$cropWheat  = CreateCrop $tokenA $farmOverview 'FP Wheat'  1 $null
$cropTieA   = CreateCrop $tokenA $farmOverview 'FP TieA'   1 $null
$cropTieB   = CreateCrop $tokenA $farmOverview 'FP TieB'   1 $null
$cropCotton = CreateCrop $tokenA $farmOverview 'FP Cotton' 1 $null
$cropRice   = CreateCrop $tokenA $farmOverview 'FP Rice'   1 $null
$cropBarley = CreateCrop $tokenA $farmOverview 'FP Barley' 1 $null
Check 'SETUP.5 overview crops created' ($cropWheat.id -and $cropTieA.id -and $cropTieB.id -and $cropCotton.id -and $cropRice.id -and $cropBarley.id)

# Ledger: Wheat +2000, TieA +2000, TieB +2000 (tie at the top), Rice +1500,
# Cotton -4000, plus one farm-level (unattributed) expense. Income 15500,
# expense 12500, net 3000, 10 transactions. Individual statements (a trailing-comma array
# would swallow the next PostTx token in PS 5.1).
$txStatuses = @()
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Seeds'      1000 (D -10) $cropWheat.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Fertilizer' 5000 (D -9)  $cropWheat.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Income'  'CropSale'   8000 (D -5)  $cropWheat.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Seeds'      1000 (D -10) $cropTieA.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Income'  'CropSale'   3000 (D -5)  $cropTieA.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Seeds'      1000 (D -9)  $cropTieB.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Income'  'CropSale'   3000 (D -4)  $cropTieB.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Income'  'CropSale'   1500 (D -3)  $cropRice.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Labour'     4000 (D -8)  $cropCotton.id).Status
$txStatuses += (PostTx $hdrA $farmOverview 'Expense' 'Irrigation' 500  (D -2)  $null).Status
Check 'SETUP.6 overview ledger created (10 tx)' ((@($txStatuses | Where-Object { $_ -ne 200 -and $_ -ne 201 })).Count -eq 0) "statuses=$($txStatuses -join ',')"

# Limited fixture: 2 expense transactions only.
$cropLim = CreateCrop $tokenA $farmLimited 'FP Limited Wheat' 1 $null
PostTx $hdrA $farmLimited 'Expense' 'Seeds'  300 (D -4) $cropLim.id | Out-Null
PostTx $hdrA $farmLimited 'Expense' 'Labour' 200 (D -2) $cropLim.id | Out-Null

# User B fixture.
$cropB = CreateCrop $tokenB $farmB 'FP B Wheat' 1 $null
Check 'SETUP.7 user B crop created' ([bool]$cropB.id)

# -----------------------------------------------------------------------------
# TEST 1: authentication (all three Prompt 11 endpoints)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: authentication ---"
$anon = @{}
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance" $anon
Check 'T1.1 performance without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance/crops" $anon
Check 'T1.2 crops breakdown without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance/activity" $anon
Check 'T1.3 activity without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$bad = @{ Authorization = 'Bearer not.a.real.token' }
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance" $bad
Check 'T1.4 malformed token -> 401' ($r.Status -eq 401) "got $($r.Status)"

# -----------------------------------------------------------------------------
# TEST 2: ownership
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: ownership ---"
$ghost = [Guid]::NewGuid()
$r = ApiCall 'GET' "$base/api/farms/$ghost/performance" $hdrA
Check 'T2.1 unknown farm -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$ghost/performance/crops" $hdrA
Check 'T2.2 unknown farm (crops) -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$ghost/performance/activity" $hdrA
Check 'T2.3 unknown farm (activity) -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/performance" $hdrA
Check 'T2.4 another user''s farm -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/performance/crops" $hdrA
Check 'T2.5 another user''s farm (crops) -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/performance/activity" $hdrA
Check 'T2.6 another user''s farm (activity) -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/performance" $hdrB
Check 'T2.7 user B reads own farm -> 200' ($r.Status -eq 200) "got $($r.Status)"

# -----------------------------------------------------------------------------
# TEST 3: overview - empty farm (NoRecordedData)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: overview on empty farm ---"
$r = ApiCall 'GET' "$base/api/farms/$farmEmpty/performance" $hdrA
Check 'T3.1 empty farm -> 200' ($r.Status -eq 200) "got $($r.Status)"
$s = $r.Data
Check 'T3.2 zeroed crop counts' ($s.totalCrops -eq 0 -and $s.activeCrops -eq 0 -and $s.cropsWithFinancialActivity -eq 0 -and $s.cropsWithoutFinancialActivity -eq 0)
Check 'T3.3 zeroed financials' ($s.transactionCount -eq 0 -and $s.totalIncome -eq 0 -and $s.totalExpense -eq 0 -and $s.netResult -eq 0)
Check 'T3.4 no best/weakest crop' ($null -eq $s.bestRecordedCrop -and $null -eq $s.weakestRecordedCrop)
Check 'T3.5 status NoRecordedData' ($s.overallStatus -eq 'NoRecordedData') "got $($s.overallStatus)"
Check 'T3.6 honest status explanation' ($s.statusExplanation -match 'No financial transactions')
Check 'T3.7 NoFinancialTransactions limitation' (HasLimitation $s 'NoFinancialTransactions')
Check 'T3.8 NoRankedCrops limitation' (HasLimitation $s 'NoRankedCrops')
Check 'T3.9 mandatory disclaimer' ($s.disclaimer -match 'Based only on data recorded in SABZ')
Check 'T3.10 farmName echoed' ($s.farmName -eq 'FP Empty Farm')

# -----------------------------------------------------------------------------
# TEST 4: overview - full recorded data, ranking, ties
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: overview with recorded data ---"
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance" $hdrA
Check 'T4.1 overview -> 200' ($r.Status -eq 200) "got $($r.Status)"
$s = $r.Data
Check 'T4.2 crop counts (6 total, 6 active)' ($s.totalCrops -eq 6 -and $s.activeCrops -eq 6)
Check 'T4.3 with/without financial activity (5/1)' ($s.cropsWithFinancialActivity -eq 5 -and $s.cropsWithoutFinancialActivity -eq 1)
Check 'T4.4 transaction count 10' ($s.transactionCount -eq 10) "got $($s.transactionCount)"
Check 'T4.5 total income 15500' ($s.totalIncome -eq 15500) "got $($s.totalIncome)"
Check 'T4.6 total expense 12500' ($s.totalExpense -eq 12500) "got $($s.totalExpense)"
Check 'T4.7 net result 3000' ($s.netResult -eq 3000) "got $($s.netResult)"
Check 'T4.8 status RecordedActivityAvailable' ($s.overallStatus -eq 'RecordedActivityAvailable') "got $($s.overallStatus)"
Check 'T4.9 best recorded crop = FP TieA (deterministic tie-break by name)' ($s.bestRecordedCrop.cropName -eq 'FP TieA') "got $($s.bestRecordedCrop.cropName)"
Check 'T4.10 best net 2000' ($s.bestRecordedCrop.netResult -eq 2000) "got $($s.bestRecordedCrop.netResult)"
Check 'T4.11 weakest recorded crop = FP Cotton' ($s.weakestRecordedCrop.cropName -eq 'FP Cotton') "got $($s.weakestRecordedCrop.cropName)"
Check 'T4.12 weakest net -4000' ($s.weakestRecordedCrop.netResult -eq -4000) "got $($s.weakestRecordedCrop.netResult)"
Check 'T4.13 Barley (no records) never ranked' ($s.bestRecordedCrop.cropName -ne 'FP Barley' -and $s.weakestRecordedCrop.cropName -ne 'FP Barley')
Check 'T4.14 CropsWithoutFinancialRecords limitation' (HasLimitation $s 'CropsWithoutFinancialRecords')
$expOnlyMsg = (@($s.limitations) | Where-Object { $_.code -eq 'ExpensesOnlyCrops' } | ForEach-Object { $_.message }) -join ' '
Check 'T4.15 ExpensesOnlyCrops limitation names FP Cotton' ($expOnlyMsg -match 'FP Cotton') "msg=$expOnlyMsg"
$incOnlyMsg = (@($s.limitations) | Where-Object { $_.code -eq 'IncomeOnlyCrops' } | ForEach-Object { $_.message }) -join ' '
Check 'T4.16 IncomeOnlyCrops limitation names FP Rice' ($incOnlyMsg -match 'FP Rice') "msg=$incOnlyMsg"
Check 'T4.17 UnattributedTransactions limitation' (HasLimitation $s 'UnattributedTransactions')
Check 'T4.18 disclaimer present' ($s.disclaimer -match 'does not measure real-world farm performance')
# Determinism: identical results on a second call.
$r2 = ApiCall 'GET' "$base/api/farms/$farmOverview/performance" $hdrA
Check 'T4.19 tie-break deterministic across calls' ($r2.Data.bestRecordedCrop.cropName -eq 'FP TieA' -and $r2.Data.weakestRecordedCrop.cropName -eq 'FP Cotton')

# -----------------------------------------------------------------------------
# TEST 5: overview date filtering + validation
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: date filtering ---"
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance?fromDate=$(D -6)&toDate=$(D 0)" $hdrA
$s = $r.Data
Check 'T5.1 filtered -> 200' ($r.Status -eq 200) "got $($r.Status)"
Check 'T5.2 range filters ledger only (5 tx in range)' ($s.transactionCount -eq 5) "got $($s.transactionCount)"
Check 'T5.3 income in range 15500' ($s.totalIncome -eq 15500) "got $($s.totalIncome)"
Check 'T5.4 expense in range 500 (farm-level only)' ($s.totalExpense -eq 500) "got $($s.totalExpense)"
Check 'T5.5 crop counts unaffected by range (still 6)' ($s.totalCrops -eq 6)
Check 'T5.6 best in range = FP Wheat (+8000)' ($s.bestRecordedCrop.cropName -eq 'FP Wheat' -and $s.bestRecordedCrop.netResult -eq 8000) "got $($s.bestRecordedCrop.cropName) $($s.bestRecordedCrop.netResult)"
Check 'T5.7 from/to echoed' ($s.fromDate -match (D -6) -and $s.toDate -match (D 0))
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance?fromDate=$(D 0)&toDate=$(D -6)" $hdrA
Check 'T5.8 fromDate after toDate -> 400' ($r.Status -eq 400) "got $($r.Status)"

# -----------------------------------------------------------------------------
# TEST 6: overview - limited data
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: limited recorded data ---"
$r = ApiCall 'GET' "$base/api/farms/$farmLimited/performance" $hdrA
$s = $r.Data
Check 'T6.1 limited farm -> 200' ($r.Status -eq 200) "got $($r.Status)"
Check 'T6.2 status LimitedRecordedData' ($s.overallStatus -eq 'LimitedRecordedData') "got $($s.overallStatus)"
Check 'T6.3 explanation names the gap' ($s.statusExplanation -match 'fewer than 5')
Check 'T6.4 expenses-only crop also weakest recorded' ($s.weakestRecordedCrop.cropName -eq 'FP Limited Wheat' -and $s.weakestRecordedCrop.netResult -eq -500)
Check 'T6.5 single qualifying crop is also best recorded' ($s.bestRecordedCrop.cropName -eq 'FP Limited Wheat')

# -----------------------------------------------------------------------------
# TEST 7: per-crop performance breakdown
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: crop performance breakdown ---"
$r = ApiCall 'GET' "$base/api/farms/$farmOverview/performance/crops" $hdrA
Check 'T7.1 breakdown -> 200' ($r.Status -eq 200) "got $($r.Status)"
$rows = AsArray $r.Data
Check 'T7.2 one row per crop (6)' ($rows.Count -eq 6) "got $($rows.Count)"
function FindRow($rows, $name) { return @($rows | Where-Object { $_.cropName -eq $name }) | Select-Object -First 1 }
$w = FindRow $rows 'FP Wheat'; $ta = FindRow $rows 'FP TieA'; $co = FindRow $rows 'FP Cotton'
$ri = FindRow $rows 'FP Rice'; $ba = FindRow $rows 'FP Barley'
Check 'T7.3 Wheat RecordedIncomeAndExpenses' ($w.financialDataStatus -eq 'RecordedIncomeAndExpenses' -and $w.hasIncomeRecords -eq $true -and $w.hasExpenseRecords -eq $true)
Check 'T7.4 Wheat net 2000 (8000-6000)' ($w.totalIncome -eq 8000 -and $w.totalExpense -eq 6000 -and $w.netResult -eq 2000 -and $w.transactionCount -eq 3)
Check 'T7.5 TieA RecordedIncomeAndExpenses' ($ta.financialDataStatus -eq 'RecordedIncomeAndExpenses')
Check 'T7.6 Cotton ExpensesOnly' ($co.financialDataStatus -eq 'ExpensesOnly' -and $co.hasIncomeRecords -eq $false -and $co.hasExpenseRecords -eq $true -and $co.netResult -eq -4000)
Check 'T7.7 Rice IncomeOnly' ($ri.financialDataStatus -eq 'IncomeOnly' -and $ri.netResult -eq 1500)
Check 'T7.8 Barley NoFinancialData' ($ba.financialDataStatus -eq 'NoFinancialData' -and $ba.transactionCount -eq 0 -and $ba.netResult -eq 0)
Check 'T7.9 existing crop status exposed (Active)' ($w.status -eq 'Active')
# Farm isolation: user B sees only their crop.
$r = ApiCall 'GET' "$base/api/farms/$farmB/performance/crops" $hdrB
$rowsB = AsArray $r.Data
Check 'T7.10 user B breakdown isolated (1 own crop)' ($rowsB.Count -eq 1 -and $rowsB[0].cropName -eq 'FP B Wheat') "got $($rowsB.Count)"

# -----------------------------------------------------------------------------
# TEST 8: recorded activity summary
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: recorded activity ---"
# Empty farm: nothing recorded anywhere.
$r = ApiCall 'GET' "$base/api/farms/$farmEmpty/performance/activity" $hdrA
$a = $r.Data
Check 'T8.1 empty activity -> 200 all zeros' ($r.Status -eq 200 -and $a.financialTransactionCount -eq 0 -and $a.monitoringCheckCount -eq 0 -and $a.recordedActivityDays -eq 0)
Check 'T8.2 empty activity dates null' ($null -eq $a.firstRecordedActivity -and $null -eq $a.latestRecordedActivity)
Check 'T8.3 explanation states recorded activity in SABZ only' ($a.explanation -match 'recorded activity in SABZ only')

# Activity farm: monitored crop + 2 transactions.
$cropAct = CreateCrop $tokenA $farmActivity 'FP Act Wheat' 1 (D -40)
Check 'T8.4 activity crop created' ([bool]$cropAct.id)
$g = ApiCall 'POST' "$base/api/crops/$($cropAct.id)/monitoring/generate" $hdrA
Check 'T8.5 monitoring checks present (created+existing=3)' ($g.Status -eq 200 -and ($g.Data.checksCreated + $g.Data.existingChecks) -eq 3) "created=$($g.Data.checksCreated) existing=$($g.Data.existingChecks)"
$checks = AsArray ((GetRaw "$base/api/crops/$($cropAct.id)/monitoring" $hdrA) | ConvertFrom-Json)
Check 'T8.6 checks listed (3, scheduled)' ($checks.Count -eq 3)
$due = @($checks | Sort-Object -Property scheduledDate)
$c1 = $due[0]; $c2 = $due[1]
$rc = ApiCall 'POST' "$base/api/monitoring/$($c1.id)/complete" $hdrA (@{ Observation = 'Normal'; Notes = 'FP activity test' } | ConvertTo-Json) 'application/json'
Check 'T8.7 complete first check -> 200' ($rc.Status -eq 200) "got $($rc.Status)"
$rs = ApiCall 'POST' "$base/api/monitoring/$($c2.id)/skip" $hdrA (@{ Notes = 'FP activity test skip' } | ConvertTo-Json) 'application/json'
Check 'T8.8 skip second check -> 200' ($rs.Status -eq 200) "got $($rs.Status)"
PostTx $hdrA $farmActivity 'Expense' 'Seeds'    100 (D -3) $cropAct.id | Out-Null
PostTx $hdrA $farmActivity 'Income'  'CropSale' 500 (D -1) $cropAct.id | Out-Null

$r = ApiCall 'GET' "$base/api/farms/$farmActivity/performance/activity" $hdrA
$a = $r.Data
Check 'T8.9 activity -> 200' ($r.Status -eq 200) "got $($r.Status)"
Check 'T8.10 financial transaction count 2' ($a.financialTransactionCount -eq 2) "got $($a.financialTransactionCount)"
Check 'T8.11 monitoring check count 3' ($a.monitoringCheckCount -eq 3) "got $($a.monitoringCheckCount)"
Check 'T8.12 completed/skipped/scheduled 1/1/1' ($a.completedMonitoringChecks -eq 1 -and $a.skippedMonitoringChecks -eq 1 -and $a.scheduledMonitoringChecks -eq 1)
Check 'T8.13 recorded activity days 3 (d-3, d-1, today)' ($a.recordedActivityDays -eq 3) "got $($a.recordedActivityDays)"
Check 'T8.14 first recorded activity is d-3' ($a.firstRecordedActivity -match (D -3)) "got $($a.firstRecordedActivity)"
Check 'T8.15 latest recorded activity is today (check actions)' ($a.latestRecordedActivity -match (D 0)) "got $($a.latestRecordedActivity)"
Check 'T8.16 activity farm overview status LimitedRecordedData (2 tx)' ((ApiCall 'GET' "$base/api/farms/$farmActivity/performance" $hdrA).Data.overallStatus -eq 'LimitedRecordedData')

# -----------------------------------------------------------------------------
# TEST 9: response hygiene (no userId leakage, raw JSON)
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: response hygiene ---"
$raw = GetRaw "$base/api/farms/$farmOverview/performance" $hdrA
Check 'T9.1 no userId in performance JSON' ($raw -notmatch '(?i)"userId"')
Check 'T9.2 no ownerId in performance JSON' ($raw -notmatch '(?i)"ownerId"')
$rawCrops = GetRaw "$base/api/farms/$farmOverview/performance/crops" $hdrA
Check 'T9.3 no userId in crops breakdown JSON' ($rawCrops -notmatch '(?i)"userId"')
$rawAct = GetRaw "$base/api/farms/$farmActivity/performance/activity" $hdrA
Check 'T9.4 no userId in activity JSON' ($rawAct -notmatch '(?i)"userId"')
Check 'T9.5 no loan/credit wording' ($raw -notmatch '(?i)(loan|credit scor|insurance|banking|investment)')

# -----------------------------------------------------------------------------
# TEST 10: cleanup + database integrity
# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: cleanup and integrity ---"
foreach ($f in @($farmEmpty, $farmOverview, $farmLimited, $farmActivity)) {
    DeleteFarmTx $hdrA $f
    DeleteFpCrops $hdrA $f
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}
DeleteFarmTx $hdrB $farmB
DeleteFpCrops $hdrB $farmB
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$leftFarms  = (SqlQuery "SELECT COUNT(*) FROM Farms WHERE FarmName LIKE 'FP %'") -join ''
$leftCrops  = (SqlQuery "SELECT COUNT(*) FROM Crops WHERE CropName LIKE 'FP %'") -join ''
$orphanTx   = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Farms f ON t.FarmId = f.Id WHERE f.Id IS NULL") -join ''
$orphanCrop = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Crops c ON t.CropId = c.Id WHERE t.CropId IS NOT NULL AND c.Id IS NULL") -join ''
$orphanChk  = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks k LEFT JOIN Crops c ON k.CropId = c.Id WHERE c.Id IS NULL") -join ''
$tableCount = (SqlQuery "SELECT COUNT(*) FROM sys.tables") -join ''
$migCount   = (SqlQuery "SELECT COUNT(*) FROM __EFMigrationsHistory") -join ''

Check 'T10.1 no FP farms left' ($leftFarms -eq '0') "left=$leftFarms"
Check 'T10.2 no FP crops left' ($leftCrops -eq '0') "left=$leftCrops"
Check 'T10.3 no orphan financial transactions' ($orphanTx -eq '0') "orphans=$orphanTx"
Check 'T10.4 no orphan crop references on transactions' ($orphanCrop -eq '0') "orphans=$orphanCrop"
Check 'T10.5 no orphan monitoring checks' ($orphanChk -eq '0') "orphans=$orphanChk"
Check 'T10.6 table count unchanged (21 incl. history)' ($tableCount -eq '21') "count=$tableCount"
Check 'T10.7 migration count unchanged (11)' ($migCount -eq '11') "count=$migCount"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T10.8 Ahmed seed farm untouched' ($ahmedBefore -eq $ahmedAfter)

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
Write-Host "`n=== Prompt 11 results: $pass passed, $fail failed ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 } else { exit 0 }
