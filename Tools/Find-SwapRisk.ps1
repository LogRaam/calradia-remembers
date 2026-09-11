<#
.SYNOPSIS
    Reports every public member in CalradiaRemembers.Logic that takes a run of same-typed arguments.

.DESCRIPTION
    A silent argument swap is only possible INSIDE a run of consecutive parameters of the same type:
    anywhere else the compiler catches it. So the length of the longest such run is the whole of a
    signature's swap risk, and it is the number this script prints.

    On 2026-09-10 the count over CalradiaRemembers.Logic went from 99 public static methods with a run
    of three or more, plus 29 more once constructors were counted, to zero. Run this after adding a
    policy: a non-zero report is not a style complaint, it is a signature that will one day be called
    with two of its arguments the wrong way round, compile, run, and give a plausible wrong answer.

    The fix is almost never "be careful". It is one of three:
      - ask for the type that already exists (RomanticTraits, WarSides, TranscriptLine, the candidate
        record the caller was unpacking to make the call);
      - give the policy a named facts object, one field per fact, each with the sentence that says what
        it means and what goes wrong if it is false;
      - and if the carrier is itself a facts object, remember that its own positional CONSTRUCTOR undoes
        the point of it. Use { get; init; } and an object initializer.

.PARAMETER Threshold
    Report members whose longest run is at least this. Defaults to 3.

.EXAMPLE
    pwsh -File Tools/Find-SwapRisk.ps1
#>
[CmdletBinding()]
param([int] $Threshold = 3)

$ErrorActionPreference = 'Stop'
$logic = Join-Path $PSScriptRoot '..\CalradiaRemembers.Logic'

function Split-TopLevel([string] $text) {
    $parts = @(); $depth = 0; $current = ''
    foreach ($ch in $text.ToCharArray()) {
        if ('<([{'.Contains($ch)) { $depth++ }
        elseif ('>)]}'.Contains($ch)) { if ($depth -gt 0) { $depth-- } }
        elseif ($ch -eq ',' -and $depth -eq 0) { $parts += $current.Trim(); $current = ''; continue }
        $current += $ch
    }
    if ($current.Trim()) { $parts += $current.Trim() }
    return $parts
}

$patterns = @(
    'public\s+static\s+[\w<>,\?\[\]\.\s]+?\b(?<name>\w+)\s*\((?<params>[^;{)]*)\)\s*(?:=>|\{)',
    'public\s+(?!static|const|readonly|sealed|abstract|class|enum|struct|record|partial)(?:[\w<>,\?\[\]\.]+\s+)?(?<name>\w+)\s*\((?<params>[^;{)]*)\)\s*(?:=>|\{|:)'
)

$findings = @()
foreach ($file in Get-ChildItem -Path $logic -Recurse -Filter *.cs |
                  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
    $source = [regex]::Replace((Get-Content -Raw -LiteralPath $file.FullName), '///.*', '')
    foreach ($pattern in $patterns) {
        foreach ($m in [regex]::Matches($source, $pattern, 'Singleline')) {
            $raw = $m.Groups['params'].Value
            if (-not $raw.Trim()) { continue }
            $parts = Split-TopLevel $raw
            $types = foreach ($p in $parts) {
                $head = ($p -split '=')[0].Trim()
                $tokens = $head -split '\s+'
                if ($tokens.Count -ge 2) { ($tokens[0..($tokens.Count - 2)] -join ' ') } else { $head }
            }
            $run = 1; $best = 1
            for ($i = 1; $i -lt $types.Count; $i++) {
                if ($types[$i] -eq $types[$i - 1]) { $run++ } else { $run = 1 }
                if ($run -gt $best) { $best = $run }
            }
            if ($best -ge $Threshold) {
                $findings += [pscustomobject]@{
                    Run    = $best
                    Params = $parts.Count
                    File   = [IO.Path]::GetRelativePath($logic, $file.FullName)
                    Member = $m.Groups['name'].Value
                }
            }
        }
    }
}

$findings = $findings | Sort-Object -Property File, Member -Unique | Sort-Object -Property Run -Descending

foreach ($f in $findings) {
    '{0,-6} n={1,-3} {2,-46} {3}' -f "run=$($f.Run)", $f.Params, $f.File, $f.Member
}

if ($findings.Count -eq 0) {
    "No public member in CalradiaRemembers.Logic takes a run of $Threshold or more same-typed arguments."
} else {
    "$($findings.Count) member(s) with a same-typed run of $Threshold or more."
}
