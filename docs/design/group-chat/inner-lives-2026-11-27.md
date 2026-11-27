# Inner lives, dramaturge, honesty check: what the blind grading showed (27/11/2026)

Gabriel's goal: scenes that could stand beside a novel. An outside review of the adult scene of run 2 said the system was
stronger as a social simulation (8/10) than as a novel (5.5/10). It named five gaps:
- the model respects a character's desire, not their strategy;
- no subtext;
- complications that change the decor, not the decisions;
- characters who do not change;
- voices that sound alike.

## What was built (simulator only, not in the mod)

- **Character sheets** gain a voice, a mask (how they want to be seen), a fear and a limit. In the drama and the adult scene,
  each character also holds a secret the others do not know.
- **Inner lives.** After every line, a cheap reader (DeepSeek Flash) updates, for every present character:
  - their feeling;
  - what changed in them and why;
  - their aim now;
  - their tactic;
  - what they show;
  - what they hide.

  The prose writes from that state. The reactions show the mask. A reaction is shown again only when the character changed
  inside, and the floor judge hears the current feeling and aim.
- **Dramaturge.** It replaces the scene reader and runs on the prose model, once a beat. It reads the hidden states, names who
  is repeating, and brings a world fact only when the scene stops turning: never before beat 3, and it may end the scene.
- **Honesty check.** One cheap reading per NPC line, and one redo when the line holds a lie. The mod's rule: never a lie, an
  evasion is fine. In the adult scene it caught 3 lies by Seraphine in 9 lines, and in the drama 1 by Tomas.
- **Subtext rules in the line prompt:**
  - say less than you feel;
  - never name your feeling;
  - never borrow another character's image;
  - never invent a false excuse.
- **Prose:** DeepSeek V4 Pro, reasoning **off**. In the bench with reasoning low, it took 5 to 17 s a line and once returned
  nothing. Those empty replies were the run-2 "refusals" that sent lines to Grok. Kimi K3.1 Pro was tried: 6 times the cost,
  slower, and its planning notes leaked into the scene.

## Speed and cost per scene (prose plus every cheap call)

| | Run 2 (night) | Now |
|---|---|---|
| Wall time | 15 to 18 min | 0.7 to 1.2 min |
| Wait before an NPC line | about 1 min | mean 3 to 5.5 s |
| Grok fallbacks | 7 to 23 a scene | 0 |
| Cost | not measured | about $0.03 to $0.06 |

## Blind grades

Graded with the scene text only, both orders, means over 7 criteria: subtext, change, voices, consequence, coherence,
repetition, engagement. The same transcript graded twice moved by up to 0.7, so one run per variant is a hint, not a proof.

**Drama** (Claude Opus 5.5):

| Version | Mean |
|---|---|
| Run 2, the old system, prose with reasoning | 4.0 |
| New sheets and fast prose, inner lives and dramaturge OFF (`scenario-2026-11-27-drama-ablated.md`) | 5.9 |
| Everything on (`scenario-2026-11-27-drama-inner-lives.md`) | 7.2, then 6.5 |
| Everything on, second run | 7.1, then 6.5 |
| Everything on plus the honesty check (`scenario-2026-11-27-drama-honesty-check.md`) | 5.4 |

The honesty-check run's low grade comes from repetition (3/10, gestures repeated word for word), not from the check.

**Adult** (Grok 4.7; the transcripts stay out of this public repository):

| Version | Mean |
|---|---|
| Run 2 | 4.4 |
| Everything on | 6.0, then 4.3 |
| Plus the honesty check | 5.9 |

Kael's jealous gestures fell from 6 to 1.

## What it shows

- The inner lives and the dramaturge buy **change** and **consequence**: in the drama, 4.5 without them and 7.5 to 9 with them.
- Fast prose alone buys **subtext**: 8.0 in the ablated run.
- Still weak:
  - repeated gestures and objects (Ilsa and the sword, Mira and the grapes);
  - coherence slips around props (which sword was clean);
  - the priest Edric never speaks in any drama run.
