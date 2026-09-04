# =============================================================================
# SABZ Prompt 5 - Dynamic Next Crop Recommendation & Crop History Foundation
# Idempotent integration test suite (PowerShell, REST + sqlcmd checks).
# Style follows test-crop-suitability.ps1. Safe to re-run any number of times.
#
# NOTE: uses fixed local test accounts only. This script is intentionally NOT
# committed to the repository (contains test credentials).
# =============================================================================

$ErrorActionPreference = "Stop"
$base = "http://localhost:5073"

$script:Pass = 0
$script:Fail = 0

function Check([string]$name, [bool]$condition, [string]$detail = "") {
    if ($condition) {
        $script:Pass++
        Write-Host "PASS  $name" -ForegroundColor Green
    } else {
        $script:Fail++
        Write-Host "FAIL  $name  $detail" -ForegroundColor Red
    }
}

function SqlQuery([string]$sql) {
    $out = sqlcmd -I -S "(localdb)\mssqllocaldb" -d SabzDB -W -s"|" -h -1 -Q $sql
    return ($out | Where-Object { $_ -and $_ -notmatch '^\(\d+ rows? affected\)' })
}

function Login([string]$email, [string]$password) {
    try {
        $body = @{ identifier = $email; password = $password } | ConvertTo-Json
        $r = Invoke-RestMethod -Method POST -Uri "$base/api/auth/login" -Body $body -ContentType "application/json"
        return $r.token
    } catch {
        return $null
    }
}

function ApiCall([string]$method, [string]$uri, $token, $bodyObj = $null) {
    # Uses Invoke-WebRequest: Invoke-RestMethod (PS 5.1) throws on ANY non-2xx,
    # including 204, which breaks status-code assertions.
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $bodyJson = $null
    if ($null -ne $bodyObj) { $bodyJson = $bodyObj | ConvertTo-Json -Depth 5 }
    try {
        $resp = Invoke-WebRequest -Method $method -Uri $uri -Headers $headers -Body $bodyJson -ContentType "application/json" -UseBasicParsing
        $data = $null
        if ($resp.Content) { $data = $resp.Content | ConvertFrom-Json }
        return @{ Status = [int]$resp.StatusCode; Data = $data }
    } catch {
        $code = 0
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        return @{ Status = $code; Data = $null }
    }
}

Write-Host "=== SABZ Prompt 5 Recommendation Tests ===" -ForegroundColor Cyan

# --- Ahmed Farm guard (must never be modified) ---
$ahmedId = "D5FBCA89-5C3C-401E-BA23-FDFF84054300"
$ahmedBefore = SqlQuery "SELECT FarmName, ProvinceId, DistrictId, TehsilId, Latitude, Longitude, FarmSize, FarmSizeUnit, SoilType, IrrigationType FROM Farms WHERE Id='$ahmedId'"

# --- Authenticate ---
$tokenA = Login "test21@example.com" "Test1234!"
$tokenB = Login "userb3@example.com" "Test1234!"
Check "Setup: both test users authenticate" ($tokenA -and $tokenB)

# --- Setup: recommendation test farm (idempotent) ---
$farmName = "Crop Recommendation Test Farm"
$myFarms = ApiCall "GET" "$base/api/farms" $tokenA
$farm = $myFarms.Data | Where-Object { $_.farmName -eq $farmName } | Select-Object -First 1
if (-not $farm) {
    $createBody = @{
        farmName = $farmName; provinceId = 1; districtId = 103; tehsilId = 1007
        latitude = 33.6844; longitude = 73.0479; farmSize = 8; farmSizeUnit = "Acres"
        soilType = "Loamy"; irrigationType = "Canal"
    }
    $created = ApiCall "POST" "$base/api/farms" $tokenA $createBody
    $farm = $created.Data
}
# Force known deterministic state
SqlQuery "UPDATE Farms SET SoilType='Loamy', IrrigationType='Canal', Latitude=33.6844, Longitude=73.0479 WHERE Id='$($farm.id)'" | Out-Null
$farmId = $farm.id
Check "Setup: recommendation test farm ready" ($farmId -ne $null)

