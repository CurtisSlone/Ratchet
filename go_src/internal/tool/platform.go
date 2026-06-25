// Platform-aware execution: the engine detects the host OS at runtime and branches the interpreter /
// shell choice. `command` argv tools run as-is (the author chose the interpreter); a bare `script` is
// dispatched to an interpreter by extension; `/do` pasted commands run through the host shell. This is
// the cross-platform replacement for src.bak's Windows-only PowerShell assumptions.
package tool

import (
	"os/exec"
	"runtime"
)

// firstAvailable returns the first interpreter from prefs found on PATH, or the last pref as a
// best-effort fallback (so the resulting exec produces a clear "not found" error rather than "").
func firstAvailable(prefs ...string) string {
	for _, p := range prefs {
		if _, err := exec.LookPath(p); err == nil {
			return p
		}
	}
	if len(prefs) > 0 {
		return prefs[len(prefs)-1]
	}
	return ""
}

// psInterp resolves a PowerShell interpreter: PowerShell Core (pwsh) is cross-platform; on Windows
// fall back to the in-box Windows PowerShell.
func psInterp() string {
	if runtime.GOOS == "windows" {
		return firstAvailable("pwsh", "powershell")
	}
	return firstAvailable("pwsh", "powershell")
}

func shInterp() string { return firstAvailable("bash", "sh") }
func pyInterp() string { return firstAvailable("python3", "python") }

// shellArgv returns argv that runs command through the host's shell, branched on the runtime OS:
// PowerShell on Windows (streams merged to text), bash/sh elsewhere.
func shellArgv(command string) []string {
	if runtime.GOOS == "windows" {
		wrapped := "$ProgressPreference='SilentlyContinue'; & {" + command + "} *>&1 | Out-String"
		return []string{psInterp(), "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", wrapped}
	}
	return []string{shInterp(), "-c", command}
}
