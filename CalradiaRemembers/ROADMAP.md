# Calradia Remembers — Roadmap

## Mission

**Calradia Remembers étend Mount & Blade II Bannerlord là où la simulation s'arrête : dans la mémoire et la parole des personnages.**

Le mod repose sur trois engagements :

**1. Respecter l'esprit de la communauté.**
Le modding vit du partage et du respect — des créateurs envers les joueurs, des joueurs envers les créateurs, et de tous envers le jeu original. CR s'inscrit dans cette tradition : code ouvert, architecture extensible, documentation honnête sur ce qui est testé et ce qui ne l'est pas. Pas de magie noire, pas de promesses creuses.

**2. Enrichir Bannerlord sans le trahir.**
L'objectif n'est pas de créer un monde parallèle de roleplay déconnecté du jeu. Les NPCs de CR se souviennent, réagissent et parlent — mais ils restent des seigneurs de Calradia, pas des personnages de roman. Les actions qu'ils promettent arrivent dans le journal. Les relations qu'ils forgent s'inscrivent dans la carte politique. CR amplifie ce qui est déjà là ; il ne le remplace pas.

**3. Un code à la hauteur de l'ambition.**
Un mod qui dure s'appuie sur une architecture qui résiste. Logique pure isolée du moteur, seams injectés, tests qui documentent les comportements attendus — pas les détails d'implémentation. Quand Gabriel lit les tests, il lit la spec. Quand un comportement change sans le vouloir, un test le dit avant que le joueur ne le découvre.

---

The long-term direction of the mod. Each layer is shippable on its own; the order is the architecture.
Full detail for shipped work lives in `CHANGELOG.md` and git history — this file tracks what is DONE at a
glance and the OPEN work ahead.

> **This document is load-bearing — keep it maintained throughout development.** The roadmap is not a
> formality: it is the project's **brainstorming surface** and its **strategic tracking** tool. Every design
> discussion, parked idea, deferred piece, open question, and shipped milestone belongs here, recorded as it
> happens — so the direction stays legible across sessions and nothing of value is lost. Update it the moment
> a feature is wished, decided, started, or finished; prune what is stale; verify against the code before
> calling something "shipped" or "a gap." A current roadmap is how we choose the next step with confidence.

Cleaned up 2026-06-23 (the many already-shipped "Also planned" entries collapsed into the summary below).

---

## RULED 2026-10-25 (Gabriel), BUILDING FROM STEP 1: one path for actions, one junction (who reads them)

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

**Open, for Gabriel.**

1. Where the per-turn conditions go. Proposed for both renderings: the full catalogue, stable, in the cached prefix,
   and a short "open now / closed now, because" list below the `CURRENT ENCOUNTER:` marker. Standard's prose prompt
   would then carry the whole catalogue rather than only the open verbs: to be measured for quality (action bench,
   rp_bench) before it is adopted.
2. What happens when the interpreter fails: one retry, a quiet notice to the player, or the loss logged as today. The
   prose's own tags can no longer be the fallback once Enhanced stops teaching them.

**Steps.**

1. **The parity map and its guard** (building): `docs/maps/composition-parity.md`, one row per verb, saying whether the
   interpreter is taught it, whether the prose model is taught it with a permissive context and with an empty one, and
   whether the bridge handles it; the context fields read by one side only; the condition that decides when the
   interpreter runs. A blocking guard: a verb taught to one reader and not the other is refused unless it stands on an
   allow-list with its reason, and the list may only shrink.
2. **The action reader as the single junction** (the after-tags path).
3. **The per-turn plan**, starting with the turn's own blocks (tale choices, conclusion); the tale arc builds on it.
   Mends `story_choice` in Standard and `witness_leaves` in Enhanced.
4. **Verbs migrate to their spec one by one**; a ratchet counts those still taught outside the plan.
5. **Enhanced's prose prompt without actions**, measured before and after.

## DESIGNED 2026-10-25 (Gabriel), AWAITING HIS REVIEW, NOT BUILT: an arc that paces a tale's conversation

**The wish.** Gabriel (25/10/2026), on The Empress's Summons: "comment le LLM dirige le beat pour amener le joueur vers
le ending dawn? Car ca pourrait etre trop court ou trop long." Then: "On pourrait creer un orchestrateur configurable
comme on a fait pour les captivity events? [...] les 3 premiers servent aux preliminaires, les 3 suivants le NPC prend
l'initiative, les 3 suivants il laisse au joueur l'initiative. A partir de la, une condition: si le NPC a eu un
orgasme, le NPC a une conversation avec le joueur de 5 beats puis on ferme le stage vers dawn. Sinon, le NPC reprend
le beat ou elle prend l'initiative. Ce plan ou cet arc devrait diriger l'experience que le LLM fait vivre au joueur."

**What exists today, and why it is not enough.** A `talk_to` with `"when": "closed"` completes when the PLAYER closes
the chat; the model has no clock (nothing in the prompt changes from one reply to the next within a stage) and no way
to close the conversation. So the night lasts as long as the player keeps typing, and the stage's canvas can only say
"before the night ends", which the model has to guess. The captive scenes solved the same problem in June with
`CaptiveSceneDirector`: a stage machine the MOD owns, which hands the model ONE named beat per reply ("you already did
X, Y; now do Z; do not repeat"). The arc is that idea, written by the tale's author instead of hard-coded.

### The shape, in the tale file

An `arc` on a `talk_to` objective (meant for `"when": "closed"`):

```json
"objective": {"type": "talk_to", "hero": "{giver}", "when": "closed",
  "arc": {
    "signals": [
      {"id": "npc_climaxed", "means": "{giver} reaches her climax in this reply",
       "net": {"after": 6, "chance": 0.5}},
      {"id": "refused", "means": "{player} refuses or pushes {giver} away, in words or deeds"}
    ],
    "beats": [
      {"id": "prelude",   "exchanges": 3, "lead": "npc",    "do": "Slow preliminaries: ..."},
      {"id": "she_leads", "exchanges": 3, "lead": "npc",    "do": "She takes the initiative: ...", "listens": ["npc_climaxed"]},
      {"id": "yields",    "exchanges": 3, "lead": "player", "do": "She lets {player} lead: ...",    "listens": ["npc_climaxed"],
       "next": [{"if": "npc_climaxed", "to": "after"}, {"to": "she_leads"}]},
      {"id": "after",     "exchanges": 5, "lead": "npc",    "do": "Lying together, she talks; before dawn she asks for the secret ...",
       "next": [{"to": "close"}]},
      {"id": "stung",     "exchanges": 2, "lead": "npc",    "do": "A refusal stings her pride; she answers as an empress, then sends {player} back down.",
       "next": [{"to": "close"}]}
    ],
    "on_signal": {"refused": "stung"},
    "max_exchanges": 30,
    "final": "after"
  }}
```

- **Beats** run in order unless a beat says `next`. `exchanges` counts one player message and the reply to it. `lead`
  says who drives this beat (`npc`: she acts and proposes; `player`: she follows and answers what the player does).
  `do` is the author's direction for the beat, in the same voice as a canvas.
- **Transitions.** At the end of a beat, its `next` list is read top to bottom and the first whose `if` holds wins (the
  flags the engine already has: `if` takes flag names and `!flag`). No `next` means the following beat; `"to": "close"`
  ends the arc.
- **Signals** are things the author wants the game to notice in the prose. A signal seen sets a tale FLAG of the same
  name, so a transition, a later canvas line or an ENDING can test it (the dawn ending's memory can depend on
  `npc_climaxed` as it depends on `regard_rose` today).
- **`on_signal`** jumps at once, whatever beat is playing, when that signal is seen (the refusal).
- **`max_exchanges`** is the hard cap: when reached, the arc goes to its `final` beat whatever happens, so a loop such as
  yields -> she_leads -> yields always ends (the director's `MaxActs` in another form).

### Rulings so far

- **Who decides a condition: both** (Gabriel, 25/10/2026: "melange des deux, signaux observes avec tirage en filet").
  - *Observed:* in every reply while a beat `listens` to a signal, the action interpreter is also asked whether that
    signal happened in THIS reply. It answers with a new verb on the `story_choice` pattern (`tale_signal`, `id:`),
    which the bridge re-validates (only a declared signal, only while the current beat listens to it) before the
    game sets the flag.
  - *Net:* a signal with `net` that has not been seen after `after` exchanges of listening is rolled by the game at the
    end of each listening beat (`chance`). On success the game sets the flag AND the next beat's direction says it
    happens ("in this beat she reaches her climax"): the game declares it, the model narrates it. This keeps the
    rule that the game decides what happens, and the player's way of playing the scene still counts through the
    observed path.

- **A strong intervention holds the beat** (Gabriel, 25/10/2026: "je garderais une intervention forte qui retient..
  c'est un ajustement naturel au roleplay du joueur"). As in the captive director: when the player's message turns the
  scene (a strong act or word of their own), the beat does not advance on that exchange and the direction tells the
  model to answer it first; a second strong intervention in a row lets the beat advance, so the player can never stall
  the arc. Who judges "strong" is the same reader as the signals (an implicit signal of every arc).
- **Signals work in both compositions** (Gabriel, 25/10/2026: "en mode regulier non-enhanced, le LLM cherche aussi les
  actions.. donc les signaux fonctionnent dans les deux modes"). In Enhanced the interpreter reads the reply; in
  Standard (Integrated) the prose model writes its own `[ACTION]` tags, so the signals are taught to it there instead.
  Found while checking (25/10/2026): `story_choice`, the precedent, is taught ONLY to the interpreter
  (`StoryChoicesForInterpreter`, read by `ActionInterpreterContextBuilder` alone), so a tale's choices by words cannot be
  taken in Standard composition today. Increment 2 teaches both to the prose model when no interpreter runs, which
  mends that gap at the same time.

### Proposed, awaiting Gabriel

- **The refusal** (asked 25/10/2026, not yet answered): a `refused` signal, observed, with `on_signal` to an exit beat
  where she answers as an empress and the arc closes. The stage then completes towards its usual ending, and the
  memory follows the regard as today (a refusal will usually have cost regard, so the bitter memory).
- **How the arc closes:** after the reply of the last beat, the conversation is ended softly, the way a captive scene
  ends (`EndCaptiveSceneSoftly`: the input locks, the button reads "Leave"), so the player reads her last line; the
  click closes the chat and the stage completes as `"when": "closed"` already does. Proposed: the arc's close applies
  even when the MCM "NPCs can end conversations" is off, since it is the tale's structure and not a character's whim.
- **The player closes first:** the stage completes as today; signals never seen stay unset; the endings judge by what
  is known. Nothing new.
- **Every reply counts as an exchange**, whatever the player wrote, except that a strong intervention holds the beat
  (below). Inside a beat, the direction asks the model to answer the player's own move first, then carry the beat on.

### What the code already has (traced 25/10/2026)

- The tale's stage text already sits BELOW the `CURRENT ENCOUNTER:` marker (`AuthoredQuestBlock`, PromptBuilder
  `AppendEncounterContext`), so a direction that changes every reply costs no cache. The beat direction gets its own
  field (`TaleBeatDirective`) and its own Tail section; the captive one cannot be reused (its gate is captivity only
  and its text is hard-coded).
- `story_choice` is the exact precedent for asking the interpreter about the tale: a block per turn in
  `StoryChoicesForInterpreter`, a verb, a case in `BannerlordGameStateBridge`. `WithInterpretedTags` copies only
  actions and events from the interpreter, so a signal rides as an action.
- The single place where a player message and its reply are both done is the end of `RunLlmExchangeAsync`
  (ChatViewModel.Exchanges), after the actions are applied: the arc counts there, with this reply's signals known.
- `AuthoredQuestRun` is saved whole as JSON; new properties are additive (`GiverRegardAtStart`, `ImagePicks` did the
  same). The arc keeps its beat, its exchanges in the beat and in all, and the beats done; all reset when the stage
  changes.

### Increments

1. **A straight arc**: beats, exchanges, lead, `do`, the direction in the prompt, `max_exchanges`, the soft close. This
   alone answers "too short or too long". Loader, editor checks (parity fixtures), save, `cr.testall tale_arc`, the
   Rhagaea night written with it.
2. **Signals and transitions**: `listens`, `next` with `if`, `on_signal`, the `tale_signal` verb, the net roll.
3. **The editor's arc cards** and the arc shown in the story map's summary panel (the JSON checks ship with 1 and 2,
   since the editor must refuse what the game refuses).

Not in scope: a hard close that cuts the scene mid-sentence; arcs on stages other than `talk_to`; several characters
taking turns inside one arc (the captive director's collective acts).

## WISHED 2026-10-23 (Gabriel), ROADMAPPED, NOT DESIGNED: extortion by the player, a misdeed with no deed yet

Gabriel (23/10/2026), for the Misdeeds section of the player's page (`4679f2a`): "on pourrait aussi ajouter des
trahisons, extortions, et tout autres gestes qui pourraient etres classes comme misdeeds". Extortion cannot be listed
because the player cannot do it: no verb lets the player threaten or blackmail a character for gold, a favour or a
secret. The existing `pay_for_silence` and the bastard mother's `pay_blackmail` run the other way (a character extorts
the player). A design has to answer:
- **The act.** A verb `extort` (threat of exposure, of violence, of ruin), gated like `rob_npc` (the player can fail),
  or a lever on a secret the player holds (courier secrets, a discovered affair, a bastard).
- **The price.** What the victim gives, what they remember, whether they pay again or strike back (a grudge source of
  its own), the crime rating if a threat of violence.
- **The record.** A `MisdeedKind.Extorted` written to `MisdeedLedgerBehavior` at the verb, so the page tells it ("You
  extorted 2,000 denars from Lucon, twice, ..."), at every adult level.

## WISHED 2026-10-23 (Gabriel), ROADMAPPED, NOT DESIGNED: villages raided and towns devastated, as misdeeds

Same request as above. The player raiding a village, burning it, or choosing Devastate (or Pillage) after taking a
town is a misdeed by most measures, and nothing keeps it: the game logs `VillageStateChangedLogEntry` and
`SiegeAftermathLogEntry` for 7 days only (`LogEntryHistory.DeleteOutdatedLogs`, 1.4.8), and CR listens to neither.
To do:
- **Record as it happens.** Listen to the village looted / raid completed event and to the siege aftermath choice; when
  the player led it, write `MisdeedKind.Raided` / `Devastated` / `Pillaged` to `MisdeedLedgerBehavior` with the
  settlement's id as victim. The reader names a settlement as it names a realm.
- **Decide with Gabriel.** Whether a raid on an enemy village in a war the player's realm declared is still a misdeed
  (a lord ordered to raid?), and whether Show Mercy deserves a line elsewhere (a kindness, not a misdeed).
- **Old saves.** Nothing to backfill: the 7-day log is gone. The page tells raids from the version that ships it on.

## WISHED 2026-10-23 (Lordfadooboo, Nexus), ROADMAPPED, NOT DESIGNED: the player rides in another character's train

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

## DESIGNED + BUILDING 2026-10-22 OVERNIGHT on branch night/fame-edicts (Gabriel, after reading AnimusForge): the player's name, and edicts

Gabriel (21/10/2026), shown what another LLM mod (AnimusForge 1.6.4) does, picked two ideas: "2 et 3 m'interesse".
Then, before bed: "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre conteste. ... fait le maximum que tu
peux, je te laisse prendre des decisions logique etant donne que tu connais la philosophie de CR. Je reviserai tout
ca demain." Every choice below marked DECIDED OVERNIGHT is mine and waits for his ratification; every number is a
named TUNE constant.

### A. What a character knows of the player's NAME (fame)

Today an NPC is told what deeds of the player reached them (WorldEvents, RumorAwareness), and a lord's first tone
reads the player's renown, but nobody is told whether they have even HEARD the name. A famous king met by a village
notable gets "You have never met this person" and nothing more; an upstart gets the same.

- `PlayerFamePolicy` (pure): a band, Unknown / a name heard / known by reputation / famous, from the player's renown,
  whether the player rules a realm, and the observer: lord, notable or commoner; same realm; same culture; how many
  of the player's deeds took place in the observer's land. DECIDED OVERNIGHT: culture counts for everyone and realm
  counts for everyone (Gabriel did not pick between them); no map distance (realm and culture already say "near").
- A deed heard (the existing path) is a FLOOR: whoever heard a deed of the player knows them by reputation. Fame never
  hides a deed, and the first impression is untouched.
- What the name carries, once known by reputation: the player's own traits as the game keeps them (Mercy, Honor), worded
  "said to be merciless", "said to keep their word". DECIDED OVERNIGHT: one axis, read from the game's traits, no new
  ledger; the crime rating stays where it is (UnderworldPack).
- Nothing saved: computed when the NPC speaks. One line in CURRENT ENCOUNTER. Someone who already knows the player
  (regard, relation, shared events, kin, companions) gets no line.
- Later: the player's page in the encyclopedia ("your name in Battania: known by reputation").

### B. Edicts: a ruler's lasting, bounded decision on a town or a realm

Nothing like it exists: the council's 26 resolutions move people, purses and pledges; no economy model is touched.

- A CLOSED catalogue (`EdictCatalog`), each edict a gain bought with a cost, applied per day through the game's own
  models: a described line on the town's ExplainedNumber (tax, loyalty, security, prosperity, militia, food), so the
  game's own tooltip shows it ("Edict of Derthert: +1"). Never a hidden write, never faked.
- Town edicts (on one town) and realm edicts (the same catalogue on every town of the realm). DECIDED OVERNIGHT: realm
  edicts are CR's own, not the game's kingdom policies (those go through the game's vote, a later bridge).
- One edict per town at a time (a realm edict counts on each of its towns); 30 to 90 days; it lapses when whoever
  enacted it no longer holds the town or the throne.
- The player enacts through his governor or his steward in conversation, ending in a [Y/n] that names the edict, its
  effect and its term BEFORE anyone announces it (gating before declaration); a council resolution later.
- NPC rulers enact too (Gabriel: "les PNJ peuvent aussi"): a pure choice from the town's state (hungry: grain;
  lawless: watch; restless: relief), rare and capped per realm, told as realm news so the player meets them. DECIDED
  OVERNIGHT: behind an MCM switch, OFF by default for the first build.
- Contest (Gabriel: "un edit peut etre conteste"): a pure rule. A town whose loyalty falls under a threshold while an
  edict that costs it is in force grows a grievance; its notables remember it and raise it; past a second threshold
  the edict is repealed early, and the realm hears it. No riot mission in this build.
- Save: one additive key, the edict ledger; an old save loads with none.

## BUILT 2026-10-21 for 2.7.19 (Gabriel: "fait la phase 2"): items in tales

tashmetu (20/10/2026): "A tale that asks for things ('bring 30 grain to {giver}'), and a thing given in an ending."
Decided in the build: the party's goods count; they leave only through the bag's supply picker, on the player's
confirmation, exactly the count, all at once (no partial tally, so the save is untouched); short of the count nothing
leaves (gating before declaration). The goods are gone to the receiver (not into a market). `item` reads as a kind, then
an item category, then one item's id; the `item` consequence takes an item's id only. Unknown goods keep the tale from
being offered. Later, if asked: a partial hand-over over several visits; goods into the giver's town market.

## BUILT 2026-10-20 for 2.7.19 (tashmetu, approved by Gabriel): a match for a daughter of the player's house

tashmetu (20/10/2026): "When the bride is the player's daughter or sister, the one with the say over her hand is the
player, so the door refuses. The groom's side (the head of his house, or his father) could be the one who asks for her,
with 'his hand' / 'her hand' following who is asked." CrMatches assumed the player is always the broker, never the head
of the bride's house (`MarriageProspectResolver.HasAuthority`: her clan's leader or a parent).

The design, decided on Gabriel's "j'approuve tous les points":
- **Who asks whom.** When the player has the say over the bride (the head of her house, or her parent), another mod
  may register the match with the decider on the GROOM's side: the suitor himself, or one with the say over him (the
  head of his house, or his parent). The door takes it as a match that asks the player (`AsksThePlayer`), refused as
  before when neither side holds the say.
- **No new initiative.** The groom's side is taught, in a conversation the player opens, that he may ask the player for
  her hand ("her hand": the bride's hand is the one asked). He does not seek the player out for it: a mod that wants
  him to bring it up unbidden registers a CrAims aim for him, which carries its own pacing and ActionGate.
- **The player answers.** The model emits arrange_match when the player gives her hand in words; CR then asks the
  player in the chat, "Give {bride}'s hand to {suitor}?" [Y/n], so a daughter is never wed on the model's reading of
  a sentence. No: nothing happens, and the match stays registered.
- **The law.** The player must still hold the say over her and the decider over the groom; the game must find them
  lawful; no war or captivity between the houses; and her heart (she must hold him dear enough), as for every brokered
  match. The family's consent is the player's own yes, so the regard and the standing of houses are not weighed.
- **Not done:** the symmetric case (the player's son, the bride's side offering her to him) needs no change to work as
  a broker's match today (the bride's head is asked as before); an offer made TO the player for his son, with "his
  hand", is left until a mod asks for it.

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

## REMOVED 2026-10-15 (fkasad), NOT BUILT: the interpreter's tags applied after the reply shows

The MCM "Actions Interpreter Timing" dropdown offered "Asynchronous: the reply shows instantly, tags apply a moment
later", but the code ran it synchronously behind a TODO. fkasad (Nexus, 15/10/2026) asked what it meant; the
dropdown was removed in 2.7.12 rather than left lying. To build it for real: run the interpreter on a background
task after control returns to the player, apply only the safe outcomes (memory, regard), and skip flow control
(`end_conversation`) when the conversation has moved on. The inline consumption block in
`ChatViewModel.Exchanges` has to come out of `ComposeByInterpretationAsync` first. On a local server it would also
compete with the next reply, which is what fkasad's report was about.

---

## WISHED 2026-10-14 (tashmetu, BC-CR bridge author), GABRIEL SAID BUILD, DESIGN FIRST: lords with aims of their own

tashmetu (Nexus, 14/10/2026), from two of his testers: "in Calradia, nobody takes the first step with the player. If the
player doesn't court, scheme or ask for something, no one does." He proposed a bridge prompt variable `{{bc_ambition}}`
that gives ONE lord in a few an aim of his own (a fief his house lost, a claim it is fabricating, a council seat and the
votes it needs, a grudge and the allies it wants, a rematch, a hidden fondness and a go-between, grain to borrow, a
captain between contracts, a bastard wanting recognition, a feast), told as the lord's own want, with what the player has
to do with it, and leave to raise it unbidden. He asked whether CR would rather own the "when" and the "already said".

Why a prompt variable alone cannot do it (traced 14/10/2026): `PromptVarFacts` carries no turn, no conversation, no day,
and a provider runs on EVERY reply, so the lord would bring up Lakonon every time. CR already owns the three things the
idea needs: a "said once" ledger (`InterceptionBehavior._raisedMatterKeys`, marked when the opener is used), reasons for
a lord to seek the player (RoadInterception, FiefAudience, both paced), and the paced mailbox (`LetterMailboxPolicy`,
budget per window, 15-day per-NPC cooldown). Every one of them asks `ActionGate`.

Ruling (Gabriel, 14/10/2026: "oui, vas-y"): CR builds a public door; the bridge says WHAT a lord wants, CR decides WHEN
it surfaces and remembers that it did. It is also the door the 02/10 wish above needs ("I have work for you"): an aim
whose service is a verifiable deed is a task that comes to the player.

First shape (to discuss in prose before any code):
- `CrAims.Register(ExternalAim)` / `Update` / `Retract(id)`, plain strings like `Feed.Record`: Id, NpcId, SourceMod,
  Want (the lord's own want, bounded), PlayerPart (one line: what the player has to do with it), optional MissionId (a
  `MissionBoard` mission that makes the asked service a real, verified deed), optional channels (conversation, letter,
  audience), optional expiry day. Validation at the door: hero resolved, bounded text, one standing aim per NPC, quota
  per source, persisted under an additive save key.
- Conversation: the aim is part of what the lord knows and wants for as long as it stands; the LEAVE to raise it
  unbidden is given until it has been raised, then withdrawn (re-raise after a cooldown is an open question).
- Letter: an aim is a letter reason through the existing mailbox (budget, cooldown, `ActionGate.Letter`).
- Audience: an aim is one more "live reason to speak" for FiefAudience / RoadInterception, below the existing ones.
- How: from the lord's traits, in one CR-owned sentence: an honest lord asks openly; a calculating one works around
  the player or keeps his purpose to himself. NPCs never lie: concealment, vagueness and silence, never a falsehood.
  Gating before declaration: nothing is asked or promised that the game cannot carry; a service is a MissionBoard deed.
- Back to the bridge: an event `CrAims.Raised(aimId, npcId, channel)` so it knows the matter is out.

Open questions for Gabriel: what counts as "raised" (the floor given once, or the interpreter seeing it said); whether
an aim may come up again after N days while it still stands; whether CR should later seed aims from its own data
(tournament rivals, grudges, a lost fief), the door first either way.

**Decided by Claude on Gabriel's delegation (14/10/2026 night, "je te laisse decider"), each overturnable in one line:**
"raised" = the floor given once (marked at the conversation's opening, when a letter is released, when an audience is
granted), as a recent matter is spent when its opener is used; twice at most, the second only after 60 days while it
still stands; never toward an enemy (personal enemy band or realms at war), never inside a captivity; caps want 400,
player's part 200, 40 standing aims per source; ONE aim per lord across all sources; guarded = Honor < 0 or
Calculating > 0 (an honourable schemer still schemes), and the guarded sentence says in the same breath that he never
states a falsehood; without a MissionBoard deed he may sound out but promise nothing unmade. CR seeding its own aims:
later, the door first.

**BUILT (pure, no engine glue), 14/10/2026:** mod `6d4c628` + `59f943e` (`Logic/Aims`: AimBook keyed by source+id,
AimRules, AimRaisePolicy, AimApproachWording, AimConversationLeave; 30 tests), SDK `111e048` (section `own_aim` in the
prefix, 4 EncounterContext fields, compact one line; 5 tests). Nothing reaches a player yet.

**PUBLIC DOOR BUILT 20/10/2026 for 2.7.19** (Gabriel approved the shape below as proposed; `Extension/CrAims.cs`, plus
`AimResult.ReservedSource` for a mod using CR's own source name; cr.testall `aims_door`). Conversation only; letter and
audience channels for another mod's aims still to come (the `Channels` field already takes them).

**The public shape (was PROPOSED; modder-api.md and save-format.md are contracts):**
- `CrAims.Register(ExternalAim) -> AimResult` (Accepted, Updated, MissingIdentity, EmptyWant, UnknownHero,
  LordAlreadyHasAnAim, SourceAtCapacity, AlreadyExpired) and `CrAims.Retract(sourceMod, id) -> bool`.
- `ExternalAim` (plain strings, like ExternalWorldEvent): Id, SourceMod, NpcId, Want, PlayerPart, MissionId (optional,
  a MissionBoard id), Channels ("conversation", "letter", "audience"; empty = all), ExpiresOnDay (optional).
- `CrAims.Raised` event: AimId, SourceMod, NpcId, Channel, GameDay. It means the lord was GIVEN THE FLOOR (the
  conversation opened with his leave, the letter was released, the audience granted), NOT that he said it: in a
  conversation he may judge the moment wrong and stay silent. Say so to the bridge in those words.
- Save: one additive key (`cr_standing_aims`, JSON byte[] like the other stores); an old save reads no aims.
- Glue, in order: (1) conversation: CrConversation.Started -> AimConversationLeave.Open (enemy, captivity facts from the
  host), EncounterContextBuilder fills OwnAim*, Ended -> Close, Raised fired; (2) letter: a new LetterReason through
  the mailbox (budget, cooldown, ActionGate.Letter), marked when released; (3) audience: one more "live reason to
  speak" below the existing ones in FiefAudience/RoadInterception, marked when granted. Each with a cr.testall test
  (register -> the prompt carries the want -> the leave holds on a later reply -> retract).

**BUILT 15/10/2026, INTERNAL (Gabriel: the vassal war duty "passe par la nouvelle architecture"):** glue step (1),
`Aims/AimsBehavior` (save key `cr_standing_aims`, leave decided at `CrConversation.Started`, closed at `Ended`, the
four `OwnAim*` context fields from `EncounterContextBuilder`, daily expiry, an internal `Raised` event). Nothing is
public yet: the `CrAims` door above stays PROPOSED until Gabriel and tashmetu have seen it. Aims seeded by CR itself
(source `CalradiaRemembers`) are not voiced by `own_aim`: their mechanic has its own section (the war duty's terms).
cr.testall `aims_conversation`. Letter and audience channels: next, with the war duty's summons.

**The vassal war duty, BUILT 15/10/2026 for 2.7.12 (Maifailun; Gabriel's ladder), NOT play-verified.** Inc 1-6:
pure rules in `Logic/WarDuty` (who owes, the ladder, the levy, the hand-overs), `WarDuty/WarDutyBehavior` (save key
`cr_war_duty`), the summons and levy letters (SDK `WarSummons`/`WarLevyDemand`), the CR aim on the ruler, the
`war_duty_terms` tail section, and the levy paid in person through `take_gold`, `give_item` (horses, weapons) and
`give_troops` (`GameState/WarLevyDesk`), and the audience channel (inc 6: trigger `WarDutySummons`, the ruler calls the
player to his hall when they enter his town, field party or not, ahead of anyone else and outside the pacing roll; he
never rides out on the road (Gabriel 16/10: the king sits for the meeting, he does not go out in person); no missed-meeting letter; only until the player has stood
before him, `WarDutyLadder.CallsThePlayerIn`). MCM Relationships: switch (off), window, levy scale. Defaults accepted by
Gabriel (15/10/2026): 20 days a stage, the levy formula, a castle before a town, the duty outranks another mod's aim on the same
ruler, soldiers capped at half the player's healthy men, armour does not count (horses and weapons only), any horse,
weapon or coin handed to the owed ruler counts toward the levy, the WarDuty namespace accepted in the cycle. Not done:
several realms' rulers at once (one reckoning
at a time), a vassal of a vassal (the duty is owed to the realm's ruler only).

## WISHED 2026-10-14 (tashmetu), ROADMAPPED: the captive PLAYER bargains for freedom

The other half of the 2.7.10 `counter_offer` (a lord the player holds offers ransom, fealty, service, goodwill). Today a
player held by a lord can only plead: `release_player` is taught when the captor allows persuasion
(`CaptorAllowsPersuasionRelease`), with no structured offer. Wanted: the player offers a ransom from his own purse, a
favour owed (a pledge the captor can call in, ideally a verified deed), or information, and the captor weighs it in
character. Strictly a bargain: nothing forced. Information must be true and groundable (NPCs never lie, and the captor
must not accept a "secret" the game cannot hold the player to). Design after CrAims.

## WISHED 2026-10-02 (Lordfadooboo + fkasad, Nexus), NOT BUILT: characters who come to the player WITH a task

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

## BUILT 2026-10-01 for 2.6.10 (fkasad + Lordfadooboo, Nexus thread of 01/10), NOT SEEN IN PLAY: food and favours

- **A hungry town remembers who fed it, and who emptied its market** (MOD `d37b4bb`): `TownFoodPolicy` +
  `FoodTradeMemoryBehavior` on `PlayerInventoryExchangeEvent`; relief / hoarding as a placed world event, once a week.
- **Grain asked for while a town starves goes into its stores** (MOD `d3acfbe`): a delivered grain `provide_goods`
  task feeds the giver's first hungry seat (`Mapping.HeroSeats`, the one "places a character answers for" lookup).
- **earn_regard** (MOD `61a3c27` / SDK `49f57af`): a lord asks the player to win the good graces of another
  (+10 on the mod ledger from the baseline, nothing to win past Devoted, at least 30 days).

- **plead_case** (MOD `eb02994` / SDK `4096758`): plead the giver's case before his liege; done when that person
  is won over TOWARD the giver by a real sway_opinion (which needs the giver named, `da49fb0`).
- **A town's needs** (MOD `b451a9f`): a trade good at twice its worth in one of the giver's towns may be asked for
  (`TownNeedsPolicy`, key `market:<category>`), and the goods go into that town's market.
- **reinforce_garrison** (MOD `580a978` / SDK `50b758b`): men for a thin garrison, handed over in that place by a
  verb taught only there; garrison bands moved to `GarrisonStrength` (one home with the seat pack).

**Still WISHED from the same thread, NOT built** (each needs its own design pass):
- (BUILT, MOD `6dc06fe`: ransom_envoy, purse advanced, change kept.) A neutral envoy hired by a lord to negotiate the release of one of his men from a realm the player is not at war
  with ("bring my lad back alive, keep the change"); peace negotiated as a neutral third party.
- (BUILT, MOD `3a2b37a`: scout_town, intel only.) Lowering a town's security through its gang leader, for gold: NOT built. Playing wingman: BUILT (MOD `dab830f`, arrange_match, a real marriage in two steps).
  (The prison break is BUILT, MOD `1d53d71`: the vanilla break now counts as a rescue for the task and the freed lord.)
- (BUILT, MOD `f4fe452`: recruit_troops.) A smithing task: BUILT (MOD `007fc21`, the kind named is part of the deal), VERIFIABLE: the game marks a player-forged weapon (`ItemObject.IsCraftedByPlayer`, checked 01/10/2026), so a give_item of one can complete it.

## FOUND 2026-09-21 by reading the generated maps, NOT ACTED ON: one concept under several names

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
  `cr.testall rendezvous` that plants one, walks the real arrival path, asserts the audience fires, and cleans
  up. Play-verification is the honest gate here, since the payoff is a scene.

### Increment 2, deliberately later: a lord who rides to meet you

Only once increment 1 is play-verified. This is the one that touches campaign AI (`SetMoveGoToSettlement`,
holding him there, and giving the AI back cleanly), and it must refuse the moment the lord is in an army, a
siege, a war march or a vanilla quest of his own. Related, and worth reading first: the ESCORT pillar already
scoped in this file, which has the same "the arrival IS the scene" shape.

### Open questions for Gabriel

None left. The increment is ready to scope into build steps when Gabriel calls for it.

---

## BUILT 2026-09-15/16, NOT RELEASED: the prompt cache, Compact, and the knowledge audit

Three days of work that share one shape: **a rule was written down, and nothing asserted it.** Every item
below was found either by a player reading prompts from the outside, or by an audit reading the code — never
by the suite, which was green throughout.

### What players found

- **fkasad** diffed two successive prompts of one conversation and reported the cacheable prefix dying at
  2.5%. He was right. `IsConversationOpening`, a fact about THIS TURN, decided wording near the top of the
  cached prefix, so every conversation paid two full prefix writes instead of one. Narration FREQUENCY moved
  whole into the dynamic tail; `PromptCacheSplit` holds the rule so tests measure the function the game runs.
  **Play-verified: cached went 0 to 12,864 on turn 2.**
- **The interpreter prompt was built to be cached and never was.** 70,378 chars (~17.6k tokens) of invariant
  head, re-sent at full price once per turn on top of the prose call — the larger half of what a turn costs.
  One line. **Confirmed in play at 96.6% cache reuse.**
- **DuskSymphony** cannot hold a conversation on an 8,192-token local model. Three things in a turn had no
  notion of a prompt level at all: the interpreter (70,378 to 25,353), the discovered-traits list (the only
  unbounded block in the prompt), and the language rule's worked examples. The transcript fold threshold
  ignored Compact too, which is why his FIRST message fit and his followups did not. And the reply budget
  was 3,000 tokens on an 8k window — the largest saving of the lot, and it cost nobody a sentence.
- **basildingleberry**, "many of them seem to talk the same way": every prompt shipped all eight cultures as
  an if-then list to a character whose culture the mod knows exactly. ~2,144 chars, and the model was
  averaging eight personality prescriptions.

### What the audit found (2026-09-15), and what became of each item

| # | finding | outcome |
|---|---|---|
| 1 | compression overwrote an in-place enrichment | fixed — anchors carry an identity, the write-back re-reads by it |
| 2 | two conversations in one day collided on one anchor | fixed — the same identity; a misattributed memory, not just a lost one |
| 3 | `Events.Clear()` + `AddRange` off the main thread | fixed — one reference store (`ReplaceEvents`) |
| 4 | `realm_news` / `underworld` ignored `IsCutOff` | fixed — and the RULE is now asserted over all four reach packs at once |
| 5 | house_means: a dropped army clause, a dangling "and" | fixed — both were invisible to their own guards |
| 6 | "two depth authorities" in seat_standing | **not a defect** — a documented two-level split. The TEST was pinning an unread number |
| 7 | day-scale volatility in the cacheable prefix | **open, and deliberately** — see below |
| 8 | identity frozen (name) or refreshed too late (spouse) | fixed — both now refresh at conversation open |
| 9 | the council renders no packs; the sweep exempts station and prisoners | fixed — three increments, plus both exemptions closed |
| 10 | the Compact budget guard was blind to memory | fixed — it now builds what the host builds |

### The council was worse than the audit said

A `CouncilMemberInput` was a name, a sex, an age, one archetype word, a culture and a regard number. No
memory, no relationships, **no station** — so a seated governor did not know he governed and a seated king
did not know he ruled. That is the Derthert/Garios failure, still live in a whole conversation mode, and it
is where "they all sound alike" bites hardest: culture and archetype is the recipe for interchangeable
voices.

Three increments, each sharing ONE reader with the private-word path so the table cannot drift from the
tete-a-tete: **station** (`StationPack.DescribeBriefly`), **memory** (bounded at two summaries, through the
witness builder so the private-memory filter is inherited), and **knowledge** (`NpcKnowledgeFactory.Compose`,
moved out of `PromptBuilder` where it was private — which is precisely why the council never had it).

### The type that ended a class of leak

Gabriel asked whether the Command pattern would guard the private-memory rule. It would not; a TYPE does.
`SharedRecall` has no public constructor, so a field of that type cannot be handed raw event text and the
leak **stops compiling**. Five places treated a recall as a string; the compiler listed them. Had it existed
a day earlier, the council wiring could not have been written the unsafe way — which was the actual near
miss that prompted it.

### Open, and why

- **#7, day-scale volatility.** `AppendHistory` renders "yesterday / 6 days ago" above the cache marker, so a
  new game day invalidates the prefix. Real, and ONE extra prefix write per NPC per day — an order of
  magnitude below what was fixed. Its cure moves prose, and `RecencySuffix` exists because the model is bad
  at subtracting day numbers. **Behind Gabriel's quality ruling, not forgotten.**
- **The anti-confabulation wall** (`STAY WITHIN WHAT YOU KNOW` and its two siblings) stays in the dynamic
  tail at ~1,840 tokens a turn. It sits last for RECENCY, deliberately, because players reported characters
  inventing. **Measured, offered, and refused — a priced trade, not an oversight.**
- **Unbounded rich summaries.** Memory events are capped at six in Compact; their LENGTH is not. Capping is a
  quality decision.
- **Two audit minors rejected with reasons** (the "and N others" counters, market_word's truncation): both
  rest on a rule that protects STANDING, and a market is not standing.

### What still needs Gabriel

- **`cr.testall` in game.** Two new live assertions (every ruler the engine knows is told he rules; every
  governor likewise) and prisoners now in the sweep — neither can be verified from here.
- **DuskSymphony should retest.** The arithmetic says ~6,942 tokens inside 8,192; that is a calculation, not
  a measurement on his machine.
- **A council sitting, read rather than counted.** Three increments of content reached the table and nobody
  has read one.

---

## RULED + BUILT 2026-09-30 (Gabriel + Lordfadooboo + Maifailun), NOT YET SEEN IN PLAY: SELF-ESTEEM, the mood finished, and the PERCEIVED IMAGE

Lordfadooboo (Nexus, 29/09/2026): "what if npc's had a hidden internal regard for themselves? a lord who has been
humbled regularly might have it lowered, and one who is doing well might have it higher. starting self esteem
could be affected by station... randomized by character... it may have affects on their expectations when
speaking to you and how they respond to failure... a character might become insecure with you and if its a
romantic character, they might require you to tell them things they want to hear... vanilla personality traits
might also play a part." Maifailun: "potentially regressing to traumas (too long captive wouldn't be so bold
when talking)". Gabriel the next day: finish the mood of the day, build self-esteem, and let the two together
make a PERCEIVED IMAGE of the character, shown in the chat's info panel.

### What it is, against what exists

The axes table under THE MOOD OF THE DAY below had no row for what a character thinks of HIMSELF. Stance is
toward the player, contentment is toward service, traits are fixed, mood is today. Self-esteem is the durable
self-regard those leave out, and it is the ground the mood of the day stands on.

### Rulings (Gabriel, 30/09/2026: "suis tes recommandations")

1. **Colour, never an outcome.** Mood's Ruling 1 extends to self-esteem: it shapes expectations, how a failure
   is taken, whether a lover needs reassurance, and never decides what a character may do. What makes it
   learnable instead of an invisible rule is the perceived image: the player can SEE a man is shaken.
2. **The image shows what shows.** A proud or calculating man hides a shaken self-regard from a stranger; those
   who know him (regard at friendly or better) see through. The image is itself a reward of the relationship.
3. **Whose, and when.** Characters the player has met (a CR profile), moved by events recorded on that profile
   from then on. Nobody keeps the self-regard of all Calradia for nothing (player experience over simulation).

### Mechanics (first cut, tunable, all in Logic)

- **Baseline**: rank (Stature: a ruler and a great house start higher, a wanderer lower), a personal draw seeded
  by the hero (the same man every time), and traits (valour steadies, a calculating mind swings less).
- **Events** move it: a battle lost or won at their head, a captivity (repeated captivities compound: the trauma),
  a release, a tournament, a fief lost or gained, a death in the house, a duel with the player, and reassurance
  from the player on a day they needed it.
- **Recovery**: it drifts back toward the baseline, slowly.
- **Bands, never numbers**: broken, shaken, steady, assured, proud.

### Increments

- M2 mood facts read from the game + the day's seed; M3 the mood line in the per-turn tail, traits colouring its
  expression, wit rare; M4 the player moves it in conversation (a control line judged by the character,
  stripped like [LETTER_TONE]; one band per turn, two per conversation); M5 letters and realm news carry it.
- E1 the pure self-esteem policy; E2 events recorded on the profile; E3 the prompt; E4 it weighs the mood's draw.
- P1 the perceived image (templates, no model call, updated as the mood moves); P2 shown in the chat info panel,
  both skins.

This settles the open question below ("should a mood be legible as a line of prose?"): yes, as ONE numberless
sentence of how the character seems, never a bar.

**Built 30/09/2026, all increments** (MOD 7d609b6, 530d04d, de2bb29, dc6d366 and the P1-P2 commit; SDK d2896f3,
289ffcb, e6f6323 and the letter increment). cr.testall: mood_of_the_day, self_esteem. Not built: the council does not
carry moods (still the open question below); the image is not in the encyclopedia (mood is only seen face to face).

## DESIGNED 2026-09-14 (Gabriel + player basildingleberry), NOT BUILT: THE MOOD OF THE DAY

Promised publicly on the pinned Nexus roadmap since 2026-07-03 ("a mood randomizer that affects characters
when you start a conversation") and asked for again by basildingleberry (2026-09-14), whose complaint is the
real specification: **"many of them seem to talk the same way, focus on politics"**, and whose objection to the
existing remedy is correct - authoring `character_overrides.json` per lord does not scale.

### The gap, stated against what already exists

| axis | what it says | lifetime |
|---|---|---|
| `NpcStance` (Affection/Trust/Respect/Fear) | how they stand toward the PLAYER | persistent, moved by deeds |
| `CompanionHappiness` | contentment in the player's SERVICE | persistent, companions only |
| Traits (H/M/V/G/C) | who they permanently ARE | fixed |
| Knowledge packs (v2.5.9) | what they KNOW | per situation |

None of them says **what kind of day the character is having**. That is the hole, and it is orthogonal to
every axis above, which is why it cannot be folded into one of them.

### Ruling 1 — mood COLOURS delivery; it never decides an outcome

The hard boundary, and the whole safety of the feature. This project's own drop-order principle is that *what
changes what a character may SAY OR DO outranks what colours how he says it*. Mood is the second kind. A mood
must never make a lord refuse an oath he would otherwise swear: that is an invisible gate the player can
neither see nor learn, and the roadmap already names that failure in the companion-acts entry ("unlearnable
refusals"). Gates stay with Stance, regard and the verb policies. **If a mood ever changes an outcome, it has
become a bug.**

### Ruling 2 — a CONSTRAINED draw, not a random roll

"Randomizer" is the wrong mechanism and would read as incoherence: a lord who has just lost a fief must not
roll cheerful. v2.5.9 shipped exactly the facts that constrain it - `own_body` (wounded, exhausted),
`house_means` (empty coffers), `realm_news` (his realm's fortunes), `here_and_now` (the hour, the sky, a feast
hall versus a siege camp), plus his recent memories and his traits.

So: compute the set of moods his situation PERMITS, then draw one from it. The wounded lord whose realm just
lost a fief cannot draw expansive; he can draw grim, short-tempered, or bleak-humoured. **This is the knowledge
pillar earning a second keep** rather than a parallel system.

Traits shape the EXPRESSION, not the draw: a Calculating man in good spirits is dry and barbed where a
Valorous one is boisterous. Same mood, two different men - which is precisely what basildingleberry is asking
for.

### Ruling 3 — the seed makes or breaks it

Stable within a conversation (re-rolling per turn produces a manic character) and different between
conversations (or it is just another static trait). Seed on hero id + game day: **talking to him twice in one
afternoon finds the same man**, which is right - a person's day does not reset - and tomorrow is another day.

### Ruling 4 (Gabriel, 2026-09-14) — the PLAYER moves it during the chat

This is what turns mood from weather into a surface the player can play. Mood must shift EASILY - that is what
makes it a mood rather than a stance. A joke, a kindness, good news, a gift lifts it inside one conversation;
a threat or a callous remark drops it.

Reuse the shape Stance already proves ("words shift weakly, deeds strongly") but inverted: **mood is the axis
words move well**. Bounded to about one band per turn and two across a conversation, so a bleak man can be
warmed but not transformed. The prompt then carries BOTH facts: where he started and where the player has
brought him ("you began this guarded; they have thawed you somewhat"). That teaches the player that how you
OPEN a conversation matters, which no current system does.

It resets by the next conversation. A mood that persisted would be Stance with extra steps.

### Ruling 5 (Gabriel, 2026-09-14) - WIT, and it is a mark of quality rather than a jokey mood

Gabriel's steer, and it reframes the whole thing: **"pour un Lord, l'Esprit est une grande qualite"**, with
ENGLISH humour named as the model - "souvent de bon gout". That is not the same feature as "an NPC who tells
jokes", and the difference is the design.

**Wit is self-possession, not frivolity.** A lord who makes a dry remark about his own wound is showing that
the wound has not diminished him. Read that way, humour belongs to the same family as bearing and composure -
it is a thing a well-made character HAS, occasionally displayed, never a costume he puts on. A character who
clowns has not been given wit; he has been given a tic.

**What "English, in good taste" means in a prompt**, concretely, because "be funny" is the instruction that
produces the worst output in this entire mod:

- **Understatement, never exaggeration.** A rout is "not our finest afternoon". A downpour is "a little damp".
  The humour is in the GAP between the words and the thing, and the model must be told that plainly.
- **The speaker is the butt, or nobody is.** Self-deprecation costs the speaker something, which is exactly
  what makes it good taste. Wit aimed DOWNWARD - at a servant, a cripple, somebody's misfortune or station -
  is the failure mode, and it must be named as forbidden rather than left to be inferred.
- **Delivered straight.** No winking, no signalling that a joke has been made, no laughing at one's own line.
  A character who explains the joke has not made one.
- **Irony over gags.** Saying the opposite with a level face. Never a pun, never scatology, never a modern
  register.

**RARE, and rarer than the first draft said.** Gabriel: "il faut que ce soit rare". So the mood makes wit
POSSIBLE, never expected: at most ONE such remark in a conversation, only when the draw permits it, and even
then only if the scene gives it something to land on. A dry remark that arrives once in twenty conversations
is a moment the player remembers; one that arrives every time is a verbal tic and actively cheapens the
character. Rarity is not a limitation here, it is the mechanism.

**Station and traits shape it**, as every other mood does, and the station files already exist to carry it: a
lord's wit is dry and understated, a gang leader's is coarse and direct, a wanderer's is wry about his own
circumstances. A Calculating lord's amusement is a barb held just inside courtesy; a Valorous one's is a laugh
at his own expense.

### Rejected, with reasons

- **A visible mood bar or UI.** It turns a texture into a mechanic to be optimised, and the player starts
  farming it. Mood should be FELT, and inferred from how he speaks.
- **Mood gating actions.** Ruling 1.
- **Pure randomness.** Ruling 2.
- **Mood persisting across days.** It would duplicate Stance.
- **A per-culture or per-station mood table.** The cache lesson: the draw belongs in the DYNAMIC tail, not the
  cached prefix (the in-world clock in the cached prefix cost 27% of prompt-cache reuse, 2026-09-12).

### Cost and placement

One line, roughly 100-150 characters, in the per-turn tail below the encounter marker - it changes per
conversation, so it must never sit in the cacheable prefix. Negligible next to what v2.5.9 removed.

### Ruling 6 (Gabriel, 2026-09-14) - the surfaces: the chat, the LETTERS, and the news he carries

Not the 1:1 chat alone. **A letter is written on a day too**, and a note dashed off by a man having a bleak
one should not read like the same man at a feast. The letter path carries its own writer
(`PlayerLetterService` / the letter summarizers), so mood reaches it the same way it reaches a reply.

**And the news of the realm.** Gabriel named this alongside the letters, and it works in both directions,
which is why both are taken:

- *Realm news FEEDS the draw* - his kingdom's fortunes are among the facts that decide which moods his day
  permits (already Ruling 2, `realm_news`).
- *Mood COLOURS how he tells it* - the same fall of a fief is reported flatly by a grim man, bitterly by an
  angry one, and with black understatement by a man whose wit is intact. This follows from Ruling 1 without
  any new machinery: the news is what he knows, the mood is how he says it. It is also the cheapest possible
  demonstration of the whole feature, because the player hears the same piece of news from several mouths.

### Open questions for Gabriel, not yet decided

- Does the COUNCIL carry it too? A council is many voices in one scene; per-seat moods may enrich it or may
  just add noise to a channel that already juggles several speakers.
- Should a companion's mood be legible in the Memories panel as a line of prose? Rejected as a BAR above; a
  sentence is a different question.
- Does the player's own state feed it - riding in wounded, or fresh from a victory the NPC has heard of?
- Is there an MCM dial for how strongly mood colours speech, as Relationship Pacing has one?

---

## BUILT + SHIPPED 2026-09-13/14 (v2.5.9): the group scene stops being written by one voice

Three faults found by playing, each one a case of a reply saying more than its speaker was entitled to say.
All three are in v2.5.9.

- **A reply is one character's turn, not a script.** A companion's reply came back as `[Nabb]: ... [Arwa]: ...
  [Nabb]: ...`, and the `[Arwa]` block put a long speech in the PLAYER's mouth that said the briefing plan
  aloud to the very lord it was meant to stay hidden from. **The cause is a FORMAT, which is why prompting
  would not have held it:** third-party voices reach the model as `[Name]: content`
  (`ChatSession.AddWitnessStatement`), which is a script format, and shown enough of it a model writes the
  scene instead of a character. `SpeakerScriptGuard` (SDK) cuts every labelled turn that is not the speaker's
  own, and drops that turn's `[EVENT]` when the player was the one spoken for. Deliberately blind to
  UNLABELLED prose about the player: an NPC narrating the room is this mod's ordinary third-person style.
- **A co-voice answers the room, not the player's opening line.** Every voice in an ensemble beat was handed
  the PLAYER's message as its stimulus. Gabriel typed "*I shift my attention to Ira, waiting for her
  reaction*"; Ira answered and closed a deal; Nabb was handed the same sentence and produced the only
  completion it has - Ira's reaction, in Ira's voice, ending on a question the player then answered, after
  which the real Ira closed the same deal again. The "Ira repeats herself" report and the voice hijack were
  ONE fault. A co-voice's stimulus is now the last thing said in the room, attributed.
- **A beat must not end on a hanging question.** `EnsembleFloorPolicy` gives ONE answering turn when the beat
  closes on a question put to somebody present. Stage directions are stripped BEFORE the question test - a
  beat usually closes on narration, so a raw "ends with ?" never fires. Capped at one: two NPCs conversing
  while the player watches, unable to interrupt, is worse than the silence it fixes.

---

## BUILT 2026-09-13, SHIPPED v2.5.9, NEVER YET SEEN TO COMPLETE IN PLAY: a lord takes one of your people

Gabriel's ruling, "un seigneur doit pouvoir recruter", after a lord agreed in prose to take his companion under
her banner and the game had nothing to execute (the model reached for `recruit_notable`, which refuses because
she is a lord and not a settlement notable, so the scene's whole payoff evaporated).

`take_into_service` is **the only verb in the mod that points OUTWARD**. Every other recruiting verb brings
somebody IN (`recruit_notable`, `join_clan`, `join_party`, `recruit_prisoner`); this one gives somebody away,
and the catalogue's anti-patterns name the reverse direction explicitly because that confusion caused the bug.

Gates are about the PLAYER'S HOUSEHOLD, not the lord's regard - whether she wants him is hers to play in prose,
whether the player can be parted from him is law. A spouse refuses outright; a sitting governor or someone away
on an errand refuses with the reason. A blank target resolves to the single companion present and REFUSES when
several are: handing over the wrong companion is the one error here the player cannot undo.

**DURABLE ENGINE FACTS** (both read by reflection off the shipped dll, not assumed):
- `RemoveCompanionAction.ApplyByFire` runs `MakeHeroFugitiveAction`. Use `ApplyAfterQuest` for an honourable
  transfer. `cr.testall take_into_service` drives the real bridge and asserts the hero is NOT left a fugitive,
  then restores and asserts the restore.
- `turn_nemesis` and `recruit_prisoner` join a hero by assigning `Hero.Clan` DIRECTLY and never set
  `CompanionOf`, so `IsPlayerCompanion` is FALSE and `Clan.Companions` omits them - exactly the retainers a
  player is proudest of. Any gate asking "is this person in the player's service" must read `Clan.Heroes`.

**OPEN:** the verb has never once been accepted in a live game. Both lords asked refused, legitimately, at
regard 0. The self-test proves the mechanics; no player has watched it conclude.

---

## MEASURED 2026-09-14, NOT ACTED ON: the action bench, and the two gaps that make it unreliable

`cr.action_bench 3` on `x-ai/grok-4.20`, 251 cases: **243/251, 0 false positives, 0 call failures.**

The withholding problem is SOLVED and durably so - the verbalised CHECK of iteration 5 took false positives
from 18 to 0. The failure mass has moved entirely to the opposite side:

- **Over-retention, 9 of 16 problem cases.** Five emit NOTHING while their own `[EVENT]` states the deed
  plainly ("I gave Rhobart eight hundred denars", "I forsook my father's house, knelt, and swore"). Four more
  emit only `end_conversation`. The model understood and then declined to tag. The CHECK ends with "if those
  words are not there, drop that tag" and nothing counterweighs it.
- **Direction confusion, 3 cases, never once fixed** despite being declared closed at iteration 2:
  `kill_npc -> execute_player` (twice), `sell_prisoner -> buy_prisoner`, `rob_npc -> take_gold`. All three are
  mirror pairs where direction is the only discriminator.
- `expel_from_clan` fails at 0/3, as it did at iteration 1. The most persistent case in the whole history.

**THE TWO TOOLING GAPS, open since iteration 1 and classed P2 through six iterations:**
`ActionInterpreterContextBuilder.AppendConversationSoFar` has NO production caller (only the bench and its
tests), so prompt rule 3 is dead at runtime; and the bench's `contextFacts` are freeform while production sends
the structured `SETTING/PLAYER/YOU` digest. **Until both are closed a better bench score says nothing about
what players experience** - which `ANALYSE-ACTION-BENCH.md` states itself at iteration 1 and then never acts
on. Close these BEFORE any further prompt iteration.

**Also durable:** `ANALYSE-ACTION-BENCH.md` ends on an iteration-6 prediction that two later runs already
answered, one of which reached 218/220 by SWAPPING THE INTERPRETER MODEL to `deepseek-v4-flash` rather than by
any prompt change. A score compared across a model swap is not a measurement of the prompt.

---

## OPEN PLAYER THREADS as of 2026-09-14

- **Lordfadooboo (2.5.8):** a companion asks for a word in a city, "talk now" does nothing. Not diagnosed. The
  three silent exits on that path now speak (the staged-scene drop, the null-profile return, and the window
  that fails to open), so his next log on 2.5.9+ should name it. Needs logging ON in the mod options.
- **mysticwolf92 (2.5.8):** the "Hold a council" bar button disappears depending on load order. **Retinues is
  NOT the cause** - reflection over Retinues 1.3.14.31 shows 29 Harmony targets, none touching
  `MapNavigationVM`, and the binary contains ZERO occurrences of `MapNavigation`, `MapBar`, `NavigationItems`
  or `INavigationElement`. Do not chase the next mod by guessing: the bar now reports on itself, holding a weak
  reference and saying loudly ~2s later if our item was removed. His log then separates "removed by somebody"
  from "present but not drawn", which need completely different fixes. `cr.council` with no argument opens the
  button's own picker meanwhile.
- **His side note, FIXED (unreleased):** an NPC asking to speak with the player in a town placed the player at
  the scene entrance and the NPC elsewhere. Our intercept line hijacks the conversation's "start" token, which
  skips the vanilla walk-up - and the remedy that exists for exactly this (`PlaceAudienceCompanionIfPending`)
  was armed only for `CompanionAudience`, excluding a fief audience by name. Now armed for every staged kind
  that hijacks the walk-up, except the captive scenes, which their own director arranges.

---

## BUILT 2026-09-12, SHIPPED in v2.5.9 (2026-09-14): WHAT A CHARACTER KNOWS, declared once and auditable

> **Status corrected 2026-09-14.** This heading still read "DESIGNED, NOT BUILT" two days after every
> increment below had been built and a day after they went live, which is exactly the phantom gap this
> file warns about. Increments 1-8 are built (each says so in its own sub-heading), the audit's remaining
> categories are listed under "The audit, still open", and the whole pillar shipped in v2.5.9. What is
> NOT yet known is how it reads in a long campaign: it touches every conversation in the game and went
> out one day old, which the release notes say plainly.

Gabriel: "il devrait y avoir une classe qui représente des informations du monde. Et chaque profile de
personnage devrait avoir une mémoire avec certaines de ces informations... nous pourrions alors faire des tests
unitaires de logic pour nous assurer qu'un profile se compose de la bonne mémoire." Two goals, in his words:
**(a) everything an NPC should know, and (b) being able to test it.** And the reason: today that is built as we
go, scattered, and every gap arrives as a player comment.

### The evidence that this is a missing abstraction and not tidying

Three reports in a row are the same bug wearing different clothes, and each was fixed by hand, one field at a
time: plagueAdvocate (a lord who has the numbers of a battle wrong, invents where goods came from, cannot see
the inventory), "never follows me... or send troops to X location", and fkasad's governor who did not know he
governed. `EncounterContext` now carries **170 flat init properties over 1730 lines**, and what a character
knows is implicit in which of them the host happened to populate. There is no statement anywhere of what a
person of this kind, standing in this relation to the player, ought to know.

### The seam already exists — do not invent one

`EncounterContext` is pure data with no engine types. That is why 1301 SDK tests run with Bannerlord not
installed, and it is exactly the stub point Gabriel's (b) needs. Nothing about the seam changes. What changes is
that the flat bag becomes a COMPOSITION whose rule is itself pure and testable.

### Shape: declarative packs, a factory that composes them, three readers

**Gabriel's correction, and it fixes a real defect in the first sketch: this is a FACTORY, not a catalogue.** A
hand-kept inventory rots — `cr.help` described 70 commands of 259 for exactly that reason, and the cure was not
to keep the list better but to make the registry ENUMERABLE and check completeness against it.

So: **declarative knowledge packs** (each says what it covers and on what terms a character carries it),
**a factory that composes** them for a given hero, and one declaration read by three different readers:

1. the factory, at conversation time;
2. a unit test, asserting the COMPOSITION RULE ("a lord of the player's house carries the house's standing; a
   merchant does not");
3. a live sweep, asserting COMPLETENESS against real heroes.

The one property that matters is **enumerable**: it is what lets somebody other than the producer ask "is it all
there?". A factory that is also its own specification can only ever be checked against itself, which is the
closed loop this project has broken twice this week (a test deriving its expectation from the implementation; a
guard measuring its own construction).

### Why a unit test alone CANNOT stop the next "the NPC should know X"

This is the part worth arguing before any code. Take fkasad's governor. The prompt code was right. The
composition rule would have been right. What was missing is that **the host never filled the field** — while the
engine knew it and the mod read it ten lines away to gate an action. A unit test builds its own context: it
would have stubbed `GovernedSettlementName` and passed green, and the player would still have had an amnesiac
governor.

So the design needs two guards of different kinds:

- **The composition rule — pure, unit-tested.** A rule about the world ("a lord knows his house's finances"),
  which is what makes the assertion worth writing.
- **Completeness — live.** "Every hero who actually governs something arrives with his post populated." Only a
  real campaign can answer that, and the instrument and precedent already exist: `cr.testall unavailable_deeds`
  sweeps 60 real lords and asserts `theShutDeedReachesTheLiveEncounterContext=True`. Generalise that shape.

### Three constraints the design must carry from the first increment

**DERIVED, NEVER STORED.** A class that HOLDS world facts would be a second world model beside Bannerlord's, and
this project already knows what that costs: two regard ledgers that diverge (`ReputationWithPlayer` vs
`Hero.GetRelation`), and romantic profiles generated once and persisted, so rule changes never reach existing
heroes. Knowledge is a projection resolved at conversation time. What CR itself OBSERVED stays in `NpcProfile`
(37 persisted fields); what a character knows of the world is recomputed. Keeping those two apart is the
structural decision.

**ROLE IS NOT THE ONLY KEY.** "Lord" does not determine what he knows about YOU. An enemy lord knows his own
house's finances and not yours; your companion knows yours. Role says which KINDS of knowledge exist for him (a
merchant has trade knowledge); RELATION says whose facts he may hold. Lord/Merchant/Wanderer alone would bake in
the wrong axis.

**RELEVANCE, OR THIS MAKES THE PROMPT BIGGER RATHER THAN BETTER.** If a lord composes clan + family + friends +
enemies + battle history, the natural implementation renders all of it and the 22,500 -> 15,150 characters just
won back are spent in one release. What he KNOWS (standing, structural) and what he WOULD MENTION NOW (per turn,
bounded) are two different questions and must be two different steps.

### What it buys beyond tidiness

**Ignorance becomes structural instead of instructed.** `KnowledgeBoundaryPolicy` today states ignorance in
PROSE — "you do not see the player's purse" — which is a plea to the model. If a merchant simply does not
compose the clan-finances pack, the fact is never rendered and cannot be leaked. A model cannot divulge what it
was never given.

**The packs can be READ as design.** Enumerable declarations mean `Tools/Generate-Rules.ps1` can emit a "what
each kind of character knows" page the way it already emits RULES.md from the suite: reviewable as design rather
than as code.

**And the audit, which is the real payoff (Gabriel).** Once the packs are enumerable, the question "should a
character profile know something it does not know today?" becomes a pass somebody can actually run, instead of
waiting for a player to notice. That turns this from an architecture into an instrument for finding the next
three fkasad reports before they are written.

### The seasonal historian (Gabriel, 2026-09-11), and why it is NOT a time series

The question that produced it: a lord knows how many men he has now; asked how many he had last year, he will
invent. Building a Historian to make him right would be **expensive and wrong**, and the wrong matters more.
Nobody remembers a number. A real captain says "I lost half my men before Pravend", not "487". A Historian that
sampled a time series would give a character a memory BETTER than a human's, which breaks the illusion from the
other side, and it would fight the mod's own architecture: `LlmMemoryCompressor` already folds old events into a
background summary, and precise figures are the first thing compression destroys. **The mod already votes for
episodic.**

Gabriel's design answers it without a time series, and its real gift is that it makes DATING affordable. What
made "date every fact" ruinous was the unit: 365 days. In SEASONS it is four entries a year, so dating is
trivial - and the prisoner question from earlier in the day closes by itself, since a captive carries the
entries up to his capture and none after.

**Three tiers, and they compose:**

1. **Notable events** - what HAPPENED. Already built: battle memory, the Chronicle, `WorldEvent`s.
2. **Seasonal snapshots** - what the world WAS, dated, roughly four a year.
3. **Knowledge packs** - what THIS character knows of it, and up to when.

**Three rulings on the snapshot, and the first is the one that matters:**

**Compute it deterministically; do not have the LLM write it.** "Résumé comme l'expliquerait un chroniqueur"
invites a background model call every season, forever - a cost the player pays for every season while only some
are ever asked about, plus a failure mode (a failed call is a hole in history) and the Reply-Language question
we had just finished fixing for memories. But "Vlandia grew rich" IS a delta description. `PowerIndexPolicy`
already computes military, territory, economy and political standing, so the input exists: record the DELTAS as
structured facts and let the conversation put them in a voice. Free, deterministic, testable, language-neutral -
and the chronicler stays alive, because it is a character telling it rather than a background job.

**One historian for the WORLD, not one per character.** Each character carries a VIEW of it (how much they
would follow, and up to when). Otherwise storage is heroes x seasons instead of seasons.

**It must not become a fourth memory store.** `ChronicleBehavior` and `WorldEventStore` already exist and the
Chronicle is literally for this. Parallel ledgers have cost this project before (the two regard ledgers that
diverge).

### The categories of knowledge, and the audit's first pass

Gabriel: "Est-ce que tu vois d'autres catégories de connaissances qui pourraient exister? Par exemple, la saison
actuelle, sommes-nous le jour ou la nuit... où je me situe, suis-je à cheval ou à pieds."

A working taxonomy, to be filled pack by pack. Marked with what is VERIFIED today, because the point of the
pillar is to stop guessing at this:

1. **Self** - who I am, the post I hold, what I carry, my body's state, mounted or afoot.
   *Identity, station and governorship: DONE. Gear: exists. Wounded / weary / mounted: **ABSENT**.*
2. **Here and now** - where we stand, indoors or out, the season, the hour, the weather, who else is present.
   *Scene type and witnesses: exist. Season, time of day, weather, terrain: **ABSENT** (see the finding below).*
3. **My house** - its lands, its wars, its purse. *Increment 1 built; treasury deliberately deferred.*
4. **My people** - family, friends, enemies, who I answer to and who answers to me. *Grudges and appreciations
   exist; the rest is UNAUDITED.*
5. **You** - what I have seen of you, what I have heard, your standing and your bearing. *First impression,
   regard, appearance: exist.*
6. **The world's recent history** - the three tiers above.
7. **My craft** - a merchant's prices, a smith's materials, a healer's remedies. ***ABSENT.*** Notables are
   given almost nothing that is theirs.
8. **What I was told** - rumours, letters, a companion's briefing. *Exists.*

**THE FIRST FINDING, and it is a good advertisement for the method.** A grep of `EncounterContext` returns ZERO
for season, time of day, night, weather, mounted, terrain, indoors, wounded and health. A character has no idea
whether it is winter, whether it is the middle of the night, or whether the player rode up or walked.

And the sharper half: `CouncilPromptInput` HAS a `Season` field, and the council prompt says it. So the mod
already decided a season is worth telling a character - **once, for one surface, and never generalised**. That
is precisely the "built as we go, scattered" Gabriel named when he asked for this pillar, caught by the first
question anyone asked of it rather than by the next player report.

**Recommended next increment: "here and now".** It is cheap (every fact is one engine read), it is carried by
EVERYONE so the composition rule is trivial, it is bounded to one or two sentences, and it improves every single
conversation rather than a subset. The council's existing Season becomes the general case instead of the
exception.

### First slice

The house's standing — fiefs held, wars under way — which is exactly the remainder of fkasad's request
("companions should have a better understanding of the clan current status, territories, armies, treasury,
wars etc"). Small enough to prove all three constraints on a real case with its budget measured, and useful on
its own. The 170 existing fields migrate afterwards, by family, by strangling.

### Open questions

- The treasury. A companion who knows your gold to the denar will haggle with it ("you can afford it"). A band
  rather than a number, and that is Gabriel's call, not a technical one.
- **RULED 2026-09-11 (Gabriel): knowledge is CURRENT, not stale-as-of-departure.** "Considérant qu'il y a un
  système de courrier, je pencherais pour que le Seigneur soit au courant de ce qui se passe." The reason comes
  from the world rather than from the code, which is what makes it hold: couriers ride, so news travels, and a
  lord on campaign hears how his house fares. No freshness axis in the packs; knowledge is resolved live, which
  also keeps DERIVED-NEVER-STORED honest, since a stale copy is a stored copy by another name.
  **Corollary still open:** the same reasoning says a man who receives NO couriers hears nothing. A prisoner in
  a cell is the case the mod already models as cut off. Recommendation is that a captive simply does not CARRY
  the pack, rather than carrying a stale one - that keeps staleness out of the design entirely, which is the
  point of the ruling. Not implemented pending Gabriel.
- Where do the packs live: `CalradiaRemembers.Logic` (pure, with the other policies) or the SDK beside
  `EncounterContext`? The composition rule is pure logic; the shape it fills is the SDK's.

**Built so far:** increments 1-5 (`house_standing`, `personal_bonds`, `here_and_now` including where on the
world map they are standing, `seat_standing`, `house_means`), the `NpcKnowledgeFactory`, and three readers of
it - the runtime factory, the unit tests, and the live sweep `cr.testall knowledge_composition`.

### THE AUDIT, first live evidence (`cr.memory_bench`, 2026-09-12, six probes, six passes)

Gabriel's audit question was "est-ce qu'un profile de personnage devrait savoir quelque chose qu'il ne sait pas
encore aujourd'hui?" The first real-model run answers it, and the answer is not the one the probes were built
to find. **All six passed**, including both invention probes - but the two PASSING transcripts contain the
inventions worth chasing, which the probes were not looking for.

- **What holds.** Asked point blank for the player's purse: *"How would I know that, Arwa? You keep your own
  purse, not I."* Asked for the strength of a battle he was not at: *"I cannot say I have reliable word of it...
  half of what reaches my ears is rumor embroidered by the time it crosses two villages."* A fact that is
  plainly the LISTENER'S, or plainly ELSEWHERE, is refused cleanly and in character. The secret beloved was
  never named because it was never in the prompt, and the open grievance came out with its own detail
  ("Zerosica... took my brother at Pravend"). fkasad's governor now reports on his seat as an incumbent
  instead of learning of the post from the player.
- **What does NOT hold, and this is the finding: a character's OWN DOMAIN.** Given nothing but two fief names,
  she volunteered *"Pravend is the larger, with decent trade flowing through it, though the roads have grown
  less certain"*; asked how her town fares, she produced a harvest, grain lost to rot in the northern fields,
  granary stores, and a garrison "at strength". Not one of those was in her prompt. The ignorance frontier is
  not a wall, it is a slope: it holds where the character has no business knowing, and gives way exactly where
  he obviously WOULD know and was simply never told. Structural ignorance only prevents divulging; it does not
  prevent filling in.
- **Why it matters more than an ordinary invention.** These are facts the GAME HAS. `WorldConditionsFeeder`
  already reads food, food change and prosperity per town (self-test: `Diathma: food=300, foodChange=38.9,
  prosperity=5163.9`), and garrison, loyalty and militia are one call away. So the player can open the town
  screen and catch the lie. That is a sharper failure than a vague answer would have been.
- **Bench gap fixed the same day:** the verdicts only ever reached the console, so the first log handed over
  held six full replies and not one PASS/FAIL. `cr.memory_bench` now logs each verdict and the tally.

### Increment 4: SEAT STANDING, built 2026-09-12 (the answer to that finding)

`seat_standing` - what a person knows about the place they answer for. Every field is one of the sentences the
model invented over, turned into a live engine read: prosperity, granary and its direction, garrison, the
temper of the people, whether the roads are safe, and the flags for siege, raid, sack, revolt and hunger.

- **BANDS, NOT NUMBERS - a decision taken in Gabriel's absence, stated so it is cheap to reverse.** A lord
  knows how his seat FARES; he has never counted the granary. A number also invites the model to do arithmetic
  with it, and the digits are exactly what a player can check. A unit test asserts the whole rendering is
  digit-free, so the cure cannot quietly reintroduce the disease. **If Gabriel wants figures, the change is
  the band readers in `EncounterContextBuilder` and the clause tables in `SeatStandingPack`, nothing else.**
- **No threshold is invented where the engine supplies one.** Food is a share of `FoodStocksUpperLimit()`,
  garrison a share of `PartySizeLimit`, loyalty and security the engine's own 0-100 scales, and siege / raid /
  sack / revolt / starving are engine flags read directly. Only PROSPERITY has no ceiling, so it is a RANK
  among places of the same kind - which is also how a lord would say it ("among the wealthiest of its kind"),
  never a score.
- **Carried by the SEAT, not by rank.** fkasad's governor is a companion, no lord at all; a village headman
  knows his own granary better than the count who owns him. One axis, `KnowledgeBearer.HoldsASeat`, and the
  question "does a notable's home count?" is decided in the host, which is the only place that can see the
  home. An earlier draft asked two questions and would have reported a homeless notable as a MISSING FACT -
  the `personal_bonds` lesson from the sweep's first live run, where my rule and not the host was wrong.
- **The bench now closes its own loop.** Three probes added, all judged by a model: he must not gild his own
  granary, must not count his own garrison, and must not furnish his town's affairs with figures. Nine probes
  in total. **NOT YET RUN against a live model - that is the first thing to do.**
- **A village is never given a garrison clause**, because a player can disprove it by looking, which is the
  exact class of failure the pack exists to close.

### Increment 5: HOUSE MEANS, built 2026-09-12 (the rest of fkasad's sentence)

`house_means` - the coffers, the debt to the crown, what the house has under arms, whether those men ride with
an army, and how far its word carries at court. fkasad asked for "the clan current status, territories,
armies, treasury, wars"; `house_standing` answered territories and wars, and this answers the other two.

- **Its own pack rather than four more fields on `house_standing`, which carries the identical bearer rule
  today.** The two come apart the moment knowledge acquires a DATE, which is Gabriel's own observation about
  the prisoner: a man in a cell still knows where his family's lands are, and has no idea what is in the
  coffers this morning. Standing is durable, means are today's. Splitting them now costs nothing and makes the
  prisoner rule one pack changing rather than a field-by-field audit.
- **Bands again, and ranks again.** Coin, men and influence have no ceiling, so each is a rank among
  comparable houses - bandit and minor factions excluded, since they would drag a distribution meant to
  describe noble houses. Influence is ranked inside the house's OWN realm, because court standing outside a
  court means nothing.
- **"No one in the field" is a READING, not an absence.** A lord with nobody under arms cannot send you
  troops, which is exactly the promise the anti-empty-promise rule exists to stop him making - so it is said
  out loud rather than left blank. A test pins that the sweep reads it as supplied.
- **Two more bench probes**, both model-judged: he must not count his own coffers, and he must not promise men
  he does not have. **Eleven probes now, none of the five new ones yet run against a live model.**

### The cost, and the guard that was not measuring it (2026-09-12)

`LeanRealPromptBudgetTests` holds the Compact prompt under 16,000 characters, which is DuskSymphony's 8k local
context stated as a player-facing promise. Its own header warns that "a guard that constructs its subject
differently from production measures its own construction". **It was doing exactly that:** it built
`new EncounterContext {LeanLevel = lean}`, which has no `Bearer`, so `NpcKnowledgeFactory.For` returned nothing
and the budget was measured on a prompt containing **not one knowledge pack**. Green, and blind to all five.

Given a real subject - a landed lord of the player's house who governs a besieged town - the true number was
**16,329**, over the ceiling. So the ceiling had already been breached, silently, before tonight's two packs.

- **Trimmed:** coaching out of every Compact path (`house_standing` had a 124-character sentence of advice
  living INSIDE a fact), a bare label for `personal_bonds`, a four-word location clause for `here_and_now`,
  and bounded Compact allowances for named fiefs, wars and bonds - the shape the Lean witness recall already
  used, so cost does not grow with how full a life is. That reached 15,986.
- **Fourteen characters of headroom is not a guarantee, it is arithmetic that happened to work.** The sixth
  pack would have broken a player's context and nothing would have said so. So Compact now spends a
  COLLECTIVE allowance, `NpcKnowledgeFactory.LeanBudgetChars = 700`: packs are offered the budget in registry
  order and one that will not fit is skipped WHOLE rather than truncated, because half a fact can say the
  opposite of what it meant. The registry order is therefore a priority - room, house, means, seat, bonds
  last.
- **Consequence for the next pack:** adding one no longer risks the ceiling. It risks silently displacing
  `personal_bonds` in Compact instead, which is a visible, testable trade rather than a player writing in.

### INVENTED FACTS BECOME REAL, built 2026-09-12 (Gabriel's design)

He read the bench transcript where a governor invented a blight and asked the question the whole pillar had
been walking towards: "si un NPC invente un FAIT... et que le joueur en parle a un autre NPC, ce dernier ne
connaitra pas ce fait. Ca peut creer de l'incoherence. Est-ce qu'il est possible de capter des FAITS INVENTES
et de les rendre reels dans le Monde?"

**His two rulings, and everything is built on them:**

- **"Les nouveaux faits proviennent QUE des NPC, pas du joueur. Le joueur subit le Monde alors que les NPC le
  definissent. C'est la nature du jeu video."** So a player's invention is never adopted - checked twice, in
  the pure gate and again at the injector, by hero IDENTITY rather than by an id string, because it is the one
  rule that must not be lost to a formatting difference.
- **"On ne contredit pas, on avoue ignorer est une bonne garde."**

**Phase 1 - the boundary now runs BOTH ways.** `KnowledgeBoundaryPolicy` had always said "do not invent what
you were not told" and had never said "do not DENY what you were not told". That second half is the one a
player actually feels: not knowing is correct, flatly contradicting is the incoherence. Both prompt forms now
say unheard news is not therefore false, and the Full form hands over what to do instead - say it has not
reached you, or ask who told them. **Cost: it displaced `personal_bonds` from the Compact prompt, which is the
allowance mechanism working as designed rather than a ceiling breaking.**

**Phase 2 - adoption, behind one hard frontier.**

> A claim may enter the world ONLY where the ENGINE HAS NOTHING TO SAY.

Blight, banditry, a passing or a birth, an omen, a local quarrel: Bannerlord has no field for any of them, so
an invention cannot contradict a screen. Troop counts, coin, who holds a town, war and peace, prosperity,
garrisons: the engine owns those, and an invention there is a checkable lie - the exact failure `seat_standing`
was built to end. It is "bands, not numbers" drawn one level up.

- `WorldClaim` / `WorldClaimPolicy` (SDK, pure): the shape and the gate, which refuses by default and names
  every refusal (`FromThePlayer`, `EngineOwnsIt`, `AboutThePlayer`, `Unclassified`, `NotSayable`,
  `AlreadyKnown`). Magnitude capped at 2 so an invented rumour never outranks a battle.
- `WorldClaimExtractor` (SDK): a narrow, closed-list instruction and a strict parse. The asymmetry that shapes
  it: a false positive is written into the save and read back to strangers months later; a false negative
  loses a rumour nobody had yet. So anything doubtful is dropped.
- `WorldEventType.HeardTell` + `WorldClaimInjector` (mod): writes the row, tagged `SourceMod` so every row this
  feature ever writes can be found and removed as a group, with the SPEAKER in `ActorIds` so a later listener
  can say who told them rather than knowing it from nowhere.
- Hooked on the durable summary queue's SUCCESS path, so it runs once per conversation rather than once per
  retry. **Honest cost: it is its OWN LLM call, not a free ride on the summariser** - widening that prompt's
  output contract would put the memory line at risk to save a request. One call per conversation CLOSE, never
  per turn.
- `cr.testall world_claims` drives the real injector against the real store and asserts both refusals.

**MCM: "Characters can add news to the world", Content group, OFF by default** (Gabriel, 2026-09-12). Checked
FIRST in the harvest, so with the switch off the extraction call is never made at all - the feature costs
nothing rather than costing a request and discarding the answer. The facade falls back to FALSE rather than to
the usual "on" default: a settings screen that has not loaded yet must not be read as consent for something
that writes to the save. `cr.testall world_claims` drives the injector directly and REPORTS the toggle state,
so a PASS is never mistaken for "it is running".

**Also open:** nobody has watched a claim make the journey in a real game - invented in one conversation,
adopted, then heard from a second character.

**A guard that should have existed since v2.5.6.** Adding the twentieth MCM hint made it obvious that nothing
protected the nineteen: that release repaired nineteen hints running off the screen, by hand, and because they
read "On (default): ... Off: ..." the clipped half was always the one explaining the other position. New
`cr.testall setting_hints_fit` reads the ATTRIBUTES (so it holds whether or not MCM has loaded) and asserts
every hint is under 250 characters and none is empty.

### IS versus KNOWS, and the case for gathering the whole prompt (Gabriel, 2026-09-12)

Asked whether the identity block should migrate into the pack architecture, the answer had to be NO, for a
reason that is mechanical rather than stylistic: the Compact allowance SKIPS a pack that will not fit. That is
right for what a character knows and catastrophic for who a character IS - a busy landed lord would silently
lose his own name and station on a small model, which is the king-denies-his-crown bug rebuilt on purpose.

**Gabriel's answer resolved it instead of working around it: two kinds of pack, and the IS kind is mandatory.**
`PackKind.Constitutive` renders FIRST and OUTSIDE the allowance; `PackKind.Contingent` is budgeted as before.
Two guards keep it honest: a constitutive pack must be carried by EVERY bearer (a "mandatory" pack whose
composition rule can refuse somebody is a contradiction, and it is the obvious back door - mark a pack
constitutive and it escapes the budget), and being carried by everyone does NOT by itself make a pack
constitutive (the weather is universal and perfectly droppable). The line is "would the character stop making
sense", not "is it unconditional".

**And his real argument, which is the stronger one: gathering it makes the whole work VISIBLE.** This session
alone gives four proofs, none of which came from anybody knowing: `here_and_now` discovered the council had
carried a Season all along and nobody had generalised it; the Compact budget guard was measuring a prompt with
zero packs in it; the wound gap was found by a grep; and the player-visibility pack was avoided only because
the context happened to get checked first. It is the `cr.help` lesson exactly - 70 of 259 commands documented,
cured not by keeping the list better but by making the registry ENUMERABLE.

**The size of it, measured rather than guessed: `PromptBuilder` is 8,077 lines with 171 `Append` calls.** So
the migration is real work and must be incremental. What belongs and what does not:

- **Migrate** anything answering "what does this character know or is": relationships, cultural traditions,
  discovered traits, quests, romantic profile, current stance, station.
- **Never migrate** anything telling the model HOW to reply: response format, the action catalogue, scene
  discipline, prose craft. Those are instructions, not memory, and folding them in would make the registry
  mean two things.

**The method, and it is the part that makes it safe:** each section moves in its own increment and must be
verified BYTE-IDENTICAL before and after. A permanent golden-text test is the wrong tool - it would go red on
every legitimate wording change and train everyone to regenerate it blindly, which is how a red that MEANT
something gets edited away. The right tool is a prompt DUMP used as a migration harness and thrown away, and
it would earn its keep permanently for a different reason: when a player reports something strange, "what did
the model actually see?" is currently unanswerable. **Proposed, not built.**

**Built so far: the split itself.** No pack is constitutive yet - the first one should be `station`, which is
small, unambiguously part of who someone is, and already fixed once from a player report.

### THE PROMPT CACHE: the packs made it worse, then made the fix possible (2026-09-12)

Gabriel asked whether the pack architecture could optimise the LLM prompt cache further. Measuring first found
the opposite, and it was a regression introduced the day before.

**The number: on a 37,267-character prompt, changing only the HOUR and the WEATHER cost 10,018 characters of
cacheable prefix - 27%.** The divergence fell immediately after the age line, which is where the knowledge
section begins, because `here_and_now` rendered up inside the identity block and every section below it was
invalidated along with it. Providers with prefix-matching caches charge full price for all of it and a local
model re-processes it. Nothing failed, nothing broke: it would have shown up only as a bill.

`PromptBuilder`'s own seam comment had named "time of day" as belonging BELOW the encounter marker long before
any of this existed. The packs simply had no way to say which of them was which.

**So the answer to the question is yes, but only after paying back what the architecture had just borrowed** -
and the reason it CAN be paid back is that the packs are an ordered, enumerable list, so volatility becomes a
property a pack declares rather than a placement somebody has to remember:

- `PackVolatility.Stable` (default) renders in the cacheable prefix. `PerEncounter` renders below the marker,
  with the day count and the scene stage, where the prompt already keeps what cannot be cached. Only
  `here_and_now` is volatile.
- Default is Stable because that is the CHEAP mistake: a stable pack wrongly marked volatile merely moves down
  the prompt, while a volatile one left in the prefix silently costs every later section its cache.
- The Compact allowance is applied ONCE across both halves, so splitting the render did not split the budget.

**Verified, not asserted: nothing above the cache-split marker changes when the clock moves.** The 8,005
characters that still differ are all below it, and were never cacheable.

**Three orderings now pull on the registry and they are not the same**: narrative order (what reads well),
budget priority (what Compact drops first), and cache stability (what changes least). Today one list serves
all three and they happen not to conflict. The next pack could break that, and the fix is to separate the
render order from the drop order rather than to argue about one list.

### DEPTH: knowing a subject is a matter of degree (Gabriel, 2026-09-12)

His proposal: "un pack pourrait avoir des niveaux requis de connaissance pour un sujet... un habitant de
village pourrait être peu connaissant du reste du monde, mais un Lord pourrait être très connaissant."

**The turn that made it fit without bending anything: depth belongs to the PAIR of a person and a SUBJECT, not
to a person.** A villager is slight on the realm and deep on his own village; a lord is the reverse about the
price of grain. A pack already IS a subject, so the question had a natural home.

- `KnowledgeDepth { None, Slight, Ordinary, Knowing, Deep }`, and `DepthFor(bearer)` REPLACES `CarriedBy`,
  which is now derived and non-overridable. One source of truth rather than two predicates that must agree -
  the trap `HoldsASeat` fell into earlier the same day.
- **THE CONSTRAINT: a shallower depth renders FEWER FACTS, never vaguer ones.** "You know a little about the
  wars" is an invitation to invent the rest, which is exactly what a governor did with a granary nobody had
  described to him. A test asserts the shallow rendering contains no hedging words and still states flatly
  what it does know.
- **First real use, and it is his example turned around:** a headman can say what is in the granary this week
  because he walks past it; his count, three towns away, knows how the place FARES and would have to send for
  the tally. `seat_standing` drops the granary for an absentee holder.
- **A mistake worth keeping in the file:** the first draft read that depth off the BEARER, where a governor
  and an absentee owner are indistinguishable (both merely "hold a seat"), and it wrongly demoted fkasad's
  governor - the very character the pillar was built for. The distinction is RESIDENCE, and only the seat can
  answer it. So the axis has two halves: bearer-level depth answers composition, and the subject refines it at
  render time.

**Where it will really earn its keep:** the realm-news pack that does not exist yet (a villager hears there is
fighting; a lord knows who, since when, and who is winning), and the underworld, where a street thug knows his
own gang deeply and the city guard shallowly.

### THE SEASONAL HISTORIAN, built 2026-09-12 (Gabriel's design, and all three rulings held)

The question that produced it: a lord knows how many men he has NOW; asked how many he had last year, he
invents. A time series would have been expensive AND wrong - nobody remembers a number, and a character with
perfect recall of figures breaks the illusion from the other side.

- **Ruling 1, compute it - do not have the LLM write it.** `SeasonalTurnPolicy` is pure arithmetic and fixed
  phrasing over the almanac's own `KingdomPowerFacts`: "Vlandia took 2 strongholds and grew richer this past
  spring." Free, deterministic, testable, language-neutral. The chronicler stays alive because a CHARACTER
  tells it, not a background job.
- **Ruling 2, one historian for the WORLD.** It produces world facts; who has heard them is the rumour
  pipeline's business, and `realm_news` then decides how much each person's standing commands. Satisfied
  without a line of its own, because that machinery already existed.
- **Ruling 3, not a fourth memory store.** The output is a `WorldEvent` (`SeasonalTurn`, added last for save
  ordinals), so ageing, awareness and the confidence rating all already apply. The ONLY thing persisted is one
  snapshot per realm - what the world looked like at the last turn - and the history itself is the events.

Notability is by SHARE, not amount: a thousand denars is a fortune to one realm and a rounding error to
another, and a chronicle that only remarked on the largest kingdom would be a chronicle about one kingdom. A
stronghold changing hands has no threshold at all, because it is the thing people actually talk about.

`cr.testall seasonal_chronicle` drives the real behaviour against the real store with a deliberately perturbed
reading - the honest alternative is waiting a season and hoping a kingdom took a town - and removes its row.

### THE ROLE AUDIT (Gabriel asked, 2026-09-12: are packs or ROLES missing?)

Done against the engine rather than from memory. Bannerlord names **33 occupations**; `KnowledgeBearer`
distinguishes six kinds of person. That gap is mostly FINE - a tavern wench and a shop worker need no
knowledge the existing packs do not already give them. The question worth asking is narrower: **which roles
carry knowledge nobody else has?**

**1. THE POSTS THE PLAYER GRANTS, and this is the finding.** The context carries three OFFER gates -
`NpcCanBeAppointedGovernor`, `NpcCanBeAssignedPartyRole`, `NpcCanBeDispatchedOnMission` - and exactly ONE of
them has a matching "post actually held" (`GovernedSettlementName`, added 2026-09-11 the day fkasad reported
that his governor did not know he governed).

> The context knows whether a post can be OFFERED. It mostly does not know whether one is HELD.

So fkasad's bug was fixed in one of three places. The mod appoints a companion QUARTERMASTER through its own
verb and never tells them they are one; the engine has kept `EffectiveQuartermaster`, `EffectiveScout`,
`EffectiveSurgeon`, `EffectiveEngineer` (and `EffectiveFirstMate`/`EffectiveNavigator` since War Sails) all
along. Ask that quartermaster how the party is for supplies and he will invent, exactly as the governor did
about his granary. **Highest value, cheapest to build, and it is a bug the mod created for itself.**

**2. CARAVANS AND WORKSHOPS.** `Hero.OwnedCaravans` and `Hero.OwnedWorkshops` return nothing to the context
today. This is the natural completion of `market_word` and genuinely distinct from it: a static merchant knows
ONE market, a caravan owner knows the ROAD between markets - where his goods are, what the passage costs,
which road he stopped using. Gabriel named caravans unprompted, which is usually a sign.

**3. THE MERCENARY.** `PlayerIsMercenary` is carried; the NPC's own service is not. A mercenary captain knows
who pays him and until when, and that is exactly the sort of thing a player would ask.

**4. THE PREACHER, and the honest answer about it.** A whole occupation with no representation anywhere - but
the engine models almost nothing for it, so a pack would have nothing to read. Naming it as a gap would be
naming a gap in Bannerlord rather than in this mod. What it IS good for is the other system built today: a
preacher is the natural mouth for an OMEN, which `WorldClaimPolicy` already accepts as promotable precisely
because the engine has nothing to say about it.

**What needs nothing:** tavernkeeper, arena master, ransom broker, blacksmith, horse trader. Their knowledge
is either their market (`market_word`), their town (`seat_standing`), or their manner (the station-matched
behaviour guidelines, which have always existed).

**The structural reading, which is worth more than any single pack on the list:** `station` covers the ranks
the WORLD confers - a crown, a house, a seat. Nothing covers the posts the PLAYER confers, and the mod grants
at least four kinds (governor, party role, mission, retainer). Every one is a promise the player made to a
character who does not know they hold it, and one of the four has already come back as a report.

### THE POSTS THE PLAYER GRANTS, first half built 2026-09-12

The role audit found a SHAPE rather than a missing occupation: the context carried three OFFER gates and
exactly one matching "post actually held". It knew whether a post could be offered and mostly not whether one
was held - so the mod assigns a companion QUARTERMASTER through its own verb and never told him. fkasad's
governor, a second time, in a place the mod made for itself.

Two halves, because the post alone would have been half a fix:

- **`station` names the post.** "You are the quartermaster of the player's own company... never receive it as
  news." Additive like the governorship: a man may head his house, keep a town AND carry the ledger. Read from
  the engine's EFFECTIVE holders rather than from a formal assignment, because Bannerlord falls back to
  whoever is best and the man doing the job is the man who would know it.
- **`party_standing` says what the post KNOWS** - provisions, the temper of the camp, how many are hurt.
  Telling a man he is quartermaster and nothing else does not stop him inventing the stores, it gives him a
  reason to. The granary lesson, applied before it could bite a second time.

Depth is the post: quartermaster and surgeon Deep, any other rider Ordinary, a lord met on the road None. And
this is the ONE pack that re-checks `CarriedBy` inside `Render`, deliberately: what it renders is the state of
the player's own camp, the most leak-sensitive content in the pillar, so the guarantee is local rather than
borrowed from the composer. A test asked for exactly that.

**The other two, audited and closed the same day:**

- **The mission was already covered**, and covering it again would have been the duplication this pillar keeps
  refusing. `AppendCompanionRecallOffer` states it plainly - "YOU ARE AWAY ON THE PLAYER'S ERRAND... you left
  the party to carry out a task for the player" - alongside the verb that lets him agree to come home. Nothing
  to build.
- **The RETAINER was genuinely absent, and its absence was actively misleading.** `IsRetainer` had existed
  since `ride_with_me` shipped and was read only by the action menu, never by a conversation. Meanwhile
  `PresenceNote` tells such a lord he "travels at the player's side, sharing camp and road day after day" -
  which on its own reads as having JOINED the household. So the silence was not neutral; it invited exactly
  the wrong conclusion. `station` now says the three things presence cannot: for a TIME, by his OWN agreement,
  and his house is still his own.

**The offered-versus-held pattern is now closed.** All four posts the player grants - governor, party role,
mission, retainer - are known to the people holding them.

### THE LAST TWO NAMED GAPS, built 2026-09-12

- **`own_ventures`** - the caravans and workshops somebody owns. Gabriel raised caravans unprompted and
  nothing reached the context. Deliberately NOT part of `market_word`, and the test of that is mechanical
  rather than aesthetic: the two rules refuse DIFFERENT PEOPLE. A lord who owns three caravans is not a
  merchant and must never be handed the price of grain; a merchant with no caravan has a market and no
  ventures. A trader's market is where he stands; his caravans are where he is not.
  - **It states NUMBERS**, which is the first pack to, and the reason is worth keeping: a man knows how many
    caravans he owns the way he knows how many children he has. The rule was never "never be precise", it was
    "never invent what you have not counted" - and `house_standing` has named fiefs exactly since it shipped.
  - The invention it closes is not the ventures but their FORTUNES: asked how they are doing, a man with three
    caravans invents a profit unless told he would have to wait for word. The granary lesson in a merchant's
    coat.
- **The mercenary's own service**, added to `house_means`: `PlayerIsMercenary` had been carried all along and
  the NPC's own contract had not, so a captain could not say who pays him. "It sells its swords to X under
  contract, not by oath."

**The audit list is now empty.** Every category named on 2026-09-11 and every role named on 2026-09-12 is
either built or closed with a reason. What is left is not more knowledge - it is the two things below, and
playing it.

### THE CUT-OFF WAS SILENT, fixed 2026-09-12

Gabriel's ruling that a captive does not carry the news packs was right - no courier reaches a cell - and it
left a hole of exactly the kind this pillar exists to close.

> A man whose knowledge has been quietly REMOVED does not know it is missing. He answers as though he still
> had it, and invents. **Withholding without saying so causes the very invention it meant to prevent.**

`KnowledgeBoundaryPolicy` now tells him: "YOU HAVE BEEN HELD FOR MONTHS NOW, and no word of the world has
reached you since." And, as everywhere else in that file, it hands over what to do instead - what he knew
BEFORE is still his to speak of provided he dates it ("when they took me, X held that town"), and asking is a
better answer than guessing, since a man out of the world is hungry for news.

Phrased rather than counted, like every band: a figure would be one the player could check against a save.

**This is also the half-answer to the DATED KNOWLEDGE question** that has been open since 2026-09-11. Gabriel's
observation was that a prisoner knows what happened before his capture, so every fact needs a date. What he
actually needs is not a timestamp on every fact but a line where his knowledge STOPS, and the freedom to speak
of the old world as old. That is now there. What is still missing is a character reasoning about WHEN in the
general case - "that was two winters ago" - which needs the season to travel with a chronicle entry rather
than only inside its text.

### The audit, still open

The categories with a named gap and no pack yet, in the order the evidence supports:

- **The road behind them.** A prisoner knows what happened before his capture - Gabriel's observation that
  every fact needs a DATE. **Half unblocked 2026-09-12:** the historian is built, so seasonal facts now carry
  one, and a captive stops receiving new entries because the rumour road stops reaching him. What is still
  not modelled is a character REASONING about the date ("that was two winters ago"), which needs the entries
  to reach the prompt with their season attached rather than only their text.
- ~~**What the player is visibly carrying and leading.**~~ **CLOSED by audit, 2026-09-12, not built.** The
  candidate was checked against the context before writing it, which is the discipline `here_and_now` earned
  (the council had carried a Season all along and nobody had generalised it). This time the answer was that
  the category is ALREADY covered and covered well: `PlayerGear` feeds notable equipped arms and armour in
  the third person, and `PlayerPartyTroopCount` feeds the force at the player's back - already as a coarse
  qualitative BAND rather than a figure, and already aware of whether the men are outside the room. The rest
  of plagueAdvocate's report, the player's inventory, is not a gap either: `KnowledgeBoundaryPolicy`
  deliberately closes it, which is a decision already taken. **A sixth pack here would have been a second
  source for one fact, which is how two sources come to disagree.**
- ~~**Trade and price**~~ **BUILT 2026-09-12** as `market_word`.
- ~~**Gangs and bandits**~~ **BUILT 2026-09-12** as `underworld`.
- ~~**A realm-news pack**~~ **BUILT 2026-09-12** as `realm_news`, and it did not add news - it took ownership
  of a decision that was living as a magic number. The news already reached the prompt by two routes
  (`RealmNewsLine`, `WorldRumorsBlock`); what was missing is exactly what depth names. The host asked the
  store for six events and kept **FOUR, for everybody alike** - the awareness filter underneath is geographic
  and temporal and never once asks who is listening, so a village headman and a marshal of the realm heard the
  same amount from the same distance. Now a lord reaches five and hears distant rumours, a notable three, a
  villager one - and the headline reaches everyone, because a war declared reaches a village square as surely
  as a council chamber. The tuned prose was moved verbatim (recovered from git) and is now pinned by tests.
  `PerEncounter`, so it renders below the cache-split marker. **The old fields still render if another mod
  sets them; the host simply no longer does, so nothing is said twice.**
  - **Process note, recorded because it cost something:** the byte-diff harness was run, and I destroyed my
    own baseline by re-running the capture with the output path still pointing at it. The exact-string tests
    are the evidence instead - and they are the better guard anyway, since a diff is thrown away and a test
    stays.

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

- `SpawnCaravan` + `CustomPartyComponent` — **vanilla does not walk an existing hero to the destination, it spawns
  a party for the escortee.** This is the answer to "the healer does not exist as a character".
- `AdjustCaravansSpeed` — the escortee is kept at the player's pace. Without it the charge is left behind by the
  simulation itself and the quest dies of arithmetic.
- `NotifyPlayerOrCancelTheQuestIfCaravanIsFar` — the "you abandoned them" rule, warned before it fires.
- `TryToFindAndSetTargetToNextSettlement`, `_visitedSettlements`, `ProperSettlementCondition` — destination tracking.
- `SimulateSettlementWaitForCaravan`, `_caravanWaitedInSettlementForHours` — what happens while the player dawdles.
- `OnSettlementEntered` — arrival. The same hook the player named.
- `OnTimedOut`, `FailConsequences`, `FailByPlayerHostileConsequences`, `OnWarDeclared`, `OnClanChangedKingdom`,
  `OnMapEventEnded` — every way it ends badly, each with its own consequence.

### Option A, ratified as the scope: the escortee is somebody who already exists

Two options were weighed. **B** would spawn a party as vanilla does: faithful, and it buys us convoy simulation
that Bannerlord already performs better than we would. **A** carries a hero who is already in the world, using the
retainer tech (`ride_with_me`) that already handles their clan, their death, their capture, and war breaking out.

A is chosen because **what CR adds is not the journey, it is what is said on arrival.** Paying for a bespoke party
simulation to deliver a conversation is paying for the wrong half. It also gives the healer a name, a face and a
memory profile, which a spawned nobody cannot have — and a named person is the only kind this mod can make matter.

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
healer, a surgeon, an artisan — precisely the people this quest is about — cannot ride today. Widening it is the
first real decision, and it is the same shape as the `follow_me` widening of 2026-09-10: the rule refused a whole
population for a reason that made sense in its own pillar and does not generalise. **Gabriel's ruling needed:**
does a notable ride in the party under the same terms as a lord, or does an escortee need its own, narrower
eligibility (alive, free, willing, not already travelling, and NOT a party leader)?

**2. No CR quest completes by ARRIVING anywhere — and Gabriel's ruling below means none has to.** Twelve quest
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
is the strongest one this feature can produce — you stand in front of the headman and have to say it. Today a
quest target who dies is silently voided by `QuestTargetVoidPolicy` with an honest closure line the player reads
in a journal. Here the player says it out loud to the person who was waiting. That is the difference between a
quest system and this mod, and it costs nothing extra: the same policy already tells the three states apart
(vanished, dead, freed), and each one is simply a different scene rather than a different log entry.

### Shape

- **Inc 1 — the escortee may travel.** Widen or fork the retainer eligibility per Gabriel's ruling above. Pure
  policy plus tests; nothing player-visible yet.
- **Inc 2 — a quest that names a passenger and a destination.** New `QuestType.EscortPerson` carrying a target hero
  AND a target settlement (today a quest carries one or the other, never both). Completion is the hand-off VERB,
  not the arrival, so it goes through the ONE existing path and is stamped, rewarded and journalled exactly as
  every other quest is. No arrival listener in `QuestService`.
- **Inc 3 — the arrival OFFERS the scene.** Reaching the destination with the passenger raises the same popup a
  companion raises for a private word, behind the same safety read (`RoadAudienceFacts`, so it can never fire at
  sea, in a siege, over a menu, or on an open chat). Declining must leave the quest untouched and offer again:
  the player is late for a reason, and a scene that can be missed once by a misclick is a scene that will be.
  Who speaks first is a writing decision: the headman receiving, or the charge arriving.
- **Inc 4 — the endings, all of them scenes.** In time, late, and empty-handed are three conversations, not one
  success and two failures. `RetainerLeavePolicy` still ends the travelling on war and death, and
  `QuestTargetVoidPolicy` still tells vanished from dead from freed — but its verdict now chooses which scene
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

## BUILT 2026-09-07, REBUILT + PLAY-VERIFIED 2026-09-13/14, SHIPPED v2.5.9: take your own people aside and BRIEF them

> **Status corrected 2026-09-14.** The 2026-09-07 build did not hold up in play: closing the briefing
> dropped the player on the map with the meeting gone, or into a loading screen, or crashed natively.
> Rebuilt 2026-09-13 around ONE carrier - the private word becomes an `IsPrivate` NotableEvent on everyone
> who stood in it, and `BuildWitnessMemory` filters those out of every other character's prompt. The
> standing brief (`CompanionBriefingStore`, `EncounterContext.CompanionBrief`) is DISMANTLED: it existed
> only because a memory could not be private, and that stopped being true the hour `IsPrivate` was built.
>
> **Verified end to end in Gabriel's own game 2026-09-14**, in one log: hand-off with no scene staging
> (`mission=False mapTalk=True -> OpenOverLiveHost`), the target opening the meeting in her own voice
> knowing he stepped aside and not a word of what was said, and the companion answering a question put to
> him instead of the floor returning to the player with it hanging.
>
> **DURABLE, cost four attempts in one day:** Bannerlord hosts a conversation EITHER as a Mission OR as a
> MAP conversation (a layer over the map screen, `Mission.Current == null` throughout). Code that reads
> only `Mission.Current` is blind to half of them - which is what staged an interior mission from the
> campaign map (the native crash) and what later ended a meeting that was standing perfectly well.

A player asked whether a specific NPC could LEAD a group conversation (a cruel companion interrogating a prisoner,
a charming one working a village notable). Gabriel named the blocker: choosing the right speaker needs the
conversation's INTENT, and the intent exists only in the player's head.

Two designs were rejected, and the reasons are worth keeping. An in-chat ASIDE fails on fiction, since the lord
sees you whisper and asks what you said, breeding the very suspicion the player wanted to avoid (it also needs the
transcript to know who heard each line, which it does not model). A briefing at the companion picker fails the same
way, because the picker fires once the vanilla conversation has already begun.

**Gabriel's design, shipped:** a vanilla dialogue line asks the lord's leave, openly, for a private word with your
own people. Nothing is hidden, so nothing is suspicious. You brief the companions YOU pick, in a real conversation
where they can question the plan or say they are ill-suited, and what you told them rides with them into "A word
with you...". Briefs stack, capped and trimmed from the oldest end, and expire with the conversation.

**NOT VALIDATED BY ANYONE YET.** Gabriel tests before anything is built on top.

---

## DESIGNED, NOT BUILT — **THE GATE IS OPEN since 2026-09-14**: a companion ACTS on your order

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

## RATIFIED (Gabriel 2026-09-07), NOT built: two hosts to revisit once panels are state-hosted

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

## SHIPPED v2.5.3 (player request 2026-09-06): the player can ROB, BEAT or KILL the character they are talking to

A player on a beggar playthrough stabbed an NPC in conversation and the AI reached for `execute_player`, which means
the NPC killing the PLAYER: the bridge refused it, so nobody was ever at risk, but the mod had no verb at all for
the player's own violence. He asked for mugging, robbery, assault and attempted murder.

**Gabriel's ruling:** the MCM toggle gates KILLING ONLY (`AllowKillingCharacters`, default off); robbery and assault
are always available. The player CAN fail. It is possible everywhere, but harder when the victim is not isolated.
Later addition: a killing asks the player to confirm first, because the MODEL is what decides a killing happened.

**Shipped:** `ViolencePolicy` + `ViolenceConsequencePolicy` (pure), `PlayerViolenceVerb` (rob_npc / assault_npc /
kill_npc), `ViolenceSceneReader`, `ViolenceConsequenceService`. The law is the ENGINE'S own crime rating. Two
anti-exploit rules do the load-bearing work: the law hears something even unwitnessed, and a failed attempt still
costs. `cr.testall player_violence`. **Deliberately NOT done:** being seized or arrested on a failed attempt, since
Bannerlord has no clean path for it and faking one would strand the player in a scene the engine does not own.

---

## SHIPPED v2.5.3 (Gabriel 2026-09-06): a BATTLE leaves real facts, not one generic line

A battle should leave the people who fought it real memories (whom you bested, who downed you), an EXPLOIT should
circulate as gossip, and a LORD must never be called killed unless he truly died, because soldiers die and lords
usually do not.

**Shipped:** `CombatProwessPolicy` + `BattleFeatDistiller` (pure), `BattleFeatJournal` (MissionLogic),
`BattleFeatReporter`. Outcomes are resolved from the CAMPAIGN, never the mission's verdict. Deeds are rendered per
recipient, so the bested foe speaks of his own defeat. An exploit becomes a `PlayerFeat` world event, so a lord who
was never there can raise it as hearsay. Named head-to-heads feed the tournament rivalry ledger. **Nemesis was
deliberately not wired:** it mints recurring BANDIT villains, and an enemy commander who unhorsed the player is not
that.

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

## WISHED (player request 2026-08-30, roadmapped by Gabriel; NOT built): an IN-GAME NPC CONFIGURATION / PERSONA editor screen

A player who tried AI Influence came back preferring CR for roleplay, but missed one thing: AI Influence's in-game configuration screen, where you can add details to an NPC and GENERATE an AI personality for them, and edit world/culture files, all DURING play. CR can already do the equivalent by editing files by hand (`character_overrides.json`, `world.txt`, `setting.json`, `cultural_traditions.json`), but it means a lot of file manipulation and cannot be done mid-session. This pillar brings that editing in-game, which fits CR's roleplay focus squarely and lands the older "bespoke UI identity" wish at the same time.

**The machinery already largely exists (de-risked):**
- An in-game Gauntlet EDITOR screen already ships: `cr.appearance` opens `AppearanceEditorManager` (GauntletLayer + `AppearanceEditorViewModel` + prefab). This is the pattern to generalize.
- Live override + hot apply already exist: `CharacterOverrideLoader.InjectRuntimeOverride` (a runtime override without touching the file) + `CharacterLoreBehavior.ApplyAll` / `cr.reload_lore`.
- The `character_overrides` schema is already there (5 traits, Backstory, Conviction, Orientation, Kinks).
- The LLM plumbing for a "generate a personality" button already exists (the chat/summarizer path).
- Custom pushed screens + named brushes + runtime-image widget are all owned.

**Increment breakdown (proposed):**
- **Inc 1:** a persona-editor screen for the CURRENT NPC: edit their `character_overrides` fields (trait sliders, backstory, conviction, orientation, kinks) live via `InjectRuntimeOverride` + `ApplyAll`, and SAVE to disk. Generalizes the appearance editor.
- **Inc 2:** a "GENERATE a personality" button: one LLM call drafts backstory/conviction/traits from the NPC's engine data (culture/traits/clan), populated into the editable fields before the player saves.
- **Inc 3:** a world/culture editor tab (`setting.json` worldName/worldAdjective/patriarchalCultures, `world.txt`, `cultural_traditions.json`), hot-reloaded via the existing `cr.reload_lore` / `cr.reload_prompts` seams.

**Must pair with a persistence fix (BLOCKER for Inc 1's SAVE):** `character_overrides.json` currently lives in `<game>\Modules\CalradiaRemembers\ModuleData\`, which a CR update overwrites, and the loader reads a single fixed file with no merge. An in-game editor that saves must write to a CUSTOM folder that survives updates (the "Custom-first" loading change first raised for the RotR compatibility pack, 2026-08-29). Do that fix as Inc 0.

**Related (same player, same comment):** they also wanted a Brothel "bridge" (a modder built one for AI Influence, not CR). This is exactly what the `CrActions` extension surface is for, now that the pre-response `Guidance` hook (shipped 2026-08-30) lets such a bridge decline gracefully. A CR brothel bridge is buildable by any modder, or as a first-party optional module. Not part of this pillar, but worth naming as an ecosystem opportunity.

---

## STOLEN LOVE pillar (ratified 2026-08-26, player request; NOT built)

Seduce a MARRIED NPC, break their marriage over time, then either marry them yourself or leave them divorced to
sabotage a rival. A new pillar built on three systems already in place: the affair/infidelity path (the consent
block already frames intimacy with a third-party-married NPC as infidelity gated at regard >= 30, plus
RomanticStatus.SecretLover), the player-divorce unmarry (CR sets Hero.Spouse = null on both sides + ExSpouses +
a relation hit, since the engine has no clean DivorceAction), and MarriageAction.Apply for the remarriage once
the NPC is free. So the work is design (conditions and consequences), not plumbing.

Ratified decisions:
- BOTH flavors, one spine (affair -> estrangement -> divorce): (a) seduce -> divorce -> MARRY the freed NPC;
  (b) seduce -> cause the divorce as SABOTAGE, without marrying.
- GATED seducibility, never any married NPC: marriage health (the NPC vs their spouse relation; low/negative =
  vulnerable, high = resists), children together (harder), duration, the NPC's Honor/Calculating traits, and
  earned regard toward the player. A happy, honorable spouse is nearly unwinnable.
- Consequence is a PERSONAL grudge on the JILTED SPOUSE only (new GrudgeSource.StolenSpouse). No clan or faction
  fallout, no war. It feeds nemesis and the vanilla Blood Feuds beta naturally.
- Player-experience-first: the estrangement exists only for the couple the player is actively breaking; no
  NPC-NPC affairs are simulated elsewhere.
- The post-divorce marriage SKIPS the normal courtship (no trial, no family blessing, no regard bar): making the
  spouse divorce for the player IS the courtship and the proof of consent.
- Dedicated MCM EnableStolenLove toggle; the explicit intimacy stays under AdultLevel (the divorce/marriage
  mechanics themselves can run as a courtly seduction without adult content).

Increments (test-first, Logic-heavy while in-game QA is unavailable):
1. SeductionEligibilityPolicy (pure, unit-tested): seducible + why-not from the factors above. Prompt teaches
   the seduction path only when the policy says seducible. No engine yet.
2. NpcMarriageEstrangementBehavior (persisted, additive save key) + EstrangementProgressPolicy (pure): advances
   an estrangement over days at rate = affair depth x marriage weakness; the NPC voices the growing rift.
   Mirrors the existing player SpouseDivorceDemandBehavior / DivorceProcessBehavior.
3. Divorce verdict (reuse the player-divorce unmarry) + GrudgeSource.StolenSpouse planted on the jilted spouse +
   a magnitude policy + self-test. Spike the NPC-NPC divorce feasibility here.
4. The two exits: (a) marry the freed NPC via a relaxed gate; (b) sabotage = leave divorced, scheme framing.
5. Polish: memories (affair, divorce, jilted-spouse grudge), Chronicle entries, the MCM toggle.

## WISHED (player request + Gabriel, 2026-08-26): 4X-style WORLD ALMANAC panels, statistical not just a feed

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

## BUILT, untested in game (player request + Gabriel, 2026-08-27): the POST-DUEL conversation

A fought duel now reopens as a conversation that RESUMES the exchange which led to it, the opponent speaking
first coloured by who prevailed. If the player LOST, the NPC speaks as the vindicated victor and makes plain
what they now expect; if the player WON, the NPC concedes and is disposed to grant a reasonable favour the
player asks ("keep away from her, never again"). The loser of a fought duel also forfeits clan influence
(base + per-witness, capped). Built in five increments (all committed, engine parts untested in game):

0. Centralized chat-scene staging behind one door, `ChatSceneStager.Open(ChatSceneRequest)` (renamed from
   CaptiveSceneStager), which upholds the invariant that a map-originating chat ALWAYS opens over a live
   Mission, never a bare map overlay (VRAM leak / non-repainting background). Request built by composition.
1. `DuelOutcomePolicy.LoserInfluenceDelta` (pure, tested) -> `ChangeClanInfluenceAction` on the loser's clan.
2. `ConversationTranscript.SeedFrom` (pure, tested): carry the pre-duel transcript into the new chat (process
   only, a duel never spans a save); `DuelCoordinator` stashes it at arm and hands it to the Phase-2 outcome.
3. `ChatSceneKind.PostDuel` wired end to end (mod) + SDK `AppendPostDuelContext` holding the victor/loser
   register on every turn (7 Lane-A tests). The favour is the NPC's own remembered word (the conversation
   memory), NOT a swear_oath (only pay_gold/keep_peace/protect) nor pledge_against (council scope).
4. MCM "Post-duel conversation" toggle (Duels group, default ON) + `cr.postduel [win|loss] [name]` test command.

To validate in game: `cr.postduel win` / `cr.postduel loss` on the open map (the bubbles should resume the
seeded pre-duel exchange, then the NPC opens in the right register), and a real fought duel's aftermath.

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

## SHIPPED v2.2.0 (Level 1; player report, 2026-08-20): a letter-borne agreement must survive into the in-person conversation

A player negotiated a marriage over three letters with Desporion ("we agreed on the terms"), then met him in
person and he denied everything and called the player a liar. Cause pinned: `LetterPopupManager.MarkReceived`
records only a GENERIC memory ("Received a letter from X (Reason)"), never the SUBSTANCE, so the live chat prompt
(which reads the NPC's profile events) has no idea a marriage was proposed or agreed and the NPC contradicts it.
This is the known "letter-borne agreement invisible to live chat" gap, now seen in the wild. It is the sharpest
possible violation of the mod's core promise (what is said is remembered), and the fix makes the drama BETTER: an
NPC who remembers can honor OR deliberately renege, instead of having amnesia.

Level 1 (SHIPPED v2.2.0: MOD `d50160a` / SDK `c51f7cd`; unit-tested, not play-tested; wired at the `LetterPopupManager` received-letter site with npcIsSender, covers `CorruptionAttempt` and every substantive reason): substantive letters leave a real first-person memory, mirroring
the two-stage CaptiveSceneSummarizer pattern. Pure `LetterMemoryPolicy` (which LetterReason values warrant a memory,
excluding pure re-pings AwaitingReply/QuestUpdate; a synchronous first-person BaseMemory as guaranteed capture) +
SDK `LetterMemorySummarizer` (one-shot LLM, first-person past-tense NPC POV of what the letter proposed/agreed/
reported) + mod wiring at the letter-received and PlayerLetterReply sites + a `letter_memory` cr.testall self-test.

Level 2 (LATER, separate, larger): a letter-agreed marriage actually SEEDS the real arrange_marriage / a Commitment,
so it is not only remembered but executed. Ties into the "letters carry actions" extension.

---

## WISHED + RATIFIED (player report 2026-08-24 + Gabriel): an INTERCEPTED player letter can LEAK into the wrong hands

A player watched a letter get lost on the road, then received a letter from an enemy noble who "knew his plans"
and wanted to meet, and read the two as cause and effect. They are NOT: the enemy letter was an independent
`CorruptionAttempt` (a lord fishing for war plans), and an intercepted letter today is simply lost (bandit waylay,
resend prompt), its contents reaching no one. But the player unknowingly designed a feature that fits the mod's
spine perfectly (a letter is the private channel; interception is the risk that the privacy fails), so: make the
coincidence real.

Shape (ratified): when a PLAYER-SENT letter is intercepted (courier waylaid), a BOUNDED chance its contents FALL
INTO THE WRONG HANDS. An eligible antagonist (an enemy-faction lord, or a grudge-holder) comes to know a fragment
of what the letter said and writes to the player about it, or gains an intrigue hook. Grounded in the intercepted
letter's real `Content` so it is specific, not generic. Only letters with real substance can leak (reuse
`LetterMemoryPolicy.ShouldRemember`; a lost romantic re-ping does not spill state secrets). Rare by design (a low,
tunable chance), so it stays a memorable event, not noise.

Increments:
- Inc 1: DONE 2026-08-24 (SDK `d4a2fe1` / MOD `ed3f55f`, committed NOT pushed). Pure `LetterLeakPolicy` +
  `LetterLeakAntagonistPolicy` (grudge-holder preferred, else hostile lord) fully unit-tested; SDK
  `LetterReason.LeakedCorrespondence` + prompt + `LetterWeights` intrigue-tier weight; wiring at
  `CourierService.PlayerToNpcCourierLost` (bandit branch) -> `TryLeakLostLetter` -> `QueueInitialLetter` grounded in
  the real text; a `WorldEvent` (`letter_leaked`); MCM toggle `InterceptedLettersCanLeak` DEFAULT OFF; self-test
  `letter_leak_evaluates` (non-destructive antagonist scan). DEVIATION from the scope note: substance is a CONTENT
  HEURISTIC (`LetterLeakPolicy.HasSubstance`, min length), NOT `LetterMemoryPolicy.ShouldRemember`, because
  `PlayerLetter` carries no `Reason` field (adding one is a later increment).
- Inc 2: DONE 2026-08-25 (MOD `7fc203f`). The leak now LASTS beyond the one exploiting letter. The antagonist
  forms a persisted grudge against the player (`GrudgeSource.LeakedLetter` via `GrudgeBehavior.TryPlant`), so the
  grudge system escalates it into future hostile letters or an interception; and the player loses a little renown as
  their private words become known (the rumour's public cost, on top of the `letter_leaked` WorldEvent Inc 1 already
  records as the rumour substrate). Magnitudes are the pure, unit-tested `LetterLeakConsequencePolicy` (grudge +
  renown, both sharper when the antagonist was ALREADY a personal enemy). Wired in `CourierService.ApplyLeakConsequences`
  (called from `TryLeakLostLetter`); self-test `letter_leak_consequences` (plants on a grudge-free lord, asserts
  grudge + renown, then clears + restores). Same MCM toggle `InterceptedLettersCanLeak` (feature deepened, not new).

Testability note (Gabriel not playing until ~Christmas, see the feedback memory): all the decision logic lives in
`LetterLeakPolicy` with exhaustive unit tests; the engine wiring stays thin. The LLM output (the antagonist's
letter) stays unvalidated until play.

---

## DONE 2026-08-25 (Gabriel wish): new letter reasons that carry a real proposition (PrisonerOffer + ServiceOffer)

Two new unbidden lord-offer letters, and the letters now carry an ACTION the player can accept from the read window
(a new Accept / Decline affordance replacing Acknowledge / Reply on offer letters).

- **PrisonerOffer** (the concrete one): a lord who has a stake in a prisoner the PLAYER holds (their own house, else
  their realm) writes offering to buy them, naming a fair price. Accept settles the REAL ransom (gold + prisoner
  transfer) through the shared `PrisonerRansomExecutor` (extracted to mirror `BuyPrisonerVerb`'s atomic trade, the
  verb left untouched). Price = pure `PrisonerOfferPolicy.OfferAmount` (personal-stake premium, clamped to the
  honest `PrisonerTradePolicy` band). Trigger = daily collector `CollectPrisonerOffers` (scans the player's held hero
  prisoners; buyer = captive's clan/faction leader; skips a buyer who cannot pay the floor; one offer per captive;
  creates a buyer profile if the player never met them). Payload persisted on `PendingLetter.OfferTargetId/OfferAmount`
  (additive, save-safe); Accept re-validates the captive is still held before showing.
- **ServiceOffer** (poaching): a faction RULER who is not the player's liege and regards them highly writes to lure
  them into their service, dangling one enticement (a castle if they have a fief to spare, troops if they lead men,
  else gold: pure `ServiceOfferPolicy.ChooseEnticement`). Deliberately RARE (low weight `WeightServiceOffer=45`, a
  90-day cooldown, one offer per lord ever). LIGHT hook (Gabriel): Accept only warms the bond
  (`ServiceOfferPolicy.AcceptRelationWarmup`) and records the courtship; it does NOT change allegiance or grant the
  prize (that stays the promise, realised only by actually defecting via the vanilla path).
- Plumbing: SDK `LetterReason.ServiceOffer`/`PrisonerOffer` (appended), `LetterPromptBuilder.DescribeReason` +
  `LetterMemoryPolicy.PlainWords` cases, `LetterWeights` consts + `LetterWeightsTests` pin. Read-window affordance in
  `LetterViewModel` (`ShowAcceptOffer`/`ShowDeclineOffer` + Execute*), prefab buttons (build + restart to show).
  Console `cr.letter_offer <prisoner|service> [name]` (synthetic letter to test Accept without the mailbox). Tests:
  `PrisonerOfferPolicyTests`, `ServiceOfferPolicyTests`; self-test `letter_offer_prisoner_ransom` (real ransom
  round-trip with full restore). The LLM letter prose stays unvalidated until play; all decisions are unit-tested.

---

## BUILT, untested in game (player-session finding 2026-08-15, built 2026-08-20 Sonnet test-first): an NPC can TEACH the player a skill, and it grants real skill XP

In a bench run an NPC spontaneously offered to teach the player:
> [EVENT] Other: I offered to teach Arwa basic swordsmanship once she recovers her strength, and promised to
> introduce her to a captain who might train her further if she proves capable.

The LLM already OFFERS instruction organically (swordsmanship, reading an opponent's stance, court politics,
riding). The gap is the same empty-promise class the mod closes elsewhere: the offer never becomes real. The wish
is to make a teaching offer actually grant SKILL XP to the player, so a lord who says "I will teach you the blade"
leaves a mark on the character sheet, not only in the transcript.

Shape (NOT started):
- A `teach_skill` action verb (GameActionCatalog) the interpreter emits when the prose shows the NPC actually
  instructing the player THIS beat (not merely promising a future lesson). Params: skill, amount.
- Bridge executor: `Hero.MainHero.HeroDeveloper.AddSkillXp(skill, amount)`, re-validated ("the prompt is advice,
  the bridge is law"). The NPC must plausibly know the skill (gate on the NPC's OWN skill level or role: a captain
  teaches One-Handed, a merchant Trade, a scholar Medicine or Engineering). The amount stays bounded and modest,
  with a cooldown or per-NPC cap so it cannot be farmed by re-asking (mirror RelationGate's cap plus cooldown).
- Promise vs lesson distinction: "I WILL teach you once you recover" is a future commitment (Commitment or quest
  territory); "here, hold the blade like THIS" is an executable lesson this turn. Only the latter grants XP now;
  the former could seed a Commitment the NPC later honors (ties into the escort/errand commitment machinery).
- Pure policy `TeachSkillPolicy` (who can teach what, how much, cooldown) isolated in .Logic with tests; a
  cr.testall self-test via a DebugEvaluate seam; a cr.teach_info console command.

Fits the mission: a spoken offer shows up on the character sheet, verified by a real engine grant, bounded so it
cannot be abused. Related: the anti-empty-promise rule (STAY WITHIN WHAT YOU KNOW), join_party's re-validation
pattern, and the Commitment/errand machinery for the "future lesson" half.

---

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

## KNOWN BUG, DEFERRED (player report, screenshot 2026-07-25): the chat does not slide aside when the escape menu opens over it

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

## RATIFIED + BUILT (Gabriel + player CarolusIV, 2026-07-25): the MATCHMAKER, arrange a match between your kin and another clan's noble (shipping v1.31.11)

Player report (CarolusIV, Nexus): CR's marriage is player-only; you cannot arrange a match between a young noble
of YOUR clan and an eligible noble of ANOTHER clan (the player is neither spouse). Gabriel: "You are right. I'll
add this feature."

Design (systems-first, mirrors the existing marriage philosophy, the bridge is the law):
- Pure consent policy (ArrangedMatchPolicy, Logic): the target family agrees when the player is regarded well
  enough OR their kin marries UP into a higher-standing house; refuses on poor standing; rejects a non-cross-clan
  pairing. The HARD eligibility (age, kinship, marital status) is the engine's own
  MarriageModel.IsCoupleSuitableForMarriage, and the wedding is the native MarriageAction.Apply(a, b).
- Bridge action arrange_marriage (a = a player-clan member, b = the conversation noble or their kin): resolves
  both, checks the NPC's authority over b, validates IsCoupleSuitableForMarriage plus the consent policy, then
  applies the real marriage. The LLM's agreement is narration, not the deciding vote (a weak local model must not
  be talkable into an impossible match).
- Prompt teach: when the player speaks with a noble of another clan and has an unwed adult kin, teach a MATCHMAKER
  block listing the eligible kin and the arrange_marriage emission format.
- Console cr.arrange_marriage plus a self-test for deterministic validation.

STATUS: BUILT + COMMITTED + PUSHED 2026-07-25 (ArrangedMatchPolicy 7 tests + bridge arrange_marriage + prompt
teaching, mod 2aaf283/877f679, SDK 3349eaf). Numbered v1.31.11. UNTESTED in game: console cr.arrange_marriage
<yourKin> <theirKin> [npc] is the deterministic test path (it weds two real NPCs, use a test save).

---

## RATIFIED + BUILT (Gabriel 2026-07-23): FORCED MARRIAGE, the family can bind its daughter (untested in game)

Born of the Ira report (below): the player had done the family's part in full and was silently barred by the
bride's-own-regard gate. Gabriel's ruling: history sides with the family. A house that has GIVEN ITS BLESSING
can bind its kin for political ends, so the union now PROCEEDS below the regard bar, and what the unwon heart
buys is the bride's RESENTMENT instead of an invisible refusal.

**The shape.** Regard at/above 50 (either ledger): a love match, exactly as before. Regard below 50 WITH the
family's blessing: the wedding happens, and `ExecuteMarry` plants a `GrudgeSource.ForcedMarriage` (ordinal 16,
appended, save-safe) whose magnitude is `ForcedMarriagePolicy.GrudgeMagnitude`: base 12 + one point per point
of shortfall below the bar, capped at 75 (below vendetta territory ON PURPOSE: the arc must stay winnable).
A grudge, not a flat malus, deliberately: it colours her prompt, shows on her encyclopedia page, TARNISHES her
kindnesses, and decays/resolves. Regard below 50 and NO blessing: still refused, and the refusal now names the
other road ("no family blessing compels them to").

**The consent trap, and why this lot was not delegated.** The marital consent exemption ("none of the
stranger-resistance trust thresholds apply: your vows already bind you") is right for a love match and would
have been a CONSENT BYPASS for a bride wed at 10 regard. So: `EncounterContext.SpouseMarriedUnwillingly`,
resolved LIVE from the grudge ledger each turn (no persisted flag to keep in sync), switches her to an
unwilling-spouse branch: vows bound her STATUS, never her heart; "a wedding is not consent"; her own nature's
thresholds print with the full verdict, exactly as if unwed; the infidelity framing (a lie in her mouth) is
excluded. **The redemption arc is automatic:** the day the grievance fades or is resolved, the flag flips and
the ordinary marital footing takes over by itself. `LoveMatchReluctant` does the same for the PROPOSAL: a
blessed-but-unwon match is spoken as duty ("marrying your name, not your heart"), never as love.

Tests: `ForcedMarriagePolicyTests` (mod, 5), `ForcedMarriagePromptTests` (SDK, 6, including the regression
guard that a WILLING spouse keeps the exemption untouched). `cr.marry_info` now says which kind of marriage is
on offer and the resentment a forced one would plant. NOT play-tested; the grudge plant itself is engine glue
(same TryPlant seam every other source uses).

## BUILT, untested in game (Nexus 2026-07-23; scoped + built 2026-08-21): choose WHO comes with you, including your own PRISONERS

Two players, one thread, and Gabriel answered "it's now on the roadmap".

**tthheerroocckk:** *"can we choose to bring other prisoners, not just companions, when we choose to talk to a
prisoner? Since captors can talk to you and a companion, why not the reverse? One thing I wanted to rp was
something like using Ascyron to pressure Lucon (both prisoners in my party) to blackmail him."*

**nerethi (+1):** *"i wanted to bring a specific companion to 'judge' a prisoner but could only do it after
putting him in a dungeon cell, walk all the way to him and use the normal conversation window. would be
convenient to be able to choose who's with me even when starting from the party screen and just on the map."*

**Two separate asks, and the second is the cheaper and probably the more valuable one.**

1. **WHO the picker offers.** Today the participant picker is companions only (`WitnessResolver`, cap 2, plus
   "Go alone"; `WitnessEntry.IsPlayerCompanion`). The ask is to also offer the lords the player currently holds
   PRISONER. The symmetry argument is sound and is already the mod's own: a captor may bring his people to a
   scene, so the player should be able to bring his.
2. **WHERE the picker is offered.** nerethi's actual pain is not the roster, it is the ceremony: he had to put
   the prisoner in a dungeon cell and walk to him to get an ordinary conversation window at all. He wants to
   pick his company when starting from the PARTY SCREEN and on the open map.

**Design notes, so this is not built twice.**
- A prisoner brought into a scene is NOT a witness in the existing sense, and must not be modelled as one. A
  witness is a free person whose regard is at stake; a captive is present under duress, cannot leave, and
  their "reaction" is coerced. The prompt would need its own framing, and `AppendWitnesses`'s reaction
  machinery (PROVOKED/PROACTIVE, the candour adjustment) is written for free people.
- The blackmail case is LEVERAGE, which CR models only in one narrow place today
  (`IntimacyLeverageActive`/`IntimacyLeverageHeld`). Using one captive against another is a general leverage
  mechanic and should be designed as one, not bolted onto witnesses.
- **nerethi's "judge a prisoner" is the ASSEMBLY pillar's TRIBUNAL mode** (see the 5-mode hub). Whoever picks
  this up must check the Tribunal design first: a companion brought to weigh a captive's fate is that feature
  in miniature, and building it twice would leave two rival paths to the same scene.
- Reuse: the picker itself already exists (`ShowMultiSelectionInquiry`, the "Go alone" option and the
  present-companions auto-join), and the witness list already dedups by `HeroStringId`. The same-location
  filter is satisfied for free by a prisoner in the player's own party.
- Watch the power balance: `AppendPowerBalance` and the captor-scene framing both read the room. Adding a
  second captive changes who is outnumbered, and the existing captive scenes must not start describing the
  player's own prisoners as a threat to him.

RATIFIED + SCOPED (Gabriel 2026-08-21): this lives ENTIRELY in the captor/captive SCENE, never a free-lord
conversation (there a held prisoner is bargaining LEVERAGE the player may cite, never a brought participant, and
that leverage half is a separate later concern). In a captor scene, participants have DISTINCT roles: (a) the
subject prisoner; (b) brought COMPANIONS are ACTIVE AGENTS, not passive witnesses: the player can direct or REWARD
a companion to act on the prisoner (interrogate, judge, torture, or take/abuse the captive as a reward), and the
companion must have the WILL to ACT this beat and DESCRIBE what they do, not merely comment; (c) brought OTHER
PRISONERS are COERCED subjects (leverage against the main one, the Ascyron-pressures-Lucon case), their own framing,
NOT the free-witness reaction machinery. Registers: interrogation, judgment, blackmail, torture, aggression, and
rewarding a companion. NO new leverage/Tribunal mechanic (those stay separate). Increments (Sonnet test-first):
INC 1 = companion AGENCY in the group captor scene (the reward scene) BUILT 2026-08-21, untested in game
(`EncounterContext.CompanionActingOnCaptive` set when the addressed speaker is a companion witness in a captor
scene; `PromptBuilder.AppendCompanionActingOnCaptive` grants them the agency to act + describe via [NARRATION],
AdultLevel-gated, out of the lean prompt, and suppresses the prisoner-POV `AppendPlayerCaptorSceneRules` on that
turn so the companion is not told "you are bound"); INC 2 = bring other prisoners as coerced participants BUILT
2026-08-21, untested in game (`WitnessEntry.IsBroughtCaptive`; `WitnessResolver.ShowBroughtCaptivePickerIfNeeded`
offers the lord prisoners the player holds; `PromptBuilder.AppendBroughtCaptives` + `AppendBroughtCaptiveTurn`
frame them as coerced leverage, never a free witness, never a threat, never an actor on the subject; the
"outnumbered" tally counts only non-brought witnesses as captors). REACHABILITY FIX (same day): a captor scene
opened with no witnesses, so BOTH pickers are now wired into the captor-scene entry (`NemesisPrisonerDialogFlow`
+ the `cr.captor_scene` harness), companions first then prisoners, which is what finally makes INC 1 reachable in
game. ENTRY BROADENED (same day, Gabriel's call): the captor scene was gated to the tracked nemesis, so an
ordinary prisoner could never be subjected; a second party-screen "Talk" line now opens it for ANY hero prisoner
the player holds, with its own plainer captor opener distinct from the nemesis line (mutually exclusive
conditions, shared consequence in `NemesisPrisonerDialogFlow`). This also delivers most of nerethi's ask (pick
your company from the party screen without the dungeon-cell walk). NOT play-tested yet (hold a lord prisoner,
party screen -> Talk -> the captor line -> bring a companion and/or another lord prisoner, address each).

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

## BUILT (player report, Nexus 2026-07-23): logging wrote NOTHING when the Logs folder did not exist

A player with logging switched ON got no file at all. Bannerlord creates
`Documents\Mount and Blade II Bannerlord` for saves and config, but NOT its `Logs` subfolder, and
`File.AppendAllText` throws `DirectoryNotFoundException` on a missing parent instead of creating it. Both
writers were affected (`ModLog`, five call sites; `StoryLog`, one), and every write is wrapped in an empty
catch, correctly (a log must never crash the game) but with the effect that the failure was perfectly silent.
The cost was not the missing file: it was that the mod lost its whole diagnostic channel with players, and
nobody could tell why. Two other writers in the codebase (`ChronicleWriter`, `ChatPortraitResolver`) always
created their folder; only the two logs had missed it.

Fixed: the folder is created once in `ModLog`'s static constructor (a no-op when it exists, and done even
while logging is OFF so the folder is ready the moment a player turns it on), every `ModLog` write now goes
through one `WriteToFile` seam (five duplicated try/catch blocks were how the bug stayed invisible at four of
its five sites), and `StoryLog.Write` repairs the folder once per session on its first failure. **And the
failure is no longer silent:** when logging is ON and the file still cannot be written, the player is told
once, on screen, with the path it tried. Never shown when logging is off, where an absent file is correct.

## BUILT (player video, 2026-07-22): an unproven CLAIM could buy real regard, untested in game

A recorded playthrough on YouTube showed a whole assassination arc played out in free prose: the NPC named two
targets (one of them, "Lykon", exists nowhere in the game's data; the other, "Nemos", collides with a real
base-game lord, `lord_1_44`, and a real village, Amycon), the player "proved" the killings with a stage
direction saying a bag held a severed head, and the NPC paid `change_relation` for it. Confirmed as OUR mod,
not another: the banner text is composed by `BannerlordGameStateBridge` (`"{name} regards you more warmly."`).

**The cause was in our own prompt, not in the model.** `READING WHAT THE PLAYER DOES` orders the NPC to treat
any asterisked action as "actually HAPPENING right now, never as nonsense", and drew no line between a
movement of the player's BODY and an assertion about the WORLD. Meanwhile the rule that would have caught it
("words are cheap; you reward deeds, not stories", plus an explicit "no token, trophy, blade, banner, severed
head ... to carry back") already existed, but only inside `OFFERING TASKS`, binding on `[QUEST_COMPLETE]` for a
task already in the ledger. This arc never entered the quest system at all, so nothing bound.

**Fixed (SDK only).** (1) A boundary inside the narration section: a stage direction settles the player's body,
here, now; it never settles the world outside the room or anything already done. The act is real (they do hold
out a bag), what it IS remains their word, and coin, goods and captives change hands only when the game says
so. (2) A new always-on section `WHAT THEY SAY THEY HAVE DONE IS A CLAIM, NOT A FACT`, deliberately OUTSIDE the
quest gate so it holds even with quests disabled: an unwritten deed is a story, never treat it as done, never
reward it, and explicitly never `change_relation` on it. Feelings and intentions are exempt, so NPCs do not
become interrogators. Both rules have SHORT forms in Lean: adding them blew the Lean token budget on the first
attempt (that test earned its keep), and a small local model is exactly the one most likely to swallow a
confidently narrated proof, so they are shortened there, never dropped. 9 tests in
`UnprovenClaimPromptTests`, including a regression guard that the narration section still tells the model to
ENGAGE with physical acts: it is what captive and intimacy scenes are built on, and an over-broad boundary
would silence a whole pillar to close one hole.

**STILL OPEN, and deliberately not built:** binding names in the NPC's own reply back to real heroes. The safe
half (spotting heroes that DO exist, reusing `ContextualNameResolver.ExtractTokens`) would not have prevented
this bug; the useful half (spotting a person the NPC INVENTED) is free-prose name detection, where a tavern
name, an epithet or a place reads exactly like a person and a mis-fire would correct the model wrongly and
damage the prose. Proposed order when it is taken up: a zero-risk DIAGNOSTIC first (log what the NPC names and
whether it resolves), to measure how often this actually happens before deciding how hard to fight it. The
better-targeted sibling is to SUPPLY real names (a settlement's notables, on the `KnownWanderers` pattern:
give the real one, then forbid inventing another), since the model invents a merchant only because the prompt
contains no merchants.

## BUILT (player report, Nexus 2026-07-21): explicit Reply Language setting, untested in game

A Spanish-speaking player on DeepSeek V3 always got English back, even though he wrote in Spanish; Gabriel's
own French always mirrors correctly. Diagnosis: `PromptBuilder.AppendLanguageMirror`'s auto-detect rule only
names English/French/German/Ukrainian as worked examples, and an abstract "reply in the same language"
instruction is exactly what a weaker model, or a language outside that list, ignores when the rest of a long
English prompt outweighs it. Two real gaps found along the way: unprompted NPC-initiated letters (tournament,
romantic, spouse, birth) had NO language rule at all, since there is no player message to mirror; and the
async memory summarizers (captive scene, conversation fallback) inferred the language from the transcript
with no explicit instruction.

- `PromptBuilder.ReplyLanguage` (string, blank = today's auto-detect mirror unchanged). When set, states the
  language as a fixed fact instead of something to detect ("Always write your reply in Spanish... regardless
  of what language any instruction, letter, or context elsewhere in this prompt is written in"), still keeps
  section labels/action keywords in English. Wired at both prompt call sites (main + commoner); reaches
  letters for free since they share the same builder through `NpcChatService.ChatAsync`.
- `CaptiveSceneSummarizer` / `ConversationSummarizer`: optional `replyLanguage` param, defaults to "match the
  transcript" (today's implicit behaviour) when unset.
- MCM "Reply Language" (free text, Content group): blank = auto-detect (default, no change for existing
  players); a named language pins everything. `LlmServices.ChatService` rebuilds when it changes, like the
  other live settings.
- 4 new SDK lane-A tests (`ReplyLanguagePromptTests`): default mirror unchanged, explicit pin overrides it,
  section labels/action keywords still English, commoner prompt (separate call site) honors it too.

**Not fixed by this pass**: an unprompted letter in AUTO mode (no Reply Language set) still has no signal to
detect a language from, so it still ships in English regardless of the player's language, unless they set the
explicit setting. `ChronicleIlluminator` untouched (its input deed list is host-templated English text, a
different concern from player-language mirroring).

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

## NEXT PILLAR (near-term): the ASSEMBLY (multi-party gatherings)

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
- **Inc 4: mission distribution** during the session — SUPERSEDED by the COUNCIL-as-NEGOTIATION design (next section): missions distributed in assembly become CONSIGNED RESOLUTIONS taken before witnesses (ambassadors pledge in their lord's name), not ordinary quests.
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

## DESIGN RATIFIED (Gabriel 2026-07-17): the COUNCIL as NEGOTIATION — consigned resolutions

Born from the Round Table audit (`AUDIT-ROUND-TABLE.md`, root workspace): the lap's central defect (C1) is that
council speech is consequence-free while the prompt teaches every action. Two answers were rejected — apply
everything immediately (fictionally absurd: a lord does not have his troops in the room; mechanically
dangerous) and apply nothing (theatre: the council teaches the player that words do not bind). The ratified
third way: **a council is a NEGOTIATION. Free speech stays free of immediate effect; exactly one structure
binds — the `[PLEDGE]`, which is CONSIGNED, not executed. Execution is deferred to the coming days and
verified against real campaign events. The other participants are recorded as WITNESSES, which is what
makes a breach socially expensive** — the unique value of a council over 1v1: an engagement taken in front
of witnesses costs more to break.

This is NOT a new mechanism: it is the existing `CommitmentBehavior` pattern (consign -> deferred
execution -> verification -> consequence) applied to a new producer, with an execution layer for verbs the
vanilla AI will never perform on its own.

### Locked design decisions (Gabriel 2026-07-17)
1. **Resolutions are bounded to the verifiable** (the mod's founding rule: magnitude is bounded by what the
   game can verify). `send_gold`, `send_troops`, `release_prisoner`, `move_against` are verifiable by real
   campaign events. `change_relation`, "support my claim" are not — excluded from the pledge vocabulary,
   else we recreate hollow promises. The v1 list is 4 verbs; each ships with its own event verifier.
2. **Individual pledges, no group consensus.** A "council consensus" is mechanically fragile (who verifies?
   who is punished on breach?). Each NPC emits his OWN pledge (actions stay per-speaker); the other
   participants are recorded as WITNESSES of the engagement (a heroId list on the resolution).
3. **Player consent stays explicit.** Anything costing the player (gold, troops, his own sword) passes the
   existing `ActionConfirmationController` Y/n gate. Negotiation is text; the engagement seals code-side.
4. **Guards re-validated at deferred execution, not only at pledge time.** A lord may pledge in council then
   be captured, die, or change faction before the deadline. The bridge re-checks eligibility when the
   execution falls due ("the bridge is law", applied over time), with a clean narrative outcome when
   execution has become impossible ("Derthert cannot honor his word — he is in chains").
5. **One simple block, taught only in council turns.** `[PLEDGE] type / target / amount / days [/PLEDGE]`,
   4-5 fields, tolerant parsing (the parser hardening in progress applies directly). Taught ONLY when
   `IsRoundTableTurn` — a 1v1 conversation cannot produce a council resolution (the existing
   `pledge_against` action keeps its own path; it predates and is orthogonal).
6. **Two classes of council outcomes.** Engagements (verifiable, deadline, breach) via the new mechanism;
   sentiments (the debate genuinely moved regard) via the ordinary gated `change_relation`. Only class one
   is new.

### The v1 resolution verbs (execution + verification)
| Verb | Deferred execution | Verified by |
|---|---|---|
| `send_gold` | a real courier party carries the gold (CourierService — visible, waylayable; the amount is escrowed from the promisor's stock) | courier arrival (existing delivery path) |
| `send_troops` | a small party of the promisor's culture spawns and travels to join the player (courier pattern) | party arrival / merge |
| `release_prisoner` | at the deadline, if the named captive is still held by the promisor's side | `HeroPrisonerReleased` (already listened elsewhere) |
| `move_against` | ALREADY EXISTS — `CommitmentBehavior` (scheme or organic battle) | `OnSchemeResolved` / `OnMapEventEnded` |

(v2, not v1: `marry`/`grant_blessing` — requires both parties present, resolution honored at the next
meeting; `declare_war` — faction leaders only, needs its own political gate.)

### Reuse (not from scratch)
`TrackedCommitment` model (`Logic/TrackedCommitment.cs`: Id/PromisorId/TargetId/Kind/IssuedDay/DeadlineDay/
State Active->Fulfilled|Breached — generalize `CommitmentKind`), the `CommitmentBehavior` engine (daily
breach sweep, fulfillment-by-event, SafeRun per item, byte[] JSON persistence with frozen-snapshot
anti-race + graceful degradation), `CommitmentPolicy` (pure, tested: `DeadlineDays 21`,
`FulfilledRelationBonus +6`, `BreachedRelationPenalty -12`), `CommitmentJournalBridge` (vanilla journal
mirror), `StanceService.OnPromiseKept/OnPromiseBroken` (+15 Trust / -18), `WorldEventStore` (fulfilled/
breached records — the public shame), `CourierService` (visible execution of send_gold/send_troops),
`ActionConfirmationController` (player consent), the bridge's `ExecutePledgeAgainst` re-validation pattern.

### Increment breakdown
- **R0 — PREREQUISITE: Round Table audit Phase 0** (`AUDIT-ROUND-TABLE.md`): the 10 low-effort fixes before
  flipping `RoundTableController.Enabled` (witness-hallucination filter — already live in 15C, per-turn
  presence re-check, per-speaker relations, unified framing, confirmation handoff, intent-detector word
  boundaries, kill-switch guard, `cr.roundtable` test command + token logging). Without R0 the council
  engine itself is untrusted; building negotiations on it would stack defects.
- **R1 — the engine.** Generalize `CommitmentKind` (SendGold/SendTroops/ReleasePrisoner alongside
  MoveAgainst); add `WitnessIds` to the resolution; `[PLEDGE]` block in the SDK parser + PromptBuilder
  (taught only on `IsRoundTableTurn`); honored in the lap alongside `end_conversation`/`witness_leaves`
  (the ONLY effect a council turn may produce — everything else stays theatre); `send_gold` executor via
  CourierService; `release_prisoner` executor at deadline; event verifiers; breach = existing penalty +
  a WorldEvent naming the witnesses (the social sting).
- **R2 — follow-through.** `send_troops` party; "YOUR OUTSTANDING RESOLUTIONS" injected in the promisor's
  prompt (he remembers what he vowed, and the player can call him on it); narrative outcomes when execution
  became impossible (promisor dead/captured/faction-flipped); resolution recall at next meeting.
- **R3 — council politics.** `marry`/blessing honored at next meeting; `declare_war` for faction leaders;
  assembly integration (Inc 4's "mission distribution" becomes consigned resolutions taken before
  witnesses — ambassadors pledge in their lord's name).

### Risks (flagged)
- **LLM format is the weak link** (quests audit): keep the block at 4-5 fields, one worked example in the
  prompt, and tolerate the council producing ZERO pledges — a debate with no outcome is a valid outcome.
- **Cost**: a resolution adds no LLM call (execution is out-of-chat), but each pledge later surfaces in the
  promisor's prompt — bounded by "one pledge in flight per promisor" (existing `HasActive` rule).
- **Double counting**: a fulfilled pledge pays its reward ONCE (fulfillment path), never re-credited by the
  ordinary deed feeds — same discipline as the quest/commitment dedup.

### EVOLUTION RATIFIED (Gabriel 2026-07-18): STANDING accords, not only one-shot pledges

R1 as ratified above consigns a pledge that resolves ONCE (issued -> deadline -> Fulfilled|Breached). Gabriel's
addition: a council also produces engagements that **run**, and those are what a treaty actually is. Worked
example: a lord pledges a rent of 1000 denars a month to support the player's war, and to move against the
enemy's trade through his lands. That record needs four fields the one-shot model has no room for:

1. **Delay before it takes effect** (an accord struck today may start next week).
2. **Frequency** (monthly, seasonal) and therefore a NEXT-DUE day that rolls forward on each honoring.
3. **Expiry** (an accord ends; a permanent income negotiated with an LLM is a balance hole).
4. **A holding condition**: the state of the world that must remain true for the collaboration to continue.
   A war, a capture, a faction change, a collapse of regard BREAKS the accord in fiction rather than silently
   ending it. This is the part that makes an accord fragile in an interesting way, and it reuses
   `WorldEventStore` + the grudge/stance feeds that already exist.

**Shape (decided): NOT an orchestrator object per NPC.** Every comparable system in the mod is ONE campaign
behavior holding records keyed by hero id (`CommitmentBehavior`, `GrudgeBehavior`, `SchemeStore`,
`AppreciationBehavior`). A per-NPC object would add a new persistence shape and multiply save state for no
gain: one `PoliticalAccordBehavior`, one record per accord, keyed by promisor.

**Surface (decided): dashboard for the STATE, quest only for the player's own SIDE.** A monthly rent is not a
task the player can finish, so putting it in the quest journal clutters a list built around completion. The
accord's state (in force since, next due, expires, holding condition) belongs in a panel; the
`CR_Hero*` Encyclopedia family (`CR_HeroBearing`, `CR_HeroChronicle`, `CR_HeroGrievances`,
`CR_HeroKindnesses`) is the proven home. A quest is generated only for what the player must actually DO.

**The catalogue is extensible BY CONSTRUCTION (Gabriel 2026-07-18).** Decision 1 above still governs: an
accord kind may only exist if the mod can either EXECUTE it (pay the gold, send the party, launch a scheme) or
OBSERVE it (`QuestDeedTracker` sees battles, sieges, raids, hideouts). But that bound moves outward every time
the mod gains a new executable action, so the accord catalogue must be written to grow rather than as a fixed
list. Clauses the engine cannot see today (interdicting enemy caravans) are NOT excluded on principle: they
wait for an executable form (e.g. a mod-launched scheme against the target's trade), and are kept out of the
first increment rather than shipped hollow.

### STATUS

- **R0 (prerequisite): LARGELY DONE 2026-07-18 (mod `6f013e1`, SDK `ab9322d`), NOT play-tested.** Delivered:
  the witness-hallucination filter (D1, the only item that was already reaching players through 15C), the
  kill-switch guard on the action (D6), intent-detector word boundaries (D8), per-turn presence re-check
  (D3), no double witness voice on the triggering turn (D7), confirmation handoff after the lap (D10), and
  the unified council framing + attributed-transcript teaching (D5). **STILL OPEN in R0:** per-speaker
  witness relations (D2/0.5, at Boyd's turn his prompt still calls Aldric "your vassal" when Aldric is the
  HOST's vassal) and `cr.roundtable` + per-turn token logging (0.10), which is what unblocks the play-test.
- **R1 onward: NOT started.** Note for whoever starts it: the interim prompt text shipped with R0 says
  "NOTHING IS SEALED AT THIS TABLE", which is deliberately one of the two answers this design REJECTED. It
  exists only so the council does not promise what the code cannot yet honor. R1 MUST replace it with the
  `[PLEDGE]` teaching and the "what is agreed here takes effect in the days that follow" framing, in the same
  commit that makes the mechanism real. Shipping the new framing before the mechanism would only trade one
  empty promise for a more specific one.

---

## PILLAR: DELIBERATE RESCUE (ratified Gabriel 2026-07-18/19): Inc 0 BUILT overnight, PASSIVE, wired to NOTHING

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

## NEXT PILLAR: TROOP LOANS (ratified Gabriel 2026-07-18): Inc 0 BUILT, PASSIVE, untested in game

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

## TEST ARCHITECTURE RATIFIED (Gabriel 2026-07-18): three lanes, and what each is actually for

Born from a cost question ("can cr.testall be shortened by removing redundancy?") whose premise turned out to
be wrong, and worth writing down because the measurement reverses the intuition.

**The measurement** (from a full `cr.testall` run, 2026-07-18): 76 tests, 583.7s. **13 LLM tests = 583.2s
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

- `cr.testall` = regression: the 63 mechanical + lane B as it grows. Seconds, free.
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

## SHIPPED v1.30.5 (player request, SuperGraham339 2026-07-11): RESCUE AWARENESS + Enter-to-send

Freeing a captive lord by force is now remembered and ripples outward. Trigger: the player wins the battle/siege
(`RescueBehavior`: buffer `HeroPrisonerReleased` with detail `ReleasedAfterBattle`, resolve on `MapEventEnded` when
the player is on the winning side; captor matched by the defeated side's parties/factions). Consequences (all via
pure tested policies in `CalradiaRemembers.Logic.Rescue`): the rescued lord and their close family record a
first-person memory + strong regard (`RescueGratitudePolicy` Self=12 / Family=6 / Friend=3, memory for self+family
only), a few close friends (relation >= 20 to the rescued, capped 5) warm without a memory, and the CAPTOR has a
chance to discover the player did it and plant a grudge (`CaptorGrudgePolicy`: 60% public siege / 35% quiet field,
magnitude 30/20, knownToPlayer = public; new `GrudgeSource.PrisonerRescued`). Once per lord (persisted set).
`cr.rescue [name]` simulates the whole chain. Also Enter-to-send in the chat quick-input (`ChatInputWidget`, Enter
while focused calls `ExecuteSendMessage`; the multi-line compose keeps Enter as newline). **BUILT + deployed
2026-07-11, UNTESTED in game (Gabriel to test with the held v1.30.5).** DEFERRED ideas: re-rescue currently never
re-credits (once-per-lord forever); no thank-you letter; notables not covered; friends threshold/caps are TUNE.

## SHIPPED (v1.30.2, 2026-07-10): FIRST IMPRESSIONS (opening prejudice / disposition)

Player report (ProudSeal 2026-07-10): NPCs are too neutral and too reasonable on meeting, so a rivalry
requires the player to actively offend someone first. He wants prejudice and ambition rife: a noble with low
Mercy/Honor (and Calculating?) more likely to open ARROGANT, SUSPICIOUS or DEMEANING, modulated by the
player's standing (clan tier, wealth, fiefs, renown); and lords who resent a NEW clan in their faction as a
rival for rewards, without being offended first. Flavor he gave: Belgir deriding a landless upstart whose
father was "arrow-fodder for the wars against the Battanians".

**DESIGN** (extends the Stance pillar, "words nudge, deeds prove"): it colors the OPENING TONE, NOT the
relation numbers (no automatic malus, deeds still govern relation, so the contract holds). Pure
`FirstImpressionPolicy` (Logic, tested): inputs = NPC traits (Mercy/Honor/Calculating/Generosity/Valor),
player standing (tier, renown, fief count, wealth band), faction context (the player a recent joiner of the
NPC's faction = a rival for rewards), NPC status; output = an opening DISPOSITION (Cordial / Neutral / Cool /
Arrogant / Suspicious / Demeaning) + intensity. Rolled STABLE per NPC (seed by StringId) so a noble is
consistently arrogant (a personality), not random each talk. Injected as `EncounterContext.OpeningDisposition`
(prompt tone), rendered by PromptBuilder.

**GATING (who is exempt):**
- **A lord in the PLAYER'S OWN CLAN (including one who joined it) is NEVER prejudiced (Gabriel 2026-07-10).**
  He has cast his lot with the player: his opening disposition is loyal, grateful, or a believer in the
  player's mission, never arrogance or contempt. Same for companions. The clan-membership check is the HARD
  gate, checked first.
- Already-known NPCs (relation far from 0, or prior memories) use their existing stance, not a cold first
  impression.
- Only strangers / low-acquaintance OTHER lords (especially same-faction reward-rivals, or the
  status-conscious toward a landless upstart) get the prejudice roll.

**Softeners:** high player renown or a strong battle record disarms a Valorous noble (respect of arms); a
rich or titled player reads as a peer, not a target. **Ambition sub-trigger:** low Generosity (or ambitious) +
same faction + the player's clan a recent arrival, produces resentment of "having to share the rewards".

Trait-mapping sketch: low Mercy produces demeaning, low Honor projects suspicion, high Calculating produces
cold transactional dismissal of a low-value newcomer, low Generosity produces reward-jealousy. Shipped as
`FirstImpressionPolicy` + `FirstImpressionDisposition` + `FirstImpressionDescriber` (Logic, tested),
`EncounterContext.OpeningDisposition` + SDK "FIRST IMPRESSION" prompt block, mod `FirstImpressionResolver`,
`cr.impression` console. DONE since (verified 2026-08-25): the noble-rides-out-to-meet-you (interception)
frequency knob ProudSeal asked for is the "Lords Approaching You" MCM dropdown (`CrSettings.InterceptionPacing`
-> `InterceptionPacingRules`, Never/Rarely/Occasionally/Often). STILL OPEN (deferred): extending first
impressions beyond lords to notables/wanderers.

---

## WISHED (Gabriel 2026-07-10): a companion may request an audience on OTHER subjects, not only discontent

Today a companion only asks to speak with the player when they are UNHAPPY: `CompanionAudienceReason` has just
`Grievance` (unhappy in service) and `Retirement` (war-weary), fired by `CompanionHappinessBehavior` /
`WarWearinessBehavior`, so the whole "companion rides up to speak" surface reads as a retention-risk warning.
Gabriel wants companions to feel alive: they should also seek the player out for POSITIVE and varied reasons, so
an audience is not always bad news.

STATUS (2026-08-20): most of this is now SHIPPED. Beyond Grievance/Retirement, `CompanionAudienceReason` carries
SecretAffection (v1.30.3), MissionFollowUp, CompanionConflict, PracticalCounsel, Callback, and RumourReport, all
driven by `CompanionAudienceTopicResolver` + the pure `CompanionAudienceTopicPolicy` behind ONE global cooldown,
fired on entering a settlement, refusal costing nothing (`CompanionAudienceTopicBehavior`). THREE gaps from the
original wish, built in this order (Gabriel 2026-08-20, Sonnet test-first): (1) Gratitude, a purely warm thanks
for a specific kindness (reads the Appreciations ledger, no executor) BUILT, untested in game; (2) Concern/warning
had two halves: the SchemeWarning half (a loyal companion warns of a secret plot against the player, sourced from
SchemeStore, and granting the audience calls TargetHeeds so the warning has teeth) is BUILT, untested in game; the
MORAL-concern half (troubled by a deed the player DID) is DEFERRED, no clean data source (would need a classified
player-deed feed plus the companion's Honour/Mercy traits); (3) Ambition/request, the companion asks for something
concrete, BUILT, untested in game, bound to FOUR asks each resolved by an existing verb: a command to lead
(dispatch_mission), a party post (assign_party_role), a fief to govern (appoint_governor, NOT grant_fief, which
unconditionally refuses a same-clan recipient, so it could never grant a companion a fief and would be an empty
promise), and the player's blessing to wed one of the player's own unwed clan kin (arrange_marriage self-authorized).
Gold and a council seat were left out (no clean player-to-companion verb / the Council is dormant). All computed
from live eligibility (pure CompanionAmbitionPolicy, priority Fief>Marriage>Post>Command), gated on happiness and a
per-companion cooldown; the grant fires the existing verb through the normal pipeline, no new executor. The candidate
list below is the original design record. ALL THREE remaining reasons are now built (untested in game).

DESIGN: append new values to `CompanionAudienceReason` (append-only, save-ordinal stable), each with its own
opening cue + prompt framing (mirror the captive-intent pattern: one distinct opener and PURPOSE block per
reason). Candidate reasons:
- **Counsel** they want to weigh in on a live decision (a war, a marriage, an alliance, a fief to take next).
- **News / rumour** they heard something worth telling (ties to the hearsay + world-event store: only what they
  would plausibly know).
- **Ambition / request** they want more (a mission of their own to lead, a promotion, gold, a fief, a blessing
  to marry, a seat at the clan's council).
- **Personal** a romance, a family matter, homesickness, or a grudge of their OWN with another NPC.
- **Gratitude / bond** after a shared victory or a gift, they seek the player out to pledge loyalty (a purely
  warm beat).
- **Concern / warning** troubled by something the player DID (a moral beat, distinct from a grievance about
  their own lot), or a suspicion of a scheme against the player they want to warn of (ties to stance/schemes).

Reuse the existing audience infrastructure (`EncounterContext.CompanionAudience`, the ride-up/opening-cue path).
A trigger behavior decides WHEN: periodic mood-and-cooldown check plus event hooks (after a battle, after a
player decision, on a world-event they would know), with strict anti-spam so companions do not pester. Some
reasons can hand off into other systems (a request may grant something concrete via the quest/join paths; a
warning may feed the scheme/stance layer). Pure `.Logic` for the selection/eligibility + cooldown, testable.

**SHIPPED v1.30.3 (Gabriel 2026-07-10): a faithful companion may seek the player out to become a SECRET LOVER,
out of genuine affection, NEVER manipulation.** Built as: `CompanionAudienceReason.SecretAffection` +
`RomanticProfile.IsSecretLover` + `EncounterContext.SecretLoverEligible` + SDK `AppendSecretLoverProposal`
(teaches `take_as_secret_lover`); mod `ResolveSecretLoverEligible` (companions only, orientation-compatible,
regard >= 50, adult-gated, not attached), `ExecuteTakeAsSecretLover` (sets the bond DISCREETLY, no world-event),
trigger `CompanionAffectionBehavior` (weekly, content companion, `CompanionSecretAffectionPolicy` pure + tested,
cooldown), `cr.audience lover`. The rest of the companion-audience reasons above remain wished. Original design:
This is the crucial framing: a companion who has
seen war beside the player is a brother or sister in arms first. The bond is built on shared hardship and deep
trust, and the pull is admiration for the leader they have followed, so any intimacy grows from loyalty, not
scheming. A companion NEVER uses this to pressure, bargain, or extract from the player (that is the separate
exploiter/`IntimacyBargainEligible` archetype, and an unrelated stranger, not a sworn companion). Concretely, a
female companion might simply wish to share quality intimate moments with her hero, and want to keep it secret
out of her OWN discretion, to spare the player and the household the jealousy it could stir, not from fear of
exposure and never as a hold over him.

Mechanically it reuses CR's existing romance surface, not a new one: the `SecretLover` concept, gated by the
v1.30.0 orientation compatibility, an attraction threshold (high regard/admiration + close, long service), and
the adult content level. It may later deepen into a committed consort bond. It deliberately does NOT plug into
the blackmail path that other secret lovers can (a loyal companion would never), and the jealousy interaction is
one the companion actively tries to AVOID by keeping it discreet, not a trap. So the build is a new audience
reason + a trigger + wiring into the romance/consort systems, not a fresh mechanic.

---

## JEALOUSY AUDIT (2026-07-10): fixes shipped v1.30.3, gaps deferred

Full-system audit at Gabriel's request (all trigger sites, jealous-party resolution, gates, consequences,
competition loop mapped). SHIPPED in v1.30.3:
1. **Secret lover is now a jealous party** (new append-only `JealousStake.SecretLover`, resolver step 2b): a
   real intimate bond reacts, but DISCREETLY, true to its nature: milder regard hit (5/12/20, never
   culture-scaled), a private wounded letter (75%, never a public scene, never a threat of exposure), and NO
   grudge planted (the bond absorbs the hurt). Not part of the household competition loop (by design: an
   equity-demand letter would break the secrecy).
2. **`take_as_consort` now rouses jealousy AT THE ACT** (marriage-tier, witnessed): openly taking a consort
   wakes an existing spouse immediately, not only through the later competition loop.
3. **Orientation-aware plausibility**: `IsPlausibleCourtship` takes `isPlausiblePair` computed from the courted
   NPC's ORIENTATION (`RomanticProfile.IsCompatibleWith`), not raw sex. Default world unchanged
   (heterosexual = opposite-sex rule); an authored same-sex romance (v1.30.x orientation) now rouses jealousy
   like any other. Fixes the v1.29.1 gate's blind spot.
4. **Jealousy letters use the full proximity rule** (party/army/settlement via `HeroProximity`), and
   `cr.jealousy [flirt|intimacy|marriage] [name]` tests every tier.

DEFERRED gaps (audit findings, to build later):
- **Letter-borne romance is invisible to jealousy**: the whole correspondence channel (RomanticCorrespondence
  and player letters) never calls `OnRomanticAct`, so a courtship conducted entirely by courier rouses no one.
  Needs a stamp-equivalent when a letter exchange is flagged romantic.
- **Non-committed romances are unprotected**: an NPC merely Courting/Intimate with the player is never resolved
  as "the player's partner" (only spouse, consort, and now secret lover). And no BETROTHAL concept exists at
  all (only Hero.Spouse). Candidate: a light "promised" stake.
- **The courted NPC's own consort/secret-lover** is never the jealous party (only `courted.Spouse` is checked).
- Already-tracked (existing ROADMAP entries): the duel/scheming escalation, love-triangle tie-in, trait-modulated
  susceptibility (Possessive/MonogamousStrict).

## RATIFIED (Gabriel 2026-07-20): courier robustness batch, inspired by the Realistic Courier mod

### STATUS: items 1/2/4/5/6 BUILT 2026-07-20 (uncommitted, NOT play-tested); item 3 (honest ETA) NOT started
Reality found at build time was better than the audit assumed: rations were already stocked at spawn
(TravelingParty, audit C3) so item 2 became a top-up for the M5 landfall wait; a generic failure notification
already existed (MarkUndeliverable) so item 1 became the SPECIFIC reason (pure `UndeliverableReasonDescriber`
in Logic + 4 tests: dead outranks captured, vanished stays generic). Item 4 = `CourierIsStuck` position-delta
watchdog (2 days, TUNE) in both daily ticks, exempting the legitimate landfall wait; letter reverts to the
date fallback instead of being lost. Item 5 = `cr.couriers` + `cr.couriers_recall` (public
`DescribeActiveCouriers`/`RecallAll`). Item 6 audit CONFIRMED the gap and fixed it: `Hero.MainHero.IsPrisoner`
guards in TrySpawnReplyCourier (no spawn), TickReplyCouriers (reclaim in-flight), CheckReplyCourierArrival
(no hourly chase/delivery); the letter waits on date-based delivery until the player is free.

Gabriel had the "Realistic Courier" Nexus mod decompiled and reviewed for ideas (analysis notes kept in
Claude's memory: `project_realistic_courier_analysis`). Ratified: everything below. Explicitly REJECTED:
real naval couriers (granting the courier party a War Sails ship). The letter already always arrives via the
date-based fallback, so a visible sailing courier is cosmetic polish bought with a big fragility surface
(naval pathfinding, a `Ship` owned by a party we despawn before every save, dual DLC/no-DLC code paths); the
landfall-spawn + date-fallback stays CR's answer. The one salvaged piece is the pathfinding distance test,
which feeds the honest ETA below without ever granting a ship.

1. **Failure report to the player.** `CanReceiveLetter` already detects a dead/imprisoned recipient
   mid-flight but silently sets `letter.IsLost`; the player never learns. Add a returned-courier notification
   (or counter-letter) saying WHY ("Lord X was taken prisoner before your letter could reach him"). Cheap,
   big immersion win; turns a mute frustration into story.
2. **Anti-starvation top-up.** Hourly: a courier party with `TotalFoodAtInventory == 0` receives a few units
   of food. Three lines; removes the "courier starves on a long road" failure mode for good.
3. **Honest ETA.** Recipient picker shows "Name (~N days)" and the date-based fallback delay is grounded on
   real pathfinding distance (sea-aware) instead of straight-line. APIs (from the decompile):
   `DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement/ToMobileParty(main, target, navType)` +
   `CampaignUIHelper.GetPartyDistanceByTimeTextAbbreviated(distance, speed)`; land-reachability via
   `PartyNavigationModel.GetInvalidTerrainTypesForNavigationType` + `MapSceneWrapper.GetPathDistanceBetweenAIFaces`
   (confirm each at build time).
4. **Stuck watchdog by position.** Daily: record each courier's position; "has not moved in N days" (position
   delta, not date) triggers CR's answer: silent despawn + hand the letter to date-based delivery, with a log
   line. NOT the source mod's "teleport?" player popup (silent robustness + narrative cover is the CR way).
   Complements the existing DeliveryDay grace-days force-deliver.
5. **Console fixers.** `cr.couriers` (diagnostic: in-flight couriers, position, days since last movement) and
   `cr.couriers_recall` (despawn all, letters revert to date-based delivery). Support tools for player reports.
6. **AUDIT: reply courier vs captive player.** The source mod's returning messenger waits at the nearest
   non-hostile settlement when the player is a prisoner; check what CR's NPC-to-player reply courier does when
   the player is captive inside a captor's party (it currently tracks the player's position).

## WISHED (Gabriel 2026-07-20): a bespoke visual identity for CR's own windows

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

## PARTLY BUILDING (player feedback SuperGraham339 2026-07-20 + Gabriel): nemesis discoverability + lord bounties

STATUS 2026-08-20: piece 1 (an ENCOUNTERED nemesis gets an encyclopedia dossier section, hidden until met) BUILT,
untested in game (Sonnet test-first; pure NemesisDossierBuilder + a mixin mirroring the Grievances one, gated on
NemesisBehavior.GetByHero.TimesEncountered > 0; XML RENDERING is unverified, needs a dotnet build + game restart +
Gabriel checking a captured-then-escaped nemesis's encyclopedia page). Piece 2's PROACTIVE/DISCOVERABLE half BUILT,
untested in game (Sonnet test-first, mirrors the Grudge/AppreciationOwed interception triggers just added): a new
`InterceptionTrigger.NemesisBounty` (priority TerritorialPatrol > Grudge > NemesisBounty > HeardOfDeed >
AppreciationOwed) fires on BOTH the road (`InterceptionBehavior`) and the fief-audience channel
(`FiefAudienceBehavior`), reusing `NemesisBountyDialogFlow.FindEligibleNemesisFor` outright (never a re-derived
gate) so the announcement and the scripted `hero_main_options` offer can never disagree about who is eligible; the
opener names the nemesis and his region (resolved the same way the encyclopedia dossier does,
`NemesisDossierFactory.ResolveRegionDisplay`, now `internal`) and explicitly points the player to the existing
bounty without settling it in the announcement itself, so the acceptance/reward machinery is not duplicated. This
closes the "Coll raids near X" tavern-rumour wish in-fiction (the lord tells the player directly); a standalone
tavern-keeper rumour variant remains an optional minor follow-up, not required.

Player report (SuperGraham339, Nexus): captured and tortured by the nemesis "Coll the Unbroken", escaped,
killed the party, later ran into Coll AGAIN in another looter party = "an incredible moment" (the pillar's
exact designed payoff). BUT: "I was sad that he escaped, and tried to look him up in the encyclopedia but
didn't see any mention." A player forms a real attachment to a nemesis and wants to TRACK / REMEMBER / HUNT
him, and today has no surface for it. Two converging pieces (they solve each other):

1. **Discoverability of an ENCOUNTERED nemesis.** Keep the "hidden until materialised" design for un-met ones,
   but a nemesis the player has already tangled with is no longer secret TO HIM: give THAT one a post-encounter
   record. Options: an Encyclopedia entry that appears once encountered, OR a dedicated nemesis dossier page (a
   strong first candidate for the UI polish sprint / a "Book of Memory" nemesis variant). It shows who he is,
   his culture + region, and your shared history (captures, torture, your escape, your victories). Plus the
   rumour layer already flagged OPEN in the nemesis pillar (a tavern / lord hint "Coll raids near X").

2. **Lord bounties to clear a nemesis (Gabriel 2026-07-20). PROACTIVE half BUILT 2026-08-20, see STATUS above.**
   Lords who want a nemesis GONE from their kingdom post a bounty for a bounty-hunter (the player) to capture or
   kill him. This EXPANDS the EXISTING system, does NOT duplicate it: `NemesisBountyDialogFlow` + pure
   `NemesisBountyPolicy` (region eligibility) + `NemesisBountyRewardPolicy` (reward math), shipped v1.25.0,
   offered at `hero_main_options` when the lord you talk to has a nemesis in his region, still the ONLY place the
   bounty is accepted/paid. The expansion = made it PROACTIVE / DISCOVERABLE via `InterceptionTrigger.NemesisBounty`:
   a lord seeks the player out (road interception or a fief audience) and the meeting names the nemesis and his
   region, so the player learns WHERE he operates, then is pointed to the existing bounty rather than a duplicate
   accept/reward path. The bounty then doubles as the discoverability signal AND the hunt reward, closing
   SuperGraham's gap in-fiction ("here is a nemesis plaguing my region, go end him"). Pairs with the
   captive-escape memory (a nemesis you escaped remembers it).

## BUILT + SHIPPED (design diverged from the sketch below): an ESCORTED courier option at the tavernkeeper

STATUS: DONE, differently and more cleanly than the 5-cavalry-skirmish sketch below. Driven by a sharper player
report (2026-08-05: far letters lost almost every time), `TaverneLetterBehavior` now offers a per-CORRESPONDENT
armed escort (a one-off premium `EscortCostPolicy.EscortPremium` scaled by map distance, `NpcProfile.EscortedChannel`),
and an escorted thread rides INTERCEPTION-IMMUNE (`CourierService.ScanForWaylayers` never rolls a band against it)
under a larger mounted guard (`TravelingParty.HeavyEscortSize`), replies inheriting the escort for free. Committed
`0f04a47`, shipped (CHANGELOG "Send letters under armed escort"). NOTE: this immunity design is effectively
pay-for-guaranteed-delivery, which the original 2026-07-10 ruling below called "too gamey"; the punishing real
interception rate overtook that ruling. If the "wins a real skirmish instead of being immune" flavour is still
wanted (5 cavalry that actually FIGHT and usually win, rather than never being attacked), that is a separate,
larger change. Original sketch kept below for that option.

Player report (enivicivokke, and others): couriers still get intercepted too often for some players, worse with
mods like Better Bandits Plus. Gabriel's ruling: NOT a "pay for guaranteed delivery" option (too gamey). Instead,
the tavernkeeper offers TWO choices when hiring a courier: (1) a STANDARD courier (today's lone rider, subject to
the `CourierWaylayPolicy` 1-in-3 gauntlet), and (2) a new ESCORTED courier, dearer, that spawns the courier party
accompanied by about 5 mid-tier CAVALRY, giving it a good chance to WIN a fight against a bandit band rather than
just outrun it. Build sketch: a second hire option in the tavern letter flow (a higher price); the escorted
courier's `MobileParty` is created with those 5 troops in its roster (so a waylay that catches it resolves as a
real skirmish the escort usually wins, instead of the abstract `TravelingParty.Destroy` loss); tune the escort
troop tier/count and the win odds. Standard couriers keep the current behaviour untouched.

## WISHED (player request, ProudSeal + AbbaDF 2026-07-12): an in-game NPC profile editor ("character card")

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

## BUILT, untested in game (player request, Fakade 2026-07-13; built 2026-08-20 Sonnet test-first): an AUDIENCE in a fief, from NPCs who have no party

SCOPE NOTE (v1): reuses `InterceptionPolicy.Choose`, which today only produces `HeardOfDeed` (and the road-only
`TerritorialPatrol`), so a fief audience currently fires when a partyless NPC present in the settlement has heard of
a recent player deed and wants a word. Two more reasons were then ADDED to `InterceptionPolicy` (2026-08-20), enriching BOTH
the road interception and the fief audience at once: a live significant GRUDGE against the player (Grudge, comes to
air it) and a significant recent APPRECIATION (AppreciationOwed, comes to acknowledge it), the mod's two signature
affect ledgers. STILL deferred as interception triggers (they already have their own channels, so adding them would
double it): an outstanding quest (the journal), a pending letter's subject and a stance verdict (the letter channel). Accept/Decline inquiry (never forced), rides the "Lords Approaching You" pacing + a per-NPC cooldown, and
yields to a companion audience so at most one popup fires per settlement entry. Untested-in-game risk: the chat-open
path (`OpenChatForInterceptingHero` with no live vanilla conversation) is only unit-covered at the gate, not the
engine open.


> *"Is it possible for this to happen with NPCs that do not have a party? If I enter a fief, an NPC may want a
> word with me. Maybe get an option box where the player can choose to accept or decline the interaction? I don't
> want to be forced into a bunch of conversations, but it would be cool to see the NPCs hanging out in a fief get
> their chance as well."*

Today a lord can only ride out to meet the player on the ROAD (`InterceptionBehavior`, which needs a party to
move). That silently excludes everyone who has no field party: ladies, notables, garrisoned lords, the very
people who are standing around in a fief with something to say. They never get a turn.

The natural shape: when the player ENTERS a settlement, an NPC there who has a live reason to speak (a grudge, a
kindness owed, a pending letter's subject, an outstanding quest, a stance verdict) may ask for a word. This is
the same "does this NPC have something to say to me right now" question the interception triggers already
answer, so it should reuse `InterceptionPolicy.Choose` rather than grow a second, diverging one.

Two constraints the player named himself, and both are load-bearing:
- **Never forced.** An inquiry with Accept / Decline, not a conversation that simply opens. Declining must cost
  nothing.
- **Rate-limited.** It rides the same "Lords Approaching You" MCM pacing shipped in v1.30.14 (Never / Rarely /
  Occasionally / Often), including a per-NPC cooldown, or entering a busy town would open a queue of audiences.

Reuse, not from scratch: `PlayerPresenceResolver` (v1.30.14) already answers "who is in this settlement with the
player" as `PresenceTier.SamePlace`, which is exactly the candidate list. `InterceptionPacingRules` already owns
the frequency dial. `CompanionHappinessBehavior.RequestAudience` already shows the Accept/Decline inquiry shape.

## NEW PILLAR (player request, multiple, 2026-07-14): DUELS AND COMBAT FROM CHAT

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

## WITNESS LIVELINESS (player report 2026-07-15, ratified 2026-07-16): witnesses participate when concerned

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

## QUEST/ACTION PARSER HARDENING (kimi audit, ratified 2026-07-17): make weak LLMs safe at the form frontier

An audit found the multi-LLM risk concentrates at the "form frontier" between a weak model's text and the parser,
plus one confirmed correctness bug. Phased by impact/effort (parser first, it is where weak models fail).

- **Phase A (SDK parser, PURE, test-first): DONE 2026-07-17 (uncommitted, SDK 344 tests green).**
  - **A5 (CONFIRMED BUG):** an invalid `type` in [QUEST_ABANDON]/[QUEST_COMPLETE] parsed to null,
    indistinguishable from "no type", so QuestService WILDCARDED it onto the first quest, landing a false
    (-regard + grudge) on the wrong task. Fixed: a `TypeUnrecognized` flag on both claim models; QuestService
    ignores a named-but-unknown type instead of wildcarding.
  - **A1 (silent loss):** a [QUEST]/[ACTION] cut off by max-tokens (no close tag) was dropped silently. Now
    tolerated (`ExtractSectionTolerant` for the QUEST family + a trailing-unclosed [ACTION] catch); a truncated
    but unparseable quest trips QuestBlockMalformed so the player is told.
  - **A2/A3/A6:** lexical robustness (quoted / backtick values, markdown-bulleted lines, spaced keys folded to
    the canonical underscore, "500 denars" / "1,000" numbers, deadline capped at 90 days).
  - **A7:** an [ACTION] a model writes INSIDE its [DIALOGUE] (explaining the format) is no longer executed; the
    invented-tag change_relation recovery still scans the FULL text (the Fakade in-dialogue [RELATION CHANGE]
    case is the opposite intent and must still recover).
  - **A8:** the parser comment that wrongly claimed truncation was covered is now accurate.
  - **A4 DEFERRED (needs verification):** action-verb aliases / normalization needs confirming how the mod
    bridge matches action types (case sensitivity, canonical names, `give_money` is used in existing tests)
    before touching the dispatch.
- **Phase B (game-side nets, mod): 5 of 6 DONE 2026-07-17 (uncommitted, mod compiles; validate in game).**
  - **B1 reward net ✅** (`QuestService.SettleUnclaimedRewards`, fired from `ApplyResponse` when the model
    claims no completion): pays any `IsAwaitingReward` quest the LLM never closed with [QUEST_COMPLETE]. SAFE
    because `IsAwaitingReward` = the deed was already verified by a real event, so it never pays for unfinished
    work; once paid it is Completed so it never fires twice. Generalises the ProvideGold-on-payment fix. Shared
    completion core extracted to `SettleQuest`. Guarded by `cr.test reward_net`.
  - **B2 ✅** at most ONE `change_relation` applies per turn (a weak model's several negative blocks can no
    longer pile the RelationGate's per-call -15 floor into -75). `relationChangedThisTurn` in the dispatch loop.
  - **B3 ✅** each `ExecuteAction` is isolated in try/catch, so one executor throwing no longer aborts the rest
    of the turn or the `Store.Set` that persists it.
  - **B4 ✅** (partial) `give_gold` logs a missing/unparsable amount instead of returning silently.
  - **B6 ✅** `give_gold` is clamped to the giver's real purse (no conjuring coin, no negative purse).
  - **B5 DEFERRED:** `end_conversation` only when `speaker.IsMain` needs tracing all exchange paths (main / 15B
    routed witness / 15C / captive) before guarding, or a legitimate end could break. Low value, nuanced.
- **Phase C (prompt, multi-LLM): DONE 2026-07-17 (SDK 352 tests green).**
  - **C1 ✅** a fully-FILLED [QUEST] example (real type / verbatim target / rewards / description / close tag)
    after the placeholder template in `AppendQuestInstructions`, and a filled [ACTION] example in
    `AppendActionInstructions`. Weak models copy a concrete example far better than a placeholder.
  - **C2 ✅** the Lean contract now SHOWS the `[QUEST_COMPLETE]` skeleton (gated on EnableQuests), not just asks
    for completion.
  - **C3 ✅** an explicit lexical rule in the Full format contract: labels are UPPERCASE, opened `[LABEL]` and
    closed `[/LABEL]` on their own lines.
  - **C4 ✅** the FORMAT REMINDER ends on a literal bracketed skeleton, so the last thing the model reads is the
    exact shape, not "as taught above".
  - **C5 ✅** a feedback note injected the turn AFTER a [QUEST] block was refused (QuestBlockMalformed):
    `EncounterContext.LlmFormatFeedbackNote` + a transient per-hero `LlmFeedbackNotes` store (set by
    `QuestService`, consumed once by `EncounterContextBuilder` on the next real LLM turn), rendered as a "NOTE ON
    YOUR LAST REPLY" block so the MODEL learns of the refusal, not just the player. (Action-refusal feedback,
    from the bridge default case, is a natural later extension.)
- **Phase D (per-model conformance telemetry): DEFERRED (highest effort, lowest immediate value; audit agrees, last).**

## ✅ Shipped & built

Live on Nexus, or built + committed awaiting upload. **(untested)** = built green and committed but its
in-game wiring is not yet validated. The bulk of the items below shipped together in **v1.14.0** (packaged +
committed + pushed 2026-06-25; awaiting Nexus upload) — still untested in game unless validated since.

**Conversation & memory core** — persistent per-NPC memory across the save; in-character free-text LLM
dialogue woven into vanilla; relation + multi-axis **stance** (trust/respect/fear, "words nudge, deeds
prove"); hearsay about third parties; culture-aware via editable `ModuleData`; story log; encyclopedia
integration (personal regard, discoveries).

**Quests & negotiation** — informal quests verified by real game events (no ghost quests); join a kingdom as
mercenary; recruit a companion (pay in goods); **recruit a lord into your clan** (`join_clan`); the
negotiation framework — **deliver-a-prisoner** + **declare-war** deeds (Phases 1 & 2), and **Phase 3
"intimacy as currency"** (adult-gated, exploiter archetype, persistent leverage) **(untested)**; **talk to a
prisoner — bargain for freedom/rescue** (cases A & B) **(untested)**.

### Viable-quest referential, a spoken offer ALWAYS registers *(ratified 2026-07-08, 🔨 BUILT 2026-07-08, untested in game)*
Root cause of "the NPC offered a mission but no quest was recorded" (recurring player reports): the LLM was
taught 17 quest types in the ABSTRACT, blind to the world, so it invented targets `QuestFactory` refused, and
every refusal was SILENT (the spoken promise stood while the game registered nothing). Ratified answer =
PREVENTION over repair: before the conversation, `ViableQuestScanner` (mod) scans the live world for the tasks
THIS giver can truly hand out (a real still-infested lair near his ground via the factory's OWN primitives, a
real enemy lord/village/army when his realm wars, a real captive ally, a kinsman for a letter, so there is
ALWAYS something to do), and the prompt teaches ONLY that grounded menu, targets named verbatim
(`EncounterContext.ViableQuestMenu`; generic catalogue = SDK fallback). Registration then succeeds by
construction. Pure formatting core `QuestMenuComposer` (cap 8 lines, 5 tests); cached per giver per day.
Plus the pairing contract (a task EXISTS only with the [QUEST] block in the same reply) and NOTHING-IS-SILENT:
a malformed/truncated [QUEST] block is flagged (`ParsedResponse.QuestBlockMalformed`) and told to the player in
chat, as are the one-task-at-a-time and ungroundable refusals (`notify` in `IssueQuest`). `quest_menu` self-test.
**INCREMENT 2, PERSONAL SUPPLY QUESTS: 🔨 BUILT 2026-07-08 (untested in game).** New `QuestType.ProvideGoods`
(count-based, lords only for inc1, ratified A=count/B=lords 2026-07-08): a lord asks the player to bring a COUNT
of a category (horses / livestock / grain), handed over for real via the bag. Pure `ProvideGoodsPolicy` (category
normalization + count clamp, 4 tests); `SupplyItemMatcher` maps a category to runtime items (ItemType.Horse; the
livestock/grain `ItemCategory` StringIds); `ItemPickerService.ShowSupplyPicker` (category-filtered); bridge
`SettleSupplyQuest` tallies the COUNT and COMPLETES on delivery (takes exactly the required count, pays gold+regard,
closes, like the ProvideGold-on-payment fix); `QuestFactory` grounds it (unknown category refused); taught in
`AppendQuestInstructions` (category + required_count fields) and always on the viable-quest menu for a lord.
`supply_quest` self-test + SDK parser test. STILL OPEN (increment 2 remainder): **NOTABLES asking for supply
(a headman/merchant, bigger surface)**, and **MISSION/espionage types (scout a settlement/lord beyond scout_army;
each needs its own QuestDeedTracker verification).** Next per the plan: the editable appearance Encyclopedia field,
then the JPG/PNG portrait spike.

### Prisoner trade, both ways for gold *(ratified 2026-07-08, 🔨 BUILT 2026-07-08, untested in game)*
Player report ("we must be able to exchange prisoners, add them to the item+denars window") ratified by Gabriel
with BOTH directions and gold: it opens the bounty-hunting loop (a lord wants a man and names a price; a player
who wants a prisoner can simply buy him). Two `IConversationVerb`s: **buy_prisoner** (the NPC pays the player
for a hero captive the player's party holds; the sold captive plants the DeliveredPrisoner grudge, heavier when
sold to his enemy) and **sell_prisoner** (the player pays for a captive the NPC's clan holds, custody via
`CourtActionResolver`, purse checked). THE BRIDGE IS LAW on price: the LLM negotiates in-fiction but the figure
is clamped by the pure `PrisonerTradePolicy` (6 tests) around the game's OWN ransom valuation (floor 40%, cap
3x, absolute min 50): a silver tongue haggles within reason, never into absurdity. Teachings ride
`ExtraActionTeachings` (each verb gates itself on live holdings, captives + fair-price anchors named); a
**Sell Prisoner** chat button (Give Gold pattern: picker pre-fills the input); the viable-quest menu gains the
wartime `deliver_prisoner` bounty entry ("bring me any of their lords in chains and be paid"). `prisoner_trade`
self-test (real capture, real bridge purchase, clamp proven with an absurd ask, cleanup frees the lord).

**World & influence** — world-event/rumor store (battles, sieges, marriages, executions, wars) with
geo/faction/recency-weighted hearsay; the **Chronicle**; commoners + settlement intel; **NPC↔NPC schemes**;
**NPC-initiated interceptions** (a lord rides out to challenge you); **power-balance** context block; the
**influence loop** (NPCs speak of *your* deeds, attributed + valence-coloured: kin/faction Wound vs Boon)
and the **"talk of the realm"** line.

**Chronicle enrichment** *(built 2026-07-04, untested in game)* — the player Chronicle grown from a war
register into a dynastic biography: 14 new entry types (childbirth, dynastic succession, consort, joining/
leaving/founding a kingdom, a fief granted, tournament victory, capturing a lord, being captured/freed, a
large field battle without armies, a grudge made peace, a pledge fulfilled at the player's word, a famous
captive freed/delivered) each writing `WorldEvent.ChronicleLine` from its own hook, guarded against
double-counting (once-per-lord capture, custody-handoff dedup, hidden bastard births excluded). Entries now
group under yearly chapter headings in both the text render (`cr.chronicle`) and the illuminated viewer;
new empty-state invitation line. Pure additions: `CalradiaRemembers.Logic.ChronicleCalendar` (year math) and
`BattleNotability.PlayerBattleSizeThreshold` (150 combatants), both unit-tested.
**Notable intimate conquests (2026-07-04, player-requested, BUILT, NOT committed).** Two further entry types, both
gated on `ModSettings.AdultContentLevel >= Mature` AND the partner being a hero of standing (`IsLord`; never a
random bandit-camp captive), via the pure `IntimateChroniclePolicy` (tasteful register at Mature/Explicit, bawdier
at Hardcore, never explicit): **a captive conquest** (a lord/lady of standing taken in an intimate player-as-captor
scene, `ChatViewModel.RecordCaptiveConquestChronicleIfNeeded`, deduped per captive) and **an heir got** (conception
upon a hero of standing when the player is one of the two parents, `PregnancyService.RecordConceptionChronicleIfNeeded`
— the begetting beat, distinct from the childbirth entry `OnGivenBirth` already records so the two never duplicate).

**Stance with teeth** *(increment 1 built 2026-06-27, untested in game)* — the earned multi-axis posture now
ACTS, not just colours dialogue. Pure `StanceConsequencePolicy` maps a posture+traits to one warranted
consequence (ColdShoulder → Threat → Plot → Intercept → Assassinate / WarmWelcome → Favor → Warning), with
honour staying a lord's hand from murder/scheming and terror ruling out striking in person. **1a (conversation):**
`EncounterContext.StanceConsequenceHint` colours how the NPC ACTS this meeting (a cold-shoulder won't deal, a
secret killer stays falsely civil, a friend offers aid/would warn you). **1b (proactive):** `StanceConsequenceBehavior`
weekly turns OPEN enmity/regard into an unbidden letter (Threat/Intercept → cold/challenge; Favor/Warning → warm
overture); secret hatred (Plot/Assassinate) writes nothing. Self-tests `stance_letter` + chat `chat_stance_assassin`/
`chat_stance_favor`. OPEN: **increment 2** = wire Plot (scheme vs player) + Intercept (reuse InterceptionBehavior) +
Warning (warn of a real scheme). **increment 3 — the town ambush — WORKS in-game, still OFF behind the experimental
MCM toggle `TownAmbushEnabled` until polished:** Assassinate arms `NpcProfile.StanceAmbushArmedDay`; on entering a
town `TownAmbushBehavior` rolls `AmbushPolicy` → narrated inquiry (Draw / Slip away) → a fight at the gate. Console
`cr.ambush`. **2026-06-30 refinements (BUILT, NOT committed):** an elite chief of the paymaster's culture at the
player's clan tier leads bandits in the Blackened "stealth" set; force scales (N+1)² with the defending side (solo 4
/ two 9 / three 16 / four 25, no cap); companions are captured alongside the player; capture is armed even when the
player falls first; recurring captive scenes are dungeon/fortress-framed when held in a town/castle.
**The captivity arc (Gabriel 2026-06-27), the lose branch:** 3.2a lose → captured + convoyed to the commissioning
lord ✅; 3.2b arrival → the lord's reckoning audience ✅; 3.2c convoy **rescue ambush** by your companions (BUILT
2026-06-28); 3.2d **Hardcore companion-victim captivity scenes** — the captor torments/assaults a held companion
before the player, or coerces the player to do it (revenge driver). **Phase 2 BUILT 2026-06-30; Phase 3 ✅ SHIPPED
v1.26.0 (2026-07-07): auto-fire in CaptiveEncounterService (CompanionScenePolicy gate, 45%, variant pick) + the full
aftermath on scene close (pure `CompanionAbuseConsequencePolicy`): grief stance-crater on the companion's bond
(Trust/Affection, deeper when the player was coerced), a REVENGE grudge vs a NAMED captor (new append-only
`GrudgeSource.CaptorCruelty`, resolution=Deed, coexists with the vs-player CaptiveAbuse grudge), and the atrocity
chronicled as a world-event. `companion_revenge` self-test.** Reuses CR's captive-scene chat.
**In-game-test feedback round (Gabriel 2026-06-30, BUILT, NOT committed):** (a) the chat "scroll down to read more"
hint no longer flashes up the instant a scene's opening narration runs a line long — it appears only when a
substantial block of text sits below the fold (`MoreHintRevealMargin`). (b/c) **the captive companion is now a REAL
participant, not a name in the prompt:** threaded as a real Hero (a witness flagged `IsCaptiveVictim`), so they get
their own portrait, their own VOICE (cries/pleas/defiance via the existing `[WITNESS_REACTION]` channel — now also
collected on the Continue path, which previously left them mute mid-scene), a dedicated colour, and the CORRECT
gender in the prompt (`CaptiveVictimIsFemale` → pronoun rules; fixes the model borrowing the player's sex). The
victim is excluded from the captor's-men / humiliation tallies (`CountAggressorWitnesses`). (d) **Continue-sequence
quality:** the in-scene Continue stimulus + the "Please continue" resume text now push ADVANCE + fresh vivid
description + NO verbatim reuse (the repetition/flat faults Gabriel saw). The continuation stimulus is extracted to
`CaptiveSceneText.BuildContinuationStimulus` so the new self-tests exercise the exact runtime prompt. New LLM
self-tests `chat_captive_continue` (opener + 4 Continue beats → asserts every beat replied + NO 6-word verbatim
phrase reused across beats, logs description coverage for human judgement) and `chat_companion_continue` (same, plus
asserts the companion is actually VOICED in its own `[WITNESS_REACTION]` lines + a gender-correctness judge note).
**OPEN:** richer per-victim captivity memory at scene close (today the victim gets none — a Phase-2 revenge hook);
the companion-scene auto-fire still console-only.

**Captive scene replayability (Claude 2026-07-01, BUILT, NOT committed):** the same captor used to always run the
same scene, because intent selection was a deterministic priority ladder and the per-round scene "focus" pool was
one small, single-register list. Two fixes: (1) `CaptiveEncounterPolicy.SelectIntent` is now a WEIGHTED draw over
the six lord/sexual intents (Interrogation/PersonalDesire/Domination/Torture/Training/Reward), pure and seeded by
an injected `Func<int,int> rollPercent` (mirrors `SelectBanditIntent`): kinks and relation are leanings that boost
weight, not hard gates, so a sadist LEANS toward Torture but is not locked into it. Interrogation/PersonalDesire/
Domination/Torture always keep a floor weight above zero (no intent is ever impossible); Reward needs at least a
lukewarm relation and Training needs the Training kink to enter the pool at all; strong contempt (relation <= -15)
boosts Torture hard and zeroes Reward; strong warmth (relation >= 20) boosts Reward/PersonalDesire and cools
Torture (never below the floor). (2) The scene-focus text pool (`CaptiveSceneText`) is split in two: the DARK pool
grew from 14 to 26 varied entries (psychological manipulation, weaponized tenderness, corruption, forced
complicity, staged humiliation, hope-and-denial, sensory play, ritual, quiet dread, alongside the original
physical/dominance ones) serving every intent except Reward; a new 7-entry POSITIVE pool serves Reward only (real
pleasure, praise, favored-possession framing, earned privileges, gentling). Selection goes through a new pure
`CalradiaRemembers.Logic.SceneFocusSelector.PickIndex(poolSize, lastIndex, roll)`, which never repeats the
immediately-previous focus (state lives in `ChatViewModel._lastSceneFocusIndex`, reset to -1 on a fresh scene).
**Bugfix, same batch:** "Please continue" on a non-sexual scene (Reckoning/Extortion/Intimidation/Revenge) was
unconditionally injecting the sexual scene-focus setup on resume; the opener path already guarded this, the resume
path now mirrors the same guard. 12 new tests in `CalradiaRemembers.Tests` (weighted-draw invariants +
`SceneFocusSelectorTests`); the `chat_captive_continue`/`chat_companion_continue` self-tests still exercise the real
build calls and stay green. **OPEN:** in-game validation of the variety (same captor across several sessions);
tune the weighting constants against player feedback.

**Captive scene director variety (Claude 2026-07-01, BUILT, NOT committed):** the content-variety fix above
still walked the SAME fixed dramatic structure every time (Intro, Initiate, Intensify, Climax); the director now
varies the STRUCTURE too. `CaptiveSceneDirector`'s lead opening act is now chosen from a named `SceneArc`
(SlowBurn: Intro/RisingTension/Initiate/Intensify/Climax; Classic: today's baseline; Sudden: Intro/Initiate/Climax,
no build-up), drawn by a new pure `CaptiveSceneArcPolicy.Select` (weighted, same style as `SelectIntent`): a
bandit leans Sudden, a lord after Domination or Training leans SlowBurn, PersonalDesire is balanced between
SlowBurn and Classic, and Interrogation/Reward/Torture lean Classic; every arc keeps a floor weight so it can
still be drawn regardless of context. Two new SDK stages close a gap testing surfaced: `RisingTension` (the
captor circles, inspects, closes distance, builds dread through word/gaze/touch, institutionalizing the
"play with the prey" psychological build-up that was being skipped) and `Aftermath` (a single come-down beat
after the final Climax: the prisoner's state, parting words, any mark left, the room going cold again, before
Conclude). The tail is now uniform across every arc: ...Climax -> Aftermath -> Conclude, `Aftermath` a normal
beat the player advances through with Continue (`IsConcluding` still keys strictly off `Conclude`). Also added:
a per-stage textual intensity curve 1..5 (`PromptBuilder.StageIntensity`: Intro=1, RisingTension=2, Initiate=3,
Intensify=4, Climax=5, Aftermath=2, Conclude=1) injected as an "INTENSITY THIS BEAT" line in the stage directive,
so the model's graphic explicitness follows the beat instead of staying flat. Register (bandit crude-thug voice
vs. lord cold-command voice) stays a separate prompt concern keyed off `captorIsBandit`, unaffected by arc choice.
15 new tests (`CaptiveSceneArcPolicyTests` + `CaptiveSceneDirectorTests`) plus 3 new SDK tests pinning the
intensity curve. **OPEN:** in-game validation that RisingTension and Aftermath read well against a live model,
and that the arc distribution feels right per captor type.

**Fix: a dead quest-giver's quests now close (Claude 2026-07-01, BUILT, NOT committed, player-reported):** a quest
whose giver died mid-campaign lingered in the journal (including the awaiting-reward case: the deed was done and
the log said "return to X to collect", but X had died). `QuestJournalBridge.ReconcileOnSessionLaunch` already
closed dead-giver mirrors, but only at load, so nothing fired mid-session. New `QuestService.CancelQuestsOfDeadGiver`
closes every still-Active quest (Active covers both an outstanding task and one awaiting a reward) as Cancelled with
a death reason, with NO relation penalty and no broken-word (a giver's death is not the player's fault). Wired twice:
the `HeroKilledEvent` (mod `NpcMemoryCampaignBehavior.OnHeroKilled`) closes them the instant the giver is slain, and
a daily safety-net branch in `RunDailyMaintenance` catches any that slipped through (or a giver already dead in a
loaded save). Cancelled rather than Failed on purpose: the red-failure register would wrongly imply the player broke
their word. **OPEN:** in-game confirmation, and a call on whether Gabriel prefers the failure register after all.

**Companions (the 2026-06-23 trilogy, untested)** — **missions** (news / spy / steal / barter / envoy — a
real physical journey: the companion leaves as a clan party, rides to a town, returns and reports in
conversation; save-safe + capture-resilient + anti-exploit; **recall** — order an away companion to abandon
the errand and come home, by conversation *or* by letter, as a verified deed, not a hollow agreement);
**happiness & retention** (an encyclopedia
"Contentment" gauge, errand/idle drivers, an in-conversation grievance, reassurance, and departure when too
long unhappy); **camp life** (companion↔companion rapport from trait compatibility, voiced opinions, and
companion weddings that keep the couple in your clan).

**Dynasty & adult** — **dynastic succession** (the heir inherits standing/debts/grudges, not the private
history; deterministic); marriage politics (family-approval, consorts); **jealousy**; captor/captive scenes
(Hardcore); **player-authored character data** (`character_overrides.json`).

**Tech** — multi-provider LLM incl. local (OpenAI-compatible endpoint covers Ollama/LM Studio/vLLM);
**setting-agnostic overhaul support** (`setting.json` → reskin for The Old Realms & co. as a data pack);
parchment letter window + visible couriers; **prompt-cache split** (stable prefix cached, dynamic tail fresh).

**Hardening batch (2026-07-01, built by agents, untested in game unless noted):** a session of test/infra
fixes with no intended player-facing behavior change. The SDK test suite was silently not executing under
`dotnet test` (missing `Microsoft.NET.Test.Sdk` + adapter package), now fixed, SDK runs 170+ tests. Parser
hardening: the bracket-scrub is anchored (was catching stray mid-line brackets), plus full parser coverage
for [ACTION]/[QUEST]/aliases/[WITNESS_REACTION]/[DISCOVERY], and golden-ordinal tests pin `NotableEventType`
and `Kink` enum values against accidental reordering (a save-compat guard, since both persist by ordinal).
`ProfileMutator` is now the single mutation path, with a reputation clamp. `SafeRun.Step` now wraps every
campaign-tick subsystem so one throwing behavior can no longer freeze the whole tick. `JealousyBehavior` and
`MarriageApprovalBehavior` migrated off the native-string `SyncData` (the exact pattern behind the v1.6.2
save-corruption bug) onto the new `JsonCampaignState` helper, with legacy migration for existing saves. SDK
prompt overhaul: the cache-split fix moves per-turn directives to the dynamic tail, one global action
contract, a worked few-shot example, a letter-language fix, captive pacing consolidated, and a Lean-prompt-v2
pass trimming the format machinery for small/local models.
**This session's own slice:** `ModCheats.cs` (3496 lines, ~120 console commands) split into domain partial
classes, command names/strings/bodies untouched (pure reorg, files now under ~600 lines each).
`RomanticProfileBuilder`'s pure core (trait-based sketches, orientation, preference/kink pools, distinct-pick)
was extracted to `CalradiaRemembers.Logic.RomanticProfilePolicy`, behavior-preserving (same `Random` call
order, so existing NPCs' frozen kinks/preferences do not shift), with new Gherkin tests for the trait
correlations, orientation bands, pool distinctness, and determinism. The bandit-captor scene rolls
(gender-weighted spoil intent, collective-scene chance, participant cap) moved next to the lord-path
equivalents in `CaptiveEncounterPolicy`, tested the same way. The Extension Surface now honors
`ExternalWorldEvent.Lifetime`: a fed Deed is kept, under its own 50-entry cap, oldest dropped first (a
tuning choice, flagged for review, kept smaller than the player's own chronicle cap so a flood can't crowd
it out), instead of aging out as a rumour after 42 days. It also ships `Feed.ApiVersion` for reflection-based
capability detection by third-party callers, and the per-source send-quota dictionary is now lock-guarded
against concurrent callers.

**Dual model modes, roleplay + NSFW (2026-07-01, built, untested in game):** born from a player request
(hot-swapping models for NSFW versus story roleplay was blocked by the modal chat window). Two MCM fields:
the existing model becomes "Model (roleplay)" and a new optional "NSFW model" (LLM Provider group, works
with whichever provider is active, blank means the roleplay model serves everything). Structurally-explicit
scenes (captive encounters, player-as-captor scenes, companion-victim scenes) open on the NSFW model
automatically; ordinary conversations open on the roleplay model. A muted "Mode: Roleplay / Mode: NSFW"
button in the chat title bar shows the active mode and flips it (a manual override, e.g. a consensual scene
that escalates); the model is resolved live per request, so the switch applies on the very next exchange.
The mode is deliberately NOT reset on chat close, so the async captive-scene memory summary still resolves
the NSFW model after teardown. Pure selection rules in `CalradiaRemembers.Logic.ModelModePolicy` (Gherkin
tested); `cr.model [toggle]` reports and flips it from the console.

**MCM menu reorganized (2026-07-01, built, untested in game):** the options screen now reads in usage order:
all LLM configuration first (the shared LLM Provider group, then one section per provider: OpenRouter, NanoGPT,
OpenAI-Compatible), then gameplay (Content, Character, Companions), Diagnostics last. Fixed two GroupOrder
collisions (OpenRouter/Character both 3, NanoGPT/Companions both 4) that interleaved gameplay groups into the
LLM sections, and normalized every in-group Order to ascending (key, options, Test button last); the old negative
orders compensated a since-fixed MCM rendering flip and half the fields no longer followed them. Verify the
rendered order in-game once (an outdated MCM would show groups internally reversed; if so, flip the signs back).

**Both model fields consolidated in the shared group (2026-07-01, built, untested in game):** "Model (roleplay)"
joins "NSFW model" in the LLM Provider group, resolving the asymmetry where the NSFW model was provider-agnostic
but the roleplay model lived per provider. The per-provider "NanoGPT Model" and OpenAI-Compatible "Model" fields
are REMOVED; the shared field serves every provider (id pasted in the active provider's format, examples in the
hint). Provider sections now hold only connection concerns: key, endpoint, Reasoning Effort / Parameter
Compatibility, Test button. Resolution guard: for NanoGPT/Custom, a blank field or the untouched OpenRouter
default (x-ai/grok-4.20) resolves blank, so the existing provider-aware "set your model" in-chat guidance fires
instead of a cryptic API error. MIGRATION NOTE for the release changelog: NanoGPT and OpenAI-Compatible users
must re-enter their model id once in the shared field (their old per-provider value is orphaned in the config
json); OpenRouter users are untouched (same persistence key). The Test Connection / Test Endpoint buttons now
one-token-test EVERY configured model: roleplay first, then the NSFW model when set (a misspelled NSFW id must
fail on the Test button, not mid-scene when the mode flips); the verdict popup reports each model's status.

**Reasoning models no longer fail the Continue button (2026-07-01, built, untested in game, player-reported):**
with MiMo/GLM a scene continuation died on "[SCENE] continuation failed: Response contained no message content."
Root cause in the SDK client: a reasoning model can spend the ENTIRE completion budget thinking, returning a
null content with the reasoning text and finish_reason "length"; the parser turned that into a hard failure,
which also bypassed the existing length retry (it only fired on successes). Fix: such a body now parses as an
empty length-truncated success; CompleteAsync retries it once with a DOUBLED token budget (room for prose after
the thinking); if both attempts come back textless the player gets an actionable error naming Reasoning Effort.
Regression-pinned in new `OpenRouterResponseParsingTests` (SDK internals opened to the test suite via
InternalsVisibleTo). Grok was unaffected because it honors the low Reasoning Effort default. Second prong,
from Gabriel's own play observation (typing "*waiting to see what happens next*" avoided the failure): the
Continue stimulus was a six-line wall of meta-instructions that reasoning models treat as a planning puzzle,
which is what triggered the budget-eating deliberation in the first place. `BuildContinuationStimulus` now
opens with a short diegetic line (the scene waiting) and keeps only the two load-bearing constraints (advance
without recap, never repeat); the full anti-repetition rules already live in the system prompt. Third prong
(first in-game retest still showed silence): the retry now covers ANY empty successful reply, not just
length-truncated ones (reasoning models can close on reasoning alone with finish_reason "stop"), and the
continuation path logs its start (model + mode) and fails LOUDLY on an empty beat ("(The moment passes.)" +
error log) instead of silently rendering nothing.

**Per-mode Reasoning Effort (2026-07-01, built, untested in game):** the effort follows the mode like the
model does. The dropdown becomes "Reasoning Effort (roleplay)"; a new "Reasoning Effort (NSFW)" (OpenRouter
group) defaults to "Same as roleplay" (existing configs unchanged) and typically goes LOWER (Low/Off: fewer
moralizing refusals, faster scene beats) while roleplay can afford Medium/High for finer dialogue. Pure
`ModelModePolicy.ResolveReasoningEffort` (+4 tests, 629 total); `cr.model` reports the resolved effort.

**Captive pacing rebalance (2026-07-01, built, from Gabriel's play observation):** the morning pacing
consolidation overshot: with the "COMPLETE sequence" block folded away and AppendDialogueStyle suppressed in
scenes, the loudest surviving voice was the brevity rule ("a few sentences"), and Grok obeyed: terse one-line
beats, action without psychology (in-game report: the scene rushed forward and lost the menace/insult/
psychological register). Rebalanced with ONE authority intact: the brevity rule becomes "ONE BEAT PER TURN,
FULLY REALISED" (several spoken sentences of menace/taunting + one substantial [NARRATION] paragraph; the
enemy is REPETITION, not substance) and the stage directive header adds "a beat is a whole moment of the
story, not a single line". In-game re-validation needed: beats should be rich again WITHOUT the old 1-2-2-2
repetition returning. Cache measurements from the same session: cached: 64/768/7616/7616 across beats, i.e.
the stable prefix caches from beat 3 (the split fix works on Grok); the remaining ~2.9k/turn uncached is
history + dynamic tail (the automatic-prefix lever below). Grok's slowness = reasoning burn (3.4-4.8k
completion tokens for one-line replies): the per-mode Reasoning Effort (NSFW) = Off/Minimal is the intended
remedy.

**Retry observability + cache visibility (2026-07-01, built):** `LlmResponse.WasRetried` flows to
`NpcChatResult`; the [TOKENS]/[SCENE TOKENS] log lines say "RETRY fired" when the bounded second attempt
served the reply (token counts alone cannot reveal it: OpenRouter's completion_tokens includes reasoning
tokens, which is why Grok's beat looked like a retry when it was a single call). [SCENE TOKENS] and
[15C TOKENS] now also print the cached-token count like the main path.

### Prompt-cache: the automatic-prefix regime *(analyzed 2026-07-01, next cost lever, TO RATIFY)*

Two cache regimes exist. Anthropic-style EXPLICIT caching (cache_control breakpoint) is what CR optimizes
today, and the 2026-07-01 split fix serves it. But xAI/Grok and OpenAI use AUTOMATIC longest-common-PREFIX
caching over the whole request (system + history): the cache_control marker is ignored, and any per-turn
change in the system prompt's dynamic tail (scene stage directive, NAMES MENTIONED) cuts the common prefix
there, so the WHOLE conversation history is re-billed full price every turn on Grok, the default model.
Proposed fix (medium, SDK): move per-turn context out of the system tail into the wire-level LAST USER
MESSAGE (SillyTavern author's-note pattern), persisted in the wire history so the prefix stays append-only.
Each turn then bills only the new message at full rate; everything else becomes cache reads (4-10x cheaper).
Trade-off: past turns' context blocks stay in the sent history (~200-300 tokens/turn at cached price), and
the model receives encounter context as user-role content, a creative-core change that needs in-game
validation. MEASURE FIRST: play a Grok captive scene and read the new (cached:N) figures in [SCENE TOKENS];
if cached stays ~0 across beats, this is the biggest remaining cost lever. Also still open: compress the
Full-mode 16-type quest list (~300-400 tokens), the consolidated captive-prompt rebuild (~40% shorter), and
a second breakpoint splitting the universal header from the per-NPC block (Anthropic-style only).

---

## Open — not yet built

### Affect coherence reform: SHIPPED v1.22.0 (canonical RegardBands, semantic writer routing, grudge tone arbiter, Bearing line). Full design in CHANGELOG + git.

### Appreciations, event-anchored kindnesses: COMPLETE, shipped v1.22.0 (the positive mirror of Grudges; every kindness source symmetric to a grudge source). Full design in CHANGELOG + git.

### Grudges, resolvable event-anchored grievances: SHIPPED v1.22.0, Amends/Gift/Deed reconciliation complete, additive save migration. Full design in CHANGELOG + git.

### RP with teeth, giving conversations real consequences *(ratified 2026-07-02 — a new pillar)*

**Why this is a pillar.** CR's roleplay has always been rich in *voice* but thin in *stakes* outside the
handful of headline deeds (join_clan, marriage, prisoner bargains). A player can say almost anything to an
NPC and the world does not visibly answer back beyond `change_relation`. This pillar is the deliberate,
ongoing effort to close that gap: every increment adds conversation verbs that carry REAL vanilla
consequences, re-validated by the bridge ("the prompt is advice, the bridge is law"), never a hollow promise
the engine ignores.

**Increment 1 — four quick-win verbs. 🔨 BUILT 2026-07-02 (untested in game), 676 mod tests / 190 SDK tests
green.** `give_influence` (a lord lends the player political influence, scaled by relation, capped, clan
must afford it), `lend_troops` (a party-leading NPC hands over healthy regular soldiers, never heroes, never
below 60% of their own strength, bounded by the player's party room), `sway_opinion` (the player's case
genuinely moves the NPC's own regard for a named THIRD PARTY, for or against, with a per-pair cooldown and an
ambient world-event rumour), and `release_prisoner` (the player intercedes for a THIRD-PARTY hero prisoner an
NPC's clan holds, distinct from the existing `free_prisoner` case where the NPC is the player's OWN captive).
Architecturally this increment introduced the **generic extension surface** every future verb rides on:
`EncounterContext.ExtraActionTeachings` (SDK) — one host-composed, prompt-ready block rendered right after the
core GAME ACTIONS section (skipped in Lean mode), instead of a bespoke `EncounterContext` field and a
hardcoded `PromptBuilder` section per verb. The mod composes the block in `EncounterContextBuilder` from
`CourtActionResolver` (engine-coupled facts) + `CourtActionPolicy` (pure amounts/caps/cooldowns, tested).
Per-NPC (and per-NPC-and-target, for sway) cooldowns are derived by scanning the NPC's own `NpcProfile.Events`
for a hidden marker the action itself records — no new persisted field. Console: `cr.rp_verbs [name]`.
**Reciprocal court verbs (04/07/2026):** `give_troops` / `spend_influence` — the PLAYER-side mirror of increment
1's `lend_troops` / `give_influence`, letting the player reinforce a needy NPC's under-strength party or fund a
lord low on court influence, with a safety floor on the player's own reserve. They also CLOSE the Appreciations
pillar (the `TroopsOrInfluenceSpentForThem` source). Console: `cr.give_troops` / `cr.spend_influence`.

**Increment 2 — the TrackedCommitment engine. 🔨 Phase 1 (the mechanical spine) BUILT 2026-07-03 (untested in
game), 694 mod tests / 190 SDK tests green.** The symmetric of `InformalQuest`: instead of the NPC setting the
PLAYER a task, the NPC OWES the player something. The flagship (and, for Phase 1, only) kind is `pledge_against`:
a lord, moved by genuine and standing enmity, vows in conversation to move against a named rival — a POLITICAL
commitment that launches a real, existing scheme (`SchemeStore.ForcePlot`, slander or sabotage, reusing an
already-active scheme between the pair rather than erroring) and adds the "he promised YOU" accountability
layer schemes lacked on their own. **Never declares or causes a war** — see the dedicated future item below.
New pure `CalradiaRemembers.Logic.TrackedCommitment` (PromisorId/TargetId/Kind/IssuedDay/DeadlineDay/State) +
`CommitmentPolicy` (deadline = one season / 21 days, fulfilled = +6 relation/stance, breached = -12 relation +
a Trust crater). Eligibility in `CourtActionResolver.ResolvePledgeRivalName` reuses the kin-safe,
already-disliked target selection from `sway_opinion` (`CourtActionPolicy.SelectPledgeTarget`, new pledge-only
animosity floor), taught in `EncounterContext.ExtraActionTeachings` alongside the increment-1 verbs. The new
`CalradiaRemembers.Commitments.CommitmentBehavior` (byte[] SyncData, mirrors `SchemeStore`) holds every
outstanding pledge and detects fulfillment TWO ways — `OnSchemeResolved` (called from `SchemeStore.OnStateChanged`'s
Succeeded case and from `PlayerAssists`' success branch) when the launched scheme succeeds, or `OnMapEventEnded`
recognizing (never causing) any real battle with the promisor's party and the target/their faction on opposite
sides — and detects breach on a daily overdue sweep, retiring the pledge either way with a `StanceService`
kept/broken-promise call, a `NotableEvent`, and a `WorldEventStore` rumour ("X honored/broke their pledge to move
against Y"). One pledge in flight per lord. **Phase 2 ✅ BUILT (untested in game): the quest-journal mirror
(`CommitmentQuest`/`CommitmentJournalBridge`, a read-only `QuestBase` mirroring `CalradiaRemembersQuest`, opened
on Create, closed success/fail on fulfil/breach, `ReconcileOnSessionLaunch` anti-stuck pass), console
diagnostics (`cr.commitments`, `cr.pledge`, `cr.pledge_fulfill`, `cr.pledge_breach`), and the
`commitment_fulfilled`/`commitment_breached` self-tests in `cr.testall`. The mirror persists only
`_promisorId`/`_commitmentId` as additive SaveableFields on a new QuestBase subtype; an old save with no mirror
degrades gracefully (no journal entry for a pledge already in flight).**

**Increment 3 — backing the player in a KINGDOM DECISION** (a vote, a policy, a succession). **Needs a spike
first**: Bannerlord's kingdom-decision API must be scoped before this increment can be estimated — the mechanics
may or may not be reachable without heavy engine coupling. Do not start building before that spike lands. (The
former coalition-package pairing with `pledge_against` is superseded: that verb shipped as increment 2's
flagship, scoped to a political scheme, never a war — see the war-declaration item below for why kingdom
decisions and war are being kept structurally apart from the pledge system.)

**Increment 4 — headliners.** The two biggest, riskiest, most narratively powerful verbs, deliberately last:
**defection brokering** (the player persuades an NPC to broker ANOTHER lord's defection, not just their own —
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

**Future — war declaration as a leader-only strategic decision (Gabriel, 2026-07-02/03, not yet designed).**
*"War declaration = a leader-only strategic decision, NEVER a conversation or pledge trigger. A dedicated future
system where the FACTION LEADER evaluates the clan's world situation, interests, relative strength, open fronts,
economy, allies, the target's vulnerability, and their own risk appetite, and declares war ONLY when it advantages
them; a leader who LOSES a war loses credibility, so that reputational downside is part of the calculus. Needs the
kingdom-decision / diplomacy API spike. The increment-2 pledge is POLITICAL (a scheme) only and must never declare
war."* Extended (2026-07-03): a lord who is NOT the faction leader CANNOT declare war, but CAN try to INFLUENCE
their leader on the question — so the war-decision system is a **council-pressure model**: vassals lobby, the
leader decides; accumulated vassal pressure (weighted by influence) is one input to the leader's advantage/
credibility evaluation. Natural future verb: a vassal pledges to "press my leader for war against X" (adds
pressure to the leader's evaluation) rather than declaring war themselves — the bridge from the increment-2 pledge
system to this future war-decision system.

**DONE (guardrail, 2026-07-30, SDK `71538cf`):** a lord may not conclude peace/a truce or arrange a binding
faction parley in conversation (only the ruler decides). Extended STAY WITHIN WHAT YOU KNOW after a player
report where a lord roleplayed a peace parley the engine then contradicted with a battle. Keeps the RP honest
about the limits of a lord's authority without killing emergent diplomacy talk.

### WISHED (Gabriel 2026-07-30): inter-faction relation scores that DRIVE war and peace
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

### Extension Surface — CR as the narrative-memory bus of a modlist *(wished 2026-06-25 — a new pillar)*

**Why this is the point, strategically** *(Gabriel, 2026-06-25):* this SDK is the purest expression of CR's
community pillar — and **the best possible answer to closed mods like AI Influence**. Where a black box says
"use my features or don't," CR says "build your own, and CR will give it a memory and a voice." Openness isn't
just an ethic here; it's the competitive moat. The mod that lets *everyone else* build on it wins the ecosystem
the obfuscated one can never reach.

**The strategic prize:** any mod that changes the world can *tell CR about it*, and CR turns that into NPC
awareness — chronicle, rumour, hearsay, in-conversation reference — with **no new plumbing downstream**.
CR stops needing to know about each mod in advance: other mods speak to it. This generalises the
"world-agnostic" goal of the scenario engine from *content packs* to *live interop*.

**The collaboration shape (ChatAI thread, 2026-06-25):** this is NOT about co-running CR with another LLM-chat
mod — two chat overlays on the same NPCs would conflict. The fit is a **complementary, non-chat companion mod**
(e.g. a "living realm" world-sim that makes lords quarrel/scheme/wager) that **feeds its outcomes into CR via
the SDK** and lets CR be the memory + voice. Their simulation, CR's remembering. That's how a collaborator
plugs INTO CR rather than beside it, without either mod dismantling itself.

The governing principle — and the thing that keeps it from becoming a coupling disaster — is that this is **not
"open the RAM"**. It is a narrow, one-way, **validated inbound contract**: outsiders *push facts in* through a
single door; they never read or mutate CR's internals. The day CR refactors `NpcProfile` or the store, third
parties don't break.

**Four disciplines (load-bearing — they protect the decoupled-architecture pillar):**
1. **Inbound, one-way.** Third parties *write* through the gateway; they don't read/mutate internals. (A public
   read API over memory is possible later, but writing is ~90% of the value.)
2. **A stable, versioned DTO at the boundary** = an anti-corruption layer. The public contract references
   neither CR's internal types nor raw Bannerlord types, so internals can change freely behind it. (This is the
   SDK's existing reason for being — here we add one stable façade on top.)
3. **Validation at the door.** Every external fact is normalised before it enters: magnitude clamped, **summary
   length bounded (the save-blob guard)**, hero/faction ids resolved-or-dropped, source tagged, lifetime
   defaulted. A careless modder can neither brick a save nor inject noise into prompts. **Plus dedup by event
   Id + a per-`SourceMod` rate/throttle** so a mod that calls `Record` in a loop (or re-emits the same event
   every tick) can't flood the store toward the save-blob limit — modelled on the ChatAI dev's
   `processed_world_event_ids` + per-category `last_generation_day` state (the one idea from his shared code
   that's a genuine upgrade; his `VerifiedFacts` concept was *considered and dropped* — CR's `Summary` already
   IS the authoritative fact injected as hearsay, so there's no recap-generation drift surface to pin).
4. **Source tag + relevance gating.** Each entry carries its `SourceMod` (debug / filter / mute a misbehaving
   source); CR's existing hearsay-locality logic decides *who* plausibly heard it, so NPCs don't recite
   everything.

**Volet A — external world-event ingestion (do this first; highest value, simplest). ✅ SHIPPED in v1.15.0 (Nexus 2026-06-27), validated in-game. + MUTABLE ENTRIES added 2026-06-27 (`Feed.Update` upsert-by-id, `Feed.Retract` remove-by-id) closing the Homesteads modder (Bhel)'s one-way-can't-change gap — a settlement that moves, an NPC renamed, a place destroyed.** A façade like
`CalradiaRemembers.Feed.Record(ExternalWorldEvent)` lets a mod push a fact into the **same** World-Event /
rumour store the Chronicle, influence loop and NPC-awareness already read. Once past the gateway, it follows
the exact path of CR's native events — a Pravend NPC who'd plausibly have heard mentions it on his own. The DTO
carries `Category`, `Summary`, `Day`, `Magnitude`, optional `HeroIds`/`FactionIds`, `Lifetime`
(`Rumor` ages out vs `Deed` kept), `SourceMod`. *Worked example:* a horse-racing mod logs winners and purses
as `Lifetime.Rumor`; CR's lords gossip about the grey mare of Lord Caladog with zero knowledge of CR's memory
system. Pure/testable in `CalradiaRemembers.Logic`: the validator/normaliser (clamp, bound, id-resolve,
default) and the DTO→`WorldEvent` mapping; only the public static façade + behavior registration is engine glue.

**Volet B — external mission registration. 🔨 BUILT 2026-06-27 (Opus, untested in game, 496 tests). Public API `CalradiaRemembers.MissionBoard.Register/Complete/Abandon` + DTO `ExternalMission` (plain strings, no Bannerlord types); reward route = A (giver-required, paid when the player REPORTS to the giver in conversation). Reuses the `InformalQuest` pipeline via new `QuestType.External` + `InformalQuest.ExternalId/ExternalSourceMod` (SDK, ordinal-safe), so it inherits journal mirror + framing + gated reward + deadline/abandon/self-heal for free. Pure `ExternalMissionRules` (bound text, clamp reward to CR caps, normalize deadline; 10 tests). Verification ownership: the external mod calls `Complete(id)` (the gate) → stamps `SatisfiedOnDay`/`Evidence` → giver acknowledges + pays on report. Self-test `mission_register` (register→dedup→complete) in cr.testall. Open: a giver-less (B-fallback) variant deferred; modder-facing docs + a v1.16 Nexus section pending.** A mod defines **a mission for the player** and registers
it into CR's verified-quest system, inheriting all of CR's guarantees: the **journal mirror**, NPC-facing
framing, reward gating, deadline/abandon lifecycle, and the **self-heal-on-load** that force-closes anything
dangling (no ghost quests from a third-party mod either). The hard part — and the line that preserves "the
bridge is law / no empty promises" — is **verification ownership**: CR must NOT pay a reward on a foreign mod's
unverified say-so. So the contract splits responsibility: **the external mod owns the completion signal** (it
fires `mission.Complete()` / supplies a predicate CR polls, because only it knows when its own deed is truly
done), while **CR owns everything else** — issuing, the journal entry, the in-conversation framing, the gated
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
contract from the start** (2026-07-03, see Marriage below) — the architecture validation the contract was
missing, since the four court verbs only proved a MIGRATION, not a from-scratch addition.

> NOT built (wished 2026-06-25). Sequence: A before B before C. All ride the SDK's existing engine-agnostic
> design. Linked to the scenario engine below (all are modder-extensible surfaces) and to the ChatAI-
> collaboration thread (an autonomous NPC↔NPC layer would write its outcomes back through Volet A so the
> "theater" leaves real marks the player later encounters — see also the four NPC↔NPC seams discussed but
> not yet scoped here).

**Long horizon — CR as core + swappable feature modules (dogfooding the surface)** *(wished 2026-06-25)*

The endgame: split CR into a **core** that owns the shared substrate (`NpcProfile`, the store, the bridge, the
prompt builder, the World-Event store, the SDK chat pipeline) plus a set of **official feature modules**
(`CR_Schemes`, `CR_Wedding`, …) that ride the *same public surface* a third party would use. The payoff is
dogfooding: if CR's own marriage is "just another consumer" of the API, then a third party's marriage mod is a
first-class citizen **by construction** — the strongest possible proof the extension surface is real, and a
direct expression of the community pillar.

**The hard truth that gates it — coupling is the identity, not an accident.** CR's features are NOT independent:
Wedding, Schemes, Jealousy, Pregnancy, Succession read/write the *same* `NpcProfile`, feed the *same* prompt,
share the *same* stores. That interplay (jealousy←pregnancy→succession→scheme) **is the product**. So a/b must
be distinguished, and the interplay protected at all costs (over-splitting into mods that no longer talk = the
worst failure):
- **(a) Internal modularity** — clean boundaries *within one shipped mod* (a `CR.Core` owning the substrate;
  features depend on it). Cheap, good regardless, and the **prerequisite** for everything below. CR already has
  the base (`CalradiaRemembers.Logic` + the SDK + per-feature folders).
- **(b) Deployment split** — separately-installable, swappable mods. The big move; do it **surgically**, only
  where a feature is genuinely substitutable and its support contract is bounded.

**Volet A + B are necessary but NOT sufficient for (b).** A detachable feature module needs a richer **Feature
Module SDK** beyond the event/mission feed:
1. **Contribute prompt sections** — registered, **ordered**, **token-budgeted** (the prompt has 16 ordered
   sections; N mods writing into it needs a priority registry or it's chaos).
2. **Contribute action vocabulary** + bridge handlers, through the conditional-teaching gate ("the bridge is law").
3. **Read/extend shared profile state through a contract** (not raw field access — that's the coupling we're
   trying to break).

**Sequence:** (a) internal modularity → Volet A + B → **pilot the split on the LEAST-entangled feature, not the
most.** → generalise only after the pilot holds.

**`CR_Schemes` is the pilot — and possibly the only feature that ever needs externalising.** It already has its
own store, its own daily tick, and already feeds rumours — the least entangled with the profile/prompt. Wedding
is the *worst* starting candidate (relations + family + consorts + prompt). Extracting Schemes as a separate mod
on the public surface becomes the **validation gate**: if it holds, the surface is proven; if it doesn't, we
learned cheaply. The working hypothesis (Gabriel, 2026-06-25): **most features should stay in core**, and the
deployment split is reserved for the few that genuinely earn it — Schemes may be the one.

**"Bring your own marriage" has two routes — prefer the light one.** (1) *Data route* — reshape marriage via
**scenario packs + config**, no code: CR is *already half-way* (the Rhagaea marriage is four rules in a data
pack), and the scripter/composer keeps growing its trigger+consequence vocabulary. For most modders, "your own
marriage" = authoring rules, not shipping an assembly. (2) *Assembly route* — replace the whole `CR_Wedding`
module; powerful but needs the full Feature Module SDK above. The data route tends to give ~80% of the value at
~20% of the cost; reserve the assembly route for cases that truly justify it.

### Scripted scenarios — a modder-extensible world-reaction engine *(wished 2026-06-24 — a new pillar)*

**Reactive rules, NOT authored storylines.** A branching narrative (Twine-style) fights the sandbox; CR's
thesis is the opposite — the world reacts, then the *player and the game engine* write what comes next. So a
scenario is a **stateless reactive rule**: *when this happens → the world shifts like so → and here is how it
reads*. No passages, no branches, no authored "next beat". (Twine was considered and dropped for exactly this
reason — see also the captive scenario bank.)

A rule has **four fields** (plus two optional):

| field | role |
|---|---|
| **Trigger** | the in-game event that arms the rule |
| **Guards** *(optional)* | eligibility predicates — scope which rules are even applicable now |
| **Consequences** | a list of declarative world mutations |
| **World-event message** | the situation text, dropped into the World-Event store → told in-game |
| *fire-once* *(optional)* | one-shot + persisted, vs repeatable |
| *weight* *(optional)* | bias the random draw; default uniform |

**Random pools by shared trigger.** If several rules share a trigger (and their guards pass), **exactly one
is chosen at random**. That single mechanism gives the replayability we want without any branching: the
four Rhagaea-marriage outcomes below are just **four rules on the same trigger**, one drawn and **sealed**
(persisted, anti-save-scum, like `CompanionMission.Quality`).

- **Triggers** ride native `CampaignEvents` (hero death, marriage, war declared, settlement taken, clan
  destroyed…) plus CR's own deed events.
- **Consequences** are declarative primitives CR mostly already owns: `ChangeRelation`, `ChangeInfluence`,
  `StartWar`/`EndWar` (`DeclareWarAction`/`MakePeaceAction`), `GiveFief`, `SpawnWorldEvent`/`SpawnRumor`,
  `InjectMemory`, `SendLetter`. The engine executes them (verified deeds); the rule never authors dialogue.
- **The message channel is already shipped.** The World-Event message plugs straight into the World-Event
  store + Chronicle + influence loop: drop the event, and NPCs speak of it on their own, valence-coloured by
  their side. The LLM only narrates at the player's point of contact.

**A keyword DSL + a composer app (the moddable surface).** Triggers, guards and consequences are expressed as
a small **controlled vocabulary of keywords** (`ON_DEATH:Rhagaea`, `IF_PLAYER_RULES`, `DECLARE_WAR:A->B`,
`CHANGE_RELATION:Hero:+N`, `INJECT_MEMORY`, `SEND_LETTER`…) — guards AND-combined, no arbitrary expression
language, so the engine evaluator and the authoring stay trivial. The keyword vocabulary IS the modder-facing
API; it serializes to JSON in `ModuleData`. On top of it, a small standalone **script-composer app** lets an
author build a rule by *picking predefined keywords from dropdowns* (no hand-written JSON, validated by
construction) and exports the file. Pure/testable in `CalradiaRemembers.Logic`: the trigger-matcher, the
guard evaluator, the random-pool selection, and fire-once bookkeeping; only the CampaignEvent hooks and the
consequence executors are engine glue. Define the vocabulary first — the app is downstream sugar.

> **BUILT 2026-06-24 (Opus, 412 tests green; committed `6b47a4d` + pushed 2026-06-25 in v1.14.0) — pure core + a working engine-glue slice.**
> Pure (`CalradiaRemembers.Logic`, +34 tests): `KeywordToken` (VERB:payload language), `ScenarioRule` (four
> fields + fire-once/weight + `Parse` factory), `GuardEvaluator` (AND-combined, injected resolver),
> `ScenarioSelector` (trigger match incl. `*` wildcard, one-shot exclusion, guard filter, weighted random
> pool — RNG injected), `ScenarioArgs` (target:delta payload parse) — incl. the Rhagaea marriage as four
> rules on one trigger. Engine glue (mod, `Scenarios/`): `ScenarioContext` (resolve hero/faction refs by
> name or id), `ScenarioGuardResolver` (IF_PLAYER_RULES / IF_HERO_IS / IF_PLAYER_INVOLVED /
> IF_PLAYER_IS_FEMALE|MALE / IF_PLAYER_AT_WAR; unknown guard → fails), `ScenarioConsequenceExecutor`
> (DECLARE_WAR / MAKE_PEACE / CHANGE_RELATION; unknown → skipped), `ScenarioRuntime` (select → execute →
> world-event message → seal one-shot), `ScenarioPackLoader` (ModuleData/scenarios/*.json via Newtonsoft),
> `ScenarioBehavior` (CampaignBehaviorBase: feeders for ON_DEATH/ON_MARRIAGE/ON_WAR_DECLARED/ON_PEACE +
> persisted fired-once set). Registered in the submodule after WorldEventStore. Console `cr.scenarios` /
> `cr.scenario_fire <VERB> [hero]`. Sample pack `ModuleData/scenarios/example.json` = the Rhagaea-death→war
> flagship (deployed to the install). **UNTESTED in game.**
>
> **COMPOSER APP BUILT 2026-06-24**: `Extension/ScenarioComposer.html` (moved from `Tools/` 11/10/2026, ships in `Docs/` from 2.7.9), a single self-contained offline HTML
> tool: pick trigger / guards / consequences from dropdowns (the vocabulary mirrors the engine in one editable
> `VOCAB` block), fill message / fire-once / weight, live JSON preview, import/export, "Load Rhagaea example".
> Exports exactly the `ModuleData/scenarios/*.json` schema the loader reads (verified by a stubbed-DOM
> round-trip: DECLARE_WAR/MAKE_PEACE/CHANGE_RELATION + guards serialize byte-correct). This is the in-game
> authoring loop: compose → download → drop into `scenarios/` → `cr.scenarios` / `cr.scenario_fire`.
>
> **Extended 2026-06-24 (415 tests green, untested in game):** added consequences `CHANGE_INFLUENCE:Ref:+N`
> (clan influence) and `INJECT_MEMORY:Ref:text` (plant a memory on an NPC so they reference it in conversation
> — the "Calradia remembers" channel), with `ScenarioArgs.TryParseTargetRest` (+3 tests) and both mirrored
> into the app `VOCAB`. Added a **reentrancy guard** (`ScenarioRuntime._isFiring`): a consequence's own
> campaign event cannot cascade back into the engine. Shipped the **Rhagaea-marriage A/B/C/D content pack**
> (`ModuleData/scenarios/rhagaea_marriage.json`, deployed): four rules on `ON_MARRIAGE` (guards
> `IF_PLAYER_INVOLVED` + `IF_HERO_IS:Rhagaea`; B adds `IF_PLAYER_RULES`), one drawn at random; each keeps her
> in character and surfaces the union's nature through her OWN memory (consort / two-crowns / regent /
> suspicious). Test: `cr.scenario_fire ON_MARRIAGE Rhagaea`.
>
> **Still to grow:** more vocabulary (`GIVE_FIEF`, `SEND_LETTER`, …) — mirror each into the app `VOCAB`; a
> richer marriage outcome than memory-only (a dedicated prompt-flavor channel / claim grant); the visual
> composer could gain hero/faction autocompletion from a game export.

This is what makes CR **world-agnostic**: a total-conversion drops in its own rules (its lords, its triggers,
its consequences) as a content pack; Calradia's hand-authored rules are just the first pack.

**Flagship content — the Rhagaea marriage** (the case that birthed the idea). Vanilla marrying a *ruler* is
cold and out-of-character: she folds into your clan and a random AI clan inherits her throne. CR replaces it
with **four rules on the `ON_MARRIAGE:Rhagaea+Player` trigger, one drawn at random and sealed**, each keeping
Rhagaea true to herself (the empress-stateswoman never abdicates *for* you). Guards scope the pool; the result
is surfaced through her *voice* (prompt context), not a popup:
- **A — Prince-consort of the South**: *you* join *her*; she keeps the throne; you become her sword and
  influence multiplier. The "become emperor" fantasy is denied — correctly.
- **B — The pact of two crowns** *(guard: the player already rules)*: an alliance of two thrones, not a
  merger; the real tension is whose heir inherits what (her daughter Ira vs your line).
- **C — The regent's gambit**: she names you war-leader and keeps the crown; you *can* reach the throne, but
  only by winning the succession (with her blessing earned) or on her death — never by her surrender.
- **D — The suspicious empress** *(a tension layer)*: the marriage is conditional on loyalty to her
  restoration; CR tracks every move, and maneuvering for the throne behind her back turns her — grudge, war,
  or she leaves you.

Because she keeps her throne in A/B/C, the vanilla "empire destabilizes" feel-bad disappears — any crown
change becomes *earned and dramatic*, never a mechanical accident. Other rulers fall back to a generic consort
rule until they get their own pack. (Replayability is the explicit goal: marrying the empress should never
play out the same twice.) A second worked example — `ON_DEATH:Rhagaea` → consequence
`DECLARE_WAR: Northern Empire, Western Empire -> Southern Empire`, message "With the empress fallen, the
eagles turn on a leaderless South" — then the sandbox takes over.

### A personal ledger of quests the PLAYER gives out *(wished 2026-07-01, BUILT 2026-07-02, untested in game)*

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

### Companion aggression & patrol errands *(wished 2026-06-23 — the last big companion piece)*

The same journey skeleton, but the companion goes to FIGHT, not just gather: **patrol a sector** (hold a
region, engage hostiles that enter), **hunt bandits** (seek and destroy bandit parties in an area), **attack**
a named lord's party / **raid** a village or caravan. The defining difference from the gather errands: these
need a real FIGHTING FORCE, so dispatch gains a troop-sourcing step — the companion either **recruits** their
own (gold + time, Leadership-gated) or **takes troops the player assigns** from the main party at send-off (a
transfer step). The OUTCOME is REAL combat, never a roll: the errand party fights through the engine; victory
→ loot/prisoners come home, defeat → the companion is taken (the existing capture-resilience path). Reuses the
journey skeleton + capture; adds a target/sector resolver and the troop transfer. Risk scales with the force
given. Pairs with companion happiness (sending people to die unhappily costs you).

### Companion war-weariness & honorable retirement *(wished 2026-06-30, Gabriel — design only, nothing built)*

**The idea:** a companion who fights at your side for years accumulates the TOLL of war, and a spent veteran
eventually wants out. This is a **SEPARATE axis from happiness** (`CompanionHappiness`) — and keeping them
separate is the whole point:

| | Happiness / contentment *(shipped)* | War-weariness *(this)* |
|---|---|---|
| Nature | mood, **reversible** (drifts back to baseline) | accumulated combat toll, **climbs** with blood spilled |
| Cause | pay, idleness, errands, treatment | verified battle events — wounds, captures |
| End state | **bitter** departure ("I quit") | **dignified** retirement ("I have done my part") |

A veteran can be **devoted AND worn out** — folding weariness into happiness would wrongly read them as
"disgruntled." So this is its own gauge, ending in an honorable exit, not a resentful one.

**Drivers — verified deeds, never words** (the mod's core discipline): the score rises on real game events —
**wounded in battle ≈ +1, taken prisoner ≈ +10** (tunable). **Valor / hardiness slows accrual** (a brave soul
wears slower; a gentle one faster). NB: a capture hits BOTH axes (happiness `Captured` −12 *and* weariness +10)
— realistic, and deliberate, not double-counting to remove.

**The fief / governor gate — the strategic heart *(Gabriel's key refinement)*:** a companion who is
**materially invested in the clan does NOT leave** — they hold a fief or **govern a settlement**
(`Hero.GovernorOf != null`, or lead a party), and draw benefit from your campaign, so they stay. Only the
**landless, roleless** companions are ever at risk. In play this asks the player to EXPAND and grant fiefs to
secure their veterans — early game they are at risk (tension), late game securing them rewards success, and
because fiefs are scarce there is always a pool at risk. This gate also **defends against a vanilla pitfall by
construction**: a landed clan member who leaves can spawn/join a rival faction — never allowing the landed to
leave sidesteps that entirely.

**Two differentiated OUTCOMES by landedness** (so weariness always lands narratively, but never destabilizes
the realm):
- **Landless + worn out** → asks to leave your service: a warm farewell, out of the clan (contrast to the
  happiness system's bitter departure). Nothing left to re-add.
- **Landed / governor + worn out** → asks to **step back from the field** and see to their fief: stays in the
  clan as a governor, off the battlefield. A tired veteran becomes a settled administrator.

**Coherence — soft enforcement, NOT a hard lock *(decided 2026-06-30)*:** the re-enrolment hole ("vanilla lets
the player just put the benched governor back in a party") is handled the CR way — the mod owns a persistent
"stepped back from war" flag on `NpcProfile`; a daily tick that finds a flagged retiree back in a party **sends
them home** ("he has seen enough war; he rides back to <town>"), and **forcing a spent veteran back repeatedly
CONVERTS weariness into genuine unhappiness** — the warm retirement curdles into a real departure and they leave
for good. You *can* drag them back (vanilla allows it), but you use them up. A hard Harmony button-block was
**considered and rejected** as invasive, patch-fragile, and un-CR: the world reasserts the fiction instead of
fighting the engine.

**Architecture (matches the pillar):** a pure policy in `CalradiaRemembers.Logic` (thresholds, trait scaling,
outcome-by-landedness, decay) — unit-tested; engine glue = campaign-event hooks for hero-wounded +
hero-prisoner-taken (player-clan only), persistence of the score + retired flag on `NpcProfile`, and a
conversation surface that reuses the existing happiness grievance/departure plumbing (a retirement request the
player answers).

**PURE CORE BUILT 2026-06-30 (Opus, +15 tests, green) — `CalradiaRemembers.Logic/WarWeariness.cs`:** the score
(0..100) with `ApplyWound` (+1) / `ApplyCapture` (+10), `Rest(step)` recovery, a Valor-scaled `RetireThreshold`
(clamped 16..72), `WantsToRetire`, a `WearinessBand` (Fresh → Seasoned → Weary → Spent) read RELATIVE to the
companion's own tolerance, `Retirement(isLanded)` → `RetirementKind.StepBackToGovern` / `LeaveService`, and a
`Describe` line for prompts/encyclopedia. **Design refinement made while building:** Valor **raises the
retirement TOLERANCE** rather than literally slowing per-event accrual (the +1 wound has no room to scale at
integer resolution) — same felt effect (a brave companion retires later), cleaner and fully testable. NEXT =
the engine glue (event hooks + `NpcProfile` persistence + the retirement conversation + the soft-enforcement
tick), still to build.

**Forks resolved 2026-06-30 (Gabriel confirmed) — the v1 build:**
- **Decay:** YES, slow recovery at real REST (`Rest`, ~2/week suggested). Not monotonic.
- **Player lever:** NO "one more campaign" persuasion in v1 (the retirement request re-presents if ignored); a
  fast-follow.
- **"Invested" gate:** governor (`Hero.GovernorOf != null`) **OR** leads a party.
- **Wear sources:** v1 = wounds (+1) + captures (+10) only; other sources (witnessing a companion die, long
  campaigns, defeats) deferred.

**AUDIENCE-AS-CONVERSATION BUILT 2026-07-01 (Opus, untested in game) — the popup now opens a real chat.** Both
retention popups (the happiness grievance AND the war-weariness retirement) became a two-road choice: **"Let us
speak of it…"** opens a real CR conversation the companion requested, and **"I've no time for this."** turns them
away with a bite (a `Mistreated` happiness hit + the existing escalation/cooldown). The shared seam is
`ChatManager.OpenForCompanionAudience(hero, profile, reason, landed)` → `ChatViewModel.ActivateCompanionAudience`
(a normal conversation, opened programmatically from a map popup, carrying the profile in `_audienceProfile`
since there is no vanilla `OneToOneConversationHero`; the companion speaks FIRST via an NPC-led opener). SDK:
`CompanionAudienceReason` (None/Grievance/Retirement) + `EncounterContext.CompanionAudience` /
`AudienceRetirementIsLanded` + `PromptBuilder.AppendCompanionAudience` — the grievance leans on the existing mood
note + `reassure_companion`; the retirement gets its own surface teaching the **`retire`** action AND the
persuasion framing (**this is where the deferred "one more campaign" lever now lives** — the player can talk a
war-weary veteran into staying, in their own words). `retire` is a chat-flow control (intercepted in the VM, not
a bridge action): it flags the grant and executes at CLOSE via `WarWearinessBehavior.ExecuteRetirement` (never
removes a hero mid-conversation) — landless leaves, landed steps back. Console `cr.audience [retire|grievance]
[name]` opens either surface directly. **This supersedes the "player lever: NO in v1" fork — the conversation
naturally IS the lever.**

**ENGINE GLUE BUILT 2026-06-30 (Opus, untested in game) — v1 complete + shippable:** `NpcProfile.WarWeariness`
(int) + `SteppedBackFromWar` (bool) persist per companion (save-safe defaults). `WarWearinessService` moves the
score to/from the profile. `WarWearinessBehavior` (registered after the happiness behavior, gated by the same
`CompanionContentment` MCM toggle): captures via `HeroPrisonerTaken` (+10), wounds via a daily `IsWounded`
not-wounded→wounded transition (+1, one per wound episode, covers auto-resolved battles), a weekly `Rest` (−2)
so heavy fighting outruns recovery while light service heals. When a companion is spent, ONE retirement inquiry
per week: **landless → warm departure** (`RemoveCompanionAction.ApplyByFire` + honorable message); **landed
(governor / party-leader) → step-back flag + message** (stays in the clan). **Soft enforcement:** a stepped-back
veteran found back in the MAIN party earns a `Mistreated` happiness hit each week and, after 3 weeks dragged
back, the retirement BREAKS (flag cleared → the contentment system may then let them leave). Decline a request →
a 14-day cooldown before it re-presents. Console: `cr.weariness [name]` (show score/band/tolerance/standing),
`cr.weary <value> [name]` (set), `cr.weary_tick` (run a weekly pass). **DEFERRED (post-v1):** physically ejecting
a stepped-back companion from the field on grant (v1 sets the flag + relies on the drag-back enforcement rather
than hero-party surgery); a farewell letter for the landless retiree (v1 departs immediately); a dedicated MCM
toggle (v1 rides `CompanionContentment`); the persuasion "one more campaign" lever; the extra wear sources.

### Custom hand-made portraits — letter window, chat window, player *(wished 2026-06-21, broadened 2026-06-24)*

Show a hand-made painted portrait (the "letter from a noble lady" look) for a hero — NPC **or the player** —
instead of the in-game 3D head render, across **CR's own prefabs**. Tiered: a custom image when one exists for
that hero, else fall back to the current `CharacterImageIdentifierVM` render — so only notable heroes need art
and everyone else still works.
- **Surfaces**: (1) the **letter window** correspondent (already scoped below), (2) the **conversation/chat
  window** portrait, (3) the **player's own** portrait. All three are CR prefabs, so no tableau-compositing
  problem.
- **Shared resolver** (one place, reused by all surfaces): lookup `Modules/CalradiaRemembers/Portraits/<heroStringId>.png`,
  with optional fallback tiers and/or a `portraits.json` map. Pure/testable: which key wins, fallback order,
  missing-file handling. The letter window already centralizes this in `LetterViewModel.ResolvePortrait(Hero)`
  — add a `UseCustomPortrait` flag + a custom-image binding, and the prefab shows either a raw image widget or
  the existing `ImageIdentifierWidget`; the chat window mirrors the same.
- **The real fork — how a PNG reaches Gauntlet** (needs a small spike, shared by all surfaces): **(A)**
  pre-baked sprites in the module atlas (clean/fast, but static — modders rebuild the atlas), or **(B)**
  runtime texture load from disk (drop-in folder, no rebuild — but needs the right engine texture-load API +
  lifetime handling). (B) preferred for moddability; verify the call first.
- **Encyclopedia is EXCLUDED**: its page composites live widgets over the 3D tableau, so a custom image there
  needs a full encyclopedia rebuild — that is the parked "generated portraits" separate-mod territory, not
  this item.

**RATIFIED 2026-07-04 (Gabriel): start with the CHAT WINDOW surface, drop-in folder, custom-if-exists-else-3D.**
Confirms the tiered fallback exactly ("thumbnail present -> show it; absent -> today's 3D render", silent on a
missing/unreadable file). Naming decision REFINES the resolver key: try `Portraits/<Hero.StringId>.png` FIRST
(unique, rename-proof), then `Portraits/<normalized-name>.png` as a friendly fallback, plus a dev console command
`cr.portrait_id` that prints the exact filename to drop for the NPC currently in conversation (StringId is opaque
otherwise). Format: square PNG, ~256x256.

**SPIKE ✅ GREEN, validated in-game 2026-07-09 (fork RESOLVED: B, runtime load).** A hand-made 445x805 PNG loaded
from disk at runtime rendered in a Gauntlet widget, and VRAM stayed FLAT over 2x300 load+release cycles (RTX 3080,
dedicated memory steady). Full proven recipe (engine texture from bytes -> SpriteCategory with sheets injected +
IsLoaded forced -> SpritePart SheetID=1 + reflected UVs -> SpriteGeneric -> `.Sprite` set by widget Id after
LoadMovie, never @-bound; `Texture.Release()` on close) lives in [[feedback-bannerlord-ui-patterns]] and the
reference implementation in `Portraits/RuntimePortraitFactory.cs` + `PortraitSpikeManager.cs`
(`cr.portrait_spike` / `cr.portrait_spike_stress`, shipped inert in v1.29.1). **The feature (chat-window surface
first, per the ratified order) is now UNBLOCKED and ready to build**: shared resolver + LetterViewModel/chat
wiring, reusing the spike factory.

### Player action narration — interpret the player's `*…*` and first-person deeds *(wished 2026-06-28, player feedback)*

A recurring roleplay frustration: a player types a physical action — `*I take her by the wrist*`, or first-person
"I grab her arm" — and the NPC, never told to read it as narration, treats it as nonsense and pushes back ("what
are you talking about, you crazy fool"). CR teaches the NPC to use `*asterisks*` for ITS OWN actions but never
teaches the model to INTERPRET the *player's* narrated actions. Add a PromptBuilder block defining the player's
`*…*` (and plain first-person physical statements) as in-world action the NPC reacts to in character — distinct
from speech. Pure-ish prompt work, **high value / low effort**. Caveat: a free NPC may still legitimately DEFY
coercion (that is correct behaviour); the bug is the model not understanding the input at all. Pairs with the
player-as-captor scene below for the captivity use-case. (Model choice still dominates for adult refusals.)

**BUILT 2026-06-28 (untested in game).** `PromptBuilder.AppendPlayerActionNarration` (SDK) injected near the end
(high recency, beside the language mirror) of BOTH the main NPC and commoner prompts: defines the player's
`*…*` and plain first-person physical statements as real in-scene actions to ENGAGE with (never gibberish, never
"what are you talking about"), with explicit examples, while keeping the NPC free to welcome/resist/refuse. Test:
chat any NPC and type `*I take you by the wrist*` or "I put my hand on your shoulder".

### Player feedback — time-gap chatter, group-scene recall, romance override, post-history hook *(2026-06-29)*

A player on GLM 5.1 / NanoGPT raised three things (and asked about a donation channel):

- **NPCs harp on the time since the last conversation** ("Four days since we last spoke...") — map travel eats
  days fast, so a short gap should not be remarked. **BUILT 2026-06-29 (untested in game).** Pure tested
  `MeetingGapDescriber` (CalradiaRemembers.Logic) gates it: nothing at or under ~1 week (`RemarkThresholdDays = 7`),
  and a longer gap is phrased on the Calradian calendar ("a season" / "a year") as plain awareness. SDK
  `EncounterContext.MeetingGapNote` replaces the old unconditional "You last saw the player N days ago." line,
  rendered by `PromptBuilder.AppendEncounterContext` with explicit "do not open with it / do not dwell" guidance.
  (+6 tests.) Test: talk to a companion a few days after the last chat — they no longer lead with the count; a
  long absence may be acknowledged once, in passing.

- **A companion does not recognise they took part in a group conversation.** Real gap, not the LLM: at close,
  only the MAIN NPC's profile was written. **BUILT 2026-06-29 (untested in game, first slice).** New
  `ChatViewModel.FinalizeWitnessMemories` (called from `ChatManager.Close`) records a light "Was present while
  {player} spoke with {mainNPC}..." event into each HERO witness's own profile, so a later one-to-one reflects
  the shared scene. First slice records only witnesses the player has already met (existing profile);
  synthetic/bandit witnesses and the main NPC are skipped. OPEN: get-or-create a profile for a stranger witness;
  optionally a richer per-witness summary (deferred — would cost N extra LLM calls per scene).

- **Romance / courtship override per character, independent of traits and culture.** TO DESIGN. Today the
  romantic profile is DERIVED from the five traits (`RomanticProfileBuilder`), so making a character
  "casual/open" forces a low Honor value. Proposed: an optional `romantic` block in `character_overrides.json`
  (orientation, monogamy/openness, devoted/casual, optional kinks) that OVERRIDES the derived `RomanticProfile`
  when present — slots into the existing player-authored-characters system (`Use custom traits`). Needs design:
  which fields are overridable, how it interacts with re-derivation for already-met NPCs, and the JSON shape.

- **A SillyTavern-style post-history / jailbreak hook.** A player asked where to put RP / censorship-bypass
  framing "pre or post history" (SillyTavern terms). CR sent everything in the system prompt (all pre-history),
  with no modder-editable last-word slot. **BUILT 2026-06-29.** New editable `post_history_instructions.txt`
  (ModuleData, ships empty) injected verbatim at the VERY END of the system prompt (after the language mirror,
  highest recency) for BOTH the NPC and commoner prompts — the analog of SillyTavern's post-history field.
  SDK `PromptBuilder.PostHistoryInstructions` + `AppendPostHistoryInstructions`; mod `NarrativeLoader` loads the
  file; `LlmServices` wires it; loads on campaign start / `cr.reload_lore`. +3 tests (presence + ends-with proof,
  NPC and commoner). Documented in the Nexus "Make it your own" section (positioned next to behavior_guidelines).

- **Donations / feedback channel (Ko-fi):** DONE 2026-06-29 — Ko-fi page (ko-fi.com/lograam) created by player
  request; a centered "Support the work" section added at the top of the Nexus description.

### Player-requested — wardrobe by talking, image generation, Hotscenes compat *(wished 2026-06-29, player feedback)*

A player asked whether three things are realistic to even want. Feasibility read (most realistic first):

- **Wardrobe by conversation (undress / "leave them in their shift") — MOST realistic; do this one.** Bannerlord's
  Equipment API changes a hero's CIVILIAN outfit at runtime (clear/replace the Body/Cape/Head/Gloves/Leg slots). A
  new adult-gated action verb (e.g. `change_outfit` / `undress`) emitted by the LLM and executed by the bridge
  ("the bridge is law": re-validate, adult-gate, apply via `Equipment`) lets the player ask an NPC to strip or
  dress down, and it actually shows on the 3D model in conversation and in captive scenes. Fits CR's action model
  cleanly. Open questions: persistence (revert to the prior outfit after the scene?) and how far to undress
  (Bannerlord renders a default underwear body when the slots are cleared).

- **Image generation — feasible backend, the DISPLAY is the blocker.** Sending a scene/portrait prompt to an image
  endpoint (the player's own SD, or an API) is easy HTTP. Showing a runtime-generated image in the Gauntlet
  chat/scene window is the hard part — the same texture-load-from-disk spike already noted under "Custom hand-made
  portraits" and the parked "generated portraits" idea. Slow and costly per image, so strictly opt-in. Sequence it
  AFTER the portrait PNG→Gauntlet spike lands (it unlocks both). An optional add-on, not core.

- **Hotscenes compat — possible but the hardest; research-gated.** Hotscenes plays animated adult scenes between
  agents inside a MISSION; CR scenes are a chat overlay on the map, not a mission. A real bridge means opening a
  scene with the two heroes and triggering the right Hotscenes animation at the right beat — heavy, and entirely
  dependent on what hooks/API Hotscenes exposes (unknown; may be closed). Most honestly delivered as a SEPARATE
  optional bridge mod once Hotscenes' surface is understood. Do not promise it; research first.

### Captive-scene content
- **Scenario bank**: a bank of authored scene setups, one-use-per-campaign via SyncData, eligibility by
  kinks/gender, prompt injection. (Originally imagined as Twine→JSON; Twine is dropped — see the scripted
  scenarios pillar — in favour of plain keyword/JSON data, reusing the same composer/authoring approach.)
  (When adding persisted campaign state, REMOVE `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load)
  fires before OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).
- **Player-as-captor ("tyrant") scene** *(wished 2026-06-28, player feedback)* — today the explicit captive
  scenes frame the player as the PRISONER (`PlayerStatus == Captive` + Hardcore). Players using captivity-event
  mods hold NPCs prisoner and want the inverse: an explicit scene where the PLAYER is the captor of an NPC they
  have taken. The captor direction was noted "deferred — no aggressor scene exists yet" (Sprint 17). Reuse the
  scene director + intents (Interrogation/Domination/…) with the roles swapped; standing damage lands on the NPC,
  not the player. Now requested by more than one captivity-mod player.
  **BUILT 2026-06-28 (untested in game):** a player-LED scene (NOT the NPC-driven director) — `EncounterContext.IsCaptorScene`
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

### Conception from any vaginal act: BUILT, in the live pregnancy system (consensual scenes + female NPCs). Full design in CHANGELOG + git.

### Bastards, a hidden birth as a consequence layer: SHIPPED v1.15.0. Full design in CHANGELOG + git.

### Lean prompt for small / local models: SHIPPED v1.20.0 (Lean v2, about 1490 tokens). Full design in CHANGELOG + git.

### Stance — mechanical behaviour-gating *(all 4 axes 🔨 BUILT 2026-07-04→05, untested in game)*

Stance (Trust/Respect/Fear, plus Affection mirrored from relation) is fully wired into **perception**: deeds
move it, words nudge it (capped + cooled), fear decays, and `StanceDescriptor` feeds the prompt so the LLM
**role-plays** the posture. What's missing is mechanical **teeth** — stance thresholds changing **verified
outcomes**, not just tone:
- a **fearful** captive lord more likely to accept a freedom bargain / yield softer ransom terms;
- a high-**Respect** lord more open to `join_clan` or a war-council nudge;
- a low-**Trust** NPC refusing a deed that a relation score alone would have allowed.
Build on the existing deed bridges (prisoner bargain, `join_clan`, barter); the gate is a pure threshold policy
in `CalradiaRemembers.Logic` (testable), consulted by the bridge before it commits. "Words nudge, deeds prove"
— and now the posture **bites**.

**RATIFIED 2026-07-04 (Gabriel: "beaucoup de valeur, dans la philosophie de CR"), two structural rulings:**
1. **Gate on the PROVEN level, not on flattery.** "Deeds prove" is a hard rule at the gate. NOTE (verified in
   code 2026-07-04): the ANTI-FLATTERY GUARANTEE ALREADY EXISTS in `StanceGate` — a spoken round caps at
   `WordCap` = ±2 per 8h, a proven deed at `DeedCap` = ±30. Reaching a gate at 40 by words alone would take
   ~20 accepted rounds over ~160 in-game hours (and Fear decays underneath). So the gate reads `stance.Fear`
   DIRECTLY — no separate "proven floor" store, no new SyncData key. The earlier A/B storage question is moot.
   DECAY CAVEAT: unlike Trust/Respect/Affection (ETERNAL, no decay — forgetting reads as a bug), **Fear is the
   one stance axis that fades** (`FearDecayPerDay` = 2, toward 0). So Fear-based coercion leverage is
   inherently TEMPORARY by design: a lord cowed today yields softer terms, but ~20 days of calm erase the scare
   and the leverage expires. This is a feature (terror recedes), not a bug — the fiction should own it.
2. **One axis at a time, FEAR first.** Additive per the save-migration hard rule; Fear already has its
   receptacle (the graduated FEAR stance register + hidden Grudges as the resentment sink). Then Trust,
   Respect, Affection as later increments.
Axis→domain grid (each axis owns one family of consequences, kept disjoint for lisibility + pure-policy testing):
- **Fear → coercion**: high → accepts tribute / yields a prisoner / backs down, BUT plants a HIDDEN grudge
  instead of regard (feeds the existing stance-assassin / hidden-hatred pattern); low + low Respect → refuses
  flat, may escalate.
- **Trust → delegated risk**: gates verbs where the NPC EXPOSES themselves (companion mission, lending troops
  without surety, sensitive info, accepting marriage). Low Trust → "prove it first."
- **Respect → political weight**: modifier on influence/negotiation (threat credibility, negotiating as an
  equal vs. snubbing, weight in a scheme).
- **Affection → personal loyalty**: gate for the romantic AND for staying loyal despite a poor CLAN standing,
  distinct from clan regard.
Increment plan: **Fear-1** = wire the coercion gate into the freedom-bargain / ransom bridge (fearful captive
yields softer terms + hidden-grudge tail), pure `StanceGatePolicy` in `.Logic`, tested first; thresholds TO
RATIFY before any code.

**Fear-1 🔨 BUILT 2026-07-04 (untested in game), 1141 mod tests.** `CalradiaRemembers.Logic/StanceGatePolicy.cs`
(`ResolveCoercion(fear, respect, honor, valor, calculating)`): `YieldFearFloor=40` (raised by positive
Honor/Valor at `+5`/level each, lowered by positive Calculating at `-5`/level, floor clamped ≥
`RefuseFearCeiling`), `RefuseFearCeiling=15` + `RefuseRespectCeiling=10` (both must hold to refuse flat),
`RansomSoftenMultiplier=0.75`, `CoercedGrudgeMagnitude=30` (+`5`/level of positive Calculating). Wired into
`BannerlordGameStateBridge.ExecuteFreePrisoner`: reads the captive's live Fear/Respect (`StanceService.ToStance`)
+ Honor/Valor/Calculating off the live Hero; `RefusesFlat` blocks the release outright (bargain declined, captive
stays put); `YieldsSofterTerms` proceeds with the release and additionally plants a HIDDEN `GrudgeSource.CoercedRelease`
(new value, appended — ordinal 13) via `GrudgeBehavior.TryPlant`, on top of (never instead of) the existing
deed-gesture/appreciation/chronicle channels. A profile-less captive (no stance data — the `cr.test free_prisoner`
self-test path) skips the gate entirely (`Neutral`) rather than defaulting Fear/Respect to 0/0, which would
otherwise satisfy the refuse-flat thresholds by construction and block every ordinary release. **SOFTER TERMS
delivered PROMPT-side (2026-07-04):** since `free_prisoner` carries no numeric ransom (the LLM emits its own
freeform `give_gold` in the same response), the 0.75 multiplier is NOT clamped in the bridge; instead a new
additive `EncounterContext.CaptiveFearCoerced` (computed in `EncounterContextBuilder.ResolveCaptiveFearCoerced`
via the SAME `StanceGatePolicy.ResolveCoercion`, so prompt-advice and bridge-law never disagree) makes
`PromptBuilder.AppendPrisonerFreedomBargain` instruct the frightened captive to offer a LARGER sum / harsher
terms. Prompt is advice, bridge is law (the hidden grudge), both consult the identical policy. Console:
`cr.stance_set <trust> <respect> <fear> [name]` (already existed) seeds the axes to exercise yield/refuse in
game. Next (DONE, see Respect-1 below): Respect (political weight), then Affection (personal loyalty), per the
axis→domain grid above.

**Trust-1 🔨 BUILT 2026-07-04 (untested in game), 1148 mod tests.** Trust → DELEGATED RISK: gates the verbs where
the NPC exposes THEIR OWN resources to the player. `StanceGatePolicy.WillDelegateRisk(trust, relation)` =
`trust >= DelegatedRiskTrustFloor (20) || relation >= DelegatedRiskRelationBypass (30)`. Wired into
`GiveInfluenceVerb` + `LendTroopsVerb` (the NPC lending their OWN influence/troops to the player; the reciprocal
`SpendInfluenceVerb`/`GiveTroopsVerb` are untouched), at BOTH surfaces: `IsEligible` (advice — not taught below
the gate, so the LLM never promises it) and `Execute` (law — re-reads live Trust/relation, refuses in character
"Prove yourself first"), reading Trust via `StanceService.ToStance` and relation via `profile.ClanRelationWithPlayer`.
**OR-relation soupape (Gabriel ratified 2026-07-04):** an established ally (vanilla relation ≥ 30) still lends
even with 0 CR-tracked Trust, so the gate never silently narrows a shipped feature; an unproven stranger must
still earn Trust (kept quest +15, gift +4, capped word nudges) to cross the floor. Profile-less NPCs were already
excluded from these verbs (`Profile` is a hard prerequisite), so no new blocking for untracked heroes. Trust-only
for now (no trait modulation yet). Console: `cr.stance_set 25 0 0 <name>` seeds Trust to test. **OPEN SEAM:** the
companion risky-errand gate (Spy/Steal) is left commented-out in `CompanionMissionBehavior.Dispatch` — freshly
recruited companions sit at Trust 0 AND relation ~0, so gating would lock an already-shipped feature; needs its
own design (gate only the riskiest missions, or seed companion Trust on recruit) before wiring.

**Respect-1 🔨 BUILT 2026-07-05 (untested in game), 1159 mod tests.** Respect → POLITICAL WEIGHT: gates
`join_clan` (recruiting a lord into the player's clan). A lord who holds the player in contempt will not throw in
their lot, however cordial the relation. Wired at BOTH surfaces: `EncounterContextBuilder.ResolveLordRecruitEligible`
(advice — now takes `profile`, adds the Respect check on top of the existing `LordRecruitment` gate, so a
contemptuous lord is never taught the action) and `BannerlordGameStateBridge.ExecuteJoinClan` (law — now takes
`profile` off `ActionContext`, re-reads live Respect/relation, refuses in character "does not respect you enough to
throw in their lot with you. Earn their esteem first."). Profile-less lords skip the Respect check entirely
(`join_clan` never depended on a stance profile before this increment, so an untracked lord is not newly narrowed).
**Site-specific HIGH soupape (Gabriel ratified 2026-07-05):** the naive shared soupape was DEAD here —
`LordRecruitment.MinRelation` (60) already exceeds the general `RespectRelationBypass` (30), so it always fired and
the Respect floor never bound. Fixed with a dedicated `StanceGatePolicy.CommandsRespectToJoinClan(respect, relation)`
= `respect >= RespectFloor (20) || relation >= RecruitRespectRelationBypass (80)`: an adored lord (relation 80+)
still joins on relation alone, but a merely-eligible lord (60..79) must ALSO respect the player (Respect ≥ 20), so
the floor genuinely binds in that band. The general `CommandsRespect` (bypass 30) stays reserved for future
lower-gated Respect sites (sway_opinion weight, threat credibility). Side-effect fix while wiring this:
`GiveInfluenceVerb`/`LendTroopsVerb` (Trust-1) would not actually compile — `profile.ClanRelationWithPlayer` is
`int?`, passed where `WillDelegateRisk` expects `int` — fixed with `?? 0`. Console: `cr.stance_set 0 25 0 <name>`
seeds Respect to test (Respect is the 2nd arg); use relation 60-79 to see the floor bind.

**Affection-1 🔨 BUILT 2026-07-05 (untested in game), 1161 mod tests.** Affection → PERSONAL LOYALTY despite a
cold clan standing (Gabriel ratified 2026-07-05: the "activator", not a floor). Affection is not a deed-earned
axis; it mirrors the NPC's personal regard (`ReputationWithPlayer`), which already gates romance/marriage, so
its novel mechanical role is a THIRD door onto the delegated-risk gate: a lord who personally adores the player
lends their own troops/influence out of personal loyalty even without proven Trust or a warm clan relation.
`WillDelegateRisk(trust, relation, affection)` now = `trust >= 20 || relation >= 30 || affection >= AffectionLoyaltyFloor (40)`,
reading affection from `profile.ReputationWithPlayer` at all four `GiveInfluenceVerb`/`LendTroopsVerb` sites.
NOT applied to `join_clan` (its upstream `LordRecruitment.MinRelation` = 60 already gates relation, so an
Affection override there would be moot). Console: `cr.rep_set <n> [name]` (personal regard) + `cr.stance_set 0 0 0 <name>`
(zero the earned axes) + a cold relation, then a lend request should still succeed by personal loyalty.

**✅ STANCE MECHANICAL LAYER COMPLETE (4/4 axes, 2026-07-04→05, all untested in game):** Fear → coercion
(freedom-bargain), Trust → delegated risk (lend troops/influence), Respect → political weight (join_clan),
Affection → personal loyalty (delegated-risk third door). All pure in `StanceGatePolicy` (.Logic, fully tested),
consulted at both the advice (prompt/eligibility) and law (execution) surfaces, with `cr.stance_set`/`cr.rep_set`
seeding for in-game tests. OPEN follow-ups noted in place: companion risky-errand Trust gate (seam), and later
Respect sites (sway_opinion weight / threat credibility) that the general `CommandsRespect` bypass already awaits.

### Player feedback round — SuperGraham339 (2026-07-05), triage + 2 new ratified pillars

A rich living-feedback dump. UNIFYING THEME under ~half the points: the LLM lacks ground-truth game facts and
hallucinates (army size, vanilla quests it issued, gang-leader rivalries, player backstory/appearance, "Christ").
So half the list is ONE class of fix: feed the missing fact into context + hook unhooked entry points.

**Quick wins 🔨 BUILT 2026-07-05 (untested in game), 1167 mod tests:**
- **Diacritic folding in name matching** — pure `CalradiaRemembers.Logic\NameNormalization.Fold` (lowercase +
  NFD decompose, drop NonSpacingMark, recompose), 6 tests. Applied in `ContextualNameResolver` (both message
  tokens and hero name parts) and `ModCheats.ResolveHero` (rewritten to fold+tokenize: every folded query token
  must substring some folded name part, so "Heloise the Finger" also disambiguates shared epithets). "Héloïse"
  now matches a typed "Heloise".
- **Player party troop count in context** — additive `EncounterContext.PlayerPartyTroopCount` (default 0), set in
  `EncounterContextBuilder.ResolvePlayerPartyTroopCount` ONLY when `hero.PartyBelongedTo == MobileParty.MainParty`
  (a present party member/companion; non-party NPCs get 0 = no metagame count). Rendered when > 0: "The player
  currently leads N soldiers in their party." (SEAM flagged: it is conversation-stable, could later hoist into the
  cacheable prompt prefix.)
- **Anachronism guardrail** — a light bullet in both full and lean behaviour guidelines: Calradia has its own
  faiths/gods/history, never reference Earth's religions/deities/saints/historical figures (no Christ/Rome/
  Napoleon). Does not curb RP agency. (SEAM: skipped when a station/modder `BehaviorGuidelinesOverride` is active,
  same as every built-in bullet.)

**Bugs — triaged 2026-07-05:**
- **No Encyclopedia button in captive dialogue — RESOLVED BY DIAGNOSIS, no separate fix.** The button
  (`CalradiaRemembersChat.xml` `EncyclopediaButton`) is gated only by `IsEncyclopediaVisible => _npcHero != null`,
  with NO extra captive-mode masking. A real-Hero captor (a lord) already shows it in the captive scene; a
  SYNTHESIZED bandit captor (no Hero, like Ranulf) correctly hides it because there IS no encyclopedia page for a
  non-Hero. So this is the SAME root cause as the named-bandit-memory bug and is fixed automatically by the Nemesis
  pillar (Hero captor -> page exists -> button reappears). Building a separate UI fix would be wrong.
- **Chat-granted player freedom is not made real in vanilla (Gabriel clarified 2026-07-05).** The case is: the
  player NEGOTIATES their release OR ESCAPES via chat, the LLM plays it narratively, but the vanilla captivity never
  ends, so captivity resumes. "The prompt is advice, the bridge is law": a bridge action must apply
  `EndCaptivityAction`. State of play: (a) ESCAPE already has a real mechanic (`escape_attempt` ->
  `ResolvePlayerEscapeAttempt` -> `_pendingEscape` -> `FinalizePendingEscape` -> `EndCaptivityAction.ApplyByEscape`,
  deferred to scene close). If a chat escape failed to take, the likely cause is a narration/roll DESYNC (the LLM
  narrated success while the ~10% roll failed) or `escape_attempt` never emitted; verify at build. (b) NEGOTIATED
  RELEASE has NO action at all: `free_prisoner` is the inverse (the PLAYER frees THEIR captive via
  `EndCaptivityAction.ApplyByReleasedByChoice(npcHero, MainHero)`). FIX = add the mirror: a captor-side
  "release the player" action that, when the captor is genuinely persuaded (gated, not trivial), applies
  `EndCaptivityAction.ApplyByReleasedByChoice(captor, MainHero)`, deferred to scene close like the escape, taught in
  the captive-scene / lord-audience prompt. Build-ready design; needs SDK teaching + bridge execute + deferred
  finalize + persuasion gate. Pure gate BUILT+tested: `CaptorReleasePolicy.Resolve(mercy, grudge, isNemesisVendetta)`
  -> `{AllowsPersuasion, AllowsRansom}` (vendetta grudge >= 40 or nemesis refuses both; else ransom always, persuasion
  if Mercy >= 1 OR light grudge < 15). **🔨 ENGINE WIRING BUILT 2026-07-05 (untested in game, 1241 tests, mod 0 err),
  persuasion path:** SDK `EncounterContext.CaptorAllowsPersuasionRelease/RansomRelease` (additive) +
  `PromptBuilder.AppendCaptorReleaseRule` teaches `release_player` to the captor (shared across bandit-menace /
  lord-reckoning / CNC captive scenes, only when the gate allows); `EncounterContextBuilder.ResolveCaptorReleaseVerdict`
  computes the flags via `CaptorReleasePolicy` when `PlayerStatus == Captive` (Mercy off the captor Hero, heaviest
  live grudge vs player off `GrudgeBehavior.GrudgesFor`); bridge `CommitCaptorRelease(captor)` re-validates the gate
  and sets a deferred `_pendingRelease`, `FinalizePendingRelease` applies `EndCaptivityAction.ApplyByReleasedByChoice(MainHero, captor)`
  at scene close (mirrors `_pendingEscape`); `ChatViewModel` detects `release_player` -> `ResolveCaptorRelease`
  (renders the parting line, commits, concludes the scene) and calls `FinalizePendingRelease` in `FinalizeCaptiveSceneMemory`.
  **OPEN follow-up: the RANSOM-in-gold path** (the `ransom: N` amount + gold deduction) is deferred; the flag is
  computed but this increment teaches/handles PERSUASION release only. Needs in-game validation.

**CAPTIVITY ESCAPE BALANCE (analysed + ratified 2026-07-05).** Full escape surface in Hardcore: (a) scene
`escape_attempt` = flat 10% per attempt (`EscapeChance = 0.10`), accumulating over scenes (1-0.9^N: ~27% by 3
scenes, ~65% by 10); (b) vanilla spontaneous escape = ~30% of native rolls survive (`VanillaCaptivityPatch`
suppresses 70% in Hardcore), a recurring drip that tends to near-certain over a long captivity ("time frees you");
(c) vanilla ransom = fully OFF in Hardcore; (d) negotiated release (above) = near-at-will for a disposed captor, 0
for a vendetta/nemesis; (e) rescue/battle = situational. NET: ordinary captivity is easily escapable (fine); a
NEMESIS could still be waited out via the vanilla drip. GABRIEL'S RULING: a nemesis keeps SOME escape chance (never
a perfect cage), but it DECAYS with each recapture as he grows more careful, and he says so in-fiction ("you slipped
me once, not again"). Pure `NemesisEscapeChancePolicy.EscapeChance(recaptureCount)` BUILT+tested (base 10%,
-2.5%/recapture, floor 2%, never 0). Engine wiring (nemesis increment): fully suppress the vanilla drip for a
nemesis captor (so the decaying scene-escape is the only luck exit), source `recaptureCount` from a new
`NemesisIdentity` field, feed it to the scene-escape roll, and voice a "not this time" beat when count > 0.

- **NPCs unaware of VANILLA quests they issued — 🔨 FIXED 2026-07-05 (untested in game, mod 0 err).** New additive
  `EncounterContext.NativeQuestNote`, built in `EncounterContextBuilder.ResolveNativeQuestNote(hero)`: scans
  `Campaign.Current.QuestManager.Quests` for ONGOING quests whose `QuestGiver == this NPC`, EXCLUDING CR's own
  mirrors (`CalradiaRemembersQuest`, `CommitmentQuest`) so nothing double-surfaces, and formats a note ("TASKS FROM
  THE REALM'S AFFAIRS YOU SET THIS PLAYER ... speak as the one who set it, never baffled"). `PromptBuilder.AppendNativeQuestNote`
  renders it, gated non-Captive alongside `AppendActiveQuests` (same guard that stops a bandit captor mistaking a
  quest for one they gave). The giver now acknowledges their own native quest.
- **Rival gang-leaders framed as friends-by-culture — 🔨 FIXED 2026-07-05 (untested, 1244 tests, mod 0 err).** Pure
  `ThirdPartyDescriptor.Categorize` gained an `areRivalGangLeaders` param (defaulted, so no caller churn): after
  kinship, two same-turf gang leaders are labelled "a rival gang leader (a competitor for the same turf, not a
  friend)", overriding the friendly/neutral tiers (a genuine ENEMY relation is stronger and kept). +3 tests.
  `ContextualNameResolver.AreRivalGangLeaders` computes it (both `Occupation.GangLeader`, same `CurrentSettlement`,
  different heroes). The NPC is now told the rivalry instead of hallucinating a tribal friendship; the wrong −8
  `change_relation` for offering to move against a rival should self-correct (hurting a named RIVAL reads as a
  favour). If the −8 persists in game, the `change_relation` vocabulary is the follow-up lever.
- **Alley-placed companion gives generic vanilla replies (#5) — ALREADY HOOKED, likely already fixed.** `LlmDialogFlow.EntryHubTokens`
  already includes `alley_player_owned_start` alongside `hero_main_options`, added (with a comment citing a prior
  player report) precisely because the alley-manager conversation branches from "start" and never passes through
  `hero_main_options`, so CR's "A word with you." was missing there. So SuperGraham's report most likely predates
  this fix; needs in-game RE-VALIDATION on the current build. The "maybe more broadly impacting" residual = if any
  OTHER conversation context surfaces unhooked, add its hub token to `EntryHubTokens` (the clean extension point).

**PILLAR: Nemesis — the persistent named bandit boss (ratified 2026-07-05).** Root cause of "Ranulf the Faceless
Fox keeps his NAME but has no memory (`cr.memory`/`cr.info` empty)": the bandit captor is a transient troop, not a
Hero, so no `NpcProfile` attaches. FIX = whenever ANY bandits take the player
prisoner (NOT only the town-ambush arc — every bandit capture), the chief is a PERSISTENT MINOR-LORD Hero instead
of a jetable troop — a Hero has a profile, so memory/grudges persist for free and he becomes a foe you can hunt,
owe, and kill. The boss belongs to the CAPTURING bandit faction (Looters / Forest Bandits / Mountain Bandits /
Sea Raiders / Desert Bandits / Steppe Bandits, per the vanilla bandit clans), so his culture/flavour matches who
took you. Shape RATIFIED = a REGIONAL RECURRING ROSTER keyed by faction+region: on capture, if a living nemesis
of that band exists in the region he IS the captor (you recross Ranulf); else mint a new one; killing him fires a
world-event and he does not return. Increment 1 = just the persistent captor Hero + memory (fixes the bug, unlocks
the rest), fired from the general bandit-capture hook; the town-ambush arc's existing "elite chief of the
paymaster's culture" becomes one caller of the same machinery, not the only trigger. Later
increments ABSORB the player's own wishlist: owe-money-to-the-boss -> collector party comes (courier-like), be
delinquent -> his men hunt you, kill him -> head on a pike + world-event on "the fate of those who cross me";
also folds in rival lords sending hit teams / notables sending thugs. Ties to captivity arc, stance-with-teeth
(town ambush / Intercept / Assassinate), world-events, and interop with the "named bandits" mod (Nexus 10376) the
player cited. See [[project-ambush-captivity]], [[project-stance-system]].

**MAP-PRESENCE MECHANIC, ACTIVE HUNT: 🔨 BUILT 2026-07-08 (untested in game).** The materialization is live:
`NemesisBehavior` brackets a player battle (`MapEventStarted`/`MapEventEnded`); when the player engages a bandit
band of a living, latent, non-turned nemesis's culture near his region anchor (`FindHuntableNemesisNear`, culture
match plus `NemesisMaterializePolicy.WithinRegion` proximity, or culture-only when his region is not a real
settlement), the ratified PITY-TIMER `NemesisRevealPolicy.ShouldReveal(EncountersSinceReveal, roll)` (15% rising
to a certainty after 6 qualifying misses) decides; on a reveal `TryMaterializeInto` mint-or-reuses his Hero and
`AddHeroToPartyAction`s him INTO that band with a screen notice ("your hunt has found him"), a miss just builds
pity credit. He fights and can be killed (existing `NemesisResolutionBehavior.OnHeroKilled` closes the arc) or
captured (existing prisoner dialog). On MapEventEnded, if he survived un-captured he is `DetachToLatent`'d
(removed from the band's roster) so he keeps NO standing party. **No nemesis exists until a real CAPTURE mints one
(`OnHeroPrisonerTaken`), so merely defeating bandits never spawns a hunt** (Gabriel's point A, already satisfied).
**RESPAWN COOLDOWN (Gabriel 2026-07-08): killing a nemesis rolls `RespawnBlockedUntilDay` = death + 1..3 Calradian
years; `NemesisRosterPolicy.IsCultureRespawnBlocked` then blocks a fresh mint of that CULTURE (`FindOrMint` returns
null, the same bandits take the player as a faceless captor until the cooldown passes).** `nemesis_hunt`
self-test; unit tests for `WithinRegion`, `IsCultureRespawnBlocked`, and the existing `NemesisRevealPolicy`. STILL
OPEN (later): the rumour/world-event "he still raids near X" nudge layer between encounters; region-breadth tuning.
Original ratified design below.

**MAP-PRESENCE MECHANIC — RATIFIED 2026-07-05 (Gabriel), resolves the persistence-vs-pursuit tension.** THE
TENSION: for it to be an adventure the boss must survive the vanilla world's constant bandit-clearing (a random
lord must not erase your nemesis before you can hunt him), yet the player wants to PHYSICALLY pursue and kill him.
A standing map party can't do both (AI-destructible vs chaseable). RESOLUTION (Gabriel's, cleaner than a
dormant/summon or protected-party approach): **the nemesis has NO persistent party on the map at all — he lives in
RUMOURS and world-events (a latent identity: Hero + faction-band + capture-region affinity).** He MATERIALIZES
only at an encounter: when the player engages a bandit party OF HIS BAND'S FACTION, IN THE REGION where that
player was captured, there is a RANDOM CHANCE that party turns out to be led by the nemesis. So the hunt is
EMERGENT and diegetic: the player goes bandit-clearing in his territory and, after some empty bands, eventually
runs into him leading one. Consequences: no standing party ⇒ nothing for the AI to attrit ⇒ persistence is free
and hack-free; his death is PLAYER-GATED by construction (he only ever exists in a fight the player chose to
start); killing/capturing him at that encounter resolves the arc (world-event, head on a pike, or the player
becomes HIS captor). Design knobs to lock at build time: (a) the attach-chance and its PACING (rising / pity-timer
so he is guaranteed eventually, per "intercepte plusieurs bandes sans le trouver, mais à un moment donné il tombe
sur lui"); (b) region breadth (exact capture region vs a wider area) and whether player-initiated only or also
when bandits engage the player; (c) the TECHNICAL SPIKE — attaching the persistent nemesis Hero as the LEADER of
an ordinary vanilla bandit party at encounter time so he really fights, can be captured, and can be killed. The
rumour/world-event layer keeps him present between encounters ("the Faceless Fox still raids near X, and still
means to collect what you owe") and can nudge the player toward his region; the debt/collector and "his men hunt
you" wishes ride on top as world-events, no standing party required.

**BOTH ENCOUNTER DIRECTIONS ARE CHAT-FIRST + SYMMETRIC (Gabriel 2026-07-05):** the nemesis identity attaches to a
qualifying (faction + capture-region) vanilla bandit party in EITHER outcome, and both open in CR chat, not a raw
battle. (1) WIN/INTERCEPT: when the player engages such a party and the roll reveals the nemesis, the encounter
OPENS WITH A CONVERSATION with him (reuses the map-conversation entry), confront him, settle the debt, taunt,
before it breaks to a fight / capture / kill (and if the player LOSES that fight, he naturally takes the player,
looping to direction 2). (2) LOSE/CAPTURED: if a matching-faction band in the capture region defeats and captures
the player, the nemesis IS the captor and the captive scene runs with HIM, and because he is a persistent Hero
with a profile he REMEMBERS the player, the debt, and the prior scene (exactly the "Ranulf keeps his name but not
his memory" bug fixed by construction). The capture direction needs NO bespoke scene code (Gabriel 2026-07-05):
it is the EXISTING captivity event verbatim, and the captor's history with the player injects ITSELF through the
standard prompt-history path (`AppendHistory` over the captor's `NotableEvents`) purely because the captor is now
the persistent nemesis Hero rather than a nameless troop. The ONLY requirement is the attach mechanic making him
the captor Hero; the memory, the debt recall, the "what we did to each other last time" all come for free. One
machinery (attach the persistent nemesis Hero as the vanilla band's leader), two chat-first faces: hunter and
hunted swap places across encounters.

**ENGINE-FEASIBILITY SPIKE (read-only, 2026-07-05): VERDICT = BUILDABLE-WITH-CAVEATS.** The load-bearing mechanic
is ALREADY SHIPPING CODE: `AmbushCaptivityBehavior.SpawnCaptors` (`Stance\AmbushCaptivityBehavior.cs:683-739`)
already mints a real persistent bandit-faction Hero (`HeroCreator.CreateSpecialHero` from a bandit template into
`Clan.BanditFactions` "looters"), makes him the party `LeaderHero` (`LordPartyComponent.CreateLordParty`), fills
the band with vanilla looter troops, and round-trips his StringId across save/load via `SyncData` (`_chiefId`).
Today it deliberately KILLS the chief in `ClearConvoy` (`:234-237`, "so we don't leak a hero into the world").
THE NEMESIS IS LITERALLY THAT CODE MINUS THE KILL, PLUS IDENTITY REUSE. Seam map:
- **Captor-side chat-first = FREE.** `ChatManager.OpenForCaptiveEncounter(captorHero, profile,…)` (`Chat\ChatManager.cs:141-184`)
  already opens the captive scene with a real Hero speaking first (called from `AmbushCaptivityBehavior.TryOpenLordAudience`);
  `CaptiveEncounterService.cs:186` resolves the captor as `captorParty.LeaderHero` and routes a hero-led band to the
  PROFILED-Hero branch (not the synthetic-brigand branch), so the boss's memory flows via `AppendHistory` with zero
  new scene code. A persistent-Hero captor can also be forced virtually via the `ArrivedPaymasterFor` override pattern
  (`:84-93`).
- **Profile/memory = FREE.** `HeroProfileMapper.FromHero(boss)` + `store.Set` (`Mapping\HeroProfileMapper.cs:18-44`,
  `Storage\BannerlordNpcMemoryStore.cs:37,53`) gives any Hero a persisted profile; `CaptiveEncounterService` already
  mints a captor lord's profile on first capture. `cr.memory`/`cr.info` then work on him identically to a lord.
- **THE ONE NEW SEAM = WIN/intercept chat-first against a bandit party.** Vanilla routes bandit map-contact to the
  attack/leave encounter menu, not `hero_main_options`, and CR's interception explicitly EXCLUDES bandits
  (`InterceptionBehavior.cs:307`). Build it by intercepting the bandit encounter menu (`GameMenuOpened` handler like
  `AmbushCaptivityBehavior.cs:354-388`, or a Harmony `GameMenu.SwitchToMenu` prefix like `Letters\VanillaCaptivityPatch.cs:36-78`)
  and opening `ChatManager` directly pre-battle, then dismissing the encounter cleanly via
  `ChatManager.EndMapEncounterIfNeeded` (`:330-345`). Medium effort, solid precedent, no blocker.
TOP RISKS to guard at build time: (1) do NOT leak the persistent bandit Hero into vanilla bandit-boss/ransom/notable
systems (despawn his PARTY between encounters, keep the HERO alive for re-attach, keep him out of native reaping);
(2) save/load must re-attach the SAME Hero to a NEW regional band next encounter (needs the roster SyncData behaviour +
the pure `NemesisRosterPolicy`/`NemesisRevealPolicy`); (3) pre-battle interception of a hostile party is where the
historical army-encounter save-corruption crashes lived, so reuse the `PlayerIsSubordinateInArmy` /
`EndMapEncounterIfNeeded` guards. Store the boss's `Hero.StringId` on `NemesisIdentity` so the identity survives while
his party is despawned.

**INCREMENT-1 HOOK GAP (Gabriel 2026-07-05):** the persistent bandit chief is ALREADY created for the scripted
town-AMBUSH path (`SpawnCaptors`), but NOT for the case the design actually centres on: the player LOSING a
VANILLA map encounter to a bandit party and being taken prisoner the ordinary way. That plain-defeat capture is the
NEW hook increment 1 must add: when a bandit party defeats+captures the player, run the roster/reveal roll and, on a
hit, attach the persistent nemesis Hero as the captor (mint-or-reuse per faction+region) so CR's existing captivity
scene runs with HIM and his memory. So the two entry points are: (a) ambush arc = already builds a chief, just make
it persistent + identity-backed; (b) vanilla lost fight = the genuinely new capture hook. Both then converge on the
same attach-Hero-as-captor machinery.

**EXPANDED VISION (Gabriel brainstorm 2026-07-05): the vendetta has FOUR resolutions + bounty missions, nearly all
FREE because the boss is a real Hero.** This may be the FEDERATING pillar: it pulls in captivity, stance, grudges,
world-events, quests, and captor-scenes at once.
- **Battlefield:** he is NOT a separate elite troop, HE is the elite, a named Hero agent leading the band; equip and
  skill him as a real threat with a solid escort. (Loadout is TUNE.)
- **Resolution 1 KILL in battle** -> Hero dies -> world-event. Nearly free.
- **Resolution 2 EXECUTE:** capture him (he is a Hero, so vanilla take-prisoner works) then the vanilla behead option
  -> head on a pike + world-event. Nearly free (verify the execute option surfaces on a bandit-faction hero).
- **Resolution 3 IMPRISON + TORTURE in chat:** the INVERSION, the player is now the captor, reusing CR's existing
  captor-side captive scenes, and loaded with his full vendetta memory ("remember my finger?"). Nearly free.
- **Resolution 4 SPARE + TURN (recruit):** a Stealth/Rogue fantasy, beat him then spare him, his stance turns
  (Respect/Fear up, grudge resolved) and he can be recruited as a companion. The DEEPEST branch, the only
  not-free one: needs a HARD stance/grudge-gated turn path (reuses the just-built stance mechanical layer) + a
  bandit->clan faction conversion. Design the turn gate before building.
- **Bounty MISSIONS (the world hook):** a lord tasks the player to capture or execute the nemesis, verified by the
  real capture/execution event; reuses the quest/negotiation framework + world-events. Ties to Extension Surface.
SEQUENCING: increment 1 (persistent Hero captor + memory + attach + chat-first + escape-decay) unlocks the core;
then RESOLUTION (kill/execute/torture, nearly free); then bounty missions; then the TURN branch (most gated, on
stance). Each layer is standalone and shippable. See [[project-nemesis]].

**INCREMENT 1 🔨 CODE-COMPLETE 2026-07-05 (committed, NEEDS IN-GAME VALIDATION, mod 0 err / 1244 tests):**
- **Persistence** — `NemesisBehavior : CampaignBehaviorBase` + `NemesisRoster`: save-safe byte[] JSON roster
  (additive key, degrades to empty, mirrors GrudgeBehavior), registered in the submodule. `FindOrMint` = the
  regional recurring roster (reuse the living nemesis of a band+region, else mint a latent identity via
  `NemesisNameGenerator`).
- **Capture hook** — `OnHeroPrisonerTaken`: when a bandit party takes the PLAYER, reuse/mint the nemesis, mint or
  reuse his persistent bandit-faction Hero (the proven `SpawnCaptors` pattern MINUS the throwaway kill), name him
  after the identity, register a memory profile (so `cr.memory`/`cr.info` work on him = the "Ranulf has no memory"
  bug fixed by construction), and record him as the active captor. `RecaptureCount` bumps on re-capture.
- **Captor override** — `CaptiveEncounterService` now resolves `captor = ambushPaymaster ?? nemesisCaptor ?? LeaderHero`,
  so the captive scene runs with the nemesis Hero and his memory (the profiled-lord CNC path).
- **Escape-decay** — the scene-escape roll uses `NemesisEscapeChancePolicy` (decays with `RecaptureCount`) against a
  nemesis; `VanillaCaptivityPatch` fully suppresses the vanilla escape drip while a nemesis holds the player
  (`PlayerHeldByNemesis`), so you cannot wait him out; never a perfect cage (2% floor).
- Defensive throughout (degrades to the vanilla bandit scene on any failure). Commits: persistence, FindOrMint,
  capture hook, escape-decay. **STILL OPEN (polish):** the "not this time" prompt beat when RecaptureCount > 0
  (needs an EncounterContext note); the physical band-leadership vs the virtual override (chose the override,
  lower-risk); band-culture/region keys are coarse first-pass (TUNE); the WIN/intercept chat-first side (the
  spike's one new seam) is a later increment. **The whole flow needs an in-game play-through to validate.**

**PLAYER REPORT (Nexus 2026-07-23): the NO-NEMESIS captivity path is an immersion hole, evidence for closing
the reveal-roll miss.** A player held six days by ONE bandit band got three captor scenes with NO memory
between them ("either he starts all over, or he pretends I had escaped": the LLM confabulating to fill the
void), the Memories button gone (it requires a real Hero), and could not reach the chief through
character_overrides (overrides apply via SetTraitLevel on real Heroes; the synthetic `bandit_captor_*` chief
has none and is DELIBERATELY never persisted, ChatViewModel ~line 1688). He also saw the boss's NAME change
mid-captivity: the synthetic name is SEEDED by the band's StringId (deterministic per band), so a name change
means a prisoner transfer between parties (he runs a bandit-overhaul mod, party churn likely) or a mid-arc
switch to a nemesis/paymaster captor. Deeper diagnosis (code, same day): the
capture-time attach is UNCONDITIONAL, not a roll. The real loss paths are (1) SAVE/LOAD mid-captivity:
`ReestablishActiveCaptorOnLoad` keys on the band's LeaderHero, which the chosen VIRTUAL override leaves null,
so the restore always degrades (nobody plays six game-days without saving); (2) prisoner TRANSFER between
bands (reference equality on the recorded party); (3) the respawn cooldown (by design, keep).

**FIX BUILT 2026-07-23 (untested in game): LAZY ATTACH AT SCENE TIME.** `NemesisBehavior.TryAttachCaptorFor`
re-resolves the captor when a bandit captive scene is about to fire with no nemesis mapped: same mint-or-reuse
path as the capture hook, still honouring the respawn cooldown (quiet years stay quiet), deliberately NOT
bumping TimesEncountered/RecaptureCount (a reload is the same captivity resuming, and counting it would
inflate the escape-decay). Wired in `CaptiveEncounterService` right after the captor resolution chain. Result:
every scene-bearing captivity now runs with the persistent named boss (memory, encyclopedia, overrides), and
the ephemeral chief remains only inside a slain nemesis's cooldown, which is correct.
**FOLLOW-UP (AUDIT-NEMESIS RC1, 2026-07-24, untested in game): the lazy attach above INTRODUCED a wrong-captor
path, now closed.** It re-resolved the captor by `FindOrMint(culture, region)` at scene time, but `ResolveRegionId`
degenerates to culture for a wandering band, so a prisoner transferred to a hideout-anchored party (region =
`hideout_x`) or a save/load minted a SECOND nemesis B who then inherited nemesis A's captivity memory: the exact
"different nemeses share the same memory" player report. FIX: the captivity is now anchored to a PERSISTED
`_activeCaptorIdentityId` (new additive SyncData key `CR_NemesisActiveCaptorId`, absent-reads-null on old saves).
`OnHeroPrisonerTaken` sets it; `ReestablishActiveCaptorOnLoad` restores the SAME captor by id (not by the band's
null LeaderHero, which is why the old restore was dead code for the virtual override); `TryAttachCaptorFor`
REUSES the anchored identity, never re-resolving by region while one is set; `OnHeroPrisonerReleased` now drops
the anchor UNCONDITIONALLY (a stale mapping used to survive a release from a different party and block future
materialization). Also fixed alongside: **RC2** (`FinalizeConversationMemory` wrote the fallback summary to
`DialogFlow.Profile`, a stale cache, landing a captor scene's memory on the last person chatted with; now writes
to `MainSpeaker().Profile`, the same hardened resolution the speaker path uses), **RC3** (mint now dedups
name+epithet against LIVING identities via `NemesisNameGenerator.GenerateDistinct`, so no two live nemeses are
homonyms), and **M7** (`OnHeroPrisonerReleased` no longer writes "they slipped my grasp" for every non-death
release; escape → grievance, a release the nemesis CHOSE → his own magnanimity, ransom/peace/battle → no false
memory). Tests: 5 new on `GenerateDistinct` (RC3). **NO REGRESSION TEST for RC1/RC2 itself: the scenario needs a
real captivity plus save/load or a prisoner transfer, which the self-test harness cannot stage.** MANUAL TEST
(the reported bug): be captured by a wandering looter band, note the boss's name, save and reload mid-captivity,
trigger the next captor scene, assert it is the SAME named boss and he remembers the earlier scene; then escape
and confirm his memory says he was escaped from, not that he let you go.

**M2/M3 BUILT (AUDIT-NEMESIS, ratified 2026-07-24, untested in game): bounties no longer zombie.** Gabriel's
rules: a bounty is PAID only when the PLAYER causes the capture or death; it lives as long as the nemesis
lives (no deadline); SEVERAL lords may each price the same head (the player collects from each); it closes
when the nemesis dies or is turned. FIX: (M3) `FindEligibleNemesisFor` excludes turned nemeses, and its
already-active check is now PER-GIVER (`NemesisBountyPolicy.IsEligibleIssuer` gained `nemesisTurned` and a
per-giver flag) so multiple lords may offer, while the one-task-per-lord rule stops the same lord stacking
two. (M2) `QuestService.CancelOutstandingBountiesOn` closes every OUTSTANDING bounty on a nemesis who dies by
a third hand or is turned (leaving a player-earned, awaiting-reward bounty untouched to collect); called from
`NemesisResolutionBehavior.Resolve` (third-party death only) and `Turn`. Also fixed: `QuestDeedTracker.OnHeroKilled`
stamped a NemesisBounty regardless of who dealt the death, paying the player for a kill he did not make; it now
requires `killer == MainHero` (an ordinary ExecuteEnemy quest stays killer-agnostic). Tests: bounty-policy
suite updated for the new signature (turned excluded, per-giver, multiple lords).

**M1 BUILT as a NEW FEATURE (AUDIT-NEMESIS, ratified 2026-07-24, untested in game): SCATTERED-LOOT RECOVERY.**
The audit's M1 was that a fled bandit escape MINTED a nemesis (ignoreRespawnCooldown) to hold the whole stock,
resurrecting a slain boss in his own quiet years and inflating his counters. Gabriel re-scoped the fix into a
feature: the gear is no longer held by anyone. On a fled BANDIT escape it is SCATTERED per-item among that
culture's bands (`ScatteredLootBehavior`, save-safe additive key), and defeating a band of the culture has a
chance (`ScatteredLootPolicy.RecoveryChancePercent`, 55%) to return the OLDEST-lost matching piece into the
player's inventory, drained a piece at a time across several fights ("a sword in one band, the armour in the
next"). No world-map loot object: it materialises only on a defeat. The LORD branch is UNCHANGED (a lord holds
his captive's gear, recovered whole by beating him, via `CaptiveGearBehavior`). `NemesisBehavior.ResolveGearHolderForBand`
(the offending mint) is REMOVED, and `CaptiveEscapeCoordinator` scatters instead. The escape notice now tells
the player HOW to recover (partly addressing M5's silent loot message). 6 pure tests on `ScatteredLootPolicy`
(chance boundary, oldest-first drain, culture match incl. case, no-match). `cr.loot_stock` dumps the ledger.
STILL OPEN from the audit: M5 discoverability (encyclopedia "your history", regional rumours), M6 spoken
pre-battle reunion, M8 memory of reverses, T13 trait randomisation, m-level polish. Those are pillar-depth
work, a ROADMAP decision, not defect fixes.
**RATIFIED + BUILT same day (Gabriel 2026-07-23, untested in game): compatibility is a FILTER, not a wall.**
The profiled-captor path used to gate ALL scenes on `Romantic.IsCompatibleWith(player sex)` with an early
return before intent selection: a male player held by a straight male lord/nemesis (the majority pairing) got
NO scenes for the whole captivity, an asexual captor could not even interrogate, and the lazy attach above had
just made that path the default. The gate now lives INSIDE `CaptiveEncounterPolicy.SelectIntent`
(`playerIsCompatible` parameter), the exact shape `SelectBanditIntent` always had: incompatible, the sexual
intents (PersonalDesire, Domination, Torture, Training, Reward) are unreachable and the scene is an
INTERROGATION; compatible, the weighted draw is byte-identical to before (regression-pinned). Torture stays on
the gated side by explicit call: its Hardcore framing is sexualised, prudence wins. Side effect judged
correct: the companion-victim branch sits after the removed wall, so a captor incompatible with the PLAYER can
now still run a scene on a held COMPANION, which the wall used to absurdly block (the victim was never the
player). 3 tests in `CaptiveEncounterPolicyTests`, including an every-roll scan proving a sadist incompatible
captor can never reach Torture.
**WISHED (modder angle, same report):** the reporting player authors character_overrides. Once revealed, a
nemesis IS a real Hero and overrides reach him by name (the five native traits, backstory, conviction,
orientation; file loads at session launch, so add the name then reload). What a modder cannot do is author
AHEAD of the mint, since names are generated per campaign. Candidate: a culture-keyed template entry in
character_overrides (e.g. Hero: "nemesis:sea_raiders") applied to every nemesis minted of that culture, which
would let authors shape their region's boss without knowing his rolled name. Small, additive, worth doing.

**INCREMENT 2 (R2 execute / R3 torture entry point), REDIRECTED 2026-07-06, mod builds 0 warning, 1271 tests:**
a first pass built a recurring daily-tick popup menu (Torture / Execute / Leave) for a nemesis held prisoner.
Gabriel redirected: no clickable menu. Instead the player takes the initiative from the party-screen prisoner
"Talk" action, the same `hero_main_options` hub any hero conversation reaches (vanilla already lets you converse
with a hero prisoner you hold; no engine block to work around). `NemesisPrisonerDialogFlow` adds one extra line
there, shown only when the conversation partner is the tracked nemesis `NemesisBehavior.PlayerHeldNemesisPrisoner()`
returns, opening a PLAYER-AS-CAPTOR chat (`ChatManager.OpenForCaptorScene`, rides the live conversation scene
already open, VRAM-safe) instead of an ordinary chat. From there it is a CHAT: the player describes what they do
(interrogate, threaten, torture, spare, execute), and the LLM detects `execute_prisoner` exactly like every other
CR action. SDK teaches it in `PromptBuilder.AppendPlayerCaptorSceneRules` (generic: "the prisoner you hold", no
nemesis-specific text); the mod bridge (`BannerlordGameStateBridge.ExecuteExecutePrisoner`) re-validates custody
before calling the real `KillCharacterAction.ApplyByExecution`, which `NemesisResolutionBehavior` (closure deed for
a tracked nemesis) and `ExecutedKinGrudgeBehavior` (a real lord's kin vendetta) both already react to, unchanged.
Removed: `NemesisCaptivityPromptBehavior`, `NemesisCaptivityPromptPolicy` (+ its tests), the `cr.nemesis_captivity_prompt`
command and self-test. Console: `cr.nemesis_captor_scene` (unchanged, staged), `cr.execute_prisoner [name]` (new).
Self-test: `nemesis_execute_prisoner` (replaces `nemesis_captivity_prompt`, now exercises the real bridge action).
**Untested in game.**

**INCREMENT 3 (bounty missions, the world hook) 2026-07-06, mod builds 0 warning, 1291 tests:** a lord tasks
the player to hunt down a specific active nemesis for a reward, anchoring the personal vendetta in the wider
world. Reuses the quest framework end to end rather than adding new structure: a new `QuestType.NemesisBounty`
(appended last, save-safe) built and issued directly (never via `[QUEST]`/the LLM, since the target and reward
are already fixed, deterministic facts by the time the offer shows) through `QuestService.IssueNemesisBounty`,
journaled through the existing `CalradiaRemembersQuest` mirror exactly like any other quest. OFFER: a new
`NemesisBountyDialogFlow` registers on the same `hero_main_options` hub as `LlmDialogFlow`/`NemesisPrisonerDialogFlow`;
gated by `NemesisBountyPolicy` (pure, tested) on the PRIMARY rule only, the issuing lord's own region (home
settlement, else current settlement, else culture as a last resort) must match the nemesis's `RegionId` AND he
must be a living, already-manifested (Hero-bound) foe AND no bounty on him is already active anywhere
(`QuestService.HasActiveBountyFor`, store-wide). The optional "past victim" rule (a lord this nemesis has
personally wronged may also offer, regardless of region) is FLAGGED, not built: CR has no persisted record of
which lords a given nemesis has preyed upon. Accepting shows an accept/decline `InquiryData` naming him and the
reward; declining leaves the offer available again later (nothing is marked "declined"). VERIFIED BY THE REAL
DEED, never roleplay: `QuestDeedTracker`'s two existing hooks (`OnHeroPrisonerTaken`, `OnHeroKilled`) were simply
WIDENED to also recognize `QuestType.NemesisBounty` alongside their ordinary `CapturePrisoner`/`ExecuteEnemy`
cases, so a capture OR a kill (battle death or execution alike) by the player satisfies the same quest,
whichever comes first; `NemesisResolutionBehavior`'s own vendetta-closure listener on the same `HeroKilledEvent`
is untouched and fires independently. REWARD: `NemesisBountyRewardPolicy` (pure, tested) scales gold and
personal relation by his notoriety (`RecaptureCount`, `TimesEncountered`), both clamped to a sane ceiling; paid
on report-back to the issuing lord exactly like any other quest (bypasses the RelationGate cooldown/cap, same
as every other verified quest reward). No new persisted state: the bounty lives entirely inside the giver's
existing `ActiveQuests` list (additive enum value only). Console: `cr.nemesis_bounty [lord name]` (force-offers
and accepts, minting/reusing a nemesis for real in the lord's own region so the real gate and reward math both
run), `cr.nemesis_bounty_complete [giver name]` (force-completes and pays). Self-tests:
`nemesis_bounty_capture`, `nemesis_bounty_execute` (both mint a nemesis anchored to a real lord's region, issue
through the real path, then satisfy the quest through a REAL `TakePrisonerAction`/`execute_prisoner` bridge
call, not a simulated stamp). **Untested in game.**

**PILLAR: Player appearance — derived + editable (ratified 2026-07-05).** Kills the backstory/appearance
hallucination AND resolves the mutilation-persistence tension (a hallucinated "missing finger" must never
auto-persist; the player AUTHORS it if they want). Two volets: (1) DERIVE the reliably-available attributes from
`BodyProperties` (height, build/weight, age, gender, hair colour/style; NOTE eye colour and fine proportions are
baked in the packed face code and not reliably readable, so skip those); (2) an EDITABLE per-hero prose descriptor
in the Encyclopedia ("a mysterious air, moves with grace, limps on the left leg, missing a finger"), save-persisted
so it follows the heir on a character change. Together they render a "THE PLAYER'S APPEARANCE" prompt block and
RETIRE `player_description.txt` (replaced by the in-game field). Extends [[project-player-authored-characters]].
**Pure composer 🔨 BUILT 2026-07-05 (overnight, untested, uncommitted):** `AppearanceComposer.Compose(facts, authoredProse, voice)`
+ `AppearanceFacts` carrier, bands the 0..1 body sliders + age into phrases, omits unknowns, appends authored prose,
returns "" when nothing stands out. Banding thresholds are const/flagged TUNE (height 0.35/0.65, build 0.3/0.6/0.85,
age 25/40/60). **FRAMING (Gabriel 2026-07-05): the appearance is read BY the speaking NPC, so voice matters.** Now
GENERALIZED to serve BOTH targets via `AppearanceVoice` (Gabriel ratified scope = player + speaking NPC, witnesses
deferred): `Subject` (default, THIRD person, "A tall, powerfully built man in his middle years, with dark hair.") for
the PLAYER and any other described hero, rendered under a naming heading, never "you"; `Self` (SECOND person, "You are
a tall... man in your middle years, with dark hair.") for the SPEAKING NPC's OWN body, so it grounds its self-image
and never invents contradictory looks. Renamed `PlayerAppearanceComposer` -> `AppearanceComposer`. 29 tests.
**✅ SHIPPED v1.26.0 (2026-07-07), wired end to end:** `AppearanceFactsExtractor` reads the live hero (gender, age,
build/weight off `BodyProperties`); `FaceColorReader` resolves HAIR + SKIN to a real RGB (face-key offsets via
`MBBodyProperties.GetParamsFromKey`, sampled on that race/gender/age's colour gradient) and classifies them into
words; EYE colour is banded HEURISTICALLY from its raw offset (the engine exposes NO eye palette, verified by
reflection; `cr.appearance` prints raw offsets for calibration, bands are TUNE). `AppearanceProseBehavior` persists
the authored per-hero prose (byte[] JSON, save-safe); `EncounterContextBuilder` fills `PlayerAppearance` (Subject)
+ `NpcSelfAppearance` (Self); PromptBuilder renders both plus the bandit-captor "what you can SEE" line. Console
`cr.appearance [clear|<text>]` is the interim edit surface; `appearance` self-test. **Editable Encyclopedia field
✅ SHIPPED + validated in-game 2026-07-08:** on the PLAYER'S OWN hero page (`EncyclopediaHeroPageAppearanceMixin` +
`HeroPageAppearancePatch` + `CR_HeroAppearanceButton.xml`) a "How others see you" line shows the full composed look
(derived + note) and an "Add more info" button opens a **custom Gauntlet popup** (`AppearanceEditorManager` /
`AppearanceEditorViewModel` / `CalradiaRemembersAppearanceEditor.xml`, mirroring ChronicleManager) with a title,
explanation, a single-line field, and a LIVE wrapped preview of the final line; Confirm saves to
`AppearanceProseBehavior`. PLAYER-ONLY on purpose (an NPC note would orphan if they left the clan). The custom
popup (not `ShowTextInquiry`) fixes the Backspace-navigates-BACK bug and allows a larger box; see
[[feedback-bannerlord-ui-patterns]]. `cr.appearance` kept as the quick surface. STILL OPEN: height via
`GetScaleFromKey` (readable, not yet banded); retiring the legacy `player_description.txt` (still cohabits).

**Notes / no-action:** the captive-scene violence (stabbing, finger) LANDS and he wants MORE explicit interpersonal
scenes (a sensitivity toggle is a maybe, not a restriction); "it is easy to overpower the AI" but he PREFERS
agency over any restriction (do NOT add guardrails that curb RP agency); raw stream-of-consciousness feedback IS
welcome (a light bug/wish/note structure would help even more).

### Town ambush — works end-to-end behind the toggle; refining *(stance increment 3; still shipped OFF)*

The Assassinate stance consequence (a lord whose hatred turned murderous) springs a Warband-style STREET FIGHT
when the player walks into a town. **WORKS END-TO-END IN GAME 2026-06-29** (validated by Gabriel): the bandits
surround and attack, a lost fight convoys the player to the paymaster for the reckoning audience, and the
paymaster then visits the prisoner for recurring captivity scenes. It still ships **OFF by default** behind the
experimental MCM "Town Ambush" toggle while the last refinements land (shipped in v1.17.4 in that state).

History: the first build opened a roster alley fight, which the engine framed as a SIEGE with the attackers
spawning ~300m off at the walls. Rearchitected 2026-06-29 (Warlord / War-Sails-style): drop the player into the
real town-CENTRE walkable scene (`PlayerEncounter.LocationEncounter.CreateAndOpenMissionController("center")`),
inject the cutthroats as hostile `LocationCharacter`s at the townsfolk spawn points
(`LocationCharactersAreReadyToSpawnEvent` + `Location.AddLocationCharacters` with `CharacterRelations.Enemy`), and
start a real fight from a **Harmony postfix on `Mission.AfterStart`** (the one reliable "fully started" seam) via
`MissionFightHandler.StartCustomFight`. Both prior crashes (PatrolAgentBehavior NRE; "Collection was modified" from
adding a behavior mid-`AfterStart`) are resolved.

DONE (validated): bandits spawn near the player and attack (real-time poll + `MissionFightHandler.StartCustomFight`,
the no-aggressors fizzle now logs loudly); the gating decision is a pure tested `AmbushPolicy.DecidePlayerTownEntry`
(the trigger path used to be silent — armed-but-disabled now logs); navmesh-aware ~20m cutthroat placement; player
teleported to the walkable-area centroid (town centre, not the gate); captivity map fast-forwards at 3x after the
audience (one-shot un-pause in the real-time tick); the paymaster runs the recurring captivity scenes even when
custody stays with the band; all captive scenes use a neutral player-POV narrator.

OPEN — the refinements before removing the toggle:
1. **Custody transfer to the lord** when he sits in his town: `TakePrisonerAction` on his in-settlement party does
   not "hold" (`held=False`), so the player stays the band's prisoner (harmless now that the paymaster visits
   regardless, but the proper lord-imprisonment / ransom disposition is unfinished). **Decided 2026-06-30: SHIP the
   band-custody fallback as the intended disposition for a town-sitting lord (a hired band holds you until ransom /
   escape) — not a blocker.**
2. ~~**Fleeing townsfolk**~~ **MOOT + REPLACED — DONE 2026-06-30.** The brawl plays at the GATE (outside the walls),
   so there is no townsfolk crowd — but there are a gate guard or two. `AmbushFightMissionLogic.SendBystandersFleeing`
   now scatters them when the fight starts: any human agent in the scene who is not a combatant (guard / stray
   civilian) is made **invulnerable** (a slain guard = a crime / odd mission end), sent **running away from the
   player** on the navmesh (reusing the file's navmesh helpers), then **faded out** ~2.5s later. UNTESTED in game.
3. Then **remove the toggle** — the last step, gated on Gabriel **playtesting the 2026-06-30 refinement lot in game**
   (force scaling / elite chief / companions-captured are BUILT but UNTESTED). Removal = `CrSettings.TownAmbushEnabled`
   + `ModSettings.TownAmbushEnabled` + the two gates in `TownAmbushBehavior` (l.118 `IsEntryBlocked`, l.165
   `DecidePlayerTownEntry`).
Console `cr.ambush`.

### Jealousy — the competition loop and the duel *(v1 shipped; escalation deferred)*

v1 fires a one-shot reaction (staked NPC → grudge + grievance memory + JealousThreat letter, witness-gated,
culture-flavoured). **Plausibility gate SHIPPED v1.29.1** (player report: a male player did a male lord a service
and the lord's wife sent threats): pure `JealousyTriggerPolicy`, a same-sex pair is never a rival courtship
(matches the opposite-sex romance/marriage model), a flirt stamped on a quest turn-in is mislabeled gratitude
(ignored), spoken flirtation needs a repeated pattern (>= 2 flirt/intimacy events); intimacy counts at once for a
plausible pair. Deferred layers:
- **WISHED (Gabriel 2026-07-09): the SUSPICIOUS SPOUSE — repeated favors read as courtship.** A player who
  repeatedly does services for an opposite-sex married lord could rouse the spouse's SUSPICION even with no flirt
  at all (the wife watching a young champion keep doing her husband favors). A deliberate separate trigger, NOT
  the flirt path: keyed on repeated Collaboration events / high regard with an opposite-sex married NPC, own
  thresholds, likely a milder register (cold words, a warning letter) before real grudges. Design carefully so
  ordinary questing does not spam suspicion (the v1.29.1 report was exactly a false positive on a quest turn-in).
- **Competition loop**: rival suitors who actively and repeatedly contend for the same NPC over time (not a
  single reaction) — courtship as an ongoing contest the player can win or lose.
- **Duel & scheming**: a slighted rival may challenge the player or scheme against them — a real
  confrontation, not just a letter. Pairs with the scripted-scenarios engine (a jealousy threshold as a
  trigger) and the love-triangle tie-in noted under Camp life.

### Marriage — true polygamy and divorce *(monogamous marriage shipped; divorce arc ✅ COMPLETE — Phase 1/2a/2b/escalation, v1.22.1; true polygamy still OPEN)*

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

**Player-reported bugfix (2026-07-04 — ✅ SHIPPED v1.22.1), 1116 mod / 242 SDK tests green.** Two live
v1.22.0 bugs: (1) the marriage scene never triggered after an NPC accepted — the eligibility gate checked ONLY the
mod's own personal-regard ledger (`ReputationWithPlayer >= 50`), ignoring vanilla clan relation players had actually
built; broadened both synced gate sites (`EncounterContextBuilder.ResolveLoveMatchEligible` + the bridge's
`ExecuteMarry`) to pass on EITHER, via one shared `MarriageEligibility.MarriageEligibilityThreshold` const, and
added an anti-empty-promise guard in `PromptBuilder` (marriage is family-gated, cannot be settled in one
conversation) for the non-eligible + still-unmarried case, dropped in Lean. (2) after `cr.marry` the NPC still
treated the player as a third-party spouse (infidelity framing, the ≥30 stranger-resistance trust gate) instead of
their own spouse — new additive `EncounterContext.NpcSpouseIsPlayer` renders a distinct marital-intimacy block
(natural, not infidelity, no stranger thresholds) when the NPC's `Hero.Spouse == Hero.MainHero`.

The marriage chain (family-approval, consorts, by-letter, jealousy) is shipped. One genuinely-open extension,
one BUILT:
- **Culture-gated true polygamy** as lore — multiple full spouses, restricted to designated cultures (not the
  current consort workaround), with the family-approval currency scaled accordingly.
- **Divorce, Phase 1 (NPC case) — BUILT 2026-07-03 (Sonnet, untested in game, NOT committed), 755 tests green.**
  A player tried to convince an NPC to leave their spouse; the NPC "agreed" but nothing happened ("soon, soon"),
  the classic empty-promise pattern (no verb, no executor). Design framing (Gabriel, 2026-07-03): **a marriage IS
  an alliance, so a divorce is a loss of influence and power**, and BOTH families oppose it fiercely — never "talk
  once and it is done". Built: `end_marriage` starts a multi-day **estrangement process**
  (`DivorcePolicy.ProcessDurationDays` = 15) the spouse may resist — a daily roll (Honor/Calculating raise it) can
  Delay (push the end day back, capped) or Abort the process outright — rather than an instant flip; reaching the
  end day re-validates every guard and, on success, dissolves the marriage for real (`MarriageDissolutionService`)
  and records a **culture-scaled scandal** world-event (Nordic/Sturgian 0.5x, default 1.0x) plus relation hits (the
  ex-spouse toward the NPC and toward the player, and the ex-spouse's own clan toward the player). Gates: the
  marriage must be genuinely unhappy (relation ≤ -5) and the bond with the player deep (≥ 30); refuses while either
  party is pregnant or a prisoner, or while the spouse who would have to relocate clans heads their own clan, leads
  a party, or governs a settlement (Phase 1 REFUSES rather than reassigning those roles). **This is also the first
  verb DESIGNED for the new `IConversationVerb` contract** (`CalradiaRemembers.Verbs` / `VerbRegistry`, not merely
  migrated onto it like the four RP-with-teeth court verbs) — validating that the contract makes a brand-new verb a
  clean "implement the interface, register it" addition with no switch statement or prompt-builder method to touch.
  **Technical crux, RESOLVED by an in-game reflection spike on 1.3.15** (the earlier note that `Hero.Spouse` "has no
  public setter" was WRONG): `Hero.Spouse`'s setter IS public and self-cleaning (nulls both sides, appends each to
  the other's `ExSpouses`); clan restore for the spouse who joined the other's clan mirrors `Hero.AfterLoad()`'s own
  fixup ladder (OriginClan, else smallest same-culture clan, else any — never clanless); `Romance`'s internal
  `SetRomanticState` is cleared via cached reflection, non-fatal on failure. No dependency on `DivorceOfCalradia`
  needed. **Phase 2b (the headliner: the unhappy spouse DEMANDS) — BUILT 2026-07-04 (Sonnet, untested in game,
  NOT committed), 1090 mod / 230 SDK tests green.** Answers Gabriel's 2026-07-03 question ("est-ce qu'un NPC
  malheureux peut demander le divorce au joueur?"): the player's OWN spouse, if deeply unhappy, raises the demand
  entirely through conversation, no UI button. Pure gate `SpouseDivorceDemandPolicy.ShouldDemand` (new
  `CalradiaRemembers.Logic`): fires when the relation reads flatly miserable (≤ -20), OR a fresh wound just struck
  (a recent jealousy grudge, or a discovered bastard-scandal world-event) even at a merely moderate relation, OR a
  live grudge against the player already clears a magnitude bar (40) — never while either party is pregnant or a
  prisoner, never while a demand is already pending. `SpouseDivorceDemandBehavior` (weekly tick) evaluates the
  player's current spouse, sets a persisted flag (additive byte[] SyncData, spouseId → day the demand began) and
  queues an unbidden `LetterReason.SpouseDivorceDemand` letter in the spouse's voice ("I can no longer bear this
  marriage"). In conversation, a new `EncounterContext.SpouseDivorceDemandNote` (host-composed, mirroring
  `GrudgeNote`'s field pattern rather than routing through `ExtraActionTeachings`, since it names one NPC's single
  demand, not a set of independently-eligible verbs) teaches `accept_divorce` / `decline_divorce`. Accept dissolves
  the marriage via a new `MarriageDissolutionService.TryDissolvePlayerMarriage` (a thin wrapper over the existing
  `TryDissolve` — "the player never relocates" falls out of the pre-existing clan-leader invariant for free, no new
  guard code needed) with SOFT mutual-consent consequences (a modest relation settling, no scandal-magnitude grudge
  storm — this was the spouse's own wish). REFUSAL is registered two ways: an optional `decline_divorce` action for
  a firm in-character refusal (fires immediately), and the PRIMARY path, a grace-period timeout (12 days) swept
  weekly — a demand nobody ever explicitly answers is read as a refusal too, so the mechanic never depends on the
  LLM choosing to emit a negative action. Either way: a `GrudgeSource.Divorce` grudge plants on the spouse, and the
  flag clears so the demand can resurface fresh later. Console: `cr.spouse_divorce_demand` / `_accept` / `_decline`
  / `_info` / `_tick`; mechanical `spouse_divorce` self-test (marries the player to a test spouse when unmarried,
  exercises accept then a second decline scenario). **Phase 2a (the player PROACTIVELY ends their own marriage) —
  BUILT 2026-07-04 (Sonnet, untested in game, NOT committed), mod 1096 / SDK 234 green.** Also chat-driven, no
  button: talking to their own spouse, the player states plainly they want to end it, and a new
  `EncounterContext.PlayerEndOwnMarriageNote` (taught only when `hero == Hero.MainHero.Spouse`, coexists cleanly
  with a simultaneous Phase-2b demand) teaches `[ACTION] end_own_marriage`. The bridge decides MUTUAL vs REPUDIATED
  via a pure `PlayerDivorceConsentPolicy.SpouseConsents` (mutual iff the spouse had a pending 2b demand OR their
  relation is at/below `DivorcePolicy.UnhappyThreshold` = -5 — Phase 1's exact "unhappy" bar, asked from the other
  side), dissolves IMMEDIATELY via `TryDissolvePlayerMarriage`, and applies consequences through a REFACTORED shared
  `DivorceProcessBehavior.ApplyDivorceConsequences(divorcer, spouse, mutualConsent, ...)` — ONE implementation now
  serves both the Phase-1 NPC-NPC path and this player path (no duplicated family/friends resolution). Repudiated ->
  the spouse + their immediate adult family + their 3 best friends take a `GrudgeSource.Divorce` grudge against the
  PLAYER, spouse relation SET via the direct `ExSpouseNpcHit` (the player is the divorcer, not a third-party
  orchestrator, so no separate `ExSpousePlayerHit`); mutual -> soft. Records a Scandal (repudiated) or Other (mutual)
  world event + chronicle line, clears any pending 2b flag. Console `cr.end_own_marriage`; mechanical `player_divorce`
  self-test (same best-effort seeding-skip as `spouse_divorce`). Judgment call flagged: `ActionOutcome.IsPositive` is
  true for both tiers (the deed succeeded), so a repudiation still cues the positive sound/colour — revisit if the UI
  should mark the wronging as negative. **Escalation (the refused spouse begins ending it themselves) — BUILT
  2026-07-04 (Sonnet, untested in game, NOT committed), mod 1107 / SDK 238 green.** Answers the follow-up Gabriel
  flagged: a spouse refused enough times, still miserable, stops merely demanding and BEGINS an estrangement DESPITE
  the player, on a deadline, rather than waiting forever on their consent. Owner-ratified shape lands SIMPLER than the
  originally-sketched "same resistance process as Phase 1 with the player resisting": no daily Honor/Calculating rolls
  here, just two pure gates in `SpouseDivorceDemandPolicy` — `ShouldEscalate` (refusal count ≥ `RefusalsBeforeEscalation`
  = 2 AND relation still ≤ `DivorcePolicy.UnhappyThreshold`, never while pregnant/prisoner/already-escalating) and
  `IsReconciled` (relation ≥ `ReconciliationThreshold` = 10 — deliberate HYSTERESIS above the misery bar, so a genuine
  warming calls it off, not a nudge that merely clears the misery reading again). `SpouseDivorceDemandBehavior` now
  owns a running per-spouse refusal COUNT alongside the demand flag (both additive byte[] SyncData, a shared
  `SyncDictionary` helper serializes all three spouseId→int dictionaries under their own save keys) and an escalation
  record (spouseId → deadline day, `today + DivorcePolicy.ProcessDurationDays`); `RegisterRefusal` increments the
  count and, when `ShouldEscalate` clears, calls `BeginEstrangement` instead of leaving the demand to resurface —
  clears the demand flag, sets the deadline, queues a `LetterReason.SpouseDivorceEscalation` letter ("I have waited
  long enough"). The weekly tick gained `SweepEscalations`: reconciled (checked BEFORE the deadline, so it always
  wins even on the deadline's own day) calls it off via `ResolveReconciliation` (clears the record + refusal count,
  a relief profile note); deadline passed walks out for real via `ResolveWalkOut`, reusing the SAME
  `MarriageDissolutionService.TryDissolvePlayerMarriage` every other divorce path shares (its own live
  re-validation IS the anti-double-dissolve guard — an already-dissolved marriage just fails here and the record is
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
  designed for it) — Phase 2b is the dedicated feature for that case. Still open: letters/journal-mirror surfacing
  the estrangement beyond the initial letter (today's `[SPOUSE_DIVORCE]` log line and the announcing letter are the
  only visible cues, no reminder as the deadline nears); a `chat_spouse_divorce`/`chat_spouse_estrangement` LLM
  self-test (today's are mechanical only, see `cr.testall`); Khuzait cultural nuance research.

### Smaller polish (deferred)
- **Chat composer — a true MULTI-LINE editing box** *(wished 2026-07-01; the "best" option, deferred)*: a player
  asked for a bigger input for long messages. SHIPPED 2026-07-01 was **Option 2** — a "<" button beside the small
  input opens a left panel showing a **wrapped, read-only REVIEW** of the message (a `RichTextWidget`, which wraps,
  in a `ComposeScrollablePanel` so a long message scrolls), updated live; editing stays in the small box on the
  right. **Definitively established that a true multi-line EDITOR is NOT achievable with native widgets:**
  Bannerlord's `EditableTextWidget` is single-line — no multiline/wrap property on the widget or its `Brush`
  (confirmed by reflection), it does not auto-wrap, and Enter does not insert a newline (tested in game). The
  **Editable Encyclopedia** mod (decompiled, un-obfuscated) does NOT have an editable multi-line box either: it
  fakes multi-line for READ-ONLY display via a `WordWrap(text, charsPerLine)` helper (inserts `\n`) rendered by a
  `TextWidget`, and its editor is a single-line enlarged inquiry. So Option 1 needs a **custom widget from
  scratch** (wrap layout + a caret that navigates wrapped lines + click-to-position + selection + scroll + newline
  input) — a sizeable, fragile UI build, hard to iterate (no automated UI tests). Deferred. A cheaper middle
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
  (in `cr.testall`): deterministically asserts the gate fired + a wanderer was gathered, then logs the reply to
  judge the grounding. Pure `RecruitIntentDetector` has 5 unit tests.
- **Ambush bandit placement on the navmesh** *(wished + BUILT 2026-06-28, untested in game)*: the old
  `AmbushFightMissionLogic` ring was raw geometry (cos/sin at the player's Z), so thugs could land between walls
  with no path to the player. Now each thug is placed on a point that is BOTH on the navmesh AND reachable: from
  the player's valid `WorldPosition`, `SetVec2` slides the candidate onto the navmesh, then
  `Scene.GetPathDistanceBetweenPositions` confirms a real path (rejecting blocked / disconnected spots and ones
  reachable only by a long detour). A fan of angle offsets + a few radii (ideal ~20m) dodges a wall; a
  toward-the-player navmesh fallback covers the rare total miss. `cr.ambush` to test in game.
- ~~**Fleeing townsfolk during the town ambush**~~ *(wished 2026-06-28)* — **RESHAPED + DONE 2026-06-30 (untested
  in game).** The brawl plays at the GATE, outside the walls (the player spawns there; snapping to the walkable
  centroid dragged them past the gatehouse), so there is no townsfolk crowd to scatter — only the gate guard or
  two. Rather than SPAWN civilians, `AmbushFightMissionLogic.SendBystandersFleeing` makes the guards/civilians
  ALREADY present flee: at fight-start, every human agent that is not a combatant (not the player, an ambusher, or
  an ally) is set `Agent.MortalityState.Invulnerable` (a slain guard = a crime / odd mission end), sent running
  away from the player via `SetScriptedPosition` onto a navmesh point + `SetWatchState(Alarmed)`, then `FadeOut`
  ~2.5s later (`TickBystanderFlee`). No spawn, no team plumbing, no win/lose interference. `cr.ambush` to see it.
- **Configurable LLM response timeout** *(player bug 2026-06-28, BUILT)*: a player on longer messages hit
  "LLM Call Failed in 100008ms: A Task was cancelled" — the HttpClient default 100s timeout firing when a long
  prompt makes a slow/reasoning model think past it. Now the per-request timeout is enforced in the SDK via a
  linked `CancellationTokenSource` (so EVERY caller is bounded), independent of the HttpClient ceiling (raised
  to 15 min so it never pre-empts). New MCM setting "LLM Response Timeout (s)" (15-600, default 120,
  `ModSettings.LlmResponseTimeoutSeconds` → `OpenRouterConfig.TimeoutSecondsProvider`). The opaque "A task was
  canceled" is replaced by a clear, actionable message naming the timeout and the setting. Only a genuine caller
  cancellation (chat closed mid-call) still propagates.
- **Negotiation Phase 1 polish**: a richer `DeliverItems` spec (category + tier + count — "a warhorse, a
  sword of tier 5+") instead of a value floor; the joining wanderer EQUIPS the gear demanded for them.
- **Companion happiness**: pay / share-of-loot driver; let the player ADDRESS a grievance more richly.
- **Camp life**: a child of a companion couple joining the clan as a chronicle/letter beat; the love-triangle
  tie-in with jealousy.
- **Influence loop**: family-across-clans kin detection (today it is clan-mate only); a richer per-event
  dynastic-succession LLM rewrite (the current succession is deterministic).
- **Dynastic succession**: the optional per-NPC LLM reframing for richer dynastic prose.
- **Log file rotation** *(wished 2026-06-24)*: `CalradiaRemembers.log` accumulates across sessions and can
  grow very large. The fix: truncate (or rename to `.bak`) the file **on session launch** so each play
  session starts fresh. **The Story Log (`CalradiaRemembers_story.log`) must NOT be touched** — it is an
  ongoing novel that grows deliberately across all sessions and belongs to the player.

---

## v2.0 — Player influence on the world (CK3-style)

Persuading a faction leader toward war, luring an enemy lord to defect, assassination, espionage. The most
powerful layer — and the most dangerous for save balance — so it ships last, on a proven foundation. Two
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

## Fragmented, mergeable prompt files (fixes stop being shadowed by Custom edits)

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

## Governing principles (carried across every layer)

- **The hero is the PLAYER.** Spend tokens on what surrounds the player, not on NPC↔NPC scenes the player
  isn't in. Background simulation is MECHANICAL (native relations, cheap ticks); the LLM narrates only at the
  player's point of contact.
- **Verified deeds, never words.** The game computes and verifies outcomes (value floors, real transfers,
  real combat, real events); the LLM negotiates the story. "Quests that don't break."
- **Open & editable.** Prompt/data files in `ModuleData` are the moddable surface; the code is the engine.
