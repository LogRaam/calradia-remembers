# Prompt Composer: CR's prompts in the hands of power users and modders

Status: DESIGN AGREED 11/10/2026 (Gabriel: "fait les choix logiques pour laisser le plus de liberte. Un param MCM pourrait
designer l'ordonnanceur a utiliser, car il pourrait y avoir plusieurs mods qui proposent un ordonnanceur. L'editeur dans
Docs. Mise en page par type."). Nothing built yet. The decisions are in "Decisions" at the end.

## Why

Gabriel (11/10/2026): "Est-ce que on pourrait sortir tous les prompts de CR vers des fichiers configurables? Et aussi
creer un ordonnanceur qui renseignerait quels blocs de prompts utiliser et dans quel ordre? Comme ca, n'importe qui qui
est un power user ou un moddeur pourrait modifier ou refaire la composition des prompts de CR." And: "Je compare ca avec
des gens qui montent leurs propres PC et qui font de l'overclocking. Donner la possibilite aux gens d'experimenter permet
de faire de CR le seul mod qui donne le controle aux superusers. De plus, des power users pourraient apporter de reelles
ameliorations. Et puis un moddeur qui a un mod overhaul pourrait etre tente de modifier les prompts de CR a sa sauce. Oui
il y a des sections qui ne pourront pas etre enleves lorsqu'ils sont juges critiques." And the tool: "un editeur HTML qui
fait un graph de la sequence et qui peut editer la sequence ou un element prompt de la sequence serait un bon outil pour
mieux guider les utilisateurs. C'est la qu'on pourrait donner des infos, trucs et astuces, pour la conception de prompts."

## Principles

1. **Stock is the reference.** CR ships its own layout and texts. With nothing customised, the prompt sent is identical,
   character for character, to the one sent before this work. A test proves it; without that, no report could ever be
   told apart from a customisation.
2. **Overclocking is visible.** When anything is customised, the log, `cr.prompt_dump` and the editor say so and list
   what. One MCM switch returns to stock instantly, without deleting anyone's files.
3. **The words move out; the decisions stay in.** Whether a section appears, and the facts it is built from, are game
   state and stay in code. Its wording becomes a file with `{{variables}}`. Nobody is asked to write C# in a text file.
4. **Some sections are locked.** The ones the game, the cache or the player's safety depend on cannot be removed (and
   some cannot be moved or reworded). Gabriel fixes the list.
5. **The editor is the guide.** It shows the sequence, explains each section, carries the prompt-craft lessons learned
   the hard way, and checks a layout exactly as the game does.
6. **Quality first** (standing ruling): nothing here may change what the stock prompt says.

## What exists today (facts, 11/10/2026)

- `PromptBuilder.BuildSystemPrompt` (SDK, `Prompts/PromptBuilder.cs:195-395`) is ONE flat method: about 200 statements,
  each an `AppendX(...)` or an `if (gate) AppendX(...)`. The method body IS the order. `docs/maps/prompt-sections.md`
  lists its 111 sections (85 before the cache marker, 25 after), generated from the source by Roslyn.
- **No section has a stable id.** The only enum, `PromptSection` (`LeanPromptPolicy.cs:27`), names the 8 sections Lean
  may drop. The map keys on C# method names.
- The cache marker is the literal `CURRENT ENCOUNTER:` written by `AppendEncounterContext` (`PromptBuilder.cs:340`);
  `PromptCacheSplit` splits on it. Above it is cached; below it is re-sent every turn.
- **Already modder-owned text** (the pattern to generalise): `world.txt`, `behavior_guidelines*.txt`,
  `player_description.txt`, `post_history_instructions.txt`, `heard_now.txt`, `council_seat.txt`, narrative styles.
  The mod reads them (`Mapping/NarrativeLoader.cs`): the player's Custom folder first (MCM path, else
  `ModuleData/Custom`), then each enabled mod's `ModuleData/cr_patch/<same file>` in launcher order, composed by
  `NarrativePatchComposer` (append by default; `!replace`, `!remove <text>`, `!block <People>`, `!#` comments). The SDK
  does no file I/O: it receives the composed strings.
- `{{variables}}`: `PromptVariableExpander` at build time; built-ins win over third-party ones
  (`CrPrompt.RegisterVariable`); an unknown token is left as written.
- How sections make their text (a 15-section sample plus signatures, an estimate): about 60 to 70% are fixed paragraphs
  with a few interpolated names, gated by one condition (most offers, proposals and teachings); 20 to 30% are built
  from loops and branches over data (`AppendIdentity`, `AppendHistory`, `AppendWitnesses`, quests, letters, companions);
  a handful are pass-throughs of the files above.
