# Calradia Remembers: roadmap archive

Sections moved out of ROADMAP.md on 2026-11-19, word for word, because the code shows them shipped, dropped or
superseded (audit of 19/11/2026). Their one-line status is under SHIPPED in ROADMAP.md. Statuses written inside
these sections ("untested", "not built") are as they were when written, not current.

---

## RULED 2026-10-27 (Gabriel: "On va le faire maintenant"), BUILDING: What You Know of them

Gabriel (27/10/2026), after the widened tastes in bed: "un NPC pourrait aimer faire certaines activitees ou encore
preferer certains cadeaux. Par exemple, un Lord pourrait aimer aller a la chasse. Une Dame pourrait aimer les fourrures
[...] Et nous pourrions aussi ajouter certaines superstitions et phobies", then "Et aussi, 'qu'est-ce qui compte le plus'
pour un NPC. Ses enfants? Sa richesse? Son amoureux?", and "il faudra penser a les indiquer dans des sections de
l'encyclopedia". One pillar, the same shape as the tastes in bed: each character has their own, the player learns them
by living with them, the encyclopedia names what was learned (a What You Know section, the game's words only; the
Discoveries section stays the model's), NPCs never lie (a guarded one may keep it private, never name a false one), and
the chain is traits (and culture, and rank) weighing what they love.

Increments, in order:

1. **What matters most** (BUILT 27/10, mod 389a718 / SDK b075fda): one anchor chosen among what is true of them.
2. **Gifts**: loved and disliked kinds of gift (furs, wine, jewelry, horses...), from the item's own category, weighed
   by culture, traits and rank; a loved gift is worth more, a wrong one less; learned on giving.
3. **Fears and superstitions**: colour for the prompt, culture-weighted, learned when witnessed or told.
4. **Pastimes** (hunting, falconry, music...): narrated only, to start.

Defaults taken overnight, Gabriel's to rule (each reversible):

- **One anchor, not a first and a second.**
- **It moves only on an event of the world** (the child dies, the lover is gone, a grudge opens), never with time alone.
- **No dark lever.** Threatening what matters to someone to get something is not a mechanic; touching it for good (freeing
  their captive child) is the direction, and is NOT wired yet: the regard of such deeds is unchanged so far.
- **A wrong gift gives less, never costs regard.**
- **Pastimes are narrated only**: the game has no hunt or feast to play; a real menu action that spends a day is a
  later question.

---

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
   alone answers "too short or too long". Loader, editor checks (parity fixtures), save, `cr.test tale_arc`, the
   Rhagaea night written with it. BUILT 25/10/2026 (Gabriel: "continu"): `TaleArc` (Logic), the direction inside the
   giver's tale block (`AuthoredQuestPrompt.ForCharacter`, below the marker, so no SDK field was needed), counted in
   `ChatViewModel` once the reply's deeds are applied (main speaker, not the scene opener, not a captive scene: the chamber
   opens as an ordinary chat through `StageTaleMeeting`), `EndTaleArcSoftly`. The proposed close was taken as the default:
   it holds even with "NPCs can end conversations" off; Gabriel has not ruled on it. Every reply counts, synthetic ones
   included. The editor's arc cards shipped with it (its field registry must cover every field the game reads).
2. **Signals and transitions**: `listens`, `next` with `if`, `on_signal`, the `tale_signal` verb, the net roll.
   BUILT 25/10/2026: `TaleArc.SignalSeen`, `Exchanged` (on_signal jump, the `turned` hold, net rolls at the end of each
   listening beat, `next`), `SignalsForReader` joined to the tale's choices (`AuthoredQuestPrompt.ForActionReader`) so
   both readers are taught it; the bridge's `tale_signal` case; the parity map lists `tale_signal` (same for both).
   The direction says LAST only when every way on closes (no conditional `next`). Neither the hold nor an `on_signal`
   applies on the arc's very last reply (the farewell), and the cap holds on a jump (a refusal repeated every exchange
   would otherwise keep the arc open); a jump to the beat already playing changes nothing. After a hold, the next reply
   is told only that the part runs one reply longer (the reply that answered the turn is already written). The Rhagaea night plays the whole shape; the refusal exit beat is NOT in
   it (still awaiting Gabriel).
3. **The editor's arc cards** and the arc shown in the story map's summary panel (the JSON checks ship with 1 and 2,
   since the editor must refuse what the game refuses).

Not in scope: a hard close that cuts the scene mid-sentence; arcs on stages other than `talk_to`; several characters
taking turns inside one arc (the captive director's collective acts).

## WISHED 2026-10-27 (DepaHN, Nexus 27/10, Gabriel: "met tout ca dans le roadmap"), ROADMAPPED, NOT DESIGNED: friends at war, calls for help, legends

DepaHN's second post, three ideas in his order of importance, traced 27/10/2026 against what exists. Proposed order:
the raid lock and the calls for help after 2.7.21, each designed in prose with Gabriel first; legends after them; the
model's review of war and peace parked.

- **1a. BUILT 01/11/2026 (2.8.2), not play-verified; see the CHANGELOG. A draw at each war, not a lock (Gabriel: "un ami PEUT ne pas toucher le village"). The closest friend tells the player by letter, once per war. Left out: sieges (ruled).** A lord's stance toward the player when a war breaks out, and the raid lock. DepaHN: "when a war breaks out
  and a lord has a very high relationship with me, the mod could forbid him from raiding or burning my villages [...]
  the LLM could decide each lord's stance once, when the war starts"; "a close friend was the first to burn my
  villages". Nothing exists: no lord stance, no raid or party-AI patch (CR's Harmony patches are UI, captivity,
  tournament, ambush, hideout and console only). Proposal: at war start each lord settles ONCE how he stands toward
  the player (from his regard, `NpcProfile.ReputationWithPlayer`, kinship and traits), a pure Logic policy, no model
  call; a rule then keeps a friend from raiding or burning the player's villages (and perhaps from leading the first
  siege of the player's fiefs). Cheap, deterministic, its own self-test (cr.test). Open: what a friend does instead
  (raids elsewhere, stays home, warns the player by letter); whether a stance changes during the war; the realm's own
  stake (a ruler who orders the raid anyway). Needs an engine spike on the party AI's raid choice (decompile 1.4.8,
  never guess). DepaHN's follow-up (27/10): kin and in-laws too, not only friends ("I've had a son and a son-in-law
  fight me despite very strong relations. They would only act against me when there is open hostility or a clear
  reason"): the stance reads kinship (`PlayerKinshipPolicy`, 27/10) as well as regard, and a kinsman turns on the
  player only for a reason the game can name (an open grudge, a betrayal, a killing).
- **1b. The model reviews war and peace decisions (PARKED).** DepaHN: before a war declaration, a war vote or a peace
  deal goes through, the model reads the context and returns it with an in-world reason if it makes no sense; if the
  model is unavailable, it passes. Not done: the AI takes these decisions constantly across Calradia, off screen, so it
  is a cost and a delay on every one, and a change to the base game's balance; and Gabriel's ruling holds (02/07/2026,
  [war = leader-only]): war is the ruler's decision alone, after a real strategic evaluation, never a model's veto. If
  ever, only on the votes of the player's own realm, which the player sees. The "war and peace tied to relations"
  point of the 26/10 entry below is the same wish. DepaHN's follow-up (27/10): an MCM option, off by default, for
  players who accept the cost; or narrowed to decisions that involve the player, their family and allies. That answers
  the cost; the leader-only ruling is Gabriel's to weigh (a model that can return a ruler's decision is a veto, even
  opt-in).
- **2. Calls for help by letter. BUILT 01/11/2026 (2.8.2), not play-verified; see the CHANGELOG.** Left out: a spouse in the
  player's clan writing for their birth house, sending troops without going, and food for a besieged place. DepaHN: "Friends, relatives, and allies could write to the player when they need
  help: military (joining a war, sending troops), financial, or economic, depending on their situation, what the
  player has, and the relationship." What exists: the ruler's war duty (`WarDuty/WarDutyBehavior`: a summons, a levy
  in coin, troops, horses and arms), a spouse's `LetterReason.ChildSupportRequest`, goods asked in tales
  (`SupplyGiftPolicy`); realm tidings letters carry news, never a request. Absent: a friend, kin or ally asking for
  troops, a war ally or coin. The best of the three: built on the letters, regard and tidings that exist, and it gives
  relationships consequences (a brother under siege whose letter goes unanswered remembers it). To design: who writes
  and when (besieged, ruined, at war, a battle lost), what is asked against what the player has, how the player answers
  (a letter reply, troops sent, coin), what answering or ignoring does to regard and memory, and how often (never a
  flood of pleas).
- **3. BUILT 01/11/2026 (2.8.2), not play-verified; see the CHANGELOG. Left out: gameplay effects beyond renown.** Legends. DepaHN: "When something extraordinary happens, such as a heroic last stand or a siege won against
  the odds, it could become a legend that characters retell, differently depending on their connection to the event.
  This could apply to the player too. Optionally, legends could have gameplay effects." What exists, as raw material:
  feats rated and remembered by those present (`Battles/BattleFeatReporter`, `CombatFeat`, `CombatProwessPolicy`),
  the chronicle and the rumours that spread it (`WorldEventStore`, `Commoners/RumorGenerator`), tournament memory, the
  player's fame read when a character speaks (`PlayerFamePolicy`, nothing saved). Absent: an extraordinary event
  raised into a named legend, and its retelling by the teller's connection (pride in an ally, rancour in the beaten).
  Fairly small: a threshold on the feat, a chronicle entry, the teller's link given to the model. Gameplay effects
  (renown, morale) later, if at all.

## BUILT 2026-11-01 (2.8.2), not play-verified, see the CHANGELOG (Gabriel, on Windmolen's report): captivity scenes other than abuse

Windmolen (Nexus, 26/10/2026): "My captor always seems to want to play some perverse game with me, trying to break my
will [...] The NPC's never seem to be capable of just doing it! Give in, or just execute you or let you free or ransom
you." Gabriel the same day: the player can leave a captivity scene at any time, and "il faudra d'autres scenarios que
de la maltraitance". Traced 26/10/2026: every captor intent the scene director draws is a variant of mistreatment
(interrogation, desire, domination, torture, training, reward, reckoning; the bandits' extortion, intimidation,
revenge), the scene always ends with the prisoner back in confinement (`PromptBuilder` ~7054-7073), and the captor is
never taught a ransom though the host already computes `CaptorAllowsRansomRelease`. A design has to answer:
- **Which other captivities.** A courteous captor who treats a noble prisoner as a guest of rank; a captor who
  negotiates a ransom with the player's house; one who wants information and trades it; one who offers service or an
  oath for freedom; one indifferent, who simply keeps the player until the ransom comes; a captor who is a friend of
  old, torn.
