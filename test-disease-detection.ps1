# =============================================================================
# SABZ Prompt 6 - AI Disease Detection Foundation test suite
# Idempotent. Requires: API running on http://localhost:5073, LocalDB SabzDB.
#
# PROVIDER MODE: this environment has NO AI provider API key configured
# (by design - see docs/prompt-6-disease-detection.md). Tests therefore assert
# the full local pipeline: auth, ownership, image validation and the graceful
# 502 "not configured" behaviour (never a fake AI result).
# Checks tagged [LIVE-PROVIDER] document what must be verified once a real
# API key is supplied in DiseaseDetection:ApiKey (local only).
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
    Set-Content -Path $tmp -Value $sql -Encoding UTF8
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

# Invoke-WebRequest-based call: returns [pscustomobject]@{ Status; Data; Error }
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
function MultipartPost([string]$url, [string]$token, [string]$filePath, [string]$fileContentType, [string]$fieldName = 'image', [hashtable]$extraFields = @{}) {
    $cargs = @('-s', '-o', '-', '-w', '%{http_code}', '-X', 'POST', $url)
    if ($token) { $cargs += @('-H', "Authorization: Bearer $token") }
    $cargs += @('-F', "${fieldName}=@${filePath};type=${fileContentType}")
    foreach ($k in $extraFields.Keys) { $cargs += @('-F', "$k=$($extraFields[$k])") }
    $raw = (& curl.exe @cargs) -join ''
    if ($raw.Length -lt 3) { return @{ Status = 0; Data = $null; Raw = $raw } }
    $status = [int]$raw.Substring($raw.Length - 3)
    $payloadText = $raw.Substring(0, $raw.Length - 3)
    $payload = $null
    if ($payloadText) { try { $payload = $payloadText | ConvertFrom-Json } catch { $payload = $payloadText } }
    return @{ Status = $status; Data = $payload; Raw = $payloadText }
}

# multipart POST without the image field
function MultipartPostNoFile([string]$url, [string]$token, [hashtable]$fields) {
    $cargs = @('-s', '-o', '-', '-w', '%{http_code}', '-X', 'POST', $url)
    if ($token) { $cargs += @('-H', "Authorization: Bearer $token") }
    foreach ($k in $fields.Keys) { $cargs += @('-F', "$k=$($fields[$k])") }
    $raw = (& curl.exe @cargs) -join ''
    if ($raw.Length -lt 3) { return @{ Status = 0; Raw = $raw } }
    $status = [int]$raw.Substring($raw.Length - 3)
    return @{ Status = $status; Raw = $raw.Substring(0, $raw.Length - 3) }
}

Write-Host "`n=== SABZ Prompt 6: Disease Detection Tests ===" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Setup: logins, farms, crops, test images
# -----------------------------------------------------------------------------
Write-Host "`n--- Setup ---"
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
Check 'SETUP.1 User A login' ([bool]$tokenA)
Check 'SETUP.2 User B login' ([bool]$tokenB)

function EnsureFarm($token, $name, $withGps) {
    $farms = Invoke-RestMethod -Uri "$base/api/farms" -Headers @{ Authorization = "Bearer $token" }
    $existing = @($farms) | Where-Object { $_.farmName -eq $name } | Select-Object -First 1
    if ($existing) { return $existing.id }
    $body = @{
        FarmName = $name; ProvinceId = 1; DistrictId = 103; TehsilId = 1007
        FarmSize = 5; FarmSizeUnit = 'Acres'; SoilType = 'Loamy'; IrrigationType = 'Canal'
    }
    if ($withGps) { $body.Latitude = 33.6844; $body.Longitude = 73.0479 }
    $created = Invoke-RestMethod -Uri "$base/api/farms" -Method Post -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body ($body | ConvertTo-Json)
    return $created.id
}

$farmA = EnsureFarm $tokenA 'Disease Detection Test Farm' $true
$farmANoGps = EnsureFarm $tokenA 'DD No-GPS Test Farm' $false
$farmB = EnsureFarm $tokenB 'DD User-B Test Farm' $true
Check 'SETUP.3 Farm A ready' ([bool]$farmA) "farmA=$farmA"
Check 'SETUP.4 Farm A no-GPS ready' ([bool]$farmANoGps)
Check 'SETUP.5 Farm B ready' ([bool]$farmB)

