# Goal: branching written tales, and the app to write them

You work autonomously, start to finish, without asking questions. Where a decision is open, take the default given
here, or the most conservative reasonable one, and record it in `docs/design/quest-editor/DECISIONS.md` with the reason.
Gabriel reviews everything when you are done. Read this whole file before you touch anything; "Where you work" is not
optional.

## Who and what

Gabriel is the solo developer of Calradia Remembers (CR), a Bannerlord mod in which characters are voiced by a language
model. He writes in Quebec French; your final report to him is in Quebec French. Everything you write in files (code,
comments, docs, commit messages) is in English.

His request (04/10/2026, translated): "Make an app to compose new quest scenarios easily, saved as JSON, able to load an
existing scenario to modify it, and to load a Twine file to create a new quest. With Twine you can give a quest several
paths, like a choose-your-own-adventure book: the player could decide whether to turn right or left, and depending on
what happens the story takes another direction. CR's quests must support that."

So there are two halves, and you build both:

1. **The mod**: written tales gain choices, several endings, and flags (Twine's variables).
2. **The app**: an offline editor for written tales, with a Twine import.

## What exists today (read these first)

- `CalradiaRemembers/Extension/HOWTO_Quests.md`: the authoring guide; the clearest description of the current format.
- `docs/design/AUTHORED_QUESTS.md`: the design, Gabriel's rulings, and "Branches: a proposal" (written before this goal;
  this goal supersedes its open questions with the defaults below).
- Logic (pure, unit-tested), `CalradiaRemembers.Logic/AuthoredQuests/`:
  - `AuthoredQuest.cs`: the JSON model.
  - `AuthoredQuestLoader.cs`: parsing and the checks (BLOCKING: not loaded; COSMETIC: loaded, reported).
  - `AuthoredQuestRun.cs`: the run state (serialized into the save) and `AuthoredQuestEngine`, the stage machine.
  - `AuthoredQuestPrompt.cs`: what each character and the scene narrator are told; `SceneMarkers` parses
    `[[found:id]]`, `[[hazard:id]]`, `[[meet:id]]` out of the narration.
  - `AuthoredQuestOfferPolicy.cs`: when a quest is offered.
- Mod (engine side): `CalradiaRemembers/Quests/AuthoredQuestBehavior.cs` (load, offer, journal, hourly/daily ticks, scene,
  band, rewards), `CalradiaRemembers/Quests/AuthoredStoryQuest.cs` (journal entry), `CalradiaRemembers/Chat/
  ChatViewModel.Exploration.cs` (the heroless narrator scene in the chat), `CalradiaRemembers/ModCheats.Tales.cs`
  (`cr.story_*`), `CalradiaRemembers/Diagnostics/CrSelfTest.cs` (`authored_quest_opening` walks the werewolf).
- Tests: `CalradiaRemembers.Tests/AuthoredQuest*Tests.cs`.
- Quests: `CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood.json` (ships), `docs/design/quests/*.json` (drafts;
  `smoke_over_the_hills.json` is the one that needs branches).
- SDK (separate repo, `LLM Roleplay Service`, branch `master`): `EncounterContext.AuthoredQuestBlock` carries the mod's
  prompt block for a character; the action interpreter is `NpcMemoryService.Core/Prompts/ActionInterpreterPromptBuilder.cs`.

## Gabriel's standing rules for tales (do not relitigate)

- Characters never lie. What a character must not tell, they do not know, or keep silent about.
- A character is told only what they know AT THIS STAGE, and never what comes next: a twist stays hidden until the player
  uncovers it.
- The model chooses the moment; the game decides whether it counts. A marker or a choice the file does not declare is
  ignored. Nothing the model writes gives gold, starts a fight or harms anyone; the game carries those out.
- The scene narrator speaks plainly, in the second person, and ends by letting the player choose.
- One tale at a time. Rewards are mostly regard and consequences.

## Part 1: the mod supports branching tales

### The format (default design; refine it if you must, and record why)