- **How they end.** A scene that may END in freedom, a struck ransom, an oath, an exchange, or execution where the MCM
  allows it, instead of always back to the cell; which of those the captor is taught, from his nature and his grudge.
- **Who draws which.** The captor's traits, his regard and grudge, the player's rank and house, the content level
  (these are not Hardcore-only).

## BUILT 2026-11-01 (2.8.2), not play-verified, see the CHANGELOG (Gabriel's "go"): extortion by the player, on a hold the game verifies

Traced 01/11/2026: a threat of violence is already `rob_npc`, and the player never holds a third party's letter (a recovered courier letter is their own), so the lever on courier secrets below does not exist. Levers built: a known hidden affair, and lands in reach of a stronger host. `expose_secret`, a named scandal the spouse learns, BUILT the same day on Gabriel's go, see the CHANGELOG. Left out: a divorce on the spot. Hidden affairs between lords BUILT the same day on Gabriel's go, see the CHANGELOG, with a companion's spying as the one road to them; rumours as a second road are left for later.


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

## BUILT 2026-11-01 (2.8.2), not play-verified, see the CHANGELOG (Gabriel, 23/10/2026): villages raided and towns devastated, as misdeeds. Left out: Show Mercy as a kindness, which has no home yet

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
`AimResult.ReservedSource` for a mod using CR's own source name; cr.test `aims_door`). Conversation only; letter and
audience channels for another mod's aims still to come (the `Channels` field already takes them). **Audience and letter
BUILT 28/10/2026 for 2.7.22** (Gabriel: "fait 2 et 1"): `InterceptionTrigger.StandingAim` (least urgent, fief and road),
`LetterReason.StandingAim` through the mailbox; cr.test `aims_channels`; not play-verified. Next, Gabriel's call: aims CR
seeds itself from its own data.

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
  speak" below the existing ones in FiefAudience/RoadInterception, marked when granted. Each with a self-test
  (register -> the prompt carries the want -> the leave holds on a later reply -> retract).

