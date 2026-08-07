# Changelog.. Calradia Remembers

All notable changes to the mod, newest first. Dates are calendar dates.

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