- **Choices.** A stage may carry `choices` instead of (or as well as) a single `objective`. Each choice has an `id`
  (unique in its stage), a `label` (what the player can do, in plain words), an optional `journal` line, a `next` (a
  stage id or an ending), optional `if` (flags that must hold for the choice to be open) and optional `sets` (flags it
  sets when taken). A choice is taken in one of three ways:
  - **by an act**: it carries an `objective` from the closed list (talk to someone, be somewhere, defeat a band, and a
    new `wait` objective: N days pass with no other choice taken). The first choice whose objective is met wins; the
    others close.
  - **in a scene**: in an `explore` stage, the narrator is told the open choices (label and marker `[[choice:id]]`) and
    writes the marker when the player's words take that path ("you turn left"). Only a declared, open choice counts.
    The scene then closes and the story moves to that choice's `next`. Closing a scene with no choice taken uses the
    stage's own `on_complete.next` if it has one, else the first choice marked `"default": true`; the loader requires
    one of the two.
  - **by words in a conversation** (do this LAST, after everything else is green): a choice with `"said": {"to":
    "{role}", "meaning": "the player promises to keep silent for a price"}` is taken when the action interpreter reports
    it during a conversation with that character. Teach the interpreter only the choices open in THAT conversation
    (declared ids, their meaning), through the existing context, and accept only a declared, open id. This touches the
    SDK interpreter prompt: run the FULL SDK suite (`dotnet test NpcMemoryServiceTests` in `LLM Roleplay Service`),
    and respect the hard budget guarded by `LeanPromptPolicyTests`. If this part cannot be done safely, leave it out,
    and say so plainly in your report.
- **Choices shown like a numbered menu** (Gabriel, 04/10/2026: "like an LLM does in the CLI: Choice 1, Choice 2, Chat
  about that"). Under the narration, the open choices appear as a numbered, clickable list:
  ```
  1. Take the left path
  2. Take the right path
  3. Something else...
  ```
  A click on a choice sends its label as the player's line; the narrator tells it and writes the marker, and only the
  marker counts (the game, not the click, decides). Typing the number alone ("1") in the input is the same as clicking
  it. The last line, "Something else...", puts the cursor in the input: the player writes freely and the scene goes on
  as today. Closed choices (an `if` not met) are not listed. The list disappears once a choice is taken or the player
  writes freely, and comes back under the next narration if choices are still open. Both skins: the Classic chat
  prefab (`CalradiaRemembersChat.xml`) and the Manuscript one (`CalradiaRemembersChat_Manuscript.xml`).
- **Endings.** `"next": "end:<id>"` leads to a named ending in a new `"endings": { "<id>": {...} }` map, each shaped like
  today's `reward` (regard, gold, memory, consequences). `"reward"` stays valid and means the single success ending, so
  every existing file still loads unchanged. `failure` stays the time-out.
- **Flags.** Simple named booleans, set by a choice (`sets`), a finding or a met encounter (`sets` on them too), checked
  by `if` (a flag name, or `!name` for "not set"). Used on: choices (open or not), knower lines (a line may be an object
  `{"text": ..., "if": ...}`; a plain string stays valid), ending consequences, and stage `canvas`/`facts` lines in the
  same object form. No arithmetic, no strings: set and test only.
- **The journal** shows the stage's line, then each open choice's `journal` line when the file gives one.
- **Checks** (loader): every choice id unique; every `next` resolves to a stage or an ending; every ending referenced
  exists, and every ending defined is reachable (cosmetic if not); every stage can reach some ending (blocking if not;
  loops are now allowed when an exit exists); a flag tested but never set anywhere is cosmetic; an `explore` stage with
  choices needs `on_complete.next` or one `default` choice; the existing checks keep working.
- **Saves.** The run state is serialized into the save. Add fields only; a run saved before this change must load and
  play on (test it). Additive save keys only, if you need new ones; never rename or remove one.

### How to build it

Test first, in pure Logic, then the engine. Suggested increments, each committed when green:

1. Model + loader + checks for choices, endings, flags (tests with the stake of each rule).
2. Engine: choices by act (including `wait`), flags, endings; journal lines; run serialization old and new.
3. Prompts: what the narrator and characters are told of choices and flags (never the future, never a closed choice).
4. Scenes: `[[choice:id]]` in `SceneMarkers`, the scene closing onto the chosen path, the numbered choice list in both
   skins (a pure Logic piece decides what the list holds and what a typed "1" means; test it first).
5. Rewrite `docs/design/quests/smoke_over_the_hills.json` with its three outcomes (expose to the lord, confront the
   rival, keep silent) as the worked example; and a small branching tale of your own in the werewolf's spirit (a turn
   left or right in a scene) to exercise scene choices. Both stay in `docs/design/quests/` for Gabriel to review; do
   not ship them in `ModuleData/Quests`.
