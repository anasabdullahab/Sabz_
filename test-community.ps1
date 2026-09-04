# =============================================================================
# SABZ Prompt 14 - Farmer Community Foundation
# Idempotent test suite. Requires: API on http://localhost:5073, LocalDB SabzDB.
#
# Covers the 35 spec checks: authentication (1-5), posts (6-16),
# ownership (17-19), comments (20-28), security (29-31), persistence (32-35).
#
# Idempotency strategy: every run deletes leftover "CM " fixture posts of both
# fixture users through the public API (soft delete), then recreates fixtures.
# Seed/reference data and other users' content are never touched.
# =============================================================================
$ErrorActionPreference = 'Continue'
$base = 'http://localhost:5073'
$pass = 0
$fail = 0
$prefix = 'CM '
$randomPost = [Guid]::NewGuid()
$randomComment = [Guid]::NewGuid()

function Check([string]$name, [bool]$condition, [string]$detail = '') {
    if ($condition) { $script:pass++; Write-Host "  PASS  $name" -ForegroundColor Green }
    else            { $script:fail++; Write-Host "  FAIL  $name  $detail" -ForegroundColor Red }
}

function Login([string]$identifier, [string]$password) {
    try {
        $body = @{ Identifier = $identifier; Password = $password } | ConvertTo-Json
        $resp = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $body
        return $resp.token
    } catch { return $null }
}

# Invoke-WebRequest-based call: returns @{ Status; Data; Raw; Error }
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

# GET returning JSON via Invoke-WebRequest so arrays are never silently
# unwrapped (Invoke-RestMethod's unwrapping corrupts list loops).
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

# Normalise PS 5.1 pipeline artefacts into a real array. The leading comma
# operator prevents PowerShell from unwrapping single-element arrays on return.
function AsArray($x) {
    if ($null -eq $x) { return ,@() }
    $a = @($x)
    if ($a.Count -eq 1 -and $a[0] -is [System.Array]) { return ,@($a[0]) }
    if ($a.Count -eq 1 -and $null -eq $a[0]) { return ,@() }
    return ,$a
}

function CreatePost($headers, $content, $imageUrl) {
    $body = @{ Content = $content }
    if ($imageUrl) { $body.ImageUrl = $imageUrl }
    return ApiCall 'POST' "$base/api/community/posts" $headers ($body | ConvertTo-Json)
}

function CreateComment($headers, $postId, $content) {
    $body = @{ Content = $content }
    return ApiCall 'POST' "$base/api/community/posts/$postId/comments" $headers ($body | ConvertTo-Json)
}

# Delete every fixture ("CM ") post owned by this user (idempotency).
function CleanupCommunity($headers) {
    $ids = @()
    for ($p = 1; $p -le 10; $p++) {
        $page = TryGetJson "$base/api/community/posts?page=$p&pageSize=50" $headers
        if ($null -eq $page) { break }
        $items = AsArray $page.items
        if ($items.Count -eq 0) { break }
        $mine = @($items | Where-Object { $_.isOwnedByCurrentUser -and $_.content -like "$script:prefix*" })
        foreach ($post in $mine) { $ids += $post.id }
        if ($items.Count -lt 50) { break }
    }
    foreach ($id in $ids) {
        ApiCall 'DELETE' "$base/api/community/posts/$id" $headers | Out-Null
    }
    return $ids.Count
}

Write-Host ''
Write-Host '=================================================================='
Write-Host ' SABZ Prompt 14 - Farmer Community Foundation Test Suite'
Write-Host '=================================================================='

# --- Fixtures ----------------------------------------------------------------
$tokenA = Login 'test21@example.com' 'Test1234!'
$tokenB = Login 'userb3@example.com' 'Test1234!'
if (-not $tokenA -or -not $tokenB) {
    Write-Host 'FATAL: fixture login failed (test21@example.com / userb3@example.com).' -ForegroundColor Red
    exit 1
}
$hdrA = @{ Authorization = "Bearer $tokenA" }
$hdrB = @{ Authorization = "Bearer $tokenB" }

