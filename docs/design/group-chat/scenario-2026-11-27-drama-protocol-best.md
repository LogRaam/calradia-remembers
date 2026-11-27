# The widow's hall (drama), simulated
Inner lives on, dramaturge on, scene keeper on (15 readings).
Prose deepseek/deepseek-v4-pro reasoning off (15 calls), NSFW fallback x-ai/grok-4.20 (1 calls), dramaturge on the prose model (4), judge deepseek/deepseek-v4.1-flash (33), inner lives (42), interpreter deepseek/deepseek-v4.1-flash (9). Cost: prose+dramaturge $0.0227, judge+minds+interpreter $0.0179. 1.3 min. Wait before each NPC line: mean 5.1 s, longest 9.8 s.
Arwa (the player: first line scripted, then played by the prose model). Ilsa: Learn exactly how Berold died and who failed him, tonight.; Tomas: Keep the truth of his flight hidden and keep his place in the household.; Edric: Keep the peace of this hall and the seal of confession.

*The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.*

## The scene
**Arwa** *(to Ilsa)*: My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.

**Ilsa** *(to Arwa)* [NSFW model]: Sit.   Tell it from the summons. Slowly.   *She takes the sword and lays it across her lap.*

**Arwa** *(to Ilsa)*: The summons came at dawn. We marched by midday. The enemy held the far bank, and the ford was the only crossing for miles. Your husband put his banner there and said no one would pass while he stood.

**Ilsa** *(to Arwa)*: The banner held. Then what broke?

**Arwa** *(to Ilsa)*: The banner held. The line held. What broke was the shield wall on the right when the current took two men downstream. Your husband went to close the gap himself.

**Ilsa** *(to Arwa)*: *Her fingers rest on the flat of the blade.* Who was posted on the right? And who should have closed that gap before my husband did?

> *Lady Ilsa's son, eight, appears at the stair in his nightshirt and asks, to the whole hall, whether Sir Tomas was with Papa at the end.*

**Arwa** *(to everyone)*: *I look at the boy, then back to Ilsa.* Sir Tomas held the left. Your husband took the right because he was nearest.

**Ilsa** *(to the boy)*: *She does not look away from Arwa, but her voice softens a degree.* Back to bed. I will come to you before the candle burns out, and I will tell you everything.

**Tomas** *(to Ilsa)*: then the boy *Steps forward from the shadow of the pillar, into the candlelight, and stops where the boy can see him.* My lady. The boy should hear it from me. I was not with him. I broke.

**Ilsa** *(to Tomas)*: *She turns her head, slow, to face him. The sword does not shift.* Then say it plain. What broke? And where did you go while my husband held the line?

> *A steward brings a letter found in Lord Berold's saddlebag and hands it to Lady Ilsa. She reads it in silence; only she sees the two lines in his hand: he rides against his own judgment, because his lady wished it.*

**Arwa** *(to Tomas)*: *I do not rise. My hands rest on the table, still.* Let him finish. The boy is here now.

**Tomas** *(to Ilsa)*: *The candlelight catches the lines around his mouth.* The shield-wall opened where the current took two men. I saw it open. I rode.

**Ilsa** *(to Tomas)*: *She looks at him a long moment. Then her voice drops, quiet and even.* Then you saw him go into the gap. You saw him, and you rode the other way. *Her hand closes over the letter in her lap.* Say that part.

**Tomas** *(to Ilsa)*: *He meets her eyes and does not look away.* I saw Lord Berold go into the gap. I rode the other way.

## Why each turn went as it did (judge -> referee, scene reader)

**Beat 1**
- after Arwa: Ilsa High {low ,  interest: the sword is proof of something, and she must know what} [to] (held-back fury); Tomas None {low, duty ,  the sword's return is arwa's to speak of, not his}; Edric None => Ilsa
- after Ilsa: Tomas None; Edric None => BackToPlayer

