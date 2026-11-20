# Calradia Remembers: Roadmap

## Mission

**Calradia Remembers étend Mount & Blade II Bannerlord là où la simulation s'arrête : dans la mémoire et la parole des personnages.**

Le mod repose sur trois engagements :

**1. Respecter l'esprit de la communauté.**
Le modding vit du partage et du respect, des créateurs envers les joueurs, des joueurs envers les créateurs, et de tous envers le jeu original. CR s'inscrit dans cette tradition : code ouvert, architecture extensible, documentation honnête sur ce qui est testé et ce qui ne l'est pas. Pas de magie noire, pas de promesses creuses.

**2. Enrichir Bannerlord sans le trahir.**
L'objectif n'est pas de créer un monde parallèle de roleplay déconnecté du jeu. Les NPCs de CR se souviennent, réagissent et parlent, mais ils restent des seigneurs de Calradia, pas des personnages de roman. Les actions qu'ils promettent arrivent dans le journal. Les relations qu'ils forgent s'inscrivent dans la carte politique. CR amplifie ce qui est déjà là ; il ne le remplace pas.

**3. Un code à la hauteur de l'ambition.**
Un mod qui dure s'appuie sur une architecture qui résiste. Logique pure isolée du moteur, seams injectés, tests qui documentent les comportements attendus, pas les détails d'implémentation. Quand Gabriel lit les tests, il lit la spec. Quand un comportement change sans le vouloir, un test le dit avant que le joueur ne le découvre.

---

The long-term direction of the mod. Each layer is shippable on its own; the order is the architecture.
Full detail for shipped work lives in `CHANGELOG.md` and git history, this file tracks what is DONE at a
glance and the OPEN work ahead.

> **This document is load-bearing, keep it maintained throughout development.** The roadmap is not a
> formality: it is the project's **brainstorming surface** and its **strategic tracking** tool. Every design
> discussion, parked idea, deferred piece, open question, and shipped milestone belongs here, recorded as it
> happens, so the direction stays legible across sessions and nothing of value is lost. Update it the moment
> a feature is wished, decided, started, or finished; prune what is stale; verify against the code before
> calling something "shipped" or "a gap." A current roadmap is how we choose the next step with confidence.

Cleaned up 2026-06-23, and again 2026-11-19: every section checked against the code; shipped ones collapsed to one
line under SHIPPED, their full text moved word for word to `ROADMAP_ARCHIVE.md`.

---

## OPEN WORK AT A GLANCE (verified against the code 19/11/2026)

Every section below this list is still open or partly built; its heading says what is built and what is not. Shipped
work is one line each under SHIPPED near the end, and the full text of every collapsed section is kept word for word in
`ROADMAP_ARCHIVE.md`.

**Recent, from November 2026 (not yet in a section):**
- Long Memory steps 3 and 4: world events, then the player's own book, built on the existing Chronicle (ChronicleBehavior, ChronicleWriter, WorldEventStore). Long Memory itself (steps 1-2, threads) SHIPPED 2.8.15, off by default; measured 19/11 (docs/discord/long-memory.md).
- Witnesses and envoys in memory (Gabriel 17/11, needs more thought): does a speaker know he was heard; a lord remembers what his envoy REPORTED, not the audience; an envoy may leave things out, never invent.
- Schmidt's ideas (Discord 16/11, awaiting Gabriel): alleys, a parley at a siege, talking in an army, player facts at creation; and party_may_join in tales (18/11).
- Tale endings chosen by what was found (Schmidt 18/11, for Gabriel).
- A visible notice when regard is gained (Lordfadooboo 16/11, for Gabriel).
- Removing CR from a save (Magadon93): test in game, then cr.prepare_removal closing CR journal quests.
- Hide or NSFW-redo refusals in Standard composition (Aporo, decision pending).
- Vexilary (19/11): a letter window that freezes on small local models, cause unknown, log asked.

**Open tails of shipped work:** Show Mercy as a kindness; the model's review of war and peace (DepaHN); first impressions
for notables and wanderers; a companion's moral concern about the player's deed; jealousy from letter-borne romance, a
betrothal concept, the courted one's own consort; an honest courier ETA in the picker; parser deferrals A4 (verb aliases),
B5 (end_conversation guard), D (conformance telemetry); more scenario consequence words (GIVE_FIEF, SEND_LETTER); a
profile for stranger witnesses and a romance override block; player height and retiring player_description.txt.

**Sections below (open or partly built):** Jev option; one path for actions; words that move the world; invited
characters seen; riding in another's train; council at sea; the captive player's offer; characters who come with a task;
one concept under several names; ask the village; a rendezvous the game keeps; escort a person; a companion acts on
order; two hosts; a world event from the map; almanac panels; narrative style per character; behaviour during battles;
chat and the escape menu; a prisoner's flight as a brawl; a Features MCM section; the Assembly; deliberate rescue; troop
loans; the test lanes (rule); visual identity; the character card; duels; witness liveliness; RP with teeth; faction
relation scores; the extension surface; a ledger of tasks given to lords; companion errands; wardrobe and images;
captive-scene content; jealousy; marriage; smaller polish; player influence on the world; fragmented prompt files.

---

## OPEN, NOT DESIGNED (ruled 16/11, Gabriel): Jev as a speed-only option for reading actions

_Heading before the 19/11/2026 audit: ROADMAPPED 2026-11-16 (Gabriel, after the bench), NOT DESIGNED: Jev as a speed-only option for reading actions_

Dutch (Discord, AI Influence ecosystem) proposed Jev (TypeSafe AI decision model, AIML API `typesafe/jev`) as the
Enhanced composition's second call. Measured on the 311-case action bench (SDK live lane, `JevActionBenchLiveLlmTests`,
16/11/2026, one pass each): action TYPE right on ~233/263 positive cases for Jev against 249 for Grok 4.20 and 260
for DeepSeek V4.1 Flash with reasoning off (255 with parameters right); 48/48 look-alikes withheld for Jev and
DeepSeek; Jev ~0.2 s a case, DeepSeek 1.6 s; Jev $0.00036 a case, DeepSeek in the same range. Jev names no
parameters (no target, amount, fief), so on its own it cannot drive a verb that needs one.

Gabriel's ruling: offer it as an OPTION for speed only, never the default. Open design points: its own interface
beside `ILlmClient` (a decision call, not chat), an MCM choice and key, and how parameters get filled (a second
cheap call only when the chosen verb needs one, or Jev choice questions over game-known candidates). Its misses
are mostly "none" on governance and intrigue verbs (dispatch_mission, revoke_fief, expel_from_clan, extort,
expose_secret, assault_npc, decline_divorce), so a player choosing it trades those away for latency.

---

## PARTLY BUILT (parity map shipped 2.7.21; later steps not re-verified 19/11): one path for actions, one junction

_Heading before the 19/11/2026 audit: RULED 2026-10-25 (Gabriel), BUILDING FROM STEP 1: one path for actions, one junction (who reads them)_

**The ruling.** Gabriel (25/10/2026): "il ne devrait pas avoir de differences entre [Enhanced et le mode regulier] car
ce sont juste deux facons differentes d'interpreter les actions dans une conversation", then: "d'un point de vue
architecture, tout devrait etre un seul chemin et la seule jonction devrait etre a quel service je demande
l'interpretation."

**What a trace found (25/10/2026).** After the tags are written, both compositions share the parser, the actions loop
and the bridge. Before, they do not: actions are taught in three places that do not know each other.

- The prose prompt teaches each verb in its own `PromptBuilder` section (91 `type:` literals), each gated by an
  `EncounterContext` flag, plus the mod's `ExtraActionTeachings` and `UnavailableDeeds`; `LlmServices.BuildActionVocabulary`
  hand-writes five verbs in its own words.
- The interpreter (`ActionInterpreterPromptBuilder`) teaches every `GameActionCatalog` verb every turn, with its own
  rules and examples, and none of the per-turn gates; the bridge refuses afterwards. A briefing once executed
  `swear_oath` that way (`ChatViewModel.Exchanges` comment, near the briefing drop).
- Asymmetries: `story_choice` (a tale's choices by words) is taught only to the interpreter, so it cannot be taken in
  Standard composition, and it is invisible to `docs/maps/verbs.md` (a constant, not a literal); `witness_leaves` is
  taught only to the prose model, whose tags Enhanced throws away (only `request_privacy` is merged back by
  `SceneDecisionMerge`), so it is probably never carried out in Enhanced; Compact drops verbs in Standard but only
  guidance in the interpreter; the interpreter prompt mentions a "CONVERSATION SO FAR" the live path never sends.
- In Enhanced the prose prompt still teaches every action, and the prose model's tags are then discarded: tokens paid
  for nothing and a distraction for the writer.
- When the interpreter call fails (timeout, parse), the reply's deeds are lost with a log line only.
- Paths that stay single-call even in Enhanced (captive and captor scenes, non-main speakers, synthetic player lines,
  prose fallbacks) are the same code in both compositions and raise no parity question.

**The target.**

- **One action plan per turn**, built once from the context: every verb (its catalogue spec: description, params, when
  to emit, when not), which are open NOW and which closed and why, and the turn's own blocks (a tale's choices by
  words, an arc's signals, a scene's conclusion clause). A verb's teaching lives in its `GameActionCatalog` spec; the
  scattered `PromptBuilder` sections migrate there.
- **Two renderings of the same plan**: into the prose prompt when the same call reads the actions, into the
  interpreter's prompt when a second call does. Parity holds by construction, and the guard compares the two renderings.
- **The one junction: an action reader** with two implementations, "the same call" (read the prose's own tags) and "the
  interpreter" (a second call), returning the same list. Everything after is one path, so merge patches such as
  `SceneDecisionMerge` disappear.
- **In Enhanced, the prose prompt teaches no action at all.**

**Ruled (Gabriel, 25/10/2026).**

1. Per-turn conditions: "oui pour la liste en cache". Both renderings carry the full catalogue, stable, in the cached
   prefix, and a short "open now / closed now, because" list below the `CURRENT ENCOUNTER:` marker. Standard's prose
   prompt then carries the whole catalogue rather than only the open verbs: measure quality before and after (action
   bench, rp_bench) when step 4 lands.
2. A failed interpreter call: "reessaie une fois l'interpreteur en ajoutant un log qui le specifie". BUILT 25/10/2026:
   `InterpreterRetryPolicy` (Logic), `ActionInterpreterComposer.AttemptAsync`; each retry and its outcome on the Compat
   log channel; `cr.test interpreter_retry`.

**Steps.**

1. **The parity map and its guard** (BUILT 25/10/2026: 85 verbs, 55 the same for both readers, 25 taught by the mod's
   own text and not compared yet; on the allowed list `story_choice` and `gather_news` (a deprecated alias).
   `assault_npc`, `kill_npc`, `rob_npc` had been kept out of the prose prompt on purpose, so player violence in words
   never happened in Standard: Gabriel ruled "enseigne la violence aux deux modes" and they are taught to both since
   `e499c36`. `witness_leaves` turned out taught to both: no bug.) `docs/maps/composition-parity.md`, one row per verb, saying whether the
   interpreter is taught it, whether the prose model is taught it with a permissive context and with an empty one, and
   whether the bridge handles it; the context fields read by one side only; the condition that decides when the
   interpreter runs. A blocking guard: a verb taught to one reader and not the other is refused unless it stands on an
   allow-list with its reason, and the list may only shrink.
2. **The action reader as the single junction** (BUILT 25/10/2026): `ActionReaderPolicy` (Logic) decides, from one
   facts object, whether a turn is an interpreter turn and who reads its actions; `ChatViewModel.ReadActionsAsync` is
   the one place the road forks. Behaviour unchanged. The parity map renders the policy's decisions and lists every
   chat method that executes actions, with whether it passes through the junction (only the ordinary 1:1 turn does;
   the commoner, envoy and council paths read their own reply). Left for later on purpose: the interpreter's silent
   failure, `SceneDecisionMerge`, the composer's signature (shared with the bench).
3. **The per-turn plan**, starting with the turn's own blocks (tale choices, conclusion); the tale arc builds on it.
   FIRST SLICE BUILT 25/10/2026: a tale's choices by words are taught to whoever reads the turn's actions (Gabriel:
   "le mode Enhanced a ete concu apres pour [...] reproduire le comportement de Regulier", then "je confirme"). The
   junction decides before the context is built and sets `EncounterContext.ProseReadsItsOwnActions`; the prose prompt
   carries the open choices below the marker only then, told not to announce or steer toward them. `story_choice` left
   the debts. Still open in this step: the prose fallback's prompt (built for an interpreter turn, then read as its
   own: CLOSED 25/10/2026, the fallback flips `ProseReadsItsOwnActions`); the arc's signals (BUILT, the arc's
   increment 2); one plan object both renderings draw from. (`witness_leaves` needed nothing: step 1 found
   it taught to both.)
4. **Verbs migrate to their spec one by one**; a ratchet counts those still taught outside the plan.
5. **Enhanced's prose prompt without actions**, measured before and after.

## PARTLY BUILT (three fixes 27/10), OPEN: words that move the world (orders to armies, goods between fiefs; DepaHN)

_Heading before the 19/11/2026 audit: WISHED 2026-10-27 (DepaHN, Nexus 26/10, Gabriel: "met les autres idees au roadmap"), ROADMAPPED, NOT DESIGNED: words that move the world_

DepaHN, a month into one campaign with Bellum Civile, Regard and Standing, Harvest and Production and Heroes Evolve:
"Many times I wanted to make a plan, move an army, or issue an order, and found only conversation [...] Then dialogue
would have real value and every word real weight." Three of his points were fixed at once (letters from strangers,
kin who forget they are family, a council only its anchor remembers, all 27/10/2026). The rest, traced 27/10/2026 against
what exists:
- **Orders to armies and lords:** move, defend, attack, a plan. Nothing exists (`follow_me` and `ride_with_me` only
  bring a lord to the player). The reverse, the player riding in another's train, is the 23/10 entry above.
- **Moving goods between towns and castles:** a verb that sends food from one fief to another. Supply tasks
  (`SupplyGiftPolicy`) and the food memory (`FoodTradeMemoryBehavior`) exist; no transfer.
- **Envoys with terms, and alliances:** `dispatch_mission` (Envoy), `mediate_peace`, council motions (war, peace,
  tribute, non-aggression, hostage) exist; a free-form envoy carrying terms to a ruler, and a military alliance, do not.
  The 30/07 diplomacy table is the related wish.
- **Request a meeting:** an envoy asking a lord to come, or to meet at a chosen place. The Invite button brings only
  those travelling with the player; "a rendezvous the game can keep" (SCOPED) covers people kept in one place only.
- **Arbitrating disputes between lords in council:** the player judging a quarrel by authority, standing and
  persuasion. Only the Tribunal verdict (prisoners) exists.
- **War and peace tied to relations:** a kinsman at 100 declared war on him. CR does not steer the AI's wars beyond
  `press_for_war`; the "relation scores that drive war and peace" wish of 30/07 is the place to answer it.
- **Events read from the campaign's history:** every 5 or 10 years, or after a great event (the player's death, a
  kingdom's fall), a model reads what happened and opens a fitting road (a split, a coalition, new characters).
- **A reader per faction:** a model that checks each realm's state and, when the plot calls for it, raises or
  empowers a character with a goal (a Sturgia near collapse finds a claimant sworn to retake it). The `CrAims` door
  (aims for a lord) and authored history are the nearest pieces.

## WISHED 2026-11-10 (Lordfadooboo, Nexus), ROADMAPPED, NOT A PRIORITY: invited characters seen in the scene

"so you can invite other people to a conversation, but is it possible to have that be visually present as well? in the
same way that sometimes when you talk to your companion in the field, you see 2 of your companions or soldiers standing
behind them. is it possible to do something like that with invited conversations or when you enter a keep and talk to a
lord that already has another lord present?" Gabriel (10/11/2026): "met une note au roadmap mais c'est pas ma priorite."

What the code shows (traced 10/11/2026):
- **Witnesses are text only.** `WitnessResolver` builds `WitnessEntry` for the prompt; nothing places them in a scene.
- **Map scene.** The two figures behind a speaker are vanilla: `ConversationMissionLogic.SpawnBodyguards` (SandBox) takes
  at most 2 from the partner's `ConversationCharacterData.Party` roster (wounded heroes first, then highest level; needs
  3 men), never for a player companion. No input names chosen characters, so showing witnesses means spawning them
  ourselves at the `player_bodyguard_*` / `opponent_bodyguard_*` spawns when the mission starts.
- **Keep or hall.** CR only teleports the partner next to the player (`ChatSceneStager.PlaceAudienceCompanionIfPending`,
  mysticwolf92, 14/09/2026). The cheap first step: the same move for each witness whose agent the scene already holds.

Open before any build: does vanilla spawn other lords in the lord's hall (LocationCharacter code not read)? Who is seen
(only those physically present, or a companion invited from afar)? What a witness who leaves (`witness_leaves`) does on
screen. Order if wanted: (1) read the hall spawning, (2) teleport present witnesses, (3) map-scene spawns.

---

## OPEN (the reverse, ride_with_me, shipped 2.8.2): the player rides in another character's train (Lordfadooboo)

_Heading before the 19/11/2026 audit: WISHED 2026-10-23 (Lordfadooboo, Nexus), ROADMAPPED, NOT DESIGNED: the player rides in another character's train_

