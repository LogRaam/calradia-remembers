# Voicing Calradia Remembers (TTS / subtitle addon surface)

Calradia Remembers raises a public event every time a line is displayed in a conversation,
so a third-party mod can voice it (text-to-speech), caption it, or drive lip-sync. You only
subscribe; CR is the sole raiser, and every subscriber runs under a guard, so a fault in your
handler can never break the conversation or the game.

## The event

```csharp
// CalradiaRemembers.CrConversation
public static event Action<CrSpeechLine> LineSpoken;   // real time, per displayed line
public static event Action<CrConversationInfo> Started; // conversation opened
public static event Action<CrConversationLog>  Ended;   // conversation closed (full transcript)
```

## The line

```csharp
// CalradiaRemembers.CrSpeechLine
string ConversationId;  // the main NPC's hero StringId (groups a conversation)
string SpeakerId;       // the speaker's hero StringId (null for narration/synthetic). A stable per-voice key.
string SpeakerName;     // display name (subtitles/logging)
bool   IsPlayer;        // the player's own line OR an out-of-character note. Skip these to voice only NPCs.
bool   IsNarration;     // scene narration (no single speaker)
bool   IsWitness;       // a witness/companion, not the main NPC
bool   IsFemale;        // best-effort, to pick a voice
string CultureId;       // the speaker's culture StringId, for accent/voice choice
string Text;            // the clean, player-visible words (CR's control tags already stripped) -> feed this to TTS
```

## Minimal addon

```csharp
using System;
using CalradiaRemembers;

public static class MyTtsAddon
{
    // Call once, e.g. from your SubModule.OnSubModuleLoad or OnGameStart.
    public static void Hook()
    {
        CrConversation.Started    += _ => MyTts.Prepare();           // optional: warm the engine
        CrConversation.LineSpoken += OnLine;
        CrConversation.Ended      += _ => MyTts.StopAndFlush();      // stop playback when the talk closes
    }

    private static void OnLine(CrSpeechLine line)
    {
        if (line.IsPlayer) return;                    // voice NPCs only (drop the player + OOC notes)
        if (string.IsNullOrWhiteSpace(line.Text)) return;

        MyTts.Speak(
            text:     line.Text,
            voiceKey: line.SpeakerId ?? "narrator",   // one consistent voice per hero
            female:   line.IsFemale,
            culture:  line.CultureId,
            narration: line.IsNarration);
    }
}
```

## Notes

- `Text` is what the player reads: CR's `[DIALOGUE]`/`[NARRATION]`/`[ACTION]` tags are already stripped.
  In-character action prose inside asterisks (`*she draws her blade*`) is left in the words as shown; drop it in
  your handler if you do not want it voiced.
- Fires for NPC lines, witness/companion lines, scene narration, and the player's line. Everything a voice-over
  should skip (the player, rare out-of-character notes) carries `IsPlayer == true`.
- Use `SpeakerId` as your per-voice cache key so a given hero keeps the same voice across conversations.
- CR references nothing of yours; you reference only `CrConversation` and `CrSpeechLine`. No hard dependency
  either way beyond that.
