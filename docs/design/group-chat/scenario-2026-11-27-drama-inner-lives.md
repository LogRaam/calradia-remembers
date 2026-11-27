# The widow's hall (drama), simulated
Prose deepseek/deepseek-v4-pro reasoning off (20 calls), NSFW fallback x-ai/grok-4.20 (0 calls), dramaturge on the prose model (4), judge deepseek/deepseek-v4.1-flash (37), inner lives (48), interpreter deepseek/deepseek-v4.1-flash (11). Cost: prose+dramaturge $0.0236, judge+minds+interpreter $0.0387. 1.2 min. Wait before each NPC line: mean 4.0 s, longest 11.5 s.
Arwa (the player: first line scripted, then played by the prose model). Ilsa: Learn exactly how Berold died and who failed him, tonight.; Tomas: Keep the truth of his flight hidden and keep his place in the household.; Edric: Keep the peace of this hall and the seal of confession.

*The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.*

## The scene
**Arwa** *(to Ilsa)*: My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.

> *Tomas steps forward, takes the sword from Arwa, and bows his head to press his lips to the crossguard.*

**Ilsa** *(to Arwa)*: *Ilsa receives the sword with both hands, her fingers closing around the scabbard.* You were there, Commander. Name the man who stood nearest him when he fell.

**Arwa** *(to Ilsa)*: *Sets the blade on the table before her, still sheathed.* I was.

> *Edric reaches for the wine, fills Ilsa's cup to the brim without looking at it.*

**Ilsa** *(to Arwa)*: Then you can tell me how it happened. Plainly. From the moment you reached the water.

> *Lady Ilsa's son, eight, appears at the stair in his nightshirt and asks, to the whole hall, whether Sir Tomas was with Papa at the end.*

**Arwa**: to Tomas: *Does not look at the boy. Keeps her eyes on Tomas.* The boy asked you a question, Tomas.

**Tomas** *(to the)*: boy *Looks at the boy, then away.* Your father was the bravest man I ever knew. You should sleep now, little lord.

**Ilsa** *(to Tomas)*: *Ilsa crosses to the stair, bends to kiss her son's forehead, and presses him gently back toward the steps.* To bed now. I will come up. *She waits until his footsteps fade, then turns back to Tomas, the sword still cradled in her arm.* Sir Tomas. The boy is gone. Now answer him.

**Tomas**: to Ilsa: *Meets her eyes for the first time since the sword came in.* The line broke at the ford. Your lord held the center with a handful of spearmen. I was on the left, and when the left gave way,  *He stops, looks at the stones between them.* He never turned his horse.

> *Ilsa draws the blade just far enough to see her reflection in the steel, then slides it home.*

> *A servant sets down Sir Tomas's own sword, brought back from the armourer: its edge has no notch, no blood.*

**Arwa**: to Tomas: *Steps between Tomas and the table. Sets one hand flat on the pommel of the lord's sword.* Tomas. Your blade came back clean. Say why.

**Tomas** *(to Arwa)*: *He does not touch the clean sword.* I sent it to the armourer this morning. The edge had a burr from last week's practice, and I would not carry a fouled blade to my lord's vigil. *He holds Arwa's gaze a beat too long, then looks away.*

**Ilsa** *(to Tomas)*: *Ilsa sets the clean sword on the table beside her husband's, parallel, the hilts aligned.* Then you can swear on it. That you held the left until the line broke, and not before.

**Tomas**: to Ilsa: *He does not move toward the table.* There is no need to swear what every man on that field already knows. Lord Berold held the center. I was on the left. Ask Commander Arwa what the left flank did, she saw it all.

> *A muddy man-at-arms is shown in, carrying Lord Berold's torn banner. He found it on the far bank, a mile behind the ford.*

**Arwa**: to Tomas: *Does not take her hand from the lord's sword.* I saw the left break before the center. I saw you, Tomas. You were already on the far bank when Berold took the spear.

> *Ilsa places her palm flat on the table between the two swords and turns her head to look at Tomas, her eyes dry and perfectly still.*

