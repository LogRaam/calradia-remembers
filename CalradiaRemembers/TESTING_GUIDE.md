# Calradia Remembers — Tester's Guide

Console commands + the in-game scenarios that must be checked by hand.

**Scope.** The mod's pure decision logic (relation caps, scheme lifecycle, recruitment gates, the
stance maths, power-balance wording, marriage eligibility, etc.) is already covered by 486 automated
unit tests — **you do not need to re-test those by hand.** This guide lists only what the unit tests
*cannot* prove: that game events actually fire the logic, that parties move, that the prompt actually
changes what NPCs say, and that everything survives a **save / reload**.

---

## 0. Before you start

- **Enable the console.** Launcher → game → engine options → enable cheats. In game press **Alt + `~`**
  to open the console. All commands are in the `cr` group: type `cr.help`.
- **`[name]` is optional** on most commands — if you already have a conversation open, it targets that
  NPC; otherwise pass a name fragment (e.g. `cr.stance Derthert`).
- **Conversation/letter tests need the LLM configured** (Mods → Mod Options → Calradia Remembers →
  API key + model, or a local OpenAI-Compatible endpoint). Mechanical tests (stance maths, party
  movement, persistence) do **not** need the LLM.
- **Adult/captive scenes** require Adult Content Level = **Hardcore** in Mod Options.
- **Turn on logging** for bug reports: Mod Options → Diagnostics → Enable Logging (+ Verbose / Story
  Log as needed). Log: `Documents/Mount and Blade II Bannerlord/Logs/CalradiaRemembers.log`.

---

## 1. Console command reference

### Self-test harness (the fast regression sweep)

One harness exercises the mod's systems end to end through the real service/bridge, asserting what
happened and logging a `[SELFTEST]` PASS / FAIL / MANUAL line each. Adding a test is one method + one
registry line — no new console command. **Tests mutate live campaign state: copy your save, or reload
when done.**

| Command | Does |
|---|---|
| `cr.testall` | Run the routine sweep: every test **except** the irreversible ones. |
| `cr.test list` | List the registered tests (name + severity + description). |
| `cr.test <name>` | Run one test by name (e.g. `cr.test pregnancy`, `cr.test bastard_claim`). |
| `cr.test chat` | Run only the conversation smoke tests (real LLM calls; **full untruncated replies** in the log). |
| `cr.test mech` | Run only the mechanical tests (no LLM call, no API cost). |
| `cr.test full` | Run **everything**, including the irreversible tests — **throwaway save only**. |

- **Irreversible tests** (`declare_war`, `scenario`) really declare a war / seal a one-shot scenario in
  the live campaign, so they are held out of `cr.testall` and run only on explicit `cr.test <name>` or
  `cr.test full`. A reload is the only undo.
- **Conversation smoke tests** (`cr.test chat`) make one real LLM call each, seed identical state every
  run, and log the full Q&A so you can judge relevance by reading it. Each line is stamped with the
  active model (e.g. `[model: x-ai/grok-4.20]`) — change the model in Mod Options and re-run to compare
  LLMs on the same questions. Coverage: plain dialogue, shared-memory recall, stance (fear/distrust),
  hearsay about a third party, a bastard-mother pressing for coin, and a fed world-event surfacing.
- **Extension Surface**: `cr.feed_test [summary]` records an external world event through the public
  `CalradiaRemembers.Feed` API (the same path other mods use) so you can see an NPC gossip it.

### Stance (multi-axis posture)
| Command | Does |
|---|---|
| `cr.stance [name]` | Show an NPC's posture: Affection / Trust / Respect / Fear + the prompt block. |
| `cr.stance_set <trust> <respect> <fear> [name]` | Set the Trust/Respect/Fear axes directly (Affection stays on the reputation path). For testing the posture block in conversation. |

### Schemes (feud plots)
| Command | Does |
|---|---|
| `cr.schemes` | List the plots currently in motion (plotter → target, kind, state, progress). |
| `cr.scheme [name]` | Force-plot a scheme: the named lord plots against their most-hated rival. |
| `cr.scheme_advance` | Advance every active scheme one tick (no waiting for the daily tick). |
| `cr.scheme_assist [name]` | Force the player to abet the named plotter's secret scheme (bypasses the LLM). |
| `cr.scheme_warn [name]` | Force the player to warn the named lord of a secret plot against them. |

### Interception (NPCs ride out to you)
| Command | Does |
|---|---|
| `cr.intercept [name]` | Force the named lord (with a party on the map) to ride out and intercept the player. |

### Recruitment & defection
| Command | Does |
|---|---|
| `cr.recruit_info [name]` | Show the join_clan verdict + gathered gate facts for a lord. |
| `cr.recruit [name]` | Force the join_clan action (re-validates the gate, then moves the lord into your clan). |
| `cr.hire_info [name]` | Companion-hire eligibility + asking/floor/cap (join_party). |

### Relations & reputation
| Command | Does |
|---|---|
| `cr.relation <delta> [name]` | Simulate a change_relation action (via RelationGate). |
| `cr.relation_intimate <delta> [name]` | Same, as an intimacy event (cap +3, bypasses cooldown). |
| `cr.relation_reset [name]` | Clear the relation-gain cooldown stamp. |
| `cr.rep_set <value> [name]` | Set the NPC's personal reputation directly (bypasses the gate). |
| `cr.standing` | Show the player's clan standing (tier / renown / influence). |
| `cr.info [name]` | Relation, cooldown state, captive status. |

### Memory
| Command | Does |
|---|---|
| `cr.memory [name]` | List every recorded event for an NPC, with its index. |
| `cr.memory_add <days_ago> <text> [name]` | Add a back-dated memory (build a prior relationship). |
| `cr.memory_set <index> <new text> [name]` | Replace one event's text. |
| `cr.forget <index> [name]` | Delete one bad event. |
| `cr.forget_all [name]` | Wipe an NPC's entire memory. |
| `cr.profile_check [name]` | Diagnostic dump of an NPC's stored profile. |
| `cr.succession [predecessor]` | Simulate dynastic succession (you as the heir): reframes all NPC memories — drops private events, marks the rest inherited. Predecessor defaults to your parent. |

