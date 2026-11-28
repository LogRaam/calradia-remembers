# Group scenes: canonical state, memory between scenes, and what Gabriel decided for the game (28/11/2026)

Everything here is in the simulator (`CalradiaRemembers.Tests/Conversation/GroupConversationSimulation.cs`). Nothing is in the mod.

## What was built

- **One canonical state.** Things have a state (one line a thing, replaced when it changes). The world's facts reach every
  reader. The keeper has an "invention" verdict for a line that states a fact the game tracks and that no fact holds.
  Private life stays free (Gabriel: "le problème arrive lorsque le LLM invente quelque chose que le jeu peut contredire").
- **Secrets one by one.** Each secret has its own state and the list of people it was told to.
- **A dossier per character, cumulative.** Events and lasting changes are appended with their day. A secret stays until
  told. The list of unsettled matters is the only part consolidated whole (six at most). Days are absolute, and
  "three days ago" is computed when the sheet is read. Characters never say a day number aloud.
- **Three meetings in a row** (day 100, 103, 104) carry only the dossiers.
- **Narrated reactions are chosen in code:** one at most a line, never on two lines running, and only a blow that leaks,
  a change of tactic or aim, a lasting change, or a character's first sign in the scene.

## Measurements

Narrated reactions, five drama runs each, graded blind by Opus in one batch:

| Variant | Mean | Subtext | Repetition |
|---|---|---|---|
| R, every reaction the referee lets through | 5.57 | 6.9 | 4.0 |
| S, no reaction narrated | 5.74 | 5.8 | 6.7 |

The mean does not move; the two trade subtext for repetition. Hence the choice in code. Its own measurement is still to do.

Memory ablation: the second meeting replayed three times at each level from the same first night, graded blind against
that first night (continuity, psychology, progress, literary):

| Memory carried | Mean | Continuity | Psychology | Progress | Literary |
|---|---|---|---|---|---|
| none | 4.21 | 3.5 | 4.3 | 4.0 | 5.0 |
| events only | 5.17 | 5.0 | 5.7 | 4.8 | 5.2 |
| events, lasting changes, bond | 6.83 | 6.7 | 7.0 | 6.8 | 6.8 |
| full (plus unsettled matters and secret states) | 6.12 | 5.8 | 6.7 | 5.7 | 6.3 |

- Memory is the largest measured gain of the whole system: 4.2 to 6.8.
- The lasting changes and the bond bring more than the events alone.
- Full scored below the level under it. With three runs this is within noise. It was also measured before the
  unsettled list was consolidated and before secrets had their own states, so it must be measured again.
- Without memory the scenes contradict the first night (letters read that were burned, a blow never struck).

## Decided by Gabriel for the game (not built)

- **Presentation, the novel's convention** ("oui ok j'aime ça"): speech in quotation marks, gestures as plain third-person
  prose, no asterisks. The speaker's name stays as a label. Purple is kept for the narrator's voice: world events and
  the rare narrated reaction, which are lines of their own. The player may still write either way. Open: whether the
  ordinary two-person chat moves to the same form, as an option or for all.
- **An intro opens every chat:** the composed setting (place, hour, season, what was just lived, how the player comes
  and with whom, what the character is doing), as in the composed Alympia scene.
- **An MCM option writes a log of a conversation** in the simulator's transcript form, including "Why each turn went as
  it did" (judge, referee, dramaturge, keeper, inner lives).

## Still open in the algorithm

- Long-term consolidation of a dossier after many meetings.
- The player's side: a human who writes short, off topic, or tests limits.
- Scenes of three and more with the canonical state and per-person secrets.
- A new secret sometimes duplicates one already kept.
