# Branching tales and the tale editor: the decisions taken alone

The goal (`goal.md`) asks for every open decision to be taken by default and written down here with its reason, so
Gabriel can overturn any of them. Each entry says what was decided, why, and what changing it would cost.

## The format

**F1. A choice may not keep watch or open a scene.** A choice's `objective` may be `talk_to`, `return`,
`talk_to_locals`, `be_at`, `defeat_party` or `wait`; `keep_watch` and `explore` are refused with the list of what a
choice may do. Why: both open the scene of the stage that FOLLOWS (keep_watch counts nights toward it, explore is the
scene itself), so as one way out among several they have nothing to open. Written as "a choice leads to a stage that
keeps watch", the same story is told with no new engine path. To overturn: per-choice night counters and a choice
that opens a scene of its own.

**F2. A choice's band does not flee.** A `defeat_party` choice spawns its band like a stage does, but its
`flee_after_days` is not played (the loader says so): the band stays until the stage ends, then leaves the map.
Why: a stage's band fleeing fails the whole tale; for one way out among several, failing the tale is wrong, and
"closing that one choice" is new state the goal did not ask for.

**F3. Only stages the player can reach must reach an ending.** The goal says "every stage can reach some ending
(blocking if not)". I block only the stages reachable from the first one, and report an unreachable stage as
cosmetic ("never reached ... never played"). Why: an unreachable stage can never strand anyone, and the old loader
walked only the chain from the first stage, so a file that loaded before (with an unreachable loop) would otherwise
be refused now: "every file that loaded before loads the same" wins.

**F4. The first stage cannot offer choices.** It is the request, completed by accepting the tale in the offering
conversation; the choices go on the next stage. Refused with that sentence.

**F5. A canvas may be several lines.** `canvas` is a string, one `{"text", "if"}` line, or a list of them; the lines
whose flags hold are said, joined. The goal asked for the string or the object form; the list costs nothing more and
lets two paths that meet again on one stage each keep their own words.

**F6. A choice may carry its own `canvas`, `facts` and `knowers`.** In the conversation that takes a choice (telling
the lord, confronting the rival), that character speaks from the choice's lines, as the character whose conversation
completes a stage speaks from that stage today. Why: without it, the lord who hears "it was your own rival" in the
very conversation that takes the "expose" choice would speak from the stage before, knowing nothing. Optional: a
choice without them speaks from its stage, as before.

