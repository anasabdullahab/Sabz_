# =============================================================================
# SABZ Prompt 15 - Farmer Marketplace + Private Inbox Foundation
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Sections: authentication (9), listing creation (19), ownership (5),
# search/filter (8), inbox (8), conversation security (6), data leakage (2),
# financial isolation (1), persistence (5).
#
# Idempotency strategy: every run deletes leftover "MK " fixture listings of
# the fixture users through the public API (soft delete), then recreates
# fixtures. Conversations tied to old deleted listings are harmless leftovers
# and are never confused with fresh ones (checks match by listing id).
# Seed/reference data and other users' content are never touched.
# =============================================================================
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5073'
$pass = 0
$fail = 0
$prefix = 'MK '
$fakePhone = '+92 300 0000001'

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { $script:pass++; Write-Host "  PASS  $name" -ForegroundColor Green }
    else            { $script:fail++; Write-Host "  FAIL  $name  $detail" -ForegroundColor Red }
}

function SqlQuery([string]$sql) {
    $tmp = Join-Path $env:TEMP ('mkq_' + [Guid]::NewGuid().ToString('N') + '.sql')
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

function TryGetJson([string]$url, $headers) {
    try { return (GetJson $url $headers) } catch { return $null }
}

function TryGetRaw([string]$url, $headers) {
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing
        return [string]$resp.Content
    } catch { return $null }
}

# Normalise PS 5.1 pipeline artefacts into a real array (comma operator
# prevents single-element array unwrapping on return).
function AsArray($x) {
    if ($null -eq $x) { return ,@() }
    $a = @($x)
    if ($a.Count -eq 1 -and $a[0] -is [System.Array]) { return ,@($a[0]) }
    if ($a.Count -eq 1 -and $null -eq $a[0]) { return ,@() }
    return ,$a
}

function NewListingBody([hashtable]$overrides = @{}, [string[]]$remove = @()) {
    $tag = [Guid]::NewGuid().ToString('N').Substring(0, 6)
    $body = @{
        Title         = "$script:prefix" + "Tractor $tag"
        Category      = "$script:prefix" + 'Tractors'
        ListingType   = 'Sale'
        Description   = "$script:prefix" + "Test listing $tag for the marketplace suite."
        Price         = 100000
        PriceUnit     = 'Total'
        Location      = "$script:prefix" + 'Peshawar'
        ContactNumber = $script:fakePhone
        Condition     = 'Used'
        Availability  = 'Available'
    }
    foreach ($k in @($body.Keys)) { if ($remove -contains $k) { $body.Remove($k) } }
    foreach ($k in $overrides.Keys) { $body[$k] = $overrides[$k] }
    return ($body | ConvertTo-Json)
}

function CreateListing($headers, $jsonBody) {
    return ApiCall 'POST' "$base/api/marketplace/listings" $headers $jsonBody
}

# Delete every fixture ("MK ") listing owned by this user (idempotency).
function CleanupMarketplace($headers) {
    $ids = @()
    for ($p = 1; $p -le 10; $p++) {
        $page = TryGetJson "$base/api/marketplace/listings?page=$p&pageSize=50" $headers
        if ($null -eq $page) { break }
        $items = AsArray $page.items
        if ($items.Count -eq 0) { break }
        $mine = @($items | Where-Object { $_.isOwnedByCurrentUser -and $_.title -like "$script:prefix*" })
        foreach ($l in $mine) { $ids += $l.id }
        if ($items.Count -lt 50) { break }
    }
    foreach ($id in $ids) {
        ApiCall 'DELETE' "$base/api/marketplace/listings/$id" $headers | Out-Null
    }
    return $ids.Count
}

Write-Host ''
Write-Host '=================================================================='
Write-Host ' SABZ Prompt 15 - Farmer Marketplace + Private Inbox Test Suite'
Write-Host '=================================================================='

# --- Fixtures ----------------------------------------------------------------
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'

# Third, unrelated farmer for conversation-security checks (registered
# idempotently; a 409 on later runs is fine).
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

$cleanedA = CleanupMarketplace $hdrA
$cleanedB = CleanupMarketplace $hdrB
Write-Host "Fixture cleanup: removed $cleanedA/$cleanedB leftover MK listings."

$finBefore = (SqlQuery 'SELECT COUNT(*) FROM FinancialTransactions') -join ''