"could it be possible to do the reverse? to join another characters party without it needing to be an army? think
something similar to the 'serve as soldier' mod." The reverse already exists: `follow_me` (a lord leading a party
follows the player) and `ride_with_me` (a partyless lord rides in the player's roster). Gabriel (23/10/2026): roadmap it.

Why it is a pillar and not a verb: the game has no way to put the main party inside another party outside an army.
Serve as Soldier gets there by hiding the main party and tying it to the lord's. A design has to answer:
- **Following.** The main party hidden and moved with the lord's, or kept visible and escorting him; what the player can
  still do on the map (enter a town, leave at will).
- **Battles.** Joining every battle the lord fights, on his side, and what happens when the player refuses.
- **The player's own troops.** Merged into his roster, left in a garrison, or riding along as a separate escort.
- **Ending it.** Leaving by choice, the lord dismissing the player, war between their realms, the lord captured.
- **What CR adds.** The lord remembers the service; a verb in conversation to ask to ride with him (and to leave).

---

## WISHED 2026-10-19 (tthheerroocckk, Nexus), ROADMAPPED, GABRIEL WANTS A DESIGN TALK FIRST: a council at sea

"can we call a council at sea? like for pirate roleplay?" Today the campaign-bar button greys out at sea and
`CouncilEntry.PromptAndConvene` says "You cannot convene a council at sea." (`CouncilEntry.cs`, header: no conversation
scene loads on open water, and the direct fallback would ride the live map). Gabriel (19/10/2026): roadmap it, and
discuss a design before anything is built.

What a design has to answer:
- **The scene.** No land scene exists at sea, and a Gauntlet layer over the live map leaks VRAM by design (the council
  MUST be a Mission). Does War Sails ship a deck scene a conversation mission could load (its naval battle scenes?),
  or does the council wait for landfall? Needs a spike in the decompiled 1.4.8 naval code before any promise.
- **Who sits.** At sea only the Party scope is at hand (`CouncilRosterResolver`: companions and kin riding with you);
  the other scopes draw from the clan wherever it stands.
- **What a sea council is for.** Pirate roleplay suggests its own matters (a prize, a course, a raid on a coast), not
  only the land agenda.

Found while tracing it, to settle in the same talk: `CouncilConversationStager.StageCouncil` has no sea refusal of its
own; at sea it logs "[COUNCIL STAGE] the player is at sea ..." and calls `OpenDirect` (on the map). Only the bar and the
picker refuse first. Reached without that refusal: `cr.council`, the war council offer after a battle
(`WarCouncilOfferBehavior`), and the arrest trial (`ArrestBehavior`, whose own triggers already skip the sea at three
sites; not verified for the trial path itself).

---

## PARTLY BUILT (2.7.16: a captive in the player's castle bargains), OPEN: the captive PLAYER makes a structured offer (ransom, favour owed, information)

_Heading before the 19/11/2026 audit: WISHED 2026-10-14 (tashmetu), ROADMAPPED: the captive PLAYER bargains for freedom_

The other half of the 2.7.10 `counter_offer` (a lord the player holds offers ransom, fealty, service, goodwill). Today a
player held by a lord can only plead: `release_player` is taught when the captor allows persuasion
(`CaptorAllowsPersuasionRelease`), with no structured offer. Wanted: the player offers a ransom from his own purse, a
favour owed (a pledge the captor can call in, ideally a verified deed), or information, and the captor weighs it in
character. Strictly a bargain: nothing forced. Information must be true and groundable (NPCs never lie, and the captor
must not accept a "secret" the game cannot hold the player to). Design after CrAims.

## PARTLY MET (tasks by letter and in person, CrAims letters), OPEN: kingdom-scale tasks from strangers who heard of the player (Lordfadooboo + fkasad)

_Heading before the 19/11/2026 audit: WISHED 2026-10-02 (Lordfadooboo + fkasad, Nexus), NOT BUILT: characters who come to the player WITH a task_

Lordfadooboo (02/10/2026): "what do you think of characters being able to approach you with a quest? and maybe they could
also send a request by letter? maybe you have to accept the quest in person but the letter would make its existence
known." fkasad, the same day: a "kingdom-scale" quest; someone outside your faction hears of you (clan rank 3 or 4), you
enter their territory and they learn of it ("lords have spies these days"): "bring me my guy", "spy on an enemy city",
"take this guy prisoner"; a kidnapping ("bring me that king's daughter", seduction to abduction); escorting a demanding
prince through three or four cities while hired kidnappers hunt the player (vanilla caravan-escort shape, parties that
target only the player), with a bedtime story each night that the LLM evaluates, a bad night slowing the party.

What exists (checked 02/10/2026): characters already approach the player (FiefAudience, CompanionAudience,
RoadInterception) and write unbidden (letters, RealmTidings), all through ActionGate; none of those paths can carry a
task yet. The grounded quest menu (ViableQuestScanner), the envoy, the scout and the capture tasks exist.

Proposed in steps (Gabriel to pick):
1. "I have work for you": a lord who holds the player in enough regard and has a viable task on his menu writes (or asks
   for an audience) to say so; the task is ACCEPTED IN PERSON through today's [QUEST] path, so every check stays. Reuses
   letters, ActionGate.Letter, the menu, the mailbox pacing.
2. "You have been noticed in our lands": a lord of another realm, when the player rides into his territory with a clan
   of rank 3 or more, sends a messenger or summons him; tasks of his own kind (the envoy, the scout, a capture).
3. A pillar of its own, design first: the prince escort with spawned hunters and the bedtime story; the "king's daughter"
   errand needs a content ruling (abducting a lady for a third party; what is allowed by default).

## FOUND 21/09 by reading the maps, NOT RE-VERIFIED 19/11: one concept under several names

_Heading before the 19/11/2026 audit: FOUND 2026-09-21 by reading the generated maps, NOT ACTED ON: one concept under several names_

The maps (`docs/maps/`, see its README) show a duplicate when two declarations share a NAME. They cannot show the
same concept under DIFFERENT names; that needs judgement. So a fresh agent read `constants.md` and `inventory.md`
end to end, opened the source for its candidates, and ruled each one "one concept", "two honest questions", or "the
owner must rule". I re-checked the ones marked VERIFIED below myself. Nothing here has been changed in the code.

**A fact that reframes several of these:** `CalradiaRemembers.Logic` already references the SDK, so anything both need
can live in the SDK. Several doc comments justify a copy with "the SDK cannot reference Logic", which is true only in
that direction.

### Probably real defects, small

- **Rescue gratitude reaches infants.** `RescueBehavior.ResolveFamily` does not exclude children, where every grudge
  path does; a baby records a memory and warms to the player. (Read in the source 2026-09-20.)
- **Lord recruitment is judged on the wrong ledger. VERIFIED.** `LordRecruitment.MinRelation = 60` is fed vanilla
  `GetRelation`; `NotableRecruitmentPolicy.RegardFloor` and `PrisonerRecruitmentPolicy.RegardFloor`, both 60, read
  `ReputationWithPlayer`, and the notable's own doc says the three "match". The durable rule is to judge on the MOD
  ledger. Needs Gabriel's ruling.
- **The 10000 single-payment clamp lives in five places. VERIFIED.** Two bare literals in the bridge (give_gold
  `:1508`, take_gold `:3734`) and three named constants (`BastardBlackmailPolicy`, `GoldPledgePolicy`,
  `HushMoneyPolicy`). The blackmail doc itself says a demand above the clamp can never be settled; changing the
  literal alone is that bug.
- **A comment that is wrong:** `EncounterContextBuilder.RecentDefeatWindowDays = 21` calls it "a season's quarter";
  21 days is a whole Calradian season.

### The one system-wide problem: the regard vocabulary

**Measured 21/09/2026, and now tracked:** `docs/maps/decisions-regard.md` can sweep **12** rules across the scale,
against **91** regard-named thresholds that exist. The other 79 are compared inline, at the place that wants the
answer, so there is nothing to call and nothing to measure. That gap IS the problem, in one number: the decision is
taken about the value instead of being asked of it. Three rules already turn at exactly +20 under three names, and
10 of the 12 swept rules do not say which ledger they read.

The table is also the safety net for the fix: turning regard into a type must not move a single cell in it.


Some thirty policies each mint a private number for the same few rungs (friend 10/15/20, close 30, love 50, deep
bond 60, disliked -10, hostile -20/-30) under names like WarmRegard, ConfidantRegardFloor, StandingFloor,
FriendRelationFloor, BestFriendThreshold, and about half never say WHICH LEDGER they read. Worse, the words do not
track the ledger: facts named `...Regard` are fed vanilla relation (`KinRegardForPlayer`, `CaptiveRegardForPlayer`)
while facts named `RelationToPlayer` are fed the mod's own. `RegardBands` fixed this for the WORDS a character
reads (2026-07-04); the DECISIONS never got the same treatment. Proposed: `RegardTiers` (mod ledger, in the SDK
beside `RegardBands`) and `VanillaRelationTiers`, with each policy keeping its constant but declaring it
`= RegardTiers.Friend` or a deliberate local number with a stated reason. "Is A a close friend of B" alone is
vanilla relation >= 20 under four names with three private scans of every living hero
(`RescueGratitudePolicy`, `SecretPropagationPolicy`, `DivorceProcessBehavior`, `JealousPartyResolver`).

### Contained consolidations, one to eight files each

- **Prior dislike deepens a wound**: the same three lines in `DivorceConsequencePolicy`, `ExecutedKinRules` and
  `Tier2GrudgeRules`, bonus 15 in one and 10 in the others with nothing saying why.
- **The Calradian calendar** (season 21, year 84): `ChronicleCalendar` exists and four files use it; everywhere
  else it is a literal or hidden inside a tuning value (168, 252, 42, 63). Its home must be the SDK, since
  `PromptBuilder` restates it too.
- **Map distance to travel days**: `LetterGenerationService.DistancePerTravelDay` calls itself "the single
  conversion" and `PlayerLetterService.MapUnitsPerDay` is a second one that has already drifted. A third convention
  (`CouncilAttendanceResolver`, 5 units an hour) and the real courier speed (6.5) disagree with both.
- **Courier speed 6.5** under three names; `TravelingParty.BaseSpeed` is public and read by nothing.
- **"Close kin of X"** enumerated at least seven times with different filters and different caps (4 vs 3).
- **A hero's five traits**: five carrier types (two identical `TraitFacts`) and eight private `TraitLevel` readers.
- **News reach**: `InterceptionBehavior.GossipMagnitudeThreshold = 55` and four feeders pinned at 55 by a comment;
  raising the bar silently mutes tournaments and exploits. About twenty bare literals in `WorldEventFeeders`.
- **Two stagers**: `CouncilConversationStager` says it "mirrors ChatSceneStager"; council is the `ChatSceneKind`
  that was never added.
- **An unfinished convergence**: `CouncilAttendancePolicy` says it consolidates three policies; two are still live
  and measure "too far" differently, and `AssemblyArrivalPolicy` appears to have no production caller.
- **Mirror enums** kept for a dependency that points the other way (`PlayerDressBand`/`PlayerDressState`,
  `EscapeOutcome`/`CaptiveEscapeResolution`).
- **"Vendetta" means two things**: `GrudgePolicy.VendettaRigidity = 100` (never decays, never resolves) and
  `CaptorReleasePolicy.VendettaGrudgeFloor = 40` (a MAGNITUDE, on grudges the first calls ordinary). Given the
  ruling that a vendetta stays unforgivable, the shared word is a trap; rename the second.

### Deliberately twins: leave them, and say so

Grudges and Appreciations mirror each other on purpose ("Trait direction is the MIRROR of GrudgePolicy"). Do NOT
merge their constants: `CalculatingFormationFloorPerLevel = 5` has the same name in both and the OPPOSITE effect,
raising one floor and lowering the other. What does want one home there is the scaffolding (`TraitFacts`,
`MigrationCandidate`, `MaxCauseLength`, `PerNpcCap`). Also honest and documented: `StipendPolicy`/`TributePolicy`,
`HostagePolicy`/`NonAggressionPactPolicy`, the three journal bridges, and the three neglect windows (7/14/30), which
are rungs of one ladder.

---

## SCOPED 2026-09-20 (player proposal, reframed by Gabriel), NOT BUILT: ask the village which way they went

fkasad (Nexus, 20/09/2026) proposed a long, carefully structured "talk to the village elders" feature: villagers
as human scouts who describe a passing army in vague sensory terms, filtered through their own skills and then
through the player's, with combat and movement bonuses on top, growing into a scouting network and an ambush
interface.

**What we take, and why it is not what he proposed.** Estimating an army's composition is something vanilla
already does better: the player hovers a trail and reads exact numbers. A villager saying "many horsemen" is
strictly less, and hiding the vanilla readout to make room for it would punish the player for our taste in
design. What the interface does NOT reliably give is the thing a player actually wants when they have LOST
somebody: who passed, when, which way, and whether they were carrying captives. That changes a decision (where
do I ride now) instead of duplicating a tooltip, and it plugs straight into the pillars we already have: a
captured companion, a nemesis to hunt, a lord who slipped away. Gabriel, 2026-09-20: "j'aime l'idee de
retrouver quelqu'un."

### The data is real (verified against the decompiled engine, 1.4.8)

`TaleWorlds.CampaignSystem.Track` carries `Position`, `Direction`, `PartyName`, `Culture`, `Speed`,
`NumberOfAllMembers`, `NumberOfMenWithHorse` / `WithoutHorse`, `NumberOfPackAnimals`, **`NumberOfPrisoners`**,
`CreationTime`, `Life` and `PartyType`. `MapTracksCampaignBehavior` keeps them in a private `_allTracks` list
(a `[SaveableField]`, so they survive a save) plus a `LocatorGrid<Track>` for position queries; only
`DetectedTracks` is public, and that is the player's own detection, not what a villager saw. So the reader
reflects the private list (resolve BY NAME, degrade to a logged no-op, own try/catch: the same defensive shape
as the two Harmony guards). Tracks expire on their own, which hands us the right narrative bound for free:
a village can only speak of a recent passage.

### Increment 1, the only one worth building first

- **Who answers:** a village notable, and a villager in a village scene. A tavern keeper in a town is a natural
  second step, not the first.
- **What the player asks:** what has passed near here lately. Not "how strong were they".
- **What the mod computes,** from the real tracks within a radius of the settlement, freshest first: who
  (`PartyName` / `Culture` / `PartyType`), when (banded: this morning, yesterday, a few days back), **which
  way** (the direction turned into a landmark, "toward the ford", "north, on the Amprela road", using the
  nearest settlement in that bearing rather than a compass degree), whether they were mounted or on foot, a
  COARSE size band, and **whether they had prisoners with them**.
- **The degradation is the mod's job, never the model's.** A villager reports direction and timing well (that
  is what a person on the land would know), numbers coarsely, and identities only when the banner or culture is
  one they would recognise. The prompt receives already-degraded facts; the character voices them and invents
  nothing.
- **NOBODY LIES (RATIFIED, Gabriel 2026-09-20).** fkasad's drunk peasant, who invents a story to squeeze a coin
  out of the player, is cut, and so is the enemy village that misleads. Gabriel's reason is about how the mod is
  judged, not about the fiction: *"les joueurs recherchent la pertinence d'un LLM. Un mensonge peut laisser
  croire que le LLM se trompe."* A player cannot tell a character who lied from a model that failed, so every
  invented fact is read as a defect of the mod. **Vagueness, ignorance and refusal are the whole range**: "I saw
  nothing", "it was dark and I was asleep", "ask me again when you have done something for this village". Those
  cost the player the same information and cost us nothing in trust. See [[feedback_npcs_never_lie]].
- **Willingness:** friendly or paid talks; a neutral wants a reason (coin, food, a cleared bandit nest);
  an enemy village refuses outright. Reuses regard, gold and the existing gating verbs.
- **The payoff is a decision, not a bonus:** the player learns where to ride. Optionally the sighting is
  recorded as a `WorldEvent` so it can circulate and other systems can read it.

### Deliberately NOT in scope

- Combat, morale, speed or evasion bonuses. That is balance, not memory and speech, and it is where we collide
  with other mods. If it is ever wanted, expose the intel through the Extension Surface and let a mechanics mod
  consume it.
- Hiding or replacing the vanilla track readout.
- Army-composition estimation as a feature in its own right.
- Party scouts, the Balance of Power analyst, and the ambush interface: fkasad's own "expanding the scope",
  worth revisiting only once the village version is play-verified.

### Open questions

- Does a bribe change what they tell you, or only whether they tell you?
- Should a sighting persist as a `WorldEvent` (so it can be gossiped about later), or stay ephemeral?
- Village notable only, or any villager in the scene?

---

## GUARDRAIL SHIPPED 2026-09-20, PILLAR SCOPED NOT BUILT: a rendezvous the game can actually keep

yozakura12 (Nexus, 19/09/2026): *"During conversations, NPCs tell me when and where to meet them. However, when
I go to the designated location at the appointed time, the NPC isn't there. Consequently, the NPC gets upset,
claiming I didn't keep the promise. Does this game actually have a meet-up feature?"*

It does not. The character invented the appointment, its own `[EVENT]` memory recorded the promise, and the
player was punished for a meeting nobody could have kept. That is the one failure this mod cannot afford: the
whole pitch is that what is remembered is true.

### Done: the guardrail (SDK, 2026-09-20)

`PromptBuilder.AppendNoAppointments`, always on, Full and Lean: never fix a place and a time, offer what exists
instead (ask now, write a letter, speak again when paths cross), and **never reproach the player for a missed
meeting, whoever seemed to arrange it**. The second half outlives the guardrail: promises made before it exists
sit in every save already written, and they keep surfacing. Pinned by `NoAppointmentsTests`.

### Scoped: increment 1, people who are already there

The expensive half of a rendezvous is making the OTHER party travel. Taking a lord's party off its army, its
siege or its own quest, and handing control back cleanly, is the kind of thing that breaks other mods. So
increment 1 buys the whole feeling without steering anyone: it is offered **only to people the game already
keeps in one place**.

- **Who may agree:** a settlement notable, a tavern wanderer, a governor, a companion or clan member living in
  a fief the player owns, a spouse or close kin at home, a prisoner the player holds. Not a roaming lord: to
  him the guardrail still applies, and he says he will write instead.
- **What is agreed:** a SETTLEMENT and a WINDOW IN DAYS, never an hour. The player cannot be held to a clock
  they cannot see, and a window ("before the week is out") is both honest and forgiving. The NPC names the
  window; three to seven days is the band to start from.
- **How it is recorded:** a rendezvous is a two-sided promise, unlike every existing `TrackedCommitment`
  (which is something the NPC owes the player). Decide at build time whether it becomes a new
  `CommitmentKind.MeetAt` with `TargetId` holding the settlement id, or its own small ledger. Additive save
  key either way; a save written before it reads no rendezvous, which is the truth.
- **The verb:** `set_rendezvous` (place, days), taught ONLY when the speaker is one of the eligible kinds and
  the place is one they are actually tied to. The teaching states the constraint, so the model proposes only
  what the game can keep, which is what the guardrail forbids everywhere else.
- **Who names the window (RATIFIED, Gabriel 2026-09-20):** when the meeting belongs to a QUEST, the NPC names
  it, because the quest owns its own deadline and two deadlines on one promise is one too many. A meeting
  arranged outside a quest can be settled between them in the conversation.
- **What a kept meeting is worth (RATIFIED, Gabriel 2026-09-20):** the scene is its own reward, no warmth
  bonus for turning up. **But arriving must CLOSE a quest when the meeting was a step of one**, which is the
  real work in this bullet: the arrival becomes a completion route alongside `[QUEST_COMPLETE]`, or the
  player keeps a task in their journal that the scene already settled. Decide at build time whether the
  arrival completes it directly or hands the NPC the chance to complete it in the scene it opens.
- **Arrival, which is the whole payoff:** the player walks into the settlement inside the window and the
  person asks for a word. This reuses `FiefAudienceBehavior` wholesale, the channel that already stages "X
  would have a word with you" inside a fief, with the rendezvous as the trigger and its purpose as the opener.
  No new hosting, no new screen.
- **Missing it (RATIFIED, Gabriel 2026-09-20):** past the window the rendezvous is Missed, and NOW the
  reproach is earned: the NPC may raise it once, cold. It costs a little personal regard **except with a
  friend or with family**, who are disappointed and say so but think nothing worse of you. Never a grudge on
  its own (grudges are for real wrongs, and a missed meeting is not one). The two exemptions have existing
  shapes to reuse rather than invent: family via `HouseholdKinPolicy.FamilyOf`, friendship at a relation of
  **10**, the mod's baseline "is this a friend at all" bar (`ThirdPartyDescriptor`, `RelationshipsBuilder`,
  `SchemeBacklash`). The 20 seen in `DivorceProcessBehavior` and `RescueGratitudePolicy` is a different
  question, "is this one of the CLOSEST few", used where a list has to be cut to three or five, and the
  rescue one does not even measure the relation with the PLAYER. Asked here: is this person a friend of the
  player's. So 10, and no third number.
- **The NPC failing:** captured, dead, or the settlement besieged or taken. The rendezvous is BrokenByNpc, the
  NPC apologises when next met, and the player may hold it against THEM. Never silence, and never blame.
- **Journal:** one entry, mirroring how quests are already mirrored, or the player has to remember for the
  game.
- **Verification:** pure policy tests (eligibility, window arithmetic, the four end states) plus a
  `cr.test rendezvous` that plants one, walks the real arrival path, asserts the audience fires, and cleans
  up. Play-verification is the honest gate here, since the payoff is a scene.

### Increment 2, deliberately later: a lord who rides to meet you

Only once increment 1 is play-verified. This is the one that touches campaign AI (`SetMoveGoToSettlement`,
holding him there, and giving the AI back cleanly), and it must refuse the moment the lord is in an army, a
siege, a war march or a vanilla quest of his own. Related, and worth reading first: the ESCORT pillar already
scoped in this file, which has the same "the arrival IS the scene" shape.

### Open questions for Gabriel

None left. The increment is ready to scope into build steps when Gabriel calls for it.

---

## DESIGNED 2026-09-11, NOT BUILT (player request + Gabriel): ESCORT a person to a place, and the arrival IS the scene

A player: "so i just had a noble ask me to bring a healer to a local village that had just been raided. is there
any possible way for this to somehow be integrated into the quest system? perhaps a 'deliver/escort (character) to
(location)' and then the events of the quest would play out in the chat window once you arrive. perhaps the chat
window would pop up similar to when a companion wishes to speak to you?"

He is describing something the mod half owns already, and he has correctly identified the mechanism to reuse
(the companion-audience popup). Gabriel pointed at the vanilla precedent, which is the right instinct and turns
out to be two precedents.

### What vanilla already solved, read out of the decompiled CampaignSystem

`HeadmanVillageNeedsDraughtAnimalsIssueBehavior` is the "deliver goods to a village" shape. The nearer model is
`EscortMerchantCaravanIssueQuest`, and its member list is a checklist of everything this feature has to answer:

- `SpawnCaravan` + `CustomPartyComponent`, **vanilla does not walk an existing hero to the destination, it spawns
  a party for the escortee.** This is the answer to "the healer does not exist as a character".
- `AdjustCaravansSpeed`, the escortee is kept at the player's pace. Without it the charge is left behind by the
  simulation itself and the quest dies of arithmetic.
- `NotifyPlayerOrCancelTheQuestIfCaravanIsFar`, the "you abandoned them" rule, warned before it fires.
- `TryToFindAndSetTargetToNextSettlement`, `_visitedSettlements`, `ProperSettlementCondition`, destination tracking.
- `SimulateSettlementWaitForCaravan`, `_caravanWaitedInSettlementForHours`, what happens while the player dawdles.
- `OnSettlementEntered`, arrival. The same hook the player named.
- `OnTimedOut`, `FailConsequences`, `FailByPlayerHostileConsequences`, `OnWarDeclared`, `OnClanChangedKingdom`,
  `OnMapEventEnded`, every way it ends badly, each with its own consequence.

### Option A, ratified as the scope: the escortee is somebody who already exists

Two options were weighed. **B** would spawn a party as vanilla does: faithful, and it buys us convoy simulation
that Bannerlord already performs better than we would. **A** carries a hero who is already in the world, using the
retainer tech (`ride_with_me`) that already handles their clan, their death, their capture, and war breaking out.

A is chosen because **what CR adds is not the journey, it is what is said on arrival.** Paying for a bespoke party
simulation to deliver a conversation is paying for the wrong half. It also gives the healer a name, a face and a
memory profile, which a spawned nobody cannot have, and a named person is the only kind this mod can make matter.

### What already exists, verified in code rather than remembered

- **A hero travels with you**: `RetainerBehavior` / `RideWithMeEligibilityPolicy`, with `RetainerLeavePolicy`
  already ending it on war, death, or loss of tracking.
- **The chat opens by itself when you arrive somewhere**: `CompanionAudienceTopicBehavior.OnSettlementEntered`,
  which is exactly the popup the player is asking for, and whose safety read (`RoadAudienceFacts`, eleven facts
  including `AtSea` after the 2026-09-10 crash) already knows when a conversation must NOT be staged.
- **A quest can name a place**: `QuestMenuEntry.TargetField` carries `target_settlement`, and the composer already
  grounds a settlement name into the prompt.
- **A quest can be voided when its subject is gone**: `QuestTargetVoidPolicy` distinguishes vanished, dead, and
  (for a rescue) freed, so an escortee who dies mid-journey already has an honest closure line waiting.

### The two things that are NOT there, and neither is cosmetic

**1. `ride_with_me` is scoped to LORDS.** `RideWithMeEligibilityPolicy` requires `IsLord`, so a village headman, a
healer, a surgeon, an artisan, precisely the people this quest is about, cannot ride today. Widening it is the
first real decision, and it is the same shape as the `follow_me` widening of 2026-09-10: the rule refused a whole
population for a reason that made sense in its own pillar and does not generalise. **Gabriel's ruling needed:**
does a notable ride in the party under the same terms as a lord, or does an escortee need its own, narrower
eligibility (alive, free, willing, not already travelling, and NOT a party leader)?

**2. No CR quest completes by ARRIVING anywhere, and Gabriel's ruling below means none has to.** Twelve quest
types exist and the closest, `DeliverPrisoner`, completes through a conversation verb (`give_prisoner`), a hand-off
someone performs, not through a place being reached. `QuestService` does not listen to `SettlementEntered` at all;
three other behaviours do (`CourierService`, the companion audience, the assembly). I first read this as the heart
of the request and the one piece of genuinely new wiring. **It is not, because the conclusion is a conversation:**
arrival only OFFERS the scene, and the hand-off inside it completes the quest through the road every other quest
already takes. The escort is `DeliverPrisoner`'s shape with a willing passenger.

### Gabriel's rulings, 2026-09-11

**The conclusion is a conversation with the destination's headman, not an arrival event.** You reach the village
with your charge and the scene opens; what completes the quest is the hand-off you perform inside it. This is
worth more than the convenience: a quest that ends because a coordinate matched ends in silence, and this mod
exists for what is said. It also means the whole feature rides the verb path CR already has, tested, journalled
and stamped.

**A late arrival is not a failure, it is a darker resolution.** "Elle pourrait être dramatique effectivement dans
certaines situations." A raided village that waited three weeks for a healer has a scene worth writing, and a flat
"quest failed" throws it away.

**The consequence of that second ruling is the part worth noticing: the bad endings become scenes too.** If the
conclusion is a conversation, then arriving LATE is a conversation, and arriving WITHOUT the person you promised
is the strongest one this feature can produce, you stand in front of the headman and have to say it. Today a
quest target who dies is silently voided by `QuestTargetVoidPolicy` with an honest closure line the player reads
in a journal. Here the player says it out loud to the person who was waiting. That is the difference between a
quest system and this mod, and it costs nothing extra: the same policy already tells the three states apart
(vanished, dead, freed), and each one is simply a different scene rather than a different log entry.

### Shape

- **Inc 1, the escortee may travel.** Widen or fork the retainer eligibility per Gabriel's ruling above. Pure
  policy plus tests; nothing player-visible yet.
- **Inc 2, a quest that names a passenger and a destination.** New `QuestType.EscortPerson` carrying a target hero
  AND a target settlement (today a quest carries one or the other, never both). Completion is the hand-off VERB,
  not the arrival, so it goes through the ONE existing path and is stamped, rewarded and journalled exactly as
  every other quest is. No arrival listener in `QuestService`.
- **Inc 3, the arrival OFFERS the scene.** Reaching the destination with the passenger raises the same popup a
  companion raises for a private word, behind the same safety read (`RoadAudienceFacts`, so it can never fire at
  sea, in a siege, over a menu, or on an open chat). Declining must leave the quest untouched and offer again:
  the player is late for a reason, and a scene that can be missed once by a misclick is a scene that will be.
  Who speaks first is a writing decision: the headman receiving, or the charge arriving.
- **Inc 4, the endings, all of them scenes.** In time, late, and empty-handed are three conversations, not one
  success and two failures. `RetainerLeavePolicy` still ends the travelling on war and death, and
  `QuestTargetVoidPolicy` still tells vanished from dead from freed, but its verdict now chooses which scene
  opens rather than which log line prints. The genuinely new question stays: what a village OWES you for a healer
  who came late, and whether the lord who sent you agrees with the village about it.

### Open questions for Gabriel, not to be decided unilaterally

- Who issues it? A lord, as the player described, or the destination's own notable asking for help?
- Does the escortee have to be found first (a real errand: go to a town, persuade a surgeon), or does the giver
  hand them to you? The first is a much better story and roughly doubles the work.
- Does the passenger occupy a party slot, and does the player feed them? Vanilla's escortee is a separate party and
  costs nothing; a retainer rides in the roster.

**Nothing here is built. This entry exists so the design is argued before any code, as with the briefing pillar.**

---

## DESIGNED, NOT BUILT (no verb found 19/11): a companion ACTS on your order

_Heading before the 19/11/2026 audit: DESIGNED, NOT BUILT, **THE GATE IS OPEN since 2026-09-14**: a companion ACTS on your order_

> The condition on this entry was "gated on the briefing being tested". The briefing is now tested and
> shipped, so nothing external blocks this any more; what remains is the four rulings below, which are
> Gabriel's and were deliberately not taken unilaterally. This is the most natural next pillar.

Gabriel asked whether he could tell a companion to stab the target on the word "GO". The briefing half works; the
deed does not. There is no verb for a companion harming anyone (every violence verb is the PLAYER acting), and the
anti-empty-promise rule names the case verbatim, so the companion refuses. **The bad failure mode is that the
player cannot tell a character's moral choice from a missing feature.**

So the gap is structural rather than cosmetic: the briefing is a command post with no troops, and players will feel
it. Open questions Gabriel has not ruled on: whose crime it is when the order was unheard (deniability, and a
companion who could denounce you); whether the POLICY decides compliance while the model merely voices it (the
house split, and the alternative is unlearnable refusals); how a conditional trigger avoids the fuzziness that
forced the kill confirmation; and what the companion himself risks.

**Recommended scope when it comes:** the NON-LETHAL half first (seize, block the exit, rob, intimidate). It serves
far more scenes than a murder, reuses ViolencePolicy and ViolenceConsequenceService wholesale, and stakes no
character's life. The lethal half then inherits those rules plus the existing MCM gate and confirmation. Do not
ship a violent, irreversible, engine-bound pillar that no human has watched run.

