# A group conversation, simulated
Judge: deepseek/deepseek-v4.1-flash (44 calls). Prose: x-ai/grok-4.20 (27 calls). Judge cost: $0.0041.
Arwa (the player, scripted), Garios (a proud lord), Ira (a bold mercenary captain), Bryn (a timid squire, the thief).

## The scene
**Arwa** *(to Garios)*: The camp stores were raided last night. Two loaves and a whole ham. Who keeps watch on them?

> *Bryn tugs repeatedly at a loose thread on his sleeve.*

**Garios** *(to Arwa)*: The foot I command set the watch, and my orders were clear. *I glare at Bryn until he freezes.* If some wretch has helped himself, we will have the truth before the sun climbs much higher.

> *Bryn wrings his hands until his knuckles turn white.*

**Arwa** *(to everyone)*: Someone in this camp is a thief. I want a name before supper.

> *Bryn's lower lip quivers.*

**Garios** *(to Bryn)*: *My voice drops to a low growl as I seize Bryn by the shoulder.* Speak, boy. Your sniveling and twitching dishonor my name, did your weakness lead you to the stores?

**Bryn** *(to Garios)*: *I flinch violently from your grip, voice cracking into a whisper.* I... the stores were under my watch, my lord. *Tears spill as I stare fixedly at the dirt, hands twisting together until they ache.*

**Garios** *(to Arwa)*: *My fingers dig into Bryn's shoulder like iron as I drag him forward.* The name is this sniveling wretch Bryn, who has shamed my house. He will taste the lash before the whole camp, and we will speak no more of it.

> *Bryn's knees knock together.*

**Arwa** *(to Ira)*: Your hired blades were near the stores at midnight, weren't they, Ira?

> *Bryn swallows convulsively, his throat bobbing.*

**Ira** *(to Arwa)*: My blades were near the stores, aye, but they steal from no one who pays them honest coin. The boy's already cracked and confessed the watch was his, let Garios flog his own failure and stop sniffing at my camp.

> *Bryn's face turns ashen.*

**Garios** *(to Ira)*: *My lip curls in contempt as I shove Bryn aside and round on the mercenary captain.* Your hired rabble slink near the stores like the thieves they are, and you dare name my squire the failure? Hold your insolent tongue before I forget Arwa's command and teach you the respect due my house.

> *Bryn's brow beads with fresh sweat.*

**Ira** *(to Garios)*: *My fingers tighten on my belt near the dagger as I square up to you.* Try teaching me respect and I'll feed your guts to the crows, lordling. The boy's already weeping out his guilt, flog your failure and keep your sneers off my camp.

> *Bryn's legs buckle beneath him.*

**Arwa** *(to Bryn)*: Bryn, you had the night watch. What did you see?

> *Garios lifts his chin sharply, nostrils flaring.*

**Bryn** *(to Arwa)*: *I collapse to my knees in the dirt, trembling so hard my teeth chatter.* I... the night was full of shadows and my own fear, commander. *I fall silent, eyes fixed desperately on the ground.*

**Garios** *(to Arwa)*: *My jaw clenches as I stare down at the wretched boy, nostrils flaring wider.* The watch was his and his alone, commander, his craven shadows and fear have cost us stores. *I haul Bryn up by the scruff before he can collapse further.* Enough groveling; my house will punish its own failure with the lash at once.

> *Bryn digs his fingernails into his palms.*

**Arwa** *(to Garios)*: Your squire has grease on his sleeve, Garios. Hams are greasy.

> *Bryn's hands flutter at his sides as his breathing quickens while Ira tilts his head, the corner of his mouth curving upward.*

**Garios** *(to Arwa)*: *I snatch Bryn's wrist in a crushing grip and thrust the greasy sleeve toward her, my lip curling in fury at the proof.* The craven stole from us under cover of his own watch, my house's shame will be scourged from his hide before the camp at once. *I shoot Ira a venomous glare.* Wipe that cur's grin from your face, sellsword, or I will do it with steel.

> *Bryn shields his head with both arms.*

**Ira** *(to Garios)*: *I hook my thumbs in my belt and meet your glare with a flat stare.* Your whelp's got ham fat on his sleeve and fear in his eyes, lordling. Flog him and spare us both your wind, I've no patience left for your posturing.

**Garios** *(to Ira)*: *My face purples with rage and I shove Bryn stumbling toward the whipping post.* Then draw that dagger, you gutter-born dog, and I will carve respect into your ribs before the camp. The boy stole under my name, his blood will cleanse it now.

**Arwa** *(to everyone)*: Well? Do I hang someone at dawn, or will the guilty one speak?

> *Bryn bites down hard on his fist.*

**Garios** *(to Arwa)*: *The boy's grease-stained sleeve and craven silence damn him plainly enough, commander.* Bryn is the thief who shamed my house; he will be flogged raw before the camp at once, and that will close the matter without need of the rope at dawn. *I tighten my grip on his collar and drag him upright.*

> *Bryn's shoulders hunch toward his ears.*

## Why each turn went as it did (judge -> referee)

**Beat 1**
- after Arwa: Garios Low [to]; Ira None; Bryn None (nervous) => Garios
- after Garios: Ira None; Bryn None [about] (terrified) => BackToPlayer

**Beat 2**
- after Arwa: Garios Low; Ira None; Bryn None (terrified) => Garios
- after Garios: Ira None; Bryn Urgent [to] (terror) => Bryn
- after Bryn: Garios Urgent [to]; Ira Low => Garios
- after Garios: Ira None; Bryn Urgent->Low [about] (terrified) => BackToPlayer

**Beat 3**
- after Arwa: Garios High; Ira Urgent [to] (held-back anger); Bryn Urgent->Low (terror) => Ira
- after Ira: Garios Urgent [about] (furious); Bryn Urgent->High (terror) => Garios
- after Garios: Ira Urgent [to] (held-back anger); Bryn None [about] (terrified) => Ira
- after Ira: Garios Urgent [to]; Bryn None (terrified) => BackToPlayer

**Beat 4**
- after Arwa: Garios High (contempt for Arwa's coddling); Ira None; Bryn Urgent [to] (terror) => Bryn
- after Bryn: Garios High (contempt for Ira); Ira None => Garios
- after Garios: Ira None; Bryn Urgent->Low [about] (terror) => BackToPlayer

**Beat 5**
- after Arwa: Garios Urgent [to] (dawning fury); Ira Low (amused contempt); Bryn Urgent->Low [about] (terror) => Garios
- after Garios: Ira Urgent->High [about]; Bryn Urgent->Low [about] (terror) => Ira
- after Ira: Garios Urgent [to] (contemptuous fury); Bryn None => Garios
- after Garios: Ira Urgent [to]; Bryn Urgent->Low [about] => BackToPlayer

**Beat 6**
- after Arwa: Garios High (contempt for Ira); Ira None; Bryn Urgent->Low (terror) => Garios
- after Garios: Ira None; Bryn Urgent->Low [about] (terrified) => BackToPlayer

