# How to register an external action (Calradia Remembers)

Calradia Remembers lets the LLM emit `[ACTION]` directives mid-conversation: change_relation,
give_gold, join_clan, and so on, all built in. `CrActions.Register` is the public door for a
third-party mod to add ONE OF ITS OWN executable deeds to that same vocabulary, in a single call,
so the LLM can be taught it, and CR will dispatch it, exactly like a built-in verb.

This replaces reflection or a Harmony patch into CR's own catalog: you no longer need to reach
into CR's internals to teach the model a deed your own mod owns.

Available from Calradia Remembers (added after 2.3.1).

---

## 1. Requirements and setup

1. Your module needs Calradia Remembers present and loaded first:

   ```xml
   <DependedModules>
     <DependedModule Id="CalradiaRemembers" />
   </DependedModules>
   ```

2. Reference `CalradiaRemembers.dll` (it ships in
   `Modules/CalradiaRemembers/bin/Win64_Shipping_Client/`). The types you use,
   `CalradiaRemembers.CrActions`, `CalradiaRemembers.ExternalActionSpec`,
   `CalradiaRemembers.ExternalActionParam`, and `CalradiaRemembers.ActionRegistrationResult`, are
   public. You will also touch two engine-agnostic SDK shapes,
   `NpcMemoryService.Core.Extension.VerbFacts` and `NpcMemoryService.Core.Extension.ActionRequest` /
   `ActionResult`.

3. Call `CrActions.Register` once, early (your `OnGameStart` or `OnSubModuleLoad` is fine). Keep the
   returned `ActionRegistrationResult` around, or at least check it, so you know whether your
   action actually made it in.

---

## 2. Registering an action

```csharp
using CalradiaRemembers;
using NpcMemoryService.Core.Extension;

var spec = new ExternalActionSpec
{
    Name = "summon_caravan",
    Description = "The NPC arranges for one of your own caravans to be escorted safely to you.",
    WhenToEmit = new[]
    {
        "the NPC actually arranges the escort in this reply, not merely offers to think about it"
    },
    WhenNotToEmit = new[]
    {
        "the NPC merely discussing or considering the request, without actually arranging anything",
        "a future or conditional promise ('once I have the men to spare') rather than it happening now"
    },
    Parameters = new[]
    {
        new ExternalActionParam("caravan_id", "which of the player's caravans is escorted")
    },
    SourceMod = "MyBridgeMod",
    Execute = request =>
    {
        // request.Facts.NpcId is a plain Hero StringId: re-resolve YOUR OWN Hero from it.
        Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == request.Facts.NpcId);
        if (npc == null)
            return new ActionResult { Success = false, Message = "the NPC could not be found" };

        // Do your own mod's engine work here. CR never touches your state.
        bool ok = MyCaravanService.ArrangeEscort(npc, request.Parameters);

        return new ActionResult
        {
            Success = ok,
            Message = ok ? "A caravan sets out under escort." : "No caravan was available to escort."
        };
    }
};

ActionRegistrationResult result = CrActions.Register(spec);
```

`Register` returns:

- `Registered`: your action is now taught and dispatched.
- `RejectedInvalid`: `spec` was null, `Name` was blank, or `Execute` was null. Nothing was registered.
- `RejectedDuplicate`: `Name` collides with a built-in verb, an already-registered external action,
  or a reserved chat-flow control name (`end_conversation`, `witness_leaves`, `request_privacy`,
  `retire`, `challenge_duel`, `release_player`, `deliver_prisoner`). Nothing was registered.

Registration never throws: a malformed spec, or an unexpected fault mid-registration, is logged and
rolled back so your action never lingers half-registered.

---

## 3. `ExternalActionSpec` fields

```csharp
string Name;                                  // REQUIRED: the [ACTION] "type:" the LLM emits
string Description;                           // one-line factual description of the deed
IReadOnlyList<string> WhenToEmit;             // teaches the interpreter what this deed looks like
IReadOnlyList<string> WhenNotToEmit;          // teaches the false positives to withhold on
IReadOnlyList<ExternalActionParam> Parameters; // named parameters the LLM fills in
Func<VerbFacts, bool> IsEligible;             // optional: is this action currently eligible?
Func<VerbFacts, string> TeachingText;         // optional: extra per-turn conditional teaching
Func<ActionRequest, ActionResult> Execute;    // REQUIRED: does your mod's own engine work
string SourceMod;                             // your mod's id, for the logs
```

- `WhenToEmit` / `WhenNotToEmit` follow the same "tells / anti-patterns" shape CR's own built-in
  verbs use: concept-level cues for how the deed reads in the NPC's prose, not lexical triggers.
  A clear pair here is what keeps the interpreter from firing your action on a mere discussion or a
  future promise.
- `Description`, `WhenToEmit` and `WhenNotToEmit` are fixed text on purpose: they form the ONE catalog
  entry every character is taught, kept in the cached part of the prompt. Wording that depends on who
  is speaking ("her sword", "his men") belongs in `TeachingText`, which runs each turn with that
  character's `VerbFacts` (`IsFemale` included); keep the catalog entry neutral ("their").