**F7. An outcome may carry a `journal` line**, the last line written when the tale ends that way ("The boy is
home."). Optional, on reward, failure and every named ending.

**F8. Every named ending closes the journal entry as a success**, the time-out (`failure`) as a failure. The tale was
played to its end; whether the ending is a happy one is in its regard, memory and journal line. To overturn: a
`"failed": true` mark on an ending.

**F9. A scene with choices needs no required finding.** The old rule ("at least one required finding, or it could
never be completed") stays for a scene without choices. A scene with choices is completed by its choice or its way
out.

**F10. The default way out of a scene may not hang on a flag** when it is the only way out (no `on_complete.next`):
refused, since a flag that does not hold would close the scene into nothing. Several defaults: the first is used, the
author is told. A default next to an `on_complete.next` is unused, and the author is told.

**F11. `if` and `sets` take one name or a list.** `"if": "saw_light"` and `"if": ["saw_light", "!refused"]` both
read; every condition in the list must hold.

**F12. A scene choice's id is a marker name** (letters, digits, `_`), refused otherwise, because the narrator writes it
as `[[choice:id]]` and nothing else can be read back. The same holds for findings, hazards and encounters, which the
old loader never checked: for them it is reported as cosmetic (no shipped or drafted file has such a name).

**F13. A written reward nothing reaches is reported** (cosmetic). A tale written entirely in named endings keeps an
empty `reward` and hears nothing.

**F14. The loader keeps date-like text as text.** It used to let Newtonsoft turn a line that looks like a date into a
date, which `ToObject<string>` then printed in another format. Found while writing the converters; no file is
affected; it is one setting on the reader.

**F15. Two messages changed wording**, for files that were refused before and still are: a stage with no `next` now
says `(a stage id, "reward", or "end:<id>")`, and an unknown objective lists `wait` among the known ones. The loop
message ("the stages loop back to ...") is replaced by the general dead-end message, naming each stage caught.

## The engine and the tests

**E1. The map tests see a git worktree.** `SourceTree` took a worktree (where `.git` is a file) for a build copy and
skipped every map test and every test reading a shipped quest. This branch lives in a worktree, so without the fix
its suite would have been green for having checked nothing. Commit `f1f58f9`.

**E2. "Has the reward any content" compares with a blank outcome** rather than testing `Regard != 0`, which the regard
ratchet rightly counts as an inline threshold. It is no threshold: it asks whether the author wrote anything.

**E3. The character whose conversation ends the tale keeps their block until it closes.** Before branches, the block
vanished the moment the run completed, so the canvas of the werewolf's last stage ("relief and gratitude, plainly
given") never reached the lord in the very conversation where he pays the reward. With endings reached in
conversation (expose the rival to the lord), this mattered more, so `PromptBlockFor` now also answers for the
`SpokenHeroId` of a finished run. Found while reading the code; it changes what the lord is told in that one
conversation, and nothing else.

**E4. A scene opens whenever its stage is reached.** In increment 1 an `explore` stage opened only after `keep_watch`
(the night that came due). Now any path into an explore stage opens its scene: at once, or when the conversation that
led there ends; and the hourly tick reopens a scene that is due and not open (after a load). Why: a scene choice
leading to another scene ("you turn left: the mill"), or `be_at` leading to "among the ashes" in smoke_over_the_hills,
must open; before, such a stage could only be left with `cr.story_step`.

**E5. A stage left behind takes its bands with it.** Every band of the tale is released when the story moves to
another stage (a stage's band, or a choice's), not only when the tale ends. A choice's band is tagged
`<quest>/<choice>`; the stage's own band keeps the tag it always had (`<quest>`), so a save taken mid-hunt finds it.

**E6. A required finding is told when the scene closes, however it closes,** a choice taken included, and its flags
are set then. A default choice taken on closing sets its flags too. Why: the old rule ("a clue is missed only in its
manner") is unchanged; an author who wants a clue only on one path marks it not required.

**E7. `cr.story_choose <choice id>`** takes an open choice however it would be taken, so an author can walk each path
(beside `cr.story_step`, which on a stage with no objective takes the first open choice). `cr.story_status` now lists
the flags, the path taken and the open choices.

**E8. Find-SwapRisk** reports 15 members on `main` already (MercenaryDeparturePolicy, RealmServiceWording,
TransientPartyLedger); this branch adds none. The goal says "it must stay at zero"; it was not at zero before.

**E9. A choice by words reaches the action interpreter through the turn's own facts, not through `CrActions`.**
An external action is frozen into the interpreter's cached head (the same for every call), and its eligibility and
teaching never reach the interpreter at all, so a `CrActions.Register("story_choice")` would have been taught in every
conversation, for every tale and none. The mod instead writes the choices open with THIS character into a new
`EncounterContext.StoryChoicesForInterpreter` (SDK), which `ActionInterpreterContextBuilder` appends below the cached
head. A conversation with no such choice carries nothing, and the head stays cached. The character's own prompt never
sees it (Gabriel: a character is not told the ways ahead); `StoryChoicesForInterpreterTests` pins all three.

**E10. The verb is `story_choice`, dispatched by the bridge, outside `GameActionCatalog` and `HandledActionTypes`.**
The catalogue is what every interpreter is taught; this verb is taught only per turn, so listing it there would teach
it everywhere and `action_catalog_parity` compares only those two lists. The bridge has its own `case` for it (the
verbs map shows it as dispatched, and a `case` never adds a row). The SDK's section parser already keeps any
`[ACTION]` type, so nothing else changed there.

**E11. The interpreter is told when NOT to report a choice**: never for an offer still weighed, a question, a refusal,
a jest or a promise to think about it, and at most one per reply. The game then accepts only an id declared on the
current stage, open (its `if` holds), and said to this very character; anything else is refused and changes nothing
(self-test `authored_quest_said_choice`, through the real bridge).

**E12. Where a reported choice runs, checked in the code (a trace of the interpreter path, 04/10/2026).** The
encounter context is rebuilt every turn for the character speaking that turn (`ChatViewModel.Exchanges`, "Rebuild
context each turn"), so a choice opened mid-conversation by a flag is taught on the next turn, and one already taken
is no longer taught. The action runs on the main thread (`MainThreadDispatcher.SwitchToMainThread` after the
interpreter call). In a council, an `actor:` naming another seated member runs the action as that member: the choice
was not said to them, so it is refused. The game puts no cap on how many actions one reply reports and asks no
confirmation for this verb; two choices in one reply take only the first, because the stage has moved
(`GIVEN_two_choices_by_words_reported_in_one_reply_WHEN_both_reach_the_game_THEN_only_the_first_is_taken`).

## The scene and its numbered list

**S1. A scene may carry a `title`**, the name over the chat window ("The burned barns of {village}"). Empty, it is
"The wood by <village>" as before, so the werewolf's scene is named as it always was. Why: a branching tale's scenes
are not all in a wood, and the old title was written into the code.

**S2. A typed number is read only alone**: "1", "1." or "1)", with spaces around. "1 and then I run" is words. The
number of "Something else..." typed alone is words too (the goal: a number with no choice behind it is free text).

**S3. "Something else..." leaves the list where it is** and puts the cursor in the input; the list goes when the
player sends their line, and comes back under the next narration if ways are still open. A click on a way keeps
whatever the player had started typing in the input.

**S4. The list sits inside the scrolling messages, under the last narration** (a `MessagesStack` around the message
list in both prefabs, the scroll panel's inner panel). The alternative, a panel over the bottom of the window, would
have hidden the end of the narration, which is where the narrator asks what the player does. The prefab change is a
wrapper and one list, inserted without touching the message template (a small diff on purpose).

**S5. Taking a way adds no line of its own.** The narrator tells the first steps on the path and the scene ends, as a
lost night already ends it; the closing text and any missed finding follow on closing, as before.

**S6. The list's colours** are a warm amber, brighter under the cursor, darker while pressed, at the same size steps
as each skin's dialogue text (MCM "Chat Text Size"). They live in brushes (`CR.SceneChoice.*`), since a bound brush
resets any colour written on the widget.

## The worked examples

**W1. The {rival} is the head of another house of the giver's realm**, picked by the dice among those the player could
find (`AuthoredRivalPolicy`): never the giver's house, the player's house, a lord of another realm, a younger son, or
someone gone from the world. A giver with no realm (an independent clan) has no rival, so a tale that needs one is not
offered by them. `requires.rival` accepts only `clan_leader` of the giver's realm (`same_realm_as: "{giver}"`).

**W2. No NPC-to-NPC grudge.** *Overturned by Gabriel, 06/10/2026: "a quest can influence the regard between lords; CR already handles the time for that." Built as the `grudge` consequence (`GrudgeSource.Tale`, mends with time), and smoke_over_the_hills' exposed ending carries one.* Original note: The draft's `"grudge": {holder, against}` between the giver and the rival needs its own
ruling (may a quest leave two lords hating each other, and for how long?), as the design doc said. Left out: the
exposed ending instead leaves a memory with the rival ("The player laid my riders' surcoat before {giver}") and news
that spreads through the realm.

**W3. smoke_over_the_hills drops what the game cannot bind.** `{enemy_realm}` became "the realm's enemies" in the
prose (binding a second faction was not asked for), and the draft's `realm_at_war`, `border_of_realm` and the
`be_at` stage's `on_complete.narration` (never read by the loader) are gone: the narration among the ashes is now a
real scene, `among_the_ashes`, opened by arriving at the village (E4).

**W4. smoke_over_the_hills has four endings, not three.** Expose, confront and keep silent are the goal's three; the
confrontation then waits on what the player does: go to the lord after all (exposed, and the rival remembers being
confronted first), agree on a price with the rival (`bought`, a choice by words, the goal's own example), or leave him
to carry it (`confronted`). Since increment 7, `his_price` is taken by words; `cr.story_choose his_price` still walks it.

**W5. the_bell_in_the_marsh** (my tale, in the werewolf's spirit): a bell rings in a marsh where a chapel sank; at a
fork on the causeway the player turns left toward the bell (smugglers, and Oswin bound in the drowned chapel) or right
into the reeds, a way open only once they saw the boot in the mud (Tam, hiding). Walking away is a loop back to the
watch, with a way out on another night. Two scenes in a row on the left path; two endings.

**F16. A file that opens with a comment now loads.** Newtonsoft read a leading `// ...` or `/* ... */` as the whole
file, which then held no quest, and said nothing: the tale silently never appeared. The loader now skips it like any
other comment (one setting). Found while probing how the game reads JSON, to make the editor read it the same way.

**F17. One broken way out is one problem.** A `next` that is missing or names nothing is refused with its own message;
it used to drag along "can never reach an ending" for every stage before it and "never reached" for what lay beyond,
three or four lines echoing one mistake. The path check now reads a broken way out as "could lead anywhere", so the
author sees the real problem. A loop with no exit at all is still refused, stage by stage.

## The editor and its guard

**P1. The fixtures go through `ParseAll`**, the door the game itself uses (one folder at a time), so two tales with
one id in one file are reported as the game reports them. The goal named `Parse`; `ParseAll` runs `Parse` and adds
only that check.

**P2. The editor reads files exactly as Newtonsoft does**, not as `JSON.parse` would: comments, trailing commas,
single quotes, bare names, hexadecimal and octal numbers, NaN, a lone comma read as "undefined", names in any case,
text from numbers ("True" from `true`, ".NET" number formatting), banker's rounding into whole numbers. Every rule was
probed against the real Newtonsoft first; the fixtures `read_*` pin the ones an author is likely to meet. Two
differences are left on purpose, never met in a quest file: Newtonsoft's `new Date(...)` constructors (the editor calls
them invalid) and a NUL character in the middle of a file.

**P3. A "not valid JSON" line is compared without the reader's own message**, which is Newtonsoft's wording ("Error
converting value ... Path ..."), not the loader's. Both sides must agree that the file cannot be read, and on
everything else word for word.

**P4. Node missing fails the parity test**, never skips it: the parity is the guard the goal asks for.