# --- 1. Authentication (9) ---------------------------------------------------
Write-Host ''
Write-Host '--- Authentication: every marketplace/inbox endpoint rejects missing tokens ---'
$randomGuid = [Guid]::NewGuid()
Check '1.1 401 listing feed'        ((ApiCall 'GET'    "$base/api/marketplace/listings").Status -eq 401)
Check '1.2 401 listing create'      ((ApiCall 'POST'   "$base/api/marketplace/listings" @{} (NewListingBody)).Status -eq 401)
Check '1.3 401 listing detail'      ((ApiCall 'GET'    "$base/api/marketplace/listings/$randomGuid").Status -eq 401)
Check '1.4 401 listing update'      ((ApiCall 'PUT'    "$base/api/marketplace/listings/$randomGuid" @{} (NewListingBody)).Status -eq 401)
Check '1.5 401 listing delete'      ((ApiCall 'DELETE' "$base/api/marketplace/listings/$randomGuid").Status -eq 401)
Check '1.6 401 inbox'               ((ApiCall 'GET'    "$base/api/marketplace/inbox").Status -eq 401)
Check '1.7 401 conversation'        ((ApiCall 'GET'    "$base/api/marketplace/inbox/$randomGuid").Status -eq 401)
Check '1.8 401 contact'             ((ApiCall 'POST'   "$base/api/marketplace/listings/$randomGuid/contact" @{} (@{ message = 'x' } | ConvertTo-Json)).Status -eq 401)
Check '1.9 401 send message'        ((ApiCall 'POST'   "$base/api/marketplace/inbox/$randomGuid/messages" @{} (@{ message = 'x' } | ConvertTo-Json)).Status -eq 401)

# --- 2. Listing creation (19) ------------------------------------------------
Write-Host ''
Write-Host '--- Listing creation and validation ---'
$sale = CreateListing $hdrA (NewListingBody @{ Title = "$prefix" + 'Tractor Sale Peshawar X1'; Category = "$prefix" + 'Tractors X1'; Location = "$prefix" + 'Peshawar X1'; Price = 2800000 })
Check '2.1 valid sale listing created' ($sale.Status -eq 200 -and $sale.Data.listingType -eq 'Sale' -and $sale.Data.isOwnedByCurrentUser -eq $true)
$saleId = $sale.Data.id

$rent = CreateListing $hdrA (NewListingBody @{ Title = "$prefix" + 'Harvester Rent Lahore X2'; Category = "$prefix" + 'Harvesters X2'; ListingType = 'Rent'; PriceUnit = 'Day'; Price = 5000; Location = "$prefix" + 'Lahore X2'; Condition = 'New' })
Check '2.2 valid rent listing created' ($rent.Status -eq 200 -and $rent.Data.listingType -eq 'Rent' -and $rent.Data.priceUnit -eq 'Day')
$rentId = $rent.Data.id

Check '2.3 missing title -> 400'          ((CreateListing $hdrA (NewListingBody @{} @('Title'))).Status -eq 400)
Check '2.4 whitespace title -> 400'       ((CreateListing $hdrA (NewListingBody @{ Title = '    ' })).Status -eq 400)
Check '2.5 oversized title -> 400'        ((CreateListing $hdrA (NewListingBody @{ Title = ('T' * 151) })).Status -eq 400)
Check '2.6 missing category -> 400'       ((CreateListing $hdrA (NewListingBody @{} @('Category'))).Status -eq 400)
Check '2.7 invalid listingType -> 400'    ((CreateListing $hdrA (NewListingBody @{ ListingType = 'Lease' })).Status -eq 400)
Check '2.8 missing description -> 400'    ((CreateListing $hdrA (NewListingBody @{} @('Description'))).Status -eq 400)
Check '2.9 oversized description -> 400'  ((CreateListing $hdrA (NewListingBody @{ Description = ('D' * 2001) })).Status -eq 400)
Check '2.10 zero price -> 400'            ((CreateListing $hdrA (NewListingBody @{ Price = 0 })).Status -eq 400)
Check '2.11 negative price -> 400'        ((CreateListing $hdrA (NewListingBody @{ Price = -5 })).Status -eq 400)
Check '2.12 excessive price -> 400'       ((CreateListing $hdrA (NewListingBody @{ Price = 2000000000 })).Status -eq 400)
Check '2.13 missing location -> 400'      ((CreateListing $hdrA (NewListingBody @{} @('Location'))).Status -eq 400)
Check '2.14 missing contact -> 400'       ((CreateListing $hdrA (NewListingBody @{} @('ContactNumber'))).Status -eq 400)
Check '2.15 invalid condition -> 400'     ((CreateListing $hdrA (NewListingBody @{ Condition = 'Refurbished' })).Status -eq 400)
Check '2.16 missing availability -> 400'  ((CreateListing $hdrA (NewListingBody @{} @('Availability'))).Status -eq 400)
Check '2.17 invalid image URL -> 400'     ((CreateListing $hdrA (NewListingBody @{ ImageUrl = 'not-a-url' })).Status -eq 400)
Check '2.18 local file path -> 400'       ((CreateListing $hdrA (NewListingBody @{ ImageUrl = 'C:\images\tractor.jpg' })).Status -eq 400)