6. `cr.testall`: a new self-test that loads a small branching tale embedded in the test (not a shipped file), drives
   the real handling through debug seams (an act choice, a scene choice by marker, a flag gating a choice, an ending),
   and cleans up after itself. Model it on `authored_quest_opening` in `CrSelfTest.cs`.
7. Conversation choices (above), last.
8. Update `HOWTO_Quests.md` (choices, endings, flags, the editor, the Twine import) and `AUTHORED_QUESTS.md` (the final
   design, replacing "Branches: a proposal").

The `smoke_over_the_hills` draft also wants things not in this goal: a `{rival}` role (a clan leader of the giver's
realm), and a grudge between two lords. Add the `{rival}` binding if the example needs it to load (with tests); leave
the NPC-to-NPC grudge out and note it in DECISIONS.md.

## Part 2: the app

A single self-contained HTML file, offline (no server, no install, no CDN at run time), shipped beside the guide in
`Modules/CalradiaRemembers/Docs/`. For players and modders: English interface, plain words, no JSON knowledge assumed;
match the guide's tone.

1. **Open** a quest `.json` (one quest or an array), start from the werewolf as a template, or from blank.
2. **Edit** every part with forms: header, requires, binds, stages (objective forms that change with the type), knowers,
   scenes (opening, findings, hazards, encounters, closing), choices (all three kinds), flags, endings, failure. Add,
   remove, reorder. Show each field's one-line explanation from the guide; mark fields read but not yet played.
3. **See the story as a graph**: stages and endings as boxes, choices as labelled arrows, a broken or dead-end path in
   red, an unreachable stage greyed. Clicking a box opens it.
4. **Check live**, with the loader's own wording, BLOCKING and COSMETIC apart, each pointing at its field.
5. **Save** as pretty-printed JSON (two-space indent, the loader's key names). What it saves must load in game with no
   problem the app did not show.
6. **Import Twine**: a Twine 2 published story (`.html`, `tw-storydata` / `tw-passagedata`) and Twee 3 (`.twee`).
   Passages become stages (or scene beats, when tagged), links `[[label->target]]` become choices, passage tags give the
   objective (`talk_to:giver`, `keep_watch:village`, `explore`, `defeat_party`...; document the convention), and simple
   variables become flags (Harlowe `(set: $x to true)` and `(if: $x)`, SugarCube `<<set $x to true>>` and `<<if $x>>`).
   Everything the import cannot map is listed for the author, never dropped silently. The result opens in the editor
   with its problems shown.
7. Put a short "Writing tales in Twine" section in `HOWTO_Quests.md` with a tiny example story and its tags.

### The one real risk: two validators drifting apart

The game's checks are C#, the app's JavaScript. If they disagree, the app says "all good" and the game refuses the
quest. Guard it structurally:

- **A JSON Schema generated from the C# model** (`docs/design/quest-editor/quest.schema.json`; fields, types, closed
  lists as enums), kept current by a test with the "regenerate with CR_UPDATE_MAPS=1" pattern of
  `CalradiaRemembers.Tests/Maps/MapsAreCurrentTests.cs`. The app takes its fields and enums from it (embedded in the
  shipped HTML, so it works offline; regenerate the embedded copy as part of the same step).
