# =============================================================================
# SABZ Prompt 16 - Precision Crop Input & Dosage Calculator
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Sections: setup (7), authentication (2), basic calculation (10),
# unit conversion (6), canonical normalization (3), validation (10),
# ownership (4), crop security (3), read-only guarantee (15),
# response security (3), teardown (2).
#
# Idempotency strategy: every run deletes leftover "IC " fixture farms of the
# fixture users through the public API (their crops first), then recreates
# fixtures. The zero-area test flips the fixture farm's FarmSize via SQL and
# always restores it. The calculator itself must never write anything.
# Seed/reference data and other users' content are never touched.
# =============================================================================
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5073'
$pass = 0
$fail = 0
$prefix = 'IC '
$times = [char]0x00D7   # multiplication sign used in calculationFormula

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { $script:pass++; Write-Host "  PASS  $name" -ForegroundColor Green }
    else            { $script:fail++; Write-Host "  FAIL  $name  $detail" -ForegroundColor Red }
}

function Near($actual, $expected) {
    try { return ([math]::Abs([decimal]$actual - [decimal]$expected) -lt [decimal]0.0001) } catch { return $false }
}

function SqlQuery([string]$sql) {
    $tmp = Join-Path $env:TEMP ('icq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

function ApiCall([string]$method, [string]$url, $headers = @{}, $body = $null) {
    try {
        $params = @{ Uri = $url; Method = $method; UseBasicParsing = $true; Headers = $headers }
        if ($null -ne $body) { $params.Body = $body; $params.ContentType = 'application/json' }
        $resp = Invoke-WebRequest @params
        $data = $null
        if ($resp.Content) { try { $data = $resp.Content | ConvertFrom-Json } catch { $data = $resp.Content } }
        return @{ Status = [int]$resp.StatusCode; Data = $data; Raw = [string]$resp.Content; Error = $null }
    } catch {
        $status = 0
        $data = $null
        $raw = ''
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $raw = $reader.ReadToEnd()
                if ($raw) { $data = $raw | ConvertFrom-Json }
            } catch { }
        }
        return @{ Status = $status; Data = $data; Raw = $raw; Error = $_.Exception.Message }
    }
}

function GetJson([string]$url, $headers) {
    $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing
    return ($resp.Content | ConvertFrom-Json)
}

# Normalise PS 5.1 pipeline artefacts into a real array.
function AsArray($x) {
    if ($null -eq $x) { return ,@() }
    $a = @($x)
    if ($a.Count -eq 1 -and $a[0] -is [System.Array]) { return ,@($a[0]) }
    if ($a.Count -eq 1 -and $null -eq $a[0]) { return ,@() }
    return ,$a
}

function NewFarmBody([string]$name, [decimal]$size, [string]$unit) {
    return (@{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        FarmSize = $size; FarmSizeUnit = $unit; SoilType = 'Loamy'; IrrigationType = 'Canal'
    } | ConvertTo-Json)
}

function CreateFarm($headers, [string]$name, [decimal]$size, [string]$unit) {
    $r = ApiCall 'POST' "$base/api/farms" $headers (NewFarmBody $name $size $unit)
    if ($r.Status -ne 200) { return $null }
    return $r.Data.id
}

function CreateCrop($headers, $farmId, [string]$name) {
    # Future planting date: generated monitoring checks stay Upcoming, so no
    # permanently-overdue debris is left for the monitoring regressions.
    $body = @{ CropName = $name; Season = 'Rabi'; CropCatalogId = 1; PlantingDate = (Get-Date).ToUniversalTime().Date.AddDays(30).ToString('yyyy-MM-dd') } | ConvertTo-Json
    $r = ApiCall 'POST' "$base/api/farms/$farmId/crops" $headers $body
    if ($r.Status -ne 200) { return $null }
    return $r.Data.id
}

# Delete every "IC " fixture farm (crops first) of this user (idempotency).
function CleanupIcFarms($headers) {
    $removed = 0
    try { $farms = AsArray (GetJson "$base/api/farms" $headers) } catch { return 0 }
    $mine = @($farms | Where-Object { $_.farmName -like "$script:prefix*" })
    foreach ($f in $mine) {
        try {
            $crops = AsArray (GetJson "$base/api/farms/$($f.id)/crops" $headers)
            foreach ($c in $crops) { ApiCall 'DELETE' "$base/api/crops/$($c.id)" $headers | Out-Null }
        } catch { }
        ApiCall 'DELETE' "$base/api/farms/$($f.id)" $headers | Out-Null
        $removed++
    }
    return $removed
}