**Tomas** *(to Arwa)*: *He is very still.* Then you saw the truth, Commander. I rode from the ford. Not after the line broke, before. *He turns to Ilsa, and his voice drops to something raw.* I left him.

> *Edric folds his hands on the table, eyes fixed on his own knuckles, and does not move.*

**Ilsa** *(to Tomas)*: *Ilsa does not move her hand from the table. The silence stretches three heartbeats before she speaks.* You have named yourself. *She turns her head to Edric.* Father, the hall has heard him.

**Tomas**: to Edric: *He unbuckles his sword-belt, lays it on the table beside the two blades, and kneels.* The hall has heard me. I am ready.

## Why each turn went as it did (judge -> referee, scene reader)

**Beat 1**
- after Arwa: Ilsa Low [to]; Tomas None (held-back shame); Edric None => Ilsa
- after Ilsa: Tomas None; Edric None => BackToPlayer
  - Ilsa changes: Tomas's kiss of the crossguard tells her he loved Berold, and that she may be the only one here who did not. (now struck, hollowed, cold; wants: Same aim, but now she must learn it without letting Tomas see what she is.)
  - Edric changes: Ilsa's question turns the hall toward naming the man at the ford, and Edric knows Tomas was that man ,  and that he fled. (now pierced, cornered; wants: Keep the peace of this hall and the seal of confession.)

**Beat 2**
- dramaturge: progress: Ilsa shifts from receiving a relic to actively interrogating the only witness, seizing control of the room's attention. | repeating: none | untapped: Tomas fled and confessed it to Edric, who is bound by the seal. | move: none
- after Arwa: Ilsa Urgent [to] (held-back fury); Tomas None (held-back fear); Edric None => Ilsa
  - Ilsa changes: Arwa's answer turns her probe back on her ,  the witness she meant to use is the one she must now judge. (now exposed, cornered, cold; wants: Same aim, but now she must get the truth out of Arwa without accusing her outright.)
  - Tomas changes: Arwa's "I was" tells him she stood near Berold and can place him at the ford, so his silence is no longer safe. (now caught, cold dread; wants: Keep the truth of his flight hidden and keep his place in the household.)
- after Ilsa: Tomas None; Edric None => BackToPlayer
  - Ilsa changes: Arwa's "I was" confirms the commander was nearest, so Ilsa's suspicion hardens into a need to test her account rather than merely hear it. (now braced, hollow, watchful; wants: Same aim: learn exactly how Berold died and who failed him.)

**Beat 3**
- dramaturge: progress: Ilsa shifts from receiving the sword to demanding a plain account, and Arwa is now the sole witness under direct scrutiny. | repeating: none | untapped: Tomas fled, and Edric knows it but cannot speak; Ilsa suspects Arwa of letting Berold die. | move: event 3
- after Arwa: Ilsa High; Tomas Urgent [to] (held-back fear); Edric None => Tomas
  - Ilsa changes: Her son's question about Tomas, and Arwa turning it on Tomas, cracks her grip on the room ,  the interrogation is no longer hers alone. (now pierced, still, exposed; wants: Same aim: learn exactly how Berold died and who failed him.)
  - Tomas changes: The boy's question and Arwa's turning it on him ,  the hall now waits on his answer, and silence itself accuses. (now exposed, sick with dread; wants: Keep the truth hidden and his place, but now he must speak to survive it.)
  - Edric changes: The boy's question and Arwa's turning it on Tomas ,  the seal now shields a man in front of a child. (now sick with dread; wants: Keep the peace of this hall and the seal of confession.)
- after Tomas: Ilsa High; Edric None [about] => Ilsa
  - Ilsa changes: Tomas's evasion tells her he was there and will not say it, so the hall itself is now the obstacle. (now cold, cornered, certain; wants: Same aim, narrowed: get the truth out of Tomas before the boy is sent away.)
  - Tomas changes: Arwa's pointed silence and the boy's question make plain that everyone is waiting for him to claim a place at Berold's side. (now cornered, heart pounding; wants: Keep the truth hidden and his place, but now he must answer the boy's question without a lie.)
  - Edric changes: Tomas's evasion to the boy hardens him ,  the seal now shields a coward while a child is fed comfort. (now cold certainty; wants: Keep the peace of this hall and the seal of confession.)