$img = CreateListing $hdrA (NewListingBody @{ Title = "$prefix" + 'Sprayer Sale Multan X3'; ImageUrl = 'https://example.com/images/sprayer.jpg' })
Check '2.19 HTTPS image URL accepted' ($img.Status -eq 200 -and $img.Data.imageUrl -eq 'https://example.com/images/sprayer.jpg')
$imgId = $img.Data.id

# --- 3. Ownership (5) ---------------------------------------------------------
Write-Host ''
Write-Host '--- Listing ownership ---'
$updBody = NewListingBody @{ Title = "$prefix" + 'Tractor Sale Peshawar X1 UPDATED'; Category = "$prefix" + 'Tractors X1'; Location = "$prefix" + 'Peshawar X1'; Price = 2900000; Availability = 'Available from 1 September' }
$updByOwner = ApiCall 'PUT' "$base/api/marketplace/listings/$saleId" $hdrA $updBody
Check '3.1 owner can update' ($updByOwner.Status -eq 200 -and $updByOwner.Data.title -like '*UPDATED' -and $null -ne $updByOwner.Data.updatedAt)

$updByOther = ApiCall 'PUT' "$base/api/marketplace/listings/$saleId" $hdrB $updBody
Check '3.2 other user update -> 403' ($updByOther.Status -eq 403)

$delByOther = ApiCall 'DELETE' "$base/api/marketplace/listings/$imgId" $hdrB
Check '3.3 other user delete -> 403' ($delByOther.Status -eq 403)

$delByOwner = ApiCall 'DELETE' "$base/api/marketplace/listings/$imgId" $hdrA
$getDeleted = ApiCall 'GET' "$base/api/marketplace/listings/$imgId" $hdrA
Check '3.4 owner delete -> 204, then detail -> 404' ($delByOwner.Status -eq 204 -and $getDeleted.Status -eq 404)

$feedAfterDelete = TryGetJson "$base/api/marketplace/listings?pageSize=50" $hdrA
$deletedVisible = @(AsArray $feedAfterDelete.items | Where-Object { $_.id -eq $imgId })
Check '3.5 deleted listing absent from feed' ($deletedVisible.Count -eq 0)

# --- 4. Search / filter / pagination (8) --------------------------------------
Write-Host ''
Write-Host '--- Search, filters and pagination ---'
$searchHit = TryGetJson "$base/api/marketplace/listings?search=X1" $hdrB
$searchIds = @(AsArray $searchHit.items | ForEach-Object { $_.id })
Check '4.1 search matches title (X1 finds sale listing)' ($searchIds -contains $saleId -and -not ($searchIds -contains $rentId))

$catHit = TryGetJson "$base/api/marketplace/listings?category=Harvesters" $hdrB
$catIds = @(AsArray $catHit.items | ForEach-Object { $_.id })
Check '4.2 category filter finds harvester listing' ($catIds -contains $rentId -and -not ($catIds -contains $saleId))

$typeHit = TryGetJson "$base/api/marketplace/listings?listingType=Rent" $hdrB
$typeItems = @(AsArray $typeHit.items | Where-Object { $_.title -like "$prefix*" })
Check '4.3 listingType=Rent returns only rent fixtures' ($typeItems.Count -ge 1 -and @($typeItems | Where-Object { $_.id -eq $saleId }).Count -eq 0 -and @($typeItems | Where-Object { $_.id -eq $rentId }).Count -eq 1)

