# Bringing the scene engine into the game: the plan (28/11/2026)

Gabriel, 28/11/2026: "oui on le fait" (bring the group-scene work into the mod, after "on intègre pas tout de suite" on 27/11).
The engine lives in the simulator, `CalradiaRemembers.Tests/Conversation/GroupConversationSimulation.cs`. Its notes are in
`inner-lives-2026-11-27.md` and `state-memory-2026-11-28.md`.

## The order, and why

The stages are ordered by **measured gain per cost to the player**, not by when each idea came up. Every stage:

- is useful on its own;
- is opt-in (an MCM switch, off by default unless noted);
- ships with a `cr.test all` self-test, which is the only in-game check: Gabriel does not play long campaigns, so
  **nothing here will be play-verified**;
- names the maps read before it is built (settings, prompt-sections, inventory, save-format);
- lands in the OPEN version (2.8.22). Release timing stays Gabriel's call.

What was measured in the simulator:

- The **memory** of a past meeting is the largest gain: 4.2 with no memory, 5.2 with the events the game already gives,
  6.8 with lasting changes and the bond added.
- The **per-turn machinery** has not been shown to beat a plain chat. On the drama, a same-batch baseline with no inner
  lives, no dramaturge and no keeper equalled the full system. That machinery is the inner lives, the keeper and the
  dramaturge.
- The **generated depth** (voice, mask, fear, limit, private secret) and the **composer** clearly improved the
  game-data scenes on reading, but have not been graded blind.
- Only DeepSeek V4 Pro was validated as the prose model, plus Grok once. A small local model was never tried.

## Stage 1: the conversation log (Gabriel: "une option MCM qui permet de créer un log d'une conversation")

- **What:** an MCM switch "Write a conversation log". When on, each chat writes a Markdown file like the simulator's
  transcripts:
  - the scene, then "Why each turn went as it did";
  - on day one, the "why" holds only what exists: the model and its latency, interpreter actions, regard changes,
    ActionGate refusals, recall passages, refusal fallbacks;
  - the judge, referee, dramaturge and keeper lines join it when those stages exist.
- **Built on:** `ILlmExchangeObserver` / `ReportRecorder` (one observer, not a second one) and the folder
  `PlayerReportService` already uses ("CR Reports"). Never writes keys or URLs.
- **Cost to the player:** none (no model call).
- **cr.test:** `conversation_log`. Drives one staged exchange through the real path and asserts the file holds the
  lines and the why section.

## Stage 2: what a meeting leaves (lasting change, bond, unsettled matters, secrets that last)

- **What:** at the close of a chat, one background call updates the NPC's memory of the player.
  - **A lasting change**, consolidated, dated.
  - **The four-part bond:** understands, forgives, loves, trusts. This is prompt texture, NOT a third regard ledger:
    `ReputationWithPlayer`, `RelationGate` and progressive regard stay the mechanism.
  - **What is left unsettled:** six at most, consolidated.
  - **Secrets one by one,** each with whom it was told to.
  - The next chat's prompt shows these with days counted at reading ("three days ago"). No day number is ever said
    aloud.
- **Built on:**
  - `NpcProfile.Events` and the existing history block (`YOUR HISTORY WITH THIS PLAYER`);
  - the housekeeping client (the Background model);
  - additive fields only (save-format map).
  - Compression must keep secrets and unsettled matters, as it keeps player-written memories.
- **Cost:** one background call per chat closed. Two short prompt sections, below the cache marker if they change per
  turn, above it otherwise; to be checked against the Lean budgets.
- **cr.test:** `meeting_memory`. Closes a staged chat, asserts the update is written, reloads, asserts the next
  prompt carries it.

## Stage 3: the intro that opens a chat (Gabriel: "au début de chaque chat on ajoute l'intro ... ça met en scène")

