# example_check.ps1 - EXAMPLE oracle for the draft chain. The deterministic check that decides whether
# a generated artifact is acceptable: read it on stdin, exit 0 to ACCEPT (-> on_success) or non-zero
# to REJECT (-> on_failure), printing the reason so the chain can feed it to the repair step.
#
# This placeholder accepts any non-empty artifact that has no leftover TODO marker. Replace it with
# your real validator: a compiler (csc), a parser (PowerShell AST), the TSV table oracle, a JSON
# schema check - whatever makes "correct" decidable for your domain. The host trusts the EXIT CODE.
$ErrorActionPreference = "Stop"
$text = [Console]::In.ReadToEnd()

if ([string]::IsNullOrWhiteSpace($text)) { Write-Output "FAIL: artifact is empty"; exit 1 }
if ($text -match 'TODO')                 { Write-Output "FAIL: artifact still contains a TODO placeholder"; exit 1 }

Write-Output "OK"
exit 0