$cleanedA = CleanupCommunity $hdrA
$cleanedB = CleanupCommunity $hdrB
Write-Host "Cleanup: removed $cleanedA (user A) + $cleanedB (user B) leftover fixture posts."

# --- 1-5: Authentication -----------------------------------------------------
Write-Host ''
Write-Host '--- Authentication (checks 1-5) ---'

$noTok = ApiCall 'GET' "$base/api/community/posts" @{}
$malformed = ApiCall 'GET' "$base/api/community/posts" @{ Authorization = 'Bearer not.a.real.token' }
Check '1. GET feed without token -> 401 (malformed token also rejected)' ($noTok.Status -eq 401 -and $malformed.Status -eq 401) "status=$($noTok.Status)/$($malformed.Status)"

$r = ApiCall 'POST' "$base/api/community/posts" @{} (@{ Content = 'CM unauthenticated' } | ConvertTo-Json)
Check '2. POST post without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"

$r = ApiCall 'DELETE' "$base/api/community/posts/$randomPost" @{}
Check '3. DELETE post without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"

$r = ApiCall 'POST' "$base/api/community/posts/$randomPost/comments" @{} (@{ Content = 'CM unauthenticated comment' } | ConvertTo-Json)
Check '4. POST comment without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"

$r = ApiCall 'DELETE' "$base/api/community/comments/$randomComment" @{}
Check '5. DELETE comment without token -> 401' ($r.Status -eq 401) "status=$($r.Status)"

# --- 6-16: Posts ---------------------------------------------------------------
Write-Host ''
Write-Host '--- Posts (checks 6-16) ---'

$post1 = CreatePost $hdrA 'CM Post: best wheat irrigation schedule for Rabi season?' 'https://example.com/images/wheat.jpg'
Check '6. create valid post' ($post1.Status -eq 200 -and $post1.Data.id) "status=$($post1.Status)"
$post1Id = $post1.Data.id

Check '7. returned post has expected content + author name + image URL' `
    ($post1.Data.content -eq 'CM Post: best wheat irrigation schedule for Rabi season?' -and $post1.Data.authorName -and $post1.Data.imageUrl -eq 'https://example.com/images/wheat.jpg') `
    "author=$($post1.Data.authorName)"

$raw1 = [string]($post1.Raw)
Check '8. UserId is not leaked in create-post response' ($raw1 -notmatch 'userId' -and $raw1 -notmatch 'password') ''

$detail1 = ApiCall 'GET' "$base/api/community/posts/$post1Id" $hdrA
Check '9. retrieve post returns post + comments array' `
    ($detail1.Status -eq 200 -and $detail1.Data.post.id -eq $post1Id -and $null -ne $detail1.Data.comments) `
    "status=$($detail1.Status)"

$feed = TryGetJson "$base/api/community/posts?page=1&pageSize=50" $hdrA
$feedItems = AsArray $feed.items
Check '10. feed contains post' (@($feedItems | Where-Object { $_.id -eq $post1Id }).Count -eq 1) ''

$post2 = CreatePost $hdrA 'CM Page alpha: sharing my tubewell experience' $null
$post3 = CreatePost $hdrA 'CM Page beta: asking about urea prices' $null
$page1 = TryGetJson "$base/api/community/posts?page=1&pageSize=1" $hdrA
$page2 = TryGetJson "$base/api/community/posts?page=2&pageSize=1" $hdrA
$badPageSize = ApiCall 'GET' "$base/api/community/posts?pageSize=51" $hdrA
$badPage = ApiCall 'GET' "$base/api/community/posts?page=0" $hdrA
$i1 = @(AsArray $page1.items)
$i2 = @(AsArray $page2.items)
Check '11. pagination works (page/pageSize/totalCount + invalid params 400)' `
    ($i1.Count -eq 1 -and $i2.Count -eq 1 -and $i1[0].id -ne $i2[0].id -and $page1.totalCount -ge 3 -and $page1.pageSize -eq 1 -and $badPageSize.Status -eq 400 -and $badPage.Status -eq 400) `
    "counts=$($i1.Count)/$($i2.Count) total=$($page1.totalCount) bad=$($badPageSize.Status)/$($badPage.Status)"

