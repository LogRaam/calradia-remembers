===== MESSAGE 1 =====
# 🧠 Long Memory: what it does, and what we measured

**The problem.** Goeth put it best: *"I had a 150-line night in a tavern with a companion, full of teasing, questions traded back and forth, and promises. The next time we talked she remembered it in two lines. Her attitude shifted, and she lost track of who had asked for what."*

By default, a character keeps a **summary** of each conversation in their history. A summary keeps the gist, but loses the details that make a relationship feel real: the inside joke, the exact promise, who owes what to whom.

**What Long Memory does.** Calradia Remembers keeps the **exact words** of every conversation in a story archive. With **Long Memory** on, before each reply the character looks back through the talks **they were present for** and brings back the passages that bear on what you just said. They are not reciting a log: they remember the moment because you touched on it.

It also follows **threads**. When a matter comes up over several talks (a task, a reward, a deadline that changed), the character pulls the whole thread and knows which talk is **the latest**, so they answer with how things stand now, not with terms you renegotiated a week ago.

===== MESSAGE 2 =====
## 📊 The measurements

We ran the same scenes against a real model (GLM 4.6, reasoning off), three times per question, **with and without** Long Memory.

**Test 1: the long night.** A companion, Ira, spent a long night at a tavern with the player: a joke about the company goat that ate the captain's boots, her promise to teach the player the longsword, the player's promise of a red cloak from Pravend, and a confidence (she is afraid of horses). Twelve days later, the player brings each one up.

- **Without Long Memory: 0 of 12 right.** Worse, she **invented**: the goat "charged you", a table was smashed at the tavern, none of which happened.
- **With Long Memory: 9 of 12 by the automatic count, about 11 of 12 read by hand.** ("Captain Goat. Barefoot in the mud for a whole week" is the right memory, even if the word "boots" is missing.)

**Test 2: a mission that changed.** A lord gave a task over three talks: clear the bandits near Varcheg for 1,000 denars, then burn their stockade for 1,500, then, after they took his nephew, bring the boy back alive for 2,000 and a horse, before the first snows. The player then asks about the reward, the deadline, the task.

- **Without: 1 of 12** current terms.
- **Word matching alone: 3 of 12, and 3 answers with old terms.**
- **With threads (what ships): 12 of 12, and no old terms.**

===== MESSAGE 3 =====
## 🍂 Does it drag the past into everything?

That was the real worry: a character who brings up the bandits when you only said "fine weather today".

We tested exactly that. On an ordinary line, the character with Long Memory was even handed three passages of the old mission, and **stayed on the moment 3 times out of 3**. The character **without** Long Memory brought the bandits up **2 times out of 3**, from their ordinary history. The exact words make the character more precise, not more fixated.

## 🎮 What it feels like in play

- A companion **laughs at the same joke** you shared weeks ago, in her own words.
- A promise is **remembered as it was made**: who promised what, to whom, by when.
- A lord who changed the terms of a task **gives you the new terms**, not the old ones, and does not make up a third set.
- What a character was **not present for** stays unknown to them. Overheard talk follows who was in the room.
- **Load an older save** and the characters forget what was said after it: the archive follows your save, per campaign.

===== MESSAGE 4 =====
## ⚙️ How to turn it on

Mod options (MCM), **Content** group:
- **Long Memory**: off by default. Turn it on.
- **Long Memory Budget (characters)**: how many characters of earlier words one reply may recall, at most. **4000** (the default) is about **1,000 tokens per reply** at most; in our tests a recall used about 1,500 characters. Raise it for long campaigns on a large model, lower it to save tokens.

**Good to know**
- It works with the **full prompt**. With **Compact prompt for small models** on, it stays silent.
- The recalled words change with each line you say, so they are billed fresh every reply (they sit after the cached part of the prompt).
- The words are always kept, even with Long Memory off: the setting only decides whether characters use them. Turn it on later in a campaign and they remember every talk since you installed 2.8.15.
- These numbers come from one model and a few draws each, measured outside the game. Your model may do better or worse; tell us what you see.

Thanks to **Goeth** for the idea, and for building the first version of it.
