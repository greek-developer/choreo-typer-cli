# BRIEF.md — choreo-typer-cli

## Overview

Choreo Typer replays a prepared script of keystrokes into whatever window currently has
focus, so a live demo types itself — the same commands, in the same order, at the same
speed, every run. It exists for recorded tutorials and in-person talks, where one typo
mid-demo costs more than the typing ever saved.

It is a **Windows desktop application, not a terminal program**, despite the `-cli`
repository name and the global-tool packaging. Running it opens a window listing the lines
of the loaded script, and the operator never types into that window. Control arrives over
HTTP — from a Stream Deck or anything else that can issue a request — so the demo is driven
from a physical button while keyboard focus stays in the editor or terminal being
demonstrated.

The project is **pre-release**: it works, it is not polished, and HTTP is currently the only
way to trigger it.

## Build & run

```powershell
dotnet build                                                 # build everything
dotnet run --project src/ChoreoTyper -- path\to\script.txt   # run, loading a script
```

There are **no tests** — `dotnet test` finds no test project. See [Tests](#tests).

Started with no argument, or with a path that does not exist, it loads a three-line built-in
sample rather than failing.

### Packaging is currently broken

`dotnet pack` **fails**, and has as long as the project has targeted .NET 5 or later:

```
error NETSDK1146: PackAsTool does not support TargetPlatformIdentifier being set.
```

`PackAsTool` cannot be combined with a `net*-windows` target framework or with
`UseWindowsForms`, and this project needs both — `SendKeys` is why it is a WinForms app at
all. So the `.csproj` asks to be a .NET global tool and the SDK refuses. Nothing has been
packed locally, and the **publish workflow's pack step fails the same way** on a push to
`release/production`.

Resolving it means picking one: split the keystroke engine into a packable non-Windows-
targeted CLI, or drop `PackAsTool` and ship the self-contained build instead. Until then,
what actually ships is the zip:

```powershell
./build-self-contained.ps1     # self-contained single-file win-x64 publish, zipped, into ./release
```

Two things to know before running that script:

- **It deletes `./release` first**, which also destroys anything a future working
  `dotnet pack` leaves there. Pack after the script, not before.
- It passes a hardcoded `0.1.0` rather than the computed version, so the artifact it
  produces is not versioned the way the package would be.

### The HTTP control surface

The app listens on `http://localhost:5005/` for as long as its window is open. Each route
answers `GET` or `POST` with `OK`; anything else gets 404.

| Route | Does |
|---|---|
| `/prev` | Move the highlight one line back — wraps at the start |
| `/next` | Move the highlight one line forward — wraps at the end |
| `/type` | Send the highlighted line, then advance |
| `/play` | Send every line from the highlight onwards, 500 ms apart |
| `/stop` | Cancel a running `/play` |

The port is fixed in source and is not configurable.

### Script syntax

A script is a plain text file, one step per line; blank lines are dropped and each line is
trimmed. A line holds one or more commands, each introduced by `##` plus a single letter,
with the argument after the colon:

| Command | Sends |
|---|---|
| `##T:` | The text, with `SendKeys` metacharacters escaped |
| `##L:` | The same, followed by ENTER |
| `##C:` | Cursor keys, one letter each — `n` ENTER, `h` HOME, `e` END, `l` LEFT, `r` RIGHT, `d` DOWN, `u` UP, `t` TAB |
| `##W:` | Nothing — waits the given number of milliseconds |
| `##R:` | The text raw and unescaped, straight to `SendKeys.SendWait` |
| `##N` | ENTER |

**The space after the colon is mandatory.** The argument is taken as everything past the
first three characters of the command, so `##T: hello` sends `hello` while `##T:hello`
sends `llo`.

Double-clicking a line in the window reloads the script from disk and keeps the selection.
That is how a script is edited while the app stays open.

## Layout

Standard grdev layout ([AGENTS.md](AGENTS.md)), less what does not exist yet: there is no
`tests/`, `docs/`, `specs/`, `tasks/` or `scripts/`.

| Deviation | Detail |
|---|---|
| `build-self-contained.ps1` | Lives at the repository root rather than in `./scripts` |
| `greekdev.choreo-typer.sln` | A `.sln` rather than `.slnx`, and named `greekdev.` rather than `grdev.` — it predates both conventions |
| `TODO.md` | At the root, and empty. Task capture belongs in `tasks/tasks.md` |

All source is one project, `src/ChoreoTyper`. `Program.cs` is the `[STAThread]` entry point
and `MainForm.cs` is everything else — window, HTTP listener, script parser and key sender.

## Stack

| Concern | Choice | Note |
|---|---|---|
| Platform | .NET 8, `net8.0-windows` | `EnableWindowsTargeting` is on, so it restores from a non-Windows SDK |
| UI | WinForms — `UseWindowsForms`, `OutputType=WinExe` | Built in code; there is no designer file |
| Keystrokes | `System.Windows.Forms.SendKeys` | The reason the app is Windows-only, and — through `UseWindowsForms` — the reason it cannot be packed as a global tool |
| HTTP | `System.Net.HttpListener` | BCL |
| Versioning | `Nerdbank.GitVersioning` | The only package reference, and it comes from `Directory.Build.props` |

**The app itself has no third-party dependencies.** Keep it that way unless something
genuinely cannot be done with the BCL.

`Nullable` is enabled in the `.csproj`. `ImplicitUsings` is **not**, so every file writes
its own `using` directives — the standard requires it and this project does not meet that
yet. Neither `TreatWarningsAsErrors` nor `EnforceCodeStyleInBuild` is set anywhere.

## Tests

**None.** There is no test project, so `dotnet test` has nothing to run.

The logic worth covering all sits in `MainForm`: `ProcessTextCommand` (the `SendKeys`
escaping), `ProcessSpecialCommand` (cursor-key expansion) and the `##` splitting in
`SendActive`. All three are private members of a `Form`, so covering them means lifting the
parser out of the UI class first.

## Never

- **Never bind the listener to anything but `localhost`.** The routes carry no
  authentication and their effect is keystrokes injected into whatever window has focus.
  Reachable from off the machine, that is remote input injection.
- **Never do blocking work on the UI thread beyond what is already there.** The HTTP handler
  marshals every action back with `Invoke`, and `SendKeys.SendWait` plus the `##W:` sleep
  already hold that thread. One more blocking call there and the listener stops answering.
- **Never make `##R:` escape its argument.** It is the deliberate raw escape hatch — `##T:`
  is the escaped form. Escape both and there is no way left to send a modifier chord.

## Decisions

### 2026-08-13

- Adopted the grdev agentic standard. `AGENTS.md` is synced verbatim from
  [`greek-developer/agentic`](https://github.com/greek-developer/agentic) and is never
  edited locally; everything project-specific lives in this file.
- `PackageOutputPath` moved from `../../nupkg` to `../../release`, the standard's gitignored
  build-output directory. `release/` is now listed explicitly in `.gitignore` rather than
  being covered incidentally by the .NET template's `[Rr]elease/` build-configuration
  pattern. The setting takes no effect until packaging works at all — see
  [Packaging is currently broken](#packaging-is-currently-broken).
- Recorded that `dotnet pack` fails with `NETSDK1146`: `PackAsTool` is incompatible with
  this project's `net8.0-windows` / `UseWindowsForms` combination. Left unresolved — the fix
  is a packaging decision, not a rename.
- `.editorconfig` added — the `dotnet new editorconfig` baseline with `end_of_line = crlf`
  and `insert_final_newline = true`, and the `[*.cs]` `insert_final_newline` flipped to
  match.
- `.gitattributes` added, pinning the working tree to CRLF with `.github/workflows/**` held
  at LF so a `run:` block still parses on a Linux runner.
- This log starts here. Decisions taken before today were never written down, so the
  repository is described as it stands above rather than reconstructed as dated entries.
