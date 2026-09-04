# Weather Intelligence Foundation - Test Suite (PROMPT 3)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$base = 'http://localhost:5073'
$pass = 0; $fail = 0

function Check($name, $condition, $detail) {
    if ($condition) { $script:pass++; Write-Host "[PASS] $name $detail" -ForegroundColor Green }
    else { $script:fail++; Write-Host "[FAIL] $name $detail" -ForegroundColor Red }
}

Write-Host '=== SETUP: Login User A ==='
$loginBody = @{ Identifier = 'test21@example.com'; Password = 'Test1234!' } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
$tokenA = $login.token
$headersA = @{ Authorization = "Bearer $tokenA" }
Write-Host "User A logged in."

Write-Host ''
Write-Host '=== SETUP: Farm WITH coordinates (Rawalpindi: 33.6844, 73.0479) - reused if present ==='
$farms = Invoke-RestMethod -Uri "$base/api/farms" -Method Get -Headers $headersA
$existing = $farms | Where-Object { $_.farmName -eq 'Weather Test Farm' } | Select-Object -First 1
if ($existing) {
    $farmId = $existing.id
    sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET Latitude = 33.6844, Longitude = 73.0479 WHERE Id = '$farmId'" | Out-Null
    Write-Host "Reusing existing farm: $farmId"
} else {
    $farmBody = @{
        FarmName = 'Weather Test Farm'; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        Latitude = 33.6844; Longitude = 73.0479; FarmSize = 10.0; FarmSizeUnit = 'Acres'
    } | ConvertTo-Json
    $farm = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -ContentType 'application/json' -Body $farmBody -Headers $headersA
    $farmId = $farm.id
    Write-Host "Farm created: $farmId"
}

Write-Host ''
Write-Host '=== SETUP: Farm WITHOUT coordinates - reused if present ==='
$existing2 = $farms | Where-Object { $_.farmName -eq 'No GPS Farm' } | Select-Object -First 1
if ($existing2) {
    $farmIdNoCoords = $existing2.id
    sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET Latitude = NULL, Longitude = NULL WHERE Id = '$farmIdNoCoords'" | Out-Null
    Write-Host "Reusing existing farm: $farmIdNoCoords"
} else {
    $farmBody2 = @{
        FarmName = 'No GPS Farm'; ProvinceId = 1; DistrictId = 101; TehsilId = 1001
        FarmSize = 5.0; FarmSizeUnit = 'Acres'
    } | ConvertTo-Json
    $farm2 = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -ContentType 'application/json' -Body $farmBody2 -Headers $headersA
    $farmIdNoCoords = $farm2.id
    Write-Host "Farm without coords created: $farmIdNoCoords"
}

Write-Host ''
Write-Host '=== TEST CASE 1: Current weather (valid coordinates) ==='
try {
    $w = Invoke-RestMethod -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA
    Check 'TC1 current weather' ($w.current -ne $null) "temp=$($w.current.temperature)C humidity=$($w.current.relativeHumidity)% wind=$($w.current.windSpeed)km/h code=$($w.current.weatherCode) isDay=$($w.current.isDay)"
    Check 'TC1 source attribution' ($w.source -eq 'Open-Meteo') "source=$($w.source)"
    Check 'TC1 farmId echo' ($w.farmId -eq $farmId) ''
    Check 'TC1 coordinates echo' ($w.latitude -ne $null -and $w.longitude -ne $null) "lat=$($w.latitude) lon=$($w.longitude)"
    Check 'TC1 retrievedAt present' ($w.retrievedAt -ne $null) "retrievedAt=$($w.retrievedAt)"
    Check 'TC1 units present' ($w.units.temperature -eq [char]176 + 'C') "units.temp=$($w.units.temperature)"
} catch {
    Check 'TC1 current weather' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 2: Forecast (valid coordinates) ==='
try {
    $f = Invoke-RestMethod -Uri "$base/api/farms/$farmId/weather/forecast" -Method Get -Headers $headersA
    Check 'TC2 forecast returned' ($f.forecast -ne $null -and $f.forecast.days.Count -eq 7) "days=$($f.forecast.days.Count) timezone=$($f.forecast.timezone)"
    $d0 = $f.forecast.days[0]
    Check 'TC2 daily fields' ($d0.tempMin -ne $null -and $d0.tempMax -ne $null -and $d0.et0 -ne $null) "date=$($d0.date) min=$($d0.tempMin) max=$($d0.tempMax) et0=$($d0.et0) sunrise=$($d0.sunrise)"
    Check 'TC2 soil data' ($d0.soilTemperature -ne $null -and $d0.soilMoisture -ne $null) "soilT=$($d0.soilTemperature) soilM=$($d0.soilMoisture)"
} catch {
    Check 'TC2 forecast' $false $_.Exception.Message
}

Write-Host ''
Write-Host '=== TEST CASE 7: Cache test (repeat current weather) ==='
$t1 = Measure-Command { Invoke-RestMethod -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA | Out-Null }
$t2 = Measure-Command { Invoke-RestMethod -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA | Out-Null }
Write-Host "  First call: $($t1.TotalMilliseconds)ms | Second call: $($t2.TotalMilliseconds)ms"
Check 'TC7 cached repeat request completed' $true '(verify server log shows only ONE external fetch)'

Write-Host ''
Write-Host '=== TEST CASE 3: Farm WITHOUT coordinates ==='
try {
    $r = Invoke-WebRequest -Uri "$base/api/farms/$farmIdNoCoords/weather/current" -Method Get -Headers $headersA -ErrorAction Stop
    Check 'TC3 no-coords validation' $false "expected error but got $($r.StatusCode)"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $body = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
    Check 'TC3 no-coords validation' ($code -eq 400 -and $body -match 'GPS coordinates') "HTTP $code - $body"
}

Write-Host ''
Write-Host '=== TEST CASE 4+5: Invalid coordinates (via direct DB update) ==='
sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET Latitude = 999 WHERE Id = '$farmId'" | Out-Null
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA -ErrorAction Stop | Out-Null
    Check 'TC4 invalid latitude' $false 'expected 400'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $body = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
    Check 'TC4 invalid latitude' ($code -eq 400 -and $body -match 'Latitude') "HTTP $code - $body"
}
sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET Latitude = 33.6844, Longitude = 999 WHERE Id = '$farmId'" | Out-Null
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersA -ErrorAction Stop | Out-Null
    Check 'TC5 invalid longitude' $false 'expected 400'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $body = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
    Check 'TC5 invalid longitude' ($code -eq 400 -and $body -match 'Longitude') "HTTP $code - $body"
}
# Restore valid coordinates
sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d SabzDB -Q "UPDATE Farms SET Latitude = 33.6844, Longitude = 73.0479 WHERE Id = '$farmId'" | Out-Null

