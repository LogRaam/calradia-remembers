# Calradia Remembers: NPC Memory & Conversation API

A public, stable API for other mods to observe conversations and read or inject an NPC's memory at
runtime. It lives in `CalradiaRemembers` (the layer that talks to TaleWorlds and owns the NPC profiles),
not in the LLM layer. All identity is by hero `StringId`, never a display name.

Check `CrNpcMemory.ApiVersion` before you call anything. It is bumped only on a breaking change; additive
members never bump it. Current version: **1**.

## What you get

### Conversation lifecycle (`CalradiaRemembers.CrConversation`)

```csharp
using CalradiaRemembers;

// Fired when a conversation OPENS, before the first exchange, so you can inject relevant memories in time
// for the first prompt.
CrConversation.Started += info =>
{
    string npcId  = info.NpcId;    // hero StringId
    string saveId = info.SaveId;   // Campaign.Current.UniqueGameId, stable per campaign (your isolation key)
    int    day    = info.GameDay;  // in-game day, for timeline context
    // ... recall this NPC's relevant memories from your own store and inject them (see AddMemory below) ...
};

// Fired when a conversation CLOSES, with the whole attributed transcript.
CrConversation.Ended += log =>
{
    foreach (CrTranscriptLine line in log.Lines)   // oldest first
    {
        // line.SpeakerId    -> hero StringId (null for narration / synthetic speakers)
        // line.SpeakerName  -> display name (log only; use SpeakerId for identity)
        // line.IsPlayer     -> the player's own line
        // line.IsNarration  -> scene narration rather than a spoken line
        // line.Text
    }
    // Third-party WITNESS lines are included and attributed by SpeakerId, so multi-party interjections
    // are no longer a guessing game.
};
```

Every subscriber is invoked under a guard: if your handler throws, it is logged and isolated, never
breaking the conversation or the game.

### NPC memory read/write (`CalradiaRemembers.CrNpcMemory`)

```csharp
using CalradiaRemembers;

// READ what CR already stores for an NPC (so you don't inject a duplicate). Empty if the id is unknown,
// the NPC has no profile yet, or no campaign is loaded.
IReadOnlyList<CrMemoryRecord> known = CrNpcMemory.GetMemories(npcId);
foreach (CrMemoryRecord m in known)
{
    // m.Day, m.Kind ("Agreement", "Conflict", "Other", ...), m.Summary
}

// INJECT a memory the NPC will treat as its own recollection in later conversations. The NPC's profile is
// created if it does not exist yet, so you can seed an NPC the player has never met.
MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry
{
    NpcId     = npcId,                                        // REQUIRED, hero StringId
    Summary   = "We agreed to meet at the ford at dawn.",     // REQUIRED, the remembered line
    Kind      = "Agreement",                                  // optional; unknown/engine kinds become "Other"
    DaysAgo   = 0,                                            // optional; 0 = today, dated back from now
    SourceMod = "MyMemoryAddon"                               // optional, for provenance in the logs
});
// result: Added | RejectedInvalid | RejectedUnknownHero | RejectedNoStore
```

Injected memories become ordinary profile events, so they surface in later conversations exactly like
memories CR recorded itself, and are compacted over time by CR's own memory compressor.

### Regard changes (`CrNpcMemory.RegardChanged`, since 2.6.7)

```csharp
CrNpcMemory.RegardChanged += change =>
{
    // change.NpcId, change.Before, change.After (-100..100), change.Cause, change.GameDay, change.SaveId
};
```

Fired each time a character's **personal regard** for the player actually changes, whatever changed it: a
conversation, a letter, a quest, a promise kept or broken, a grievance put to rest, jealousy, a rescue, a
departure from their realm. `Cause` is one of `conversation`, `letter`, `quest`, `commitment`, `compensation`,
`grudge`, `jealousy`, `divorce`, `rescue`, `stance`, `violence`, `departure`, `unspecified`; more may be added,
so treat an unknown one as a change like any other. Raised on the game thread, after the change, each listener
under its own guard.

**Two numbers, two names.** CR keeps its own regard (what one character personally thinks of the player,
built in conversation) apart from the game's relation. In the prompt the game's value is called **"your clan's
standing"** and CR's is **"your own personal regard"**; write prompt rules with those words, or the model may
read the wrong number. `RelationToPlayer` in `VerbFacts` and `PromptVarFacts` is always CR's personal regard,
never the game's relation.

**CR never writes the game's relation from regard, on purpose.** The game's relation is effectively clan-wide
(the engine resolves a lord's relation through his clan leader), and regard is paced (a +1 cap and a cooldown
in conversation) precisely so a player cannot talk their way to the top. A mod that mirrors `RegardChanged` into
the game's relation takes that balance on itself; do it from this event, never as a side effect of a prompt
variable provider.

## Notes and limits (v1)

- **Kinds you cannot spoof.** `FirstMeeting`, `Captivity`, and `Farewell` are engine-owned (they drive
  have-we-met logic, compression, and input locking). A caller asking for one gets `Other` instead.
- **Summary length** is bounded (300 chars) to keep the save blob safe; longer lines are trimmed.
- **`Started` fires for every conversation type** (ordinary, captive, companion, commoner). v1 does not
  tag which; ask if you need that distinction.
- **No update/remove yet.** v1 is read + inject. Targeted edit/remove and a `Discovery`-based recall path
  are candidates for a later version.

## Calling without a compile-time reference

If you would rather not reference CR's assembly, reach these types by reflection: read the static
`CrNpcMemory.ApiVersion` first, then invoke `CrNpcMemory.GetMemories` / `CrNpcMemory.AddMemory` and
subscribe to the `CrConversation.Started` / `CrConversation.Ended` events on the `CalradiaRemembers`
types.

## Quick in-game check

`cr.api_probe <npc name>` prints the API version and save id, injects a test memory on that NPC, and
reads it back, so you can confirm the door works before wiring your addon.