$locHit = TryGetJson "$base/api/marketplace/listings?location=Lahore" $hdrB
$locIds = @(AsArray $locHit.items | ForEach-Object { $_.id })
Check '4.4 location filter finds Lahore listing' ($locIds -contains $rentId -and -not ($locIds -contains $saleId))

$condHit = TryGetJson "$base/api/marketplace/listings?condition=New" $hdrB
$condIds = @(AsArray $condHit.items | ForEach-Object { $_.id })
Check '4.5 condition=New finds the new harvester' ($condIds -contains $rentId -and -not ($condIds -contains $saleId))

$page1 = TryGetJson "$base/api/marketplace/listings?page=1&pageSize=2" $hdrB
Check '4.6 pagination: pageSize=2 respected with total count' ((AsArray $page1.items).Count -le 2 -and $page1.totalCount -ge 2 -and $page1.page -eq 1 -and $page1.pageSize -eq 2)

Check '4.7 page=0 -> 400' ((ApiCall 'GET' "$base/api/marketplace/listings?page=0" $hdrB).Status -eq 400)
Check '4.8 pageSize=51 -> 400' ((ApiCall 'GET' "$base/api/marketplace/listings?pageSize=51" $hdrB).Status -eq 400)

# --- 5. Inbox (8) --------------------------------------------------------------
Write-Host ''
Write-Host '--- Private inbox: contact, conversation, messaging ---'
$contact1 = ApiCall 'POST' "$base/api/marketplace/listings/$saleId/contact" $hdrB (@{ message = "$prefix" + 'Is this tractor available tomorrow?' } | ConvertTo-Json)
$conv = $contact1.Data
Check '5.1 buyer starts conversation' ($contact1.Status -eq 200 -and $null -ne $conv.conversationId -and $conv.currentUserRole -eq 'Buyer' -and (AsArray $conv.messages.items).Count -eq 1)
$convId = $conv.conversationId

$contact2 = ApiCall 'POST' "$base/api/marketplace/listings/$saleId/contact" $hdrB (@{ message = "$prefix" + 'Following up - same conversation?' } | ConvertTo-Json)
Check '5.2 duplicate contact reuses conversation (no duplicates)' ($contact2.Status -eq 200 -and $contact2.Data.conversationId -eq $convId)

$inboxSeller = TryGetJson "$base/api/marketplace/inbox?pageSize=50" $hdrA
$sellerRow = @(AsArray $inboxSeller.items | Where-Object { $_.conversationId -eq $convId })
Check '5.3 seller sees conversation with Buyer role + buyer name' ($sellerRow.Count -eq 1 -and $sellerRow[0].role -eq 'Seller' -and $sellerRow[0].listingId -eq $saleId -and $sellerRow[0].otherParticipantName)

$inboxBuyer = TryGetJson "$base/api/marketplace/inbox?pageSize=50" $hdrB
$buyerRow = @(AsArray $inboxBuyer.items | Where-Object { $_.conversationId -eq $convId })
Check '5.4 buyer sees conversation with Buyer role + seller name' ($buyerRow.Count -eq 1 -and $buyerRow[0].role -eq 'Buyer' -and $buyerRow[0].otherParticipantName)

$msg1 = ApiCall 'POST' "$base/api/marketplace/inbox/$convId/messages" $hdrB (@{ message = "$prefix" + 'Buyer asks: can I inspect it this week?' } | ConvertTo-Json)
Check '5.5 buyer sends message' ($msg1.Status -eq 200 -and $msg1.Data.isOwnMessage -eq $true -and $msg1.Data.messageId)

$msg2 = ApiCall 'POST' "$base/api/marketplace/inbox/$convId/messages" $hdrA (@{ message = "$prefix" + 'Seller replies: yes, any day before noon.' } | ConvertTo-Json)
Check '5.6 seller replies' ($msg2.Status -eq 200 -and $msg2.Data.isOwnMessage -eq $true)

$convPage = TryGetJson "$base/api/marketplace/inbox/$convId`?page=1&pageSize=2" $hdrB
Check '5.7 message pagination works' ((AsArray $convPage.messages.items).Count -eq 2 -and $convPage.messages.totalCount -ge 3 -and $convPage.messages.totalPages -ge 2)

