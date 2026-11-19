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

.PARAMETER AllowApiRemoval
  The second change a regeneration will NOT absorb (23/09/2026). A public member of the modder API that SHIPPED and
  is no longer there breaks every bridge built on it, so the suite refuses it until you say you meant it. Members
  added and withdrawn between two releases are not affected. Pass this only when a released member truly goes, name
  it under "For modders" in the release notes, and say why in the commit.

.PARAMETER RecordReleasedApi
  At a RELEASE CUT only: records the modder API as it is being shipped (docs/maps/modder-api-released.md, with the
  version from SubModule.xml) as the new baseline the guard above protects. A plain regeneration never touches it.

.EXAMPLE
  .\Tools\Update-Maps.ps1
  Regenerates, then prints which maps moved and the full diff of the small ones.

.EXAMPLE
  .\Tools\Update-Maps.ps1 -AllowSaveKeyRemoval
  The same, for a release where a save key is deliberately retired.

.EXAMPLE
  .\Tools\Update-Maps.ps1 -RecordReleasedApi
  At a release cut, after bumping SubModule.xml: regenerates and records the shipped modder API.
#>
[CmdletBinding()]
param(
   [switch] $AllowSaveKeyRemoval,
   [switch] $AllowApiRemoval,
   [switch] $RecordReleasedApi
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "MapTableDiff.ps1")
$tests = Join-Path $repo "CalradiaRemembers.Tests"

# A diff this short is printed in full, whatever map it is in: the whole value of this step is READING what moved, and
# a two-line change is exactly the kind that gets waved through when it is hidden behind a line count. Longer ones are
# summarised and left to `git diff`, since nobody reads four hundred lines in a terminal either.
$printedInFull = 40

Write-Host "Regenerating the maps from the code..." -ForegroundColor Cyan

$env:CR_UPDATE_MAPS = "1"
if ($AllowSaveKeyRemoval) { $env:CR_ALLOW_SAVE_KEY_REMOVAL = "1" }
if ($AllowApiRemoval) { $env:CR_ALLOW_API_REMOVAL = "1" }
if ($RecordReleasedApi) { $env:CR_RECORD_RELEASED_API = "1" }

try {
   $output = & dotnet test $tests -p:NCrunch=1 --filter "FullyQualifiedName~Maps" 2>&1
   $failed = $LASTEXITCODE -ne 0
}
finally {
   Remove-Item Env:CR_UPDATE_MAPS -ErrorAction SilentlyContinue
   Remove-Item Env:CR_ALLOW_SAVE_KEY_REMOVAL -ErrorAction SilentlyContinue
   Remove-Item Env:CR_ALLOW_API_REMOVAL -ErrorAction SilentlyContinue
   Remove-Item Env:CR_RECORD_RELEASED_API -ErrorAction SilentlyContinue
}

if ($failed) {
   # Name the real cause (19/11/2026: a namespace-pair ratchet refused, and the message spoke only of save keys and the
   # modder API, so the cause was found by re-running the suite): the failing tests, their message, then the advice
   # that fits what refused.
   $text = $output -join "`n"
   $output | Select-String -Pattern "^\s+Failed \w|Error Message:|Expected |because |no longer written|no longer there|error CS" |
      Select-Object -First 30 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
   Write-Host ""
   Write-Host "The regeneration was REFUSED, so nothing was rewritten." -ForegroundColor Red
   $known = $false
   if ($text -match "no longer written") {
      $known = $true
      Write-Host "A save key is gone: a campaign in progress would lose that data. Re-run with" -ForegroundColor Red
      Write-Host "-AllowSaveKeyRemoval only if you truly mean to retire it, and say why in the commit." -ForegroundColor Red
   }
   if ($text -match "no longer there") {
      $known = $true
      Write-Host "A shipped modder API member is gone: a bridge already shipped would break. Re-run with" -ForegroundColor Red
      Write-Host "-AllowApiRemoval only if it truly goes, and name it under 'For modders' in the release notes." -ForegroundColor Red
   }
   if ($text -match "error CS\d+") {
      $known = $true
      Write-Host "The code does not compile: fix the build first." -ForegroundColor Red
   }
   if (-not $known) {
      Write-Host "A guard or ratchet among the Maps tests refused the change (its message is above). It is not" -ForegroundColor Red
      Write-Host "absorbed by a regeneration: fix the code it names, never widen the ratchet to pass." -ForegroundColor Red
   }

   exit 1
}

$changed = & git -C $repo -c core.safecrlf=false diff --numstat -- "docs/maps" "RULES.md" "docs/design/quest-editor/quest.schema.json" "CalradiaRemembers/Extension/TaleEditor.html" |
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

# Every map, whatever its size: table rows as "column: old -> new" under their table's heading, renumbering counted,
# then the other changed lines in full when the map moved little (MapTableDiff.ps1).
foreach ($file in $changed) {
   Write-Host ""
   Write-Host "--- $($file.Path)" -ForegroundColor Cyan
   $diff = @(& git -C $repo -c core.safecrlf=false --no-pager diff --unified=0 -- $file.Path)
   $now = @(Get-Content -LiteralPath (Join-Path $repo $file.Path) -Encoding UTF8)
   $other = Show-MapTableChanges $diff $now
   if ($other -eq 0) { continue }
   if ($file.Added + $file.Removed -le $printedInFull) {
      $diff | Where-Object { $_ -match "^[+-][^+-]" -and $_ -notmatch "^[+-]\s*\|" } | ForEach-Object { Write-Host "   $_" }
   }
   else {
      Write-Host ("   {0} other lines moved: git diff -- {1}" -f $other, $file.Path) -ForegroundColor DarkGray
   }
}

Write-Host ""
Write-Host "To approve: read the above, then commit the maps WITH the change that moved them." -ForegroundColor Green
Write-Host ("Lines outside tables are printed in full up to {0} lines a map; beyond, git diff -- docs/maps/<the map>" -f $printedInFull) -ForegroundColor DarkGray