function Calc($headers, $farmId, [hashtable]$overrides = @{}, [string[]]$remove = @()) {
    $body = @{
        InputName  = "$script:prefix" + 'Urea'
        Category   = 'Fertilizer'
        DosageRate = 2
        DosageUnit = 'Kg'
        DosageBasis = 'PerAcre'
    }
    foreach ($k in @($body.Keys)) { if ($remove -contains $k) { $body.Remove($k) } }
    foreach ($k in $overrides.Keys) { $body[$k] = $overrides[$k] }
    return ApiCall 'POST' "$base/api/farms/$farmId/input-calculator" $headers ($body | ConvertTo-Json)
}

Write-Host ''
Write-Host '=================================================================='
Write-Host ' SABZ Prompt 16 - Precision Input & Dosage Calculator Test Suite'
Write-Host '=================================================================='

# --- Fixtures ----------------------------------------------------------------
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
$regBody = @{ FullName = 'Test Farmer C'; Email = 'userc3@example.com'; Password = 'Test1234!'; ConfirmPassword = 'Test1234!' } | ConvertTo-Json
ApiCall 'POST' "$base/api/auth/register" @{} $regBody | Out-Null
$tokenC = Login 'userc3@example.com' 'Test1234!'

if (-not $tokenA -or -not $tokenB -or -not $tokenC) {
    Write-Host 'FATAL: fixture login failed (test21 / userb3 / userc3).' -ForegroundColor Red
    exit 1
}
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }
$hdrC = @{ Authorization = "Bearer $tokenC" }

$cleaned = (CleanupIcFarms $hdrA) + (CleanupIcFarms $hdrB) + (CleanupIcFarms $hdrC)
Write-Host "Fixture cleanup: removed $cleaned leftover IC farms."

$farmAcres   = CreateFarm $hdrA ($prefix + 'Test Farm Acres') 5 'Acres'
$farmHectares = CreateFarm $hdrA ($prefix + 'Test Farm Hectares') 4 'Hectares'
$farmB       = CreateFarm $hdrB ($prefix + 'Test Farm B') 5 'Acres'
$cropA       = CreateCrop $hdrA $farmAcres ($prefix + 'Wheat A')
$cropB       = CreateCrop $hdrB $farmB ($prefix + 'Wheat B')

Write-Host ''
Write-Host '--- Setup ---'
Check 'SETUP.1 user A acres farm created'    ([bool]$farmAcres)
Check 'SETUP.2 user A hectares farm created' ([bool]$farmHectares)
Check 'SETUP.3 user B farm created'          ([bool]$farmB)
Check 'SETUP.4 user A crop created'          ([bool]$cropA)
Check 'SETUP.5 user B crop created'          ([bool]$cropB)

$me = $null
try { $me = GetJson "$base/api/auth/me" $hdrA } catch { }
Check 'SETUP.6 current user profile reachable' ([bool]$me)
$userIdA = if ($me -and $me.id) { [string]$me.id } else { '' }

# Ahmed Farm guard snapshot (must remain untouched).
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + FarmSizeUnit FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# --- 1. Authentication (2) ----------------------------------------------------
Write-Host ''
Write-Host '--- 1. Authentication ---'
Check '1.1 no token -> 401'      ((ApiCall 'POST' "$base/api/farms/$farmAcres/input-calculator" @{} (@{ InputName = 'x'; Category = 'Fertilizer'; DosageRate = 1; DosageUnit = 'Kg'; DosageBasis = 'PerAcre' } | ConvertTo-Json)).Status -eq 401)
Check '1.2 malformed token -> 401' ((ApiCall 'POST' "$base/api/farms/$farmAcres/input-calculator" @{ Authorization = 'Bearer not.a.jwt' } (@{ InputName = 'x'; Category = 'Fertilizer'; DosageRate = 1; DosageUnit = 'Kg'; DosageBasis = 'PerAcre' } | ConvertTo-Json)).Status -eq 401)