- Other prompts: `BuildCommonerSystemPrompt` (shares 6 sections), `ActionInterpreterPromptBuilder` (the second call,
  its own text, mirrors the parser's format), `CouncilPromptBuilder`, `EnvoyPromptBuilder` (no shared sections),
  `LetterPromptBuilder` (reuses `BuildSystemPrompt` whole), the chronicle and the compressors/summarisers (own prompts).
- Guards today: `LeanPromptPolicyTests` (Lean prompt under 7,400 chars), `LeanRealPromptBudgetTests` (with the real
  action catalogue), `PromptCacheTurnStabilityTests` (nothing per-turn above the marker), the map. **There is no golden
  (snapshot) test of a whole prompt.**
- `cr.prompt_dump [name]` writes the real prompt to `Logs/CalradiaRemembers_prompt_<name>_<id>.txt`, with no section
  boundaries.

## Scope

First: **the character prompt** (`BuildSystemPrompt`), the one people will want to tune; letters inherit it. Then the
commoner prompt (its own sections plus the 6 shared). The action interpreter, council and envoy prompts later, the
interpreter mostly locked (it is a format contract). The housekeeping prompts (summaries, compression, extraction) stay
out: no player reads them and their output is parsed.

## The model

### Sections

Every section gets a **key**: snake_case of its method (`AppendIntimacyConsentRules` -> `intimacy_consent_rules`).
A key is a contract, like a save key: once shipped it is never renamed (a ratchet map guards it, as the modder API map
does). Each section declares, in code:

- `zone`: `prefix` or `tail` (the marker is not a section; it is the boundary).
- `default position` in the stock layout.
- `gate`: when it appears (a description for the editor; the condition itself stays code).
- `lean`: kept, dropped, or level-aware.
- `lock`: see below.
- `kind`:
  - **template**: its whole text is a file with `{{variables}}` (most sections).
  - **framed data**: code renders the rows (a history, the witnesses); the frame around them (heading, introduction,
    closing instruction) is a template with `{{rows}}`. Row wording can follow later, section by section.
  - **pass-through**: one of today's files (`world.txt`...), unchanged.
- `variables`: the section's own (`{{npc_name}}`, `{{offer_amount}}`...) besides the global ones (`{{user}}`,
  `{{char}}`...), each with a one-line meaning for the editor.
- `doc` and `tips`: why it exists, what goes wrong without it, and prompt-craft advice.

To make this real, `BuildSystemPrompt` becomes a walk over a **section registry** (key, zone, gate, render) in layout
order, instead of a hard-coded sequence. The stock layout is the current order. That refactor is the riskiest step and
is done with no other change, guarded by golden prompts (increment 0).

### Prompt profiles (the "ordonnanceur" a player chooses)

Several mods may each offer their own way of composing CR's prompts, so a composition is a named **profile**, and the
player picks ONE in MCM ("Prompt profile": a dropdown listing every profile found). A profile is a folder:

```
Prompts/
  profile.json            name, author, description, based_on (the CR version it was made from)
  layouts/<kind>.json     the order and the sections off, per kind of character (see below)
  sections/<key>.txt      the reworded sections (only those it changes)
```

Where profiles are found:

- **Stock**: CR's own `ModuleData/Prompts/`. Always listed, always complete. The default.
- **Mods**: each enabled mod's `ModuleData/cr_prompts/<profile id>/`, one or more per mod.
- **The player's**: `Custom/Prompts/<profile id>/` (the MCM Custom path, else `ModuleData/Custom`), which CR updates never
  touch.

Inside the chosen profile, anything it does not provide falls back to stock: a section it does not reword uses the stock
text, a kind it has no layout for uses its own `default` layout, else stock's. So a profile can be one reworded section
or a total overhaul. A player who likes a mod's profile but wants to change one line copies it to Custom (the editor does
it in one click) and selects the copy. A profile can name a `parent` profile in `profile.json`, and falls back to the
parent before stock, so a small tweak need not copy a whole overhaul.

A missing or broken chosen profile falls back to stock, with a notice in the log and on screen. Stock is never more than
one MCM switch away.

### Texts

Stock texts ship as files, one per section: `ModuleData/Prompts/sections/<key>.txt`. The texts move out of
`PromptBuilder` verbatim, and the golden prompts prove nothing changed. A profile's section file REPLACES the stock text
(a section is rewritten, the most freedom); the existing directives remain available: `!append` adds to the stock text
instead, `!remove <text>` drops lines, `!#` is a comment the model never sees.

