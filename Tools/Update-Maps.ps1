<#
.SYNOPSIS
  Regenerates every map in docs/maps (and RULES.md) from the code, then shows you exactly what moved.

.DESCRIPTION
  Gabriel, 21/09/2026: "Quand il y a un mismatch entre le code et les maps, je peux voir les edges qui me montre les
  changements structuraux. Comment je fais pour approuver ces changements pour mettre les tests de maps au vert?"

  This is that approval step. A red Maps test never means the code is wrong: it means the code moved and the committed
  map did not. Approving is two acts, and the script does the first and shows you the second:

    1. regenerate  -> the maps are rewritten from the source as it stands now;
    2. READ THE DIFF -> that diff is the architectural summary of your change, and it is the thing worth reading
       before the code diff. Then commit the maps WITH the change that moved them.

  Why a script rather than a command line: the env var the test reads cannot be set inline in PowerShell the way it can
  in bash, and NCrunch (which Gabriel runs) compiles from a workspace copy that has no source tree, so the Maps tests
  are Ignored there and cannot regenerate anything. This runs the real `dotnet test`.

.PARAMETER AllowSaveKeyRemoval
  The one change a regeneration will NOT absorb on its own. A save key that disappears means every campaign in
  progress silently loses that data on its next load, so the suite refuses it until you say you meant it. Pass this
  only when you did, and say why in the commit message.

.EXAMPLE
  .\Tools\Update-Maps.ps1
  Regenerates, then prints which maps moved and the full diff of the small ones.

.EXAMPLE
  .\Tools\Update-Maps.ps1 -AllowSaveKeyRemoval
  The same, for a release where a save key is deliberately retired.
#>
[CmdletBinding()]
param(
   [switch] $AllowSaveKeyRemoval
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$tests = Join-Path $repo "CalradiaRemembers.Tests"

# A diff this short is printed in full, whatever map it is in: the whole value of this step is READING what moved, and
# a two-line change is exactly the kind that gets waved through when it is hidden behind a line count. Longer ones are
# summarised and left to `git diff`, since nobody reads four hundred lines in a terminal either.
$printedInFull = 40

Write-Host "Regenerating the maps from the code..." -ForegroundColor Cyan

$env:CR_UPDATE_MAPS = "1"
if ($AllowSaveKeyRemoval) { $env:CR_ALLOW_SAVE_KEY_REMOVAL = "1" }

try {
   $output = & dotnet test $tests -p:NCrunch=1 --filter "FullyQualifiedName~Maps" 2>&1
   $failed = $LASTEXITCODE -ne 0
}
finally {
   Remove-Item Env:CR_UPDATE_MAPS -ErrorAction SilentlyContinue
   Remove-Item Env:CR_ALLOW_SAVE_KEY_REMOVAL -ErrorAction SilentlyContinue
}

if ($failed) {
   $output | Select-String -Pattern "no longer written|error|Failed" | ForEach-Object { Write-Host $_ -ForegroundColor Red }
   Write-Host ""
   Write-Host "The regeneration was REFUSED, so nothing was rewritten." -ForegroundColor Red
   Write-Host "If a save key is listed above, a campaign in progress would lose that data. Re-run with" -ForegroundColor Red
   Write-Host "-AllowSaveKeyRemoval only if you truly mean to retire it, and say why in the commit." -ForegroundColor Red

   exit 1
}

$changed = & git -C $repo -c core.safecrlf=false diff --numstat -- "docs/maps" "RULES.md" |
   ForEach-Object {
      $parts = $_ -split "\t"
      [pscustomobject] @{ Added = [int] $parts[0]; Removed = [int] $parts[1]; Path = $parts[2] }
   }

if (-not $changed) {
   Write-Host ""
   Write-Host "No map moved: the dimensions they watch are unchanged. Nothing to approve." -ForegroundColor Green

   exit 0
}

Write-Host ""
Write-Host "These maps moved. THIS IS THE SUMMARY OF YOUR CHANGE, worth reading before the code diff:" -ForegroundColor Yellow
Write-Host ""

foreach ($file in $changed) {
   Write-Host ("  {0,5} added, {1,5} removed   {2}" -f $file.Added, $file.Removed, $file.Path)
}

foreach ($file in $changed | Where-Object { $_.Added + $_.Removed -le $printedInFull }) {
   Write-Host ""
   Write-Host "--- $($file.Path)" -ForegroundColor Cyan
   & git -C $repo -c core.safecrlf=false --no-pager diff --unified=0 -- $file.Path |
      Select-String -Pattern "^[+-][^+-]" | ForEach-Object { Write-Host "   $_" }
}

Write-Host ""
Write-Host "To approve: read the above, then commit the maps WITH the change that moved them." -ForegroundColor Green
Write-Host ("For anything longer than {0} lines, shown as counts only: git diff -- docs/maps/<the map>" -f $printedInFull) -ForegroundColor DarkGray