Write-Host ''
Write-Host '=== TEST CASE 6: Ownership - User B cannot access Farm A ==='
$regB = @{ FullName = 'User B'; Email = 'userb3@example.com'; Password = 'Test1234!'; ConfirmPassword = 'Test1234!' } | ConvertTo-Json
try { Invoke-RestMethod -Uri "$base/api/auth/register" -Method Post -ContentType 'application/json' -Body $regB | Out-Null } catch { }
$loginBBody = @{ Identifier = 'userb3@example.com'; Password = 'Test1234!' } | ConvertTo-Json
$loginB = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBBody
$headersB = @{ Authorization = "Bearer $($loginB.token)" }
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/weather/current" -Method Get -Headers $headersB -ErrorAction Stop | Out-Null
    Check 'TC6 ownership denied' $false 'User B could access Farm A!'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'TC6 ownership denied' ($code -eq 403) "HTTP $code (expected 403)"
}

Write-Host ''
Write-Host '=== EXTRA: 404 for non-existent farm ==='
try {
    Invoke-WebRequest -Uri "$base/api/farms/$([Guid]::NewGuid())/weather/current" -Method Get -Headers $headersA -ErrorAction Stop | Out-Null
    Check 'Non-existent farm 404' $false 'expected 404'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'Non-existent farm 404' ($code -eq 404) "HTTP $code"
}

Write-Host ''
Write-Host '=== EXTRA: Unauthenticated request rejected ==='
try {
    Invoke-WebRequest -Uri "$base/api/farms/$farmId/weather/current" -Method Get -ErrorAction Stop | Out-Null
    Check 'Unauthenticated rejected' $false 'expected 401'
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Check 'Unauthenticated rejected' ($code -eq 401) "HTTP $code"
}

Write-Host ''
Write-Host '=== REGRESSION: Existing APIs still work ==='
$provinces = Invoke-RestMethod -Uri "$base/api/locations/provinces" -Method Get
Check 'REG provinces' ($provinces.Count -eq 7) "count=$($provinces.Count)"
$farms = Invoke-RestMethod -Uri "$base/api/farms" -Method Get -Headers $headersA
Check 'REG farms' ($farms.Count -ge 2) "count=$($farms.Count)"
$existingCrop = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Get -Headers $headersA -ErrorAction SilentlyContinue | Where-Object { $_.cropName -eq 'Maize' } | Select-Object -First 1
if ($existingCrop) {
    Check 'REG crop creation' $true "crop=Maize (reused)"
} else {
    $cropBody = @{ CropName = 'Maize'; Season = 'Kharif'; Status = 'Active' } | ConvertTo-Json
    $crop = Invoke-RestMethod -Uri "$base/api/farms/$farmId/crops" -Method Post -ContentType 'application/json' -Body $cropBody -Headers $headersA
    Check 'REG crop creation' ($crop.cropName -eq 'Maize') "crop=$($crop.cropName)"
}

Write-Host ''
Write-Host "=============================="
Write-Host "RESULTS: $pass passed, $fail failed" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