### Layouts, by kind of character

One layout per kind, chosen by who is speaking. The kinds, most specific first (the first that fits is used, then
`default`):

- `spouse`
- `companion`
- `captive_scene` (a captive or captor scene)
- `gang_leader`
- `notable`
- `wanderer`
- `lord`
- `default`

The commoner prompt has its own builder and gets its own `commoner` layout once it is in scope. The kinds follow the
stations CR already distinguishes for the behaviour guidelines; which kind applies is decided by the engine, as the
station is today.

`layouts/<kind>.json`:

```json
{
  "based_on": "2.8.0",
  "prefix": ["adult_framing_preamble", "format_instructions", "dialogue_style", "identity", "...", "narrative_style"],
  "tail":   ["who_sought_whom", "...", "role_anchor", "format_reminder", "post_history_instructions"],
  "off":    ["prose_craft", "relationships"]
}
```

Loading a layout checks it (the same checks run in the editor):

- **Unknown key**: reported, ignored.
- **A section missing from the layout**:
  - a section CR has added since `based_on` is inserted at its stock position, with a notice;
  - any other missing section is treated as `off`, unless locked.
- **A tail section listed in the prefix: allowed, with a strong warning.** A per-turn fact above the marker breaks
  the cache on every reply, so every reply costs the full prompt. The editor shows that cost. It is the player's money
  and the player's choice; it never breaks the game.
- **A prefix section listed in the tail: allowed, with a warning.** It costs cache, not correctness.
- **Locked sections**: see below.
- **Lean**: the layout applies to both levels. Lean still drops what Lean drops. A warning appears if a custom Lean
  prompt measures over its budget at build time (logged once per session).

### Locks

Three levels:

- **required**: cannot be turned off; can move within its zone; text editable.
- **pinned**: cannot be turned off or moved.
- **sealed**: pinned, and its text cannot be overridden.

The list, chosen for the most freedom (Gabriel, 11/10/2026): a lock exists only where the GAME would break, or where
the player's safety is at stake. Everything else is free, with a warning where it costs something.

| Section | Lock | Why |
|---|---|---|
| `format_instructions` | sealed | the parser's contract (`[DIALOGUE]`, `[ACTION]`, `[NARRATION]`...): reworded, the game stops understanding replies |
| `intimacy_consent_rules` | sealed | consent: a protection, not a matter of style |
| `narration_frequency` | sealed | the captive-scene counterpart of the consent rules |
| `format_reminder` | required | the same contract at highest recency; its wording may change, it cannot go |
| `role_anchor` | required | who is who; without it the model speaks as the player |
| `stay_within_what_you_know` | required | the anti-confabulation wall |
| `claims_are_not_proof` | required | no reward or regard for an unproven claim (the game acts on it) |
| `player_action_narration` | required | fate, not narration, resolves a captive's escape (the game acts on it) |
| the encounter context (marker) | not a section | the cache boundary itself |

Everything else, `adult_framing_preamble` included, can be moved, reworded or turned off. The editor's tips say what
each costs (the preamble, for instance, weighs most when it is first).

Character age is not a prompt section: who may act toward the player is decided in the engine
(`Mapping.ActionGate`), which no layout touches.

### Overclocking is visible

- At session start, one log line: `[PROMPT] custom prompt active: layout from <source>; N sections reworded (<keys>);
  M off (<keys>)`. Stock: `[PROMPT] stock prompt`.
- `cr.prompt_dump` heads its file with the same line; `cr.prompt_dump <name> sections` marks each section's start
  (`<<< identity >>>`), for the editor's preview.
- `cr.prompt_check`: lists every problem in the chosen profile, and every **drift** (below).
- MCM: "Prompt profile" (default: stock). Choosing stock is the instant reset; no file is deleted. The player-report
  skill learns to ask for the `[PROMPT]` line first.

### Drift between versions

A custom text is a copy of a stock text that may improve later. Each custom file can carry `!# based_on: <key>@<hash>`
(the editor writes it). When the stock text's hash no longer matches, `cr.prompt_check` and the editor say "CR has
changed this section since you copied it" and offer the new stock text beside the custom one. Nothing is changed for the
player.

## The editor (`Docs/PromptEditor.html`)

Offline, one page, like the Tale Editor.