$feed = TryGetJson "$base/api/community/posts?page=1&pageSize=50" $hdrA
$feedItems = AsArray $feed.items
$idx3 = -1; $idx2 = -1
for ($i = 0; $i -lt $feedItems.Count; $i++) {
    if ($feedItems[$i].id -eq $post3.Data.id) { $idx3 = $i }
    if ($feedItems[$i].id -eq $post2.Data.id) { $idx2 = $i }
}
Check '12. newest-first ordering works' ($idx3 -ge 0 -and $idx2 -ge 0 -and $idx3 -lt $idx2) "idx(newer)=$idx3 idx(older)=$idx2"

$r = CreatePost $hdrA '   ' $null
Check '13. whitespace content rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$r = CreatePost $hdrA '' $null
Check '14. empty content rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$r = CreatePost $hdrA ('x' * 2001) $null
Check '15. overlong content (2001 chars) rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$badImg1 = CreatePost $hdrA 'CM bad image 1' 'C:\Windows\System32\cmd.exe'
$badImg2 = CreatePost $hdrA 'CM bad image 2' '/etc/passwd'
$badImg3 = CreatePost $hdrA 'CM bad image 3' 'file:///c:/temp/img.png'
$badImg4 = CreatePost $hdrA 'CM bad image 4' 'not a url'
Check '16. invalid image URLs rejected -> 400 (paths/file scheme/non-URL)' `
    ($badImg1.Status -eq 400 -and $badImg2.Status -eq 400 -and $badImg3.Status -eq 400 -and $badImg4.Status -eq 400) `
    "statuses=$($badImg1.Status)/$($badImg2.Status)/$($badImg3.Status)/$($badImg4.Status)"

# --- 17-19: Ownership ----------------------------------------------------------
Write-Host ''
Write-Host '--- Ownership (checks 17-19) ---'

$postDel = CreatePost $hdrA 'CM Post: scheduled for deletion' $null
$del = ApiCall 'DELETE' "$base/api/community/posts/$($postDel.Data.id)" $hdrA
$gone = ApiCall 'GET' "$base/api/community/posts/$($postDel.Data.id)" $hdrA
Check '17. owner can delete own post (204, then 404)' ($del.Status -eq 204 -and $gone.Status -eq 404) "del=$($del.Status) get=$($gone.Status)"

$foreign = ApiCall 'DELETE' "$base/api/community/posts/$post1Id" $hdrB
$stillThere = ApiCall 'GET' "$base/api/community/posts/$post1Id" $hdrA
Check '18. another user cannot delete the post (403, post intact)' ($foreign.Status -eq 403 -and $stillThere.Status -eq 200) "del=$($foreign.Status) get=$($stillThere.Status)"

$unknownGet = ApiCall 'GET' "$base/api/community/posts/$randomPost" $hdrA
$unknownDel = ApiCall 'DELETE' "$base/api/community/posts/$randomPost" $hdrA
Check '19. unknown post returns 404 (GET and DELETE)' ($unknownGet.Status -eq 404 -and $unknownDel.Status -eq 404) "get=$($unknownGet.Status) del=$($unknownDel.Status)"

# --- 20-28: Comments -------------------------------------------------------------
Write-Host ''
Write-Host '--- Comments (checks 20-28) ---'

$c1 = CreateComment $hdrB $post1Id 'CM Comment: drip irrigation worked well for my wheat.'
Check '20. create comment' ($c1.Status -eq 200 -and $c1.Data.id) "status=$($c1.Status)"
$c1Id = $c1.Data.id

