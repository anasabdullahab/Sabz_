# =============================================================================
# SABZ Prompt 17 - Crop Price Intelligence
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Sections: T1 authentication (4), T2 basic endpoint (6), T3 crop filtering (6),
# T4 location filtering (5), T5 date filtering (5), T6 pagination (7),
# T7 source transparency (5), T8 provider failure behaviour (2),
# T9 security (2), T10 financial isolation (1), T11 database integrity (3),
# T12 disclaimer (2).
#
# The feature is strictly read-only and creates no fixture data, so the suite
# is self-cleaning by construction and safe to run repeatedly. The current
# provider is the clearly-labelled SABZ reference dataset (non-live), so all
# expectations are deterministic and never depend on fake live data.
# =============================================================================
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5073'
$pass = 0
$fail = 0
$disclaimerText = 'Crop prices shown by SABZ are informational market data. Prices may change and SABZ does not predict prices, guarantee future prices, or provide financial, investment, or trading advice.'
$validStatuses = @('Live', 'Historical', 'Reference', 'Unavailable')

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { $script:pass++; Write-Host "  PASS  $name" -ForegroundColor Green }
    else            { $script:fail++; Write-Host "  FAIL  $name  $detail" -ForegroundColor Red }
}

function SqlQuery([string]$sql) {
    $tmp = Join-Path $env:TEMP ('cpq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

# Normalise PS 5.1 pipeline artefacts into a real array.
function AsArray($x) {
    if ($null -eq $x) { return ,@() }
    $a = @($x)
    if ($a.Count -eq 1 -and $a[0] -is [System.Array]) { return ,@($a[0]) }
    if ($a.Count -eq 1 -and $null -eq $a[0]) { return ,@() }
    return ,$a
}

Write-Host ''
Write-Host '=================================================================='
Write-Host ' SABZ Prompt 17 - Crop Price Intelligence Test Suite'
Write-Host '=================================================================='

$tokenA = Login 'test21@example.com' 'Test1234!'
if (-not $tokenA) {
    Write-Host 'FATAL: fixture login failed (test21).' -ForegroundColor Red
    exit 1
}
$hdrA = @{ Authorization = "Bearer $tokenA" }

# --- T1. Authentication (4) -----------------------------------------------------
Write-Host ''
Write-Host '--- T1. Authentication ---'
Check 'T1.1 feed without token -> 401'       ((ApiCall 'GET' "$base/api/crop-prices").Status -eq 401)
Check 'T1.2 detail without token -> 401'     ((ApiCall 'GET' "$base/api/crop-prices/Wheat").Status -eq 401)
Check 'T1.3 feed with malformed token -> 401' ((ApiCall 'GET' "$base/api/crop-prices" @{ Authorization = 'Bearer not.a.jwt' }).Status -eq 401)
Check 'T1.4 detail with malformed token -> 401' ((ApiCall 'GET' "$base/api/crop-prices/Wheat" @{ Authorization = 'Bearer not.a.jwt' }).Status -eq 401)

# --- T2. Basic endpoint (6) -------------------------------------------------------
Write-Host ''
Write-Host '--- T2. Basic endpoint ---'
$feed = ApiCall 'GET' "$base/api/crop-prices?pageSize=50" $hdrA
$items = AsArray $feed.Data.items
Check 'T2.1 feed -> 200 with paged shape' (($feed.Status -eq 200) -and ($null -ne $feed.Data.page) -and ($null -ne $feed.Data.pageSize) -and ($null -ne $feed.Data.totalCount) -and ($null -ne $feed.Data.totalPages) -and ($null -ne $feed.Data.dataStatus) -and ($null -ne $feed.Data.disclaimer)) "status=$($feed.Status)"
Check 'T2.2 defaults honoured (page=1, pageSize=50 respected, items <= pageSize)' (($feed.Data.page -eq 1) -and ($feed.Data.pageSize -eq 50) -and ($items.Count -le 50) -and ($items.Count -gt 0)) "items=$($items.Count)"
$first = $items[0]
Check 'T2.3 record shape (all spec fields present)' (($null -ne $first.cropName) -and ($null -ne $first.province) -and ($null -ne $first.district) -and ($null -ne $first.market) -and ($null -ne $first.price) -and ($null -ne $first.unit) -and ($null -ne $first.priceDate) -and ($null -ne $first.source) -and ($null -ne $first.dataStatus) -and ($null -ne $first.disclaimer))
Check 'T2.4 prices are positive decimals' ($items | Where-Object { [decimal]$_.price -le 0 } | Measure-Object | ForEach-Object { $_.Count -eq 0 })
Check 'T2.5 totalCount > 0 and totalPages consistent' (($feed.Data.totalCount -gt 0) -and ($feed.Data.totalPages -eq [math]::Ceiling($feed.Data.totalCount / 50))) "total=$($feed.Data.totalCount) pages=$($feed.Data.totalPages)"
$wheatDetail = ApiCall 'GET' "$base/api/crop-prices/Wheat" $hdrA
Check 'T2.6 detail -> 200 with latest + history + date range' (($wheatDetail.Status -eq 200) -and ($wheatDetail.Data.cropName -eq 'Wheat') -and ($null -ne $wheatDetail.Data.latest) -and ((AsArray $wheatDetail.Data.historicalRecords).Count -gt 0) -and ($null -ne $wheatDetail.Data.firstDate) -and ($null -ne $wheatDetail.Data.latestDate)) "status=$($wheatDetail.Status)"

# --- T3. Crop filtering (6) -------------------------------------------------------
Write-Host ''
Write-Host '--- T3. Crop filtering ---'
$wheatFeed = ApiCall 'GET' "$base/api/crop-prices?crop=Wheat&pageSize=50" $hdrA
$wheatItems = AsArray $wheatFeed.Data.items
Check 'T3.1 supported crop filter returns only that crop' (($wheatFeed.Status -eq 200) -and ($wheatItems.Count -gt 0) -and (($wheatItems | Where-Object { $_.cropName -ne 'Wheat' } | Measure-Object).Count -eq 0))
$lowerFeed = ApiCall 'GET' "$base/api/crop-prices?crop=wheat&pageSize=50" $hdrA
Check 'T3.2 crop filter case-insensitive (same total)' ($lowerFeed.Data.totalCount -eq $wheatFeed.Data.totalCount) "lower=$($lowerFeed.Data.totalCount) upper=$($wheatFeed.Data.totalCount)"
$gramFeed = ApiCall 'GET' ("$base/api/crop-prices?crop=" + [uri]::EscapeDataString('gram chickpea') + "&pageSize=50") $hdrA
$gramItems = AsArray $gramFeed.Data.items
Check 'T3.3 safe name normalisation matches catalog crop' (($gramFeed.Status -eq 200) -and ($gramItems.Count -gt 0) -and (($gramItems | Where-Object { $_.cropName -ne 'Gram (Chickpea)' } | Measure-Object).Count -eq 0))
$unknownFeed = ApiCall 'GET' "$base/api/crop-prices?crop=Dragonfruit&pageSize=50" $hdrA
Check 'T3.4 unknown crop -> honest empty result (no fake prices)' (($unknownFeed.Status -eq 200) -and ($unknownFeed.Data.totalCount -eq 0) -and ((AsArray $unknownFeed.Data.items).Count -eq 0) -and ($unknownFeed.Data.dataStatus -eq 'Unavailable'))
Check 'T3.5 unknown crop detail -> 404' ((ApiCall 'GET' "$base/api/crop-prices/Dragonfruit" $hdrA).Status -eq 404)
$potatoDetail = ApiCall 'GET' "$base/api/crop-prices/potato" $hdrA
Check 'T3.6 detail crop name case-insensitive, canonical returned' (($potatoDetail.Status -eq 200) -and ($potatoDetail.Data.cropName -eq 'Potato'))

# --- T4. Location filtering (5) ----------------------------------------------------
Write-Host ''
Write-Host '--- T4. Location filtering ---'
$punjab = ApiCall 'GET' "$base/api/crop-prices?province=Punjab&pageSize=50" $hdrA
Check 'T4.1 province filter returns records' (($punjab.Status -eq 200) -and ($punjab.Data.totalCount -gt 0))
$sindh = ApiCall 'GET' "$base/api/crop-prices?province=Sindh&pageSize=50" $hdrA
Check 'T4.2 unsupported province -> honest empty result' (($sindh.Status -eq 200) -and ($sindh.Data.totalCount -eq 0))
$multan = ApiCall 'GET' "$base/api/crop-prices?district=Multan&pageSize=50" $hdrA
$multanItems = AsArray $multan.Data.items
Check 'T4.3 district filter honoured' (($multan.Status -eq 200) -and ($multanItems.Count -gt 0) -and (($multanItems | Where-Object { $_.district -ne 'Multan' } | Measure-Object).Count -eq 0))
$market = ApiCall 'GET' "$base/api/crop-prices?market=Wholesale&pageSize=50" $hdrA
$marketItems = AsArray $market.Data.items
Check 'T4.4 market filter honoured' (($market.Status -eq 200) -and ($marketItems.Count -gt 0) -and (($marketItems | Where-Object { $_.market -notlike '*Wholesale*' } | Measure-Object).Count -eq 0))
$multanLower = ApiCall 'GET' "$base/api/crop-prices?district=multan&pageSize=50" $hdrA
Check 'T4.5 district filter case-insensitive' ($multanLower.Data.totalCount -eq $multan.Data.totalCount)

# --- T5. Date filtering (5) ---------------------------------------------------------
Write-Host ''
Write-Host '--- T5. Date filtering ---'
$fromFeed = ApiCall 'GET' "$base/api/crop-prices?fromDate=2026-08-23&pageSize=50" $hdrA
$fromItems = AsArray $fromFeed.Data.items
Check 'T5.1 fromDate inclusive' (($fromFeed.Status -eq 200) -and ($fromItems.Count -gt 0) -and (($fromItems | Where-Object { ([datetime]$_.priceDate).Date -lt [datetime]'2026-08-23' } | Measure-Object).Count -eq 0))
$toFeed = ApiCall 'GET' "$base/api/crop-prices?toDate=2026-08-21&pageSize=50" $hdrA
$toItems = AsArray $toFeed.Data.items
Check 'T5.2 toDate inclusive' (($toFeed.Status -eq 200) -and ($toItems.Count -gt 0) -and (($toItems | Where-Object { ([datetime]$_.priceDate).Date -gt [datetime]'2026-08-21' } | Measure-Object).Count -eq 0))
$rangeFeed = ApiCall 'GET' "$base/api/crop-prices?fromDate=2026-08-22&toDate=2026-08-23&pageSize=50" $hdrA
$rangeItems = AsArray $rangeFeed.Data.items
$rangeOk = $true
foreach ($r in $rangeItems) {
    $d = ([datetime]$r.priceDate).Date
    if ($d -lt [datetime]'2026-08-22' -or $d -gt [datetime]'2026-08-23') { $rangeOk = $false }
}
Check 'T5.3 date range inclusive both ends' (($rangeFeed.Status -eq 200) -and ($rangeItems.Count -gt 0) -and $rangeOk)
Check 'T5.4 fromDate > toDate -> 400' ((ApiCall 'GET' "$base/api/crop-prices?fromDate=2026-08-24&toDate=2026-08-20" $hdrA).Status -eq 400)
Check 'T5.5 invalid date string -> 400' ((ApiCall 'GET' "$base/api/crop-prices?fromDate=not-a-date" $hdrA).Status -eq 400)

# --- T6. Pagination (7) ----------------------------------------------------------------
Write-Host ''
Write-Host '--- T6. Pagination ---'
$p2 = ApiCall 'GET' "$base/api/crop-prices?pageSize=2&page=1" $hdrA
$p2b = ApiCall 'GET' "$base/api/crop-prices?pageSize=2&page=2" $hdrA
Check 'T6.1 pageSize respected' (($p2.Status -eq 200) -and ((AsArray $p2.Data.items).Count -le 2))
$p2items = AsArray $p2.Data.items
$p2bitems = AsArray $p2b.Data.items
$overlap = @($p2items | Where-Object { $x = $_; @($p2bitems | Where-Object { ($_.cropName -eq $x.cropName) -and ($_.district -eq $x.district) -and ($_.priceDate -eq $x.priceDate) }).Count -gt 0 })
Check 'T6.2 pages do not repeat records' ($overlap.Count -eq 0)
Check 'T6.3 pageSize=50 (maximum) accepted' ((ApiCall 'GET' "$base/api/crop-prices?pageSize=50" $hdrA).Status -eq 200)
Check 'T6.4 page=0 -> 400'   ((ApiCall 'GET' "$base/api/crop-prices?page=0" $hdrA).Status -eq 400)
Check 'T6.5 pageSize=0 -> 400' ((ApiCall 'GET' "$base/api/crop-prices?pageSize=0" $hdrA).Status -eq 400)
Check 'T6.6 pageSize=51 -> 400' ((ApiCall 'GET' "$base/api/crop-prices?pageSize=51" $hdrA).Status -eq 400)
$beyond = ApiCall 'GET' "$base/api/crop-prices?page=999&pageSize=50" $hdrA
Check 'T6.7 page beyond last -> honest empty page' (($beyond.Status -eq 200) -and ((AsArray $beyond.Data.items).Count -eq 0))

# --- T7. Source transparency (5) ----------------------------------------------------------
Write-Host ''
Write-Host '--- T7. Source transparency ---'
$allItems = $items
Check 'T7.1 every record has a source' (($allItems | Where-Object { [string]::IsNullOrWhiteSpace($_.source) } | Measure-Object).Count -eq 0)
Check 'T7.2 every record has a price date' (($allItems | Where-Object { $null -eq $_.priceDate } | Measure-Object).Count -eq 0)
Check 'T7.3 dataStatus always a controlled value' (($allItems | Where-Object { $script:validStatuses -notcontains $_.dataStatus } | Measure-Object).Count -eq 0)
Check 'T7.4 no record claims Live (reference provider is non-live)' (($allItems | Where-Object { $_.dataStatus -eq 'Live' } | Measure-Object).Count -eq 0)
Check 'T7.5 source consistently labelled "SABZ Reference Dataset"' (($allItems | Where-Object { $_.source -ne 'SABZ Reference Dataset' -or $_.dataStatus -ne 'Reference' } | Measure-Object).Count -eq 0)

# --- T8. Provider failure behaviour (2) ----------------------------------------------------
Write-Host ''
Write-Host '--- T8. Provider failure behaviour ---'
$err400 = ApiCall 'GET' "$base/api/crop-prices?pageSize=0" $hdrA
$err404 = ApiCall 'GET' "$base/api/crop-prices/Dragonfruit" $hdrA
$leak400 = ($err400.Raw -match 'System\.') -or ($err400.Raw -match 'at SABZ') -or ($err400.Raw -match 'stackTrace')
$leak404 = ($err404.Raw -match 'System\.') -or ($err404.Raw -match 'at SABZ') -or ($err404.Raw -match 'stackTrace')
Check 'T8.1 error responses are structured, no internal exception leakage' (($err400.Status -eq 400) -and ($err404.Status -eq 404) -and ($null -ne $err400.Data.message) -and ($null -ne $err404.Data.message) -and (-not $leak400) -and (-not $leak404))
$secretLeak = ($err400.Raw -match 'apiKey') -or ($err400.Raw -match 'password') -or ($err400.Raw -match 'Server=') -or ($err404.Raw -match 'apiKey') -or ($err404.Raw -match 'Server=')
Check 'T8.2 error responses leak no secrets/connection details' (-not $secretLeak)

# --- T9. Security (2) ------------------------------------------------------------------------
Write-Host ''
Write-Host '--- T9. Security ---'
$forbidden = @('userId','ownerId','email','phone','password','token','apiKey','secret')
$feedLeaks = @($forbidden | Where-Object { $feed.Raw -like ('*' + $_ + '*') })
Check 'T9.1 feed response exposes no identity/secret keys' ($feedLeaks.Count -eq 0) ("leaked: " + ($feedLeaks -join ','))
$detailLeaks = @($forbidden | Where-Object { $wheatDetail.Raw -like ('*' + $_ + '*') })
Check 'T9.2 detail response exposes no identity/secret keys' ($detailLeaks.Count -eq 0) ("leaked: " + ($detailLeaks -join ','))

# --- T10. Financial isolation (1) ---------------------------------------------------------------
Write-Host ''
Write-Host '--- T10. Financial isolation ---'
$finBefore = (SqlQuery 'SELECT COUNT(*) FROM FinancialTransactions') -join ''
ApiCall 'GET' "$base/api/crop-prices" $hdrA | Out-Null
ApiCall 'GET' "$base/api/crop-prices/Wheat" $hdrA | Out-Null
ApiCall 'GET' "$base/api/crop-prices?crop=Onion&district=Multan" $hdrA | Out-Null
$finAfter = (SqlQuery 'SELECT COUNT(*) FROM FinancialTransactions') -join ''
Check 'T10.1 price lookups create zero FinancialTransactions' ($finBefore -eq $finAfter) "before=$finBefore after=$finAfter"

# --- T11. Database integrity (3) ------------------------------------------------------------------
Write-Host ''
Write-Host '--- T11. Database integrity ---'
$tableCount = (SqlQuery 'SELECT COUNT(*) FROM sys.tables') -join ''
Check 'T11.1 table count unchanged (21)' ($tableCount -eq '21') "got=$tableCount"
$priceTables = (SqlQuery "SELECT COUNT(*) FROM sys.tables WHERE name LIKE '%Price%' OR name LIKE '%Market%Price%'") -join ''
Check 'T11.2 no price/cache tables created' ($priceTables -eq '0') "found=$priceTables"
$migrations = (SqlQuery 'SELECT COUNT(*) FROM [__EFMigrationsHistory]') -join ''
Check 'T11.3 migration count unchanged (11)' ($migrations -eq '11') "got=$migrations"

# --- T12. Disclaimer (2) -----------------------------------------------------------------------------
Write-Host ''
Write-Host '--- T12. Mandatory disclaimer ---'
Check 'T12.1 feed carries the mandatory disclaimer verbatim' ($feed.Data.disclaimer -eq $script:disclaimerText) "got '$($feed.Data.disclaimer)'"
Check 'T12.2 detail carries the mandatory disclaimer verbatim' ($wheatDetail.Data.disclaimer -eq $script:disclaimerText)

Write-Host ''
Write-Host "RESULT: $pass passed, $fail failed." -ForegroundColor ($(if ($fail -eq 0) { 'Green' } else { 'Red' }))
if ($fail -gt 0) { exit 1 }
exit 0
