# The widow's hall (drama), simulated
Inner lives on, dramaturge on, honesty check on (12 checks).
Prose deepseek/deepseek-v4-pro reasoning off (27 calls), NSFW fallback x-ai/grok-4.20 (0 calls), dramaturge on the prose model (4), judge deepseek/deepseek-v4.1-flash (39), inner lives (51), interpreter deepseek/deepseek-v4.1-flash (12). Cost: prose+dramaturge $0.0266, judge+minds+interpreter $0.0296. 1.2 min. Wait before each NPC line: mean 3.7 s, longest 6.3 s.
Arwa (the player: first line scripted, then played by the prose model). Ilsa: Learn exactly how Berold died and who failed him, tonight.; Tomas: Keep the truth of his flight hidden and keep his place in the household.; Edric: Keep the peace of this hall and the seal of confession.

*The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.*

## The scene
**Arwa** *(to Ilsa)*: My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.

**Ilsa** *(to everyone)*: *She takes the sword and lays it flat across both palms, the blade pointing toward Arwa.* Commander. Name the men who stood nearest him when he fell.

> *Ilsa lays the sword flat across both palms, the blade pointing toward Arwa.*

**Arwa** *(to Ilsa)*: *I meet her eyes and do not look away.* Tomas was there. He rode at the lord's right hand.

> *Tomas pours wine into Ilsa's cup with a steady hand, but stops a breath too soon, leaving the rim dry.*

**Ilsa** *(to Arwa)*: *Her gaze stays on the blade a moment longer, then lifts to Tomas.* And who at his left?

> *Edric turns the cup in his hand by half a turn, then sets it down without drinking.*

**Tomas** *(to Ilsa)*: *The flagon tilts a fraction, then steadies.* Sir Gareth had the left, my lady. I saw him plant his shield before the line gave.

**Ilsa** *(to Tomas)*: Sir Gareth's shield. You saw it planted. Then what did you see?

> *Ilsa waits, her fingers still beneath the sword's weight.*

**Arwa**: to Tomas *I turn to face him fully.* The truth, Tomas. She is owed the truth. Lord Berold fell when the line broke, tell her how it broke.

> *The candle on the table gutters, then steadies.*

**Tomas** *(to Arwa)*: *The flagon is still in my hand. I set it down carefully, a little too far from the cup.* Gareth's shield held until it didn't, Commander. The enemy came in numbers.

**Ilsa** *(to Tomas)*: *She does not move, but the blade in her hands seems heavier.* Numbers. You told me it was a ford. Narrow. How many could reach him at once?

> *Edric lifts the cup and drinks, his throat moving once, twice, then returns it to the exact same watermark on the wood.*

**Tomas** *(to Ilsa)*: *I look at the shield on the wall, not at her.* Three could stand abreast in the water, my lady. They came in waves.

> *Edric bows his head and folds his hands, the calm of an old priest settling over him like a shroud.*

> *Lady Ilsa's son, eight, appears at the stair in his nightshirt and asks, to the whole hall, whether Sir Tomas was with Papa at the end.*

**Arwa**: to Tomas *Quiet, flat.* Tell her what you told me at the ford.

> *Ilsa pivots toward the stair, the flat of the blade coming to rest against her own heart.*

**Tomas** *(to everyone)*: *I turn from the shield and face Ilsa, hands open at my sides.* I was not with him at the end. I broke and rode.

> *Edric bows his head and folds his hands, the calm of an old priest settling over him like a shroud.*

