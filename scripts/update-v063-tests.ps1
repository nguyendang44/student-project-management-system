# Migrates only the LecturerSupervision tests affected by the new two-stage approval.
# Preserves your TopicIntegrationTests and all unrelated local tests.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'backend\StudentProjects.IntegrationTests\LecturerSupervisionIntegrationTests.cs'
$template = Join-Path $PSScriptRoot 'LecturerSupervisionIntegrationTests.v063.template.txt'
$extras = Join-Path $PSScriptRoot 'v063-extra-tests.txt'
if (!(Test-Path $dest) -or !(Test-Path $template) -or !(Test-Path $extras)) { throw 'v0.6.3 test upgrade files missing' }
$data = [System.IO.File]::ReadAllText($dest)
$src = [System.IO.File]::ReadAllText($template)
function MethodPattern([string]$name) {
    return '(?ms)^    \[Fact\]\r?\n    public async Task ' + [regex]::Escape($name) + '\(\)\r?\n    \{.*?^    \}\r?\n'
}
$names = @(
  'CapacityIsPeriodSpecificAndCannotDropBelowAssigned',
  'AcceptCreatesProjectAndCancelsOtherSupervisorRequests',
  'CapacityFullPreventsFurtherSubmissionAndAcceptance',
  'StudentsAndLecturersOnlySeeTheirOwnRequestsAndProjects',
  'JointProposal_Accept_GrantsOwnershipAndCreatesProject',
  'JointProposal_Revision_StudentResubmitsToSameLecturer',
  'JointApplication_PublicTopicRevision_DoesNotModifyOtherStudentsTopic',
  'JointApplication_TwoStudentsCompeteForTopic_OnlyOneMayWin',
  'AcceptedProject_CannotCancelRequest_ButStudentCanWithdrawFreshProject'
)
foreach ($name in $names) {
    $newName = if ($name -eq 'CapacityIsPeriodSpecificAndCannotDropBelowAssigned') { 'CapacityIsGlobalAcrossPeriodsAndCannotDropBelowAssigned' } else { $name }
    $newMatch = [regex]::Match($src, (MethodPattern $newName))
    if (!$newMatch.Success) { throw "Missing template method $newName" }
    $oldMatch = [regex]::Match($data, (MethodPattern $name))
    if (!$oldMatch.Success -and $name -ne $newName -and $data.Contains("public async Task $newName()")) { continue }
    if (!$oldMatch.Success) { throw "Expected original test method $name not found; no file written" }
    $data = $data.Substring(0, $oldMatch.Index) + $newMatch.Value + $data.Substring($oldMatch.Index + $oldMatch.Length)
}
if (!$data.Contains('private static async Task<Guid> Choose(HttpClient student, Guid requestId)')) {
    $pos = $data.LastIndexOf('}')
    if ($pos -le 0) { throw 'Could not find class closing brace' }
    $data = $data.Substring(0, $pos).TrimEnd() + "`r`n`r`n" + [System.IO.File]::ReadAllText($extras).TrimEnd() + "`r`n" + $data.Substring($pos)
}
$backup = "$dest.before-v063.bak"
if (!(Test-Path $backup)) { Copy-Item -LiteralPath $dest -Destination $backup }
[System.IO.File]::WriteAllText($dest, $data, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'OK: tests updated in place; all other files and test methods preserved.'