---

## RATIFIED 07/09, NOT RE-VERIFIED 19/11: two hosts to revisit once panels are state-hosted

_Heading before the 19/11/2026 audit: RATIFIED (Gabriel 2026-09-07), NOT built: two hosts to revisit once panels are state-hosted_

Settling where a panel lives (see CLAUDE.md, "Where a panel lives") reopens two decisions that were made under a
constraint that no longer holds. A pushed GameState genuinely suspends the map (measured: 0 ticks against 481), so
"it has to be a Mission or the map keeps running underneath" is no longer a reason for anything.

**The COUNCIL can come back to the map as a state.** It was made a Mission because a Gauntlet layer over the LIVE map
leaks by design, and a Mission was the only way to stop the map. That reason is spent. Note the council is also a
conversation with characters, so a Mission remains defensible on its own merits: the point is that the OLD
justification must not be cited any more, and the choice should be made on whether the scene is wanted.

**CAPTIVITY EVENTS should split by WHO holds you.** With the Nemesis or a lord, the encounter deserves a real scene,
so a Mission: these are named characters the player has history with, and the scene is the payoff. With a generic
bandit there is no character worth staging, so it stays on the map in a state, which is also far cheaper (no scene
load for a nameless captor). This is a design ruling about when an encounter earns a scene, not only about hosting.

---

## WISHED (player request 2026-09-02, roadmapped; NOT built): write a WORLD EVENT from the map screen, no console

A player running CR alongside a total-conversion (Calradia Old Gods) wants to funnel that mod's many in-fiction
events to the AI, but the only way to inject one today is the console (`cr.world_event`). The wish: a shortcut or
a small option from the MAP screen to write a world event by hand during play, so a player who is not console-savvy
can feed the world's happenings into what NPCs know and talk about. This fits CR's "amplify what is already
there" axis squarely: the World Event store already exists, is injectable, and already feeds rumours/knowledge and
the Chronicle.

**Largely de-risked:**
- The write path already exists end to end: `cr.world_event` (`ModCheats.WorldEvents.cs`) records a `WorldEvent`
  into the store, which already flows to rumour/knowledge/Chronicle. The feature is a UI front-end over that same
  seam, not new domain logic.
- Custom pushed Gauntlet screens + named brushes + a text-input widget are all owned (chat window, letter composer,
  appearance editor). A small "compose a world event" panel is the same pattern as the letter composer.
- A map-screen entry point: a hotkey, or a menu/button, reachable while not in a settlement (the point is to log an
  event happening out in the world).

**Open design questions:** what fields the player fills (a free-text line vs. a few structured fields: who/where/
what/recency); how far the event spreads (faction/region/everyone) and how that maps onto the store's existing
relevance gating; whether to expose event "kind" or keep it a plain happening. Pairs naturally with the in-game
persona/world editor pillar below (same "edit CR's world during play" theme, same screen machinery).

---

## PARTLY BUILT (Balance of Power panel, 2.5.0), OPEN: the other 4X almanac panels

_Heading before the 19/11/2026 audit: WISHED (player request + Gabriel, 2026-08-26): 4X-style WORLD ALMANAC panels, statistical not just a feed_

Players want summary panels on world events, in the spirit of the info panels in 4X strategy games. Gabriel's
call: go past a plain chronological event LIST and build STATISTICAL views computed from the live campaign,
each panel optional behind an MCM toggle. Concrete panel ideas he named:
- Top 10 most powerful clans (a power score: fiefs + party strength + wealth + influence).
- Which kingdom holds the military advantage over the others (aggregate army/garrison strength per faction).
- The richest kingdom (aggregate treasury / settlement prosperity).
- The 10 greatest battles ever fought in Calradia (needs a persisted battle ledger: participants, casualties,
  victor, date, place; nothing native records this, so CR would log battles as they happen).

Design notes (not ratified, for the design pass):
- Two data classes: LIVE snapshots (clan power, faction wealth/military) read on demand from the campaign at
  panel-open time = no persistence, cheap, always current; and HISTORICAL series (greatest battles, biggest
  sieges, rise/fall of clans over time) that DO need a persisted ledger written as events happen. The battle
  ledger is the real new build; the ranked snapshots are mostly pure aggregation over `Clan.All` / `Kingdom.All`.
- Fits CR's memory identity: "Calradia Remembers" literally remembering the shape of its own history. The
  battle ledger could reuse the WorldEvent store ([[project_world_event_store]]) rather than a new store.
- Presentation: reuse the leather-book window chrome (letters/council) for the almanac; bar-style ranked lists
  first (a filled-bar-per-row is achievable in GauntletUI), true plotted graphs are a stretch (no native chart
  widget, would be hand-drawn Widgets). Keep V1 to ranked bars + numbers.
- Each panel = its own MCM toggle under a "World Almanac" group, all default OFF (opt-in, keeps the campaign
  bar uncluttered for players who do not want it).
- Player-experience-first: compute snapshots when the player opens the panel, do not maintain running stats
  for realism nobody sees; only the battle ledger persists, because the player can verify "was that battle
  actually recorded".

## WISHED (player request, 2026-08-27): a narrative STYLE attached to a character (or a station/culture)

Players want a character to keep a consistent writing voice of their own: a knight in a Dumas register, a
darker lord in Grimdark, a village commoner in plain prose, without the player switching the global style by
hand each time. Today the quill picker sets ONE active style for everyone, and switching mid-conversation lands
too late: the NPC-led opener, and the LLM's next completion drawing on the previous turn, still use the
previously selected style (a peasant "speaks like a brave knight" because Dumas was last picked).

Design notes (not ratified):
- Bind a style to a character (by hero id / name, like ChatPortraits and character_overrides), resolved at
  conversation OPEN so the opener already uses it, not mid-turn. Falls back to the global picked style, then the
  default, when a character has no assigned style.
- A tier below the character: a per-STATION or per-CULTURE default (a wanderer plain, a gang leader terse, a
  Battanian a given voice), so a whole class of NPCs reads right without naming each one. Resolution order:
  character override > station/culture default > global picked style > shipped default.
- Editable like the rest of the prompt surface: a small mapping file (e.g. `character_styles.json`, Custom-first
  so edits survive updates), plus a console command to set one live (`cr.style_for <name> <style>`), mirroring
  `cr.orientation`. The player also asked whether post_history_instructions could do it: it substitutes
  {{char}}/{{location}} as TEXT but cannot load a different style FILE, so it only affords weak inline guidance,
  not a real per-character style; this feature is the proper answer.
- Player-experience-first: purely a presentation layer (how the prose reads), never what a character knows or
  would do, exactly like the existing narrative styles.

## WISHED, DEFERRED (player wish, 2026-08-03): NPC behaviors DURING battles (a promise to stay by your side, honored in combat)

A player pledged a companion to "stay by my side in battle" and noted, correctly, that the engine will not honor
that without a helper layer steering an agent's combat AI (the player: "a completely different mod"). The wish is
broader than one line: NPC behaviors playing out ON the battlefield, driven by what was said or promised in
conversation (guard the player, hold a line, hunt a named rival, flee when broken) so that a spoken commitment
shows up where the player fights, not only in the journal.

Honest assessment: HARD, and a different discipline than CR's conversation/memory core. It needs a Mission-behavior
layer over the live battle (agent targeting / formation / behavior overrides via `MissionBehavior` + the agent AI
components), plus a bridge from a tracked commitment ("stay by my side") to a per-agent combat directive. Parked as
a future direction (roadmap, not scheduled). Gabriel: original and immersive, added for later.

---

## KNOWN BUG 25/07, NOT RE-VERIFIED 19/11 (1.30.15 changed the Options button): the chat does not slide aside when the escape menu opens

_Heading before the 19/11/2026 audit: KNOWN BUG, DEFERRED (player report, screenshot 2026-07-25): the chat does not slide aside when the escape menu opens over it_

A player sent a screenshot with THREE UI layers stacked at once: the town menu (left), the vanilla escape/PAUSED
menu (centre), and the CR chat window (right), all visible together ("the UI does not refresh").

Cause: the chat is an overlay layer on the map screen. `ChatManager.ArbitrateFocus` is meant to slide the chat
off-screen and yield focus whenever a modal opens above it, but that arbiter is pumped from
`ChatScrollablePanel.OnLateUpdate`, a widget INSIDE the chat's own layer. When the escape menu opens as a PUSHED
screen, the engine suspends the map screen (and the chat layer), so `OnLateUpdate` stops ticking, the arbiter
never runs, and the chat never slides aside. Chicken-and-egg: the detector lives in the layer that got suspended.
(Same freeze would hang an in-flight LLM reply, since `MainThreadDispatcher.Pump` shares that tick.)

Planned fix (LOW RISK, additive, NOT started): make the chat's "Options" button (`ChatViewModel.ExecuteOptions`)
PROACTIVELY do the same yield the arbiter already does (slide aside + reset input + `_chatHoldsFocus=false`)
BEFORE opening the escape menu, so the chat is out of the way even if the pump then freezes. Recovery is the
existing reclaim branch when the menu closes and the map resumes ticking. Do NOT alter `ArbitrateFocus` itself;
the pre-yield is a superset of current behaviour (idempotent when the menu is a mere layer).

Gabriel deferred it (2026-07-25): the focus arbiter was tricky to get right across the naval map / encyclopedia /
pushed-screen cases and has no auto-tests, so any change needs the manual regression checklist first (Options from
a town chat; encyclopedia; naval map; captive-scene mission where Options is hidden; menu-close reclaim). Confirm
the exact path with the player's log first (`[MENU]` + `[SCREENS] yield` lines; absence of a `yield` line = the
pump-freeze case, which the pre-yield fixes).

---

## WISHED (Gabriel 2026-08-21): a prisoner who tries to flee triggers the unarmed combat scene

In a captor/captive scene a prisoner may attempt to bolt. Rather than a text-only "you failed to hold them", it
could drop into the real UNARMED combat scene (the player, or the scene, physically stops them), reusing the duel /
`MissionFightHandler.StartCustomFight` machinery the DUELS pillar already owns (and the captive-ambush 3D fight).
A brief, weaponless struggle: subdue the fleeing captive, or let them slip away. Ties the escape beat to a real
mechanic instead of prose. Design it against the captive-escape pillar and the duels-from-chat plumbing so it is
not built twice.

WISHED (Gabriel 2026-08-26): an IN-TOWN captive escape. The CR escape (CaptiveEscapeCoordinator) is map-only by
design: it stages when Mission.Current is null, which never happens while the player is inside a settlement (the
campaign tick is paused and a location mission holds Mission.Current). This is fine for now, captivity happens
with the captor's party on the map, so the common case works. A real in-town escape would mean staging the fight
in the PRISON / DUNGEON scene (the settlement-interior mission path the audience staging already uses via
LocationEncounter.CreateAndOpenMissionController), and driving the coordinator from the pause-independent
OnApplicationTick with a settlement-location-mission end, exactly as the town DUEL launch now does. Deferred:
Gabriel is comfortable that CR's escape attempt does not apply in town until then.

## WISHED (Gabriel 2026-07-19): a FEATURES section in MCM, one toggle per pillar plus its thresholds

**The ask:** a new MCM section ("Calradia Remembers Features" or similar) giving the player a toggle for
each of the mod's systems (marriage, assassinations, jealousy, bastards, letters, duels, nemesis, captivity,
interception, quests...) and, per section, the threshold values that govern it, editable rather than baked in.

**Why it is worth doing:** the mod is now large enough that no two players want the same one. Someone here
for lordly politics does not want bastards; someone here for romance does not want a nemesis hunting them.
Today the only dials are `Adult Content Level`, `Relationship Pacing` and `Lords Approaching You`, and each
was added because a player asked. That is the pattern arriving one request at a time, and it should be a
structure instead.

**Existing MCM groups**, which the new section must sit beside without duplicating: LLM Provider, OpenRouter,
NanoGPT, OpenAI-Compatible, Content, Character, Companions, Diagnostics. The three dials above already live
in Content/Character; decide whether they MOVE into Features or stay where a player already found them
(moving a setting a player has already configured is not free, MCM keys are how the value is persisted).

**The three problems to solve before writing any toggle:**

1. **A toggle is not an erase, and must not be.** Turning marriage off while the player IS married cannot
   unmake the marriage: the save holds it, other systems reference it (jealousy, bastards, succession,
   grudges), and stripping it would leave dangling references across the ledger. The contract has to be
   "stop GENERATING new ones, honour what exists". That has to be stated once and applied uniformly, or every
   pillar will answer it differently and the player will not be able to predict any of them.
2. **Exposing a threshold changes the tested layer.** 63 constants are already marked TUNE in
   `CalradiaRemembers.Logic` and they are `const` today, which is what makes the pure policies pure. Making
   them player-editable means threading a settings carrier into those policies. Doable and still testable
   (pass the value, test the boundary), but it is a broad, mechanical refactor and the tests that pin
   boundaries by literal number will need to pin them by the injected value instead.
3. **Player-facing dial or modder-facing JSON?** Not every knob belongs in MCM. The project already puts
   `jealousy_cultures.json` and the narrative files in editable JSON precisely so a total conversion can
   change them without a recompile. A rough split to ratify: MCM for what a player tunes to taste (frequency,
   severity, on/off), JSON for what a MODDER redefines (catalogues, cultural rules, vocabularies). Putting a
   catalogue in MCM would be unusable; putting a frequency in JSON would be unfindable.

**Also worth deciding:** a toggled-off pillar should stop costing TOKENS too, not merely stop firing. Most
pillars own a prompt section, so an off switch that still ships its teaching text is a silent tax on every
request. The `LeanPromptPolicy` seam already gates sections by level and is the natural place.

**Not scoped yet.** Needs a pass to enumerate the actual pillar list and, for each, which thresholds are worth
exposing (most of the 63 are not: an internal cooldown nobody can reason about is noise in a menu).

## PARTLY BUILT (the council is live; AssemblyBehavior and AssemblyMenuBehavior are gated OFF in NpcMemoryServiceSubmodule): the ASSEMBLY

_Heading before the 19/11/2026 audit: NEXT PILLAR (near-term): the ASSEMBLY (multi-party gatherings)_

Ratified with Gabriel 2026-07-10. NOT started yet, scheduled "very soon". A player (or a lord) convenes an
ASSEMBLY of lords and companions at a strategic settlement: the invited travel there in person, a dedicated
multi-party session orchestrates the meeting, and missions may be distributed. Example: a lord at war summons
a war-council of all his allies to talk strategy. The pillar = the conversational ENGINE (already built, see
below) + a convening/travel/presence layer + a session layer + a mission-distribution layer.

**STATUS (2026-07-27): AUDIT-CONSEIL remediation COMPLETE (code lots), not yet play-tested. The Assembly
convening MENU stays GATED OFF (`NpcMemoryServiceSubmodule` AddBehavior lines) pending Gabriel's in-game test;
the council session itself (`cr.council`) is active and testable.** Re-verified against main: Inc 1/2 had
already fixed C1 (per-member resolutions consumed), C2 (target_settlement taught), C3 (prompt contradiction +
council action guard), M3 (RSVP order), m3 (anchor stability). Done this session: **C4** in-flight council
persists save/load (`CouncilSessionBehavior` + shared `CouncilLift`, only AGREED pledges lift on load, no
double-execution); **M1/M2** `OpenForCouncil` guards (`CouncilAccessPolicy`); **M4** `ResolutionCatalog` is now
the canonical kind registry (enum<->string, only executable kinds lift); **M6/M7** `WitnessIds` + a pledge is
remembered in every outcome (`CouncilPledgeMemory`); **M9/M11/m2** ledger hardened + `Sanitize`; **M12**
per-member lap capped (`RoundTablePolicy.CouncilLapCap`); **m4/m5/m6/m7/m10** hardening. **M8/M10 (votes) PARKED**
(consultative ratified). Ratified decisions (2026-07-27): **(Q2)** `change_relation` is immediate at the table,
deed pledges are deferred; **(Q4)** persist the ledger; **(Q5)** EXTEND the `[RESOLUTION]`->quest path now,
MIGRATE to the `[PLEDGE]`/`CommitmentBehavior` deferred-over-days design LATER (the R1 section below is the
FUTURE path, not the current one); **(Q6)** CONSULTATIVE (a spoken pledge IS the decision, no binding votes).