- **A shared corpus of fixtures** (`docs/design/quest-editor/fixtures/<name>.json` + `<name>.expected.txt`), covering
  every rule, the new ones included. A C# test runs them through `AuthoredQuestLoader.Parse`; the same fixtures run
  through the app's validator under Node (Node 26 is installed), called from a C# test, so the two cannot disagree
  silently. Keep the validator as its own JS module inside the HTML so the Node run tests the exact shipped code.
- Twine import gets its own fixtures (a Harlowe story, a SugarCube story, a Twee file) with their expected quest JSON.

## Behaviour tests, as this repository writes them (Gabriel, 04/10/2026: "as we do it")

Read `CalradiaRemembers/CLAUDE.md`, sections "Unit tests carry their WHY" and "What a test is for", before writing the
first test. In short, Gabriel's ruling: tests protect the behaviours the world expects, not the code. The code must stay
free to change; a behaviour must never degrade.

- **The expectation comes from outside the code**: a player's or an author's view, Gabriel's words, the guide. Write the
  test before the code, and write its name so it could have been written before the code existed. The THEN names what
  someone would notice, never a return value, a flag, a branch or a method.
  - Good (from `AuthoredQuestEngineTests.cs`): `GIVEN_a_scene_closed_before_a_required_finding_WHEN_closed_THEN_the_missed_finding_is_returned_to_be_said`,
    `GIVEN_the_wrong_person_place_or_party_WHEN_reported_THEN_the_stage_does_not_move`.
  - Bad: `GIVEN_choice_list_WHEN_Resolve_called_THEN_returns_null`.
- **Every test file** opens with a header saying what the policy decides, what its answer feeds, and what breaks if it
  is wrong. **Every test** carries a comment above it with its STAKE: finish the sentence "if this fails, a player (or an
  author) ...". Never invent a rationale; if a boundary has no derivable reason, say plainly what it pins.
- **Facts objects, not rows of positional arguments**, so a test names only the facts it cares about and adding a fact
  costs no rewrite (see `AuthoredOfferFacts` and its tests). Run `Tools/Find-SwapRisk.ps1` after adding a policy: it
  must stay at zero.
- **Silent on a refactor, red on a behaviour change.** Do not test a method's shape or an overload's agreement with
  another. If you restructure and an assertion about something a player would notice has to change, stop: that is a
  behaviour change, and it goes in DECISIONS.md.
- **Build the subject the way production builds it** (the real loader on real JSON, the real engine on a real run), not
  a hand-made object that skips the path the game takes.

The behaviours to cover, at least (one or more tests each, named from the player's or author's side):

- Choices: the first act met decides and the others close; a closed choice (its `if` not met) can never be taken, by
  act, marker, click or words; a choice taken sets its flags; a scene choice counts only by its marker, never by the
  click alone; an undeclared or closed marker changes nothing; a scene closed with no choice goes to its declared
  default; a typed "1" is the first OPEN choice, not the first declared one; a number with no such choice is free text.
- Endings: each path reaches its own ending with its own regard, memory and consequences; `reward` still means the one
  success ending of an old file; the time-out still fails the tale.
- Flags: a knower line, canvas or fact under `if` reaches the character only when its flag holds; `!flag` the reverse;
  a flag never set is reported to the author.
- Secrecy: no character or narrator is told a closed choice, a later stage, or a flag-gated line whose flag is not set.
- Loader: every new check (unreachable stage, dead end, unknown ending, duplicate choice id, a scene with choices and no
  way out) is reported in plain words; every file that loaded before loads the same.
