# Changelog.. Calradia Remembers

All notable changes to the mod, newest first. Dates are calendar dates.

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
