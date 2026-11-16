# The maps: views of the system, generated from its own source

Gabriel, 21/09/2026: *"Plus le projet grossit, plus c'est difficile pour moi de voir l'ensemble du systeme... si on
implemente une regle dans le code, je ne vois pas si on est en train de creer un doublon parce que le concept existe
deja ailleurs."*

Nothing in this folder is written by hand except this file. Every map is regenerated from the source by a test, and
the test FAILS when the committed copy no longer matches the code. That failing test is the trigger a pipeline would
have been: this mod cannot be built on a CI server (it needs the game's DLLs), but the suite runs on every change and
nothing is committed until it is green.

**Explaining the method to someone outside this project?** There is a write-up of the approach itself (the
problem it answers, the trigger, what it does not solve, and how to start) at
https://claude.ai/code/artifact/77578734-d506-4fee-8b87-f715be8dc307 . It is written for colleagues working
with AI on other code bases, and it is a shareable page rather than a file in this repo, so it carries no
project internals.

## What to read, and when

| Map | Read it to answer |
|---|---|
| [`inventory.md`](inventory.md) | Does this concept already exist, and where does it live? Every type, by layer and namespace, with one sentence each. Also the files of 1000 lines and more. |
| [`dependencies.md`](dependencies.md) | Who knows whom? Cycles first, then who uses the most and who is used the most, then the pairs that use each other with the files that make each side (where to cut a cycle), then every edge. |
| [`placement.md`](placement.md) | Is this namespace one module or several, and does this type live where it is used? Types whose users are mostly in one other namespace (the cheapest cut for a cycle), then how many parts each namespace falls into. |
| [`../../RULES.md`](../../RULES.md) | What does the MOD promise to do? Every test name as a sentence, with each fixture's reason for existing. |
| [`rules-sdk.md`](rules-sdk.md) | The same for the SDK. |
| [`constants.md`](constants.md) | Is there already a number for this? Opens with the NAMES declared in more than one type. |
| [`engine-facts.md`](engine-facts.md) | Is this CONCEPT already somewhere, under another name? Which fact of the game each pure rule reads, and the facts read by more than one rule (rank was a prestige score, a stature, a station and a tier: all of them read `Clan.Tier`). Read from syntax, so keys are best guesses. |
| [`actions.md`](actions.md) | Where does a character act toward the player (a letter, an audience, an interception, a threat), and does each path ask the one gate (alive, of age, free, not the player)? Also every letter written straight into the post. |
| [`settings.md`](settings.md) | Who obeys this MCM switch? Every setting with every member that reads it, rules and glue alike, then the settings read in ONE place (a broad promise kept by one road) and those read by no road of play. Read it before adding a road that does what a setting governs, and when a player says "I turned it off and it still happens": the road doing it is the one missing from the list. engine-facts.md cannot show this: it sees only the values that reach a pure rule. |
| [`event-listeners.md`](event-listeners.md) | What does the mod hear from the engine, and which of an event's reasons does each listener name? |
| [`prompt-sections.md`](prompt-sections.md) | In what order is the prompt written, what is cached, what is per-turn, what does Compact drop? |
| [`prompt-pipelines.md`](prompt-pipelines.md) | Where is the model called (letters, envoys, the council, summaries, strategic reports), what writes each prompt, and which editable `ModuleData` file reaches which? Shipped to players as `Docs/PROMPT_PIPELINES.md`. |
| [`verbs.md`](verbs.md) | Is this action declared everywhere it has to be? |
| [`gates.md`](gates.md) | What OPENS this verb, what the bridge DEMANDS of it, and which other verbs can change the answer between the two? The pairing lens: a teaching gate is resolved when the context is built, a bridge guard runs when the action is dispatched, and the turn's other actions run in between. |
| [`composition-parity.md`](composition-parity.md) | Is each action taught to BOTH readers of a conversation, the prose model (Standard) and the interpreter (Enhanced)? Both prompts rendered by the real builders over four scenarios; a verb taught to one reader only is refused unless written down with its reason (`CompositionParityTests`). |
| [`shared-state.md`](shared-state.md) | What outlives the call that wrote it, and so has a lifetime somebody must decide? |
| [`glue-decisions.md`](glue-decisions.md) | Where does the engine glue decide for itself? Every fact of the game (a member read off an object, a `Get...` call) compared with a bare number outside `Logic`, Diagnostics and the console: Tell Don't Ask in this project's terms (the decision belongs in a pure policy). Ratcheted: a new one fails `RatchetTests`. Two lines comparing the same fact with the same number in two places (a threshold written twice, agreeing today, free to drift) are the first thing to look for. |
| [`decisions-regard.md`](decisions-regard.md) | **The two-minute view.** What does each rung of the regard scale actually decide? Real calls, swept from -100 to +100, plus where each rule turns. |
| [`save-format.md`](save-format.md) | **Contract.** What does the mod write into a player's campaign? Every save key and every saveable type id. |
| [`modder-api.md`](modder-api.md) | **Contract.** What are other people's mods built against? Every public member under an `Extension` folder. |

`decisions-regard.md` is the only one that RUNS the code rather than reading it, so a changed cell there is a changed
behaviour, not a moved declaration. It is also the safety net for giving regard a type: that refactor must not move a
single cell. Its last section counts the rules it can sweep against the thresholds that exist, and that gap is the
measure of the "fetch the value and compare it yourself" habit. It should shrink.

The last two are not views, they are promises made to people outside the project: a player with a campaign in
progress, and a modder with a bridge already shipped. A MEMBER that shipped and is removed from `modder-api.md` is
refused the same way (since 23/09/2026, see below): the guard compares with `modder-api-released.md`, the surface as
last RELEASED, so a member added and withdrawn between two releases passes freely. A KEY removed from `save-format.md` is the one change a
regeneration will not absorb: the suite refuses it, update mode included, until it is declared on purpose with
`CR_ALLOW_SAVE_KEY_REMOVAL=1` and explained in the commit. Gabriel's hard rule is additive keys only, because a
renamed key is a player's data silently never read again, and nothing fails when it happens.

`gates.md` is the only one that pairs two halves of the same decision rather than listing one thing. It exists because settling whether a single audit line meant anything took five separate reads on 22/09/2026, and because the fact that actually mattered, that another verb in the same turn can invalidate a verb's teaching gate, was in none of them.

The first two are the generic net: a mechanism nobody planned a map for still adds a type or an edge. The rules are
the behaviour index: a change inside a method leaves no structural trace, but a behaviour that matters is held by a
test. The last five are lenses, one per family of bug this project has actually had; each generator's header names
the player report it answers.

## Which tests INFORM, and which BLOCK

They are not all the same kind of test, and treating them as one kind is how this stops working.

**Informing.** `inventory`, `dependencies`, the two rules files, `constants`, `event-listeners`, `prompt-sections`,
`prompt-pipelines`, `verbs`, `gates`, `shared-state`, `glue-decisions`. These fail, you regenerate, you read the diff, you commit. Whatever the code now says
becomes the new truth. Their whole value is the two seconds of attention the diff buys, and that value decays: the
real risk here is not a wrong map, it is a reflex. Regenerate-and-commit becomes muscle memory within weeks, and then
these are green paperwork.

**Blocking.** `save-format`'s vanished-key guard, the modder API's vanished-member guard, and the ratchets in
[`RatchetTests`](../../CalradiaRemembers.Tests/Maps/RatchetTests.cs) (cycles, mutual pairs, regard thresholds, bare
`IsAlive`, coin and goods moves, relation touches and, since 14/11/2026, the glue's decisions against a bare number and
the swap risk: public members of `Logic` taking three same-typed parameters in a row, the suite's copy of
`Tools/Find-SwapRisk.ps1`). These do NOT accept what the code now says.
Regenerating does not make them green; only changing the code, or writing down on purpose why the rule no longer
holds, does.

- A save key that vanished. Every campaign in progress loses that data on its next load, and nothing else notices.
- A modder API member that SHIPPED and vanished (or changed signature, which is the same thing to a compiled caller).
  A bridge already on Nexus breaks on a player's machine, and the report reaches its author, not us. Built on
  23/09/2026 after `ExternalWorldEvent.PlayerIsActor` left the API map with no sound at all: harmless that time (it had
  never shipped), silent all the same. Demonstrated the same day by renaming `PlaceId` to `Place`: a plain
  regeneration was refused with "These public members shipped in v2.5.23 and are no longer there: ExternalWorldEvent:
  string PlaceId { get; set; }". The pinned decisions are in `ModderApiGuardTests`.
- A namespace newly caught in a dependency cycle. Every other structural change can be an improvement; this one
  cannot. Inside a cycle nothing can be understood, tested or moved alone.
- A regard threshold newly written as a bare number. That is the duplication this whole folder exists to catch,
  in its most literal form: one more private copy of a rung that already has a name.

The two ratchets hold a BASELINE, not a standard: 46 namespaces are in a cycle today and 74 of the 91 regard
thresholds are bare numbers. Neither is acceptable; both may only shrink. When one does, its test says so and asks
for the baseline to be lowered in the same commit, so the mess being worked off cannot quietly become the target.

`decisions-regard.md` sits between the two. It is an informing map by mechanism, but a changed cell in it is a
changed behaviour, so in practice a diff there is read like a blocking failure.

## When a Maps test fails

It is never a bug in the code under test. The code moved and the map did not.

```powershell
.\Tools\Update-Maps.ps1
```

It regenerates every map and then PRINTS what moved: the counts for each map, and the full diff of any change short
enough to read (40 lines or less), which is most of them. Read that, then commit the maps with the change. Under the
hood it is `CR_UPDATE_MAPS=1 dotnet test CalradiaRemembers.Tests -p:NCrunch=1 --filter "FullyQualifiedName~Maps"`; the
script exists because that env var cannot be set inline in PowerShell, and because NCrunch cannot regenerate at all
(it works from a workspace copy, so the Maps tests are Ignored there). Use `dotnet test`, or this.

If a SAVE KEY disappeared, the script refuses and rewrites nothing: see the contract note above, and re-run with
`-AllowSaveKeyRemoval` only when you mean it. A SHIPPED API MEMBER that disappeared is refused the same way: put it
back (mark it `[Obsolete]` and keep it working if it must go), or re-run with `-AllowApiRemoval` and name it under
"For modders" in the release notes.

**At every release cut**, after bumping `SubModule.xml`, run `.\Tools\Update-Maps.ps1 -RecordReleasedApi`. It records
the surface being shipped in `modder-api-released.md` with its version, and that becomes what the guard protects. A
plain regeneration never touches that file: if it did, it would absorb the very removal it exists to refuse.

A RATCHET failure is not fixable this way at all. The script will report it and leave it red, because there is no
copy to bring up to date: either the edge is broken and the namespace leaves the cycle, or the threshold points at a
named rung, or the baseline is raised by hand with the reason in the commit message.

Then commit the regenerated map WITH the change that moved it. Its diff is the architectural summary of that change:
a new threshold appears beside the ones that already exist, a new edge between two namespaces is one line, a prompt
section that moved across the cache marker changes its `Zone`. Read the map's diff before the code's.

An unchanged map is information too: the dimension it watches did not move.

## What the maps cannot see

- A behaviour that changed with no test changing. That is not a gap in the maps; it is a missing test.
- Dependencies are read from `using` directives. A fully qualified name is missed and an unused import is counted.
- `event-listeners.md` names the reasons a listener's FILE mentions; it does not know the engine's full list, so it
  shows "names one of them", not "names one of seven".
- `shared-state.md` lists where a lifetime has to be decided. It cannot tell which value is stale.
- These tests are Ignored under any runner that works from a COPY of the sources, NCrunch included. A workspace
  copy carries a project's sources, because those are project items; it carries neither `.git` nor the maps
  themselves, which belong to no project. So such a runner can regenerate a map perfectly and have nothing to
  compare it with. The check is for `.git` in both repositories, which needs no knowledge of the runner. Run
  them with `dotnet test`, or `Tools/Update-Maps.ps1`.

- A map that finds things by their NAME is wrong in both directions (26/09/2026). Loudly, when a name misleads it
  into a false positive (the regard ratchet counted four regard DELTAS as thresholds; the fix was better names).
  Silently, when something it should see has no such name (33 regard thresholds compared inline, with no constant,
  were invisible to the name-based list until `decisions-regard.md` also read what the code COMPARES). Pair a
  name-based detector with one that reads a fact wherever a fact exists.

## Adding a map

A generator in `CalradiaRemembers.Tests/Maps`, one test in `MapsAreCurrentTests`, one row in the table above. Add
one when a family of bug has shown itself twice, not before: a map nobody reads is one more thing to regenerate.

**A map that mixes detection modes says, for every line, which one produced it** (Gabriel, 26/09/2026). A fact (a
call, an attribute, an import, a value actually compared) and a name (a constant called `*SaveKey*`, a local called
`regards`) are not equally sure, and a reader deciding what to trust needs to know which is which. Today:
`save-format.md` marks each key `(fact)` or `(name)`, and a key both find is labelled by the fact;
`decisions-regard.md` marks each inline comparison `ledger` or `named`; `gates.md` marks each guard `[the caller's
if]` or `[early return naming context]`.
