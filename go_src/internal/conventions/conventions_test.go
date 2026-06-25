package conventions

import "testing"

// Ports SelfTest.PathConventions.
func TestRelBuilders(t *testing.T) {
	if SchemaRel("skills") != "schemas/skills.json" {
		t.Fatalf("SchemaRel wrong: %s", SchemaRel("skills"))
	}
	if SampleRel("skills") != "samples/skills.txt" {
		t.Fatalf("SampleRel wrong: %s", SampleRel("skills"))
	}
	if FlowRel("answer") != "flows/answer.json" {
		t.Fatalf("FlowRel wrong: %s", FlowRel("answer"))
	}
}
