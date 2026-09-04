# =============================================================================
# SABZ Prompt 9 - Farm Profit & Loss (P&L) Financial Ledger
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Farmer-entered income/expense transactions only - the system never invents
# financial data. P&L summaries are computed dynamically, never persisted.
#
# Idempotency strategy: every run deletes leftover "FIN " fixture crops and
# every fixture-farm transaction through the public API, then recreates
# fixtures with dates relative to today. Seed/reference data is never touched.
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
    $tmp = Join-Path $env:TEMP ('fqbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# GET returning JSON - Invoke-WebRequest + ConvertFrom-Json so arrays are never
# silently unwrapped (Invoke-RestMethod's unwrapping corrupts list loops).
function GetJson([string]$url, $headers) {
    $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing
    return ($resp.Content | ConvertFrom-Json)
}

function TryGetJson([string]$url, $headers) {
    try { return (GetJson $url $headers) } catch { return $null }
}

# Normalise PS 5.1 pipeline artefacts into a real array (handles $null,
# single-element unwrapping and nested-array member enumeration).
function AsArray($x) {
    if ($null -eq $x) { return @() }
    $a = @($x)
    if ($a.Count -eq 1 -and $a[0] -is [System.Array]) { return @($a[0]) }
    if ($a.Count -eq 1 -and $null -eq $a[0]) { return @() }
    return $a
}

function EnsureFarm($token, $name) {
    $farms = AsArray (TryGetJson "$base/api/farms" @{ Authorization = "Bearer $token" })
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

function PostTx($headers, $farmId, $type, $category, $amount, $dateIso, $cropId, $notes) {
    $body = @{ TransactionType = $type; Category = $category; Amount = $amount }
    if ($dateIso) { $body.TransactionDate = $dateIso }
    if ($cropId) { $body.CropId = $cropId }
    if ($null -ne $notes) { $body.Notes = $notes }
    return ApiCall 'POST' "$base/api/farms/$farmId/transactions" $headers ($body | ConvertTo-Json) 'application/json'
}

function PutTx($headers, $txId, $type, $category, $amount, $dateIso, $cropId, $notes) {
    $body = @{ TransactionType = $type; Category = $category; Amount = $amount }
    if ($dateIso) { $body.TransactionDate = $dateIso }
    if ($cropId) { $body.CropId = $cropId }
    if ($null -ne $notes) { $body.Notes = $notes }
    return ApiCall 'PUT' "$base/api/transactions/$txId" $headers ($body | ConvertTo-Json) 'application/json'
}

# Remove every transaction of a farm through the public API (idempotency).
function DeleteFinTransactions($headers, $farmId) {
    $list = AsArray (TryGetJson "$base/api/farms/$farmId/transactions?take=100" $headers)
    foreach ($t in $list) {
        ApiCall 'DELETE' "$base/api/transactions/$($t.id)" $headers | Out-Null
    }
}

function DeleteFinCrops($token, $farmId) {
    $crops = AsArray (TryGetJson "$base/api/farms/$farmId/crops" @{ Authorization = "Bearer $token" })
    $fins = @($crops | Where-Object { $_.cropName -like 'FIN *' })
    foreach ($c in $fins) {
        try {
            Invoke-WebRequest -Uri "$base/api/crops/$($c.id)" -Method Delete -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing | Out-Null
        } catch {
            $code = 0
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            if ($code -ne 404) { Write-Host "  WARN  cleanup delete '$($c.cropName)' -> $code" -ForegroundColor Yellow }
        }
    }
}

Write-Host "`n=== SABZ Prompt 9: Farm P&L Financial Ledger Tests ===" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Setup: logins, farms, fixture crops, deterministic empty ledger state
# -----------------------------------------------------------------------------
Write-Host "`n--- Setup ---"
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
Check 'SETUP.1 User A login' ([bool]$tokenA)
Check 'SETUP.2 User B login' ([bool]$tokenB)
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }

$farmA = EnsureFarm $tokenA 'FIN Ledger Test Farm'
$farmB = EnsureFarm $tokenB 'FIN User-B Test Farm'
Check 'SETUP.3 Farm A ready' ([bool]$farmA) "farmA=$farmA"
Check 'SETUP.4 Farm B ready' ([bool]$farmB)

# Ahmed Farm guard snapshot (must remain untouched)
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: clear leftover ledger rows, then fixture crops
DeleteFinTransactions $hdrA $farmA
DeleteFinTransactions $hdrB $farmB
DeleteFinCrops $tokenA $farmA
DeleteFinCrops $tokenB $farmB

$cropA = CreateCrop $tokenA $farmA 'FIN Wheat Ledger' 1 $null
$cropA2 = CreateCrop $tokenA $farmA 'FIN Wheat SetNull' 1 $null
$cropB = CreateCrop $tokenB $farmB 'FIN Wheat User-B' 1 $null
Check 'SETUP.5 fixture crop A created' ([bool]$cropA.id)
Check 'SETUP.6 fixture crop A2 created' ([bool]$cropA2.id)
Check 'SETUP.7 fixture crop B created' ([bool]$cropB.id)

$startEmpty = AsArray (TryGetJson "$base/api/farms/$farmA/transactions" $hdrA)
Check 'SETUP.8 farm A ledger starts empty' ($startEmpty.Count -eq 0) "count=$($startEmpty.Count)"

$today = (Get-Date).ToUniversalTime().Date
$d30 = $today.AddDays(-30).ToString('yyyy-MM-dd')
$d12 = $today.AddDays(-12).ToString('yyyy-MM-dd')
$d10 = $today.AddDays(-10).ToString('yyyy-MM-dd')
$d5  = $today.AddDays(-5).ToString('yyyy-MM-dd')
$d4  = $today.AddDays(-4).ToString('yyyy-MM-dd')
$d2  = $today.AddDays(-2).ToString('yyyy-MM-dd')
$d1  = $today.AddDays(-1).ToString('yyyy-MM-dd')
$dFuture = $today.AddDays(10).ToString('yyyy-MM-dd')

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: create income/expense transactions (system never invents data) ---"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 12500.50 $d30 $cropA.id 'certified wheat seed'
Check 'T1.1 create expense (Seeds, crop-scoped) -> 200' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txSeeds = $r.Data
Check 'T1.2 fields echoed correctly' ($txSeeds.transactionType -eq 'Expense' -and $txSeeds.category -eq 'Seeds' -and $txSeeds.amount -eq 12500.50 -and $txSeeds.farmId -eq $farmA -and $txSeeds.cropId -eq $cropA.id) "amount=$($txSeeds.amount)"
Check 'T1.3 notes echoed' ($txSeeds.notes -eq 'certified wheat seed')
Check 'T1.4 createdAt populated' ([bool]$txSeeds.createdAt)
$rawTx = (Invoke-WebRequest -Uri "$base/api/transactions/$($txSeeds.id)" -Headers $hdrA -UseBasicParsing).Content
Check 'T1.5 response never exposes userId/ownerId' (($rawTx -notmatch '"userId"') -and ($rawTx -notmatch '"ownerId"'))
Check 'T1.6 transactionDate stored as the farmer date' ($rawTx -match [regex]::Escape("`"transactionDate`":`"$d30")) "raw=$rawTx"

$r = PostTx $hdrA $farmA 'Expense' 'Labour' 20000.00 $d10 $null $null
Check 'T1.7 create farm-level expense (no crop) -> 200' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txLabour = $r.Data
Check 'T1.8 cropId null for farm-level entry' ($null -eq $txLabour.cropId)

$r = PostTx $hdrA $farmA 'Income' 'CropSale' 50000.00 $d5 $cropA.id 'wheat sold at mandi'
Check 'T1.9 create income (CropSale) -> 200' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txSale = $r.Data

$r = PostTx $hdrA $farmA 'Income' 'OtherIncome' 500.00 $d2 $null $null
Check 'T1.10 create income (OtherIncome) -> 200' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txOther = $r.Data

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: validation rejects bad financial input (400/404) ---"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' -5.00 $d1 $null $null
Check 'T2.1 negative amount rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 0 $d1 $null $null
Check 'T2.2 zero amount rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 1000000000.01 $d1 $null $null
Check 'T2.3 amount above PKR 1,000,000,000 rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 100 $dFuture $null $null
Check 'T2.4 future TransactionDate rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Transfer' 'Seeds' 100 $d1 $null $null
Check 'T2.5 unknown transaction type rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 100 $d1 $cropB.id $null
Check 'T2.6 crop of another farm rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 100 $d1 ([Guid]::NewGuid().ToString()) $null
Check 'T2.7 nonexistent crop rejected (404)' ($r.Status -eq 404) "status=$($r.Status)"
$body = @{ TransactionType = 'Expense'; Amount = 100; TransactionDate = $d1 } | ConvertTo-Json
$r = ApiCall 'POST' "$base/api/farms/$farmA/transactions" $hdrA $body 'application/json'
Check 'T2.8 missing category rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'CropSale' 100 $d1 $null $null
Check 'T2.9 income category on expense rejected' ($r.Status -eq 400) "status=$($r.Status)"
$r = PostTx $hdrA $farmA 'Expense' 'Seeds' 100 $d1 $null ('x' * 1001)
Check 'T2.10 notes over 1000 chars rejected' ($r.Status -eq 400) "status=$($r.Status)"
$body = @{ TransactionType = 'Expense'; Category = 'Seeds'; Amount = 100 } | ConvertTo-Json
$r = ApiCall 'POST' "$base/api/farms/$farmA/transactions" $hdrA $body 'application/json'
Check 'T2.11 missing TransactionDate rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: list + filters + take semantics (default 50 / max 100) ---"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions" $hdrA)
Check 'T3.1 unfiltered list returns the 4 fixtures' ($list.Count -eq 4) "count=$($list.Count)"
Check 'T3.2 newest TransactionDate first' ($list[0].id -eq $txOther.id) "first=$($list[0].id)"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions?type=Expense" $hdrA)
Check 'T3.3 type=Expense filter' ($list.Count -eq 2) "count=$($list.Count)"
$rawSeeds = (Invoke-WebRequest -Uri "$base/api/farms/$farmA/transactions?category=Seeds" -Headers $hdrA -UseBasicParsing).Content
$seedsCount = ([regex]::Matches($rawSeeds, '"id"\s*:')).Count
Check 'T3.4 category=Seeds filter' ($seedsCount -eq 1) "count=$seedsCount raw=$rawSeeds"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions?cropId=$($cropA.id)" $hdrA)
Check 'T3.5 cropId filter (2 crop-scoped entries)' ($list.Count -eq 2) "count=$($list.Count)"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions?fromDate=$d12&toDate=$d4" $hdrA)
Check 'T3.6 date-range filter' ($list.Count -eq 2) "count=$($list.Count)"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions?take=2" $hdrA)
Check 'T3.7 take=2 caps the list' ($list.Count -eq 2) "count=$($list.Count)"
$list = AsArray (TryGetJson "$base/api/farms/$farmA/transactions?take=200" $hdrA)
Check 'T3.8 take above max still returns all (capped at 100)' ($list.Count -eq 4) "count=$($list.Count)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/transactions?take=0" $hdrA
Check 'T3.9 take=0 rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: dynamic P&L summary (never persisted) ---"
$s = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary" $hdrA
Check 'T4.1 totalIncome = 50500' ($s.Status -eq 200 -and $s.Data.totalIncome -eq 50500.00) "got=$($s.Data.totalIncome)"
Check 'T4.2 totalExpenses = 32500.50' ($s.Data.totalExpenses -eq 32500.50) "got=$($s.Data.totalExpenses)"
Check 'T4.3 netProfitLoss = income - expenses, count = 4' ($s.Data.netProfitLoss -eq 17999.50 -and $s.Data.transactionCount -eq 4) "net=$($s.Data.netProfitLoss) count=$($s.Data.transactionCount)"
$s = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary?fromDate=$d12&toDate=$d4" $hdrA
Check 'T4.4 date-range summary (2 entries)' ($s.Data.transactionCount -eq 2 -and $s.Data.totalIncome -eq 50000.00 -and $s.Data.totalExpenses -eq 20000.00) "count=$($s.Data.transactionCount)"
$s = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary?cropId=$($cropA.id)" $hdrA
Check 'T4.5 crop-scoped summary' ($s.Data.totalIncome -eq 50000.00 -and $s.Data.totalExpenses -eq 12500.50 -and $s.Data.netProfitLoss -eq 37499.50) "net=$($s.Data.netProfitLoss)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary" $hdrB
Check 'T4.6 summary of another user farm -> 403' ($r.Status -eq 403) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: authentication and ownership ---"
$r = ApiCall 'POST' "$base/api/farms/$farmA/transactions" @{} (@{ TransactionType = 'Expense'; Category = 'Seeds'; Amount = 1; TransactionDate = $d1 } | ConvertTo-Json) 'application/json'
Check 'T5.1 POST without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/transactions" @{}
Check 'T5.2 list without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/transactions/$($txSeeds.id)" @{}
Check 'T5.3 get by id without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'PUT' "$base/api/transactions/$($txSeeds.id)" @{} (@{ TransactionType = 'Expense'; Category = 'Seeds'; Amount = 1; TransactionDate = $d1 } | ConvertTo-Json) 'application/json'
Check 'T5.4 PUT without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'DELETE' "$base/api/transactions/$($txSeeds.id)" @{}
Check 'T5.5 DELETE without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary" @{}
Check 'T5.6 summary without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/transactions/$([Guid]::NewGuid())" $hdrA
Check 'T5.7 unknown transaction id -> 404' ($r.Status -eq 404) "status=$($r.Status)"
$r = ApiCall 'PUT' "$base/api/transactions/$([Guid]::NewGuid())" $hdrA (@{ TransactionType = 'Expense'; Category = 'Seeds'; Amount = 1; TransactionDate = $d1 } | ConvertTo-Json) 'application/json'
Check 'T5.8 PUT unknown id -> 404' ($r.Status -eq 404) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/transactions/$($txSeeds.id)" $hdrB
Check 'T5.9 user B reading user A transaction -> 403' ($r.Status -eq 403) "status=$($r.Status)"
$r = ApiCall 'DELETE' "$base/api/transactions/$($txSeeds.id)" $hdrB
Check 'T5.10 user B deleting user A transaction -> 403' ($r.Status -eq 403) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: full PUT update with ownership re-validation ---"
$r = PutTx $hdrA $txLabour.id 'Expense' 'Fuel' 18000.25 $d10 $cropA.id 'updated: diesel for tractor'
Check 'T6.1 full PUT replace -> 200' ($r.Status -eq 200) "status=$($r.Status)"
$g = ApiCall 'GET' "$base/api/transactions/$($txLabour.id)" $hdrA
Check 'T6.2 PUT persisted type/category/amount' ($g.Data.category -eq 'Fuel' -and $g.Data.amount -eq 18000.25 -and $g.Data.transactionType -eq 'Expense') "cat=$($g.Data.category) amt=$($g.Data.amount)"
Check 'T6.3 PUT persisted crop + notes' ($g.Data.cropId -eq $cropA.id -and $g.Data.notes -eq 'updated: diesel for tractor')
Check 'T6.4 updatedAt set by update' ([bool]$g.Data.updatedAt)
$r = PutTx $hdrA $txLabour.id 'Expense' 'Fuel' 18000.25 $d10 $cropB.id $null
Check 'T6.5 PUT with another farm crop rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$r = PutTx $hdrA $txLabour.id 'Expense' 'CropSale' 18000.25 $d10 $null $null
Check 'T6.6 PUT with type/category mismatch rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$g = ApiCall 'GET' "$base/api/transactions/$($txLabour.id)" $hdrA
Check 'T6.7 failed PUT leaves original intact' ($g.Data.category -eq 'Fuel' -and $g.Data.amount -eq 18000.25) "cat=$($g.Data.category)"
$r = PutTx $hdrA $txLabour.id 'Expense' 'Fuel' 18000.25 $dFuture $null $null
Check 'T6.8 PUT future date rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$r = PutTx $hdrA $txLabour.id 'Expense' 'Fuel' 0 $d10 $null $null
Check 'T6.9 PUT zero amount rejected (400)' ($r.Status -eq 400) "status=$($r.Status)"
$r = PutTx $hdrB $txLabour.id 'Expense' 'Fuel' 18000.25 $d10 $null $null
Check 'T6.10 user B PUT on user A transaction -> 403' ($r.Status -eq 403) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: crop deletion keeps financial history (SetNull semantics) ---"
$r = PostTx $hdrA $farmA 'Expense' 'Fertilizer' 750.25 $d1 $cropA2.id 'urea top dressing'
Check 'T7.1 crop-scoped transaction on crop A2 created' ($r.Status -eq 200 -and $r.Data.id) "status=$($r.Status)"
$txFert = $r.Data
$del = ApiCall 'DELETE' "$base/api/crops/$($cropA2.id)" $hdrA
Check 'T7.2 crop A2 deleted -> 204' ($del.Status -eq 204) "status=$($del.Status)"
$g = ApiCall 'GET' "$base/api/transactions/$($txFert.id)" $hdrA
Check 'T7.3 transaction survives with cropId null, amount intact' ($g.Status -eq 200 -and $null -eq $g.Data.cropId -and $g.Data.amount -eq 750.25) "status=$($g.Status) crop=$($g.Data.cropId)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: farm deletion cascades its ledger; isolation between users ---"
$farmC = EnsureFarm $tokenA 'FIN Cascade Test Farm'
$cropC = CreateCrop $tokenA $farmC 'FIN Cascade Crop' 1 $null
$r = PostTx $hdrA $farmC 'Expense' 'Fuel' 123.45 $d1 $null $null
Check 'T8.1 cascade farm + crop + transaction ready' (([bool]$farmC) -and ([bool]$cropC.id) -and ($r.Status -eq 200)) "status=$($r.Status)"
$farmB2 = EnsureFarm $tokenB 'FIN User-B Cascade Farm'
$rB = PostTx $hdrB $farmB2 'Income' 'CropSale' 999.99 $d1 $null $null
Check 'T8.2 user B transaction on own cascade farm' ($rB.Status -eq 200 -and $rB.Data.id) "status=$($rB.Status)"
$del = ApiCall 'DELETE' "$base/api/farms/$farmC" $hdrA
$leftoverC = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions WHERE FarmId='$farmC'") -join ''
Check 'T8.3 farm C deleted -> 204' ($del.Status -eq 204) "status=$($del.Status)"
Check 'T8.4 farm C ledger cascade-deleted (0 rows)' ($leftoverC -eq '0') "rows=$leftoverC"
$del = ApiCall 'DELETE' "$base/api/farms/$farmB2" $hdrB
Check 'T8.5 user B cascade farm deleted -> 204' ($del.Status -eq 204) "status=$($del.Status)"
$farmB = EnsureFarm $tokenB 'FIN User-B Test Farm'
$listA = AsArray (TryGetJson "$base/api/farms/$farmA/transactions" $hdrA)
Check 'T8.6 other users/farms ledger untouched (A still 5)' ($listA.Count -eq 5) "count=$($listA.Count)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: regressions (Prompts 4-8 intact) ---"
$p = AsArray (TryGetJson "$base/api/locations/provinces" $hdrA)
Check 'R1 provinces seed intact (7)' ($p.Count -eq 7) "count=$($p.Count)"
$r = ApiCall 'GET' "$base/api/monitoring/upcoming" $hdrA
Check 'R2 monitoring upcoming endpoint healthy (P7)' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/notifications" $hdrA
Check 'R3 notifications endpoint healthy (P8)' ($r.Status -eq 200) "status=$($r.Status)"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'R4 Ahmed Farm guard row untouched' ($ahmedBefore -eq $ahmedAfter -and $ahmedBefore -ne '') "before=$ahmedBefore after=$ahmedAfter"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crops" $hdrA
Check 'R5 crop list endpoint healthy (P5)' ($r.Status -eq 200) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmA" $hdrA
Check 'R6 farm read endpoint healthy (P4)' ($r.Status -eq 200) "status=$($r.Status)"
$ahmedTx = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions WHERE FarmId='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'R7 no transactions invented for Ahmed Farm' ($ahmedTx -eq '0') "rows=$ahmedTx"
$futureTx = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions WHERE TransactionDate > CAST(SYSUTCDATETIME() AS date)") -join ''
Check 'R8 no future-dated transactions in DB' ($futureTx -eq '0') "rows=$futureTx"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: database integrity ---"
$orphanFarm = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Farms f ON t.FarmId=f.Id WHERE f.Id IS NULL") -join ''
Check 'I1 no orphaned FarmId references' ($orphanFarm -eq '0') "rows=$orphanFarm"
$orphanCrop = (SqlQuery "SELECT COUNT(*) FROM FinancialTransactions t LEFT JOIN Crops c ON t.CropId=c.Id WHERE t.CropId IS NOT NULL AND c.Id IS NULL") -join ''
Check 'I2 no orphaned CropId references' ($orphanCrop -eq '0') "rows=$orphanCrop"
$idx = (SqlQuery "SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_FinancialTransactions_FarmId_TransactionDate','IX_FinancialTransactions_FarmId_TransactionType','IX_FinancialTransactions_CropId')") -join ''
Check 'I3 all three P&L indexes present' ($idx -eq '3') "idx=$idx"
$moneyType = (SqlQuery "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables tb ON c.object_id=tb.object_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id WHERE tb.name='FinancialTransactions' AND c.name='Amount' AND ty.name='decimal' AND c.precision=18 AND c.scale=2") -join ''
Check 'I4 Amount stored as decimal(18,2)' ($moneyType -eq '1') "rows=$moneyType"

# -----------------------------------------------------------------------------
Write-Host "`n--- Cleanup verification (next run starts deterministic) ---"
$s = ApiCall 'GET' "$base/api/farms/$farmA/financial-summary" $hdrA
Check 'C1 summary after crop A2 delete keeps SetNull row (income 50500 / expenses 31251 / 5 rows)' ($s.Data.totalIncome -eq 50500.00 -and $s.Data.totalExpenses -eq 31251.00 -and $s.Data.transactionCount -eq 5) "inc=$($s.Data.totalIncome) exp=$($s.Data.totalExpenses) n=$($s.Data.transactionCount)"
$listB = AsArray (TryGetJson "$base/api/farms/$farmB/transactions" $hdrB)
Check 'C2 recreated user B farm ledger empty' ($listB.Count -eq 0) "count=$($listB.Count)"
$netDb = (SqlQuery "SELECT CAST(SUM(CASE WHEN TransactionType='Income' THEN Amount ELSE -Amount END) AS varchar) FROM FinancialTransactions WHERE FarmId='$farmA'") -join ''
Check 'C3 DB-side dynamic P&L matches API summary' ([decimal]$netDb -eq $s.Data.netProfitLoss) "db=$netDb api=$($s.Data.netProfitLoss)"

Write-Host "`n=== RESULTS: $pass passed, $fail failed ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
exit $fail
