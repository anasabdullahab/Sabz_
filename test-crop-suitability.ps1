# Crop Suitability & Recommendation Foundation - Test Suite (PROMPT 4)
# Idempotent: reuses existing fixtures, never touches Ahmed Farm.
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$base = 'http://localhost:5073'
$pass = 0; $fail = 0

function Check($name, $condition, $detail) {
    if ($condition) { $script:pass++; Write-Host "[PASS] $name $detail" -ForegroundColor Green }
    else { $script:fail++; Write-Host "[FAIL] $name $detail" -ForegroundColor Red }
}

function SqlQuery($sql) {
    return (sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q $sql -W -s"|" -h -1 | Where-Object { $_ -notmatch '^\s*$' -and $_ -notmatch 'rows affected' }) -join "`n"
}

Write-Host '=== GUARD: Ahmed Farm integrity snapshot (before) ==='
$ahmedBefore = SqlQuery "SELECT FarmName, SoilType, IrrigationType, Latitude, Longitude, ProvinceId, DistrictId, TehsilId, FarmSize FROM Farms WHERE Id = 'D5FBCA89-5C3C-401E-BA23-FDFF84054300'"
Write-Host "  Ahmed Farm (before): $ahmedBefore"

Write-Host ''
Write-Host '=== SETUP: Login User A ==='
$loginBody = @{ Identifier = 'test21@example.com'; Password = 'Test1234!' } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
$tokenA = $login.token
$headersA = @{ Authorization = "Bearer $tokenA" }
Write-Host "User A logged in."

Write-Host ''
Write-Host '=== SETUP: Farm WITH soil+irrigation+coords (Rawalpindi) - reused if present ==='
$farms = Invoke-RestMethod -Uri "$base/api/farms" -Method Get -Headers $headersA
$existing = $farms | Where-Object { $_.farmName -eq 'Crop Suitability Test Farm' } | Select-Object -First 1
if ($existing) {
    $farmId = $existing.id
    Write-Host "Reusing existing farm: $farmId"
} else {
    $farmBody = @{
        FarmName = 'Crop Suitability Test Farm'; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        Latitude = 33.6844; Longitude = 73.0479; FarmSize = 10.0; FarmSizeUnit = 'Acres'
    } | ConvertTo-Json
    $farm = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -ContentType 'application/json' -Body $farmBody -Headers $headersA
    $farmId = $farm.id
    Write-Host "Farm created: $farmId"
}
# Ensure deterministic soil/irrigation/coords regardless of prior state
sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET SoilType = 'Loamy', IrrigationType = 'Canal', Latitude = 33.6844, Longitude = 73.0479 WHERE Id = '$farmId'" | Out-Null

Write-Host ''
Write-Host '=== SETUP: Farm WITHOUT coordinates - reused if present ==='
$existing2 = $farms | Where-Object { $_.farmName -eq 'No GPS Farm' } | Select-Object -First 1
if ($existing2) {
    $farmIdNoCoords = $existing2.id
    Write-Host "Reusing existing farm: $farmIdNoCoords"
} else {
    $farmBody2 = @{
        FarmName = 'No GPS Farm'; ProvinceId = 1; DistrictId = 101; TehsilId = 1001
        FarmSize = 5.0; FarmSizeUnit = 'Acres'
    } | ConvertTo-Json
    $farm2 = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -ContentType 'application/json' -Body $farmBody2 -Headers $headersA
    $farmIdNoCoords = $farm2.id
}

