# Calradia Remembers.. Console Commands Guide

Every Calradia Remembers command lives in the `cr` group and is typed in the Bannerlord developer
console. There are **two families**, and telling them apart is the whole point of this guide:

- **Cheats & player tools**.. things YOU can use in your own campaign: hand out gold, fix a bad memory,
  seed a prior relationship, inject a rumour, read your saga. Safe to use; some change your save.
- **Developer & test commands**.. tools I use to verify the mod still works, and that seed or force
  internal state for testing. They are **not gameplay features**; running them won't break anything,
  but they are not how you "play" the mod, and a few of them only make sense to a developer.

> **The live reference is in-game.** Type **`cr.help`** for the full command list and **`cr.test list`**
> for the self-tests. This file explains what they're *for* and how to use the useful ones well; the
> in-game lists are always current even if this document drifts.

---

## Opening the console

1. In the launcher, open the game's **engine options** and **enable cheats** (or run with the cheat
   flag). Without this the `cr` commands are not registered.
2. In a loaded campaign press **`Alt` + `~`** (the key left of `1`) to open the console.
3. Type a command and press Enter. Most take an optional **`[name]`** at the end.. a fragment of a
   hero's name (e.g. `Derthert`). Omit it while you have a CR conversation open and it targets that NPC.

`< >` marks a required argument, `[ ]` an optional one.

---

# Part 1.. Cheats & player tools

These are for you. The ones that change your campaign are marked **(save)**.. they persist when you
save the game.

### Gold

| Command | What it does | Example |
|---|---|---|
| `cr.give_gold <amount> [name]` | The NPC gives YOU gold. **(save)** | `cr.give_gold 5000 Derthert` |
| `cr.take_gold <amount> [name]` | You give the NPC gold (also satisfies a "bring me N denars" task). **(save)** | `cr.take_gold 1000` |

### Standing with an NPC

| Command | What it does | Example |
|---|---|---|
| `cr.info [name]` | Show your standing, the relation-gain cooldown, and captive status. Read-only. | `cr.info Liena` |
| `cr.relation <delta> [name]` | Nudge how an NPC regards you (the same gated path a conversation uses). **(save)** | `cr.relation 10 Liena` |
| `cr.relation_intimate <delta> [name]` | Same, as an intimacy event (cap +3, ignores the cooldown). **(save)** | `cr.relation_intimate 3 Liena` |
| `cr.relation_reset [name]` | Clear the "already gained relation recently" cooldown so the next gain lands. **(save)** | `cr.relation_reset` |

### NPC memory.. fix it or curate it

When the AI records something wrong, or you want someone to *not* start as a stranger, edit their
memory directly. These write **straight into one NPC's memory**, so that character reliably knows it
(unlike a world rumour, which only spreads by hearsay.. see `cr.world_event`).

| Command | What it does | Example |
|---|---|---|
| `cr.memory [name]` | List every event an NPC remembers, each with an index. Read-only. | `cr.memory Liena` |
| `cr.memory_add <days_ago> <text> [name]` | Add a memory dated in the past.. build a prior relationship. **(save)** | `cr.memory_add 30 We fought side by side at Pendraic. Derthert` |
| `cr.memory_set <index> <new text> [name]` | Replace the text of one event (fix a bad AI line). **(save)** | `cr.memory_set 2 She remembered me warmly. Liena` |
| `cr.forget <index> [name]` | Delete one event. **(save)** | `cr.forget 1 Liena` |
| `cr.forget_all [name]` | Wipe an NPC's whole memory for a clean slate. **(save)** | `cr.forget_all Liena` |

> The `[name]` is optional: with a CR conversation open it targets that NPC; otherwise the last word is
> tried as a name, and if it doesn't match a hero the whole text after the index is the new memory.
> Memory lives in your save, so **save the game** after fixing.

### World news.. inject your own rumour

| Command | What it does | Example |
|---|---|---|
| `cr.world_event <what happened>` | Inject a world rumour your NPCs may gossip about. Any **kingdom named in the text** is auto-tagged so its people are likeliest to have heard it. **(save)** | `cr.world_event A great fire gutted the Vlandian docks at Sargot.` |

This is a *rumour*: it spreads by CR's hearsay rules (an NPC mentions it only if they'd plausibly have
heard it.. distance, faction, recency.. and as hearsay when it fits, never recited). **Ask a nearby NPC
about recent news** to draw it out. To make ONE specific NPC know a precise fact, use `cr.memory_add`
instead.

### Your saga (the Chronicle)