**BUILT 15/10/2026, INTERNAL (Gabriel: the vassal war duty "passe par la nouvelle architecture"):** glue step (1),
`Aims/AimsBehavior` (save key `cr_standing_aims`, leave decided at `CrConversation.Started`, closed at `Ended`, the
four `OwnAim*` context fields from `EncounterContextBuilder`, daily expiry, an internal `Raised` event). Nothing is
public yet: the `CrAims` door above stays PROPOSED until Gabriel and tashmetu have seen it. Aims seeded by CR itself
(source `CalradiaRemembers`) are not voiced by `own_aim`: their mechanic has its own section (the war duty's terms).
cr.test `aims_conversation`. Letter and audience channels: next, with the war duty's summons.

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

**CR-seeded strategist aims, increment 1 BUILT 30/10/2026 for 2.7.22, NOT play-verified.** Gabriel (29/10/2026): a
lord's ambition is whatever makes his house or realm stronger, plotting against a rival or a realm grown too strong,
arrangements with weaker realms against a stronger one. His answers (30/10/2026): CR may file the game's own kingdom
decisions on its own; the lords may plot against the player's house once it dominates; start with these two; no
encyclopedia line (rumour, warning and conversation only). Built offscreen, MCM switch off by default:
`RealmCounterweightPolicy` + `LordStrategyBehavior` (war, peace, alliance, call to arms against a dominant realm; the
game 1.4.8 HAS alliances), and `AutonomousSchemePolicy.IsOvershadowingTarget` (a rival house twice as strong, own realm
or an enemy one, one such plot per target). Defaults taken while Gabriel's three questions stood open: the player's own
realm is left out; a ruling player is not plotted against; one step a month per REALM (proposed: per house).
Increment 2 BUILT 30/10/2026: the lord asks the PLAYER for help (`StrategistAimPolicy`, `Aims/StrategistAims`):
attack_lord on his rival grown too strong (in a realm at war with his), else attack_faction on the dominant realm,
under the source tag `CalradiaRemembers.Strategy` (voiced; the war duty evicts it), its deed put at the head of his
grounded quest menu (`ViableQuestScanner.LeadingEntries`), so the asked task is the one the factory issues and the
tracker verifies. Three aims at once, 40 days. Raid and siege asks added the same day (Gabriel: "go pour le pillage
et le siège"; raid_village / siege on the target's nearest village or stronghold, a siege already under way first,
a merciless lord asks the raid). Not done: scouting asks; a letter channel (a letter cannot issue a task); the quest
is still issued without an accept step, as every chat-issued task is.

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

## BUILT 2026-09-15/16, NOT RELEASED: the prompt cache, Compact, and the knowledge audit

Three days of work that share one shape: **a rule was written down, and nothing asserted it.** Every item
below was found either by a player reading prompts from the outside, or by an audit reading the code, never
by the suite, which was green throughout.

### What players found

- **fkasad** diffed two successive prompts of one conversation and reported the cacheable prefix dying at
  2.5%. He was right. `IsConversationOpening`, a fact about THIS TURN, decided wording near the top of the
  cached prefix, so every conversation paid two full prefix writes instead of one. Narration FREQUENCY moved
  whole into the dynamic tail; `PromptCacheSplit` holds the rule so tests measure the function the game runs.
  **Play-verified: cached went 0 to 12,864 on turn 2.**
- **The interpreter prompt was built to be cached and never was.** 70,378 chars (~17.6k tokens) of invariant
  head, re-sent at full price once per turn on top of the prose call, the larger half of what a turn costs.
  One line. **Confirmed in play at 96.6% cache reuse.**
- **DuskSymphony** cannot hold a conversation on an 8,192-token local model. Three things in a turn had no
  notion of a prompt level at all: the interpreter (70,378 to 25,353), the discovered-traits list (the only
  unbounded block in the prompt), and the language rule's worked examples. The transcript fold threshold
  ignored Compact too, which is why his FIRST message fit and his followups did not. And the reply budget
  was 3,000 tokens on an 8k window, the largest saving of the lot, and it cost nobody a sentence.
- **basildingleberry**, "many of them seem to talk the same way": every prompt shipped all eight cultures as
  an if-then list to a character whose culture the mod knows exactly. ~2,144 chars, and the model was
  averaging eight personality prescriptions.

### What the audit found (2026-09-15), and what became of each item

| # | finding | outcome |
|---|---|---|
| 1 | compression overwrote an in-place enrichment | fixed, anchors carry an identity, the write-back re-reads by it |
| 2 | two conversations in one day collided on one anchor | fixed, the same identity; a misattributed memory, not just a lost one |
| 3 | `Events.Clear()` + `AddRange` off the main thread | fixed, one reference store (`ReplaceEvents`) |
| 4 | `realm_news` / `underworld` ignored `IsCutOff` | fixed, and the RULE is now asserted over all four reach packs at once |
| 5 | house_means: a dropped army clause, a dangling "and" | fixed, both were invisible to their own guards |
| 6 | "two depth authorities" in seat_standing | **not a defect**, a documented two-level split. The TEST was pinning an unread number |
| 7 | day-scale volatility in the cacheable prefix | **open, and deliberately**, see below |
| 8 | identity frozen (name) or refreshed too late (spouse) | fixed, both now refresh at conversation open |
| 9 | the council renders no packs; the sweep exempts station and prisoners | fixed, three increments, plus both exemptions closed |
| 10 | the Compact budget guard was blind to memory | fixed, it now builds what the host builds |

### The council was worse than the audit said

A `CouncilMemberInput` was a name, a sex, an age, one archetype word, a culture and a regard number. No
memory, no relationships, **no station**, so a seated governor did not know he governed and a seated king
did not know he ruled. That is the Derthert/Garios failure, still live in a whole conversation mode, and it
is where "they all sound alike" bites hardest: culture and archetype is the recipe for interchangeable
voices.

Three increments, each sharing ONE reader with the private-word path so the table cannot drift from the
tete-a-tete: **station** (`StationPack.DescribeBriefly`), **memory** (bounded at two summaries, through the
witness builder so the private-memory filter is inherited), and **knowledge** (`NpcKnowledgeFactory.Compose`,
moved out of `PromptBuilder` where it was private, which is precisely why the council never had it).

### The type that ended a class of leak

Gabriel asked whether the Command pattern would guard the private-memory rule. It would not; a TYPE does.
`SharedRecall` has no public constructor, so a field of that type cannot be handed raw event text and the
leak **stops compiling**. Five places treated a recall as a string; the compiler listed them. Had it existed
a day earlier, the council wiring could not have been written the unsafe way, which was the actual near
miss that prompted it.

### Open, and why

- **#7, day-scale volatility.** `AppendHistory` renders "yesterday / 6 days ago" above the cache marker, so a
  new game day invalidates the prefix. Real, and ONE extra prefix write per NPC per day, an order of
  magnitude below what was fixed. Its cure moves prose, and `RecencySuffix` exists because the model is bad
  at subtracting day numbers. **Behind Gabriel's quality ruling, not forgotten.**
- **The anti-confabulation wall** (`STAY WITHIN WHAT YOU KNOW` and its two siblings) stays in the dynamic
  tail at ~1,840 tokens a turn. It sits last for RECENCY, deliberately, because players reported characters
  inventing. **Measured, offered, and refused, a priced trade, not an oversight.**
- **Unbounded rich summaries.** Memory events are capped at six in Compact; their LENGTH is not. Capping is a
  quality decision.
- **Two audit minors rejected with reasons** (the "and N others" counters, market_word's truncation): both
  rest on a rule that protects STANDING, and a market is not standing.

### What still needs Gabriel

- **`cr.test all` in game.** Two new live assertions (every ruler the engine knows is told he rules; every
  governor likewise) and prisoners now in the sweep, neither can be verified from here.
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
289ffcb, e6f6323 and the letter increment). cr.test: mood_of_the_day, self_esteem. Not built: the council does not
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

### Ruling 1, mood COLOURS delivery; it never decides an outcome

The hard boundary, and the whole safety of the feature. This project's own drop-order principle is that *what
changes what a character may SAY OR DO outranks what colours how he says it*. Mood is the second kind. A mood
must never make a lord refuse an oath he would otherwise swear: that is an invisible gate the player can
neither see nor learn, and the roadmap already names that failure in the companion-acts entry ("unlearnable
refusals"). Gates stay with Stance, regard and the verb policies. **If a mood ever changes an outcome, it has
become a bug.**

### Ruling 2, a CONSTRAINED draw, not a random roll

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

### Ruling 3, the seed makes or breaks it

Stable within a conversation (re-rolling per turn produces a manic character) and different between
conversations (or it is just another static trait). Seed on hero id + game day: **talking to him twice in one
afternoon finds the same man**, which is right - a person's day does not reset - and tomorrow is another day.

### Ruling 4 (Gabriel, 2026-09-14), the PLAYER moves it during the chat

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
  transfer. `cr.test take_into_service` drives the real bridge and asserts the hero is NOT left a fugitive,
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

### The seam already exists, do not invent one

`EncounterContext` is pure data with no engine types. That is why 1301 SDK tests run with Bannerlord not
installed, and it is exactly the stub point Gabriel's (b) needs. Nothing about the seam changes. What changes is
that the flat bag becomes a COMPOSITION whose rule is itself pure and testable.

### Shape: declarative packs, a factory that composes them, three readers

**Gabriel's correction, and it fixes a real defect in the first sketch: this is a FACTORY, not a catalogue.** A
hand-kept inventory rots, `cr.help` described 70 commands of 259 for exactly that reason, and the cure was not
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
composition rule would have been right. What was missing is that **the host never filled the field**, while the
engine knew it and the mod read it ten lines away to gate an action. A unit test builds its own context: it
would have stubbed `GovernedSettlementName` and passed green, and the player would still have had an amnesiac
governor.

So the design needs two guards of different kinds:

- **The composition rule, pure, unit-tested.** A rule about the world ("a lord knows his house's finances"),
  which is what makes the assertion worth writing.
- **Completeness, live.** "Every hero who actually governs something arrives with his post populated." Only a
  real campaign can answer that, and the instrument and precedent already exist: `cr.test unavailable_deeds`
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
PROSE, "you do not see the player's purse", which is a plea to the model. If a merchant simply does not
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

The house's standing, fiefs held, wars under way, which is exactly the remainder of fkasad's request
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
it - the runtime factory, the unit tests, and the live sweep `cr.test knowledge_composition`.

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
- `cr.test world_claims` drives the real injector against the real store and asserts both refusals.

**MCM: "Characters can add news to the world", Content group, OFF by default** (Gabriel, 2026-09-12). Checked
FIRST in the harvest, so with the switch off the extraction call is never made at all - the feature costs
nothing rather than costing a request and discarding the answer. The facade falls back to FALSE rather than to
the usual "on" default: a settings screen that has not loaded yet must not be read as consent for something
that writes to the save. `cr.test world_claims` drives the injector directly and REPORTS the toggle state,
so a PASS is never mistaken for "it is running".

**Also open:** nobody has watched a claim make the journey in a real game - invented in one conversation,
adopted, then heard from a second character.

**A guard that should have existed since v2.5.6.** Adding the twentieth MCM hint made it obvious that nothing
protected the nineteen: that release repaired nineteen hints running off the screen, by hand, and because they
read "On (default): ... Off: ..." the clipped half was always the one explaining the other position. New
`cr.test setting_hints_fit` reads the ATTRIBUTES (so it holds whether or not MCM has loaded) and asserts
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

`cr.test seasonal_chronicle` drives the real behaviour against the real store with a deliberately perturbed
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
costs. `cr.test player_violence`. **Deliberately NOT done:** being seized or arrested on a failed attempt, since
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
reported) + mod wiring at the letter-received and PlayerLetterReply sites + a `letter_memory` cr.test all self-test.

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
  cr.test all self-test via a DebugEvaluate seam; a cr.teach_info console command.

Fits the mission: a spoken offer shows up on the character sheet, verified by a real engine grant, bounded so it
cannot be abused. Related: the anti-empty-promise rule (STAY WITHIN WHAT YOU KNOW), join_party's re-validation
pattern, and the Commitment/errand machinery for the "future lesson" half.

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

## DESIGN RATIFIED (Gabriel 2026-07-17): the COUNCIL as NEGOTIATION, consigned resolutions

Born from the Round Table audit (`AUDIT-ROUND-TABLE.md`, root workspace): the lap's central defect (C1) is that
council speech is consequence-free while the prompt teaches every action. Two answers were rejected, apply
everything immediately (fictionally absurd: a lord does not have his troops in the room; mechanically
dangerous) and apply nothing (theatre: the council teaches the player that words do not bind). The ratified
third way: **a council is a NEGOTIATION. Free speech stays free of immediate effect; exactly one structure
binds, the `[PLEDGE]`, which is CONSIGNED, not executed. Execution is deferred to the coming days and
verified against real campaign events. The other participants are recorded as WITNESSES, which is what
makes a breach socially expensive**, the unique value of a council over 1v1: an engagement taken in front
of witnesses costs more to break.

This is NOT a new mechanism: it is the existing `CommitmentBehavior` pattern (consign -> deferred
execution -> verification -> consequence) applied to a new producer, with an execution layer for verbs the
vanilla AI will never perform on its own.

### Locked design decisions (Gabriel 2026-07-17)
1. **Resolutions are bounded to the verifiable** (the mod's founding rule: magnitude is bounded by what the
   game can verify). `send_gold`, `send_troops`, `release_prisoner`, `move_against` are verifiable by real
   campaign events. `change_relation`, "support my claim" are not, excluded from the pledge vocabulary,
   else we recreate hollow promises. The v1 list is 4 verbs; each ships with its own event verifier.
2. **Individual pledges, no group consensus.** A "council consensus" is mechanically fragile (who verifies?
   who is punished on breach?). Each NPC emits his OWN pledge (actions stay per-speaker); the other
   participants are recorded as WITNESSES of the engagement (a heroId list on the resolution).
3. **Player consent stays explicit.** Anything costing the player (gold, troops, his own sword) passes the
   existing `ActionConfirmationController` Y/n gate. Negotiation is text; the engagement seals code-side.
4. **Guards re-validated at deferred execution, not only at pledge time.** A lord may pledge in council then
   be captured, die, or change faction before the deadline. The bridge re-checks eligibility when the
   execution falls due ("the bridge is law", applied over time), with a clean narrative outcome when
   execution has become impossible ("Derthert cannot honor his word, he is in chains").
5. **One simple block, taught only in council turns.** `[PLEDGE] type / target / amount / days [/PLEDGE]`,
   4-5 fields, tolerant parsing (the parser hardening in progress applies directly). Taught ONLY when
   `IsRoundTableTurn`, a 1v1 conversation cannot produce a council resolution (the existing
   `pledge_against` action keeps its own path; it predates and is orthogonal).
6. **Two classes of council outcomes.** Engagements (verifiable, deadline, breach) via the new mechanism;
   sentiments (the debate genuinely moved regard) via the ordinary gated `change_relation`. Only class one
   is new.

### The v1 resolution verbs (execution + verification)
| Verb | Deferred execution | Verified by |
|---|---|---|
| `send_gold` | a real courier party carries the gold (CourierService, visible, waylayable; the amount is escrowed from the promisor's stock) | courier arrival (existing delivery path) |
| `send_troops` | a small party of the promisor's culture spawns and travels to join the player (courier pattern) | party arrival / merge |
| `release_prisoner` | at the deadline, if the named captive is still held by the promisor's side | `HeroPrisonerReleased` (already listened elsewhere) |
| `move_against` | ALREADY EXISTS, `CommitmentBehavior` (scheme or organic battle) | `OnSchemeResolved` / `OnMapEventEnded` |

(v2, not v1: `marry`/`grant_blessing`, requires both parties present, resolution honored at the next
meeting; `declare_war`, faction leaders only, needs its own political gate.)

### Reuse (not from scratch)
`TrackedCommitment` model (`Logic/TrackedCommitment.cs`: Id/PromisorId/TargetId/Kind/IssuedDay/DeadlineDay/
State Active->Fulfilled|Breached, generalize `CommitmentKind`), the `CommitmentBehavior` engine (daily
breach sweep, fulfillment-by-event, SafeRun per item, byte[] JSON persistence with frozen-snapshot
anti-race + graceful degradation), `CommitmentPolicy` (pure, tested: `DeadlineDays 21`,
`FulfilledRelationBonus +6`, `BreachedRelationPenalty -12`), `CommitmentJournalBridge` (vanilla journal
mirror), `StanceService.OnPromiseKept/OnPromiseBroken` (+15 Trust / -18), `WorldEventStore` (fulfilled/
breached records, the public shame), `CourierService` (visible execution of send_gold/send_troops),
`ActionConfirmationController` (player consent), the bridge's `ExecutePledgeAgainst` re-validation pattern.

### Increment breakdown
- **R0, PREREQUISITE: Round Table audit Phase 0** (`AUDIT-ROUND-TABLE.md`): the 10 low-effort fixes before
  flipping `RoundTableController.Enabled` (witness-hallucination filter, already live in 15C, per-turn
  presence re-check, per-speaker relations, unified framing, confirmation handoff, intent-detector word
  boundaries, kill-switch guard, `cr.roundtable` test command + token logging). Without R0 the council
  engine itself is untrusted; building negotiations on it would stack defects.
- **R1, the engine.** Generalize `CommitmentKind` (SendGold/SendTroops/ReleasePrisoner alongside
  MoveAgainst); add `WitnessIds` to the resolution; `[PLEDGE]` block in the SDK parser + PromptBuilder
  (taught only on `IsRoundTableTurn`); honored in the lap alongside `end_conversation`/`witness_leaves`
  (the ONLY effect a council turn may produce, everything else stays theatre); `send_gold` executor via
  CourierService; `release_prisoner` executor at deadline; event verifiers; breach = existing penalty +
  a WorldEvent naming the witnesses (the social sting).
- **R2, follow-through.** `send_troops` party; "YOUR OUTSTANDING RESOLUTIONS" injected in the promisor's
  prompt (he remembers what he vowed, and the player can call him on it); narrative outcomes when execution
  became impossible (promisor dead/captured/faction-flipped); resolution recall at next meeting.
- **R3, council politics.** `marry`/blessing honored at next meeting; `declare_war` for faction leaders;
  assembly integration (Inc 4's "mission distribution" becomes consigned resolutions taken before
  witnesses, ambassadors pledge in their lord's name).

### Risks (flagged)
- **LLM format is the weak link** (quests audit): keep the block at 4-5 fields, one worked example in the
  prompt, and tolerate the council producing ZERO pledges, a debate with no outcome is a valid outcome.
- **Cost**: a resolution adds no LLM call (execution is out-of-chat), but each pledge later surfaces in the
  promisor's prompt, bounded by "one pledge in flight per promisor" (existing `HasActive` rule).
- **Double counting**: a fulfilled pledge pays its reward ONCE (fulfillment path), never re-credited by the
  ordinary deed feeds, same discipline as the quest/commitment dedup.

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
committed + pushed 2026-06-25; awaiting Nexus upload), still untested in game unless validated since.

**Conversation & memory core**, persistent per-NPC memory across the save; in-character free-text LLM
dialogue woven into vanilla; relation + multi-axis **stance** (trust/respect/fear, "words nudge, deeds
prove"); hearsay about third parties; culture-aware via editable `ModuleData`; story log; encyclopedia
integration (personal regard, discoveries).

**Quests & negotiation**, informal quests verified by real game events (no ghost quests); join a kingdom as
mercenary; recruit a companion (pay in goods); **recruit a lord into your clan** (`join_clan`); the
negotiation framework, **deliver-a-prisoner** + **declare-war** deeds (Phases 1 & 2), and **Phase 3
"intimacy as currency"** (adult-gated, exploiter archetype, persistent leverage) **(untested)**; **talk to a
prisoner, bargain for freedom/rescue** (cases A & B) **(untested)**.

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

**World & influence**, world-event/rumor store (battles, sieges, marriages, executions, wars) with
geo/faction/recency-weighted hearsay; the **Chronicle**; commoners + settlement intel; **NPC↔NPC schemes**;
**NPC-initiated interceptions** (a lord rides out to challenge you); **power-balance** context block; the
**influence loop** (NPCs speak of *your* deeds, attributed + valence-coloured: kin/faction Wound vs Boon)
and the **"talk of the realm"** line.

**Chronicle enrichment** *(built 2026-07-04, untested in game)*, the player Chronicle grown from a war
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
,  the begetting beat, distinct from the childbirth entry `OnGivenBirth` already records so the two never duplicate).

**Stance with teeth** *(increment 1 built 2026-06-27, untested in game)*, the earned multi-axis posture now
ACTS, not just colours dialogue. Pure `StanceConsequencePolicy` maps a posture+traits to one warranted
consequence (ColdShoulder → Threat → Plot → Intercept → Assassinate / WarmWelcome → Favor → Warning), with
honour staying a lord's hand from murder/scheming and terror ruling out striking in person. **1a (conversation):**
`EncounterContext.StanceConsequenceHint` colours how the NPC ACTS this meeting (a cold-shoulder won't deal, a
secret killer stays falsely civil, a friend offers aid/would warn you). **1b (proactive):** `StanceConsequenceBehavior`
weekly turns OPEN enmity/regard into an unbidden letter (Threat/Intercept → cold/challenge; Favor/Warning → warm
overture); secret hatred (Plot/Assassinate) writes nothing. Self-tests `stance_letter` + chat `chat_stance_assassin`/
`chat_stance_favor`. OPEN: **increment 2** = wire Plot (scheme vs player) + Intercept (reuse InterceptionBehavior) +
Warning (warn of a real scheme). **increment 3, the town ambush, WORKS in-game, still OFF behind the experimental
MCM toggle `TownAmbushEnabled` until polished:** Assassinate arms `NpcProfile.StanceAmbushArmedDay`; on entering a
town `TownAmbushBehavior` rolls `AmbushPolicy` → narrated inquiry (Draw / Slip away) → a fight at the gate. Console
`cr.ambush`. **2026-06-30 refinements (BUILT, NOT committed):** an elite chief of the paymaster's culture at the
player's clan tier leads bandits in the Blackened "stealth" set; force scales (N+1)² with the defending side (solo 4
/ two 9 / three 16 / four 25, no cap); companions are captured alongside the player; capture is armed even when the
player falls first; recurring captive scenes are dungeon/fortress-framed when held in a town/castle.
**The captivity arc (Gabriel 2026-06-27), the lose branch:** 3.2a lose → captured + convoyed to the commissioning
lord ✅; 3.2b arrival → the lord's reckoning audience ✅; 3.2c convoy **rescue ambush** by your companions (BUILT
2026-06-28); 3.2d **Hardcore companion-victim captivity scenes**, the captor torments/assaults a held companion
before the player, or coerces the player to do it (revenge driver). **Phase 2 BUILT 2026-06-30; Phase 3 ✅ SHIPPED
v1.26.0 (2026-07-07): auto-fire in CaptiveEncounterService (CompanionScenePolicy gate, 45%, variant pick) + the full
aftermath on scene close (pure `CompanionAbuseConsequencePolicy`): grief stance-crater on the companion's bond
(Trust/Affection, deeper when the player was coerced), a REVENGE grudge vs a NAMED captor (new append-only
`GrudgeSource.CaptorCruelty`, resolution=Deed, coexists with the vs-player CaptiveAbuse grudge), and the atrocity
chronicled as a world-event. `companion_revenge` self-test.** Reuses CR's captive-scene chat.
**In-game-test feedback round (Gabriel 2026-06-30, BUILT, NOT committed):** (a) the chat "scroll down to read more"
hint no longer flashes up the instant a scene's opening narration runs a line long, it appears only when a
substantial block of text sits below the fold (`MoreHintRevealMargin`). (b/c) **the captive companion is now a REAL
participant, not a name in the prompt:** threaded as a real Hero (a witness flagged `IsCaptiveVictim`), so they get
their own portrait, their own VOICE (cries/pleas/defiance via the existing `[WITNESS_REACTION]` channel, now also
collected on the Continue path, which previously left them mute mid-scene), a dedicated colour, and the CORRECT
gender in the prompt (`CaptiveVictimIsFemale` → pronoun rules; fixes the model borrowing the player's sex). The
victim is excluded from the captor's-men / humiliation tallies (`CountAggressorWitnesses`). (d) **Continue-sequence
quality:** the in-scene Continue stimulus + the "Please continue" resume text now push ADVANCE + fresh vivid
description + NO verbatim reuse (the repetition/flat faults Gabriel saw). The continuation stimulus is extracted to
`CaptiveSceneText.BuildContinuationStimulus` so the new self-tests exercise the exact runtime prompt. New LLM
self-tests `chat_captive_continue` (opener + 4 Continue beats → asserts every beat replied + NO 6-word verbatim
phrase reused across beats, logs description coverage for human judgement) and `chat_companion_continue` (same, plus
asserts the companion is actually VOICED in its own `[WITNESS_REACTION]` lines + a gender-correctness judge note).
**OPEN:** richer per-victim captivity memory at scene close (today the victim gets none, a Phase-2 revenge hook);
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

**Companions (the 2026-06-23 trilogy, untested)**, **missions** (news / spy / steal / barter / envoy, a
real physical journey: the companion leaves as a clan party, rides to a town, returns and reports in
conversation; save-safe + capture-resilient + anti-exploit; **recall**, order an away companion to abandon
the errand and come home, by conversation *or* by letter, as a verified deed, not a hollow agreement);
**happiness & retention** (an encyclopedia
"Contentment" gauge, errand/idle drivers, an in-conversation grievance, reassurance, and departure when too
long unhappy); **camp life** (companion↔companion rapport from trait compatibility, voiced opinions, and
companion weddings that keep the couple in your clan).

**Dynasty & adult**, **dynastic succession** (the heir inherits standing/debts/grudges, not the private
history; deterministic); marriage politics (family-approval, consorts); **jealousy**; captor/captive scenes
(Hardcore); **player-authored character data** (`character_overrides.json`).

**Tech**, multi-provider LLM incl. local (OpenAI-compatible endpoint covers Ollama/LM Studio/vLLM);
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

## Affect coherence reform: SHIPPED v1.22.0 (canonical RegardBands, semantic writer routing, grudge tone arbiter, Bearing line). Full design in CHANGELOG + git.

## Appreciations, event-anchored kindnesses: COMPLETE, shipped v1.22.0 (the positive mirror of Grudges; every kindness source symmetric to a grudge source). Full design in CHANGELOG + git.

## Grudges, resolvable event-anchored grievances: SHIPPED v1.22.0, Amends/Gift/Deed reconciliation complete, additive save migration. Full design in CHANGELOG + git.

## Scripted scenarios, a modder-extensible world-reaction engine *(wished 2026-06-24, a new pillar)*

**Reactive rules, NOT authored storylines.** A branching narrative (Twine-style) fights the sandbox; CR's
thesis is the opposite, the world reacts, then the *player and the game engine* write what comes next. So a
scenario is a **stateless reactive rule**: *when this happens → the world shifts like so → and here is how it
reads*. No passages, no branches, no authored "next beat". (Twine was considered and dropped for exactly this
reason, see also the captive scenario bank.)

A rule has **four fields** (plus two optional):

| field | role |
|---|---|
| **Trigger** | the in-game event that arms the rule |
| **Guards** *(optional)* | eligibility predicates, scope which rules are even applicable now |
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
`CHANGE_RELATION:Hero:+N`, `INJECT_MEMORY`, `SEND_LETTER`…), guards AND-combined, no arbitrary expression
language, so the engine evaluator and the authoring stay trivial. The keyword vocabulary IS the modder-facing
API; it serializes to JSON in `ModuleData`. On top of it, a small standalone **script-composer app** lets an
author build a rule by *picking predefined keywords from dropdowns* (no hand-written JSON, validated by
construction) and exports the file. Pure/testable in `CalradiaRemembers.Logic`: the trigger-matcher, the
guard evaluator, the random-pool selection, and fire-once bookkeeping; only the CampaignEvent hooks and the
consequence executors are engine glue. Define the vocabulary first, the app is downstream sugar.

