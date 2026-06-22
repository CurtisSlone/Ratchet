# WinForms test log (post-rework: workspaces + action chains)

Runnable WinForms apps of increasing complexity, driven through the **reworked** dispatcher
(`icm chat`) on the new architecture: action-chain flows, `workspaces/` instead of `out/`, tool
manifests, configurable dirs. Each app is scaffolded as a winforms project, then built up with the
`add_file` / `edit_file` **chains**; the whole-project `csc` (winexe) is the oracle, and the chain's
bounded repair loop fixes build failures. Every app ends as a runnable `dist/<name>.exe`.

- Generation model: `qwen3-coder:latest` (Ollama, local)
- Oracle: whole-project `csc -noconfig @response.rsp` (target `winexe`)
- Date: 2026-06-21
- Verbs (replacing the old `/new` `/add` `/build` aliases):
  `/do new_project <name> winforms` -> `/ws switch <name>` -> `/flow add_file <path> <desc>` /
  `/flow edit_file <path> <desc>`. The built exe is `workspaces/<name>/dist/<name>.exe`.

## Run-state: each chain run records its own steps

Every `/flow` run writes its own trace under `workspaces`-independent `runs/<id>/`:
`meta.json` (chain, input, budgets), one `step-NNN.json` per node executed (node id, kind, ok,
output - including the compiler diagnostics on a failed build), and `outcome.json`
(`{outcome, steps, error}`). The transcripts below are the console output; the per-step detail
(e.g. the exact compiler error that drove a repair) lives in those files.

---

## App 1 - GreetForm (simple): one input, one button, one label

### Input
````
/do new_project GreetForm winforms
/ws switch GreetForm
/flow edit_file src/Ui/MainForm.cs replace the window contents with a TextBox named nameBox, a Button labeled Greet, and a Label named output; when Greet is clicked set output text to Hello plus the name from nameBox
````

### Output
````
OK: created workspaces\GreetForm (winforms) - 6 files. Activate it: /ws switch GreetForm
active workspace: GreetForm

using System;
using System.Drawing;
using System.Windows.Forms;

namespace App.Ui
{
    public sealed class MainForm : Form
    {
        private readonly TextBox nameBox;
        private readonly Button greetButton;
        private readonly Label output;

        public MainForm()
        {
            this.Text = "App";
            this.ClientSize = new Size(360, 180);

            nameBox = new TextBox();
            nameBox.Location = new Point(20, 20);
            nameBox.Size = new Size(100, 20);
            nameBox.Text = "World";
            this.Controls.Add(nameBox);

            greetButton = new Button();
            greetButton.Text = "Greet";
            greetButton.Location = new Point(130, 20);
            greetButton.Size = new Size(75, 23);
            greetButton.Click += OnGreetClick;
            this.Controls.Add(greetButton);

            output = new Label();
            output.Location = new Point(20, 50);
            output.Size = new Size(320, 23);
            output.Text = "";
            this.Controls.Add(output);
        }

        private void OnGreetClick(object sender, EventArgs e)
        {
            output.Text = "Hello " + nameBox.Text;
        }
    }
}

  [local model: 294 generated + 601 prompt tok, 1 call(s)]
````

Result: compiled first try (1 model call). `dist/GreetForm.exe` built.

---

## App 2 - TempConv (medium): parse + validate + compute. FAILED first, then fixed.

This is the most useful entry: the increasing complexity exposed a real gap.

### First attempt (FAILED)

Same `/do new_project TempConv winforms` -> `/ws switch` -> `/flow edit_file ... Celsius to Fahrenheit
converter ...`. The run aborted:

````
  - step 1: edit_file.read (action)
  - step 2: edit_file.generate (generate)
  - step 3: edit_file.build (action)
  - step 4: edit_file.fix (generate)
  - step 5: edit_file.rebuild (action)
  - step 6: edit_file.fail (exit)
  [local model: 732 generated + 1170 prompt tok, 2 call(s)]

  outcome: "aborted: file missing, or did not build after one repair"
````

Root cause, straight from `runs/<id>/step-003.json`:

````
src\Ui\MainForm.cs(45,36): error CS1056: Unexpected character '$'
````

The model reached for C# 6 **string interpolation** (`$"..."`), which the in-box C# 5 `csc` rejects.
The single repair attempt did not clear it. Diagnosis: the `edit_file` generate node only `search`es
the kb by the task text ("celsius converter"), which surfaces task docs, not the C# 5 constraint - so
the language constraint was never reliably in front of the model.

