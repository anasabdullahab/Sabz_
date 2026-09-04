# =============================================================================
# SABZ Prompt 10 - Farm Financial Health & Readiness Intelligence
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Read-only analytics derived from the Prompt 9 ledger. No lending, no credit
# scoring, no AI, no background jobs, no new tables, nothing persisted.
#
# Idempotency strategy: every run deletes leftover "FH " fixture crops, their
# transactions and the "FH " fixture farms through the public API, then
# recreates fixtures with dates relative to today. Seed/reference data is
# never touched.
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
    $tmp = Join-Path $env:TEMP ('fhbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# Raw GET (string) so single-element arrays are never unwrapped and regex
# checks run against the exact JSON bytes.
function GetRaw([string]$url, $headers = @{}) {
    try { return (Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing).Content } catch { return $null }
}

# Normalise PS 5.1 pipeline artefacts into a real array. Three PS 5.1 traps:
# (1) 'return $arr' ENUMERATES the array, unwrapping single-element results -
# always return with the comma operator; (2) an Object[] argument is bound
# as-is, so explicit arrays are returned untouched; (3) the comma-wrapper is
# only unwrapped by a PLAIN assignment - '$v = @(AsArray $x)' preserves the
# wrapper and collapses multi-element arrays to Count=1.
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

function CreateCrop($token, $farmId, $name, $catalogId) {
    $body = @{ CropName = $name; Season = 'Rabi'; CropCatalogId = $catalogId }
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

function DeleteFhCrops($headers, $farmId) {
    $raw = GetRaw "$base/api/farms/$farmId/crops" $headers
    if (-not $raw) { return }
    foreach ($c in AsArray ($raw | ConvertFrom-Json)) {
        if ($c.cropName -like 'FH *') {
            try { Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers $headers -UseBasicParsing | Out-Null } catch { }
        }
    }
}

Write-Host "`n=== SABZ Prompt 10: Financial Health & Readiness Intelligence Tests ===" -ForegroundColor Cyan

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

$farmNoData   = EnsureFarm $tokenA 'FH NoData Farm'
$farmIncome   = EnsureFarm $tokenA 'FH Income-Only Farm'
$farmExpense  = EnsureFarm $tokenA 'FH Expense-Only Farm'
$farmMixed    = EnsureFarm $tokenA 'FH Loss-BreakEven-Positive Farm'
$farmCats     = EnsureFarm $tokenA 'FH Categories Farm'
$farmActivity = EnsureFarm $tokenA 'FH Activity Farm'
$farmFull     = EnsureFarm $tokenA 'FH Completeness Farm'
$farmB        = EnsureFarm $tokenB 'FH User-B Farm'
Check 'SETUP.3 FH farms ready' ($farmNoData -and $farmIncome -and $farmExpense -and $farmMixed -and $farmCats -and $farmActivity -and $farmFull -and $farmB)

# Ahmed Farm guard snapshot (must remain untouched)
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: wipe fixture ledgers and crops, then delete FH farms left by
# any previous run, and recreate everything from scratch.
foreach ($f in @($farmNoData, $farmIncome, $farmExpense, $farmMixed, $farmCats, $farmActivity, $farmFull)) {
    DeleteFarmTx $hdrA $f
    DeleteFhCrops $hdrA $f
}
DeleteFarmTx $hdrB $farmB
DeleteFhCrops $hdrB $farmB
foreach ($f in @($farmNoData, $farmIncome, $farmExpense, $farmMixed, $farmCats, $farmActivity, $farmFull)) {
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$farmNoData   = EnsureFarm $tokenA 'FH NoData Farm'
$farmIncome   = EnsureFarm $tokenA 'FH Income-Only Farm'
$farmExpense  = EnsureFarm $tokenA 'FH Expense-Only Farm'
$farmMixed    = EnsureFarm $tokenA 'FH Loss-BreakEven-Positive Farm'
$farmCats     = EnsureFarm $tokenA 'FH Categories Farm'
$farmActivity = EnsureFarm $tokenA 'FH Activity Farm'
$farmFull     = EnsureFarm $tokenA 'FH Completeness Farm'
$farmB        = EnsureFarm $tokenB 'FH User-B Farm'
Check 'SETUP.4 FH farms recreated empty' ($farmNoData -and $farmIncome -and $farmExpense -and $farmMixed -and $farmCats -and $farmActivity -and $farmFull -and $farmB)

$cropFull = CreateCrop $tokenA $farmFull 'FH Wheat Completeness' 1
Check 'SETUP.5 completeness fixture crop created' ([bool]$cropFull.id)
$cropB = CreateCrop $tokenB $farmB 'FH Wheat User-B' 1
Check 'SETUP.6 user-B fixture crop created' ([bool]$cropB.id)

$today = (Get-Date).ToUniversalTime().Date
$D = $today.AddDays(-40)
function Iso([DateTime]$d) { return $d.ToString('yyyy-MM-dd') }
$unknownFarm = [Guid]::NewGuid().ToString()
$unknownCrop = [Guid]::NewGuid().ToString()

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST A: authentication and ownership (401/404/403/400) ---"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health"
Check 'A.1 no token -> summary 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health/categories"
Check 'A.2 no token -> categories 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health/activity"
Check 'A.3 no token -> activity 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health/completeness"
Check 'A.4 no token -> completeness 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmFull/crops/$($cropFull.id)/financial-health"
Check 'A.5 no token -> crop health 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health" @{ Authorization = 'Bearer not.a.token' }
Check 'A.6 invalid token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$unknownFarm/financial-health" $hdrA
Check 'A.7 unknown farm -> 404' ($r.Status -eq 404) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/financial-health" $hdrA
Check 'A.8 foreign farm -> 403' ($r.Status -eq 403) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/financial-health/completeness" $hdrA
Check 'A.9 foreign farm completeness -> 403' ($r.Status -eq 403) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmFull/crops/$unknownCrop/financial-health" $hdrA
Check 'A.10 unknown crop -> 404' ($r.Status -eq 404) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/crops/$($cropFull.id)/financial-health" $hdrB
Check 'A.11 crop of another farm -> 400 (P9 convention)' ($r.Status -eq 400) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST B: NoData + LimitedData + foreign isolation ---"
$r = ApiCall 'GET' "$base/api/farms/$farmNoData/financial-health" $hdrA
Check 'B.1 no-data farm -> 200' ($r.Status -eq 200) "status=$($r.Status)"
Check 'B.2 indicator NoData' ($r.Data.healthIndicator -eq 'NoData') "got=$($r.Data.healthIndicator)"
Check 'B.3 zero totals' ($r.Data.totalIncome -eq 0 -and $r.Data.totalExpense -eq 0 -and $r.Data.netResult -eq 0)
Check 'B.4 zero counts and dates' ($r.Data.totalTransactionCount -eq 0 -and $null -eq $r.Data.firstTransactionDate -and $r.Data.numberOfActiveFinancialDays -eq 0)

$r = PostTx $hdrA $farmIncome 'Income' 'CropSale' 5000.00 (Iso $today.AddDays(-3))
Check 'B.5 income-only fixture created' ($r.Status -eq 200) "status=$($r.Status)"
$r = PostTx $hdrA $farmExpense 'Expense' 'Seeds' 3000.00 (Iso $today.AddDays(-3))
Check 'B.6 expense-only fixture created' ($r.Status -eq 200) "status=$($r.Status)"
$r = PostTx $hdrB $farmB 'Income' 'CropSale' 700.00 (Iso $today.AddDays(-2))
Check 'B.7 user-B fixture created' ($r.Status -eq 200) "status=$($r.Status)"

$r = ApiCall 'GET' "$base/api/farms/$farmIncome/financial-health" $hdrA
Check 'B.8 income-only -> LimitedData (expenses missing)' ($r.Data.healthIndicator -eq 'LimitedData') "got=$($r.Data.healthIndicator)"
Check 'B.9 income-only totals' ($r.Data.totalIncome -eq 5000 -and $r.Data.totalExpense -eq 0 -and $r.Data.netResult -eq 5000)
$r = ApiCall 'GET' "$base/api/farms/$farmExpense/financial-health" $hdrA
Check 'B.10 expense-only -> LimitedData (income missing)' ($r.Data.healthIndicator -eq 'LimitedData') "got=$($r.Data.healthIndicator)"
$r = ApiCall 'GET' "$base/api/farms/$farmB/financial-health" $hdrB
Check 'B.11 user-B single tx -> LimitedData (<5)' ($r.Data.healthIndicator -eq 'LimitedData' -and $r.Data.totalTransactionCount -eq 1) "got=$($r.Data.healthIndicator)"
$rawB = GetRaw "$base/api/farms/$farmB/financial-health" $hdrB
Check 'B.12 foreign isolation - user-A never sees user-B data' ((ApiCall 'GET' "$base/api/farms/$farmB/financial-health" $hdrA).Status -eq 403 -and ($rawB -notmatch '(?i)"userId"'))

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST C: positive / loss / break-even + date filtering + UserId leak ---"
$cm1 = Iso $D
$cm5 = Iso $D.AddDays(4)
$cm8 = Iso $D.AddDays(7)
# day1: 5 tx, net -19000 (loss range); day5: 6 tx, net 0 (break-even);
# day8: single income tx (limited-data range); overall net +41000.
$r = PostTx $hdrA $farmMixed 'Expense' 'Seeds' 15000.00 $cm1
$r = PostTx $hdrA $farmMixed 'Expense' 'Labour' 5000.00 $cm1
$r = PostTx $hdrA $farmMixed 'Expense' 'PestDiseaseManagement' 1000.00 $cm1
$r = PostTx $hdrA $farmMixed 'Expense' 'Fuel' 4000.00 $cm1
$r = PostTx $hdrA $farmMixed 'Income' 'CropSale' 6000.00 $cm1
$r = PostTx $hdrA $farmMixed 'Income' 'CropSale' 9000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Income' 'OtherIncome' 3000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Expense' 'Fertilizer' 7000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Expense' 'Irrigation' 5000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Expense' 'Transport' 2000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Income' 'LivestockIncome' 2000.00 $cm5
$r = PostTx $hdrA $farmMixed 'Income' 'CropSale' 60000.00 $cm8

$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health" $hdrA
Check 'C.1 full range -> PositiveNetResult' ($r.Data.healthIndicator -eq 'PositiveNetResult') "got=$($r.Data.healthIndicator)"
Check 'C.2 totals 80000 - 39000 = 41000' ($r.Data.totalIncome -eq 80000 -and $r.Data.totalExpense -eq 39000 -and $r.Data.netResult -eq 41000) "net=$($r.Data.netResult)"
Check 'C.3 counts per type' ($r.Data.incomeTransactionCount -eq 5 -and $r.Data.expenseTransactionCount -eq 7 -and $r.Data.totalTransactionCount -eq 12) "inc=$($r.Data.incomeTransactionCount) exp=$($r.Data.expenseTransactionCount)"
Check 'C.4 first/last dates and active days' (($r.Data.firstTransactionDate -match [regex]::Escape($cm1)) -and ($r.Data.lastTransactionDate -match [regex]::Escape($cm8)) -and $r.Data.numberOfActiveFinancialDays -eq 3) "first=$($r.Data.firstTransactionDate)"
Check 'C.5 all farm-level (no crop links)' ($r.Data.cropRelatedTransactionCount -eq 0 -and $r.Data.farmLevelTransactionCount -eq 12)

$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health?fromDate=$cm1&toDate=$cm5" $hdrA
Check 'C.6 range day1-5 -> LossRecorded (11 tx)' ($r.Data.healthIndicator -eq 'LossRecorded' -and $r.Data.netResult -eq -19000 -and $r.Data.totalTransactionCount -eq 11) "ind=$($r.Data.healthIndicator) net=$($r.Data.netResult) count=$($r.Data.totalTransactionCount)"
$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health?fromDate=$cm5&toDate=$cm5" $hdrA
Check 'C.7 range day5 -> BreakEven (6 tx)' ($r.Data.healthIndicator -eq 'BreakEven' -and $r.Data.netResult -eq 0 -and $r.Data.totalTransactionCount -eq 6) "ind=$($r.Data.healthIndicator) count=$($r.Data.totalTransactionCount)"
$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health?fromDate=$cm8&toDate=$cm8" $hdrA
Check 'C.8 single-tx range -> LimitedData' ($r.Data.healthIndicator -eq 'LimitedData' -and $r.Data.totalTransactionCount -eq 1) "ind=$($r.Data.healthIndicator)"
$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health?fromDate=$(Iso $today.AddDays(-1))&toDate=$(Iso $today)" $hdrA
Check 'C.9 empty range -> NoData with zero totals' ($r.Data.healthIndicator -eq 'NoData' -and $r.Data.totalTransactionCount -eq 0) "ind=$($r.Data.healthIndicator)"
$r = ApiCall 'GET' "$base/api/farms/$farmMixed/financial-health?fromDate=$cm5&toDate=$cm1" $hdrA
Check 'C.10 fromDate after toDate -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$rawMixed = GetRaw "$base/api/farms/$farmMixed/financial-health" $hdrA
Check 'C.11 response JSON never exposes userId/ownerId' (($rawMixed -notmatch '(?i)"userId"') -and ($rawMixed -notmatch '(?i)"ownerId"'))
Check 'C.12 explanation is factual (no advice keywords)' (($rawMixed -notmatch '(?i)(credit|loan|borrow|invest|approve|should)'))

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST D: category breakdown + percentages + date filtering ---"
$cd1 = Iso $today.AddDays(-20)
$cd2 = Iso $today.AddDays(-10)
PostTx $hdrA $farmCats 'Expense' 'Seeds' 4200.75 $cd1 | Out-Null
PostTx $hdrA $farmCats 'Expense' 'Labour' 7000.00 $cd1 | Out-Null
PostTx $hdrA $farmCats 'Expense' 'Fertilizer' 1500.00 $cd2 | Out-Null
PostTx $hdrA $farmCats 'Income' 'CropSale' 10000.00 $cd1 | Out-Null
PostTx $hdrA $farmCats 'Income' 'OtherIncome' 1500.00 $cd2 | Out-Null

$r = ApiCall 'GET' "$base/api/farms/$farmCats/financial-health/categories" $hdrA
Check 'D.1 categories -> 200' ($r.Status -eq 200) "status=$($r.Status)"
Check 'D.2 totals 11500 / 12700.75' ($r.Data.totalIncome -eq 11500 -and $r.Data.totalExpense -eq 12700.75) "inc=$($r.Data.totalIncome) exp=$($r.Data.totalExpense)"
$exp = AsArray $r.Data.expenses
$inc = AsArray $r.Data.income
Check 'D.3 three expense categories' ($exp.Count -eq 3) "count=$($exp.Count)"
Check 'D.4 two income categories' ($inc.Count -eq 2) "count=$($inc.Count)"
$labour = @($exp | Where-Object { $_.category -eq 'Labour' }) | Select-Object -First 1
$seeds = @($exp | Where-Object { $_.category -eq 'Seeds' }) | Select-Object -First 1
$fert = @($exp | Where-Object { $_.category -eq 'Fertilizer' }) | Select-Object -First 1
Check 'D.5 Labour amount + count' ($labour.amount -eq 7000 -and $labour.transactionCount -eq 1)
Check 'D.6 Labour percentage 55.11' ([Math]::Round($labour.percentage, 2) -eq 55.11) "pct=$($labour.percentage)"
Check 'D.7 Seeds percentage 33.07' ([Math]::Round($seeds.percentage, 2) -eq 33.07) "pct=$($seeds.percentage)"
Check 'D.8 Fertilizer percentage 11.81' ([Math]::Round($fert.percentage, 2) -eq 11.81) "pct=$($fert.percentage)"
$pctSum = ($exp | ForEach-Object { $_.percentage } | Measure-Object -Sum).Sum
Check 'D.9 expense percentages sum within rounding of 100' ([Math]::Abs($pctSum - 100) -le 0.05) "sum=$pctSum"
$cropsale = @($inc | Where-Object { $_.category -eq 'CropSale' }) | Select-Object -First 1
Check 'D.10 CropSale percentage 86.96 of income' ([Math]::Round($cropsale.percentage, 2) -eq 86.96) "pct=$($cropsale.percentage)"
$rawD11 = GetRaw "$base/api/farms/$farmCats/financial-health/categories?fromDate=$cd2&toDate=$cd2" $hdrA
$d11 = $rawD11 | ConvertFrom-Json
$ed = AsArray $d11.expenses
$id = AsArray $d11.income
Check 'D.11 day2-only filter: single expense at 100%' ($ed.Count -eq 1 -and $id.Count -eq 1 -and $ed[0].percentage -eq 100 -and $ed[0].amount -eq 1500) "exp=$($ed.Count) inc=$($id.Count) pct=$($ed[0].percentage)"
$dbExp = (SqlQuery "SELECT CAST(SUM(CASE Category WHEN 'Labour' THEN Amount ELSE 0 END) AS varchar) + '|' + CAST(SUM(CASE Category WHEN 'Seeds' THEN Amount ELSE 0 END) AS varchar) + '|' + CAST(SUM(CASE Category WHEN 'Fertilizer' THEN Amount ELSE 0 END) AS varchar) FROM FinancialTransactions WHERE FarmId='$farmCats'") -join ''
Check 'D.12 DB category sums match API' ($dbExp -eq '7000.00|4200.75|1500.00') "db=$dbExp"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST E: monthly activity buckets ---"
# Month-anchored fixture dates: day 5 of (month-2), days 6/7 of (month-1),
# and a safe day of the current month - guaranteed three distinct buckets.
$mPrev2 = $today.AddMonths(-2)
$mPrev1 = $today.AddMonths(-1)
$aa = Iso (Get-Date -Year $mPrev2.Year -Month $mPrev2.Month -Day 5)
$ab = Iso (Get-Date -Year $mPrev2.Year -Month $mPrev2.Month -Day 6)
$ac = Iso (Get-Date -Year $mPrev1.Year -Month $mPrev1.Month -Day 6)
$ad = Iso (Get-Date -Year $mPrev1.Year -Month $mPrev1.Month -Day 7)
$ae = Iso (Get-Date -Year $today.Year -Month $today.Month -Day ([Math]::Min($today.Day, 5)))
PostTx $hdrA $farmActivity 'Income' 'CropSale' 10000.00 $aa | Out-Null
PostTx $hdrA $farmActivity 'Expense' 'Seeds' 2000.00 $aa | Out-Null
PostTx $hdrA $farmActivity 'Income' 'OtherIncome' 500.00 $ab | Out-Null
PostTx $hdrA $farmActivity 'Expense' 'Labour' 3000.00 $ac | Out-Null
PostTx $hdrA $farmActivity 'Income' 'CropSale' 7000.00 $ad | Out-Null
PostTx $hdrA $farmActivity 'Income' 'CropSale' 100.00 $ae | Out-Null

$ma1 = $mPrev2.ToString('yyyy-MM')
$ma2 = $mPrev1.ToString('yyyy-MM')
$ma3 = $today.ToString('yyyy-MM')
$expectedPeriods = @($ma1, $ma2, $ma3) | Select-Object -Unique | Sort-Object

$r = ApiCall 'GET' "$base/api/farms/$farmActivity/financial-health/activity" $hdrA
Check 'E.1 activity -> 200' ($r.Status -eq 200) "status=$($r.Status)"
$periods = AsArray $r.Data.periods
Check 'E.2 expected monthly buckets' (($periods | ForEach-Object { $_.period } | Sort-Object) -join ',' -eq ($expectedPeriods -join ',')) "got=$(($periods | ForEach-Object { $_.period }) -join ',') want=$($expectedPeriods -join ',')"
$p1 = @($periods | Where-Object { $_.period -eq $ma1 }) | Select-Object -First 1
$p2 = @($periods | Where-Object { $_.period -eq $ma2 }) | Select-Object -First 1
$p3 = @($periods | Where-Object { $_.period -eq $ma3 }) | Select-Object -First 1
Check "E.3 bucket $ma1 income/expense/net/count" ($p1.income -eq 10500 -and $p1.expense -eq 2000 -and $p1.netResult -eq 8500 -and $p1.transactionCount -eq 3) "p1=$($p1 | ConvertTo-Json -Compress)"
Check "E.4 bucket $ma2 income/expense/net/count" ($p2.income -eq 7000 -and $p2.expense -eq 3000 -and $p2.netResult -eq 4000 -and $p2.transactionCount -eq 2) "p2=$($p2 | ConvertTo-Json -Compress)"
Check "E.5 bucket $ma3 single income" ($p3.income -eq 100 -and $p3.expense -eq 0 -and $p3.netResult -eq 100 -and $p3.transactionCount -eq 1) "p3=$($p3 | ConvertTo-Json -Compress)"
Check 'E.6 overall totals' ($r.Data.totalIncome -eq 17600 -and $r.Data.totalExpense -eq 5000 -and $r.Data.netResult -eq 12600 -and $r.Data.totalTransactionCount -eq 6) "net=$($r.Data.netResult)"
$fpRaw = GetRaw "$base/api/farms/$farmActivity/financial-health/activity?fromDate=$ac&toDate=$ad" $hdrA
$fpCount = ([regex]::Matches($fpRaw, '"period"\s*:')).Count
$fp = AsArray (($fpRaw | ConvertFrom-Json).periods)
Check 'E.7 date-filtered activity has single bucket' ($fpCount -eq 1 -and $fp.Count -eq 1 -and $fp[0].period -eq $ma2 -and $fp[0].netResult -eq 4000) "rawCount=$fpCount parsed=$($fp.Count)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST F: completeness (100) + crop financial health ---"
$fd = @((Iso $D), (Iso $D.AddDays(5)), (Iso $D.AddDays(10)), (Iso $D.AddDays(15)), (Iso $D.AddDays(20)), (Iso $D.AddDays(35)))
PostTx $hdrA $farmFull 'Income' 'CropSale' 20000.00 $fd[0] | Out-Null
PostTx $hdrA $farmFull 'Income' 'CropSale' 15000.00 $fd[1] | Out-Null
PostTx $hdrA $farmFull 'Income' 'CropSale' 10000.00 $fd[2] $cropFull.id | Out-Null
PostTx $hdrA $farmFull 'Income' 'OtherIncome' 5000.00 $fd[4] | Out-Null
PostTx $hdrA $farmFull 'Income' 'OtherIncome' 0.50 $fd[5] | Out-Null
PostTx $hdrA $farmFull 'Expense' 'Seeds' 8000.00 $fd[0] | Out-Null
PostTx $hdrA $farmFull 'Expense' 'Labour' 12000.00 $fd[1] | Out-Null
PostTx $hdrA $farmFull 'Expense' 'Fertilizer' 6000.00 $fd[3] $cropFull.id | Out-Null
PostTx $hdrA $farmFull 'Expense' 'Irrigation' 9000.00 $fd[4] | Out-Null
PostTx $hdrA $farmFull 'Expense' 'Fuel' 6300.00 $fd[5] | Out-Null

$r = ApiCall 'GET' "$base/api/farms/$farmFull/financial-health/completeness" $hdrA
Check 'F.1 completeness -> 200' ($r.Status -eq 200) "status=$($r.Status)"
Check 'F.2 score 100 (all five checks)' ($r.Data.score -eq 100) "score=$($r.Data.score)"
Check 'F.3 status Complete' ($r.Data.status -eq 'Complete') "status=$($r.Data.status)"
$checks = AsArray $r.Data.checks
Check 'F.4 five checks, all passed' ($checks.Count -eq 5 -and @($checks | Where-Object { -not $_.passed }).Count -eq 0) "count=$($checks.Count)"
Check 'F.5 mandatory disclaimer present' ($r.Data.disclaimer -eq 'Based only on transactions entered into SABZ.') "disclaimer=$($r.Data.disclaimer)"
$limits = AsArray $r.Data.limitations
Check 'F.6 limitations state not-credit/loan' (@($limits | Where-Object { $_ -match '(?i)credit|loan' }).Count -ge 1)
$rawFull = GetRaw "$base/api/farms/$farmFull/financial-health/completeness" $hdrA
Check 'F.7 completeness text never implies loan approval' (($rawFull -notmatch '(?i)(loan readiness|creditworthy|approved for|qualify)') -and ($rawFull -notmatch '(?i)"userId"'))

$r = ApiCall 'GET' "$base/api/farms/$farmFull/crops/$($cropFull.id)/financial-health" $hdrA
Check 'F.8 crop health -> 200' ($r.Status -eq 200) "status=$($r.Status)"
Check 'F.9 crop totals 10000/6000 -> net 4000' ($r.Data.totalIncome -eq 10000 -and $r.Data.totalExpense -eq 6000 -and $r.Data.netResult -eq 4000) "net=$($r.Data.netResult)"
Check 'F.10 crop counts/dates' ($r.Data.totalTransactionCount -eq 2 -and $r.Data.numberOfActiveFinancialDays -eq 2)
Check 'F.11 crop scope: all crop-related, farm-level zero' ($r.Data.cropRelatedTransactionCount -eq 2 -and $r.Data.farmLevelTransactionCount -eq 0)
Check 'F.12 crop indicator LimitedData (only 2 tx)' ($r.Data.healthIndicator -eq 'LimitedData') "ind=$($r.Data.healthIndicator)"
$r = ApiCall 'GET' "$base/api/farms/$farmFull/crops/$($cropFull.id)/financial-health?fromDate=$($fd[2])&toDate=$($fd[2])" $hdrA
Check 'F.13 crop health date filtering' ($r.Data.totalIncome -eq 10000 -and $r.Data.totalExpense -eq 0 -and $r.Data.totalTransactionCount -eq 1) "net=$($r.Data.netResult)"
$dbNet = (SqlQuery "SELECT CAST(SUM(CASE TransactionType WHEN 'Income' THEN Amount ELSE -Amount END) AS varchar) FROM FinancialTransactions WHERE FarmId='$farmFull' AND CropId IS NOT NULL") -join ''
Check 'F.14 DB crop-linked net matches API' ($dbNet -eq '4000.00') "db=$dbNet"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST G: completeness scores 0/20/40/60/80 across five farms ---"
$farmC0  = EnsureFarm $tokenA 'FH Completeness 0 Farm'
$farmC20 = EnsureFarm $tokenA 'FH Completeness 20 Farm'
$farmC40 = EnsureFarm $tokenA 'FH Completeness 40 Farm'
$farmC60 = EnsureFarm $tokenA 'FH Completeness 60 Farm'
$farmC80 = EnsureFarm $tokenA 'FH Completeness 80 Farm'
foreach ($f in @($farmC0, $farmC20, $farmC40, $farmC60, $farmC80)) { DeleteFarmTx $hdrA $f }

# 0: empty farm.
$r = ApiCall 'GET' "$base/api/farms/$farmC0/financial-health/completeness" $hdrA
Check 'G.1 score 0 + status NoData' ($r.Data.score -eq 0 -and $r.Data.status -eq 'NoData') "score=$($r.Data.score) status=$($r.Data.status)"

# 20: one income tx only (checks: exists).
PostTx $hdrA $farmC20 'Income' 'CropSale' 100.00 (Iso $today.AddDays(-1)) | Out-Null
$r = ApiCall 'GET' "$base/api/farms/$farmC20/financial-health/completeness" $hdrA
Check 'G.2 score 20 + status Partial' ($r.Data.score -eq 20 -and $r.Data.status -eq 'Partial') "score=$($r.Data.score)"

# 40: 11 tx over 2 dates (checks: exists, >=10 only).
PostTx $hdrA $farmC40 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-3)) | Out-Null
for ($i = 1; $i -le 5; $i++) { PostTx $hdrA $farmC40 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-2)) | Out-Null }
for ($i = 1; $i -le 5; $i++) { PostTx $hdrA $farmC40 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-2)) | Out-Null }
$r = ApiCall 'GET' "$base/api/farms/$farmC40/financial-health/completeness" $hdrA
Check 'G.3 score 40 (exists + count>=10)' ($r.Data.score -eq 40) "score=$($r.Data.score)"