| Command | What it does | Example |
|---|---|---|
| `cr.chronicle` | Print your chronicle (your deeds, in a chronicler's voice) to the console. | `cr.chronicle` |
| `cr.chronicle_open` | Open the illuminated parchment Chronicle window. | `cr.chronicle_open` |

### Your LLM models

| Command | What it does | Example |
|---|---|---|
| `cr.model [toggle]` | Show the active model mode (Roleplay or NSFW), the model id it resolves to, and whether the in-chat mode button would show. Pass `toggle` to flip the mode (the same override as the button). | `cr.model toggle` |

### Your edited prompt files

| Command | What it does | Example |
|---|---|---|
| `cr.reload_lore` | Reload the editable `ModuleData` files (`world.txt`, `behavior_guidelines.txt`, `character_overrides.json`, `post_history_instructions.txt`, …) mid-campaign, so your edits apply without restarting. | `cr.reload_lore` |
| `cr.help` | List every `cr` command. | `cr.help` |

---

# Part 2.. Developer & test commands

**Not gameplay features.** These verify the mod works and seed/force internal state for hand-testing.
They are safe to run but are not how you play; many only make sense to a developer. The full list is
`cr.help`; the self-tests are `cr.test list`.

### The self-test harness

One harness drives the mod's systems end to end and reports PASS / FAIL / MANUAL for each.

| Command | What it does |
|---|---|
| `cr.testall` | Run the routine sweep: every self-test except the irreversible ones. |
| `cr.test list` | List the registered self-tests (name + severity + description). |
| `cr.test <name>` | Run one self-test (e.g. `cr.test pregnancy`, `cr.test bastard_claim`). |
| `cr.test chat` | Run only the conversation smoke tests (real LLM calls; full replies shown). |
| `cr.test mech` | Run only the mechanical tests (no LLM call, no API cost). |
| `cr.test full` | Run **everything**, including the irreversible tests. **Throwaway save only.** |

The irreversible tests (`declare_war`, `scenario`) really declare a war / seal a one-shot scenario in
the live campaign, so they're held out of `cr.testall` and run only on explicit `cr.test <name>` or
`cr.test full`. A reload is the only undo. The chat tests each make one real LLM call and log the full
Q&A, stamped with the active model.. change the model and re-run to compare LLMs on the same questions.

### Diagnostics (read-only inspectors)

Safe to peek at internal state; they change nothing.

| Command | Shows |
|---|---|
| `cr.standing` | The player's clan standing summary. |
| `cr.stance [name]` | An NPC's posture (Affection / Trust / Respect / Fear) + the prompt block. |
| `cr.schemes` / `cr.scenarios` | Active feud plots / scripted-scenario state. |
| `cr.quests [name]` / `cr.missions [name]` | An NPC's quests / companion missions and their state. |
| `cr.bastards` / `cr.bastard_mother [name]` / `cr.bastard_blackmail [name]` | The player's tracked bastards / a mother's disposition / blackmail leverage. |
| `cr.jealousy [name]` | The jealousy reaction an NPC would have to a romantic act. |
| `cr.happiness [name]` / `cr.rapport [name]` | Companion contentment / companion-to-companion rapport. |
| `cr.bargain [name]` / `cr.hire_info [name]` / `cr.recruit_info [name]` | Intimacy-bargain eligibility / hire price / clan-recruit eligibility. |
| `cr.kinks [name]` | An NPC's kinks + the valid kink names. |
| `cr.profile_check [name]` / `cr.journal_check` | A profile's romantic-compatibility diagnosis / the quest journal mirror. |
| `cr.rumor [settlement]` | The current world events a settlement's commoners would repeat. |

### Seed / force / simulate (for hand-testing a feature)

Each of these stages a feature so it can be checked in one step. They mutate the live campaign.

| Area | Commands |
|---|---|
| Relations / stance | `cr.rep_set <value>`, `cr.stance_set <trust> <respect> <fear>`, `cr.discover_test` (seed 3 discoveries) |
| Schemes | `cr.scheme`, `cr.scheme_advance`, `cr.scheme_assist`, `cr.scheme_warn`, `cr.intercept` |
| Quests / negotiation | `cr.quest`, `cr.quest_done`, `cr.quest_complete`, `cr.quest_abandon`, `cr.quest_gold`, `cr.quest_scout`, `cr.declare_war_quest`, `cr.deliver_prisoner`, `cr.bargain_items`, `cr.bargain_marriage`, `cr.bargain_leverage` |
| Marriage / pregnancy / succession | `cr.marry`, `cr.marriage_approve`, `cr.marriage_revoke`, `cr.marriage_status`, `cr.preg`, `cr.preg_birth`, `cr.succession`, `cr.backstory` |
| Bastards | `cr.bastard_check`, `cr.bastard_tag`, `cr.bastard_season`, `cr.bastard_claim`, `cr.bastard_demand`, `cr.bastard_pay` |
| Companions (missions / camp) | `cr.send_news`, `cr.send_spy`, `cr.send_steal`, `cr.send_barter`, `cr.send_envoy`, `cr.mission_return`, `cr.journey_recall`, `cr.recall_letter`, `cr.happiness_tick`, `cr.happiness_set`, `cr.rapport_tick` |
| Letters / couriers | `cr.letter`, `cr.letter_compose`, `cr.letter_read`, `cr.letter_reply`, `cr.courier`, `cr.courier_slow` |
| Captivity / ambush | `cr.capture`, `cr.ambush`, `cr.ambush_lose`, `cr.ambush_gate`, `cr.ambush_scene`, `cr.ambush_audience`, `cr.escape_force` |
| Adult scenes (Hardcore) | `cr.crc_scene`, `cr.captor_scene`, `cr.bandit_scene`, `cr.bandit_queen`, `cr.add_kink`, `cr.remove_kink` |
| Witnesses / world events | `cr.witness`, `cr.witnesses`, `cr.deed_heard`, `cr.realm_news`, **`cr.feed_test`** |
| Chronicle (regenerate) | `cr.chronicle_illuminate`, `cr.chronicle_rewrite` |

> **`cr.feed_test`** is the developer test of CR's *inbound Feed API*.. the door **other mods** use to
> push world events into CR. It is the test counterpart of the player-facing `cr.world_event` (Part 1).
> If you just want to inject your own rumour, use `cr.world_event`.

---

*The systems this mod drives change quickly. When in doubt, `cr.help` and `cr.test list` are the source
of truth in-game.*
