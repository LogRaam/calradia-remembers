===== MESSAGE 1 =====
# 📜 Tutorial: playing the Tales

**What is a tale?**
A tale is a written quest, in several steps, that a character offers you in conversation. The story is written in advance; the AI gives it its voice. Each character tells their part in their own words, but **the game decides when the story moves on**: no character can skip a step, add a clue or pay a reward by talking.

The mod ships with one: **The Wolf of the Wood**, a werewolf story by a forest village. Others can be added (see the Tale Editor tutorial).

⚠️ **Tales are off by default.** Turn them on in MCM → Calradia Remembers → Content → **Authored Quests (experimental)**.

**How a tale finds you**
- A character who can give one raises it in about **one conversation out of three**. You talk as usual; they bring it up.
- When the conversation ends, a window asks: **Take it on** or **Not now**. Nothing starts without your yes.
- Declined, they do not raise it again for a season.
- **One tale at a time.** A character who already has another task for you does not raise one.

===== MESSAGE 2 =====
## ➡️ How the story moves on

Each tale is a chain of **steps**. Each step has **one thing to do**, and the game watches for it:
- **talk to** someone (the giver, or another character),
- **question the people** of a village (talk to a few of its notables),
- **go** somewhere, **wait** some days, **bring** goods,
- **keep watch** at night near a place,
- **explore** a scene told by a narrator (see the next message),
- **defeat a band** on the map, in a real battle.

**Where to look**
- The **journal** (Quests) always shows the step you are on, in a line of the story.
- The tale's **place is marked on the map**, like a vanilla quest.
- When a step is done, a notice says **"Step complete"** and the next one begins.
- Stuck? The console command `cr.story_status` says where the tale stands.

**Talk naturally.** The characters of the tale know their part, and only their part: the villagers believe what villagers believe, and nobody knows what comes next. Ask them, press them, follow up later: they remember what you said.

Some tales have **choices**: tell the lord or keep silent, go left or right. What you do or say takes one way, and the story goes on along it, sometimes to a different ending.

**Time:** some tales have a limit in days. Run out and the tale fails.

===== MESSAGE 3 =====
## 🌲 Scenes: when a narrator takes over

Some steps are a **scene**: the chat opens by itself with a **Narrator** instead of a character, and you play the moment yourself ("you follow the tracks...").
- If you are in another conversation when it comes due, it waits and opens as soon as that conversation ends.
- Write what you do, in your own words. Under each narration, a short list offers a few ways; type its **number**, or choose **Something else...** to write your own.
- The narrator only describes; **you decide** what your character does. What is to be found is found only if you go to it.
- The narrator knows **what you carry** (your weapons, armour and mount): you can use those, not a weapon you do not have. Someone in the scene may lend you something; it is yours for the scene only.
- It also knows **your companions** and the **lords and ladies nearby** (the lord of the nearest place, those staying in the nearest town). Name one and the people of the scene take them for that very person (from 2.8.17).
- In a tale whose author allows it, you may **send for your companions and men** in a scene. They come in the telling only: they help, but nothing that happens to them is real (no one is hurt or lost), and a fight stays yours. Most tales are written for you alone, and there your party stays away (from 2.8.18).
- Some clues are **needed**. When you have found them all, the chat says so: **"Everything this place had to show you is found. You may end the scene when you wish."** Close the scene then and the story goes on.

## 🚪 Leaving and coming back

**In a conversation step:** leave whenever you like. The tale waits in your journal and in your save. Speak with the character again later, today or in ten days, and the story is where you left it. They remember what was said.

**In a scene:** closing the scene **ends it**: it does not pause.
- The clues you did not reach are **told to you in a window** as it closes, so you never miss the story itself, only how you found it.
- Then the tale moves to its next step.
So play a scene to the end when you can, or close it on purpose when you are done with it.

**Saving and loading:** the tale lives in your save. If the game closes in the middle of a scene, the scene opens again by itself after you load, since its step is not done.

===== MESSAGE 4 =====
## ⛓️ Special cases

- **Captured during a tale?** Another tale may start in captivity (a night with your captor, for example). Your first tale is **set aside, its clock stopped**, and picks up again where it was once you are free.
- **A night or a long talk:** some steps are one conversation that has its own pace; the chat closes softly at its last moment. Leaving early ends that moment, and the story goes on with what happened in it.
- **The place is lost** (the village burned, or no longer held by the giver's house): the tale ends and its journal entry closes.

## 🛠️ If a tale truly gets stuck
- `cr.story_status`: where the tale stands, who and where it is bound to.
- `cr.story_step`: completes the current step as if it were done. Use it only to get past a real problem.
- `cr.story_abandon`: sets the tale aside, with no reward and no penalty.
And send the report (the **Report** button in the chat header) so it can be fixed.

Enjoy the story! 🐺