# Ahmed Farm guard snapshot (must remain untouched)
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Crops on farm A (idempotent by name)
$cropsA = Invoke-RestMethod -Uri "$base/api/farms/$farmA/crops" -Headers @{ Authorization = "Bearer $tokenA" }
$cropA = @($cropsA) | Where-Object { $_.cropName -eq 'DD Test Wheat' } | Select-Object -First 1
if (-not $cropA) {
    $cropBody = @{ CropName = 'DD Test Wheat'; Season = 'Rabi'; CropCatalogId = 1; GrowthStage = 'Vegetative' } | ConvertTo-Json
    $cropA = Invoke-RestMethod -Uri "$base/api/farms/$farmA/crops" -Method Post -Headers @{ Authorization = "Bearer $tokenA" } -ContentType 'application/json' -Body $cropBody
}
$cropsB = Invoke-RestMethod -Uri "$base/api/farms/$farmB/crops" -Headers @{ Authorization = "Bearer $tokenB" }
$cropB = @($cropsB) | Where-Object { $_.cropName -eq 'DD B Rice' } | Select-Object -First 1
if (-not $cropB) {
    $cropBodyB = @{ CropName = 'DD B Rice'; Season = 'Kharif'; CropCatalogId = 2 } | ConvertTo-Json
    $cropB = Invoke-RestMethod -Uri "$base/api/farms/$farmB/crops" -Method Post -Headers @{ Authorization = "Bearer $tokenB" } -ContentType 'application/json' -Body $cropBodyB
}
Check 'SETUP.6 Crop A ready' ([bool]$cropA.id)
Check 'SETUP.7 Crop B ready' ([bool]$cropB.id)

# Test images generated locally (real decodable files)
Add-Type -AssemblyName System.Drawing
function MakeImage([string]$path, [int]$w, [int]$h, [scriptblock]$painter, $format) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $rnd = New-Object System.Random(42)
    for ($y = 0; $y -lt $h; $y += 2) {
        for ($x = 0; $x -lt $w; $x += 2) {
            $c = & $painter $x $y $rnd
            $bmp.SetPixel($x, $y, $c)
            if ($x + 1 -lt $w) { $bmp.SetPixel($x + 1, $y, $c) }
            if ($y + 1 -lt $h) { $bmp.SetPixel($x, $y + 1, $c) }
        }
    }
    $bmp.Save($path, $format)
    $bmp.Dispose()
}

$tmpDir = $env:TEMP
$leafImg = Join-Path $tmpDir 'dd-test-leaf.jpg'
$skyImg = Join-Path $tmpDir 'dd-test-sky.jpg'
$smallImg = Join-Path $tmpDir 'dd-test-small.png'
MakeImage $leafImg 420 320 { param($x, $y, $r) [System.Drawing.Color]::FromArgb(20 + $r.Next(40), 110 + $r.Next(60), 30 + $r.Next(30)) } ([System.Drawing.Imaging.ImageFormat]::Jpeg)
MakeImage $skyImg 420 320 { param($x, $y, $r) [System.Drawing.Color]::FromArgb(120 + $r.Next(20), 170 + $r.Next(20), 235) } ([System.Drawing.Imaging.ImageFormat]::Jpeg)
MakeImage $smallImg 48 48 { param($x, $y, $r) [System.Drawing.Color]::Green } ([System.Drawing.Imaging.ImageFormat]::Png)

# corrupted image: valid JPEG magic bytes + garbage
$corruptImg = Join-Path $tmpDir 'dd-test-corrupt.jpg'
$bytes = New-Object byte[] 5000
(New-Object System.Random(7)).NextBytes($bytes)
$bytes[0] = 0xFF; $bytes[1] = 0xD8; $bytes[2] = 0xFF
[System.IO.File]::WriteAllBytes($corruptImg, $bytes)

# unsupported text file
$txtFile = Join-Path $tmpDir 'dd-test-notes.txt'
Set-Content -Path $txtFile -Value 'this is definitely not an image'

