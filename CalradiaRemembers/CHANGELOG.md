# Changelog.. Calradia Remembers

All notable changes to the mod, newest first. Dates are calendar dates.

## v2.8.1.. 2026-10-31

### Fixed
- **A tale's picture no longer lands on the pause menu** (Gabriel's screenshot, 31/10/2026). In Dark Leather the picture
  sits in the margin left of the book and slid with it when the chat yielded, and 800 to the right only carries the
  margin to the middle of the screen. It now hides while the chat has yielded (IsSceneImageVisible) and comes back with it.
- **The killing window's ground is dark brown**, not white: the tint set on the ground button itself never drew; a plain
  tinted child does (Gabriel's screenshot, 31/10/2026).
- **A letter rider catches you on the move** (kurushimi00, Nexus, 31/10/2026: riders "all requiring me to keep my party
  immobile to let them actually deliver the letters"; Gabriel: "il faudrait qu'un cavalier courrier puisse rattrapper le
  joueur en mouvement [...] lorsque le cavalier est assez pres de lui sans avoir a le toucher forcement"). A rider was steered
  once a game hour to where the player then stood and its arrival read on that hour, so nobody moving faster than 2 units an
  hour ever received one (true since the riders were added in June); the 2.8.0 volume made it plain. Now
  CourierService.TickReplyRiders runs on every campaign tick: the letter is handed over at the same 2 units, the rider turns
  when the player has moved (ReplyRiderPolicy), and it rides 1 unit an hour faster than a player who outruns 6.5. The tick
  comes before the engine's encounters, so clicking a rider should give the letter on the way in instead of its "not allowed
  to talk" line and the encounter menu (not play-verified). The hourly pass stays as the backstop.

### New
- **The NPC panel holds what the player has learned** (Gabriel, 31/10/2026: "Dans le panneau du NPC, on pourrait ajouter
  tout ce que le joueur a appris sur ce NPC et qui a ete ajoute a l'Encyclopedia"). In both chat skins and at the council:
  the reason beside Personal regard, Bearing, What You Know folded into What you have learned, Heart, Kindnesses, Standing
  accords, the Nemesis dossier and, at Hardcore, Trauma. What a reply teaches shows at once in the open panel
  (RefreshOpenDossier). The model holds truths the player has not learned, so every section goes through the gated reader
  of its encyclopedia row; Grievances, Kindnesses and Standing accords moved into Mapping.DossierReader, which the page and
  the chat now share. Left on the page: friends, enemies and houses, Misdeeds, Contentment. cr.test npc_dossier.
- **Two letter settings in the MCM** (kurushimi00: "an MCM setting to turn letters completely off"; Gabriel: yes, duty
  letters kept). The 2.8.0 letter box took the weight of many letters in the box, not on the map. "Letter Riders on the Map"
  off: no rider, the letter reaches the box on its date, riders out are reclaimed by the daily pass (ReplyCourierBlock.NotWanted,
  which also spares a ride to a letter that would arrive unseen). "Unasked Letters" off: characters stop writing of their
  own accord (LetterBoxPolicy.IsUnasked, read by LetterBox.IsSilenced, so nothing is written and what is on the road is set
  aside unseen). Replies, war summons and levies, divorce, news of a child and child support, blackmail, jealous threats,
  patrol reports, farewells, other mods' letters and a courtship's marriage letter still come; a parent's proposal stops
  (Claude's default).
- Tests: ReplyRiderPolicyTests, ReplyCourierGatePolicyTests, LetterBoxPolicyTests; cr.test letter_riders. Not play-verified.

### Changed
- **The chat's buttons answer the cursor** (Gabriel, 31/10/2026: "ce serait bien d'ajouter le hover effect sur les
  boutons"). Dark Leather's tool buttons were transparent hotspots over painted tiles, and the small buttons of both
  skins carried no brush, so nothing showed they could be clicked. A gold halo now fades in around the button, its art
  untouched, and deepens on a press, never on a disabled button (ButtonGlowPolicy, CrHoverGlowWidget, cr_hover_glow.png,
  a nine-patch). A flat wash dulled the painted tiles, and in a button sized by its text it lit half the screen
  (Gabriel's screenshots), so those text links change colour instead (CR.Link.* brushes). ButtonGlowPolicyTests fails on
  a future button in either skin with no way to answer, and on a glow inside a button sized by its text.
- **World's Initiative hints** (MCM): letters no longer named as taking turns. "Letters Count As" stays: a lord's stance
  letter, a companion's farewell and a divorce demand still ask for one, and its hint now says so. A silenced stance letter
  asks for no turn.
- **A killing attempt is played in front of you** (zhenja1, Nexus, 31/10/2026: he confirmed a killing twice, of the bandit
  who held him and later of the same man in a tavern, and the man lived both times; Gabriel: the window names the chance,
  "a bit like Baldur's Gate's dice, but only the number changing quickly until it stops on the score", then the model
  writes the scene). The attempt was always a roll (ViolencePolicy: 5 to 95, a town or castle counts as guarded), but the
  "Take a life?" popup named no odds and a failure showed only in the message feed while the character talked on.
  Now, in a chat: the chat's own window (CalradiaRemembersKillRoll block in both chat skins, ChatViewModel.KillAttempt)
  names the chance and why (KillAttemptPolicy.ChanceReasons), Strike rolls against the facts the chance was read from,
  the deed is applied at once, the number rolls and lands on the roll (shown 1 to 100, a kill at most the chance), then
  the model tells the scene (KillAttemptPolicy.NarratorTask: the outcome overrides what was said before; a failure stops
  before the character answers) and it goes into the conversation as narration. After a kill only Leave remains, also
  with witnesses present (default, Gabriel did not rule on that case); a captive who kills the captor escapes as the
  scene closes (ResolvePlayerEscapeAttempt forced); after a failure the character answers it in one more turn, a captive
  scene included. Elsewhere (the council) the game's popup asks, now with the chance. The send box is held while the
  window is open. The window is Gabriel's painted panel (GUI/Images/cr_killroll_*.png: panel, chance plaque, Strike and
  Stay your hand in four states each, shown by the new CrButtonArtWidget); its art loads only when a window first opens.
- **A life the game holds is refused before the window** (found tracing zhenja1's report: KillCharacterAction.ApplyInternal
  returns silently when Hero.CanDie is false, e.g. a notable who gave a task under way, while CR still said "You kill
  X"). Asked before the window, again at the strike, and a murder that leaves the character alive is logged.
- Tests: KillAttemptPolicyTests (12); cr.test kill_roll (a real chat ViewModel, no harm, no model call); cr.kill_roll
  [chance] [roll] plays the window in the open chat with nobody harmed. Not play-verified.


## v2.8.0.. 2026-10-31

### New
- **Tasks say plainly what to do** (avalanche24, Nexus, 31/10/2026, with a screenshot: "What do I need to do here
  exactly?"; Gabriel: "Il faudrait que les quetes soient decrites simplement pour les joueurs"). A task's journal entry
  showed only the giver's words, as its title and its only line ("Sargot's gang leader hoards what he learns. You'll slip
  into its shadows..."). The game now writes a short title ("Scout Sargot from inside (Dnin)") and a first line "What to
  do:" for every kind of task: where to go, whom to see, what counts (being declared on does not count for a war to
  declare, a bought weapon does not count for one to forge, a letter is delivered through "I bear a message for you"),
  whether the reward waits with the giver or is settled on the spot, and the deadline as the game shows dates, the day itself included (the
  engine's own countdown is hidden for these tasks). The giver's words stay underneath, as theirs. Each line follows
  the deed's real check, traced in the code the same day. The title is read live, so tasks already under way get it;
  their "What to do" line is added once at session launch, silently. `QuestJournalText` (Logic),
  `CalradiaRemembersQuest.FactsOf`/`EnsurePlainSteps`; tests QuestJournalTextTests; `cr.test quest_journal_plain`.
  Not done: `QuestService.Describe` (chat lines, memories) still prints a raw goods key for a market or tale good.
- **The letter box** (Gabriel, 30/10/2026, after tashmetu relayed two players buried in letters: "toutes les lettres sont
  envoyees et vont dans la boite aux lettres [...] c'est le joueur qui classe, elimine, abonne et desabonne les
  conversations par lettres"). A letter that arrives no longer opens a window: it waits in the letter box, a notice names
  its sender (and says when it looks urgent), and every letter due that day arrives, not one a day. It is the only way
  now (Gabriel, 31/10/2026: "on peut juste garder la boite et enlever l'ancien mode"): a letter never opens by itself,
  and the short-lived MCM choice "When a letter arrives" is gone. The sender's side of the arrival is
  unchanged (`MarkReceived` at arrival: his memory of writing, a child's pension quest; an unopened letter is an
  unanswered one, he cannot know it was not read). `LetterBox` (mod) and `LetterBoxPolicy` (Logic):
  - Throwing a letter away silences its SUBJECT from that sender ("le joueur ne veut plus entendre parler de ce sujet de
    la part de cet expediteur"): letters of one conversation share a subject (a divorce demand and its escalation, a love
    letter and "you did not answer me"); a later letter on a silenced subject arrives unseen. It silences the letter, not
    the person: what the character does in the world is unchanged. A reply to the player's own letter and another mod's
    letter are never silenced (the mod hears `SetAside`). A war summons or levy, a divorce and a child's support may be
    thrown but silence nothing: the game still holds the player to them (Claude's default, put to Gabriel).
  - Urgent mark: war summons and levy, prisoner offer, divorce, blackmail, jealous threat, child support.
  - The box is ordered by arrival; a letter still in the box, or thrown away, is not in the letter book until read.
  - Saved state: `PendingLetter.InBox`, `ThrownAway`, `NpcProfile.SilencedLetterSubjects` (SDK 24dce1e), additive: every
    letter delivered before the box counts as read.
  - The courier's sound (Gabriel's choice, a letter through a house letterbox) plays once per tick.
  - The map bar: a letter icon beside the council's (our own image, a sealed envelope in the council button's frame),
    its badge counting the letters waiting; the tooltip says when one looks urgent. A click opens the letter box window:
    the letters by order of arrival, the URGENT mark and the ribbon, Open (the letter window; the box comes back when it
    closes) and Throw away (an urgent letter asks twice: "Throwing this letter away changes nothing of what it
    announces"); below, the subjects silenced, each with "Accept again".
  - A silenced subject is not written at all: it leaves the mailbox's daily pool (it no longer takes a wanted letter's
    place) and `QueueInitialLetter` refuses it before any model call; the arrival check stays as the backstop.
  - A patrol's report is never silenced either (the player decides on it whether the men come home).
  - Step 3 (Gabriel, 31/10/2026: "oui go pour etape 3"): a letter now travels as an ENVELOPE and its text is written
    when the player first opens it, so a letter thrown away or never opened costs no model call. Everything it is written
    from is composed the day it is sent (the occasion, where the sender and the player were, the sender's mood) and the
    text is written as of that day, from the memories its sender held then (`EnvelopeMessage`, `EnvelopeKnownThrough`, the
    prompt's day = `SentOnDay`). Read at opening, not at sending: the regard, the standing, and other mods' prompt
    variables (`StandingContextFor` resolves them then), so a bridge's realm facts can still be of the opening day. The sender remembers writing it from
    the day it is sent (raphareish's rule), and that memory gets the letter's substance once the text exists
    (`LetterMemoryFinalizer.EnrichFromText`). A courier taken by bandits or enemies writes the envelope then, so its
    captors carry real words and the letter sent again carries the same ones. Opening breaks the seal ("You break the
    seal of X's letter."); the letter opens a few seconds later, or stays in the box if the model did not answer.
    `LetterEnvelopePolicy` (Logic) keeps the four places that read "no text" as "not ready" or "dead" from losing an
    envelope (delivery, the session-start cleanup, a courier past its deadline, a scheduled letter). Every letter an NPC
    starts is an envelope (none is sent without a model to write it). Letters written before keep their text.
  - Step 4 (Gabriel, 31/10/2026, "sans reglage pour le moment"): no more mailbox budget. Every letter ready today is
    sent, one per sender (the weightiest of his), since a letter costs nothing until opened; each sender's own wait
    between letters (15 days, 10 for a love letter, 30 for a marriage offer) is what spaces a correspondence. Letters
    no longer ask the world's initiative for a turn (the mailbox, a partner's jealous letter, a scheduled letter): one
    waiting in a box interrupts no one. The liege's war summons still waits for its turn, since it opens a reckoning
    with a deadline. Removed from the MCM: "Letters from NPCs" (Rarely / Occasionally / Often) and the initiative's
    "Weight: Letters". `LetterPacing`, `LetterBudget` and `LetterMailboxPolicy.SelectNext` are gone;
    `LetterMailboxPolicy.ReleasedToday` replaces the last. The correspondence-interest dampers (saved) still order one
    sender's letters.
  - For modders: `CrLetters.GetStatus` answers `in_box` for a letter that reached the player unopened (it said
    `delivered`); throwing another mod's letter raises `CrLetters.SetAside`.
- **Lords play their realm's strategy** (Gabriel, 29-30/10/2026: a lord's ambition is "tout ce qui renforcit son Clan ou
  sa faction militairement", plotting against a rival or a realm grown too strong, arrangements with weaker realms
  against a stronger one). MCM, Relationships, "Lords play the realm's strategy", OFF by default for this first build.
  - A realm grown too strong (at least 1.5 times the next and 30% of all the realms' might, `RealmCounterweightPolicy`):
    once a week, one other realm takes one step of a counterweight against it, through the game's own kingdom
    decisions (the house that backs it most by the game's own support files it and pays its influence; the clans vote
    and the ruler decides). At war with it: call an ally to arms, make peace with a weaker enemy, seek an alliance with
    its other enemies. At peace with it: allies first, then peace on the other fronts, then the war put to the vote.
    At most one step a month per realm. The court hears who urged it, never the outcome, and only once the game has
    accepted the decision (`LordStrategyBehavior`).
  - The game (1.4.8) has real alliances and calls to arms (`StartAllianceDecision`, `ProposeCallToWarAgreementDecision`);
    the strategists use them, within the game's own limits (two alliances at most, both sides must want it).
  - A house grown too strong: a house head whose house another outgrows twice over, and who does not count its head a
    friend, may plot against him (`AutonomousSchemePolicy.IsOvershadowingTarget`), the player too when the player's
    house dominates. Only a house of his own realm or of a realm at war with it, one such plot per target at a time,
    never kin, never a ruler, and the plotter asks the action gate (`ActionKind.Plot`). An old hatred still comes
    first; the plot runs the usual scheme road.
  - The strategist turns to the player (second increment, same switch): once a week, up to three house heads who
    know the player and do not dislike them (the warmest first) hold an aim of their own toward the player, against
    the head of a rival house grown too strong who rides for a realm at war with theirs, else against the realm grown
    too strong (`StrategistAimPolicy`, `Aims/StrategistAims`): beat it in the field, raid one of its villages, or
    storm one of its strongholds (Gabriel, 30/10/2026: "go pour le pillage et le siège"). A siege the lord's realm
    already presses comes first; a merciless lord (Mercy below zero) asks for the raid; otherwise a battle while the
    target can be met in the field (Claude's defaults). An ordinary CrAims aim under its own
    source tag (`CalradiaRemembers.Strategy`), so it is voiced in conversation and may bring the lord to the player
    through the world's initiative; its deed heads the lord's grounded quest menu (attack_lord / attack_faction), so
    the task he asks outright is the one the quest factory issues and the deed tracker verifies. Never against the
    player's own realm or house, never from a lord at war with the player; withdrawn once the deed no longer stands
    (target fallen, the village looted or the stronghold changed hands, peace made) or the task is given, and lapses
    after 40 days.
  - Left out on purpose: the player's own realm (a decision filed there would raise the game's vote popup outside the
    world's initiative regulator).
  - `cr.strategy [plan|run|aims]`; `cr.test lord_strategy`, `cr.test strategist_aim`. Not play-verified.
- **Reply Length, in the MCM** (RaveN017Mod, Nexus PM, 29/10/2026: replies too long, "like a novel", next to the short
  replies he was used to; he asked for a minimum and maximum word count). Content group, Reply Length: Full (the length
  the mod has always taught, 3 to 7 sentences, longer for a story; the default, so nothing changes untouched),
  Balanced (2 to 4, up to 6 for a story) or Short (1 to 3, always). Counted in sentences rather than words: a word cap
  cuts a reply off mid-sentence, and a model keeps to a sentence count far better. A length chosen on purpose holds
  against the chat's narrative style, which may otherwise run speeches long; Short also shortens a street commoner's
  replies. Applies at once, no restart (`ReplyLengthPolicy`, `PromptBuilder.ReplyLength`, SDK `b90dc8c`).
  `cr.test reply_length`. Not changed: the council (its table has its own length rules) and letters.
- **Who is here, and who speaks: the participants panel in the chat** (RaveN017Mod, Nexus PM, 29/10/2026: the clan
  members he invited "often don't interact much at all", "maybe we should add an @ system so it's clear who I'm
  addressing"; Gabriel's design the same day). Invited companions were witnesses: they react only when the model lets
  them, and the one way to draw them in, speaking their name, was invisible. The "Present: ..." line in the chat's
  title now opens a panel on the left (both skins) listing the one you came to see, the active voices and the
  witnesses. A click on a name turns you to them; a witness you turn to joins the conversation and then speaks every
  turn, each with their own memory (the ensemble turn that speaking a name already started). "Step back" sends an
  active voice back among the witnesses. At most three voices speak at once, the one you came to see among them
  (`ConversationCast.MaxActiveVoices`): each is a model call and a wait every turn, and a larger table is the
  council's. The cap holds for a name spoken in the message too; a fourth is answered and stays a witness. Someone who
  leaves the room (a private audience) now leaves the cast as well, so they no longer keep the scene an ensemble.
  `CastPanelPolicy`, `ChatViewModel.Participants`, cr.test `participants_panel`. Not in a council, a captive or a
  captor scene. Not play-verified: the panel's place and look.
- **The world's initiative: one conductor for everything that comes to you unasked** (Gabriel, 28/10/2026: keep the
  player in the game, "pas le sortir trop souvent vers une gestion de conversation sans que le joueur l'ait initie de
  lui-meme"; "a qui le tour", coherent priorities and variety; every kind in the queue at least once a month). Lords
  seeking a word, companions, letters and the rest ask the conductor before they come; it gives one turn at a time:
  never in a battle, a chat or right after one; the arrest, the liege's war summons, an ambush and a short-deadline
  envoy always pass; then how many at once, the quiet after a conversation you opened, the gap between two, a kind
  that went a month without a turn, the budget, and a draw by rank, weight, age and variety (`WorldInitiativePolicy`,
  `WorldInitiativeBehavior`, save key `cr_world_initiative`). MCM group "The World's Initiative": How Often the World
  Comes to You (Off = each system paces itself as before; Rarely; Occasionally, the default; Often; Custom), the Custom
  timing, Variety, Requests Expire After, Letters Count As (Half), Urgent Matters Wait Their Turn (off), and a weight per
  kind. `cr.knocks` shows who waits and who had a turn. cr.test `initiative_director`, `initiative_turns`.
  Asking the conductor now (each asks before it stamps its own cooldown, so a lord kept waiting comes another day):
  - a lord riding out to the player on the road, including the stance-driven ride (key `road:<lord>`, the lords' family,
    or the aims' for a lord's own aim; the war duty imposed; a grudge, news, a nemesis or a patrol pressing; a kindness
    or an aim ordinary, `InitiativeRanks`);
  - a lord asking for a word in the town the player walks into (`here:audience:<lord>`, withdrawn when the player
    leaves the town);
  - the companions: a private word on a topic (`companion:<id>`), secret affection (`affection:<id>`), an unhappy
    companion asking to be heard (`retention:<id>`), a war-weary one asking to retire (`retirement:<id>`). A companion
    kept waiting by the conductor is not turned away by the player: no refusal is counted, no decline cooldown starts,
    and they ask again at the next pass. Leaving, retiring, a plot uncovered and an errand's news press; the rest can
    wait (`InitiativeRanks.RankOf(CompanionAudienceReason)`);
  - the mailbox: the letter it chose asks for its turn (`letter:<sender>:<reason>`, a letter costing what Letters Count
    As says); kept waiting, it competes again the next day. What the household writes (a spouse, a lover, the mother
    of the player's child) asks as the household (`InitiativeRanks.FamilyOf(LetterReason)`);
  - a written tale beginning on the map (`tale:<id>`, ordinary; kept waiting, it may begin another day);
  - a prisoner envoy riding up (`envoy:<lord>`) and the town ambush (`ambush:<assassin>`): two of Gabriel's four that
    always pass, yet they too wait out a battle, a chat or the settling delay (the envoy comes the next hour, the blades
    stay armed for the next visit).
  - added 29/10/2026 (Gabriel: "oui pour le regulateur, go"), the household: a spouse's divorce demand
    (`household:divorce:<spouse>`, pressing; no clock starts until its turn), a pension asked for a child
    (`scheduled:<mother>:<reason>`, no attempt spent while it waits), a partner's jealous letter about her place
    (`household:jealousy:<partner>`); a lord's stance letter, a threat pressing, a kindness ordinary
    (`stance:<lord>`); an unhappy companion's farewell (`farewell:<id>`, pressing, dropped if they no longer want to
    leave); a captor's scene (`captive:<captor>`, pressing, not one of the four); the liege's war summons
    (`warduty:summons`, imposed; no reckoning or deadline opens until it has its turn), the arrest's warrant
    (`arrest:warrant`, imposed, asked at most once an in-game hour, the sergeants shadowing meanwhile) and the trial
    (`arrest:trial`, imposed). A source that reckons only weekly (the divorce, a stance letter, a farewell) waits in the
    queue and knocks again every day until its turn or until its reason goes (`KnockOrWait`).
  Deliberately not asking: what rides on a turn already given (the divorce escalation, the missed interception, the
  jealous reaction letter, the levy after a summons, the ambush's arrival, the bounty hunters after a refused warrant);
  what the player asked for (a reply, the courtship acceptance, a patrol report); another mod's letter (`CrLetters`, a
  modder's contract); the war council offer (off, and a battle's end would always find the player "settling", with no
  second chance); a bastard's mother's note (its season latch would lose a whole season: Gabriel's call).
  The older pace dials, while the conductor paces (29/10/2026): Lords Approaching You, Letters from NPCs and Companions
  Asking For A Word are now weights on their kind's turns (Never stops it, Rarely halves it, Often adds half, times the
  conductor's own slider; `InitiativeDials`), and their own rates step aside: the lords' daily roll, the mailbox's cap
  per window and the companions' shared cooldown. Before, both applied, so the world came FAR less often than either
  setting said: expect more of it at the same settings. Kept: a lord's own cooldown, a correspondent's, the mailbox's
  variety. With the conductor Off, every dial works exactly as before. Hints updated. cr.test `initiative_dials`.
  A request whose system has stopped asking for 14 days is dropped whatever its rank (`AbandonedAfterDays`): nothing
  withdrew a spent ambush or a departed envoy, and the world's impositions never expire, so they stayed in the save.
- **An awaited correspondence** (Gabriel, 28/10/2026: "si je suis en train d'ecrire des lettres d'amour a une NPC,
  peut-etre que j'aimerais avoir de ses nouvelles rapidement meme si c'est la guerre"; the ribbon bookmark he chose over a
  star). `NpcProfile.LettersAwaited` (additive): that character's letters come first in the mailbox, past the diversity
  roll (`LetterCandidate.Awaited`), and press with the world's initiative. Priority only: the budget, the cooldowns and
  the courier are unchanged. The ribbon hangs from the letter book's left page, by the spine: faint, a click marks the
  correspondence as awaited; full, it is awaited (`LetterViewModel.ExecuteToggleRibbon`, `GUI/Images/cr_ribbon.png`, the
  broad gold-trimmed drawing Gabriel chose in game over a narrow one with a clasp). `cr.awaited <name>` does the same from
  the console.
- **What matters most to a character, and a What You Know section in the encyclopedia** (Gabriel, 27/10/2026: "Et
  aussi, 'qu'est-ce qui compte le plus' pour un NPC. Ses enfants? Sa richesse? Son amoureux? etc.", then "On va le
  faire maintenant" and "il faudra penser a les indiquer dans des sections de l'encyclopedia"). Every character met is
  given one anchor (`MattersMost`, SDK, saved as an integer, append only): their children, their spouse, a lover, the
  player, their family, the honour of their house, wealth, power, glory in battle, their lands and people, their
  freedom, or a score to settle. `MattersMostPolicy` chooses it among what is TRUE of them (no children, no "my
  children"; freedom only for one of no noble house; a score to settle only with a live grudge the player may know of),
  weighed by their traits, seeded by their id; it is kept while its ground stands and moves when the world takes it
  away (a mother whose child is lost and who holds a grudge now lives for a score to settle). Read on every load
  (`MattersMostReader`, called from `HeroProfileMapper.UpgradeIfNeeded`). The prompt states it at every adult level, in
  the cacheable prefix (`matters_most` section): it weighs on what they agree to, forgive and never forgive; a guarded
  nature may keep it private, never name a false one. The player learns it when the character says it: a [DISCOVERY]
  with the reserved key `matters_most` (`MattersMostDiscovery.Key`) marks it known and is never stored as free text
  beside it. The new What You Know section (`KnownPageReader`, `EncyclopediaHeroPageKnownMixin`, CR_HeroKnown*
  prefabs; NPC pages only) then names it in the game's own words, without leaking a secret: a lover the player does
  not know of reads "someone she loves". Unlike Discoveries, whose lines the model writes, every line there is the
  game's; the pillar's later increments (gifts, fears and superstitions, pastimes) add theirs. `cr.matters [learn|forget]
  [name]`; `cr.test matters_most`. Defaults awaiting Gabriel's ruling: one anchor, not two; it moves only on an event of
  the world, never with time alone; no dark lever (threatening what matters is not a mechanic).
- **Fears, superstitions and gifts they love** (same pillar; Gabriel, 27/10/2026: "un NPC pourrait [...] preferer
  certains cadeaux. Par exemple [...] Une Dame pourrait aimer les fourrures", "nous pourrions aussi ajouter certaines
  superstitions et phobies"). Each character met is rolled once (`NaturePolicy`, `GiftTastePolicy`, `NatureReader`,
  from `HeroProfileMapper.UpgradeIfNeeded`):
  - perhaps a fear (`Fear`: the sea, fire, wolves, the dark, sickness, heights, snakes, closed places; about one in
    three, more for the timid, fewer for the valorous) and perhaps a superstition (`Superstition`: omens, charms,
    ancestors, the evil eye, dreams, rituals, the moon, iron or salt at the door; fewer for the calculating, more among
    the Battanians, Sturgians, Khuzaits and Nords), weighed by their people (a Khuzait fears closed places, a Nord never
    the sea). Beliefs, never magic. The prompt states them as colour (`fear_and_superstition`), and the player learns
    each when the character lets it show (reserved [DISCOVERY] keys `fear`, `superstition`).
  - one or two kinds of gift they love and, one in two, a kind that leaves them cold (`GiftKind`: furs, wine, ale,
    jewelry, velvet, fine cloth, horses, weapons, armour, delicacies, silver), weighed by their people most (furs and ale
    in Sturgia, horses on the steppe, wine and velvet in the Empire), then their traits and station, never their sex.
    The kind is read from the item's own category at the one place every hand-over passes (`ExecuteGiveItem`): a loved
    gift earns twice the regard its value would and is remembered as a gift after their own heart, a cold one half,
    never less than nothing; the player learns the taste by giving it. The prompt tells them how to meet a gift
    (`gift_tastes`).
  The What You Know section names each once learned. `cr.matters` shows and sets all of it; `cr.test gift_tastes`.
  Defaults awaiting Gabriel: a cold gift costs nothing; pastimes not built yet (narrated only, when they come).

### Changed
- **A parley with an enemy ruler, from the campaign bar** (timicu, Nexus, 28/10/2026: "negotiating wars, peace treaties
  [...] through natural conversations with NPCs, with successful negotiations actually changing the game world"; Gabriel,
  28/10/2026: "go"). The parley (peace, tribute, prisoners released, a hostage given, a non-aggression pact) was built and
  tested but opened only from the console (`cr.council parley <faction>`). The council picker now lists "A parley with
  {realm} ({ruler})" for every realm at war with the faction the player leads (`ParleyEntryPolicy`: a ruler or the head of
  an independent clan only, since the parley's peace and war are refused to anyone else). cr.test `parley_entry`.
- **Companions know their place, and the duel is a noble's affair** (avalanche24, Nexus, 28/10/2026: a companion reasoned
  she was "from the Rauvegroving clan of Vlandia ... loyal to her house", another told him to "take her men and prove"
  himself, a companion hired hours before and a landowner with no skill at arms each called him out to a duel; Gabriel,
  28/10/2026: "je ferais la difference entre le duel qui est noble et la bagarre de rue", "bagarre seulement pour le
  compagnon"). First step of the station pillar. A hired companion's header no longer names the player's clan and realm
  as hers ("with no noble house of your own, riding for hire in the player's company"); the station pack tells her the
  player is her captain and pays her wage, that she may argue in private but never commands their men nor asks them to
  prove themselves; the house packs say she serves the player's house rather than being of it (`HouseStandingPack`,
  `HouseMeansPack`). A duel of honour is now offered only with a noble: companions and notables, a peer since v1.31.9 (a
  player then asked to duel a companion), no longer duel with steel; they brawl instead (below).
- **Every character knows where the player stands beside them, in rank and at arms** (Gabriel, 28/10/2026: "Un paysan qui
  parle au joueur niveau 1 sera plus rude que si il parle au joueur niveau 20 Clan tier 3"; avalanche24, 28/10/2026: a
  companion "with his best stat less than 100" talked of winning a war beside a player who kills a hundred a battle).
  Second step of the station pillar. `PlayerRungPolicy` places the player on five rungs read from the one rank type
  (`Stature`) and the clan's oath: a nobody, a sword for hire (a mercenary, tier 1, or level 15 and up), the head of a
  rising house (tier 2+, a fief or an oath), a great lord (a sworn vassal with a fief, or tier 4+), a sovereign. It reads
  who fights better from the one prowess rule (`CombatProwessPolicy.Prowess`), bucketed so a skill point never moves it:
  far below, below, even (nothing said), above, far above. The station pack carries the line for everyone, Lean too
  (`EncounterContext.BesideThePlayer`, `PlayerRungReader`). `BattleFeatFacts` moved to `Mapping` so no new namespace cycle
  formed. How each station treats each rung comes with the station definitions. cr.test `player_rung`.
- **One definition per social station, meeting the player by their rung** (Gabriel, 28/10/2026: "faire une definition
  par statut social, et injecter par composition le bon statut dans le prompt", in "txt"). Third step of the station
  pillar. `ModuleData/stations/` ships eleven: ruler, lord (heads a landed house), noble (of a house, commanding nothing),
  companion, wanderer, merchant, artisan, ruralnotable (landowner), headman, preacher, gangleader (bandit chiefs too).
  Each says who the character is (`[who]`, rendered first in the station pack, `EncounterContext.OwnStation`) and how they
  treat the player at each rung, which follows the rung line (a landowner is rude to a nobody, takes his hat off to the
  head of a rising house, is afraid of a great lord). `SocialStationPolicy` resolves the station and reads the files;
  `ModuleData/Custom/stations/` and `cr_patch` apply as to every narrative file; what a station may DO stays in code. The
  companion guidelines lose "your place is not in doubt" and the familiarity of long campaigns for a new hire, and gain
  "Then you follow their order: the player is your captain". The archetypes written for lords ("A formidable lord.", "The
  Hollow Lord") are worded without lordship for everyone else (`ArchetypeWording`, the romance still reads the original),
  existing saves included; a companion's and a wanderer's rank line names them instead of "a person of some standing".
  `cr.station [name]` shows it all; cr.test `station_definitions`.
- **The street brawl beside the duel** (Gabriel, 28/10/2026: "je ferais la difference entre le duel qui est noble et la
  bagarre de rue qui est un reglement de compte", "bagarre seulement pour le compagnon"). Last step of the station pillar.
  `BrawlPolicy.KindFor`: a crown, a lord or a noble duel with steel; a companion, a wanderer, the street (gang leaders,
  bandit chiefs) and the notables of trade brawl with fists; a preacher fights no one. The same verb and the same road
  (`challenge_duel`, `DuelEligibilityResolver`): the character is taught "A SCORE MAY BE SETTLED WITH FISTS" (no standing,
  no steel, no satisfaction), the player answers "Raise your fists", and the fight is staged where the duel would be: in
  town with both fighters' weapon slots cleared as the game's own fistfight clears them (not `StartFistFight` itself: it
  re-equips both after the fight, when CR has already knocked one out and closed the scene), in the field with town
  clothes and the weapon slots cleared. A companion the player married is a spouse, never voiced as a hireling. Nobody dies, as in a duel. A brawl moves only what the two
  think of each other: no renown, no house influence, no wager, and it is not recorded as the realm's news. A commoner
  starts one only over a real grievance (a live grudge, or a regard at `Regard.WaryTier` or below), a notable of trade
  never does it himself, and one far outmatched at arms, or facing a lord when they are not the player's companion, keeps
  his fists down unless reckless (Valor 2) (`BrawlPolicy.MayCallOut`, asked when the verb is taught and again before
  anything is armed; a refused call-out is absorbed, nothing armed). The friendly bout with a companion comes back as a
  friendly scuffle. The resumed conversation after a brawl speaks of blows traded, never of crossed blades
  (`PostDuelWasBrawl`). The menu's "Call them out to a duel" stays for nobles; a brawl is called for in words.
  cr.test `brawl_initiative`. Not play-verified: whether the AI engages bare-handed in the open field.
- **Tastes in bed: a wider list, weighed by who the character is** (Hardcore only; Gabriel, 27/10/2026: "Je trouve
  interessant d'en mettre beaucoup. Ca peut amener des scenes plus subtiles et complexes", then "les kinks pourraient
  fortement qualifier ou disqualifier certains gestes" and "les traits influencent les kinks qui eux influencent les
  gestes"). Thirteen gestures join the eleven (`IntimateAct`, appended): being teased, kisses on the body, a slow
  massage, tender words, lingering in each other's arms afterwards, taking the lead, being led, being held down or
  bound, being spanked, haste, face to face, being undressed slowly, bathing together; chosen in pairs that pull against
  each other (tender words and crude ones, leading and being led, slow and urgent). Each character now loves two or
  three, is less fond of one or two, and about one in two dislikes one outright (rarely two). The roll
  (`IntimateActPolicy.Roll(IntimateTasteFacts)`) reads the kinks and romantic preferences the traits gave them: a kink's
  gestures are likelier loved and never disliked nor less loved (a masochist who disliked rough handling told the model
  two opposite things), a preference's gestures are likelier loved, and what runs against a nature (a dominant being
  led) is rarer to love and likelier to dislike, never impossible. A profile rolled before (`TastesRevision` 0) keeps
  what it loved, was less fond of and disliked, is completed with the new gestures (`IntimateActPolicy.Complete`), and
  loses a dislike its kinks contradict unless the player already learned it. The reader, the Heart line, the imposed
  memory and the prompt name every new gesture; `character_overrides.json` lists their ids. `cr.test intimate_acts`
  completes an old profile through the real load path.

### For modders
- **CrAims: lords seek the player out, and write, about their aims** (tashmetu, 14/10/2026: "nobody takes the first step
  with the player"; Gabriel, 28/10/2026: "fait 2 et 1"). The audience and letter channels another mod's aim already named
  now carry it. Audience: `InterceptionTrigger.StandingAim` (appended), the least urgent reason in
  `InterceptionPolicy.Choose`, fief-valid and road-valid, fed by `AimsBehavior.AimToRaise(hero, Audience)`; the
  invitation names "a matter of their own", the opener sends the lord to his WHAT YOU WANT section, and the conversation
  that follows asks the audience channel for his leave (`AimOpening.Channel`), recorded as an audience; a missed chase
  writes no generic letter (no recent matter spent). Letter: `LetterReason.StandingAim` (appended), one mailbox candidate
  per lord whose aim may be raised by letter (`LetterTriggerEvaluator.CollectAimLetters`, weighed like a missed meeting),
  written from `AimLetterText.Context` (want, player's part, his manner) and marked raised when released. The twice-at-most
  rule counts across the three channels. cr.test `aims_channels`.
- **A mod can forbid a conception CR would create** (tashmetu, Nexus, 28/10/2026: Traits of Calradia's Sterile
  character is told "you have no child of your blood and know you will have none", yet CR's own conception never read
  it). `CrConception.RegisterVeto(source, (mother, father) => reason or null)`; `PregnancyService.TryConceive` asks
  every veto after its own gates, forced or not (`ConceptionVetoBook`, first refusal wins, a veto that throws is logged
  and passed over, the same source replaces its veto). Documented in EXTENSION_API.md. cr.test `conception_veto`.

### Fixed
- **The letter box, Gabriel's first play test (31/10/2026)**: `cr.letter_sound` made no sound, and a test letter
  announced its arrival but never showed in the box nor on the badge. The courier's sound: the module sound did not
  resolve, and the file road used `event:/ui/default`, which took the file and played nothing; it now uses
  `event:/Extra/voiceover`, the engine's own event for a sound read from a file (vanilla plays its notification voice
  lines through it on the map), keeps the event alive while it plays, and logs the module sound's id. The test letters
  (`cr.letter_read`, `letter_long`, `letter_glyphs`, `letter_offer` and their kin) were never held in their sender's
  letters, where the box looks; they are now (`DeliverTestLetter`). `cr.letter_sound` no longer resets the box's session
  (it could drop an envelope being written). `cr.test letter_box` removes its own letters, so the box is empty after it.
  The box's "No letter waits" line was laid over the list at a fixed height, so with no letter and a silenced subject
  the two overlapped (Gabriel's screenshot); it is now the list's first row, and the subjects' heading sits left.
  The box now wears Gabriel's painted panel (31/10/2026: parchment, illuminated border, dove banner, seal) and his
  icons: a sealed envelope per letter, an open envelope for Open and a red cross for Throw away at the far end (a rule
  between them), a sealed scroll per silenced subject and a gilded return arrow for Accept again (generated to match his
  icons, 0.23 USD). His icons were cut out of their parchment ground (OpenCV GrabCut) so they sit on any part of the
  painting. The panel keeps its proportions (640 x 936 at 1080p) and every margin is a place on the painting.
- **A lord's friendly letter is no longer the same words from every lord** (tashmetu, Nexus 30/10/2026, for two players
  with Bellum Civile's collection: "a crowd of lords writing [...] as if it made them lifelong friends"). The unbidden
  StanceFavor letter (StanceConsequenceBehavior, a regard hardened into friendship) was one fixed text for everyone.
  `StanceFavorLetterText`: six fixed texts (Gabriel: "textes fixes, pas de delais ni de couts"), rotated by how many
  such letters the lord already sent and started at a stable point read from his id, so lords writing the same week
  rarely match and one lord never repeats himself back to back. None names a deed (NPCs never lie). And fewer of them: the letter now waits for a deep
  bond (`StanceConsequencePolicy.FriendlyLetterAffection` = `Regard.DeepBondTier`, 60; the Favor rung itself is 40,
  Gabriel: "les lettres d'amities devraient demander plus de regards pour diminuer le nombre"). The conversation still
  reads the Favor rung at 40; a Warning naming a real plotter is still written. The rest of the volume is left to the
  letter box (ROADMAP, 30/10/2026).
- **Give several goods at once, each in the amount you choose** (fearente, Nexus, 29/10/2026: he gave 60 grain, then
  fish and meat one by one, and had to add an out-of-character note to say how much). The Give bag now takes several
  goods; for each one held more than once, a box asks how many (it opens filled with all of it, so Enter gives the whole
  stack). The chat line names them all ("*I hand you 20 Fish, 10 Meat and Grain (worth N denars).*") and the first
  give_item of the turn hands the whole bundle over (`ItemPickerService.ShowGiftPicker`, `GiftBundle`,
  `ActionContext.OfferedBundle`); another give_item the model writes in the same turn moves nothing more. Each good
  still goes the road a single one does: a tale or a supply task it answers settles it, horses and weapons owed to a
  liege count toward the levy. The rest is ONE gift: one trust bump, one remembered kindness weighed on the whole value
  (so the second good no longer earns nothing for being "a second gift today"), one grudge eased; it is loved when it
  holds something they love, cold only when all of it leaves them cold (`GiftBundlePolicy`). A single good picked
  from the bag now leaves in the amount chosen, not one unit. The amounts only decide while the line is sent as the bag
  wrote it: a line edited before sending is the player's word (one good: the count he writes; several: a gift in words,
  confirmed good by good), so nothing leaves beyond what the sent line says. The Give entry of the manual actions menu
  opens the same bag. Gold stays its own entry. `cr.test give_bundle`.
- **A lord who asks for food takes any food** (fearente, Nexus, 29/10/2026: asked for food, he handed over grain, fish
  and meat, and only the grain counted). "food" and "provisions" were read as grain. They now ground a new `food`
  category (`ProvideGoodsPolicy.Food`) that takes whatever the game counts as food (`ItemObject.IsFood`); "grain",
  "wheat" and "corn" still mean grain. The model is taught the new category. Food brought to a hungry place he answers
  for feeds it, as grain did. A supply task already in a save keeps the category it was given (an old "food" task stays
  grain). `cr.test supply_quest` checks a non-grain food answers food and not grain.
- **A companion's backstory no longer sends him after people who are not in the world** (fearente, Nexus, 29/10/2026:
  his companion Brave believed the creditor officer of his backstory still existed and wanted to settle with him). The
  story a wanderer tells at the first meeting is kept as his own life, and nothing said its people are not in Calradia.
  It now closes, once, on "the people, debts and quarrels in this story belong to my past [...] I cannot meet them, and
  I do not set out to find them or settle with them. I may remember them and speak of them"
  (`WandererBackstoryPolicy.PastOnly`). A story kept in an older save gains the closing when profiles are refreshed at
  load. `cr.test companion_identity` checks the closing reaches the real prompt. Not built: an out-of-character channel
  for the player's quick corrections (still a ROADMAP wish, it needs design first).
- **Characters know who the others in the room are, and their own children** (fearente, Nexus, 29/10/2026: a lady
  governor spoke to her father, the head of her clan, "like he was a servant"; a ruler was treated like an ordinary
  noble; a brother spoke of his son "Lorgand" when he has a daughter). A person present was described only by how the
  speaker felt about them ("your clan member", "a lord you know"), and a companion of the player's party only as "the
  player's companion". Each line now says who they are to the speaker of the turn: kin (father, mother, son, daughter,
  brother, sister, husband, wife, grandparents and grandchildren) and rank (ruler of a realm and whether it is the
  speaker's own, head of a clan and whether it is the speaker's own, governor of a town or castle), in both prompt
  modes (`WitnessStandingText`, `WitnessResolver.WithStanding`, `WitnessEntry.Standing`). It is composed last, for
  whoever speaks, so in a group scene each voice reads the room from its own side. The family list is now closed
  for a grown lord ("..., and no other children", or "Children: none"; `ChildrenListText`); a wandering companion's
  list is left open, since their past is the backstory they told and may name children the game never made. The Compact
  prompt, which drops RELATIONSHIPS whole, now keeps the family part as YOUR FAMILY (`NpcProfile.Family`). Fixed on the
  way: a person with a memory lost their gear line in a group scene (the copy that adds the memory dropped it).
  A kinsman of the player asking for a private word is pointed at YOUR FAMILY in Compact, not at the RELATIONSHIPS
  heading Compact drops. `cr.test witness_standing`. The council's whole-table call, which writes every seat at
  once, now says each seat's kin beside its rank, of the seat: "father of Lady Ira; the player's husband"
  (`CouncilMemberInput.KinAtTable`, `WitnessStandingText.AtTable`); a council run voice by voice goes through the
  chat's road and gets the standing as above.
- **Lords no longer invent lands or wars to fill a silence** (RaveN017Mod, Nexus PM, 29/10/2026: lords and vassals
  "make up possessions they don't have or wars that don't exist"). A lord was told which towns and castles his house
  holds and which realms it fights only when there was something to list; a landless house or a realm at peace got no
  line at all, and the model filled it. Both lists are now closed: "holds X with their villages, and no other lands" or
  "holds no town, castle or village"; "at war with Y, and with no other realm" or "at war with no realm"
  (`HouseStandingPack`, SDK `963bd92`). A captive, cut off from news, is still told nothing about wars. Not changed:
  the house's standing can still be dropped from a Compact prompt when the budget binds (its place in that order is
  pinned on purpose).
- **Giving a ruler all you own no longer reads as joining her house** (RaveN017Mod, Nexus, 28/10/2026: "I assumed this
  would mean joining House Pethros [...] I specifically chose the second option: to give her everything and join
  her"). On that road the player stays head of their own house, sworn to her realm: the game always keeps the player
  at the head of their own clan. The window said "Leave {house} for Rhagaea?" with a "Leave the house" button, and
  the ruler was taught nothing against promising her house. Now (`LeaveHouseWording`): the window is titled "Wed
  Rhagaea: choose your road", its button "Take this road"; the give-her-all road says "You remain head of {house},
  now sworn to her realm; you do not join {her house}"; the ruler is told never to say they will; the note after the
  choice names the road taken. Nothing in the hand-over itself changed.

## v2.7.21.. 2026-10-27

### New
- **Leave your house for a ruler who will never leave hers** (Gabriel, 26/10/2026: "Jamais elle laissera son trone
  [...] le joueur aura le choix soit d'abandonner son personnage et continuer avec l'heritier"; RaveN017Mod asked to
  marry Rhagaea). A lady who heads her house, her heart won, may accept you on her one condition: you hand your house to
  your heir. A popup names the heir and says it cannot be undone; once back on the map, the heir becomes head of the
  house and the character you play on, and your old character, now one of the world's lords, weds her and joins her
  house, she keeping her rule. The heir inherits your standing, not your romances; she remembers why you came;
  `rhagaea_marriage.json` plays for the wedding. Needs an unwed player and an heir of age riding in your party; a
  wedding the game still refuses hands the house and the character back. New action `leave_house_to_wed` (not taught in Compact),
  `cr.leave_house_check`, `cr.leave_house` (irreversible, for a throwaway save), `cr.test leave_house_for_ruler`.
  **A second road, and the one with no heir** (Gabriel, 26/10/2026: "le Clan du joueur est transfere a Rhagaea. C'est
  aussi l'option si aucun heritier. Mais ca pourrait declencher la guerre"): give her all. You keep your character and
  your family ("le lien de sang est tres fort"); she receives your towns and castles (villages follow), your gold and,
  if you rule one, your realm, whose houses join hers; your house serves her realm, and the influence the move wipes is
  given back; you wed, each keeping your own house, and the children of the union are of her line; she pays a season
  (60 days) of your house's wages. Her people's rivals may answer with war: near certain for a realm handed over or a
  defection from a rival empire, likelier with each fief, none for a landless house; the AI makes peace when it will.
  When both roads are open, the popup lets you choose. `cr.give_her_all` (irreversible, for a throwaway save).
  When her road is closed for a reason you could mend (her heart not yet won, a war or a captivity, an army or a
  battle), she says so instead of meeting your resolve with silence. The popup says plainly when you give up a crown,
  how many fiefs you bring, and how likely her people's rivals are to answer with war.
  Through the heir, your old character keeps his life (Gabriel, 26/10/2026: "transferer les souvenirs au 'nouveau'
  npc"): his weightiest deeds from your chronicle, in order, why he left the house and to whom; and the ruler keeps her
  romance with him as her own life, by his name, instead of losing it to the succession. A wed player must first part
  from their spouse (`end_own_marriage`): she would never be a second wife.
- **A tale's conversation can be paced beat by beat, and The Empress's Summons is** (Gabriel, 25/10/2026: "ca pourrait
  etre trop court ou trop long", then the 3/3/3/5 night). A `talk_to` with `"when": "closed"` may carry an `arc`: beats
  played in order, each for its number of exchanges, with its direction and who leads; the character is told each
  reply which part it plays, and on its last reply that this reply ends the scene; the chat then closes softly (Leave)
  and the stage completes as before, whatever the MCM says of characters ending conversations. `max_exchanges` caps
  it. Logic `TaleArc` (tests `TaleArcTests`), loader checks mirrored in the tale editor with five parity fixtures, arc
  cards in the editor, run state saved (`ArcBeatId`, `ArcBeatExchanges`, `ArcExchanges`, `ArcClosed`), HOWTO_Quests.
  The Adult Tales' Rhagaea night plays 3 preliminaries, 3 where she leads, 3 where the player leads, 5 of talk in which
  she asks for the secret. `cr.test tale_arc`.
- **The arc turns on what happens** (Gabriel, 25/10/2026: "melange des deux, signaux observes avec tirage en filet";
  "je garderais une intervention forte qui retient"). An arc declares `signals` its beats `listen` to; whoever reads
  the turn's actions reports one with the new `tale_signal` verb (taught to the interpreter in Enhanced, to the
  character's own call in Standard, like a tale's choices by words), the game takes it only while a beat listens, and
  it sets the tale flag of its name. A beat's `next` goes where the first holding `if` says; `on_signal` jumps at once;
  a signal's `net` lets the game decide it after enough listening, and the character is told to narrate it. The
  player's strong turn (`turned`, the game's own signal) holds the beat once. An arc that can loop needs
  `max_exchanges`. The Rhagaea night now plays Gabriel's whole shape: if she reached her climax, the talk before dawn;
  otherwise she takes the initiative again (the net decides at six exchanges of listening, one chance in two; capped
  at 30). The editor has the signal and transition cards; two more parity fixtures. Also: an Enhanced turn redone by
  the prose fallback now carries the turn's action blocks (choices and signals) in its prompt, since that one call
  reads its own tags (`EncounterContext.ProseReadsItsOwnActions` is now settable); the arc's close now locks the input
  with a notice, as an ordinary close does. `cr.test tale_arc` drives signals through the real bridge and a forced
  net roll.

- **Two tales at once: a scenario's tale sets the one under way aside** (Gabriel, 25/10/2026: "je fais la quete du
  loup, mais je suis fait prisonnier par Rhagaea.. je devrais pouvoir vivre la nuit avec elle et reprendre ensuite la
  quete du loup lorsque je suis libere de prison"). The tale under way waits, its clock stopped and its journal entry
  open, while a tale a scenario opens plays; it takes up again once that is over and the player is free. One level
  only; a tale raised in conversation waits for both. `cr.story_status` shows the tale set aside. `TalePause`, save key
  `CalradiaRemembers_TalePausedRun_v1`. A summons is no longer barred by anything of the giver's own, an errand on offer
  or a quest of theirs under way. cr.test `tale_interrupted`.
- **The Empress's Summons, continued** (Adult Tales, Gabriel 26/10/2026): the lady first takes the prisoner to a small
  warm bath, washes them and dresses them in clean night clothes, then leaves them at the Empress's door, "sa tache se
  termine ici"; turning back at either is an ending of its own (`turned_back`). The chamber now opens on the scene's dark
  backdrop with no vanilla conversation behind it (her armour and her guards in the street, her vanilla greeting under
  the video of her bed), the picture large, and she speaks first. A second night with her follows the first: she
  remembers the other nights and does not greet the prisoner as a stranger.
- **A tale remembers its earlier playings with the same giver** (Gabriel, 26/10/2026: "il faudrait que Rhagaea se
  souvienne des autres fois et que ca soit une continuite"). `TaleHistory`, save key `CalradiaRemembers_TaleHistory_v1`:
  a run begun again with the same giver carries `played_before` and `ended_before:<ending>` for its lines to test.
- **Characters have gestures they love in bed, and some they dislike** (Hardcore; Gabriel, 26/10/2026: "le LLM a
  tendance a aimer tous les gestes sexuels que le joueur peut decrire [...] un NPC pourrait particulierement aimer le
  sexe oral mais detester le sexe anal"; a refusal "pourrait etre franc, ou encore un malaise"; then 27/10: "au moins
  quelque chose que le personnage aime beaucoup, et une chose qu'elle aime moins"). Each character is given once one or
  two gestures they particularly love, exactly one they are less fond of, and about two in five one they dislike
  outright, among kissing, oral given or received, anal given or received, on top, beneath, from behind, hands and
  fingers, rough handling, crude words. They seek what they love; what they are less fond of they go along with
  without eagerness; what they dislike they refuse plainly or with an unease the player can read, never with feigned
  pleasure. Rolled from the character's id with a stable seed (`IntimateActPolicy`), kept
  in the save (`RomanticProfile.LovedActs` / `DislikedActs`); characters met before are given theirs the next time
  they are spoken to. Whoever reads the turn's actions (the interpreter, or the prose call in Standard) is taught,
  only at Hardcore in a lovemaking the game permits and never in a captive scene, the gestures this character loves or
  dislikes, and reports one the player does or asks for (`intimate_act`). `LessFondActs` is met without eagerness, at
  no cost, and learned all the same. A disliked gesture imposed costs 4 personal
  regard and leaves a memory, once a day per gesture; only asked for, it costs nothing; a loved one found the first
  time warms them by 1 (`DeedRegardPolicy`). Either way the player has learned that taste (`KnownActs`), and the
  character's Heart section names it, since the player may forget (Gabriel 27/10): "In bed she loves being on top, and
  does not like being handled roughly." (`HeartIntimacyPolicy.TastesLine`); never a taste not yet learned.
  `character_overrides.json` takes `loves`, `lessFond` and `dislikes`. `cr.acts [name]`, `cr.act <gesture>
  [performed|proposed] [name]`, `cr.test intimate_acts`.

### Changed
- **`cr.testall` is gone: `cr.test all` runs every self-test** (Gabriel 27/10: "cr.testall avec une commande fait tous
  les tests quand meme", then "Enleve testall. Remplace-le par cr.test all"). A name typed after `cr.testall` was
  ignored and the whole sweep ran. Now one command: `cr.test all [budget]` for the sweep (the number is the
  conversation tests' budget in seconds), `cr.test <name> [name ...]` for one or several, `cr.test list` for the
  names, `cr.test chat | mech | full` as before. The help, the guides, the docs and every self-test description say
  `cr.test` now; past changelogs are left as published.
- **A guard holds every mention of a console command to what the console accepts** (Gabriel 27/10, on how the
  `cr.testall <name>` mistake could be prevented: "je tente de voir comment on peut prevenir ca"). The suite's
  `ConsoleCommandMentionsTests` reads the code (messages and comments) and the guides against `ConsoleCommandHelp`: a
  command a message or a guide tells you to type must exist; a command that takes nothing is never written with an
  argument; `cr.test <name>` names a real self-test. On its first run it found four messages telling the player to
  type `cr.chat_reset`, which does not exist: they now say `cr.reset`. Also fixed: the help named the gone
  `cr.letter_deliver` and `cr.chat`, `cr.courier_test` declared no argument though it needs a settlement, and the
  console guide gave `cr.missions [name]` and `cr.model [toggle]`, which take nothing.
- **The Adult Tales have their own git repository** (Gabriel 27/10: "Calradia Remembers Adult Tales", beside CR,
  github.com/LogRaam/Calradia-Remembers-Adult-Tales), history kept. `Extras/CalradiaRemembersAdultTales` is gone from
  CR, with its installer and `Tools/IntimateTales/wife_tales.py`, which moved there (`python Tools/install.py`). CR's
  tests that read the tales look for that sibling folder (`SourceTree.AdultTalesRepo`); makezip and verifyzip still
  refuse every adult tale file.
- **The player's violence in words works in both compositions** (Gabriel 25/10: "enseigne la violence aux deux
  modes"). rob_npc, assault_npc and kill_npc were taught to the interpreter alone (PlayerViolenceVerb kept them out of
  the prose prompt so no conversation was invited to violence), so in Standard composition a robbery, a beating or a
  killing in words was never carried out. `ProseActionVocabulary` now lists them from their catalogue spec; the bridge
  re-checks each as before (ViolencePolicy, the killing option). Found by the new composition-parity map
  (`docs/maps/composition-parity.md`, `CompositionParityTests`).

### Fixed
- **A witness remembers what they agreed to, and what they said as a witness** (LapitaPolata, Nexus 27/10: "I asked an NPC
  X to back my story, they agreed [...] Then I talked to another person Y and invited X as a witness, and X behaved like
  they knew nothing about my scheme [...] they remembered everything except for the conversation between I and Y").
  In the compact prompt a witness's recall was cut to fit by keeping its OLDEST memories, so the fresh agreement was the
  first thing lost; the cut now keeps the newest. And a witness who spoke in a conversation now keeps it, in their own
  voice, by the same summary queue as the one addressed; a silent onlooker keeps that they were there (`CouncilMemoryPolicy`).
- **Letters, family and council, as DepaHN found them** (Nexus 26/10, a month into one campaign):
  - "a lord in the north whom I've never met keeps writing to me [...] about rebellions and wars in the north that have
    nothing to do with me": news of the realm now comes only from someone you have met, and only of what touches you,
    your realm, your kin, the country around you, or the writer's own house (`RealmTidingsRelevancePolicy`).
  - "relationships with children and grandchildren feel very cold, as do those with brothers, sisters, and relatives":
    kin were told they are family only until they shared a first memory with you, and grandchildren, nephews, nieces
    and cousins never. Every prompt now names the bond ("You are the player's granddaughter: their blood and their
    family"), Lean included, as it does for a spouse (`PlayerKinshipPolicy`). `cr.test kin_identity`.
  - "the council is completely cut off from memory": only the lord you addressed kept the sitting. Every member seated
    now keeps it too, summarised in their own voice by the same durable queue (`CouncilMemoryPolicy`); an onlooker to an
    ordinary conversation keeps that they were there, as before.
  His other ideas (orders to armies, moving food between towns, envoys with terms, alliances, requesting a meeting,
  arbitrating lords' quarrels, war tied to relations, events read from the campaign's history, a reader per faction)
  are on the roadmap.
- **Brown hair is no longer described as red** (Windmolen, Nexus 27/10: "my character, according to the description, has
  red hair. He does not", in two campaigns). The player's appearance named a colour red whenever it was warm and
  saturated, asked before the browns, which almost every brown is. Red is now kept for a true red, a reddish brown is
  auburn, and the rest are the browns they were (`HairColorBands`, tested on sample colours). `cr.test appearance`
  prints the player's hair word and its raw offset.
- **A hired companion knows what they are, and keeps the story they told you** (fearente, Nexus 26/10: "a companion that
  I hired a week ago talked like we were of the same clan from childhood [...] If there is an easy way to include their
  vanilla stories to themself when met for the first time it would be nice"). The game counts a hired companion in your
  clan, and the prompt said only that. Now a companion is told they joined your company as a hired companion, some N
  days ago (the day is kept from now on, on every hire), with no blood, house or childhood shared with you. And the
  story a wanderer tells at your first meeting, line by line, is kept once as a memory of their own life, in their own
  words (`WandererBackstoryPolicy`); a refusal to speak of the past is not kept as a story. `cr.test companion_identity`.
- **Scenes of a tale** (Gabriel's play test, 26/10/2026): typing the number of "Something else..." sent the number to
  the narrator ("3", read as "You say nothing"); it now leaves the input to you, and once you choose it or write your own
  words the numbered choices stay gone for the rest of that scene. The narration is signed "Narrator", not the scene's
  name ("The Cell Door"). A scene that ends by itself, and a tale's arc that closes, say "Close this conversation to
  continue...". A conversation a scenario's tale brings to the player sits on the scene's dark backdrop, with no vanilla
  conversation, and the character speaks first (that line is not an exchange of the arc). cr.test
  `summoned_meeting_on_backdrop`.
- **Grain and other goods a lord asked for count however you hand them over** (froggyluv, Nexus 26/10: "sometimes the
  flavor text will say i delivered but the NPC still complain of my broken promises"). Only the chat window's bag
  button settled a supply task; goods handed over in words, or through the Give menu, left your party while the task
  stayed open and expired as a broken promise. Now a gift of the goods the task asks for settles it the same way, the
  exact count leaving and the reward paid; short of the count nothing leaves (`SupplyGiftPolicy`). `cr.test
  supply_quest` now hands grain over in words too.
- **"Never Mind" no longer ends the conversation** (Windmolen, Nexus 26/10: closing the "Speak With Your Own People"
  window left him unable to reach a private conversation). The meeting the player came for opens instead, as "A word
  with you" would.
- **A lady who heads her house no longer "marries" you in words while nothing is sealed** (RaveN017Mod, Nexus 26/10:
  "I want my hero to marry Rhagaea [...] it feels like it's just not triggering"; Gabriel the same day: "Jamais elle
  laissera son trone"). The game weds no two heads of houses, and you always head yours: its marriage refused in
  silence while the mod announced the wedding, so `rhagaea_marriage.json` never fired. Such a lady is now told why
  (`HouseHeadMarriagePolicy`, a new `LeadsTheirOwnHouse` obstacle): she keeps her house and says what she can offer
  instead, to be lovers, a bond owned openly, or that you leave your own house to join hers. And any marriage the
  game will not seal (an army, a battle) is now refused plainly instead of announced. `cr.test marriage_house_heads`.
- **OpenRouter credits Calradia Remembers again** (fearente, 24/10: the activity page showed every call under another
  app). OpenRouter now reads the app's name from `X-OpenRouter-Title`; the SDK sends it along with the older `X-Title`.
- **A consort who accepted sharing you no longer holds a grievance over your other consort, but still minds being
  second** (Zivkyn, Nexus 25/10: "I took them both as Consorts, And they both agree no jealousy, Yet one always ends up
  with a Greivence"; Gabriel the same day: "si elles acceptent, elles devraient etre plus tolerantes a la jalousie.
  Mais si le joueur passe beaucoup plus de temps avec une que l'autre, la jalousie s'applique quand meme... accepter
  de partager ne veut pas dire accepter etre la deuxieme"). Every intimacy used to estrange a partner and plant a
  Jealousy grudge, whatever she had accepted and whoever the other was. Now a partner on a sharing track (her people's
  custom, or her own word through accept_other_partners) bears the player's love for another partner of the household
  (the spouse or a committed consort) with the lighter regard price only: no estrangement, no grudge
  (`JealousyWoundPolicy`). A liaison outside the household still estranges her and is nursed. And a partner who
  accepted against her custom now takes part in the household's competition: neglected far more than the favourite,
  she feels slighted like a woman whose people share (`CompetitionSelector`). Grievances already on a save stay
  (`cr.grudge_resolve`). `cr.test consort_tolerance`.
- **Edicts move taxes again under a mod that replaces the tax model** (tashmetu, Nexus 25/10, from a 1.5.5 beta log
  with Harvest and Production: "[EDICT] could not attach to CalculateTownTax [...] So edicts never move taxes"). The
  patch took the method as seen from the active model's own type; a model that inherits it without overriding it
  got refused by Harmony. Edicts and the courier speed floor now patch the method where it is declared
  (`DeclaredPatchTarget`), the one the model really runs. cr.test `edict_effects` now also requires all six
  channels attached.
- **A tale's choice by words can now be taken in Standard composition.** Its open choices were taught only to the
  action interpreter (Enhanced), so with one call per turn the character's reply could never report one and the tale
  waited on a path nobody could take. Gabriel (25/10/2026): Enhanced was built to reproduce Standard, then "je
  confirme" to teaching the open choices there like any other open deed. On a turn whose own reply is read for
  actions, the prompt now carries them below the `CURRENT ENCOUNTER:` marker (Compact included), with the rule that
  they are for the [ACTION] block only, never to be announced, offered or steered toward. In Enhanced nothing changes:
  the interpreter is taught them and the character is not. The action-reader decision is now taken before the
  context is built (`EncounterContext.ProseReadsItsOwnActions`). Not covered yet: an Enhanced turn redone by the prose
  fallback reads its own tags from a prompt built without the choices (closed since, see the arc's signals above). The
  parity map's `story_choice` debt is gone.
  `cr.test authored_quest_said_choice` now checks the real prompt in both modes.
- **A wife is no longer narrated as "he", and gestures stop leaning on furniture** (Eisendrache2, Nexus 25/10,
  LM Studio with qwen3.5-9B: "My wife is speaking to me and the AI is describing HIS actions", "Like leaning on a
  table"). The compact prompt for small models kept one gesture example, "*He leans back in his chair*", for every
  character, and a small model copied it. Every gesture example (dialogue style, narrative voice, the complete reply
  example) now follows the character's own gender, and the compact one touches nothing. A spouse is now told in
  every prompt that the player is their own husband or wife; before, only on an empty ledger or in the intimacy
  rules that Adult Off removes. From his log, the same small model also told its own gestures in the first person
  ("*I lean against the post*"): the compact prompt had no third-person rule (the Full one does), and the only person
  rule it kept was the memory summary's "first person". It now asks for third-person stage directions, never "I",
  in fewer characters than before. SDK `GestureExampleAndSpouseIdentityTests`.
- **A failed action reading is tried once more** (Gabriel 25/10: "reessaie une fois l'interpreteur en ajoutant un log
  qui le specifie"). In Enhanced composition, when the action interpreter's call failed, timed out, came back empty or
  could not be parsed, the reply's deeds were lost with a log line only. It is now asked once more
  (`InterpreterRetryPolicy`, at most two calls; a reading that found no deed is never retried), and each retry and its
  outcome is written on the Compat log channel, which is on even when logging is off. cr.test `interpreter_retry`.
- **Duels and the captive's escape ready for the 1.5.5 beta** (tashmetu, Nexus 25/10, from a check of every mod DLL
  against the beta): `Mission.SpawnAgent(AgentBuildData, bool)` gained two optional parameters in 1.5.5, so the
  two-parameter call compiled against 1.4.8 would throw `MissingMethodException` as the first fighter spawned.
  `MissionSpawn.Spawn` now finds the overload at run time (`SpawnAgentSignaturePolicy`: public, on the mission,
  AgentBuildData first, every other parameter optional, the fewest parameters winning) and fills the rest with their
  defaults; on 1.4.8 it calls the very same method. Used by `DuelMissionLogic`, `CaptiveEscapeMissionLogic` and
  `DuelSpikeMissionLogic`. cr.test `spawn_agent_signature`. Not run on 1.5.5 itself.

### For modders
- **A tale a scenario opens, judged by what it left** (Gabriel 24/10, for the night Rhagaea sends for her prisoner).
  Scenarios: trigger `ON_CAPTIVE_NIGHT` (once a night, at 23:00, while the player is a prisoner); guards
  `IF_CAPTOR_IS:hero` (the player is held by that hero's house, in a town or castle it holds or a party it leads),
  `IF_PRESENT:hero` (that hero is where the player is held), `IF_CHANCE:p` (a roll below p; no number, never);
  consequence `OPEN_TALE:tale,giver` (a tale with `on_scenario`, by its popup, its own rules holding). Tales:
  `on_scenario` (only a rule opens it; never with `on_map`); `talk_to` with `"when": "closed"` completes as the
  conversation ends, so all of it counts; the game sets `regard_rose` / `regard_fell` as a tale ends, from the giver's
  regard when it began (`AuthoredQuestRun.GiverRegardAtStart`, `TaleRegardFlags`); consequence `regard` (`who`,
  `amount`, under `if`); a stage's `image_pool` shows one picture or video not yet seen each time the tale is played
  (`TaleImagePool`, shown pictures kept in the save, additive key).
  Editor, schema, HOWTO_Quests, EXTENSION_API and the Scenario Composer updated; two editor fixtures. cr.test
  `tale_opened_by_scenario`. Engine spike `cr.captive_talk_probe` (24/10: a conversation opens while the player is held
  in a town's dungeon and the captivity survives it; the backdrop is the map conversation's throne room).
- **More for scenario tales** (Gabriel 25/10, the night Rhagaea sends for her prisoner):
  - `on_scenario.decline_ending` names an ending the popup's no plays at once, its regard, memory and cooldown with it,
    instead of the tale passing for a season; the loader refuses an ending the tale lacks.
  - A scenario tale with no invitation begins at once, and a `talk_to` stage in it opens that conversation by itself
    once no chat is open: the summons is played as a narrated scene, no popup.
  - A stage's `image_pool_pick`: `unseen` (the default, one not shown yet) or `random` (any, a repeat included).
  - A `talk_to` completing as the conversation closes lets its giver speak the stage's canvas and facts all through.
  - `ON_CAPTIVE_NIGHT` fires from 23:00 on, once a night (a tick read at 22.9999 could miss it).
  Editor, schema and HOWTO_Quests follow. cr.test `tale_opened_by_scenario`.
- **Tales may test what they already lived with this giver**: `played_before`, `ended_before:<ending id>`
  (`ended_before:reward`, `ended_before:failure`), set by the game as the tale begins again with the same giver; the
  loader and the editor count them as set. HOWTO_Quests documents them.

### Developer tools
- **CrvidPlayer converts and recompresses to an SSIM level** (Gabriel, 27/10/2026: a .crvid four times its .mp4; "on peut
  en fait mettre une option de recompression avec un niveau de SSIM a partir du player [...] glisser un .mp4 dans le player
  devrait le convertir en crvid"). Drop or open a .mp4/.webm/.mov and it is converted from its source frames, each at the
  lowest JPEG quality that keeps the SSIM level (luma, 7x7 box) against that frame, fitted in 768x1152 at 30 fps, saved and
  listed; "Recompress" does the same to an open .crvid's own frames. Default 0.960, today's quality on the_door, which the
  estimate puts at 8.3 MB instead of 12.7.
- **Tale Editor:** Save writes back into the file being edited (browsers with the File System Access API; Firefox
  keeps the download). A click on a stage opens a read-only summary under the story map, resizable: objective and how
  the stage is completed, a scene's opening and closing as beats, what each character knows in full, choices,
  outcomes, the checker's problems first. A stage an arc paces shows its beats, signals and jumps, and an ARC badge
  on its node.
- `cr.tale_open <tale id> <giver>` opens a scenario tale now, through the same door as the rule's `OPEN_TALE`.
- `Tools/Video/CrvidPlayer.html` plays `.crvid` videos in the browser (files or a folder, frame by frame, speed).
- The wire snapshot carries the request itself, so the cost bench capture can be replayed outside the game.

### Internal
- One junction decides who reads a reply's actions (`ActionReaderPolicy`, one path for actions, step 2); the
  composition-parity map renders it from real calls.

## v2.7.20.. 2026-10-24

### New
- **Grok 4 no longer thinks at Low** (cost bench 24/10: Grok 4.20 thinks as long at Minimal, Low or High, about 1,800
  hidden tokens a reply, and that thinking was most of its price; only Off stops it, at half the price and about five
  times faster, and the writing held). On a Grok 4 model, Reasoning Effort Minimal and Low (the default) now mean Off;
  Medium and High still let it think. Other models keep their levels.
- **Your OpenRouter activity page names the mod** (it filed every call under another app, "AI Writing Companion",
  because OpenRouter groups apps by web address and the mod sent its Nexus page's). The mod now sends an address of its
  own.
- **The interpreter now defaults to DeepSeek V4.1 Flash** (cost bench 24/10, the whole action catalog of 296 cases on
  each candidate). DeepSeek V4.1 Flash read 291 right, invented no deed and fell for no false lead, at about $0.0004
  and under a second per reply; Grok 4.20, the old default, cost about ten times more for fewer right readings. A
  new install starts on it; if you already saved an Interpret model, yours is kept (to switch, type
  deepseek/deepseek-v4.1-flash in Interpret model; a blank field reuses your PROSE model). Pointed at your own server, an untouched field still sends nothing, as before.
- **What a call costs, measured** (fearente 24/10: "$3.19 without particularly heavy usage" in a week the page priced
  at about $1, and no way to tell which calls were the mod's). On OpenRouter every call now carries the mod's name and
  asks for its bill; a retry is counted with the attempt it replaced
  (it used to report the retry alone). Developer tool `cr.cost_bench [plan|run] [cap]` (Gabriel 24/10: PROSE and
  INTERPRET measured apart, by length of conversation, quality beside cost), four passes from
  Configs/CalradiaRemembers_cost_bench.txt: [prose] each model plays the opening for real in Enhanced with no
  interpreter (fearente's ships and reassurance), then answers in the middle (turn 8) and late in a long conversation
  (turn 18, ten exchanges folded into a recap) over a synthetic history the same for every model, each after a
  warm-up reply so the cache is as warm as in play (Gabriel 24/10: three points instead of twenty replies), then
  writes a letter (signals for repeats, refusals, sermons, Earth references, and whether turn 18 recalls what only the
  recap holds); [standard] the same in one call;
  [interpret] each model reads the whole cr.action_bench catalog (about 300 replies; a number asks for a sample) plus
  ordinary replies, by difficulty (no action, simple, oblique, false lead), scored with a 95% interval, deeds invented
  counted apart; [fold] each Background model folds the same recap. Then the prose x interpret
  grid by addition (the two calls share nothing), in dollars and in seconds the player waits (Gabriel 24/10: response
  time beside cost). Each prose call's cacheable prefix is compared with the previous one of its row and any change is
  named, where and how (Grok read 128 cached tokens of 16,000 on most calls of the first run although Grok caches a
  stable prefix reliably, probed). Nothing saved, no letter sent, settings restored, a spend cap (default $10). Tables in Markdown and as [code] blocks for the Nexus page with the mod version and date, every billed
  call and the transcripts. Not part of cr.testall (spends credit).
- **Counsel, not sermons** (fearente 24/10, on Gemini 3.8 Flash and Gemma 4: a harmless decision met "These lands have
  swallowed many arrogant people like you"; told of two Dromons bought at Diatma, his wife lectured on pride twice,
  through his reassurance). Every station's behavior guidelines (lord, companion, gang leader, notable, wanderer) and
  the built-in fallback now say: an ordinary plan meets interest, a practical question or an honest view said once; a
  danger is named only when real, and once the player has answered it his word is taken; never a lecture on pride or
  arrogance; a spouse, kin or old friend is on the player's side even in disagreement. Found by tracing the prompt: the
  files licensed friction ("judge by deeds", "name the danger", "no yes-man") and nothing bounded a warning. The
  Compact fallback (no file loaded) is left as it was, for its budget; the shipped file is what Compact sends. The
  guidelines file grows by ~470 characters (budget constants moved in both repos, Lean guard green).
- **Romance status for other mods** (LapitaPolata 22/10: "I managed to have a secret lover, the relationship only
  exists in my and her memory. Affairs of Calradia does not know about it"; Gabriel 23/10: a door, the bridge does the
  rest). `CrRomance.GetStatus(npcId)` reads a character's bond with the player as one word (`RomanceBondPolicy.Read`,
  over the Heart page's own facts so a bridge never contradicts the encyclopedia) with its day and open terms;
  `CrRomance.StatusChanged` announces a change (`RomanceBondWatch`, read when a conversation closes and hourly by
  `RomanceBondWatchBehavior`; a loaded save's bonds are learned, not announced; `DuringSelfTest` marks a test's change).
  `HeartPagePolicy.Dates` is public for it. Not done: writing into any other mod. testall `romance_status_door`.
- **Characters set the scene** (Lordfadooboo 21/10: "instead of doing a cold open... the ai would set the stage a
  little before your first word"; Gabriel 22/10: by default, as captive scenes already open themselves). A conversation
  the player starts opens with the character: what they were doing as he came, whether they notice him, their first
  gesture or words. The short greeting shows at once and the scene replaces it; the player may type meanwhile, and a
  message sent first sets the scene aside (the greeting stands, his words open the scene). A failed call leaves the
  greeting, never an error line. Not for an interception, a tale meeting, a conversation picked up from earlier today
  or one that follows the game's own dialogue. One more model call per conversation: MCM "Characters Set The Scene"
  (Content, on by default) turns it off (`SceneOpenerPolicy`, `ChatViewModel.SceneOpener`). cr.testall `scene_opener`.
- **What a character knows of the player's name** (Gabriel 21/10, after reading another mod's notoriety per culture).
  A character who has never met the player is now told how far the name has reached them: never heard of it, a name
  heard, known by reputation, or famous ("you would know the banner anywhere"). It reads the player's renown, whether
  he rules a realm, and the listener: a lord hears more than a notable, a notable more than a wanderer; the player's own
  realm and culture talk of their own; deeds done in the listener's realm carry the name there. Once known by
  reputation, the name carries what the game says of the player (merciless, merciful, keeps or breaks his word). A
  character who heard of one of the player's deeds knows him by reputation at least, and the first impression is
  unchanged. Nothing is saved (`PlayerFamePolicy`). cr.testall `player_fame`.
- **Edicts** (Gabriel 21/10: "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre conteste."). A ruler's lasting,
  bounded decision on one town or on every town of a realm, from a closed list of eight, each a gain bought with a cost:
  tax relief, heavy levy, doubled watch, militia muster, grain requisition, market charter, curfew, feast days. Applied
  through the game's own loyalty, security, prosperity, militia, food and tax, so the town's tooltip shows a line named
  for the edict. 30 to 90 days, 25 influence for a town, 100 for a realm; one per town at a time, the town's own over its
  realm's; ten days' rest between two. It lapses when whoever decreed it loses the town or the throne. A burdened town
  under 40 loyalty grows a grievance: it contests the edict openly after 10 days and forces it off after 25. With the new
  MCM switch "Edicts of the world's lords" (off by default) the world's lords answer their towns' sorest needs and rulers
  a need most of their towns share, seldom, at most two per realm. Every decree, contest and repeal is realm news.
  The player decrees through a member of his house, the governor of one of his towns, or, when he rules, a lord of his
  realm (`enact_edict`): he is asked first, "Decree the Bread Edict (a grain requisition) for Pravend: loyalty -1 a day,
  food +4 a day, for 60 days, at 25 influence?", and nothing is proclaimed before his yes. The people of a town, and its
  lords, know the edicts where they live: who decreed it, what it does, how long it holds, whether they grumble or
  contest it, and one they forced off. `cr.edict` lists them and pulls the levers; cr.testall `edict_effects`,
  `enact_edict`. For Compact users: each action's description in the interpreter is cut at 86 characters (was 88).
- **Companions remember their own part in a fight, and sieges are news** (chmidt 23/10: companions "can't remember
  the last battle you fought with them, how many kills they had or if they were downed", and a lord would not believe
  his city had been besieged, "only knowing who owned the city afterwards"). Every hero present now has their own
  part counted in the fight (troops felled, foes of name bested, struck down and by whom), and their memory of it ends
  with it: "My own part: I cut down seven of their men and bested Lucon, before Ira struck me down." For great battles
  (lords who fought, companions in the party) and for small fights alike, where the companion's folded "small fights"
  line now ends with their part in the last one (it was lost before). An auto-resolved battle has no one's deeds to
  tell; a companion it left wounded says only that. A foe is slain only when the campaign says dead. A siege begun and
  a siege lifted are now news, naming the besieger and both realms, which the two realms and the country around it
  hear of; a capture was already. cr.testall `battle_reader`.
- **Lords know who sits in their dungeons, and the player's prisoners remember** (chmidt 23/10: "Lords and ladies
  should also know who I put in their dungeons, which they refuse to acknowledge if I tell them that I put an enemy
  lord in their own town dungeon"). A lord's own seat now says who sits in its dungeon, at most three of rank, and
  "left there by Arwa" when the player did, in Compact too; and who lays siege to it, by name. A lord the player
  takes remembers being taken ("Taken prisoner by Arwa after the battle near Pravend"); a prisoner the player leaves
  in a dungeon remembers it, and so do the head of the house that holds the place and its governor ("Arwa left Lucon
  of the dey Meroc in my dungeon at Hargard"); when such a captivity ends the captive remembers how (ransomed, peace,
  freed in battle, escaped, set free). Where the player left each prisoner is kept in the save (additive key).
  cr.testall `prisoner_memory`.
- **A tournament is remembered by the town that held it** (chmidt 23/10: "the lady of Hargard couldn't even remember
  that I won a tournament at her city, with her completely forgetting that there was even one to begin with"). Only
  the heroes the player faced kept a memory, and the player's victory was news no one could hear (no place, no realm).
  Now the lords and ladies of the house that holds the town, if they are in town, keep "I watched the tourney at
  Hargard, where the player took the crown" (or where the player fell), as do the lords and notables in town the player
  already knows; at most eight, the holding house first. The victory carries the town and its realm, so the realm and
  the country around hear of it. Each fighter's memory of the lists ends with their own part ("I put two opponents out
  of the lists and bested Lucon, before Ira put me out of the lists"). cr.testall `tournament_audience`.
- **Nights in the Heart section, at Hardcore only** (Gabriel 23/10: consensual nights "apparaissent sur la page du
  joueur", "les actes que le joueur a fait subir peuvent apparaitre", "les scenes forcees subies par le joueur ne sont pas
  affichees", sober "du genre de quelque chose que le joueur aurait pu ecrire dans son journal intime"). A character's
  page counts the nights shared with the player, the first and the last ("You have shared three nights together, the
  first in spring 1091, the last in autumn 1093"); the player's page has a line per partner. Never the content of a
  night. The nights are the character's own intimate memories, which compression always keeps, a day counted once
  (`HeartIntimacyPolicy`). `cr.heart_seed` takes `forced`; cr.testall `heart_intimacy`.
- **Misdeeds, a section of the player's own page** (Gabriel 23/10: "Heart doit etre une section sur l'amour et le
  desir. L'evenement 'take by force' devrait etre une autre section avec des actions reprehensibles", then "trahisons,
  extortions, et tout autres gestes qui pourraient etres classes comme misdeeds", "divorce est un choix de vie. adultere
  est un misdeed"). What the player did of their own will, one line per victim and deed, counted and dated: "You took
  Phenoria by force while she was your prisoner, twice, the first in spring 1091, the last in summer 1092", "You had
  Derthert put to death", "You killed Ira outside of battle", "You broke your word to Lucon on a task he gave you",
  "You handed Monchug over to his enemies while he was your prisoner", "You were unfaithful to Ira", "You lay with
  Lucon's wife", "You took Lucon's wife from him", "You forsook your oath to Vlandia", beatings and robberies. The
  captive scenes (force, torture, interrogation under duress, torment) show at Hardcore only, the rest at every level.
  Each kind comes from one source so a deed is told once: the Trauma ledger (the same records each victim's Trauma
  section reads), the game's own death log, the tasks the player gave up or let lapse (not a bandit lair that could not
  be found), and a new misdeeds ledger (additive save key) for adultery found out, a captive handed to his enemies, a
  realm forsaken and a murder of a man of no house; on older saves the grudges of a captive handed over and of a spouse
  taken stand in. Adultery and realms forsaken count from this version on. Never what a captor did, nor what a captor
  forced the player's hands to do; a divorce is a life choice and stays out. Extortion and the raiding of villages are
  not recorded anywhere yet. `MisdeedsPagePolicy`; cr.testall `misdeeds_page`.

### Changed
- **Friends, enemies and houses as rows on the encyclopedia page** (Gabriel 22/10: "une liste ou chaque ligne serait
  avec le portrait, le statut et la raison", a pale green marker for friends and red for enemies, the house's banner
  instead of the portrait). Each friend or enemy with a reason is a row: the game's own portrait (it opens their page),
  the status ("Manteos, enemy"; on the player's page how they hold him, "Deep affection (+42)"), the reason beneath. The
  houses: one row per reason they share, their banners side by side (each opens the house's page), "at war" or
  "through" a lord under the banner it belongs to; affinity rows first. The colour replaces the "At odds" / "Of
  affinity" subtitles. The game's Friends and Enemies grids below are unchanged. `ClanRegardListPolicy.Groups`,
  `RegardPersonRowVM`, `RegardHouseRowVM`; cr.testall `npc_regard_reason` extended.
- **Every deed moves regard, for the victim and the family** (Gabriel 23/10: "il faudrait que pour chaque action il y
  a un impact sur l'estime, qu'il soit positif ou negatif. Car je pourrais complimenter un NPC et augmenter son estime,
  pour ensuite commettre un mefait contre lui qui ne changerait pas sa perception de moi"; Arcor, whose wife the player
  took, was missing from "Hold you in contempt"). Most misdeeds planted a grudge and left personal regard untouched, the
  score "Hold you dear / in contempt" lists by. Now one table (`DeedRegardPolicy`) charges each deed on the victim and
  on the close kin (spouse, parents, grown children, siblings): a spouse taken -60 / kin -20; a captive handed to his
  enemies -50 / -15 (sold as chattel -15); an execution or a murder, kin -70; a prisoner taken by force or tortured
  -60 / -20, tormented -40 / -20, interrogated under duress -30; a beating, kin -15; a fief revoked -40; cast out of
  the clan -30; a plot abetted found out -25; opinion swayed found out -20; a pact broken -20 on the realm's leader; a
  realm forsaken by defection or rebellion -15 on each of its lords; a bitter arena rivalry -10. And the kind side: a
  prisoner freed by mercy +15 / kin +5; a wedding +20; a gift +1 to +6 by its worth, once a day; a shared night +3 when
  the conversation itself raised nothing; a friendly arena rivalry +3. A character the player never spoke to now gets a
  profile, so a husband never met feels it, and a beating or robbery of a stranger costs regard too. Deeds that already
  moved regard keep their own number (beating -50, robbery -30, adultery found out, a task broken or done, an oath
  broken, a departure, a rescue, a duel). A grudge the player cannot know of moves nothing yet, or the page would give
  it away. `cr.heart_seed` raises a lover to at least +50. cr.testall `deed_regard`.
- **A house on the game's relation number names the two heads of house** (Gabriel 23/10: "deep trust between their
  lords" was "pas tres clair", it read as every lord of both houses). The number is between the two heads of house only,
  so such a house now has a row of its own, its banner then its line: "Hertogi and Ulfhild trust each other deeply
  (+42)", "Hertogi and Lucon resent each other (-35); their realms are at war". The four bands: trust each other
  deeply, hold each other in good regard, quietly distrust each other, resent each other. Houses sharing a story keep
  sharing one row. `ClanRegardListPolicy.HeadsLine`.

### Fixed
- **Grok keeps its cache from the first reply** (cr.cost_bench 24/10: on OpenRouter the first reply after the scene
  opener, and the first after a long talk was summed up, re-read the whole prompt at full price, about $0.020 instead
  of $0.0035). Each request now tells the provider which cached prompt it continues (`prompt_cache_key`, named from
  the character's stable prompt), and Grok finds it again: 3 tries out of 3 in a probe, against 0 before. OpenRouter
  only.
- **Gemini as interpreter reads at low effort, once** (cr.cost_bench 24/10: Gemini 3.8 Flash refuses reasoning off, was
  then left to think at its own default, ran past the interpreter's budget and was asked twice per reply: 20 s and
  about three times Grok's price for each reading). A call that wants no reasoning now goes at "low" when the model
  cannot turn it off, and a Gemini 3 model gets "low" from its first call. The same holds for memory summaries and the
  other background calls. Your prose keeps your own Reasoning Effort.
- **Custom chat portraits no longer turn blue** (Lordfadooboo 23/10: "any custom ones I added seemed to be blue, but in
  the chat portrait files they all look completely normal"; Rhagaea, a JPG, was fine). The game shows a 24-bit image
  (JPG, RGB PNG) with red and blue swapped and an RGBA PNG true, and the loader swapped red and blue on every image,
  which turned PNGs with an alpha channel blue. Every portrait, tale picture and chat art image is now re-encoded as an
  RGBA PNG, the encoding CR's icons and the political map already use and that renders true, whatever the file's
  own format (Gabriel 24/10: detect, never assume from the format). PNG headers name a colour type, never a channel
  order, so the engine is what is measured: cr.testall `portrait_channels` loads probes of one colour into the
  engine and reads its pixels back.
- **A fight's deeds could be told as another's.** The battle journal kept the last mission's deeds until the next
  mission began, so a tournament followed by an auto-resolved battle (which has no mission) told the tournament's
  deeds as the battle's. The journal is now bound to the battle it was fought for and knows a tournament from a
  battle; each reader takes only its own. It also counts every hero's own part now, not only the player's (felled,
  bested, struck down, by whom), ready for the battle memory below.
- **A lord executed right after a battle was not seen as executed.** The game marks that popup's execution apart from
  the prisoners screen's, and every listener checked only the latter: no vendetta from his kin, no chronicle line, a
  Nemesis not resolved as executed, a rumour saying he "died". All now read both (`ExecutionDetail`).
- **Bounties and their hunters** (froggyluv 22/10: hunters of Vlandia and the Southern Empire again and again, charging
  him inside a 2000-strong army, "I've disabled that in options but they're still showing up", and no way to settle;
  Gabriel 23/10). Turning "Arrests and outlaws" off now lifts every bounty as an amnesty lifts it (crime rating, the war
  on an independent clan, a fled outlaw's case, the hunters), unless the law already holds the player
  (`OutlawPolicy.BountiesLiftedWhenOff`, one road `ArrestBehavior.LiftBounty` for amnesty, payment and the option). No
  hunters ride against a player marching in another lord's army; a band already riding is called off
  (`OutlawPolicy.HuntersMayRide`). The day each realm last sent hunters is saved (`HuntersSentDay`, additive): a load
  sent a fresh band at once. New verb `pay_bounty`: a lord of a realm with a price on the player's head takes it off for
  `OutlawPolicy.BountyPrice` (3000 brigandage, 6000 flight, TUNE), taught with the exact price, confirmed [Y/n] naming
  lord, realm and price; the gold goes to the lord. SDK: catalogue line and three bench phrasings;
  `ActionVocabularyPolicy.LeanDescriptionChars` 86 -> 84 (the Compact interpreter head was 35 over). Not changed: a
  caravan attack still brings a bounty from its sender and its destination (Gabriel's ruling of 26/09, kept 23/10: a
  caravan is business between two realms, and both suffer its loss). testall `bounty_settled`.

### Developer tools
- **The cost bench outside the game** (Gabriel 24/10: "ça te donnerait la possibilité de vérifier des choses par
  toi-même"). `cr.cost_bench run` captures every request its prose and standard rows send to
  Logs/CalradiaRemembers_cost_capture.json; `CostReplayLiveTests` ([Explicit], CR_RUN_LIVE_LLM=1) replays them against
  any model through the same client, each model over its own replies, after a free check that the bodies are the game's
  byte for byte, and runs the interpreter sweep (302 readings). The synthetic history's gestures are now in the first
  person (DeepSeek V4.1 Flash had answered "*He snorts*" over subjectless ones). SDK: `LlmWireSnapshot.Request`.
- `cr.heart_seed [days_ago] [name]` (Gabriel 23/10, to see the encyclopedia's Heart without playing a romance out):
  a flirt, a first night and a recent one in the character's memory, the bond Intimate dated from the first night, and
  the kindness an intimate moment leaves (through `AppreciationBehavior.TryPlant`).

## v2.7.19.. 2026-10-22

### New
- **Heart, in the encyclopedia** (Gabriel 20/10: love stories were kept and shown nowhere). On the page of a character
  engaged with the player (courting, lovers, secret lover, consort, spouse, former spouse, estranged or broken), the
  story of the bond told to the player: "She has been your secret lover since spring 1091", the wedding, when they first
  became lovers, their children together, open terms, and her own lover only once the player knows of him. The player's
  own page lists one line per such person. A former spouse reads as such even though a divorce leaves the romantic state
  as it was; a hidden bastard is never named; a bond an old save never dated is told without a date
  (`HeartPagePolicy`, `HeartPageReader`). cr.testall `heart_page`.
- **Trauma, in the encyclopedia, Hardcore only** (Gabriel 20/10: "les aggressions vecues", on characters' pages only,
  "pas ceux du joueur car on ne decide pas a la place du joueur ce qui est traumatisant pour lui"). On a character's page,
  the aggressions they lived through that the player knows because the player was there, soberly: tortured as the
  player's prisoner, a companion tortured or assaulted by a captor or his men before the player's eyes, or by the
  player's own hands under a captor's compulsion, beaten down or robbed at swordpoint by the player in a conversation
  turned violent (robbery added on Gabriel's "oui, ajoute le vol"; an empty purse is not recorded). A new
  additive ledger (`TraumaLedgerBehavior`, save key `CalradiaRemembers_TraumaLedger_v1`) is written whatever the adult
  level and shown only at Hardcore; it starts with this version, so nothing from before shows. A captor scene is
  recorded under the same conduct gate as its grudge (only what the scene was, never its intent's ceiling). Grudges are
  deliberately not read: one can be refused or resolve, and the companion's "let me be broken" grudge targets a player
  who did not do it (`TraumaPagePolicy`, `TraumaPageReader`). cr.testall `trauma_page`; on Gabriel's word ("c'est une
  sauvegarde de test") `heart_page` and `trauma_page` now keep what they record on a companion of the player's, so the
  pages can be opened afterwards.
- **Items in tales** (tashmetu 20/10: "a tale that asks for things ('bring 30 grain to {giver}'), and a thing given in
  an ending"; Gabriel approved it for a second phase). A `bring` objective, on a stage or a choice: `item` (a kind,
  horses, livestock or grain; an item category, wine or iron; or one item's id), `count` up to 200, `to` a role, the
  giver when left out. The player hands the goods over with the bag in a conversation with that person, through the
  same picker as a lord's supply task: on his confirmation exactly the count leaves the party, short of it nothing does
  and the tale waits, and a mere greeting with the goods in the baggage takes nothing. The chat reminds him the goods go
  over with the bag. A hand-over written in the chat counts too (Gabriel, 21/10): the give_item is asked first naming
  the count the tale takes, then exactly that count leaves; short of it, nothing leaves and he is told why. An `item` consequence gives the player `count` of one item named by its id; a kind or a category is
  refused, since "a horse" would be whichever loaded first. Goods this game does not know are listed at load and keep
  the tale from being offered. The bag still settles a bargain or a supply task owed to that person first
  (`TaleItemPolicy`, `OfferFlowGate.WithTale`). Editor forms; HOWTO_Quests.md sections 6 and 8. cr.testall `tale_items`.
- A romantic bond now keeps the day it last changed and the day it became intimate (`RomanticProfile.StatusSinceDay`,
  `LoversSinceDay`, `MoveTo`/`NoteChangeFrom`, SDK). A rebuilt romantic profile (an orientation fix) now carries
  everything lived (`CarryRelationshipFrom`): until now the secret lover's day, open terms and her own lover were lost.

### For modders
- **A regard source as a function** (tashmetu 20/10: without CR, the game refused Regard and Standing altogether, since
  a class implementing `IPairRegardSource` cannot load where CR is absent). New
  `CrPairRegard.RegisterPairRegardSource(string id, Func<Hero, Hero, int, IEnumerable<(int weight, string line, string
  pageLine, bool knownToPlayer)>>)` and `UnregisterPairRegardSource(string id)`, naming no CR type. EXTENSION_API.md
  explains the load failure. `OptionalDependencyDoorsTests` holds every door documented "Names no Calradia Remembers
  type" to it. cr.testall `pair_regard_source` extended.
- **Following a letter another mod delivered** (tashmetu 20/10: R&S found its letter by sender, day and text, and read
  `PlayerReply` by reflection every hour). `CrLetters.DeliverWithId` returns the letter's id (`Deliver` unchanged);
  events `Replied(id, sender, reply)` and `SetAside(id, sender)` (the window's acknowledge, once); `GetStatus(id)`
  (on_the_road, lost, delivered, set_aside, answered, unknown, `ModLetterStatusPolicy`) and `GetReply(id)`. SDK:
  `PendingLetter.Id` and `SetAside`, additive. The window raises them through `Letters.ModLetterNotices`, connected
  at load (no new pair of namespaces). cr.testall `letter_from_another_mod` extended.
- **Lords with aims of their own: the public door** (tashmetu 14/10, "in Calradia, nobody takes the first step with
  the player"; shape proposed in ROADMAP, approved by Gabriel 20/10). `CrAims.Register(ExternalAim)` ->
  `AimResult`, `CrAims.Retract(sourceMod, id)`, event `CrAims.Raised(CrAimRaised)` (the lord was given the floor),
  over the book CR's own war duty already uses (`cr_standing_aims`). Another mod's aim surfaces in conversation;
  letter and audience later. cr.testall `aims_door`.
- **A match for a daughter of the player's house** (tashmetu 20/10: "When the bride is the player's daughter or
  sister, the one with the say over her hand is the player, so the door refuses."). `CrMatches.Register` now takes the
  groom's side as the decider when the player holds the say over the bride (`PendingMatchBook.AsksThePlayer`); the
  groom's side is taught to ask for her hand, and arrange_match is always confirmed in the chat ("Give Ylva's hand to
  Erek?" [Y/n], `ActionConfirmationPolicy.BuildHandQuestion`). The law: `CourtActionResolver.TryGiveHandOfPlayersHouse`
  (her heart and the houses; the player's yes is the family's). Design in ROADMAP. cr.testall `match_asks_the_player`.
- **Four tale keys** (tashmetu 20/10, "four small things that came up while writing new ones"):
  `requires.player.gender` (`AuthoredPlayerPolicy`, which now also holds the clan tier); `requires.giver.trade`
  (merchant, artisan, ruralnotable, headman, preacher; notables only, the trade guidelines' keys);
  `requires.village.type: "town"` (bound as `{village}`; a besieged town is never chosen); a negative ending `gold`
  the player pays to the giver, a choice leading straight there offered only while the player has it
  (`AuthoredQuestEngine.CostOf`, gating before declaration). The loader refuses a misspelt sex, an unknown trade, a
  trade asked of a lord and any other place type. Editor schema and TaleEditor regenerated; HOWTO_Quests.md.
  Items asked of the player or given in an ending: phase 2. cr.testall `tale_keys`.
- **A contract the employer ended is no abandonment** (tashmetu 20/10: Mercenary Management's AI dismisses the
  player's contract, and every lord of the realm lost regard for the player being let go; the game reports both as
  `LeaveAsMercenary`). New `CrMercenaries.ContractEndedByEmployer(Kingdom)`, the same day, before or after the game's
  report: the end is judged a plain end (`MercenaryDepartureFacts.EndedByEmployer`), and joining the realm's enemy
  afterwards is no betrayal. cr.testall `mercenary_dismissed`.

### Fixed
- **Five deeds are now taught to the character who can do them, not only listed.** arrange_match (the head of house
  giving a hand, and since this version the groom's side asking the player for his daughter's or sister's, tashmetu
  20/10), reinforce_garrison, share_intel, deliver_recruits and admit_secret_lover were registered and worked when
  emitted, but sat in no teaching list, so the model saw only their one-line catalogue entry and rarely used them
  right. Each is now taught in full while its task, match or secret is live. cr.testall `every_verb_taught` checks that
  every one of CR's own deeds is taught by a list or named as taught another way; `registered_match` and
  `admit_secret_lover` now also check the teaching reaches the real prompt.
- **A character no longer asks the player the same question again after a bystander speaks** (Lordfadooboo 20/10: on
  the road with Apolanea and a companion invited in, the question came back two or three times before he could answer).
  The bystander exchange (NPC Cross-Talk) re-called the character after every bystander line without looking at its own
  line, and the last round's directive even said to "turn the question to the player". Now the exchange stops once the
  character's line ends on a question that names no bystander (`WitnessExchangePolicy`), and the witness-turn directive
  says a question already put to the player is still waiting and must not be asked again (SDK `PromptBuilder`).
- **Kingdom votes: CR's code on their path can no longer crash the game** (CarolusIV 20/10: a crash on every vote on fief
  awards, trade agreements and some war declarations, with CC and "the bridge" also running; cause NOT proven ours, crash
  report asked). Nothing in CR hooks the vote itself, but three unguarded handlers run inside its outcome: the popup
  postfix behind "Who is this?" (every vanilla popup, `AudienceInquiry.OnPopupShown`), the war-declared quest stamp
  (`QuestDeedTracker.OnWarDeclared`) and the fief-changed self-esteem record (`SelfEsteemBehavior`). Each now runs in
  `SafeRun.Step` and logs its fault instead of throwing into the game's action.
- **The encyclopedia no longer seems to explain a regard it does not know** (Gabriel 20/10: "Personal regard: unknown"
  then "Why:" over Abagai's own friend and enemy). On a character's page the list is headed "Friends and enemies, and
  why:"; the player's page keeps "Why:", where the lines are the reasons others hold the player dear or in contempt.
  Under "Houses:", "At odds" and "Of affinity" are now subtitles, each with its bulleted list beneath (Gabriel 20/10),
  rather than two words repeated at the head of every line.
- **A tale's giver no longer remembers the same ending twice** (tashmetu 20/10, the Moonlit Maiden). A `locals_of`
  memory left nobody out, so a giver who is a notable of the village the ending speaks to got his own memory and the
  village's. Now the crowd leaves out anyone the ending names (`AuthoredQuestEngine.NamedMemoryHolders`); two memories an
  author aims at one hero by name both stand. cr.testall `tale_locals_memory`.
- **Another mod's secrets stay out of the Memories panel** (tashmetu 20/10: a player read his wife's secret affair in
  her lover's memories before it came to light). A private memory with a `SourceMod` other than CR's is left out of
  the chat's Memories panel (`MemoryPanelPolicy`); new MCM "Show secret memories" (Relationships, off) lists them
  marked secret. CR's own private memories (the player's word to a companion) stay listed. The character still
  remembers and speaks of it as before. A private memory sent with no `SourceMod` is recorded as "(another mod)"
  (`InjectedMemoryRemovalPolicy.UnnamedSource`), kept out too and still removable by any mod. cr.testall
  `memory_panel_secrets`.

### Internal
- **Logic split into themes, pilot** (Gabriel 19/10). The 420 flat files of `CalradiaRemembers.Logic` start moving into
  sub-namespaces by theme, the way `Logic.Duels` and `Logic.Grudges` already were: `Logic.Bastards` (7 files),
  `Logic.Marriage` (20: marriage, divorce, estrangement, courtship), `Logic.Nemesis` (12; `NemesisTurnPolicy` stays in
  the root because `CaptiveOfferPolicy` reads its thresholds). New `Tools/Move-LogicTypes.py` moves a set (git mv,
  namespace, the `using` of every user) and refuses a move that would add a pair of namespaces using each other.
  `Jealousy` stays in the root for now: `RomanticProfilePolicy` and the marriage rules read it. No behaviour change.
- **The dependency map sees a bare name resolved to an enclosing namespace** (`c0a0e9d`): it revealed 53 old pairs
  between the mod root and the namespaces calling `ModLog` or `SafeRun` without a using (pairs 84 -> 137).
- **New namespace `CalradiaRemembers.Foundation`**: `ModLog`, `SafeRun`, `StoryLog`, `ModSettings`, `CrSettings`,
  `LocalContextState` and `MainThreadDispatcher` (from Chat), the things every namespace reads, now in one place
  that reads none of them back. `ModSettings.ChatSkinPrefab` became `ChatManager.MenuSkinPrefab` (the settings no
  longer name the chat's prefabs); the MCM Test buttons reach `Services.ApiKeyTester` through hooks the submodule
  sets at load (cr.testall `mcm_test_buttons_wired`). New `Tools/Remove-DeadUsings.py` removed 102 imports nothing
  used. Pairs 137 -> 92; nine namespaces leave the cycle set. The `Settings` namespace is gone; MCM keeps your
  settings (it finds them by id). No behaviour change.
- **Logic split, the themes** (same day): Captivity, Chat, Witnesses, Items, RealmTidings, Dynasty, Prisoners,
  Providers, Conversation, Battles, War, Letters, Companions, Quests, Scenarios, Romance (Jealousy, secret lovers,
  open partners), plus RoundTable into Assemblies. Each theme left in the root what the root still reads (the letter
  mailbox and weights, companion happiness, jealousy culture, the stance core...). Root of Logic: 420 files, 729
  types in 57 parts -> 247 files, 445 types in 37 parts. Pairs stay at 92, no new cycle. `Move-LogicTypes.py` now
  also rewrites the partial form `Logic.X` written inside a mod namespace. Stance, Schemes, Rumors and Mercenaries
  stay whole in the root: their core is what the root itself reads.

## v2.7.18.. 2026-10-19

### New
- **The MCM page can be translated** (kkxz3, 18/10: "Could you make it possible to translate the MCM settings?";
  Gabriel 19/10). Every setting's name and hint, every group and button in `CrSettings` opens on a `{=CR_MCM_...}` id
  (179 strings). MCM 1.4.1 shows them through the game's `TextObject` (`SettingsPropertyVM`), so the game's language
  files translate them and the inline English is the fallback. Ships `ModuleData/Languages/EN/` (language_data +
  `std_CalradiaRemembers_mcm.xml`, the translators' template) and `ModuleData/Languages/README.txt` (how to make a
  language). Saved settings are unaffected: MCM keys them by property name, not display name. The template is
  GENERATED by `McmStringsTests` (ApprovedMap, so Update-Maps.ps1 regenerates it); the same tests refuse a setting
  without an id, an id with two texts, and a hint over 250 characters once its id is set aside (a unit test now, beside
  cr.testall `setting_hints_fit`, which strips the id too). Not yet: drop-down choices (the code compares them by
  their English text), the encyclopedia templates and in-play notices.

### Fixed
- **Memory compression and summaries work with a model that always reasons** (LapitaPolata, 19/10, on 2.7.17,
  glm-5.3-flash on OpenAI-Compatible with Reasoning Effort "low": "memory not compressed", HTTP 400 code 1210 in
  Chinese, "this model always thinks, does not support turning thinking off; use low, high or max"; then "[CONV QUEUE]
  dropped a summary job"). Proven: every housekeeping call forces `ReasoningOverride = "off"`, which wins over the
  player's dial and goes out as `reasoning_effort: "none"`; the 06/10 safety net (Thoragoros1) recognised only the
  English "reasoning is mandatory". `OpenRouterClient.CompleteAsync` (SDK): a refusal is now any HTTP 400 answering a
  request that turned reasoning off, whatever its language; the call goes again with the PLAYER'S dial ("low"), or the
  model's default when that dial is off too, and the client remembers the refusal so later background calls carry the
  dial at once instead of failing first.
- **A tale's band is placed where the game can put a party** (cr.testall `authored_quest_opening`, Gabriel 19/10:
  `[TALE] band spawn failed: AccessViolationException` near Mecalovea, the tale then abandoned; the run before in the
  same session was green). The band's point was the village plus a random offset of up to 3 in each direction, raw
  arithmetic that can land in water or off the walkable map before the native party creation. Now the game picks it
  (`NavigationHelper.FindPointAroundPosition` around the gate, as `BanditSpawnCampaignBehavior` does), and any point
  (a band restored from a save, the spike) is checked walkable first. The cause is the likely one, not proven: the
  crash was caught, so the log names no point. Read in the decompiled engine since: a party created with a spawn
  radius runs `NavigationHelper.FindReachablePointAroundPosition`, whose native path query starts from the given
  point's navigation face, and a point off the walkable map has none. So the check is now shared by EVERY CR spawn
  (`Travel.MapSpot.Walkable`: kept if walkable, else placed by the game around the home's gate, else not spawned,
  logged `[SPAWN]`): tale bands, every `TravelingParty` (couriers, bounty hunters, sergeants, a courier restored from
  a save) and the ambush convoy, which also now waits for land instead of springing at sea (the player's own
  position is water there). cr.testall `spawn_spot`.
- **cr.testall `chat_focus_arbiter` arranges its own screen** (Gabriel 19/10: FAIL with `restingFocusClaimers=
  [MapEscapeMenu]`, the map's escape menu up when the run began, so every "hold" read Yield). The arbiter was right to
  yield; the test assumed an empty screen. Whatever already claims focus when it starts is scenery, by the arbiter's
  own rule, and the test's layers are judged against that screen; the tavern-backdrop case is noted, not failed, when a
  settlement menu is already up.
- **A notable's trade can have guidelines of its own** (Windmolen, 19/10: "can I make a separate file behavior
  guidelines_merchants?"). `behavior_guidelines_<trade>.txt` (merchant, artisan, ruralnotable, headman, preacher) is
  read Custom-first before `behavior_guidelines_notable.txt` and replaces it for that trade
  (`StationGuidelinesPolicy.TradeKeys` / `TradeKeyOf` / `SelectStationText(station, trade, lookup)`); none ships, so
  nothing changes until a player or a mod writes one; cr_patch applies to them like any narrative file. And the
  profile now names all five trades (`NotableTradeLine`): a headman and a preacher read as "a person of some
  standing", so a section written for them had nothing to recognise them by. Custom/README.txt documents the files.
  cr.testall `trade_guidelines`.
- **Closing a chat or council never finishes the encounter during a siege** (Zivkyn, 18/10, on 2.7.16: "I crash every
  time i end a Council meeting with my own war band men if we are preparing for a seige defense"). Defence in depth,
  the crash's cause is NOT proven: the battle guard could not see a siege with no fight yet, and
  `PlayerEncounter.Finish` begins with `GameMenu.ExitToLast`, tearing down the siege menu. `MapEncounterCloseFacts.SiegeUnderWay`
  (the player's siege, their party's or army leader's, the settlement they stand in) keeps the encounter; logged
  `[CLOSE] encounter kept: a siege is under way`. Crash report and log asked of Zivkyn. cr.testall `close_settled_battle`.
- **A character pack's entry no longer reaches the character's children** (LapitaPolata, 19/10, CalradiaRemembersCustomPack:
  "a noble of Rhagaea's clan ... has the exactly same backstory as Rhagaea"). Proven: the game makes a child born in the
  campaign from a parent's character (`DefaultHeroCreationModel.GetCharacterTemplateForOffspring`, daughter from mother,
  son from father; `CharacterObject.CreateFrom` keeps the origin down the line), so `Hero.Template` of Rhagaea's daughter
  IS Rhagaea, and the template match added for tashmetu's mercenary captains (26/09) handed her Rhagaea's whole entry
  (backstory, conviction, traits, tastes). `CharacterOverrideSet.TemplateKey`: a hero born of parents is never matched on
  his template; a hero spawned from a template (captain, wanderer) has no parents and still is. Applied at every resolve
  site (`CharacterOverrideLoader.TemplateKeyOf`) and in `cr.lore_status`. cr.testall `override_not_inherited`.

### Changed
- **A low day can be argued with** (fkasad, 19/10, on 2.7.17: "a way to lift the spirits of a depressed NPC ... accurate
  and logical arguments the model can weigh against what caused the npc moral drop"). The lift existed (the model's
  `mood: lifted` in [STANCE], one band a turn, two a conversation) but nothing told him a true answer to his trouble is
  what moves it: `MoodLine.Compose` now says so on a low day (grim, short-tempered, guarded, weary) with a REAL cause,
  and that flattery and empty cheer do not (SDK). And the war is no longer a bare "your realm's war going badly":
  `DayMoodFactsPolicy.WarWhat` names the enemy realms and the odds by the same measure that calls the war lost or won
  (`MercenaryDeparturePolicy.IsLosing`; stronger / much stronger / several times stronger), carried as
  `MoodFacts.RealmWarWhat` into `MoodCause`. Not done: the Balance of Power's strategic assessment still feeds only the
  player's panel (fkasad's second idea, a ruler drawing on it, is a larger build). cr.testall `mood_of_the_day` extended.
- **The encyclopedia's "Personal regard" row heads the regard block** (tashmetu, 18/10, ask 6: "the player's own regard
  first"; Gabriel 19/10), above "Why:" and the houses, under the standing accords. It sat under the houses. UIExtenderEx
  stacks the hero-page rows in type order, which follows the SOURCE FILE names, last on top: `HeroPagePersonalRelationPatch`
  became `HeroPageRegardYouPatch(.cs)`. Guard: `HeroPageRowOrderTests` reads the compiled order with Cecil (and checks
  every heading sits above its list). Kept: six houses a side (Gabriel 19/10: design 8.2, the grouping already fixed
  the density); the collapsible section waits.

### Internal
- **Maps, after reading Graphify** (Gabriel 19/10). `dependencies.md` lists the pairs of namespaces that use each other,
  with the files behind each side (`37f1bc4`; `RatchetTests` pins their count); new `placement.md`: types living away
  from their users, with the effect of moving each, and how many parts each namespace falls into (`4fb00f1`, `b30ef34`).
- **15 pairs untangled** (`8636d26`): `SafeRun` moved from `Diagnostics` to the root beside `ModLog`, 47 imports of
  `CalradiaRemembers.Diagnostics` nothing used removed, `RelationWarmth` now in `CalradiaRemembers.Logic` (it was the
  mod's root namespace in the Logic project). Mutual pairs 99 -> 84; `Retainer` leaves the cycle. No behaviour change.

## v2.7.17.. 2026-10-18

### New
- **A character's mood of the day has a cause** (Lordfadooboo, 18/10: "the ai struggles to invent a reason for their
  mood and engaging with that mechanic can sometimes end the moment its addressed"). Gabriel: the list lives in the
  code, and the real cause is always named. `MoodCause.Of` (SDK): the fact that leaned the drawn mood most (a captive's
  captivity, a death in the family, a fief lost, a tournament won, wounds, the realm's war, coin running short), else
  a trifle from the public `MoodCause.Trifles` table by the day's seed (same all day; names no one, no weather). Said
  in the YOUR DAY block as true, or as "a small private matter"; he may keep it to himself. Self-esteem events now
  keep their kind and subject (`NpcProfile.LastPersonalBlowKind/Subject`, `LastPersonalGladnessKind/Subject`,
  additive; old saves name a blow vaguely). Letters keep the mood alone. cr.testall `mood_of_the_day` extended.
- **A new mood: weary** (Lordfadooboo, 18/10: "tired"; Gabriel: tired, not drunk). `DayMood.Weary` (SDK `e9cf453`),
  worn down, not unkind; leaned by the small hours, a wound, a captivity; level with guarded on the ladder. The
  perceived image reads "looks worn out" (`PerceivedImagePolicy`).

### For tale authors
- **Ways out the world takes** (Gabriel, 12/10 and 18/10, the Troy-inspired tale: the war ends some other way, she or
  the king dies, the player weds her). Objectives `war_ends`, `dies`, `weds_player` (`hero`: a role), on a stage or a
  choice, read once a day (`AuthoredQuestEngine.WorldNow`, `TaleRoleState`, run field `WarsSeen`, additive). A war
  stage waits for its war before reading peace as its end. Loader, Tale Editor, HOWTO_Quests. cr.testall
  `tale_world_exits` (`0e2de23`).
- **A tale may name its giver's realm** (Gabriel, 18/10: the queens' tale only where the queen has a face; the three
  empires share one culture). `requires.giver.realm`, one or a list, id or name, through the culture's own
  id-or-name rule (`AuthoredCulturePolicy.Matches`). Tale Editor, HOWTO_Quests. cr.testall `tale_realm` (`1831b03`).
- **A companion may give a tale** (tashmetu, 18/10: a road tale of "someone who rides with the player" used `any`
  with `with_player`, which let in the spouse or a child along). `requires.giver.role` `companion`
  (`Hero.IsPlayerCompanion`, `GiverFacts.IsPlayersCompanion`). Tale Editor, HOWTO_Quests. cr.testall
  `tale_companion_giver`.

### Changed
- **The encyclopedia's houses and reasons read more easily** (tashmetu, 18/10, with screenshots of Halthdar's and
  Svorni's pages: one dense block, "resentment between their lords" five or six times, a shared past said twice).
  `ClanRegardListPolicy.Lists` gives one line per reason with the houses sharing it ("At odds, resentment between
  their lords: Osticos, dey Meroc (at war)"); a house whose lord is already explained under Why reads "through
  <lord>" (`ClanRegardCandidate.Through`); Why reads "<name>, friend: <reason>" and a reason opening on both names
  drops them (`PairRegardPolicy.WithoutThePair`). Still six houses a side; the regard words unchanged (the chat uses
  the same ones).

### For modders
- **A letter from another mod, in CR's letter window** (tashmetu, 18/10: R&S's tournament invitations opened in the
  game's plain popup). `CrLetters.Deliver(sender, body, sourceMod)`: the body as given, CR's courier, window and
  letter book, the sender remembers it, the player may reply. Gated by `ActionKind.Letter`. SDK `6c50fdd`: new last
  `LetterReason.FromAnotherMod` (memory purpose, reply occasion); bypasses the mailbox budget. No answer buttons
  yet. EXTENSION_API. cr.testall `letter_from_another_mod` (`de097dd`).
- **Other mods add to what lies between two characters** (tashmetu, 18/10: lords R&S made close through years of
  battles side by side showed as "neither" or "enemy" beside its own tooltip). `IPairRegardSource`,
  `CrPairRegard.RegisterPairRegardSource`; `PairRegardFacts.OutsideTraces`, counted together for at most
  `PairRegardPolicy.OutsideCap` (40) either way; CR's own reason first on equal weight. EXTENSION_API. cr.testall
  `pair_regard_source`.
- **Companion patrols, public and read-only** (tashmetu, 18/10: R&S read `CompanionPatrolBehavior._patrols` by
  reflection). `CrPatrols.Current` (copies, `CrPatrolInfo`), `CrPatrols.Fought` (`CrPatrolBattle`) and
  `CrPatrols.Ended` (`CrPatrolEnd`, `How`: Back, Scattered, Captured, Killed). EXTENSION_API. cr.testall
  `companion_patrol` extended.

### Held back (Extras/RealmQueens, not shipped)
- **The Guest of the King's Hall**, the Troy-inspired reference tale, and its scenario pack (war when the queen
  leaves, peace when she is returned, the king's gratitude when told). makezip skips the pack, verifyzip refuses it
  (`a66cd46`).

## v2.7.16.. 2026-10-18

### New
- **Press a lord toward war** (Gabriel, 18/10: "qu'on puisse influencer en chat pour la guerre"; any lord; the cost
  is the risk the target realm learns of it; the player sees nothing special). `press_for_war`
  (`PressForWarVerb`, `PressForWarPolicy`): a lord truly persuaded resolves to carry the case for war on a named
  realm to his court. Taught only with the realms his house could really propose a war against (the game's own
  rules: not at war, not allied, peace older than 20 days, no war proposal pending, the influence to pay), each
  with how his house leans by the game's own `DeclareWarDecision.CalculateSupport`. Three days later
  (`PressForWarBehavior`, save key `CalradiaRemembers_PressForWar_v1bytes`) his house files the game's own
  `DeclareWarDecision`, paying its influence: the clans vote, the ruler decides; the mod declares nothing (rule
  of 02/07: only the ruler declares). The talk is `WarLooming` gossip, watched for the target realm's house
  heads by `SwayExposureBehavior` (new `Cause`); one it reaches keeps a hidden grudge. The case must name the
  realm (`PlayerSpokeOfRealm`). SDK `d6fedbe`: catalogue entry, bench phrasings, Lean description cut 90 -> 88
  to keep the Compact interpreter head under 28,000. testall `press_for_war`.

### Fixed
- **An enemy lord no longer asks the player's business on his lands, nor rides up outnumbered** (LapitaPolata, Nexus
  18/10). `InterceptionPolicy.Choose`: `TerritorialPatrol` now fires only for a realm wary of the player but not at war
  (`InterceptionFacts.RealmAtWarWithPlayer`); at war the crossing is plain and the game's own war (attack or flight)
  answers it. A lord of a realm at war whose party is outnumbered by the player's force
  (`InterceptionPolicy.IsOutnumbered`, the engine's `EstimatedStrength`, the player's army when they lead one) never
  rides out at all, for any reason, as the game has such a lord flee. A stronger enemy still presses a grudge (and asks
  first, 2.7.12). testall `enemy_no_border_challenge`.
- **A written tale is set in a quiet village, and ends when its giver loses the village** (fkasad, Nexus 18/10: the
  Drowned Bridge was set in a burned village whose town was besieged and fell). `TaleVillagePolicy`: a tale's village is
  no longer the giver's first one whatever its state; a village being raided, looted, or bound to a besieged fortress is
  passed over for a quiet one, and with none the tale is not offered now. While the tale runs, when the giver's house no
  longer holds the village (`StopIfVillageLost`, daily), the tale ends without reward or penalty and its journal says
  who holds it now (Gabriel, 18/10). testall `tale_quiet_village`.
- **A lord raising a tale sets no errand of his own beside it** (fkasad, Nexus 18/10, his log: raising the Drowned
  Bridge on the road, the lord also set an errand about the same bridge at another village, written as an `[ACTION]
  type: quest` the bridge refused, the purple "That deed finds no purchase" line). `AuthoredTaleRoles.HoldsARole`
  covered only a tale already accepted; the hero raising one (`_offerHeroId`) now holds its role too, so
  `InActiveWrittenTale` closes his quest offers while he raises it. testall `authored_quest_opening`
  (`theRaisingLordSetsNoOtherTask`).
- **A captive in the dungeon of the player's castle can bargain for freedom** (LapitaPolata, Nexus 18/10: the Sultan's
  wife and son promised 20,000, the player agreed, nothing happened). The player's-captive standing
  (`PlayerStandingFacts.NpcIsCaptiveOfPlayer`), `counter_offer` and `free_prisoner` read the party only, while
  `recruit_prisoner` already read the custody rule (`IsHeldByPlayer`, `Prisoners.PrisonerCustodyPolicy`: the
  player's party or the player's house, dungeons included). All now read it: a dungeon captive is offered terms, and
  an accord frees him. testall `captive_dungeon_bargain`.

## v2.7.15.. 2026-10-17

### New
- **A lord says why he holds someone friend or enemy** (NPC regard, design `NPC_REGARD.md` sections 5 and 9;
  Gabriel, 17/10: every friend or enemy carries its reason). When the player names someone, the speaker is told what
  lies between them, from things that happened: a kinsman the other had executed or killed in battle (the engine's
  death mark), his own grudges and debts of gratitude to them, the campaign's written past (a betrayal, a feud, a
  rescue, by its actors and those harmed), a capture or a defeat in battle the game recorded. Weighed and aged
  (`PairRegardPolicy`: an execution never fades, a wrong halves every 10 years, a defeat every 5), it gives "an enemy,
  for what lies between you: had your brother Garios executed (9 years ago)", with a strong reason the other way
  ("though ...") and, when the player could not know the main one, that the speaker may keep it to himself. Kin keep
  their word and gain the reason; a pair with nothing between them reads as before (vanilla relation is not a
  source, it has no reason). The friends and enemies listed in the prompt drop anyone the derived regard says is the
  opposite. `PairRegardReader`, `WorldEventStore.EventsNamingBoth`; cr.testall `npc_regard_reason`. Not yet: the
  encyclopedia pages, the clan lists, the generated past.
- **The encyclopedia's Friends and Enemies say why** (NPC regard, design section 8, route b). Your own page lists who
  holds you dear and who holds you in contempt, by each character's personal regard (the mod's own ledger, the case
  Gabriel saw on 17/10: a vanilla "friend" bearing a bitter grudge), with the strongest known grudge or kindness
  toward you, else the regard in words. A character's page keeps vanilla's lists, less anyone what really happened
  makes the opposite, plus anyone it makes a friend or enemy with a reason you could know (`PairRegardPolicy.
  PageReason`: a secret reason lists the pair nowhere); a page where nothing applies stays exactly as vanilla built
  it. The reasons sit in a "Why:" block, written about the character ("had his brother Garios executed (9 years
  ago)"). `EncyclopediaHeroPageRegardMixin` refills the vanilla lists from its refresh (portraits, tooltips and links
  stay vanilla's); `PairRegardReader.TiedTo`, `WorldEventStore.EventsNaming`. cr.testall `npc_regard_reason` now also
  builds the wronged lord's page and checks it before and after a refresh.
- **Houses at odds and of affinity** (NPC regard, design 5.4 and 8.2; Gabriel's answer to question 1, 17/10: "les
  deux"). A character's page lists, in a "Houses:" block, the houses at odds with theirs and those of affinity, at
  most six each: by what really happened between the two house lords, with its reason, else by the vanilla relation
  between them; a house of a realm at war is marked "(at war)". On your own page, by each house lord's personal
  regard for you. `ClanRegardListPolicy`; cr.testall `npc_regard_reason` checks the betrayer's house is at odds.
- **Lords with no story may have shared a past** (NPC regard, design section 7; Gabriel's rulings: drawn once and
  never re-rolled, a good share get nothing, written into the world's long-term memory, vanilla relation biases the
  draw). When a character's encyclopedia page opens, up to six of the lords it would show (its lists and the house
  lords of the realm, the nearest first) who are not kin and have no story of their own draw, once, what passed
  between them before the campaign: a boundary quarrel, an insult never forgiven, an old friendship, riding together
  against raiders, a rescue, an unpaid debt, a rivalry in the lists, a refused suit; about two pairs in three draw
  nothing. Each kind fits only what the game holds (neighbouring fiefs, opposed or alike traits, the same realm and
  culture, valour, generosity, never married); a pair warm by the vanilla number draws no grievance, a bitter one no
  bond. It is dated one to twenty years before the start, when both were of age, placed near a town, never a death,
  marriage, child, conquest or third person. It joins the long-term memory, so the two, their kin and houses know it,
  the friends lists and the conversation weigh it (full for two years, then a grievance halves every 8, a bond every
  15), and a lord speaks of it when it is named. `SharedPastPolicy`, `SharedPastBehavior` (save key
  `CalradiaRemembers_SharedPastDrawn_v1bytes`, additive: an old save has drawn nothing), `WorldEventStore
  .RemoveHistory`; cr.testall `shared_past_draw`. A conversation never draws.
- **The "Personal regard" row gives its reason** (design Q2, proposed and taken as the default): "bitter grudge
  (-80): ..." with the strongest known grudge (for a low regard) or kindness (for a high one).

## v2.7.14.. 2026-10-17

### Fixed
- **Characters speak your language from their first line** (RaveN017Mod, 17/10: French game, Reply Language French,
  yet whoever came to him on the map, and every council, opened in English until asked to repeat in French). The
  council builds its own prompt (`CouncilPromptBuilder`) and never carried the language; it now does, last in the
  prompt (SDK `989492b`). An opening the character speaks first had only an English stage direction to go on; the
  direction now ends naming the language (`ReplyLanguagePolicy.WithLanguage`, every line spoken without a player
  message). And "Reply Language" left blank now means the game's own language (`BannerlordConfig.Language`, mapped to
  a name the model knows); an English game keeps the old mirror of whatever you write. cr.testall
  `reply_language_everywhere`.
- **A companion's news is fresh, and not yours** (LapitaPolata, 17/10: a companion told of a castle being besieged
  after it had fallen, and of a war breaking out while the player was already riding to a siege in it, "like they
  were not in my team witnessing everything with me"). The news a companion comes to bring was anything they could
  have heard, up to about three weeks old for a war, never checked when spoken. It is now only news that is a week old
  at most, that the player did not take part in, that is not a war or peace of the player's own realm or clan, that a
  later event has not overtaken (a siege fight at a place that has since fallen), and that is still true (a war not
  since made peace). The topic says how long ago, and the companion is told to tell it as past, heard on the road with
  the player. `CompanionRumourFreshnessPolicy`, `CompanionAudienceTopicResolver.FreshnessFacts`; cr.testall
  `companion_news_fresh`. Letters and what a character has heard keep the 40-day window.

## v2.7.13.. 2026-10-17

### New
- **Companion patrols, the engine (increment 1 of 4)** (jaserms1, 16/10: a war council's patrol order did nothing;
  Gabriel's rulings 16-17/10 and his engine spike at Ebereth). A companion patrols any settlement with the player's
  own men until called back. The order opens the game's own "create a party" screen (on the map, never inside a
  conversation), with the companion as leader and the men prefilled; the player chooses men and companions, up to the
  leader's party size, or cancels and nothing moves. Confirmed, the men leave the player's party (moved, never
  conjured), with five days of the player's food; the clan pays their wages like any clan party. The AI stays free so
  the band fights what it meets; an hourly leash sends it back when it strays more than 15, is re-anchored elsewhere
  (the spike's drift) or leaves its post, never mid-fight or mid-flight, and lets a hungry band go for food. Capture
  and death of the leader are told by name; a scattered band's leader makes their way back; recalled (chat or the
  errand recall paths), the band rides back and hands over every man, wounded and prisoners included. Battles won and
  lost are counted for the monthly letter. A letter to a patrolling companion no longer recalls them (errands only):
  stopping by letter comes with the monthly letter. `CompanionPatrolPolicy`, `CompanionPatrolBehavior`, save key
  `CalradiaRemembers_CompanionPatrols_v1bytes` (additive); console `cr.patrol`, `cr.patrol_home`; cr.testall
  `companion_patrol`. Not yet: the order in chat and council, the monthly letter, the journal.
- **Companion patrols, the order (increment 2 of 4).** `dispatch_mission` gains `target_mission: patrol` with
  `target_settlement`, the place the player names (the game never picks it, the model never invents it). In chat, the
  companion who agrees takes the order and the troop screen opens once the conversation closes; in council, a
  `dispatch_mission` resolution carries the place through `DispatchGroundingCodec` ("patrol|Ebereth"; a bare errand
  name from an older save still reads as the errand), and the screen opens once the council has risen. A place that
  matches no settlement exactly refuses the order with a reason. Taught in the action catalog (the interpreter), the 1:1
  block and the council block; the catalog's `about` and `target_settlement` texts are short to keep the compact
  interpreter prompt under its 28,000-character budget. The number of men is chosen on the screen, not in the order.
- **Companion patrols, the letters (increment 3 of 4).** Every 30 days a patrolling companion writes (new
  `LetterReason.PatrolReport`, outside the mailbox budget) with the patrol's real record only (`PatrolLetterText`: the
  place, the days, the fights won and lost and against how many, the men fit now) and asks whether to keep on. A letter
  from the player that clearly orders the patrol home carries a "[PATROL] stop" control line the game reads: a letter
  the player writes first is obeyed when it arrives (even if the companion does not write back); an answer to the
  monthly letter rides the courier (2 days). No line, or no answer, and the patrol goes on. cr.testall
  `companion_patrol` now also checks the monthly letter is sent and that a stop order turns the patrol for home.
- **Companion patrols, the journal (increment 4 of 4).** A patrol has its own entry in the quest journal ("Patrol: X
  around Y"), a read-only mirror like the errands': it logs the departure, each fight, each monthly letter and the
  recall, and closes when the band comes home (success) or is broken (capture, death, scattered). An entry whose patrol
  is no longer tracked is closed at session launch. `CompanionPatrolQuest`, save class id 5 (additive). The engine spike
  `cr.patrol_spike` is removed; `cr.patrol_kit` stays as a test helper.
- **Chat Temperature setting** (fkasad, 17/10: does the mod tune the temperature by the kind of call? It does,
  from 0.1 for extraction to 0.7 for conversation, but the conversation value was fixed in code). MCM, LLM Provider:
  "Chat Temperature", 0.10 to 1.20, default 0.70, so nothing changes for anyone who leaves it. It drives the
  conversation turn and the envoy; background calls keep their own low values, and the war council its 0.8.
  Reasoning models (gpt-5, o-series) are sent no temperature at all, as before. Varying it by relationship was
  declined: temperature spreads word choice, it does not make a lord formal (the prompt does).
  `ChatTemperaturePolicy`; cr.testall `chat_temperature`.
- **The world's long-term memory (increment 1)** (Gabriel, 17/10: "apres 42 jours, ca devrait basculer vers une
  memoire long terme. La memoire long terme servirait entre autre a definir les relations entre NPC.. et pourrait
  parfois etre utilise pour des conversations entre joueur et NPC sur le passe"; design NPC_REGARD.md 7.7a, bar
  accepted the same day). A third-party world event leaving the news (the 42-day window, or the 200-rumour cap) is
  now kept in a second tier when it is history: a battle of several hundred men or more (250, the record's own
  "several hundred"; a battle recorded before this version, its size unknown, is kept from magnitude 45), a siege, a
  fief taken, an execution, a marriage, a war, a peace, a discovered or open scheme, a lord's capture. A skirmish
  fades as before. The tier is never news (not scored by `RumorAwareness`, never in a companion's report): it is
  known to the event's actors, their kin and house, the lords of its place, and from magnitude 45 everyone of its
  realms; the player's secret and hide vetoes still hold. Cap 3000, the least then the oldest evicted first.
  Captures between NPCs (Gabriel's answer to question 6): a ruler's or house head's capture stays news and is now
  marked so it reaches the tier; a lesser lord taken by a lord, not news (Lordfadooboo, 03/10), is written straight
  into it. NPC executions and discovered schemes were already recorded; scheme lines now name both sides with their
  house (`HeroLabel`). `LongTermHistoryPolicy` (`IsMemorable`, `WouldKnow`, `Evict`), `WorldEventPruner.PruneAndHandOff`,
  `WorldEventStore.GetHistory(HistoryQuery)` / `RecordHistory`, `WorldEvent.MenAtStart` / `IsSiege` (additive),
  save key `CalradiaRemembers_WorldHistory_v1bytes` (additive: an older save opens with an empty history, and what is
  still news at load reaches it in time); console `cr.history [name]`; cr.testall `world_long_term_memory`. Not yet:
  the encyclopedia lists, the regard between NPCs, the generated past, the conversation.
  Later the same day (Gabriel's answers): a secret event reaches the tier with its secret and is spoken of only in
  confidence (`RumorDiscretion.SecretTag`); risings, divorces and a lord fallen in battle are on the bar
  (`HistoryKind.Rebellion`, `Divorce`, `FellInBattle`).
- **Authored history: the past before day 1, written by you** (Gabriel, 17/10: "je pourrais mettre en scene des
  evenements du passe qui ne se sont pas produits dans le temps de jeu"; "A file per save ID. File editable with an
  external tool.. something like a web page with a timeline."; design `docs/design/AUTHORED_HISTORY.md`). One file per
  campaign in `Documents/Mount and Blade II Bannerlord/CalradiaRemembers/History/<campaign id>.history.json`, read at
  every load and by `cr.history_reload`: its events (dated before the campaign began, by year and season) join the
  long-term memory as authored, never evicted, known by the same rules plus the people the file names as knowing a
  secret. An event may change or set a starting relation; a ledger in the save moves the relation by what changed in
  the file since the last load, so reloading never stacks, and a relation naming your character also moves the
  character's own regard. What the file gets wrong is set aside with a reason in `<campaign id>.history.problems.txt`.
  The timeline page `HistoryEditor.html` (in the module's Docs) edits the file and checks it against a snapshot of the
  world written by `cr.history_file`. `AuthoredHistoryLoader`, `HistoryRelationLedger`, `AuthoredHistoryBehavior`
  (save key `CalradiaRemembers_AuthoredHistoryLedger_v1bytes`, additive), SDK `VanillaRelation.Bound`; cr.testall
  `authored_history`.
- **Lords speak of the past you name** (authored history increment 4; Gabriel, 17/10: the long-term memory serves
  "des conversations entre joueur et NPC sur le passe"). When your message names a person (the dead included), a
  place or a realm, the speaker gets up to three long-term events naming it that he would know, the greatest first,
  each saying how long ago, whether he was part of it or knows it only as told (older than him), and the secret tag.
  Only a name brings up the past, never a word: "the battle yesterday" raises nothing from ten years ago. Rendered
  below `CURRENT ENCOUNTER:` under its own heading, which tells the model it is the past, not news.
  `PastOnMentionPolicy`, `WorldEventStore.KnownPastNaming`, `EncounterContextBuilder.ResolvePastOnMention`, SDK
  `EncounterContext.PastOnMentionBlock`; cr.testall `history_on_mention`. Not yet: the regard reasons on a named
  person (waits on `PairRegardPolicy`).

### Changed
- **The background model is logged** (fkasad, 17/10: background calls seemed to reach the prose model). Each time
  the background client is built, `[LLM] background client: provider, model, address` goes to the log; the
  "Background model" hint now says it must be a chat model, not an embedding model. The code already honoured the
  setting with the provider on "(default)"; a local server may answer with whatever model it has loaded.
- **The game names your character, never "the player"** (Gabriel, 17/10: a grievance read "Holds an old bitterness
  against you, over The marriage was ended by the player's own choice"). Every screen that shows stored or written
  text (chat lines and narration, the memory panel, the chat side panel, the encyclopedia's grievances, kindnesses,
  accords, discoveries and nemesis dossier, letters and the letter archive, the chronicle, council engagements, the
  journal) now shows the character's name where a memory, a cause or a model's reply says "the player"
  (`PlayerNameText`, `Mapping.ShownText`), so old saves read right too. The divorce texts are written with the name.
  A grievance whose cause is a whole sentence follows a colon instead of "over". Notifications and popups that pass
  such text are not covered yet. Logs and the model's own instructions keep the role word. cr.testall
  `player_named_on_screen`.
- **Memories get the player's sex right** (Gabriel, 17/10: a companion remembered "the threat he described" of a
  female character). The model that writes a memory of the turn (the interpreter, in Enhanced) and the conversation
  and letter summaries knew the player only by name and guessed "he". They are now told her pronouns
  (`EncounterContext.PlayerIsFemale`, `PlayerPronounLine`, SDK). Memories already written keep their words.
- **An enemy lord asks before he talks** (fkasad, 16/10: an enemy lord rode up as the player was about to attack him;
  the chat opened by force and a local model reasoned for half a minute over an opener the player closed unread;
  Gabriel the same day: a popup, for hostile lords only). A lord of a realm at war with the player, or one defending
  his land, who reaches the player on the road now raises "X bars your way" (Speak with them / Not now, with "Who is
  this?"). Declined, no conversation opens and nothing is sent to the model; the chase simply ends. Anyone else still
  opens the meeting at once, so a word owed cannot be waved away. `InterceptionPolicy.AsksBeforeMeeting`; cr.testall
  `enemy_meeting_offer`.

### Fixed
- **The same news asked about in every town** (froggyluv, 17/10, with "NPCs Raise World News" on: "just about
  everyone wants to stop me ... and ask me the same question", the same war, then the same capture, "stuck in a
  loop"). The ledger of raised matters was per character, so the next notable asked again, and each picked the
  greatest news of the moment. A piece of world gossip is now asked about once, by whoever asks first; an invitation
  that names it counts, taken up or declined (`RecentMatterSelector.GossipAlreadyRaisedByAnyone`, "offered:" entries
  in the existing ledger, no new save key). The asking character is told the player may well know nothing of it, and
  to take that lightly (the model had turned insulting). cr.testall `gossip_once_per_news`.
- **A chat on the road ended in a battle** (Wallace1067, 17/10, on 2.7.12; the 13/10 fix aimed at the wrong cause): CR
  never told the game the meeting was over, so its meeting menu started a battle with the party met after every chat
  (`encounter_meeting` on init: meeting done, not leaving, `StartBattle`). The close now sets `LeaveEncounter` as the
  game's own leave line does (`MapEncounterClosePolicy.LeavesTheMeeting`: a friendly party met, or one gone; an enemy at
  war stays on the encounter menu). A companion recalled from an errand while the player meets his party no longer has
  that party disbanded mid-talk (it waits for the encounter's end), which left an empty battle with no way out.
  testall `close_leaves_meeting`.
- **A council member's own offer no longer becomes your quest** (jaserms1, 16/10: a war council ordered a companion
  to patrol villages; the game handed the player a bandit-clearing task instead). A council "quest" resolution is,
  and always was, a task set FOR the player, but the council was taught to record a member's own pledge with it
  ("Ira will ride to Pravend..."). It is now taught as the player's task, and a member's own deed is not to be
  recorded as one. A quest naming a deed the game cannot set (patrol, a hero-bound deed) now fails the pledge with a
  reason instead of silently becoming a bandit-clear; a pledge naming no deed keeps the legacy bandit-clear.
  `CouncilQuestTypePolicy.TryResolve`. The real companion patrol is in progress.
- **The world's record of a battle says who, where and how big** (fkasad, 16/10: "defeated a host in battle" said
  nothing of who lost, a band of looters read like an army, and a field battle had no place). A side no hero led is
  named for what it was ("a band of Looters", "men of Vlandia") instead of "a host"; a field battle the game tied to
  no settlement is placed "near" the closest one within 40 map units (also in the memories of the player's own
  battles); the line ends with its scale from the men on the field at the start ("a skirmish of a few dozen men" to
  "a great battle of thousands"); a skirmish carries less far (magnitude minus 10), a great battle further (plus 10);
  a leaderless side's realm is now among those who hear of it. `BattleRecordText`. Word already fades with time and
  distance (RumorAwareness: 40 days, the place's proximity, the hedged "heard secondhand" / "a distant, unverified
  rumour").
- **Closing the chat stops the reply being written** (fkasad, 16/10). A reply still in flight when the window closed
  ran to completion and was thrown away; on a local server that held the model for as long as it took, and the next
  call waited behind it. The window's conversation calls (the turn and the NPC's opening words, a commoner, an
  ensemble voice, an envoy) are now cancelled at the start of the close (`ChatViewModel.CancelPendingReplies`).

## v2.7.12.. 2026-10-16 (2.7.11 skipped: a Nexus upload tagged 2.7.10 as both 2.7.10 and 2.7.11)

### New
- **A liege holds his vassal to the war** (Maifailun, 15/10: a vassal who never fights the realm's enemy hears
  nothing from his ruler; Gabriel's ladder the same day). MCM Relationships "Vassal war duty" (OFF by default),
  "War duty window (days)" (30) and "War levy scale (%)" (100). A sworn vassal who has not fought the realm's
  enemies (a battle against them, a siege, or a day in one of the realm's armies) for the window while the realm is
  at war receives a SUMMONS letter from the ruler: come and answer for it within 20 days. Until the player has stood
  before him, the ruler also calls them in himself: walking into the town or castle where he stands, he summons
  them to his hall (ahead of anyone else there, a companion's private word included, whatever the "Lords
  approaching" roll; "Off" still stops it). He never rides out after them on the road: the king sits for the
  meeting, he does not go out in person. Coming before him (a conversation, or riding in his army) brings the
  LEVY demanded in person; never coming brings it by letter. The
  levy (1000 per clan tier plus 1500 per fief, times the scale, 1000 to 10000) is paid in person in any mix of coin
  (take_gold), horses or weapons (give_item, at market value) and soldiers (give_troops, cheapest first, never more
  than half the player's healthy men); none of it counts as a gift. Riding to war settles it at any step. If nothing
  is given in the next 20 days the ruler takes one fief (a castle before a town, the poorest, never one under
  siege) or, with none to take, remembers it. Every step leaves the ruler a memory; the letters and the ruler's
  terms name only the consequence the game will carry out. Peace, or leaving the realm, drops it; a captive's days
  do not run. The ruler holds the duty as a standing aim of the mod's own (CrAims, internal for now). Console
  `cr.war_duty`; cr.testall `war_duty_ledger`, `war_duty_reckoning`, `war_duty_levy`, `war_duty_audience`,
  `aims_conversation`.
- **Consequences on a stage (`on_enter`)** (tashmetu, 15/10: consequences ran only at an ending, so a lady could not
  leave midway and be given back later in the same tale, which the Troy design needed). A stage's `on_enter` runs as
  the tale enters it, under its own `if`; never on the first stage (the request; the loader and the editor say so).
  Checked as endings' consequences are (types, the husband bound, flags). `AuthoredQuestEngine.OnEnter`.
- **`CrTales` events for modders** (tashmetu, 15/10): `StageReached`, `Ended` (endings, failure, and
  "end:abandoned", which is not sent to scenario rules) and `ConsequenceSettled` (a return that waited), with the
  roles, the flags and each consequence's real outcome (`Done` / `NotDone` + reason / `Waiting`), so a mod never
  answers an elopement that did not happen. On_enter runs before the stage is announced, so scenario rules and mods
  both see it done. cr.testall `tale_on_enter` (Heavy), `tale_states` hears the abandon.
- **Background work waits for the conversation, on a local server** (fkasad, 15/10: summaries, memory compaction
  and letters slipped in between two replies, the local server swapped its KV cache and replies timed out). A
  background call routed to OpenAI-Compatible now waits for the open conversation to close before it is sent
  (`BackgroundCallHoldPolicy`, capped at 10 minutes; log `[BG HOLD]`): conversation summaries (queued, captive,
  world claims), memory compaction, letter memories, courier-secret judgements, and letters (held after their
  prompt is built, before their timeout starts). Cloud providers are untouched. Not held: the transcript fold inside
  a conversation, which the reply waits on. Trade-off: reopening a chat with the same character at once, on a local
  server, the summary of the last one waits until this one ends. cr.testall `background_hold`.

- **Another mod can hand the player a match to broker** (tashmetu, 16/10: a head of house agreed in the chat to give
  his kinswoman to a lord of the player's house, as Regard and Standing's matchmaker had proposed, and nothing
  happened; Gabriel approved the door the same day). For modders: `CrMatches.Register(new CrMatchRequest {...})` (suitor,
  bride, decider, source, named) teaches the decider `arrange_match` for that match, and CR's own law settles it
  (`CourtActionResolver.TryArrangeThirdPartyMatch`); the door refuses at once an unknown id, one person twice, a couple
  the game will not wed, or a decider with no say over her hand (`PendingMatchBook.Judge`). `CrMatches.Settled`
  reports each answer (wed, or the reason); `Withdraw` takes a match back. Memory only, forgotten when the game is
  left. EXTENSION_API.md "Matches the player brokers". cr.testall `registered_match`.
- **"You lay together" in the manual action menu** (tashmetu, 16/10: impregnation_risk could not be sent by hand in
  any mode, and a local interpreter now and then produced no tags; Gabriel approved it the same day). A new Intimacy
  section records a scene the two have played carried to completion, between the player and a spouse, consort or
  lover only; the bridge's own guards and the conception roll still decide, and the line says only "Whether a child
  comes of it, time will tell." It is listed only for a spouse, consort or lover, and never below Explicit; for
  anyone else it is not shown at all, not even as blocked. `ManualActionCatalog.ConceptionRisk` (outside `All`), three new facts in
  `ManualActionFacts`; the pairing read is shared with the bridge (`PregnancyService.RoleOf`). cr.testall
  `manual_conception_entry`.

### Removed
- **MCM "Actions Interpreter Timing"** (fkasad, 15/10): its "Asynchronous" choice ran synchronously all along (a
  TODO). The interpreter always applies its actions with the reply, as it always did; the real async path is a
  ROADMAP entry.

### Fixed
- **A lord above the player no longer greets them as a servant** (tashmetu, 16/10: a princess greeted a landless
  player with no troops with "I am at your disposal"). The opening greeting comes from a fixed pool chosen by regard
  and traits; the lines that offer service ("at your disposal", "What can I do for you?", "You honor me with your
  company", "Whatever I can spare is yours to ask for", and three more) are now left out when the speaker outranks
  the player, by the rule of rank letters already follow (a ruler to anyone who does not rule; a house above the
  player's tier). A spouse, the player's own house or close blood, a romance, a dear friend, and anyone speaking to a
  ruling player keep them. `GreetingDeferencePolicy`; cr.testall `greeting_rank`.
- **A mercenary contract signed in the chat no longer runs out at once** (tashmetu, 16/10: hired through the chat
  with Mercenary Management, the clan left the realm three days later and every lord lost 8 regard, judged an
  abandonment). `join_as_mercenary` passed today as the contract's minimum term where every vanilla hiring path
  passes none, and Mercenary Management reads that field as the end of its contract. Now no fixed term, as the game,
  and the pay rate asked of the game's own model (`GetMercenaryAwardFactorToJoinKingdom`) instead of a flat 50.
  cr.testall `mercenary_join_term` (signs a real contract in the chat, then puts the clan back).
- **Giving more than 10,000 at once says so** (tashmetu, 16/10: asked to give 20,000, gave 10,000, thought the rest
  was lost). The ceiling stays (a guard against an invented sum); the line now reads "the most that can change hands
  at once" when it held the sum back (`GoldTransferLimit.CappedNote`).
- **A conception that does not happen says why in the log** (tashmetu, 16/10: 30 nights, no child, nothing logged).
  `[PREGNANCY] no conception:` with the reason (age, already with child, a recent birth, Life/Death Cycle off) or the
  failed roll and its chance (`PregnancyService.IneligibilityReason`). The MCM help of "Adult Content Level" now says
  a child can be conceived only at Explicit or above.
- **No audience at a gate the player cannot walk through** (Gabriel, 16/10: a player sneaking into a town must not
  be summoned to the hall). The entry popup comes up at the gate, before the player chooses to sneak in, and only
  war blocked it; a town that turns the player away for a crime (disguise only), a castle barred by its owner, or a
  player already in disguise still raised a companion's private word or a fief audience. The game's own gate rule
  is now read (`SettlementAudienceSafetyPolicy.NotOpenlyAdmitted`).
- **A parent knows a child who died** (Maifailun via tashmetu, 15/10: the player's wife Debana died in childbirth and
  her father asked "Which Debana?"). `RelationshipsBuilder` listed living children only; dead children, a dead
  spouse (the game moves them to the former spouses) and dead siblings are now named "the late", with how and when
  (`KinDeathDescriber`), and "once the player's wife/husband" when so. A partner of the player is told the
  player's spouses who died (Maifailun's other wife asked "Debana of Dolentos?"), and the name hint finds the
  speaker's own dead kin (and the player's dead spouses, for a partner) instead of only a living namesake.
  cr.testall `dead_kin` (arranges a minted family; the hint included).
- **Companions remember the small fights too** (Thoragoros1, 15/10: three companions, battles against bandits, a
  patrol and lords' parties, and nobody remembered any of them). Only a battle the chronicle records (a siege,
  armies on both sides or 150 men, and a lord leading a side) left a companion memory. Now every fight the player
  fights leaves one, and the small ones fold into a single running line per companion ("Rode with X in 3 small
  fights of late; the last against a band of Looters, and we won it"), a new line after 30 days or 12 fights, so
  the save does not grow a memory per looter band. No stance moves for a skirmish. `SkirmishMemoryText`.
  A second cause: a companion (or kin) got a memory profile only from a first Calradia Remembers conversation, so
  one hired through the game's own dialogue and never spoken to remembered no battle at all, small or great. Riding
  in the player's party now makes one. cr.testall `companion_skirmish_memory` (runs with no profile, as theirs did).

## v2.7.10.. 2026-10-14

### New
- **Selling an item** (3chmidt, 13/10): `sell_item` verb (catalogue, interpreter rule 4 + worked example, bench),
  executed as one deal (`BannerlordGameStateBridge.ExecuteSellItem`: item out, coin in, item back if the payment
  fails), price from `ItemSalePolicy` (purse, single-transfer ceiling), always confirmed (`BuildItemQuestion`).
  Grounding before the price: `item_on_offer` tail section (value and the buyer's purse) from the bag pick, kept on
  the table for the conversation (`OfferController.ItemOnTheTable`). A worded `give_item` is now confirmed naming the
  real roster item (`ResolveNamedItem`/`PreviewItem`). One deal per turn: `TradeBatchPolicy` (same ITEM, not word)
  and `TurnPaymentPolicy` (sell_item moves its own coin). SDK `7e34fec`; mod `c05d9e0`, `e5b8ef3`.
- **Captive counter-offer** (Maifailun, 14/10; Gabriel: the captive proposes, intimacy a term in Mature+):
  `CaptiveOfferPolicy` (terms open to HER: ransom by her house's means, fealty only from a clan head not ruling a
  realm, service through the recruit gate, intimacy at Mature+ between adults if orientation allows and never from
  an honorable captive; disposition from the nemesis turn thresholds), `captive_terms` tail section, `counter_offer`
  verb executed after the player's [Y/n] (ransom, fealty via `ChangeKingdomAction.ApplyByJoinToKingdom`, service via
  recruit_prisoner, then release). SDK `e5da52e`, `bbd4add`; mod `c522934`, `a094d5c`.
- **Who is asking** (Lordfadooboo, fkasad, 13/10): `AudienceWhoLine`/`AudienceStation` on every invitation;
  "Who is this?" button on the game's popup (UIExtenderEx prefab + mixin on `SingleQueryPopUpVM`, shown only for
  `AudienceInquiry` invitations), `EncyclopediaDetour` brings the invitation back. Mod `859dddd`, `a5c837a`.
- **Local model context window** (fkasad, 13/10): MCM "Local model context (tokens)" (0 = off); `ContextFitPolicy`
  in `NpcChatService` fits each request (oldest raw lines after the pinned recap, then compact), recalibrated from
  the server's prompt_tokens; commoner conversations too; the interpreter fits its own window; compact sticks for the
  session (`LocalContextState`). SDK `e7d1439`, `6983452`; mod `85b6815`, `b4a20da`.
- **Prompt Composer increments 2-3**: prompt profiles chosen in MCM, a layout per kind of character, sections
  reworded by `sections/<key>.txt` within the locks; `cr.prompt_check`. SDK `ae5a0c3`, `79bb4ad`; mod `c666021`, `6dc428c`.

### Changed
- **Envoys** (fkasad, 12/10): a second envoy offers more (`PrisonerOfferPolicy.RaisePerEnvoyTurnedAway`), and fewer
  come. SDK `2d215a5`; mod `384d5f1`.

### Fixed
- **Chat close on the map** (Wallace1067, 13/10): only a LIVE map event keeps the encounter
  (`MapEncounterClosePolicy.IsLive`: not finalized, no winner); the `[CLOSE]` line names which. Mod `0c6913a`.
- **Battle memories** (tashmetu, 13/10): no more "at at" (the feat reporter now gets the ChronicleEngagement noun
  phrase), and the witness lines name the player instead of "you". Mod `d1098f2`.

### For modders
- **Tale-scenario bridge**: `requires.giver` married / not_clan_leader / not_of_players_realm, `requires.husband`
  ({husband}), consequences `elope` and `return_spouse` (`TaleElopementPolicy.DecideReturn`: a husband on campaign or
  a captivity waits and is retried daily, saved under `CalradiaRemembers_TaleReturnsWaiting_v1`; only the game's
  new-match rule refusing restores the marriage directly), `ON_TALE` triggers, roles in rules, one-argument `IF_RULES`,
  `PRESS_FOR_WAR`. Mod `cb343d6`, `4b7831e`, `b253a07`, `ed8fa46`, `0c2c627`.
- `{giver_realm}` in tale image names (`AuthoredTaleImages.Resolve`). Mod `54e599a`.
- Faces files (`RealmFaceBehavior`, Custom/faces.json, a mod's cr_faces.json, optional chat portrait through
  `ChatPortraitResolver.ByHero`). CR's own queens are held back in `Extras/RealmQueens` (Gabriel, 13/10: "ne livre pas
  les reines"); `makezip.py` skips them. Mod `f4aade9`, `857db18`, `e3f9aa7`.

### Developer tools
- `cr.testall` arranges its own context (Gabriel, 13/10): `ArrangedCompanion`, `SeedPlayerMarriage`, lords in a
  given state, the player's state through seams; diplomacy arranged only in `cr.test full` (`MayArrangeDiplomacy`);
  a marriage safety net after every test. Mod `bb46f16`, `ee48bc2`, `662dba0`, `6106aaf`.
- New tests: audience_who, sell_item, captive_terms, captive_counter_offer, local_context, close_settled_battle,
  battle_memory_wording, prompt_profile, realm_faces, tale_husband, tale_elopement, tale_scenario_bridge.

## v2.7.9.. 2026-10-12

### New
- **A tale's picture may be a video** (Gabriel): `.crvid`, CR's own format (JPEG frames in a zip, a manifest;
  `CrvidRules`, `VideoClock`), decoded ahead on a thread (`CrvidDecoder`), shown as a new texture per frame by
  `CrVideoPlayer` on the application tick (the game's `VideoPlayerView` does not play inside a conversation). Played
  once it rests on its first frame; PLAY, STOP, LOOP at its bottom-right. Classic skin: the first frame as a still
  (`CrvidPoster`). `Tools/Video/make_crvid.py`. The Tale Editor's picker takes a `.crvid`.
- **A click on a tale's picture enlarges or shrinks it** (`SceneImageLayout`, kept for the conversation).
- **Tales given by the player's spouse** (Gabriel): `requires.giver` role `spouse`, `gender`, `with_player`,
  `not_pregnant` (`AuthoredGiverPolicy`).
- **Tales that begin on the map** (Gabriel): `on_map` (hours still, by day, giver's lands, daily chance, popup;
  `ChatSceneKind.TaleMeeting`).
- **Tale stages as states** of one conversation (Gabriel): the character speaks from the state reached by words, its
  picture shows (`AuthoredQuestEngine.Spoken`).
- **Consequence `conception`** (Gabriel): `TaleConceptionPolicy`, then `PregnancyService.TryConceive`.
- **A tale's content level** (Gabriel): `requires.content` none/mature/explicit/hardcore (`AuthoredContentPolicy`); the
  offer waits for a player whose setting reaches it, a tale under way stops if the setting is lowered.
- **The Drowned Bridge**, a second written tale, with its pictures.
- **Scenarios** (Gabriel): every ready rule for an event plays (`ScenarioPass`), `group` for alternatives, a rule plays
  only when all its consequences can happen (`CanApply`); guards IF_AT_WAR, IF_AT_PEACE, IF_ALIVE, IF_KINGDOM_EXISTS,
  IF_RULES. The Scenario Composer ships in Docs/.

### Fixed
- **New Story Mode game hung on the training field's loading screen** (Lordfadooboo, crazyblox): not reproduced; the
  likely cause is the elder brother's private word (2.7.8 family audiences) raised as the tutorial mission opened. The
  one initiative gate honours the game's own tutorial restriction (`StoryModeTutorial`, `ActionRefusal.TutorialUnderWay`).
- **Envoy spam about one prisoner** (fkasad): `EnvoyVisitFacts`, `CaptiveSpacingDays` 5 after any envoy about him,
  `EnvoysBeforeGivingUp` 2 per captivity, one bitter enemy per realm (`PrisonerBuyerPolicy.Order`); additive save key
  `CR_PrisonerEnvoysCame_v1bytes`.
- **A lord riding out reached a besieging army: a forced fight** (Maifailun): not reproduced; nothing kept a chase from
  starting or going on during the player's own siege or battle. `InterceptionPolicy.PlayerCanBeApproached`,
  `ChaseFacts.PlayerInSiegeOrBattle`, `InterceptionBehavior.PlayerInSiegeOrBattle` (also in `PlayerIsFreeOnMap`).
- **Escape out of a conversation crashed the game** (philip0105): not reproduced; the close ran inside the chat's own
  widget update with the press live for the game. `EscapeCloseLatch` + `ChatManager.TickEscapeClose` (application tick,
  key up, one frame later).
- **Freed lords forgot their captivity** (zhenja1): a lord freed from a beaten party's prisoners (the player's loot)
  remembers the rescue and who held them.
- **A tale's picture in unrelated conversations** (Gabriel): `AuthoredTaleImages.ConversationIsTheStep`; in a
  conversation the picture is small, top-left (`SceneImageLayout.Fit`).
- **Scenarios:** IF_HERO_IS matched a death's killer (`ScenarioContext.Subjects`, `ScenarioBehavior.ForDeath`);
  kingdom names did not resolve in a translated game (`ScenarioReferenceMatch`, game ids in the packs); an injected
  memory needs no prior profile.
- **The Tale Editor would not open** (an apostrophe in a script string); `ShippedHtmlToolsParseTests` runs
  `node --check` on both shipped HTML tools.
- **Manuscript chat book took clicks over the picture** beside it: `BookRoot` DoNotAcceptEvents.

### Changed
- Tale Editor story map: stages that lead to one another form one framed group; long edges go over the row.

### For modders
- `{player}` in every tale (tashmetu): `AuthoredQuestLoader.GameBoundRoles`.
- Scenario packs from other mods (`ModuleData/cr_scenarios`) and the player's own (`Custom/scenarios`):
  `ScenarioPackFolders`, `ScenarioRuleMerge` (first read keeps an id).
- Vivid World (philip0105): `CrNpcMemory.RemoveMemory` (`InjectedMemoryRemovalPolicy`), memories keep `SourceMod`,
  `heard_now.txt` for a live `CrPrompt` variable (SDK 40ffa9a).

### Developer tools
- cr.testall scenario_packs, scenario_death, scenario_marriage, scenario_sources, scenario_all_play, scenario_guards,
  video_decoder, envoy_pacing, tutorial_gate.
- cr.tale_on_map, cr.tale_test, cr.video.
- SDK: golden prompts (28 configurations) and the prompt as a registry of 111 keyed sections, written exactly as
  before (e2a1644, 8ecdbf4); Prompt Composer design in docs/design/prompt-composer.

## v2.7.8.. 2026-10-10

### New
- **Family asks for a private word** (HakiTakiUmba): `TravellingAudiencePolicy` (hired companion or player's clan in
  the party; the last one asked waits while anyone else has a matter), SDK `EncounterContext.AudienceFromKin`.
- **Letters from castles and villages** (raphareish): "Send a letter" in the castle and village menus
  (`CourierHirePlacePolicy`, `TaverneLetterBehavior`).
- **Tale band `on_defeat`** (Gabriel): `AuthoredDefeat`, `AuthoredQuestEngine.PlayerLostTo`, heard on
  `MapEventEnded`; the wolf resumes. Editor and four parity fixtures.

### Fixed
- **Raid close** (Zivkyn): `MapEncounterCloseFacts.BattleIsRaid`; a raid is finished as before 2.7.5.
- **Accents** (elpyf0, fkasad): the language rule keeps an accent or dialect (SDK).

### Changed
- **THE PLAYER in the prompt's tail** (fkasad): before WHO IS WHO, uncached per turn (SDK).
- **Marriage length floor** (fkasad, Windmolen): `SpouseTenurePolicy.LongStanding` from the couple's eldest child.

### Developer tools
- cr.testall kin_audience, courier_menu_option; authored_quest_opening loses to the band; wedding_day_line.
- `cr.tale_fight_spike` (spike: company-only hideout battle over the map battle, no chief).
- Tooling: release scripts in `Tools/Release`, em-dash git hooks, `Tools/edit.py`, CLAUDE.md trimmed.

## v2.7.7.. 2026-10-09

### For modders
- **Tales from other mods** (tashmetu): every enabled mod's `ModuleData/cr_quests` (one sub-folder per tale), in launcher
  order, after CR's own and the player's folders (`AuthoredQuestFolders.FromModules`, the enabled list cr_patch reads).
  A tale's source carries its mod's id; a duplicate id keeps the first read and reports the other.

### Developer tools
- cr.testall tale_mod_folders.

## v2.7.6.. 2026-10-08

### New
- **Tale images** (Gabriel): `AuthoredStage.image`, and `image` on findings, hazards and encounters (`AuthoredTaleImages`:
  bare .png/.jpg name, met > hazard > found). The stage's picture opens with its scene and its conversations
  (`ConversationImageFor`, `ConversationImageForLocalsOf`); `AuthoredQuestBehavior.LoadFiles` reads one level of
  sub-folders. Dark Leather: the picture stands in the margin (`SceneImageArea`, `IsSceneImageVisible`).
- **The Wolf of the Wood** moves to `ModuleData/Quests/wolf_of_the_wood/` with thirteen pictures.
- **Tale Editor**: `imageField` with a picker and a preview.

### Fixed
- **Letters** (IdentityCrysys): `LetterReplyReason.Of` answers a "why no reply?" letter as the correspondence it carries on.

### Changed
- **Chat title**: the name alone (`ChatTitleText`), without "Calradia Remembers -".

### Developer tools
- cr.testall scene_image_shown.

## v2.7.5.. 2026-10-08

### Fixed
- **Town duel crash** (Thoragoros1, reproduced by Gabriel with `cr.duel_take_field`): the square opened in the second the
  chat closed while the map conversation unwound; `ReadyToOpenTheSquare(SquareReadinessFacts)` waits for the game's
  conversation to end and counts from the chat's close.
- **Post-duel scene**: a staged settlement conversation opens where the character stands (`AudienceLocationPolicy`,
  `LocationComplex.GetLocationOfCharacter`), and the stager begins the conversation itself when the game does not
  (`StagingGracePolicy.ShouldStartTheConversation`, `MissionConversationLogic.StartConversation`).
- **Battle under way on close** (Maifailun): `MapEncounterClosePolicy` never finishes an encounter mid-battle.
- **Captor memory** (Windmolen): `CaptureMemoryBehavior`/`CaptureMemoryText`, the battle, place and men led.
- **Partners know each other** (Windmolen): `PlayerPartnersText` in the family line.

### Changed
- **Captures framed as war** (Fakade): `CaptureNorm`, stake-tagged capture events.
- **Native quest memories** (Maifailun, tashmetu): `NativeQuestMemoryBehavior`.
- **Companion picker** off by default (MCM "Ask Which Companions Come Along").
- **StageDirectionDedupe** (SDK).

### For modders
- SDK `NotableEventType.OwnLife` (ordinal 13), `HistoryWindowPolicy` strands, heir note frames lived history only.
- `CrCultures.MarriageCustom`, `CrRomance.WouldAcceptIntimacy` / `RecordSharedIntimacy`.

### Developer tools
- `cr.duel_take_field`; cr.testall capture_memory, partners_know_each_other, native_quest_memory,
  romance_api_held_to_the_chats_bar.

## v2.7.4.. 2026-10-07

### Tales (fkasad, tashmetu)
- **Leaving a scene asks first** (`SceneLeavePolicy`), unless it ended or nothing it needs is left (`SceneMayEnd`, which also
  tells the narrator to let the scene pause and says so once in the chat).
- **Companions remember a scene** (`SceneCompanionMemory`, Collaboration): the witness memory needed ordinary turns.
- **One errand at a time**: a character holding a role in the tale under way offers no other task
  (`AuthoredTaleRoles.HoldsARole` -> SDK `EncounterContext.InActiveWrittenTale`, `MayOfferTasks`).
- **`"template": "local"`**: the nearest hideout's bandits, a seasoned share by the player's average troop tier
  (`AuthoredBandMakeupPolicy`, `RegionalBandits`); the wolf's slavers are local. `cr.testall tale_local_band`.
- **Said choices**: the interpreter list opens with the id alone; `SaidChoiceId` matches a glued "choice" prefix; the
  refusal shows no chat line.
- **Journal**: `TaleJournalPolicy` keeps only the newest open entry of the tale under way; reconciled at load, daily and
  at the end.

### Fixed
- **Birth house** (Maifailun): the family line says "Born into the X (of realm); joined the Y by marriage." (`BirthHouseText`,
  engine origin clan else a parent's). `cr.testall birth_house_known`.

### For modders
- `CrNpcMemory.GetRegard(npcId)` (additive). HOWTO_Quests: a choice stage knows only its own knowers.

## v2.7.3.. 2026-10-07

### Fixed
- **Black screen after a tale's scene, for real** (fkasad): `OpenForExploration`'s OnClose queued `ExplorationBackdrop.Close`
  for the next frame, and `FreeResources` in the close's finally cleared the dispatcher queue, so the pushed backdrop
  state was never popped (and the closing popup and a scene following a conversation were lost too). The
  conversation's continuations are now dropped at the START of `ChatManager.Close`. `cr.testall
  scene_close_gives_the_map_back` goes through the real open and close.
- **The narrator never herds the player** (fkasad): told never to steer toward what is to be found, and to mention a
  place once (`AuthoredQuestPrompt.Narrator`).

### Added
- **Scene surroundings** (fkasad): terrain, season, hour and the player's culture in the narrator's prompt
  (`SceneSurroundingsText`, `AuthoredQuestBehavior.Surroundings`; `cr.testall scene_surroundings_read`).
- **`scale_with_player`** on a tale's band (`AuthoredBandSizePolicy`: 0.6 x healthy men, never below the roll, max 120);
  the wolf's slavers scale.
- **`invite_prisoners`** on a scene, off by default (`PlayerCompanyPolicy.InviteCandidates(prisonersWelcome)`).

## v2.7.2.. 2026-10-06

### Changed
- **Chat header shows the character's own relation** (Fakade, True Relations): `RelationSummaryText` adds "Own +N"
  between Clan and Personal when `OwnGameRelationWithPlayer` differs from the clan leader's.

### Fixed
- **Reasoning-mandatory models** (Thoragoros1, Gemini 3.7 Flash): an HTTP 400 "Reasoning is mandatory" on a call sent
  with reasoning off is retried once with the model's default reasoning (SDK `OpenRouterClient`,
  `IsReasoningMandatoryRefusal`, `RebuildWithModelDefaultReasoning`).
- **Grudge banner names its target** (tashmetu): `GrudgeBannerText`; "against you" only when the target is the player.

### For modders
- **Tale culture filter** (tashmetu): `giver.culture` and `village.culture`, one or many, id or name, any case
  (`AuthoredCulturePolicy`); schema, Tale Editor forms and HOWTO_Quests updated.
- **`Hearsay` memory kind** (tashmetu, Regard and Standing): SDK `NotableEventType.Hearsay` (ordinal 12, appended);
  `HistoryWindowPolicy` counts lived memories and hearsay apart: shown under their own header, Compact keeps 6 lived
  plus `LeanHearsayLimit` (2) heard, compression's recent-5 shield counts lived memories only and the compressor is
  told to fold hearsay first; only-heard is still a first meeting. Accepted by `CrMemoryEntry.Kind`.

## v2.7.1.. 2026-10-06

### Added
- **Branching tales** (Gabriel, 04/10/2026; autonomous run of docs/design/quest-editor/goal.md, merged `77ae4c1`): stage
  `choices` taken by act (first objective met; new `wait` objective), in a scene by `[[choice:id]]` with a numbered
  clickable menu (`SceneChoiceMenu`), or by words through the interpreter (`story_choice`, SDK
  `EncounterContext.StoryChoicesForInterpreter`); named `endings`; flags (`sets`, `if`, `!name`); the `{rival}` role.
  Loader checks for unreachable stages, dead ends, unknown endings, duplicate choices. `cr.story_choose`.
  REPORT.md and DECISIONS.md list every default taken.
- **Tale Editor** (`Docs/TaleEditor.html`): offline forms, story graph, live checks, Twine import; held to the loader by a
  schema generated from the C# model and 88 parity fixtures run in C# and in Node.
- **Grudge consequence** (Gabriel, 06/10/2026): `{type: grudge, who, against, text, magnitude}` plants a
  `GrudgeSource.Tale` grudge (mends with time); `AuthoredQuestEngine.GrudgeOf`.
- **Tales** (fkasad): `ModuleData/Custom/Quests` and the MCM Custom path (`AuthoredQuestFolders`); the village tracked
  on the map; villagers get the tale's `locals_of` lines (SDK `CommonsKnowledge.AuthoredQuestBlock`); no tale from a
  giver with a vanilla task (`AuthoredOfferFacts.GiverHasVanillaQuest`); wolf watch radius 12.

### Fixed
- **Black screen after a tale's scene** (fkasad): the ordinary close ended the player's village encounter, the active
  state moved and the scene backdrop was never popped; a scene now closes as `ChatCloseKind.Scene` (window and clock).

## v2.7.0.. 2026-10-05

### Added
- **Written tales, experimental and OFF by default** (MCM "Authored Quests (experimental)", Content): multi-stage quests
  in JSON (`Logic.AuthoredQuests`: loader with blocking/cosmetic checks, stage machine, prompts, offer policy with
  `weight` and `repeat_cooldown_days`; `Quests.AuthoredQuestBehavior`; "Tale:" journal entry, saveable id 4; stage
  notices). Objectives talk_to, talk_to_locals, be_at, keep_watch, explore, defeat_party, return. The exploration scene
  is a heroless narrator chat with `[[found/hazard/meet]]` markers, hosted on a pushed GameState
  (`Chat.ExplorationBackdrop`), never over the live map. Ships `ModuleData/Quests/wolf_of_the_wood.json`; Custom folder;
  `Docs/HOWTO_Quests.md`; `cr.story_check [name]`, `cr.story_status`, `cr.story_start`, `cr.story_step`,
  `cr.story_abandon`. New save keys (additive): `CalradiaRemembers_AuthoredQuestRun_v1`, `..._AuthoredQuestsDone_v1`,
  `..._AuthoredQuestsDeclined_v1`, `..._AuthoredQuestsPlayedOn_v1`. SDK `EncounterContext.AuthoredQuestBlock`.
- **Invite and Request Private** (nerethi): Invite seats a companion, travelling lord or held prisoner
  (`Witnesses.PlayerCompany`, `PlayerCompanyPolicy`); Request Private repeatable, hidden with no witness, granted at once
  when every witness is the player's own (`PrivateAudiencePolicy`).
- **Going apart counts as a private audience** (IdentityCrysys, SDK `85d1f4d`), and the chat says the room is theirs.
- **World-event discretion** (IdentityCrysys): `cr.world_events`, `cr.world_event_remove|secret|hide`; the veto in
  `WorldEventStore.Explain` (`RumorDiscretion`).
- **MCM**: "Lords Hold Their Course" (Fakade), "Mercenary departures are judged" (Fakade).

### Changed
- **Mercenary departures** (Fakade): held `RenewalWindowDays` (3) so a renewal is no departure; penalties fade over
  `ResentmentFadeDays` (a year); new save key `CR_MercenaryResentment_v1bytes`.
- **Chase at the gate** (Fakade): a siege ends the chase; inside walls the day limit applies.
- **Clan standing is the clan leader's relation** (tashmetu): `GameRelationReading`; SDK
  `NpcProfile.OwnGameRelationWithPlayer` said apart in CURRENT STANCE only when it differs.
- **Calendar follows the game** (tashmetu): `ResolveCalendar` takes setting.json, else `CampaignTime`, else Calradia.
- **Wars line** opens with the player's realm's wars (fkasad).
- **One meaning for "mood"** (fkasad): guidelines section renamed "READING THE CONVERSATION"; SDK YOUR DAY never
  overrides why the conversation is happening.
- **Wanderers** (Felido, fkasad): between engagements and looking for work.
- **Background calls honour the MCM timeout** (CarolusIV): letters, summaries, courier secrets, letter memories
  (`BackgroundCallTimeout`, floor 60 s).
- **Enhanced mode**: a refused beat goes to the NSFW model, the scene narrator included (`RefusalFallbackPolicy`).
- **Dark Leather**: Request Private and Invite in one row.

### Fixed
- **Module path** (EmperorHaterius): every file was read from a path rebuilt from `BasePath.Name`; now
  `Paths.ModulePaths.Root` (the engine's `ModuleHelper` path); `ModuleRootPathTests` forbids the old form; Dark Leather
  falls back to Origin when its art cannot load (`ChatSkinFallbackPolicy`); images log why they fail (`[IMAGE]`).
- **Whose "you"** (fkasad): a lived memory raised in a meeting is quoted with whose "you" is whose
  (`RecentMatterWording`); SDK history header names the character and is bounded to the memory lines.
- **give_item count, give_prisoner target** (fkasad): declared in the SDK catalogue; `ActionParameterContractTests`
  checks every action's taught parameters against what the game reads.
- **"the player"** (Felido): both format reminders forbid it (Lean budget unchanged).
- **Companion audience promises** (EnjoyingHydra): spent only once on screen (`AudiencePromiseLedger`).
- **Ultimatum** (fkasad, SDK): an either/or is not a dismissal.
- **Ransom envoys** (aphareish): `EnvoyDelayAfterCaptureDays` (3).
- **Refused sale wording** (Lordfadooboo): `NotHisToSellNote` says who holds the captive.
- **Landfall** (CarolusIV): a held audience waits `LandfallSettleHours` ashore and no starting encounter.

## v2.6.11.. 2026-10-03

### Added
- **Escape leaves a CR conversation** (EmperorHaterius): one layer at a time (`ChatEscapePolicy`): a screen over the chat
  keeps its Escape, an open side panel closes first, the message field is left with its draft kept, then the chat closes
  as its close button does.
- **A captive of rank knows the world talks of her capture** (Lordfadooboo: Rhagaea in an Aserai cell): SDK
  `EncounterContext.CaptiveNotoriety` adds a line after the cut-off; a ruler's or landed house head's capture is world
  news even without the player (`CaptiveNotoriety`, `WorldEventFeeders.RecordGreatCapture`).
- **Newlyweds** (raphareish): `WeddingDayBehavior` records each wedding day (new save key); the spouse line says how
  recent it is (`SpouseTenurePolicy`).
- **A house in rebellion remembers it** (FloatingMammal): `WorldEventType.Rebellion`, memories of the rising and the
  return for every adult member (`RebellionWording`).
- **A divorce another mod made** (tashmetu): `ForeignDivorceBehavior` compares marriages daily; a couple parted while
  both live becomes `WorldEventType.Divorce` and a memory for each; CR's own divorces are not counted twice.
- **MCM Self-Esteem Climb** (fkasad), Character group, 0..4, default 2.

### Changed
- **Self-esteem** (fkasad): a skirmish is no feat, a win against the odds is worth more, gains shrink near the top
  (`SelfEsteemPolicy.GainFactor`).
- **Duel publicity** (fkasad): the audience is whoever is present in the place, capped at 10; the news weighs the
  opponent's station (`DuelPublicityPolicy`).
- **Strategic Assessment** (fkasad): compact wars, each realm's real battle record, the player's world name and texts.
- **Personas** (tashmetu): six archetypes no longer word ordinary -1 trait mixes as sadism (`RetiredArchetypes`
  re-derives stored ones at load); a trait at +-2 now meets its archetype by direction, with a sentence saying what is
  extreme (`PersonaTraitIntensity`).

### Fixed
- **A settled matter stays settled** (Lordfadooboo): the recap ends with a "Settled:" line; the dialogue style forbids
  re-raising what was asked and answered.
- **The offer bag reads the current speaker's profile** (Lordfadooboo: a companion's grain task never completed), not
  the `DialogFlow.Profile` cache; `SpeakerProfileSourceTests` allows one read.
- **A new spouse's romantic status is healed at the wedding** (brorespeta123), not only at the next load.
- **Accents in cr.* console commands** (Shinelegacy): the console hands UTF-8 read as Windows-1252; repaired at the
  engine's dispatch for the cr group only (`CrConsoleTextPatch`, `ConsoleTextRepair`). `cr.memory_repair_accents`
  mends stored memories.

### For modders
- **`VerbFacts.IsFemale`**; HOWTO_Actions says per-character wording belongs in `TeachingText`.
- `cr.prompt_dump` takes a hero id and lists namesakes.

## v2.6.10.. 2026-10-01

### Added
- **Food and favours: eleven kinds of task, every one verified by the game** (fkasad and Lordfadooboo, Nexus, 01/10/2026).
  - **A hungry town remembers** who sold it a large load of food (relief) and who bought up its market (hoarding),
    once a week per town, as a world event its people hear (`TownFoodPolicy`, `FoodTradeMemoryBehavior`).
  - **Grain a lord asked for while a place he answers for starves** goes into that place's stores (`Mapping.HeroSeats`).
  - **What a town lacks**: a trade good at twice its worth on its market may be asked for, and goes into that market.
  - **reinforce_garrison** (men for a thin garrison, handed over in that place), **recruit_troops** (fresh recruits of
    the lord's own people), one close for both (`SettleTroopHandOver`).
  - **earn_regard** (win another's good graces, +10 from where it stood), **plead_case** (plead the lord's case
    before his liege; done when that person is truly won over toward him).
  - **ransom_envoy**: buy back a lord's man from a realm the player is at peace with; the purse is advanced (1.5x
    the ransom value) and the change is the player's; freed some other way first, the task ends with no fault and
    the advance goes back.
  - **scout_town**: enter an enemy town; its gang leader may tell (`share_intel`) what he knows of its walls, stores,
    people and streets, the town's true state, which the lord reads back.
  - **craft_weapon**: forge a weapon of the kind the lord names; only a weapon the player forged, of that kind, counts,
    and the lord wields it.
  - **arrange_match**: win a lady for a lord (a real sway toward him), then her family's consent (`arrange_match`), and
    the game weds them. She must hold him a friend first; the family weighs it as any arranged match.
  - **A prison break is a rescue**: the freed lord, his family and friends remember it; the task names the prison
    (`Mapping.PlayerPrisonBreakPatch` on `EndCaptivityAction.ApplyByEscape`).
- **Capture tasks and the escaped target** (fkasad): a beaten target who escapes is recorded; the giver may settle for
  half the reward or take a lord of the same realm in his place; a lapse while he is a fugitive is no fault.

### Fixed
- **A bow is no case** (Maifailun, 01/10/2026): `sway_opinion` now needs the person named by the player in that
  conversation, and the court verbs' cooldown markers (`[cr_sway:...]`) no longer show in memories or reach the prompt;
  old saves are cleaned at load.
- **Town duel** (Zivkyn, Maifailun): companions stand aside, and the square waits for the town menu (defensive; the
  settlement crash is still unconfirmed until a `[DUEL]` log arrives).

### For modders
- **`CrMemoryEntry.Private`** (tashmetu): an injected memory said in confidence stays out of other characters' prompts.
- The warm-start MCM hint now says "when you next speak".

### Developer tools
- cr.testall: escaped_target, private_memory_injection, town_food_memory, grain_relief_destination, earn_regard_quest,
  plead_case_quest, town_needs_supply, garrison_reinforcement, prison_break_rescue, ransom_envoy_quest,
  scout_town_quest, recruit_troops_quest, craft_weapon_quest, arrange_match_quest; rp_sway and the court tests changed;
  quest_menu's referee is the SDK parser (`QuestMenuTokensTests` guards it before launch).

## v2.6.9.. 2026-09-30

### Fixed
- **A private audience granted in character clears the room again** (Lufaine, Maifailun, Nexus, 30/09/2026: "the npc
  will agree, emote a private departure, but the audience is still there listening to every word"). In the two-call
  chat the interpreter's actions replaced the scene model's, and the scene model is the one taught to answer a privacy
  request (request_privacy) while the interpreter is told not to record an implied one. `SceneDecisionMerge`: the
  character's own decision about the room stands when the interpreter recorded none; deeds stay the interpreter's.
  New console command `cr.private` clears the witnesses of the open conversation. `cr.testall
  privacy_survives_interpreter`.
- **No task is taught to a character at war with the player** (fkasad, 29/09/2026: an enemy lord buying back a
  prisoner slid into demanding a deed, then into invalid-quest messages; "Vanilla lords don't give quest to enemies").
  OFFERING TASKS and the deliver_prisoner bargain are withheld at war; tasks given before the war are still known, and
  trading prisoners (a verb, not a task) stays. `cr.testall no_tasks_from_enemies`.
- **The captive rule never blocks talking to a prisoner** (Gabriel, 30/09/2026): a conversation with the player's own
  captive begun at sea is received at landfall again, and a captive in the room is a witness again.

### For modders
- **`RegardCause.SelfTest` ("self_test")** (tashmetu, 30/09/2026): every regard change made while `cr.testall` runs is
  reported with this cause, whatever path made it; the tests restore regard without a notice, so a mod mirroring
  regard should skip it. EXTENSION_API.md also states which deeds write the game's relation as well: of CR's regard
  changes, only a divorce.


### Fixed
- **A witness addressed by name in Japanese answers** (yozakura12, Nexus, 24/09/2026: 「カリー、聞かせてくれるか？」 stayed
  with the main character and カリー never got a turn). `AddressedSpeakerDetector` split on spaces and read a Latin comma;
  CJK punctuation is now understood, a name opening the line before 、 or ！/？ (or closing it after 、) is an address, a
  name followed by a particle stays a mention, and two-character CJK names count.
- **A lord announced as riding out to meet the player now does** (Lordfadooboo: "passing me by or going into a city").
  The engage order was a bare nudge the lord's own AI took back on its next cycle; the lord is now held on course (the
  AI lock the escort and the couriers use) and released on every end of the chase; one called into an army stands
  down. `cr.testall interception_holds_course`.
- **The dead no longer sit at a council** (Maifailun: "my characters dead father is also partaking in the meeting").
  The game keeps the dead in a clan's heroes, and a dead lord's travel estimate read 0 days.
- **Every action a character takes now asks one gate** (Gabriel, 30/09/2026: "s'asseoir a la table devrait etre une
  action"). `InitiativeGate` became `ActionGate` with a rule per act, and reaches every path an inventory found: a
  council seat, a letter the player sends (no longer received by a hero taken out of the world), a warrant (never from
  a dead, disabled, child or captive ruler), witnesses, a held landfall scene, a prisoner's envoy on arrival, marriage
  prospects, hostages and wards, duels, companion missions, take_into_service, follow_me, an ambush paymaster or a
  nemesis at scene time, council pacts executed later, and quests of a giver gone from the world. The engine counts
  a DISABLED hero as alive (`Hero.IsAlive` means only "not dead"); `HeroPresence.IsInTheWorld` is the one reading of
  presence, and a ratchet counts every bare `IsAlive` read. `cr.testall action_gate` (renamed from initiative_gate),
  `council_attendance`.
- own_body read a "maiming" from `Hero.IsDisabled`, which is a hero taken out of the world; nothing is claimed now.

### New
- **A friendly clan warms a first meeting** (tashmetu): once per character, a clan counting the player a trusted
  friend (+30) starts its member at friendly regard, a favourable one (+10) at cordial, never beyond, never lowering,
  never over a personal offence. MCM "Clan standing warms first meetings", default on. `cr.testall clan_standing_seed`.
- **The mood of the day, finished** (ROADMAP, 14/09 and 30/09/2026): read off the game (wounds, purse, the realm's
  war, a hall or the field, the hour, captivity, a recent blow or gladness), the same man all day; one "YOUR DAY" line
  in the per-turn tail that colours delivery and never decides; the player lifts or sours it during a conversation
  (the character's own `mood:` line in `[STANCE]`, one band a turn, two a conversation); letters carry the writer's
  day. `cr.testall mood_of_the_day`.
- **Self-esteem** (Lordfadooboo, Maifailun): a met character's regard for himself, from station, a personal draw and
  valour, moved by battles, captivities (each landing harder than the last), tournaments, fiefs, a death in the house,
  duels with the player, violence, and reassurance; it mends with time and leans the mood of the day. A shaken
  character is quicker to take offence and wants reassurance; a proud one expects deference. `cr.testall self_esteem`.
- **How they seem**: the chat's character dossier (both skins) shows one numberless sentence of how the character
  carries himself and how his day shows. A proud or calculating character hides a shaken spirit from strangers; those
  who know him well see the strain beneath it.

### For modders
- `RegardChanged` has a new cause, `clan_standing` (it comes from the game's relation: a mirroring mod skips it).
  EXTENSION_API.md: `AddMemory`/`GetMemories` are called on the game thread; an injected memory never moves regard.


### Fixed
- **A child never takes the initiative toward the player** (Nexusrrr, Nexus, 28/09/2026: "my son is a 0-year-old baby,
  but he wants to discuss warfare with me"). The fief audience checked no age, and an inventory of every path where a
  character acts first (letters, road interceptions, audiences, stance consequences, ambushes, realm tidings, trial
  seats) found about a dozen filters, two of them checking age, several trusting `Hero.IsLord`, which is an occupation
  and not an age. The widest, a stance consequence, could write a threat, force a scheme or arm an attempt on the
  player's life from a child of another house. Every path now asks one gate (`NpcInitiativeGate`, live side
  `InitiativeGate`): alive, of age, free, not the player; each keeps its own conditions on top. An attempt armed by a
  child before the gate existed is disarmed. Children still answer when spoken to. `cr.testall initiative_gate`,
  `letter_place_and_audience_age`.
- **A lone player no longer rides "with your own retinue"** (Lordfadooboo, Nexus: "a letter from them that mentioned my
  riding in the field with THEIR retinue... i am alone"). The writer's own whereabouts phrase, addressed to the writer,
  was reused for the player. The player's is now its own (`LetterWhereaboutsPolicy.ForPlayer`): riding alone when the
  party is the player alone, at the head of their own company otherwise.

### Architecture
- `docs/maps/initiatives.md`: every initiative kind and the places that ask it, and every letter written straight into
  the post with whether its file asks the gate. Two walls in `RatchetTests`: a kind declared and never asked, and a
  letter added to `SentLetters` in a file that never asks.

## v2.6.6.. 2026-09-29

### Fixed
- **The chat no longer opens over the naval map** (Windmolen, Nexus, 28/09/2026: "the chat window opens but flickers
  a few seconds and then my game crashes. The last time it was a naval move."). At sea no conversation scene can load,
  and the chat opened straight over the live naval map instead. A conversation asked for at sea now waits for landfall
  (`StagingHostPolicy`), and a prisoner's envoy, who rides, only arrives once the player is ashore. `cr.testall
  landfall_wait`.
- **A provider pin with no provider left is named** (Windmolen, 29/09/2026: "unable to talk to characters... I have
  tested my connection in game and there appears nothing wrong with it"). With providers pinned in MCM and fallbacks off,
  OpenRouter answered "No endpoints found" once none of them served the model any more, and the chat said only that the
  AI did not respond. It now says which model and how to fix it (`ProviderPinFailurePolicy`), and the MCM Test Connection
  sends the same pin the chat sends, so it fails where the chat fails.
- **In a town duel the player is armed too** (Thoragoros1, 29/09/2026: "He comes out dressed in his armor and weapons,
  and my character is using her civilian clothing"). The opponent was spawned in battle gear, the player stayed in the
  town's civilian outfit; the player now takes up their battle gear when the duel begins.

### New
- **Regard earned by letter** (Lordfadooboo, Nexus, 28/09/2026: "exchanging letters with someone a dozen times and
  yet when you meet them again they are still only a +1"). A letter may emit no [ACTION], so letters never moved regard
  at all. The reader now judges each letter the player sends, in the call that already writes the reply, on a
  `[LETTER_TONE] warmer|same|cooler` line (asked even when they decline to answer; stripped before the player reads the
  letter). `LetterRegardPolicy`: a warm letter is +1 at most every 5 days per correspondent (3 on Warmer, 1 on Very
  Easy), and letters alone stop at "friendly regard" (`Regard.FriendlyTier`); a cold or insulting one is -1 on no
  timer, never past `Regard.WaryTier` by letters alone. `NpcProfile.LastLetterRegardGainDay` (SDK, additive).
  `cr.testall letter_regard`.
- **The game's own mercenary companies are judged when they leave a war** (Zivkyn: "why does nobody resent the
  Skolderbrotva?"), by fkasad's 2.6.1 grid unchanged. The realm's lords take it out on the company's leader through the
  game's relation (the mod's regard is toward the player only), and word goes about only of an abandonment or a
  betrayal (`MercenaryDeparturePolicy.CompanyDepartureIsNews`). Save key `CR_MercenaryCompanyDepartures_v1bytes`
  (additive). `cr.testall mercenary_company_judged`.
- **A vassal holding no fief who leaves a war is judged like a mercenary** (fkasad: "Should also apply when leaving as
  vassal with no fief"), the oath weighing like kinship: a fault x2, x3 for a vassal of the realm's own people, never
  thanked for leaving a winning war, worded as a renounced oath. One who leaves with fiefs takes them to war against
  the realm and is not judged; the plain departure news stands at peace. `cr.testall vassal_departure_judged`.
- **Greater lords write through their household** (Gabriel, 28/09/2026: an empress wrote in her own hand to a landless
  newcomer). A ruler, or a house two tiers above the player's, has the letter written by "the chancellery of Her Majesty
  Rhagaea" or "the steward of Lord X" (a title, never an invented clerk), formal and in the third person, unless the
  matter is private (love, marriage, family, threats, blackmail, a bribe), the bond personal, or the regard deep
  (`CorrespondenceRankPolicy`). Such a letter stays in the lord's letter history, headed by who wrote it, and in the lord's
  memory as a letter they had written. The same rule keeps a greater lord from riding out in person to intercept the
  player. `cr.testall correspondence_rank`, `letter_memory`.

### For modders
- **`CrNpcMemory.RegardChanged`** (tashmetu, 29/09/2026): fired on every actual change of a character's personal regard
  for the player, with before, after and the cause (`CrRegardChange`). Every change now goes through one place
  (`ProfileMutator.ApplyReputationDelta`, with a `RegardCause`), guarded by a wall in the suite. CR never writes the
  game's relation from regard; EXTENSION_API.md says why and how a mod may. Additive.
- `RelationToPlayer` (`VerbFacts`, `PromptVarFacts`) is documented as CR's personal regard, never the game's relation;
  EXTENSION_API.md names the prompt's two words for them ("your clan's standing", "your own personal regard").
- Three regard sites that did not clamp to -100..100 (violence, a departure from a realm, an offer of service by letter)
  now do.

### Architecture
- **Rank as a type**: `Stature` (clan tier and rulership, with the marks already in use named once: Established 1,
  LandedNoble 3, GreatHouse 4) and `StatureReader`, the one place rank is read from the game. The duel's station moves
  into it, pure and tested. No behaviour moves.

### Developer tools
- `cr.balance_survey on|off|now|report`: a weekly reading of every realm (fiefs, strength, wars, allies) and every war,
  peace and alliance, with graphics memory beside each reading, written to `Logs\CR_BalanceSurvey_<campaign>.csv`.
  First run (6.7 game years): no realm ran away with the map.
- New `cr.testall` tests: `landfall_wait`, `balance_survey`, `correspondence_rank`, `regard_changed`; `letter_memory`
  extended.
- Maps: `engine-facts.md`, which fact of the game each rule reads (a duplicated concept hides under other names but reads
  the same fact); `save-format.md` proves every key reaches the save.
- `save-format.md` now proves every key reaches the save: 20 keys were listed only because their constant was named
  *SaveKey*. The map follows a key through the helpers that make the SyncData call, and a wall in `RatchetTests`
  refuses a key found by its name alone from now on (a helper the map cannot follow, or data nothing saves any more).

## v2.6.5.. 2026-09-27

### Fixed (tashmetu, Nexus, 27/09/2026: the prisoner envoy)
- **A captive is given away only when the player says so.** An envoy's "final offer of eleven thousand five hundred"
  was answered "i will return him", the envoy wrote price 0, and the captive went home for nothing. A gift now needs
  the player's own words (free, for nothing, no ransom, or 0, with no other sum named: `EnvoyMandatePolicy.PlayerGivesAway`);
  a plain acceptance closes at the envoy's last offer, read from its own lines in digits or in words
  (`SpokenFigure.LastIn`). The envoy's prompt says the same (SDK).
- **The envoy keeps its ceiling to itself.** It told a player "my lord's final authority extends only to 4,839" and
  then offered less than the player's figure: it now never names the ceiling, and may accept a figure within it after
  a round or two of haggling.
- **An exchange's gold is explained**: it evens out the two captives' worth and is not a ransom offer, so it no
  longer reads oddly beside the opening ransom figure.
- **No envoy from the player's own house, and no captive offered for himself** (2.6.4): the player's own heir came
  "under a flag of truce" to buy the player's captive, and offered "another Rex Derthert", the same man, in exchange.
  A lord of the player's own clan never sends an envoy (`PrisonerBuyerCandidate.IsOfPlayersClan`), the wanted captive is
  never his own counterpart (`ExchangeCaptiveFacts.IsTheWantedCaptive`), and a companion or clan member is no longer
  taught to exchange the player's own prisoners for the player's own prisoners (`PrisonerExchangePolicy.IsAnotherSide`).

### Maps
- A silent error fixed: `shared-state.md` listed a readonly static as a collection whose contents change whenever its
  type CONTAINED "Dictionary" or "List", so six read-only tables (IReadOnlyDictionary, IReadOnlyList) were listed
  wrongly. Read-only views are recognised now (307 statics can change, not 313).

## v2.6.4.. 2026-09-27

### For modders (tashmetu, Nexus, 27/09/2026)
- **A character's FACTION BEHAVIOR block is chosen by their own people.** The realm helped choose the "If you are
  from ..." block for everyone, but a companion's realm is the player's and a notable's, wanderer's or gang leader's
  is whoever holds the town: a Battanian companion of the "Kingdom of Rhodok" read the Rhodok block too, and a
  Vlandian notable read "If you are from Kingdom of Prueba reino:" above Vlandian text. The realm now counts only when
  its culture is the character's (`FactionGuidelinesPolicy.RealmOfOwnPeople`), and the culture alone is tried first.
  A lord's runtime-renamed realm (Bellum Civile) still retitles his own block. `cr.testall faction_block_realm`.
- **cr_patch `!block <people>`** replaces, adds or removes one "If you are from ..." block by its heading, with the
  lines under it up to the next directive; a new people is placed inside the FACTION BEHAVIOR section. No more
  `!remove`-ing a section line by line, where a line CR later rewords stays behind. cr_patch and its directives are
  documented in HOWTO_Actions, section 7 (they never were).

### Fixed
- **A refusal's reason is a rule of the game, not a story** (the rest of tashmetu's ransom report). The note every
  refusal leaves for the model said the deed did not happen, never that its reason is a rule: the model explained an
  empty purse as a seized treasury and remembered it. It now says not to explain the reason within the world nor to
  remember it as something that happened (`ActionRefusalNote`, all refusals at once).
- Four refusals written as small scenes are plain statements: an execution or real harm the game does not allow,
  killing turned off in the settings ("You could not bring yourself to end ..." also put a hesitation in the player's
  head), and two captive handovers that "fell through".
- **One payment per deal, for every verb that moves its own coin**: a give_gold or take_gold beside buying, selling
  or exchanging a captive, paying for silence, paying blackmail or a robbery is dropped as the same payment
  (`TurnPaymentPolicy`); 2.6.3 covered captive sales only.
- **Buying a captive yourself**: the price band is known before a figure is named, and the receipt says why the
  figure paid differs from the one asked, above as well as below (`PriceDifferenceNote`).

## v2.6.3.. 2026-09-27

### Fixed
- **A captive's ransom is paid once, at the figure the buyer could really pay, and says so** (tashmetu, Nexus,
  27/09/2026, on 2.5.25). A lord named "fifteen thousand denars ... I will pay it now"; the game paid 5098 (the price
  is held to three times the captive's ransom value, then to the buyer's purse), then attempted a SECOND payment from
  a give_gold the model had also emitted. Its refusal, "(... reaches for a purse that holds nothing ...)", was read by
  the model as a fact, and the lord accused the player of seizing her treasury and remembered it.
  - buy_prisoner's teaching gives, per captive, the most THIS buyer can pay, or that they cannot raise a fair price
    (`PrisonerTradePolicy.MostABuyerCanPay`), so the figure is known before it is named.
  - A payment short of the figure named says why (`ShortfallNote`): all the buyer could raise, or the most such a
    captive fetches.
  - A give_gold or take_gold in a turn that also buys or sells a captive is dropped as the same payment written twice
    (`IsPaymentForATradeThisTurn`), whichever came first.
  - The empty-purse refusal is a plain statement: "(<giver> has no coin to give, so none changed hands.)".
  - Tests: `PrisonerTradePaymentTests` (11); `cr.testall prisoner_ransom_home` asserts the player is told why the
    figure named was not paid.

## v2.6.2.. 2026-09-27

### For modders (tashmetu, Nexus, 26/09/2026)
- **The council can carry your text, one seat at a time.** The council prompt carried no modder text at all (no
  behavior_guidelines, no cr_patch, no `{{variables}}`), so a bridge's view of a lord never reached the table.
  Contribute `ModuleData/cr_patch/council_seat.txt`: for each seated member CR expands it with that member's own
  variables, on the game thread, and prints it under that member's roster line as "<Name>'s own view (not common
  knowledge; it shows only when the matter touches it)" (SDK `CouncilSeatNote`, `CouncilMemberInput.ModderNote`; mod
  `CouncilSeatNoteResolver`). New `PromptVarFacts.CouncilSeat` (added member, nothing removed): "you" is the PLAYER at
  that table, so write about the seat in the third person. A note is cut at a sentence past 700 characters. CR ships
  no council_seat.txt: without a contribution nothing changes and no provider is called. HOWTO_Actions section 6a;
  `cr.reload_prompts` and `cr.lore_status` know the file; `cr.testall council_seat_note`.

### Fixed
- **Sergeants and bounty hunters no longer ride back to their town on every save.** A save took them off the map and
  sent a fresh band from the town on the next hour, so with frequent autosaves a band riding from far off might never
  reach the player. They are now put back where they stood once the save is over, the couriers' own recipe, now
  shared by all three (`TransientPartyRegistry`).

### Changed
- **"friendly regard"** is the word from 10 to 14 on the personal-regard scale (was "cordial regard"), where a flirt
  can turn into courtship (`Regard.FriendlyTier`); also on the encyclopedia's personal-regard row. No rule moved.
- **A captor at exactly -5 regard is no longer told they bear the player no goodwill**: that note starts at -6
  (`Regard.CoolTier`), where the scale stops saying "neutral".

### Code design (driven by the maps; no behaviour change)
- **One service for the mod's transient parties** (couriers, sergeants, bounty hunters: `TransientPartyRegistry` over
  the pure `TransientPartyLedger`, 6 tests). The event-listeners map showed the same save lifecycle copied three
  times; it is now one listener of each kind. The engine REUSES a destroyed party's id, so a restored courier clears
  the old id's escort and waylay state first. `cr.testall transient_party_registry`, `courier_save_restore`.
- **Every regard threshold written in place now points at a named rung** (27 to 0): new `Regard.FriendlyTier`,
  `GenuineTier`, `CoolTier`, `SouredTier`, `VanillaRelation.NeutralTier`, `WaryTier`. The inline ratchet is now a
  wall: the next threshold written in place fails the suite. A quest reward's amount stopped being called a
  "relation", and a sign printed for display stopped counting as a decision.

## v2.6.1.. 2026-09-26

### Added
- **The end of a mercenary contract is judged by its circumstances** (fkasad's grid, Nexus 25/09/2026, ratified by
  Gabriel; Zivkyn's report that NPCs called a sellsword an oathbreaker was the 2.5.24 fealty wording, already fixed).
  `MercenaryDeparturePolicy` (pure, 9 tests) and `MercenaryReputationBehavior` (additive save key
  `CR_MercenaryDeparture_v1bytes`): the realm at peace, no fault; at war and winning (its strength at least 90% of
  its enemies' combined), let go with thanks (+3 regard); at war and losing, abandoned (-8); going over to its enemy
  within 30 days, a betrayal (-20, charging only the difference); doubled against a company of the realm's own
  culture. Every clan leader of the realm remembers it (regard and memory) and word gets about in the verdict's own
  words. Coming to a realm of one's own people in a war it is losing is remembered well (+5). `cr.testall
  mercenary_departure_judged` judges real realms without applying anything.
- Maps: the first live RATCHET case. The bare-number regard ratchet caught the four new regard constants; they are
  SHIFTS on the ledger, not thresholds anything is compared against, so they are named as penalty and gratitude
  rather than the baseline raised.

### Changed (code design, driven by the maps; no behaviour change)
- **The regard map finds thresholds by what the code COMPARES, not only by name.** 33 regard thresholds written in
  place were invisible to the name-based list; they are listed now (`ledger` or `named`) and ratcheted
  (`RatchetTests.InlineRegardComparisonsOn26September`, 33 -> 27 the same day).
- **`Regard.NeutralTier` (SDK)**, the line between goodwill and ill will; the trial's default verdict points at it.
- **The "which ledger" column is read off types.** Ten regard rules said nothing about which ledger they read; each
  now takes the one its callers really pass (`VanillaRelation` or `Regard`), so the compiler holds every caller to it.
  `CompanionRapport.Band`'s rungs name `VanillaRelation`'s own tiers. A reward misnamed "relation" was renamed; the
  map's doc fallback now reads the whole doc (it read only the first sentence).
- **Maps that mix detection modes say, per line, which produced it** (`save-format.md` fact/name with the fact read
  first, `gates.md`, `decisions-regard.md`), written into `docs/maps/README.md`.

## v2.6.0.. 2026-09-26

Two new pillars from player reports and Gabriel's own design: letters that are never lost for good but can betray
their writer, and arrests in the King's name (off by default, experimental).

### For modders (tashmetu, Nexus, 26/09/2026)
- **character_overrides.json can key an entry on a hero's TEMPLATE.** A mercenary captain's StringId and name are
  rolled per campaign, so an entry keyed on them stopped matching in every new game; an entry whose `id` is the template
  the hero spawns from (`spc_skolderbrotva_leader_0`) now applies in any campaign (`CharacterOverrideSet.Resolve`: the
  hero's own id first, then the template, then the name).
- **cr.lore_status lists the prompt files read from Custom**, flags any whose shipped version is newer than the
  player's copy (a Custom copy replaces the shipped file entirely, so CR's later changes stop reaching the player), and
  says whether world.txt is sent at all: the Compact prompt drops it.

### Added
- **Arrests in the King's name, trials, outlaws and brigands** (Gabriel's design, 26/09/2026; MCM "Arrests and
  outlaws", OFF by default; never a kingdom's ruler).
  - A vassal or mercenary who does something grave (a letter of theirs read as treason by a lord of their realm,
    violence against a member of their realm, the game's crime rating turning severe there) gets a warrant in their
    liege's name (`ArrestPolicy`, `ArrestCase`, `ArrestBehavior`, save key `CR_ArrestCase_v1bytes`).
  - Sergeants in the liege's colours ride from the nearest town of the realm and read the warrant: surrender, or refuse.
  - Surrender: the player alone is put in the liege's nearest cells, and tried by a COUNCIL over the captivity
    (`CouncilScope.Trial`, the one council captivity does not block): the liege presides with the vassals who hate the
    player most, wherever they are; the prompt makes the table the player's judges (SDK); the verdict is a new council
    motion (`ResolutionKind.Verdict`) applied at the lift. Acquitted, every charge is wiped; convicted, prison for a
    term the charges set, then release. A trial that rose without a verdict is decided by the bench's regard
    (`TrialVerdictPolicy`), so closing the window is no escape. Ransom offers are suppressed while the law holds the
    player; an escape is a flight.
  - Flight (refusing, beating off the sergeants, breaking out): the player leaves the kingdom (a mercenary the
    contract), a price is put on their head, and the realm's crime rating passes the game's own war threshold, so the
    game itself makes war on the now independent clan (`OutlawPolicy`).
  - Brigandage: attacking a caravan, a merchant ship or a fishing boat in peace outlaws the player; a merchant's sender
    and receiver both put a price on their head, a fishing boat only its owner (`BrigandagePatch`, a Harmony hook on the
    game's own hostile-act core).
  - Bounty hunters engage the player (in the realm's colours against an independent clan, under an outlaw clan
    against a sworn vassal). Taken, the player is handed to the sovereign who wanted them and tried by that realm.
    Each realm may declare a rare amnesty (1% a week): bounty lifted, crime cleared, war ended, outlaw pardoned.
  - The council's reply ceiling rises from 1500 to 3000 tokens (a reasoning model starved it, as it did the envoy).
  - `cr.testall`: `arrest_warrant_and_bench`, `brigandage_patch_attached`. The trial over a settlement captivity, the
    hunters and the handover have NOT been run in game.

### Changed
- **Letters lost on the road: resent until they arrive, and an intercepted secret travels in the world.**
  raphareish (Nexus, 24/09/2026) asked whether a sender knows their letter was intercepted: asked about it, they said
  they had "been busy". The sender's memory of writing was recorded only on delivery, so a lost letter left nothing.
  Gabriel's rulings (26/09/2026), in five increments:
  - `CourierSecretPolicy` (pure): what took the courier (bandits, a troop at war with the courier's clan, or anything
    else), what becomes a secret (intercepted AND with substance, always, no roll), who takes up a carried secret
    when its carrier is beaten, a 60-day shelf life, and the lines the sender and the reader remember.
  - The sender remembers writing when the letter is SENT (`LetterMemoryFinalizer.RememberSending`, once, via the
    additive `PendingLetter.SenderRemembers`), and what befell the courier.
  - A lost letter goes again until it arrives, both directions, on a guarded courier (`Resent`, additive on both
    letter types); the Send-again prompt is gone. The player is told the honest cause.
  - `CourierSecretBehavior` (additive save key `CR_CourierSecrets_v1bytes`): bandits carry the letter, another band
    carries it on, a band going home leaves it in its hideout; any other troop that beats the carrier sends it up to
    the lord who answers for it (leader, else owner, else the owner of its home settlement: a Pravend patrol,
    Pravend's owner); an enemy troop hands it to its lord at once. The player's side beating the carrier, or clearing
    the hideout, recovers it unread.
  - The lord who reads it remembers what it said, word gets about, and one call asks how it touches them
    (`SecretReactionPolicy`): against their interests, a grudge on the author (and a little renown if the player
    wrote it); treason, a sharp grudge, more renown, and a letter confronting the player who wrote it.
  - MCM: "Intercepted letters can leak" and its 35% roll are gone; "Invulnerable Couriers" is the one switch.
    `cr.testall`: `courier_secret_travels`, `courier_secret_reaction` replace the two old leak tests;
    `courier_waylay` asserts the resend rides guarded.

## v2.5.26.. 2026-09-26

### Fixed
- **A retired predecessor is no longer called dead.** Since 2.5.24 the mod tells a retirement (or a character switch)
  from a death, but the heir note in the prompt still said the predecessor "has died", a falsehood the player can
  check. The profile now carries `InheritedPredecessorRetired` (SDK, additive, false on older profiles, which were all
  read as deaths), and the note says the predecessor "has stepped down and still lives".
- **`cr.testall succession` no longer damages the save it runs on.** It ran the real `SuccessionBehavior.Apply`,
  which reframes EVERY stored profile (private memories erased, all marked inherited from the current character) and
  writes a succession into the chronicle. The per-profile rule is now `SuccessionMemoryPolicy.Reframe` (unit-tested),
  `Apply` is a loop over it, and the self-test reframes a detached copy and asserts the save is untouched.

### Added
- **In-game tests for the prisoner trade of 2.5.24/2.5.25** (prisoner exchange, increment 5): `cr.testall`
  `prisoner_ransom_home` (a captive bought by his own clan leader goes home free, the player paid the clamped price),
  `prisoner_gift` (the envoy's gift deal moves no gold) and `exchange_prisoners` (real lords: the player's captive goes
  home free, the other side's becomes the player's prisoner, the evening gold moves). Each restores both purses and
  frees every captive it took. The envoy audience itself needs a model and stays untested in game.

## v2.5.25.. 2026-09-26

### Fixed
- **The envoy, from fkasad's first audiences (Nexus, 26/09/2026).**
  - "The envoy hesitates, and waits for you to speak" on most turns: the envoy's reply had a 700-token ceiling, and a
    reasoning model spent it thinking and returned nothing (his server showed the finished thought). Now the chat's
    own 3000 ceiling; an empty reply logs its finish reason.
  - Nothing of the audience reached the log, and `cr.prompt_dump` answered "nobody is being spoken to": each turn now
    logs a line, the full prompt under verbose logging, and `cr.prompt_dump` mid-audience writes the envoy's last prompt.
  - "The prisoner is yours for 0 denars" paid the full ceiling (20,689): a price of 0 or "free" was read as no price. A
    captive given away is now a gift, and no gold moves (`EnvoyMandatePolicy`, `buy_prisoner` `gift`).
  - A captive ransomed by his OWN side was put in his own lord's cells, and the game reported him "escaped". He now
    goes home free by ransom, as the exchange does, and bears no grudge for it. Applies to any `buy_prisoner`.
  - The envoy opens with an offer (half the ceiling, never below the honest floor) and haggles, as fkasad asked; he
    says an exchange is off the table when his lord holds none of the player's people; he no longer comes while his
    lord is himself a prisoner.

## v2.5.24.. 2026-09-25

A day of player reports, and one pillar they asked for: prisoners can now be EXCHANGED, and a lord who wants a captive
back sends an envoy to treat with the player instead of a letter.

### Added
- **Prisoner exchange, and an envoy instead of a ransom letter** (fkasad, 25/09/2026: a ransom offer was "merely an
  offer that can be accepted or refused", "several llm calls for no interactions"; "a lord who wishes to retrieve one of
  his own would dispatch a trusted agent to negotiate"). Gabriel's rulings: heroes only (lords, vassals, companions,
  never troops); always an envoy, never the lord in person; anonymous; announced by a popup.
  - `exchange_prisoners`, a new verb declared in every place the verbs map checks: the player gives a hero captive and
    receives one the NPC's clan holds. `PrisonerExchangePolicy` (pure): even within 25% of the dearer captive's worth,
    else the side that gets more pays the difference; the game's own ransom valuation, as the sale verbs use. A captive
    handed to his own side is FREED (the engine's ransom path, the one that keeps a player companion from turning
    fugitive; the player's own rejoins his party); otherwise he changes gaolers. Usable in ordinary chat too.
  - The envoy (`PrisonerEnvoyBehavior`) replaces the PrisonerOffer letter, carrying the same fair ceiling. A popup when
    the player stands free on the open map; a pipeline of its own (`EnvoyPromptBuilder`, SDK; `ChatViewModel.Envoy`),
    as the council has, because the 1:1 prompt is built from a hero and would make the envoy speak AS the lord. No hero
    in the chat, a troop of the lord's clan in the shot. `EnvoyMandatePolicy` rebinds whatever deal the model writes to
    the lord's mandate before the bridge runs it in the LORD's name (his gold, his prisons, his memory). One envoy per
    captive and lord every 20 days. Additive save key `CR_PrisonerEnvoys_v1bytes`. A letter an older save has pending
    keeps its Accept button.
  - Who may send one (`PrisonerBuyerPolicy`): the captive's house, a parent of ANOTHER house (a daughter married out),
    the realm, and a lord of the player's own realm who bitterly hates the captive, who wants him in his own custody.
  - NOT run in game yet; the scene staged with an anonymous troop is the part only play can confirm.

### Fixed
- **The council never returned to the game thread after its model call** (found building the envoy on its shape).
  Every other chat path awaits `MainThreadDispatcher.SwitchToMainThread()`; the council added its bubbles and applied
  its regard shifts from a thread-pool thread while the simulation ran.
- **Every renown penalty is logged** (a player asked whether CR caused his clan to lose whole tiers; it cannot: the
  engine only raises a tier, and CR writes none). Three of the five penalties logged nothing; `RenownPenalty.Apply` now
  writes a `[RENOWN]` line for each.
- **A council sitting was kept for the conversation resume** (yozakura12, 24/09/2026, whose log showed "[CONV RESUME]
  kept 23 line(s)" as a two-companion council closed). The close side excluded the companion briefing and the captive
  mode by name; a council is neither and runs in the ordinary conversation mode, so its transcript was kept under the
  anchor's id. Opening a private word with that companion the same day would have replayed the whole sitting into
  it, the other councillor's lines included. `ConversationResumePolicy.MayBeKeptOnClose` now asks what the chat
  OPENED as: only an ordinary conversation, the one kind the resume ever puts back, is kept. One rule for both sides,
  so a staged scene added later cannot be missed the same way.
- **A lord may ask you to capture a rival in peace, and the task holds** (FloatingMammal, 25/09/2026: "A lord tasked
  me with capturing his rival, but no quest was generated. When I brought the prisoner to him the text in brackets told
  me that there was no such bargain between us"). The capture was created, then voided the next day in silence by the
  rule that sets a war errand aside once peace holds, since there had never been a war; and handing the prisoner over
  settled nothing, since only a delivery bargain can be settled that way. Gabriel's ruling: two different tasks. In
  war, capturing an enemy is a war errand, set aside if the war ends (unchanged). In peace, capturing a rival
  (blackmail, vengeance, a scheme) is untouched by any war. `CaptureQuestPolicy`: a capture asked in peace is issued
  as the DELIVERY it is (take him, bring him into my hands), completed by handing him over. The refusal now says the
  captive "stays with you" rather than "in your hold" (a mercenary carries his prisoner himself), and a delivery reads
  as one in the journal.
- **A family that arranged the match is no longer affronted by it** (found tracing zhenja1's report; Gabriel's ruling,
  25/09/2026). Every marriage without a CR blessing record was judged unblessed: -40 on the spouse's whole family and
  UnblessedMarriage grudges on its kin. But a marriage the base game makes always had the family's consent (the clan
  head bargained it, or the family offered it). `MarriageGuardPolicy.FamilyApproves`: a marriage CR did not make is
  approved; only one CR sealed (`MarriageApprovalBehavior.MarryThroughCr`: the marry verb, a marriage quest reward) is
  still judged by its blessing, as before. The unblessed-grudge self-test now marries through CR's door.
- **A new spouse remembers the wedding, and how little she knows you** (zhenja1, 25/09/2026: after accepting a vanilla
  marriage offer, his bride spoke at their first word "almost like she'd been part of my house the whole time"; fkasad
  suggested preparing the bride for that meeting). Nothing wrote the marriage onto the spouse, and a spouse with an
  empty history is told "you are already married and share a life", a line meant for campaigns that start with one.
  The marriage listener now writes the wedding through `CrNpcMemory.AddMemory` (which creates her profile if needed),
  worded by `WeddingMemoryText` from what is true at the wedding: with a history of talks, simply "Were wed to X"; an
  arranged match with someone met once, newlyweds who "hardly know each other yet"; never met, "still strangers to
  each other". Applies to marriages made from this version on; a marriage already made has no date to recover.
  `cr.testall marry_companion` asserts the memory through the real engine event.
- **For modders: `PromptVarFacts.Lean`** (tashmetu, 25/09/2026: in "Compact prompt for small models" CR trims its own
  sections while a bridge's `{{variables}}` went in whole, and he was reading `ModSettings.LeanPrompt` by reflection to
  find out). True when the prompt the value goes into is built Compact. It is the level of THAT prompt, not the setting
  alone: `Build` computes the level once and hands the same value to the prompt and to the providers, and a letter,
  always built in full, says false. Additive; the modder-api map gains one line. `cr_patch` text is still static.
- **Switching characters with another mod was treated as the player's death, and erased memories** (Wallace1067,
  25/09/2026, with The Original Player Switcher: "switching out logs a death for the original character, switching
  back logs a death for the temporary one", with "[SUCCESSION] reframed 24 profile(s)" each way). `SuccessionBehavior`
  read EVERY `OnPlayerCharacterChanged` as a death, and the reframe is not a notice: it DELETES the predecessor's
  private history from every NPC and marks the rest inherited. Decompiled `ApplyHeirSelectionAction`: vanilla KILLS
  the old hero before handing over on a death and DISABLES him on a retirement, so his state when the event fires
  says which. `SuccessionTrigger.Classify` reads it: dead is a death, disabled a retirement (now chronicled as
  "retired", not "died"), anything else a switch, which reframes nothing and is logged. No suppression flag was
  needed: the engine's own state answers it for every switcher mod. Memories already erased in the reporter's save
  cannot be restored. `cr.testall succession_switch` classifies live heroes without applying anything.
- **Maps: a shipped modder API member may not vanish.** A blocking guard: `docs/maps/modder-api-released.md` holds the
  surface as released, recorded at each cut (`Tools/Update-Maps.ps1 -RecordReleasedApi`); a released member that
  disappears or changes signature fails the suite, update mode included.
- **Hiring on as a mercenary was recorded as swearing fealty** (tashmetu, 25/09/2026: his sellsword was told by
  Derthert "I've heard word you swore fealty"). `OnClanChangedKingdom` sent `JoinAsMercenary` down the vassalage path,
  so the news read "swore fealty" and the chronicle "bent the knee"; the leaving side already had its own contract
  wording. `RealmServiceWording.Joined` now words the start as what it is ("took service with ... as a mercenary
  captain", chronicled "for coin and not for oath"), at a smaller reach than an oath (35 against 55, never below the
  contract END's 30). Covers vanilla hiring and CR's own `join_as_mercenary`, since both fire the same engine event.
- Not changed, and why: the rest of that report is the council working as designed. A council turn is ONE model call
  that writes the whole table as a scene; `CouncilPromptBuilder` requires every member to speak at least once and
  allows a second block for cross-talk after everyone has spoken, so "Kari, Bella, Kari" and "Kari, Bella, Bella,
  Kari" are one reply each, rendered bubble by bubble (the parser was checked: one bubble per [SPEAKER] block). The
  "fallback (intercept line never fired)" open and the "orphaned staged mission" close are the two safety nets for a
  conversation that never got the engine's start token, both doing their job. Whether the model's scene itself
  repeats earlier lines needs the text; asked for `CalradiaRemembers_story.log`.

## v2.5.23.. 2026-09-23

A modder release, from reviewing the Bellum bridge: fed events now say who DID a thing, apart from who it concerns.

### For modders
- **Naming the player in `HeroIds` no longer makes a fed event the player's own deed; the player among the new
  `ExternalWorldEvent.ActorHeroIds` does** (tashmetu, 23/09/2026, wiring `PlaceId` into the Bellum bridge; found reading his `EventFeeder`). `Feed` set
  `InvolvesPlayer` whenever the player's id was among the heroes, and the bridge names a besieged, raided or revolting
  town's OWNER there. When the owner was the player, the siege left the news, entered the player's chronicle, and
  characters told it back to the player as something they had done. Being named is being concerned, not being the
  one who acted. A first cut added a `PlayerIsActor` flag; replaced before release (Gabriel, 23/09/2026, asking which
  shape the single-responsibility principle favours): a flag makes every bridge ask "is this the player?", a CR concern
  leaking into the feeder. `ActorHeroIds` lets the bridge describe ROLES (who did it, who it concerns) and
  `ExternalEventRules.IsPlayersOwnDeed` decides what is the player's. Actors also count as concerned (awareness lift,
  message relevance). BEHAVIOUR CHANGE for a bridge that relied on the old inference to file the player's deeds: list
  the player among `ActorHeroIds`. Nothing in CR itself relied on it (every internal `Feed` caller checked). Additive member, `ApiVersion` unchanged.
  `cr.testall feed_player_named` drives both halves through the real door.
- **The modder guides ship at last**, in `Modules/CalradiaRemembers/Docs` (`HOWTO_WorldEvents`, `HOWTO_Actions`,
  `HOWTO_Voicing`, `EXTENSION_API`). The Nexus page said "See the HOWTO_Voicing guide shipped with the mod" and
  nothing copied them; found cutting this release by checking the archive for the guide the changelog promised.
  The csproj deploy step now copies them, so the archive, built from the deployed folder, carries them.
- **`Extension/HOWTO_WorldEvents.md` gains three worked examples** (situation, call, what CR does): a siege of the
  player's town, an execution by the player's decree, a village's failing harvest. Ids checked against the game's
  data; `KillCharacterAction` decompiled to confirm the victim's dungeon is still readable when `HeroKilledEvent` fires.

## v2.5.22.. 2026-09-23

A modder release: fed world events can carry a place, and a console command says who has heard what. 2.5.21 was
skipped (Gabriel's numbering). The Actions panel built on 22/09 ships dormant, and the cut caught a defect in it.

### Fixed before it shipped
- **The Actions tile would have opened nothing in the default skin.** The new Actions panel (fkasad, 22/09/2026,
  increments 1-2) is drawn only by the Classic chat prefab, but the tile that opens it sits in every skin and
  `ExecuteOpenActionMenu` sent every click to the panel. The Manuscript skin ("Dark Leather") is the MCM DEFAULT, so
  most players would have lost manual action injection entirely. Found reading the prefabs at the cut.
  `ActionsPanelGate` now sends the tile to the panel only when the panel is switched on AND the open skin draws it;
  otherwise the vanilla menu opens, as in 2.5.20.
- **The panel ships switched off** (Gabriel, 23/09/2026): its layout was built blind and has not been seen in game.
  `cr.actions_panel on` turns it on for the session in the Classic skin (`cr.skin classic`), `cr.reload_ui` re-reads
  its layout on an open chat. Players see no change from 2.5.20.

### For modders
- **Fed world events can say where they happened, the heroes they name can speak of them, and a console command
  says who has heard what** (tashmetu, 23/09/2026, designing a test pass for a bridge feeding sieges, executions
  and rebellions: "Is there any console command or save-file way to check whether a specific event I fed has
  actually propagated to a given NPC's own memory yet?"). Answering him from the code turned up three gaps.
  - **`ExternalWorldEvent.PlaceId`** (additive, `ApiVersion` unchanged). The awareness model always had a
    proximity term, but `Feed` never set a place, so distance played no part in a fed event. Resolve-or-drop like
    the other ids, and the `[FEED]` log line now names the id, the refs and the place, plus a line when a
    PlaceId did not resolve.
  - **A hero named in `HeroIds` now hears it in conversation too.** The M-G6 concerned-party lift (+25, after
    recency) reached only `IsAwareOf` (sway exposure, jealousy); the prompt queries passed no observer, so the
    besieged lord of a faint siege report could not speak of it while the sway check said he knew. The hero-facing
    queries now pass the observer; settlement queries (commoners) have none and are unchanged. This applies to CR's
    own events as well: a lord named in a battle now hears of his own battle more readily.
  - **`cr.heard [name] [event-id|all]`**: the verdict from `RumorAwareness.Explain`, the same call the prompt's
    queries now score through (so the console cannot disagree with the prompt): score, each term, days left,
    "faded" versus "could never hear it", and which tags would reach them. `GeoBonusMax` and `ConcernedPartyBonus`
    moved from the store into `RumorAwareness` to make that one path. New modder guide `Extension/HOWTO_WorldEvents.md`.
- **`cr.feed_test` fed a fact nobody could ever hear.** Magnitude 30 with no tag is under the awareness threshold on
  its first day, while the console said it "reaches fewer people". It now feeds magnitude 60 tagged with the
  player's realm and the nearest settlement, and prints how far and how long it carries (`FedEventReach`, from the
  same arithmetic). `cr.world_event` (the player tool) now also takes the settlement named in its text as the place
  and prints the same reach line instead of "spreads as general realm news", which an untagged magnitude 50 fact
  did for one day only.
- `ExternalEventLifetime` now documents what it really decides: STORAGE (a Rumor is pruned at 42 days, a Deed is
  kept, so dedup on its id holds for good). Both fade from conversation on the same 40-day curve. Deliberately not
  changed: making a Deed talked about longer would be a design change to the awareness model, not a fix.

## v2.5.20.. 2026-09-21

Four player-facing changes, one of them a verb that has been unreachable since July and another a conversation
that survives a break, plus a day of work on the thing underneath: views of the system, generated from its own
source, that a test keeps honest.

### Added
- **A conversation you stepped out of is still there when you come back** (fkasad, 20/09/2026: "I quit the chat,
  save and close the game and live my life. Then come back and reload... He remembers talking to me but most of
  what we said is gone. I need to restart the negociation from the begining"). Roleplay outlasts a play session,
  and CR kept the SUMMARY of a conversation and dropped its words, which is right for one that ended and wrong
  for one that was merely interrupted. The last conversation now rides in the save (`CR_ConversationResume`,
  additive, tolerant): reopen with the SAME person on the SAME day and the exchange comes back, in the bubbles
  and in what the character knows, because the transcript IS the prompt (`ComposeSessionFor` rebuilds each
  session from it before every call, so one stored fact restores both). Opening any chat consumes it, which is
  the report's own rule and makes "he has not talked to anyone else since" need no bookkeeping. One afternoon
  stays one memory: the resumed sitting inherits the event the first close wrote rather than adding a second,
  and the summary queue now lets a fuller account replace a shorter one still pending for the same anchor.
- **A duel can be fought in a castle.** `DuelEligibilityResolver` stopped demanding `IsTown` on 10/09/2026 on
  FloatingMammal's report that castles never offered the duel; `TownDuelBehavior.Arm` kept demanding it and was
  never reconciled, so in a castle the duel was offered, armed, and then silently refused. The game's own data
  settles it: `castle_complex` owns a `center` location, the Castle Courtyard. A village answers to
  `village_center`, so the existing "has a duelling ground" test already excluded everything with nowhere to
  fight, and the kind test was redundant as well as wrong. The diagnostic commands were measuring their own
  construction (`cr.duel_arm` mimicked the venue choice with an `IsTown` test of its own and so could never have
  found this); both now go through the real path, and the success log names the ground.

- **A partner can accept that you keep others, without it becoming an open relationship** (yozakura12,
  22/09/2026: "accepting the player having multiple wives is not the same as agreeing to a reciprocal open
  relationship... The player may have multiple wives/consorts, but each wife remains exclusive to the player").
  New verb `accept_other_partners`, distinct from `open_relationship` in the catalogue, in the prompt and in what
  it does: open terms are reciprocal and suppress her jealousy outright, this is her consent to HIS other loves
  and changes only what jealousy COSTS her. `JealousyTrack` gives the system three prices instead of two, the new
  one a factor on the betrayal band (5/13/20 against 10/25/40 and 3/6/8) with a floor at `Regard.WaryTier`: she
  can come to resent it, never to hate him for it, and once there the grievance costs only the scene, which is
  what he asked to keep. `PluralAcceptancePolicy` is Gabriel's ruling that the culture sets the PRICE, not a
  veto: a people who already share ask nothing extra, a people who call it betrayal ask for devotion. An act
  OUTSIDE what she accepted is a betrayal at full price whoever she is.

### Fixed
- **`grudge_terms` could never be emitted.** The verb had a class, a teaching, a registry entry and a `cr.testall`
  self-test, and was in NEITHER `GameActionCatalog` NOR the bridge's `HandledActionTypes`. The parity self-test
  compares exactly those two, so it saw nothing wrong. The interpreter only learns what the catalogue lists, so in
  the two-call mode every player runs, no character could emit it: asking a lord what would mend a grievance
  (`Grudge.ResolutionHintDiscovered` has no other writer) has been impossible since it shipped in July. Found by
  `docs/maps/verbs.md` on the day that map was built.
- **Lord recruitment read the wrong ledger** (Gabriel's ruling, 21/09/2026). It gated on vanilla `GetRelation` while
  `NotableRecruitmentPolicy` and `PrisonerRecruitmentPolicy` gated on `ReputationWithPlayer` at the same mark of 60,
  and the notable policy's doc claimed all three matched. A lord befriended across a campaign IN CONVERSATION could
  stand at 80 of the mod's own regard and be refused to his face. Vanilla relation stays the fallback for a lord
  with no profile yet.
- **Rescue gratitude reached infants.** Every grudge path already gated on adulthood; the rescue path did not, so a
  baby recorded "my house owes you a debt". Both the family and friends passes now go through
  `RescueGratitudePolicy.CanFeelTheDebt`.
- **The 10000-denar single-transfer ceiling lived in five places** under four spellings, two of them bare literals in
  the bridge where it is actually enforced. `BastardBlackmailPolicy`'s own doc explains the trap: a demand above what
  one `take_gold` carries is structurally impayable, so the gold goes and no silence is bought. One home
  (`GoldTransferLimit`); each policy may still be tuned LOWER, none can drift above the bridge.

### For modders
- **A closed plural house is now expressible.** `jealousy_cultures.json` gains `outsidePartnersAllowed` (default
  true, so nothing moves). `patriarchalHousehold` was carrying two facts at once, that a people keeps plural
  households AND that a neglected wife of that people strays; yozakura12 wanted the first without the second.
  `IsHaremCulture` now requires both.
- **The chronicle stops losing a chapter when a fed fact is retracted** (tashmetu, 22/09/2026, flagging before
  publishing that his bridge is the first caller anywhere to use `Feed.Retract`). The illuminator's bookmark was
  a COUNT into the deed list, exact only while that list could grow; retract a deed already written up and the
  next illumination started one entry too far in and silently skipped a new one. It is now an IDENTITY, found
  again wherever the deed moved to, with the count as the fallback for older saves.
- **A total conversion may bring its own calendar.** `setting.json` gains `daysPerSeason` and `seasonsPerYear` beside
  the world's name and adjective it already carried. Until now a conversion could rename the world while its year
  stayed 84 days long, so every "a season ago" the mod wrote was wrong for it. Absent or nonsense values keep
  Calradia's 21-day season and four-season year, so a stock install is unchanged.

### Under the hood
- **`docs/maps/`: eleven generated views of the system**, regenerated from the source by the test suite and compared
  with the committed copy, so they cannot rot while the suite is green (there is no CI that can build this mod; the
  suite is the trigger). Every type and namespace, every dependency edge with its cycles, every numeric constant,
  every engine event listened to, the prompt's sections in order with their cache zone, every verb with all seven
  places it must be declared, everything static that can change, a swept decision table for the regard scale, and
  two CONTRACTS: every save key (a key that disappears is refused outright, since a renamed key silently loses a
  player's data) and the whole modder API. `RULES.md`, which existed and had sat ten days stale, is generated the
  same way now.
- **Duplicated knowledge, consolidated where it was found:** `Regard` and `VanillaRelation` give the two -100..100
  ledgers one home each for their rungs, so "how friendly is a friend" stops being answered in seven places;
  `WorldCalendar` does the same for the season and the year. No tuning value moved, and the swept decision table is
  the witness: across the whole batch it changed only where a declaration stopped being a bare number.
- `SchemeAgentPolicy.MinPlotterRegard` is renamed `MinPlotterRelation`: it reads the engine's ledger, and "regard"
  is this mod's word for its own. That kind of lie is how lord recruitment came to gate on the wrong one.

## v2.5.19.. 2026-09-21

Two fixes that missed the 2.5.18 upload by minutes, one from a player and one from a modder who asked the right
question before it bit him.

### Fixed
- **Buying a prisoner ran the trade backwards** (Lordfadooboo, 21/09/2026: "he states his price and agrees to
  the transfer... but then i get a CR message that says 'you hold no such captive to sell' but im not selling a
  captive, im buying one. and then the transaction gets blocked even though my money is already taken"). The
  model heard the player say "buy" and emitted `buy_prisoner`, the mirror verb (the NPC buying FROM the
  player). Both verbs were taught and each spec already names the other as its anti-pattern, so teaching harder
  was not the answer. Custody is: `PrisonerTradePolicy.DirectionFor` reads WHO HOLDS the named captive, and
  either verb given the other side's captive now runs the trade that actually exists. A name neither side holds
  stays a refusal, since redirecting that would invent a trade. Both teachings also forbid a separate
  `give_gold`/`take_gold`, which is how his purse emptied with nothing to show for it.
- **For modders: `{{variable}}` providers were being called off the main thread** (tashmetu, 21/09/2026, who
  asked before it bit him: his `{{bc_player}}` reads `Hero.MainHero` and `Campaign.Current` live). The mod calls
  `ChatAsync` inside a `Task.Run` and the prompt is built at the top of it, so a provider ran on a thread-pool
  thread while the simulation ran. Both context builders (conversation and letter) now resolve every registered
  variable first, on the game's own thread, and pass the values in through
  `EncounterContext.ExternalPromptVariables`; with those set the builder calls no provider at all.
  `PromptVariableRegistry.Compose` is public so any host can do the same. No bridge needs to change.

## v2.5.18.. 2026-09-20

Four player reports, and three of them are the same class of bug: a fact the prompt stated ONCE, somewhere the
model could not see it when it mattered.

### Fixed
- **A character took the player's side of the matter for its own** (Fakade, on gemma-4-e4b through LM Studio,
  who pasted the model's own reasoning). Turn one: "Goal: I sought this meeting. The matter is 'Arthur took Lady
  Debana captive'". Turn two, after he confirms he holds her: "He challenges Diasca's right to hold her
  captive". The identity contract existed, near the TOP of the prompt: thousands of characters back for a 4B
  model on a short context, and the first stretch such a context drops. `AppendRoleAnchor` now restates it at
  the point of highest recency, right before the format reminder, naming both parties and whose deeds, captives
  and claims are whose (3 lines Full, 2 Lean). A captor who has not been told the prisoner's name still does not
  get it there. The held-captives line moves from Lean-only to both.
- **A lord took the PLAYER's defeat for his own** (reminensce, with Complex Characters injecting a taunt about
  it in the vanilla dialogue first). CR never reads vanilla dialogue, so that was not the source: the memory
  lines were. They said "you" meaning the player ("Faced you in battle and saw you put their host to flight",
  mixing both parties in one sentence) inside a prompt whose every other "you" is the character. New memories
  name the player (`BattleMemoryText.NameOrPlayer`); `AppendHistory` states the convention for memories already
  in saves; `EventSubjectNormalizer` swaps "the player" for their name as a model-written `[EVENT]` is stored,
  and the teaching asks for the name. `LostBattleToPlayerRecently` matches both the old and the new phrasing.
- **Characters arranged meetings the game cannot keep** (yozakura12: "when I go to the designated location at
  the appointed time, the NPC isn't there. Consequently, the NPC gets upset, claiming I didn't keep the
  promise"). There is no rendezvous in this mod; the character invented one and the player was punished for it.
  `AppendNoAppointments`, always on: never fix a place and a time, offer what exists instead, and never reproach
  the player for a missed meeting whoever seemed to arrange it. That second half outlives the guardrail, since
  the promises already in saves keep surfacing. A real rendezvous is scoped in `ROADMAP.md`.
- **A rescue the player CHOSE was not counted as a rescue** (Lordfadooboo: he hunted the deserter band holding
  Lady Mesui, the vanilla thank-you encounter fired, and she later had "no knowledge of ever being held
  prisoner, being rescued, or even knowing who i was"). The ledger listened for one of the engine's seven
  endings: `ReleasedAfterBattle`, which is the engine freeing a prisoner ITSELF when no winner wants them. When
  the winner is the player, vanilla asks instead, and letting them go is `ReleasedByChoice`: the common case,
  dropped whole. Both count now (`RescueEligibilityPolicy.CanBeARescue`); the defeated-side guard still keeps a
  pardon from the player's own dungeon out of it. Verified against the decompiled engine that the release fires
  before `MapEventEnded` in both paths, so the buffering order was right and is unchanged.

## v2.5.17.. 2026-09-19

One player report, two causes, and a hole in the guard that should have caught the second.

### Fixed
- **A character who sought the player out spoke, one turn later, as though the player had come to THEM**
  (Fakade, on a local model through LM Studio: "it says that it knows they approached me, and the dialogue
  begins normally. I respond, then the AI treats the conversation as if I approached them"). The only sentence
  stating who approached whom was the interception directive, which the host consumes on read, so from turn two
  the prompt said nothing and the transcript's most recent voice was the player's.
  `EncounterContext.NpcSoughtThePlayer` is the standing fact, restated every turn, Lean included;
  `InterceptionBehavior` remembers the seeker until the conversation ends (never persisted) and
  `ForgetConversationSeeker` clears it as the next one opens.
- **His prisoner example had a second cause.** The line naming the player as the captives' holder lives in the
  prisoner-bargain teaching, which Lean drops entirely, so on a compact prompt nothing said whose they were and
  the character claimed them as its own spoils of war. One short line now says it, Lean only, only when the
  player holds any.
- **The prompt cache died on turn two of every conversation an NPC began.** `InterceptionReason` and
  `LlmFormatFeedbackNote` are one-shot facts and sat ABOVE the `CURRENT ENCOUNTER:` marker: exactly the defect
  `IsConversationOpening` had in September. Both moved to the per-turn tail.
  **`PromptCacheTurnStabilityTests` listed seven booleans and no one-shot strings, so the fixture written to
  catch this class of bug could not see either of them.** It now covers them and `NpcSoughtThePlayer`.

`cr.testall` gains `approach_framing`.

## v2.5.16.. 2026-09-19

Grievances that remember being answered, duels that finally act on the grudge that drove them, and a player who
leaves a realm is no longer written to as a member of it.

### Fixed
- **A lord kept asking why the player did what he holds against them** (Zivkyn: "asking why i killed their
  Kinsmen, that i did months ago, even though ive explained it 10 times now"). Nothing linked an explanation to
  the grudge, so every conversation met it fresh, and the permissive "you may name it openly" read every turn let
  the model reopen it. A vendetta also drove the road interception and the fief audience every thirty days with
  a "confront them" opener, forever. New verb `hear_account` records `Grudge.AnsweredDay` + `PlayerAccount`
  (additive; old saves read "never answered"); `GrudgeNarrator` then tells the holder the account and not to ask
  again; `GrudgePolicy.StillDrivesConfrontation` keeps an answered grudge out of `HoldsLiveGrudgeAgainstPlayer`.
  **Gabriel's ruling: answering mends nothing.** A vendetta stays unforgivable, weight and rigidity untouched;
  only amends, gifts, deeds and time soften a grudge. Taught for vendettas too, unlike `make_amends`.
- **A duel fought over a grudge never touched the grudge.** `DuelOutcomePolicy` has computed Settled/Hardened
  since the pillar shipped; `DuelOutcomeApplier` logged it and applied neither, because nothing could deepen a
  grudge. `GrudgePolicy.Harden` now exists; `DuelOutcomePolicy.ApplyGrudgeEffect` resolves on a fought duel and
  adds `GrudgeHardenedByForfeit` (15) on a forfeit by either side. Vendettas refused both ways.

- **Letters and prompts kept treating an expelled player as a member of their old realm** (tashmetu, expelled by
  Bellum Civile after a lost war). The live standing reads were right; four places were not. ServiceOffer: once
  the player had no kingdom every friendly ruler qualified, the expelling one included, and the context always
  said "leave the lord they now serve"; the premise now follows the live realm and the ruler of a realm the
  player left stays silent 120 days (`ServiceOfferPolicy`). Departures other than LeaveKingdom/LeaveWithRebellion
  went unrecorded; every departure now carries the `player_left_realm` marker (`WorldEventFeeders`), an
  unrecognised one logged and recorded neutrally. `{{faction}}`/`{{user_clan}}` were captured once per
  PromptBuilder until reload; a realm or clan change now rebuilds it. BattleVictory candidates are retired at
  release if the sender no longer shares the player's realm. SDK: the history block now says memories keep the
  allegiances of their day and the present standing is true now.

`cr.testall` gains `grudge_answered` (drives `hear_account` through the real bridge) and `realm_departure`.

## v2.5.15.. 2026-09-19

Four fixes, all found by reading the first real bridge mod built on the modder API (tashmetu's Bellum Bridge)
against the code. Every one was invisible from outside the mod.

### Fixed
- **The cr_patch scanner read DISABLED modules.** `ModuleHelper.GetModules()` is, per the decompiled engine, a
  walk over `_loadedModules.Values`: every module found on disk, enabled or not, in dictionary order. A bridge
  turned off in the launcher kept feeding its prose, and with its assembly unloaded its `{{variables}}` were
  unregistered, and `PromptVariableExpander` leaves an unknown token literal, so the model read raw braces every
  turn. Now `TaleWorlds.Engine.Utilities.GetModulesNames()` (enabled, load order), fallback
  `ModuleHelper.GetActiveModules()`. The "launcher load order" the 2.5.13 notes promised is now true.
  `cr.testall` gains `narrative_patch_scope`. **DURABLE: verify an engine method by decompiling it, not by its
  name; `GetModules` sounds like "the loaded modules" and is not.**

### For modders
- **`{{variables}}` are expanded in the behaviour guidelines**, full and Lean. Style, player description, world
  and post-history were expanded; the guidelines were not. They sit in the CACHED prefix, so a variable there
  should change rarely.
- **A contribution to a sectioned file gets its own section.** behavior_guidelines ends with per-culture blocks,
  Nords last, and `FactionGuidelinesPolicy` ends a section at the next `=== ` heading. Text appended with no
  heading became part of the Nords block and was stripped for every other culture. The composer now places
  `=== FURTHER GUIDANCE ===` before a contribution's first line of text, unless it opens with its own heading.
  Two features, each tested and correct alone, and no test for the seam: there is one now, end to end.
- **`!#` comments** in cr_patch files. A comment-only file returns the shipped text byte for byte.

## v2.5.14.. 2026-09-19

One addition: a BACKGROUND role, so the mod's mechanical calls can go to a different model from the prose. And
three fixes folded in before upload: `take_into_service` could give away family by marriage or the wider tree,
`teach_skill` never checked whether the player already knew more than the teacher, and a prisoner handed over as a
SALE never settled the delivery bargain it answered.

### Fixed
- **A daughter-in-law could be offered to a lord as a retainer** (reminensce, a bridge-submod author who asked
  before patching around it). The pool walks `Clan.Heroes` and not `Clan.Companions` ON PURPOSE, because
  `turn_nemesis` and `recruit_prisoner` set `Hero.Clan` directly and never `CompanionOf`, so narrowing it would
  lose the retainers players care most about. The EXCLUSIONS were the defect: "own blood" was parents, children
  and siblings only. `HouseholdKinPolicy` now means family: a shared forebear within three generations, or
  married to one, from the engine's tree with dead forebears included. Pool and teaching gate share it.
  - **Renamed as well as widened**: `CompanionIsPlayerBlood` and the refusal "{name} is your own blood" became
    FAMILY, or a daughter-in-law's refusal would have told the player she was their blood. The same class of
    defect as the duel: a predicate whose name answers a different question than its body.
- **A master at 100 could teach a player at 300 and pay him for it** (found answering Zinced121212). The lesson
  never read the player's level. `TeachSkillPolicy.CanTeachPlayer`: competent at all AND strictly better than the
  player, used by the list the prompt offers and the bridge that pays. **Kept out of `GrantedXp` on purpose**:
  it already takes two ints in a row, and a third where swapping teacher and player silently inverts the rule is
  the signature this repo spent a campaign removing. A test calls the pair both ways and must go red on a swap.
  The prompt line now reads "and the player knows less of them than you do", since the list means that now.
- **A captive delivered by SALE left the bargain open forever** (Lordfadooboo: paid, quest stuck). Two verbs
  hand a captive to an NPC and only `give_prisoner` settled the bargain; `buy_prisoner` paid a price and touched
  nothing, and the bag's only prisoner option was worded as a sale. `DeliveryQuestMatcher` asks the one question
  for every path: a sale of a captive the buyer asked for is handed over as the DELIVERY (reward paid once), the
  bag offers a handover in that case, and `give_prisoner` now reads the whole custody (party and own dungeons,
  as the bag and the sale already did), tries every open bargain, and honours the captive the player named. The
  catalogue's give_prisoner anti-pattern named `sell_prisoner` for a sale to the NPC; it is `buy_prisoner`.
  `cr.testall` gains `deliver_prisoner_by_sale`.

### New
- **`ServiceRole.Housekeeping`** plus `LlmServices.HousekeepingClient`, a lazily-built, fail-soft-to-main client
  mirroring `InterpreterClient`. MCM gains a Background model, and a Background endpoint and API key for an
  OpenAI-Compatible server. **Blank means the prose model on the prose provider**, guarded by an assertion added
  to `cr.testall`'s `provider_routing`: an install that ignores the setting is unaffected.
  - Asked for by fkasad (Nexus, 19/09/2026), running a local model. There are no sessions anywhere in this mod
    (every call is a standalone POST with its own `messages`), so the first half of his worry was unfounded and
    said so. The second half is exact: **a local server reuses its KV cache by longest common prefix, and two
    prompt families alternating on one slot evict each other every time.** The client cannot prevent that; a
    different model can.
  - **DURABLE, and it was MY error for a day: `ModelOverride` is NOT set by most requests.** CLAUDE.md said "every
    request already carries its own, so this is a setting away". An inventory found it on exactly two in the
    whole codebase (`NpcChatService`, and explicitly null in `ActionInterpreterComposer`) and on NONE of the ten
    housekeeping calls. A second CLIENT is also the better route regardless: it carries provider, base URL and
    key as well as the model, which is what pointing background work at another server actually needs.
  - Eleven call sites repointed across six files: the conversation, captive-scene and letter summarizers, the
    memory and transcript compressors, the claim extractor, and both strategic-assessment paths.
  - **`ChronicleIlluminator` is deliberately NOT moved.** Background by shape, prose by purpose: the player reads
    the illuminated chronicle, and it is the only member of the family with no `ReasoningOverride`, at Creativity
    0.7 and 1200 tokens. Sending it to a cheap model to save on background work would degrade something visible.

## v2.5.13.. 2026-09-19

Two changes, one day after 2.5.12, both from the page discussion and both things the mod already knew and was
discarding before the decision that needed them.

### Fixed
- **A matter is chosen by what touches the speaker, then its size, then the clock** (fkasad: a notable whose
  village was being looted raised a war three hundred leagues south instead). `RecentMatterSelector.IsBetter`
  ranked personal-over-gossip and then by DAY alone; `WorldEvent.PlaceId`, `FactionIds` and `Magnitude` were all
  available and were dropped on the way into `RecentMatter`. It carries `TouchesTheSpeaker` and `Weight` now.
  **No thresholds: this is an ORDER, not a filter**, so it needs no tuning values and hides nothing that was not
  already going to lose. **Scoped to gossip on purpose, with a test saying so**: weighing a quiet conversation
  memory by "size" would bury the most personal material under the loudest.

### For modders
- **`ModuleData/cr_patch/<filename>` in any loaded module** contributes to that narrative file. Applied after
  ours and after the player's Custom folder, in launcher load order. `!remove <text>` drops matching lines,
  `!replace` discards everything before it, anything else appends. Asked for by tashmetu (Bellum Civile bridge),
  whose argument settled the format: a small bridge appends a few lines while a total conversion replaces the
  geography, and one mechanism must serve both without the small case causing the large one by accident.
  - **DURABLE: the directive marker is `!` and not `-`.** Prose written for a model is full of lines beginning
    with a dash, because that is how anyone writes a list, and a patch silently deleting a lord's behaviour over
    a bullet would be unbearable to debug from outside the mod. There is a test for the bulleted contribution.
  - **No patches means no change, byte for byte, line endings included.** The first draft returned a rejoined
    copy on that path and the test caught it on the line endings: composing normalises CRLF and collapses blank
    runs, which is right after a patch and wrong for every player who has no bridge mod.

### Workflow
- `.claude/agents/cr-trace.md`, a read-only tracer for this codebase, plus the CLAUDE.md rule for when to
  delegate a search and why building in parallel is almost always wrong here (git, the shared prompt budget,
  and the bar). The CLAUDE.md "Current state" section was three releases stale and is rewritten.

## v2.5.12.. 2026-09-19

Eleven fixes in two days, every one from a post on the Nexus page. Supersedes the v2.5.11 cut, which was built
but not uploaded, so this carries both spans. Saves carry over untouched.

### What NPCs stop you about
- **`RecentMatterRegister.WorldGossip` is off by default** (Kilju101 17/09, seconded Lordfadooboo 18/09, priced
  by fkasad 18/09: "one in ten NPCs has something interesting to say... I'm forced to decline because of the
  other nine"). **DURABLE: that register's own doc says it always yields to anything personal, so it fires
  exactly when a lord has nothing of his own to say.** Gated where a gossip matter is MINTED, not at the
  approach, so a lord left with no matter has nothing to say, `HeardRecentPlayerDeed` is false, and no
  invitation is raised. MCM `NpcsRaiseWorldNews`, default false.
- **The invitation names the subject** (`AudienceTopicLine`, tested). `HeardOfDeed`, by far the commonest
  trigger, was the only one carrying no `detail` at all, while the matter was already in hand. The test earned
  its keep at once: trimming the summary's full stop AFTER cutting it ate the ellipsis.
- **STILL OPEN:** `RumorAboutPlayer` has no magnitude filter (gossip does, threshold 55), so a cleared looter
  camp counts as much as a city falling. fkasad's point 3. Needs thresholds set against real play.

### Fixed
- **The duel crash, third report, third version** (yozakura12). **DURABLE: `Settlement.CurrentSettlement` is set
  for a party MERELY STANDING ON a settlement; only `PlayerEncounter.InsideSettlement` means inside**, and
  `CreateAndOpenMissionController` on an interior from outside crashes loading the scene. Three readings of one
  question disagreed; now one, `DuelEligibilityResolver.PlayerCanFightADuelHere`, pure half in
  `DuelStagingSupport.CanHoldATownDuel`. Outside the walls is the WILDERNESS venue, not a refusal.
- **`DuelReason.Friendly`** (Zinced121212). Every chat-armed duel was `HonourAffront`, hard-coded, whose premise
  is that somebody was insulted. New `spirit:` parameter on `challenge_duel`, defaulting to grievance so nothing
  changes unless the fiction says so. A control test asserts an affront duel still wounds pride.
- **The companion audience that opened nothing, inside a town** (Lordfadooboo, Zinced121212). The staging
  watchdog is reasoned about `OpenMapConversation`, which registers at once; it was applied unchanged to
  `CreateAndOpenMissionController`, which loads a scene first. `StagingGracePolicy`, 10 frames vs 1800.
  `CouncilConversationStager` had the identical defect.
- **`ModLog.Notice`**, always on, for a dead end the player experienced. `Info` and `Error` are both gated by the
  MCM switch, off by default on Nexus.
- **A letter REPLY left no trace on the NPC's profile** (Zivkyn). Three letter directions exist and only two were
  written down; `LetterMemoryFinalizer` had carried the `npcIsSender:false` direction unused since it was written.
- **A council seat had no profile** (fkasad, via the innkeeper's courier list). `OpenForAssembly` and the
  companion briefing both guarantee one; `OpenForCouncil` never did. Both council write paths `continue` on a
  missing profile, so a councillor kept NO memory, NO sealed agreement and NO regard shift from a sitting.
- **A quest agreed mid-conversation read as an errand under way** (raphareish). `QuestsKnownThrough`, twin of
  `HistoryKnownThrough`. `IssuedOnDay` cannot settle it: a deal struck a minute ago and one struck at dawn share
  the day stamp.
- **`PlayerStatusVsNpc.Free` rendered nothing** (zhenja1zhenja1), so the commonest standing reached the model as
  silence. `PlayerStandingNote`, rendered ABOVE the `CURRENT ENCOUNTER:` marker. The letter path sent
  `EncounterContext.Empty`, so `StandingContextFor` was needed or the fix would have missed the only reported
  case. **The marker is now emitted UNCONDITIONALLY**: tying the cache breakpoint to whether one paragraph had
  content meant a letter had no prefix at all.
- **Renamed kingdoms** (Bellum Civile bridge modder). No lookup to invert: the vanilla names are PROSE in
  `world.txt` and `behavior_guidelines.txt`, and the live name already won wherever it was a value. A precedence
  rule in the cacheable prefix now says the background document may be out of date. The matched guidelines
  heading is also re-titled from the live realm. **LIMIT, pinned by a test: the Empire's headings split by REALM
  rather than culture, so a renamed imperial realm keeps both headings; re-titling both would merge two peoples.**

### Testing
- **`cr.testall` gains `conversation_cut`**: the two cuts are produced by the HOST and honoured by the SDK, and
  the unit tests on either side cannot see the wire between them. No LLM, deliberately.
- **`cr.audience ask <name>`** raises the real "X asks for a word" inquiry so the accept path can be walked on
  demand, inside a town.

## v2.5.11.. 2026-09-18

Six fixes, five reported by name in the two days since 2.5.10. Saves from 2.5.10 carry over untouched.

### Fixed
- **The duel crash, for real this time.** yozakura12, twice: 17/09 on 2.5.9, and 18/09 on the version meant to
  have fixed it. His second log carried the deciding line, `returned to campaign map`, immediately before
  `venue=TownGates`. He was AT a town, parked on it, not inside it. **DURABLE: `Settlement.CurrentSettlement`
  is set for a party merely standing on a settlement; only `PlayerEncounter.InsideSettlement` says the player
  is in it,** and `CreateAndOpenMissionController` on an interior location from outside crashes loading the
  scene. Three readings of one question disagreed (`here != null` in `DuelEligibilityResolver`, "owns a centre"
  in `DuelCoordinator`, `is {IsTown: true}` in `TownDuelBehavior.Arm`), so on the map beside a town the
  resolver refused the duel outright while the coordinator staged one. Now one predicate,
  `PlayerCanFightADuelHere`, pure half in `DuelStagingSupport.CanHoldATownDuel` with tests. Outside the walls
  is no longer a refusal: it is the wilderness venue. The 2.5.10 fix (`277026a`) closed a different hole, a
  MISSING town centre, which was real and was not his.
- **A companion audience that opened nothing, inside a town.** Lordfadooboo 11/09 and Zinced121212 17/09, in
  almost the same words. The staging watchdog is reasoned about `OpenMapConversation`, whose success registers
  at once, and its own doc comment says it is not a timeout on a scene load. It was applied unchanged to the
  settlement twin, which calls `CreateAndOpenMissionController` and has an interior scene to load first, so a
  healthy staging was declared dead in ten frames. Rule moved to `StagingGracePolicy` (10 frames vs 1800).
  `CouncilConversationStager` carried the identical defect and is fixed with it, which may be why nobody has
  ever managed to read a council sitting: a council is convened from a settlement menu.
- **`ModLog.Notice`, an always-on channel for a dead end the player actually experienced.** `Info` and `Error`
  are both gated by the MCM switch, off by default on Nexus, so every branch that could swallow an answered
  popup wrote its account where the reporter could never read it. A recovering fallback stays on `Info`.
- **A reply to an NPC's letter left no trace on their profile.** Zivkyn: four letters answered, she is still
  angry he never wrote back. **DURABLE: three letter directions exist and only two were written down.** NPC
  letter to the player goes through `LetterPopupManager` to `LetterMemoryFinalizer`; an unprompted player
  letter goes through `PlayerLetterService.RecordReceipt`; `SealAndSendReply` went through neither.
  `LetterMemoryFinalizer` had carried the `npcIsSender:false` direction unused since it was written.
- **A quest agreed mid-conversation was rendered as an outstanding errand** with "you may ask how it fares"
  (raphareish). `EncounterContext.QuestsKnownThrough` is the twin of `HistoryKnownThrough`, captured per
  speaker in `ChatViewModel`. **`IssuedOnDay` cannot settle this: a deal struck a minute ago and one struck at
  dawn share the day stamp.** MARKED, not hidden, unlike the history cut: the character must still know what
  they agreed to.
- **`PlayerStatusVsNpc.Free` rendered nothing**, so the commonest standing in the game reached the model as
  silence and a lord wrote to a tournament loser as though he rode in that lord's retinue (zhenja1zhenja1).
  The sentence now lives in `PlayerStandingNote` and is rendered ABOVE the `CURRENT ENCOUNTER:` marker: about
  500 chars added to the cached prefix for `Free`, 70 to 130 chars MOVED out of the per-turn tail for the
  others. The letter path sent `EncounterContext.Empty`, so `EncounterContextBuilder.StandingContextFor` was
  needed or the fix would have missed the only case reported.
- **The `CURRENT ENCOUNTER:` marker is now emitted unconditionally.** Tying the cache breakpoint to whether one
  paragraph had content meant a letter had no prefix at all, which the letter path's own comment had recorded.

### Testing
- **`cr.testall` gains `conversation_cut`.** `HistoryKnownThrough` and `QuestsKnownThrough` are counts the HOST
  captures and the SDK honours; the unit tests on either side prove each half and neither can see the wire
  between them. It takes both cuts through the real `ChatViewModel` and reads the exact prompt the chat
  pipeline builds. No LLM: the question is what the prompt CONTAINS, which is decidable and free.
- **`cr.audience ask <name>`** raises the real "X asks for a word" inquiry instead of opening the chat, so the
  accept path can be walked on demand. Try it inside a town.

## v2.5.10.. 2026-09-17

Two reports drove this release: the mod costs too much per conversation, and characters repeat themselves. Both
were right, and both had a single cause that had been in place for a long time. Saves from 2.5.9 carry over
untouched.

*(v2.5.7 through v2.5.9 are recorded in `NEXUS_CHANGELOG_v2.5.7.txt`, `…v2.5.8.txt` and `…v2.5.9.txt`.)*

### Cost
- **The prompt cache died on turn two of every conversation.** Reported by fkasad with two consecutive prompts and
  a finger on the line where they diverged. `AppendNarrativeVoice` took an `isOpening` flag and rendered it ABOVE
  the `CURRENT ENCOUNTER:` marker, which is the cache breakpoint, so the stable prefix was byte-identical on turn
  one and different on every turn after. A changed prefix is a full-price prefix. Fixed by moving the opening
  clause below the marker; `PromptCacheSplit.StablePrefixOf` now holds the rule in one place and
  `PromptCacheTurnStabilityTests` pins it. Play-verified: 0 cached tokens before, 12,864 after.
- **The action interpreter carried the largest artefact in the mod and had no breakpoint at all.** 70,378
  characters of invariant head, sent fresh every turn in Enhanced. `ActionInterpreterPromptBuilder` now exposes a
  stable prefix and the client marks it. Play-verified at 96.6% cached.
- **Compact now fits 8,192 tokens.** Reported by DuskSymphony. The interpreter head drops to 25,353 characters in
  Compact (the `emit when:` / `not when:` clauses and the worked examples come out), the transcript folds at 6
  unfolded lines instead of 14, and `LeanReplyBudget.Tokens = 1200` budgets the completion, which nothing had
  ever done.
- **The truncation retry was dropping `ReasoningOverride`.** Every housekeeping call sets it off; those are the
  calls most likely to truncate, so they were retried at double budget with the global reasoning dial back on.
  `RebuildForBiggerBudget` now carries it, and `AllowTruncationRetry` with it.
- **`cr.wire` reports the cache from the inside.** Keyed on `(model, subject)`, because prose and interpreter
  alternate on one conversation and a group scene has several subjects.

### Fixed
- **A character recited the conversation back at the player.** Reported 17/09: replies grew each turn until they
  restated the whole exchange. `ProfileMutator` runs every turn, so an `[EVENT]` from turn two is a notable
  memory on turn three, rendered under "reference them when relevant", while still verbatim in the raw messages.
  `AppendHistory` now stops at `EncounterContext.HistoryKnownThrough`, a count taken when the conversation opens.
  Nullable on purpose: zero is a real and common answer.
- **Duels crashed on the action button.** Staging asked `Settlement.CurrentSettlement != null`; teaching asked
  `SettlementCanHostADuel`. Two predicates for one question, so a null location could reach
  `CreateAndOpenMissionController`. Both now go through `DuelVenuePolicy.Choose`.
- **The council button vanished when Retinues was installed.** Retinues declares
  `MapBar.Left.Button.Backgrounds` under its native name, which replaces the brush whole rather than merging into
  it, taking the `cr_council` layer with it. `CouncilBarIconSprite` rebuilds the layer at runtime if it is gone
  and says so on the always-on channel. Play-verified with Retinues 2.0.0.12: both buttons present.
  `CRMapBar.xml` stays as it is: it uses `OverrideBrush`, which is the polite form.
- **Every character was handed all eight cultures' behaviour guidelines.** `FactionGuidelinesPolicy` gives each
  character their own.
- **Namesakes and the wars list**, both from fkasad: a character had no way to distinguish themselves from another
  hero of the same name (`SpeakerNamesakePolicy`), and active conflicts were listed once per participant
  (`ActiveConflictsFormatter`).

### Memory and knowledge audit
- **Anchors carry an identity.** Two conversations on one day produced byte-identical anchors, so the summary
  queue deduplicated one away and a `FindLastIndex` wrote the first summary onto the second event.
  `NotableEvent.Id` + `NpcProfile.IndexOfAnchor`; an identified anchor that is gone returns −1 and never falls
  back to a value match, which would reintroduce the defect it fixes.
- **Compression stopped undoing enrichment**, and `ReplaceEvents` swaps the reference instead of `Clear()` then
  `AddRange`, so a reader mid-prompt never sees a half-emptied past.
- **`SharedRecall`** replaces `WitnessMemoryDigest` and is the only way to obtain a shareable recall: the
  `IsPrivate` filter and the oldest+recent bound are one function, and a field typed `SharedRecall` cannot be
  handed raw event text. The leak stops compiling instead of stopping in review.
- **A cut-off bearer gets no realm news and no underworld.** `RealmNewsPack` and `UnderworldPack` return
  `KnowledgeDepth.None` when `bearer.IsCutOff`.
- **The conversation-open refresh covers name and spouse**, not name and station only.
- **The council fills its seats through the shared builders** (`EncounterContextBuilder.StationContextFor` and
  `KnowledgeContextFor`) rather than assembling its own version of a person.
- Minors: `OwnBodyPack.IsSupplied` accepts `IsMaimed`; `MarketWordPack` renders an unnamed place as "Here, …";
  `HouseMeansPack.Muster` answers on an unknown band; wit reaches the letter voice.

### Known
- `take_into_service` has never completed in a live game.
- Nobody has read a council sitting, three increments of content later.

## v2.5.6.. 2026-09-08

A bug-fix release, and every fix in it came from a player's own log. Two of them unblock a campaign rather than
polish one. Saves from 2.5.5 carry over untouched.

### Fixed
- **A chat that failed to open no longer kills every conversation until you restart the game.** Reported on 2.5.5:
  "first time everything works, then if I select A word with you the panel closes on its own straight away... the
  conversation never starts". The mod decided "a chat is already open" from a single field holding the live layer,
  so an open whose close never came left it recorded against a screen that had gone, and from then on every entry
  silently did nothing while the vanilla dialogue, with nowhere to route, simply ended. "Already open" now means a
  window still attached to its screen; a leftover is cleared and the chat opens.
- **Succeeding at a rescue no longer cancels the quest to rescue.** Reported on 2.5.3: the player broke someone out
  of a prison, then days later read that they were "lost, no longer on the map" and the task had failed. A rescue
  was only ever credited from a won field battle, so a dungeon breakout raised nothing the mod could see, and the
  daily sweep then read the freed captive as proof the task was impossible. It is now credited however the captive
  was freed, and a task set aside because they are already free says so instead of claiming they vanished.
- **Kings and Emperors know they rule.** Reported: "I tried to address Derthert and Garios as kings but they deny
  the fact and claim that they are just a lord serving the throne." They were never told. The only rulership fact
  in the prompt was gated behind an oath being available, so a monarch learned his own rank in exactly one kind of
  conversation. It now sits in his identity, with the realm's own word for its ruler, so an Emperor is an Emperor.
- **A character no longer restarts the conversation at every reply.** The vanilla exchange that precedes the chat
  was fed to the AI as turns awaiting an answer, and a wanderer's backstory ritual opens with the player
  introducing themselves, so the AI kept answering the introduction. It now arrives as history, which is all it
  ever was: a bridge between the two conversations.
- **You pay the price that was named.** A companion who said "five hundred and ninety-five denars" counted it
  aloud as "five... ninety-five", and 95 reached the game, where a rule pulled it into a band and charged 446. The
  agreed price is now charged exactly, and a purse that cannot cover it does not buy a cheaper version of the deal.
- **Nineteen mod options no longer explain themselves off the edge of the screen.** These read "On (default): ...
  Off: ...", so the part that fell off the top was usually the half describing the other position.

### New
- **Take your own people aside.** Ask a lord for a moment, in the vanilla dialogue, to speak privately with the
  companions you choose. Tell them what you want from the meeting to come, and they answer as themselves: they may
  question the plan or say they are ill-suited to it. What you told them travels with them into the conversation
  that follows, and the lord never learns of it. Briefs stack, so you can go back for what you forgot.
- **Companions ask for a word on the road.** They only ever did so on entering a settlement, so a player who
  travels never saw it. New mod option, Companions Asking For A Word, from Never to Often.

### Changed
- **A companion confides sooner if you asked for a warmer game.** How deep a bond it takes before they confess
  feelings now follows your Relationship Pacing: unchanged at Grounded, nearer at Warm and Storyteller.

## v2.5.5.. 2026-09-07

Group conversations keep their thread, and how a character loves is no longer decided by whether they are honourable.
All from player reports this week. Saves from 2.5.4 carry over untouched.

### Fixed
- **A group conversation stopped remembering the moment you turned to someone else.** The enrichment that gives
  each person in the room their own recall lived in the main speaker's branch alone, so addressing a witness
  DIRECTLY described a room full of people with no past, and re-describing an entry for another observer dropped
  the memory while carefully carrying the gear beside it. A group conversation is precisely the act of turning from
  one person to another, so the branch that forgot was the one a group scene leans on entirely. It is now the
  single exit path, so no future branch can forget it again.
- **On a small local model, nobody in the room remembered anything.** The Lean prompt dropped every witness's
  recall line outright, which silently undid the same fix for exactly the players the mod's own setting tells to
  enable Lean. Two people now get a short recall, trimmed to 90 characters, companions first: bounded, so the cost
  does not grow with the size of the party.

### Changed
- **Polyamory is decided by CULTURE, not by a dishonourable trait.** It was reachable only through negative Honor,
  which modelled a way of loving as a moral failing, and it ignored `jealousy_cultures.json` entirely, the very
  file the mod already consults to decide which cultures tolerate shared partners and which keep harems. So a
  noble of a harem culture came out strictly monogamous unless he happened to be dishonourable. The preference now
  asks that file's own rule. Honour keeps what actually belongs to it: INFIDELITY, the betrayal of an exclusivity
  that was promised, which it already governs where that happens.
- **Nor is a taste for brief attachments.** `Casual` moves onto the axis this model already uses for attachment
  (Generosity: Devoted when high, Independent when low), so an honourable character who simply does not give of
  themselves can exist, which the old rule made impossible. A dead rule went with it, one that had never added
  anything to the pool.

### New
- **`character_overrides.json` can set a character's romantic preferences.** `"preferences": ["Polyamorous"]`, and
  the whole vocabulary is documented in the file's own header. It is the way to write one character against the
  grain of their culture, and it behaves like the orientation and kink locks beside it: omit the field and the
  roll stands, an empty list is a deliberate lock to none, and it survives `cr.reload_lore`. A mistyped name is
  skipped rather than thrown, then NAMED by `cr.lore_status` and the reload log, so it never fails in silence.

## v2.5.4.. 2026-09-07

One fix, at its cause. Two players crashed a few seconds after opening a letter, one of them on the Balance of Power
panel too, and not on every letter. Both panels are now hosted the way the game hosts its own screens. Saves from
2.5.3 carry over untouched.

### Fixed
- **The map no longer keeps running underneath a panel.** CR opened its full-map panels by pushing a SCREEN, while
  the game opens Clan, Kingdom and Inventory by pushing a game STATE. A screen push is purely visual: the state
  machine is never told, so the map state stays active and the map keeps frame-ticking under the panel. Measured in
  game over four seconds: 481 settlement-visual ticks under a pushed screen, exactly the same as with nothing open at
  all, and ZERO under a pushed state. Among the things that kept running was the vanilla siege-visual tick, whose
  bombardment branch moves projectiles in flight through native calls, which is what crashed. It also explains why
  the crash was intermittent (it needed a siege actually bombarding somewhere) and why it never reproduced here.
  The letter book and the Balance of Power are now hosted as a game state, so the map is genuinely suspended.
  **Honest caveat:** neither report could be reproduced here, so this repairs the mechanism rather than a
  reproduction. If a panel still crashes, the log now records, on every open, whether the map went quiet underneath.
- **Two internal guards can no longer fail in silence.** The Harmony guards that protect panels report a failed
  attach on the channel that is always recorded, instead of the one the Nexus default switches off. The one fact
  that would explain a crash was unobtainable from a default install.

### Changed
- Closing the Balance of Power returns to the clan screen it was opened from, rather than to the map. It now opens
  over the clan screen the way any game screen opens over another, instead of closing it first.

### Diagnostics
- The mod records, once per conversation, whether the chat entry option was offered and which dialogue hubs the
  conversation passed through, for the reports of NPCs who cannot be spoken to. Only when logging is enabled.

## v2.5.3.. 2026-09-06

Battles and quarrels leave real marks. What you did in the fighting is remembered by the people who were there, a
great feat travels the realm on its own, and for those who want it, the sandbox opens a darker door: you can turn on
the person in front of you. Saves from 2.5.2 carry over untouched.

### New
- **Battle memory.** A fight no longer leaves one generic line. The characters who were there remember what the
  player actually did, distilled to a few first-hand facts: the standout deed, whoever put the player down, and a
  rank-and-file toll only when it was heavy enough to be worth saying. Each outcome is read from the CAMPAIGN, never
  from the mission's verdict, because Bannerlord routinely spares a hero the mission reported as killed, so nobody
  mourns a lord the player can visit in his own dungeon. Deeds are rendered per recipient: the bested foe speaks of
  his own defeat, the man who downed the player owns the blow, everyone else witnessed it. A named head-to-head also
  feeds the same standing-rivalry ledger the arena uses, so a lord met sword to sword often enough becomes a worthy
  rival or a bitter one by his own character.
- **A great feat travels.** Besting someone well above your weight, or a name the realm knows, becomes a world event,
  so a lord who was never there can raise it as something he has heard. Ordinary wins stay with those who saw them.
  A rated victory in the lists records the same, which is what finally lets a third lord praise an arena win instead
  of only the men who fought it.
- **Turning on a character (player request).** You can rob, beat, or kill the character you are speaking to.
  **Killing is gated by a mod option, off by default**; robbery and a beating are always available. Nothing is a
  formality: how it ends depends on how the two of you compare as fighters and on whether they are alone, so a
  guarded victim is far harder to reach, and you can fail. A killing always asks you to confirm first, because the
  AI is what decides a killing was narrated and no bench tuning makes that judgement safe enough to spend a
  character on. Declining records nothing at all.
- **And the world answers.** The law is the game's OWN crime rating against the victim's realm, which is what brings
  the guards in the engine's own time. The law hears something even when nobody watched, so getting someone alone
  stays an advantage and never becomes a free pass, and a failed attempt still costs, so attempted murder is not a
  free roll to retry. Kin carry a grudge, the victim's regard collapses, standing is lost only when the deed was
  seen, and infamy circulates: a killing always becomes news, a robbery or beating only when someone saw it.

### Fixed
- **Asking a lord to ride with you worked again.** It had been refused for every lord since v2.4.4: bandit clans
  counted as realms at war, and Calradia is always at war with bandits, so the guard meant to keep lords in their
  faction's army pool during a war rejected everyone, always.
- **Renown penalties actually take renown.** Every penalty in the mod had been doing nothing at all: the engine's
  own GainRenownAction silently discards a negative amount, and so does Clan.AddRenown, so five systems (the bastard
  scandal, a lost duel, the captive humiliation, a leaked letter, and now violence) had never moved a single point
  despite reading as working code.
- **Hotkeys no longer reach the map from under a panel.** Typing a letter reply and pressing the face-generator key
  opened the character editor underneath. The letter book and the Balance of Power sit over the live map, which
  kept reading its own hotkeys through them.
- **The AI no longer reverses who is attacking whom.** A player who struck at a character could have the exchange
  reach for the verb meaning the NPC kills the PLAYER. The direction is pinned, and the catalog now names the verbs
  that actually exist for a player's violence.
- **A per-character voice no longer fades.** A voice written into a character's backstory ("in the voice of Homer")
  held for an exchange or two and then drifted, because it sat in the cached opening of the prompt and the closing
  instructions drowned it. It is now restated at the end, the same anchor the narrative style already had.
- **Panel zoom is only rewritten when it actually changes**, a follow-up to the aspect-ratio fit fix.

## v2.5.2.. 2026-09-06

Tournaments become memory: the lords who shared the lists remember how it went between you, and repeated meetings
build into rivalries. Saves from 2.5.1 carry over untouched.

### New
- **Tournament memory (player suggestion).** The arena bracket is read from the mission itself (rounds, matches,
  participants, winners), so each lord who faced the player carries a first-hand memory of the real result: who
  beat whom, in which round, and who took the crown. The recent-matter interception then has them approach about
  it, and the tone follows their character (Valor/Honor/Mercy): a proud low-honour victor brags, an honourable one
  is gracious, a hard bout is met with respect. A champion who beat the player also becomes realm gossip for the
  lords who were not there.
- **Grounded tournament letters.** A lord with something to say may write instead of waiting to meet: a braggart
  crows, a gracious one congratulates or is magnanimous. Capped at a couple per tournament, and never from a lord
  travelling with the player. New neutral `LetterReason.TournamentResult` (TournamentVictory presumes a player win,
  so it cannot frame a brag after a loss).
- **Tournament rivalries.** A persisted per-lord tally of arena head-to-heads; at the threshold a standing rivalry
  crystallizes once, planting a worthy-opponent appreciation (honourable) or a light grudge (low honour), so both
  a friendly rival and a sore one fall out of the same matches by character.

### Fixed
- **An authored character is never skipped in silence.** Follow-up to the 2.5.0 fix for the "no kinks entry"
  report: an entry whose ONLY field was a misspelled orientation parsed to nothing, so it counted as empty and was
  passed over. Orientation TEXT now counts as authored content, and unreadable values are reported rather than
  ignored (`CharacterOverride.AuthoringProblems`, `KinkParser.Unrecognized`): cr.lore_status shows them per entry
  and cr.reload_lore logs them plus any entry it had nothing to apply from. Parsing stays forgiving; a typo never
  throws, it is simply no longer invisible.

## v2.5.1.. 2026-09-06

Living lords who seek you out now raise the right matter, a secret-affair drama threads through the world, and
regard is earned rather than farmed. Saves from 2.5.0 carry over untouched.

### New
- **Lords raise the freshest matter they truly know.** A lord who rides out to find you now leads with the most
  recent thing he knows, framed by how he knows it: a battle he fought at your side (first person), a rumour of
  your deed (hedged hearsay), or realm gossip he cannot vouch for and asks YOU about. Each matter is raised only
  once, and a chase that ends in a letter names that same matter instead of a generic "your recent deeds".
- **A secret-affair drama.** Take a secret lover (a devoted companion, or a married lord drawn into an affair) and
  whispers of the bond's existence, never the identity, begin to circulate after a delay; other lords probe you
  about it. The identity is never deduced, only learned if you confess it. A knowing friend keeps the confidence,
  a rival lets it slip among his own friends over time, and an enemy holds it over you and presses you to pay for
  his silence.
- **Progressive regard.** Warmth is earned, not farmed: each point raises the bar for the next, so a long warm
  conversation advances it where a drive-by stalls, and skipping a few hours no longer farms +1s. The difficulty
  eases with real time, on a pace set by a new MCM setting "Regard Warmth Cooldown (days)" (default 7, 0 = off).
  Intimacy and negatives are unchanged.

### Fixed
- **No interception from a lord who is not free.** A lord committed to an army, or already caught in a battle, no
  longer breaks off to ride after you (and no longer sends the useless "entering your lands" letters).
- **Companion private-word no longer collides with vanilla.** A companion's private-word offer is not raised over
  top of a vanilla conversation on settlement entry.
- **Narrative style actually reshapes the prose.** Switching your chosen voice now changes length, rhythm, and
  interiority instead of barely changing anything (a prose-vs-structure split lets the voice win on how it reads).
- **BoP and letter book fit the screen.** The Balance of Power panel and the letter book scale to the screen's
  aspect ratio instead of being cropped or mis-zoomed when the ratio changes.

### For mod authors
- **ActionResult.Recap / CrConversation.PromptReply.** An external action can carry a short recap the NPC voices in
  the same beat, and a mod can prompt an NPC to answer a line.

## v2.5.0.. 2026-09-05

The World Almanac arrives: a Balance of Power war-room panel, opened from the Clan screen, plus a companion
strategist who counsels you from it. Saves from 2.4.x carry over untouched. (Per-version notes for 2.4.0 through
2.4.10 live in their NEXUS_CHANGELOG files.)

### New
- **Balance of Power (World Almanac).** A new panel, opened from a "Balance of Power" button on the Clan screen.
  It ranks every kingdom by power (armies, lands, wealth, political weight, war record, diplomacy) with a top-three
  podium of rulers and banners, lists the age's key figures (largest army, richest treasury, most influential
  ruler, and more), and draws a map of Calradia coloured by faction.
- **Strategic Assessment.** On that panel, one of your own people reads the situation and counsels you. A companion
  riding with you gives the counsel, and the higher their Tactics the sharper it is; with no companion, a warband
  veteran gives a plainer read; alone, a factual summary with no advisor. Grounded in the real standings and the
  wars actually under way, it gives genuine advice (press an advantage, seek an ally, or leave a failing banner),
  not just a recital. A companion counsels in their own character and in your chosen narrative style.

### Fixed
- **Panel crashes.** Eliminated a native crash when a siege was being fought while a full-screen panel was open, a
  crash on opening a panel, and a crash when entering a hostile settlement while a companion's private-word popup
  was open.
- **A lord no longer forgets a failed approach.** A lord who tries to reach you and fails keeps his errand in mind
  for a couple of days, still sends the missed-meeting letter, and this now survives saving and loading.
- **Novelistic style respects the scene.** The Novelistic narrative style no longer speaks for other characters
  inside your own reply; the room reads as inhabited through their reactions instead.
- **Backstory-only authored characters apply.** A character in character_overrides.json defined with only a
  backstory, conviction, or orientation (no traits or kinks) is now picked up by cr.reload_lore.

## v2.3.1.. 2026-08-27

Scenes with more than one character present feel more alive, and the mod's logging is tidier. Your saves from
2.3.0 carry over untouched.

### New
- **Draw a bystander into the conversation.** Speak to someone standing by, by name, and they are no longer just
  a witness watching from the edge: they step in as a full participant and stay one, replying in their own voice
  alongside the character you came to see, with their own interests and standing, rather than lapsing back to a
  silent onlooker on the next line.

### Fixed
- **A character spoken to no longer answers with silence.** In a scene with other characters present, a
  bystander addressed directly (asked something, or challenged) could reply with only a wordless gesture, which
  read as the cold shoulder and soured the conversation. Anyone spoken to now answers in their own words; a
  silent gesture is kept for a genuine background moment.

### Improved
- **Two separate logging switches, and a log that stays small.** The single logging option is now split into
  **file logging** (writes CalradiaRemembers.log for a bug report) and **on-screen logging** (the top-left
  message stream), so you can keep the diagnostics in the file without them crowding your screen during play.
  On-screen logging is off by default. The log file is also trimmed to its last two sessions on each launch, so
  it never grows without bound.

## v2.3.0.. 2026-08-27

A duel now leads somewhere: the fight is followed by a conversation, coloured by who won. Two opt-in paths
join the mod (winning a married character away from their spouse, and a reasoning dial for local models), and a
batch of fixes lands for duels, ransoms, farewells, and joining a campaign already under way. Your saves from
2.2.4 carry over untouched.

### New
- **The conversation continues after a duel.** When you cross blades with someone, the meeting no longer ends
  at the fight: the conversation reopens right where it left off before the swords came out. If you won, the
  loser concedes, and a character beaten honourably is inclined to grant a reasonable favour you ask of them
  (to keep away from someone, to let a matter drop) and to hold to their word. If you lost, they speak as the
  victor and make plain what they now expect of you. Either way the loser's house loses some standing for the
  defeat, the more so before a crowd. A new "Post-duel conversation" setting (on by default) turns it off if
  you would rather a fought duel end in silence.
- **Stolen Love (off by default).** A new setting opens a path to win away a MARRIED character. Seducing a
  married character becomes a personality-true, gated pursuit (an honourable or contentedly married one holds
  firm however high your standing); win them over and, in time, their marriage frays, they leave their spouse,
  and you may marry them or simply leave them divorced. The jilted spouse remembers who did it. Explicit
  content still follows your Adult Content Level.
- **Reasoning Effort for local and OpenAI-compatible models.** A new setting lets you dial how hard such a
  model thinks, and the mod no longer asks a local endpoint for a second interpreter pass it cannot serve.
- **Reload custom prompts without restarting.** A new `cr.reload_prompts` console command re-reads your custom
  prompt and style files on the spot, and the settings template is now unambiguous so uncommenting a line can
  no longer accidentally double the file.

### Fixed
- **A character no longer ends the chat over a pleasantry.** A closing-flavoured line ("safe travels, then")
  could end the whole conversation as though they had walked off; now only a real departure closes it.
- **A routine ransom no longer earns a grudge.** Selling a prisoner back to their own people for coin is
  ordinary and forgotten; only selling them to their sworn enemy now plants a lasting grievance.
- **Duels arranged in a town now actually happen.** A duel agreed inside a town used to wait until you left
  town by hand; it now stages when you close the conversation, and the place is decided from where you stand
  at the moment it begins (so a duel arranged in town but answered on the road is fought on the road).
- **A matter of honour is answered by default.** A character called out over a genuine affront now takes up
  the challenge instead of often declining. A refusal takes a real reason now: fear, plain cowardice, or being
  called out by someone far beneath their station (a nobody who challenges a king is dismissed, not fought).
- **An established spouse or close kin is recognized on an in-progress save.** Adding the mod to a campaign
  already under way no longer treats your husband, wife, or close relatives as strangers.

### Improved
- **A companion's private word is heard in the tavern, at their side.** When a companion asks you for a word,
  the audience now opens in the tavern with them standing beside you, rather than in the lord's hall.
- **A deed sworn at a council becomes the right kind of quest.** A pledge taken in council is now issued as
  its true quest type instead of a generic errand.

## v2.2.4.. 2026-08-25

A persistent crash on leaving a character-requested private word is fixed, and the characters who seek you out
now stand before you in a proper scene. Plus new letters, and new settings for marriage and text size. Your
saves from 2.2.3 carry over untouched.

### Fixed
- **The crash on leaving a private audience is gone.** Since a recent update, leaving a conversation that a
  character had started by asking you for a private word could hard-crash the game with nothing in the log. The
  window now closes cleanly every time.
- **The Memories panel closes properly during an audience.** When a character sought you out (a companion with
  something to say, or someone standing in a fief who wanted a word), the conversation opened as a flat overlay
  over the frozen map, so opening the Memories panel and closing it again could leave it stuck on screen. These
  audiences now open over a real scene with the character present, exactly like an ordinary talk, and the side
  panels open and close cleanly.

### New
- **Set the conversation text size.** Two new mod settings let you scale the chat text and the council text
  independently, so you can make either one bigger or smaller to taste.
- **Choose how hard marriage is to reach.** A new "Marriage Regard Needed" setting lets you lower the regard a
  prospective spouse must feel before they will accept, for a gentler courtship. Their family's blessing can
  still open a match below that bar, as before.
- **Letters that carry a real offer.** A lord may now write to buy a prisoner you are holding, paying a real
  ransom into your purse if you accept; or write to tempt you into their own service, sweetening the offer with
  gold, a castle, or troops. Both arrive as ordinary letters you can accept or decline.
- **A council of those riding with you.** Alongside the household, clan, and war councils, you can now convene
  the companions and lords currently travelling in your own party.

### Improved
- **A reluctant bride points to a real path.** When a match is not yet possible, the character no longer puts
  you off with a vague excuse: she names what would actually change her mind (winning her regard by deeds,
  earning her family's blessing, or courting her at your side through a season).
- **No children seated at a council.** The rule that keeps anyone under thirteen out of a council now applies to
  every kind of council, not only the family one.
- **A leaked letter leaves a mark.** When an intercepted letter falls into the wrong hands (an option you turn
  on), the one who read it no longer merely mentions it once: they can carry a lasting grudge over it, and it
  can cost you standing.

## v2.2.3.. 2026-08-25

A character keeps their own voice at the council table. Your saves from 2.2.2 carry over untouched.

### Fixed
- **A character keeps their own manner at a council.** A courteous, composed, knight-like character could turn
  unexpectedly blunt or rough once seated at a council, while the others stayed themselves. The council was
  describing each member only by their culture and rank, dropping the individual character that carries their
  voice, so a polite lord of a warlike people fell back to a rough, warrior tone at the table. Each member now
  carries their own nature into the council and keeps the same manner and level of politeness they use in a
  private word, so their voice stays consistent between a one-to-one talk and a meeting.

## v2.2.2.. 2026-08-25

Two fixes: a settlement-entry crash, and characters keeping up with a realm that changed under them. Your
saves from 2.2.1 carry over untouched.

### Fixed
- **A word in a settlement no longer floods the screen.** When a character caught you as you entered a town,
  castle, or village and asked for a word, accepting could pile the same popup up without end and freeze the
  game (you had to force it closed). The conversation now opens cleanly, one popup, no cascade. Declining was
  never affected.
- **Characters keep up with a realm that changed under them.** After a lord defected or a kingdom split apart
  at runtime (including new realms spawned by other mods, such as civil-war mods), they could still be
  introduced by the allegiance they had left, even while the same conversation already knew the new war lines.
  A character's realm and house are now refreshed to the current state every time you speak, so they name the
  kingdom they actually belong to now.

## v2.2.1.. 2026-08-25

Your letters become a book you can leaf through. Your saves from 2.2.0 carry over untouched.

### New
- **Your letters are now an open book.** Reading a letter opens a two-page leather book: the whole
  correspondence with that person runs down the left page (every letter that has ever passed between you,
  kept for good, oldest to newest), and the letter in your hand sits on the right. Their portrait rides the
  page, using a custom chat portrait if you have dropped one in, otherwise their usual face.
- **Intercepted letters can go astray (off by default).** Turn on "Intercepted letters can leak" in the mod
  settings and a letter waylaid on the road may fall into the wrong hands, and someone who wishes you ill may
  write to you about what they read.
- **A quieter or a busier room.** A new "Witness Density" setting lets you thin the crowd that gathers around
  a conversation, or leave it as full as it is now.
- **Council decisions that stick.** The accords your council reaches are now remembered as standing
  agreements: a character brings them up when you speak one to one, and you can review them on their
  encyclopedia page. A hostage pledged to you whose house then breaks faith is forfeit to you and becomes
  your prisoner.

### Fixed
- **Backing out of a reply no longer closes the letter.** When you started a reply and changed your mind,
  Cancel shut the whole window. It now simply returns you to reading the letter, so you can still acknowledge
  it and leave the ordinary way.
- **A slow letter is given more time.** Writing a letter waited only thirty seconds on the model before giving
  up; it now waits sixty, and a slow reply is no longer reported to you as an error.

## v2.2.0.. 2026-08-23

Gather your people to a council. Your saves from 2.1.5 carry over untouched.

### New
- **A council you can convene.** Call your people together to talk as a group: your companions, your kin, your
  clan's lords, or a war council of your allies. Open it from a new button on the campaign bar (bottom left), on land; it stays
  within reach whether you are out on the map or inside a town, castle, or village. Everyone sits at one table and speaks in the same scene, each in their own voice,
  weighing their regard for you, their culture, and their age. A new book-style window shows who is seated, where
  the sitting is held, and the decisions taken as they settle. The scene art follows the kind of council and where
  you hold it. Turn the whole feature on or off with the new "Enable Council" option in the mod settings.
- **A family council that knows its own.** Your kin gather at your word, seated from your household. Children old
  enough to sit (thirteen and up) take a place, and everyone speaks true to their age: a young one as a young one,
  an elder with the weight of years. The sitting opens with each of them greeting you in their own way.

### Fixed
- **A character no longer cuts you off mid-conversation.** An NPC would too often end the talk right after their own
  line, even while asking you something or making a request, so you never got to answer. They now keep the
  conversation open whenever they are waiting on a reply from you, and end it only when they truly part or dismiss
  you (a grave insult, a firm farewell). You decide when you have said enough.
- **No quest in an intimate scene.** In NSFW mode the mod no longer turns anything into a quest (that is not where a
  field task belongs). Roleplay and Enhanced still can. And, as before, a vow or a feeling is never mistaken for a
  quest: a quest is a concrete deed you can go and do, nothing else.
- **A witness remembers what they know.** When someone else is present in a conversation and speaks up, they now
  draw on their own memories, not just the last few lines said, so a companion no longer forgets an agreement you
  made with them alone. A character's memories are also tidied when you end the conversation.

## v2.1.5.. 2026-08-22

A companion's vow is theirs to keep, not a quest you can fail. Your saves from 2.1.4 carry over untouched.

### Fixed
- **A character's pledge is no longer your quest.** When a companion or lord vowed something to you (to fight at
  your side, to pay you a sum, to keep a peace), the mod wrongly turned it into an entry in YOUR quest journal that
  could close as "failed", and then cost you their regard and planted a grudge, even when there was never a chance
  to honor it (a companion who governs a fief, for instance, can never ride into battle beside you). A vow is the
  character's own word, not a task for you. It is now simply remembered: it appears in their Memories, carries no
  deadline, opens no quest, and never moves your relationship on its own. Old pledges from an earlier save are
  quietly retired with no penalty.

## v2.1.4.. 2026-08-22

A model switch in the chat, actions you can take by hand, and a redesigned Memories page.
Your saves from 2.1.3 carry over untouched.

### New
- **A model switch in the chat window.** It cycles Enhanced (two models for the best result, a writer plus an
  action reader), Roleplay (one lighter model), and NSFW (the explicit model). Roleplay and NSFW are a single
  call, lighter on local setups.
- **An Actions button.** Emit an action by hand (give gold, free a prisoner, appoint a governor, and so on) when
  you want to be explicit, or when the character missed what you meant.
- **A redesigned Memories page**, on illuminated parchment. Each entry that carried a real deed (coin changing
  hands, regard rising or falling, someone recruited, a fief granted) is marked in blue, so the turning points in
  your history with a character stand out.
- **A tavern keeper may point you to a nemesis** you have already met who ranges nearby, naming him and his region.

### Fixed
- **Characters who ask for a word inside a settlement** now show their portrait, open the conversation knowing it
  was THEY who sought you out, and no longer send a "lost track of you" notice hours after you have already spoken.
- **A noble or notable in a town you hold** now knows you are its lord, instead of asking what brings you to their city.
- **Giving a character a large sum of gold** now registers the full amount.

### For mod authors
- **A new voicing surface.** An addon can subscribe to each line as it is spoken (speaker, text, voice hints) to
  add text-to-speech, subtitles, or lip-sync. See the how-to in the module's Extension folder.

## v2.1.3.. 2026-08-21

Choose who is in the room when you confront a prisoner. Your saves from 2.1.2 carry over untouched.

### New
- **Any prisoner you hold can be confronted, not only your nemesis.** Talking to a captured lord from the party
  screen now lets you open a private confrontation with them, with its own captor's line; before, only a tracked
  nemesis could be brought to that scene.
- **Bring a companion into the confrontation.** You can hand the prisoner to one of your own companions, who will
  then act on their own initiative rather than only stand and watch, in their own voice and manner.
- **Bring another of your prisoners as leverage.** A second captive you hold can be brought in, coerced and in
  chains, so you can use one against the other. They are present under duress, never a free voice in the room and
  never a threat to you.

These are part of the mod's mature captor and captive scenes; the explicit layer follows your Adult Content
setting, and none of it appears at the lowest setting.

## v2.1.2.. 2026-08-21

Characters who come to you, a lesson that sticks, and a nemesis you can finally track. Your saves from 2.1.1
carry over untouched.

### New
- **Characters seek you out for more reasons.** A lord who holds a real grudge against you may ride out, or ask
  for a word in a fief, to air it; one who owes you a kindness may seek you out to acknowledge it. And a character
  standing in a settlement with **no field party** of their own (a lady, a notable, a garrisoned lord) can now ask
  for a private word too, where before only a lord who could ride out on the road ever got a turn. Always an
  accept/decline, never forced, declining costs nothing, and rate-limited so a busy town never opens a queue.
- **A lesson leaves a mark.** When a character genuinely skilled in something gives you a real lesson in
  conversation, it grants actual **skill XP** on your character sheet, bounded so it is a nudge not a shortcut, and
  one teacher cannot be farmed.
- **Your nemesis is no longer a ghost.** A nemesis you have already tangled with now carries a record on his
  **encyclopedia page** (who he is, the region he ranges, your shared history: the times you met, your escape, your
  victories). And a lord whose lands he plagues may seek you out to name him and tell you where he operates,
  pointing you to the bounty to see him gone, so a nemesis you swore to hunt can actually be found.

## v2.1.1.. 2026-08-20

More life from your companions, and letters that are remembered. Your saves from 2.1.0 carry over untouched.

### New
- **Companions seek you out for good reasons, not only when unhappy.** Three new private-audience occasions: a
  companion may come to **thank you** warmly for a kindness you did them and reaffirm their loyalty; a loyal one
  may **warn you of a secret plot** against you (heeding it brings the plot into the open); and an ambitious one
  may **ask to rise**, for a command of their own to lead, a post in your party, a fief to govern in your name, or
  your blessing to wed. They come at a settlement, gated so they never pester, and refusing costs nothing.
- **Separate API keys per model role.** The explicit model and the action reader can each carry their own API key
  now, not just their own endpoint, so you can split them across local servers that each require a key.

### Fixed
- **A promise made by letter is remembered in person.** An NPC who proposed a match or agreed to something by
  letter now recalls it face to face instead of denying it happened: a substantive letter leaves a real,
  first-person memory of what was written, not just a note that a letter arrived.

## v2.1.0.. 2026-08-20

Some requested features, and a big sharpening of how the mod reads what actually happens in a scene. Your saves
from 2.0.0 carry over untouched.

### New
- **A switch to steer which model writes the scene, back by request.** Turn on "Manual Roleplay/NSFW switch in
  chat" in the mod options and a small switch appears in the chat window: you choose whether your everyday Roleplay
  model or the model you use for explicit content writes the current scene. The automatic fallback to the explicit
  model still applies either way; this simply lets you nudge it by hand, the way older versions did. Off by default.
- **Point each model role at its own server.** If you run more than one local model, the explicit model and the
  action reader can each have their own endpoint address now, so you can split the roles across different machines.
- **A "?" button in the chat window** opens a short guide to what each control does.

### Improved
- **Actions are read far more accurately, across different models.** The pass that reads each turn now checks every
  action against the actual words of the scene before recording it, so it stops logging things that were only
  offered, refused, or promised; it keeps the direction straight (who paid whom, who freed whom, who holds whom);
  and it no longer loses a real deed, an execution, a divorce, casting a companion out, sending an escort home,
  just because the scene also comes to a close.
- **Letters are now signed.** Each letter closes with the name of whoever wrote it, so you always know who it is from.
- **Battles read truer in memory.** A siege is remembered as a siege rather than "an open battle near" the town, and
  a victory you won as part of an army is remembered as a shared one.

## v2.0.0.. 2026-08-16

A new two-model roleplay engine, and the largest pass yet on how intimate and captive scenes read. Your saves
carry over untouched.

### New
- **Give each job its own AI model, and let a reliable model finish what a strong writer dodges.** You can now
  set three models: one for prose (the writing), one for explicit content, and one that reads each turn to detect
  actions. The new default "Enhanced" mode lets your chosen writing model carry every conversation, and when that
  model stalls or cuts an explicit beat short, as many capable writing models do, a reliable second model quietly
  completes the beat and hands the scene back. You get better prose without losing the follow-through, and nothing
  to toggle by hand. A lighter "Standard" mode keeps the older single-call behaviour when you would rather spend less.
- **Each model role can use its own provider.** Prose on one service, explicit content on another, the action
  reader on a local model, whatever suits you. Set it once in the mod options.
- **Actions are read by a dedicated pass.** Relation changes, gold, duels, recruiting a companion and the rest are
  now detected by a model whose only job is to read the scene, so they fire reliably no matter which writing model
  you use.

### Fixed
- **Captive and coercive scenes read far better.** When your men act on your order, it is written as them acting,
  not as you; the band speaks to the prisoner as they use them; each man's finish is depicted and spread across the
  scene rather than everyone at once; a blow that draws blood now registers as real harm; a captor never assumes the
  prisoner obeyed a command they in fact refused; a captor uses only what is actually at hand instead of conjuring
  implements out of nowhere.
- **A captor who has just won a battle no longer invents a false past for the loser.** Previously one might claim he
  had captured you "two moons past" and already sold you off, moments after the fight; now he speaks the plain truth
  of who he is holding right now.
- **Characters narrate their own gestures in the first person.** A character now writes "I narrow my eyes" rather
  than "your eyes narrow", which used to read as though you had done it; and a stray narration label no longer leaks
  into a spoken line.
- **Warning a character of danger, or helping them, no longer costs you their regard.** Guidance now separates what
  the player earned from the character's own wary mood: a helpful act met with suspicion is still helpful. Over-
  generous relation jumps have also been calmed.
- **A character with no noble clan no longer calls themselves "of the No clan clan"**; they read as being of no
  noble house.
- **Fixed a crash on loading with the current game version (War Sails)**, and letters written in Russian now use a
  legible font instead of a decorative one that could not render Cyrillic.
- **Memory upkeep is quieter and cheaper.** The memory of characters who have died is no longer compressed (it is
  never read again), and a memory task that simply times out no longer shows an alarming red error; it retries later.
- **Long explicit beats are no longer cut off mid-sentence.**

## v1.34.7.. 2026-08-12

Marry one of your own companions, and household who stay behind no longer feel forgotten. Your saves carry over
untouched.

### New
- **Marry your own companion.** You can now wed a wanderer you recruited into your clan, not only a born noble.
  Once your regard is high enough and neither of you is otherwise wed, the companion is raised to a lordship of
  your house and the marriage proceeds like any other. Blood relatives are always off-limits, and a marriage will
  not be arranged while either of you is bound up in an army. Should such a marriage later end, the former spouse
  (who had no birth house of their own) returns to the pool of wanderers you could recruit again, rather than
  lingering as a stranded noble.

### Fixed
- **A spouse or family who stays behind no longer feels left behind.** Your wife, consort, or close kin who is
  keeping your household while you are away on campaign now understands that this is a married life split between
  road and hearth, not an absence they resent. Previously a household member apart could open a conversation as
  though you had abandoned them, counting the days since they last saw you; now they speak as one of your own
  house, awaiting your return. Household who ride with you were already handled; this covers those who stay home.
- **Characters are no longer relentlessly cold, especially on lighter AI models.** Players on smaller local models
  reported that almost everyone they met acted mean, with no good people anywhere. The guidance that shapes how
  every character behaves now states plainly that being guarded is not being cruel: a stranger who has given no
  offense is met with ordinary courtesy, and coldness or insult is a response to a real slight, a threat, or a bad
  history, never the opening manner toward someone who has done nothing. This restores the range of decent, warm,
  and neutral people the world is meant to have.
- **Bandit tasks are findable, and reporting an unfindable one no longer earns a grudge.** A "clear the hideout"
  task now puts its map marker on the actual lair instead of the nearby town; a "clear the bandits" task is only
  ever offered when bandits are truly roaming near that town; and if you tell the giver you could not find the
  target, they take it as a mild disappointment rather than a broken word, with no lasting grudge held against you.

## v1.34.6.. 2026-08-11

Lords who ride with you, courtship trials, and recruiting a notable. Your saves carry over untouched.

### New
- **A lord or lady can ride with you.** A friendly noble of another clan may agree, in conversation, to accompany
  you for a time, for whatever reason the moment gives (friendship, a shared cause, a courtship). If they command
  an army of their own, their party escorts yours and they keep their command; if they have no party, they ride
  in your own party while remaining of their own house and colours. You are told when one joins or leaves, and if
  their people go to war with yours, they break off to answer that call. Ask them to part ways whenever you wish.
- **Courtship trials.** Court someone and have them ride at your side, and after a couple of seasons the time spent
  together settles them: they write to you themselves to accept the match, and the marriage opens. The trial only
  clears the "not close enough yet" gate, never a real barrier such as an existing marriage, a war, or kinship.
- **Recruit a notable into your clan.** Persuade a settlement notable (a gang leader and the like), once you have
  earned their deep trust, to leave their post and swear into your clan as a companion. They bequeath their gang
  and standing to one of their own people, who takes up the post after them, and they arrive already an ally.
- **The Empire's collapse reads as recent, not ancient.** Characters now treat the Empire's civil war as the fresh
  wound it is, a few years old and within living memory, rather than an old ruin fallen generations past.

## v1.34.5.. 2026-08-10

Open relationships, a jealousy you can turn off, and several fixes from player reports. Your saves carry over
untouched.

### New
- **Open relationships.** You and a committed partner (a spouse or consort) can agree to open terms, and either
  of you may raise it: you may offer reciprocal freedom, or she may ask leave to love another (or urge you to
  take another spouse). An open partner no longer grows jealous of your other loves, and over time takes a lover
  of her own, the more so the more you have neglected her. You can learn of it, speak of it with her, ask her to
  end the affair, or revoke the open terms entirely. In a harem-minded culture (such as the Aserai) a wife keeps
  such an affair secret, and it may come to light as a scandal. A child she conceives by her lover belongs to his
  line, never yours.
- **Turn jealousy off.** A new mod option disables the whole jealousy and competition system for a drama-free
  household. The world is richer with it on (partners react to being favoured or slighted), but the choice is yours.
- **Companions remember fighting beside you.** After a notable battle, the companions riding in your party (and
  your kin) gain a memory of having fought at your side, just as allied lords already did.
- **News of the realm reaches you.** A friendly lord may write to share word stirring in their kingdom: a war
  looming, hard times fallen on a town, or a rumour that reached them, relayed in their own voice.

### Fixed
- **Kin know when you hold their relative prisoner.** A character whose child, sibling, or spouse you hold captive
  no longer calls you a liar for it; they know their kin is in your hands.
- **No more impossible quests.** A task can no longer hinge on an item that does not exist in the game, and a task
  you tell the giver you cannot or will not carry out is now always released.
- **A warm word no longer costs you regard.** A character who answers you warmly will not, in the same breath,
  lower their opinion of you.

## v1.34.4.. 2026-08-07

A new diplomatic action, and your letters are now kept and re-readable. Your saves carry over untouched.

### New
- **Broker peace between realms.** In conversation with a faction leader (a king or ruler) whose realm is at
  war, you can now mediate peace between their faction and a named enemy realm, even when your own faction is
  not part of that war. It takes standing: you must be a noble of rank (clan tier 3 or higher) whom that leader
  deeply trusts.
- **Your letters, kept and re-readable.** Every letter you exchange with someone is now saved as a dated
  thread. Open a character's Encyclopedia page and press "Letters" to read your whole correspondence with them,
  oldest first, each headed with the season and day it was sent, in a larger scrollable window styled after the
  conversation view.

## v1.34.3.. 2026-08-06

Three fixes from player feedback. Your saves carry over untouched.

### New
- **Recruit a captured lord to your side.** Persuade a hero prisoner you hold, once you have earned enough
  standing with them, to switch sides: they are freed, swear into your clan, and join your party. Before, a
  captured noble could be talked into joining but nothing actually happened.

### Fixed
- **Your narrative style is remembered.** The writing style you pick is now saved and restored on the next
  launch, and survives a mod update, instead of resetting to Default on every restart.
- **Asking a lord to ride out with you is more reliable.** When a lord agrees to follow you on the map, the
  model is now pushed to actually set the escort in motion rather than only describing it in prose (a weaker
  model used to just narrate riding out and nothing followed); and it will no longer pretend you rode out
  together when the lord has no field party to bring along.

## v1.34.2.. 2026-08-06

A resilience release. The conversation actions are now more robust and fairer when the world changes between
what is said and what is done. No new features; your saves carry over untouched.

### Fixed and hardened
- **Actions re-check the world at the moment they happen.** Naming a governor now respects an already-filled
  post and a companion who is away on an errand; joining a kingdom you are at war with is refused; revoking a
  fief releases its sitting governor.
- **You are no longer blamed for what the world does.** An oath to keep the peace is not broken when someone
  attacks YOU (only the one who marches breaks it); a promisor taken captive is not branded an oathbreaker while
  he can do nothing; a quest whose target dies, or a pledge whose rival dies, is quietly voided instead of
  penalizing you.
- **Prisoner and gold dealings are safe and honest.** No captive is handed over or sold without a real holder to
  receive them; gold moves only after the trade is certain, and is refunded if it fails; a lord cannot buy a
  captive for a pittance while claiming the full price; and a member of your own house is never merchandise.
- **Captive-scene actions respect their own rules again** (the darker ones only apply while you are genuinely a
  prisoner in a Hardcore scene).
- **Companions are looked after.** A stipend refunds the remainder and tells you if its recipient dies; a
  companion away on an errand can no longer wrongly quit or stay away forever; an expelled companion will not
  reappear in your party.
- **Marriage and persuasion are fairer.** Arranging a match now respects captivity and war; a divorce blocked
  only because your spouse was handed a role now proceeds instead of falsely closing ranks against you; swaying
  a lord no longer defaults to the harmful direction on a garbled request.
- **The channels tell one truth.** The chat, the story log, and the quest journal now agree with the game: no
  logged sum the purse never saw, a refused action leaves a clear note, and an unverified quest-complete claim
  tells you plainly that nothing was paid.
- **Under the hood:** unreachable dead code removed, and a new quiet diagnostic watches for actions the AI
  proposes out of context (log only, for future tuning).

## v1.34.1.. 2026-08-06

A hardening release: many conversation actions are now safer and fairer, and letters survive the road better.
Your saves carry over untouched.

### Fixed
- **Fiefs stay within your realm.** Granting or revoking a fief in conversation now checks that the other party
  is a vassal of your own kingdom, so a ruler can no longer be talked into handing a crown fief to a foreign or
  enemy house, or confiscating an enemy's holding with no siege.
- **No more vanishing prisoners.** A prisoner handed over or bought in conversation now always ends up with a
  real captor; before, a lord with nowhere to hold one could take your coin and leave the captive in limbo.
- **Ransom is honored.** A captor open to ransom now actually frees you once the terms are met, instead of
  taking the payment and keeping you caged.
- **Goods returned on a failed hire.** Wares you offer to seal a recruitment that then falls through are
  returned to you, rather than lost.
- **A clan keeps its head.** A lord can no longer be persuaded to dissolve their own clan into yours.
- **The peace oath blames the aggressor, not the victim.** A lord who swore to keep the peace and is then
  attacked is no longer branded an oathbreaker for a war waged against them.
- **Honest reinforcements.** Troops a lord gives your party are now described as a permanent gift, not a loan
  that never returned.
- **No silent non-actions.** When the AI proposes something the game cannot actually do, it now says so plainly
  instead of quietly doing nothing.
- **Letters survive the road.** A letter carried a long way (when you are far from the sender) is far less
  likely to be lost to bandits; interception no longer piles up with distance.

### New
- **Send letters under armed escort.** When you hire a courier, you can pay for an armed escort; that whole
  correspondence, both directions and every later reply, then always gets through, safe from interception.
- **New option: Invulnerable couriers.** A single switch in the mod options that makes your letters never get
  lost to bandits at all.

## v1.34.0.. 2026-08-05

Characters can now act on what you agree to in a one-to-one conversation, not only at a council, plus a new
cross-talk option and a pregnancy fix. Your saves carry over untouched.

### New
- **Characters carry out commands and appointments right in conversation.** When it genuinely fits your situation,
  a character may act on what you agree to, one to one, instead of only at a council: name a companion or vassal as
  governor of one of your fiefs, give a companion a party role (scout, engineer, quartermaster, surgeon), call a
  companion back into your party, send a companion off on an errand (bring back word of the realm, spy, and so on),
  set a standing stipend, or take a sworn oath; and as a ruler, grant or revoke a vassal's fief, or dismiss a
  companion from your clan. Each is only offered when it truly applies, and the game re-checks it before anything
  actually happens.
- **New option: NPC Cross-Talk.** Controls how many times the NPCs in a scene may bounce a remark off one another
  before the turn comes back to you (0 to 3, default 2; set it to 0 so only the character you spoke to replies).

### Fixed
- **Pregnancies no longer last forever on a no-births campaign.** If your campaign has the Life and Death cycle
  turned off, a pregnancy could never be delivered and a character would keep bringing it up with no end; such a
  pregnancy now quietly ends on its own, and none is started while births are disabled.

## v1.33.6.. 2026-08-02

More fixes and a safeguard, all from your feedback. Your saves carry over untouched.

### New
- **Keep your custom prompts and styles safe from updates.** A new mod option, "Custom Folder Path", lets you point
  the mod at a folder OUTSIDE the mod for your edited prompts and styles, so a reinstall or a clean update can never
  wipe them (leave it blank for the default ModuleData/Custom). It also lets you keep several prompt sets and switch
  between them. (Report: a player whose Custom folder was emptied on an update.)

### Improved
- **A promised task now actually becomes a quest.** When a character agrees in conversation to send you on a
  concrete mission or errand, the quest is created that same turn, instead of the agreement sometimes evaporating
  into back-and-forth talk with nothing recorded. (Report: a player on Nexus.)

### Fixed
- **Captured at sea no longer plays out in a land camp.** If you are taken prisoner at sea (for instance by Sea
  Raiders), the scene now knows you are aboard a ship rather than inventing a tent by a campfire. (Report: a player
  on Nexus.)
- **Coercion is no longer remembered as intimacy.** A non-consensual captive scene is never recorded as a consensual
  intimate memory, and never warms a character's regard as though it were one; the harm is remembered as what it is.

## v1.33.5.. 2026-08-01

Quality-of-life additions and fixes from player feedback. Your saves carry over untouched.

### New
- **Lock an NPC's kinks in the character override file.** If you author characters in
  `ModuleData/character_overrides.json`, you can now add a `kinks` list to lock specific kinks on an NPC (the
  full list of valid names is in that file). Unlike adding them by console, an authored lock survives a lore
  reload, shows up when you inspect the NPC, and drives the scenes you stage. Requires "Use custom traits" in the
  mod options. (Report: a player on Nexus.)
- **Dark Leather size option.** A new "Dark Leather Size (%)" setting in the mod options scales the leather chat
  window, so you can make it larger on a high-resolution screen where it felt cramped. 100 is the shipped size,
  and the Origin skin is unaffected. (Report: a player on Nexus.)

### Improved
- **Characters hold truer to their nature.** A cold, cruel, or calculating lord could drift into kindly, agreeable
  answers that did not fit them (a ruthless lord suddenly championing the peasantry). The prompt now presses harder
  to keep replies in line with the character's own traits. (Report: a player on Nexus.)
- **NPCs stop putting your army in the room.** In a city or a keep, an NPC would speak as though your soldiers
  stood at your back, when your men would really be beyond the walls. Indoors they now know your force is not
  present, while still aware of your standing as a commander; out on the open road it is unchanged. (Report: a
  player on Nexus.)

## v1.33.4.. 2026-08-01

A rework of how NPC letters reach you, plus two fixes from player reports. Your saves carry over untouched.

### Improved
- **Letters now come at a saner pace, and the mod learns which ones you care about.** Several of you deep into long
  campaigns said the same thing: you love the letters, but after building many relationships there were simply too
  many. Letters are now chosen by importance and held to a steady volume, so a long campaign no longer buries you.
  The most meaningful letters (a threat, a proposal, a wartime plea) take priority; routine ones wait their turn or
  step aside. The mailbox adapts to you: dismissing a letter with Acknowledge quietly lowers that sender and that
  kind of letter, while writing a real reply raises them, and you can simply tell someone in a reply to write you
  less (or more), which the mod picks up in any language. A new option in the mod menu, "Letters from NPCs"
  (Rarely / Occasionally / Often), sets the overall pace. A battle won against long odds is now far more likely to
  be the one an ally writes about. Letters that must never be missed (a divorce, a child-support request, a
  companion leaving, a reply to a letter you sent) always come through.

### Fixed
- **Pregnant women now know they are pregnant.** A pregnant NPC would deny being with child when asked, because her
  pregnancy was never told to her in conversation. She now acknowledges it plainly, and stays discreet about the
  father when it is a secret (an affair, or a hidden bastard), so nothing is spoiled. (Report: a player on Nexus.)
- **Female villagers are no longer spoken to as men.** A commoner's gender was picked at random, disconnected from
  the villager on screen, and the commoner prompt never stated a gender at all, so a woman was often voiced as a
  man. Both are fixed: the mod reads the real villager's gender and tells the character who they are. This also
  corrects vanilla female townsfolk and villagers. (Report: a player on Nexus.)

## v1.33.3.. 2026-07-31

A compatibility fix and a bug fix, both from player reports. Your saves carry over untouched.

### Fixed
- **No more phantom companion weddings.** Camp life could announce that two companions had married when the game
  had actually declined the match (`MarriageAction.Apply` silently no-ops unless the marriage model deems the
  couple suitable, and a same-clan companion pair often is not), so the wedding appeared nowhere afterward.
  TryMarry now pre-checks `MarriageModel.IsCoupleSuitableForMarriage` before the chance roll and verifies the
  spouse link after applying, so a wedding is announced only when it truly took place. (Report: a player on Nexus.)
- **No longer crashes at startup alongside Europe1100 / EOE 1100.** Both mods registered a save type at the same
  internal ID (300000001), so whichever loaded second crashed the game at boot ("an item with the same key has
  already been added"), and no load order avoided it. Calradia Remembers now detects Europe1100 at boot and shifts
  its own quest save IDs to a distinct base, so the two run together. Players without Europe1100 are completely
  unaffected: the shipped base is kept and existing saves are byte-for-byte untouched. NPC memories and relations
  are unaffected in either case (they live in a separate store). The only edge is that adding or removing
  Europe1100 mid-campaign moves the base and drops any in-progress CR quest, but that already desyncs the mod from
  the game. (Report: a player on Nexus.)

## v1.33.2.. 2026-07-31

A small follow-up for players who use overhaul mods. Your saves carry over untouched.

### Improved
- **The Custom folder now keeps your culture edits too.** `cultural_traditions.json` and `jealousy_cultures.json`
  can now live in `ModuleData/Custom/`, so an overhaul player's own cultures survive a mod update, like the other
  prompt files already do. They were read only from the shipped copy before. Both are keyed by the culture's
  display name (case-sensitive; the "Culture:" line on a character's chat info panel shows it), and an unlisted
  culture still degrades gracefully. (Question: a player on Nexus.)

## v1.33.1.. 2026-07-31

Two relevance fixes from a player report. Your saves carry over untouched.

### Fixed
- **Characters speak their age.** A character's own age was never given to the AI, so a lord of twenty-two could
  talk about himself as if he had lived a long life. Each character now knows how old they are (carried on their
  profile, refreshed each conversation) and the identity prompt tells them to speak as someone of their years.
- **Characters who share a first name are no longer mistaken for one another.** Names repeat in Calradia, and the
  model could conflate two different people who share one (a player's six-year-old son and a Battanian lord both
  named Luichan, so the father spoke as if the child had captured a castle). Heroes are now named with their
  house wherever it matters (rumors read "Luichan of Fen Company took X"; a shared `HeroLabel` helper appends
  clan and culture at every naming site), family members carry their age ("Luichan (male, age 6)"), and a
  standing prompt directive states that a name in the news is not necessarily a relative of the same name. Scales
  as later generations repeat names. (Report: a player on Nexus.)

## v1.33.0.. 2026-07-31

Two features for players who like to tinker with the writing, plus a couple of quiet improvements. Your saves
carry over untouched.

### Added
- **A Custom folder that survives updates.** The prompt files that shape how characters write (world.txt,
  player_description.txt, post_history_instructions.txt, the behaviour guidelines) and your narrative styles all
  live in ModuleData next to the mod's own files, so editing them directly meant an update would overwrite your
  work. There is now a `ModuleData/Custom` folder: drop a copy of any of those files there (same name), or put a
  style in `Custom/Styles`, and the mod loads yours first and never touches it on update.
- **Template variables in your prompts and styles.** Player-authored text (styles, and the prompt files above)
  now understands SillyTavern-style `{{token}}` variables, resolved fresh for the character you are talking to
  and the hero you are: `{{char}}`, `{{char_gender}}`, `{{user}}`, `{{user_gender}}`, `{{user_clan}}`,
  `{{faction}}` (also `{{user_faction}}`), `{{location}}`, `{{spouse}}`, `{{father}}`, `{{mother}}`. Resolved at
  the moment each reply is written, so one style file works for every character. Case-insensitive; an unknown
  token is left exactly as written instead of vanishing.

### Improved
- **Characters stay consistent through long conversations.** In a long exchange (above all a gathering of
  several characters at once), the earlier part of the talk is folded into a compact running recap given to
  every speaker, so no one loses track of what was said or agreed to earlier, without the prompt growing without
  bound. Short conversations are untouched.

### Fixed
- **A lord no longer "agrees" to peace or a parley in conversation.** War and peace between realms are a ruler's
  strategic decision, not something settled in a chat, so a character can no longer talk as if concluding a truce
  or committing their faction to a negotiation on the spot: they defer it to their liege, closing off a promise
  the game could never keep.

## v1.32.3.. 2026-07-30

A hotfix for the Dark Leather window. Your saves carry over untouched.

### Fixed
- **Dark Leather is no longer oversized on high-resolution screens.** The v1.32.2 fill-the-screen change scaled
  by the display's resolution, so on a 1440p or 4K screen the illuminated book grew far too large and spilled off
  the edges. It now fills a fixed share of the screen height on every resolution. (Report: a player on Nexus.)

## v1.32.2.. 2026-07-29

A few fixes, mostly from player reports. Your saves carry over untouched.

### Fixed
- **The Dark Leather chat window now fills the screen.** On a high-resolution display the illuminated book
  opened noticeably smaller than the classic panel. It now scales to fill the screen height at any resolution,
  with the ornate border and every control kept in place. (It respects your UI Scale setting.)
- **Characters now remember letters you sent them.** A letter you sent to a character (through the tavern
  keeper) was only "known" to them for their immediate reply, then dropped, so later in person they could act
  as if it had never arrived ("still awaiting a letter on the subject"). A delivered letter is now filed as a
  lasting memory, so the character remembers it in later conversation, the same way they already remember the
  letters they send you. (Report: a player on Nexus.)
- **Memory compression no longer fails on heavy reasoning models.** With a reasoning-heavy model (e.g. GLM),
  the memory-compression step could fail with "the model spent its entire reply budget on internal reasoning":
  it reserved too little room for the answer after the model's thinking. It now reserves far more. Compression
  was already safe when it failed (no memories are ever lost, the character simply keeps them uncompressed);
  this just stops the error. If it still happens on a very heavy reasoning model, lower Reasoning Effort in the
  mod options. (Report: a player on Nexus.)

## v1.32.1.. 2026-07-29

Two fixes on top of v1.32.0, both from player reports. Your saves carry over untouched.

### Fixed
- **A violent or coerced encounter no longer leaves a false "tender moment."** A character's encyclopedia could
  show a fond kindness ("carries a small kindness remembered towards you for a tender moment we shared") after a
  hostile scene that actually lowered your relation, because that kindness was recorded on any moment the AI
  tagged as intimate, with no check on the scene's tone. A fond memory now forms only when the moment neither
  lowered your relation nor was a captive/coerced scene; a hostile encounter is still remembered, it just no
  longer leaves a false tenderness on the page. (Report: a player on Nexus.)
- **Compatibility with total-conversion overhauls.** Calradia Remembers no longer declares hard dependencies on
  two base modules it never actually used (StoryMode, CustomBattle). An overhaul that replaces the vanilla
  campaign and does not load those (e.g. Empires of Europe 1100) could stop the game from launching at all, with
  nothing in Crash Doctor (a launcher-level module error, not a code crash); it now loads alongside them.
  (Report: a player on Nexus.)

## v1.32.0.. 2026-07-28

A new look for the conversation, a new thing to do with your prisoners, and a compatibility fix. Your saves
carry over untouched.

### Added
- **A new chat window: "Dark Leather."** Conversations now open in an illuminated two-page leather book, the
  new default look. The exchange is on the right page; click any portrait to open a dossier on the left, the
  speaker's culture, age, role, standing, background, what you have learned about them, and their grievances.
  Click your OWN portrait to read your own notable deeds. Prefer the original panel? A new **Chat Window Style**
  option in the mod menu switches between **Origin** (the classic panel) and **Dark Leather** at any time. It is
  the same conversation either way, only the look differs. (More skins are planned.)
- **Dress your prisoners.** Give a held prisoner a garment (a gown, a fine tunic) in conversation and she
  actually puts it on, visible the next time you speak with her. A captor imposes the garment regardless of how
  the prisoner regards you; only clothing is ever handed over this way, never a weapon.

### Fixed
- **Compatibility with Immersive Recruit Commoners.** Calradia Remembers' townsfolk and villager chat option
  used to sit on the raw conversation opening, where it could crowd out another mod's options (IRC's recruit
  lines). It now hangs off the normal town/village talk menu, so both mods' options coexist. (Report: a player
  on Nexus.)
- **A grouped execution shows a single notice.** Executing several captives at once now raises one banner for
  the deed instead of one per grieving kinsman.

## v1.31.15.. 2026-07-27

Two fixes, from a player report. Your saves carry over untouched.

### Fixed
- **The chronicle now follows your chosen Reply Language.** With Reply Language set to a language (say
  Spanish), conversations, letters, and memories obeyed it, but the illuminated chronicle kept coming out in
  English: it is a separate piece of writing that was never told the language. Now it is, so the illuminated
  chronicle reads in your language too. (The plain, non-illuminated chronicle still uses fixed English date
  headings; it is the illuminated version that follows the setting.) If you want it without waiting for a
  rewrite, you can also name the language in `chronicle_style.txt`. (Report: a player on Nexus.)
- **A failed AI reply now tells you why.** When a reply fails, the on-screen notice used to give the same
  generic "check your model and endpoint" no matter the cause. It now shows the actual reason when there is
  one, so a timeout on a long prompt (raise "LLM Response Timeout" in the mod options) reads differently from an
  endpoint or model mistake, and you are not left guessing.

## v1.31.14.. 2026-07-27

A conversation fix, from a player report. Your saves carry over untouched.

### Fixed
- **Mentioning another character no longer hijacks the conversation.** With several NPCs present, referring
  to one of the others in a comma-separated phrase ("I wanted to ask about your friend, Manteos", "Do you know
  my sister, Aitheen?") wrongly switched the active speaker to the mentioned character, who then often answered
  oddly for a line before recovering. That phrasing is a reference, not an address: the current NPC now keeps
  the floor, and a name is treated as a direct address only when you actually address them ("Manteos, what say
  you?"). (Report: a player on Nexus.)

## v1.31.13.. 2026-07-26

Two features: choose the voice your story is told in, and a memory limit so long-lived saves stay quick.
Your saves carry over untouched.

### New
- **Choose the voice the story is told in.** A new quill button in the chat window opens a picker of writing
  styles that change how the prose reads, never who a character is or what they will do. Fifteen ship with the
  mod, from plain Novelistic, Terse, and Grimdark to author-anchored voices (Tolkien, Martin, Cornwell, Austen,
  McCarthy, Dumas, Kay, Homer, Dostoevsky, Howard, and more). Your choice applies everywhere, to lords and
  commoners alike, and to whichever model you run. Styles are plain text files under ModuleData/Styles: edit one
  or drop in your own, even in the middle of a game, and your next line uses it. (Console: cr.styles, cr.style.)
- **A limit on how much a character remembers, so veteran saves stay quick.** As a character piles up remembered
  moments, the mod now folds the oldest into a short background outline instead of handing the AI a character's
  entire history on every message. Long-lived saves keep their speed and cost in check without losing the shape
  of a character's past. On by default, with a toggle in the mod options. (From a modder's report of a
  conversation's context running past 20,000 tokens.)

## v1.31.12.. 2026-07-26

A small consistency fix, from a player report. Your saves carry over untouched.

### Fixed
- **A character's memories now keep one voice.** Some remembered moments were written in the first person ("I
  told him...") and others in the third ("Nethor granted...", "he admits..."), so a character's memory panel
  could read unevenly. New memories are now always written in the character's own first-person voice, matching
  the conversation recaps. Memories already saved keep whatever voice they were written in (the mod does not
  rewrite them). (Report: a player on Nexus.)

## v1.31.11.. 2026-07-25

A new feature, from a player request: you can now play matchmaker. Your saves carry over untouched.

### New
- **Arrange a marriage between your kin and another noble's house.** You can now propose a match between one of
  your own unwed kin and an eligible noble of another clan, a marriage in which you are neither party. Speak with
  the head of another house (or a noble parent) and, if you have unwed kin of your own, you may put the match to
  them. They weigh your standing and the gain to their house: a family that thinks well of you agrees, and so
  does one whose kin would marry up into a greater clan, while a stranger of no greater standing is turned away.
  The wedding is real and follows the game's own rules of who may wed whom. (Console: cr.arrange_marriage.)
  (Request: CarolusIV on Nexus.)

## v1.31.10.. 2026-07-25

A grab-bag release: three fixes so a devoted companion stops asking to leave out of nowhere, a new touch (NPCs
can now wear the equipment you gift them), and a quest-compatibility fix. Your saves carry over untouched.

### New
- **Gift equipment, and they may actually wear it.** When you hand an NPC a piece of equipment in conversation,
  they may put it on then and there: give your wife a fine gown and she wears it about town, give a companion a
  better blade and he takes it up. An empty hand or a real upgrade is always taken; a lateral gift is worn only
  by someone fond of you (a spouse, a companion, a lord who holds you dear), while a rival keeps his own gear.
  It respects the item (a man is not put in a gown), and civilian pieces show in town while war gear is drawn on
  the field.

### Fixed (companions)
- **A content companion no longer asks to leave out of nowhere.** When a companion pulled you aside for a
  private word (a rumour they had heard, some practical counsel, an old memory that resurfaced), the mod could
  wrongly frame it as a war-weary bid to retire, so a perfectly devoted, unwearied companion would suddenly ask
  to lay down the sword, then never actually leave and, asked about it later, have no memory of it. That private
  word now stays on its real subject. (Report: a player on Nexus.)
- **Companions are far more resilient to the toll of war.** In a game built on battle, a companion should endure
  many more wounds and defeats before growing weary enough to want out. The weariness a companion bears before
  asking to retire is raised substantially, and real rest eases it a little faster.
- **A companion will not ask to retire within their first two years of service.** A young recruit who has barely
  served can no longer ask to leave for war-weariness, however hard the road has been.

### Fixed (quests)
- **Townsfolk you question for a quest are no longer interrupted by the local-rumours chat.** During a quest that
  has you ask people around a town for information (for example The Spy Among Us), CR's "talk to the locals"
  option was diverting those conversations, so you could not get the quest's clues. That option now steps aside
  for the length of such a quest, and returns once it is done. (Report: a player.)

## v1.31.9.. 2026-07-25

A duel fix, from a player report. Your saves carry over untouched.

### Fixed (only if you use duels)
- **You can now duel companions and settlement notables, not just lords.** Challenging a companion (or a gang
  leader, a merchant, a village headman) did nothing but make the AI describe a fight in text: a real duel
  could only be offered against a noble, so with anyone else the challenge never launched. Any named person of
  standing is now a fair field of honour; only a nameless commoner is still turned away. (Report: a player.)
- **The AI no longer plays out a fight it cannot actually stage.** When a real duel is not on the table, a
  character is now told plainly never to narrate a duel, or a blow struck, as if it happened, and to say so
  rather than acting it out. A fight the game will not run is treated as talk, not a scene.

## v1.31.8.. 2026-07-24

A batch release: characters stop forgetting a plan you just made with them, several player-reported fixes,
duels and forced marriage, and a new door for other mods to read and write what a character remembers. Your
saves carry over untouched.

### Fixed (everyone, please read)
- **Characters no longer forget a plan you just made with them.** You could agree on something with a
  character (say, a family member offering to gather information and report back to you that same day), let a
  little time pass, return, and find they had no memory of it, sometimes indignant that you would claim such a
  thing. A conversation's memory used to be kept only when the model happened to flag a dramatic "event"; a
  plan or a promise it did not flag was lost when the window closed. Now every conversation of substance
  leaves a memory, the summary is told to keep any plan, promise, or arrangement, and it is written on a
  durable queue that survives a save, a quit, or a crash. (Report: a player on Nexus.)
- **A mercenary contract now pays what it should.** Signing on as a mercenary paid a fiftieth of the agreed
  sum: a contract worth thousands handed over a few dozen denars. It now pays in full. (Report: a player.)
- **A bandit nemesis no longer "forgets" who he is mid-captivity.** The named bandit boss who holds you could
  seem to become a different man partway through, with no memory of what came before: a save, a reload, or a
  prisoner transfer could quietly mint a second boss who inherited the first one's memories. He is now
  anchored to one identity, restored intact across saves and never silently replaced. (Report: a player.)
- **A fond memory is no longer duplicated.** A character's encyclopedia could show the same tender moment
  three separate times. Repeats of the same moment are now COUNTED ("three times over") rather than stacked as
  identical copies, which also stops them crowding out everything else the character remembers of you. Old
  saves are repaired automatically. (Report: a player screenshot.)
- **A meeting is no longer called off while you ride toward it.** Riding straight at an announced lord could
  cancel the meeting out from under you. As long as you are actually closing the gap, it now waits for you.
  (Report: a player.)
- **File logging actually writes now.** Turning logging on could produce no file and no explanation, because
  the game does not create the Logs folder the mod writes into. The folder is created now, and if a log still
  cannot be written you are told once, on screen, with the path it tried. (Report: a player.)

### New
- **Duels, from a conversation.** You can challenge a character to a duel in chat, and a character may
  challenge you; refusing a challenge is a real, remembered act, and it cannot be farmed. Duels can be fought
  in the wilderness or at a town's gates. (Also `cr.duel_arm` / `cr.duel_town`.)
- **A family can marry a daughter against a lukewarm bond.** When a match is blessed by the family but your
  personal standing with the bride falls short, the marriage now goes ahead (the family's will prevails) and
  the bride carries a real, mendable resentment scaled to how far it fell short, instead of the match quietly
  failing for no visible reason.

### Improved
- **Captive scenes read less repetitively.** Each scene is now written through a rotating "lens", and the
  prose no longer leans on the same handful of phrasings from one scene to the next.

### For modders
- **A new door onto a character's own memory, and onto conversations.** Other mods can read what a character
  remembers, inject memories it will treat as its own, and listen for a conversation opening (with the
  character id, the save id, and the in-game day) or closing (with the whole attributed transcript, witness
  lines included). It joins the existing world-event and mission doors. Full example on the mod page and in
  `EXTENSION_API.md`.

## v1.31.7.. 2026-07-21

An important fix to what characters believe about themselves, plus the first slice of the council. Your saves
carry over untouched, but see the repair note below if your characters already picked up wrong beliefs.

### Fixed (everyone, please read)
- **Characters no longer record facts about YOU as facts about THEMSELVES.** The "discoveries" a character
  gathers are meant to be what you have learned about THEM, and that is what the encyclopedia shows. A
  character could instead write down something about you and file it in the same place. This was worse than a
  strange line on a page: those notes are read back to the character in every later conversation, under the
  heading of what you know about them, so a character could slowly build a false picture of itself out of
  facts that describe someone else. The wording that caused it is fixed, and a guard now refuses the clearest
  cases outright. (Report: a player on Nexus.)
- **Repairing a character who already picked this up.** Existing notes do not clean themselves, so until you
  remove them the character keeps being told those things. Three new console commands do it:
  `cr.discoveries [name]` lists what a character has recorded, each with an index;
  `cr.discover_forget <index> [name]` removes one; `cr.discover_forget_all [name]` clears them all. Save the
  game afterwards. There is a step-by-step guide on the mod page.

### Changed (only if you use the escape encounter)
- **Escaping now treats fighting and fleeing differently.** Putting every captor down means you take your
  equipment back off them, and you keep it. Slipping away to the edge of the scene still frees you, but your
  captor's side keeps your gear, and from then on it is held by the bandit chief of that region (or by the
  lord who held you). Beat or capture them later and it comes back to you, so nothing is ever lost for good.
  Being knocked out costs you nothing at all: you are simply still a prisoner.
- **Freeing yourself now teaches you something.** Escaping grants Roguery, and outrunning your pursuers also
  grants Athletics.
- **Characters notice how you are dressed.** Turning up in rags, or in nothing at all, no longer goes without
  comment. Ordinary clothing and armour pass without remark, as they should.

### Added (console only, groundwork)
- **The clan council**, first slice, reachable only with `cr.council [companions|family|lords]`. It seats your
  real companions, family, or clan lords, and one message puts the question to the whole table: one of them
  answers and the others react. Members can pledge to do things, those pledges can be renegotiated at the
  table, and nothing is settled until the council rises, at which point each pledge is checked against the
  world and either carried out or refused with a plain reason. A button in the window switches between one
  reply for the whole table and one reply per member. This is early work, offered for the curious.

## v1.31.6.. 2026-07-21

A marriage fix, from a report by tthheerroocckk. Your saves carry over, and a marriage the game never
performed can now be repaired from the console.

### Fixed (everyone)
- **A wedding is only remembered if the game actually held it.** Characters could narrate a complete
  marriage, and record it in their memory as something that happened, while the game never married anyone:
  the courtship read as finished, but no spouse was ever assigned. A wedding memory is now kept only when
  you are truly wed, and if the wedding did not take place you are told so in the conversation itself,
  right away, instead of discovering it much later.
- **Age no longer prevents a marriage.** A marriage is a contract between two adults, and it was being
  refused when the two were more than 25 years apart, silently, so a long courtship could never be sealed.
  That limit is gone. Age still governs the two things it should: who can bear a child, and who a noble
  house puts forward for a match of its own accord (a house offers its young, since it is seeking a long
  alliance, but you remain free to ask for an older hand yourself).

### Added: console
- **`cr.marry_check [name]`** walks every rule the game checks before a wedding and tells you which one is
  blocking, changing nothing. Use it when a marriage the conversation promised does not happen.
- **`cr.marry [name]`** now reports its failures instead of going quiet, confirms the marriage afterwards,
  and correctly records the bond, so it is a safe way to seal a wedding your campaign was already told had
  happened. To clear a memory that turned out to be false, `cr.memory [name]` lists them with an index and
  `cr.forget <index> [name]` removes one. Save the game afterwards.

## v1.31.5.. 2026-07-21

A real escape encounter for captive scenes, a fairer physical-agency system to go with it, a courier
robustness batch, and a Reply Language option. Your saves carry over untouched.

### New (adult content, Hardcore tier, while held captive)
- **Trying to escape a captor is now a real fight, not a line the AI decides for you.** Break for it and you
  face up to three bandits bare-handed, spawned close so a straight run bares your back. Knock them all down,
  or reach the edge of the scene, and you go free, though you lose your weapons and your mount. Lose the
  fight and you are back in the captor's hands, the conversation picks back up rather than ending. Only one
  escape attempt per captivity: you cannot keep trying until the dice favor you.
- **Physical attempts in a captive scene now have a real, fair outcome, not the AI's own judgment.** Resisting,
  breaking a hold, snatching something: the game itself rolls the outcome (a floor chance plus your remaining
  health), and the AI is told the result rather than deciding it. A wound the captor gives you quietly lowers
  your odds on your next attempt, so being worn down costs you something real.
- **Bandit and nemesis captors' men now act like people in the scene, not furniture.** Their witnesses carry
  their own reactions instead of the passive, generic ones used for a lord's own retinue.
- **Less description, more action.** Captive-scene narration was leaning on exhaustive description at the
  expense of what was actually happening; the action now leads. A repetitive "grabs your chin" opening move
  is gone.
- **The escape trigger is narrower.** An ordinary struggle, a headbutt, or plain defiance stays an in-character
  beat; only a genuine bid to get away starts the encounter.

### Improved: couriers
- **A failed letter now tells you why, specifically.** Dead, captured, or simply vanished, instead of one
  generic "could not be reached" line.
- **A courier stuck in one place for two days now falls back to date-based delivery** instead of losing the
  letter to a burned grace window.
- **A reply courier can no longer be routed into your captor's own party**, or chase you out to sea.
- Ration top-ups only happen when a courier's larder is truly empty.
- Console: `cr.couriers` (diagnostic), `cr.couriers_recall` (revert all to date-based delivery).

### New: Reply Language
- **A "Reply Language" option in Mod Options.** Left blank (the default), replies keep auto-matching whatever
  language you write in, unchanged. Name a language explicitly and every reply, letter, and generated memory
  is written in it regardless, which fixes reports of the AI answering in English despite the player writing
  in another language, most often on a weaker or non-default model.

### Under the hood
- Groundwork only, switched off and not yet playable: an Assembly Council foundation (roster, consent,
  resolution lifecycle) for a future multi-character gathering system.

## v1.31.4.. 2026-07-20

A fix for letter-carriers under the War Sails DLC. Your saves carry over untouched.

### Fixed (everyone, matters if you have War Sails)
- **Couriers no longer crawl when War Sails is installed.** The naval DLC swaps the game's party-speed model,
  and the mod's courier speed boost was attached only to the base model, so with War Sails a letter-carrier
  could be slowed to a crawl and its small party could starve on the road. The boost now attaches to whatever
  speed model the game is running, so couriers keep their pace. (Letters already arrived on time regardless,
  through a date-based fallback; this fixes the courier parties themselves.) Thanks to the player who spotted
  the warning in the log.

## v1.31.3.. 2026-07-20

A small fix for a quest that would not advance. Your saves carry over, and a stuck quest of this kind repairs
itself on load.

### Fixed (everyone)
- **Capturing a lord now advances an "attack" quest.** When a lord asked you to attack a rival and you took
  that rival prisoner instead of only beating them in the field, the quest failed to notice, because taking a
  captive is a stronger outcome than the defeat it was watching for. A capture now counts. (Report:
  tthheerroocckk, Nexus.)
- **A quest already stuck by this repairs itself on load.** If you are still holding the target prisoner, the
  quest is credited the next time you load the save, no need to recapture.

## v1.31.2.. 2026-07-20

A stability fix everyone needs, plus a long round of captive-scene work from a full evening of play-testing.
Your saves carry over untouched.

### Fixed (everyone)
- **Conversations could stop working entirely.** A certain kind of bandit captor (a faceless raider with no
  named leader) hit an error that broke the chat for the whole scene. Fixed.
- **A bandit chief no longer greets you by name.** Brigands live apart from lords and their registers, so a
  captor you have never spoken to should not know who you are. He now sees a captive he cannot name, and
  sizes up your bearing and gear for whether you are worth a ransom. If you TELL him your name, he remembers
  it, and a recurring one keeps it. A returning captor still knows your face and your history.
- **A recurring captor survives a save and reload.** Saving while held prisoner and reloading could turn
  your named captor into a generic bandit; the mod now restores who is holding you on load.
- **A lord no longer agrees to a surrender that never happens.** Told to surrender instead of fight, a lord
  would agree and then simply keep his army and ride off, because there is no mechanic behind it. He now
  declines or names his terms rather than promising something the game cannot carry out.

### Changed (adult content, Explicit / Hardcore tiers)
A round of work on the captive scenes, aimed at variety and immersion. All of it is behind the adult-content
setting and applies only at the Explicit or Hardcore tiers.
- **Scenes follow their own thread now.** Each scene is given a driving focus (interrogation, a war of
  wills, dread, corruption, and many more), and that focus shapes the WHOLE scene rather than fading after
  the first line. A psychological scene can stay psychological and resolve as one, or build toward the
  physical when it genuinely leads there. Nothing is on a fixed track any more, which is meant to make
  captivity worth replaying.
- **A scene is an experience that moves.** A captor who wants something and does not get it no longer loops
  the same demand forever; the scene escalates, makes good on a threat, or ends.
- **The escalation is paced.** The gravest acts are reached by degrees, held long enough to land, with your
  answer between them, rather than arriving all at once.
- **Accompanying soldiers actually take part** in a group scene, each acting and speaking in his own right,
  so you feel outnumbered rather than alone with one captor while the rest watch.
- **The captor's own body, and richer physical description.** What the aggressor feels and does with his own
  body is now part of the scene, brute force (a blow, a shove) sits alongside the rest, and intimate detail
  is written more fully.
- **The "Continue" button no longer reads as you staying silent.** It simply carries the scene onward; if
  you want to be silent in the fiction, say so with "Try something else".
- **A scene ends with you still a prisoner**, taken back to wherever your captor holds you, not untied.

### Under the hood
- Passive groundwork, switched off and not yet playable: a troop-loan ledger and a deliberate-rescue system.
- Roughly 2,600 automated tests now cover the mod and its service.

## v1.31.1.. 2026-07-19

A same-day follow-up to v1.31.0, from what players reported within hours of it going up.

### Fixed
- **A bandit chief no longer greets you by name.** A named brigand (one the mod has raised into a recurring
  nemesis) was being treated as a lord, so his prompt handed him your name, your house and your station on
  a first meeting. He now sees what a brigand can see: a captive whose name he does not know, and whose
  bearing and gear may or may not suggest someone worth ransoming. If he has held you before, he still
  knows exactly who he has. As a consequence, an abasement at a named brigand's hands no longer costs you
  renown at court, which matches how the mod already treated faceless ones: word of it does not reach the
  courts.
- **Characters no longer recite the game's plumbing.** Some models answer with a function call, `{"name":
  "change_relation", ...}`, instead of the format the mod teaches. Nothing read it, so the regard never
  moved, and nothing removed it, so the character said it out loud. Both halves are fixed: the intent is now
  honoured (still capped and rate-limited as always) and the raw text never reaches the conversation.
- **No more endless procession of lords who never speak.** If a meeting's conversation failed to start, the
  mod waited for it forever, and that wait blocked every later meeting: lord after lord would ride up to you
  and nothing would happen. The failure is now detected the moment it happens, and the conversation opens
  directly instead.
- **Riding alone now means riding alone.** A party of one counts you yourself, and that was being reported
  to characters as "a handful of companions ride with them", so they spoke, and narrated, as though you had
  an escort you did not have. Your solitude is now stated plainly instead of left unsaid, and characters are
  told not to furnish you with riders, guards or servants.
- **Characters describe eye colour correctly again.** The Empress Rhagaea's amber eyes were being called
  green, and every face in the middle of the range was off by roughly the same margin: greys were called
  deep blue, and the greens ran too late. The table was rebuilt from eighteen measured samples across the
  full range.

## v1.31.0.. 2026-07-19

The stabilization release. Six systematic audits (how NPCs perceive you, couriers, romance and
marriage, adult prompts, the council groundwork, and the test architecture itself) plus the bug reports
you sent in. Your saves carry over untouched: everything here loads an existing campaign as it stands.

### Added
- **Characters now set the scene.** The first reply of every conversation opens with a short passage of
  narration, shown in its own colour: where you are standing, the light, the sound of the room around
  you. After that opening it becomes occasional, appearing when the moment earns it rather than every
  turn. An NPC's own gestures stay woven into their speech where they belong.
- **A conversation that ends now says so.** When a character takes their leave, a line tells you plainly
  instead of the window simply going quiet.

### Fixed: letters and couriers
- **A letter could be received and never shown to you, then lost for good.** If a letter arrived while
  another was already open on screen, it was marked as read without ever being displayed and then filtered
  out forever. It now waits its turn and arrives properly.
- **The letter window could trap you with no way out but closing the game.** A failed window is now rolled
  back instead of leaving a blank panel holding your input and freezing time.
- **Couriers starved on long journeys, and the mod blamed bandits.** They carry provisions now, and you no
  longer hear tales of a waylaying that never happened.
- **Letters from the dead are no longer delivered.** A hero who died before their letter arrived no longer
  writes to you from beyond the grave, nor sends replies.
- **The 500-denar courier fee is charged only when the letter is truly on its way,** with an honest
  message when your recipient cannot be reached, instead of a cheerful "your courier departs" and no
  letter.
- **One waylaid letter no longer silences a correspondent for a fortnight.** A lost letter was still
  counted as in transit, so the sender waited on a delivery date that would never come.
- **A reply that lost its courier now still reaches you.** Orphaned reply couriers are released so the
  letter arrives by the ordinary route rather than hanging forever.
- **"Your silence weighs on me" no longer arrives the day after you replied.** Romantic follow-ups are
  measured from when a letter actually reached you, not from the day it was due.
- **A letter for someone at sea now waits for landfall** instead of counting as delivered to open water,
  and the courier resumes the chase when they make port.
- **A child-support demand can no longer be lost forever.** If the service was unreachable at the moment
  of the birth, the pension letter (and its quest) used to vanish permanently; it is now scheduled and
  retried.
- **An active romance no longer starves the rest of your mail.** Spouse, follow-up and marriage letters
  were being crowded out indefinitely; the four kinds now take turns.
- **A courier who reached a town or port is not ambushed inside it.**

### Fixed: romance, marriage and family
- **A character now recognises being married to you.** A new spouse was not pinned as committed, so the
  bond could read as though the marriage had not happened. Existing saves are healed on load.
- **A marriage no longer resets on reload.** Romantic state (status, attraction, secret-lover and consort
  flags) survives a profile refresh instead of quietly reverting.
- **A divorce demand no longer soft-locks.** A lost or failed letter re-announces and then forces a
  notice, rather than leaving the process silently stuck; persuading both spouses opens one process, not
  two; a pregnancy or captivity defers it without penalty instead of retrying forever.
- **Marriage is refused across a war or with a captive,** and whoever blesses it must be alive, free, at
  peace, and not hostile to you. Stale approvals are swept when you load.
- **A betrayal now wounds a bond rather than ending it outright,** leaving room to repair what you broke.
- **Jealousy is more discerning:** a close relative is no longer treated as a rival suitor, neglect counts
  only genuine attention, a captive sends no threats, and a marriage always registers with anyone who had
  a stake.
- **Bastard handling corrected:** a child born inside your own clan is an open child of the house rather
  than a scandal to conceal, a posthumous child of a marriage is not tagged a bastard, marrying the other
  parent legitimates a concealed child, and you can no longer blackmail yourself.
- **An adulterous conception is no longer announced to the world** as public news.

### Fixed: conversations and how characters see you
- **A ruler no longer mistakes you for their own liege.** After you swore to a kingdom, its ruler could
  be told that YOU were the one they served, and would talk accordingly: denying their own throne,
  speaking of their title as if it belonged to somebody else, and calling you "my liege" while
  correctly noting you had only just taken your oath. Reported with the Aserai.
- **Meeting a lord at sea no longer hangs on an endless loading screen.** A lord who rode out to find
  you could reach you while you were sailing, and the game asked for a conversation scene on open water,
  where there is none. Lords now wait until you are ashore, and a chase that follows you out to sea
  stands down instead. Two reports.
- **Hired swords are no longer mistaken for sworn vassals.** Under a mercenary contract your clan was
  described to that kingdom's lords as oath-bound to them. They now know the difference: you fight for
  pay, the contract ends, and no loyalty is owed.
- **Lords of your own kingdom now know you serve the same crown.** Two vassals of one realm, neither
  ruling the other, previously met with nothing said about the bond at all.
- **Mercenaries you employ know they are paid, not sworn.** When your own realm hires a clan, its lords
  were told you were their liege.
- **Lords now judge the force you actually lead.** An army you merely travel inside is not your host,
  and a captive commands nothing; both used to colour how a lord sized you up.
- **Asking a lord to hand over a prisoner no longer sets that prisoner free.** Freeing someone and
  transferring them into your custody had become the same request.
- **Couriers no longer strand themselves.** A letter is not sent when no road exists between sender and
  recipient (across water, for instance), a courier whose route becomes impossible turns back, a letter
  is never delivered at sea but waits for your first day ashore, and one sender keeps one courier in
  flight at a time.
- **A character can no longer celebrate something the game refused.** When an action is declined (a
  marriage that cannot happen, a transfer that is not allowed), the refusal now reaches the character,
  who used to carry on as though it had gone through and call you "husband" the following turn.
- **Whoever broke your captivity open is now remembered for it.** When a lord fell upon the band holding
  you and you slipped away in the confusion, the world recorded only that you "won free of captivity",
  naming nobody. Thank him for it and he would answer, quite correctly, that you had freed yourself.
- **A character can now tell when a claim about rank is wrong.** The note that places you also names who
  rules which realm, so a ruler told something that contradicts their own title has the facts in hand
  instead of inventing a way to reconcile it.
- **A tavern is now a tavern.** Conversations inside a town were described as taking place in a keep,
  because in Bannerlord a town counts as a fortification and that check came first.
- **A grievance can be mended again.** A grudge could quietly saturate to the level reserved for having
  someone's kin put to death, which no living path can ever resolve: never forgiven, never fading, every
  gesture refused. Lords already frozen this way are healed when you load your save.
- **Grudges no longer pile up forever.** Grievances held by or against someone who has died are now
  retired instead of accumulating in your save for the rest of the campaign.
- **A hidden grievance can be discovered and then mended.** Softening one now brings it into the open,
  which unblocks the whole path from hidden, to known, to settled.
- **A character no longer greets you warmly while distrusting you.** Warmth is withheld when trust has
  collapsed, and a live vendetta forces a cold greeting whatever the relationship number says.
- **Standing earned once no longer lasts forever.** Trust and respect drift slowly back toward neutral,
  and any deed renews them in full.
- **The same threat is no longer mailed to you repeatedly.** A lasting enmity rotates its letters.
- **Repeated gifts no longer stack identical lines** in what a character knows about you.
- **A completed task's reward no longer silences a warm conversation.** Turning in a quest used to
  consume the conversational goodwill timer for the next several hours.
- **The intimacy threshold is now enforced rather than merely stated.** A persistent player could argue
  past a bar that nothing checked. It follows your Relationship Pacing setting, so a Grounded campaign
  keeps exactly the behaviour it had.
- **The adult-content setting has the last word on which model is used.** With adult content off, an
  automatic switch could still move you onto the NSFW model, changing your cost and your filtering with
  no visible button. A captive scene is also no longer treated as a sexual one by default: an
  interrogation or a reckoning is not.
- **The model switch button now reads "Model" rather than "Mode",** which is all it ever changed.
- **Characters repeat themselves less** (a small generation adjustment), and a scene's narration is now
  replayed to the character as their own words, so a long scene no longer teaches them that YOU are the
  one narrating it.
- **A witness who is not in the room can no longer speak.** An invented bystander used to be shown,
  logged, and then remembered, so the invention fed itself for the rest of the conversation.
- **Saving no longer risks an error from the courier system**, and if one ever occurs the log now says
  exactly where.

### Fixed: stability
- **A daily pass could abort halfway through, silently.** Letter generation finished on a background
  thread and edited the same records the game was reading, which could throw mid-pass (couriers not sent,
  deliveries skipped that day) and carried a risk of a corrupted save. All of it is now handed back to the
  main thread, and the profile store is guarded.
- **A warning at launch when another mod has replaced the party-speed model,** which would silently
  disable the courier speed floor and leave your letters crawling.

### Changed
- Internal: the conversation's own record, and the rules that decide what each character is shown, moved
  into the tested layer. Roughly 2,500 automated tests now cover the mod and its service.
- Groundwork only, switched off and not yet playable: the council (several characters at one table) and
  the duel field.
- **A ruler no longer mistakes you for their own liege.** After you swore to a kingdom, its ruler could be
  told that YOU were the one they served. They would then talk accordingly: denying their own throne,
  speaking of their title as if it belonged to somebody else, and addressing you as "my liege" while
  correctly noting you had only just taken your oath. The two directions of the bond were reversed, so a
  king read as a vassal and a vassal as a king. Reported by JoshGray700 with the Aserai.
- **A character can now tell when a claim about rank is wrong.** The note that places you now also names who
  rules which realm, so a ruler told something that contradicts their own title has the facts in hand instead
  of inventing a way to reconcile it.
- **Whoever broke your captivity open is now remembered for it.** When a lord fell upon the band holding
  you and you slipped away in the confusion of the fight, the world recorded only that you "won free of
  captivity", naming nobody. Thank him for it and he would answer, quite correctly, that you had freed
  yourself. The chronicle now names whoever set upon your captors, and it keeps the distinction honest: it
  says you got yourself out, and that he is the reason you could. An escape or a ransom with no one else
  involved reads exactly as it always did.
- **Hired swords are no longer mistaken for sworn vassals.** While under a mercenary contract, your clan was
  described to that kingdom's lords as oath-bound to them. They now know the difference: you fight for pay,
  the contract ends, and no loyalty is owed. Expect a ruler to weigh your word accordingly.
- **Lords of your own kingdom now know you serve the same crown.** Two vassals of one realm, neither ruling
  the other, previously met with nothing said about the bond at all, so a comrade sworn to the same king
  treated you as a stranger of no particular standing.
- **Mercenaries you employ know they are paid, not sworn.** When your own realm hires a clan, its lords were
  told you were their liege. They now treat you as a paymaster whose contract runs out, which is a rather
  different conversation. A hired band serving a realm you merely belong to is likewise no longer greeted as
  a sworn comrade.

## v1.30.16.. 2026-07-15

### Fixed
- **A reasoning model's private thoughts no longer bleed into the conversation or its memories.** Some models
  (Nemotron and similar, especially with Reasoning Effort set high) write their step-by-step thinking inside the
  reply itself. The game hid the surrounding tags and showed only the bare thinking, so an NPC could end up
  "remembering" something like "The user wants me to write a memory line from their perspective..." instead of
  the actual memory. Those stray thoughts are now removed from every reply before it reaches you, and any
  memories a previous version already saved this way are cleaned up automatically the next time you load a save
  (a memory that was nothing but leaked thinking becomes a plain note that the details have faded, rather than
  vanishing or being invented).

## v1.30.15.. 2026-07-14

### Added
- **Pin a different OpenRouter provider for explicit scenes.** The same model costs different rates at different
  providers, and only some of them moderate their own output. You can now leave the ordinary roleplay pin blank,
  so OpenRouter routes freely (which is what lets a `:floor` model id find the cheapest provider), and pin a
  trusted provider only for explicit scenes. The switch is automatic: an explicit scene already changes model, it
  now changes provider with it. Leave the new field blank and nothing changes: your existing pin keeps applying
  to everything, exactly as before.
- **Write what a character BELIEVES, not just how they sound.** `character_overrides.json` takes a new
  `"conviction"` field beside `"backstory"`. The backstory colours only how an NPC SPEAKS; a conviction moves
  what they WANT and what they pursue, and it may be perfectly false: a lord convinced your clan is plotting to
  unseat him, a companion convinced she is secretly a necromancer. They act on it, and if it would ruin them
  were it known, they hide it, and the hiding becomes a motive of its own. Three limits keep it honest: it grants
  no powers (he may hoard bones, he may not raise the dead), it imports no lore from outside the world you are
  playing, and it cannot bend the world to suit it. A belief can make a character want a thing. Only deeds decide
  whether they get it.

### Fixed
- **The chat's Options button now opens the menu where you can actually see it.** If you opened the chat inside a
  scene (a settlement, a conversation, a naval deck), the menu really did open, on the campaign map hidden behind
  the scene. The log said "opened" and you saw nothing. The game answers ESC with two different mechanisms
  depending on where you are, and the mod only knew one of them.

### Changed
- **The prompt no longer assumes the world is Calradia.** A total-conversion mod that replaces the setting was
  still telling every NPC to import no lore from outside Calradia, and the chronicle still called your lords
  "lords of Calradia". Every such sentence now names the world the mod actually declares.

## v1.30.14.. 2026-07-13

### Added
- **Lords Approaching You** (Mod Options, Content): how often a lord rides out to have a word with you on the road. Never, Rarely, Occasionally (the shipped feel, and the default), or Often. Never switches it off entirely: the road is yours alone.
- **Pin an OpenRouter provider.** Several of OpenRouter's providers moderate their own output and stop generating the moment profanity appears, so the reply arrives cut mid-sentence and reads as though the mod were censoring you. You can now name the providers you trust (Mod Options, OpenRouter, "Pin Providers"), comma separated, and the request goes only to them. Leave it blank to keep OpenRouter's normal routing.
- **Contentment Pacing** (Mod Options, Companions): choose how quickly companions get over things. Forgiving, Balanced (the default), or Exacting. It changes how fast a grievance fades, never what a slight costs, and in no setting can a companion who is merely idle ever leave you.
- **Companions know how they stand beside you.** Someone riding in your party, marching in your army, or sharing a town with you now speaks as a person who is there with you, on land or at sea, rather than as someone you met once, long ago.
- **The deeds of ordinary play now count toward companion contentment**: a battle won at your side, the mood of the camp, a governorship handed over, and actually sitting down and talking with them.

### Changed
- **Companion contentment rebalanced.** A companion with nothing to do no longer slides inexorably toward the door. They settle at a resting contentment set by the standing of your clan, because serving a great house is an honour in itself: a tier-6 clan keeps its people devoted with no courting at all. Only a real, ignored grievance can still carry someone away, and it now takes seasons rather than days. You have a realm to run; you should not have to spend your evenings nursing morale.

### Fixed
- **A lord who stopped chasing you no longer stands there until he starves.** After riding out to speak with you, a lord was ordered to hold his position, and nothing ever told him to think again: he obeyed, forever. One player watched a clan party stand on a single spot until its food ran out. He is now handed back to his own AI and resumes his plans, and any party already stranded in an earlier version is freed the next time you load your save.
- **An NPC no longer reads the mod's plumbing aloud.** A weaker model sometimes writes a block of our internal format instead of using it, and it was being spoken in the NPC's own voice ("[RELATION CHANGE] delta = 2") while the regard never actually moved. Whatever the model invents, you never see it now, and the change the NPC plainly meant is applied as they said it would be.
- **Mod Options hints are no longer cut off.** Half the explanations in the options menu were too long for their control and lost their first line off the top of the screen.
- **A governor is not idle.** A companion you appointed governor, or gave a party to lead, was still counted as kicking their heels and soured week after week. Any post you hand them is employment now, and the appointment itself is a mark of trust that lifts their spirits.
- **Companions riding with you no longer act abandoned.** A crew member sailing at your side between two ports could open a conversation as if you had ignored them for a season, because the mod measured the time since your last CONVERSATION rather than since you last actually saw each other.
- **A message lost to a timeout is no longer lost.** When the AI does not answer (a provider hiccup, a timed-out request), your message is put back in the input box so you can press Send again instead of typing it all a second time.

## v1.30.13.. 2026-07-12

### Fixed
- The chat "Options" button now opens the mod options menu even while you are at sea. It was opening the menu on the land map, which sits hidden behind the naval map while you sail, so nothing appeared for players on the water (the button looked dead even though the log said it opened). It now opens the menu on whichever map the chat is actually over, and logs more detail if it still cannot.

## v1.30.12.. 2026-07-12

### Fixed
- A companion who is already present is no longer listed twice when you bring them into a conversation. Speaking with one companion and using "Bring" to add another who is already there (or auto-included) used to show their name twice in the Present line; the witness list now recognises the same person and keeps a single entry.
- The main speaker no longer answers or narrates in a present companion's voice. Building on the fix that lets you address a companion by name, the prompt now firmly forbids the main character from putting spoken words in another present character's mouth: a witness's words appear only in their own reaction, never woven into the main speaker's lines.
- More kinds of letters are dropped when the reason behind them has passed, not just divorce and war letters. A marriage proposal is dropped once you have married; a jealous threat once the jealousy has been resolved; an enemy's bribe once they are no longer hostile; a blackmail once there is no longer both a spouse and a secret lover to expose. A letter never outlives the intent that wrote it.

## v1.30.11.. 2026-07-12

### Fixed
- When you address a companion or witness by name at the end of a line, they now answer in their own voice. Speaking to someone present with a tag like "isn't that right, Tezzeret?" (even without the comma) hands the turn to that character, so they reply under their own portrait instead of the main speaker answering for them. A plain mention of someone ("what do you make of Tezzeret?") still stays with whoever you were talking to, as before.

## v1.30.10.. 2026-07-12

### Fixed
- People no longer talk about horses and roads while you are sailing the open sea. When a conversation happens aboard your ship out on the water, the character now knows they are at sea, with no roads, inns, or farms in sight, and speaks and acts accordingly.
- A mercenary contract that simply runs its course is no longer narrated as if you had betrayed an oath. When your hired service to a kingdom ends (a vanilla outcome the mod only reports, never causes), the chronicle now says your contract came to an end, instead of the harsher "you forsook them and the oath was broken" wording that was meant for a vassal who abandons their liege. A diagnostic line is also written to the log so it is clear the mod only observed the change.

### Changed
- character_overrides.json is clearer about how to add a character. The file confused some players: the example was commented out above a separate empty list, so uncommenting it left two lists and broke the file. It now has numbered steps and a single list with the example commented inside it, so you just remove the // from the example, edit it, and save.

## v1.30.9.. 2026-07-12

### Fixed
- You can free a prisoner again by telling them so. Telling a prisoner you hold that they are free to go now actually releases them. A recent change had let a defiant, unafraid captive "refuse to be swayed" and quietly blocked the release even though freeing a prisoner is the captor's choice to make, while still leaving the NPC with the memory of being let go. A captor can always free their own prisoner.
- A companion you are talking to is no longer offered, or brought in, as a witness to their own conversation. When you chat with someone travelling in your party (a married-in spouse, a sibling), the "bring companions" picker and the automatic join no longer list them, so they cannot end up witnessing, or in a round table taking a second turn as, themselves. This was behind the "there seem to be two of my wife" reports; it was never a duplicated character.
- Letters no longer outlive the intent that wrote them. A letter is re-checked against its sender's current intention the moment it would reach you, and dropped (never delivered) if that intention is gone: a spouse who reconciles and no longer wants a divorce has her pending divorce letters cleared and never sent, and a lord who makes peace before his war letter arrives has it quietly discarded on arrival. An autonomous letter that no longer reflects its sender only caused confusion.

### Added
- Freeing a prisoner now asks you to confirm first, in chat ("Set NAME free? [Y/n]"), just like paying coin or hiring, so a character can never give away a prisoner you meant to keep without your say-so. Press Enter or y to free them, n to keep them.

## v1.30.8.. 2026-07-12

### Fixed
- Authored backstories land forcefully again. A character you have written a strong persona for (in character_overrides.json), especially one that names a bold figure to model the voice on, now clearly adopts that manner instead of the flat, generic voice a recent version produced (the "it just said 'I smile with a character-like smile', no hint of personality" report). The prompt that carries your backstory to the model now commits to it fully and, for a named figure, takes that figure's full manner and force of personality while still keeping their world out of Calradia (no imported names, places, powers, or lore). A late instruction that had the model invent its own faint, subtle quirks was watering strong personas down; inventing quirks now only kicks in when the backstory itself spells out none.

### Added
- New `cr.backstory_test [name]` console command injects a strong named persona (a Kratos-style warrior) onto a chosen character at runtime, without editing character_overrides.json, so the authored-backstory path can be checked end to end in one command. It targets the named character (an exact name match wins over a partial one, so "Mina" is not swallowed by "Vamina") or the current conversation NPC, turns custom traits on for the session, and logs what it did.

### Changed
- Characters are a little more reliable about actually doing what they say when you run a weaker or smaller AI model. The rules that tell the model how to signal a real change (coin moving, a deal struck, a farewell, a discovery) are now also restated briefly at the very end of its prompt, where an instruction carries the most weight, so a model that tends to answer with dialogue only is nudged to still emit those signals and let the mechanics fire. Strong models are unaffected. This is a best-effort nudge, not a guarantee: some models follow structure better than others, and for a prose-only model a two-model "composed" mode remains the deterministic answer.

## v1.30.7.. 2026-07-11

### Fixed
- You can gift goods or gold again while holding prisoners. When you offered something and also held captives, the game first asked whether you meant "Goods or gold" or "A prisoner"; choosing "Goods or gold" then silently did nothing (the give picker never opened), so only prisoners could be handed over. The picker now opens correctly in that case (it was being requested in a way the game dropped when it followed another popup; it now waits its turn, exactly as the prisoner picker already did).

## v1.30.6.. 2026-07-11

### Fixed
- Spouses no longer divorce a happy marriage, and no longer divorce you behind your back. A whole class of "my wife is right here in my party, happy, +31 relation, yet keeps sending lonely letters and eventually a divorce" reports is addressed. The divorce system now reads your ACTUAL personal regard (the number you build through conversation), not just the vanilla clan relation, so a genuinely warm marriage never raises a divorce demand at all. The refusal countdown that turns an unanswered demand into a divorce now starts only once the demand letter has truly reached you, so a demand you never saw can never quietly escalate. A divorce letter is always delivered (you are never blindsided by an unfolding divorce), while lonely "I miss you" letters are held while their sender is standing beside you in your own party or army. And if you mend a strained marriage, its divorce grievance is cleared, so she no longer reads as warm right up until you mention the divorce and then fixates on it: a genuinely warm regard now tempers any lingering grievance instead of letting it override her fondness.
- Letters read true to how the writer actually feels. A far-off but happy spouse now writes with warmth and news instead of a generic ache; the letter's tone follows her real regard for you, not a hard-coded assumption of longing or reproach.
- A captor no longer speaks with, and remembers, someone else's life. In a captive scene the character talking to you now always uses their OWN memories and persona, instead of accidentally borrowing the profile of the last companion you happened to chat with (the "my captor thinks he's my companion Fercread" report). The speaker's memories now always follow the face and name you see, so a scene's events are also recorded on the right person rather than bleeding onto a companion's memory.

### Added
- Coin transfers and hires are now confirmed in the chat before they happen. When a character would take your coin or take service in your party for a price, a short terminal-style prompt appears in the conversation ("Give Lyfris the Knife 500 coins? [Y/n]"): press Enter or y to go through with it, n to decline, or just type your next message to move on (which declines it). This tames the case where the model re-proposed the same payment every turn and coin moved repeatedly, and a declined transfer is forgotten (never applied, and the character is told it did not happen). A character GIVING you coin, and consequences you should not be able to veto (relation changes, grudges), are never gated.

## v1.30.5.. 2026-07-11

### Added
- Freeing a captive lord is now remembered, and it makes waves. When you win the battle or siege that breaks a lord or lady out of captivity (not a bandit, and not one of your own clan), they remember it: a lasting first-person memory ("You freed me from the dungeons of X, I will not forget that you came for me") and real warmth toward you. Their close family, who owe you a debt for it, warm and remember too, and a few of the rescued lord's closest friends warm a little as well. It is a strong commitment, and it can make you an enemy: the captor whose prisoner you tore away has a chance to trace the deed back to you and nurse a grudge over it, more likely and more openly resented when you took it by a public siege than by a quiet field victory. New `cr.rescue [name]` console command simulates the whole chain on a chosen lord.
- Press Enter to send your chat message. While the quick-input line is focused, Enter (or numpad Enter) sends, just like clicking Send. The multi-line compose panel on the right still keeps Enter for new lines, so stage directions are unaffected.
- Left-click a chat portrait to open that character's roleplay dossier. A dedicated panel slides in showing, at a glance, everything that helps you play the scene: their culture, age, role and your standing (both the clan relation and your personal regard, named in plain words like "genuine regard (14)"); the game's own encyclopedia description of who they are; their authored backstory if you have written one for them in character_overrides.json; a physical description drawn from their actual appearance; the traits and kinks you have discovered about them over the campaign; and any grievances you have learned they hold. The text scrolls, and their portrait sits permanently at the bottom (your custom ChatPortraits image if there is one, otherwise their normal 3D render, always at its true aspect). The panel closes itself the moment another screen opens over the chat, so it never overlaps the encyclopedia or a menu.

### Changed
- The Options and Mode controls are now small, unobtrusive text at the bottom-left of the chat instead of two green buttons, keeping the focus on the conversation.
- The portrait was removed from the Memories panel (it now lives in the dedicated portrait dossier above), so the memories list reads cleanly on its own again.

### Fixed
- A character's 3D portrait now shows at its true wide shape (it renders roughly 2:1, wider than tall) instead of being squeezed into a square, so vanilla portraits are no longer distorted.

## v1.30.4.. 2026-07-11

### Added
- The Memories panel now shows the character's portrait. "What X remembers" is split in two: their shared history at the top, and their portrait below it. It uses the custom image you dropped in ChatPortraits for that character if there is one, otherwise their normal 3D portrait, and it is always shown at its true aspect ratio (never stretched), so a 4:5, square, or wide image all display correctly.

## v1.30.3.. 2026-07-10

### Added
- A faithful companion may confide they have come to love you. One of your own companions, someone who has followed you through war, may now ask for a private word and, if the bond has quietly grown into something more, confide genuine affection and ask to become your discreet secret lover: quiet moments together, kept hidden by their own choice to spare you and your household the jealousy it could stir. This is brotherhood or sisterhood first, never manipulation and never a bargain: they ask nothing in return but you, and a gentle refusal leaves the friendship whole. If you welcome it, the bond is set discreetly, with no gossip and no chronicle to spread it. It only ever comes from a content, deeply-bonded, orientation-compatible companion, and only when adult content is enabled. New `cr.audience lover [name]` console command opens the conversation directly for testing.

### Fixed
- People beside you no longer write you letters. "Near" now means what it should: an NPC in your party, in a party you lead, marching in your ARMY (the reported case: a lady invited into the player's army kept sending "I miss you" letters), or in the same town or castle as you, sends no letter at all. And a letter already written before you came together is not lost, only held: the courier simply departs later, once you are truly apart. This covers every letter the mod sends and heals letters already queued on existing saves.
- Received letters now say when they were written. The reading window's heading shows "From X, sent 3 days ago" (or "sent today" / "sent yesterday"), so a note penned before current events reads as the dated dispatch it is instead of a strangely out-of-touch message. Letters from older saves without a recorded writing day simply show no age line.
- Paying someone in a stage direction no longer sometimes pays YOU instead. Typing something like *he gives her 1000 denars* (you, writing about yourself in the third person) could make the model read "gives" backwards and have the other person hand you the money. The model is now taught the direction explicitly, with those exact stage-direction cases spelled out: when the player is the one paying, it is always the player's coin that moves.
- Only adults write letters. A child you once spoke with could be swept up by the correspondence triggers (a tournament congratulation, for instance); children now initiate no letters at all, and any such letter already queued on an existing save is quietly dropped. A child can still reply if you choose to write to them.
- Your secret lover now feels it when you court another. Until now the discreet bond was inert: you could flirt or even marry elsewhere and they never reacted. They do now, but in their own way, true to the secrecy: a quiet cooling and, likely, a private wounded letter asking where they truly stand. Never a public scene, never a threat to expose what you share, and no lasting grudge: the bond absorbs the hurt.
- Taking a consort now wakes your spouse at that moment. Openly binding yourself to a consort is the most public commitment short of marriage, yet your existing spouse only found out later through the daily grind; the act itself now rouses them immediately.
- An enabled same-sex romance now rouses jealousy like any other. The jealousy system judged rivalry by sex alone, so a deliberately enabled same-sex romance (through the new orientation settings) was invisible to spouses and rivals; plausibility now follows the courted person's actual orientation. The default world is unchanged.
- The jealousy test command covers every tier: `cr.jealousy [flirt|intimacy|marriage] [name]`.
- Custom chat portraits no longer show inverted colours. The engine reads a loaded image's red and blue channels swapped (warm skin rendered blue); the mod now corrects the channels itself before handing the image over, so your PNG shows in its true colours.

### Changed
- Authored personalities come through more strongly. When you give a character a backstory (in character_overrides.json) that implies a distinctive manner but you do not spell out their speech quirks by hand, the character now invents two or three subtle, fitting ones of their own (a recurring word, a favourite turn of phrase, a way of addressing people) and uses them consistently. Players found a persona like "Kratos from God of War style" needed hand-written quirks to really land; now the character comes through even without them, while still staying entirely within Calradia (style and temperament only, never imported names, places, or powers).

## v1.30.2.. 2026-07-10

### Added
- Nobles now have a first impression of you. A lord you barely know no longer opens perfectly neutral and reasonable: his own nature and your standing colour how he receives you. A cruel or dishonourable lord (low Mercy or Honor) is more likely to open arrogant, suspicious or demeaning toward an unproven, landless newcomer, and an ambitious lord can resent a new clan risen in his own faction as one more mouth at the table when the spoils are shared, all without you having to offend him first. Your standing disarms it: a high clan tier, renown or a string of fiefs makes you read as a peer rather than a target. Each lord's leaning is fixed to his character (a given lord is consistently haughty, not randomly so). Two things it deliberately does NOT do: it never touches the relation number (it colours his opening tone only, and only your deeds move his regard), and a lord in YOUR OWN CLAN is never prejudiced, he opens loyal and glad of the cause you share. An already-known friend or rival keeps the standing you have built. New `cr.impression` console command prints how a given lord would open.

## v1.30.1.. 2026-07-10

### Added
- NPCs now know the real lay of the land. Until now they had culture labels but no sense of where places actually sit, so they would invent geography (a lord calling a Vlandian border castle a watch "toward Empire territory" when its pass in fact leads to Battania). The mod now grounds them from the game's own map data: an NPC is told the encyclopedia description of the settlement they stand in, and whenever you name a place in conversation, they are given that place's real description plus which people hold it, its culture, and roughly which direction and how far it lies from where they are. This is drawn live from the world (never a hand-written lorebook that could drift), so it is always current and correct, and it costs only a couple of lines added when a place is actually mentioned. New `cr.geo` console command prints exactly what an NPC would be told.

## v1.30.0.. 2026-07-10

Custom chat portraits, letters that actually get through, changeable orientation, and two engine-level fixes straight from this week's reports.

### Added
- Custom chat portraits. Drop a PNG or JPG in `Modules/CalradiaRemembers/ModuleData/ChatPortraits/` and that hero's lines in the chat window show YOUR image instead of the 3D render: `player.png` for your own hero, or name the file after an NPC (use the `cr.portrait_id` console command in a chat to get the exact file name). The image keeps its own proportions, sized to the portrait column, and heroes without an image keep the normal 3D portrait. The folder is created for you with a README on first launch.
- Sexual orientation can now be authored per character. `character_overrides.json` accepts an `orientation` field (straight, bisexual, gay, lesbian, pansexual, asexual), and the `cr.orientation` console command changes it live, so a female-female romance is now possible with an NPC written that way instead of her insisting she is only into men.
- Letters can now run long: the message box accepts up to 8000 characters (it was cutting off far earlier).

### Fixed
- Couriers are no longer robbed on every single trip. A letter's journey is now a real gauntlet instead of a rigged one: each bandit party near the road has a one-in-three chance of taking hostile interest, and even then the chase catches the courier less than a third of the time. And when a letter IS lost, you are told and offered to send it again (the text is kept, not retyped).
- Typing a long message no longer tanks the frame rate. The engine recomputes the text box's visible window every frame by shaving the whole text one character at a time, which at a few thousand characters meant millions of wasted operations per frame and a stuttering map behind the chat. The mod now does that same computation the fast way for long text (short text and every vanilla box keep the engine's own path). The letter editor had the same lag and gets the same cure.
- A written backstory now actually shapes how the NPC speaks. Voice and mannerisms from the backstory (speech quirks, recurring words, a distinctive manner) are adopted as HOW the character talks, while staying inside Calradia: no imported names, places or powers from outside the world, and no change to WHAT the character decides.
- The chat window no longer trims older portraits out of long conversations; every line keeps its speaker's face for the whole session (measured safe on the mission screen where the chat lives).
- Your own spouse no longer demands child support by letter. Once you are married to the mother of your child, the child is yours together and kept in your household, not a debt collected by courier: she sends no support demand (and a mother riding in your own party sends no birth letters at all, since you were there). Existing saves are healed: any such letter already queued from your spouse is dropped on the next day's tick, which also ends the divorce-for-unpaid-support spiral it could cause.
- A prisoner can no longer refuse you a private word. When you ask to speak privately with someone you hold captive, the others are simply sent out: a prisoner does not get a say over who stands in the room, whether you ask through the button or in your own words.

## v1.29.2.. 2026-07-09

### Fixed
- Reloading a save no longer plays a "quest failed" then "quest accepted" chime with two banners for each Calradia Remembers quest. The engine cancels, on every load, any quest not tied to one of its own live "issues"; the mod's quests are not, so each was cancelled and instantly re-created by the save-heal (nothing was ever lost, but it was noisy). The mod's journal entries now mark themselves as the engine's own "special quest" kind, the supported path for issue-less custom quests to survive a reload, so they persist silently. Also fixes the same silent reload-cancel for companion-errand and pledge journal entries.

## v1.29.1.. 2026-07-09

### Fixed
- The dead no longer write letters. A letter queued before its sender died (a wife lost in childbirth still "demanding" child support was the reported case) is now dropped instead of dispatched: no courier departs for a dead sender, one already on the road turns back, and stuck letters in existing saves clear on the next day's tick.
- A lord's wife no longer sends jealous threats to a male player who simply did her husband a service. Jealousy now checks the rival is a plausible one: a same-sex pair is never read as courtship (matching the mod's romance and marriage model), flirtation stamped on a quest hand-over is treated as the gratitude it is, and spoken flirtation alone only reads as courtship once it forms a repeated pattern. Real intimacy between a plausible pair still counts immediately.

## v1.29.0.. 2026-07-08

### Added
- Personal supply quests. A lord can now ask you to bring a supply of goods: horses for their cavalry, or livestock or grain to feed their host. Bring the goods and hand them over through the coin-purse bag; the game counts what you give and closes the task the moment you deliver enough, paying its reward. Verified by a real hand-over (never the model's word), and always on offer, so there is always something practical to do for a lord.
- Your appearance, editable on your own Encyclopedia page. Your hero page now shows "How others see you", the description every NPC reads of you (your build, age and colouring, plus any note you add), with an "Add more info" button that opens an editor to write a detail others can see (a scar, a limp, a certain air). A live preview shows the final line as you type.

## v1.28.1.. 2026-07-08

### Fixed
- Child support requests now actually close when you pay them. The support quest ("help support my child") was marked done and told you to return for a reward, but never completed no matter how much coin you gave, leaving it stuck in your log. Since you pay the support straight to the person who asked, it now settles the instant you pay, and she remembers the support with a little goodwill.

## v1.28.0.. 2026-07-08

The bandit boss who took you prisoner can now be HUNTED DOWN and finished, and the chat's offer controls are tidied into one.

### New
- Hunt down the nemesis who took you. Until now the recurring bandit boss only crossed your path if he captured you again. Now you can go after him: he keeps no army standing on the map (so no random lord can wipe him out before you get your reckoning), but when you go bandit-clearing in his territory, a band you engage has a rising chance to turn out to be led by HIM. Ride into his region, clear a few empty bands, and eventually you run into the man himself, to kill or capture as you please. And he only ever appears in a fight YOU chose to start, so his end is always in your hands. Kill him and no new boss of that same bandit type rises for a good year or three, so the same looters take you as a nameless captor for a while before another named nemesis is born. (A nemesis is still only ever created by a real capture, so simply beating bandits never conjures one.)

### Fixed
- The offer buttons no longer overlap in conversation. The Sell Prisoner button could sit on top of the Mode button. Offering is now one control: click the coin-purse and, when you hold captives, choose whether you are offering goods or gold, or a prisoner. One button fewer, and nothing overlaps at any UI scale.

## v1.27.0.. 2026-07-08

Prisoners become a currency you can trade in conversation, a task an NPC offers now always lands as a real quest, and a fistful of fixes straight from this week's reports.

### New
- Trade prisoners in conversation, both ways, for gold. A lord may offer to BUY a hero captive you are dragging along (a new Sell Prisoner button in the chat opens a picker), or SELL you one his clan holds. You haggle the price in character, but the figure is held to reason: it is anchored to the captive's real ransom worth (a hard bargain never goes below a floor, a silver tongue never talks a lord into absurdity). This opens bounty hunting: a lord at war can ask you to bring him an enemy in chains for a price, and a captive you sell to his enemy remembers being handed over.
- A task an NPC offers is now always a real, doable quest. Before the conversation, the mod checks the living world for the tasks THIS person can truly set you (a real bandit lair near his lands, a real enemy lord to defeat when his realm is at war, a letter to carry to a real kinsman, and so on) and teaches the NPC only those, with the real names. So when a lord gives you a task, it lands in your journal with a target you can actually reach, and when you ask for work, there is always something real to be done. No more missions spoken aloud that never appear.

### Fixed
- A bandit boss who has met you before now knows your name. On a first capture a brigand does not know who you are (you tell him in the scene, as before), but a recurring nemesis who already holds memories of you greets you by name instead of pretending you are a stranger.
- Bandit bosses are no longer all named the same. A minting quirk made the first named bandit of every campaign come out identical ("Wick the Cutthroat"); their names are now truly rolled from their culture's pool.
- Replies cut short by a provider's content filter are handled and explained. Some model hosts (notably certain DeepSeek deployments) stop a reply mid-sentence when their own moderation trips, which read as the mod censoring text. The mod now automatically re-rolls such a cut reply once (a fresh attempt usually gets through), and if it is cut again, tells you plainly that the provider's filter, not the mod, did it. Switching model or provider remains the reliable fix for adult play.

## v1.26.0.. 2026-07-07

NPCs finally SEE you, captivity alongside your companions grows real teeth, and a save/load quest bug reported this week is fixed with a healing pass that restores quests older versions wrongly cancelled.

### New
- NPCs see what you actually look like. Every character you speak to now perceives your real appearance, read live from your actual character: gender, age, build, hair colour and skin tone (resolved from the game's own face data), plus eye colour. No more NPCs inventing a description that contradicts the person standing in front of them. You can add your own touch with the console: cr.appearance <text> (for example "moves with a slight limp, a scar across the left brow") and every NPC will weave it in; cr.appearance alone shows what they see, cr.appearance clear removes your text. Each NPC also keeps their OWN appearance straight, and even a bandit captor who does not know your name still sees the frame and bearing of the prisoner in front of him.
- Captors may turn on your companions (Hardcore). When you are taken prisoner together with companions, the captor may drag ONE OF THEM before you instead: tormented in person, given to the soldiers, or, cruellest of all, your own hands forced to it. And it leaves marks: your companion's trust in you is scarred (deeper if you were made to do it), the atrocity enters the world's memory and your chronicle, and against a NAMED captor (a lord, or your nemesis) the companion swears a revenge of their own, joining your vendetta.

### Fixed
- Quests no longer auto-fail when you load a save. Reported this week: loading a save fired the "quest cancelled" banner on a perfectly valid quest, even one already completed and awaiting its reward. Root cause found (the journal entry silently failed to match its quest across a reload or a version upgrade, and a safety pass then wrongly cancelled it) and fixed for good: the load pass can no longer cancel any quest it cannot PROVE is finished, matching now uses a stable key that survives reloads and upgrades, and a healing pass RESTORES quests that an earlier version wrongly cancelled, so if the task is still alive in the giver's mind, it reappears in your journal, including its "Done, return for your reward" state.

## v1.25.0.. 2026-07-07

The bandit boss who takes you prisoner grows into a real rival: he now sounds like the brute he is, remembers when you slip his grasp, and, once you have beaten and captured him, can be spared and turned to your own service. Plus a handful of fixes straight from your reports.

### New
- Spare a beaten nemesis and turn him to your side. Once you hold the recurring bandit boss prisoner and speak with him, you may offer to spare his life and take him into your service. Whether he bends is a matter of the man: one you have truly cowed and who has come to respect you may swear himself to you; a brave or honour-bound one would sooner die free than serve his captor; and one whose hatred runs too deep refuses unless your dominance over him is utter. Turn him and he forsakes his band, rides at your side, and the long vendetta between you is closed for good.
- An Options button, right in the chat. It opens the game's own options (and the mod's MCM settings) over the conversation, exactly as pressing Escape would, so you can switch models, enable logging, or change a setting without ever ending the chat. The model-mode toggle now sits down in that same button row.

### Fixed
- The recurring bandit boss now talks like the brigand he is. He was writing with the eloquence and cold calculation of a lord. He keeps his name and his memory of you, but now sounds like the coarse, brutal cutthroat he is, not a nobleman on a dais. This also curbs the odd, too-soft turns some of you saw; a brute menaces, he does not reassure.
- Bandit leaders always show a chat portrait now. A freshly-named bandit boss could appear with no portrait on the very first meeting (his face was not yet ready the instant he was created), which ran his lines under yours. He now falls back to his band's own face, so every line is clearly his and reads as its own turn.
- The bandit boss remembers your escape. Slipping free of a nemesis now leaves a mark in his memory ("they slipped my grasp"), so the next time your roads cross he knows you got away from him.

## v1.24.0.. 2026-07-07

Straight from your comments. Lords who ride up to you now actually talk, the tasks NPCs give you can actually be finished, and you can glance back at what someone remembers of you without ever leaving the chat.

### Fixed
- Lords who ride out to meet you now truly meet you. Several of you reported a lord galloping toward you on the map after some deed, a notice popping up.. and then nothing, the meeting never opening. Now, the moment they reach you, the conversation opens on its own in a proper scene, and they speak first: they raise why they sought you out, be it a challenge for crossing their lands or word of what you have just done. It can no longer be missed by walking away, and it cannot come to nothing.
- Tasks you are given can actually be completed now. Several problems were tangled together here. A character could describe a task the game could never honour, asking you to "bring back proof" (a blade, a banner, a captive) that exists as no real item. That is gone: a task is proven by the deed itself, which the game verifies from real events, and the giver now says so plainly and tells you where to go instead of inventing an errand you cannot finish.
- Bandit tasks are far easier to finish honestly. Clearing the enemy anywhere in the region around the named town now counts, rather than demanding an exact, fragile match on a single settlement name. A "clear the hideout" task now names a real, existing lair and points you toward it ("the lair lies to the north of.."), and felling it is recognised as your deed. Such a task is never given at all when no real lair sits nearby.
- Tasks now drop a marker on the map for their target, the settlement or the person, so you are no longer left guessing where to ride.

### New
- A "Shared memories" panel, right in the chat. While you speak with someone you have met before, a Memories button opens a side panel that lays out, plainly, everything they remember of you.. each entry dated, oldest first, like a shared journal. Read-only for now; editing your history from the panel will follow.

## v1.23.0.. 2026-07-05

A big update. Captivity becomes a scene you are truly in, the bandit chief who takes you gains a name and a memory, and the graphics crash a few of you ran into is gone at the root.

### Fixed
- The graphics crash ("rglGPU_device_d3d11::create_render_texture: Not enough memory resources") that could strike during a captivity scene is gone. It hit hardest for players running the War Sails expansion: the chat window sat as an overlay over the live campaign map, and that map kept filling video memory frame after frame, never letting it go, until a long scene or a second and third one ran the card dry. Rather than paper over it, captivity scenes no longer play over the map at all (see New below), which removes the cause entirely.

### New
- Captivity is now a scene you stand inside. When your captor comes for you, you are brought before them in a proper 3D scene, the two of you face to face, instead of a window floating over the world map. It reads the way a real conversation does, and being a scene of its own is exactly what makes the crash above impossible.
- The Nemesis, first step. A bandit chief who takes you prisoner is no longer a faceless nobody you forget the moment you are free. The one who holds you becomes a named, persistent figure the world remembers, who keeps his own account of what passed between you, and whom your road can cross again. This is the opening of a larger rivalry that will grow in later updates.

## v1.22.2.. 2026-07-04

A crash fix, straight from a player's report.

### Fixed
- Fixed a crash that happened when you were travelling inside another lord's army and that lord, having heard
  of your deeds, decided to ride toward you. The mod no longer tries to intercept a player who is already with
  the pursuer, and abandons any chase the moment you join their army. If this bug hit you, your save is not
  lost: it was never damaged, only caught in a re-crash loop, so simply updating to this version and reloading
  will let it open and play on. As an extra safeguard, on every load the mod now stands down any pursuer left
  bearing on a player who is inside an army.

## v1.22.1.. 2026-07-04

A patch built from your reports, with a few new threads woven in while I was there.

### Fixed (from your reports)
- Marriage through conversation now works when you have built a strong bond the ordinary way. The wedding
  used to check only the mod's own private "regard" score, so a lord or lady would agree in words yet never
  actually wed you. It now accepts a strong vanilla relationship too, and an NPC will no longer promise a
  marriage it cannot actually grant.
- An NPC no longer mis-speaks its own gender. A follower whose stored profile had gone stale could refer to
  themselves with the wrong pronouns; their gender is now re-read live every conversation.
- After you marry an NPC, they now recognise that you are their spouse. They used to treat you and the person
  they married as two different people.

### New
- The divorce story is now complete. You can end your own marriage in conversation; a deeply unhappy spouse
  can ask you to end it; and a spouse you refuse for too long will, in time, leave you of their own accord
  unless you truly mend things between you.
- You can now settle a grievance with a real deed in the wronged NPC's favour, not only with words or a gift.
- Your chronicle now records notable conquests of the heart and the bedchamber, in a period register, when
  adult content is enabled.

### Improved
- Long, multi-part intimate scenes no longer echo the same phrase or image from an earlier beat.

## v1.22.0.. 2026-07-04

The big one: NPCs now hold KINDNESSES as well as grudges, you can reconcile a grievance, and the chronicle
of your life reads like a real chronicle. A pass over how NPCs feel about you tied it all together.

### New: Kindnesses (Appreciations), the positive mirror of grudges
- An NPC now remembers the specific KIND things you did for them, told as a story, separate from their raw
  regard: a gift, gold, a freedom you gave them, a task done on their behalf. A new "Kindnesses" section sits
  beside "Grievances" on their encyclopedia page.
- A fresh grudge SOURS old kindness: if you wound someone, the warmth they remembered dims in proportion to
  what you now owe them. The two histories, good and ill, are told side by side.

### New: reconcile a grievance
- You can now ask an NPC what it would take to make things right. They name their price in character, and it
  is then written into their "Grievances" entry, so you know how to mend it. Follow through, an apology, a
  worthy gift, or a real deed in their favour, and the grievance eases or is laid to rest. Some wounds, a
  vendetta, cannot be mended, and they will tell you so.

### New: a chronicle worth reading
- The chronicle of your deeds now records a whole LIFE, not just battles: the births of your children, your
  own death and the passing of your line to your heir, oaths of fealty sworn and crowns won, fiefs granted,
  tournament prizes, lords you captured, your own captivity and escape, peace made over an old grievance,
  and pledges kept at your word. Entries are grouped into year chapters, and the empty page now tells you
  what will one day fill it.

### Changed: how NPCs feel about you, made coherent
- One consistent vocabulary now describes an NPC's regard everywhere (the encyclopedia and the way they
  speak no longer disagree about what "wary" means).
- A strong grievance now colours their tone: a deep, concealed grudge keeps them cold beneath any courtesy,
  rather than letting warmth and resentment contradict each other.
- Personal deeds (mending a grudge, a jealous wound, the relief of a family glad a marriage ended) now move
  the PERSONAL regard you actually see, while public and political events move the clan standing.
- An NPC's bearing toward you (whether they trust, respect, or fear you) is now shown in words on their
  encyclopedia page, once you have met them.
- Ending a marriage now wounds the set-aside spouse's immediate family and their closest friends, rather
  than an entire clan.

## v1.21.1.. 2026-07-04

A hotfix for a crash reported right after v1.21.0.

### Fixed
- **Conversations no longer crash on some saves.** On certain game builds a conversation could close with a
  "method not found" error the moment you spoke to anyone, because the new divorce feature read a game property
  (a hero's origin clan) that build does not expose. The mod no longer hard-depends on it: when that property is
  present it is used as before, and when it is absent (an older build, or a beta that renamed it) divorce stays
  fully functional, deciding which spouse relocates by a clan-leader heuristic instead. New campaigns were
  unaffected; the crash only hit older saves where a married couple already existed.
- **A single faulty conversation option can no longer close the whole chat.** As a safety net beyond the fix
  above, if any one conversation action ever fails to evaluate, it is now quietly left out for that turn instead
  of bringing down the entire chat window.

## v1.21.0.. 2026-07-03

Two big additions this release: NPCs now carry resolvable GRUDGES you can see and mend, and you can persuade a
lord or lady to end a marriage. Plus a batch of fixes from your bug reports (thank you, keep them coming).

### New: Grudges, a resolvable memory of what was done
- NPCs now hold event-anchored grudges, distinct from their overall regard: someone can like you and still nurse
  a specific wound (reconcilable), while someone who dislikes you AND holds a grudge is far harder to win back.
  How rigid a grudge is depends on how they already regard you, and it softens over time (unless the wound is a
  vendetta, or the holder is the kind who forgets nothing).
- Grudges are born from the things that already mattered: jealousy, a warned or abetted plot, a task you left
  unfinished, a marriage you broke, a bastard's claim. They colour how the NPC speaks to you.
- A new "Grievances" section on an NPC's encyclopedia page shows the grudges you have actually LEARNED of (a
  concealed grudge stays hidden until it surfaces), in plain words with a sense of how deep it runs.
- Console: `cr.grudges [name]` (dev view of every grudge), `cr.grudge_test [name]` (plant a test grudge).
- Existing saves are migrated silently: old jealousy/quest/divorce grievances already in your save become
  grudges on load, with no double-counting and nothing lost.

### New: Divorce (NPC case)
- You can now persuade a lord or lady to end their marriage. How willing they are slides with how unhappy the
  marriage is: a barely-unhappy spouse needs a deep bond with you, a wretched one needs only the opening, and a
  spouse who is courting you leaves for you outright.
- The consequences are signed by the parting: a spouse set aside against their will turns on you and the one who
  left them, while a divorce both sides wanted stings far less. (A fuller family-by-family reckoning comes with
  the grudge system above.)

### Fixed (from your reports)
- A gang leader (or a town notable, or a wanderer) no longer speaks like a lord on a throne. Each now carries
  themselves in a voice fit to their station, editable in ModuleData for the curious.
- In a keep with two people present, the NPC no longer answers in the wrong person's voice when you merely
  MENTION the other by name. Only a real address ("Martira, ...") routes the turn to them now.
- NPCs no longer guess a MENTIONED person's gender from an ambiguous name and "correct" you: they are now told
  each named person's actual gender and instructed never to second-guess it.
- The "A word with you..." option now appears when you speak with a clan member you assigned to run an alley.

### Changed
- In-chat: the Encyclopedia button moved down beside the text box, and the model-mode toggle moved to the far
  left, clearing the header.

## v1.20.9.. 2026-07-03

A small tool for the curious, to help validate the courier fix.

### New (developer/testing)
- **`cr.courier_test <settlement>`** console command: spawns a test courier from the named town or castle's
  gate, riding to you with a stamped letter. If it reaches you, the courier cleared the walls; if it sits frozen
  inside, that settlement can be reported. Handy for checking towns, port towns, and castles across the map.

> On the couriers you cannot see: this is by design, not a bug. To keep saves safe, courier parties are removed
> from the world before every save and are not respawned on load, so a letter in transit across a save then
> arrives by an invisible fallback. And "waylaid" means the courier was genuinely caught and killed on the road,
> which means it did leave the settlement, exactly what we want.

## v1.20.8.. 2026-07-03

One fix, straight from a player's report.

### Fixed
- **A single meaningful exchange is no longer forgotten.** If you said one substantial thing to an NPC and left,
  the conversation could leave no memory at all (the safety net that records a memory when the AI does not
  required at least two exchanges), so the NPC greeted you two days later as if nothing had happened. Now one
  exchange with real substance is remembered too; a drive-by greeting still leaves no clutter.

> Diagnosis tip from the same report: memories live in your save file, one profile per hero, and you can inspect
> what an NPC remembers at any time with the console command `cr.memory <name>`. And beware, Calradia sometimes
> spawns TWO wanderers with the same name; if an NPC seems amnesiac, make sure you are not talking to the twin.

## v1.20.7.. 2026-07-03

One fix, straight from a player's bug report.

### Fixed
- **Couriers no longer spawn stuck inside a castle or town.** A courier dispatched by someone living in a
  settlement (a governor wife writing from her castle) used to appear at the settlement's center, trapped
  inside the walls, where it starved until the letter was reported waylaid. Couriers now set out from the
  settlement's gate, on the road, exactly where a rider would. The same guard applies to couriers you hire
  in a tavern.

## v1.20.6.. 2026-07-03

A lord's word now costs him something, and letters know how far away you really are.

### New
- **Pledges with teeth.** After you turn a lord against a rival, you can now get them to PLEDGE to move against
  that rival, and it is a real deed, not talk: the pledge sets a genuine political scheme in motion (they begin
  to plot, to slander or undermine), you can help it along, and a "Pledge: [lord]" entry tracks it in your quest
  journal. If they follow through, their bond with you deepens and word spreads that they kept their word; if
  they let it lapse, their trust in you craters and word spreads that they promised and did nothing. Moving
  against a ruler is never offered this way, that is a war, and war is a leader's own decision.

### Fixed
- **No more blank letters.** A letter whose text failed to generate is no longer delivered as an empty page.
- **A spouse in the same castle no longer laments your absence.** Whether your wife writes that you are away is
  now a matter of real distance, measured as a courier's journey in days. She does not fret when you have merely
  stepped outside the walls, only when you are genuinely far across the map, and even then only sometimes.
- **Lords no longer make hollow promises.** Asked to do something the game cannot honor (kill a person, declare a
  war, dissolve a marriage), a lord now declines or names hard conditions in character, instead of vaguely
  agreeing ("soon, soon") while nothing ever happens.

## v1.20.5.. 2026-07-02

Conversations that reach into the world, the first step of a larger effort to give what you say real weight.

### New
- **Four new things you can ask a lord, and they actually happen.** In conversation you can now ask a lord to
  lend you their voice at court (their influence passes to you), to spare you some soldiers for the road, to
  turn against a third party you both have cause to distrust, or to free a prisoner they hold at your word.
  Each is a real deed with real consequences (their influence, their troops, their standing with the person
  they turn on, a captive actually freed), granted only when it makes sense for that lord and their character,
  and refused plainly when it does not. A calculating steward will still weigh what he gains before he agrees.

### Fixed
- **Lords no longer mistake a plain request for a threat.** They now take what you actually say at face value:
  a request, an offer, or a proposal is not an attack. Approaching a settlement's owner about a deal no longer
  makes him answer as though you rode up to conquer his home.

## v1.20.4.. 2026-07-02

See who you sent where, and letters that better know where everyone is.

### New
- **Companion errands now show in your quest journal.** When you send a companion on an errand (news, spying,
  stealing, bartering, an envoy), an entry appears in your journal, [b]Errand: [name][/b] with their portrait,
  and updates as they ride out, reach the town, and head back, so you can always see who you sent where. It
  closes when they return or you recall them. A companion away on an errand is the only one who gets an entry.

### Fixed
- **Letters are better grounded in where everyone actually is.** A settled correspondent (a spouse at her own
  hall) now writes from her real place instead of from nowhere, so the location detail you may not have seen
  before now shows up. And a letter no longer tells you to "come home" while you stand in the same hall, or
  speaks as though you are somewhere you are not: the writer now knows where you are and whether you are
  together.

## v1.20.3.. 2026-07-02

Two fixes from a player report about a bastard child and letters that did not fit the situation.

### Fixed
- **A mother who cares for you no longer blackmails you over a bastard.** If she holds your child out of wedlock
  but her regard for you is warm (and, in the reported case, you had agreed to wed), she now writes with longing,
  not extortion, whatever her scheming nature. A woman who loves you, and whose child your marriage would
  legitimize, has no secret to sell.
- **People in your own party no longer send you courier letters as if you were apart.** A companion travelling at
  your side would write letters that ignored their real closeness, in one case a mother complaining you were
  "never there for the children" while she rode in your party. Anyone in your party is present now, so they speak
  in person rather than by courier. A companion away on an errand still writes, as they should.

## v1.20.2.. 2026-07-02

Look people up without leaving the conversation, and a fix for the chat holding the game hostage.

### New
- **Open the encyclopedia from a conversation.** [b]Right-click any portrait[/b] in the chat, the person you
  are speaking with or anyone else present, to open their encyclopedia page. There is also an [b]Encyclopedia[/b]
  button in the chat's title bar for the person you are talking to. The chat slides aside so you can read and
  browse freely, then slides back when you close it. Handy for placing who a lord is talking about mid-scene.

### Fixed
- **The chat no longer blocks the game's pause menu.** If you alt-tabbed to the desktop and back, the pause menu
  appeared but the chat kept exclusive focus, so you could not click [b]Return to the Game[/b]. The chat now
  steps aside for any game screen that opens over it (the pause menu, the encyclopedia) and takes focus back
  when that screen closes.

## v1.20.1.. 2026-07-02

Two things players told us about: an NPC who would not let go of a heavy opener, and letters that wrote from the wrong place.

### Fixed
- **NPCs no longer fixate on how you opened the conversation.** The menu line that starts a chat was being read
  by the AI as your first, weighty statement, so it would insist you were hiding something (or that a plain
  question "seemed important") even when you only asked about the weather. The opener is now a neutral "A word
  with you.", and it is no longer fed to the AI as a real line: the character simply greets you and answers what
  you actually say.
- **Letters are written from where the sender actually is.** A correspondent who had marched off to war would
  still pen letters as if sitting in the town where you last met. Now the letter is grounded in the writer's
  real whereabouts (a town by name, or on campaign in the field), and a place from an old memory is treated as
  the past ("our talk in Ortysia"), never as where they are now. When their location cannot be read, the letter
  simply keeps its setting unstated rather than guess.

### Changed
- The "[b]<[/b]" review-panel button beside the chat input is a touch slimmer.

## v1.20.0.. 2026-07-01

Bring your own models.. one voice for the road and another for the dark.. and no two captive scenes told the same way.

### New
- **Two models, one for roleplay and one for the explicit.** Alongside your main model there is now an optional
  [b]NSFW model[/b] field (Mod Options, LLM Provider). Captive and other explicit scenes switch to it
  automatically; ordinary conversations stay on your roleplay model. A small [b]Mode: Roleplay / Mode: NSFW[/b]
  button in the chat flips it on the fly, for a consensual scene that turns, without leaving the conversation.
  Works with whichever provider you use. Leave the field empty and one model serves everything, exactly as before.
- **Separate Reasoning Effort per mode.** [b]Reasoning Effort (roleplay)[/b] and [b]Reasoning Effort (NSFW)[/b]:
  keep roleplay high for finer dialogue, drop NSFW to Low or Off for fewer moralizing refusals and faster scenes.
  The NSFW setting defaults to "Same as roleplay", so nothing changes until you touch it.
- **Compact prompt for small models.** A new toggle that trims the system prompt to fit a small, short-context
  local model (roughly 4k to 8k, an LM Studio or Ollama model). Turn it on if the AI stays silent or you see
  "(My words escape me...)".. the full prompt was overflowing your model's context. Console: [b]cr.prompt_size[/b].
- **Captive scenes vary far more.** Three changes make the same captor stop retelling the same scene: their
  intent is now drawn from their nature rather than fixed (so visits differ), the pool of "what drives them
  tonight" nearly doubled and gained new registers (psychological manipulation, corruption, theatre for the
  onlookers, a slow game of hope and denial), and the dramatic [b]structure[/b] itself now varies.. a slow
  build-up, the classic arc, or a sudden brutal assault, chosen by who holds you. Two new beats round it out: a
  rising tension before the act, and an aftermath after it, instead of cutting straight from the peak to the cell.

### Changed
- **Mod Options reorganized.** LLM settings come first now (the shared provider group, then one section per
  provider: OpenRouter, NanoGPT, OpenAI-Compatible), then gameplay, then diagnostics. The model fields are shared
  across providers.
- **Test Connection checks every model you configured.** It now sends a one-token test to both the roleplay model
  and, if set, the NSFW model, so a mistyped id fails on the button instead of mid-scene.

### Fixed
- **Reasoning models (MiMo, GLM and the like) no longer stall on Continue.** They could spend their whole reply
  budget "thinking" and return nothing; the mod now retries with a larger budget, and the Continue nudge is
  lighter so they stop over-deliberating.
- **A quest whose giver dies now closes** instead of lingering in your journal forever. No penalty.. their death
  is not your doing.
- **Letters reply in the language you wrote in.** A letter penned in French no longer comes back in English.
- **Save stability.** Two subsystems (jealousy, marriage approval) moved off a text-based save format that could
  corrupt a long campaign's save.

### ⚠️ One-time step for NanoGPT / OpenAI-Compatible users
- The per-provider model fields were merged into one shared [b]Model (roleplay)[/b] field. If you run NanoGPT or a
  local OpenAI-compatible server, [b]re-enter your model id once[/b] in the LLM Provider section after updating.
  OpenRouter users are unaffected.

## v1.19.0.. 2026-07-01

Companions who have bled for you long enough may ask to lay down the sword.. and when a companion wants a word,
you can now truly talk it through.

### New
- **Companion war-weariness & honorable retirement.** A companion who fights at your side for years accumulates
  the toll of war.. wounds and captures wear them down (a brave, hardened soul bears far more than a cautious
  one, and rest slowly eases it). When one is truly spent, they ask to retire: a **landless** companion leaves
  your service with a warm farewell; a **landed** one (a governor, or a companion leading their own party) asks
  to **step back from the field** and tend their holdings while remaining of your clan. In play it rewards
  expansion.. giving a veteran a fief keeps them. Drag a companion who has stepped back to war again and their
  granted rest curdles into real resentment. Rides under the [b]Companion contentment[/b] toggle (Mod Options).
- **A companion asking for a word now opens a real conversation.** When a companion is unhappy in your service,
  or war-weary and wishing to retire, the pop-up gives you two roads: [b]Let us speak of it…[/b] opens an actual
  conversation.. they voice it in their own words and you answer in yours, winning them back (or granting their
  leave) by what you say.. or [b]I've no time for this[/b], which turns them away and sours them. The old
  one-click "reassure" is gone; you talk it out now.
- **A composer/review panel for long messages.** A "[b]<[/b]" button beside the chat input opens a panel on the
  left showing your whole message [b]wrapped and scrollable[/b], so a verbose player can read the full thing back
  before sending (Bannerlord's text box is single-line only, so this is a read-along view; you still type and
  send in the chat box on the right). Close it with [b]Close Preview[/b].

### Fixed
- The chat **input box and Send button no longer appear before the character has given their opening line**..
  they stay hidden through the "(considering…)" moment and show once the introduction is delivered.
- The "**scroll down to read more…**" hint no longer flashes up when a message actually fits the window; it
  appears only when the text genuinely overflows and you are scrolled up.

## v1.18.0.. 2026-06-30

The town ambush leaves "experimental" behind and becomes a full feature.. and the people you fight beside, and
the ones held with you, feel like real people now.

### Changed
- **The Town Ambush is now a full feature, on by default.** The experimental toggle is gone. A lord whose hatred
  has turned murderous can hire blades to jump you as you walk into a town.. lose the street fight and his band
  convoys you to him for a reckoning, after which he keeps visiting his prisoner. It only ever happens if you make
  an enemy that bitter, so most playthroughs will never trip it.. but when you have earned someone's murderous
  hatred, the streets are no longer safe.

### New
- **Ambushes scale to your retinue, and your companions share your fate.** The hired band is led by an elite of
  the paymaster's own culture (a real soldier.. the tell that a lord, not a gang, is behind it), and its numbers
  grow with how many stand at your side: a lone traveller faces a handful, a full retinue faces a small warband.
  Any companions with you are cut down and **taken prisoner alongside you**, not left behind.
- **The gate guards flee.** When the knives come out at the gate, the town guards scatter rather than wade into a
  pitched ambush.. no bystanders caught in the crossfire, no odd interference with the fight.
- **A companion held captive with you is a real presence in the scene.** In a captivity scene where the captor
  works on one of your companions, that companion now has their **own portrait and their own voice**.. they cry
  out, plead, curse, or defy on their own.. and they are always referred to with **their own gender**, never
  confused with yours. No longer a nameless figure the story only talks about.

### Notes
- **Better "Continue" beats in captive scenes.** Pressing Continue now pushes the scene to move forward with fresh
  description and stop repeating earlier lines and images.
- **A small chat fix:** the "scroll down to read more" hint no longer flashes up the instant a reply is a line
  longer than the window.. it shows only when there is a real block of text below the fold.

## v1.17.5.. 2026-06-29

### New
- **Inject your own world news from the console.** A new [b]cr.world_event[/b] command lets you tell the realm
  something happened and have your NPCs gossip about it -- e.g. [i]cr.world_event A great fire gutted the Vlandian
  docks at Sargot[/i]. Any kingdom you name in the text is auto-tagged so its people are the likeliest to have
  heard it. It spreads as a rumour (by the same hearsay rules as real news: an NPC mentions it only if they would
  plausibly have heard it, as hearsay, when it fits), so ask a nearby NPC about recent news to draw it out. To
  make ONE specific NPC know a fact directly, use [b]cr.memory_add[/b] instead.

### Notes
- **A Console Commands Guide now ships with the mod** (CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md). It sorts
  every [b]cr.[/b] command into "cheats & player tools" and "developer tests", with examples, so it is clear which
  are for playing and which are only for testing. In game, [b]cr.help[/b] lists them all.

## v1.17.4.. 2026-06-29

### Changed
- **Captive scenes now read as the prisoner's story.** In a captivity scene the captor speaks and acts, but no
  longer narrates their own private thoughts (you cannot read a captor's mind), and the descriptive narration is
  now a neutral narrator written from YOUR perspective.. second person, what you see and feel, not the captor's
  point of view. It is your ordeal, told as yours.

### Notes
- **The experimental Town Ambush (opt-in) now plays end to end.** With the Town Ambush toggle on (Mod Options ->
  Experimental, still OFF by default while it is polished), a lord whose hatred has turned murderous can have
  hired blades jump you in a town: lose the street fight and his band convoys you to him for a reckoning, after
  which he keeps visiting his prisoner. The cutthroats now close in from across the square, you start in the
  heart of town, and the map fast-forwards through the captivity instead of sitting on pause. Still being
  refined, so expect rough edges.. leave the toggle off if you would rather wait.

## v1.17.3.. 2026-06-29

A new editable file for advanced prompt tinkerers.. 

### New
- **A "last word" prompt file for advanced users.** A new editable [i]post_history_instructions.txt[/i] (in the
  mod's ModuleData folder, ships empty) is injected at the [b]very end[/b] of the prompt, right before the model
  replies.. the highest-recency position. If you come from SillyTavern-style setups, this is the equivalent of the
  post-history / jailbreak field: whatever you write is sent verbatim, no wrapping from the mod, for a final tone
  push or roleplay framing. [b]behavior_guidelines.txt[/b] remains your "main system prompt" (early, global voice);
  this is your "last word". Loads on campaign start or [i]cr.reload_lore[/i]. See the Nexus "Make it your own"
  section. (Thanks to the player who asked where to put RP framing pre/post history.)
- **A Ko-fi page, by request.** Several of you asked for a way to support the work, so there is now a Ko-fi page
  (a "Support the work" section sits at the top of the Nexus page). The mod stays fully free and complete for
  everyone, supporter or not.. donations are hugely appreciated and never expected.

## v1.17.2.. 2026-06-29

Endpoints that stream their replies (e.g. Chub / Mars) now work.

### Fixed
- **Replies from streaming endpoints no longer fail to parse.** Some OpenAI-compatible endpoints (Chub / Mars
  among them) stream their answer as "data: ..." chunks even when the mod asks for a single, non-streamed
  response, which produced [i]Failed to parse response: Unexpected character ... value: d[/i]. CR now detects a
  streamed body and reassembles the chunks into one reply, so these endpoints work with no change on your side.
  (Thanks to the player who reported it with the exact error.)

## v1.17.1.. 2026-06-29

A hotfix for a town-access conflict with War Sails (and any mod that adds its own settlement locations).

### Fixed
- **Towns no longer lock you out of ports (and other mod-added locations).** With War Sails installed and CR
  loaded after it, every port showed "Door is locked!" and could not be entered on the town menu. CR registers
  its own settlement-access model (for the experimental town ambush) and was doing so in a way that [b]replaced[/b]
  the other mod's, falling back to the vanilla rules.. which do not know those added locations and lock them. CR
  now [b]wraps[/b] the model already in effect and forwards every decision to it, so other mods' location rules
  are preserved and only the ambush re-entry bar is added on top. (Thanks to the player who caught it on a fresh
  campaign.)

## v1.17.0.. 2026-06-29

Those who shared a conversation now remember it, and NPCs stop harping on how long it has been since you last spoke.

### New
- **Characters who were present at a conversation now remember it.** When others stand in on a talk.. your
  companions, a lord's peers.. each of them who is a real character now carries a memory that they were there
  and saw what passed, so a later one-to-one with them reflects the shared scene instead of drawing a blank.
  (First slice: characters you have already spoken with one-to-one; a complete stranger who merely stood by is
  left for a later pass.) Thanks to the player who noticed companions not recognising a group conversation.

### Changed
- **NPCs no longer harp on how long it has been since you last spoke.** A few days between conversations is
  nothing when you are crossing the map, yet NPCs kept opening with it ("Four days since we last spoke..."). The
  time apart is now mentioned only after a genuinely long absence (a season or more), and even then in passing,
  never as the opening line. Thanks to the player who flagged it.

## v1.16.4.. 2026-06-29

Newer OpenAI models now work when you point the mod straight at OpenAI.

### Fixed
- **Newer OpenAI models (the gpt-5 family and the o-series.. o1, o3, o4) now work through the
  OpenAI-Compatible endpoint.** Pointed directly at OpenAI, these models reject the older [i]max_tokens[/i]
  setting (and a custom temperature) that gpt-4o accepted.. so the connection failed with an "unsupported
  parameter" error, both in conversation and on the [i]Test Endpoint[/i] button. CR now recognises a
  reasoning model and sends what it expects ([i]max_completion_tokens[/i], no temperature). If you use
  OpenRouter or NanoGPT this never touched you.. they normalise it for you. (Thanks to the player who
  reported it.)

### New
- **A [i]Parameter Compatibility[/i] option for the OpenAI-Compatible endpoint.** [b]Auto[/b] (the default)
  detects reasoning models for you; [b]Standard[/b] forces the classic [i]max_tokens[/i] + temperature
  shape; [b]Reasoning[/b] forces [i]max_completion_tokens[/i] with no temperature.. for any endpoint that
  is strict about one or the other. Leave it on Auto unless your server rejects a field.

### Notes
- A new [i]Experimental[/i] section in the mod options holds a [b]Town Ambush[/b] toggle, [b]off by
  default[/b].. an in-progress feature still being polished. It does nothing until you turn it on, and the
  toggle will go away once the feature is finished.

## v1.16.3.. 2026-06-27

### New (for modders)
- **The inbound feed can now correct and retract facts.** Alongside [i]Feed.Record[/i], a mod can call
  [i]Feed.Update(...)[/i] to replace a fed entry by its id, and [i]Feed.Retract(id)[/i] to remove one that's no
  longer true. This keeps CR's gossip current for things that change.. a settlement that moves, a character
  renamed, a place that's destroyed.. instead of leaving a stale entry behind. (Thanks to Bhel / Homesteads for
  the feedback that prompted it.) See the Nexus page's "[i]make Calradia Remembers your modlist's memory[/i]"
  section.

## v1.16.2.. 2026-06-27

### Changed
- **The game now pauses while you read or write a letter.** When a letter arrives (or you open the parchment
  to compose or reply), campaign time stops.. so you can read and answer without the world running on under a
  window you couldn't pause. Closing the parchment restores your previous game speed.

## v1.16.1.. 2026-06-27

A save-stability patch.

### Fixed
- **Fixed a crash when saving with a letter courier in transit.** Restoring an in-flight courier after a save
  could hit a null reference and bring the game down at save-completion.. it showed up most often on autosave or
  save-and-exit, while a manual save a moment earlier seemed fine. The restore is now fully guarded: if a courier
  can't be replaced, the letter simply falls back to date-based delivery and the save completes cleanly. If you
  were getting a crash on autosave, this is very likely it.
- **Hardened the mod's own saving against a rare data race.** World events, schemes, and NPC memory are now
  serialized from a frozen snapshot, so an autosave landing while the game is still writing memory in the
  background can no longer drop a block of data.. or contribute to a save-time hitch.

## v1.16.0.. 2026-06-27

Other mods can now hand the player a MISSION through Calradia Remembers.. CR issues, frames, journals and pays
it like any quest, while the other mod keeps ownership of when its own deed is truly done.

### New
- **Other mods can now set the player a mission.** A second inbound door alongside the v1.15.0 event feed:
  [i]CalradiaRemembers.MissionBoard.Register(...)[/i] issues an external mod's mission as a verified quest.. the
  giver frames it in conversation, it mirrors into the journal, and the reward is gated. The mod calls
  [i]MissionBoard.Complete(id)[/i] when ITS OWN deed is truly done (it owns that truth.. CR never pays on the
  AI's word), and the reward lands when the player reports back to the giver. This completes the Extension
  Surface pillar: world events in v1.15.0, player missions here. See the Nexus page's
  "[i]make Calradia Remembers your modlist's memory[/i]" section.

### Fixed
- **Externally-fed world events that named heroes now keep them.** The hero-id resolution in the v1.15.0 feed
  API silently dropped every referenced hero (a wrong lookup); faction-tagged events were unaffected. Hero
  references now resolve correctly.

## v1.15.0.. 2026-06-27

Your past catches up with you.. a child kept secret can surface years later as scandal, a claim, or quiet
blackmail. And for the first time, other mods can feed events straight into Calradia's memory.

### New
- **A child kept secret.** A romance can leave a hidden child, born out of wedlock with the realm only
  guessing at its father.. and the secret does not stay buried. Season by season it may surface (the older
  the child, the likelier the day), and when it does a [b]scandal[/b] breaks.. a hit to your renown and your
  clan's standing, scaled by how harshly the mother's culture judges such things. The mother reacts in her
  own character: a warm one longs for you to see the child, a proud one wants only that you stop pretending
  it away, and a cold, scheming one turns the secret into [b]leverage[/b].. a demand letter, then a
  conversation pressing you for coin to keep her silence. Pay her off (the Give Gold button settles it) for a
  season's peace, or refuse and risk her letting it slip. And when the child comes of age it may press a
  [b]claim[/b] of its own, a maternal house staking what it feels owed.. and a bastard who becomes a real enemy.
- **Other mods can now feed Calradia's memory.** A new one-way, validated inbound API lets any mod report a
  world event.. [i]CalradiaRemembers.Feed.Record(...)[/i].. and CR turns it into something its NPCs are aware
  of (chronicle, rumour, hearsay in conversation), with no further wiring. The door validates everything it
  is handed (bounds text, clamps magnitude, drops unknown heroes/factions, de-duplicates, rate-limits each
  source), so CR can safely act as the narrative-memory bus of a whole modlist. See the Nexus page's
  "[i]make Calradia Remembers your modlist's memory[/i]" section.

### Fixed
- **An NPC no longer "talks to itself."** With no one else present, a reply could invent a bystander and
  answer them; witness reactions are now dropped when you are alone or when the AI attributes one to the
  speaker themselves.
- **Long replies no longer drag the scrollbar away from you.** While a lengthy, multi-part answer streamed
  in, the view kept snapping to the bottom even if you had scrolled up to read.. it now only follows the
  newest line when you were already at the bottom.
- **Pregnancies the mod arranges now truly take.** A CR-conceived pregnancy is properly flagged and carried
  to term, and the child is placed in the mother's clan.

### Changed
- **Fear reads on a lord the way it should.** When a lord genuinely fears you, it now shows in how he
  carries himself and graduates with the dread.. from weighing his words, to yielding ground, to conceding
  what he would never otherwise grant. A feared lord no longer mistakes the feeling for yours.

### Notes
- A console self-test harness ([i]cr.testall[/i] / [i]cr.test[/i]) now exercises the mod's systems end to
  end (including live conversation smoke tests that stamp which LLM produced each reply, for comparing
  models). It is a developer/regression tool; see CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md.

## v1.14.0.. 2026-06-25

Conversations stay open, gold can change hands with a word, and meddling in a lord's plot earns you a
real friend.. and a real enemy.

### Fixed
- **Conversations no longer stop opening partway through a campaign.** A rare hitch while a chat was
  closing could leave the window unable to reopen.. every later attempt would show the lord's greeting
  but never the chat itself, for the rest of that play session. The closing sequence is now hardened so
  the window always tears down cleanly, whatever happens. If you ever hit this, please send the log
  ([i]Enable Logging[/i] + [i]Verbose[/i] in the mod options) so the underlying cause can be found too.

### New
- **Hand over gold with a word.** A [b]Give Gold[/b] button now sits beside the gift button in
  conversation.. name an amount and offer it on the spot. This also makes it plain how to satisfy a
  "bring me [i]N[/i] denars" request: the lord acknowledges the coin and marks the task done.
- **Warning a lord of a plot now truly costs and rewards.** Tip a lord off to a scheme against them and
  the grateful target becomes a genuine ally, while the exposed plotter turns a solid enemy.. and how far
  each moves depends on character. An honourable lord feels a real debt; a calculating one suspects your
  motive. A vindictive plotter hates you the harder; a merciful one bears it with more grace.

### Notes
- [i]cr.info[/i] now reports [i]pacing=[/i] (your Relationship Pacing setting) instead of the misleading
  [i]warmth=[/i] label. See CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md for the scheme test commands.

## v1.13.0.. 2026-06-22

The world starts to talk about [i]you[/i], and a captive can bargain for their freedom.

### New
- **They have heard of your deeds.** A lord you have never fought, but whom word has reached, may now
  speak of what you have done.. attributed to you, as hearsay ("they say you broke our host…"), never
  recited as a list. And it colours how they receive you: wariness or fear where the deed cut against
  their own kin or banner, respect where it served their side. Your standing itself still moves only on
  what a lord has truly seen or lived.. this is reputation, the world reflecting your acts back at you.
- **Talk a prisoner into a bargain for freedom.** Speak with a captive lord and they may offer for their
  release. If they are [b]your[/b] prisoner, you can set them free on the spot in exchange for goodwill,
  gold, or service. If they are held by [b]someone else[/b], they can ask you to break them out.. a deed
  paid only when you truly free them in the field, never on a promise. (The natural entry point in a
  prison cell is still being confirmed.. report back if the option does not appear.)

## v1.12.1.. 2026-06-22

### Fixed
- **A captor no longer mistakes your quests for their own.** While you were held prisoner, a captor..
  especially a bandit.. could see your outstanding tasks and act as the lord who had given you one,
  torturing you for "failing" a bandit-clearing quest that was, absurdly, to defeat them. Quests are now
  left out of a captive scene entirely, where they never belonged.

## v1.12.0.. 2026-06-22

A wave of negotiation deeds, dynastic memory across generations, and a fix for an army-encounter trap.

### New
- **Pay for a lord's service with an enemy captive.** A lord may demand you hand over an enemy prisoner..
  one you already hold (given over on the spot) or one you must go capture and bring back.. as the price
  of a favour, often their own service. Honoured only on a real prisoner transfer, never on a mere claim.
- **A lord may ask you to go to war.** With cause against a faction, a lord can make their service (or
  reward) conditional on YOU declaring war on it.. "rid me of the Vlandians and I am yours." It completes
  only when your own faction declares that war, never when you are merely attacked.
- **Your house is remembered across generations.** When your character dies and an heir takes over, the
  lords of Calradia do not forget.. they inherit the standing, debts, and grudges of your house. They
  speak to your heir of what their father or mother did ("your father helped me at that siege…"), while
  the private history of the dead stays buried, and the bond with the heir begins fresh from there.
- **Hand-authored personalities shape derived nature too.** When you set a character's traits via
  `character_overrides.json`, their derived nature (including kinks at Hardcore) now re-derives to match..
  even for NPCs you have already met.

### Fixed
- **No more being trapped by an army after a conversation.** Talking on the map to a lord leading an army
  could, on closing the chat, drop you into the hostile army menu (Attack / Send troops / …) with no
  "Leave". The conversation now returns you cleanly to the campaign map.

### Notes
- New test commands: cr.deliver_prisoner, cr.declare_war_quest, cr.succession. See
  CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md.

## v1.11.1.. 2026-06-22

### Fixed
- **Giving an item as a gift now works.** Offering an item from the bag picker could fail with "you
  don't seem to have it to give".. the transfer relied on the AI repeating the item's exact name back,
  and any rewording broke the match. The item you pick is now handed over directly, whatever the
  character says.

### New
- **Hand-author an NPC's personality.** An editable `ModuleData/character_overrides.json` lets you set
  a character's five personality traits (−2..+2) and an optional backstory. Turn it on with the new
  **Use custom traits** option (Mod Options → Calradia Remembers → Character). The authored traits become
  the real source of truth.. driving how they speak, their derived nature, and the campaign AI alike..
  and turning the option off restores their vanilla traits. Off by default: your game is unchanged until
  you opt in. (Console: cr.backstory, cr.reload_lore.)

## v1.11.0.. 2026-06-21

Letters get their own home, and the letter-reply crash from v1.10.0 is fixed.

### New
- **Letters now open on their own sheet of parchment.** Reading a letter, writing a new one, or
  replying each happens in a dedicated courier window.. the correspondent's portrait at the top, the
  words in a clean medieval hand on aged paper.. instead of borrowing the conversation panel.
  Composing a letter shows a compact window; reading and replying give the letter room to breathe.

### Fixed
- **Replying to a letter no longer crashes (the v1.10.0 known issue).** A reply is now sealed and sent
  at once.. the window closes cleanly, the game un-pauses, and the courier carries your words to be
  answered later. No more "(considering…)" wait, no hang if the AI hiccups, and no crash on close,
  whatever the model does.
- **Letter text reads cleanly.** The parchment uses a crisp medieval serif instead of the heavy,
  shadowed look it had before.

### Notes
- Letter test commands: cr.letter_read / cr.letter_compose / cr.letter_reply. See
  CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md.

## v1.10.0.. 2026-06-21

A big one.. a wave of new world-simulation and roleplay systems, plus two fixes from tester
feedback. The new systems are freshly built and being refined; expect rough edges and please report
them. (Console commands and an in-game test guide ship in CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md.)

### New
- **NPCs now have a posture toward you, not just a like/dislike.** Beyond plain regard, each lord
  carries how far they **trust** your word, how much they **respect** your worth, and how much they
  **fear** you.. and they speak in keeping with all of it at once. A beaten rival may hate you yet
  respect and fear you; a charmer you like may still not trust your word. These shift the way the
  game works: **a real deed moves them strongly** (a kept promise earns trust, a won siege earns
  respect, taking a lord prisoner earns fear, a discovered treachery shatters trust), while **mere
  talk only nudges** them a little. Fear fades over time as the threat recedes.
- **Recruit a lord into your own clan, through conversation.** A lord who trusts you deeply, whose
  house has fallen on hard times (landless, broken clan, or estranged from his liege), and whose
  standing your own house has risen above, can be persuaded to forsake his banner and swear to your
  clan.. no menu, just the talk. (One of the most-requested features from AI Influence players.)
- **Feuds become real plots.** Bitter rivals now quietly scheme against one another in the
  background.. entirely mechanical, no AI cost.. and you only ever hear of it as a rumour. A plot
  matures, may be discovered, and on success costs its target real standing. You can take a hand:
  a plotter who trusts you may ask you to help against their rival, or you can **warn the target**
  and give them a fighting chance. But meddling has a price.. if your hand in a plot is exposed, the
  wronged party turns on you (worst of all when you betrayed your own realm.. that is treason), and
  word of your treachery spreads, so other lords speak of you as a known schemer.
- **Lords ride out to meet you.** A lord may now seek you out on the campaign map rather than wait
  to be found.. a hostile realm's lord challenging a notorious traveller crossing their land, or a
  lord reacting to fresh news of your deeds. They open the conversation on that footing. They give
  up the chase if you clearly outrun them, and won't pester you (one at a time, with a cooldown).
- **Lords read the balance of power.** When a lord's realm is far weaker than yours, when they are at
  war and losing, when they recently lost to you on the field, or when they are your prisoner, it
  now colours how they speak.. calculated, wary deference rather than blind defiance or empty
  submission.

### Fixed
- **Letters arrive less often.** Unprompted letters were fine one at a time but piled up across many
  correspondents. There is now a world-wide throttle on top of each character's own cooldown, so the
  flow stays alive without burying you.
- **Saved memory grows far more slowly.** Every battle, however minor, was being written into the
  memory of every lord present, swelling the save. Now only sieges and clashes of two armies are
  remembered.. the engagements a lord would actually recall.. which also keeps battle rumours to the
  ones that matter. (This was never a save-corruption risk; it just kept the file growing.)

### Notes
- New console commands for the above (testing): cr.stance / cr.stance_set, cr.recruit / cr.recruit_info,
  cr.scheme / cr.schemes / cr.scheme_advance / cr.scheme_assist / cr.scheme_warn, cr.intercept. See
  CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md for the full reference and what to try.
- Known issue under investigation: replying to a letter can occasionally crash on the close of the
  letter window (a UI-tick fault). It is not save-breaking.. "attempt to continue" recovers cleanly.
  A fix is in progress.

## v1.9.1.. 2026-06-20

### New
- **Use any local AI server.** In Mod Options → Calradia Remembers → LLM Provider, set Provider
  to **OpenAI-Compatible** and fill in three fields: the server's base URL (e.g.
  `http://localhost:1234/v1` for LM Studio, `http://localhost:11434/v1` for Ollama), the model id
  your server expects, and an API key if your server requires one.. leave the key blank for the
  default keyless configuration. A **Test Endpoint** button verifies the connection before you
  play. This lets you run any GGUF, llama.cpp, Ollama, vLLM, or TabbyAPI model locally without
  any cloud subscription. Quality and speed will depend entirely on your local hardware and the
  model you load.

## v1.9.0.. 2026-06-20

### Fixed
- **Captive scenes no longer cut off right after they begin.** A captor could pose their opening
  threat.. "talk, or I get creative".. and then the AI would immediately *end* the conversation,
  stranding the scene on a "has ended" line before it had really started. The captor can now only
  end a captive scene when it genuinely concludes; an early end is ignored, and the scene carries
  on with its normal Continue / Try-something-else controls.
- **The tavern keeper takes letters again.** The "talk to the locals" chat had begun hijacking
  the tavern keeper.. he'd answer as a random townsperson ("a blacksmith's apprentice") and his
  "send a letter" service was lost. Idle-chat is now offered only to genuinely anonymous
  townsfolk and villagers, never to a functional NPC with their own dialogue (tavern keeper,
  a captured bandit, a merchant, and so on).
- **Replying to a letter no longer risks a crash, and no longer looks like a stalled chat.**
  Generating an NPC's letter reply could, on a background thread, read a character's memory at the
  same instant the game was writing to it ("Collection was modified"), and the reply window left
  the recipient stuck "(considering…)" as it closed.. which could destabilise the UI. Replies are
  now built from a stable snapshot, and the window resolves and closes cleanly.

### New
- **The world has news now, and NPCs have heard it.** Battles, sieges, conquered towns,
  noble marriages, executions, and declarations of war are quietly recorded as they happen.
  When you talk to a lord, they may bring up recent events they would plausibly know of..
  what their own faction is involved in, what happened near them, what is still fresh..
  spoken as hearsay ("word came that…"), never as omniscience. A Sturgian lord won't recite
  yesterday's distant Aserai gossip; the news that reaches someone depends on how far it is,
  whose realm it touches, and how recently it happened.
- **Commoners share real news, not invented news.** The "talk to the locals" rumours are
  now drawn from things that actually happened in your campaign, so a townsperson's word on
  the street is genuinely current.
- **Distant news arrives uncertain.** The further word has had to travel to reach someone,
  the less sure they are of it.. a faraway rumour comes with real doubt ("they say, though
  none can vouch for it…") and the teller may have the details wrong, while news from their
  own region or realm is told plainly.
- **Insults travel.** Affront a lord steeply in front of witnesses and word of the disgrace
  gets around.. it becomes a rumour others may have heard, rather than evaporating the moment
  the conversation closes.
- **Feuds are common knowledge.** When two lords of the same crown bear real bad blood, word
  of their rivalry circulates as ambient gossip.. so a lord may mention which of their peers
  are at each other's throats.
- **Even a private affair can reach the wrong ears.** A romance with no witnesses no longer
  relies on a flat chance to draw a jealous reaction.. word of it now travels through the same
  rumour network, and a rival or wronged spouse may grow jealous if and when it reaches them,
  depending on how near they are and whose circles the gossip runs in.
- **Lords remember how you fought.** Win or lose a battle and the lords who stood with you..
  or against you.. record it: "fought at your side when you broke the enemy", "saw you put
  their host to flight". They simply know you acquitted yourself well, instead of waiting for
  you to tell them so.
- **Your Chronicle.** Your own deeds are written into a chronicle in the voice of a medieval
  chronicler.. "In the autumn of the 6th year, the lord {you} met {foe} in open battle near
  {place}, and by sword and valour put the foe to flight." Open it any time from the **"Read
  the Chronicle"** button on your own character's Encyclopedia page: a parchment-styled
  window of your saga, dated and in order. (Also written to CalradiaRemembers_chronicle.log.)
- **An illuminated chronicle, if you want it.** Beyond the plain dated entries, an **Illuminate**
  button in the chronicle window has the AI rewrite your saga into flowing chronicler's prose..
  grand, period-voiced, and shaped by an editable style file (`ModuleData/chronicle_style.txt`:
  a devout monk by default, but write your own skald or court flatterer). It is a *living*
  manuscript: as your deeds mount, Illuminate sets only the newest ones to fine prose and appends
  them, while the very latest read as the chronicler's rough notes until you do.. and a toggle
  flips between the plain record and the illuminated one. The illuminated chronicle is saved with
  your campaign; "Rewrite all" redoes it in a fresh voice after you change the style.

### Notes
- Console: `cr.chronicle` prints/writes the chronicle; `cr.chronicle_open` opens the in-game
  viewer; `cr.chronicle_illuminate` generates the AI-illuminated prose.

## v1.8.0.. 2026-06-19

### New
- **Romance now has rivals.** Court, kiss, or wed one person and a third party with a real
  stake in them may turn against you.. never a random grudge, always someone with a believable
  reason: the wronged spouse of the one you courted, your own slighted partner, a close associate
  who hoped to marry them first, or an admirer of yours you passed over. They find out the way
  people really do.. if there were **witnesses**, word travels fast; flirting with a married lady
  in front of others is a telegraphed risk. A few days later the consequence lands: their regard
  for you cools, they remember the slight, and they may send a **letter**.. a cold warning, a
  wounded reproach, or an open threat ("leave her be.. I mean to marry her one day").
- **Jealousy reads the culture of the wronged party.** A partner from a strictly monogamous
  people (Vlandia, the Empire, Sturgia) treats a love given elsewhere as a betrayal. A partner
  from a culture that accepts several spouses among its highborn (Khuzait, Aserai, Battania,
  the Nords) is not outraged that you took another.. but the rivalry shifts to standing and
  attention between your partners, so a great love becomes a harder one to keep. Which factions
  hold which view lives in an editable `ModuleData/jealousy_cultures.json`, so total-conversion
  and faction mods can define their own peoples' customs without touching the mod.

### Fixed
- **Hideout boss fights work again.** When clearing a bandit hideout, the final confrontation
  with the boss could open the mod's "talk to a local" chat instead of the vanilla choice to
  duel the leader alone or charge in with everyone.. leaving you unable to start the fight, with
  the AI treating the brigand as an ordinary townsperson. The "talk to the locals" option is now
  limited to real civilian settlements (towns, villages, castles) and never offered to bandits,
  so a hideout plays out exactly as the base game intends.

### Notes
- New console command `cr.jealousy [name]` resolves and fires a jealousy reaction on demand, for
  testing the feature without building up a full romance first. `cr.jealousy_comp` reports your
  current partner household and forces one rivalry-over-attention pass.

## v1.7.8.. 2026-06-19

### Fixed
- **Letters from the daily flow now actually arrive.. and stop repeating.** A reliability
  bug could strand letters that an NPC decided to write on their own each day (a romantic
  note, an impatient follow-up, a word from a spouse or consort, a lord's marriage
  proposal). The moment such a letter began generating, the same daily pass that started
  it could mistake the half-written letter for an abandoned one and quietly discard it.
  The letter never reached you, and because nothing recorded that it had been sent, the
  NPC tried again the next day, and the next.. an endless, undelivered loop. The worst
  case in the wild: an NPC writing the same "you still haven't written back" letter over
  and over, none of which arrived. Letters now survive the day they are written, reach
  you as intended, and a follow-up is sent once and only once. (Cleanup of genuinely
  abandoned letters from an interrupted session still happens.. but now safely at load
  time, where it can no longer collide with a letter being written.)
- **The "you haven't replied" follow-up now waits five days from when the letter
  *arrived*, not from when it was written.** Couriers take time on the road, so the
  silence now starts counting from the day the letter actually reached your hands.

## v1.7.7.. 2026-06-19

### Enhanced
- **Commoner conversations now carry live rumors.** Every time you speak to a local,
  the mod samples what the street is actually talking about: captured lords, nobles who
  recently fell in battle, the mood of the market, active wars. The commoner weaves these
  into the conversation naturally.. "I heard", "they say", "word came that".. rather than
  reciting them. The rumor list refreshes once per game day per settlement and is never
  saved to the file.

## v1.7.6.. 2026-06-18

### New
- **Talk to the locals.** When you approach a random townsperson, villager, or castle
  servant.. anyone without a name and title.. you can now ask "What's the news here?"
  This opens a short LLM conversation with a procedurally generated commoner: their name,
  trade, and manner come from the local culture. They know what street-level people know:
  who rules the settlement, how things are going, which lords are visiting, and what wars
  the kingdom is fighting. Nothing is saved.. every encounter is transient. The only action
  they can take is to ask for a coin in exchange for something worth knowing (take_gold).

## v1.7.5.. 2026-06-18

### New
- **Join a kingdom as a mercenary through conversation.** If a lord who belongs to a
  kingdom speaks with you while your clan is free of any obligation, they can offer you a
  mercenary contract on their kingdom's behalf.. hired swords, no oath of fealty. You can
  also propose it yourself. When both parties clearly agree, your clan enrolls immediately.
- **Marry someone you love.** When the bond between you and an NPC has grown deep enough,
  they can propose marriage in conversation.. and so can you. The game's full marriage
  logic runs when you both agree: they join your household, the clans gain a formal tie.
  A wedding without the family's prior blessing (earned through Phase A.2) will chill
  their kin's regard for you.. love over politics always has a price.
- **Lords can propose a match by letter.** A lord who regards you well may now write to
  propose a marriage alliance.. one of their eligible kin offered to your house. You will
  receive this letter as a courier; the door from their side is now open.
- **Take a consort.** Not every deep bond needs to be.. or can be.. a legal marriage.
  When your relationship with an NPC has become genuinely intimate, you may name it openly:
  a consort bond is a private, mutual commitment, real to both of you even if Calradian
  law does not recognise it. Available even if you are already wed. Consorts write to you
  as a spouse would when you have been apart too long.

## v1.7.4.. 2026-06-18

### Fixed
- **Romantic letters no longer arrive in doubles.** When an NPC decided to write and
  the game saved or the daily tick fired before the letter finished generating, a second
  copy of the same letter could be queued. The system now reserves a slot the instant
  the decision is made, so no duplicate can slip through.
- **NPCs now write back when you send them a letter.** In many cases the AI was quietly
  deciding not to reply.. reasonable in the abstract, but frustrating when you had gone
  to the trouble of hiring a courier. The default is now the opposite: a letter by courier
  deserves a written answer, and the AI only stays silent for genuinely exceptional reasons.

### New
- **Silence gets a follow-up.** If an NPC sent you a romantic letter and you have not
  replied after five days, they write again.. impatient, longing, or quietly wounded,
  depending on their nature. Only one follow-up per unanswered letter, so it stays
  dramatic rather than becoming spam.
- **Replies to romantic letters feel like replies.** When you finally answer an NPC's
  letter, their response now opens with the relief and warmth of someone who had been
  anxiously waiting.. not a stock greeting as if they had forgotten they wrote to you.

## v1.7.3.. 2026-06-17

### Fixed
- **NPCs now reliably reply in the player's language.** The previous attempt placed
  the language instruction early in the prompt where English-centric models ignored
  it. The directive is now at the very end of the system prompt (where it is read
  immediately before the reply), made explicit with per-language examples, and
  marked as overriding everything else. Models that follow instructions should now
  mirror the player's language consistently.

## v1.7.2.. 2026-06-17

### Fixed
- **Inventory button hidden during captive encounters.** The gold-bag button was
  visible but did nothing when clicked.. a captive has nothing to offer. It is
  now correctly hidden for the duration of the scene.
- **Logging off by default.** In some configurations (MCM loading late, mod
  conflicts) logging could activate silently even when the player had never
  turned it on. The fallback is now off, matching the documented default.

## v1.7.1.. 2026-06-17

### Fixed
- **Step-children no longer misattributed.** When a lord had children from a
  previous marriage, the AI would treat them as shared with the player's
  character. The family block now names the child's actual other parent inline
  ("Garios (male, by the late Liena)") so the model understands the distinction.
- **NPCs now reply in the player's language.** The model is instructed to mirror
  the language of the player's latest message for all dialogue and narration.
  Section labels and action keywords remain in English so the parser is
  unaffected; only the spoken prose is translated.

## v1.7.0.. 2026-06-16

The largest update so far: a near-complete rebuild of captive encounters, a
marriage-blessing mechanic, broader AI-model support, and a wave of fixes to
how characters understand the world around them.

### New
- **Marriage now needs a family's blessing.** Win a house's approval in
  conversation before you wed.. marry into a family without its blessing and its
  members take the slight personally and cool toward you. The blessing is theirs
  to give, withhold, or set a worthy deed as its price.. the old tension between
  love and ambition, made real.
- **"Please continue" at the end of a scene.** When a captive encounter closes,
  you can choose to carry it on for another round instead of leaving.. your call.
- **Real escape attempts.** A captive who tries to break free now has a true
  (if slim) chance to actually get away.. and if they do, they really are freed.
- **A captive encounter can leave consequences.** A woman may fall pregnant from
  an encounter, the child born into your own clan.

### Improved
- **Captive encounters, rebuilt (Hardcore).** Scenes now move with a clear
  shape.. a beginning, a build, and a real ending.. instead of stalling or
  cutting off. "Continue" and "Try something else" controls are always on hand,
  so you can let a scene unfold or step in and steer it yourself at any moment,
  and every encounter opens and develops differently now so no two feel the
  same. Others present act with their own personalities and agendas rather than
  standing about as scenery.
- **Works with far more AI models.** The mod is now much more tolerant of how
  different models format their replies, so lighter or cheaper models no longer
  produce stray tags or empty lines. A new "Reasoning Effort" option and a
  one-click model test.. both in the mod settings.. help you dial in and verify
  your model.

### Fixed
- **Characters know their own families.** They no longer mistake a daughter for
  a son, invent the names of their own children, or fail to recognise their
  in-laws.. and they now know which of their kin are brothers and sisters.
- **They remember the recently fallen.** A rival who has just died is no longer
  forgotten the instant they fall.. a character still recognises a foe they lost
  only days ago.
- **Guests don't claim another lord's hall.** A character visiting another
  lord's town no longer speaks of the hall as though it were their own holding.

## v1.6.2.. 2026-06-13

A critical save-reliability fix.

### Fixed
- **Saves with many remembered characters no longer break.** As you spoke with more and
  more NPCs, the growing block of memories could exceed an internal limit in the game's
  save format and leave the file unable to load. Memories are now stored in a way that has
  no such ceiling, so a long campaign full of remembered characters saves and loads
  reliably. Existing saves are upgraded automatically the next time you save.. nothing to
  do. (Layered safety net on top: any single memory that still cannot be written or read
  is skipped rather than taking the whole save down, so your campaign always loads.)

## v1.6.1.. 2026-06-13

A stability release.. and a small touch of life in conversation.

### Fixed
- **Saves that refused to load.** A letter courier travelling the map at the moment
  you saved could, in some states, corrupt the save so it would not load again. The
  mod now keeps courier parties out of the save entirely: they step aside the instant
  you save and return right after, and a letter caught mid-journey across a save is
  delivered on schedule by its fallback. Existing campaigns are safe again.

### New
- **NPCs can tell you where someone is.. if they would plausibly know.** Ask a
  character about another person they are close to (kin, a clansman, a good friend,
  or someone in their own faction) and they may share where that person was headed,
  spoken as hearsay: "Last I heard, she was riding toward Ocs Hall." A stranger or a
  distant rival they simply cannot place.. and no one invents a whereabouts they were
  never told.

## v1.6.0.. 2026-06-13

Bargains beyond coin.. and conversations that feel less scripted.

### New
- **Pay for a hire in goods, not just coin.** Ask a wanderer whether something
  other than denars would seal the deal, and they may take payment in kind. A bag
  icon in the chat opens a "Pay in Goods" picker.. choose whole stacks from your
  party inventory, each shown with its value, and hand them over to close the
  bargain. Goods are worth their weight, not their wish: the asking value in kind
  is roughly double the coin price, and an NPC may refuse the barter outright and
  demand denars.. in character, by their nature. When the deal closes they accept
  in their own voice, and the moment is written into your shared history.
- **Offer an item to any NPC.** The same bag lets you give a character something
  from your inventory as a gift in ordinary conversation.. they react to it in
  character. (Quest items like the Dragon Banner are valued honestly and won't be
  mistaken for real payment.)
- **Varied conversation openings.** The first line an NPC greets you with now
  shifts with their regard for you and their personality, instead of the same
  fixed phrase every time.. so your story log reads less repetitively.

### Improved
- **NPCs no longer know your exact purse.** A character can no longer see the
  precise coin you carry and name it to the denar.. they bargain in good faith,
  and the game still enforces what you can actually afford when a deal is struck.
- **In-conversation barters stay out of the quest journal.** Settling a hire in
  goods on the spot doesn't open and close a journal entry.. only genuine deferred
  tasks (deeds you go off and perform) are mirrored there.

## v1.5.0.. 2026-06-12

Recruit companions by talking to them.. negotiation included.

### New
- **Recruit companions through conversation.** Convince a wanderer to join you in
  the chat window and they actually will: the new `join_party` action moves them
  into your party and pays them, exactly like a tavern hire. The price is the
  vanilla asking price.. and it is negotiable: impress them and they may come down
  (never below 75% of their price), offend them and they may demand more, or
  refuse outright. All the usual rules hold: clan companion limit, your gold,
  and only genuinely free wanderers can be hired.
- NPCs will no longer *agree* to deeds the game cannot deliver (joining you,
  handing over troops, freeing prisoners..) unless the matching action exists..
  they deflect or refuse in character instead of making promises that break.
- `cr.hire_info [name]` console command: shows whether an NPC is recruitable in
  conversation and the negotiation price band.
- Gold lines in the chat now show your purse balance ("You give Gunefer 550
  denars (purse: 669)").. the map bar does not refresh while time is frozen
  under the chat window, so this is your live confirmation that coin moved.
- Double-payment guard: when an NPC joins you, any stray take_gold emitted for
  the same payment is ignored.. you are charged the settled price exactly once.

## v1.4.0.. 2026-06-12

A reliability-and-feedback release: NPCs now remember conversations far more
dependably, multi-character scenes feel grounded, and the first wave of player
feedback from Nexus is addressed. No new save format.. drop it in over your
existing campaign.

### New
- **Companions in conversation.** Companions who physically followed you into a
  scene now join the conversation automatically. When they aren't present (e.g. a
  menu "Talk"), a picker lets you bring up to two.. or meet the lord alone. You can
  also dismiss a companion mid-conversation just by asking them, in your own words,
  to leave you and the NPC alone.
- **Private audiences in plain language.** Ask any NPC to speak privately, however
  you phrase it, and they will decide whether to clear the room.. no longer tied to
  the button alone.
- **`cr.memory_add` console command.** Create a memory dated in the past, so you can
  give a character a shared history with you instead of starting as strangers.
  Example: `cr.memory_add 30 We fought side by side at Pendraic. Derthert`

### Improved
- **Conversations are remembered even if you walk away early.** Previously a memory
  was only saved when an NPC formally ended the exchange; leaving before that left no
  trace. Now any conversation of real substance is summarised and recorded when the
  window closes.
- **Witnesses make sense.** Only people actually in the same room are present:
  lords share a keep audience, while notables (smiths, merchants, gang leaders) meet
  you one-on-one.. sometimes with a fitting bystander of their own (an apprentice, a
  customer, an enforcer), never an unrelated lord wandering in.
- **NPCs speak of the dead accurately.** A relative who died now reads with the real
  cause and how long ago ("the late Ingalther, who fell in battle a week past")
  instead of an invented "three winters ago."
- **The AI understands time.** It is told how long ago each remembered event was, so
  it stops guessing and contradicting your own campaign's history.
- **Relationship Pacing** (renamed from "Relationship Warmth"): the three modes are
  now **Grounded / Warm / Storyteller**, framed as a style of play rather than a
  difficulty. Grounded keeps the core game's feel; Storyteller is roleplay-first.
  At the warmer settings, sincere praise, pledges of support and shared victories
  now earn goodwill more readily.
- **Clearer console feedback.** `cr.memory` now shows how long ago each event was
  ("Day 91078 (6 days ago)") so the big day numbers make sense at a glance.

### Fixed
- **Couriers no longer strand at sea.** A letter sent from (or to) someone aboard
  ship now departs from the nearest port instead of a courier sitting motionless on
  the water.
- **Stale dialogue no longer leaks between conversations.** Lines from a previous
  chat could bleed into the opening of the next; the capture buffer is now cleared
  between partners.
- **No witness picker when you have no companions**.. the prompt simply doesn't
  appear instead of showing an empty list.
- A prisoner can no longer clear their captor's guards from a scene.

### Notes
- `cr.discover` is renamed **`cr.discover_test`** and now clearly seeds *placeholder*
  traits for UI testing.. real discoveries still come only from conversation. (The
  old name made identical test results on different NPCs look like a bug.)
- NPC memories are stored inside your campaign save, so they persist when you **save
  the game**. Edits you make in the console follow the same rule.. save after fixing.

### Under the hood
- Background refactor of the chat and witness systems for maintainability.. no change
  to how the mod plays.
