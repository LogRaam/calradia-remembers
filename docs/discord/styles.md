===== MESSAGE 1 =====
# 🪶 Tutorial: choose and write narrative styles

**What is a style?**
A style decides **how** the characters write: the rhythm of the sentences, how much is described, the mood of the prose. It never changes **who** they are, what they know or what they are willing to do: that is decided elsewhere, and always wins.

One style applies to **every conversation** (adult scenes included), and your choice is **kept between sessions and saves**.

**Choosing one in game**
In the chat window, click the **quill** in the header (or the **Style:** button). Pick a style: it applies from your **next message**. From the console (`Alt + ~`): `cr.styles` lists them and marks the active one, `cr.style Martin` picks one.

**What ships with the mod**
- **Descriptive** (no author): **Default** (the mod's ordinary voice), **Novelistic** (a page from a novel), **Terse** (short and fast, for those who hate walls of text), **Grimdark** (the hard, unromantic world).
- **Anchored on an author**: **Austen** (wit and social nuance), **McCarthy** (spare, biblical cadence), **Tolkien** (elevated, deep time), **Martin** (feudal intrigue, sudden violence), **Cornwell** (shield-wall grit), **Abercrombie** (grimdark with dark humour), **Kay** (lyrical melancholy), **Dumas** (swashbuckling honour), **Homer** (epic similes), **Dostoevsky** (feverish moral intensity), **Howard** (pulp action).

Try a few on the same character: the difference is striking.

===== MESSAGE 2 =====
## Writing your own

A style is a plain `.txt` file. **Its file name is its name in the picker**: `Saga.txt` shows up as *Saga*.
1. Go to `Modules/CalradiaRemembers/ModuleData/Styles/` and **copy** a style you like.
2. Put the copy in **`ModuleData/Custom/Styles/`** (create the folder) so updates never touch it, and rename it.
3. Edit it. It appears in the picker right away. A file with the **same name** as a shipped style replaces it. A draft whose name starts with `_` (`_test.txt`) stays out of the picker.

Write in plain English, as instructions ("Write...", "Keep...", "Never..."). **Ten to twenty lines.** It is read with every message, so make it dense, not exhaustive.

**Edit while you play**: save the file, and your next message uses the new rules, no restart. `cr.style_show` prints what is being sent right now, to check your edit took.

**Two ways to describe a voice** (they combine well):
- **Describe** the qualities: sentence rhythm, what is shown, what is withheld, what to avoid. Precise, but it takes lines.
- **Name an author.** Three words carry a whole syntax and rhythm. But: famous authors work, obscure ones give a vague imitation; and a name alone drifts into caricature over a long conversation.

===== MESSAGE 3 =====
## The recipe that works

```
Write with the voice of <author>.
<three to five concrete traits you want: rhythm, diction, what is shown, what is held back>
Keep the mod's conventions: spoken words in quotes, gestures in *asterisks*.
<that author's habits you do NOT want, named plainly>
Never let the manner become a tic: if you used a construction in your last reply, find another.
```
**The last two lines matter most.** The first protects the chat's format; the second keeps a strong voice from turning into a mannerism by the tenth message.

The shipped `Cornwell.txt`, for instance, ends with:
> This is Calradia: use its own cultures and gods, never import Saxons, Danes, or English geography, and do not turn every quiet scene into a battle.

**Variables**: `{{char}}` (the character's name), `{{user}}` (your character's), `{{char_gender}}`, `{{user_gender}}`, `{{user_clan}}`, `{{faction}}`, `{{location}}`, `{{spouse}}`, `{{father}}`, `{{mother}}` are filled in at each reply, so one style fits every character:
> Write {{char}}'s dialogue in short, clipped sentences, and have them address {{user}} by name when the scene turns tense.

===== MESSAGE 4 =====
## Two limits

- **Keep it about the writing.** A style that tries to rewrite a character, hand out secrets or bend a scene's rules fights the parts of the prompt that decide those, and they win: you only muddy the voice.
- **Keep it under about 1500 characters.** Longer, the end is cut off before it reaches the model (1200 at a war council). On a small local model, a bloated style can also crowd out the character. The shipped styles are all well under.

## Good to know
- `ModuleData/Styles/README.txt` has all of this, with the shipped styles described.
- If you set a **Custom Folder Path** in MCM, your styles go in its `Styles` sub-folder (see the Custom folder tutorial).
- Don't want the button in the chat header? `cr.style_ui off` hides it until you restart the game, `cr.style_ui on` brings it back.

Share your best styles here, with a line of what they produce! 🛡️
