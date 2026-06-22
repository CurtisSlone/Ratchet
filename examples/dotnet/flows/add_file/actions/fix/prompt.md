Your new file `{{ path }}` failed to build the project. Return the CORRECTED complete file, fixing
exactly what the compiler reported while still fulfilling the request.

C# 5 ONLY (in-box csc): error CS1056 'Unexpected character $' means you used `$"..."` interpolation -
replace it with `+` string concatenation. Also avoid `?.`, expression-bodied members, and tuples.

## Request
{{ request }}

## Compiler errors
{{ errors }}

## Your previous version of {{ path }}
{{ prev }}

Output ONLY the complete corrected source of {{ path }} - no prose, no markdown fences.
