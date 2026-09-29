param([ValidateSet('preflight','reserve','start','complete')][string]$Transition, [string]$ChildId, [string]$Verdict)
$ErrorActionPreference = 'Stop'
$taskEvidence = $PSScriptRoot
$taskLockPath = Join-Path $taskEvidence 'review.lock'
$taskLock = [IO.File]::Open($taskLockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$taskAcquired = [DateTimeOffset]::UtcNow.ToString('o')
try {
    $taskJournalPath = Join-Path $taskEvidence 'attempts.json'
    $taskJournal = Get-Content $taskJournalPath -Raw | ConvertFrom-Json
    if ($taskJournal.assuranceUnitId -ne 'chapanakit-care/reports/reference-completion' -or $taskJournal.reopenGeneration -ne 0 -or $taskJournal.maxReviewCalls -ne 3) { throw 'Identity or budget mismatch.' }
    if ($Transition -eq 'preflight') {
        @{ primitive='System.IO.File.Open(FileMode.CreateNew,FileAccess.Write,FileShare.None)'; path=$taskLockPath; acquired=$taskAcquired; stateRead='unit generation budget attempts'; result='pass'; release='finally dispose then Remove-Item exact lock path' } | ConvertTo-Json | Set-Content (Join-Path $taskEvidence 'coordination-preflight.json') -Encoding utf8
    } elseif ($Transition -eq 'reserve') {
        $taskReady = Get-Content (Join-Path $taskEvidence 'readiness.json') -Raw | ConvertFrom-Json
        $taskProof = Get-Content (Join-Path $taskEvidence 'readiness-proof.json') -Raw | ConvertFrom-Json
        if ($taskProof.readinessGate -ne 'pass') { throw 'Missing passing proof.' }
        $taskIntent = Get-Content (Join-Path $taskEvidence 'intended-call-1.json') -Raw | ConvertFrom-Json
        if ($taskReady.unitStatus -ne 'open' -or !$taskReady.reviewReady -or $taskJournal.activeReviewReservation -or $taskJournal.reviewCallsUsed -ge $taskJournal.maxReviewCalls) { throw 'Reservation is not allowed.' }
        if ($taskReady.assuranceUnitId -ne $taskJournal.assuranceUnitId -or $taskReady.reopenGeneration -ne $taskJournal.reopenGeneration -or $taskReady.reviewCallsUsed -ne $taskJournal.reviewCallsUsed -or $taskReady.assurancePacketId -ne $taskIntent.assurancePacketId -or $taskReady.frozenCandidateId -ne $taskIntent.frozenCandidateId) { throw 'Ready identity differs.' }
        foreach ($taskPair in @(@('ledger.md','ledgerSha256'),@('attempts.json','attemptsSha256'),@('review-packet.md','packetSha256'),@('candidate-manifest.json','candidateManifestSha256'))) {
            $taskHash = (Get-FileHash (Join-Path $taskEvidence $taskPair[0]) -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($taskHash -ne $taskReady.($taskPair[1])) { throw ('Stale readiness: ' + $taskPair[0]) }
        }
        $taskPython = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
        & $taskPython (Join-Path $taskEvidence 'freeze_candidate.py') --check
        if ($LASTEXITCODE -ne 0) { throw 'Candidate drift.' }
        # Re-run the exact machine validator while holding the exclusive lock.
        & $taskPython (Join-Path $env:USERPROFILE '.agents/skills/solweaver/scripts/validate_final_strict_packet.py') --readiness (Join-Path $taskEvidence 'readiness.json') --ledger (Join-Path $taskEvidence 'ledger.md') --attempts $taskJournalPath --packet (Join-Path $taskEvidence 'review-packet.md') --candidate-manifest (Join-Path $taskEvidence 'candidate-manifest.json')
        if ($LASTEXITCODE -ne 0) { throw 'Machine gate failed.' }
        $taskAttempt = [ordered]@{ reviewAttemptId=$taskIntent.reviewAttemptId; call=1; state='reserved'; frozenCandidateId=$taskIntent.frozenCandidateId; assurancePacketId=$taskIntent.assurancePacketId; reservedAt=$taskAcquired }
        $taskJournal.activeReviewReservation = $taskIntent.reviewAttemptId
        $taskJournal.attempts = @($taskJournal.attempts) + [pscustomobject]$taskAttempt
    } elseif ($Transition -eq 'start') {
        if (!$ChildId -or !$taskJournal.activeReviewReservation) { throw 'No child/reservation.' }
        $taskAttempt = $taskJournal.attempts | Where-Object reviewAttemptId -eq $taskJournal.activeReviewReservation
        if ($taskAttempt.state -ne 'reserved') { throw 'Attempt was already started.' }
        $taskAttempt.state = 'started'
        $taskAttempt | Add-Member NoteProperty childId $ChildId
        $taskAttempt | Add-Member NoteProperty startedAt $taskAcquired
        $taskJournal.reviewCallsUsed++
    } elseif ($Transition -eq 'complete') {
        $taskRuntime = Get-Content (Join-Path $taskEvidence 'reviewer-runtime.json') -Raw | ConvertFrom-Json
        if ($Verdict -ne 'ship' -or $taskRuntime.runtimeGate -ne 'pass') { throw 'This completion command only accepts a verified ship.' }
        $taskAttempt = $taskJournal.attempts | Where-Object reviewAttemptId -eq $taskJournal.activeReviewReservation
        if ($taskAttempt.state -ne 'started' -or $taskAttempt.childId -ne $taskRuntime.childThreadId) { throw 'Runtime/attempt mismatch.' }
        $taskAttempt.state = 'completed'
        $taskAttempt | Add-Member NoteProperty runtimeGate 'pass'
        $taskAttempt | Add-Member NoteProperty verdict 'ship'
        $taskAttempt | Add-Member NoteProperty outcome 'accepted'
        $taskAttempt | Add-Member NoteProperty completedAt $taskAcquired
        $taskJournal.activeReviewReservation = $null
        $taskJournal | Add-Member NoteProperty unitStatus 'ship' -Force
        $taskLedgerPath = Join-Path $taskEvidence 'ledger.md'
        $taskLedger = [IO.File]::ReadAllText($taskLedgerPath).Replace('- UNIT_STATUS: open','- UNIT_STATUS: ship').Replace('MAX_REVIEW_CALLS: 3; REVIEW_CALLS_USED: 0.', 'MAX_REVIEW_CALLS: 3; REVIEW_CALLS_USED: 1.')
        [IO.File]::WriteAllText($taskLedgerPath, $taskLedger)
    }
    if ($Transition -ne 'preflight') {
        $taskJournal | ConvertTo-Json -Depth 12 | Set-Content $taskJournalPath -Encoding utf8
        $taskTransitionName = if ($Transition -eq 'reserve') { 'reservation' } else { $Transition }
        @{ transition=$Transition; acquired=$taskAcquired; releasedBy='finally dispose/remove exact lock'; primitive='FileMode.CreateNew'; journalSha256=(Get-FileHash $taskJournalPath -Algorithm SHA256).Hash.ToLowerInvariant(); attemptId=$taskJournal.attempts[-1].reviewAttemptId } | ConvertTo-Json | Set-Content (Join-Path $taskEvidence ($taskTransitionName + '-call-1.json')) -Encoding utf8
    }
    Write-Output ('Coordination transition passed: ' + $Transition)
} finally {
    $taskLock.Dispose()
    Remove-Item -LiteralPath $taskLockPath
}