### Quests
| Command | Does |
|---|---|
| `cr.quest [name]` | Seed a bandit-clear quest (issue → inject → complete path). |
| `cr.quest_scout [name]` | Seed a scout-army quest. |
| `cr.quest_done [name]` | Force-stamp an outstanding quest satisfied (test reward without the battle). |
| `cr.quest_complete [name]` | Make the NPC acknowledge the completed quest + pay the reward (needs quest_done first). |
| `cr.quest_abandon [name]` | Force-abandon an outstanding quest (broken-word penalty). |
| `cr.quests [name]` | List an NPC's quests + state. |
| `cr.journal_check` | Diagnostic of the quest journal mirror. |
| `cr.bargain_items [name]` | Seed a pay-in-goods bargain (deliver-items reward). |
| `cr.bargain_marriage [name]` | Seed a marriage-consent bargain. |
| `cr.deliver_prisoner [name]` | Seed a deliver-prisoner bargain targeting the first lord you hold (case a). Then offer to deliver them in conversation. |
| `cr.declare_war_quest [name]` | Seed a declare-war bargain targeting the first faction you can still declare war on. Then declare war on it → the deed stamps done. |
| `cr.capture [name]` | Take a lord prisoner (test shortcut). Then speak with your captive — they may bargain for their freedom (free_prisoner). |

### Marriage
| Command | Does |
|---|---|
| `cr.marry [name]` | Force a marriage to the NPC (test the family-reaction consequence). |
| `cr.marriage_approve [name]` | Grant the family's blessing (A.2). |
| `cr.marriage_revoke [name]` | Revoke the blessing. |
| `cr.marriage_status [name]` | Show the blessing state. |

### Letters & couriers
| Command | Does |
|---|---|
| `cr.letter [name]` | Queue an NPC-initiated letter to the player (arrives at the next daily tick). |
| `cr.letter_read [name]` | Deliver a synthetic letter through the real delivery path → the parchment courier window in READ mode (Acknowledge / Reply). |
| `cr.letter_compose [name]` | Open the parchment courier window in COMPOSE mode — write a fresh letter and seal it. (Was `cr.compose`.) |
| `cr.letter_reply [name]` | Open the parchment courier window in REPLY mode with a synthetic received letter — reply, send, watch it seal + close synchronously. |
| `cr.courier [name]` | Spawn a visible player→NPC courier. |
| `cr.courier_slow [name]` | Same, slowed, to watch it cross the map / be intercepted. |

### Player-authored characters
| Command | Does |
|---|---|
| `cr.backstory [name]` | Show the authored override for an NPC (matched id, backstory) + the effective vs vanilla level of each of the five personality traits. |
| `cr.reload_lore` | Reload `ModuleData/character_overrides.json` and re-apply (or restore) traits per the current "Use custom traits" toggle — no relaunch. |

### Witnesses
| Command | Does |
|---|---|
| `cr.witnesses [name]` | Diagnose who would witness a conversation here. |
| `cr.witness [witnessName]` | Seed a synthetic witness. |

### Jealousy
| Command | Does |
|---|---|
| `cr.jealousy [name]` | Resolve + fire a jealousy reaction as if the player romanced this NPC. |
| `cr.jealousy_comp` | Test the competition (polygamous-household) jealousy path. |

### Captive / adult (Hardcore only)
| Command | Does |
|---|---|
| `cr.crc_scene [female|male] [solo|collective] [intent] [name]` | Open a lord captive scene. intent = interrogation\|desire\|domination\|torture\|training\|reward. |
| `cr.bandit_scene [extortion|intimidation|revenge|spoil] [female|male] [solo|collective]` | Open a bandit captive scene. |
| `cr.bandit_queen [solo|collective] [male|female]` | Open a captive scene led by a female bandit chief. |
| `cr.kinks [name]` | List an NPC's kinks + valid kink names. |
| `cr.add_kink <kink> [name]` / `cr.remove_kink <kink> [name]` | Add / remove a kink (apply BEFORE opening a scene). |
| `cr.preg [name]` | Force a conception with the female PLAYER as mother ([name] = father, else a shared hidden "Unknown" father). |
| `cr.preg mother <name>` | Force a conception with that NPC as mother and the player as father (the bastard path) — use to verify which clan the child lands in. |
| `cr.escape_force` | Force the captive-escape path. |

### Chronicle & world rumors
| Command | Does |
|---|---|
| `cr.chronicle` | Print/write the player's chronicle. |
| `cr.chronicle_open` | Open the in-game illuminated chronicle viewer. |
| `cr.chronicle_illuminate` | Generate the AI-illuminated prose. |
| `cr.chronicle_rewrite` | Re-illuminate from scratch (after editing the style file). |
| `cr.rumor [settlement]` | Show the street rumours a commoner here would share. |
| `cr.deed_heard [name]` | Seed a deed of yours that wounds this NPC's faction, then echo what they've heard of you (influence loop). |
| `cr.realm_news` | Seed a war between two kingdoms; talk to an NPC to hear "the talk of all the realm". |

