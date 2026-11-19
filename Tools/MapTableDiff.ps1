# Code written by Gabriel Mailhot, 19/11/2026.
# Reads a map's diff at the level of its TABLE ROWS, for Update-Maps.ps1. Two frictions reported 19/11/2026 while using
# the maps: placement.md moved "| `NpcMemoryService.Core.Prompts` | 48 | 3 | 14 | loose |" to "... | 13 | ..." and the
# reader could not tell which column it was (the nearest header seen belonged to another table), so the commit guessed;
# and prompt-sections.md showed "122 added, 120 removed" for two new rows, the rest being rank renumbering. Gabriel:
# "ajuste la maps pour que ca fonctionne mieux".
#
# So a changed row is paired with its old self by its first non-number cell, and printed as "column: old -> new" under
# its table's own section heading; a row whose only change is its rank is counted, not printed; new and gone rows are
# named. Lines outside tables are left to the raw diff.

function Split-MapRow([string] $line)
{
   $inner = $line.Trim()
   if ($inner.StartsWith("|")) { $inner = $inner.Substring(1) }
   if ($inner.EndsWith("|")) { $inner = $inner.Substring(0, $inner.Length - 1) }

   return , @($inner -split "\|" | ForEach-Object { $_.Trim() })
}

# The cell that names a row: the first that is not a number or a count ("29000+", "12%"), with a trailing count dropped
# ("`Hero.Father` (32)" names the same row as "`Hero.Father` (31)": engine-facts.md, 19/11/2026).
function Get-MapRowKey([string[]] $cells)
{
   foreach ($c in $cells) { if ($c -ne "" -and $c -notmatch "^[\d.,+%\-]+$") { return ($c -replace "\s*\(\d+\)$", "") } }

   return ($cells -join "|")
}

# The header cells and the section heading of the table holding the row at $index of $lines.
function Get-MapTableContext([string[]] $lines, [int] $index)
{
   $i = $index
   while ($i -gt 0 -and $lines[$i - 1].TrimStart().StartsWith("|")) { $i-- }
   $header = if ($i + 1 -lt $lines.Count -and $lines[$i + 1] -match "^\s*\|[\s\-:|]+\|\s*$") { Split-MapRow $lines[$i] } else { $null }
   $heading = ""
   for ($j = $i - 1; $j -ge 0; $j--) { if ($lines[$j].StartsWith("#")) { $heading = $lines[$j].TrimStart("#").Trim(); break } }

   return [pscustomobject] @{ Header = $header; Heading = $heading }
}

# Prints what moved in the tables of one map. $diffLines: `git diff --unified=0` of the map; $newLines: the map as it
# now stands. Returns the number of diff lines that were NOT table rows (left to the raw diff).
function Show-MapTableChanges([string[]] $diffLines, [string[]] $newLines)
{
   $removed = [ordered] @{}
   $added = [ordered] @{}
   $other = 0
   # A table's header line is not a row (constants.md repeats "| Constant | ..." under every type, 19/11/2026).
   $headers = New-Object System.Collections.Generic.HashSet[string]
   for ($h = 0; $h + 1 -lt $newLines.Count; $h++)
   {
      if ($newLines[$h].TrimStart().StartsWith("|") -and $newLines[$h + 1] -match "^\s*\|[\s\-:|]+\|\s*$") { [void] $headers.Add($newLines[$h]) }
   }
   foreach ($d in $diffLines)
   {
      if ($d.StartsWith("+++") -or $d.StartsWith("---")) { continue }
      if ($d -match "^([+-])\s*\|") {
         $sign = $d.Substring(0, 1)
         $text = $d.Substring(1)
         if ($text -match "^\s*\|[\s\-:|]+\|\s*$" -or $headers.Contains($text)) { continue } # a header rule or a header
         $cells = Split-MapRow $text
         $key = Get-MapRowKey $cells
         $bucket = if ($sign -eq "+") { $added } else { $removed }
         if (-not $bucket.Contains($key)) { $bucket[$key] = [pscustomobject] @{ Cells = $cells; Text = $text } }
      }
      elseif ($d -match "^[+-]") { $other++ }
   }

   $renumbered = 0
   $byHeading = [ordered] @{}
   function Add-Line([string] $heading, [string] $text)
   {
      if (-not $byHeading.Contains($heading)) { $byHeading[$heading] = New-Object System.Collections.Generic.List[string] }
      $byHeading[$heading].Add($text)
   }
   function Context-Of([string] $rowText)
   {
      $at = [array]::IndexOf($newLines, $rowText)
      if ($at -lt 0) { return [pscustomobject] @{ Header = $null; Heading = "" } }

      return Get-MapTableContext $newLines $at
   }

   foreach ($key in $added.Keys)
   {
      $new = $added[$key]
      $ctx = Context-Of $new.Text
      if (-not $removed.Contains($key)) { Add-Line $ctx.Heading "  new row: $key"; continue }

      $old = $removed[$key]
      $changes = @()
      $onlyRank = $true
      $n = [Math]::Max($old.Cells.Count, $new.Cells.Count)
      for ($c = 0; $c -lt $n; $c++)
      {
         $o = if ($c -lt $old.Cells.Count) { $old.Cells[$c] } else { "" }
         $w = if ($c -lt $new.Cells.Count) { $new.Cells[$c] } else { "" }
         if ($o -ceq $w) { continue }
         if (-not ($c -eq 0 -and $o -match "^\d+$" -and $w -match "^\d+$")) { $onlyRank = $false }
         $name = if ($ctx.Header -and $c -lt $ctx.Header.Count -and $ctx.Header[$c] -ne "") { $ctx.Header[$c] } else { "column $($c + 1)" }
         # A list cell (names joined by ", ") shows what joined and what left, not two truncated lists (engine-facts.md).
         if ($o.Contains(", ") -or $w.Contains(", "))
         {
            $was = @($o -split ",\s*" | Where-Object { $_ -ne "" })
            $now = @($w -split ",\s*" | Where-Object { $_ -ne "" })
            $in = @($now | Where-Object { $was -notcontains $_ } | ForEach-Object { "+$_" })
            $out = @($was | Where-Object { $now -notcontains $_ } | ForEach-Object { "-$_" })
            if ($in.Count + $out.Count -gt 0) { $changes += "${name}: " + (($in + $out) -join ", "); continue }
         }
         $ov = if ($o.Length -gt 60) { $o.Substring(0, 57) + "..." } else { $o }
         $wv = if ($w.Length -gt 60) { $w.Substring(0, 57) + "..." } else { $w }
         $changes += "${name}: $ov -> $wv"
      }
      if ($changes.Count -eq 0) { continue }
      if ($onlyRank) { $renumbered++; continue }
      Add-Line $ctx.Heading ("  {0}: {1}" -f $key, ($changes -join "; "))
   }
   foreach ($key in $removed.Keys)
   {
      if (-not $added.Contains($key)) { Add-Line "" "  row gone: $key" }
   }

   foreach ($heading in $byHeading.Keys)
   {
      if ($heading -ne "") { Write-Host "   [$heading]" -ForegroundColor DarkCyan }
      foreach ($l in $byHeading[$heading]) { Write-Host "   $l" }
   }
   if ($renumbered -gt 0) { Write-Host ("   ({0} rows only renumbered, not shown)" -f $renumbered) -ForegroundColor DarkGray }

   return $other
}