- after Ilsa: Tomas Urgent [to] (held-back dread); Edric None => Tomas
  - Tomas changes: Ilsa's "Now answer him" strips away the boy's grief as a shield, so he can no longer turn the question aside. (now exposed, cold; wants: Same aim, keep the truth hidden and his place, but he must now speak to Ilsa directly without lying.)
  - Edric changes: Ilsa's demand that Tomas answer the boy's question forces the plain account into the open, and Edric cannot steer it. (now cornered dread; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)
- after Tomas: Ilsa Urgent [about] (held-back fury); Edric None => BackToPlayer
  - Tomas changes: Ilsa's "Now answer him" stripped the boy from the room and left him alone with the question he has been dodging. (now cornered, sick with dread; wants: Same aim, keep the truth hidden and his place, but he can no longer hide behind the boy or the stones.)
  - Edric changes: Tomas's half-confession in open hall turned Edric's private dread into a public trap he cannot speak into. (now sick certainty; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)

**Beat 4**
- dramaturge: progress: Ilsa has shifted from receiving the sword to demanding the truth directly from Tomas, and Tomas has been forced to speak of the left flank, though he still hides his own flight. | repeating: Tomas: avoiding the boy, then avoiding Ilsa's eyes, then stopping mid-confession | Ilsa: pressing for the full account, now aimed at Tomas instead of Arwa | untapped: Ilsa's secret that she urged Berold to ride to the ford, and Tomas's secret that he fled it. | move: event 2
- after Arwa: Ilsa High; Tomas Urgent [about] (cornered fear); Edric None => Tomas
  - Tomas changes: The clean sword and Arwa's question strip away his last cover; the unfinished sentence no longer holds. (now exposed, cold; wants: Same aim, keep the truth hidden and his place, but now he must answer the clean blade directly.)
  - Edric changes: The clean sword on the table turned his steering into a lie by omission, so his old tactic fails. (now cornered dread; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)
- after Tomas: Ilsa Urgent; Edric None => Ilsa
  - Ilsa changes: The clean, unblooded sword and Tomas's too-long look tell her the left flank was never truly held; the suspicion hardens into knowledge. (now cold, certain, sick at heart; wants: Same aim: make Tomas say plainly that he broke and left Berold to die.)
  - Tomas changes: Arwa's demand that he say why the blade came back clean forces him to hear his own lie aloud, and the too-long gaze he could not hold tells him it has failed. (now cornered, ashamed; wants: Same aim, keep the truth hidden and his place, but he can feel the room closing and knows he cannot answer Arwa twice.)
  - Edric changes: Arwa's demand that Tomas explain the clean blade ,  the trap is closing and the seal now shields a lie, not a man. (now cold certainty; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)
- after Ilsa: Tomas Urgent [to] (held-back fear); Edric None => Tomas
  - Tomas changes: Ilsa's demand to swear on the blade turns his silence into a trap he cannot slip, and he knows the oath would be a lie. (now trapped, sick with dread; wants: Same aim, keep the truth hidden and his place, but now the cost is a false oath before the whole hall.)
  - Edric changes: Ilsa's demand that Tomas swear on the blade turns the hall into a trap for the innocent, and Edric cannot lie to spring it. (now cornered dread; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)