# --- 2. Basic calculation, no conversion (10) ---------------------------------
Write-Host ''
Write-Host '--- 2. Basic calculation (farm unit == dosage basis) ---'
$r = Calc $hdrA $farmAcres
Check '2.1 acres x per-acre -> 200' ($r.Status -eq 200) "status=$($r.Status) raw=$($r.Raw)"
Check '2.2 quantity = 5 Acres x 2 Kg/acre = 10 Kg' (Near $r.Data.requiredQuantity 10) "got $($r.Data.requiredQuantity)"
Check '2.3 no conversion applied' ($r.Data.conversionApplied -eq $false)
Check '2.4 calculation area equals recorded farm area' ((Near $r.Data.calculationArea 5) -and ($r.Data.calculationAreaUnit -eq 'Acres') -and (Near $r.Data.farmArea 5) -and ($r.Data.farmAreaUnit -eq 'Acres'))
Check '2.5 output unit equals dosage unit' (($r.Data.requiredQuantityUnit -eq 'Kg') -and ($r.Data.dosageUnit -eq 'Kg'))
Check '2.6 formula is human readable' ($r.Data.calculationFormula -like ("*5 Acres*$times*2 Kg/acre*= 10 Kg*")) "got '$($r.Data.calculationFormula)'"
Check '2.7 disclaimer present' ($r.Data.disclaimer -like '*product label*')
Check '2.8 echo of farm id and input name' (($r.Data.farmId -eq $farmAcres) -and ($r.Data.inputName -eq ($prefix + 'Urea')) -and ($r.Data.category -eq 'Fertilizer'))

$rh = Calc $hdrA $farmHectares @{ DosageRate = 3; DosageUnit = 'Liters'; DosageBasis = 'PerHectare'; Category = 'Herbicide'; InputName = ($prefix + 'Glyphosate') }
Check '2.9 hectares x per-hectare -> 200, 12 Liters' (($rh.Status -eq 200) -and (Near $rh.Data.requiredQuantity 12) -and ($rh.Data.conversionApplied -eq $false)) "status=$($rh.Status) qty=$($rh.Data.requiredQuantity)"
Check '2.10 hectares formula correct' ($rh.Data.calculationFormula -like ("*4 Hectares*$times*3 Liters/hectare*= 12 Liters*")) "got '$($rh.Data.calculationFormula)'"

# --- 3. Unit conversion (6) ----------------------------------------------------
Write-Host ''
Write-Host '--- 3. Area conversion (farm unit != dosage basis) ---'
$rc1 = Calc $hdrA $farmHectares @{ DosageRate = 2; DosageUnit = 'Kg'; DosageBasis = 'PerAcre' }
# 4 ha x 2.47105 = 9.8842 acres -> x 2 Kg = 19.7684 -> 19.77 Kg
Check '3.1 hectares farm, per-acre basis -> 200' ($rc1.Status -eq 200) "status=$($rc1.Status)"
Check '3.2 conversion applied with documented constant 2.47105' (($rc1.Data.conversionApplied -eq $true) -and (Near $rc1.Data.calculationArea 9.8842) -and ($rc1.Data.calculationAreaUnit -eq 'Acres')) "area=$($rc1.Data.calculationArea)"
Check '3.3 converted quantity 19.77 Kg (final rounding only)' (Near $rc1.Data.requiredQuantity 19.77) "got $($rc1.Data.requiredQuantity)"

$rc2 = Calc $hdrA $farmAcres @{ DosageRate = 4; DosageUnit = 'Liters'; DosageBasis = 'PerHectare' }
# 5 ac x 0.404685642 = 2.02342821 ha -> x 4 L = 8.09371284 -> 8.09 L
Check '3.4 acres farm, per-hectare basis -> 200' ($rc2.Status -eq 200)
Check '3.5 conversion applied with documented constant 0.404685642' (($rc2.Data.conversionApplied -eq $true) -and (Near $rc2.Data.calculationArea 2.02342821) -and ($rc2.Data.calculationAreaUnit -eq 'Hectares')) "area=$($rc2.Data.calculationArea)"
Check '3.6 converted quantity 8.09 Liters (final rounding only)' (Near $rc2.Data.requiredQuantity 8.09) "got $($rc2.Data.requiredQuantity)"

# --- 4. Canonical normalization (3) --------------------------------------------
Write-Host ''
Write-Host '--- 4. Case-insensitive controlled values ---'
$rn = Calc $hdrA $farmAcres @{ Category = 'fertilizer'; DosageUnit = 'kg'; DosageBasis = 'peracre' }
Check '4.1 lowercase input accepted' ($rn.Status -eq 200) "status=$($rn.Status) raw=$($rn.Raw)"
Check '4.2 category normalized to canonical casing' ($rn.Data.category -eq 'Fertilizer')
Check '4.3 unit + basis normalized to canonical casing' (($rn.Data.dosageUnit -eq 'Kg') -and ($rn.Data.dosageBasis -eq 'PerAcre'))

