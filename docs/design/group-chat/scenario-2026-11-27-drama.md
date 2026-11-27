# The widow's hall (drama), simulated
Prose deepseek/deepseek-v4-pro (21 calls), NSFW fallback x-ai/grok-4.20 (7 calls), judge deepseek/deepseek-v4.1-flash (37), interpreter deepseek/deepseek-v4.1-flash (11), scene reader (4). Judge+interpreter+reader cost $0.0123. 15.1 min.
Arwa (the player: first line scripted, then played by the prose model). Ilsa: Learn exactly how Berold died and who failed him, tonight.; Tomas: Keep the truth of his flight hidden and keep his place in the household.; Edric: Keep the peace of this hall and the seal of confession.

*The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.*

## The scene
**Arwa** *(to Ilsa)*: My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.

> *Tomas's fingers curl into his palm, his knuckles whitening as he stares at the ground.*

**Ilsa** *(to Tomas)*: *I take a step toward him, my gaze not leaving his face.* Sir Tomas. You were his shield-bearer. How is it that my husband held the line alone, and you stand here clenching your fist at the stones?

**Tomas** *(to Ilsa)*: *His voice is rough with shame, and he does not lift his eyes from the stones.* My lady, I lost sight of him when the line broke. I should have been at his side. I will carry that failure to my grave, if you will let me remain to serve this house.

**Arwa**: to Ilsa: *Arwa steps forward and lays the sword gently on the table before Ilsa, her hand lingering on the hilt.* My lady, the blade is home. But the full account of his death is not for every ear, I ask to speak with you alone, before this hall hears more.

> *Tomas's breath catches, a shallow hitch that he smothers before it can become a sound.*

**Ilsa** *(to Arwa)*: *My hand rests on the back of my husband's empty chair.* You will speak here, Commander, where his shield can hear you. Sir Tomas and Father Edric will stay. If there is shame to be borne, let it be borne openly.
  `actions: request_privacy(result=refused)`

**Tomas** *(to Arwa)*: Commander, you saw me at the ford. I beg you, let the words come from my own mouth. I would not have my lady hear them from another.

**Arwa** *(to Tomas)* [NSFW model]: Speak it yourself, then, as you asked of me.   *I turn slightly toward Ilsa, acknowledging her will while my gaze stays level on the knight.* Tell your lady exactly how her husband died at the ford, and why you did not die beside him.

**Ilsa** *(to Tomas)* [NSFW model]: *My gaze shifts from Arwa to Tomas, grief sharpening into something colder.* Before one word leaves your mouth, swear on this sword and to Father Edric that you will hide nothing of what you saw at the ford. I will have the truth entire, not a knight's edited confession.

**Tomas** *(to Ilsa)* [NSFW model]: *I lift my eyes at last, not to the sword but to Father Edric, my shoulders rigid beneath the weight of every candle's glare.* Father, you hold what I confessed last night. Tell my lady if the seal of that hour permits me to speak without fresh sin.  I will walk barefoot to the shrine of St. Alaric or yield my spurs forever if it redeems my place in this hall.

> *Edric's spine straightens, his hands clasping behind his back in a single, precise

**Arwa** *(to Edric)* [NSFW model]: *My gaze holds steady on the priest while the candles flicker low across Lord Berold's empty chair.* The seal of confession cannot outweigh a widow's rightful claim in her own hall, Father. Loose Tomas to speak the whole of it, or I will finish what the ford began and name the hand that truly failed my lord.

> *Tomas's hand lifts to his collar, tugs once at the fabric, then drops again as his gaze fixes on the priest.*

**Edric** *(to Tomas)*: *His voice is quiet, but it carries through the hall.* "The seal binds my tongue, Sir Tomas, not yours. What you speak now is between you and your conscience, and your lady. I can neither give you leave nor withhold it."

> *Ilsa's jaw tightens, the pulse at her throat beating visibly against the stillness of her posture.*

**Tomas** *(to Ilsa)* [NSFW model]: *I draw a slow breath that does not steady the tremor in my clasped hands, meeting her gaze for the first time since the sword was laid down.* My lady, the command at the ford failed us all before any man broke. Name what atonement you require of me and I will perform it here under his shield, if only this household will still have my sword arm in the trials ahead.

**Ilsa** *(to Tomas)* [NSFW model]: *I lift my husband's sword from the table, its hilt cold against my palm.* Then speak the name of the command that failed him, Sir Tomas. Was it hers, or did another hand send him to hold that ford while you survived it?

**Arwa** *(to Ilsa)*: *I meet her gaze over the bare blade, my voice low but carrying.* The command was mine, my lady, I gave your husband the ford, and I would give the same order again. The failure was not in the order, but in the shield that broke behind him. Your husband held the line; the man who should have guarded his back did not.

> *Tomas's neck cords sharply, a single vein pulsing hard beneath the skin as his ears flush dark.*

**Ilsa** *(to Tomas)*: *I lift the sword once more, the point drifting toward you like a needle seeking the heart of the truth.* "You were his shield-bearer, Sir Tomas. Tell me the manner of his death, what weapon struck him, who saw him fall. If you cannot, then you have already named your own failure, and there is no need for an oath."

**Tomas** *(to Ilsa)*: *I sink to one knee on the cold stone, my voice fraying into something barely above a whisper.* I cannot name the weapon that struck him, my lady, I was not there to see it. Let me stay within these walls as the least of your servants, so I may look upon his shield every day and never forget the weight of what I lost.

## Why each turn went as it did (judge -> referee, scene reader)

**Beat 1**
- after Arwa: Ilsa Low [to]; Tomas None (held-back shame); Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => Tomas
- after Tomas: Ilsa High [to]; Edric None => BackToPlayer

**Beat 2**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa High [to] (suspicion); Tomas Urgent (dread); Edric None => Ilsa
- after Ilsa: Tomas Urgent->High [about] (dread); Edric None [about] => Tomas
- after Tomas: Ilsa High [about]; Edric None => BackToPlayer

**Beat 3**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa Urgent [to]; Tomas Urgent [to]; Edric None => Ilsa cuts off Tomas
- after Ilsa: Tomas Urgent [to]; Edric None [about] => Tomas
- after Tomas: Ilsa High [to]; Edric Low [to] (unbending) => BackToPlayer

**Beat 4**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa High [about]; Tomas Urgent [about] (cornered dread); Edric Urgent [to] (held-back anger) => Edric
- after Edric: Ilsa Urgent (held-back anger); Tomas Urgent [to] (cornered dread) => Tomas
- after Tomas: Ilsa Urgent [to] (cold fury); Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (cornered dread); Edric None => BackToPlayer

**Beat 5**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa Urgent [to] (cold fury); Tomas Urgent (held-back fury); Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => Tomas
- after Tomas: Ilsa High [to]; Edric None => BackToPlayer