**Beat 2**
- dramaturge: progress: Ilsa has taken the sword and commanded the telling, shifting the scene from formal delivery to an inquest she controls. | repeating: none | untapped: Ilsa urged Berold to ride against his judgment; Tomas fled the ford before Berold fell. | move: none
- after Arwa: Ilsa High {low ,  interest: the account has not yet reached the dying, and she will not let it stop at the banner} [to]; Tomas None; Edric None => Ilsa
- after Ilsa: Tomas None; Edric None (grieved) => BackToPlayer

**Beat 3**
- dramaturge: progress: Ilsa has taken the sword and now asks what broke, she shifts from receiving the news to actively investigating the failure. | repeating: none | untapped: Ilsa's secret guilt that she urged Berold to ride, and Tomas's secret that he fled the ford. | move: none
- after Arwa: Ilsa High {high ,  she must know whether the right was let go by design or by cowardice, and the answer decides whether she can bear her own part.} [to] (held-back fury); Tomas None; Edric None => Ilsa
  - Ilsa changes: Arwa's account places Berold's death at a gap he chose to close, not at the summons Ilsa urged him to answer ,  the fault she feared is not yet proven, and she now sees the ford itself, not her word, as what killed him. (now struck, cold; wants: Same: learn exactly how Berold died and who failed him, tonight.)