# --- 5. Validation (10) ---------------------------------------------------------
Write-Host ''
Write-Host '--- 5. Validation ---'
Check '5.1 zero dosage rate -> 400'      ((Calc $hdrA $farmAcres @{ DosageRate = 0 }).Status -eq 400)
Check '5.2 negative dosage rate -> 400'  ((Calc $hdrA $farmAcres @{ DosageRate = -5 }).Status -eq 400)
Check '5.3 excessive dosage rate -> 400' ((Calc $hdrA $farmAcres @{ DosageRate = 200000 }).Status -eq 400)
Check '5.4 unsupported category -> 400'  ((Calc $hdrA $farmAcres @{ Category = 'Gadget' }).Status -eq 400)
Check '5.5 unsupported dosage unit -> 400' ((Calc $hdrA $farmAcres @{ DosageUnit = 'Bags' }).Status -eq 400)
Check '5.6 unsupported dosage basis -> 400' ((Calc $hdrA $farmAcres @{ DosageBasis = 'PerCanal' }).Status -eq 400)
Check '5.7 missing input name -> 400'    ((Calc $hdrA $farmAcres @{} @('InputName')).Status -eq 400)
Check '5.8 whitespace input name -> 400' ((Calc $hdrA $farmAcres @{ InputName = '    ' }).Status -eq 400)
Check '5.9 oversized input name -> 400'  ((Calc $hdrA $farmAcres @{ InputName = ('U' * 151) }).Status -eq 400)

# Zero recorded area: FarmSize is server-side authoritative and the public
# farm endpoints refuse non-positive sizes, so the fixture is flipped via SQL
# and always restored.
SqlQuery "UPDATE Farms SET FarmSize = 0 WHERE Id = '$farmAcres'" | Out-Null
$zero = Calc $hdrA $farmAcres
SqlQuery "UPDATE Farms SET FarmSize = 5 WHERE Id = '$farmAcres'" | Out-Null
$restored = Calc $hdrA $farmAcres
Check '5.10 zero recorded farm area -> 400, restored farm -> 200' (($zero.Status -eq 400) -and ($restored.Status -eq 200)) "zero=$($zero.Status) restored=$($restored.Status)"

# --- 6. Ownership (4) -----------------------------------------------------------
Write-Host ''
Write-Host '--- 6. Farm ownership ---'
$unknownFarm = [Guid]::NewGuid()
Check '6.1 owner calculates on own farm -> 200' ((Calc $hdrA $farmAcres).Status -eq 200)
Check '6.2 user B on user A farm -> 403'        ((Calc $hdrB $farmAcres).Status -eq 403)
Check '6.3 user C on user A farm -> 403'        ((Calc $hdrC $farmAcres).Status -eq 403)
Check '6.4 unknown farm -> 404'                 ((Calc $hdrA $unknownFarm).Status -eq 404)

# --- 7. Crop security (3) --------------------------------------------------------
Write-Host ''
Write-Host '--- 7. Optional crop reference ---'
$ownCrop = Calc $hdrA $farmAcres @{ CropId = $cropA }
Check '7.1 own-farm crop accepted -> 200, echoed' (($ownCrop.Status -eq 200) -and ($ownCrop.Data.cropId -eq $cropA)) "status=$($ownCrop.Status)"
Check '7.2 foreign-farm crop rejected -> 400'     ((Calc $hdrA $farmAcres @{ CropId = $cropB }).Status -eq 400)
Check '7.3 unknown crop -> 404'                   ((Calc $hdrA $farmAcres @{ CropId = [Guid]::NewGuid() }).Status -eq 404)

# --- 8. Read-only guarantee (15) --------------------------------------------------
Write-Host ''
Write-Host '--- 8. Read-only guarantee (calculation never writes) ---'
$tables = @('Farms','Crops','FinancialTransactions','CropMonitoringChecks','Notifications','MarketplaceListings','MarketplaceConversations','MarketplaceMessages','CommunityPosts','CommunityComments')
$before = @{}
foreach ($t in $tables) { $before[$t] = (SqlQuery "SELECT COUNT(*) FROM $t") -join '' }
$tableCountBefore = (SqlQuery 'SELECT COUNT(*) FROM sys.tables') -join ''
$migrationsBefore = (SqlQuery 'SELECT COUNT(*) FROM [__EFMigrationsHistory]') -join ''

# Exercise the calculator a few extra times across users.
Calc $hdrA $farmAcres | Out-Null
Calc $hdrA $farmHectares @{ DosageBasis = 'PerHectare'; DosageUnit = 'Liters'; DosageRate = 1.5 } | Out-Null
Calc $hdrB $farmB @{ InputName = ($prefix + 'DAP'); DosageRate = 1.25 } | Out-Null