Write-Host ''
Write-Host '=== TEST CASE 1: Rabi evaluation (full farm data) ==='
try {
    $r = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crop-suitability?season=Rabi" -Method Get -Headers $headersA
    Check 'TC1 returns 200 with crops' ($r.crops.Count -ge 3) "crops=$($r.crops.Count) season=$($r.evaluationSeason)"
    Check 'TC1 season echo + source' ($r.evaluationSeason -eq 'Rabi' -and $r.seasonSource -eq 'ClientProvided') "season=$($r.evaluationSeason) source=$($r.seasonSource)"
    Check 'TC1 location echo' ($r.location.province -eq 'Punjab' -and $r.location.district -eq 'Rawalpindi') "loc=$($r.location.province)/$($r.location.district)/$($r.location.tehsil)"
    Check 'TC1 weather used' ($r.weatherDataAvailable -eq $true) ''
    $names = $r.crops | ForEach-Object { $_.cropName }
    Check 'TC1 Rabi crop set' (($names -contains 'Wheat') -and ($names -contains 'Gram (Chickpea)') -and ($names -contains 'Lentil') -and ($names -notcontains 'Rice')) "crops=$($names -join ', ')"
    $wheat = $r.crops | Where-Object { $_.cropName -eq 'Wheat' }
    $sum = $wheat.factorScores.location + $wheat.factorScores.climate + $wheat.factorScores.soil + $wheat.factorScores.water + $wheat.factorScores.season
    Check 'TC1 score = factor sum' ($wheat.suitabilityScore -eq $sum) "wheat=$($wheat.suitabilityScore) (loc=$($wheat.factorScores.location) cli=$($wheat.factorScores.climate) soil=$($wheat.factorScores.soil) water=$($wheat.factorScores.water) season=$($wheat.factorScores.season))"
    Check 'TC1 soil factor evaluated' ($wheat.factorScores.soil -eq 20) "soil=$($wheat.factorScores.soil)"
    Check 'TC1 explanations present' ($wheat.positiveFactors.Count -ge 1) "positives=$($wheat.positiveFactors.Count) limitations=$($wheat.limitations.Count) missing=$($wheat.missingData.Count)"
} catch {
    Check 'TC1 Rabi evaluation' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 2: Kharif evaluation differs from Rabi ==='
try {
    $k = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crop-suitability?season=Kharif" -Method Get -Headers $headersA
    $namesK = $k.crops | ForEach-Object { $_.cropName }
    Check 'TC2 Kharif crop set' (($namesK -contains 'Rice') -and ($namesK -contains 'Cotton') -and ($namesK -contains 'Maize') -and ($namesK -contains 'Mung bean') -and ($namesK -contains 'Mash bean') -and ($namesK -notcontains 'Wheat')) "crops=$($namesK -join ', ')"
    Check 'TC2 season echo' ($k.evaluationSeason -eq 'Kharif') ''
} catch {
    Check 'TC2 Kharif evaluation' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 3: Season auto-detection (no season param) ==='
try {
    $a = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crop-suitability" -Method Get -Headers $headersA
    Check 'TC3 auto-detected season' ($a.seasonSource -eq 'AutoDetected' -and ($a.evaluationSeason -eq 'Rabi' -or $a.evaluationSeason -eq 'Kharif')) "season=$($a.evaluationSeason) source=$($a.seasonSource)"
} catch {
    Check 'TC3 auto-detect' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 4: Farm WITHOUT GPS - partial evaluation, no crash ==='
try {
    $n = Invoke-RestMethod -Uri "$base/api/farms/$farmIdNoCoords/crop-suitability?season=Rabi" -Method Get -Headers $headersA
    Check 'TC4 returns 200 partial' ($n.crops.Count -ge 3) "crops=$($n.crops.Count)"
    Check 'TC4 weather marked unavailable' ($n.weatherDataAvailable -eq $false) ''
    $first = $n.crops[0]
    Check 'TC4 climate unevaluated + reported' ($first.factorScores.climate -eq 0 -and ($first.missingData | Where-Object { $_ -match 'Weather|climate' }).Count -ge 1) "climate=$($first.factorScores.climate) missing=$($first.missingData -join ' | ')"
} catch {
    Check 'TC4 no-GPS evaluation' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 5: Ownership - User B cannot evaluate Farm A ==='
$regB = @{ FullName = 'User B'; Email = 'userb3@example.com'; Password = 'Test1234!'; ConfirmPassword = 'Test1234!' } | ConvertTo-Json
try { Invoke-RestMethod -Uri "$base/api/auth/register" -Method Post -ContentType 'application/json' -Body $regB | Out-Null } catch { }
$loginBBody = @{ Identifier = 'userb3@example.com'; Password = 'Test1234!' } | ConvertTo-Json
$loginB = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBBody
$headersB = @{ Authorization = "Bearer $($loginB.token)" }
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/crop-suitability" -Method Get -Headers $headersB -ErrorAction Stop | Out-Null
    Check 'TC5 ownership denied' $false 'User B could evaluate Farm A!'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'TC5 ownership denied' ($code -eq 403) "HTTP $code (expected 403)"
}

Write-Host ''
Write-Host '=== TEST CASE 6: Unknown farm -> 404 ==='
try {
    Invoke-WebRequest -Uri "$base/api/farms/$([Guid]::NewGuid())/crop-suitability" -Method Get -Headers $headersA -ErrorAction Stop | Out-Null
    Check 'TC6 unknown farm' $false 'expected 404'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'TC6 unknown farm' ($code -eq 404) "HTTP $code"
}

Write-Host ''
Write-Host '=== TEST CASE 7: Ranking sorted desc + levels consistent with thresholds ==='
try {
    $s = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crop-suitability?season=Rabi" -Method Get -Headers $headersA
    $scores = $s.crops | ForEach-Object { $_.suitabilityScore }
    $sorted = @($scores | Sort-Object -Descending)
    Check 'TC7 scores sorted desc' (-not (Compare-Object $scores $sorted)) "scores=$($scores -join ', ')"
    $levelsOk = $true
    foreach ($c in $s.crops) {
        $expected = if ($c.suitabilityScore -ge 80) { 'Highly Suitable' } elseif ($c.suitabilityScore -ge 60) { 'Suitable' } elseif ($c.suitabilityScore -ge 40) { 'Moderately Suitable' } else { 'Low Suitability' }
        if ($c.suitabilityLevel -ne $expected) { $levelsOk = $false; Write-Host "  level mismatch: $($c.cropName) score=$($c.suitabilityScore) level=$($c.suitabilityLevel)" }
    }
    Check 'TC7 levels match thresholds' $levelsOk ''
} catch {
    Check 'TC7 ranking' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 8: Invalid season -> 400 ==='
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/crop-suitability?season=Summer" -Method Get -Headers $headersA -ErrorAction Stop | Out-Null
    Check 'TC8 invalid season' $false 'expected 400'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'TC8 invalid season' ($code -eq 400) "HTTP $code"
}

Write-Host ''
Write-Host '=== TEST CASE 9: Unauthenticated -> 401 ==='
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/crop-suitability" -Method Get -ErrorAction Stop | Out-Null
    Check 'TC9 unauthenticated' $false 'expected 401'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'TC9 unauthenticated' ($code -eq 401) "HTTP $code"
}

Write-Host ''
Write-Host '=== REGRESSION: Existing APIs still work ==='
$provinces = Invoke-RestMethod -Uri "$base/api/locations/provinces" -Method Get
Check 'REG provinces' ($provinces.Count -eq 7) "count=$($provinces.Count)"
$farms2 = Invoke-RestMethod -Uri "$base/api/farms" -Method Get -Headers $headersA
Check 'REG farms' ($farms2.Count -ge 2) "count=$($farms2.Count)"
$w = Invoke-RestMethod -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA
Check 'REG weather current' ($w.current -ne $null -and $w.source -eq 'Open-Meteo') "temp=$($w.current.temperature)C"
$cropsCatalog = $null
try { $cropsCatalog = Invoke-RestMethod -Uri "$base/api/crops/catalog" -Method Get -Headers $headersA } catch { }
if ($cropsCatalog) { Check 'REG crop catalog (Mung/Mash added)' ($cropsCatalog.Count -eq 22) "count=$($cropsCatalog.Count)" }
else {
    # No public catalog endpoint - verify directly in DB (Mung/Mash seeded as 21/22)
    $catalogCount = SqlQuery 'SELECT COUNT(*) FROM CropCatalog'
    Check 'REG crop catalog (Mung/Mash added)' ($catalogCount -eq '22') "count=$catalogCount (DB)"
}

Write-Host ''
Write-Host '=== GUARD: Ahmed Farm integrity (after) ==='
$ahmedAfter = SqlQuery "SELECT FarmName, SoilType, IrrigationType, Latitude, Longitude, ProvinceId, DistrictId, TehsilId, FarmSize FROM Farms WHERE Id = 'D5FBCA89-5C3C-401E-BA23-FDFF84054300'"
Write-Host "  Ahmed Farm (after): $ahmedAfter"
Check 'Ahmed Farm untouched' ($ahmedBefore -eq $ahmedAfter) ''

Write-Host ''
Write-Host "=============================="
Write-Host "RESULTS: $pass passed, $fail failed" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
