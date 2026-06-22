Apply the requested change to the file `{{ path }}` and return the COMPLETE updated file (whole-file
rewrite, not a diff). Preserve everything not related to the change. Keep all `using` directives the
file needs.

STAY STRICTLY IN C# 5 (the in-box csc rejects newer syntax):
- NO string interpolation: never write `$"..."` - build strings with `+` concatenation.
- NO null-conditional `?.`, NO expression-bodied members (`=> ...`), NO tuples, NO `var` in fields.

## Change requested
{{ request }}

## Current contents of {{ path }}
{{ current }}

## Relevant references
{{ refs }}

Output ONLY the full updated source of {{ path }} - no prose, no markdown fences.