- **What:** the scene composer. Before the first line it reads what the game knows:
  - where they are, hour and season;
  - how the player comes (gear, companions present);
  - what the two just lived (a battle side by side above all);
  - the talk of the realm and the NPC's memories.

  It writes one paragraph of setting, shown at the top in the narrator's purple: what the NPC is doing as the player
  enters. It also gives the NPC a concern for today and a tension with the player, kept hidden in its prompt.
- **The rule (Gabriel):** it chooses among true facts and stages them; it invents nothing the game tracks. A cheap
  check reads its output against the facts. One composed event that invented a raid was caught that way in the
  simulator.
- **Built on, not beside:**
  - it IS the opener: governed by `CharactersSetTheScene` and `SceneNarration` (1Enfant asked for the purple narration
    off; that switch must keep working);
  - season, hour and night already come from `HereAndNowFacts`;
  - gear and companions from `ScenePlayerText`, today serving tales only;
  - battle memory from `BattleMemoryText`.
- **To decide:** which entry points get an intro. Proposed: an ordinary chat yes; envoy, captive scene, council, and
  reopening the same chat the same day no.
- **Cost:** it replaces the existing opener call (same count), plus one cheap check.
- **cr.test:** `composed_intro`. Stages a meeting after a shared battle, asserts the intro names the place, the season
  and the battle, and invents no event.

## Stage 4: depth for the characters the game describes thinly

- **What:** once per important character, the first time it matters, one call writes a voice, a mask, a fear, a limit
  and a private secret from its game sheet. They are stored and never regenerated, and evolve only by lived events
  (stage 2).
  - **The secret (Gabriel):** private life the game cannot check. Never a marriage, a kinship, a death, a battle, a
    fief, money or troops.
- **Collisions to resolve first:**
  - `NpcProfile` already has `Fear`, `MattersMost`, `Personality`, `AuthoredBackstory` and `AuthoredConviction`.
  - There is the earlier ruling on authored voices ("no invented tics") and the voice-habit stability work.

  Depth fills what is empty and complements what exists; it never adds a second fear or a second voice. An authored
  character keeps its authored text.
- **Cost:** one call per character, once.
- **cr.test:** `generated_depth`. Generates for a staged hero, saves, reloads, asserts it is the same and that the
  secret touches no tracked fact.

## Stage 5: the novel's presentation (Gabriel: "oui ok j'aime ça")

- **What:**
  - speech in quotation marks;
  - gestures as plain third-person prose, no asterisks;
  - the speaker's name as a label;
  - purple kept for the narrator's lines only (the intro, world events, the rare narrated reaction).
- **Why it is late:** it is the riskiest stage, not a quick win.
  - It changes the reply format for every chat, every model, Full, Compact and Lean.
  - It invites models to narrate the player's own actions.
  - It touches whatever reads asterisks today. The player's `*acts*` are taught as real acts, and the intimacy rule
    keys on the player's stage directions.
- **So:** an MCM choice "Narrative style: Roleplay (asterisks, first person) / Novel (third person, quoted speech)",
  default Roleplay. The player may write either way in both. A guard: never speak or act for the player. A
  multi-model bench before it ships (cost bench, Full and Compact). Open: whether the ordinary chat follows, as an
  option for all.
- **cr.test:** `novel_style`. Asserts a reply in the novel style renders speech and gestures and no asterisks.

## Stage 6: the per-turn machinery in the two-person chat, only after an A/B

- **What:** the inner life after each line (feeling, aim, tactic, what shows, the bond), the keeper (lie, repeat,
  contradiction, invention; a rewrite read again), and the reaction chosen in code.
- **Gate:** a cost-bench replay A/B on real chats, against today's chat, blind. It goes in only if it wins: nothing
  yet says it does.
- **Cost:** two or three cheap calls per turn, in parallel; about 1 to 3 s more per reply in the simulator.

## Stage 7: the group engine

- **What:**
  - the floor judge and the turn arbiter (`FloorJudge`, `TurnArbiter`, written 26/11 and "not wired yet") replace
    `EnsembleFloorPolicy` in council, assembly and companion briefings;
  - the dramaturge brings true events and ends the scene;
  - per-person secrets.
