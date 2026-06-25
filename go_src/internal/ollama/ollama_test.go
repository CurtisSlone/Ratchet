package ollama

import "testing"

func TestTokenMeter(t *testing.T) {
	p0, e0, c0 := MeterPrompt(), MeterEval(), MeterCalls()
	MeterRecord(10, 5)
	MeterRecord(3, 2)
	if MeterPrompt()-p0 != 13 || MeterEval()-e0 != 7 {
		t.Fatalf("meter sums wrong: dPrompt=%d dEval=%d", MeterPrompt()-p0, MeterEval()-e0)
	}
	if MeterCalls()-c0 != 2 {
		t.Fatalf("meter calls: want +2, got +%d", MeterCalls()-c0)
	}
	if MeterTotal() != MeterPrompt()+MeterEval() {
		t.Fatal("total != prompt + eval")
	}
}

func TestCancelNilSafe(t *testing.T) {
	var c *Cancel
	c.Abort() // must not panic on a nil handle
	if c.cancelled() {
		t.Fatal("nil cancel should not report cancelled")
	}
	live := NewCancel()
	if live.cancelled() {
		t.Fatal("fresh cancel should not be cancelled")
	}
	live.Abort()
	if !live.cancelled() {
		t.Fatal("aborted cancel should report cancelled")
	}
}