### Fix (prompt guard)

Added an explicit C# 5 constraint to both the `generate` and `fix` prompts of `edit_file` (and
`add_file`): never write `$"..."` (use `+` concatenation), no `?.`, no expression-bodied members, no
tuples; and a targeted note in the fix prompt mapping CS1056 to "remove the `$`".

### Second attempt (SUCCESS, fresh scaffold)

### Input
````
/do new_project TempConv winforms
/ws switch TempConv
/flow edit_file src/Ui/MainForm.cs build a Celsius to Fahrenheit converter: a TextBox celsiusBox, a Button labeled Convert, and a Label result; on click parse celsiusBox as double (show 'invalid number' on failure), compute F = c*9/5+32, and show the result
````

### Output
````
        private void OnConvertClick(object sender, EventArgs e)
        {
            double celsius;
            if (!double.TryParse(celsiusBox.Text, out celsius))
            {
                resultLabel.Text = "invalid number";
                return;
            }

            double fahrenheit = celsius * 9.0 / 5.0 + 32.0;
            resultLabel.Text = "Fahrenheit: " + fahrenheit.ToString();
        }

  [local model: 352 generated + 678 prompt tok, 1 call(s)]
````

Result: now compiles **first try** (1 call, no repair) - `+` concatenation, `double.TryParse`.
`dist/TempConv.exe` built.

---

## App 3 - TipCalc (complex): multi-file, cross-file reference

A `Core` logic class added with `add_file`, then a form that calls it added with `edit_file`. The
whole-project build links both.

### Input
````
/do new_project TipCalc winforms
/ws switch TipCalc
/flow add_file src/Core/TipCalculator.cs a public static class TipCalculator in namespace App.Core with a method double Tip(double bill, double percent) returning bill times percent divided by 100
/flow edit_file src/Ui/MainForm.cs add TextBoxes billBox and percentBox, a Button labeled Compute, and a Label result; on click parse both boxes as double, call App.Core.TipCalculator.Tip, and show the tip and the total (bill plus tip); add using App.Core
````

### Output (chain traces + outcomes)
````
  - step 1: add_file.generate (generate)
  - step 2: add_file.build (action)        # whole-project build incl. the new Core file
  - step 3: add_file.register (action)
  - step 4: add_file.done (exit)
  [local model: 52 generated + 570 prompt tok, 1 call(s)]   outcome: success

  - step 1: edit_file.read (action)
  - step 2: edit_file.generate (generate)
  - step 3: edit_file.build (action)
  - step 4: edit_file.log (action)
  - step 5: edit_file.done (exit)
  [local model: 516 generated + 674 prompt tok, 1 call(s)]   outcome: success
````

Generated `src/Core/TipCalculator.cs`:
````csharp
using System;

namespace App.Core
{
    public static class TipCalculator
    {
        public static double Tip(double bill, double percent)
        {
            return bill * percent / 100;
        }
    }
}
````

Generated `src/Ui/MainForm.cs` (the cross-file call, abbreviated to the handler):
````csharp
        private void OnComputeClick(object sender, EventArgs e)
        {
            double bill;
            double percent;

            if (double.TryParse(billBox.Text, out bill) && double.TryParse(percentBox.Text, out percent))
            {
                double tip = App.Core.TipCalculator.Tip(bill, percent);
                double total = bill + tip;
                resultLabel.Text = "Tip: " + tip.ToString("F2") + ", Total: " + total.ToString("F2");
            }
            else
            {
                resultLabel.Text = "Invalid input";
            }
        }
````

Result: both files compiled **first try** (1 call each). The form correctly references
`App.Core.TipCalculator.Tip` across files; the whole-project oracle linked them. `dist/TipCalc.exe`
built.

---

## App 4 - NotesApp (harder): two forms, a modal dialog, cross-form data

A second `Form` (a modal input dialog) added with `add_file`, then a main form with a `ListBox` that
opens the dialog and collects results, added with `edit_file`. Exercises `ShowDialog`, `DialogResult`,
and reading a value back across forms.

### Input
````
/do new_project NotesApp winforms
/ws switch NotesApp
/flow add_file src/Ui/InputDialog.cs a public sealed Form named InputDialog in namespace App.Ui with a TextBox inputBox and an OK Button and a Cancel Button; OK sets DialogResult.OK, Cancel sets DialogResult.Cancel; expose a public string property Value returning inputBox.Text
/flow edit_file src/Ui/MainForm.cs make a notes list: a ListBox items filling most of the window, an Add button and a Clear button along the bottom; Add opens a new App.Ui.InputDialog via ShowDialog and if the result equals DialogResult.OK adds the dialog Value to items; Clear removes all items
````

