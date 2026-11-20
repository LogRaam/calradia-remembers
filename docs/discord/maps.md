===== MESSAGE 1 =====
# 🗺️ Tutorial for modders: keep control of an AI-built mod with MAPS

**Who is this for?**
Modders who let an AI (Claude Code, Cursor, Copilot...) write a good part of their mod's code, and who are starting to feel they no longer see the whole of it.

**The problem**
When a model writes most of the code, the first thing to rot is not the code. It is **knowing what already exists**.
A real case from Calradia Remembers: the question "how friendly must a character be to count as a friend?" had **five answers** in the code. `FriendThreshold = 10` sat in three files, and two more constants said 20. Every class was clean. Nobody made a mistake. The model had no memory of the first one when it wrote the second, and nothing prompted it to look.

A rules file for the AI ("always reuse existing thresholds") is advice and ages. A hand-written architecture document rots and is still trusted. A generated document rots too, unless something forces it to be regenerated.

**The answer: maps.** The full reasoning, with numbers, is in Gabriel's article:
https://scripting.pro/posts/keeping-control-of-code-you-no-longer-write/

===== MESSAGE 2 =====
## What a map is

A map is **a view of your mod generated from its own source code, committed to the repository as a file, and checked by a test that fails when the committed copy no longer matches the code.**

Three parts:
1. **A generator**: a small program that reads your source and writes a Markdown file.
2. **The map**: that file, in `docs/maps/`, committed and reviewed like code.
3. **The test**: it regenerates the map and compares. If the code moved and the map did not, your test suite goes red.

**The test is the part that matters.** Without it you have written one more document that will rot. With it, the map is always as true as the code.

**Why it helps:**
- **The model reads the map before it writes.** "Is there already a constant for this?" now has a true answer in one file.
- **Every change gets two diffs**: the code diff, and the map diff, which shows what the change meant for the structure (a new dependency, a second constant with an existing name, a save key gone).
- **A red map test is a message to the model**, while it is still working on the change: "you just added this; did you mean to?"

===== MESSAGE 3 =====
## Step 1: install the skill

Gabriel wrote a skill that teaches Claude Code the whole method:
https://github.com/LogRaam/My-Claude-Skills

Copy the `generated-maps` folder into:
- `~/.claude/skills/` to use it in every project, or
- `.claude/skills/` inside your mod's folder, for that project only.

Then, in your mod's folder, ask Claude Code:
> Set up generated maps on this project.

It follows the order in the next message. Read the skill's own `README.md` first if you want to understand the idea before trusting it.

Not on Claude Code? The skill's `SKILL.md` is plain text: give it to your agent as instructions, or follow the steps below by hand.

===== MESSAGE 4 =====
## Step 2: the first maps, in this order

Each step is worth having on its own. Stop wherever you like.

1. **Inventory**: every type of your mod, by namespace, with its one-line summary (from your `///` comments). Plus the very large files.
2. **The test** that regenerates the inventory and compares it with the committed copy. This is the step that makes it real.
3. **An update command** that regenerates every map and **prints the diff** of what moved (not just "3 files changed": a two-line change hides behind a number).
4. **Your contracts**, before any other view (see message 6).
5. **Dependencies** between your namespaces (cycles first), and a **rules index** built from your test names: the closest thing your mod has to a specification.
6. **Then one map per kind of bug you have actually had twice.** Look at your last twenty bug reports and group them. Your list will not be CR's.

**Tips for C# mods:**
- Have the generators **read the source as text** with Roslyn (`Microsoft.CodeAnalysis.CSharp`, syntax only). Your tests then need neither the game's dlls nor a build, and your doc comments survive into the map.
- **Deterministic output**: sorted, no dates, no absolute paths. A noisy diff is a diff nobody reads.
- Add a `.gitattributes` with `* text=auto eol=lf` **before** the first map, or Windows line endings will turn every map red for nothing.

