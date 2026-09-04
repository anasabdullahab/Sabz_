# =============================================================================
# SABZ Prompt 8 - Central In-App Notification & Reminder Foundation
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# IN-APP ONLY: notifications are database rows served through the API. No
# SMS/email/push/external provider is involved or claimed.
#
# Idempotency strategy: every run deletes leftover "NOTIF " fixture crops
# (monitoring checks cascade-delete with crops) AND the fixture users'
# CropMonitoringCheck notifications, then recreates fixtures with planting
# dates relative to today, so due/upcoming/notification states are
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
    $tmp = Join-Path $env:TEMP ('nqbq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# GET returning JSON - uses Invoke-WebRequest + ConvertFrom-Json so arrays are
# never silently unwrapped (Invoke-RestMethod's unwrapping corrupts list loops).
function GetJson([string]$url, $headers) {
    $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing
    return ($resp.Content | ConvertFrom-Json)
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

function DeleteNotifCrops($token, $farmId) {
    try { $crops = @(GetJson "$base/api/farms/$farmId/crops" @{ Authorization = "Bearer $token" }) } catch { return }
    # Guard against a nested array (member-enumeration artefact on some hosts).
    if ($crops.Count -eq 1 -and $crops[0] -is [System.Array]) { $crops = @($crops[0]) }
    $notifs = @($crops | Where-Object { $_.cropName -like 'NOTIF *' })
    foreach ($c in $notifs) {
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

# Delete the fixture users' monitoring-check notifications so every run starts
# from a deterministic state (notifications never affect seed data).
function ResetFixtureNotifications() {
    SqlQuery "DELETE FROM Notifications WHERE ReferenceType='CropMonitoringCheck' AND UserId IN (SELECT Id FROM Users WHERE Email IN ('test21@example.com','userb3@example.com'))" | Out-Null
}

Write-Host "`n=== SABZ Prompt 8: In-App Notification Tests ===" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# Setup: logins, farms, deterministic fixture crops, clean notification state
# -----------------------------------------------------------------------------
Write-Host "`n--- Setup ---"
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
Check 'SETUP.1 User A login' ([bool]$tokenA)
Check 'SETUP.2 User B login' ([bool]$tokenB)
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }

$farmA = EnsureFarm $tokenA 'Notifications Test Farm'
$farmB = EnsureFarm $tokenB 'NOTIF User-B Test Farm'
Check 'SETUP.3 Farm A ready' ([bool]$farmA) "farmA=$farmA"
Check 'SETUP.4 Farm B ready' ([bool]$farmB)

# Ahmed Farm guard snapshot (must remain untouched)
$ahmedBefore = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''

# Idempotency: remove leftover fixtures from previous runs
DeleteNotifCrops $tokenA $farmA
DeleteNotifCrops $tokenB $farmB
ResetFixtureNotifications

$today = (Get-Date).ToUniversalTime().Date
$pdDueA1 = $today.AddDays(-40).ToString('yyyy-MM-dd')   # offsets 14/30 due, 60 upcoming
$pdDueA2 = $today.AddDays(-35).ToString('yyyy-MM-dd')   # offsets 14/30 due, 60 upcoming
$pdFuture = $today.AddDays(30).ToString('yyyy-MM-dd')   # all checks upcoming
$pdB = $today.AddDays(-20).ToString('yyyy-MM-dd')       # offset 14 due

$cropDueA1 = CreateCrop $tokenA $farmA 'NOTIF Wheat Due A' 1 $pdDueA1
$cropDueA2 = CreateCrop $tokenA $farmA 'NOTIF Wheat Due B' 1 $pdDueA2
$cropFuture = CreateCrop $tokenA $farmA 'NOTIF Future Wheat' 1 $pdFuture
$cropB = CreateCrop $tokenB $farmB 'NOTIF Wheat User-B' 1 $pdB

Check 'SETUP.5 due crop A1 created' ([bool]$cropDueA1.id)
Check 'SETUP.6 due crop A2 created' ([bool]$cropDueA2.id)
Check 'SETUP.7 future crop created (creation not broken)' ([bool]$cropFuture.id)
Check 'SETUP.8 user B crop created' ([bool]$cropB.id)
$emptyA = @(GetJson "$base/api/notifications" $hdrA)
Check 'SETUP.9 notification list starts empty after reset' ($emptyA.Count -eq 0) "count=$($emptyA.Count)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 1: lazy due-notification generation from the monitoring read path ---"
$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
$dueChecks = @($r.Data) | Sort-Object -Property scheduledDate
Check 'T1.1 due list has the 4 overdue wheat checks' ($r.Status -eq 200 -and $dueChecks.Count -eq 4) "status=$($r.Status) count=$($dueChecks.Count)"

$notifs = @(GetJson "$base/api/notifications" $hdrA)
Check 'T1.2 exactly 4 notifications created (one per due check)' ($notifs.Count -eq 4) "count=$($notifs.Count)"
Check 'T1.3 all are category MonitoringDue' (@($notifs | Where-Object { $_.category -ne 'MonitoringDue' }).Count -eq 0)
Check 'T1.4 all reference CropMonitoringCheck' (@($notifs | Where-Object { $_.referenceType -ne 'CropMonitoringCheck' }).Count -eq 0)
$dueIds = @($dueChecks | ForEach-Object { $_.id })
$matched = @($notifs | Where-Object { $dueIds -contains $_.referenceId })
Check 'T1.5 referenceId matches the due check ids' ($matched.Count -eq 4) "matched=$($matched.Count)"
Check 'T1.6 title is "Crop monitoring check due"' (@($notifs | Where-Object { $_.title -ne 'Crop monitoring check due' }).Count -eq 0)
Check 'T1.7 message explains the reminder' (@($notifs | Where-Object { $_.message -notmatch 'due now' }).Count -eq 0)
Check 'T1.8 all start unread with null readAt' (@($notifs | Where-Object { $_.isRead -or ($null -ne $_.readAt) }).Count -eq 0)

$r = ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA
Check 'T1.9 unread-count returns { count: 4 }' ($r.Status -eq 200 -and $r.Data.count -eq 4) "status=$($r.Status) count=$($r.Data.count)"
$unread = @(GetJson "$base/api/notifications/unread" $hdrA)
Check 'T1.10 unread list matches the count' ($unread.Count -eq 4) "count=$($unread.Count)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 2: idempotency - repeated due calls never duplicate notifications ---"
1..5 | ForEach-Object { ApiCall 'GET' "$base/api/monitoring/due" $hdrA | Out-Null }
$notifsAfter = @(GetJson "$base/api/notifications" $hdrA)
Check 'T2.1 five more due calls -> still exactly 4 notifications' ($notifsAfter.Count -eq 4) "count=$($notifsAfter.Count)"
$dbCount = (SqlQuery "SELECT COUNT(*) FROM Notifications WHERE Category='MonitoringDue' AND UserId IN (SELECT Id FROM Users WHERE Email='test21@example.com')") -join ''
Check 'T2.2 database confirms no duplicates (4 rows)' ($dbCount -eq '4') "db=$dbCount"

# Upcoming endpoint must not create notifications either
ApiCall 'GET' "$base/api/monitoring/upcoming" $hdrA | Out-Null
Check 'T2.3 upcoming calls create nothing' (@(GetJson "$base/api/notifications" $hdrA).Count -eq 4)

# User B isolation: B gets exactly their own single due notification
ApiCall 'GET' "$base/api/monitoring/due" $hdrB | Out-Null
$notifsB = @(GetJson "$base/api/notifications" $hdrB)
Check 'T2.4 user B gets exactly 1 notification for their 1 due check' ($notifsB.Count -eq 1) "count=$($notifsB.Count)"
Check 'T2.5 B notification references B''s due check' ($notifsB[0].referenceId -eq (@(GetJson "$base/api/monitoring/due" $hdrB))[0].id)
Check 'T2.6 A still has exactly 4 (no cross-user leak)' (@(GetJson "$base/api/notifications" $hdrA).Count -eq 4)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 3: authentication and validation (401 / 400) ---"
$anon = @{}
$r = ApiCall 'GET' "$base/api/notifications" $anon
Check 'T3.1 notifications without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/notifications/unread" $anon
Check 'T3.2 unread without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/notifications/unread-count" $anon
Check 'T3.3 unread-count without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'PATCH' "$base/api/notifications/read-all" $anon
Check 'T3.4 read-all without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'PATCH' "$base/api/notifications/$([Guid]::NewGuid())/read" $anon
Check 'T3.5 mark-read without token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$badHdr = @{ Authorization = 'Bearer not.a.jwt' }
$r = ApiCall 'GET' "$base/api/notifications" $badHdr
Check 'T3.6 malformed token -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'PATCH' "$base/api/notifications/read-all" $badHdr
Check 'T3.7 malformed token on read-all -> 401' ($r.Status -eq 401) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/notifications?take=0" $hdrA
Check 'T3.8 take=0 rejected -> 400' ($r.Status -eq 400) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 4: ownership, IDOR protection, DTO hygiene ---"
$nB = $notifsB[0]
$r = ApiCall 'PATCH' "$base/api/notifications/$($nB.id)/read" $hdrA
Check 'T4.1 mark another user''s notification -> 403' ($r.Status -eq 403) "got $($r.Status)"
$nA = $notifsAfter[0]
$r = ApiCall 'PATCH' "$base/api/notifications/$($nA.id)/read" $hdrB
Check 'T4.2 user B cannot mark A''s notification -> 403' ($r.Status -eq 403) "got $($r.Status)"
$r = ApiCall 'PATCH' "$base/api/notifications/$([Guid]::NewGuid())/read" $hdrA
Check 'T4.3 unknown notification -> 404' ($r.Status -eq 404) "got $($r.Status)"
$r = ApiCall 'PATCH' "$base/api/notifications/$($nB.id)/read" $hdrA
Check 'T4.4 forbidden mark did not change state' ($r.Status -eq 403 -and @(GetJson "$base/api/notifications/unread" $hdrB).Count -eq 1)
$rawResp = Invoke-WebRequest -Uri "$base/api/notifications" -Headers $hdrA -UseBasicParsing
Check 'T4.5 DTO never exposes userId' ($rawResp.Content -notmatch '"userId"')
Check 'T4.6 DTO carries the documented shape' ($null -ne $notifsAfter[0].id -and $null -ne $notifsAfter[0].category -and $null -ne $notifsAfter[0].createdAt)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 5: read state lifecycle (mark read / idempotent / read-all) ---"
$target = @(GetJson "$base/api/notifications/unread" $hdrA)[0]
$r = ApiCall 'PATCH' "$base/api/notifications/$($target.id)/read" $hdrA
Check 'T5.1 mark read -> 200 with isRead=true and readAt set' ($r.Status -eq 200 -and $r.Data.isRead -eq $true -and ($null -ne $r.Data.readAt)) "status=$($r.Status)"
$readAt1 = $r.Data.readAt
# Raw-JSON readAt extraction: PS 5.1 DateTime parsing/kind handling is unreliable
# for equality checks, so the serialized values are compared verbatim.
function RawReadAt([string]$notifId, $headers) {
    $raw = (Invoke-WebRequest -Uri "$base/api/notifications/$notifId/read" -Method Patch -Headers $headers -UseBasicParsing).Content
    if ($raw -match '"readAt"\s*:\s*"([^"]+)"') { return $matches[1] }
    return $null
}
$raw1 = RawReadAt $target.id $hdrA
$r = ApiCall 'PATCH' "$base/api/notifications/$($target.id)/read" $hdrA
Check 'T5.2 marking again is idempotent -> 200' ($r.Status -eq 200) "got $($r.Status)"
$raw2 = RawReadAt $target.id $hdrA
Check 'T5.3 readAt keeps the original timestamp' (($null -ne $raw1) -and ($raw1 -eq $raw2)) "first=$raw1 second=$raw2"

$r = ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA
Check 'T5.4 unread count dropped to 3' ($r.Data.count -eq 3) "count=$($r.Data.count)"
$unreadNow = @(GetJson "$base/api/notifications/unread" $hdrA)
Check 'T5.5 read notification absent from unread list' (@($unreadNow | Where-Object { $_.id -eq $target.id }).Count -eq 0)

$r = ApiCall 'PATCH' "$base/api/notifications/read-all" $hdrA
Check 'T5.6 read-all marks the remaining 3' ($r.Status -eq 200 -and $r.Data.markedRead -eq 3) "marked=$($r.Data.markedRead)"
$r = ApiCall 'GET' "$base/api/notifications/unread-count" $hdrA
Check 'T5.7 unread count now 0' ($r.Data.count -eq 0) "count=$($r.Data.count)"
$r = ApiCall 'PATCH' "$base/api/notifications/read-all" $hdrA
Check 'T5.8 read-all again -> 200 with markedRead=0 (idempotent)' ($r.Status -eq 200 -and $r.Data.markedRead -eq 0) "marked=$($r.Data.markedRead)"
$all = @(GetJson "$base/api/notifications" $hdrA)
Check 'T5.9 every stored notification is read with readAt' (@($all | Where-Object { -not $_.isRead -or ($null -eq $_.readAt) }).Count -eq 0)

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 6: no redundant Completed/Skipped/Upcoming notifications ---"
$dueBefore = @(GetJson "$base/api/monitoring/due" $hdrA)
$c1 = $dueBefore[0]
$c2 = $dueBefore[1]
$r = ApiCall 'POST' "$base/api/monitoring/$($c1.id)/complete" $hdrA (@{ Observation = 'Normal'; Notes = 'notification regression check' } | ConvertTo-Json) 'application/json'
Check 'T6.1 completing a check still works -> 200' ($r.Status -eq 200) "got $($r.Status)"
$afterComplete = @(GetJson "$base/api/notifications" $hdrA)
Check 'T6.2 completion creates NO MonitoringCompleted notification' ($afterComplete.Count -eq 4) "count=$($afterComplete.Count)"
$r = ApiCall 'POST' "$base/api/monitoring/$($c2.id)/skip" $hdrA (@{ Notes = 'notification regression skip' } | ConvertTo-Json) 'application/json'
Check 'T6.3 skipping a check still works -> 200' ($r.Status -eq 200) "got $($r.Status)"
$afterSkip = @(GetJson "$base/api/notifications" $hdrA)
Check 'T6.4 skip creates NO MonitoringSkipped notification' ($afterSkip.Count -eq 4) "count=$($afterSkip.Count)"
$upcomingCount = (SqlQuery "SELECT COUNT(*) FROM Notifications WHERE Category='MonitoringUpcoming'") -join ''
Check 'T6.5 no MonitoringUpcoming notifications exist anywhere' ($upcomingCount -eq '0') "db=$upcomingCount"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 7: concurrency - parallel due calls create exactly one per check ---"
$cropRace = CreateCrop $tokenA $farmA 'NOTIF Wheat Race' 1 $today.AddDays(-40).ToString('yyyy-MM-dd')
Check 'T7.1 race crop created (2 new due checks, no notifications yet)' ([bool]$cropRace.id)
$jobs = @()
1..6 | ForEach-Object {
    $jobs += Start-Job -ScriptBlock {
        param($u, $t)
        try { $resp = Invoke-WebRequest -Uri "$u/api/monitoring/due" -Headers @{ Authorization = "Bearer $t" } -UseBasicParsing; return [int]$resp.StatusCode }
        catch { return 0 }
    } -ArgumentList $base, $tokenA
}
$jobs | Wait-Job | Out-Null
$jobCodes = @($jobs | Receive-Job)
$jobs | Remove-Job -Force
Check 'T7.2 six parallel due calls all succeed' (@($jobCodes | Where-Object { $_ -eq 200 }).Count -eq 6) "codes=$($jobCodes -join ',')"
$raceNotifs = @(GetJson "$base/api/notifications" $hdrA)
Check 'T7.3 exactly 6 notifications total (4 original + 2 race)' ($raceNotifs.Count -eq 6) "count=$($raceNotifs.Count)"
$dup = (SqlQuery "SELECT COUNT(*) FROM (SELECT ReferenceId FROM Notifications WHERE ReferenceType='CropMonitoringCheck' GROUP BY UserId, ReferenceId, Category HAVING COUNT(*) > 1) d") -join ''
Check 'T7.4 database has zero duplicate (user, category, reference) rows' ($dup -eq '0') "dup=$dup"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 8: Prompt 4/5/6/7 regressions ---"
$r = ApiCall 'GET' "$base/api/monitoring/due" $hdrA
Check 'T8.1 due list now 4 (2 race + 2 remaining original)' (@($r.Data).Count -eq 4) "count=$(@($r.Data).Count)"
$r = ApiCall 'GET' "$base/api/monitoring/upcoming" $hdrA
Check 'T8.2 upcoming list now 6 (3 future + 2 offset-60 + 1 race offset-60)' (@($r.Data).Count -eq 6) "count=$(@($r.Data).Count)"
$r = ApiCall 'POST' "$base/api/crops/$($cropDueA1.id)/monitoring/generate" $hdrA
Check 'T8.3 P7 generate endpoint still idempotent (checksCreated=0)' ($r.Status -eq 200 -and $r.Data.checksCreated -eq 0) "created=$($r.Data.checksCreated)"

$ddEndpoint = "$base/api/farms/$farmA/disease-detection"
Add-Type -AssemblyName System.Drawing
$leafImg = Join-Path $env:TEMP 'notif-test-leaf.jpg'
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
$txtFile = Join-Path $env:TEMP 'notif-test-notes.txt'
Set-Content -Path $txtFile -Value 'not an image'
$r = MultipartPost $ddEndpoint $tokenA $leafImg 'image/jpeg'
Check 'T8.4 P6 valid image still reaches provider gate 502 (no key configured)' ($r.Status -eq 502) "got $($r.Status)"
Check 'T8.5 P6 no fake AI result (message says not configured)' ($r.Raw -match 'not configured')
$r = MultipartPost $ddEndpoint $tokenA $txtFile 'image/png'
Check 'T8.6 P6 non-image still rejected -> 400' ($r.Status -eq 400) "got $($r.Status)"
Remove-Item $leafImg, $txtFile -Force -ErrorAction SilentlyContinue

$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Rabi" $hdrA
Check 'T8.7 P4 suitability 200 with crops' ($r.Status -eq 200 -and @($r.Data.crops).Count -gt 0) "status=$($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-suitability?season=Invalid" $hdrA
Check 'T8.8 P4 invalid season still 400' ($r.Status -eq 400) "got $($r.Status)"
$r = ApiCall 'GET' "$base/api/farms/$farmA/crop-recommendations?season=Rabi" $hdrA
Check 'T8.9 P5 recommendations 200' ($r.Status -eq 200 -and @($r.Data.recommendations).Count -gt 0) "status=$($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 9: crop CRUD regression with notifications decoupled ---"
$crud = CreateCrop $tokenA $farmA 'NOTIF CRUD Temp' 1 $today.AddDays(-5).ToString('yyyy-MM-dd')
Check 'T9.1 crop create 200/201' ([bool]$crud.id)
$updBody = @{ CropName = 'NOTIF CRUD Temp'; Season = 'Rabi'; CropCatalogId = 1; GrowthStage = 'Vegetative' } | ConvertTo-Json
$r = ApiCall 'PUT' "$base/api/crops/$($crud.id)" $hdrA $updBody 'application/json'
Check 'T9.2 crop update 200' ($r.Status -eq 200) "got $($r.Status)"
$notifsBeforeDelete = @(GetJson "$base/api/notifications" $hdrA).Count
$r = ApiCall 'DELETE' "$base/api/crops/$($crud.id)" $hdrA
Check 'T9.3 crop delete 204' ($r.Status -eq 204) "got $($r.Status)"
$notifsAfterDelete = @(GetJson "$base/api/notifications" $hdrA).Count
Check 'T9.4 deleting a crop never touches notification state' ($notifsBeforeDelete -eq $notifsAfterDelete) "before=$notifsBeforeDelete after=$notifsAfterDelete"
$r = ApiCall 'GET' "$base/api/crops/$($crud.id)" $hdrA
Check 'T9.5 deleted crop -> 404' ($r.Status -eq 404) "got $($r.Status)"

# -----------------------------------------------------------------------------
Write-Host "`n--- TEST 10: data integrity guards ---"
$ahmedAfter = (SqlQuery "SELECT FarmName + '|' + CAST(FarmSize AS varchar) + '|' + CAST(Latitude AS varchar) + '|' + CAST(SoilType AS varchar) FROM Farms WHERE Id='D5FBCA89-5C3C-401E-BA23-FDFF84054300'") -join ''
Check 'T10.1 Ahmed Farm untouched' ($ahmedBefore -eq $ahmedAfter) "before=$ahmedBefore after=$ahmedAfter"
$ruleCount = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringRules") -join ''
Check 'T10.2 seed monitoring rules intact (15)' ($ruleCount -eq '15') "count=$ruleCount"
$catalogCount = (SqlQuery "SELECT COUNT(*) FROM CropCatalog") -join ''
Check 'T10.3 crop catalog intact (22)' ($catalogCount -eq '22') "count=$catalogCount"
$uniqueIdx = (SqlQuery "SELECT COUNT(*) FROM sys.indexes WHERE name='IX_Notifications_UserId_Category_ReferenceType_ReferenceId' AND is_unique=1") -join ''
Check 'T10.4 duplicate-prevention unique index exists' ($uniqueIdx -eq '1') "idx=$uniqueIdx"

# -----------------------------------------------------------------------------
Write-Host "`n--- Cleanup: remove fixtures so the script is safe to rerun ---"
DeleteNotifCrops $tokenA $farmA
DeleteNotifCrops $tokenB $farmB
ResetFixtureNotifications
$leftover = (SqlQuery "SELECT COUNT(*) FROM Crops WHERE CropName LIKE 'NOTIF %'") -join ''
$orphanNotifs = (SqlQuery "SELECT COUNT(*) FROM Notifications WHERE ReferenceType='CropMonitoringCheck' AND UserId IN (SELECT Id FROM Users WHERE Email IN ('test21@example.com','userb3@example.com'))") -join ''
$orphanChecks = (SqlQuery "SELECT COUNT(*) FROM CropMonitoringChecks WHERE CropId NOT IN (SELECT Id FROM Crops)") -join ''
Check 'CLEANUP.1 all fixture crops removed' ($leftover -eq '0') "leftover=$leftover"
Check 'CLEANUP.2 fixture notifications removed' ($orphanNotifs -eq '0') "remaining=$orphanNotifs"
Check 'CLEANUP.3 no orphaned monitoring checks' ($orphanChecks -eq '0') "orphans=$orphanChecks"

# -----------------------------------------------------------------------------
Write-Host "`n=== RESULTS: $pass passed, $fail failed (total $($pass + $fail)) ===" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 } else { exit 0 }
