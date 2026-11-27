# The widow's hall (drama), simulated
Inner lives off, dramaturge off (old scene reader).
Prose deepseek/deepseek-v4-pro reasoning off (19 calls), NSFW fallback x-ai/grok-4.20 (0 calls), dramaturge on the prose model (4), judge deepseek/deepseek-v4.1-flash (35), inner lives (0), interpreter deepseek/deepseek-v4.1-flash (10). Cost: prose+dramaturge $0.0117, judge+minds+interpreter $0.0151. 0.7 min. Wait before each NPC line: mean 2.0 s, longest 7.8 s.
Arwa (the player: first line scripted, then played by the prose model). Ilsa: Learn exactly how Berold died and who failed him, tonight.; Tomas: Keep the truth of his flight hidden and keep his place in the household.; Edric: Keep the peace of this hall and the seal of confession.

*The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.*

## The scene
**Arwa** *(to Ilsa)*: My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.

> *Tomas pours himself a cup of wine, his hand steady as a stone.*

**Ilsa** *(to Arwa)*: *accepts the sword, sets it across her lap without looking down* Tell me of the end, Commander. Every detail.

**Arwa** *(to Ilsa)*: *stands at attention, hands clasped behind back* He took an arrow in the throat. It was quick. He did not suffer.

**Ilsa** *(to Arwa)*: *her fingers find the notch in the blade, still dark with old blood* An arrow in the throat. Who loosed it, and from where?

**Arwa** *(to Ilsa)*: *holds Ilsa's gaze without flinching* A rider on the far bank. I did not see his face.

**Ilsa** *(to Arwa)*: *her thumb stops on the notch* You did not see his face. And where was Tomas, my husband's sworn knight, while a rider on the far bank took aim?

**Tomas** *(to Ilsa)*: *Sets the cup down untouched.* My place was at his side. I was not there at the end.

**Ilsa** *(to Tomas)*: *she does not look at him* Not at his side. You were his sworn shield, Tomas. Where were you?

> *Tomas lifts the cup and drinks, a single swallow that takes too long to finish.*

**Arwa** *(to Tomas)*: *turns her head slightly toward Tomas, voice level* The knight held the left flank. I ordered him there.

> *Ilsa turns the sword over on her lap, aligning the blade precisely with the seam of her gown.*

**Tomas** *(to Ilsa)*: *Sets the cup back on the table, precise.* The flank folded. I could not hold it alone.

**Ilsa** *(to Arwa)*: *The sword does not move.* You ordered my husband's sworn shield away from him. Why?

> *Tomas turns to the window, his reflection a pale ghost in the dark glass.*

**Arwa** *(to Ilsa)*: The left was breaking. If it fell, the ford was lost and every man in it. Your husband knew. He gave the order with me.

**Ilsa** *(to Tomas)*: *still not looking at him* You came back alive. The flank fell. My husband lies in the crypt.
  `actions: change_relation(delta=-2)`

**Tomas** *(to Ilsa)*: *Does not turn from the window.* I ride to the chapel. I will keep vigil tonight.
  `actions: end_conversation`

**Ilsa** *(to Tomas)*: *rises, the sword still flat across her hands* No. You will kneel before that shield and tell me why my husband died without his sworn sword at his back.

> *Tomas keeps his back to the room, but his hand rises once to touch the scar beneath his jaw, then drops.*

## Why each turn went as it did (judge -> referee, scene reader)

**Beat 1**
- after Arwa: Ilsa Low [to] (held-back anger); Tomas None (held-back shame); Edric None => Ilsa
- after Ilsa: Tomas None; Edric None => BackToPlayer

**Beat 2**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa High [to]; Tomas None; Edric None => Ilsa
- after Ilsa: Tomas None; Edric None => BackToPlayer

**Beat 3**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa Urgent [to] (held-back fury); Tomas None; Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => Tomas
- after Tomas: Ilsa Urgent [to]; Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => BackToPlayer

**Beat 4**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa Urgent (held-back fury); Tomas Urgent [to] (held-back shame); Edric None => Tomas
- after Tomas: Ilsa High [to] (held-back anger); Edric None => Ilsa
- after Ilsa: Tomas Urgent->High (held-back shame); Edric None => BackToPlayer

**Beat 5**
- scene reader: tension rising, stuck no
- after Arwa: Ilsa High [to]; Tomas None; Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => Tomas
- after Tomas: Ilsa Urgent [to] (held-back fury); Edric None => Ilsa
- after Ilsa: Tomas Urgent [to] (held-back shame); Edric None => BackToPlayer

**Who each character became**
- Ilsa: came wanting "Learn exactly how Berold died and who failed him, tonight."; leaves feeling as the scene opens, wanting "Learn exactly how Berold died and who failed him, tonight.", hiding "It was you who urged Berold to answer the summons and ride to the ford; he had wanted to wait for his levies. Nobody here knows.".
- Tomas: came wanting "Keep the truth of his flight hidden and keep his place in the household."; leaves feeling as the scene opens, wanting "Keep the truth of his flight hidden and keep his place in the household.", hiding "You fled the ford before Berold fell. You confessed it to Father Edric last night.".
- Edric: came wanting "Keep the peace of this hall and the seal of confession."; leaves feeling as the scene opens, wanting "Keep the peace of this hall and the seal of confession.", hiding "Tomas confessed to you that he fled the battle. The seal binds you: you will not reveal it, nor lie about it.".