- **The sequence as a graph**:
  - prefix and tail in two bands, the cache boundary drawn between them;
  - each section a box: key, kind, gate in a few words, a padlock when locked, greyed when off, an estimated size;
  - drag to reorder (the zone rules enforced live);
  - Lean shown as a toggle on the whole view.
- **A section**:
  - its text, with the variables it may use (click to insert, each explained);
  - its doc and tips;
  - its stock text beside a custom one, with the drift warning.
- **Checks**: the engine's own, run on every change. Parity tests under Node hold the editor and the game to the same
  verdicts (the Tale Editor's method: shared fixtures, the expected verdict written once).
- **Preview**: open a `cr.prompt_dump ... sections` file to see the real prompt of a real character, section by section;
  the editor cannot compute game state itself.
- **Profiles**: open the stock profile, a mod's or the player's; "copy to Custom" in one click; set a parent.
- **Kinds**: one tab per kind of character, each showing which layout applies (its own, `default`, or stock).
- **Output**: a profile folder to drop in `Custom/Prompts/`, or in a mod's `ModuleData/cr_prompts/`.
- **Where**: `Docs/PromptEditor.html`, shipped with CR beside the Tale Editor and the Scenario Composer.
- **Tips**: written with the editor, from what CR learned. Examples:
  - position matters (first and last are read hardest);
  - anything that changes per turn goes in the tail, or the cache is lost every reply;
  - small local models need the Lean prompt and short sections;
  - a rule stated as a prohibition plants the word it forbids;
  - two sections that disagree make the model pick at random.

The editor reads a **catalog** generated from the code: keys, zones, locks, kinds, gates, variables, docs, tips and stock
texts, `Docs/prompt_catalog.character.json`. A maps-style test regenerates it and fails when it is stale, so the editor
can never describe a prompt the game no longer builds.

## Increments

0. **Golden prompts.** A handful of real configurations (lord, companion, spouse, captive scene, Lean, Full, a tale
   giver) built through the real builder and stored. Every later increment must leave them identical. It is also the
   test CR is missing today.
1. **Section registry.**
   - `BuildSystemPrompt` becomes a walk over keyed sections in the stock order.
   - Zone, lock and kind are declared.
   - A ratchet map of the keys is added.
   - No behaviour change: the golden prompts stay identical.
2. **Profiles and layouts.**
   - Profiles found in stock, mods (`cr_prompts`) and Custom; the MCM "Prompt profile" choice; parents and fallback.
   - Layouts by kind, with the checks and the locks.
   - The custom-active log line, `cr.prompt_check`.
   - `cr.testall`: a test profile built through the real path, then stock restored.
3. **Texts out**, in batches:
   - first the sections people want to tune: dialogue style, prose craft, narrative framing, the intimacy texts (not
     the sealed rules), the Lean teachings;
   - then the offers and teachings;
   - framed-data sections last.

   Golden prompts identical after each batch.
4. **The catalog and the editor**, with parity tests.
5. **The commoner prompt**; then decide about the interpreter, council and envoy.

Each increment ships on its own; 1 and 2 are useful to modders before any text moves.

## Risks

- **Support.** A customised prompt will produce "CR answers badly" reports. Hence the log line first, the MCM switch,
  and the player-report skill asking for it.
- **The refactor (1)** touches the heart of every conversation. Golden prompts first, and no other change in the same
  increment.
- **The Lean budget and cache stability** must still be measured on the stock layout; a custom layout is warned about,
  never refused, on size.
- **Performance.** Texts are read once per session (like `NarrativeLoader`), never per reply.
- **Letters** reuse the character prompt, so a custom layout changes letters too. That is consistent; the editor says
  so.

## Decisions (Gabriel, 11/10/2026: "fait les choix logiques pour laisser le plus de liberte")

1. **Locks**: only where the game would break or the player's safety is at stake. Three sealed (format contract, consent,
   captive-scene consent); five required with free wording; everything else free. Breaking the cache is warned about,
   never forbidden.
2. **A section file replaces** the stock text by default (`!append` to add instead).
3. **No precedence between mods and the player**: the player chooses the profile in MCM ("un param MCM pourrait designer
   l'ordonnanceur a utiliser, car il pourrait y avoir plusieurs mods qui proposent un ordonnanceur"). Stock is the
   default and the reset.
4. **Layouts by kind of character** from the start ("mise en page par type"), with fallback to the profile's `default`
   layout, then stock.
5. **The editor ships in Docs** with CR.

Still to check when building: MCM must offer a dropdown filled at run time with the profiles found (increment 2 starts
with that spike; if MCM cannot, a text field naming the profile id, listed by `cr.prompt_check`).
