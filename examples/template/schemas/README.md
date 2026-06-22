# Oracle table schemas

The deterministic oracle validates tab-separated data tables against a schema. Put one schema per
table here as `schemas/<table>.json`, and the data to check as `samples/<table>.txt`. Run
`ratchet validate <instance> <table>`.

## Schema format

```json
{
  "name": "widgets",
  "key": "id",                                              // column other tables reference (this table's id set)
  "columns": [
    { "name": "id",    "type": "int",    "required": true },
    { "name": "label", "type": "string", "required": true },
    { "name": "size",  "type": "int",    "min": 1, "max": 100 },
    { "name": "kind",  "type": "enum",   "values": ["small", "large"] },
    { "name": "owner", "type": "ref",    "ref_table": "people" }   // must exist in people's key set
  ]
}
```

## Column types

- `int` / `float` - parses as a number; honors `min` / `max`.
- `bool` - must be `0` or `1`.
- `string` - any non-empty value (when `required`).
- `enum` - must be one of `values`.
- `ref` - must appear in `ref_table`'s `key` column (cross-table referential integrity).

## What the oracle checks

- The header contains every column the schema names.
- Every data row has the header's column count (the classic tab-added/dropped corruption catch).
- Each typed cell parses and sits in range; enums are in the allowed set.
- `ref` cells resolve against the referenced table's id set, when that table's schema + data are
  available to the validate run (the cross-table integrity check).

`example.json` here + `samples/example.txt` are a working pair you can `ratchet validate <instance> example`.
Delete them once you have your own tables.