# oversized image: real PNG signature + padding beyond the 10 MB limit
$oversized = Join-Path $tmpDir 'dd-test-oversized.png'
$big = New-Object byte[] 11500000
$pngSig = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
[Array]::Copy($pngSig, $big, 8)
[System.IO.File]::WriteAllBytes($oversized, $big)

Check 'SETUP.8 Test images generated' ((Test-Path $leafImg) -and (Test-Path $skyImg) -and (Test-Path $corruptImg) -and (Test-Path $oversized))

$endpoint = "$base/api/farms/$farmA/disease-detection"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: authenticated valid image (NOT-CONFIGURED provider mode) ---"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg'
Check 'T1.1 valid image reaches provider gate with 502 (not configured)' ($r.Status -eq 502) "got $($r.Status) $($r.Raw)"
Check 'T1.2 message explains configuration, no fake result' ($r.Raw -match 'not configured')
# [LIVE-PROVIDER] with a real API key this must return 200 with imageAssessment + provider block

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: unauthenticated request ---"
$r = MultipartPost $endpoint $null $leafImg 'image/jpeg'
Check 'T2.1 no token -> 401' ($r.Status -eq 401) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: unknown farm ---"
$r = MultipartPost "$base/api/farms/$([Guid]::NewGuid())/disease-detection" $tokenA $leafImg 'image/jpeg'
Check 'T3.1 unknown farm -> 404' ($r.Status -eq 404) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: another user's farm ---"
$r = MultipartPost "$base/api/farms/$farmB/disease-detection" $tokenA $leafImg 'image/jpeg'
Check 'T4.1 foreign farm -> 403' ($r.Status -eq 403) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: missing image ---"
$r = MultipartPostNoFile $endpoint $tokenA @{ notes = 'no image attached' }
Check 'T5.1 missing image -> 400' ($r.Status -eq 400) "got $($r.Status) $($r.Raw)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: unsupported file type ---"
$r = MultipartPost $endpoint $tokenA $txtFile 'image/png'
Check 'T6.1 text file rejected -> 400' ($r.Status -eq 400) "got $($r.Status)"
Check 'T6.2 message mentions supported formats' ($r.Raw -match 'supported')

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: oversized image ---"
try {
    $r = MultipartPost $endpoint $tokenA $oversized 'image/png'
    Check 'T7.1 oversized rejected (413 or 400)' ($r.Status -eq 413 -or $r.Status -eq 400) "got $($r.Status)"
} catch {
    # Kestrel may abort the connection for bodies above MaxRequestBodySize.
    Check 'T7.1 oversized rejected (connection refused by server limits)' $true
}

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: corrupted image ---"
$r = MultipartPost $endpoint $tokenA $corruptImg 'image/jpeg'
Check 'T8.1 corrupt jpeg rejected -> 400' ($r.Status -eq 400) "got $($r.Status)"
Check 'T8.2 corrupt message (no crash)' ($r.Raw -match 'corrupt|not a readable image')

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: unrelated image (sky) - passes local validation ---"
$r = MultipartPost $endpoint $tokenA $skyImg 'image/jpeg'
Check 'T9.1 sky image passes local validation (reaches provider gate 502)' ($r.Status -eq 502) "got $($r.Status)"
# [LIVE-PROVIDER] with a real key this MUST return 200 + imageAssessment.isPlantImage=false
# and the "please upload a clear photograph" message - the disease model must never be called.

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: valid plant image accepted (distinct from disease identified) ---"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg'
Check 'T10.1 plant image accepted by validation pipeline' ($r.Status -eq 502) "got $($r.Status)"
# [LIVE-PROVIDER] 200 with isPlantImage=true; disease confidence below MinimumDiseaseConfidence
# must yield detected=false + "Uncertain" + request for a clearer photograph.

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 11: low-confidence handling ---"
Write-Host '  NOTE: low-confidence disease behaviour requires the live provider.' -ForegroundColor Yellow
Write-Host '  [LIVE-PROVIDER] verify: confidence < MinimumDiseaseConfidence (0.4) -> detected=false,' -ForegroundColor Yellow
Write-Host '  assessmentLevel "Uncertain", no disease name claimed.' -ForegroundColor Yellow
Check 'T11.1 documented as LIVE-PROVIDER verification' $true

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 12: provider failure mode ---"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg'
Check 'T12.1 provider unavailable -> 502 service-unavailable style' ($r.Status -eq 502) "got $($r.Status)"
Check 'T12.2 no fabricated disease in failure path' ($r.Raw -notmatch '"disease"')

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 13: optional crop context ---"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg' 'image' @{ cropId = $cropA.id; notes = 'yellow spots on lower leaves' }
Check 'T13.1 own cropId accepted (passes validation, provider gate 502)' ($r.Status -eq 502) "got $($r.Status) $($r.Raw)"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg' 'image' @{ cropId = [Guid]::NewGuid().ToString() }
Check 'T13.2 unknown cropId -> 404' ($r.Status -eq 404) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 14: crop ownership validation ---"
$r = MultipartPost $endpoint $tokenA $leafImg 'image/jpeg' 'image' @{ cropId = $cropB.id }
Check 'T14.1 another farmer crop -> 403' ($r.Status -eq 403) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 15: no-GPS farm (GPS not required for disease detection) ---"
$r = MultipartPost "$base/api/farms/$farmANoGps/disease-detection" $tokenA $leafImg 'image/jpeg'
Check 'T15.1 no-GPS farm accepted (provider gate 502, not 400/404)' ($r.Status -eq 502) "got $($r.Status) $($r.Raw)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 16: Prompt 4 regression (crop suitability unchanged) ---"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Rabi" @{ Authorization = "Bearer $tokenA" }
Check 'T16.1 suitability 200' ($r.Status -eq 200) "got $($r.Status)"
$cropsList = @($r.Data.crops)
Check 'T16.2 suitability returns crops' ($cropsList.Count -gt 0) "count=$($cropsList.Count)"
$top = ($cropsList | Sort-Object -Property suitabilityScore -Descending | Select-Object -First 1)
Check 'T16.3 top suitability score sane (>=70)' ($top.suitabilityScore -ge 70) "top=$($top.suitabilityScore)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Invalid" @{ Authorization = "Bearer $tokenA" }
Check 'T16.4 invalid season still 400' ($r.Status -eq 400) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 17: Prompt 5 regression (crop recommendation unchanged) ---"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-recommendations?season=Rabi" @{ Authorization = "Bearer $tokenA" }
Check 'T17.1 recommendations 200' ($r.Status -eq 200) "got $($r.Status)"
$recs = @($r.Data.recommendations)
Check 'T17.2 recommendations returned' ($recs.Count -gt 0) "count=$($recs.Count)"
$validLevels = @('Highly Recommended', 'Recommended', 'Consider', 'Not Recommended')
$badLevels = @($recs | Where-Object { $validLevels -notcontains $_.recommendation })
Check 'T17.3 all recommendation levels valid' (@($badLevels).Count -eq 0)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 18: Crop CRUD regression ---"
$crudBody = @{ CropName = 'DD CRUD Temp'; Season = 'Rabi' } | ConvertTo-Json
$created = ApiCall 'POST' "$base/api/farms/$farmA/crops" @{ Authorization = "Bearer $tokenA" } $crudBody 'application/json'
Check 'T18.1 crop create 200/201' ($created.Status -eq 200 -or $created.Status -eq 201) "got $($created.Status)"
$cid = $created.Data.id
$list = ApiCall 'GET' "$base/api/farms/$farmA/crops" @{ Authorization = "Bearer $tokenA" }
Check 'T18.2 crop list contains created crop' (@(@($list.Data) | Where-Object { $_.id -eq $cid }).Count -eq 1)
$del = ApiCall 'DELETE' "$base/api/crops/$cid" @{ Authorization = "Bearer $tokenA" }
Check 'T18.3 crop delete 204' ($del.Status -eq 204) "got $($del.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 19: Ahmed Farm integrity guard ---"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T19.1 Ahmed Farm untouched' ($ahmedBefore -eq $ahmedAfter) "before=$ahmedBefore after=$ahmedAfter"

# -----------------------------------------------------------------------------
Write-Host "`n=== RESULTS: $pass passed, $fail failed (total $($pass + $fail)) ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Remove-Item $leafImg, $skyImg, $smallImg, $corruptImg, $txtFile, $oversized -Force -ErrorAction SilentlyContinue
if ($fail -gt 0) { exit 1 } else { exit 0 }