- Saves: a run saved before this change loads and plays on; a branching run survives save and load mid-path.
- App: the parity fixtures (C# and Node) cover each loader rule; each Twine fixture imports to its expected quest; a
  macro the import cannot map is listed for the author, never dropped.

In `REPORT.md`, list these behaviours with the test name(s) that guard each one, so Gabriel can read the rules from the
tests, as he does.

## House rules (non-negotiable)

- **Tests first**, each with a comment above it giving the STAKE (what goes wrong for a player if it fails), never
  "this test verifies". Gherkin names (`GIVEN_..._WHEN_..._THEN_...`). Read `CalradiaRemembers/CLAUDE.md`, "Unit tests
  carry their WHY" and "What a test is for". Never invent a rationale.
- **No em dashes** in anything you add. Before each commit, count the added lines holding the em dash character
  (U+2014) in `git diff --cached`, and commit only when the count is 0. The Write/Edit tools turn a typed unicode
  escape into the character itself; build escapes in Python with `chr(92)` if you need one.
- **Indentation is mixed per file**: match the file you edit, or its nearest sibling.
- **The maps** (`docs/maps/`, `RULES.md`): run the suite PLAIN first, read what moved, then `.\Tools\Update-Maps.ps1`,
  and commit the maps WITH the change. Never widen a ratchet (namespace cycles, inline regard comparisons, bare
  `IsAlive` reads...): fix the cause. A new namespace that joins a cycle is fixed by placing the code elsewhere.
- **Every engine-bound behaviour ships a cr.testall** self-test (see part 1, step 6).
- **MCM hint texts** are at most 250 characters, if you add a setting.
- Commit messages say WHY, end with the attribution trailer your harness gives you.
- Never accept a key or a login in conversation. Publish nothing to Nexus or anywhere else.

## Where you work (read twice)

Gabriel is about to cut release 2.7.0 from `main` in another session. Your work must not land in it, and must not
disturb the game he is testing.

- **A branch, in separate worktrees.** Create `quest-branches` in BOTH repos and work in worktrees that keep the two
  repos side by side (the mod finds the SDK at `..\..\LLM Roleplay Service` from its csproj):
  ```
  V:\OneDrive\Programmation\GIT Workspace\Bannerlord LLM project\_quest-branches\Calradia Remembers
  V:\OneDrive\Programmation\GIT Workspace\Bannerlord LLM project\_quest-branches\LLM Roleplay Service
  ```
  (`git worktree add -b quest-branches "<path>"` from each repo.) Commit and push to `quest-branches` freely. NEVER
  commit to `main` or `master`, never merge, never rebase their history.
- **Never deploy into the game.** Build and test with `-p:NCrunch=1` always (it skips the copy into the game folder):
  `dotnet build CalradiaRemembers/CalradiaRemembers.csproj -c Release -p:NCrunch=1` and
  `dotnet test CalradiaRemembers.Tests -p:NCrunch=1`. A plain build would overwrite the DLLs Gabriel is playing with.
- **Keep up with main.** From time to time, `git fetch` and `git rebase origin/main` (`origin/master` for the SDK) onto
  your branch. On a map conflict, take main's map, rerun `.\Tools\Update-Maps.ps1` and the suite, commit the result.
- Both suites green before every push: `dotnet test CalradiaRemembers.Tests -p:NCrunch=1` in the mod worktree, and
  `dotnet test NpcMemoryServiceTests` in the SDK worktree whenever you touched the SDK.

## Done means

- Every existing quest file loads exactly as before; a run saved before your change loads and plays on.
- A branching tale (the rewritten smoke_over_the_hills, and your scene-choice tale) loads with no problem, and the
  engine walks every path in unit tests; the new cr.testall walks the engine-bound half.
- The app opens offline, round-trips all quest files (load, save, reload: same problems), builds a branching tale from
  blank that the loader accepts, shows the graph, and imports the three Twine fixtures.
- The schema test and the fixture parity (C# and Node) are green.
- `HOWTO_Quests.md`, `AUTHORED_QUESTS.md` and `DECISIONS.md` are up to date.
- A report to Gabriel, in Quebec French, written to `docs/design/quest-editor/REPORT.md` and given as your final
  message:
  - what was built, commit by commit, in both repos;
  - every default decision you took (from DECISIONS.md), so he can overturn it;
  - what is guarded and how;
  - what was NOT done or not safely possible (say it plainly);
  - what only a game session can confirm, as a short play-test list, and how to try the app;
  - how to merge when he is satisfied (the branch names; he decides).
