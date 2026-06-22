Your edited `{{ path }}` failed to build the project. Return the CORRECTED complete file, fixing
exactly what the compiler reported while still applying the original change.

C# 5 ONLY (in-box csc): error CS1056 'Unexpected character $' means you used `$"..."` interpolation -
replace it with `+` string concatenation. Also avoid `?.`, expression-bodied members, and tuples.

## Change requested
{{ request }}

## Compiler errors
{{ errors }}

## Your previous version of {{ path }}
{{ prev }}

Output ONLY the full corrected source of {{ path }} - no prose, no markdown fences.
