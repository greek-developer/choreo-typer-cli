# ChoreoTyper

Choreo Typer is a keystroke automation tool, intended to be used for automating tutorials either for videos or for in-person delivery. 

# PRE-RELEASE DISCLAIMER

Choreo Typer is still in an early version, it works, but it's not yet polished and currently it can be triggered only by http commands 


## HTTP API
 
Choreo Typer listens on localhost:5005 and its designed to be used with a Stream Deck with an HTTP plugin (like BarRaiders API Ninja) or equivalent

```
// Moves one line backwards
GET/POST http://localhost:5005/prev

// Moves one line forwards
GET/POST http://localhost:5005/next

// Types the highlighted line and moves to the next one
GET/POST http://localhost:5005/type

// Starts typing all commands, one-by-one from the currently highlighted one
GET/POST http://localhost:5005/play

// Stops the play 
GET/POST http://localhost:5005/stop
```

## Usage

`dotnet run -- path\to\file.txt`

## Command Syntax

### Choreo Typer Commands
- A command in Choreo Typer consists of two parts:
  - The Command identifier starts with `##` and is followed by a letter 
  - The Command text (optional). The text must be seprated by a `:` from the command indentifier


- A line in the text file can contain multiple commands

### Command indentifiers
  - `##T:` Type the command text as text, escaping any characters that need to.
  - `##T:` Type the command text as text, escaping any characters that need to, with a new line at the end.
  - `##C:` Type the command text as keyboard command 
    - Keyboard commands:
      - `n` -> ENTER
      - `h` -> HOME
      - `e` -> END
      - `d` -> DOWN
      - `u` -> UP
      - `t` -> TAB
  - `##W:` Pause for the duration specified in command text as milliseconds
  - `##R:` Send the command text as it is (`SendKeys.SendWait` is used) 
  - `##N` Send a new line (no text expected)

### Examples
  - `##T: Console.WriteLine("Hello World")` will type "Console.WriteLine("Hello World")" in the active window
  - `##R: ^p` will send "CTRL + p" to the active window
  - `##C: ehtt` will send an ENTER, follwoed by a HOME and 2 TABs (usefull for identing)
  - `##W: 500` will wait for 500 ms
  - `##C: ehtt ##L: Console.WriteLine("Hello World") ##W: 500` will send ENTER, HOME, TAB, TAB, type Console.WriteLine("Hello World"), send ENTER and wait for 500ms


## Tips for VS Code


### Set shortcuts to focus terminal windows

CTRL + ALT + P -> Open Keyboard Shortcuts

workbench.action.terminal.focusAtIndex1 -> CTRL + ALT + 1
workbench.action.terminal.focusAtIndex2 -> CTRL + ALT + 2
workbench.action.terminal.focusAtIndex3 -> CTRL + ALT + 3
workbench.action.terminal.focusAtIndex4 -> CTRL + ALT + 4  

### Open a file at a specific line number and column number

`##R: ^p <filename>:<line-number>:<column-number>`

## Versioning

Versions are computed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
from [`version.json`](version.json) plus the git height — there is no hardcoded version anywhere.
`version.json` holds the `major.minor`; the patch is the number of commits since that value last
changed, so **every commit bumps the patch automatically**.

Install the CLI once:

```powershell
dotnet tool install --global nbgv
```

### Viewing the version

```powershell
nbgv get-version                    # full summary for HEAD
nbgv get-version -v SimpleVersion   # just x.y.z, for scripts
nbgv get-version -f json            # everything, as JSON
```

### Setting the version

The patch bumps on its own with every commit. To change the major or minor, hand-edit the
`version` field in `version.json` and commit it — the patch count restarts from there:

```json
"version": "1.3"
```

Do **not** run `nbgv set-version`. It rewrites `version.json` from scratch and silently drops the
`publicReleaseRefSpec` and `cloudBuild` settings this repo relies on. Never add a `<Version>`
element to `Directory.Build.props` or a `.csproj` either — it would override the computed version.

### Releases

A build from `release/production` is a public release and gets a clean version (`1.3.4`). Every
other branch is a prerelease and gets a commit-id suffix (`1.3.4-g1a2b3c4`). Pushing to
`release/production` triggers the [publish workflow](.github/workflows/publish-nugets.yml).