- `IsEligible` and `TeachingText` are optional. Your catalog entry is taught once registered,
  whatever they say; supply them to add per-turn teaching only while some condition of your own
  holds (see 4a for where each part goes).
- `Execute` gets an `ActionRequest` carrying `VerbName`, the `[ACTION]` block's raw `Parameters`
  (a `IReadOnlyDictionary<string, string>`, exactly as the LLM emitted them), and `Facts` (a
  `VerbFacts`: `NpcId`, `RelationToPlayer`, `FactionId`, `IsLord`, `IsPrisoner`, `IsFemale`). Everything is a
  plain id or value, never a live `Hero` or a CR type. Re-resolve your own `Hero` from `NpcId` and
  do whatever engine work your mod needs entirely on your own side; CR never reaches into it.

---

## 4. What registering wires up for you

One `CrActions.Register` call reaches every place CR itself teaches and dispatches an action:

- **The Action Interpreter catalog**, so the interpreter can recognise your deed happening in an
  NPC's free-form reply.
- **The 1:1 prose prompt**, so a plain conversation (not routed through the interpreter) is taught
  your action's vocabulary too.
- **Dispatch**, via `BannerlordGameStateBridge.ExecuteAction`: an `[ACTION]` type it does not itself
  recognise falls through to the shared verb registry, which calls your `Execute` callback.

In normal play, with nothing registered, every prompt and dispatch path is unchanged: registering
is strictly additive.

### 4a. Where each part goes, and what it costs

No field has a hard length limit. Everything you write is read by a model on every turn it reaches,
so size it like CR sizes its own verbs:

| Part | Reaches | When |
|---|---|---|
| `Description`, `Parameters` | the Action Interpreter's catalog, and the GAME ACTIONS index of the 1:1 prose prompt | every interpreter call; every 1:1 turn (cut to 90 characters in the Compact prompt) |
| `WhenToEmit`, `WhenNotToEmit` | the Action Interpreter's catalog only | every interpreter call in the full prompt; the Compact prompt drops them |
| `TeachingText` | the 1:1 prose prompt only | each turn while `IsEligible` holds (until 05/10/2026 it reached no prompt at all) |
| `Guidance` | the 1:1 prose prompt only | EVERY turn, whatever `IsEligible` says |

- `IsEligible` gates `TeachingText` and nothing else: the catalog entry is always taught, and
  `Execute` is called without asking `IsEligible` again. Re-check your own conditions in `Execute`.
- Return `null` from `Guidance` unless your deed is plausibly in play: it is paid for on every turn
  of every conversation.
- Sizes that work: a one-sentence `Description` with its point in the first 90 characters; two or
  three `WhenToEmit` / `WhenNotToEmit` lines of one sentence each; a `TeachingText` of a few lines
  (a few hundred characters) holding the `[ACTION]` block and its conditions. For scale, CR's own
  verbs average about 105 characters each in the GAME ACTIONS index, and the interpreter's whole
  catalog is tens of thousands of characters, cached by providers that cache; the tight one is the
  Compact prompt, built for small local models.
- Register before the first conversation of the session (`OnSubModuleLoad` or `OnGameStart`): the
  interpreter's catalog is assembled once, at its first use, and an action registered after that
  reaches the interpreter only after the game restarts.

---

## 5. Unregistering

```csharp
bool removed = CrActions.Unregister("summon_caravan");
```

Undoes every part of `Register`. Returns `true` when anything was actually removed; `false`,
without throwing, for a null/blank name or one nothing was registered under.

---

## 6. Live prompt variables

`CrPrompt.RegisterVariable` is the public door for a third-party mod to contribute one LIVE
`{{name}}` prompt variable, computed per conversation, that the post-history prompt can reference.
This replaces a Harmony patch on the SDK's own `PromptBuilder.BuildPromptVariables` to splice a
value in after the fact.

```csharp
using CalradiaRemembers;
using NpcMemoryService.Core.Extension;

CrPrompt.RegisterVariable("bc_court_state", facts =>
{
    // facts.NpcId is a plain Hero StringId: re-resolve YOUR OWN Hero from it, exactly like an
    // ExternalActionSpec.Execute callback does. CR never hands you a live Hero here.
    Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == facts.NpcId);
    if (npc == null) return "";

    return MyCourtService.DescribeState(npc);
});
```

The provider is called once per prompt build, with an engine-agnostic `PromptVarFacts` (`NpcId`,
`RelationToPlayer`, the same personal regard `VerbFacts` carries, and, since 2.5.24, `Lean`: true when
this prompt is being built Compact for a small model, so shorten your text the way CR shortens its own;
a letter is always built in full). It gets no live `Hero`, kingdom,
or other CR type: `BuildPromptVariables` only has the NPC's profile and the current encounter on
hand at that point, so re-resolve whatever engine state you need from `NpcId` yourself.

A built-in variable (`{{user}}`, `{{char}}`, `{{location}}`, and so on) ALWAYS wins a name
collision: a third-party name can never shadow a core token. Register the same name again to
replace your own provider (the last registration wins); call `CrPrompt.Unregister("bc_court_state")`
to remove it. Neither call throws on a bad input (null/blank name, null provider): they are
silently ignored.

