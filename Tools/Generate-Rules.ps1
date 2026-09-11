<#
.SYNOPSIS
  Writes RULES.md: everything the mod promises to do, as its own tests state it.

.DESCRIPTION
  Gabriel, 2026-09-10: "en bonus, je peux mieux comprendre la logique implementee car les tests expriment des
  regles du mod."

  That is only true if the rules can be READ. They exist, but scattered across 414 test files, so answering "what
  are the rules about duels" meant grepping. This turns the suite into the document it already almost was.

  Nothing here is authored. Every line is lifted from a test name and a file header, so the specification cannot
  drift from the behaviour it describes: the same reason cr.help is read from the command registry rather than
  from a list somebody maintains.

  A test name is only a rule if it was written as one. GIVEN_..._WHEN_..._THEN_... names a situation and an
  outcome; a name like THEN_it_is_returned names a return value and says nothing to a reader. Those still appear,
  under their fixture, because a gap in the document should be visible rather than hidden.

.PARAMETER Filter
  Print only fixtures whose path or name contains this text, to the console, instead of writing the file.
  Example: .\Tools\Generate-Rules.ps1 -Filter duel

.EXAMPLE
  .\Tools\Generate-Rules.ps1
  Rewrites RULES.md at the repository root.

.EXAMPLE
  .\Tools\Generate-Rules.ps1 -Filter escort
  Prints the escort rules and writes nothing.
#>
[CmdletBinding()]
param(
   [string] $Filter = ""
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repo "CalradiaRemembers.Tests"
$outputPath = Join-Path $repo "RULES.md"

if (-not (Test-Path $testRoot)) { throw "Test project not found at $testRoot" }

# ── A test name becomes a sentence ────────────────────────────────────────────────────────────────────────────
# GIVEN_a_lord_with_nothing_in_his_way_WHEN_asked_THEN_he_will_ride_with_the_player
#   -> GIVEN a lord with nothing in his way, WHEN asked, THEN he will ride with the player
function ConvertTo-Sentence([string] $name) {
   $text = $name -replace "_", " "
   $text = $text -replace "\bWHEN\b", ", WHEN"
   $text = $text -replace "\bTHEN\b", ", THEN"
   $text = $text -replace "\s+", " "
   $text = $text -replace "\s+,", ","

   return $text.Trim()
}

# ── The one line of a file header worth carrying: why the fixture exists ──────────────────────────────────────
function Get-FixturePurpose([string[]] $lines) {
   $header = @()
   foreach ($line in $lines) {
      if ($line -match "^\s*//") { $header += ($line -replace "^\s*//\s?", "") } else { break }
   }
   if ($header.Count -eq 0) { return "" }

   # Prefer the file's own "What this pins" sentence; it is written for exactly this purpose.
   for ($i = 0; $i -lt $header.Count; $i++) {
      if ($header[$i] -match "What this pins:") {
         $sentence = ($header[$i] -replace ".*What this pins:\s*", "")
         $j = $i + 1
         while ($j -lt $header.Count -and $header[$j].Trim() -ne "" -and $sentence -notmatch "\.\s*$") {
            $sentence = "$sentence $($header[$j].Trim())"
            $j++
         }
         return $sentence.Trim()
      }
   }

   # Otherwise the first real SENTENCE after the authorship line, carried across the wrapped lines it was
   # written on, so the summary never stops mid-clause.
   $sentence = ""
   foreach ($line in $header) {
      $t = $line.Trim()
      if ($sentence -eq "" -and ($t -eq "" -or $t -match "^Code written by")) { continue }
      if ($t -eq "") { break }
      $sentence = if ($sentence -eq "") { $t } else { "$sentence $t" }
      if ($sentence -match "\.\s*$") { break }
   }
   if ($sentence) { return $sentence.Trim() }

   return ""
}

$files = Get-ChildItem -Path $testRoot -Filter *.cs -Recurse |
   Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
   Sort-Object FullName

$sections = [ordered] @{}
$ruleCount = 0
$unnamedCount = 0

foreach ($file in $files) {
   $relative = $file.FullName.Substring($testRoot.Length).TrimStart("\", "/") -replace "\\", "/"
   if ($Filter -and $relative -notmatch [regex]::Escape($Filter)) { continue }

   $lines = Get-Content -LiteralPath $file.FullName
   $names = @()
   foreach ($line in $lines) {
      if ($line -match "public\s+void\s+([A-Za-z_0-9]+)\s*\(") { $names += $Matches[1] }
   }
   if ($names.Count -eq 0) { continue }

   $group = if ($relative -match "/") { ($relative -split "/")[0] } else { "General" }
   if (-not $sections.Contains($group)) { $sections[$group] = @() }

   $rules = @()
   foreach ($name in $names) {
      if ($name -like "GIVEN_*") { $rules += ConvertTo-Sentence $name; $ruleCount++ }
      else { $rules += "(not stated as a rule) " + (ConvertTo-Sentence $name); $unnamedCount++ }
   }

   $sections[$group] += [pscustomobject] @{
      Path    = $relative
      Purpose = Get-FixturePurpose $lines
      Rules   = $rules
   }
}

# ── Render ────────────────────────────────────────────────────────────────────────────────────────────────────
$out = [System.Text.StringBuilder]::new()

if (-not $Filter) {
   [void] $out.AppendLine("# Calradia Remembers: the rules, as its own tests state them")
   [void] $out.AppendLine()
   [void] $out.AppendLine("GENERATED by ``Tools/Generate-Rules.ps1``. Do not edit: every line is lifted from a test")
   [void] $out.AppendLine("name and a test file's header, so this cannot drift from the behaviour it describes.")
   [void] $out.AppendLine("Regenerate it whenever the suite changes.")
   [void] $out.AppendLine()
   [void] $out.AppendLine("$ruleCount rules across $($files.Count) fixtures. A line marked ``(not stated as a rule)`` is a")
   [void] $out.AppendLine("test whose name describes a return value or a branch rather than something anyone would")
   [void] $out.AppendLine("notice; those are shown rather than hidden, because a gap in a specification should be")
   [void] $out.AppendLine("visible. There are $unnamedCount of them. See CLAUDE.md, `"What a test is for`".")
   [void] $out.AppendLine()
}

foreach ($group in $sections.Keys) {
   [void] $out.AppendLine("## $group")
   [void] $out.AppendLine()

   foreach ($fixture in $sections[$group]) {
      [void] $out.AppendLine("### $($fixture.Path)")
      [void] $out.AppendLine()
      if ($fixture.Purpose) { [void] $out.AppendLine("_$($fixture.Purpose)_"); [void] $out.AppendLine() }
      foreach ($rule in $fixture.Rules) { [void] $out.AppendLine("- $rule") }
      [void] $out.AppendLine()
   }
}

if ($Filter) {
   Write-Output $out.ToString()
} else {
   Set-Content -LiteralPath $outputPath -Value $out.ToString() -Encoding UTF8
   Write-Output "RULES.md written: $ruleCount rules, $($files.Count) fixtures, $unnamedCount not stated as rules."
}
