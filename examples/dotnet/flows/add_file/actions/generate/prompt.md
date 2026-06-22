Write a NEW C# file `{{ path }}` for the project, fulfilling the request below. It must compile when
built as part of the whole project (C# 5, all needed `using` directives). Match the project's
conventions.

STAY STRICTLY IN C# 5 (the in-box csc rejects newer syntax):
- NO string interpolation: never write `$"..."` - build strings with `+` concatenation.
- NO null-conditional `?.`, NO expression-bodied members (`=> ...`), NO tuples.

## Request
{{ request }}

## Relevant references
{{ refs }}

Output ONLY the complete C# source of {{ path }} - no prose, no markdown fences.