> **BUILT 2026-06-24 (Opus, 412 tests green; committed `6b47a4d` + pushed 2026-06-25 in v1.14.0), pure core + a working engine-glue slice.**
> Pure (`CalradiaRemembers.Logic`, +34 tests): `KeywordToken` (VERB:payload language), `ScenarioRule` (four
> fields + fire-once/weight + `Parse` factory), `GuardEvaluator` (AND-combined, injected resolver),
> `ScenarioSelector` (trigger match incl. `*` wildcard, one-shot exclusion, guard filter, weighted random
> pool, RNG injected), `ScenarioArgs` (target:delta payload parse), incl. the Rhagaea marriage as four
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
>, the "Calradia remembers" channel), with `ScenarioArgs.TryParseTargetRest` (+3 tests) and both mirrored
> into the app `VOCAB`. Added a **reentrancy guard** (`ScenarioRuntime._isFiring`): a consequence's own
> campaign event cannot cascade back into the engine. Shipped the **Rhagaea-marriage A/B/C/D content pack**
> (`ModuleData/scenarios/rhagaea_marriage.json`, deployed): four rules on `ON_MARRIAGE` (guards
> `IF_PLAYER_INVOLVED` + `IF_HERO_IS:Rhagaea`; B adds `IF_PLAYER_RULES`), one drawn at random; each keeps her
> in character and surfaces the union's nature through her OWN memory (consort / two-crowns / regent /
> suspicious). Test: `cr.scenario_fire ON_MARRIAGE Rhagaea`.
>
> **Still to grow:** more vocabulary (`GIVE_FIEF`, `SEND_LETTER`, …), mirror each into the app `VOCAB`; a
> richer marriage outcome than memory-only (a dedicated prompt-flavor channel / claim grant); the visual
> composer could gain hero/faction autocompletion from a game export.

This is what makes CR **world-agnostic**: a total-conversion drops in its own rules (its lords, its triggers,
its consequences) as a content pack; Calradia's hand-authored rules are just the first pack.