Start-Sleep -Milliseconds 30
$c2 = CreateComment $hdrA $post1Id 'CM Comment: thanks, trying that next season.'
$comments = TryGetJson "$base/api/community/posts/$post1Id/comments?page=1&pageSize=20" $hdrA
$commentItems = AsArray $comments.items
$cIds = @($commentItems | ForEach-Object { $_.id })
Check '21. retrieve comments (paginated, oldest first)' `
    ($comments.totalCount -ge 2 -and $cIds.Count -ge 2 -and $cIds[0] -eq $c1Id) `
    "total=$($comments.totalCount) first=$($cIds[0])"

$commentsPost2 = TryGetJson "$base/api/community/posts/$($post2.Data.id)/comments?page=1&pageSize=20" $hdrA
$onWrongPost = @((AsArray $commentsPost2.items) | Where-Object { $_.id -eq $c1Id })
Check '22. comment appears under correct post only' (@($commentItems | Where-Object { $_.id -eq $c1Id }).Count -eq 1 -and $onWrongPost.Count -eq 0) ''

$r = CreateComment $hdrA $post1Id '   '
Check '23. whitespace comment rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$r = CreateComment $hdrA $post1Id ''
Check '24. empty comment rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$r = CreateComment $hdrA $post1Id ('y' * 1001)
Check '25. overlong comment (1001 chars) rejected -> 400' ($r.Status -eq 400) "status=$($r.Status)"

$c3 = CreateComment $hdrB $post1Id 'CM Comment: owned by user B, delete test.'
$wrongDel = ApiCall 'DELETE' "$base/api/community/comments/$($c3.Data.id)" $hdrA
Check '27. another user cannot delete comment -> 403' ($wrongDel.Status -eq 403) "status=$($wrongDel.Status)"

$ownDel = ApiCall 'DELETE' "$base/api/community/comments/$($c3.Data.id)" $hdrB
$afterDel = TryGetJson "$base/api/community/posts/$post1Id/comments?page=1&pageSize=20" $hdrB
$leftover = @((AsArray $afterDel.items) | Where-Object { $_.id -eq $c3.Data.id })
Check '26. owner can delete own comment (204, disappears from list)' ($ownDel.Status -eq 204 -and $leftover.Count -eq 0) "del=$($ownDel.Status)"

$unknownComment = ApiCall 'DELETE' "$base/api/community/comments/$randomComment" $hdrA
Check '28. unknown comment returns 404' ($unknownComment.Status -eq 404) "status=$($unknownComment.Status)"

# --- 29-31: Security -------------------------------------------------------------
Write-Host ''
Write-Host '--- Security (checks 29-31) ---'

$rawFeed = TryGetRaw "$base/api/community/posts?page=1&pageSize=50" $hdrA
$rawDetail = TryGetRaw "$base/api/community/posts/$post1Id" $hdrA
$rawComments = TryGetRaw "$base/api/community/posts/$post1Id/comments" $hdrA
$leakUserId = ($rawFeed -match 'userId') -or ($rawDetail -match 'userId') -or ($rawComments -match 'userId')
Check '29. no UserId leakage in feed/detail/comments' (-not $leakUserId) ''

$leakSecret = ($rawFeed -match 'password|token|apiKey|secret') -or ($rawDetail -match 'password|token|apiKey|secret') -or ($rawComments -match 'password|token|apiKey|secret')
$leakPii = ($rawFeed -match 'passwordHash|phoneNumber') -or ($rawDetail -match 'passwordHash|phoneNumber') -or ($rawComments -match 'passwordHash|phoneNumber')
Check '30. no password/token/key/PII leakage' (-not $leakSecret -and -not $leakPii) ''