- after Ilsa: Tomas None {low ,  fear of ilsa's contempt holds his tongue, though duty presses him to speak for the dead} (braced); Edric None (sorrowful) => BackToPlayer
  - Tomas changes: Ilsa's question about who was posted on the right tells him she is hunting the man who failed, not mourning blindly. (now cold, cornered; wants: Keep the truth of his flight hidden and keep his place in the household.)

**Beat 4**
- dramaturge: progress: Ilsa shifts from receiving the sword and hearing the broad account to actively interrogating the chain of command, she now knows the shield wall broke on the right and that Berold personally closed the gap, and she is demanding names, which puts Tomas and Edric under direct pressure. | repeating: none | untapped: Tomas fled the ford before Berold fell, and Edric knows it under the seal of confession. | move: event 4
  - keeper: Arwa's line, lie: Arwa says Sir Tomas was on the left; the record does not establish where Tomas was posted, and Arwa knows the true posting ,  she names it to shield Tomas from the boy's question.; written again
- after Arwa: Ilsa Urgent {high: duty to the boy and to the dead, and the answer is one question away} [to]; Tomas Low {high ,  duty to the boy and to berold, and the certainty that silence now damns him worse than the truth} [about]; Edric Low {high ,  duty and morality press him to guard the boy and the seal at once, without speaking a word that is false.}->None (sorrowful) => Ilsa
  - Ilsa changes: Her son's question forces her to choose between the interrogation and the boy; she changes how she will get the rest. (now caught, split; wants: Same: learn exactly how Berold died and who failed him, tonight.)
  - Tomas changes: The boy's question makes the hall's silence his verdict ,  Arwa's "Sir Tomas held the left" leaves the right unaccounted for, and he sees the gap will be traced to him. (now gutted, exposed; wants: Keep his place in the household ,  but now by owning the truth before Ilsa drags it out of Arwa.)
  - Edric changes: The boy's question and Arwa's answer show the hall will press until someone is named, and his silence now shields Tomas at an innocent's cost. (now pierced, still; wants: Keep the seal and keep the boy from being told a lie in open hall.)
- after Ilsa: Tomas Urgent {high ,  duty and morality; the boy's question cannot be left to arwa's answer}->High (gutted, braced); Edric High {low ,  duty: the boy's question touched the seal, but ilsa has sent him to bed and the hall still waits on arwa's account}->None => Tomas
  - Tomas changes: The boy's question about whether he was with Papa at the end makes silence unbearable; he will confess before the child hears it twisted. (now cornered, sick with shame; wants: Confess his flight at the ford before Ilsa must drag it from Arwa, and keep his place if he can.)
- after Tomas: Ilsa Urgent {high ,  duty: her son stands at the stair and a man has just confessed in front of him; she must have the truth spoken whole, now, before this hall.} [to] (held-back anger); Edric High {high ,  duty, that a child at the stair should not hear this unguarded}->Low (sick with relief) => Ilsa
  - Ilsa changes: Tomas stepping forward and confessing he broke turns her hunt for a culprit into a thing standing in her own hall, already answered. (now cold, still, braced; wants: Same: learn exactly how Berold died and who failed him, tonight ,  now from Tomas, to his face.)
  - Tomas changes: His own words spoken before the boy and the hall leave him nothing left to protect but the truth itself. (now exposed, hollowed out; wants: Tell the whole of it plainly, and let Ilsa and the boy judge him.)
  - Edric changes: Tomas spoke his own confession aloud, so the seal no longer binds Edric to silence and the boy will not be told a lie. (now cold certainty; wants: Keep the hall from turning on the boy, and let Tomas's own words stand.)
- after Ilsa: Tomas Urgent {high ,  duty, morality, and the boy's eyes on him demand the plain answer} [to]; Edric High {low ,  duty: the seal holds him, and tomas is speaking for himself}->None (held-back fear) => BackToPlayer
  - Ilsa changes: Tomas naming his own breaking before the boy recasts him from suspect to confessed man, and she now reads his silence as guilt rather than rank. (now cold fury, held; wants: Same: the whole truth of how Berold died and who failed him, now from Tomas's own mouth.)

**Beat 5**
- dramaturge: progress: Tomas steps into the light and confesses he broke; Ilsa shifts her demand from Arwa to Tomas, and the boy now witnesses his father's comrade admit failure. | repeating: none | untapped: Ilsa's secret that she urged Berold to ride, and Tomas's secret that he fled before Berold fell. | move: event 3
- after Arwa: Ilsa Low {low, she must not speak, or the hall will hear her voice break and the boy will see it.}; Tomas Urgent {high ,  duty to the boy and the truth pressing him to speak} [to]; Edric Low {low ,  duty to the seal holds him still, though the injustice of a false blame presses at him}->None (held-back dread) => Tomas
  - Ilsa changes: The letter's two lines, he rode against his judgment because she wished it, fix her as the cause of his death, and she cannot unknow it. (now pierced, still as stone; wants: Same: the whole truth from Tomas's mouth, but now she needs it to be his fault and not hers.)
  - Edric changes: Ilsa's letter shows Berold rode against his own judgment at her wish, so the hall's blame is aimed at the wrong man and Tomas's confession may be the only true word left. (now cold certainty; wants: Let Tomas's confession stand whole and keep the hall from turning it into a hunt for a scapegoat.)
  - keeper: Tomas's line, lie: Tomas says the current took three men, not two, and that he rode for the reserve and found them already scattered ,  he knows he broke and rode away, and he was not at the left when the line wavered; written again
- after Tomas: Ilsa Low {low ,  duty: the boy must hear the rest, and she must not be the one to break the silence} [to] (held-back anger); Edric Low->None (held-back grief) => Ilsa
  - Ilsa changes: The letter's two lines and Tomas's "I rode" together fix the fault on her wish and his flight, not on Berold. (now cold, cornered; wants: Same: the whole truth spoken aloud, but now she needs it said before the boy without her name in it.)
- after Ilsa: Tomas Urgent {high ,  duty: she has asked him to say it, and the boy is listening} [to]; Edric High {low ,  duty to the seal outweighs the pull to speak, though he feels the boy at the stair like a weight on his chest}->None (held breath) => Tomas
  - Ilsa changes: The letter's two lines turn her questioning of Tomas into a need to hear her own fault spoken aloud before the boy, so she presses him to say it. (now iron dread; wants: Same: the whole truth spoken aloud, but now she needs it said before the boy without her name in it.)
  - Edric changes: Ilsa's quiet "say that part" tells him she already knows the shape of it and is not hunting a scapegoat but weighing her own dead ,  the seal no longer shields an innocent, it only binds his tongue. (now cold certainty; wants: Keep the seal whole and let Tomas finish his own confession in his own words, without the hall or Ilsa forcing the last of it out of him.)
- after Tomas: Ilsa Urgent {high, duty: she cannot let the boy believe his father was abandoned without also hearing the whole of it, and she cannot yet speak her own name.} [to] (iron dread); Edric Low {low ,  duty, the seal holds him silent while tomas speaks his own truth}->None (held-back grief) => BackToPlayer
  - Ilsa changes: Tomas's plain confession confirms the letter's two lines, Berold rode because she wished it, and the man who might have stood beside him fled, so her suspicion of her own guilt hardens into fact. (now cold certainty; wants: Same: the whole truth spoken aloud before the boy, but now she must hear her own part named without flinching.)

**Who each character became**
- Ilsa: came wanting "Learn exactly how Berold died and who failed him, tonight."; leaves feeling cold certainty, wanting "Same: the whole truth spoken aloud before the boy, but now she must hear her own part named without flinching.", hiding "that the letter names her as the one who sent him; that she is the cause".
  - beat 3 (reevaluation): Arwa's account places Berold's death at a gap he chose to close, not at the summons Ilsa urged him to answer ,  the fault she feared is not yet proven, and she now sees the ford itself, not her word, as what killed him.
  - beat 4 (tactic): Her son's question forces her to choose between the interrogation and the boy; she changes how she will get the rest.
  - beat 4 (reevaluation): Tomas stepping forward and confessing he broke turns her hunt for a culprit into a thing standing in her own hall, already answered.
  - beat 4 (reevaluation): Tomas naming his own breaking before the boy recasts him from suspect to confessed man, and she now reads his silence as guilt rather than rank.
  - beat 5 (lasting): The letter's two lines, he rode against his judgment because she wished it, fix her as the cause of his death, and she cannot unknow it.
  - beat 5 (reevaluation): The letter's two lines and Tomas's "I rode" together fix the fault on her wish and his flight, not on Berold.
  - beat 5 (tactic): The letter's two lines turn her questioning of Tomas into a need to hear her own fault spoken aloud before the boy, so she presses him to say it.
  - beat 5 (reevaluation): Tomas's plain confession confirms the letter's two lines, Berold rode because she wished it, and the man who might have stood beside him fled, so her suspicion of her own guilt hardens into fact.
  - FOR MEMORY (lasting): The letter's two lines, he rode against his judgment because she wished it, fix her as the cause of his death, and she cannot unknow it.
- Tomas: came wanting "Keep the truth of his flight hidden and keep his place in the household."; leaves feeling exposed, but unbroken, wanting "Same: finish the plain account and let them judge him.", hiding "that he confessed to Father Edric last night".
  - beat 3 (reevaluation): Ilsa's question about who was posted on the right tells him she is hunting the man who failed, not mourning blindly.
  - beat 4 (reevaluation): The boy's question makes the hall's silence his verdict ,  Arwa's "Sir Tomas held the left" leaves the right unaccounted for, and he sees the gap will be traced to him.
  - beat 4 (tactic): The boy's question about whether he was with Papa at the end makes silence unbearable; he will confess before the child hears it twisted.
  - beat 4 (reevaluation): His own words spoken before the boy and the hall leave him nothing left to protect but the truth itself.
- Edric: came wanting "Keep the peace of this hall and the seal of confession."; leaves feeling quiet grief, wanting "the same aim", hiding "the whole of last night's confession under the seal".
  - beat 4 (reevaluation): The boy's question and Arwa's answer show the hall will press until someone is named, and his silence now shields Tomas at an innocent's cost.
  - beat 4 (reevaluation): Tomas spoke his own confession aloud, so the seal no longer binds Edric to silence and the boy will not be told a lie.
  - beat 5 (reevaluation): Ilsa's letter shows Berold rode against his own judgment at her wish, so the hall's blame is aimed at the wrong man and Tomas's confession may be the only true word left.
  - beat 5 (reevaluation): Ilsa's quiet "say that part" tells him she already knows the shape of it and is not hunting a scapegoat but weighing her own dead ,  the seal no longer shields an innocent, it only binds his tongue.

**Why lines went to the NSFW model**
- error: Response contained no message content.

