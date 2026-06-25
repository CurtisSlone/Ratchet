// TableSchema is the data model for the oracle: a table's columns and their constraints, plus the
// Problem type the oracle reports. The validation logic lives in internal/oracle. Port of
// src.bak/Model/TableSchema.cs.
package model

import (
	"encoding/json"
	"fmt"
	"os"
)

// ColSpec is one column's constraints. CType is one of: int | float | bool | string | enum | ref.
type ColSpec struct {
	Name     string
	CType    string   // defaults to "string"
	Required bool     //
	Min      *float64 // nil = no minimum
	Max      *float64 // nil = no maximum
	Values   []string // for enum
	RefTable string   // for ref ("" = none)
}

// TableSchema is a table's name, optional key column, and ordered columns.
type TableSchema struct {
	Name    string
	Key     string // the id column other tables reference ("" = none)
	Columns []ColSpec
}

// the on-disk JSON shape (snake_case keys), kept separate from the in-memory model.
type colSpecJSON struct {
	Name     string   `json:"name"`
	CType    string   `json:"type"`
	Required bool     `json:"required"`
	Min      *float64 `json:"min"`
	Max      *float64 `json:"max"`
	Values   []string `json:"values"`
	RefTable string   `json:"ref_table"`
}

type tableSchemaJSON struct {
	Name    string        `json:"name"`
	Key     string        `json:"key"`
	Columns []colSpecJSON `json:"columns"`
}

// LoadTableSchema reads and parses schemas/<table>.json.
func LoadTableSchema(path string) (*TableSchema, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("reading schema %s: %v", path, err)
	}
	var raw tableSchemaJSON
	if err := json.Unmarshal(data, &raw); err != nil {
		return nil, fmt.Errorf("parsing schema %s: %v", path, err)
	}
	s := &TableSchema{Name: raw.Name, Key: raw.Key}
	for _, c := range raw.Columns {
		ctype := c.CType
		if ctype == "" {
			ctype = "string"
		}
		values := c.Values
		if values == nil {
			values = []string{}
		}
		s.Columns = append(s.Columns, ColSpec{
			Name: c.Name, CType: ctype, Required: c.Required,
			Min: c.Min, Max: c.Max, Values: values, RefTable: c.RefTable,
		})
	}
	return s, nil
}

// Problem is one oracle finding. Row is the 1-based data row (0 = header).
type Problem struct {
	Row int
	Col string
	Msg string
}

// String renders a problem the way the C# Problem.ToString did.
func (p Problem) String() string {
	if p.Row == 0 {
		return "[header] " + p.Col + ": " + p.Msg
	}
	return fmt.Sprintf("[row %d] %s: %s", p.Row, p.Col, p.Msg)
}