$feedB = TryGetJson "$base/api/community/posts?page=1&pageSize=50" $hdrB
$itemsB = AsArray $feedB.items
$seenOwn = @($itemsB | Where-Object { $_.isOwnedByCurrentUser -eq $true })
$seenOther = @($itemsB | Where-Object { $_.id -eq $post1Id })
$crossOk = ($seenOther.Count -eq 1 -and $seenOther[0].isOwnedByCurrentUser -eq $false) -and `
           (@($seenOwn | Where-Object { $_.authorName }).Count -eq $seenOwn.Count) -and `
           ($foreign.Status -eq 403)
Check '31. cross-user isolation: visible but not deletable, ownership flags correct' $crossOk ''

# --- 32-35: Persistence ------------------------------------------------------------
Write-Host ''
Write-Host '--- Persistence (checks 32-35) ---'

$post4 = CreatePost $hdrA 'CM Post: persistence check across request cycles' $null
$detail4 = ApiCall 'GET' "$base/api/community/posts/$($post4.Data.id)" $hdrB
$feed4 = TryGetJson "$base/api/community/posts?page=1&pageSize=50" $hdrB
$found4 = @((AsArray $feed4.items) | Where-Object { $_.id -eq $post4.Data.id })
Check '32. post survives request cycle (visible to another user)' `
    ($detail4.Status -eq 200 -and $found4.Count -eq 1) "detail=$($detail4.Status) feed=$($found4.Count)"

$c4 = CreateComment $hdrB $post4.Data.id 'CM Comment: persistence check.'
$comments4 = TryGetJson "$base/api/community/posts/$($post4.Data.id)/comments" $hdrA
$foundC4 = @((AsArray $comments4.items) | Where-Object { $_.id -eq $c4.Data.id })
Check '33. comment survives request cycle' ($foundC4.Count -eq 1 -and $comments4.totalCount -eq 1) "total=$($comments4.totalCount)"

ApiCall 'DELETE' "$base/api/community/posts/$($post4.Data.id)" $hdrA | Out-Null
$feedAfter = TryGetJson "$base/api/community/posts?page=1&pageSize=50" $hdrA
$stillVisible = @((AsArray $feedAfter.items) | Where-Object { $_.id -eq $post4.Data.id })
$detailAfter = ApiCall 'GET' "$base/api/community/posts/$($post4.Data.id)" $hdrA
$commentsAfter = ApiCall 'GET' "$base/api/community/posts/$($post4.Data.id)/comments" $hdrA
Check '34. soft-deleted post disappears from feed, detail and comments' `
    ($stillVisible.Count -eq 0 -and $detailAfter.Status -eq 404 -and $commentsAfter.Status -eq 404) `
    "feed=$($stillVisible.Count) detail=$($detailAfter.Status) comments=$($commentsAfter.Status)"

$post5 = CreatePost $hdrA 'CM Post: comment soft-delete visibility check' $null
$c5 = CreateComment $hdrB $post5.Data.id 'CM Comment: will be soft-deleted.'
ApiCall 'DELETE' "$base/api/community/comments/$($c5.Data.id)" $hdrB | Out-Null
$comments5 = TryGetJson "$base/api/community/posts/$($post5.Data.id)/comments" $hdrA
$detail5 = ApiCall 'GET' "$base/api/community/posts/$($post5.Data.id)" $hdrA
Check '35. soft-deleted comment disappears from comments and comment count' `
    ((AsArray $comments5.items).Count -eq 0 -and $comments5.totalCount -eq 0 -and $detail5.Data.post.commentCount -eq 0) `
    "total=$($comments5.totalCount) count=$($detail5.Data.post.commentCount)"

# --- Cleanup -----------------------------------------------------------------------
$cleanedA2 = CleanupCommunity $hdrA
$cleanedB2 = CleanupCommunity $hdrB
Write-Host ''
Write-Host "Final cleanup: removed $cleanedA2 (user A) + $cleanedB2 (user B) fixture posts."

Write-Host ''
Write-Host '=================================================================='
Write-Host " RESULT: $pass passed, $fail failed (of $($pass + $fail) checks)"
Write-Host '=================================================================='
if ($fail -gt 0) { exit 1 } else { exit 0 }
