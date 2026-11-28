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

## Second round: scene keeper, visible change, the pull to speak, the protocol (27/11/2026, late morning)

A second outside review asked for:
- a canonical record of what the scene makes true;
- an inner change that MAY show, not one that must;
- levels of change;
- a reason to speak out of duty or morality (Edric never spoke);
- complications with two edges instead of verdicts;
- respect for `end_conversation`;
- an ablation protocol.

What was built:
- **Scene keeper.** It replaces the honesty check. One cheap reading per line keeps the scene record (objects, who holds
  what, admissions; a claim is recorded as a claim). It also flags a lie, a repeated gesture or image, or a contradiction,
  and asks for one redo. A sincere false belief is not a lie. The record goes to the prose, the inner lives, the reaction
  writer and the dramaturge. The model-played protagonist is checked too: in the game a person writes those lines.
- **Inner life, three new fields:**
  - level: none, fluctuation, reevaluation, tactic, aim or lasting. Only reevaluation and above count as a change, and a
    lasting change is kept "for memory";
  - visible: nothing, a detail or words. A reaction shows only when something is visible;
  - urge: low or high, and the reason. A high pull of duty or morality lifts the judge's verdict one notch. Edric now
    speaks in some runs.
- **Dramaturge:** it prefers events with two edges. The drama gained one:
  - the notched sword ground smooth, a false exoneration only Tomas can refuse;
  - Berold's letter, read by Ilsa alone: he rode because she wished it.
- **Actions honoured:** an NPC whose line the interpreter reads as `end_conversation` leaves the scene.
- **Echoes dropped:** a reaction that only copies a line already in the scene is dropped. The banner's arrival had
  appeared three times.

**Protocol.** Five variants, five drama runs each, on the same sheets. Claude Opus 5.5 graded each run's variants side by
side, blind, in two random orders.

| Variant | Inner lives | Dramaturge | Keeper | Mean | Spread across runs (sd) |
|---|---|---|---|---|---|
| A | off | off (old reader) | off | 5.29 | 0.37 |
| B | on | off | off | 6.03 | 0.73 |
| C | off | on | off | 5.57 | 0.92 |
| D | on | on | off | 5.83 | 0.72 |
| E | on | on | on | 6.01 | 0.97 |

The fixes (the protagonist checked, echoes dropped) were then graded against A and E on the same runs: A 5.30, E 6.17, F
(E plus fixes) 6.36.

What the protocol shows:
- Inner lives are the one clear gain: +0.7 on the mean. Repetition goes from 4.4 to 6.7, consequence from 4.5 to 5.8.
- The dramaturge alone adds little (+0.3), and inner lives plus dramaturge score no better than inner lives alone.
- The keeper mostly helps repetition.
- The whole system against the baseline: about +1.0 (A 5.3, F 6.4). With five runs per variant, any difference under
  about 0.5 is noise.
- The flaws the grader names most now:
  - a revelation that lands with no visible reaction (the letter, the banner);
  - repeated questions;
  - small parse slips ("then the boy");
  - narration slipping into the first person.
- One drama line still went to Grok in a fixed run.

Best run of the fixed variant: `scenario-2026-11-27-drama-protocol-best.md`.

## Third round: reacting to revelations, a simpler dramaturge tried and withdrawn (27/11/2026, noon)

What changed:
- **News.** Every inner life is told what entered the record since the last line. News that touches a character, their
  secret or their aim must be at least a reevaluation. This answers "the letter and the banner arrive and change nothing".
  CORRECTION (evening): this wiring was lost when the dramaturge was restored, before the H runs and before the commit.
  H is F plus the format fixes only; the news first ran in the fourth round.
- **Format.** Actions and narration are always in the third person, and "to: Ilsa, then the boy" now parses as Ilsa.
- **A simpler dramaturge, tried and withdrawn:**
  - it ran on the cheap model;
  - it could bring an event only when no inner life had changed during the beat;
  - it scored G 5.40 against F 6.61 and A 5.83 on the same runs;
  - zero events came in five runs, since someone always changes, and the scenes went flat;
  - so the events are worth more than the ablation suggested: F scores 8.3 on consequence;
  - the prose-model dramaturge is back.

Graded on the same five runs:

| Version | Mean |
|---|---|
| A, baseline | 5.20 |
| F, last commit | 6.09 |
| H, F plus news and format | 6.29 |

H against F is within the noise. The grader no longer names the format slips.

Still open:
- Tomas sometimes confesses too early, at the first pressure.
- Three adults send the boy to bed in turn.
- Ilsa's mask hides the letter so well that a reader sees no trace of it. The visibility rule may be too strict when the
  news is that strong.

## Fourth round: the three open flaws (27/11/2026, evening)

Two corrections to the record first:
- **The news never ran before this round.** It was lost from commit e6572c7 (see the third round). H measured the format
  fixes alone.
- **The letter's private lines sat in the shared record in every earlier variant, A to H.** Tomas and Edric could reason
  on what only Ilsa had read, and her silence about it could be graded a lie. Every earlier drama grade carries this bug.

What changed:
- **A secret has a price.** The inner life counts how often the character was pressed (addressed or named) and the ways
  of keeping the secret already tried. A secret goes only with proof in the record, or after at least two ways have
  failed. Once pressed four times with those ways spent, the character may give it up. When the ways run out, the choice
  is silence or the truth, never a false account. A first version had no way out: Tomas never confessed and lied
  more under the pressure.
- **No echo of another's demand.** The line prompt says not to repeat an order someone else already gave. The scene
  keeper counts such a line as a repeat and has it written again.
- **Private events.** An event can now be "public|Name|private". The record, the news and every prompt get only the
  public part. The reader sees the private line, and so does the character who read it, as part of what they know.
- **News is events only.** Only what the world brings counts as news; it is no longer everything new in the record.
  Only news that touches the character's own secret or aim changes them.
- **A blow shows.** News read alone, or news that changes what a character wants, leaks at least a detail whatever the
  mask, shown once.
- **Parsing.** "to everyone *She turns*", with no colon, now parses.

The steps, five drama runs each, graded blind by Opus. A grading differs by up to 0.4 from the next one, so the variants
were graded side by side each time:

| Variant | What it added | Mean | Against, same grading |
|---|---|---|---|
| I | the news as everything new in the record, plus the secret, echo and leak rules | 4.53 | H 6.13 |
| J | news as events only | 5.77 | H 5.81 |
| K | private events | 5.71 | H 6.14 |
| L | the leak limited to blows | 5.74 | H 6.10 |
| M | news changes only those it touches; the secret has a way out | **6.37** | H 5.81 |

What the steps showed:
- **I failed.** The record grows with every line, so the news was never empty. A leak fired on nearly every line, and the
  grader scored repetition at 2.2.
- **The three target flaws were gone from J onward.** The grader no longer named an early confession, adults echoing the
  bedtime order, or a letter that changes nothing.
- **M is the best variant so far.** It leads H on six criteria of seven: subtext 7.4, change 6.6, voices 6.3,
  consequence 7.0, coherence 5.9 and engagement 7.1.

What M still gets wrong:
- **Repetition is worse.** M scores 4.3 against 5.8 for H. M scenes have 9 to 13 narrated reactions, H scenes 5 to 6,
  and the inner lives change 19 to 30 times a scene against 13 to 22. More change reopens the reaction gate, so gestures
  recur (Edric's cup, gazes held). The next lever is that gate, not the minds.
- **A rewritten line is never checked again.** The keeper catches two to four lies a scene and has each line written
  again, but nothing reads the new version, and one rewrite still misstated the letter.