**Flagship content, the Rhagaea marriage** (the case that birthed the idea). Vanilla marrying a *ruler* is
cold and out-of-character: she folds into your clan and a random AI clan inherits her throne. CR replaces it
with **four rules on the `ON_MARRIAGE:Rhagaea+Player` trigger, one drawn at random and sealed**, each keeping
Rhagaea true to herself (the empress-stateswoman never abdicates *for* you). Guards scope the pool; the result
is surfaced through her *voice* (prompt context), not a popup:
- **A, Prince-consort of the South**: *you* join *her*; she keeps the throne; you become her sword and
  influence multiplier. The "become emperor" fantasy is denied, correctly.
- **B, The pact of two crowns** *(guard: the player already rules)*: an alliance of two thrones, not a
  merger; the real tension is whose heir inherits what (her daughter Ira vs your line).
- **C, The regent's gambit**: she names you war-leader and keeps the crown; you *can* reach the throne, but
  only by winning the succession (with her blessing earned) or on her death, never by her surrender.
- **D, The suspicious empress** *(a tension layer)*: the marriage is conditional on loyalty to her
  restoration; CR tracks every move, and maneuvering for the throne behind her back turns her, grudge, war,
  or she leaves you.

Because she keeps her throne in A/B/C, the vanilla "empire destabilizes" feel-bad disappears, any crown
change becomes *earned and dramatic*, never a mechanical accident. Other rulers fall back to a generic consort
rule until they get their own pack. (Replayability is the explicit goal: marrying the empress should never
play out the same twice.) A second worked example, `ON_DEATH:Rhagaea` → consequence
`DECLARE_WAR: Northern Empire, Western Empire -> Southern Empire`, message "With the empress fallen, the
eagles turn on a leaderless South", then the sandbox takes over.

## Companion war-weariness & honorable retirement *(wished 2026-06-30, Gabriel, design only, nothing built)*

**The idea:** a companion who fights at your side for years accumulates the TOLL of war, and a spent veteran
eventually wants out. This is a **SEPARATE axis from happiness** (`CompanionHappiness`), and keeping them
separate is the whole point:

| | Happiness / contentment *(shipped)* | War-weariness *(this)* |
|---|---|---|
| Nature | mood, **reversible** (drifts back to baseline) | accumulated combat toll, **climbs** with blood spilled |
| Cause | pay, idleness, errands, treatment | verified battle events, wounds, captures |
| End state | **bitter** departure ("I quit") | **dignified** retirement ("I have done my part") |

A veteran can be **devoted AND worn out**, folding weariness into happiness would wrongly read them as
"disgruntled." So this is its own gauge, ending in an honorable exit, not a resentful one.

