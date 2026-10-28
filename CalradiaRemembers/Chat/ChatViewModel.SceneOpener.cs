// Code written by Gabriel Mailhot, 22/10/2026.
// Lordfadooboo (Nexus, 21/10/2026): a conversation opened cold. Gabriel (22/10/2026): the character sets the scene by
// default, as captive scenes already do. Unlike every other NPC-led opener, this one never takes the floor from the
// player: the canned greeting stays on screen while the scene is written (it is the placeholder the scene replaces),
// the input stays open, and a message sent first sets the scene aside, so nobody waits on a slow model to say hello.
// The greeting reaches the transcript only when it stands (SceneOpenerPolicy.GreetingStands): a model that saw both
// the greeting and its own scene would read two openings.

#region

using System.Threading;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Chat;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private ChatMessageViewModel _sceneOpenerGreeting;
      private string _sceneOpenerGreetingText;
      private CancellationTokenSource _sceneOpenerCancel;
      private bool _sceneOpenerInFlight;
      private bool _sceneOpenerAbandoned;
      private bool _sceneOpenerCommitted; // the scene arrived and is being applied: it holds the floor until then

      /// <summary>True when a sitting kept from earlier today was put back as this chat opened.</summary>
      public bool ResumedASitting { get; private set; }

      /// <summary>
      ///   Opens an ordinary conversation on <paramref name="greeting" /> and has the character set the scene in its
      ///   place. The greeting is shown at once and not yet remembered; the input stays open throughout.
      /// </summary>
      public void OpenWithScene(string greeting)
      {
         ChatSpeaker speaker = MainSpeaker();
         if (_mode != ChatMode.Conversation || speaker.Profile == null || speaker.Hero == null)
         {
            AppendNpcMessage(greeting);

            return;
         }

         _sceneOpenerGreetingText = greeting;
         _sceneOpenerGreeting = MakeSpeakerMessage(speaker, greeting);
         _messages.Add(_sceneOpenerGreeting);
         ChatManager.ScrollToBottom();

         _sceneOpenerCancel = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token);
         _sceneOpenerInFlight = true;
         _sceneOpenerAbandoned = false;
         _sceneOpenerCommitted = false;
         ModLog.Info($"[SCENE] {speaker.Name} sets the scene; the greeting stands until it arrives.");
         _ = RunLlmExchangeAsync(SceneOpenerPolicy.Cue, speaker, _sceneOpenerGreeting, true, isSceneOpener: true);
      }

      /// <summary>The token the scene-setting call is aborted by: a message sent first, or the window closing.</summary>
      private CancellationToken SceneOpenerToken => _sceneOpenerCancel?.Token ?? CancellationToken.None;

      /// <summary>
      ///   The player spoke before the scene arrived: the greeting stands and is remembered before his line, the call is
      ///   aborted, and his first turn opens the scene instead (the opening narration goes back to it). No-op otherwise.
      /// </summary>
      private void SetSceneOpenerAside()
      {
         if (!_sceneOpenerInFlight || _sceneOpenerAbandoned || _sceneOpenerCommitted) return;

         _sceneOpenerAbandoned = true;
         SettleSceneOpener(SceneOpenerOutcome.Abandoned);
         try { _sceneOpenerCancel?.Cancel(); }
         catch (System.ObjectDisposedException) { }
         ModLog.Info("[SCENE] the player spoke first; the scene is set aside.");
      }

      /// <summary>
      ///   Settles the greeting for how the scene turn ended. A greeting that stands is remembered as said, and the
      ///   opening narration is owed to the next turn; a delivered scene took its place and needs nothing here.
      /// </summary>
      private void SettleSceneOpener(SceneOpenerOutcome outcome)
      {
         if (!SceneOpenerPolicy.GreetingStands(outcome) || _sceneOpenerGreetingText == null) return;

         RecordTranscript(false, _npcName, _npcHero?.StringId, _sceneOpenerGreetingText);
         _sceneOpenerGreetingText = null;
         _openingNarrationPending = true;
      }

      /// <summary>cr.test scene_opener: the scene is still being written.</summary>
      internal bool SceneOpenerInFlightForDiagnostics => _sceneOpenerInFlight;

      /// <summary>cr.test scene_opener: what the player's first message does to a scene in flight, without sending one.</summary>
      internal void SetSceneOpenerAsideForDiagnostics() => SetSceneOpenerAside();

      /// <summary>cr.test scene_opener: the last line this conversation remembers, or null.</summary>
      internal string LastRememberedLineForDiagnostics => _transcript.Count > 0 ? _transcript.Lines[_transcript.Count - 1].Text : null;

      /// <summary>cr.test scene_opener: the greeting is still on screen.</summary>
      internal bool ShowsLineForDiagnostics(string text) => System.Linq.Enumerable.Any(_messages, m => m?.DisplayText?.Contains(text) == true);

      /// <summary>The scene turn is over, whatever became of it.</summary>
      private void EndSceneOpenerFlight()
      {
         _sceneOpenerInFlight = false;
         _sceneOpenerCommitted = false;
         _sceneOpenerGreeting = null;
         _sceneOpenerCancel?.Dispose();
         _sceneOpenerCancel = null;
      }
   }
}
