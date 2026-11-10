===== MESSAGE 1 =====
# 🕰️ Tutorial: write your campaign's past with the History Editor

**What is it for?**
A new campaign starts with a world that has no past: no old wars, no betrayals, no grudges. The History Editor lets you **write events that happened before day 1** of your campaign, and the characters will **remember them** as if they had lived them.

- A lord's father fell at the siege of a town ten years ago. Mention that town, and he remembers.
- Your character and a lady were betrothed and she broke it off. She knows. So do her kin.
- A secret: two lords plotted together. Only they and the people you choose know it.
- **Starting relations**: you can make two heroes enemies or friends from the start, including with your own character.

**How do characters use it?**
When you **name** a person, a place or a realm in a conversation, the character is given the one to three events about it that they would know, with how long ago it was, and whether they lived it or only heard of it. Only a name brings the past up, never a loose keyword.

**Who knows an event?** Those who took part, those it harmed, their family and their house, the people of the place. With an **importance of 45 or more**, the whole of the realms you named know it too. A **secret** event is known only to the actors, the harmed, and the confidants you pick.

The tool ships with the mod: `Modules/CalradiaRemembers/Docs/HistoryEditor.html`. Double-click it, it opens in your browser and works offline. **Chrome or Edge recommended**: they can save straight into the right folder.

===== MESSAGE 2 =====
## Step 1: get your campaign's files

Each campaign has its own history. In the game, load your save and open the console (`Alt + ~`):
`cr.history_file`
It writes two files and prints where they are:
`Documents/Mount and Blade II Bannerlord/CalradiaRemembers/History/`
- `<campaign>.world.json`: a **snapshot of your world** (heroes, houses, realms, towns, the calendar, and what the game has already recorded). The editor reads it so you can pick people from lists, and so it can check you are not writing something impossible. It is refreshed at every save.
- `<campaign>.history.json`: **your history**, empty for now. The mod reads it but never writes over it: it is yours.

## Step 2: open them in the editor

Click **Open world snapshot** and pick the `.world.json`, then **Open history file** and pick the `.history.json`. You can also drag them onto the page.

You now see a **timeline**. A marked line is the day your campaign began: everything you write must be **before** it. The grey marks are events the game recorded itself (read only), so you do not write a past that contradicts them. Zoom with **Decades / Years / Seasons / Fit**, and filter by hero, realm, place or kind at the top.

===== MESSAGE 3 =====
## Step 3: add an event

Click **Add event** (choose the year first), or **double-click the timeline** where you want it. A form opens on the right:
- **When**: year and season.
- **Kind**: battle, siege, fief taken, execution, fell in battle, marriage, divorce, war, peace, rebellion, scheme, capture, betrayal, feud, kindness, debt, rescue, duel, other.
- **Summary**: *the line characters will remember*. Write it like a chronicle: *"At the siege of Pravend, Lord X's father was cut down on the walls."*
- **Actors**: who did it. **Harmed**: who suffered it. Type a name and pick from the list: the editor shows each hero's house and life years.
- **Place** (optional) and **Realms** it concerns.
- **Importance** (0 to 100): 45 and up, the realms named all know it. Below, only those tied to it.
- **Secret**: tick it, then add the **Known by** confidants.

**Starting relations** (bottom of the form): pick two heroes and a value from -100 to 100.
- **Change** adds to the relation the game gave them (*-40: they never forgave each other*).
- **Set** is the value they start at.
If one of them is your character, their personal regard for you moves by the same amount.

===== MESSAGE 4 =====
## Step 4: fix what the editor flags

The editor checks every event live, with the same rules as the game. An event in red will be **set aside** by the game until you fix it. The **Problems** panel at the bottom lists them all. The usual ones:
- the event is dated **on or after** the start of the campaign (the game owns that time);
- a hero **was not yet born** that year;
- an **execution** or **fell in battle** must name its dead among the harmed, and that hero must be **dead today** (otherwise the encyclopedia would say he is alive);
- the event names **no actor and no harmed** hero;
- a place, realm or house that does not exist in this campaign.
A yellow warning is kept, just told (e.g. a marriage between two people who are not married today: they may have been widowed).

## Step 5: save and load

Click **Save** (or `Ctrl + S`). In Chrome or Edge, pick the `.history.json` in the `History` folder and it is overwritten in place. In other browsers it downloads: move it into that folder yourself, with the same name.

Then in the game: `cr.history_reload` reads it at once (it is also read at every load). If the game set something aside, it says why in `<campaign>.history.problems.txt` next to it.
To check: `cr.history` lists the world's memory (your events carry an `authored` tag), and `cr.history Name` shows only what that character would know.

===== MESSAGE 5 =====
## Good to know

- **Edit freely.** Each event keeps its **id**: change it, delete it, reload, and the game updates or removes it instead of piling up copies. Relations work the same way: changing -40 to -60 moves the pair by -20 only, and deleting an event takes back what it gave. What the campaign did to the pair since is kept.
- **An older save of the same campaign** reads the same file, so your past stays the same whichever save you load.
- **No file, no change.** A campaign without a history plays exactly as before.
- Your events are never pushed out of the world's memory, however long you play.
- Name your character in the history like any other hero: pick them from the list.

Show us the past you wrote for your Calradia! 🛡️