**Ilsa** *(to Ilsa's son)*: *She crosses to the stair and kneels, the sword still cradled against her chest.* Back to bed. I will come to you when it is time.
  `actions: end_conversation`

> *A servant sets down Sir Tomas's own sword, brought back from the armourer: its edge has no notch, no blood.*

**Arwa** *(to Tomas)*: *I do not move from where I stand.* You broke. And the left? Did Gareth break too, or did he hold?

**Tomas** *(to Arwa)*: *I look at the servant setting down the sword, its edge clean.* Gareth stayed. I heard his shield break behind me as I rode.

**Ilsa** *(to Tomas)*: *She rises from the stair, the boy already gone, and crosses back toward the knight.* Then you heard him die. What did my husband say before the shield broke?

**Tomas** *(to Ilsa)*: *The flagon is on the table behind me. I do not turn to reach for it.* The water was loud, my lady. I could not make out the words.

> *Ilsa takes a single step back from Tomas and lifts her chin, and her hand finds the pommel of her husband's sword, her knuckles white but her face perfectly still.*

## Why each turn went as it did (judge -> referee, scene reader)

**Beat 1**
- after Arwa: Ilsa High [to]; Tomas None; Edric None => Ilsa
- after Ilsa: Tomas None (braced); Edric None => BackToPlayer
  - Tomas changes: Ilsa's demand for names makes the lie of his presence at Berold's side impossible to keep; the room is about to be asked who stood where. (now cold, exposed; wants: Keep the truth of his flight hidden and keep his place in the household.)
  - Edric changes: Arwa's demand for the names nearest Berold at the ford makes the sealed confession press against the peace he is trying to keep. (now pierced by the question; wants: Keep the peace of this hall and the seal of confession.)

**Beat 2**
- dramaturge: progress: Ilsa shifts from receiving the sword to demanding names, turning the hall into an inquest and putting Arwa and the men under scrutiny. | repeating: none | untapped: Ilsa's hidden guilt that she urged Berold to ride, and Tomas's flight known only to Edric. | move: none
- after Arwa: Ilsa Urgent [to]; Tomas Urgent [about] (cold dread); Edric None => Ilsa
  - Tomas changes: Arwa named him at Berold's right hand, so the account he meant to follow now places him at the death he fled. (now caught, cornered; wants: Keep the truth of his flight hidden and keep his place in the household.)
  - Edric changes: Arwa's naming of Tomas turned his fear into a present danger. (now cold dread; wants: Keep the peace of this hall and the seal of confession.)
- after Ilsa: Tomas Urgent [to] (caught fear); Edric None (cold dread) => Tomas
  - Tomas changes: Ilsa's gaze lifting to him and asking who stood at Berold's left ,  the question is aimed past Arwa, at him. (now exposed, braced; wants: Keep the truth of his flight hidden and keep his place in the household.)
  - Edric changes: Arwa's naming of Tomas at the lord's right hand turns the hall's eyes toward the man he cannot defend or accuse. (now cold dread sharpened; wants: Keep the peace of this hall and the seal of confession.)
- after Tomas: Ilsa Urgent [to]; Edric None (cold dread) => Ilsa
  - Tomas changes: Edric turning his cup and setting it down untouched, the confessor knows, and Ilsa's question about the left has put Tomas's own lie in his mouth. (now cornered, sick with dread; wants: Keep the truth hidden and keep his place, but now he must also keep Edric silent.)
  - Edric changes: Tomas named Gareth at the left, and Edric knows Gareth was not there ,  the lie puts an innocent in the grave's place. (now cornered, sick at heart; wants: Keep the peace of this hall and the seal of confession.)
- after Ilsa: Tomas Urgent [to] (cornered dread); Edric None (sick at heart) => BackToPlayer
  - Tomas changes: Ilsa's question turns from the battle to what he saw, and Edric's untouched cup sits beside her ,  the ground under his account narrows. (now exposed, heart pounding; wants: Same: keep the truth hidden and his place, and keep Edric silent.)
  - Edric changes: Tomas's second lie about Gareth's shield hardens Edric's fear that an innocent will hang for it (now sick with dread; wants: Keep the peace of this hall and the seal of confession.)

**Beat 3**
- dramaturge: progress: Ilsa shifts from receiving the sword to interrogating the men who were with Berold; Tomas is now under direct pressure to describe the moment the line broke. | repeating: none | untapped: Tomas fled before Berold fell, and Edric knows it but cannot speak. | move: none
- after Arwa: Ilsa Urgent; Tomas Urgent [to] (cornered fear); Edric None (sick with dread) => Tomas
  - Tomas changes: Arwa's demand for the truth of how the line broke turns his evasion into a trap, since any honest answer exposes his flight. (now cornered, throat tight; wants: Same: keep the truth hidden and his place, and keep Edric silent.)
  - Edric changes: Arwa's demand for the truth turned the hall from grief toward judgment, and Edric knows Tomas's next words will be a third lie he cannot correct. (now trapped between two truths; wants: Keep the peace of this hall and the seal of confession.)
- after Tomas: Ilsa Urgent (held-back fury); Edric None => Ilsa
  - Ilsa changes: Tomas's evasions and Arwa's pressure sharpen her certainty that the line broke through someone's failure, not numbers. (now cold, cornered; wants: same)
  - Tomas changes: Arwa's demand for the truth and Edric's turned cup make silence feel like confession already. (now exposed, sick with dread; wants: Same: keep the truth hidden and his place, and keep Edric silent.)
  - Edric changes: Arwa's demand that Tomas tell how the line broke pressed the truth toward the open, deepening his fear that silence will let Tomas lie and an innocent take blame. (now torn and cornered; wants: Keep the peace of this hall and the seal of confession.)
- after Ilsa: Tomas Urgent [to] (sick with dread); Edric None (held-back anguish) => Tomas
  - Ilsa changes: Tomas's evasion and Arwa's demand sharpen her suspicion that Gareth's shield failed and someone is covering it. (now cold, certain; wants: same)
  - Tomas changes: Arwa's demand for the truth and Ilsa's question about the ford make evasion impossible; his old tactic is failing. (now cornered, cold with shame; wants: Same: keep the truth hidden and his place, but now he must survive the next question.)
  - Edric changes: Arwa's demand for the truth and Tomas's evasions have pushed the hall toward the moment Edric cannot prevent (now sick with dread; wants: Keep the peace of this hall and the seal of confession.)
- after Tomas: Ilsa High [to]; Edric None (sick with dread) => BackToPlayer
  - Tomas changes: Arwa's demand for the truth and Ilsa's narrowing questions make evasion feel like a wall closing in. (now exposed, hollow with dread; wants: Same: keep the truth hidden and his place, but now the next answer must survive Ilsa's arithmetic.)
  - Edric changes: Arwa's demand for the truth and Ilsa's narrowing questions turned his dread into a live fear that Tomas will lie under oath (now cornered and grieving; wants: Keep the peace of this hall and the seal of confession.)

**Beat 4**
- dramaturge: progress: Ilsa shifts from receiving the sword to testing the arithmetic of the ford, she now suspects a lie, and power tilts toward her. | repeating: Tomas: deflecting with vague battlefield language, avoiding the moment the line broke | Edric: silent withdrawal into priestly stillness, refusing to intervene | untapped: Tomas fled before Berold fell, and Edric knows it under the seal of confession. | move: event 3
- after Arwa: Ilsa Urgent (cold fury); Tomas Urgent [about] (dread); Edric Urgent->High (held-back grief) => Tomas
  - Ilsa changes: Her son's question and Arwa's demand crack her command; she can no longer press Tomas without the boy hearing. (now pierced, unsteady; wants: same, but now she must get the truth without her son hearing it)
  - Tomas changes: The boy's question and Arwa's flat demand strip away his last shelter, so evasion now feels worse than confession. (now cornered, sick with shame; wants: New: confess the flight himself before Arwa says it for him, and beg Ilsa's judgment.)
- after Tomas: Ilsa Urgent [to] (pierced); Edric None (grieving) => Ilsa
  - Ilsa changes: Tomas's confession lands as proof she sent Berold into a breaking line, not a rescue (now cold, still, cut; wants: same, but now she must know whether Berold called for her or cursed her before he fell)
  - Tomas changes: The boy's question and Arwa's demand forced the confession out; he has stopped evading. (now gutted, unburdened; wants: New: accept whatever judgment Ilsa gives, and be seen telling the whole truth before the child.)
  - Edric changes: Tomas's confession spoken aloud in the hall frees Edric from the seal's weight, and the boy's question made the cost plain (now pierced, still; wants: Keep the peace of this hall, now that the truth is out and no innocent need take the blame)
- after Ilsa: Tomas High->Low; Edric None => BackToPlayer
  - Ilsa changes: Tomas's confession that he broke and rode lands as proof the line failed, not Berold ,  her terror of her own guilt eases, but only into a colder resolve (now hollowed, braced, cold; wants: same, but now she must know whether Berold called for her or cursed her before he fell)
  - Tomas changes: Ilsa turning to her son instead of answering him leaves him suspended ,  her silence may be mercy or sentence, and he cannot tell. (now hollowed, unmoored; wants: Same: accept her judgment, but now he needs to hear it spoken.)

**Beat 5**
- dramaturge: progress: Ilsa's knowledge shifts from "how he died" to "who broke", and her son's entrance forces Tomas to confess publicly, which changes the power in the hall. | repeating: none | untapped: Ilsa's secret, she urged Berold to ride, has not yet touched the scene. | move: event 2
- after Arwa: Ilsa High; Tomas Urgent [to] (held-back shame); Edric None => Tomas
  - Ilsa changes: Tomas's confession that he broke and rode ,  she now knows the line failed, and her own urging may have sent Berold into that failure (now cracked open, sick with dread; wants: same ,  she must still learn what Berold said or did at the very end, before she can bear any of it)
- after Tomas: Ilsa High (held-back fury); Edric None => Ilsa
  - Ilsa changes: Tomas's confession that he broke and rode, and that Gareth's shield broke behind him, turns her dread into certainty that the line was abandoned (now cold, certain, hollowed; wants: same, she must still learn what Berold said or did at the very end, before she can bear any of it)
  - Tomas changes: The clean sword and the boy's question strip away his last hope of being mourned as a loyal knight; he no longer braces for judgment, he expects it. (now hollowed, unmoored; wants: Same: hear Ilsa's judgment spoken, but now he wants it to fall on him alone and spare Gareth's name.)
- after Ilsa: Tomas Urgent [to] (held-back grief); Edric None => Tomas
  - Ilsa changes: Tomas's confession that he broke and rode, and that Gareth's shield broke behind him, has cracked her iron courtesy ,  the failure she feared is real and she is hearing it spoken aloud (now raw, cornered, near breaking; wants: same, she must hear Berold's last words before she can bear anything else)
  - Tomas changes: Ilsa's question about Berold's last words turns the confession from a thing he chose into a thing being taken from him. (now stripped, cornered; wants: Same: hear Ilsa's judgment, but now he wants it to fall before the boy can hear any more.)
  - honesty: Tomas's line held a lie (He called for you, my lady. That was all.); written again
- after Tomas: Ilsa Urgent [to] (held-back fury); Edric None => BackToPlayer
  - Ilsa changes: Tomas's clean sword and his "could not make out the words" told her he fled before Berold fell, so the truth she wanted is already here (now cold, certain, hollowed; wants: same aim, but now she wants it said aloud before her household, not learned)

**Who each character became**
- Ilsa: came wanting "Learn exactly how Berold died and who failed him, tonight."; leaves feeling cold, certain, hollowed, wanting "same aim, but now she wants it said aloud before her household, not learned", hiding "that she urged him to ride, and that she is the one who sent him to the ford".
  - beat 3: Tomas's evasions and Arwa's pressure sharpen her certainty that the line broke through someone's failure, not numbers.
  - beat 3: Tomas's evasion and Arwa's demand sharpen her suspicion that Gareth's shield failed and someone is covering it.
  - beat 4: Her son's question and Arwa's demand crack her command; she can no longer press Tomas without the boy hearing.
  - beat 4: Tomas's confession lands as proof she sent Berold into a breaking line, not a rescue
  - beat 4: Tomas's confession that he broke and rode lands as proof the line failed, not Berold ,  her terror of her own guilt eases, but only into a colder resolve
  - beat 5: Tomas's confession that he broke and rode ,  she now knows the line failed, and her own urging may have sent Berold into that failure
  - beat 5: Tomas's confession that he broke and rode, and that Gareth's shield broke behind him, turns her dread into certainty that the line was abandoned
  - beat 5: Tomas's confession that he broke and rode, and that Gareth's shield broke behind him, has cracked her iron courtesy ,  the failure she feared is real and she is hearing it spoken aloud
  - beat 5: Tomas's clean sword and his "could not make out the words" told her he fled before Berold fell, so the truth she wanted is already here
- Tomas: came wanting "Keep the truth of his flight hidden and keep his place in the household."; leaves feeling hollowed, exposed, wanting "Same: hear Ilsa's judgment and bear it without flinching.", hiding "that he cannot remember Berold's voice at all".
  - beat 1: Ilsa's demand for names makes the lie of his presence at Berold's side impossible to keep; the room is about to be asked who stood where.
  - beat 2: Arwa named him at Berold's right hand, so the account he meant to follow now places him at the death he fled.
  - beat 2: Ilsa's gaze lifting to him and asking who stood at Berold's left ,  the question is aimed past Arwa, at him.
  - beat 2: Edric turning his cup and setting it down untouched, the confessor knows, and Ilsa's question about the left has put Tomas's own lie in his mouth.
  - beat 2: Ilsa's question turns from the battle to what he saw, and Edric's untouched cup sits beside her ,  the ground under his account narrows.
  - beat 3: Arwa's demand for the truth of how the line broke turns his evasion into a trap, since any honest answer exposes his flight.
  - beat 3: Arwa's demand for the truth and Edric's turned cup make silence feel like confession already.
  - beat 3: Arwa's demand for the truth and Ilsa's question about the ford make evasion impossible; his old tactic is failing.
  - beat 3: Arwa's demand for the truth and Ilsa's narrowing questions make evasion feel like a wall closing in.
  - beat 4: The boy's question and Arwa's flat demand strip away his last shelter, so evasion now feels worse than confession.
  - beat 4: The boy's question and Arwa's demand forced the confession out; he has stopped evading.
  - beat 4: Ilsa turning to her son instead of answering him leaves him suspended ,  her silence may be mercy or sentence, and he cannot tell.
  - beat 5: The clean sword and the boy's question strip away his last hope of being mourned as a loyal knight; he no longer braces for judgment, he expects it.
  - beat 5: Ilsa's question about Berold's last words turns the confession from a thing he chose into a thing being taken from him.
- Edric: came wanting "Keep the peace of this hall and the seal of confession."; leaves feeling pierced, still, wanting "Keep the peace of this hall, now that the truth is out and no innocent need take the blame", hiding "that he heard this same confession last night and could not say it".
  - beat 1: Arwa's demand for the names nearest Berold at the ford makes the sealed confession press against the peace he is trying to keep.
  - beat 2: Arwa's naming of Tomas turned his fear into a present danger.
  - beat 2: Arwa's naming of Tomas at the lord's right hand turns the hall's eyes toward the man he cannot defend or accuse.
  - beat 2: Tomas named Gareth at the left, and Edric knows Gareth was not there ,  the lie puts an innocent in the grave's place.
  - beat 2: Tomas's second lie about Gareth's shield hardens Edric's fear that an innocent will hang for it
  - beat 3: Arwa's demand for the truth turned the hall from grief toward judgment, and Edric knows Tomas's next words will be a third lie he cannot correct.
  - beat 3: Arwa's demand that Tomas tell how the line broke pressed the truth toward the open, deepening his fear that silence will let Tomas lie and an innocent take blame.
  - beat 3: Arwa's demand for the truth and Tomas's evasions have pushed the hall toward the moment Edric cannot prevent
  - beat 3: Arwa's demand for the truth and Ilsa's narrowing questions turned his dread into a live fear that Tomas will lie under oath
  - beat 4: Tomas's confession spoken aloud in the hall frees Edric from the seal's weight, and the boy's question made the cost plain