# 40b: 8 tx over 2 dates, both types (checks: exists, both types only).
for ($i = 1; $i -le 4; $i++) { PostTx $hdrA $farmC60 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-10)) | Out-Null }
for ($i = 1; $i -le 4; $i++) { PostTx $hdrA $farmC60 'Expense' 'Seeds' 10.00 (Iso $today.AddDays(-9)) | Out-Null }
$r = ApiCall 'GET' "$base/api/farms/$farmC60/financial-health/completeness" $hdrA
Check 'G.4 score 40 (exists + both types only)' ($r.Data.score -eq 40) "score=$($r.Data.score)"

# 60: add one expense on a third date -> 3 active days (count still <10, span <30).
PostTx $hdrA $farmC60 'Expense' 'Fuel' 10.00 (Iso $today.AddDays(-8)) | Out-Null
$r = ApiCall 'GET' "$base/api/farms/$farmC60/financial-health/completeness" $hdrA
Check 'G.5 score 60 (exists + both types + 3 active days)' ($r.Data.score -eq 60) "score=$($r.Data.score)"

# 80: 11 tx, both types, 3 active days, but span only 12 days (<30).
PostTx $hdrA $farmC80 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-12)) | Out-Null
for ($i = 1; $i -le 5; $i++) { PostTx $hdrA $farmC80 'Income' 'CropSale' 10.00 (Iso $today.AddDays(-6)) | Out-Null }
for ($i = 1; $i -le 5; $i++) { PostTx $hdrA $farmC80 'Expense' 'Seeds' 10.00 (Iso $today) | Out-Null }
$r = ApiCall 'GET' "$base/api/farms/$farmC80/financial-health/completeness" $hdrA
Check 'G.6 score 80 (span<30 fails only)' ($r.Data.score -eq 80 -and $r.Data.status -eq 'Partial') "score=$($r.Data.score)"
$g7checks = AsArray $r.Data.checks
$failedOnly = @($g7checks | Where-Object { -not $_.passed })
Check 'G.7 exactly HistorySpan failed' ($failedOnly.Count -eq 1 -and $failedOnly[0].name -eq 'HistorySpan') "failed=$(($failedOnly | ForEach-Object { $_.name }) -join ',')"