### 6a. Variables at the council table, one seat at a time (since 2.6.2)

The council is one prompt voicing the whole table, and it does not read `behavior_guidelines.txt`
or the other prompt files. To give each seated member your own text, contribute a template:

```
YourModule/ModuleData/cr_patch/council_seat.txt
```

It merges like any other `cr_patch` file (load order, `!remove`, `!replace`, `!#` comments). CR
ships no `council_seat.txt` of its own, so without a contribution nothing changes and no provider
is called for the council at all.

For EACH seated member, CR expands the template with that member's own variables and prints the
result under that member's roster line, announced as "<Name>'s own view (not common knowledge; it
shows only when the matter touches it)". Your provider is called once per seat, on the game
thread, with `facts.NpcId` = that seat and `facts.CouncilSeat = true`. Two things differ from a
private word:

- **Write in the third person.** At this table "you" means the PLAYER ("remembers of you",
  "regard toward the player"). Use the seat's name, or he/she, never "you" for the seat. Check
  `facts.CouncilSeat` and phrase accordingly.
- **Keep it short.** A seat's note is cut at a sentence past 700 characters, and a table seats up
  to six. `facts.Lean` still tells you when the player runs the Compact prompt.

`{{char}}` is the seat's name and `{{user}}` the player's. Headings (`=== ...`) and blank lines are
dropped. A seat whose text comes out empty carries no note.

```
!# council_seat.txt: Bellum Civile's view of each seat
{{bc_temper}}
```

---

## 6b. Make the NPC voice the outcome (ActionResult.Recap / CrConversation.PromptReply)

Your `Execute` runs AFTER the NPC has already spoken this turn: the model wrote its reply before your
deed resolved, so it cannot yet know the result. If the outcome is worth an in-character reaction
("I will not rule on your claim to Ostican, it is too weak"), set `ActionResult.Recap` to the plain
fact:

```csharp
Execute = req =>
{
    var upheld = JudgeClaim(req);            // your own engine work
    return new ActionResult
    {
        Success = upheld,
        Message = upheld ? "The court upholds the claim." : "The court denies the claim.",
        Recap   = upheld
            ? "the claim to Ostican was upheld"
            : "the claim to Ostican was denied: the claim is too weak"
    };
};
```

When `Recap` is set AND the conversation is still open, CR fires ONE extra, voice-only NPC turn in the
same beat: the NPC speaks that outcome in its own words, with no new player message. Give the raw fact,
not a scripted line, CR frames it as "voice this now, in character". It fires on a **refusal** too
(`Success = false`), which is often exactly the case worth voicing. Leave `Recap` null (the default)
for no follow-up. Each recap is one more LLM turn, so use it only when the outcome genuinely warrants a
reaction. A recap turn never chains into another (an action it might emit does not spawn a second recap).

For an outcome that resolves **outside** a player message, e.g. the player clicks a decision in your own
UI popup, call `CrConversation.PromptReply("the player chose to spare the prisoner")` from your handler.
It makes the NPC in the currently open conversation react in character, no player line needed. It returns
`false` (doing nothing) when no conversation is open, the conversation ended, or a turn is already in
flight, so you may retry when the player next has the floor. Call it on the main thread.

---

## 7. The other doors

`CrActions` is the door for an executable deed. Three others cover different needs:

- **World events**: `Feed.Record` lets you push a fact into CR's world (a battle, a marriage, a
  political shift) for NPCs to know about, without an executable verb. See `HOWTO_WorldEvents.md`
  for who hears it, for how long, and how to check it with `cr.heard`.
- **Conversation lifecycle**: `CrConversation` (see `HOWTO_Voicing.md`) lets you listen to when a
  conversation opens, closes, and each line as it is spoken, for a voice-over, subtitle, or logging
  addon.
- **Live prompt variables**: `CrPrompt.RegisterVariable` (section 6 above) lets you contribute a
  `{{name}}` token the prompt can reference, without an executable verb.
- **Prompt text from your module (`cr_patch`)**: ship `YourModule/ModuleData/cr_patch/<file>` with the
  same name as one of CR's prompt files (`world.txt`, `behavior_guidelines.txt`, the four
  `behavior_guidelines_<station>.txt`, `player_description.txt`, `post_history_instructions.txt`,
  `council_seat.txt`). Its lines are added after CR's own, in launcher load order. Directives, each
  on its own line: `!remove <text>` drops every line containing that text; `!replace` discards
  everything so far (a total conversion); `!#` is a comment the model never sees; and, since 2.6.4,
  `!block <people>` replaces, adds or removes ONE "If you are from ..." block of the FACTION BEHAVIOR
  section by its heading (`!block Vlandia` or `!block If you are from Vlandia:`). The lines under it,
  up to the next directive or the end of the file, become the block; none removes it; a people CR
  does not have is added inside the section, where each character is shown only their own block.
  Prefer `!block` to `!remove`-ing a block line by line: a line CR rewords later would otherwise
  stay behind.

None of these require a hard reference: you can reach any of them by reflection instead, if you
would rather your mod still load when Calradia Remembers is absent.