===== MESSAGE 5 =====
## Step 3: the loop, every change

The order is the point:

1. **Before writing**: open the map that answers your question. Is there already a type for this? A number for this? Am I adding a dependency?
2. **Make the change.**
3. **Run the map tests in COMPARE mode first**, never update mode. If you regenerate first, every map compares with itself and passes, and nobody ever sees the drift. This is the most common way the method silently stops working.
4. **Read every failure.** Is this the move you meant?
5. **Only then regenerate**, run the full suite, and **commit the maps with the change**.
6. **In the commit message, say what the maps showed**, even "nothing like this existed". A skipped look then shows in review.

When you review a change (yours or the AI's), **read the map diff before the code diff**. It is the short version. An unchanged map is information too: that part did not move.

===== MESSAGE 6 =====
## Two kinds of test: informing and blocking

**Informing maps** (inventory, dependencies, constants...) accept whatever the code now says: red, read, regenerate, commit. Their value is the attention the diff buys, and that wears off. Within weeks "regenerate and commit" becomes a reflex. Plan for it.

**Blocking tests** refuse a change just because the code made it:
- **Contracts: promises made to people outside your mod.** For a Bannerlord mod, two matter most:
  - **Your save data**: every key you write in `SyncData`, every saveable type id. A key that disappears means your players' saved data is silently never read again. Additive keys only.
  - **Your public API**, if other mods build on yours. A member that disappears breaks their mod.
  Compare these with a **snapshot recorded at each release**, not with the last commit, and let one go only through an explicit override with a reason in the commit.
- **Ratchets**: a number that may only get better ("modules caught in a dependency cycle may not grow", "bare-number thresholds may only go down"). When it drops, lower the baseline in the same commit. A ratchet needs no human reader, which makes it the best answer to review fatigue.

===== MESSAGE 7 =====
## Calradia Remembers as a worked example

CR runs on maps every day. Its source is public on GitHub:
https://github.com/LogRaam/calradia-remembers

Worth opening:
- `docs/maps/README.md`: every map, and the question each one answers. Start here.
- `docs/maps/constants.md`: opens with the names declared in more than one place (the friend-threshold problem, made visible).
- `docs/maps/save-format.md` and `docs/maps/modder-api.md`: the two contracts. `modder-api-released.md` is the snapshot taken at each release.
- `docs/maps/settings.md`: every MCM setting with every piece of code that reads it. When a player says "I turned it off and it still happens", the road doing it is the one missing from the list.
- `CalradiaRemembers.Tests/Maps/`: the generators, `MapsAreCurrentTests` (the compare test) and `RatchetTests`.
- `Tools/Update-Maps.ps1`: the update command, which prints what moved.

⚠️ CR's code is **all rights reserved**: read it and learn from it, but don't copy it into your mod. Have your agent write your own generators with the skill. That takes an afternoon, and they will fit your mod better.

===== MESSAGE 8 =====
## What it costs, and what maps cannot do

**Cost on CR** (two repositories, about 1,400 types at the time): eleven generators at first, about a thousand lines, written by the AI in a day. The suite went from about one second to seven. Every structural change carries one more step, reading the map diff, and that friction is the point.

**It paid off on day one.** Just reading the first maps showed a feature declared in six of the seven places it needed (unreachable for months), a payment ceiling defined in five places, and a dependency cycle across 37 namespaces.

**What maps do not solve:**
- **The same concept under two names** (`HostileThreshold = -30`, `EnemyLimit = -25`): the map puts them side by side, and a person decides.
- **A behaviour that changed with no test changing**: that is a missing test, not a missing map.
- **The same model writing the code and its tests**: real bug reports turned into tests, and a human reading the diffs, are what keep it honest.
- **Intent**: a map shows what is there, never what it was meant to be.

Questions, or a step that did not work for your mod? Ask here: Gabriel is still improving the skill, and real cases from other mods are exactly what he needs.