**Drivers, verified deeds, never words** (the mod's core discipline): the score rises on real game events:
**wounded in battle ≈ +1, taken prisoner ≈ +10** (tunable). **Valor / hardiness slows accrual** (a brave soul
wears slower; a gentle one faster). NB: a capture hits BOTH axes (happiness `Captured` −12 *and* weariness +10)
,  realistic, and deliberate, not double-counting to remove.

**The fief / governor gate, the strategic heart *(Gabriel's key refinement)*:** a companion who is
**materially invested in the clan does NOT leave**, they hold a fief or **govern a settlement**
(`Hero.GovernorOf != null`, or lead a party), and draw benefit from your campaign, so they stay. Only the
**landless, roleless** companions are ever at risk. In play this asks the player to EXPAND and grant fiefs to
secure their veterans, early game they are at risk (tension), late game securing them rewards success, and
because fiefs are scarce there is always a pool at risk. This gate also **defends against a vanilla pitfall by
construction**: a landed clan member who leaves can spawn/join a rival faction, never allowing the landed to
leave sidesteps that entirely.

**Two differentiated OUTCOMES by landedness** (so weariness always lands narratively, but never destabilizes
the realm):
- **Landless + worn out** → asks to leave your service: a warm farewell, out of the clan (contrast to the
  happiness system's bitter departure). Nothing left to re-add.
- **Landed / governor + worn out** → asks to **step back from the field** and see to their fief: stays in the
  clan as a governor, off the battlefield. A tired veteran becomes a settled administrator.

**Coherence, soft enforcement, NOT a hard lock *(decided 2026-06-30)*:** the re-enrolment hole ("vanilla lets
the player just put the benched governor back in a party") is handled the CR way, the mod owns a persistent
"stepped back from war" flag on `NpcProfile`; a daily tick that finds a flagged retiree back in a party **sends
them home** ("he has seen enough war; he rides back to <town>"), and **forcing a spent veteran back repeatedly
CONVERTS weariness into genuine unhappiness**, the warm retirement curdles into a real departure and they leave
for good. You *can* drag them back (vanilla allows it), but you use them up. A hard Harmony button-block was
**considered and rejected** as invasive, patch-fragile, and un-CR: the world reasserts the fiction instead of
fighting the engine.

**Architecture (matches the pillar):** a pure policy in `CalradiaRemembers.Logic` (thresholds, trait scaling,
outcome-by-landedness, decay), unit-tested; engine glue = campaign-event hooks for hero-wounded +
hero-prisoner-taken (player-clan only), persistence of the score + retired flag on `NpcProfile`, and a
conversation surface that reuses the existing happiness grievance/departure plumbing (a retirement request the
player answers).

**PURE CORE BUILT 2026-06-30 (Opus, +15 tests, green), `CalradiaRemembers.Logic/WarWeariness.cs`:** the score
(0..100) with `ApplyWound` (+1) / `ApplyCapture` (+10), `Rest(step)` recovery, a Valor-scaled `RetireThreshold`
(clamped 16..72), `WantsToRetire`, a `WearinessBand` (Fresh → Seasoned → Weary → Spent) read RELATIVE to the
companion's own tolerance, `Retirement(isLanded)` → `RetirementKind.StepBackToGovern` / `LeaveService`, and a
`Describe` line for prompts/encyclopedia. **Design refinement made while building:** Valor **raises the
retirement TOLERANCE** rather than literally slowing per-event accrual (the +1 wound has no room to scale at
integer resolution), same felt effect (a brave companion retires later), cleaner and fully testable. NEXT =
the engine glue (event hooks + `NpcProfile` persistence + the retirement conversation + the soft-enforcement
tick), still to build.

**Forks resolved 2026-06-30 (Gabriel confirmed), the v1 build:**
- **Decay:** YES, slow recovery at real REST (`Rest`, ~2/week suggested). Not monotonic.
- **Player lever:** NO "one more campaign" persuasion in v1 (the retirement request re-presents if ignored); a
  fast-follow.
- **"Invested" gate:** governor (`Hero.GovernorOf != null`) **OR** leads a party.
- **Wear sources:** v1 = wounds (+1) + captures (+10) only; other sources (witnessing a companion die, long
  campaigns, defeats) deferred.

**AUDIENCE-AS-CONVERSATION BUILT 2026-07-01 (Opus, untested in game), the popup now opens a real chat.** Both
retention popups (the happiness grievance AND the war-weariness retirement) became a two-road choice: **"Let us
speak of it…"** opens a real CR conversation the companion requested, and **"I've no time for this."** turns them
away with a bite (a `Mistreated` happiness hit + the existing escalation/cooldown). The shared seam is
`ChatManager.OpenForCompanionAudience(hero, profile, reason, landed)` → `ChatViewModel.ActivateCompanionAudience`
(a normal conversation, opened programmatically from a map popup, carrying the profile in `_audienceProfile`
since there is no vanilla `OneToOneConversationHero`; the companion speaks FIRST via an NPC-led opener). SDK:
`CompanionAudienceReason` (None/Grievance/Retirement) + `EncounterContext.CompanionAudience` /
`AudienceRetirementIsLanded` + `PromptBuilder.AppendCompanionAudience`, the grievance leans on the existing mood
note + `reassure_companion`; the retirement gets its own surface teaching the **`retire`** action AND the
persuasion framing (**this is where the deferred "one more campaign" lever now lives**, the player can talk a
war-weary veteran into staying, in their own words). `retire` is a chat-flow control (intercepted in the VM, not
a bridge action): it flags the grant and executes at CLOSE via `WarWearinessBehavior.ExecuteRetirement` (never
removes a hero mid-conversation), landless leaves, landed steps back. Console `cr.audience [retire|grievance]
[name]` opens either surface directly. **This supersedes the "player lever: NO in v1" fork, the conversation
naturally IS the lever.**

**ENGINE GLUE BUILT 2026-06-30 (Opus, untested in game), v1 complete + shippable:** `NpcProfile.WarWeariness`
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

## Custom hand-made portraits, letter window, chat window, player *(wished 2026-06-21, broadened 2026-06-24)*

Show a hand-made painted portrait (the "letter from a noble lady" look) for a hero, NPC **or the player**:
instead of the in-game 3D head render, across **CR's own prefabs**. Tiered: a custom image when one exists for
that hero, else fall back to the current `CharacterImageIdentifierVM` render, so only notable heroes need art
and everyone else still works.
- **Surfaces**: (1) the **letter window** correspondent (already scoped below), (2) the **conversation/chat
  window** portrait, (3) the **player's own** portrait. All three are CR prefabs, so no tableau-compositing
  problem.
- **Shared resolver** (one place, reused by all surfaces): lookup `Modules/CalradiaRemembers/Portraits/<heroStringId>.png`,
  with optional fallback tiers and/or a `portraits.json` map. Pure/testable: which key wins, fallback order,
  missing-file handling. The letter window already centralizes this in `LetterViewModel.ResolvePortrait(Hero)`
 , add a `UseCustomPortrait` flag + a custom-image binding, and the prefab shows either a raw image widget or
  the existing `ImageIdentifierWidget`; the chat window mirrors the same.
- **The real fork, how a PNG reaches Gauntlet** (needs a small spike, shared by all surfaces): **(A)**
  pre-baked sprites in the module atlas (clean/fast, but static, modders rebuild the atlas), or **(B)**
  runtime texture load from disk (drop-in folder, no rebuild, but needs the right engine texture-load API +
  lifetime handling). (B) preferred for moddability; verify the call first.
- **Encyclopedia is EXCLUDED**: its page composites live widgets over the 3D tableau, so a custom image there
  needs a full encyclopedia rebuild, that is the parked "generated portraits" separate-mod territory, not
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

## Player action narration, interpret the player's `*…*` and first-person deeds *(wished 2026-06-28, player feedback)*

A recurring roleplay frustration: a player types a physical action, `*I take her by the wrist*`, or first-person
"I grab her arm", and the NPC, never told to read it as narration, treats it as nonsense and pushes back ("what
are you talking about, you crazy fool"). CR teaches the NPC to use `*asterisks*` for ITS OWN actions but never
teaches the model to INTERPRET the *player's* narrated actions. Add a PromptBuilder block defining the player's
`*…*` (and plain first-person physical statements) as in-world action the NPC reacts to in character, distinct
from speech. Pure-ish prompt work, **high value / low effort**. Caveat: a free NPC may still legitimately DEFY
coercion (that is correct behaviour); the bug is the model not understanding the input at all. Pairs with the
player-as-captor scene below for the captivity use-case. (Model choice still dominates for adult refusals.)

**BUILT 2026-06-28 (untested in game).** `PromptBuilder.AppendPlayerActionNarration` (SDK) injected near the end
(high recency, beside the language mirror) of BOTH the main NPC and commoner prompts: defines the player's
`*…*` and plain first-person physical statements as real in-scene actions to ENGAGE with (never gibberish, never
"what are you talking about"), with explicit examples, while keeping the NPC free to welcome/resist/refuse. Test:
chat any NPC and type `*I take you by the wrist*` or "I put my hand on your shoulder".

## Player feedback, time-gap chatter, group-scene recall, romance override, post-history hook *(2026-06-29)*

A player on GLM 5.1 / NanoGPT raised three things (and asked about a donation channel):

- **NPCs harp on the time since the last conversation** ("Four days since we last spoke..."), map travel eats
  days fast, so a short gap should not be remarked. **BUILT 2026-06-29 (untested in game).** Pure tested
  `MeetingGapDescriber` (CalradiaRemembers.Logic) gates it: nothing at or under ~1 week (`RemarkThresholdDays = 7`),
  and a longer gap is phrased on the Calradian calendar ("a season" / "a year") as plain awareness. SDK
  `EncounterContext.MeetingGapNote` replaces the old unconditional "You last saw the player N days ago." line,
  rendered by `PromptBuilder.AppendEncounterContext` with explicit "do not open with it / do not dwell" guidance.
  (+6 tests.) Test: talk to a companion a few days after the last chat, they no longer lead with the count; a
  long absence may be acknowledged once, in passing.

- **A companion does not recognise they took part in a group conversation.** Real gap, not the LLM: at close,
  only the MAIN NPC's profile was written. **BUILT 2026-06-29 (untested in game, first slice).** New
  `ChatViewModel.FinalizeWitnessMemories` (called from `ChatManager.Close`) records a light "Was present while
  {player} spoke with {mainNPC}..." event into each HERO witness's own profile, so a later one-to-one reflects
  the shared scene. First slice records only witnesses the player has already met (existing profile);
  synthetic/bandit witnesses and the main NPC are skipped. OPEN: get-or-create a profile for a stranger witness;
  optionally a richer per-witness summary (deferred, would cost N extra LLM calls per scene).

- **Romance / courtship override per character, independent of traits and culture.** TO DESIGN. Today the
  romantic profile is DERIVED from the five traits (`RomanticProfileBuilder`), so making a character
  "casual/open" forces a low Honor value. Proposed: an optional `romantic` block in `character_overrides.json`
  (orientation, monogamy/openness, devoted/casual, optional kinks) that OVERRIDES the derived `RomanticProfile`
  when present, slots into the existing player-authored-characters system (`Use custom traits`). Needs design:
  which fields are overridable, how it interacts with re-derivation for already-met NPCs, and the JSON shape.

- **A SillyTavern-style post-history / jailbreak hook.** A player asked where to put RP / censorship-bypass
  framing "pre or post history" (SillyTavern terms). CR sent everything in the system prompt (all pre-history),
  with no modder-editable last-word slot. **BUILT 2026-06-29.** New editable `post_history_instructions.txt`
  (ModuleData, ships empty) injected verbatim at the VERY END of the system prompt (after the language mirror,
  highest recency) for BOTH the NPC and commoner prompts, the analog of SillyTavern's post-history field.
  SDK `PromptBuilder.PostHistoryInstructions` + `AppendPostHistoryInstructions`; mod `NarrativeLoader` loads the
  file; `LlmServices` wires it; loads on campaign start / `cr.reload_lore`. +3 tests (presence + ends-with proof,
  NPC and commoner). Documented in the Nexus "Make it your own" section (positioned next to behavior_guidelines).

- **Donations / feedback channel (Ko-fi):** DONE 2026-06-29, Ko-fi page (ko-fi.com/lograam) created by player
  request; a centered "Support the work" section added at the top of the Nexus description.

## Conception from any vaginal act: BUILT, in the live pregnancy system (consensual scenes + female NPCs). Full design in CHANGELOG + git.

## Bastards, a hidden birth as a consequence layer: SHIPPED v1.15.0. Full design in CHANGELOG + git.

## Lean prompt for small / local models: SHIPPED v1.20.0 (Lean v2, about 1490 tokens). Full design in CHANGELOG + git.

## Stance, mechanical behaviour-gating *(all 4 axes 🔨 BUILT 2026-07-04→05, untested in game)*

Stance (Trust/Respect/Fear, plus Affection mirrored from relation) is fully wired into **perception**: deeds
move it, words nudge it (capped + cooled), fear decays, and `StanceDescriptor` feeds the prompt so the LLM
**role-plays** the posture. What's missing is mechanical **teeth**, stance thresholds changing **verified
outcomes**, not just tone:
- a **fearful** captive lord more likely to accept a freedom bargain / yield softer ransom terms;
- a high-**Respect** lord more open to `join_clan` or a war-council nudge;
- a low-**Trust** NPC refusing a deed that a relation score alone would have allowed.
Build on the existing deed bridges (prisoner bargain, `join_clan`, barter); the gate is a pure threshold policy
in `CalradiaRemembers.Logic` (testable), consulted by the bridge before it commits. "Words nudge, deeds prove"
,  and now the posture **bites**.

**RATIFIED 2026-07-04 (Gabriel: "beaucoup de valeur, dans la philosophie de CR"), two structural rulings:**
1. **Gate on the PROVEN level, not on flattery.** "Deeds prove" is a hard rule at the gate. NOTE (verified in
   code 2026-07-04): the ANTI-FLATTERY GUARANTEE ALREADY EXISTS in `StanceGate`, a spoken round caps at
   `WordCap` = ±2 per 8h, a proven deed at `DeedCap` = ±30. Reaching a gate at 40 by words alone would take
   ~20 accepted rounds over ~160 in-game hours (and Fear decays underneath). So the gate reads `stance.Fear`
   DIRECTLY, no separate "proven floor" store, no new SyncData key. The earlier A/B storage question is moot.
   DECAY CAVEAT: unlike Trust/Respect/Affection (ETERNAL, no decay, forgetting reads as a bug), **Fear is the
   one stance axis that fades** (`FearDecayPerDay` = 2, toward 0). So Fear-based coercion leverage is
   inherently TEMPORARY by design: a lord cowed today yields softer terms, but ~20 days of calm erase the scare
   and the leverage expires. This is a feature (terror recedes), not a bug, the fiction should own it.
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
(new value, appended, ordinal 13) via `GrudgeBehavior.TryPlant`, on top of (never instead of) the existing
deed-gesture/appreciation/chronicle channels. A profile-less captive (no stance data, the `cr.test free_prisoner`
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
`SpendInfluenceVerb`/`GiveTroopsVerb` are untouched), at BOTH surfaces: `IsEligible` (advice, not taught below
the gate, so the LLM never promises it) and `Execute` (law, re-reads live Trust/relation, refuses in character
"Prove yourself first"), reading Trust via `StanceService.ToStance` and relation via `profile.ClanRelationWithPlayer`.
**OR-relation soupape (Gabriel ratified 2026-07-04):** an established ally (vanilla relation ≥ 30) still lends
even with 0 CR-tracked Trust, so the gate never silently narrows a shipped feature; an unproven stranger must
still earn Trust (kept quest +15, gift +4, capped word nudges) to cross the floor. Profile-less NPCs were already
excluded from these verbs (`Profile` is a hard prerequisite), so no new blocking for untracked heroes. Trust-only
for now (no trait modulation yet). Console: `cr.stance_set 25 0 0 <name>` seeds Trust to test. **OPEN SEAM:** the
companion risky-errand gate (Spy/Steal) is left commented-out in `CompanionMissionBehavior.Dispatch`, freshly
recruited companions sit at Trust 0 AND relation ~0, so gating would lock an already-shipped feature; needs its
own design (gate only the riskiest missions, or seed companion Trust on recruit) before wiring.

**Respect-1 🔨 BUILT 2026-07-05 (untested in game), 1159 mod tests.** Respect → POLITICAL WEIGHT: gates
`join_clan` (recruiting a lord into the player's clan). A lord who holds the player in contempt will not throw in
their lot, however cordial the relation. Wired at BOTH surfaces: `EncounterContextBuilder.ResolveLordRecruitEligible`
(advice, now takes `profile`, adds the Respect check on top of the existing `LordRecruitment` gate, so a
contemptuous lord is never taught the action) and `BannerlordGameStateBridge.ExecuteJoinClan` (law, now takes
`profile` off `ActionContext`, re-reads live Respect/relation, refuses in character "does not respect you enough to
throw in their lot with you. Earn their esteem first."). Profile-less lords skip the Respect check entirely
(`join_clan` never depended on a stance profile before this increment, so an untracked lord is not newly narrowed).
**Site-specific HIGH soupape (Gabriel ratified 2026-07-05):** the naive shared soupape was DEAD here:
`LordRecruitment.MinRelation` (60) already exceeds the general `RespectRelationBypass` (30), so it always fired and
the Respect floor never bound. Fixed with a dedicated `StanceGatePolicy.CommandsRespectToJoinClan(respect, relation)`
= `respect >= RespectFloor (20) || relation >= RecruitRespectRelationBypass (80)`: an adored lord (relation 80+)
still joins on relation alone, but a merely-eligible lord (60..79) must ALSO respect the player (Respect ≥ 20), so
the floor genuinely binds in that band. The general `CommandsRespect` (bypass 30) stays reserved for future
lower-gated Respect sites (sway_opinion weight, threat credibility). Side-effect fix while wiring this:
`GiveInfluenceVerb`/`LendTroopsVerb` (Trust-1) would not actually compile, `profile.ClanRelationWithPlayer` is
`int?`, passed where `WillDelegateRisk` expects `int`, fixed with `?? 0`. Console: `cr.stance_set 0 25 0 <name>`
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

## Player feedback round, SuperGraham339 (2026-07-05), triage + 2 new ratified pillars

A rich living-feedback dump. UNIFYING THEME under ~half the points: the LLM lacks ground-truth game facts and
hallucinates (army size, vanilla quests it issued, gang-leader rivalries, player backstory/appearance, "Christ").
So half the list is ONE class of fix: feed the missing fact into context + hook unhooked entry points.

**Quick wins 🔨 BUILT 2026-07-05 (untested in game), 1167 mod tests:**
- **Diacritic folding in name matching**, pure `CalradiaRemembers.Logic\NameNormalization.Fold` (lowercase +
  NFD decompose, drop NonSpacingMark, recompose), 6 tests. Applied in `ContextualNameResolver` (both message
  tokens and hero name parts) and `ModCheats.ResolveHero` (rewritten to fold+tokenize: every folded query token
  must substring some folded name part, so "Heloise the Finger" also disambiguates shared epithets). "Héloïse"
  now matches a typed "Heloise".
- **Player party troop count in context**, additive `EncounterContext.PlayerPartyTroopCount` (default 0), set in
  `EncounterContextBuilder.ResolvePlayerPartyTroopCount` ONLY when `hero.PartyBelongedTo == MobileParty.MainParty`
  (a present party member/companion; non-party NPCs get 0 = no metagame count). Rendered when > 0: "The player
  currently leads N soldiers in their party." (SEAM flagged: it is conversation-stable, could later hoist into the
  cacheable prompt prefix.)
- **Anachronism guardrail**, a light bullet in both full and lean behaviour guidelines: Calradia has its own
  faiths/gods/history, never reference Earth's religions/deities/saints/historical figures (no Christ/Rome/
  Napoleon). Does not curb RP agency. (SEAM: skipped when a station/modder `BehaviorGuidelinesOverride` is active,
  same as every built-in bullet.)

**Bugs, triaged 2026-07-05:**
- **No Encyclopedia button in captive dialogue, RESOLVED BY DIAGNOSIS, no separate fix.** The button
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

- **NPCs unaware of VANILLA quests they issued, 🔨 FIXED 2026-07-05 (untested in game, mod 0 err).** New additive
  `EncounterContext.NativeQuestNote`, built in `EncounterContextBuilder.ResolveNativeQuestNote(hero)`: scans
  `Campaign.Current.QuestManager.Quests` for ONGOING quests whose `QuestGiver == this NPC`, EXCLUDING CR's own
  mirrors (`CalradiaRemembersQuest`, `CommitmentQuest`) so nothing double-surfaces, and formats a note ("TASKS FROM
  THE REALM'S AFFAIRS YOU SET THIS PLAYER ... speak as the one who set it, never baffled"). `PromptBuilder.AppendNativeQuestNote`
  renders it, gated non-Captive alongside `AppendActiveQuests` (same guard that stops a bandit captor mistaking a
  quest for one they gave). The giver now acknowledges their own native quest.
- **Rival gang-leaders framed as friends-by-culture, 🔨 FIXED 2026-07-05 (untested, 1244 tests, mod 0 err).** Pure
  `ThirdPartyDescriptor.Categorize` gained an `areRivalGangLeaders` param (defaulted, so no caller churn): after
  kinship, two same-turf gang leaders are labelled "a rival gang leader (a competitor for the same turf, not a
  friend)", overriding the friendly/neutral tiers (a genuine ENEMY relation is stronger and kept). +3 tests.
  `ContextualNameResolver.AreRivalGangLeaders` computes it (both `Occupation.GangLeader`, same `CurrentSettlement`,
  different heroes). The NPC is now told the rivalry instead of hallucinating a tribal friendship; the wrong −8
  `change_relation` for offering to move against a rival should self-correct (hurting a named RIVAL reads as a
  favour). If the −8 persists in game, the `change_relation` vocabulary is the follow-up lever.
- **Alley-placed companion gives generic vanilla replies (#5), ALREADY HOOKED, likely already fixed.** `LlmDialogFlow.EntryHubTokens`
  already includes `alley_player_owned_start` alongside `hero_main_options`, added (with a comment citing a prior
  player report) precisely because the alley-manager conversation branches from "start" and never passes through
  `hero_main_options`, so CR's "A word with you." was missing there. So SuperGraham's report most likely predates
  this fix; needs in-game RE-VALIDATION on the current build. The "maybe more broadly impacting" residual = if any
  OTHER conversation context surfaces unhooked, add its hub token to `EntryHubTokens` (the clean extension point).

**PILLAR: Nemesis, the persistent named bandit boss (ratified 2026-07-05).** Root cause of "Ranulf the Faceless
Fox keeps his NAME but has no memory (`cr.memory`/`cr.info` empty)": the bandit captor is a transient troop, not a
Hero, so no `NpcProfile` attaches. FIX = whenever ANY bandits take the player
prisoner (NOT only the town-ambush arc, every bandit capture), the chief is a PERSISTENT MINOR-LORD Hero instead
of a jetable troop, a Hero has a profile, so memory/grudges persist for free and he becomes a foe you can hunt,
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

**MAP-PRESENCE MECHANIC, RATIFIED 2026-07-05 (Gabriel), resolves the persistence-vs-pursuit tension.** THE
TENSION: for it to be an adventure the boss must survive the vanilla world's constant bandit-clearing (a random
lord must not erase your nemesis before you can hunt him), yet the player wants to PHYSICALLY pursue and kill him.
A standing map party can't do both (AI-destructible vs chaseable). RESOLUTION (Gabriel's, cleaner than a
dormant/summon or protected-party approach): **the nemesis has NO persistent party on the map at all, he lives in
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
when bandits engage the player; (c) the TECHNICAL SPIKE, attaching the persistent nemesis Hero as the LEADER of
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
- **Persistence**, `NemesisBehavior : CampaignBehaviorBase` + `NemesisRoster`: save-safe byte[] JSON roster
  (additive key, degrades to empty, mirrors GrudgeBehavior), registered in the submodule. `FindOrMint` = the
  regional recurring roster (reuse the living nemesis of a band+region, else mint a latent identity via
  `NemesisNameGenerator`).
- **Capture hook**, `OnHeroPrisonerTaken`: when a bandit party takes the PLAYER, reuse/mint the nemesis, mint or
  reuse his persistent bandit-faction Hero (the proven `SpawnCaptors` pattern MINUS the throwaway kill), name him
  after the identity, register a memory profile (so `cr.memory`/`cr.info` work on him = the "Ranulf has no memory"
  bug fixed by construction), and record him as the active captor. `RecaptureCount` bumps on re-capture.
- **Captor override**, `CaptiveEncounterService` now resolves `captor = ambushPaymaster ?? nemesisCaptor ?? LeaderHero`,
  so the captive scene runs with the nemesis Hero and his memory (the profiled-lord CNC path).
- **Escape-decay**, the scene-escape roll uses `NemesisEscapeChancePolicy` (decays with `RecaptureCount`) against a
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

**PILLAR: Player appearance, derived + editable (ratified 2026-07-05).** Kills the backstory/appearance
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

## Town ambush, works end-to-end behind the toggle; refining *(stance increment 3; still shipped OFF)*

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
(the trigger path used to be silent, armed-but-disabled now logs); navmesh-aware ~20m cutthroat placement; player
teleported to the walkable-area centroid (town centre, not the gate); captivity map fast-forwards at 3x after the
audience (one-shot un-pause in the real-time tick); the paymaster runs the recurring captivity scenes even when
custody stays with the band; all captive scenes use a neutral player-POV narrator.

OPEN, the refinements before removing the toggle:
1. **Custody transfer to the lord** when he sits in his town: `TakePrisonerAction` on his in-settlement party does
   not "hold" (`held=False`), so the player stays the band's prisoner (harmless now that the paymaster visits
   regardless, but the proper lord-imprisonment / ransom disposition is unfinished). **Decided 2026-06-30: SHIP the
   band-custody fallback as the intended disposition for a town-sitting lord (a hired band holds you until ransom /
   escape), not a blocker.**
2. ~~**Fleeing townsfolk**~~ **MOOT + REPLACED, DONE 2026-06-30.** The brawl plays at the GATE (outside the walls),
   so there is no townsfolk crowd, but there are a gate guard or two. `AmbushFightMissionLogic.SendBystandersFleeing`
   now scatters them when the fight starts: any human agent in the scene who is not a combatant (guard / stray
   civilian) is made **invulnerable** (a slain guard = a crime / odd mission end), sent **running away from the
   player** on the navmesh (reusing the file's navmesh helpers), then **faded out** ~2.5s later. UNTESTED in game.
3. Then **remove the toggle**, the last step, gated on Gabriel **playtesting the 2026-06-30 refinement lot in game**
   (force scaling / elite chief / companions-captured are BUILT but UNTESTED). Removal = `CrSettings.TownAmbushEnabled`
   + `ModSettings.TownAmbushEnabled` + the two gates in `TownAmbushBehavior` (l.118 `IsEntryBlocked`, l.165
   `DecidePlayerTownEntry`).
Console `cr.ambush`.

## DECIDED 2026-10-28 (Gabriel), STEP 1 BUILT: the world's initiative, one director for every unasked interruption

Gabriel (28/10/2026): keep the player in the game, not pulled "trop souvent vers une gestion de conversation sans que le
joueur l'ait initie de lui-meme"; today "chaque sujet se gere lui-meme sans tenir compte de l'ensemble". An inventory
(28/10) found about twenty unasked sources (road interception, fief audience, stance interception, four companion
audiences, the mailbox and its bypass letters, the letter window, tales on the map, scenario tales, the war council offer,
the prisoner envoy, the arrest, the town ambush, captive scenes) and no shared pacing beyond three hand-offs.

The rule (Gabriel: "a qui le tour", coherent priorities, variety "pour ne pas qu'un systeme ecrase les autres"):
- Every system files a request (key, family, rank, character, filed hour, letter or not); the director grants one turn at
  a time. Families: Lords, Companions, Household, Letters, Tales, WarAndLaw, Aims.
- Ranks: Imposed (Gabriel's four: the arrest, the war duty, the ambush, an envoy with a short deadline) passes whenever
  the moment allows and counts; Pressing; Ordinary.
- Never while busy (battle, mission, chat, menu) or before the settling delay; then the player's limits (at once, quiet
  after their own conversation, gap between two turns).
- The fair turn (Gabriel: "Il ne faut pas que les priorites empechent les autres [...] chaque categorie qui est dans la
  file d'attente puisse etre jouee une fois par mois"): a family waiting with no turn for 30 days comes before any rank
  and past the budget (the unhappy wife who could never say so).
- Then the budget, then the best rank drawn by weight: family weight, age, variety (a family served in the window steps
  back), the same character all but passed over for 72 hours. Requests expire after 10 days (impositions never).
- MCM "The World's Initiative" (Gabriel: "combien de systemes peuvent se manifester en meme temps, le delai avant que
  l'orchestrateur decide de lancer une intervention"): a pace dial (Rarely / Occasionally / Often / Custom) over At once,
  Interruptions per window / Window, Time between two, Settling delay, Quiet after your own talk, Variety, Requests
  expire, Letters count as (Half), Urgent matters wait their turn (Off), and one weight per family replacing Lords
  approaching, Letters from NPCs and the companion cadence (read once, converted).

**Step 1 BUILT 28/10/2026:** `Logic/Initiative` (WorldInitiativeModel, WorldInitiativePolicy, presets), 15 tests. Nothing
wired, so nothing changes for the player; the MCM group and `cr.knocks` come with step 2 (inert options would mislead).
Step 2: the engine director (queue and turns saved under an additive key; systems ask at the door and act only when
granted, withdraw when their reason goes), the MCM group, `cr.knocks`, and the big talkers (interception, fief audience,
companions, letters). Step 3: the rest. Step 4: lords' aims of their own as strategists (an advantage for the lord;
between NPCs too, played offscreen and heard as news; only aims with a part for the player knock).

**Step 3 BUILT 29/10/2026** (Gabriel: "oui pour le regulateur, go"): the household (divorce demand, a child's pension,
a partner's jealous letter, the household's mailbox letters), stance letters, the companion's farewell, captor scenes
(pressing), the war summons, the arrest's warrant and the trial (imposed); `KnockOrWait` for sources that reckon only
weekly (they wait and knock daily); any request silent 14 days is dropped (`AbandonedAfterDays`). DELIBERATELY NOT
GATED, and why: what rides on a turn already given (divorce escalation, missed interception, jealous reaction letter,
levy after a summons, ambush arrival, bounty hunters after a refused warrant; gating the escalation in RegisterRefusal
would also lose the grudge and the regard cut); what the player asked for (replies, courtship acceptance, patrol
reports); `CrLetters.Deliver` (a modder contract, "never ours to hold back"). LEFT FOR GABRIEL: the war council offer
(hard-coded off, no retry, and the director's own MapEventEnded -> NoteSettled would refuse the battle-won trigger every
time) and the bastard's mother's note (its season latch would cost a whole season). Next: the old pace dials (lords
approaching, letters, companions asking) read as family weights while the director is on.

**Wished the same day (Gabriel): an awaited correspondence, marked by a ribbon bookmark** (Gabriel chose the ribbon over
a star, 28/10/2026; it may be drawn with the OpenAI image model; tooltip "Awaited: their letters come before others'").
The player puts out the ribbon on a correspondent in the letter book ("si je suis en train d'ecrire des lettres d'amour a une NPC, peut-etre que j'aimerais avoir de ses nouvelles
rapidement meme si c'est la guerre"): that correspondent's letters rank Pressing in the director, and that is all it does
(Gabriel: "ne fait que donner la priorite pour l'orchestrateur"); they still count in the budget. Saved on the profile.
To build with step 2's letters.

**Designed 30/10/2026 (Gabriel, after tashmetu's two players: too many letters, too repetitive): the letter box and a
Courier section in the MCM.** Order agreed: (1) make "Rarely" a real cap again (PROVEN bug: with the World's Initiative
director on, InitiativeDials.Budget lifts the letter budget and the dial is only a draw weight, so about six letters a
week pass; Household letters, romance and spouse included, never follow the dial); (2) a Courier section in the MCM by
FAMILY of letters, not by the 33 LetterReasons (congratulations, realm news, marriage offers, romance, spouse and
family, politics and favours, threats and schemes), each with an on/off and a frequency that is a real ceiling; a
global "the same person at most once every N days" (the hidden 15-day LetterCadence wait); a ribbon filter ("only
awaited correspondents write unasked"); obligations never switch off (replies to the player, war summons and levy,
patrol report, companion farewell, divorce, child support, bastard's mother, other mods' letters); (3) the letter box.
Open for (2): may threats be switched off; three steps or a letters-per-season slider.

The letter box (Gabriel's rulings, 30/10/2026): a letter icon on the map bar (built like the council's,
Map/CouncilNavigationItemVM) with the count of unread letters; a click opens a "Box" page of the letter book listing the
unread letters (seal, sender, day, the kind of letter, no excerpt): pressing ones first, then the ribbon's, then newest.
Letters STAY and never age out ("les lettres restent et ne vieillissent pas"). An icon throws a letter away unread.
A distinct courier sound on arrival, like a notification (a medieval instrument or a pigeon's coo). Our own image for
the icon. DEFAULT stays immediate opening ("le defaut est ouverture immediate"); the box is the MCM choice "on arrival:
open it / keep it in the box". Agreed: the sender counts from ARRIVAL, not reading (MarkReceived unchanged; an unread
letter is an unanswered one, the sender cannot know it was not read); IsRead is an additive field, old saves read every
delivered letter as read; pressing letters (war summons and levy, prisoner offer, divorce and its escalation,
blackmail, jealous threat, child support) are flagged by an alert colour, a game notice and their deadline, top of the
list. Not in the first version: writing the text only on first opening (breaks courier secrets and the sender's memory
of what he wrote). To check before building: whether MapNavigationItemVM already shows a count or alert; how a module
registers its own sound (module_sounds.xml, SoundEvent).
The courier sound is CHOSEN (Gabriel, 30/10/2026): a letter dropping through a house letterbox, freesound_community-letter-from-a-house-letterbox-101145.mp3 (Pixabay, 0.84 s; his Downloads/Music), to ship as ModuleSounds/cr_letter_arrival.ogg (leading silence trimmed, 0.55 s, vorbis 44.1 kHz). Lyria 3 Clip (OpenRouter, 0.04 USD a clip) was tried first: it composes 25-30 s of music whatever the prompt, no sound effects. Engine 1.4.8 (TaleWorlds.Engine.SoundEvent): PlaySound2D(string), GetEventIdFromString, CreateEventFromExternalFile; the module route (a module_sound entry in ModuleData/module_sounds.xml, the file under ModuleSounds/) is loaded natively and is unproven here until a cr.test plays it.

**The letter box, final design (Gabriel, 30/10/2026; SUPERSEDES the Courier section by family, the mailbox lottery and
"default = immediate opening" above). Ships in 2.8.0 (the open version, renamed from 2.7.22 the same day).**
Gabriel: "toutes les lettres sont envoyees et vont dans la boite aux lettres [...] plus besoin de reduire la frequence ou
le nombre de lettres, c'est le joueur qui classe, elimine, abonne et desabonne les conversations par lettres."
- Every letter that passes its own rules (its trigger, the per-sender wait) is sent; the mailbox budget, the weights, the
  aging and the diversity roll go (once the box exists). Letters leave the world's initiative: a letter waiting in a box
  interrupts no one; the director keeps only what comes to the player in person.
- Everything goes into the box, with the courier sound; nothing opens by itself (Gabriel: "tout dans la boite avec le
  son"). The old immediate opening stays an MCM choice.
- The box lists letters by order of arrival. Icons: urgent (war summons and levy, prisoner offer, divorce and its
  escalation, blackmail, jealous threat, child support), ribbon (an awaited correspondent), none for the rest. A letter
  read leaves the box for the letter book. Letters never age.
- Throwing a letter away = "the player no longer wants to hear of this subject from this sender": the pair sender +
  subject (the family of the reason: divorce demand and escalation are one subject, a love letter and "you did not
  answer me" another) is silenced. It silences the LETTER, not the person: the jealous husband stays jealous, his duel
  or ambush still comes; the sender cannot know (NPCs never lie), no regard lost. Urgent letters may be thrown too
  (Gabriel: "oui"), with a warning that what they announce is unchanged (a thrown war summons keeps its deadline).
  Undo: on each correspondent's page of the letter book, beside the ribbon, the silenced subjects with "accept their
  letters on this again".
- The text is written only when the letter is first opened (Gabriel: "oui"): the envelope (sender, subject, day) travels;
  a letter thrown unread costs nothing. To rework: the courier secret (write the text when the letter is intercepted)
  and the sender's memory of what he wrote (filed at opening, or from the subject until then).
- Kept: the sender's arrival rules (MarkReceived at arrival; an unread letter is an unanswered one), IsRead additive
  (old saves: every delivered letter read), the per-sender wait (an MCM setting), CrLetters.RegisterVeto for other mods.
- Order of work: (1) StanceFavor letters rotate several fixed texts (Gabriel: "textes fixes, pas de delais ni de couts";
  the identical "I count you a friend" from every lord in 2.7.21); (2) the box: icon, count, sound, silencing (Opus);
  (3) writing at opening; (4) the budget and the draw removed.

**The letter box, progress (night of 30-31/10/2026).** BUILT, not play-verified: friendly letters rotate six fixed texts
(62de59c) and wait for a deep bond (fe9ecac); part 1, arrival into the box, throw = silence (SDK 24dce1e, f2c80fb);
part 2, the map bar icon with its badge and the box window (88afc1e); a silenced subject never written nor pooled
(f371ec8). Defaults taken, put to Gabriel: a war summons or levy, a divorce, a child's support and a patrol's report
silence nothing when thrown (a jealous threat and blackmail do, his own example); a thrown letter leaves no trace in the
letter book; the badge has one look (urgency shows in the tooltip, the notice and the row's URGENT, not on the icon).

Step 3, writing the text at first opening, NOT built: its consequences were never put to Gabriel in prose.
- Anachronism: a letter written when opened would know what happened after its sender wrote it. Remedy: write it with
  the world as it stood on its sending day (the prompt's date and the sender's memories cut at SentOnDay), which the
  prompt cannot fully do (the realm's live state is read today).
- The courier's proximity rule (a longing letter never reaches the player while its sender rides beside them) is moot
  for the arrival, but the text would be written in the sender's presence: the prompt must say it was written days ago.
- The sender's memory of what he wrote moved to SENDING on 26/09 (raphareish: a sender denied a lost letter). Written at
  opening, the memory can only be the subject until then ("I wrote to her of the war"), then the text once opened.
- Courier secrets need the text when a courier is taken: write it at interception instead.
- Delivery refuses a blank letter (CheckDeliveries); the window needs a waiting state while the model writes (a few
  seconds, or more with a slow model).
- Gain: a letter thrown or never opened costs nothing. Without step 3, step 4 (removing the mailbox budget) would
  multiply what players pay, every letter being written at sending: step 4 waits for step 3.

**Step 3 BUILT (31/10/2026, Gabriel: "oui go pour etape 3"), not play-verified:** SDK 1a68a9c, mod 1996246 + 2fb4c9d.
Envelopes in box mode; written as of SentOnDay, cut at EnvelopeKnownThrough; the sender's memory filed at sending and
enriched once written; a taken courier writes it then; LetterEnvelopePolicy guards the four "no text" readers; results
wait for the campaign map and are dropped across sessions. Known gaps: regard, standing and other mods' variables are
read at opening; a broken API key now fills the box with letters that cannot be opened (before, they were never sent);
the memory cut is an index, so a memory compaction between sending and opening shifts it. Step 4 (removing the budget)
NOT built and has no go: "Open it at once" still writes every letter at sending, so that mode must keep a budget.

**Steps 3-4 done (31/10/2026):** the old immediate opening removed (7824213, Gabriel: "on peut juste garder la boite et
enlever l'ancien mode"); step 4 built (c843c71, "sans reglage pour le moment"): no mailbox budget, every ready letter
sent one per sender, letters ask the world's initiative no turn (the war summons still does), MCM "Letters from NPCs"
and "Weight: Letters" removed. Watch in play: how full the box gets. A per-sender wait in the MCM was offered and
declined for now.
