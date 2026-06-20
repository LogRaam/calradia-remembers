# Changelog.. Calradia Remembers

All notable changes to the mod, newest first. Dates are calendar dates.

## v1.9.0.. 2026-06-19

### New
- **The world has news now, and NPCs have heard it.** Battles, sieges, conquered towns,
  noble marriages, executions, and declarations of war are quietly recorded as they happen.
  When you talk to a lord, they may bring up recent events they would plausibly know of —
  what their own faction is involved in, what happened near them, what is still fresh —
  spoken as hearsay ("word came that…"), never as omniscience. A Sturgian lord won't recite
  yesterday's distant Aserai gossip; the news that reaches someone depends on how far it is,
  whose realm it touches, and how recently it happened.
- **Commoners share real news, not invented news.** The "talk to the locals" rumours are
  now drawn from things that actually happened in your campaign, so a townsperson's word on
  the street is genuinely current.
- **Distant news arrives uncertain.** The further word has had to travel to reach someone,
  the less sure they are of it — a faraway rumour comes with real doubt ("they say, though
  none can vouch for it…") and the teller may have the details wrong, while news from their
  own region or realm is told plainly.
- **Insults travel.** Affront a lord steeply in front of witnesses and word of the disgrace
  gets around — it becomes a rumour others may have heard, rather than evaporating the moment
  the conversation closes.
- **Feuds are common knowledge.** When two lords of the same crown bear real bad blood, word
  of their rivalry circulates as ambient gossip — so a lord may mention which of their peers
  are at each other's throats.
- **Even a private affair can reach the wrong ears.** A romance with no witnesses no longer
  relies on a flat chance to draw a jealous reaction — word of it now travels through the same
  rumour network, and a rival or wronged spouse may grow jealous if and when it reaches them,
  depending on how near they are and whose circles the gossip runs in.
- **Lords remember how you fought.** Win or lose a battle and the lords who stood with you —
  or against you — record it: "fought at your side when you broke the enemy", "saw you put
  their host to flight". They simply know you acquitted yourself well, instead of waiting for
  you to tell them so.
- **Your Chronicle.** Your own deeds are written into a chronicle in the voice of a medieval
  chronicler — "In the autumn of the 6th year, the lord {you} met {foe} in open battle near
  {place}, and by sword and valour put the foe to flight." Open it any time from the **"Read
  the Chronicle"** button on your own character's Encyclopedia page: a parchment-styled
  window of your saga, dated and in order. (Also written to CalradiaRemembers_chronicle.log.)
- **An illuminated chronicle, if you want it.** Beyond the plain dated entries, an **Illuminate**
  button in the chronicle window has the AI rewrite your saga into flowing chronicler's prose —
  grand, period-voiced, and shaped by an editable style file (`ModuleData/chronicle_style.txt`:
  a devout monk by default, but write your own skald or court flatterer). It is a *living*
  manuscript: as your deeds mount, Illuminate sets only the newest ones to fine prose and appends
  them, while the very latest read as the chronicler's rough notes until you do — and a toggle
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
