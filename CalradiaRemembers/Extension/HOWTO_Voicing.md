# How to voice Calradia Remembers (TTS / subtitle / lip-sync addons)

Calradia Remembers raises a public event every time a line is displayed in a conversation.
Subscribe to it and you can speak the line (text-to-speech), caption it, or drive lip-sync.
You only ever subscribe; CR is the sole raiser. Every subscriber runs under a guard, so a
fault in your handler can never break the conversation or crash the game.

Available from Calradia Remembers **2.1.4**.

---

## 1. Requirements and setup

1. Your module needs Calradia Remembers present. Declare it in your `SubModule.xml` so it loads first:

   ```xml
   <DependedModules>
     <DependedModule Id="CalradiaRemembers" />
   </DependedModules>
   ```

2. Reference `CalradiaRemembers.dll` from your project (it ships in
   `Modules/CalradiaRemembers/bin/Win64_Shipping_Client/`). The two types you use,
   `CalradiaRemembers.CrConversation` and `CalradiaRemembers.CrSpeechLine`, are public.

3. Subscribe once, early. `OnSubModuleLoad` (or your `OnGameStart`) is fine; the events are
   static, so a single subscription lasts the whole session.

If you would rather not take a hard reference (so your mod still loads when CR is absent),
subscribe through reflection instead. Everything below works the same either way.

---

## 2. The events

All three live on `CalradiaRemembers.CrConversation`:

```csharp
public static event Action<CrSpeechLine>      LineSpoken; // real time, once per displayed line
public static event Action<CrConversationInfo> Started;   // a conversation opened
public static event Action<CrConversationLog>  Ended;     // a conversation closed (full transcript)
```

- `Started` fires when a conversation opens (before the first line). Warm up your engine here.
- `LineSpoken` fires the instant each line is shown. This is the one you voice.
- `Ended` fires when the conversation closes. Stop or flush playback here.

---

## 3. `CrSpeechLine`

```csharp
string ConversationId; // the main NPC's hero StringId: groups all lines of one conversation
string SpeakerId;      // the speaker's hero StringId (null for narration / synthetic speakers)
string SpeakerName;    // display name, for subtitles or logging
bool   IsPlayer;       // the player's own line, OR an out-of-character note
bool   IsNarration;    // scene narration (no single speaker), not a spoken line
bool   IsWitness;      // a witness / companion, not the main conversation NPC
bool   IsFemale;       // best-effort, to pick a voice (false for narration / unknown)
string CultureId;      // the speaker's culture StringId, for accent / voice selection
string Text;           // the clean, player-visible words to voice (CR control tags already stripped)
```

- Voice **`Text`**. It is exactly what the player reads, with CR's `[DIALOGUE]` / `[NARRATION]` /
  `[ACTION]` control tags already removed.
- Use **`SpeakerId`** as your per-voice cache key, so a given hero keeps the same voice across
  conversations. Fall back to a fixed "narrator" voice when it is null.
- Everything a voice-over should normally skip (the player's line, the occasional
  out-of-character note) arrives with **`IsPlayer == true`**, so one check filters them out.

---

## 4. A complete minimal addon

```csharp
using System;
using CalradiaRemembers;              // CrConversation, CrSpeechLine
using TaleWorlds.MountAndBlade;       // MBSubModuleBase

public sealed class VoicedCalradiaSubModule : MBSubModuleBase
{
    private bool _hooked;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        if (_hooked) return;
        _hooked = true;

        CrConversation.Started    += _    => VoiceEngine.Prepare();
        CrConversation.LineSpoken += OnLineSpoken;
        CrConversation.Ended      += _    => VoiceEngine.StopAndFlush();
    }

    private static void OnLineSpoken(CrSpeechLine line)
    {
        // Voice NPCs only: drop the player's line and out-of-character notes.
        if (line.IsPlayer) return;
        if (string.IsNullOrWhiteSpace(line.Text)) return;

        // IMPORTANT: this runs on the game's main (UI) thread. Do NOT block it: hand the work
        // to your own queue / worker thread and return immediately, or you will stall the chat.
        VoiceEngine.Enqueue(new VoiceRequest {
            Text     = line.Text,
            VoiceKey = line.SpeakerId ?? "narrator", // one consistent voice per hero
            Female   = line.IsFemale,
            Culture  = line.CultureId,
            Narration = line.IsNarration
        });
    }
}
```

`VoiceEngine` is your own code (a wrapper around whatever TTS you use). `Prepare`, `Enqueue`,
`StopAndFlush` are yours to implement; CR does not care what is behind them.

---

## 5. Important notes

- **Do not block the handler.** `LineSpoken` is raised on the main thread during the chat turn.
  Synthesizing or playing audio inline would freeze the UI. Enqueue and return; do the audio on
  your own thread.
- **Interruptions are yours to manage.** If a new line arrives while the previous is still
  playing, decide whether to queue it or cut the previous. `Ended` is your signal to stop
  everything for that conversation.
- **Narration and action prose.** `IsNarration` marks scene prose with no single speaker (voice
  it with a narrator voice, or skip it). In-character action written inside asterisks
  (`*she draws her blade*`) stays inside `Text` as shown; strip it in your handler if you do not
  want it spoken.
- **Failures are isolated, but be polite.** CR wraps every subscriber in try/catch, so a throw in
  your handler is logged and swallowed rather than breaking the chat. Still, do not throw on the
  hot path, and never block.
- **No hard coupling.** CR references nothing of yours. You reference only `CrConversation` and
  `CrSpeechLine` (or reach them by reflection). There is no interface to implement and no object
  to register.

---

## 6. Lifecycle at a glance

```
Started(info)          -> a conversation opened (warm up)
LineSpoken(line)  x N   -> each displayed line, in order (speak the NPC ones)
Ended(log)             -> the conversation closed (stop / flush; log carries the full transcript)
```

That is the whole surface. Subscribe, read `Text`, pick a voice from `SpeakerId` / `IsFemale` /
`CultureId`, and play it off the main thread.
