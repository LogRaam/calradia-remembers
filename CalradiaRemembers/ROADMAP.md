# Calradia Remembers — Roadmap

The long-term direction of the mod, agreed 2026-06-12. Each layer is shippable and
useful on its own, and each one feeds the next — the order is the architecture.

---

## Now — Nexus release (v1.0)

Ship the mod. The remaining gate items:

- [ ] In-game validation: witness same-location/social filters (Keep vs town, notables, companions picker ×2)
- [ ] In-game validation: Sprint 15A/15B (address a witness directly) and 15C (NPC↔witness exchange loop, cap 3)
- [ ] No-key UX: "We need to talk…" must fail gracefully with a clear message, never crash
- [ ] Confirm AdultLevel default is Mature in `CrSettings`
- [ ] Bug #1 (battle instead of chat after reload) — deferred pending a repro log with Enable Logging ON
- [ ] Commit both repos, publish

---

## v1.1 — Talk to everyone (commoners + settlement knowledge)

LLM conversations with generic townsfolk and villagers (non-Hero Agents — the
Townswoman, the dockworker). They are ephemeral by nature: **no memory, no
relation — ambient intel instead.** That trade-off is the design, not a limitation.

- **Settlement knowledge builder** (the load-bearing piece, built first): a game-side
  context block with what a commoner actually knows — who holds the town, current
  wars, which lords are present today (`LocationComplex` already tells us),
  security/loyalty/prosperity as street mood, recent raids, spotted hideouts nearby.
  Without this feed the commoner confabulates; with it, they're useful.
- **Rumor for coin**: the prompt teaches them to ask 10–30 denars (`take_gold`,
  already in the action pipeline) before sharing the juicier piece.
- **Slim LLM stack**: a second `NpcChatService` with quests, change_relation,
  [EVENT], discovery, and witnesses all disabled. Short prompt = cheap chats.
- Entry point on the commoner dialog token (not `hero_main_options`); transient
  synthetic profile (template name, settlement culture, persona from a small pool).
- Prototype path: `cr.rumor` console command before any dialog wiring.
- **Action vocabulary expansion** (tester suggestion): `give_troops` and `give_item`
  alongside the gold actions — same mold (verifiable, bounded, executed by the
  bridge; never a free-form world action). A lord granting you five recruits or a
  blade as reward is the natural next rung above `give_gold`.

## v1.2 — Rumor store (the foundation layer)

A persisted `WorldEventStore` (SyncData) of dated events: type, actors, place,
visibility, status. Three feeders, all cheap:

1. **Real campaign events** — `CampaignEvents` listeners: battles, sieges, marriages,
   executions, war declarations. Zero LLM. Include the **player's own battlefield
   performance** (kills, presence at a victory) recorded into the profiles of lords
   who fought alongside or against them — tester's immersion argument: "currently it
   is down to you having to tell people you fought well, which is a little boastful."
   NPCs who were there should simply know.
2. **Mod conversation outcomes** — insults before witnesses, furious
   `end_conversation`, public humiliation (already implemented, currently evaporates;
   this gives "word gets around" a literal home).
3. **Hero↔hero relation tracking** — native `GetRelation` between lords detects
   brewing rivalries mechanically and emits gossip beats.

Consumers: the commoner intel feature (its first visible client), NPC prompt
injection (filtered by geography/faction/recency — a Sturgian lord doesn't know
yesterday's Aserai gossip), and letters (an ally writes to warn you of a plot —
courier system already exists).

Rumors age, spread by region, and may distort with distance.

## v1.3 — NPC↔NPC rivalries and schemes

Active feuds: two hostile lords trying to harm each other.

**Governing principle: the simulation is mechanical; the LLM narrates only at the
player's point of contact.** A daily tick advances schemes probabilistically
(traits, relations, opportunity). The LLM is called only when the player touches
the affair — hears the rumor, is recruited as an agent, uncovers the plot.
Background LLM simulation would be an unacceptable API cost.

Schemes have a lifecycle: plotted → progressing → discovered / succeeded / failed,
with counterplay. Every state change feeds the rumor store.

## v2.0 — Player influence on the world (CK3-style)

Persuading a faction leader toward war, luring an enemy lord to defect,
assassination, espionage. The most powerful layer — and the most dangerous for
save balance — so it ships last, on a proven foundation. Two hard rules:

1. **No direct world action from a conversation.** Same philosophy as quests
   (verified deeds, never words): a successful conversation plants a *disposition*,
   not a war. The lord "brings it to his council"; days later the event lands (or
   doesn't), weighted by personality, relations, and kingdom state. An eloquent
   player must never trigger a world war in three messages.
2. **Build on native mechanics.** `ChangeKingdomAction` (defection),
   `DeclareWarAction`, `KillCharacterAction` all exist engine-side. The mod adds
   the decision layer and the narrative, not the mechanics.

---

## Also planned (unscheduled)

- **Scenario bank** for captive scenes: Twine→JSON, one-use-per-campaign via
  SyncData, eligibility by kinks/gender, prompt injection.
  (When adding persisted campaign state, REMOVE
  `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load) fires before
  OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).
- **Prompt caching split**: stable system prefix + dynamic second system message,
  so the `cache_control` breakpoint survives per-turn changes (Day, encounter).