- after Tomas: Ilsa Urgent [about]; Edric Low->None => BackToPlayer
  - Ilsa changes: Tomas's refusal to swear, and his turning to Arwa, tells her the oath she wanted will not come freely. (now cold, cornered, sick with doubt; wants: Same aim, narrowed: force the truth of the left flank out of Arwa, who saw it.)
  - Tomas changes: Arwa's demand about the clean blade and Ilsa's oath force him closer to the truth; his dodge to Arwa fails because she saw the left flank break. (now cornered, sick with shame; wants: Same aim, keep the truth hidden and his place, but now he must survive Arwa's account of the left.)
  - Edric changes: Tomas's refusal to swear and his turning the question to Arwa tells Edric the innocent man is already being measured for the blame. (now sick certainty; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)

**Beat 5**
- dramaturge: progress: Ilsa shifts the inquiry from Tomas's word to Arwa's testimony, raising the stakes for Tomas and forcing Arwa into the center. | repeating: Tomas: deflecting with appeals to common knowledge and Arwa's witness, still avoiding a direct oath or account of his own actions. | untapped: Tomas confessed his flight to Father Edric, who cannot speak it. | move: event 1
- after Arwa: Ilsa Urgent (held-back fury); Tomas Urgent [to] (cornered); Edric None => Tomas
  - Ilsa changes: Arwa's naming of Tomas on the far bank turns her suspicion from the left flank to the man before her. (now cold, cornered, sick with doubt; wants: Same aim, narrowed: force Tomas to answer for the far bank, before the hall.)
  - Tomas changes: Arwa's naming of him on the far bank ends the hope that her account would spare him. (now exposed, hollowed out; wants: Same aim, but now only to confess before Ilsa strips it from him.)
  - Edric changes: Arwa's naming of Tomas on the far bank forces him to act before the hall condemns a man on his silence. (now cornered dread; wants: Keep the seal of confession while preventing an innocent man from being condemned by it.)
- after Tomas: Ilsa Urgent [to] (held-back fury); Edric Urgent->High (held-back grief) => Ilsa
  - Ilsa changes: Tomas's confession lands as proof, not relief: the man who fled is named, and her own part in sending Berold stays buried. (now still, hollow, coldly certain; wants: Same aim, completed in part: make the hall witness Tomas's guilt, then decide what is owed to the dead.)
  - Tomas changes: Arwa's plain naming of his flight forces him to confess before Ilsa can take it from him. (now stripped, unsteady; wants: Same aim, but now only to confess fully and take what judgment she gives.)
  - Edric changes: Tomas's own confession of "I left him" lifts the need to shield him, so Edric's dread turns to guarding the seal itself. (now sick relief; wants: Keep the seal of confession intact now that the truth is out without him.)
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric Low [to] => Tomas
  - Ilsa changes: Tomas's raw confession of "I left him" lands as her own guilt, since she urged Berold to the ford. (now hollow, unsteady, cold; wants: Same aim, but now she must get through the naming without breaking.)
  - Edric changes: Ilsa turning to him made the seal a public trap, not a private burden. (now cornered dread; wants: Same: keep the seal unbroken while an innocent's name hangs on his silence.)
- after Tomas: Ilsa Low; Edric None [about] => BackToPlayer
  - Edric changes: Ilsa turning to him and Tomas kneeling forced him to see the seal will not save the hall from a false verdict. (now cold certainty; wants: Same: keep the seal unbroken, but now he must find a way for the truth to surface without his lips breaking it.)

**Who each character became**
- Ilsa: came wanting "Learn exactly how Berold died and who failed him, tonight."; leaves feeling hollow, unsteady, cold, wanting "Same aim, but now she must get through the naming without breaking.", hiding "that she urged Berold to the ford, and that she is the other one who left him".
  - beat 1: Tomas's kiss of the crossguard tells her he loved Berold, and that she may be the only one here who did not.
  - beat 2: Arwa's answer turns her probe back on her ,  the witness she meant to use is the one she must now judge.
  - beat 2: Arwa's "I was" confirms the commander was nearest, so Ilsa's suspicion hardens into a need to test her account rather than merely hear it.
  - beat 3: Her son's question about Tomas, and Arwa turning it on Tomas, cracks her grip on the room ,  the interrogation is no longer hers alone.
  - beat 3: Tomas's evasion tells her he was there and will not say it, so the hall itself is now the obstacle.
  - beat 4: The clean, unblooded sword and Tomas's too-long look tell her the left flank was never truly held; the suspicion hardens into knowledge.
  - beat 4: Tomas's refusal to swear, and his turning to Arwa, tells her the oath she wanted will not come freely.
  - beat 5: Arwa's naming of Tomas on the far bank turns her suspicion from the left flank to the man before her.
  - beat 5: Tomas's confession lands as proof, not relief: the man who fled is named, and her own part in sending Berold stays buried.
  - beat 5: Tomas's raw confession of "I left him" lands as her own guilt, since she urged Berold to the ford.
- Tomas: came wanting "Keep the truth of his flight hidden and keep his place in the household."; leaves feeling scraped bare, steady, wanting "Same aim: to take whatever judgment Ilsa gives, without defense.", hiding "that he confessed to Edric last night, and that he fears her contempt more than exile.".
  - beat 2: Arwa's "I was" tells him she stood near Berold and can place him at the ford, so his silence is no longer safe.
  - beat 3: The boy's question and Arwa's turning it on him ,  the hall now waits on his answer, and silence itself accuses.
  - beat 3: Arwa's pointed silence and the boy's question make plain that everyone is waiting for him to claim a place at Berold's side.
  - beat 3: Ilsa's "Now answer him" strips away the boy's grief as a shield, so he can no longer turn the question aside.
  - beat 3: Ilsa's "Now answer him" stripped the boy from the room and left him alone with the question he has been dodging.
  - beat 4: The clean sword and Arwa's question strip away his last cover; the unfinished sentence no longer holds.
  - beat 4: Arwa's demand that he say why the blade came back clean forces him to hear his own lie aloud, and the too-long gaze he could not hold tells him it has failed.
  - beat 4: Ilsa's demand to swear on the blade turns his silence into a trap he cannot slip, and he knows the oath would be a lie.
  - beat 4: Arwa's demand about the clean blade and Ilsa's oath force him closer to the truth; his dodge to Arwa fails because she saw the left flank break.
  - beat 5: Arwa's naming of him on the far bank ends the hope that her account would spare him.
  - beat 5: Arwa's plain naming of his flight forces him to confess before Ilsa can take it from him.
- Edric: came wanting "Keep the peace of this hall and the seal of confession."; leaves feeling cold certainty, wanting "Same: keep the seal unbroken, but now he must find a way for the truth to surface without his lips breaking it.", hiding "that he already knows the whole of it and has absolved the man".
  - beat 1: Ilsa's question turns the hall toward naming the man at the ford, and Edric knows Tomas was that man ,  and that he fled.
  - beat 3: The boy's question and Arwa's turning it on Tomas ,  the seal now shields a man in front of a child.
  - beat 3: Tomas's evasion to the boy hardens him ,  the seal now shields a coward while a child is fed comfort.
  - beat 3: Ilsa's demand that Tomas answer the boy's question forces the plain account into the open, and Edric cannot steer it.
  - beat 3: Tomas's half-confession in open hall turned Edric's private dread into a public trap he cannot speak into.
  - beat 4: The clean sword on the table turned his steering into a lie by omission, so his old tactic fails.
  - beat 4: Arwa's demand that Tomas explain the clean blade ,  the trap is closing and the seal now shields a lie, not a man.
  - beat 4: Ilsa's demand that Tomas swear on the blade turns the hall into a trap for the innocent, and Edric cannot lie to spring it.
  - beat 4: Tomas's refusal to swear and his turning the question to Arwa tells Edric the innocent man is already being measured for the blame.
  - beat 5: Arwa's naming of Tomas on the far bank forces him to act before the hall condemns a man on his silence.
  - beat 5: Tomas's own confession of "I left him" lifts the need to shield him, so Edric's dread turns to guarding the seal itself.
  - beat 5: Ilsa turning to him made the seal a public trap, not a private burden.
  - beat 5: Ilsa turning to him and Tomas kneeling forced him to see the seal will not save the hall from a false verdict.