- **Not validated yet:** the canonical state and per-person secrets were only tried with two characters.

## The models in the MCM (Gabriel: "on va aussi ajouter un modèle dramaturge j'imagine?")

- **No new setting before stage 7:** nothing needs a dramaturge until the group engine.
- **When it comes:** "Dramaturge model", its own field, blank = the PROSE model. Gabriel (28/11/2026): "je pourrais vouloir
  DeepSeek, et Mimo en PROSE", so the two must be chosen independently. The cheap dramaturge failed measurably (5.40 against
  6.61, no event in five runs), so its default must be the prose model. It would ride the prose connection with a model
  override per request (`CrLlmRequest.Model` already exists), not a new provider, key and URL.
- **The cheap readers ride existing clients:**
  - the judge, inner lives and keeper ride the Interpret client (Flash by default);
  - the composer, depth and memory updates ride the Background (housekeeping) client.

## What stays open

- Long-term consolidation of a memory after many meetings.
- A human player's side (short, off topic, testing limits) was never simulated; Corvan and Arwa were played by the
  model.
- How these stages behave on a small local model.

## Decided with Gabriel before the build (28/11/2026)

- **Three modes** in the existing "Chat Composition" list: Standard (one model plus NSFW), Enhanced (PROSE, INTERPRET,
  NSFW), and **Dramaturge** (DRAMATURGE, PROSE, INTERPRET, NSFW). The values 0 and 1 keep their meaning, so old settings
  load as they are. "Novel" was set aside: it names the presentation style of stage 5. The Dramaturge mode and its model
  appear only when they do something (stages 6 and 7).
- **The dramaturge is a role like the others:** its own model (blank = the PROSE model) and provider ("(default)" = the
  PROSE provider), plus its URL and key for an OpenAI-compatible server. Gabriel wants to be able to run Mimo in PROSE and
  DeepSeek as the dramaturge.
- **Stage 0, the MCM regroup:** "LLM Provider" splits into "Mode & Models" (the mode, then each role in the order it
  serves) and "Prompt & Memory" (the rest). A setting is found by its property name, so moving it breaks nothing.
- **The in-chat switch keeps two positions:** the mode chosen in the MCM, or Roleplay.
- Gabriel, before the night's build: "go pour tout le système ... je testerai demain".

## Rules for the night's build

- With every new switch off, the game behaves exactly as today. `GoldenPromptTests` (SDK) pins the default prompt
  character for character; a golden that moves is a stage that leaked into the default.
- Stages 6 and 7 cannot meet their A/B gate tonight (the only cost-bench capture is from 24/10, and only Gabriel can
  take a new one). They live ONLY under the Dramaturge mode. Standard and Enhanced do not change, and council stays on
  `EnsembleFloorPolicy` outside it. They are marked "not A/B-validated".
- No prefab or XML change for stage 5.
- Live model runs are capped; the night's total is reported.

## Status ledger

| Stage | State | Commit | Notes |
|---|---|---|---|
| 0 MCM regroup | done | fdf427f | Mode & Models (GroupOrder 0) + Prompt & Memory (1); other groups +1; properties and defaults unchanged; not play-verified |
| 1 Conversation log | done | (this commit) | MCM Diagnostics "Write a conversation log" (WriteConversationLog, off); cr_chat_<npc>_<stamp>.md in "CR Conversations"; 1:1 and council lines via RecordTranscript; why = calls (role, tokens, cost, finish), reply time, deeds done/refused/dropped, fallbacks, recall; no latency per call (SDK untouched); cr.test conversation_log; not play-verified |
| 2 What a meeting leaves | to do | | |
| 3 Composed intro | to do | | |
| 4 Generated depth | to do | | |
| 5 Novel style | to do | | |
| 6 Per-turn machinery (Dramaturge mode) | to do | | |
| 7 Group engine (Dramaturge mode) | to do | | |