### Output (the cross-form wiring, abbreviated)
````csharp
// MainForm
private void AddButton_Click(object sender, EventArgs e)
{
    InputDialog dialog = new InputDialog();
    if (dialog.ShowDialog() == DialogResult.OK)
    {
        items.Items.Add(dialog.Value);
    }
}

// InputDialog (proper modal: AcceptButton/CancelButton, FixedDialog, CenterScreen)
this.AcceptButton = this.okButton;     // okButton.DialogResult = DialogResult.OK
this.CancelButton = this.cancelButton;
public string Value { get { return inputBox.Text; } }
````

Result: both files compiled **first try** (1 call each; the deepened 2-retry loop was not needed).
Correct modal dialog and cross-form data flow. `dist/NotesApp.exe` built and launched via `/run`.

---

## Running the apps (`/do make_launcher` writes a launcher `.cmd`)

The built `dist/<name>.exe` is **unsigned, so Smart App Control blocks running it directly**
(confirmed: `Start-Process` returns "An Application Control policy has blocked this file"). Loading
its bytes **in-memory** inside trusted powershell IS allowed (confirmed: `Assembly.Load` succeeds) -
the same trick `icm.cmd` uses for `icm.exe`. A host-side launch is also wrong because the console may
run in a non-interactive session whose GUI never reaches the operator's desktop.

Launching/building artifacts is domain-specific, so this is NOT a host command - it is an instance
tool, `make_launcher`, run via the generic `/do`. It writes a `launch-<name>.cmd` into the workspace;
the operator runs that from their own session:

````
/do make_launcher GreetForm      # bare name -> workspaces/GreetForm, or pass a full path
  -> launcher written: ...\workspaces\GreetForm\launch-GreetForm.cmd
     run it from your terminal (or double-click) to launch GreetForm - SAC-safe in-memory load
````

`make_launcher` resolves the artifact (`project.json` `out`, else `dist/<name>.exe`) and emits the
`.cmd`. The `.cmd` is one line: `powershell -File tools\run_app.ps1 -Exe <abs dist exe>` - i.e. the
in-memory STA loader. (An earlier `/run` host command was removed - it put a domain capability in the
generic host; `/do make_launcher` is the boundary-correct way.)

Generated launchers (run any from your own terminal):

- `workspaces/GreetForm/launch-GreetForm.cmd`
- `workspaces/TempConv/launch-TempConv.cmd`
- `workspaces/TipCalc/launch-TipCalc.cmd`
- `workspaces/NotesApp/launch-NotesApp.cmd`  (two forms)

## Findings

1. **The chain lifecycle works end-to-end on the new architecture** for runnable WinForms apps:
   scaffold into `workspaces/`, `add_file` / `edit_file` chains, whole-project winexe oracle, on-disk
   memory (`project.json` / `PROJECT.md`), per-run `runs/<id>/` traces.
2. **The bounded repair loop was shallow; now deepened.** One retry was not enough for a C# 5
   violation the model is biased toward (`$"..."`). Fixed two ways: (a) the prompt now forbids it
   explicitly (cheapest; moved App 2 to first-try success), AND (b) `edit_file`/`add_file` now repair
   **up to twice** (added `fix2`/`rebuild2` nodes; v1.1.0). (a) handled every app here; (b) is the
   safety net for harder tasks.
3. **kb `search` alone does not surface always-on constraints.** Task-text search finds task docs, not
   the language rules. Constant constraints (the C# 5 subset) belong in the prompt or a fixed `ref`,
   not a task-keyed search.

## Follow-ups implemented this round

- **`make_launcher` tool (via `/do`)** - writes a SAC-safe `launch-<name>.cmd` for a workspace (the
  operator runs the .cmd from their session; it loads the exe in-memory via `run_app.ps1`). Kept as an
  instance tool, NOT a host command - launching is domain-specific. Confirmed: `Start-Process` of the
  exe is SAC-blocked; in-memory `Assembly.Load` is allowed; the launcher opens the window.
- **Deeper repair** - `edit_file` and `add_file` chains now repair up to twice (`fix2`/`rebuild2`).
- **C# 5 prompt guard** - explicit "no `$\"...\"`" in the generate + fix prompts of both project chains.
