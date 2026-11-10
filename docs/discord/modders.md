===== MESSAGE 1 =====
# 🔌 Tutorial for modders: plug your mod into Calradia Remembers

**Why?**
Your mod changes the world: a siege, a festival, a new kind of quest, a rebellion. With Calradia Remembers next to it, the characters can **remember it, speak of it, and act on it**. CR gives other mods public, stable doors for that, and every one of them is checked at the door: a mistake on your side is refused and logged, never a crash.

There are two kinds of doors:
- **Data doors, no code.** Files your module ships in its own `ModuleData/`. CR reads them from every enabled mod, in launcher order. Your players copy nothing, and disabling your mod takes the content away with it.
- **C# doors.** A few static classes in `CalradiaRemembers.dll` for anything that happens at runtime.

**The guides ship with the mod** in `Modules/CalradiaRemembers/Docs/`:
`EXTENSION_API.md` (the full reference), `HOWTO_WorldEvents.md`, `HOWTO_Actions.md`, `HOWTO_Voicing.md`, `HOWTO_Quests.md`.
This post is the map; they are the territory.

===== MESSAGE 2 =====
## Data doors (no code)

In `YourModule/ModuleData/`:
- `cr_patch/<file>`: add to CR's prompt files (`world.txt`, `behavior_guidelines.txt`, `stations/<station>.txt`...). Your lines come after CR's. Directives: `!remove <text>`, `!replace` (a total conversion), `!block Vlandia` (rewrite one people's block), `!#` (a comment the model never sees).
- `cr_scenarios/*.json`: scenario rules, made with the Scenario Composer.
- `cr_quests/`: written tales, made with the Tale Editor, one folder per tale with its pictures.
- `cr_prompts/<profile>/`: prompt profiles, offered in the MCM **Prompt Profile** list.
- `cr_faces.json`: a chosen face for each realm's queen.

**Prefix every id with your module's** (`mymod_siege_song`). The first one read wins, and a later one with the same id is skipped and logged.

## Before any C#
1. In your `SubModule.xml`: `<DependedModule Id="CalradiaRemembers" />` so CR loads first.
2. Reference `Modules/CalradiaRemembers/bin/Win64_Shipping_Client/CalradiaRemembers.dll` (do not ship a copy).
3. Check `CrNpcMemory.ApiVersion` (today **1**): it only changes on a breaking change.
**Want your mod to load without CR?** Every door can be reached by reflection instead of a reference.

===== MESSAGE 3 =====
## The C# doors you will use most

**Tell the world something happened**: `Feed.Record`
```csharp
Feed.Record(new ExternalWorldEvent {
    Id = "mymod_siege_sargot_day412", SourceMod = "MyMod",
    Summary = "The siege of Sargot was broken after forty days.",
    Magnitude = 70, FactionIds = new List<string> { "vlandia" } });
```
Characters who would plausibly have heard it bring it up, as hearsay, by how surely it reached them. `cr.heard Name` says whether a character knows it, and why.

**Give one character a memory**: `CrNpcMemory.AddMemory` (and `GetMemories` to read). They speak of it as their own. `Private = true`: only in private. `Kind = "Hearsay"`: something they only heard.

**Teach the AI a deed of your mod**: `CrActions.Register`. Name, when to do it and when NOT to, parameters, and your own `Execute`. The model learns it, and CR calls you when a character really does it in conversation.

**Listen to conversations**: `CrConversation.Started`, `LineSpoken` (each line as it shows: voice it, caption it), `Ended` (the whole transcript, speaker by speaker).

Call them on the **game thread** (a campaign event, a tick, a menu), never from a background task.

===== MESSAGE 4 =====
## The other doors
- `CrPrompt.RegisterVariable`: a `{{variable}}` filled at every reply, for knowledge that changes all the time.
- `CrLetters.Deliver`: a letter from a lord, in CR's letter window, carried by courier. The player can answer, and the lord replies.
- `CrTales`: follow a written tale's stages and endings. `CrMatches`: hand the player a marriage to broker. `CrPatrols`, `CrAims`, `CrRomance`, `CrPairRegard`, `CrFolklore`, `CrAppearance`, `CrConception`...: each has its section in `EXTENSION_API.md`.
- `CrNpcMemory.GetRegard` and the `RegardChanged` event: read and follow a character's regard for the player.

## Test your wiring
- `cr.api_probe Name`: prints the API version and the save id, injects a test memory on that character and reads it back.
- `cr.heard Name`: whether your world event reached them, and why.
- `cr.scenarios`, `cr.story_check`: your packs and tales, and every problem found in them.

Building something? Tell us here: a door you are missing is worth asking for. 🛡️
