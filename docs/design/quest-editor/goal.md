# Goal: a quest editor for Calradia Remembers' written tales

You are building this in parallel with another session that keeps working on the mod itself. Read the whole of this
file before you start; the "Working alongside" section is not optional.

## What Gabriel asked for (04/10/2026, in his words, translated)

"Could you make an app that lets us compose new quest scenarios, so they are easy to write? Saving would be in JSON. We
could also load an existing scenario to modify it. And we could load a Twine file to create a new quest."

Gabriel is the solo developer. He writes to you in Quebec French; answer him in Quebec French. Everything you write in
files (code, comments, docs, commit messages) is in English.

## What exists today

Calradia Remembers (a Bannerlord mod) built "written tales" for its 2.7.0 release (not yet published): a quest in several stages, written as a
JSON file, offered by a character, voiced by the model, checked by the game at every step. Read these first:

- `CalradiaRemembers/Extension/HOWTO_Quests.md`: the authoring guide, the best description of the format. Start here.
- `docs/design/AUTHORED_QUESTS.md`: the design, Gabriel's rulings, and what is planned (branches, Inc 2).
- `CalradiaRemembers.Logic/AuthoredQuests/AuthoredQuest.cs`: the JSON model (every `[JsonProperty]` is a field).
- `CalradiaRemembers.Logic/AuthoredQuests/AuthoredQuestLoader.cs`: the loader and its checks. `Validate`,
  `ValidateObjective` and `ValidateScene` are the rules; `ObjectiveTypes`, `HazardEffects` and `ConsequenceTypes` are
  the closed lists. Problems are BLOCKING (the quest is not loaded) or COSMETIC (it loads, the report says what will go
  wrong).
- `CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood.json`: a complete tale, the reference example.
- `docs/design/quests/*.json`: three more drafts (they use fields not yet played; the loader reports them).
- In game, `cr.story_check` reloads the files and lists every problem. It stays the final referee.

## What to build

### Phase 1: the editor (this is the goal)

A single, self-contained HTML file: no install, no server, works offline by double-clicking it. It ships with the mod in
`Modules/CalradiaRemembers/Docs/` beside `HOWTO_Quests.md` (see "Shipping" below).

1. **Open** a quest `.json` from disk (a single quest or an array, as the loader accepts), or start from the werewolf
   tale as a template, or from blank.
2. **Edit** with forms, not raw JSON:
   - the header (`id`, `title`, `author`, `unique`, `repeat_cooldown_days`, `weight`);
   - `requires` (giver role, holds, min regard, not at war; village near; player clan tier) and `binds`;
   - the stages, one at a time, in order: journal line, objective (a form that changes with the objective type and
     shows only that type's fields), canvas, facts, knowers (key = a role or `locals_of:{place}`), `on_complete.next`
     chosen from the other stage ids or `reward`;
   - an `explore` objective's scene: opening canvas and facts, findings (id, required, text), hazards (id, trigger,
     effect type and its fields), encounters (id, name, persona, knows), closing;
   - reward and failure (regard, gold, memory, days, consequences).
   Add, remove and reorder stages, findings, hazards, encounters, facts and knowers.
3. **See the chain**: the stages as a readable flow (request, then question the village, then keep watch...), the
   current stage highlighted, a broken `next` shown as broken.
4. **Check live**: the loader's problems, worded the same way, BLOCKING and COSMETIC apart, each pointing at the field
   it concerns. Include the `{placeholder}` check (a role used but not bound).