# --- Setup: deterministic crop history (idempotent: delete named records first) ---
SqlQuery "DELETE FROM Crops WHERE FarmId='$farmId' AND CropName LIKE 'HistTC%'" | Out-Null
$histBody = @(
    @{ cropName="HistTC Gram Old"; cropCatalogId=12; season="Rabi"; plantingDate="2023-11-01T00:00:00Z"; harvestDate="2024-03-01T00:00:00Z"; status="Harvested" },
    @{ cropName="HistTC Rice Kharif"; cropCatalogId=2; season="Kharif"; plantingDate="2024-06-01T00:00:00Z"; harvestDate="2024-10-15T00:00:00Z"; status="Harvested" },
    @{ cropName="HistTC Wheat Recent"; cropCatalogId=1; season="Rabi"; plantingDate="2024-11-15T00:00:00Z"; harvestDate="2025-05-01T00:00:00Z"; status="Harvested" }
)
foreach ($h in $histBody) {
    $r = ApiCall "POST" "$base/api/farms/$farmId/crops" $tokenA $h
    if ($r.Status -ne 200) { Write-Host "WARN history seed failed: $($r.Status)" }
}
$cropRowCount = SqlQuery "SELECT COUNT(*) FROM Crops WHERE FarmId='$farmId' AND CropName LIKE 'HistTC%'"
Check "Setup: 3 history records seeded" ($cropRowCount -eq "3") "rows=$cropRowCount"

# --- TEST 1: valid farm with history ---
$t1 = ApiCall "GET" "$base/api/farms/$farmId/crop-recommendations?season=Rabi" $tokenA
Check "T1.1 recommendation succeeds (200)" ($t1.Status -eq 200) "status=$($t1.Status)"
Check "T1.2 multiple candidates returned" ($t1.Data.recommendations.Count -ge 2) "count=$($t1.Data.recommendations.Count)"
Check "T1.3 farm suitability category present" ($t1.Data.recommendations[0].farmSuitability -in @("Highly Suitable","Suitable","Moderately Suitable","Low Suitability"))
Check "T1.4 recommendation category present" ($t1.Data.recommendations[0].recommendation -in @("Highly Recommended","Recommended","Consider","Not Recommended"))
Check "T1.5 explanation present" (-not [string]::IsNullOrWhiteSpace($t1.Data.recommendations[0].explanation))
Check "T1.6 history available" ($t1.Data.cropHistory.available -eq $true)

# --- TEST 3: multiple history -> documented rule (most recent completed cycle) ---
Check "T3.1 previous crop is most recent completed record (Wheat)" ($t1.Data.cropHistory.previousCropName -eq "Wheat") "got=$($t1.Data.cropHistory.previousCropName)"
Check "T3.2 previous crop category resolved (Cereal)" ($t1.Data.cropHistory.previousCropCategory -eq "Cereal")
Check "T3.3 usable record count = 3" ($t1.Data.cropHistory.usableRecordCount -eq 3)
$wheatItem = $t1.Data.recommendations | Where-Object { $_.cropName -eq "Wheat" } | Select-Object -First 1
$gramItem  = $t1.Data.recommendations | Where-Object { $_.cropName -eq "Gram (Chickpea)" } | Select-Object -First 1
Check "T3.4 cereal-after-cereal rule applied to Wheat (Caution)" ($wheatItem -and $wheatItem.historyConsideration -eq "Caution") "got=$($wheatItem.historyConsideration)"
Check "T3.5 pulse-after-cereal rule applied to Gram (Positive)" ($gramItem -and $gramItem.historyConsideration -eq "Positive") "got=$($gramItem.historyConsideration)"

# --- TEST 9: ranking consistency ---
$rankOrder = @("Highly Recommended","Recommended","Consider","Not Recommended")
$prevIdx = -1; $ordered = $true
foreach ($rec in $t1.Data.recommendations) {
    $idx = $rankOrder.IndexOf($rec.recommendation)
    if ($idx -lt $prevIdx) { $ordered = $false; break }
    $prevIdx = $idx
}
Check "T9.1 recommendations ordered by category" $ordered
$noContradiction = $true
foreach ($rec in $t1.Data.recommendations) {
    if ($rec.recommendation -eq "Highly Recommended" -and $rec.historyConsideration -eq "Negative") { $noContradiction = $false }
    if ($rec.recommendation -eq "Not Recommended" -and $rec.historyConsideration -eq "Positive") { $noContradiction = $false }
}
Check "T9.2 no contradictory recommendation/consideration" $noContradiction

