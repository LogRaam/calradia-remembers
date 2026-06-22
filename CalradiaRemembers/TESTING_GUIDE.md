# Calradia Remembers — Tester's Guide

Console commands + the in-game scenarios that must be checked by hand.

**Scope.** The mod's pure decision logic (relation caps, scheme lifecycle, recruitment gates, the
stance maths, power-balance wording, marriage eligibility, etc.) is already covered by 250 automated
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
| `cr.preg [name]` | Test the captive-pregnancy hook. |
| `cr.escape_force` | Force the captive-escape path. |

### Chronicle & world rumors
| Command | Does |
|---|---|
| `cr.chronicle` | Print/write the player's chronicle. |
| `cr.chronicle_open` | Open the in-game illuminated chronicle viewer. |
| `cr.chronicle_illuminate` | Generate the AI-illuminated prose. |
| `cr.chronicle_rewrite` | Re-illuminate from scratch (after editing the style file). |
| `cr.rumor [settlement]` | Show the street rumours a commoner here would share. |

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
**Discovered** and is far likelier to **fail** over the next `cr.scheme_advance` ticks. X's regard for
you rises; Derthert's drops (he suspects the meddler).

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

---

## 3. What is already unit-tested (do NOT re-test by hand)

These are proven by `dotnet test` (266 tests); only their *wiring* (above) needs in-game eyes:

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

If one of these behaves wrong *in game* despite the math being right, it points at the **wiring** — log
it with Enable Logging on and the exact `cr.*` steps to reproduce.