$after = @{}
foreach ($t in $tables) { $after[$t] = (SqlQuery "SELECT COUNT(*) FROM $t") -join '' }
$tableCountAfter = (SqlQuery 'SELECT COUNT(*) FROM sys.tables') -join ''
$migrationsAfter = (SqlQuery 'SELECT COUNT(*) FROM [__EFMigrationsHistory]') -join ''

$rowCountsStable = $true
for ($i = 0; $i -lt $tables.Count; $i++) {
    $t = $tables[$i]
    Check "8.$($i + 1) $t row count unchanged ($($before[$t]))" ($before[$t] -eq $after[$t]) "before=$($before[$t]) after=$($after[$t])"
    if ($before[$t] -ne $after[$t]) { $rowCountsStable = $false }
}
$calcTables = (SqlQuery "SELECT COUNT(*) FROM sys.tables WHERE name LIKE '%Calculat%' OR name LIKE '%Dosage%' OR name LIKE '%InputApplication%'") -join ''
Check '8.11 no calculator/dosage table exists' ($calcTables -eq '0') "found=$calcTables"
Check '8.12 table count unchanged (21)' (($tableCountBefore -eq $tableCountAfter) -and ($tableCountAfter -eq '21')) "before=$tableCountBefore after=$tableCountAfter"
Check '8.13 migration count unchanged (11)' (($migrationsBefore -eq $migrationsAfter) -and ($migrationsAfter -eq '11')) "before=$migrationsBefore after=$migrationsAfter"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + FarmSizeUnit FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check '8.14 seed Ahmed farm untouched' ($ahmedBefore -eq $ahmedAfter) "before=$ahmedBefore after=$ahmedAfter"
$fixtureArea = (SqlQuery "SELECT CAST(FarmSize AS decimal(18,2)) FROM Farms WHERE Id = '$farmAcres'") -join ''
Check '8.15 fixture farm area restored (5)' ($fixtureArea -eq '5.00') "got=$fixtureArea"

# --- 9. Response security (3) ------------------------------------------------------
Write-Host ''
Write-Host '--- 9. Response payload never leaks identity/secret material ---'
$raw = $r.Raw
$forbidden = @('userId','ownerId','email','phone','password','token','apiKey','secret','creditCard')
$leaked = @($forbidden | Where-Object { $raw -like ('*' + $_ + '*') })
Check '9.1 no identity/secret keys in response' ($leaked.Count -eq 0) ("leaked: " + ($leaked -join ','))
Check '9.2 caller user id never present' ((-not $userIdA) -or ($raw.IndexOf($userIdA, [System.StringComparison]::OrdinalIgnoreCase) -lt 0)) "userIdA=$userIdA"
Check '9.3 foreign farm id never present' ($raw.IndexOf([string]$farmB, [System.StringComparison]::OrdinalIgnoreCase) -lt 0)

# --- Teardown -----------------------------------------------------------------------
Write-Host ''
Write-Host '--- Teardown ---'
foreach ($pair in @(@($hdrA, $farmAcres), @($hdrA, $farmHectares), @($hdrB, $farmB))) {
    $h = $pair[0]; $fid = $pair[1]
    if (-not $fid) { continue }
    try {
        $crops = AsArray (GetJson "$base/api/farms/$fid/crops" $h)
        foreach ($c in $crops) { ApiCall 'DELETE' "$base/api/crops/$($c.id)" $h | Out-Null }
    } catch { }
    ApiCall 'DELETE' "$base/api/farms/$fid" $h | Out-Null
}
$leftoverA = @(AsArray (GetJson "$base/api/farms" $hdrA) | Where-Object { $_.farmName -like "$prefix*" }).Count
$leftoverB = @(AsArray (GetJson "$base/api/farms" $hdrB) | Where-Object { $_.farmName -like "$prefix*" }).Count
Check 'T.1 all IC farms removed' (($leftoverA -eq 0) -and ($leftoverB -eq 0)) "leftoverA=$leftoverA leftoverB=$leftoverB"
Check 'T.2 Ahmed farm still intact after teardown' ($ahmedBefore -eq ((SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + FarmSizeUnit FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''))

Write-Host ''
Write-Host "RESULT: $pass passed, $fail failed." -ForegroundColor ($(if ($fail -eq 0) { 'Green' } else { 'Red' }))
if ($fail -gt 0) { exit 1 }
exit 0