# --- TEST 4: Kharif evaluation changes candidates ---
$t4 = ApiCall "GET" "$base/api/farms/$farmId/crop-recommendations?season=Kharif" $tokenA
$kharifNames = ($t4.Data.recommendations | ForEach-Object { $_.cropName }) -join ","
Check "T4.1 Kharif evaluation succeeds" ($t4.Status -eq 200)
Check "T4.2 Kharif candidates differ from Rabi" ($kharifNames -notmatch "Wheat" -and $kharifNames -match "Rice") "crops=$kharifNames"
Check "T4.3 invalid season -> 400" ((ApiCall "GET" "$base/api/farms/$farmId/crop-recommendations?season=Summer" $tokenA).Status -eq 400)

# --- TEST 2: new farm / no crop history ---
$noHistFarmName = "Crop Rec No-History Test Farm"
$nh = $myFarms.Data | Where-Object { $_.farmName -eq $noHistFarmName } | Select-Object -First 1
if (-not $nh) {
    $nhCreated = ApiCall "POST" "$base/api/farms" $tokenA @{
        farmName = $noHistFarmName; provinceId = 1; districtId = 103; tehsilId = 1007
        latitude = 33.6844; longitude = 73.0479; farmSize = 4; farmSizeUnit = "Acres"
        soilType = "Loamy"; irrigationType = "Canal"
    }
    $nh = $nhCreated.Data
}
SqlQuery "DELETE FROM Crops WHERE FarmId='$($nh.id)'" | Out-Null
$t2 = ApiCall "GET" "$base/api/farms/$($nh.id)/crop-recommendations?season=Rabi" $tokenA
Check "T2.1 no-history recommendation succeeds" ($t2.Status -eq 200)
Check "T2.2 history reported unavailable" ($t2.Data.cropHistory.available -eq $false)
Check "T2.3 no invented previous crop" ([string]::IsNullOrEmpty($t2.Data.cropHistory.previousCropName))
Check "T2.4 history note explains fallback" ($t2.Data.cropHistory.historyNote -match "not available")
Check "T2.5 suitability still used (candidates present)" ($t2.Data.recommendations.Count -ge 2)

# --- TEST 5: missing coordinates/weather ---
SqlQuery "UPDATE Farms SET Latitude=NULL, Longitude=NULL WHERE Id='$($nh.id)'" | Out-Null
$t5 = ApiCall "GET" "$base/api/farms/$($nh.id)/crop-recommendations?season=Rabi" $tokenA
Check "T5.1 no-coordinates recommendation does not crash" ($t5.Status -eq 200)
Check "T5.2 climate missing-data reported (Prompt 4 behavior preserved)" (($t5.Data.recommendations[0].missingData -join " ") -match "eather|limate")
SqlQuery "UPDATE Farms SET Latitude=33.6844, Longitude=73.0479 WHERE Id='$($nh.id)'" | Out-Null

# --- TEST 6/7/8: security ---
Check "T6 ownership: User B on User A farm -> 403" ((ApiCall "GET" "$base/api/farms/$farmId/crop-recommendations" $tokenB).Status -eq 403)
Check "T7 unknown farm -> 404" ((ApiCall "GET" "$base/api/farms/00000000-0000-0000-0000-000000000000/crop-recommendations" $tokenA).Status -eq 404)
Check "T8 no token -> 401" ((ApiCall "GET" "$base/api/farms/$farmId/crop-recommendations" $null).Status -eq 401)

