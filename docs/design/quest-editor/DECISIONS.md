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