# Cleanup the five score farms.
foreach ($f in @($farmC0, $farmC20, $farmC40, $farmC60, $farmC80)) {
    DeleteFarmTx $hdrA $f
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST H: Prompt 9 ledger CRUD + regression sanity ---"
$r = PostTx $hdrA $farmCats 'Expense' 'Transport' 100.25 (Iso $today.AddDays(-1))
Check 'H.1 P9 create still works' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txId = $r.Data.id
$body = @{ TransactionType = 'Expense'; Category = 'Transport'; Amount = 200.50; TransactionDate = (Iso $today.AddDays(-1)) } | ConvertTo-Json
$r = ApiCall 'PUT' "$base/api/transactions/$txId" $hdrA $body 'application/json'
Check 'H.2 P9 update still works' ($r.Status -eq 200 -and $r.Data.amount -eq 200.50) "status=$($r.Status)"
$r = ApiCall 'DELETE' "$base/api/transactions/$txId" $hdrA
Check 'H.3 P9 delete still works' ($r.Status -eq 204) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmCats/financial-summary" $hdrA
Check 'H.4 P9 financial-summary unaffected (income 11500)' ($r.Data.totalIncome -eq 11500 -and $r.Data.netProfitLoss -eq -1200.75) "net=$($r.Data.netProfitLoss)"

$provinces = AsArray ((GetRaw "$base/api/locations/provinces") | ConvertFrom-Json)
Check 'H.5 provinces regression (7)' ($provinces.Count -eq 7) "count=$($provinces.Count)"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'H.6 Ahmed Farm guard unchanged' ($ahmedBefore -eq $ahmedAfter)
$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
Check 'H.7 monitoring due reachable' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/notifications" $hdrA
Check 'H.8 notifications reachable' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmCats/crop-recommendations" $hdrA
Check 'H.9 recommendation reachable' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmCats/crop-suitability" $hdrA
Check 'H.10 suitability reachable' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmCats/weather/current" $hdrA
Check 'H.11 weather endpoint reachable (400 = farm has no GPS coords, pre-existing behavior)' ($r.Status -in @(200, 400, 502, 503)) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST I: integrity + cleanup ---"
foreach ($f in @($farmNoData, $farmIncome, $farmExpense, $farmMixed, $farmCats, $farmActivity, $farmFull)) {
    DeleteFarmTx $hdrA $f
    DeleteFhCrops $hdrA $f
    ApiCall 'DELETE' "$base/api/farms/$f" $hdrA | Out-Null
}
DeleteFarmTx $hdrB $farmB
DeleteFhCrops $hdrB $farmB
ApiCall 'DELETE' "$base/api/farms/$farmB" $hdrB | Out-Null

$fhFarmsLeft = (SqlQuery "SELECT COUNT(*) FROM Farms WHERE FarmName LIKE 'FH %'") -join ''
Check 'I.1 all FH fixture farms removed' ($fhFarmsLeft -eq '0') "left=$fhFarmsLeft"
$orphans = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Farms f ON t.FarmId=f.Id WHERE f.Id IS NULL") -join ''
Check 'I.2 no orphan transactions' ($orphans -eq '0') "orphans=$orphans"
$invented = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions WHERE FarmId IN (SELECT Id FROM Farms WHERE FarmName LIKE 'FH %')") -join ''
Check 'I.3 no leftover/invented FH transactions' ($invented -eq '0') "left=$invented"

Write-Host "`n=== RESULT: $pass passed, $fail failed ===" -ForegroundColor Cyan
exit $fail