5. **Save** as `.json` (pretty-printed, two-space indent, keys in the loader's names). The output must load in game
   with no problem the editor did not show.
6. Show, beside each field, the one-line explanation from `HOWTO_Quests.md`. Mark the fields the game reads but does
   not play yet (section 9 of the guide) as such; do not hide them, so a file written for later versions still opens.

The audience is players and modders, not only Gabriel: the interface is in English, plain, and assumes no knowledge of
JSON. Match the guide's tone (short plain sentences).

### Phase 2: Twine import (NOT now; write the design note only)

Twine passages map to stages (passage text to canvas, links to `next`), but Twine has no notion of an objective the game
can check, and a passage with several links is a branch, which the game does not play yet (branches are Inc 2, waiting
on Gabriel's answers in `AUTHORED_QUESTS.md`, "Branches: a proposal"). So in this goal: write
`docs/design/quest-editor/twine-import.md`, a short proposal (which Twine format: Twine 2 HTML archive and/or Twee 3;
the tag convention for objectives, e.g. `talk_to:giver`; what the import does with a passage of several links today),
with open questions for Gabriel. Do not build it.

## The one real risk: two validators that drift apart

The game's checks are C#; the editor's are JavaScript. If they disagree, the editor says "all good" and the game refuses
the quest, which is the worst outcome for a non-programmer. Guard it structurally, in the house style (the repo keeps
generated maps fresh with tests; do the same):

- **A JSON Schema generated from the C# model**, `docs/design/quest-editor/quest.schema.json` (fields, types, the
  closed lists as enums). Write the generator as a test helper and a test that fails when the committed schema differs
  from what the model produces, with the same "regenerate with CR_UPDATE_MAPS=1" pattern as
  `CalradiaRemembers.Tests/Maps/MapsAreCurrentTests.cs` (read it). The editor reads its field list and enums from that
  schema (embed it at build time, or load it beside the HTML; your call, but the shipped editor must work offline).
- **A shared corpus of problem fixtures**: small `.json` files, each with the problems the loader reports, e.g.
  `docs/design/quest-editor/fixtures/<name>.json` + `<name>.expected.txt`. A C# test runs them through
  `AuthoredQuestLoader.Parse` and asserts the expected problems; the same fixtures run through the editor's validator
  under Node (Node 26 is installed: `node --version`), from a test or a script the C# suite calls, so the two cannot
  disagree silently. If calling Node from NUnit is awkward, a script plus a C# test that at least checks every fixture
  has an expected file is acceptable; say plainly in your report which it is.
- Keep the editor's validator in its own small JS module inside the HTML so the Node run tests the exact code shipped.

## House rules (non-negotiable)

- **Tests first**, each with a comment above it giving the STAKE (what goes wrong for a player if it fails), never "this
  test verifies". Test names are Gherkin (`GIVEN_..._WHEN_..._THEN_...`). Read `CalradiaRemembers/CLAUDE.md`, section
  "Unit tests carry their WHY" and "What a test is for".
- **No em dashes** in anything you add. Before each commit:
  count the added lines holding the em dash character (U+2014) in `git diff --cached`, and commit only when the count
  is 0 (`[ "$n" = 0 ] && git commit ...`). The Write/Edit tools turn a typed
  unicode escape into the character itself; build escapes in Python with `chr(92)` if you need one.
- **Indentation is mixed per file**: look at the file you edit, or its nearest sibling, and match it.
- **Commits and pushes are automatic** (repo `Calradia Remembers`, branch `main`), once the suites are green:
  `dotnet test CalradiaRemembers.Tests -p:NCrunch=1`. End commit messages with the attribution trailer your harness
  gives you. A commit message says WHY.
- **The maps**: any new test file changes `RULES.md`. Run the suite PLAIN first, read what moved, then
  `.\Tools\Update-Maps.ps1`, and commit the maps WITH the change. Never widen a ratchet; fix the cause.
- **Never touch** the game's Logic or mod code for this goal (`CalradiaRemembers.Logic/AuthoredQuests/*`,
  `CalradiaRemembers/Quests/*`): the other session owns them. If the model or loader needs a change for the editor
  (for instance to expose the closed lists to the schema generator), stop and write it down in your report for Gabriel
  instead. Reading them is fine.
- Never accept a key or a login in conversation. Nothing is published to Nexus by you.

## Working alongside the other session

The other session commits to the same repository and branch, often. To stay out of its way:

- Your files live in `docs/design/quest-editor/`, `CalradiaRemembers/Extension/` (the HTML only) and new test files in
  `CalradiaRemembers.Tests/QuestEditor/`. Touch nothing else, except the `.csproj` line for shipping (below).
- Before every commit: `git pull --rebase`. If the maps conflict, take theirs, then rerun `.\Tools\Update-Maps.ps1` and
  the suite, and commit the regenerated maps.
- If the loader's rules change under you (the other session may add an objective type or a check), your schema test or
  fixture test should go red. That is the guard working: update the schema and the editor, not the test.

## Shipping

The mod's csproj copies `Extension\HOWTO_*.md` into `Modules/CalradiaRemembers/Docs` (`ModderGuideFiles`, around line
195 of `CalradiaRemembers/CalradiaRemembers.csproj`). Add the editor HTML to that item so it ships beside the guide, and
add one line to `HOWTO_Quests.md` section 2 pointing at it. Build with
`dotnet build CalradiaRemembers/CalradiaRemembers.csproj -c Release` (the copy into the game folder fails while the game
runs; that is expected, not an error to fix).

## Done means

- The HTML opens offline, loads `wolf_of_the_wood.json` and all three drafts, round-trips them (load then save gives a
  file the loader reads with the same problems as before), and builds a new two-stage tale from blank that the loader
  accepts with no problem.
- The schema test and the fixture parity are in place and green.
- `twine-import.md` is written, with its open questions.
- A short report to Gabriel, in Quebec French: what was built, how to open it, what is guarded and how, what you could
  not do, and any change you need in the other session's files.

When it is ready, Gabriel may want to try it as a published page before it ships; he will tell you.