**FOUNDATION ALREADY BUILT (2026-07-10, committed + GATED OFF `RoundTableController.Enabled = false`, never play-tested): the ROUND TABLE.** A deterministic
group-turn driver, the model-independent answer to a player report (weak LLMs never volunteer witness
reactions, so witnesses stay mute unless singled out; the existing Sprint 15C back-and-forth is model-driven).
Trigger = a "Round Table" chat button OR a free-text intent ("what do you all think?"). It gives each present
participant a deterministic turn (they may address one another), told to speak at more length. Files: pure
`RoundTableIntentDetector` + `RoundTablePolicy` (Logic, tested), `EncounterContext.IsRoundTableTurn` + SDK
`PromptBuilder` "ROUND TABLE" directive, mod `RoundTableController.RunAsync`/`ChatViewModel.ExecuteRoundTable` (reuses the
15B/15C per-speaker machinery: `SpeakerForWitness`/`BuildSessionFor`/`RenderNpcTurn`), a prefab button. In the
chat it caps at 3 turns per burst; the ASSEMBLY lifts that cap (full round of present participants). Ship this
as v1.31.0 (a shippable feature on its own, and the assembly's engine).

### CONVERGENCE RATIFIED 2026-08-29 (supersedes parts of the 2026-07-10 decisions below)
Triggered by player feedback that the war council felt "too magic" (distant kingdom nobility appearing in person when the player is not the ruler, no distance). Council (cr.council, shipped, instant/abstract) and Assembly (this section, real cross-map travel + RSVP + ambassador, dormant) are two parallel implementations of one concept: CONVERGE them into a single council driven by ONE pure attendance policy + one session engine, and RETIRE the Assembly's real cross-map travel.
- Attendance decided per candidate as InPerson / Ambassador / Absent: prisoner -> Absent; in the player's PARTY -> InPerson; own-faction lord -> InPerson if travel-time <= an MCM threshold (~1 day) and not declining (war / hostile regard), else Absent (NEVER an ambassador for your own faction); other-faction lord (parley only) -> Ambassador; else Absent. Instant model (no simulated travel); a war council with your Army falls out for free (army allies are at distance 0).
- Ambassador = ALWAYS a light generated synthetic envoy (rankless herald: historically you did not risk sending a capturable Lord). NOT a clan member or companion (that part of decision 2 below is dropped). One generic herald portrait for now, faction carried by the name/title "Herald of {Faction}"; ephemeral; speaks in the lord's name (provisional / "subject to his lord's seal").
- Retire: real party movement (SetMoveGoToSettlement), the separate "Summon an assembly" menu, deadline/EnRoute, AssemblyRecord persistence. Keep: campaign-bar entry, book UI, round-table engine, resolutions/EngagementLedger, EnableCouncil MCM; fold the RSVP/distance logic into the attendance policy.
- Increments (test-first): (1) pure CouncilAttendancePolicy (.Logic, consolidates CouncilRosterPolicy + AssemblyRsvpPolicy + arrival) -> (2) mod projection/roster (ETA) -> (3) session unification (seat InPerson + Ambassador) -> (4) synthetic envoy identity -> (5) entry consolidation + retire Assembly + MCM proximity threshold. Full design in memory [[project_assembly_vision]].
- MARQUEE FUTURE MODE (player-wished 2026-08-29): an ad-hoc DIPLOMACY / TREATY table. The player brokers a treaty between two (or more) OTHER factions by inviting specific lords into one negotiation via the messenger, gated by a minimum personal regard with each participant. It sits on top of the converged council + the herald (other-faction envoy) + the invitation layer, and is the use-case that justifies the herald work. Open question at build time: foreign lords in person on neutral ground, or by envoy. Build after the convergence core.

### Locked design decisions (Gabriel 2026-07-10)
1. **Host = ANY town or castle.** RSVP refuses an ENEMY-owned host; accepts a host that is the invitee's own,
   an ally's, or neutral. Emergent constraint: a parley with an enemy therefore requires NEUTRAL ground.
2. **REAL party movement** toward the host settlement. If an accepted invitee cannot present himself by a
   deadline, an **AMBASSADOR** (a stand-in of his clan or a companion, else a light generated envoy) is created
   to sit in his name. This is the robust answer to fragile forced-AI movement (CR's courier scar tissue):
   narration covers the gap instead of a stuck party breaking the feature.
3. **Enemies MAY be invited** (a parley). A refusal costs NO relation.
4. **The PLAYER decides when to open** (no minimum present). Invitees wait; the player must WAIT at the
   settlement for people to arrive. If the player tries to LEAVE the town/castle, a popup asks to cancel;
   leaving CANCELS the assembly (invitees stand down, ambassadors despawn).
5. **NO participant cap:** the player invites and assumes the LLM cost (do not limit roleplay). Consequence: in
   assembly mode the round-table goes the FULL round of present participants (no 3-turn per-lap cap, that is
   only the in-chat convenience), and the player paces it, addressing individuals as he likes.

### Lifecycle
1. At a town/castle menu: "Summon an assembly" (invitee picker: known lords + companions) sends summons (CR letters).
2. RSVP resolved (policy + a short acceptance/refusal letter). Accepted lords travel to the host; party companions are present already.
3. Settlement menu shows a live counter "Assembly: X of N present", updated as parties arrive.
4. The player opens when ready: "Open the assembly" menu button opens the round-table session over the present roster.
5. (Later inc) missions distributed during the session.
6. REVERSE: a lord summons the PLAYER (a war-council); a summons letter arrives, the player travels to the lord's settlement, and on arrival the same menu opens the assembly with the lord as host.

### Increment breakdown + seams
- **State layer (foundation):** `AssemblyBehavior : CampaignBehaviorBase` holding the pending assembly (host `Settlement`, initiator, invitees each { heroId, status Invited/Accepted/Declined/EnRoute/Present/Ambassador, ETA, deadline }). `SyncData` ADDITIVE + idempotent restore (save-safe hard rule: an in-flight assembly must survive a save/load). Pure `.Logic` (testable, delegatable to Sonnet): `AssemblyRsvpPolicy` (relation to the host owner, war, distance, occupation, besieged -> Accept/Decline + reason; enemy-owned host -> Decline) and `AssemblyArrivalPolicy` (ETA + deadline + the ambassador-fallback decision).
- **Inc 1: convene + presence (no session yet).** Settlement menu options via `AddGameMenuOption` (CR already touches game menus): "Summon an assembly" (picker), dynamic "X of N present" text, "Open the assembly". Summons via the letter system; RSVP + reply letters. Accepted lords move via `SetMoveGoToSettlement` refreshed on tick (courier pattern); presence marked on `OnSettlementEntered` when the party enters the host. Deadline -> ambassador. Leave-guard: intercept the settlement Leave menu -> "Cancel assembly?" popup -> cleanup. Deliverable: summon, watch them converge, counter rises, cancel works. No dialogue yet.
- **Inc 2: the assembly session** = the round-table engine over the present roster (2-companion cap lifted, full round with no 3-cap, a "host" framing in the prompt, ambassadors speak for their lord).
- **Inc 3: lord-summons-player** (summons letter -> player travels to the lord's settlement -> on arrival the menu opens the assembly, lord = host).
- **Inc 4: mission distribution** during the session, SUPERSEDED by the COUNCIL-as-NEGOTIATION design (next section): missions distributed in assembly become CONSIGNED RESOLUTIONS taken before witnesses (ambassadors pledge in their lord's name), not ordinary quests.
- **UI:** first reuse the existing chat window (functional); a bespoke multi-portrait assembly window is later polish.

### Technical risks (flagged, not blocking)
- **Enemy on neutral ground:** ensure no auto-hostility/encounter fires during a parley (a safe-conduct); needs handling.
- **Ambassador identity:** prefer a living clan member or a companion of the lord; fall back to a light generated envoy (needs a profile + portrait for the session). To confirm at build time.
- **Real-movement fragility:** mitigated by the ambassador fallback (Gabriel's idea turns the failure mode into narrative).
- **Cost:** uncapped by design (the player's choice).

### Reuse (not from scratch)
CR letters/couriers (summons + replies), the courier pattern (visible movement + guaranteed arrival/ambassador), settlement game menus, the ROUND TABLE engine (session), `QuestFactory`/viable-quest referential (missions), and the interception 3D-conversation staging (VRAM-safe) for the meeting.

### STATUS: Inc 0 BUILT 2026-07-12 (autonomous, uncommitted, unit-tested; NOT play-tested)
Pure `.Logic/Assemblies` (all tested), plus the mod state layer:
- `AssemblyRsvpPolicy.Decide(AssemblyInvitationFacts)` -> `RsvpOutcome` (Accept/Decline + reason). Order: companion-shortcut, availability (dead/prisoner/occupied), enemy-owned host (neutral-ground constraint emerges), willingness (a lower regard floor for a parley than for an ally), relevance (far + lukewarm). Tuning constants: `HeedRegardFloor -20`, `ParleyRegardFloor -60`, `FarDistanceTier 3`, `WorthTravelRegard 10` (all TUNABLE, exhaustively tested by decision SHAPE not exact numbers).
- `AssemblyArrivalPolicy.ResolveArrival(etaDays, daysUntilDeadline)` -> InPerson/Ambassador (the fragile-movement fallback made narrative).
- `AssemblyProgress.From(statuses)` -> present/inTransit/declined/awaiting + `AllResolved` (the "X of N present" counter + "stop waiting?" check).
- `AssemblyDistanceTiers.Of(mapDistance)` -> tier 0..3 (TUNABLE thresholds).
- Enums `RsvpDecision/RsvpReason/ArrivalMode/AssemblyInviteeStatus`, struct `AssemblyInvitationFacts`.
- Mod `Assemblies.AssemblyBehavior : CampaignBehaviorBase` (registered): holds one `AssemblyRecord` (host/initiator/deadline/invitees), save-safe byte[] JSON SyncData (additive, idempotent, survives save/load), API `Begin/SetStatus/Progress/Cancel`. Passive (no events yet). Added `IsExternalInit.cs` polyfill to the Logic project. NOTE: namespace is `Assemblies` (plural) to avoid clashing with `System.Reflection.Assembly`.

### Inc 1 wiring plan (outward-facing, NOT done, needs Gabriel + in-game iteration)
The facts-gatherer maps the campaign into `AssemblyInvitationFacts` (untestable engine reads; confirm each API at build):
- `IsAlive` = `hero.IsAlive`; `IsPrisoner` = `hero.IsPrisoner`.
- `IsOccupied` = the invitee's party is in a `MapEvent` (battle) or its settlement is under siege.
- `HostOwnedByInviteesEnemy` = `hero.MapFaction.IsAtWarWith(host.OwnerClan.MapFaction)`.
- `RegardToInitiator` = when the initiator is the player, the mod `Store.Get(hero.StringId)?.ReputationWithPlayer`; else vanilla `hero.GetRelation(initiator)`.
- `InitiatorAtWarWithInvitee` = `initiator.MapFaction.IsAtWarWith(hero.MapFaction)`.
- `DistanceTier` = `AssemblyDistanceTiers.Of(hero.PartyBelongedTo.Position2D.Distance(host.Position2D))`.
- `IsCompanionOfInitiator` = the hero travels in the initiator's own party (MainParty for the player).
Then: settlement menu "Summon an assembly" (invitee picker, filtered to eligible lords + companions) -> `AssemblyBehavior.Begin` -> summons via CR letters -> RSVP resolved by the policy, reply letters -> accepted lords `SetMoveGoToSettlement` (courier-pattern tick) with `AssemblyArrivalPolicy` re-evaluated -> presence on `OnSettlementEntered` -> live "X of N present" menu text from `AssemblyProgress` -> leave-guard popup cancels. All player-visible, so intentionally left for a supervised build.

---

## PARTLY BUILT: Inc 0 passive (LordRescuePolicy has no caller outside Logic), Inc 1 OPEN: deliberate rescue

_Heading before the 19/11/2026 audit: PILLAR: DELIBERATE RESCUE (ratified Gabriel 2026-07-18/19): Inc 0 BUILT overnight, PASSIVE, wired to NOTHING_

**Where it came from:** a lord fell upon the bandits holding Gabriel's character; she slipped away in the
confusion of the fight and thanked him for a rescue. It was an accident of vanilla AI, and CR had no part
in it (the log settled that, after two wrong readings). His call: make it DELIBERATE.

**The three paths, ratified:**

| Path | Trigger | Behaviour |
|---|---|---|
| **CR rescue** | explicit, rationed | the lord rides FOR the player; intent is genuinely known |
| **Vanilla incident** | the AI hunts bandits on its own | the lord DISCOVERS the player was held by that band |
| **Interception while captive** | CR | **blocked outright** |

The vanilla-incident path is DONE and shipped in v1.31.0 (`CaptivityReleaseNarrator`). The other two are
the work.

**Gabriel's rules, all of them load-bearing:**
- The gate is the captivity having been **LIVED** (its event played), NOT a number of days. A rescue that
  can fire on day one cancels the captivity instead of ending it, and being a prisoner stops costing
  anything.
- A lord with **no interest** does not come, and the interception is stopped rather than quietly turned
  into something it is not.
- Eligibility is **not** "who likes the player enough". The transcript wrote the rule: that lord framed it
  as DUTY, not affection. So an honourable stranger is a legitimate rescuer.
- If the player gets free before the rescuer arrives, he **calls it off**.

**Inc 0 (BUILT 2026-07-19, overnight, wired to NOTHING):** `LordRescueFacts` + `LordRescuePolicy` with 21
tests. Six motives (Kinship, Devotion, Vendetta, Fealty, Regard, Duty), five hard gates, and the
abandon rule. The motive is returned beside the verdict on purpose: when CR dispatches the rescuer the
intent is finally KNOWN, which is the one case where the chronicle may honestly say he came for the player.

**AWAITING GABRIEL'S RATIFICATION (tuning, all flagged in code):**
- `RegardRidesThreshold = 25` (personal regard at which a lord rides for the player's own sake)
- `DutyHonorThreshold = 1` and `DutyMercyThreshold = 2` (kept as SEPARATE paths, not a sum: principle and
  pity are different reasons and a lord may have either)
- Scope call to confirm: increment 1 is **bandit captors only**. A lord captor can only be attacked where a
  war already exists, which is a different rule with a diplomatic consequence.

**Inc 1 (next, needs his eyes, NOT overnight work):** the `IsPrisoner` guard in `InterceptionBehavior`
becoming the switch (eligible rescue, he rides; otherwise nothing at all), the dispatch itself on the
proven `AmbushCaptivityBehavior.TryRescueRoll` pattern, and the captivity-event trigger/scene split so the
"lived" gate exists below Hardcore (today the captive encounter is Hardcore-only, so keying eligibility to
it as-is would silently make rescue a Hardcore feature). **CORRECTION (2026-07-19): an earlier note here claimed `CalculateProbability` had no callers. That was
wrong, found while reading the bandit path: it IS applied, at `CaptiveEncounterService` line 249, so the
documented "15% per day, cap 60%" rationing does gate the BANDIT captor scene. Whether the LORD path applies
it too has not been checked.**

## PARTLY BUILT: Inc 0 passive (TroopLoanPolicy unused; lend_troops still the old verb), Inc 1-2 OPEN: troop loans

_Heading before the 19/11/2026 audit: NEXT PILLAR: TROOP LOANS (ratified Gabriel 2026-07-18): Inc 0 BUILT, PASSIVE, untested in game_

**The report:** Mina gave the player a hideout quest and offered to send some of her men along. Nothing
happened. `lend_troops` was never eligible (it wants relation >= 20; she was a stranger at 0), so the offer
was pure fiction, and the anti-empty-promise rule did not catch it because the promise rode in the QUEST
TEXT, which no rule polices.

**What scoping it turned up, and it is the bigger fault:** `lend_troops` calls `MoveLowestTierTroops` one
way and keeps NO ledger. What the verb calls a loan has always been a permanent GIFT. A player repeating it
every 14 days (the cooldown) accumulates free soldiers for life.

**The design, every boundary Gabriel's call:**

- **A LOAN is bound to a quest.** No quest means no way to know when the men go home, so that case is a GIFT
  instead, and a gift keeps no ledger at all. This removes the whole "return after N days" machinery.
- **Two verbs, and here a second verb IS justified** (the criterion being execution, not gating): a loan
  comes back and a gift does not. `lend_troops` becomes a true loan; the permanent NPC-to-player gift needs
  its own verb (`give_troops` is already taken, it is the reciprocal player-to-NPC direction).
- **Casualties carry NO consequence**, even if every man dies. The quest's own success or failure is the
  consequence; the lender does not also grieve the arithmetic. Whatever survives goes back.
- **The loan closes when the DEED is done** (the hideout falls), NOT when the player walks back to collect
  the reward. That is exactly `InformalQuest.IsOutstanding` going false, a distinction the model already
  documents ("an Active quest can already be satisfied and awaiting the player's report back").
- **Every resolution returns them, not just success.** If the men only came home on success, FAILING a quest
  would be the way to keep them permanently. Same flag covers expired, abandoned and cancelled, with no
  penalty attached.
- The men leave silently, with a message telling the player they are returning to their banner.

**Inc 0 (BUILT 2026-07-18, wired to NOTHING):** SDK `TroopLoan` + `NpcProfile.TroopLoans` (additive, an old
save loads empty, migration test first-class); pure `TroopLoanPolicy` (`ShouldReturn`, `ReturnableCounts`,
`LoansToReturn`) with 11 tests. `lend_troops` keeps its current behaviour until Inc 1.

**Inc 1:** wire the quest escort (its own gate, LOWER than relation 20 since the task is the justification,
but requiring the quest to exist). **Inc 2:** the gift verb, and retro-fit `lend_troops` onto the ledger.

## RULE (ratified 18/07, backlog cleared): the test architecture, three lanes

_Heading before the 19/11/2026 audit: TEST ARCHITECTURE RATIFIED (Gabriel 2026-07-18): three lanes, and what each is actually for_

Born from a cost question ("can cr.test all be shortened by removing redundancy?") whose premise turned out to
be wrong, and worth writing down because the measurement reverses the intuition.

**The measurement** (from a full `cr.test all` run, 2026-07-18): 76 tests, 583.7s. **13 LLM tests = 583.2s
(99.9%). The other 63 mechanical tests = 0.5s TOTAL.** Removing every "redundant" mechanical test would save
half a second out of thirteen minutes. All the cost is real LLM calls, so that is the only place cost can be cut.

### The three lanes

| Lane | Question it answers | Where it lives | Cost |
|---|---|---|---|
| **A** | Does the prompt INSTRUCT / INCLUDE X for this config? | `NpcMemoryServiceTests` (headless) | free, auto-runs |
| **B** | Given a response R, does the mod DO the right thing? | **unit tests, on extracted `.Logic` rules** | free, auto-runs |
| **C** | Does the model actually BEHAVE well? | `NpcMemoryService.LiveLlmTests`, opt-in | tokens |

**Lane B needs no mock LLM client and no console command** (a wrong turn taken and corrected on 2026-07-18).
`ParsedResponse` is an SDK type: a unit test constructs one by hand and feeds it to a pure rule. What was
blocking was never the LLM, it was that these decisions sat inside `ChatViewModel`, and the mod is net472
while the test project is net10. The fix is the project's OWN documented pattern: decision in `.Logic`, thin
controller in the mod.

### Trigger vs consequence (Gabriel's framing, and the rule that follows)

The same fact is testable two ways, and the distinction decides the lane: a value can be a TRIGGER (feed it
in as a stub input, unit test) or a CONSEQUENCE (let real play produce it, in-game test).

The grudge saturation regression of 2026-07-18 is the case study, because BOTH halves were already unit
tested and it shipped anyway:

- `RigidityAt(-100, facts).Should().Be(100)` pinned the trigger, and asserted the BUG as correct behaviour.
- `IsResolvable(grudge)` pinned the consequence, but always on a rigidity the TEST invented
  (`NewGrudge(rigidity: 50)`), never on one `RigidityAt` had actually produced.

**RULE: when the output of one pure function feeds the threshold of another, test the CHAIN, not just each
end.** A test that fabricates the next stage's input can never see a mismatch at the boundary, and the
boundary is where that bug lived. The in-game test caught it only because it necessarily chained them on real
campaign data (a lord at regard -80 that no developer would have thought to write down).

**Which is the irreducible value of the in-game suite**: not assertion power, but running on input
combinations nobody planned. That justifies keeping the 63 mechanical tests (0.5s, real campaign objects) and
condemns the LLM showcases, which explore nothing: they replay five framings written in advance.

### The showcase finding

The five captor variants (`chat_captor_cnc`, `_break`, `_group`, `_torture`, `_cnc_mf`) cost **232 seconds**
and assert exactly ONE thing each: `t.Expect("threeBeatsReplied", replied, true)`. Everything else is "judge
in log", i.e. a human reading the transcript. They are prompt-iteration TOOLS, not regression tests.

### The split to build

- `cr.test all` = regression: the 63 mechanical + lane B as it grows. Seconds, free.
- `cr.testchatall` (new) = conversation quality: the 13 real-LLM tests, on demand.
- Keep ONE captor variant in the sweep as a canary for total generation failure (which is exactly what
  `chat_captor_scene` caught on 2026-07-18: an empty beat 2 after the client's own retry).
- The mechanism is half-built already: `SelfTest.CallsLlm`, `cr.test chat`, `cr.test mech` all exist.

### The lane B thesis, proven by a shipped bug (2026-07-18)

`BannerlordGameStateBridge.GetPlayerStatus` mapped engine state to `PlayerStatusVsNpc` with **vassal and
liege inverted**, and it reached a player (JoshGray700, Nexus, v1.30.16, Grok low reasoning). The Aserai
Sultan was told "The player is your liege lord.", and played it straight: he denied being Sultan, invented a
third-party Sultan to reconcile the clash with his own "Holds royal authority over a kingdom." rank line, and
called his brand-new vassal "my liege".

Three lessons, all of which the architecture above had already predicted:

1. **The naming trap.** `PlayerStatusVsNpc` describes THE PLAYER's standing. Read from the call site's own
   point of view ("this NPC is my liege") the values look swapped. The bridge's comment said "Player is a
   vassal" and the line beneath it returned `Liege`. A comment is not a test.
2. **Test the CHAIN, not the enum.** A test asserting `Resolve(...) == PlayerStatusVsNpc.Liege` would have
   restated the convention rather than checked it, and would have declared the bug correct, exactly as the
   grudge saturation tests did. `PlayerStandingPolicyTests` therefore asserts the **rendered sentence**, the
   thing the model actually reads.
3. **A bare conclusion is unfalsifiable.** The prompt asserted a rank the model could not check, while
   another section contradicted it, and the model resolved the clash the wrong way. The feudal note now
   states WHO RULES WHAT alongside the verdict (`EncounterContext.SharedRealmName`).

Fixed with the house facts pattern (`AppearanceFacts` / `AppearanceComposer`): `PlayerStandingFacts` (plain,
engine-agnostic) to `PlayerStandingPolicy` (pure, tested) to prose. `GetPlayerStatus` was the one place that
had skipped that pipeline and jumped straight from engine state to a conclusion.

**Gabriel's question, and why the answer was not JSON** (2026-07-18): the proposal was to ship raw game state
as JSON so the prompt could never misrepresent the game. It would NOT have caught this bug: the bridge would
have emitted `"player_status": "liege"`, the same inverted value, and harder to spot than an absurd English
sentence. The defect was in the mapping, not the format. Shipping raw facts would also move the derivation
from lane B (deterministic, free, testable) into the model itself (lane C only), which is the wrong direction
for a mod that must run on weak, cheap models: multi-hop inference is exactly what they fail at, and this
report came from Grok low reasoning. The valuable half of the idea (facts, not conclusions) is what the facts
pattern above already delivers, in tested code. Structured data stays where it belongs: the machine-to-machine
seams (`VerbFacts`, `ExternalVerbContract`, `ActionRequest`).

**Two further gaps found while fixing it, both now CLOSED (same day):**

- **The mercenary trap, and it was the same bug wearing another hat.** `PlayerStatusVsNpc.Mercenary` was
  never returned at all, and that was not merely a missing sentence: the engine records a mercenary contract
  by setting the clan's `Kingdom` exactly as it does for a sworn vassal, so once the fix above was in, a
  hired-sword player read as an OATH-BOUND vassal to the employing king. CR's own `join_as_mercenary` action
  creates that state. `PlayerIsMercenaryOfNpcRealm` is therefore asked BEFORE either feudal fact, and the
  order is load-bearing. The note says pay and not fealty, because everything that follows (no loyalty owed,
  the contract ends, they may take the enemy's coin next) follows from that.
- **Fellow vassals rendered as silence.** Two lords of one crown, neither ruling, resolved to `Free`, so the
  prompt said nothing and a comrade sworn to the same king met the player as a stranger of no standing. New
  `PlayerStatusVsNpc.SameRealm` (appended last), and the note states equality explicitly ("neither commanding
  the other") so the model cannot invent a rank between two lords who have none over each other.

**The mirror, also closed the same day, and scoping it turned up TWO situations rather than one:**

- The player's own crown rents this NPC's sword. Reported as `Liege`, claiming a fealty a contract does not
  buy. New `PlayerStatusVsNpc.Employer` (appended last): paymaster, not liege, and the contract will end.
- A hired band in a realm the player merely SERVES. This one is subtler and is why the fix needed its own
  fact rather than a tweak to the liege branch: it would otherwise have fallen through to `SameRealm` and
  been called a comrade of the same oath. They took the same coin, not the same vow, so the policy
  deliberately says NOTHING there. Saying nothing beats saying something false.

The whole family now rests on one principle worth keeping: **the engine stores a contract and an oath
identically, so every status that reads `Clan.Kingdom` must ask about mercenary service FIRST, in both
directions.** Four of the five bugs in this cluster were that one ambiguity wearing different hats.

### Extraction backlog (lane B)

Decisions written straight into `ChatViewModel` and therefore untestable. Each one guards a fault found by
the 2026-07-18 audits:

| Rule | Where | Status |
|---|---|---|
| Witness reaction presence/self/repeat filter (D1) | `CollectWitnessReactions` | DONE: `WitnessReactionGate` + 11 tests |
| Assistant-turn recomposition (C4) | `BuildSessionFor` | DONE: `SessionHistoryComposer` + 13 tests |
| Refusal note from a bridge outcome | `ChatViewModel.Exchanges` | DONE: `ActionRefusalNote` + 5 tests |
| Dropping ventriloquized reactions when the floor opens (D7) | `ChatViewModel.Exchanges` | DONE: `VentriloquizedReactionPolicy` + 5 tests |

Backlog cleared 2026-07-18. Mod builds 0 warning, tests 1952 PASS / 0 FAIL. UNTESTED IN GAME.

**The transcript itself moved to Logic in the same batch** (`ConversationTranscript` + `TranscriptLine`), which
is what makes the C4 extraction structural rather than cosmetic. It had been a raw `List<TranscriptEntry>` of
a class nested privately in `ChatViewModel`, so the conversation's single source of truth was unreachable by
any test even though not one line of it touches the engine. It is written in ONE place and read in TWO, both
load-bearing (every rebuilt ChatSession; the text the memory summarizer turns into a permanent memory), so it
now owns its own invariant (a blank line is never recorded) instead of trusting each caller. The controller's
three entry points are one-line delegations. Any future pure transcript rule has an obvious tested home, and
`TranscriptLine` is immutable so no participant's view can be rewritten after the model has been shown it.

Each extraction is behavior-preserving except one deliberate fix, and both of the defects found were at a
BOUNDARY, which is the rule above proving itself twice more:

- D1: the last-line lookup used the name the MODEL wrote while the store is keyed by the CANONICAL name, so a
  casing difference silently defeated the anti-repeat rule.
- The refusal note interpolated `outcome.Message` unconditionally, so a refusal the bridge did not explain
  produced a note trailing a dangling separator into the prompt. `ActionRefusalNote` now emits a clean
  reasonless denial in that case (the only behavior change in the batch).
- C4's own boundary is now pinned: an entry trimmed by `excludeLastN` can no longer be pulled back into the
  preceding run and smuggled, tagged as already said, into the very turn it was withheld from.

---

## PARTLY BUILT (chat skins 1.32.0, letters as a book 2.2.1, dossier panel), OPEN: AI scene-image generation

_Heading before the 19/11/2026 audit: WISHED (Gabriel 2026-07-20): a bespoke visual identity for CR's own windows_

Gabriel saw a screenshot of AI Influence's "Book of Memory" (КНИГА ПАМЯТИ): a full-screen bespoke window with
a real visual identity: dark ornate outer frame, warm parchment panels, serif headers, a master/detail layout
(a scrollable list of memories left with title + "day: N", a large detail panel right with the entry text and
a per-entry illustration, a "AlIANA . DAY: 65" footer, Back/Next pagination "4 of 10"). CR's windows are
FUNCTIONAL but plain by comparison (chat, chronicle, the RP dossier, the letter parchment, and the assembly
window still to come). This is a real competitive gap worth closing: a consistent, recognisable CR look that
makes the mod feel like a crafted product, not a debug panel.

Scope / ruling:
- **CR's OWN style, not a copy.** Study how AI Influence builds its custom Gauntlet windows for TECHNIQUE
  (ornate brush frames, panelled backgrounds, serif brushes, master/detail scroll, pagination), then give CR a
  distinct identity. We already own most of the machinery: named brushes, `AutoHideRichTextWidget`,
  `NavigatableListPanel` scroll lists, the runtime JPG/PNG-in-widget recipe (VRAM-flat), custom prefabs +
  UIExtenderEx (see the Bannerlord UI patterns note in Claude's memory). The gap is a deliberate visual
  language (a shared brush set / frame prefab) applied across all CR windows, not new tech.
- **First candidate = the Chronicle / memory view** (the direct analogue of their Book of Memory), then the
  chat window and the dossier, then the new ASSEMBLY window (whose "bespoke multi-portrait window" was already
  flagged as later polish, line ~125). Do it as ONE shared style pass so the windows look like a family.
- **The AI-generated illustrations were PARKED, now being REVISITED** (2026-07-29, on repeated player demand,
  Gabriel open in-thread). Originally a separate-mod concern (Claude memory `project_parked_generated_portraits`);
  CR uses real portraits from BodyProperties by default. See the dedicated WISHED entry below; if added it stays
  strictly OPTIONAL / off by default. A richer illustrated panel using the player's actual character portrait
  (which CR can already render in-widget) remains in scope regardless.
- Cost: pure cosmetic/UX, no logic; slower iteration loop (a prefab edit needs a build + a game restart, the
  GauntletUI prefab cache). Best done as a focused polish sprint, not folded into a feature.

### WISHED (player thread Romde1 / Cyb3rFuse / AbbaDF, Nexus 2026-07-28/29): optional AI scene-image generation
REVISITS the parked "generated illustrations" ruling above ([[project_parked_generated_portraits]]): strong,
repeated player demand plus Gabriel open to it in-thread ("I can add this feature on the roadmap"). Like AI
Influence's per-scene images, but done CR's way: strictly OPTIONAL, off by default (cost, plus the well-known
AI jank the players themselves note, extra fingers and arms).

Three backends, all wanted:
- CLOUD: an OpenAI-compatible image API (Gabriel's figure in-thread: ~0.05 USD/image, high quality, slower).
  Mirrors the existing pluggable text-LLM config.
- LOCAL: ComfyUI / Stable Diffusion via a local endpoint (Cyb3rFuse + AbbaDF +1; free, private). Fits CR's
  existing OpenAI-compatible-local-endpoint pattern (Ollama / LM Studio are already supported for text).
- MANUAL (already works today): a player drops images into the mod folder (someguynamedjoe uses Perchance SD +
  the ChatPortraits folder). Keep this as the zero-cost baseline.

DISPLAY is already solved: `CrRuntimeImageWidget` + `RuntimePortraitFactory` render a runtime image from a disk
path, VRAM-flat. The NEW work is a pluggable IMAGE client (cloud + local), deciding WHEN/WHAT to illustrate (a
captive scene, a memory, a portrait), caching to disk so a beat is never regenerated, and MCM toggles (backend,
endpoint, key, on/off, per-context). Portraits already have a manual path; SCENE illustration is the real ask.

### Concrete target (Gabriel 2026-07-20): a redesigned CHAT window (mockup "Better Layout CHAT.png")
A two-column, gold-on-dark manuscript design: LEFT a large parchment narrative scroll under a branded
"Calradia Remembers" header + crest; RIGHT the live chat: a header (NPC portrait, name in gold serif, "House X
. Clan Tier N", a "Mode: Roleplay" pill, a ? help), then message CARDS (small portrait + coloured speaker name
[NPC gold / You blue] + text + a timestamp, a faction icon on some), then an input row (icon toolbar quill /
book / crown, a "Write your message..." field with a `0 / 1500` counter and a send arrow), then an ornate
"End Conversation" button.
- **Architectural win, not just polish:** the card layout puts the speaker NAME and the message TEXT in
  SEPARATE widgets, so each is coloured whole (name gold, text cream) WITHOUT ever colouring part of a line,
  which is the exact Gauntlet limit CR cannot cross (see `project_inline_colour_impossible` in Claude memory).
  Narration becomes its own card style (full-width, no portrait), matching the "[NARRATION] as a separate
  block" rule already in place.
- **Cheap / data already present:** speaker colours (existing), House + `Clan.Tier` (Hero.Clan), the length cap
  (existing), the mode pill (the Roleplay/Lean model mode). Only TWO portraits recur (NPC + You), reused across
  cards, so VRAM is a non-issue (unlike the assembly roster's many faces).
- **The real cost is ART** (ornate gold frame / lozenge-ornament sprites) and the slow prefab iteration loop,
  NOT code. Toolbar icons need a mapping to existing actions (quill = compose a letter, book = memory/dossier,
  crown = round-table?), to confirm.
- **The left panel = the player's OWN long-message expander** (clarified Gabriel 2026-07-20; the mockup shows
  the full text there with the small box left empty). The 1500-char cap makes a long message unreadable in the
  narrow input box, so a click (the quill icon) opens a roomy panel to see the whole text. NO new FPS risk: a
  read-only preview is a plain RichText (the O(n^2) bug was in EDITABLE selection-update, not display); an
  editable panel would be an EditableText, already covered by the existing Harmony prefix fix. DECIDED
  (Gabriel 2026-07-20): PREVIEW-ONLY, keeping the current behaviour (type in the box, the panel mirrors the
  full text). No two-way sync to build. (This is distinct from the Chronicle/Book-of-Memory redesign, which
  stays the first candidate for the shared-style pass above.)

**Scope ruling (Gabriel 2026-07-20):** this chat redesign is primarily a REORGANISATION + a different SKIN,
plus the chat upgrading to coloured message CARDS. NO new logic: the long-text panel stays preview-only as it
is today, the data all already exists. So the work is prefab/XML layout + a shared brush/frame style + art
assets, on the slow prefab-iteration loop, exactly the "focused polish sprint" this section calls for.

### Second reference mockup (Gabriel 2026-07-20): "Better Layout 2.png" - dossier-left variant
A warmer, house-coloured variant. Gabriel will PICK the final SKIN when he builds the prefab; both mockups are
references. DECIDED so far: the MESSAGE BLOCKS follow mockup 1 (distinct bordered cards per message: portrait +
coloured name + text + timestamp), NOT this mockup's flowier multi-paragraph body. What this mockup contributes
is its LEFT panel and header content, all mapping onto EXISTING CR data (no new logic):
- LEFT panel = the RP DOSSIER made permanent (house crest, Culture/Age/Role, **both regard ledgers shown
  side by side**: "Relation: 0" = vanilla clan relation vs "Personal regard: neutral (0)" = `ReputationWithPlayer`;
  echoed in the chat header "Clan 0 . Personal 0"), Background text, the real portrait, "View in Encyclopedia".
  This is CR's KEY two-ledger arch gotcha made visible and legible, a genuinely good idea to keep.
- Chat header "Present: Mina . Colambea" = witnesses + location; "Request Private" + lock as today.
- Footer buttons "Memories" (the chronicle / Book-of-Memory view) + "Encyclopedia".
- A single NPC card mixes grey stage-direction/narration paragraphs with cream dialogue: this works ONLY
  because they are SEPARATE blocks rendered as SEPARATE whole-coloured widgets, which is exactly what the
  `[DIALOGUE]`/`[NARRATION]` parser already produces. Inline stage-direction colouring inside a dialogue
  sentence stays impossible (`project_inline_colour_impossible`); the design sidesteps it by structure, so keep
  emitting narration as its own block (already the rule).
- **Left-panel divergence to reconcile:** mockup 1's left panel is the long-message compose PREVIEW; this
  one's is the permanent DOSSIER. Both keep the quill (expand) icon in the input row. Likely reconciliation:
  the left shows the dossier by default and SWAPS to the compose preview when the quill is clicked on a long
  message. Decide at prefab time.

### STATUS 2026-07-28: UI SKIN SYSTEM + Manuscript skin V1 (BUILT, UNTESTED in game)
Ratified (Gabriel 2026-07-27): keep the current window as the "Classic" SKIN and add the ornate two-page book
as a SELECTABLE second skin, aiming for 2-3 skins the player can choose. A skin is just another PREFAB bound to
the SAME `ChatViewModel` (a superset of properties), so a skin changes ONLY the look, never behaviour; the
prefab name is chosen at window-open (`ChatManager.SkinPrefab`, applies next conversation).
- **Infrastructure (committed `63307ec`):** `ChatManager.SkinPrefab` (default `ClassicSkin`), a Classic
  fallback if a skin prefab is missing, `cr.skin [classic|manuscript]`, and `ChatViewModel.ChatLeftPanelPath`/
  `ChatRightPanelPath` exposing Gabriel's page art (`GUI/Images/cr_chat_left.jpg` 709x1867 / `cr_chat_right.jpg`
  1194x1867, opaque JPG, falcon baked into the right one).
- **Manuscript prefab V1 (committed `b0fa2cd` + `3128018`):** `CalradiaRemembersChat_Manuscript.xml`. A
  FIXED-SIZE, centred two-page book (fixed, NOT height-stretch, because the page art has a fixed aspect: a
  stretch would distort the painted borders, so the high-res JPGs scale down cleanly; the world shows on the
  sides). Right page = the conversation (title/present/relation, the message list + scrollbar + input + send +
  every utility button, all COPIED VERBATIM from Classic so commands/bindings/relative scrollbar paths are
  identical and proven). Left page = the NPC dossier, binding the EXISTING `Panel*` props (name via
  `PortraitPanelTitle`, `PanelInfoFacts` = Culture/Age/Role/Relation/Personal regard, `PanelBackground`,
  portrait), refreshed to the current speaker per NPC line by new `ChatViewModel.RefreshManuscriptDossier`
  (extracted `PopulatePanelData` out of `OpenPortraitPanel`; gated to the manuscript skin, no-op in Classic).
  Added `ManuscriptPortraitWidth/Height` so the left portrait fits the narrower page.
- **Verified without a game:** builds 0 warnings; the prefab is well-formed XML; all widget types reused from
  Classic; 0 em-dashes. NOT yet loaded in game.
- **NEXT (needs Gabriel testing):** (1) does `cr.skin manuscript` render the two backgrounds at the right
  colour (CorrectRedBlue currently `false`) and size? (2) tune every control POSITION (estimated from the
  montage, not pixel-aligned) + the fixed `Book.Height` against high UI-Scale; (3) replace the utility buttons'
  Classic chrome with invisible hit-boxes over PAINTED button art (that art is not delivered yet); (4) the
  "Background" heading is currently rendered text, not baked art. Then: a player-facing SKIN PICKER (MCM
  dropdown or an in-chat button) once the skin is validated, and a 3rd skin.

## OPEN (wished 12/07 and 30/08; CrPrompt.StockSections exists for another modder's editor): an in-game NPC profile editor, the character card

_Heading before the 19/11/2026 audit: WISHED (player request, ProudSeal + AbbaDF 2026-07-12): an in-game NPC profile editor ("character card")_

The `character_overrides.json` format confuses some players (the "uncomment the example, but there is also an
empty `[]` below" trap: uncommenting the example while leaving the `[]` yields two arrays and invalid JSON).
Two players asked for a friendlier way to author an NPC's traits and backstory. Idea: an in-game Gauntlet popup
(a "character card") to view and edit an NPC's override, reusing the v1.29.0 editable-appearance popup pattern
(the player-only Encyclopedia editor). Reachable from the chat portrait dossier or the NPC's Encyclopedia page.

IMPORTANT constraint (Gabriel 2026-07-12): the JSON files MUST STAY, alongside the editor, never replaced by
it. They are CR's moddability and extension surface: a CR sub-mod author ships their own
`character_overrides.json` and publishes it, and that has to keep working. The editor writes to the same file
(or an author-owned one); it is a convenience layer ON TOP of the files, never a substitute. In the meantime,
v1.30.10 rewrote the shipped file so the "two arrays" trap is gone (the example now sits commented inside the
single `[]`).

DESIGN (Gabriel + Claude, 2026-07-13), TWO LAYERS, the save WINS:
- `character_overrides.json` = MODDER-owned, global, read-only from in game. A CR sub-mod ships it; every
  campaign sees it. This is the extension surface and it never moves.
- The NpcProfile IN THE SAVE = PLAYER-owned, per-campaign, edited from the Encyclopedia page (same custom
  Gauntlet popup as the v1.29.0 player-appearance editor, but on any hero's page). Where both speak, the SAVE
  wins: the player's own campaign is the last word, and a modder's default never silently overwrites what the
  player wrote in their game.
- An **"Export to JSON"** button turns the in-game editor into the AUTHORING TOOL for CR sub-mods: author a cast
  comfortably in game, export, ship the file. The two layers stop competing and start feeding each other.
- Gate the whole thing behind the existing `UseCustomTraits` MCM toggle (already the switch that decides whether
  authored data reaches the prompt at all).

STATUS: the DATA side landed first. `conviction` (2026-07-13) is authored today in the JSON: what an NPC holds
true, and therefore what they pursue, as opposed to `backstory` which only colours their voice. The editor gives
the player a face for these fields; it does not need new fields to be worth building.

## PARTLY BUILT (duels from chat 1.31.8 .. gold wager 2.8.17, grudge effect applied), OPEN: Inc 2 grudge duel verb, Inc 3 hostile attack, courtship-rival duel from chat: duels and combat from chat

_Heading before the 19/11/2026 audit: NEW PILLAR (player request, multiple, 2026-07-14): DUELS AND COMBAT FROM CHAT_

Players asked for two things: an NPC that decides to ATTACK the player from a conversation (opening a combat
encounter), and an NPC that offers or accepts a DUEL (the player and the NPC crossing blades before witnesses).
These are NOT the same feature and must not be conflated.

### The war-declaration line still holds
A lord AT PEACE who attacks the player commits an act of war, and war in CR is strategic, leader-only, NEVER a
conversation trigger (see the war-declaration constraint). So the two requests split cleanly:
- **Already at war / bandit / nemesis / outlaw**: hostile ATTACK is legitimate, and the machinery mostly exists
  (`NemesisBehavior` brackets MapEvents; `InterceptionBehavior` engages a party). This is a LATER increment.
- **At peace**: the honest channel is a DUEL, never an army. Steel between two people, not two realms.

### We already own ~90% of the plumbing (verified against the engine + our own ambush code, 2026-07-14)
The captive-ambush pillar already runs a REAL 3D fight and everything a duel needs sits around it:
- `MissionFightHandler.StartCustomFight(playerSide, opponentSide, dropWeapons, isItemUseDisabled, onFightEnd,
  minEndTime)` is the core, and there is also `StartFistFight` for an unarmed bout. `onFightEnd(isPlayerSideWon)`
  hands back the winner; `OnAgentRemoved` on the MainAgent already catches the player going down.
- Spawning a REAL hero (not a troop) into a scene is done today by `TownAmbushBehavior.CreateAlly` (a `Hero` with
  their own face and battle gear). A duel just puts that hero on the ENEMY side, alone.
- Opening the fight scene in a settlement: `PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(
  GetLocationWithId("center"))`; the driver is attached by a Harmony postfix on `Mission.AfterStart`
  (`AmbushMissionStartPatch` / `AttachBrawlIfPending`).
- Non-lethal is native: `Agent.SetMortalityState(Invulnerable)` is already used on fleeing bystanders, and
  `StartFistFight` / the mission mode give a knock-out bout.

### Ambush -> duel: what actually changes
One opponent (the real hero, not chief + cutthroats); CONSENTED (challenge + acceptance) instead of an ambush;
NON-LETHAL knock-out; witnesses STAY and watch (we simply do not call `SendBystandersFleeing`, the crowd IS the
audience); companions are spectators, not combatants; defeat costs honour / renown / regard / a real stake,
never captivity.

### Ratified decisions (Gabriel, 2026-07-14)
1. **Non-lethal by default.** The loser is knocked unconscious. A duel to the death would let a chat delete a
   named hero forever. A later MCM "duels to the death" toggle is possible, not assumed.
2. **Location follows the chat encounter, no arena required** (Gabriel's key insight, this is what frees the duel
   from a town's arena):
   - IN TOWN -> reuse the ambush town-centre path (low risk).
   - IN A KEEP -> stage it AT THE TOWN GATES (same settlement mechanism, different location).
   - IN THE WILDERNESS -> generate a matching FIELD scene. This WAS the ONE engine unknown (no `LocationComplex` /
     `center` outside a settlement, so the ambush launch path does not apply). **DE-RISKED and VALIDATED in game
     2026-07-15 via the `cr.duel_here` spike (mod `d3de0db`): the wilderness is VIABLE via CUSTOM placement, no
     battle-MapEvent machinery needed.** The proven recipe:
     1. Build the record with `SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, "", false,
        DecalAtlasGroup.Battle)` (fills AtmosphereOnCampaign / TerrainType / difficulty; building it by hand
        left no atmosphere and the scene hung on the loading screen forever).
     2. Scene from `GameSceneDataManager.Instance.SingleplayerBattleScenes` (first non-naval, e.g. `battle_terrain_a`).
     3. `MissionState.OpenNew(name, rec, initBehaviors, addDefaultMissionBehaviors:true, needsMemoryCleanup:true)`;
        VRAM returns clean between cycles (no render-target leak).
     4. Spawn on the scene's NAVIGABLE centre (bounding-box mid + `GetGroundHeightAtPosition`; `(0,0,0)` is the
        off-navmesh corner). Player agent `Controller = Player`.
     5. `Mission.SetMissionMode(MissionMode.Battle)` is what frees WALKING + weapon switching.
     6. Opponent engages with `AgentHumanAILogic` in the mission + the foe set `Agent.WatchState.Alarmed` (the
        arena duel's 1v1 pattern; NO formation, NO MapEvent). Everything runs on the battlefield navmesh.
     (Gotcha found in testing: the RGL console keeps keyboard focus after the command, so click into the scene
     before judging movement.)
3. **Real stakes from Inc 1.** Honour always moves (relation, renown, Fear/Respect, a grudge settled or
   hardened), and a concrete wager is on the table from the first increment: gold, or a prisoner's freedom. This
   is pure policy, so it lives entirely in Inc 0 and is tested before touching the engine.

### The bridge is law
The LLM emits `challenge_duel`; the mod re-validates everything (same place, a peer not a commoner, not a
prisoner, cooldown, traits: Valor/Honour accept, a fearful Calculating slips out of it, an affronted grudge-holder
demands it). A refusal must COST (renown / honour before witnesses) or no one ever fights and the feature is dead.

### Reason-driven consequences (ratified 2026-07-16): the duel is a convergence point, not a generic renown swap
A duel carries a REASON, and the consequence flows from it. TWO reasons only (both "soft": they move perception,
never the strategic board, so a chat-triggered duel can never be an exploit):
- **HonourAffront** (an insulted lord demands satisfaction): renown + the opponent's stance, and a driving grudge
  settled/hardened. Already ~80% covered by the existing `DuelOutcomePolicy`.
- **CourtshipRivalry** (a rival suitor for a hero the player courts): the NEW work. Outcome moves the courted
  hero's `RomanticProfile.AttractionToPlayer` (the stat ALREADY EXISTS, clamp [-100,100], deliberately distinct
  from ReputationWithPlayer and from stance). WIN -> AttractionToPlayer up (admiration) + player honour safe;
  LOSE -> AttractionToPlayer down (she doubts his worth) + honour safe (he fought); REFUSE -> reputation/renown
  damaged (cowardice before witnesses, `DuelStakesPolicy.ForfeitRenownCost`). The rival's fall in her eyes is
  NARRATIVE ONLY (a world-event she learns of; the LLM can reference it): CR has no hero-to-hero attraction
  stat, and the player's rise already tilts the triangle. She need NOT be present; she learns of the outcome.
- **VENDETTA / capture-the-loser: DROPPED (2026-07-16).** Real exploit risk (talk the Vlandian king into a duel
  and capture him, bypassing armies/sieges) and it violates the no-chat-strategic-exploit / bridge-is-law
  principle. Vengeance already resolves through nemesis/assassination and war.

Flow (CourtshipRivalry, Inc 1 = RIVAL-INITIATED only): the rival comes to the player; a chat opens where he says
he loves her, knows the player is also drawn to her, and wants it settled with steel (varied LLM intros for
context). Approach reuses existing plumbing: ON THE LAND MAP -> `InterceptionBehavior` (a lord rides out to meet
you) + its cadence/cooldown dial to avoid spam, duel in the WILDERNESS; IN A SETTLEMENT -> audience -> duel AT
THE TOWN GATES (the ambush settlement-scene path). NO sea duel: defer until the player is ashore.

- **Inc A (PURE Logic, test-first): `DuelReason` enum + a reason-consequence layer** on top of `DuelOutcomePolicy`
  (CourtshipRivalry: CourtedAttractionDelta win/lose + player renown/honour + refuse cost); a
  `CourtshipRivalryEligibility` (is there a real triangle: player courts L, R desires L, R bold enough); a
  location classifier (settlement -> gates / land -> wilderness / sea -> no duel). Read as the spec first.
- **Inc B (engine glue): the approach behavior** (specialise `InterceptionBehavior`), the chat intro (contextual
  prompt block + variants), the gates/wilderness launch, and applying the outcome to the live systems
  (`AttractionToPlayer`, renown, stance) + the world-event.

### Increments
- **Inc 0 (PURE, no engine, Opus, design):** `DuelChallengePolicy` (who may challenge / be challenged, when),
  `DuelAcceptancePolicy` (traits + stance + grudge -> accept / refuse / set terms), `DuelStakesPolicy` (honour +
  gold + prisoner freedom, and what a forfeit costs), `DuelOutcomePolicy` (winner/loser -> relation, renown,
  Fear/Respect, grudge settled or hardened, stake transferred). All unit-tested; Gabriel reads the tests as the
  spec before a line touches the engine.
- **Inc 1 (Sonnet, ambush pattern):** WILDERNESS SPIKE ✅ DONE + validated (mod `d3de0db`, recipe above). Remaining:
  the real stake changing hands (`DuelStakesPolicy` wager is still honour-only), witnesses STAY (do not flee).
  NOTE the spike is a throwaway diagnostic (`DuelSceneSpike` / `DuelSpikeMissionLogic` / `cr.duel_here`); the
  real duel mission is built from its proven recipe, and the spike files can be retired.
- **Inc 1b, THE CHAT WIRING ✅ BUILT 2026-07-22, UNTESTED IN GAME.** A duel can now be called for in a live
  conversation, which is what players asked for. The chain, end to end:
  - SDK `PromptBuilder.AppendDuelChallenge` teaches `challenge_duel` ONLY when `EncounterContext
    .DuelChallengeAvailable` is set (conditional teaching, the `CompanionAskingPrice` contract), with the venue
    phrase from `DuelVenueLabel`. It declares the duel non-lethal and immediate, and it STOPS THE MODEL AT THE
    CHALLENGE: the NPC may not say whether it accepts, because that is `DuelAcceptancePolicy`'s call, not the
    LLM's (the same split the captive escape uses). 6 prompt tests.
  - `DuelEligibilityResolver` (mod) is the ONE gate, called twice: at prompt time by
    `EncounterContextBuilder`, and again at COMMIT time by the chat handler, because a model can emit the
    challenge several turns after the context was built. Facts resolved from the live heroes
    (`PlayerPresenceResolver.IsBeside` for face to face, the `BuildStandingFacts` mirror for captivity,
    `npc.IsLord` for peerage, `NpcProfile.LastDuelDay` for the cooldown).
  - `DuelStagingSupport.CanStage` (pure, 3 tests): both real venues are stageable; `NotNow` (at sea) never can
    be, since that is no ground to fight on rather than missing code.
  - `ChatViewModel.ResolveDuelChallenge`: re-validates, reads the action's `challenger` field (a missing or
    garbled value fails SAFE to "the NPC issued it", since guessing "player" could make a lord balk at his own
    demand), and on a player-issued challenge runs `DuelAcceptancePolicy`. A refusal is a DEED: scene narration
    (so the NPC's next line knows it refused) plus `NpcForfeited` consequences, and the conversation carries on.
    On acceptance the chat locks to a single close button, exactly like the escape.
  - `DuelCoordinator`: two-phase deferral copied from `CaptiveEscapeCoordinator` (a mission cannot open over the
    chat screen). An NPC-issued challenge raises the accept/refuse inquiry on the map; a player-issued one goes
    straight to the field. `cr.duel_arm [name] [npc|player]` tests the whole deferral without the LLM.
  - `DuelOutcomeApplier` now stamps `LastDuelDay` on EVERY resolution including both forfeits: a refusal that
    reset nothing would let the player re-challenge each turn and farm `RenownForCalledBluff`. It also now feeds
    a real `AGrudgeDroveIt` (dominant resolvable live grudge against the player) instead of the hard-coded false.
- **Inc 1c, THE TOWN VENUE ✅ BUILT 2026-07-22, UNTESTED IN GAME.** Duels now work where conversations actually
  happen. No new scene was invented: `TownDuelBehavior` reuses the street ambush's proven town-centre recipe
  (arm, open the centre exactly as the "take a walk" menu does, inject on `LocationCharactersAreReadyToSpawnEvent`,
  attach the driver from the `Mission.AfterStart` Harmony postfix, which is the only seam where the agents exist).
  `TownDuelMissionLogic` is the town twin of `DuelMissionLogic`: it does NOT spawn the fighters (both are already
  in the scene), it starts the fight through `MissionFightHandler.StartCustomFight` (the arena's own pattern),
  and it keeps the same `IAgentStateDecider` answering Unconscious, so non-lethality is unchanged. Bystanders
  are deliberately NOT scattered (unlike the ambush): a duel is public, and the pillar says witnesses stay.
  Decisions worth remembering: the opponent spawns NEUTRAL, not Enemy, because a consented duel is not a crime
  and the town must not treat it as an assault; only a TOWN qualifies (a village or castle has no centre scene),
  narrowed in `DuelEligibilityResolver` rather than in the pure `DuelVenuePolicy`, since that is engine truth,
  not design; and the venue phrase says "in the streets" rather than naming a gate, because the fight really is
  staged in the centre and the NPC must not promise a place the game will not open. `cr.duel_town [name]` tests
  the scene staging in isolation; `cr.duel_arm` now resolves the venue from where the player stands.
  - **STILL OPEN.** (a) `DuelGrudgeEffect` is computed and LOGGED but NOT applied: `GrudgePolicy.Resolve` can
    settle a grudge, but the pillar has no API to HARDEN one, and applying only the settle half would read as
    "a duel can only ever help a grudge". Closing this = a harden API on `GrudgePolicy` plus its tests.
    (b) The COURTSHIP-RIVALRY duel is still console-only: the chat wires `HonourAffront` only, since
    nothing yet fills `EncounterContext.CourtshipRivalLadyName` (that is Inc B's approach behavior).
    (d) The inquiry path is unverified in game (known engine gotcha: an inquiry shown with `prioritize:true`
    has been seen swallowed; the call shape mirrors `ModCheats.ForceDuelChallenge`).
- **Inc 2:** the grudge duel as a first-class resolution verb ("let us settle this with steel"); richer stakes (a
  courted lady's hand, an abandoned suit); the post-duel conversation ("you spared me").
- **Inc 3 (SEPARATE, guarded):** the hostile ATTACK, restricted to already-at-war / bandit / nemesis, reusing the
  existing MapEvent brackets. NEVER a peace-time conversation opening a battle.

## PARTLY BUILT (Inc 1 in the SDK scene discipline), OPEN: Inc 2 a witness's question routes the next line, Inc 3 a liveliness dial: witness liveliness

_Heading before the 19/11/2026 audit: WITNESS LIVELINESS (player report 2026-07-15, ratified 2026-07-16): witnesses participate when concerned_

A player reported a sharp decline in witnesses addressing each other / interjecting "since the last two
versions". Diagnosis (verified in code, not the parser: ParseWitnessReactions reads the FULL raw text, so the
v1.30.14 machinery strip never eats witness blocks): the v1.30.12 "Scene discipline" tightening opens the
final format block, at maximum recency, with "You are the ONLY character speaking" plus a wall of NEVERs, and
it is emitted UNCONDITIONALLY, even when witnesses are present. The early WITNESSES PRESENT teaching encourages
proactive reactions, but the last thing the model reads before generating forbids them in spirit. Recency wins.

Gabriel's ratified direction: witnesses must be able to PARTICIPATE when the talk concerns them, and that makes
the world alive. They may react physically (present and attentive), but they may also SPEAK and ASK QUESTIONS.

- **Inc 1 (Fable, prompt-only, SDK): 🔨 built 2026-07-16.** SCENE DISCIPLINE becomes witness-aware: without
  witnesses it keeps the strict "only character speaking" wall (that bug fix stays); with witnesses present it
  says the NPC is the scene's ANCHOR but not alone, keeps the core prohibition (never compose another's words
  in YOUR blocks; a witness's words live in THEIR [WITNESS_REACTION]), and ENDS on an active invitation to let
  a concerned witness step in THIS turn (the invitation now sits at recency, where the restriction used to).
  The witness teaching drops "One sentence in their voice, no more": a CONCERNED witness may ask the player or
  the main NPC a pointed question, object, or press a point (2-3 sentences), while the main NPC stays the
  anchor (witnesses interject, they never take over).
- **Inc 2 (designed, not built): questions that get answered.** A field on WitnessReaction marking "this
  witness addresses the PLAYER (a question)" (parsed, pure/testable); the player's next message then routes to
  that witness automatically (15B addressing, but witness-initiated). A question aimed at the main NPC gets a
  real answer through the 15C loop.
- **Inc 3 (designed, not built): pacing dial + anti-hijack.** MCM "Witnesses" dial (Quiet / Balanced / Lively)
  tuning the 15C exchange cap + eagerness to step in; hard caps so the main NPC remains the conversation's
  anchor.

## PARTLY BUILT (inc 1 four verbs 1.20.5, inc 2 commitments 1.20.6; Custom folder 1.33.0; war as press_for_war 2.7.16), OPEN: inc 3 backing the player in a kingdom decision, inc 4 defection brokering, marriage-brokered clan-join: RP with teeth

_Heading before the 19/11/2026 audit: RP with teeth, giving conversations real consequences *(ratified 2026-07-02, a new pillar)*_

**Why this is a pillar.** CR's roleplay has always been rich in *voice* but thin in *stakes* outside the
handful of headline deeds (join_clan, marriage, prisoner bargains). A player can say almost anything to an
NPC and the world does not visibly answer back beyond `change_relation`. This pillar is the deliberate,
ongoing effort to close that gap: every increment adds conversation verbs that carry REAL vanilla
consequences, re-validated by the bridge ("the prompt is advice, the bridge is law"), never a hollow promise
the engine ignores.

**Increment 1, four quick-win verbs. 🔨 BUILT 2026-07-02 (untested in game), 676 mod tests / 190 SDK tests
green.** `give_influence` (a lord lends the player political influence, scaled by relation, capped, clan
must afford it), `lend_troops` (a party-leading NPC hands over healthy regular soldiers, never heroes, never
below 60% of their own strength, bounded by the player's party room), `sway_opinion` (the player's case
genuinely moves the NPC's own regard for a named THIRD PARTY, for or against, with a per-pair cooldown and an
ambient world-event rumour), and `release_prisoner` (the player intercedes for a THIRD-PARTY hero prisoner an
NPC's clan holds, distinct from the existing `free_prisoner` case where the NPC is the player's OWN captive).
Architecturally this increment introduced the **generic extension surface** every future verb rides on:
`EncounterContext.ExtraActionTeachings` (SDK), one host-composed, prompt-ready block rendered right after the
core GAME ACTIONS section (skipped in Lean mode), instead of a bespoke `EncounterContext` field and a
hardcoded `PromptBuilder` section per verb. The mod composes the block in `EncounterContextBuilder` from
`CourtActionResolver` (engine-coupled facts) + `CourtActionPolicy` (pure amounts/caps/cooldowns, tested).
Per-NPC (and per-NPC-and-target, for sway) cooldowns are derived by scanning the NPC's own `NpcProfile.Events`
for a hidden marker the action itself records, no new persisted field. Console: `cr.rp_verbs [name]`.
**Reciprocal court verbs (04/07/2026):** `give_troops` / `spend_influence`, the PLAYER-side mirror of increment
1's `lend_troops` / `give_influence`, letting the player reinforce a needy NPC's under-strength party or fund a
lord low on court influence, with a safety floor on the player's own reserve. They also CLOSE the Appreciations
pillar (the `TroopsOrInfluenceSpentForThem` source). Console: `cr.give_troops` / `cr.spend_influence`.

**Increment 2, the TrackedCommitment engine. 🔨 Phase 1 (the mechanical spine) BUILT 2026-07-03 (untested in
game), 694 mod tests / 190 SDK tests green.** The symmetric of `InformalQuest`: instead of the NPC setting the
PLAYER a task, the NPC OWES the player something. The flagship (and, for Phase 1, only) kind is `pledge_against`:
a lord, moved by genuine and standing enmity, vows in conversation to move against a named rival, a POLITICAL
commitment that launches a real, existing scheme (`SchemeStore.ForcePlot`, slander or sabotage, reusing an
already-active scheme between the pair rather than erroring) and adds the "he promised YOU" accountability
layer schemes lacked on their own. **Never declares or causes a war**, see the dedicated future item below.
New pure `CalradiaRemembers.Logic.TrackedCommitment` (PromisorId/TargetId/Kind/IssuedDay/DeadlineDay/State) +
`CommitmentPolicy` (deadline = one season / 21 days, fulfilled = +6 relation/stance, breached = -12 relation +
a Trust crater). Eligibility in `CourtActionResolver.ResolvePledgeRivalName` reuses the kin-safe,
already-disliked target selection from `sway_opinion` (`CourtActionPolicy.SelectPledgeTarget`, new pledge-only
animosity floor), taught in `EncounterContext.ExtraActionTeachings` alongside the increment-1 verbs. The new
`CalradiaRemembers.Commitments.CommitmentBehavior` (byte[] SyncData, mirrors `SchemeStore`) holds every
outstanding pledge and detects fulfillment TWO ways, `OnSchemeResolved` (called from `SchemeStore.OnStateChanged`'s
Succeeded case and from `PlayerAssists`' success branch) when the launched scheme succeeds, or `OnMapEventEnded`
recognizing (never causing) any real battle with the promisor's party and the target/their faction on opposite
sides, and detects breach on a daily overdue sweep, retiring the pledge either way with a `StanceService`
kept/broken-promise call, a `NotableEvent`, and a `WorldEventStore` rumour ("X honored/broke their pledge to move
against Y"). One pledge in flight per lord. **Phase 2 ✅ BUILT (untested in game): the quest-journal mirror
(`CommitmentQuest`/`CommitmentJournalBridge`, a read-only `QuestBase` mirroring `CalradiaRemembersQuest`, opened
on Create, closed success/fail on fulfil/breach, `ReconcileOnSessionLaunch` anti-stuck pass), console
diagnostics (`cr.commitments`, `cr.pledge`, `cr.pledge_fulfill`, `cr.pledge_breach`), and the
`commitment_fulfilled`/`commitment_breached` self-tests in `cr.test all`. The mirror persists only
`_promisorId`/`_commitmentId` as additive SaveableFields on a new QuestBase subtype; an old save with no mirror
degrades gracefully (no journal entry for a pledge already in flight).**

**Increment 3, backing the player in a KINGDOM DECISION** (a vote, a policy, a succession). **Needs a spike
first**: Bannerlord's kingdom-decision API must be scoped before this increment can be estimated, the mechanics
may or may not be reachable without heavy engine coupling. Do not start building before that spike lands. (The
former coalition-package pairing with `pledge_against` is superseded: that verb shipped as increment 2's
flagship, scoped to a political scheme, never a war, see the war-declaration item below for why kingdom
decisions and war are being kept structurally apart from the pledge system.)

**Increment 4, headliners.** The two biggest, riskiest, most narratively powerful verbs, deliberately last:
**defection brokering** (the player persuades an NPC to broker ANOTHER lord's defection, not just their own:
a step beyond the existing `join_clan`) and **NPC matchmaking** (the player arranges a marriage between two
OTHER NPCs, reading and writing the same marriage-prospect machinery `MarriageProspectResolver` already
provides). Both need real design passes; neither is scoped yet.

**WISHED (player flashdragonwolf thread, Nexus 2026-07-29): marriage-brokered clan-join.** "Talk a lord into
joining my clan by marrying one of my ladies." A COMPOUND deed combining two shipped systems: `join_clan`
(recruit a lord into the player's clan) + `arrange_marriage` / `grant_blessing` (the match), where the marriage
to a lady of the player's clan is the RP lever for the defection. Grounded and in-character, exactly CR's lane
(same thread asked for AI-Influence-style diplomacy depth; this is CR's grounded answer, reinforcing the
political/defection pillar above). **Engine caveat to spike first:** vanilla `MarriageAction` decides which clan
the couple ends up in, and a lord who LEADS his own clan pulls the bride into HIS clan, not himself into the
player's. So it works cleanly for a NON-leader lord (a younger son, a minor family member, a landless lord), or
the "join your clan" step must be made explicit alongside the marriage so he actually lands under the player's
banner. Scope the `MarriageAction` API + clan-assignment rules before estimating.

**BUILT (player request, Nexus 2026-07-30): `ModuleData/Custom/` override folder.** Edited prompts and custom
styles were overwritten on every mod update (re-extraction). A `Custom/` folder the mod never ships into now
survives updates (the CK3 VOTC pattern); the loaders read `Custom/<file>` first, else the shipped file
(`NarrativeLoader.ReadFile` for the prompt .txts; `StyleCatalog.PathOf` + `MergeStyleNames` for styles).
A pure openness win. Committed local (`13bc5d0`), not yet in a numbered release. Could extend to the editable
JSON (`cultural_traditions`, `character_overrides`, `jealousy_cultures`) later.

**Future, war declaration as a leader-only strategic decision (Gabriel, 2026-07-02/03, not yet designed).**
*"War declaration = a leader-only strategic decision, NEVER a conversation or pledge trigger. A dedicated future
system where the FACTION LEADER evaluates the clan's world situation, interests, relative strength, open fronts,
economy, allies, the target's vulnerability, and their own risk appetite, and declares war ONLY when it advantages
them; a leader who LOSES a war loses credibility, so that reputational downside is part of the calculus. Needs the
kingdom-decision / diplomacy API spike. The increment-2 pledge is POLITICAL (a scheme) only and must never declare
war."* Extended (2026-07-03): a lord who is NOT the faction leader CANNOT declare war, but CAN try to INFLUENCE
their leader on the question, so the war-decision system is a **council-pressure model**: vassals lobby, the
leader decides; accumulated vassal pressure (weighted by influence) is one input to the leader's advantage/
credibility evaluation. Natural future verb: a vassal pledges to "press my leader for war against X" (adds
pressure to the leader's evaluation) rather than declaring war themselves, the bridge from the increment-2 pledge
system to this future war-decision system.

**DONE (guardrail, 2026-07-30, SDK `71538cf`):** a lord may not conclude peace/a truce or arrange a binding
faction parley in conversation (only the ruler decides). Extended STAY WITHIN WHAT YOU KNOW after a player
report where a lord roleplayed a peace parley the engine then contradicted with a battle. Keeps the RP honest
about the limits of a lord's authority without killing emergent diplomacy talk.

## OPEN (wished 30/07): inter-faction relation scores that DRIVE war and peace, and a diplomacy screen

_Heading before the 19/11/2026 audit: WISHED (Gabriel 2026-07-30): inter-faction relation scores that DRIVE war and peace_
The concrete, grounded answer to "kingdom/war impact is lacking" (player feedback flashdragonwolf/AbbaDF) and to
the peace-parley RP gap above. A PERSISTENT table of faction-to-faction relation scores (e.g. Sturgia toward
Vlandia -10, toward Khuzait +10) that CR reads to actually DECLARE war or broker peace (`DeclareWarAction` /
`MakePeaceAction`), so faction diplomacy becomes a real thing the player's roleplay can move, not just talk.
- **Overhaul-mod safe by DISCOVERY:** do NOT hardcode the vanilla factions; BUILD the table as the campaign
  encounters each faction (via the world-event store), so it works when an overhaul replaces the kingdoms.
- **Vanilla stays the truth:** if VANILLA declares a war on a pair, reset that pair's score to 0 (CR layers on
  top, never fights the engine's own diplomacy).
- **Thresholds:** past a NEGATIVE threshold CR declares war between the two; past a POSITIVE one, peace can be
  declared through CR. Makes a slice of diplomacy RP concrete.
- **A consultable RELATIONS SCREEN (Gabriel 2026-07-30):** a player-facing overview of inter-faction relations,
  like Civilization's diplomacy screen, so the table is legible (you SEE who hates whom and anticipate). The
  READ surface for the WRITE system above; only DISCOVERED factions show (discovery-driven, overhaul-safe by
  construction). Fits CR's bespoke-window direction (an enluminated "diplomacy ledger" in the shared UI style).
  Each relation can link back to the WORLD EVENTS that moved it (click -> the events behind the score, via the
  chronicle), turning a number matrix into narrative. Build the table first; the screen is the natural follow-on.
- **Design tensions to resolve (needs the kingdom-decision / diplomacy API spike first):** (a) coordinate with
  Bannerlord's OWN war/peace AI so the two do not oscillate (hysteresis + cooldowns, not a per-tick flip);
  (b) WHAT MOVES a score, world events (battles, betrayals, cross-faction marriages) plus the player as a
  PRESSURE source, which ties straight into the council-pressure war model above (the player lobbies, the score
  is the accumulator); (c) persistence + faction lifecycle (kingdoms are created/destroyed mid-campaign);
  (d) never silently flip a war the PLAYER is party to. Big new pillar, but the strongest grounded answer to the
  closed diplomacy mods.

**Settlement sale: EVALUATED AND REJECTED (Gabriel, 2026-07-02): a fief is a lord's sovereignty and reason to
exist, not merchandise; no amount of gold makes selling it in-character. Do not revisit.**

## SHIPPED (volets A/B 1.15-1.16, CrActions 2.4.0, the CR API since), OPEN long horizon: core + swappable modules: the extension surface

_Heading before the 19/11/2026 audit: Extension Surface, CR as the narrative-memory bus of a modlist *(wished 2026-06-25, a new pillar)*_

**Why this is the point, strategically** *(Gabriel, 2026-06-25):* this SDK is the purest expression of CR's
community pillar, and **the best possible answer to closed mods like AI Influence**. Where a black box says
"use my features or don't," CR says "build your own, and CR will give it a memory and a voice." Openness isn't
just an ethic here; it's the competitive moat. The mod that lets *everyone else* build on it wins the ecosystem
the obfuscated one can never reach.

**The strategic prize:** any mod that changes the world can *tell CR about it*, and CR turns that into NPC
awareness, chronicle, rumour, hearsay, in-conversation reference, with **no new plumbing downstream**.
CR stops needing to know about each mod in advance: other mods speak to it. This generalises the
"world-agnostic" goal of the scenario engine from *content packs* to *live interop*.

**The collaboration shape (ChatAI thread, 2026-06-25):** this is NOT about co-running CR with another LLM-chat
mod, two chat overlays on the same NPCs would conflict. The fit is a **complementary, non-chat companion mod**
(e.g. a "living realm" world-sim that makes lords quarrel/scheme/wager) that **feeds its outcomes into CR via
the SDK** and lets CR be the memory + voice. Their simulation, CR's remembering. That's how a collaborator
plugs INTO CR rather than beside it, without either mod dismantling itself.

The governing principle, and the thing that keeps it from becoming a coupling disaster, is that this is **not
"open the RAM"**. It is a narrow, one-way, **validated inbound contract**: outsiders *push facts in* through a
single door; they never read or mutate CR's internals. The day CR refactors `NpcProfile` or the store, third
parties don't break.

**Four disciplines (load-bearing, they protect the decoupled-architecture pillar):**
1. **Inbound, one-way.** Third parties *write* through the gateway; they don't read/mutate internals. (A public
   read API over memory is possible later, but writing is ~90% of the value.)
2. **A stable, versioned DTO at the boundary** = an anti-corruption layer. The public contract references
   neither CR's internal types nor raw Bannerlord types, so internals can change freely behind it. (This is the
   SDK's existing reason for being, here we add one stable façade on top.)
3. **Validation at the door.** Every external fact is normalised before it enters: magnitude clamped, **summary
   length bounded (the save-blob guard)**, hero/faction ids resolved-or-dropped, source tagged, lifetime
   defaulted. A careless modder can neither brick a save nor inject noise into prompts. **Plus dedup by event
   Id + a per-`SourceMod` rate/throttle** so a mod that calls `Record` in a loop (or re-emits the same event
   every tick) can't flood the store toward the save-blob limit, modelled on the ChatAI dev's
   `processed_world_event_ids` + per-category `last_generation_day` state (the one idea from his shared code
   that's a genuine upgrade; his `VerifiedFacts` concept was *considered and dropped*, CR's `Summary` already
   IS the authoritative fact injected as hearsay, so there's no recap-generation drift surface to pin).
4. **Source tag + relevance gating.** Each entry carries its `SourceMod` (debug / filter / mute a misbehaving
   source); CR's existing hearsay-locality logic decides *who* plausibly heard it, so NPCs don't recite
   everything.

**Volet A, external world-event ingestion (do this first; highest value, simplest). ✅ SHIPPED in v1.15.0 (Nexus 2026-06-27), validated in-game. + MUTABLE ENTRIES added 2026-06-27 (`Feed.Update` upsert-by-id, `Feed.Retract` remove-by-id) closing the Homesteads modder (Bhel)'s one-way-can't-change gap, a settlement that moves, an NPC renamed, a place destroyed.** A façade like
`CalradiaRemembers.Feed.Record(ExternalWorldEvent)` lets a mod push a fact into the **same** World-Event /
rumour store the Chronicle, influence loop and NPC-awareness already read. Once past the gateway, it follows
the exact path of CR's native events, a Pravend NPC who'd plausibly have heard mentions it on his own. The DTO
carries `Category`, `Summary`, `Day`, `Magnitude`, optional `HeroIds`/`FactionIds`, `Lifetime`
(`Rumor` ages out vs `Deed` kept), `SourceMod`. *Worked example:* a horse-racing mod logs winners and purses
as `Lifetime.Rumor`; CR's lords gossip about the grey mare of Lord Caladog with zero knowledge of CR's memory
system. Pure/testable in `CalradiaRemembers.Logic`: the validator/normaliser (clamp, bound, id-resolve,
default) and the DTO→`WorldEvent` mapping; only the public static façade + behavior registration is engine glue.

**Volet B, external mission registration. 🔨 BUILT 2026-06-27 (Opus, untested in game, 496 tests). Public API `CalradiaRemembers.MissionBoard.Register/Complete/Abandon` + DTO `ExternalMission` (plain strings, no Bannerlord types); reward route = A (giver-required, paid when the player REPORTS to the giver in conversation). Reuses the `InformalQuest` pipeline via new `QuestType.External` + `InformalQuest.ExternalId/ExternalSourceMod` (SDK, ordinal-safe), so it inherits journal mirror + framing + gated reward + deadline/abandon/self-heal for free. Pure `ExternalMissionRules` (bound text, clamp reward to CR caps, normalize deadline; 10 tests). Verification ownership: the external mod calls `Complete(id)` (the gate) → stamps `SatisfiedOnDay`/`Evidence` → giver acknowledges + pays on report. Self-test `mission_register` (register→dedup→complete) in cr.test all. Open: a giver-less (B-fallback) variant deferred; modder-facing docs + a v1.16 Nexus section pending.** A mod defines **a mission for the player** and registers
it into CR's verified-quest system, inheriting all of CR's guarantees: the **journal mirror**, NPC-facing
framing, reward gating, deadline/abandon lifecycle, and the **self-heal-on-load** that force-closes anything
dangling (no ghost quests from a third-party mod either). The hard part, and the line that preserves "the
bridge is law / no empty promises", is **verification ownership**: CR must NOT pay a reward on a foreign mod's
unverified say-so. So the contract splits responsibility: **the external mod owns the completion signal** (it
fires `mission.Complete()` / supplies a predicate CR polls, because only it knows when its own deed is truly
done), while **CR owns everything else**, issuing, the journal entry, the in-conversation framing, the gated
reward, and lifecycle hygiene. That lets a mod define an arbitrary mission (win a tournament series, smuggle a
crate, survive a siege) without CR knowing its internals, and without weakening CR's "rewards only for real
deeds" promise. Pure/testable: the registration record + lifecycle state machine; engine glue is the journal
mirror + the completion-callback wiring.

**Volet C (Actions): external mods register conversation VERBS.** External mods register conversation
VERBS (`IConversationVerb` internally / `ExternalVerbContract` at the SDK boundary), CR teaches + routes
them exactly like its own, validated at the door like `Feed.Record`. **Foundation (the VerbContract
abstraction) SPIKED 2026-07-03** on the four existing "RP with teeth" court verbs (`give_influence`,
`lend_troops`, `sway_opinion`, `release_prisoner`), no behavior change: each verb's eligibility (still
`CourtActionResolver`/`CourtActionPolicy`, reused not duplicated), teaching text, and execution now live
together in one `CalradiaRemembers.Verbs` class, registered on a shared `VerbRegistry`
(`EncounterContextBuilder` composes teaching from it, `BannerlordGameStateBridge.ExecuteAction` routes to
it by name) instead of being scattered across a resolver/builder/switch. SDK-side DTOs added
(`NpcMemoryService.Core.Extension`: `VerbFacts`, `ActionRequest`/`ActionResult`, `ExternalVerbContract`,
plain Funcs, no TaleWorlds dependency) plus a mod-side `ExternalVerbAdapter` proving an external contract
can sit in the SAME registry as CR's own verbs. **NOT yet built:** the public `Actions.Register(...)` entry
point and its `Feed.Record`-style validation pipeline (source id, quota, name-collision): that is the next
increment. The other ~20 conversation actions stay on the old switch-based path for now (incremental
migration later). **`end_marriage` (divorce, Phase 1) is now BUILT as the first verb DESIGNED for this
contract from the start** (2026-07-03, see Marriage below), the architecture validation the contract was
missing, since the four court verbs only proved a MIGRATION, not a from-scratch addition.

> NOT built (wished 2026-06-25). Sequence: A before B before C. All ride the SDK's existing engine-agnostic
> design. Linked to the scenario engine below (all are modder-extensible surfaces) and to the ChatAI-
> collaboration thread (an autonomous NPC↔NPC layer would write its outcomes back through Volet A so the
> "theater" leaves real marks the player later encounters, see also the four NPC↔NPC seams discussed but
> not yet scoped here).

**Long horizon, CR as core + swappable feature modules (dogfooding the surface)** *(wished 2026-06-25)*

The endgame: split CR into a **core** that owns the shared substrate (`NpcProfile`, the store, the bridge, the
prompt builder, the World-Event store, the SDK chat pipeline) plus a set of **official feature modules**
(`CR_Schemes`, `CR_Wedding`, …) that ride the *same public surface* a third party would use. The payoff is
dogfooding: if CR's own marriage is "just another consumer" of the API, then a third party's marriage mod is a
first-class citizen **by construction**, the strongest possible proof the extension surface is real, and a
direct expression of the community pillar.

**The hard truth that gates it, coupling is the identity, not an accident.** CR's features are NOT independent:
Wedding, Schemes, Jealousy, Pregnancy, Succession read/write the *same* `NpcProfile`, feed the *same* prompt,
share the *same* stores. That interplay (jealousy←pregnancy→succession→scheme) **is the product**. So a/b must
be distinguished, and the interplay protected at all costs (over-splitting into mods that no longer talk = the
worst failure):
- **(a) Internal modularity**, clean boundaries *within one shipped mod* (a `CR.Core` owning the substrate;
  features depend on it). Cheap, good regardless, and the **prerequisite** for everything below. CR already has
  the base (`CalradiaRemembers.Logic` + the SDK + per-feature folders).
- **(b) Deployment split**, separately-installable, swappable mods. The big move; do it **surgically**, only
  where a feature is genuinely substitutable and its support contract is bounded.

**Volet A + B are necessary but NOT sufficient for (b).** A detachable feature module needs a richer **Feature
Module SDK** beyond the event/mission feed:
1. **Contribute prompt sections**, registered, **ordered**, **token-budgeted** (the prompt has 16 ordered
   sections; N mods writing into it needs a priority registry or it's chaos).
2. **Contribute action vocabulary** + bridge handlers, through the conditional-teaching gate ("the bridge is law").
3. **Read/extend shared profile state through a contract** (not raw field access, that's the coupling we're
   trying to break).

**Sequence:** (a) internal modularity → Volet A + B → **pilot the split on the LEAST-entangled feature, not the
most.** → generalise only after the pilot holds.

**`CR_Schemes` is the pilot, and possibly the only feature that ever needs externalising.** It already has its
own store, its own daily tick, and already feeds rumours, the least entangled with the profile/prompt. Wedding
is the *worst* starting candidate (relations + family + consorts + prompt). Extracting Schemes as a separate mod
on the public surface becomes the **validation gate**: if it holds, the surface is proven; if it doesn't, we
learned cheaply. The working hypothesis (Gabriel, 2026-06-25): **most features should stay in core**, and the
deployment split is reserved for the few that genuinely earn it, Schemes may be the one.

**"Bring your own marriage" has two routes, prefer the light one.** (1) *Data route*, reshape marriage via
**scenario packs + config**, no code: CR is *already half-way* (the Rhagaea marriage is four rules in a data
pack), and the scripter/composer keeps growing its trigger+consequence vocabulary. For most modders, "your own
marriage" = authoring rules, not shipping an assembly. (2) *Assembly route*, replace the whole `CR_Wedding`
module; powerful but needs the full Feature Module SDK above. The data route tends to give ~80% of the value at
~20% of the cost; reserve the assembly route for cases that truly justify it.

## PARTLY BUILT (companion errands in the journal 1.20.4), OPEN: tasks the player gives lords: a personal ledger of quests the player gives out

_Heading before the 19/11/2026 audit: A personal ledger of quests the PLAYER gives out *(wished 2026-07-01, BUILT 2026-07-02, untested in game)*_

**BUILT 2026-07-02**: companion errands (gather news / spy / steal / barter / envoy) now mirror into the
vanilla quest journal, option (a) below: same pattern as `QuestJournalBridge`, applied to
`CompanionMissionBehavior`. `CompanionErrandQuest` (a read-only `QuestBase`, no dialogs/reward, saveable
fields = companion StringId + mission Id, type-definer id 2 under the existing base 300_000_000) is
opened on dispatch, its log line pushed on every phase change (Outbound/Lingering/Returning/AwaitingReunion),
and closed on homecoming (success) or recall (letter or immediate), all via the new
`CompanionErrandJournalBridge` (the sole place that opens/updates/closes it, mirroring
`QuestJournalBridge`'s contract exactly). Anti-stuck: `ReconcileOnSessionLaunch` closes any mirror whose
mission Id is no longer in `CompanionMissionBehavior`'s active list, wired next to
`QuestJournalBridge.ReconcileOnSessionLaunch` in `NpcMemoryCampaignBehavior.OnSessionLaunched`. The status-line
wording is pure logic in `CalradiaRemembers.Logic/CompanionErrandJournalText.cs` (14 Gherkin tests). The
LORDS extension below remains open/to-evaluate; the companion ledger stands on its own. **Not yet validated
in-game**, next test pass: dispatch a companion, open the quest journal, watch the entry update through the
phases, confirm it closes on return and on recall.

**The gap (original problem statement):** the mod tracks two opposite directions of tasking, but only surfaced
one. Quests a NPC gives the PLAYER get a real vanilla quest-journal mirror (via `QuestJournalBridge`). But
errands the PLAYER hands OUT, today the companion missions (gather news / spy / steal / barter / envoy, in
`CompanionMissionBehavior`), were recorded only in the save (the persisted `_active` list) and the console
(`cr.missions`). In-game the player's only trace was the send-off line, the companion physically away as a
party, and their report on return. Send two or three companions off and it was easy to forget who went where.
**The idea:** a player-facing ledger of the tasks the player has commissioned, so "I sent someone somewhere"
is legible.

**Open design questions (to settle before building):**
- **Surface.** Two candidates, both with existing precedent in the mod: (a) reuse the `QuestJournalBridge`
  pattern for a read-only vanilla quest-journal mirror per active errand, opened on dispatch, closed on
  return/recall (cheapest, familiar to players, but crowds the vanilla log with our own outbound tasks); or
  (b) a dedicated CR panel, like the illuminated Chronicle viewer or the letter window (cleaner separation, more
  UI work). Leaning (a) for a first pass, (b) if it feels cramped.
- **Naming/framing.** Keep it clearly DISTINCT from quests-given-to-the-player: this is the player's own
  order-book / errand ledger, not tasks owed to them.
- **Data already exists.** Companion-mission state is already persisted, so a first version is purely a
  presentation layer over `CompanionMissionBehavior._active` (+ closing on the return/recall paths). No new
  gameplay, low risk.
- **Extension to LORDS (to EVALUATE, not committed).** Gabriel floated also logging tasks given to certain
  lords. FIRST verify what "giving a lord a task" even means in CR today: the negotiation framework has the
  player REQUEST deeds of lords (deliver a prisoner, declare war, recruit into the clan), but those are
  one-shot conversation outcomes, not tracked ongoing errands. So the lord case may need a tracked-commitment
  concept to exist before it can be journaled. Evaluate whether it is a real, coherent surface or scope creep;
  the companion ledger stands on its own regardless.

## PARTLY BUILT (settlement patrol 2.7.13), OPEN: hunt bandits, attack a party, raid, recruit: companion aggression and patrol errands

_Heading before the 19/11/2026 audit: Companion aggression & patrol errands *(wished 2026-06-23, the last big companion piece)*_

The same journey skeleton, but the companion goes to FIGHT, not just gather: **patrol a sector** (hold a
region, engage hostiles that enter), **hunt bandits** (seek and destroy bandit parties in an area), **attack**
a named lord's party / **raid** a village or caravan. The defining difference from the gather errands: these
need a real FIGHTING FORCE, so dispatch gains a troop-sourcing step, the companion either **recruits** their
own (gold + time, Leadership-gated) or **takes troops the player assigns** from the main party at send-off (a
transfer step). The OUTCOME is REAL combat, never a roll: the errand party fights through the engine; victory
→ loot/prisoners come home, defeat → the companion is taken (the existing capture-resilience path). Reuses the
journey skeleton + capture; adds a target/sector resolver and the troop transfer. Risk scales with the force
given. Pairs with companion happiness (sending people to die unhappily costs you).

## OPEN (feasibility notes only): wardrobe by talking, image generation, Hotscenes compatibility

_Heading before the 19/11/2026 audit: Player-requested, wardrobe by talking, image generation, Hotscenes compat *(wished 2026-06-29, player feedback)*_

A player asked whether three things are realistic to even want. Feasibility read (most realistic first):

- **Wardrobe by conversation (undress / "leave them in their shift"), MOST realistic; do this one.** Bannerlord's
  Equipment API changes a hero's CIVILIAN outfit at runtime (clear/replace the Body/Cape/Head/Gloves/Leg slots). A
  new adult-gated action verb (e.g. `change_outfit` / `undress`) emitted by the LLM and executed by the bridge
  ("the bridge is law": re-validate, adult-gate, apply via `Equipment`) lets the player ask an NPC to strip or
  dress down, and it actually shows on the 3D model in conversation and in captive scenes. Fits CR's action model
  cleanly. Open questions: persistence (revert to the prior outfit after the scene?) and how far to undress
  (Bannerlord renders a default underwear body when the slots are cleared).

- **Image generation, feasible backend, the DISPLAY is the blocker.** Sending a scene/portrait prompt to an image
  endpoint (the player's own SD, or an API) is easy HTTP. Showing a runtime-generated image in the Gauntlet
  chat/scene window is the hard part, the same texture-load-from-disk spike already noted under "Custom hand-made
  portraits" and the parked "generated portraits" idea. Slow and costly per image, so strictly opt-in. Sequence it
  AFTER the portrait PNG→Gauntlet spike lands (it unlocks both). An optional add-on, not core.

- **Hotscenes compat, possible but the hardest; research-gated.** Hotscenes plays animated adult scenes between
  agents inside a MISSION; CR scenes are a chat overlay on the map, not a mission. A real bridge means opening a
  scene with the two heroes and triggering the right Hotscenes animation at the right beat, heavy, and entirely
  dependent on what hooks/API Hotscenes exposes (unknown; may be closed). Most honestly delivered as a SEPARATE
  optional bridge mod once Hotscenes' surface is understood. Do not promise it; research first.

## PARTLY BUILT (captor scene for any held hero), OPEN: companion selector, scenario bank: captive-scene content

_Heading before the 19/11/2026 audit: Captive-scene content_
- **Scenario bank**: a bank of authored scene setups, one-use-per-campaign via SyncData, eligibility by
  kinks/gender, prompt injection. (Originally imagined as Twine→JSON; Twine is dropped, see the scripted
  scenarios pillar, in favour of plain keyword/JSON data, reusing the same composer/authoring approach.)
  (When adding persisted campaign state, REMOVE `CaptiveEncounterService.ResetOnSessionLaunch`, SyncData(load)
  fires before OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).
- **Player-as-captor ("tyrant") scene** *(wished 2026-06-28, player feedback)*, today the explicit captive
  scenes frame the player as the PRISONER (`PlayerStatus == Captive` + Hardcore). Players using captivity-event
  mods hold NPCs prisoner and want the inverse: an explicit scene where the PLAYER is the captor of an NPC they
  have taken. The captor direction was noted "deferred, no aggressor scene exists yet" (Sprint 17). Reuse the
  scene director + intents (Interrogation/Domination/…) with the roles swapped; standing damage lands on the NPC,
  not the player. Now requested by more than one captivity-mod player.
  **BUILT 2026-06-28 (untested in game):** a player-LED scene (NOT the NPC-driven director), `EncounterContext.IsCaptorScene`
  + `AppendPlayerCaptorSceneRules` frame the NPC as the player's prisoner reacting per their nature (per-intent;
  Hardcore CNC layer; reads the player's `*…*` as the captor's deeds). Mod: `ChatViewModel.ActivateCaptorSceneMode`
  (normal conversation path = full memory/actions, flagged), `ChatManager.OpenForCaptorScene`, console `cr.captor_scene
  [intent] [name]`. Intents/variants validated via self-tests (cr.test chat_captor_*): proud vs craven (Trait-driven),
  torture (breaks under the lash), CNC, male-captor/female-prisoner, and a GROUP CNC (player + companions, collective
  branch in the prompt). **PARTLY PLAYABLE IN GAME:** a held NEMESIS opens the scene from his own prisoner dialogue
  (`NemesisPrisonerDialogFlow.OpenCaptorScene`, riding the live conversation the party-screen Talk already started).
  **STILL OPEN:** the same entry for ANY prisoner (a "talk to your prisoner" option beyond the nemesis case), AND a
  companion SELECTOR (like the captive-encounter witness picker) to bring 1-2 allies into a collective scene. Plus NPC
  standing effects (the inverse of public humiliation).

## PARTLY BUILT (v1 + plausibility gate 1.29.1), OPEN: the suspicious spouse, the competition loop, the rival duel: jealousy

_Heading before the 19/11/2026 audit: Jealousy, the competition loop and the duel *(v1 shipped; escalation deferred)*_

v1 fires a one-shot reaction (staked NPC → grudge + grievance memory + JealousThreat letter, witness-gated,
culture-flavoured). **Plausibility gate SHIPPED v1.29.1** (player report: a male player did a male lord a service
and the lord's wife sent threats): pure `JealousyTriggerPolicy`, a same-sex pair is never a rival courtship
(matches the opposite-sex romance/marriage model), a flirt stamped on a quest turn-in is mislabeled gratitude
(ignored), spoken flirtation needs a repeated pattern (>= 2 flirt/intimacy events); intimacy counts at once for a
plausible pair. Deferred layers:
- **WISHED (Gabriel 2026-07-09): the SUSPICIOUS SPOUSE, repeated favors read as courtship.** A player who
  repeatedly does services for an opposite-sex married lord could rouse the spouse's SUSPICION even with no flirt
  at all (the wife watching a young champion keep doing her husband favors). A deliberate separate trigger, NOT
  the flirt path: keyed on repeated Collaboration events / high regard with an opposite-sex married NPC, own
  thresholds, likely a milder register (cold words, a warning letter) before real grudges. Design carefully so
  ordinary questing does not spam suspicion (the v1.29.1 report was exactly a false positive on a quest turn-in).
- **Competition loop**: rival suitors who actively and repeatedly contend for the same NPC over time (not a
  single reaction), courtship as an ongoing contest the player can win or lose.
- **Duel & scheming**: a slighted rival may challenge the player or scheme against them, a real
  confrontation, not just a letter. Pairs with the scripted-scenarios engine (a jealousy threshold as a
  trigger) and the love-triangle tie-in noted under Camp life.

## PARTLY BUILT (divorce 1.22.1, prisoner may deny 1.30.0, geography 1.30.1), OPEN: true polygamy, marrying a notable, an OOC correction channel: marriage

_Heading before the 19/11/2026 audit: Marriage, true polygamy and divorce *(monogamous marriage shipped; divorce arc ✅ COMPLETE, Phase 1/2a/2b/escalation, v1.22.1; true polygamy still OPEN)*_

**WISHED (player 2026-07-08, note to build LATER): marry a NOTABLE, and on marriage they join your CLAN.** A
player leaving a conflicting mod asked whether CR could "marry notables then make them family or clan members".
Today it CANNOT: `MarriageProspectResolver` draws candidates only from `npc.Clan.Heroes` / the NPC's children, so
a notable (gang leader, merchant, artisan, village headman, `Clan == null`) is never a marriage prospect. Design
notes (Gabriel 2026-07-08): (a) a notable needs **NO family blessing** (there is no clan head to consult, so it is
a direct courtship of the notable themselves, gated only on regard + the notable's own consent, not the
family-approval currency lords require); (b) the value proposition DIFFERS by target and should be surfaced so the
choice matters, **marrying a LORD grants influence/alliance benefits (political allies, a house tie)** while
marrying a notable is a lesser, more personal match (settlement/economic ties, not political power). Build sketch:
a notable-candidate pool for the prospect resolver + prompt block (the notable NPC offers THEMSELVES, no blessing
path), and on marriage promote the notable into the player's clan as a Hero (mirror `AddCompanionAction` /
clan-join, detaching them from their settlement post). A real chantier (notables are not lords: settlement
detachment, clan status, no `OriginClan`), so a separate increment.

**WISHED (player greenarcher13, 2026-07-10, note to build LATER): an OOC (out-of-character) channel to correct the
AI mid-conversation.** The player suggested a way to tell the AI "OOC: you're sending letters that aren't
applicable, please remove all references to them and any relationship modifiers." Genuinely useful as an escape
hatch when the world state drifts, but non-trivial and abuse-prone: it would let the player edit memory/stance
directly through free text, so it needs (a) a distinct parse path (an `OOC:` prefix the LLM never treats as
in-character), (b) a bounded, auditable set of effects (forget a named event, clear a specific pending letter,
nudge a stance back), NOT arbitrary "give me +100 relation", and (c) a world-consistency guard so it corrects
drift without becoming a cheat console. Design before building. Note: the specific case that prompted this (a
married spouse still demanding child support) is fixed deterministically in v1.30.0, so the OOC channel is for the
GENERAL class of AI drift, not that bug. RELATED follow-up: an already-ACTIVE child-support `ProvideGold` quest
whose giver later became the player's spouse is not retroactively cancelled today (only the pending LETTER is
healed); a quest-level heal (close a `ProvideGold` quest when its giver is now `Hero.MainHero.Spouse`) would fully
close greenarcher13's loop for saves where the quest already opened.

**FIXED v1.30.0 (player suggestion, ApocalyptikGreen 2026-07-10): a prisoner cannot deny a private audience.** A
held captive now yields the room by force when the player asks to speak privately (button or free text), rather
than the LLM being allowed to refuse. `ChatViewModel.NpcIsPlayerPrisoner()` gates both the button short-circuit
and the free-text `request_privacy` resolution.

**SHIPPED v1.30.1 (player suggestion, SuperGraham339 2026-07-10): geography grounding from the engine, not a
lorebook.** Report: NPCs invent where places sit (a Vlandian border castle described as lying "toward Empire
territory" when the game's own encyclopedia says its pass leads to Battania). The game already holds authoritative,
always-current geography, so instead of the player's literal "SillyTavern-style JSON lorebook" (which would
DUPLICATE and DRIFT as mods rename settlements and ownership shifts mid-campaign), CR grounds from `Settlement`
data: `SettlementContextResolver` (mod, mirrors `ContextualNameResolver`) injects the NPC's current-settlement
`EncyclopediaText` plus, for any settlement named in the player message, its `EncyclopediaText` + culture/holder +
a bearing (`GeographyDescriber` pure, direction + coarse distance band from map positions). Pass-through string in
`EncounterContext.GeographyNote`, rendered by SDK `PromptBuilder` under "THE LAY OF THE LAND (real geography, treat
as fact)". `cr.geo` console prints it. FOLLOW-UPS (open): (a) ground the QUEST-TARGET settlement too (the reported
case was a lord describing a place tied to his own watch/quest, which the player-message scan does not catch until
the player names it; thread the quest/encounter's target settlement into the resolver); (b) OPTIONAL later flavor
layer, a small keyword-triggered CULTURE lore snippet (temperament/history the engine does NOT encode), which is
the one place a hand-authored lorebook is genuinely the right tool (geography never is).

**Divorce arc COMPLETE (shipped v1.22.1):** Phase 1 (persuade an NPC to divorce their NPC spouse), Phase 2a (the
player ends their OWN marriage in chat), Phase 2b (an unhappy spouse DEMANDS a divorce), and the escalation (a
spouse refused too long BEGINS the estrangement themselves, walking out at a deadline unless the player genuinely
repairs the bond, with a save-safe on-load heal). All chat-driven, no button. Still OPEN: TRUE POLYGAMY.

**Player-reported bugfix (2026-07-04, ✅ SHIPPED v1.22.1), 1116 mod / 242 SDK tests green.** Two live
v1.22.0 bugs: (1) the marriage scene never triggered after an NPC accepted, the eligibility gate checked ONLY the
mod's own personal-regard ledger (`ReputationWithPlayer >= 50`), ignoring vanilla clan relation players had actually
built; broadened both synced gate sites (`EncounterContextBuilder.ResolveLoveMatchEligible` + the bridge's
`ExecuteMarry`) to pass on EITHER, via one shared `MarriageEligibility.MarriageEligibilityThreshold` const, and
added an anti-empty-promise guard in `PromptBuilder` (marriage is family-gated, cannot be settled in one
conversation) for the non-eligible + still-unmarried case, dropped in Lean. (2) after `cr.marry` the NPC still
treated the player as a third-party spouse (infidelity framing, the ≥30 stranger-resistance trust gate) instead of
their own spouse, new additive `EncounterContext.NpcSpouseIsPlayer` renders a distinct marital-intimacy block
(natural, not infidelity, no stranger thresholds) when the NPC's `Hero.Spouse == Hero.MainHero`.

The marriage chain (family-approval, consorts, by-letter, jealousy) is shipped. One genuinely-open extension,
one BUILT:
- **Culture-gated true polygamy** as lore, multiple full spouses, restricted to designated cultures (not the
  current consort workaround), with the family-approval currency scaled accordingly.
- **Divorce, Phase 1 (NPC case), BUILT 2026-07-03 (Sonnet, untested in game, NOT committed), 755 tests green.**
  A player tried to convince an NPC to leave their spouse; the NPC "agreed" but nothing happened ("soon, soon"),
  the classic empty-promise pattern (no verb, no executor). Design framing (Gabriel, 2026-07-03): **a marriage IS
  an alliance, so a divorce is a loss of influence and power**, and BOTH families oppose it fiercely, never "talk
  once and it is done". Built: `end_marriage` starts a multi-day **estrangement process**
  (`DivorcePolicy.ProcessDurationDays` = 15) the spouse may resist, a daily roll (Honor/Calculating raise it) can
  Delay (push the end day back, capped) or Abort the process outright, rather than an instant flip; reaching the
  end day re-validates every guard and, on success, dissolves the marriage for real (`MarriageDissolutionService`)
  and records a **culture-scaled scandal** world-event (Nordic/Sturgian 0.5x, default 1.0x) plus relation hits (the
  ex-spouse toward the NPC and toward the player, and the ex-spouse's own clan toward the player). Gates: the
  marriage must be genuinely unhappy (relation ≤ -5) and the bond with the player deep (≥ 30); refuses while either
  party is pregnant or a prisoner, or while the spouse who would have to relocate clans heads their own clan, leads
  a party, or governs a settlement (Phase 1 REFUSES rather than reassigning those roles). **This is also the first
  verb DESIGNED for the new `IConversationVerb` contract** (`CalradiaRemembers.Verbs` / `VerbRegistry`, not merely
  migrated onto it like the four RP-with-teeth court verbs), validating that the contract makes a brand-new verb a
  clean "implement the interface, register it" addition with no switch statement or prompt-builder method to touch.
  **Technical crux, RESOLVED by an in-game reflection spike on 1.3.15** (the earlier note that `Hero.Spouse` "has no
  public setter" was WRONG): `Hero.Spouse`'s setter IS public and self-cleaning (nulls both sides, appends each to
  the other's `ExSpouses`); clan restore for the spouse who joined the other's clan mirrors `Hero.AfterLoad()`'s own
  fixup ladder (OriginClan, else smallest same-culture clan, else any, never clanless); `Romance`'s internal
  `SetRomanticState` is cleared via cached reflection, non-fatal on failure. No dependency on `DivorceOfCalradia`
  needed. **Phase 2b (the headliner: the unhappy spouse DEMANDS), BUILT 2026-07-04 (Sonnet, untested in game,
  NOT committed), 1090 mod / 230 SDK tests green.** Answers Gabriel's 2026-07-03 question ("est-ce qu'un NPC
  malheureux peut demander le divorce au joueur?"): the player's OWN spouse, if deeply unhappy, raises the demand
  entirely through conversation, no UI button. Pure gate `SpouseDivorceDemandPolicy.ShouldDemand` (new
  `CalradiaRemembers.Logic`): fires when the relation reads flatly miserable (≤ -20), OR a fresh wound just struck
  (a recent jealousy grudge, or a discovered bastard-scandal world-event) even at a merely moderate relation, OR a
  live grudge against the player already clears a magnitude bar (40), never while either party is pregnant or a
  prisoner, never while a demand is already pending. `SpouseDivorceDemandBehavior` (weekly tick) evaluates the
  player's current spouse, sets a persisted flag (additive byte[] SyncData, spouseId → day the demand began) and
  queues an unbidden `LetterReason.SpouseDivorceDemand` letter in the spouse's voice ("I can no longer bear this
  marriage"). In conversation, a new `EncounterContext.SpouseDivorceDemandNote` (host-composed, mirroring
  `GrudgeNote`'s field pattern rather than routing through `ExtraActionTeachings`, since it names one NPC's single
  demand, not a set of independently-eligible verbs) teaches `accept_divorce` / `decline_divorce`. Accept dissolves
  the marriage via a new `MarriageDissolutionService.TryDissolvePlayerMarriage` (a thin wrapper over the existing
  `TryDissolve`, "the player never relocates" falls out of the pre-existing clan-leader invariant for free, no new
  guard code needed) with SOFT mutual-consent consequences (a modest relation settling, no scandal-magnitude grudge
  storm, this was the spouse's own wish). REFUSAL is registered two ways: an optional `decline_divorce` action for
  a firm in-character refusal (fires immediately), and the PRIMARY path, a grace-period timeout (12 days) swept
  weekly, a demand nobody ever explicitly answers is read as a refusal too, so the mechanic never depends on the
  LLM choosing to emit a negative action. Either way: a `GrudgeSource.Divorce` grudge plants on the spouse, and the
  flag clears so the demand can resurface fresh later. Console: `cr.spouse_divorce_demand` / `_accept` / `_decline`
  / `_info` / `_tick`; mechanical `spouse_divorce` self-test (marries the player to a test spouse when unmarried,
  exercises accept then a second decline scenario). **Phase 2a (the player PROACTIVELY ends their own marriage):
  BUILT 2026-07-04 (Sonnet, untested in game, NOT committed), mod 1096 / SDK 234 green.** Also chat-driven, no
  button: talking to their own spouse, the player states plainly they want to end it, and a new
  `EncounterContext.PlayerEndOwnMarriageNote` (taught only when `hero == Hero.MainHero.Spouse`, coexists cleanly
  with a simultaneous Phase-2b demand) teaches `[ACTION] end_own_marriage`. The bridge decides MUTUAL vs REPUDIATED
  via a pure `PlayerDivorceConsentPolicy.SpouseConsents` (mutual iff the spouse had a pending 2b demand OR their
  relation is at/below `DivorcePolicy.UnhappyThreshold` = -5, Phase 1's exact "unhappy" bar, asked from the other
  side), dissolves IMMEDIATELY via `TryDissolvePlayerMarriage`, and applies consequences through a REFACTORED shared
  `DivorceProcessBehavior.ApplyDivorceConsequences(divorcer, spouse, mutualConsent, ...)`, ONE implementation now
  serves both the Phase-1 NPC-NPC path and this player path (no duplicated family/friends resolution). Repudiated ->
  the spouse + their immediate adult family + their 3 best friends take a `GrudgeSource.Divorce` grudge against the
  PLAYER, spouse relation SET via the direct `ExSpouseNpcHit` (the player is the divorcer, not a third-party
  orchestrator, so no separate `ExSpousePlayerHit`); mutual -> soft. Records a Scandal (repudiated) or Other (mutual)
  world event + chronicle line, clears any pending 2b flag. Console `cr.end_own_marriage`; mechanical `player_divorce`
  self-test (same best-effort seeding-skip as `spouse_divorce`). Judgment call flagged: `ActionOutcome.IsPositive` is
  true for both tiers (the deed succeeded), so a repudiation still cues the positive sound/colour, revisit if the UI
  should mark the wronging as negative. **Escalation (the refused spouse begins ending it themselves), BUILT
  2026-07-04 (Sonnet, untested in game, NOT committed), mod 1107 / SDK 238 green.** Answers the follow-up Gabriel
  flagged: a spouse refused enough times, still miserable, stops merely demanding and BEGINS an estrangement DESPITE
  the player, on a deadline, rather than waiting forever on their consent. Owner-ratified shape lands SIMPLER than the
  originally-sketched "same resistance process as Phase 1 with the player resisting": no daily Honor/Calculating rolls
  here, just two pure gates in `SpouseDivorceDemandPolicy`, `ShouldEscalate` (refusal count ≥ `RefusalsBeforeEscalation`
  = 2 AND relation still ≤ `DivorcePolicy.UnhappyThreshold`, never while pregnant/prisoner/already-escalating) and
  `IsReconciled` (relation ≥ `ReconciliationThreshold` = 10, deliberate HYSTERESIS above the misery bar, so a genuine
  warming calls it off, not a nudge that merely clears the misery reading again). `SpouseDivorceDemandBehavior` now
  owns a running per-spouse refusal COUNT alongside the demand flag (both additive byte[] SyncData, a shared
  `SyncDictionary` helper serializes all three spouseId→int dictionaries under their own save keys) and an escalation
  record (spouseId → deadline day, `today + DivorcePolicy.ProcessDurationDays`); `RegisterRefusal` increments the
  count and, when `ShouldEscalate` clears, calls `BeginEstrangement` instead of leaving the demand to resurface:
  clears the demand flag, sets the deadline, queues a `LetterReason.SpouseDivorceEscalation` letter ("I have waited
  long enough"). The weekly tick gained `SweepEscalations`: reconciled (checked BEFORE the deadline, so it always
  wins even on the deadline's own day) calls it off via `ResolveReconciliation` (clears the record + refusal count,
  a relief profile note); deadline passed walks out for real via `ResolveWalkOut`, reusing the SAME
  `MarriageDissolutionService.TryDissolvePlayerMarriage` every other divorce path shares (its own live
  re-validation IS the anti-double-dissolve guard, an already-dissolved marriage just fails here and the record is
  left for a retry next week). Walk-out consequences are deliberately MODERATE (not the tiered family/friends
  storm): ONE `GrudgeSource.Divorce` grudge on the spouse, a Scandal world event + chronicle line framed as the
  SPOUSE's own departure ("weary of waiting... ended their marriage... and departed"), a Betrayal profile note.
  `ExecuteAcceptDivorce`/`ExecuteEndOwnMarriage` (the bridge) now also recognize an active estrangement (not just a
  pending demand) as consent and call `CancelEstrangement` on success. New SDK `EncounterContext.SpouseEstrangementNote`
  (host-composed, mirrors `SpouseDivorceDemandNote`'s field pattern, rendered right after
  `PlayerEndOwnMarriageNote`, dropped in Lean) teaches ONLY `accept_divorce` (no `decline_divorce`: an estrangement
  under way is not something to refuse in words) plus reconciliation-through-deeds framing. Console
  `cr.spouse_divorce_escalate` (+ `_info` now reports ESTRANGING/deadline); mechanical `spouse_estrangement`
  self-test (force-escalate → force the deadline to today → tick → assert dissolved + grudge; second test spouse →
  force-escalate → raise relation above the reconciliation threshold → tick → assert marriage SURVIVES). NOTE:
  Phase 1 explicitly REFUSES the case where the NPC's spouse IS the player (guard + test added 2026-07-03, because
  the romance waiver would otherwise have opened the player's own marriage to end_marriage by accident, a path never
  designed for it), Phase 2b is the dedicated feature for that case. Still open: letters/journal-mirror surfacing
  the estrangement beyond the initial letter (today's `[SPOUSE_DIVORCE]` log line and the announcing letter are the
  only visible cues, no reminder as the deadline nears); a `chat_spouse_divorce`/`chat_spouse_estrangement` LLM
  self-test (today's are mechanical only, see `cr.test all`); Khuzait cultural nuance research.

## PARTLY DONE (one Give control, wanderer for hire, navmesh ambush, timeout setting, fleeing bystanders, log rotation by session), the rest OPEN: smaller polish

_Heading before the 19/11/2026 audit: Smaller polish (deferred)_
- **Chat composer, a true MULTI-LINE editing box** *(wished 2026-07-01; the "best" option, deferred)*: a player
  asked for a bigger input for long messages. SHIPPED 2026-07-01 was **Option 2**, a "<" button beside the small
  input opens a left panel showing a **wrapped, read-only REVIEW** of the message (a `RichTextWidget`, which wraps,
  in a `ComposeScrollablePanel` so a long message scrolls), updated live; editing stays in the small box on the
  right. **Definitively established that a true multi-line EDITOR is NOT achievable with native widgets:**
  Bannerlord's `EditableTextWidget` is single-line, no multiline/wrap property on the widget or its `Brush`
  (confirmed by reflection), it does not auto-wrap, and Enter does not insert a newline (tested in game). The
  **Editable Encyclopedia** mod (decompiled, un-obfuscated) does NOT have an editable multi-line box either: it
  fakes multi-line for READ-ONLY display via a `WordWrap(text, charsPerLine)` helper (inserts `\n`) rendered by a
  `TextWidget`, and its editor is a single-line enlarged inquiry. So Option 1 needs a **custom widget from
  scratch** (wrap layout + a caret that navigates wrapped lines + click-to-position + selection + scroll + newline
  input), a sizeable, fragile UI build, hard to iterate (no automated UI tests). Deferred. A cheaper middle
  ground if ever wanted: a **Send-time `WordWrap`** that inserts soft `\n` into the single-line text so the sent
  message (and any later re-open) reads multi-line, without a real editor.
- **One "give" control for items AND gold** *(wished 2026-06-28, BUILT 2026-06-28, validated in game)*: the
  gift-icon button now opens a single "Give" picker with a "Gold" entry pinned atop the inventory items;
  picking Gold prompts for a denar amount. The separate Give Gold button is retired (`IsOfferGoldVisible`
  forced false). `ItemPickerService.ShowItemPicker(onItemSelected, onGoldSelected)`.
- **Grounded "where do I find a companion?"** *(wished + BUILT 2026-06-28, validated in game)*: an NPC asked
  where to recruit was inventing a fictitious wanderer (the "Oros" bug). Now, gated by the pure
  `RecruitIntentDetector`, the host injects ONE REAL hireable wanderer and their town, drawn at random
  (`EncounterContext.KnownWanderers`, `PromptBuilder` A WANDERER FOR HIRE section): a single name is more
  credible than one person knowing every wanderer's comings and goings. Eligible = wanderers in the NPC's own
  town (certain, "in the tavern", preferred) or of the NPC's culture (heard of); the prompt forbids inventing a
  name OR claiming to know of others, and falls back to "taverns of towns" when none are known. Only injected
  when the player actually asks (no token cost otherwise). Console self-test `cr.test chat_recruit_companion`
  (in `cr.test all`): deterministically asserts the gate fired + a wanderer was gathered, then logs the reply to
  judge the grounding. Pure `RecruitIntentDetector` has 5 unit tests.
- **Ambush bandit placement on the navmesh** *(wished + BUILT 2026-06-28, untested in game)*: the old
  `AmbushFightMissionLogic` ring was raw geometry (cos/sin at the player's Z), so thugs could land between walls
  with no path to the player. Now each thug is placed on a point that is BOTH on the navmesh AND reachable: from
  the player's valid `WorldPosition`, `SetVec2` slides the candidate onto the navmesh, then
  `Scene.GetPathDistanceBetweenPositions` confirms a real path (rejecting blocked / disconnected spots and ones
  reachable only by a long detour). A fan of angle offsets + a few radii (ideal ~20m) dodges a wall; a
  toward-the-player navmesh fallback covers the rare total miss. `cr.ambush` to test in game.
- ~~**Fleeing townsfolk during the town ambush**~~ *(wished 2026-06-28)*, **RESHAPED + DONE 2026-06-30 (untested
  in game).** The brawl plays at the GATE, outside the walls (the player spawns there; snapping to the walkable
  centroid dragged them past the gatehouse), so there is no townsfolk crowd to scatter, only the gate guard or
  two. Rather than SPAWN civilians, `AmbushFightMissionLogic.SendBystandersFleeing` makes the guards/civilians
  ALREADY present flee: at fight-start, every human agent that is not a combatant (not the player, an ambusher, or
  an ally) is set `Agent.MortalityState.Invulnerable` (a slain guard = a crime / odd mission end), sent running
  away from the player via `SetScriptedPosition` onto a navmesh point + `SetWatchState(Alarmed)`, then `FadeOut`
  ~2.5s later (`TickBystanderFlee`). No spawn, no team plumbing, no win/lose interference. `cr.ambush` to see it.
- **Configurable LLM response timeout** *(player bug 2026-06-28, BUILT)*: a player on longer messages hit
  "LLM Call Failed in 100008ms: A Task was cancelled", the HttpClient default 100s timeout firing when a long
  prompt makes a slow/reasoning model think past it. Now the per-request timeout is enforced in the SDK via a
  linked `CancellationTokenSource` (so EVERY caller is bounded), independent of the HttpClient ceiling (raised
  to 15 min so it never pre-empts). New MCM setting "LLM Response Timeout (s)" (15-600, default 120,
  `ModSettings.LlmResponseTimeoutSeconds` → `OpenRouterConfig.TimeoutSecondsProvider`). The opaque "A task was
  canceled" is replaced by a clear, actionable message naming the timeout and the setting. Only a genuine caller
  cancellation (chat closed mid-call) still propagates.
- **Negotiation Phase 1 polish**: a richer `DeliverItems` spec (category + tier + count, "a warhorse, a
  sword of tier 5+") instead of a value floor; the joining wanderer EQUIPS the gear demanded for them.
- **Companion happiness**: pay / share-of-loot driver; let the player ADDRESS a grievance more richly.
- **Camp life**: a child of a companion couple joining the clan as a chronicle/letter beat; the love-triangle
  tie-in with jealousy.
- **Influence loop**: family-across-clans kin detection (today it is clan-mate only); a richer per-event
  dynastic-succession LLM rewrite (the current succession is deterministic).
- **Dynastic succession**: the optional per-NPC LLM reframing for richer dynastic prose.
- **Log file rotation** *(wished 2026-06-24)*: `CalradiaRemembers.log` accumulates across sessions and can
  grow very large. The fix: truncate (or rename to `.bak`) the file **on session launch** so each play
  session starts fresh. **The Story Log (`CalradiaRemembers_story.log`) must NOT be touched**, it is an
  ongoing novel that grows deliberately across all sessions and belongs to the player.

---

## PARTLY SUPERSEDED (press_for_war 2.7.16, lords play their realm's strategy 2.8.0), the two rules stand: player influence on the world (CK3-style)

_Heading before the 19/11/2026 audit: v2.0, Player influence on the world (CK3-style)_

Persuading a faction leader toward war, luring an enemy lord to defect, assassination, espionage. The most
powerful layer, and the most dangerous for save balance, so it ships last, on a proven foundation. Two
hard rules:

1. **No direct world action from a conversation.** Same philosophy as quests (verified deeds, never words):
   a successful conversation plants a *disposition*, not a war. The lord "brings it to his council"; days
   later the event lands (or doesn't), weighted by personality, relations, and kingdom state. An eloquent
   player must never trigger a world war in three messages.
2. **Build on native mechanics.** `ChangeKingdomAction` (defection), `DeclareWarAction`, `KillCharacterAction`
   all exist engine-side. The mod adds the decision layer and the narrative, not the mechanics.

NPC-initiated faction wars and assassination-as-a-deed both wait for this layer (where the player could
otherwise extract outsized advantage at no cost).

---

## OPEN, design only (Custom/ per file 1.33.0 and the Prompt Profile give partial relief): fragmented, mergeable prompt files

_Heading before the 19/11/2026 audit: Fragmented, mergeable prompt files (fixes stop being shadowed by Custom edits)_

Player report (2026-08-19): the `ModuleData/Custom` folder overrides a whole file, so a player who copies
`behavior_guidelines.txt` into Custom to change one line keeps their old copy and MISSES every later fix we
ship in that file (the decency baseline, grounding rules, etc.). "Saves carry over untouched" is only about
save data, not these editable files. This is a real architectural weakness: the most-customized file is also
the most-patched, and the two collide.

The fix (deferred, not urgent): move from one monolithic file to **additive fragments**, so our fixes and the
player's edits coexist without shadowing.

- **`.txt` prompt files** (`behavior_guidelines*`, `world.txt`, `post_history_instructions.txt`,
  `player_description.txt`, `chronicle_style.txt`): a folder of fragments (e.g. `behavior_guidelines.d/`),
  shipped fragments + Custom fragments, all concatenated. Ordering by **numeric filename prefix**
  (`010_decency.txt`, `020_grounding.txt`), NOT raw GUID (GUIDs sort randomly and scramble prompt order).
- **Per-fragment override / opt-out:** a Custom fragment of the SAME name as a shipped one REPLACES it (the
  same Custom-first shadow, but per fragment, not per whole file), so the player masks only the ONE rule they
  change and still gets updates to all the others. An empty Custom fragment disables that rule. (A shipped
  fragment cannot be deleted durably: an update restores it, hence override-by-shadow rather than delete.)
- **Back-compat:** keep loading the legacy monolithic file as one more fragment so existing setups do not
  break; migrate over time.
- **`.json` files** (`cultural_traditions.json`, `jealousy_cultures.json`, `character_overrides.json`) are
  keyed by faction/hero, so the right model there is NOT text concat but **merge/override by key** (a Custom
  entry adds or replaces one culture/hero without shadowing the whole file). Two mechanisms, not one.
- **Cheap stopgap to ship first if wanted:** a staleness warning, stamp shipped files with a version and, at
  load, warn when a Custom copy has fallen behind ("your Custom behavior_guidelines.txt is N updates behind").

Aligns with the "make it your own" principle by making customization SAFE (customize without losing fixes).

---

## SHIPPED (one line each; the full sections are in ROADMAP_ARCHIVE.md)

Many 2.8.x pieces never had a section here (the story archive and Long Memory, captor audiences, women in the ranks,
the report button, the modder API doors, duel wagers): the CHANGELOG is their record.

- What You Know of them: SHIPPED 2.8.0 (KnownPageReader, encyclopedia section), folded into "What you have learned" in 2.8.1.
- An arc that paces a tale's conversation: SHIPPED 2.7.21 (TaleArc, max_exchanges, the 3/3/3/5 Rhagaea night).
- Friends at war, calls for help, legends (DepaHN): SHIPPED 2.8.2 (raid restraint, HelpPleaBehavior, legends). Parked: the model's review of war and peace (listed under Open).
- Captivity scenes other than abuse: SHIPPED 2.8.2 (CaptorAudiencePolicy, at every content level).
- Extortion by the player: SHIPPED 2.8.2 (extort, expose_secret, lords' own secrets).
- Raids and devastation as misdeeds: SHIPPED 2.8.2 (MisdeedLedgerBehavior). Left out: Show Mercy as a kindness (listed under Open).
- The player's name (fame) and edicts: SHIPPED 2.7.20 (PlayerFamePolicy, EdictBehavior; tax patch repaired 2.7.21).
- Items in tales: SHIPPED 2.7.19.
- A match for a daughter of the player's house (tashmetu): SHIPPED 2.7.19 (CrMatches, CrMatchRequest).
- The interpreter's tags applied after the reply shows: DROPPED, the MCM choice removed in 2.7.12.
- Lords with aims of their own (tashmetu): SHIPPED as the CrAims door 2.7.19, lords seek the player out about their aims 2.8.0, CrAims.List 2.8.10.
- Food and favours: SHIPPED 2.6.10 (earn_regard, plead_case, TownFoodPolicy).
- The prompt cache, Compact, and the knowledge audit: SHIPPED 2.5.10 (cache fix again 2.5.18).
- Self-esteem, the mood finished, the perceived image: SHIPPED 2.6.9 (SelfEsteemBehavior, PerceivedImagePolicy; cause-named moods 2.7.17).
- The mood of the day: SHIPPED 2.6.9 ("The mood of the day, finished").
- The group scene stops being written by one voice: SHIPPED 2.5.9.
- A lord takes one of your people (take_into_service): SHIPPED 2.5.9; never yet seen to complete in play.
- The action bench and its two gaps (14/09 measure): SUPERSEDED by the reworked bench (2.8.14) and cr.cost_bench (2.8.15-2.8.17).
- Open player threads as of 14/09: STALE, archived (2.5.x reports).
- What a character knows, declared once and auditable (knowledge packs): SHIPPED 2.5.9.
- Take your own people aside and brief them: SHIPPED 2.5.9, play-verified (CompanionBriefingCoordinator).
- Rob, beat or kill the character you talk to: SHIPPED 2.5.3.
- A battle leaves real facts (BattleFeatJournal): SHIPPED 2.5.3.
- In-game NPC persona editor (wished 30/08): MERGED into the character card wish (Open).
- Stolen love: SHIPPED 2.3.0, off by default (EnableStolenLove, NpcMarriageEstrangementBehavior).
- The post-duel conversation: SHIPPED 2.3.0 (PostDuelPayload; staged settlement scene 2.7.5).
- A letter-borne agreement survives into the conversation: SHIPPED 2.2.0 (Level 1).
- An intercepted player letter can leak: SUPERSEDED in 2.6.0 by courier secrets (the leak setting and its roll removed).
- Letter reasons with a real proposition: ServiceOffer SHIPPED; the PrisonerOffer letter replaced by PrisonerEnvoyBehavior in 2.5.24.
- An NPC teaches the player a skill (teach_skill, real XP): SHIPPED (cr.teach_info, self-test).
- The matchmaker: SHIPPED 1.31.11 (ArrangedMatchPolicy).
- Forced marriage: SHIPPED 1.31.8 (ForcedMarriagePolicy).
- Choose who comes with you, prisoners included: BUILT and in the chat (version of the prisoner picker not pinned).
- Logging wrote nothing when the Logs folder was missing: SHIPPED 1.31.8.
- An unproven claim could buy real regard: SHIPPED (claim gate; names the player since 2.8.11).
- Explicit Reply Language setting: SHIPPED (14 readers; blank = the game's language since 2.7.14).
- The council as negotiation, consigned resolutions: SHIPPED in the Council* behaviours (pledges became tasks 2.7.13, scout pledges 2.8.8-2.8.9).
- Rescue awareness + Enter-to-send: SHIPPED 1.30.5 (RescueGratitudePolicy, CaptorGrudgePolicy).
- First impressions: SHIPPED 1.30.2 (FirstImpressionPolicy). Open tail: notables and wanderers (listed under Open).
- Companion audiences on other subjects: SHIPPED 1.30.3 / 2.1.1 (CompanionAudienceTopicPolicy). Open tail: a moral concern about the player's deed (listed under Open).
- Jealousy audit: fixes SHIPPED 1.30.3. Open gaps listed under Open.
- Courier robustness batch: SHIPPED 1.31.5 (items 1, 2, 4, 5, 6). Item 3, an honest ETA in the picker, listed under Open.
- Nemesis discoverability + lord bounties: SHIPPED 2.1.2 (NemesisDossierBuilder, InterceptionTrigger.NemesisBounty).
- Escorted courier at the tavernkeeper: SHIPPED 1.34.1. Not built: an escort that actually fights.
- An audience in a fief from characters with no party: SHIPPED 2.1.2 (FiefAudiencePolicy).
- Quest/action parser hardening: phases A/B/C SHIPPED. Deferred A4, B5, D listed under Open.
- Affect coherence reform: SHIPPED 1.22.0.
- Appreciations: SHIPPED 1.22.0.
- Grudges: SHIPPED 1.22.0.
- Scripted scenarios: SHIPPED 1.14.0 (Scenarios/, ScenarioComposer.html). Open tail: more consequence words (listed under Open).
- Companion war-weariness and honourable retirement: SHIPPED 1.19.0 (WarWearinessBehavior).
- Hand-made portraits in the chat and for the player: SHIPPED 1.30.0 (custom folder 2.8.8, CrPortraits.RegisterSource 2.8.10); the letter window not confirmed.
- Player action narration (*...*): SHIPPED (about 1.16-1.17).
- Time-gap chatter, group-scene recall, post-history hook: SHIPPED 1.17.0-1.17.3. Open tails (stranger witness profile, romance override block) listed under Open.
- Conception from any vaginal act: SHIPPED in the pregnancy system.
- Hidden bastards: SHIPPED 1.15.0.
- Lean prompt for small models: SHIPPED 1.20.0.
- Stance, mechanical gating on four axes: SHIPPED (about 1.23; StanceGatePolicy, TownAmbushBehavior).
- SuperGraham339 round (Nemesis 1.25.0/1.28.0, player appearance 1.26.0/1.29.0): SHIPPED. Open tails (height, player_description.txt) listed under Open.
- Town ambush: SHIPPED 1.18.0, out of experimental (no toggle left); files with the world's initiative since 2.8.0.
- The world's initiative, one director for every unasked interruption: SHIPPED 2.8.0 (WorldInitiativeBehavior, cr.knocks, the letter box), hints 2.8.1.

---

## Governing principles (carried across every layer)

- **The hero is the PLAYER.** Spend tokens on what surrounds the player, not on NPC↔NPC scenes the player
  isn't in. Background simulation is MECHANICAL (native relations, cheap ticks); the LLM narrates only at the
  player's point of contact.
- **Verified deeds, never words.** The game computes and verifies outcomes (value floors, real transfers,
  real combat, real events); the LLM negotiates the story. "Quests that don't break."
- **Open & editable.** Prompt/data files in `ModuleData` are the moddable surface; the code is the engine.