$inboxAfterReply = TryGetJson "$base/api/marketplace/inbox?pageSize=50" $hdrB
$firstRow = @(AsArray $inboxAfterReply.items)
$ordered = ($firstRow.Count -ge 1 -and $firstRow[0].conversationId -eq $convId -and $firstRow[0].latestMessagePreview -like "$prefix*Seller replies*")
Check '5.8 latest message ordering (inbox newest-activity first + preview)' $ordered

# --- 6. Conversation security (6) ----------------------------------------------
Write-Host ''
Write-Host '--- Conversation security: participants only, no spoofing ---'
Check '6.1 unrelated farmer cannot read conversation' ((ApiCall 'GET' "$base/api/marketplace/inbox/$convId" $hdrC).Status -eq 403)
Check '6.2 unrelated farmer cannot send message' ((ApiCall 'POST' "$base/api/marketplace/inbox/$convId/messages" $hdrC (@{ message = 'intruder' } | ConvertTo-Json)).Status -eq 403)

$spoofSender = ApiCall 'POST' "$base/api/marketplace/inbox/$convId/messages" $hdrB (@{ message = "$prefix" + 'Spoof attempt sender.'; senderUserId = [Guid]::NewGuid() } | ConvertTo-Json)
Check '6.3 sender cannot be spoofed (body senderUserId ignored)' ($spoofSender.Status -eq 200 -and $spoofSender.Data.isOwnMessage -eq $true)

$spoofBuyer = ApiCall 'POST' "$base/api/marketplace/listings/$rentId/contact" $hdrB (@{ message = "$prefix" + 'Spoof attempt buyer.'; buyerUserId = [Guid]::NewGuid() } | ConvertTo-Json)
Check '6.4 buyer cannot be spoofed (body buyerUserId ignored)' ($spoofBuyer.Status -eq 200 -and $spoofBuyer.Data.currentUserRole -eq 'Buyer')

$convAfterSpoof = TryGetJson "$base/api/marketplace/inbox/$($spoofBuyer.Data.conversationId)?pageSize=50" $hdrA
Check '6.5 seller cannot be spoofed (conversation owned by real seller)' ($null -ne $convAfterSpoof -and $convAfterSpoof.currentUserRole -eq 'Seller' -and $convAfterSpoof.listingId -eq $rentId)

Check '6.6 seller cannot contact own listing' ((ApiCall 'POST' "$base/api/marketplace/listings/$saleId/contact" $hdrA (@{ message = 'self contact' } | ConvertTo-Json)).Status -eq 400)

