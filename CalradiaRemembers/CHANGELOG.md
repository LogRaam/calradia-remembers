# Changelog.. Calradia Remembers

All notable changes to the mod, newest first. Dates are calendar dates.

## v1.6.2.. 2026-06-13

A critical save-reliability fix.

### Fixed
- **Saves with many remembered characters no longer break.** As you spoke with more and
  more NPCs, the growing block of memories could exceed an internal limit in the game's
  save format and leave the file unable to load. Memories are now stored in a way that has
  no such ceiling, so a long campaign full of remembered characters saves and loads
  reliably. Existing saves are upgraded automatically the next time you save — nothing to
  do. (Layered safety net on top: any single memory that still cannot be written or read
  is skipped rather than taking the whole save down, so your campaign always loads.)

## v1.6.1.. 2026-06-13

A stability release — and a small touch of life in conversation.

### Fixed
- **Saves that refused to load.** A letter courier travelling the map at the moment
  you saved could, in some states, corrupt the save so it would not load again. The
  mod now keeps courier parties out of the save entirely: they step aside the instant
  you save and return right after, and a letter caught mid-journey across a save is
  delivered on schedule by its fallback. Existing campaigns are safe again.

### New
- **NPCs can tell you where someone is — if they would plausibly know.** Ask a
  character about another person they are close to (kin, a clansman, a good friend,
  or someone in their own faction) and they may share where that person was headed,
  spoken as hearsay: "Last I heard, she was riding toward Ocs Hall." A stranger or a
  distant rival they simply cannot place — and no one invents a whereabouts they were
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