### Scripted scenarios (world-reaction engine)
| Command | Does |
|---|---|
| `cr.scenarios` | List the loaded scenario rules (from ModuleData/scenarios/*.json) and which one-shots have fired. |
| `cr.scenario_fire <VERB[:payload]> [heroName]` | Manually fire a trigger to test a scenario. The hero name (if any) becomes the event's primary hero; the player is always included. |

**Scenario test (S1) — Rhagaea's death turns the empires:** run `cr.scenario_fire ON_DEATH Rhagaea`. The sample pack's rule (guard `IF_HERO_IS:Rhagaea`) should fire: the Northern + Western Empires declare war on the Southern Empire, and a world-event message ("the eagles turn upon a leaderless South") is recorded — talk to an NPC of those realms to hear it. `cr.scenarios` then lists `rhagaea_death_eagles_turn` as a fired one-shot. (Fires without actually killing her.)

### Companion missions (first slice — "bring me the news")
| Command | Does |
|---|---|
| `cr.send_news [about]` | Send your first companion to gather news (optionally scoped: `cr.send_news Sturgia`). |
| `cr.send_spy [about]` | Send your first companion to scout a town (Roguery) — intel comes back in conversation. |
| `cr.send_steal [about]` | Send your first companion to lift coin from a town (Roguery) — loot reaches you on return. |
| `cr.send_barter [about]` | Send your first companion to trade at a town (Trade) — profit reaches you on return. |
| `cr.send_envoy [about]` | Send your first companion as an envoy (Charm) — the faction's mood comes back in conversation. |
| `cr.missions` | List the errands companions are currently away on. |
| `cr.mission_return` | Force-resolve the first errand now and print the report (skip the travel). |
| `cr.journey_recall` | Recall EVERY away companion immediately — abandon the errand, rejoin the party now (conversation/console path). |
| `cr.recall_letter` | Send a letter-borne recall to the first away companion — they ride home on a daily tick after the courier delay. |
| `cr.happiness [name]` | Show a companion's contentment in your service (score + band). |
| `cr.happiness_set <0..100> [name]` | Set a companion's contentment (test the bands + the encyclopedia line). |
| `cr.rapport [name]` | Show how a companion feels about each fellow companion (camp life). |
| `cr.rapport_tick` | Run a weekly camp-life pass now (compatibility nudges + any companion weddings). |
| `cr.bargain [name]` | Report whether an NPC may offer the intimacy-as-currency bargain (Phase 3, adult-gated). |
| `cr.bargain_leverage [name]` | Mark an NPC as holding leverage, to test the escalation prompt. |

### Discovery / misc
| Command | Does |
|---|---|
| `cr.discover_test [name]` | Inject 3 placeholder discoveries (encyclopedia panel test). |
| `cr.help` | List the commands. |

---

## 2. In-game test scenarios (the things unit tests can't prove)

Each scenario lists **what it proves**, the **steps**, and the **expected** result. Where a 💾 appears,
**save, reload, and confirm the state persisted** — persistence is engine-only and a frequent break point.

### A. Stance system (NEW — fully untested in game)

**A1 — Posture colours the dialogue (read path).** *Proves the prompt block actually reaches the LLM
and changes its tone.*
1. `cr.stance_set 60 -40 70 Derthert` (high trust, low respect, high fear).
2. Open a conversation with Derthert.
3. Expected: he speaks **warily/deferentially and intimidated**, yet **dismissive of your word** — not
   generic politeness. Try other mixes: `cr.stance_set -50 60 70 X` should read as a *beaten rival*
   (hatred + grudging respect + fear).

**A2 — Deeds move the axes (the core "deeds prove").** *Proves each event hook fires.*
- **Respect/Fear from battle:** beat an enemy lord you have a profile for in a field battle you win →
  `cr.stance <that lord>` → Respect and Fear up. A lord who fought **on your side** in a win → Respect up.
- **Trust from a quest:** `cr.quest Lord` → `cr.quest_done Lord` → talk and let him acknowledge it (or
  `cr.quest_complete`) → `cr.stance Lord` → **Trust up**. Then `cr.quest Lord` again → `cr.quest_abandon
  Lord` → **Trust down**.
- **Fear/Respect from capture:** take a lord prisoner in battle → `cr.stance <that lord>` → Fear + Respect up.
- **Trust from a gift:** in conversation, give the NPC an item (the bag/give path) → Trust up a little.
- **Trust collapse from treachery:** see scenario **C4**.

**A3 — Words only nudge, and are rate-limited.** *Proves the word-round cap + cooldown.*
1. Note `cr.stance X`.
2. Have a conversation where you clearly flatter / impress / threaten. At most each axis moves **±2**.
3. Immediately have another such conversation: **no further stance move** for ~8 in-game hours (cooldown).
   Confirm a **deed** in the same window still moves it strongly (deeds ignore the cooldown).

**A4 — Fear fades.** Set `cr.stance_set 0 0 80 X`, then let several in-game days pass → `cr.stance X` →
Fear has dropped (~2/day).

**A5 — 💾 Persistence.** Set a distinctive stance (`cr.stance_set 33 -22 44 X`), **save, reload**,
`cr.stance X` → unchanged. Open an **old save** from before this build → that NPC reads neutral (0/0/0),
no crash.

### B. Interception (NEW — untested in game)

**B1 — A lord rides out.** `cr.intercept <a lord with a party on the map>` → a notification appears and
that lord's party turns and **moves toward you** on the campaign map. Let them reach you.

**B2 — The opener.** When you talk to the lord that intercepted you, his **first line opens on that
footing** (a territorial challenge, or "word of your deeds reached me…") rather than waiting on you.
(The opener is consumed — a second conversation is normal.)

**B3 — Cooldown + one-at-a-time.** After an interception, the same lord should not intercept again for
~30 days, and only one pursuer runs at a time. 💾 **Save/reload mid-cooldown**: the lord still should
**not** re-intercept immediately (the cooldown is persisted).

**B4 — Giving up the chase.** Trigger an interception, then ride away on a **faster** party. The pursuer
should **abandon** the chase once you are clearly out of reach (rather than trail you forever), and at
the latest after ~4 days.

**B5 — "Heard a rumour" trigger (organic).** With the rumour store populated (win a notable battle,
take a fief, etc.), a nearby lord aware of it may ride out on his own over the following days. This is
probabilistic — let several days pass near other lords and watch for the notification.

### C. Schemes & player involvement (NEW — untested in game)

**C1 — A plot matures and spreads.** `cr.scheme Derthert` → `cr.schemes` (Plotted). `cr.scheme_advance`
a few times → watch it go Progressing → (maybe Discovered) → Succeeded/Failed. On **success**, the
target clan's **influence drops**. Talk to a lord of the same realm who would have heard → he may
**mention the feud / its outcome** (rumour → "what you've heard").

**C2 — Assist a plotter.** Make a plotter regard you (`cr.relation 30 Derthert`), `cr.scheme Derthert`,
then `cr.scheme_assist Derthert` → the plot leaps forward (`cr.schemes`); aiding it to completion lands
the standing damage. In a real game, the plotter should also *offer* this in conversation (the
`scheme_assist` action) only when you are not allied with the target.

**C3 — Warn a target.** `cr.scheme Derthert` (against rival X) → `cr.scheme_warn X` → the plot becomes
**Discovered** and is far likelier to **fail** over the next `cr.scheme_advance` ticks. The warned target
X is grateful (**+12** — `cr.info X`); Derthert, suspecting the meddler, drops **−20** (`cr.info Derthert`
should show a clearly negative relation, pushing into "distrust" territory in the LLM prompt). Note:
`cr.info` now shows `pacing=` (the MCM relationship-pacing setting), not the NPC's emotional attitude.

**C4 — Caught treachery spreads (reputation + lords' talk).** Assist a plot (C2), then let it be
discovered/succeed. Expected: the **target turns on you** (relation + Trust drop, scaled by who they
were — try a **same-faction** target for the harshest "treason" hit, vs an existing enemy for the
mildest), a **notification** fires, and word of your hand **spreads as a rumour** — talk to other lords
who'd have heard and they should reference you as a known schemer. 💾 Save/reload: active schemes persist.

### D. Recruit a lord into your clan (NEW — untested in game)

**D1 — Eligibility gate.** Find a lord who is **landless / of a broken clan / estranged from his liege**,
raise relation high (`cr.relation` repeatedly or `cr.rep_set 70 X`), ensure **your clan tier ≥ his**.
`cr.recruit_info X` → ELIGIBLE. A comfortable, high-tier, loyal lord → blocked (and `cr.recruit_info`
says why).
**D2 — Execution.** `cr.recruit X` → X **leaves his clan and joins yours** (check the clan screen). In a
real conversation the lord should only offer/accept `join_clan` when eligible. 💾 Save/reload: he's still
in your clan.

### E. Power balance (NEW prose dims — untested in game)

The lord should **read the room**: open a conversation with a lord of a **much weaker** realm (or one you
recently **beat in battle**, or **your prisoner**) → his tone reflects the imbalance (precarious /
deferential), not blind defiance. (The threshold maths are unit-tested; only the *prompt effect* needs eyeballing.)

### F. OpenAI-Compatible local endpoint (NEW — untested in game)

**F1 — Keyless local server.** Run LM Studio/Ollama, load a model. Mod Options → Provider =
**OpenAI-Compatible**, Endpoint URL `http://localhost:1234/v1` (or `:11434/v1`), Model id, **API Key
blank**, **Test Endpoint** → OK. Have a conversation → it uses the local model. Confirm switching back to
OpenRouter/NanoGPT still works.

### G. Regression checks for prior untested-in-game work

These shipped built-but-unvalidated earlier; worth a pass:
- **join_party / mercenary / marry / consort / give_item** actions actually execute from conversation.
- **Courier** crosses the map and can be intercepted (`cr.courier_slow`), NPC replies arrive by courier.
- **Captive scenes** (Hardcore): `cr.crc_scene` / `cr.bandit_scene` open, pace through beats, and **end
  cleanly** (the v1.9.0 "cut off at the start" fix).
- **World rumours**: `cr.rumor` shows current events; a lord's "what you've heard" reflects real battles/
  sieges/marriages near him, hedged by distance.
- 💾 **General save safety:** play a CR session with schemes/letters/couriers in flight, save, reload —
  the campaign opens and state is intact.

### H. Parchment courier window (NEW — letters split out of the chat overlay, untested in game)

**What it proves:** letters now live in their own parchment window, not the chat. Sealing closes the
window **synchronously** — the fix for the old reply-then-crash / stuck-on-pause bug.

- **H1 — Read.** `cr.letter_read [name]` → a parchment letter opens with **Acknowledge** and **Reply by
  letter**. Acknowledge closes it; the game un-pauses. The same letter never pops again.
- **H2 — Reply (the crash regression).** `cr.letter_reply [name]` (or **Reply** from H1) → write a reply →
  **Seal & Send**. Expected: the window closes **instantly**, time un-pauses, a "reply sealed" notice
  appears, **no "(considering…)", no hang, no crash**. The NPC's answer arrives later by courier.
- **H3 — Compose.** `cr.letter_compose [name]` → write a fresh letter → **Seal & Send** → courier-departs
  notice; from a tavernkeeper the 500-denar fee is charged and **Seal & Send stays disabled if you can't
  afford it** (the message isn't lost).
- **H4 — Real delivery.** Let an NPC-initiated letter actually arrive (`cr.letter` then wait a daily
  tick, or play on): it should open the **parchment** window, not the old vanilla box.
- **Look & feel:** report anything off about the parchment (colours, font, spacing, the reply box) — the
  visuals are a first pass to refine.

### I. Player-authored character data (NEW — untested in game)

**What it proves:** authored traits become the single source of truth (prompt, derived features, AI),
the MCM toggle applies/restores live, and the backstory reaches the prompt.

- **Setup:** edit `ModuleData/character_overrides.json` — add one entry (e.g. Derthert with `honor: -2`,
  `calculating: 2`, a `backstory`). In Mod Options → Calradia Remembers → Character, turn **Use custom
  traits** ON.
- **I1 — Applied.** `cr.reload_lore` then `cr.backstory Derthert` → the effective Honor/Calculating show
  your values (with the vanilla values in parentheses), and the backstory is listed.
- **I2 — Persona follows.** Talk to him: his manner should reflect the authored traits + backstory, not
  the vanilla personality.
- **I3 — Toggle off restores.** Turn **Use custom traits** OFF (or `cr.reload_lore` with it off) →
  `cr.backstory Derthert` shows the effective traits back at their **vanilla** values.
- **I4 — Homonyms / bad file.** Two entries with the same `hero` and no `id` should be refused (neither
  applied); a malformed file disables the feature without crashing (check the log).
- **I5 — Kinks follow (Hardcore).** With an authored NPC you have already met, `cr.kinks [name]` before
  and after `cr.reload_lore` (toggle on) → the kinks re-derive from the authored traits (deterministic,
  so they're stable across reloads); toggling off re-derives them back to the vanilla-trait set.

### J. Deliver-a-prisoner deed (NEW — untested in game)

**What it proves:** a lord can demand an enemy captive as the price of a favor, and a real prisoner
transfer (never the LLM's word) completes it.

- **J1 — Case (a), hand over now.** Capture an enemy lord (so you hold one). `cr.deliver_prisoner [lord]`
  seeds the bargain targeting that captive. Talk to the lord and offer to deliver them → the NPC emits
  give_prisoner → the captive leaves your **Prisoners**, is in the lord's party, and you get 500g/+5.
- **J2 — Wrong / no captive.** With no matching captive held, agreeing should NOT transfer anyone (the
  NPC says you don't hold the one they asked for) — nothing is faked.
- **J3 — Case (b), capture-and-deliver (emergent).** In real play, a lord with an enemy may set a
  deliver_prisoner bargain naming a lord you do NOT hold; it stays outstanding until you capture them,
  then deliver as in J1. (Issued by the LLM; the prompt teaches it.)
- **J4 — Issuance is grounded.** The deed only forms when the named target resolves to a real lord/
  faction (QuestFactory); a JoinParty reward only when the giver is a recruitable wanderer.

### K. Declare-war deed (NEW — untested in game)

**What it proves:** a lord can ask the player to declare war on a faction, and ONLY the player's own
declaration (never being attacked) completes it.

- **K1 — Player declares → done.** `cr.declare_war_quest [lord]` seeds the deed targeting a faction you
  can still declare war on. Open the kingdom screen and declare war on that faction → the deed stamps
  "Declared war on … " (notification), and reporting back to the lord pays the reward (1000g/+8).
- **K2 — Attacked ≠ done.** If that faction declares war on YOU (or you go to war with a *different*
  faction), the deed must NOT complete.
- **K3 — Grounded issuance.** The deed only forms for a real faction you are not already at war with and
  not your own (QuestFactory `BindDeclarableWarTarget`); already-at-war or self → refused.

### L. Dynastic succession (NEW — untested in game)

**What it proves:** on player death the heir inherits standing, debts, and grudges — not the predecessor's
private history — and NPCs address the heir as the new person.

- **Setup:** with an NPC you have a rich history with, ideally including a private event (`cr.memory [name]`
  to see it; a Flirt/Intimacy/Captivity/Jealousy entry).
- **L1 — Private history dropped.** `cr.succession` (uses your parent as predecessor) → `cr.memory [name]`
  shows the Flirt/Intimacy/Captivity/Jealousy events are gone; the political/relational ones remain.
- **L2 — Inherited framing.** Talk to that NPC: they should speak of the predecessor as your kin ("your
  father once…") and treat YOU as the new person — never address you as the dead predecessor.
- **L3 — Real death.** In actual play, when the player dies and an heir takes over
  (`OnPlayerCharacterChanged`), the same reframing runs automatically across all NPCs.

### M. Prisoner bargains for freedom — case A (NEW — untested in game, the entry point still to confirm)

**What it proves:** a lord YOU hold captive can offer a reward to be set free, and the deal really
releases them.

- **Setup:** `cr.capture [lord]` takes a lord prisoner (no battle needed).
- **M1 — Entry point (the open question).** Speak with that captured lord — in their cell at a fief
  dungeon, or however the game lets you. **Does "We need to talk…" appear?** If yes, the entry is free
  (`hero_main_options`). If NOT, tell me the dialogue you DO get and I'll hook the right token.
- **M2 — The bargain.** In the CR chat, the lord should offer to bargain for freedom (goodwill, a
  ransom they'll pay, or — if their house is broken — to swear to your clan). A proud lord may refuse.
- **M3 — Release.** Agree to free them → the NPC emits `free_prisoner` → they actually leave your
  Prisoners (no longer captive), and any promised reward (relation / gold / join) applies.
- **Guard:** `free_prisoner` only frees a lord you genuinely hold — a stray emission can't free anyone.

### N. Prisoner bargains for freedom — case B (NEW — untested in game)

**What it proves:** a lord held by ANOTHER faction can ask you to break them out (a rescue deed), paid
only when you actually free them.

- **Setup:** find a lord currently a prisoner of an enemy (common in war — check the encyclopedia), and
  visit them (their captor's dungeon). The context flags them `NpcIsPrisonerOfAnother`.
- **N1 — The rescue bargain.** Speak with them in the CR chat: they may offer gold + goodwill if you
  break them out, and issue a `rescue_prisoner` deed targeting THEMSELVES (no target named).
- **N2 — Verified only on a real rescue.** The deed stays outstanding until you actually free them —
  defeat their captor in the field or storm the settlement holding them. Then it stamps done (reusing
  the existing rescue verification), and reporting back pays the reward. A promise alone never completes it.

### O. Influence loop — NPCs hear of YOUR deeds (NEW — untested in game)

**What it proves:** the world reflects the player's own acts back. A lord who was NOT present but whom
word has reached references your deed, attributed to you, and it colours how they open — wary/afraid if
it cut against their side, respectful if it served it. (Distinct from the third-party "what you've heard"
gossip, and from the battle stance bumps, which only touch lords who were actually there.)

- **Setup (no siege needed):** `cr.deed_heard [lord]` seeds a notable feat of yours that wounds that
  lord's own faction, and prints back what they would now have heard. Pick a lord of a faction other
  than your own for a clean "(this cut against you or your own)" tag.
- **O1 — Attributed, not anonymous.** Open the CR chat with them: they should allude to your deed as
  *yours* ("they say you broke our host…"), hedged as hearsay — never recite it as a list, never claim
  to have witnessed it.
- **O2 — Valence colours the tone.** Against their side → wariness/resentment/fear in how they greet you;
  a lord of your OWN faction seeded the same way reads it as a boon (pride/respect). A lord too far away
  or of an unrelated minor faction simply won't have heard (the command says so).
- **O3 — Reputation, not a stance change.** Their persisted Trust/Respect/Fear (`cr.stance`) is NOT moved
  by merely hearing — only real, witnessed acts move it. The deed colours the *conversation*, not the save.

### P. Companion missions — "bring me the news" (NEW — untested in game)

**What it proves:** a companion **physically leaves your party** (as their own clan party on the map),
rides to a town, lingers, and rides back with an account of the realm's doings — scaled by their Scouting
skill + a roll. News has a *source* and a *delay*. The errand **persists across saves** (no save-scum) and
**survives capture** (taken prisoner → the news waits and is delivered when they rejoin you).

- **Setup:** at least one companion in your party. World-events to report (play a while, `cr.deed_heard`,
  or let battles/sieges happen).
- **P1 — Dispatch (physical).** `cr.send_news` → your first companion **leaves the party** and a party
  named for them appears on the map, riding to a town. `cr.send_news Sturgia` scopes it (they ride to a
  **Sturgian** town); a bare command picks the nearest. `cr.missions` shows the phase/destination. A second
  send on the same companion is refused; a too-unskilled one is refused.
- **P2 — Watch them travel (💾).** Follow the party to the town, see it linger, then ride back to you. On
  reaching you they **rejoin your party** and a report banner appears: a **full** briefing on a good roll,
  a **thinner** one on a middling roll, **empty-handed** on a bad one. **Save/reload mid-journey** — the
  errand and the party resume; you cannot reload to re-roll or to get the news instantly.
- **P3 — Skip the travel (test).** `cr.mission_return` force-completes the first errand now (gather +
  rejoin + report) so you needn't ride along to verify the report.
- **P4 — Ask in conversation (any errand).** Open a CR chat with a companion and ask them, in your words,
  to go do one of: bring back news / scout a place / steal coin / trade for profit / sound out a faction —
  optionally naming a faction/place. They agree, leave, and the errand registers (the LLM picks the matching
  `errand`). Same as the `cr.send_*` commands but player-driven; they do NOT do it on the spot, they bring
  the result back. (Offer is taught to any own-companion in your party who is not already away; the specific
  skill is checked at dispatch.)
- **P5 — Capture resilience.** If the errand party is beaten and the companion taken prisoner, the errand
  flips to *AwaitingReunion* (see `cr.missions`). When you **free them** (they rejoin your party — e.g. your
  rescue), they deliver whatever news they had. (Pairs with the prisoner-rescue feature.)
- **P6 — The report is a CONVERSATION.** On return you get a banner "X has returned — have a word with
  them." Open a CR chat with that companion: they should **open by reporting back in their own voice** —
  weaving in what they learned (or "the roads were quiet", or "I was waylaid"), not reciting a list. It is
  delivered ONCE (consumed), and **persists across a save** taken between their return and your talk.
- **P7 — Spy & steal (same skeleton, Roguery).** `cr.send_spy [about]` → on return the companion reports
  **intel** (garrison, militia, prosperity, where the lord is) in conversation. `cr.send_steal [about]` →
  on return your coffers gain the **loot** (full on a good roll, ~40% on a partial, nothing on a failure)
  and they tell you of it. Both are risky errands — the road party can be caught (→ P5). Scope works too:
  `cr.send_spy Vlandia` rides to a Vlandian town.
- **P8 — Barter & envoy.** `cr.send_barter [about]` (Trade) → a trading **profit** reaches your coffers on
  return and they tell of it. `cr.send_envoy [about]` (Charm) → the companion brings back a read on the
  **faction's mood** (who they war with, how settled the town is) in conversation. Same journey, payload by
  type: news/intel = words, steal/barter = gold, envoy = mood.
- **P9 — Recall by conversation (A).** Send a companion out (`cr.send_news`), then ride to their party on
  the map and open a CR chat with them. Ask them, in your words, to **abandon the errand and come back with
  you**. They should agree AND actually return — the errand vanishes from `cr.missions` and they are back in
  your party (check the party screen). This is the anti-AI-Influence guarantee: agreement is a *verified
  deed*, never a "yes" that the companion then ignores forever. (If they refuse the task by choice, fine —
  but if they agree, they MUST come home.) `cr.journey_recall` is the console safety net (recalls everyone).
- **P10 — Recall by letter (B).** Send a companion out, then hire a tavern courier (talk to a tavernkeeper →
  "Hire a Courier") and write to that away companion. Within a day or two they **abandon the errand and ride
  home on their own** — watch `cr.missions` empty and the companion reappear in your party. `cr.recall_letter`
  is the console test (schedules the recall on the first away companion; advance a couple of days). A captured
  companion can't obey a recall yet — the order waits until they are free, then takes effect.

### Q. Companion happiness (NEW — untested in game)

**What it proves:** each companion carries a CONTENTMENT score (their satisfaction in your service, distinct
from relation), moved by errand outcomes, shown on their encyclopedia page.

- **Q1 — Encyclopedia line.** Open a companion's encyclopedia page → a **"Contentment: <band> (N)"** line
  appears (a sibling of "Personal regard"), shown only for your own companions. `cr.happiness_set 20 [name]`
  then reopen the page → it reads the lower band ("restless"/"disgruntled"). Non-companions show no line.
- **Q2 — Errands move it.** Send a companion on an errand (scenario P) and let it resolve: a **success**
  nudges contentment up, a **failure** down, an errand that **suits** them (high governing skill) up; being
  **captured** stings hardest, being **freed** recovers a little. Check with `cr.happiness [name]`.
- **Q3 — The leave floor.** `cr.happiness_set 10 [name]` → `cr.happiness` reports "**WANTS TO LEAVE**".
- **Q4 — Grievance in conversation.** Set a companion low (`cr.happiness_set 30 [name]` restless, `15`
  disgruntled/leaving) and open a CR chat with them: they should **voice their discontent in their own
  voice** — a coolness, a complaint, or (at the floor) that they mean to leave. Content companions say nothing.
  If you then **address it sincerely** in chat (apologise, promise better, a kindness), they may soften
  (`reassure_companion` → a small contentment bump) — empty words should not move them.
- **Q5 — Drift + departure.** `cr.happiness_tick` runs a weekly pass: an idle companion's contentment
  eases down (and toward baseline). A companion kept at the leave floor for a **season** (≈21 days; tick a
  few times after `cr.happiness_set 5`) eventually **leaves your service** (a banner + they exit the clan).
  An employed companion (on an errand) takes no idle hit that week.

### R. Camp life — companions and each other (NEW — untested in game)

**What it proves:** companions form opinions about EACH OTHER from trait compatibility, voice them to you,
and the well-matched may wed (staying in your clan).

- **Setup:** have ≥2 companions in your party with contrasting traits (an honourable one + a cruel/dishonest
  one will feud; two of like temper will bond).
- **R1 — Relations drift.** `cr.rapport [name]` shows their feeling toward each fellow companion. Run
  `cr.rapport_tick` a few times: compatible pairs warm (toward Cordial/Close), clashing pairs cool (toward
  Tense/Feud), bounded.
- **R2 — They raise it.** Once a feeling is strong (`cr.rapport` shows ≥25 or ≤−25), open a CR chat with that
  companion: they may **bring up their fellow companion in their own voice** — praise or a complaint — only
  if it fits, never forced.
- **R3 — Companion weddings.** Two warm (≥40), unmarried, opposite-gender companions may **wed** on a tick
  (`cr.rapport_tick` a few times after warming a pair). They **stay in your clan** — confirm both remain
  yours (the design: the clan grows, no one leaves to found a household).

### T. Scripted scenarios — world-reaction engine (NEW — untested in game)

**What it proves:** JSON scenario packs load at session start, fire real world-consequences (war declarations,
relation changes, injected NPC memories) when a game event matches a rule's trigger, and one-shot rules seal
permanently so reloading cannot re-fire them.

- **T1 — Rhagaea's death triggers two wars.** `cr.scenario_fire ON_DEATH Rhagaea` → the Southern Empire's two
  rivals should **declare war** on it immediately, and a world-event message ("the eagles turn upon a leaderless
  South") is recorded. Confirm with `cr.rumor` (commoners repeat it) and the kingdom diplomacy screen.
- **T2 — One-shot sealed.** After T1, `cr.scenarios` shows `rhagaea_death_eagles_turn` in the fired list.
  `cr.scenario_fire ON_DEATH Rhagaea` again → **nothing fires** (the rule is sealed). 💾 Save/reload:
  it stays sealed — cannot save-scum the trigger.
- **T3 — Rhagaea marriage draws randomly.** Actually marry Rhagaea (or `cr.scenario_fire ON_MARRIAGE Rhagaea`
  several times on different fresh saves). The four rules share the trigger — only ONE fires per campaign.
  Across several tries you should see different outcomes (consort / two-crowns / regent / suspicious). The
  chosen rule injects a memory into Rhagaea (`cr.memory Rhagaea`) and optionally changes relations or influence
  depending on which rule fired.
- **T4 — Scenario Composer app.** Open `Tools/ScenarioComposer.html` in a browser (no install). Build a custom
  rule: pick a trigger, add a guard (`IF_HERO_IS:Aelius`), add a consequence (`CHANGE_RELATION:Aelius:+5`).
  Click **Export JSON**, drop the `.json` into `Modules/CalradiaRemembers/ModuleData/scenarios/`, restart the
  game, and `cr.scenarios` lists your rule. `cr.scenario_fire <trigger>` fires it; the relation change applies.
- **T5 — Reentrancy guard.** Fire a rule that has a `DECLARE_WAR` consequence (which itself emits a
  `WarDeclared` event). The guard inside `ScenarioRuntime` should prevent an infinite cascade — no stack
  overflow, scenario fires once cleanly.

### U. Generalized pregnancy — consensual + NPC mothers (NEW — untested in game, adult-gated)

**What it proves:** conception chance now arises from ANY completed vaginal act, not only captive scenes;
female player OR female NPC can be the mother (resolved by sex/age); the prompt teaches it at Explicit+.

- **Setup:** Adult Content = **Explicit** or higher (MCM). Use `cr.preg` / `cr.preg mother` to verify without
  waiting for a random roll.
- **U1 — Female player conceives (player-as-mother).** Female player, male NPC, open a CR chat and conduct an
  explicit vaginal scene. The prompt should mention **impregnation_risk** and name the NPC as father.
  `cr.preg [npcName]` force-triggers a conception → the engine's pregnancy tracks correctly (verify by waiting
  a Bannerlord birth cycle — ~84 days — or inspecting the hero list for a newborn in your clan).
- **U2 — NPC mother conceives (male player).** Male player, female NPC. Same scene type. The prompt teaches
  `impregnation_risk` from the NPC's side ("inside her"). `cr.preg mother <npcName>` force-triggers the NPC's
  pregnancy → the child should land in the **NPC's clan** (the open clan spike — report if the child ends up
  in your clan instead; that is the known edge case for `DeliverOffSpring` with a `MainHero` father).
- **U3 — Same-sex pairing → no hint.** Female player + female NPC, or male player + male NPC: the
  `impregnation_risk` block should be **absent** from the prompt (check with Story Log on). `cr.preg` still
  works as a force override, but no in-scene hint is given.
- **U4 — Age gate.** `cr.preg mother <anNpcOver45OrUnder18>` should be **refused** with a log note; the
  ConceptionResolver clamps fertile window to 18–45.
- **U5 — Unknown father (bandit).** If the player is female and the scene is a bandit captive scene (no named
  NPC hero), `cr.preg` (no argument) → the child's father should show as the shared hidden `cr_unknown_father`
  placeholder, not crash.

### V. ProvideGold quest — Give Gold button (NEW — just built, untested)

**What it proves:** the "Give Gold" button gives the player a discoverable path to satisfy a ProvideGold
quest (e.g. a spouse's "financial support for your child" quest) — previously there was no button and the
only path was free-typing in chat, which players couldn't find.

- **Setup:** acquire a ProvideGold quest from an NPC (spouse/companion asking for financial support; or
  `cr.quests [name]` to see if one is outstanding; the quest is issued by the LLM in conversation).
- **V1 — Button visible.** Open a CR Conversation chat window: a **"Give Gold"** button appears to the right
  of the bag icon, visible whenever the exchange is active (same gating as "Offer item").
- **V2 — Amount prompt.** Click **Give Gold** → a native text-inquiry popup appears: "How many denars? (You
  carry N.)" Enter an amount and confirm → the chat input pre-fills with "I would like to give you N denars."
- **V3 — NPC emits take_gold.** Send the message → the NPC responds and emits `[ACTION] type: take_gold
  amount: N` → gold is transferred (watch your purse) AND the ProvideGold quest stamps done (`cr.quests [name]`
  shows it satisfied).
- **V4 — Cancel is safe.** Click Give Gold → cancel the prompt → nothing happens, input unchanged.
- **V5 — Zero / invalid input.** Enter 0 or letters → nothing pre-fills (guard in ExecuteOfferGold).
- **V6 — Capped to carried gold.** Enter more than you carry → input pre-fills with what you actually have.

### S. Intimacy as currency — Negotiation Phase 3 (NEW — untested, adult-gated)

**What it proves:** a cynical, unrelated man may, from leverage, offer the FEMALE player a favour in exchange
for intimacy — a cold transaction, not courtship — and once it happens he holds it over her. Strictly gated.

- **Setup:** female player; **Adult Content ≥ Explicit** (MCM). Find a man of low scruple (dishonourable +
  calculating/callous) who is NOT in your clan. `cr.bargain [name]` should report **eligible: true** (it
  reports the failing condition otherwise — male player, honourable man, kin, or tier too low → false).
- **S1 — The proposition.** Open a CR chat and raise a need he could exploit (money trouble, safe passage,
  a favour). He may proposition you transactionally — and **take a clear refusal** for an answer (never out
  of nowhere, never courtship). On an honourable man / male player / below Explicit, the section is never taught.
- **S2 — Leverage persists.** After such an intimacy (or `cr.bargain_leverage [name]` to force it),
  `cr.bargain` shows **leverage held: true**, and a later chat has him **press the advantage**, reminding
  you of what passed. It persists across a save.

---

## 3. What is already unit-tested (do NOT re-test by hand)

These are proven by `dotnet test` (430 tests); only their *wiring* (above) needs in-game eyes:

- **RelationGate**: +1 cap, warmth cooldowns, intimacy +3 bypass, −15 / −100 floors.
- **Stance maths**: word cap (±2) + 8h cooldown, deed cap (30), fear decay, axis clamps, and the
  posture-block wording for every believable combination.
- **Scheme lifecycle**: plotted→progressing→discovered/succeeded/failed transitions, the roll bands,
  and the player-assist / warn / backlash rules (incl. the tiered treason/friend/neutral/enemy penalty).
- **Interception decision**: which trigger fires, the notoriety/cooldown/one-pursuer guards, and the
  give-up-the-chase rule.
- **Lord-recruitment gate**, **power-balance wording**, **marriage eligibility**, **letter cadence**,
  **captive intent/probability ladder**, **public-affront thresholds**, **third-party descriptions**.
- **Courier-window rules** (`LetterComposerState`): which parts of the parchment show per mode, and the
  can't-send-blank / can't-afford-the-courier guards.
- **Authored-character rules** (`CharacterOverride` / `CharacterOverrideSet`): trait clamp to −2..+2,
  unset traits left vanilla, and resolution (id wins over name, an id-less homonym is refused not guessed).
- **Prisoner-deed matching** (`PrisonerDeedTarget`): a named hero wins over a faction demand, faction
  match is case-insensitive, a targetless demand matches nothing, and "do I already hold one" (case a vs b).
- **War-deed matching** (`WarDeedMatcher`): satisfied only when the player's faction is the aggressor
  against the demanded faction — never when attacked, never for an unrelated war.
- **Succession rules** (`SuccessionMemoryPolicy` / `SuccessionKinship`): private events (flirt, intimacy,
  captivity, jealousy) do not cross to the heir while political/relational ones do; correct kinship word.
- **Prisoner-release branching** (`PrisonerReleasePolicy`): only a captive lord may bargain; your own
  captive is an immediate release, another lord's captive is a rescue deed, a non-prisoner gets nothing.
- **Conception resolver** (`ConceptionResolver`): fertile-mother window (18–45), same-sex → None, player-as-
  mother vs NPC-as-mother vs None, dead participant → None.
- **Scenario engine** (`ScenarioSelector`): trigger matching (including wildcard), eligibility filter (one-shot
  seal + guard eval), weighted random draw; `GuardEvaluator` AND-combines, empty passes, unknown guard fails;
  `ScenarioArgs` parses `Hero:+N` and `Hero:free text` payloads; `ScenarioRule.Parse` clamps weight ≥ 1.

If one of these behaves wrong *in game* despite the math being right, it points at the **wiring** — log
it with Enable Logging on and the exact `cr.*` steps to reproduce.
