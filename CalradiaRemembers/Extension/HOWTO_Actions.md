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
- `IsEligible` and `TeachingText` are optional. Leave them null and your action is always taught
  once registered (its catalog entry is enough); supply them if the action should only be taught
  while some condition of your own holds.
- `Execute` gets an `ActionRequest` carrying `VerbName`, the `[ACTION]` block's raw `Parameters`
  (a `IReadOnlyDictionary<string, string>`, exactly as the LLM emitted them), and `Facts` (a
  `VerbFacts`: `NpcId`, `RelationToPlayer`, `FactionId`, `IsLord`, `IsPrisoner`). Everything is a
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

The provider is called once per prompt build, with an engine-agnostic `PromptVarFacts` (`NpcId` and
`RelationToPlayer`, the same personal regard `VerbFacts` carries). It gets no live `Hero`, kingdom,
or other CR type: `BuildPromptVariables` only has the NPC's profile and the current encounter on
hand at that point, so re-resolve whatever engine state you need from `NpcId` yourself.

A built-in variable (`{{user}}`, `{{char}}`, `{{location}}`, and so on) ALWAYS wins a name
collision: a third-party name can never shadow a core token. Register the same name again to
replace your own provider (the last registration wins); call `CrPrompt.Unregister("bc_court_state")`
to remove it. Neither call throws on a bad input (null/blank name, null provider): they are
silently ignored.

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

None of the four require a hard reference: you can reach any of them by reflection instead, if you
would rather your mod still load when Calradia Remembers is absent.