# --- 7. Data leakage (2) ----------------------------------------------------------
Write-Host ''
Write-Host '--- Data leakage: raw JSON never exposes ids, email, phone, secrets ---'
$raws = @()
$raws += TryGetRaw "$base/api/marketplace/listings?pageSize=50" $hdrB
$raws += TryGetRaw "$base/api/marketplace/listings/$saleId" $hdrB   # non-owner detail
$raws += TryGetRaw "$base/api/marketplace/inbox?pageSize=50" $hdrA
$raws += TryGetRaw "$base/api/marketplace/inbox/$convId" $hdrA
$forbidden = @('"userId"', '"buyerUserId"', '"sellerUserId"', '"senderUserId"', '"email"', '"password"', '"passwordHash"', '"token"', '"apiKey"', 'test21@example.com', 'userb3@example.com')
$leakFound = @()
foreach ($raw in $raws) {
    if ($null -eq $raw) { continue }
    foreach ($f in $forbidden) { if ($raw.IndexOf($f, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $leakFound += $f } }
}
Check '7.1 no ids/email/password/token in marketplace+inbox responses' ($leakFound.Count -eq 0) ($leakFound -join ',')

$feedRaw = TryGetRaw "$base/api/marketplace/listings?pageSize=50" $hdrB
$phoneInFeed = ($null -ne $feedRaw -and $feedRaw.IndexOf($fakePhone, [System.StringComparison]::Ordinal) -ge 0)
$contactKeyInFeed = ($null -ne $feedRaw -and $feedRaw.IndexOf('contactNumber', [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
Check '7.2 seller contact number absent from public feed' (-not $phoneInFeed -and -not $contactKeyInFeed)

# --- 8. Financial isolation (1) -----------------------------------------------------
Write-Host ''
Write-Host '--- Financial isolation ---'
$finMid = (SqlQuery 'SELECT COUNT(*) FROM FinancialTransactions') -join ''
Check '8.1 marketplace activity creates zero FinancialTransactions' ($finMid -eq $finBefore) "before=$finBefore after=$finMid"

# --- 9. Persistence (5) ----------------------------------------------------------------
Write-Host ''
Write-Host '--- Persistence and soft-delete behaviour ---'
$reSale = ApiCall 'GET' "$base/api/marketplace/listings/$saleId" $hdrA
Check '9.1 listing persists (owner detail shows contact number)' ($reSale.Status -eq 200 -and $reSale.Data.contactNumber -eq $fakePhone -and $reSale.Data.isOwnedByCurrentUser -eq $true)

$reConv = TryGetJson "$base/api/marketplace/inbox/$convId`?page=1&pageSize=50" $hdrB
Check '9.2 messages persist (oldest-first, >=3)' ((AsArray $reConv.messages.items).Count -ge 3 -and (AsArray $reConv.messages.items)[0].content -like "$prefix*available tomorrow*")

ApiCall 'DELETE' "$base/api/marketplace/listings/$saleId" $hdrA | Out-Null
$dbDeleted = (SqlQuery "SELECT CAST(IsDeleted AS varchar) + '|' + CASE WHEN DeletedAt IS NOT NULL THEN 'ts' ELSE 'null' END FROM MarketplaceListings WHERE Id='$saleId'") -join ''
Check '9.3 soft deletion recorded in DB (IsDeleted=1, DeletedAt set)' ($dbDeleted -eq '1|ts') "got=$dbDeleted"

$convAfterListingDelete = TryGetJson "$base/api/marketplace/inbox/$convId`?page=1&pageSize=50" $hdrB
Check '9.4 conversation remains valid after listing deletion' ($null -ne $convAfterListingDelete -and $convAfterListingDelete.listingTitle -like "$prefix*" -and (AsArray $convAfterListingDelete.messages.items).Count -ge 3)

$orphans = 0
$orphans += [int]((SqlQuery 'SELECT COUNT(*) FROM MarketplaceListings l LEFT JOIN Users u ON l.UserId=u.Id WHERE u.Id IS NULL') -join '')
$orphans += [int]((SqlQuery 'SELECT COUNT(*) FROM MarketplaceConversations c LEFT JOIN MarketplaceListings l ON c.ListingId=l.Id WHERE l.Id IS NULL') -join '')
$orphans += [int]((SqlQuery 'SELECT COUNT(*) FROM MarketplaceConversations c LEFT JOIN Users ub ON c.BuyerUserId=ub.Id LEFT JOIN Users us ON c.SellerUserId=us.Id WHERE ub.Id IS NULL OR us.Id IS NULL') -join '')
$orphans += [int]((SqlQuery 'SELECT COUNT(*) FROM MarketplaceMessages m LEFT JOIN MarketplaceConversations c ON m.ConversationId=c.Id WHERE c.Id IS NULL') -join '')
$orphans += [int]((SqlQuery 'SELECT COUNT(*) FROM MarketplaceMessages m LEFT JOIN Users u ON m.SenderUserId=u.Id WHERE u.Id IS NULL') -join '')
$dupConv = [int]((SqlQuery 'SELECT COUNT(*) FROM (SELECT ListingId, BuyerUserId, SellerUserId FROM MarketplaceConversations GROUP BY ListingId, BuyerUserId, SellerUserId HAVING COUNT(*) > 1) d') -join '')
Check '9.5 no orphan records, no duplicate conversation identities' ($orphans -eq 0 -and $dupConv -eq 0) "orphans=$orphans dups=$dupConv"

# --- Final cleanup (idempotency): remove remaining MK listings ------------------------
CleanupMarketplace $hdrA | Out-Null
CleanupMarketplace $hdrB | Out-Null

$finAfter = (SqlQuery 'SELECT COUNT(*) FROM FinancialTransactions') -join ''
if ($finAfter -ne $finBefore) { Write-Host "WARNING: FinancialTransactions changed ($finBefore -> $finAfter)" -ForegroundColor Yellow }

Write-Host ''
Write-Host '=================== MARKETPLACE SUITE SUMMARY ==================='
Write-Host "PASS: $pass   FAIL: $fail"
if ($fail -eq 0) { Write-Host 'ALL CHECKS PASSED' -ForegroundColor Green; exit 0 }
else { Write-Host 'CHECKS FAILED' -ForegroundColor Red; exit 1 }