# --- TEST 12: existing Crop CRUD regression (incl. new HarvestDate field) ---
$cropBody = @{ cropName="CRUD Regression Crop"; cropCatalogId=1; season="Rabi"; plantingDate="2025-11-10T00:00:00Z"; harvestDate="2026-04-20T00:00:00Z"; status="Active" }
$c1 = ApiCall "POST" "$base/api/farms/$farmId/crops" $tokenA $cropBody
Check "T12.1 create crop" ($c1.Status -eq 200 -and $c1.Data.cropName -eq "CRUD Regression Crop")
Check "T12.2 harvest date round-trips through CRUD" ($c1.Data.harvestDate -match "2026-04-20") "got=$($c1.Data.harvestDate)"
$c2 = ApiCall "GET" "$base/api/farms/$farmId/crops" $tokenA
Check "T12.3 list crops includes new record" (@($c2.Data | Where-Object { $_.id -eq $c1.Data.id }).Count -eq 1)
$c3 = ApiCall "GET" "$base/api/crops/$($c1.Data.id)" $tokenA
Check "T12.4 get crop by id" ($c3.Status -eq 200 -and $c3.Data.id -eq $c1.Data.id)
$upd = $cropBody.Clone(); $upd.status = "Harvested"; $upd.growthStage = "Harvesting"
$c4 = ApiCall "PUT" "$base/api/crops/$($c1.Data.id)" $tokenA $upd
Check "T12.5 update crop" ($c4.Status -eq 200 -and $c4.Data.status -eq "Harvested")
$c5 = ApiCall "DELETE" "$base/api/crops/$($c1.Data.id)" $tokenA
Check "T12.6 delete crop -> 204" ($c5.Status -eq 204)
$c6 = ApiCall "GET" "$base/api/crops/$($c1.Data.id)" $tokenA
Check "T12.7 deleted crop -> 404" ($c6.Status -eq 404)

# --- TEST 10: Prompt 4 suitability regression ---
$suit = ApiCall "GET" "$base/api/farms/$farmId/crop-suitability?season=Rabi" $tokenA
Check "T10.1 Prompt 4 endpoint still works" ($suit.Status -eq 200)
Check "T10.2 Prompt 4 candidates unchanged (Wheat/Gram/Lentil)" (($suit.Data.crops | ForEach-Object { $_.cropName }) -join "," -match "Wheat.*Gram.*Lentil|Gram.*Lentil.*Wheat") "crops=$(($suit.Data.crops | ForEach-Object { $_.cropName }) -join ',')"
Check "T10.3 Prompt 4 scores intact (top score 70-100)" ($suit.Data.crops[0].suitabilityScore -ge 70)

# --- TEST 11: weather regression ---
$w1 = ApiCall "GET" "$base/api/farms/$farmId/weather/current" $tokenA
$w2 = ApiCall "GET" "$base/api/farms/$farmId/weather/forecast" $tokenA
Check "T11.1 current weather works" ($w1.Status -eq 200)
Check "T11.2 forecast works with 7 days" ($w2.Status -eq 200 -and $w2.Data.forecast.days.Count -eq 7)
$w3 = ApiCall "GET" "$base/api/farms/$farmId/weather/current" $tokenA
Check "T11.3 cached second call returns data" ($w3.Status -eq 200)

# --- Ahmed Farm guard ---
$ahmedAfter = SqlQuery "SELECT FarmName, ProvinceId, DistrictId, TehsilId, Latitude, Longitude, FarmSize, FarmSizeUnit, SoilType, IrrigationType FROM Farms WHERE Id='$ahmedId'"
Check "Guard: Ahmed Farm unchanged" ($ahmedBefore -eq $ahmedAfter) "before=$ahmedBefore after=$ahmedAfter"

# --- Teardown: remove HistTC history crops. Their historical planting dates
# generate permanently-overdue monitoring checks that would pollute the exact
# due-list counts of the Prompt 7/8 regression suites on later runs. The next
# run recreates them idempotently in setup. ---
SqlQuery "DELETE FROM Crops WHERE FarmId='$farmId' AND CropName LIKE 'HistTC%'" | Out-Null

Write-Host ""
Write-Host "=== RESULT: $script:Pass passed, $script:Fail failed ===" -ForegroundColor $(if ($script:Fail -eq 0) { "Green" } else { "Red" })
if ($script:Fail -gt 0) { exit 1 }
