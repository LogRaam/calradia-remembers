// Code written by Gabriel Mailhot, 13/10/2026.
// What CR did to fit a local model's declared context window, kept for the session and told to the player once.
//
// fkasad (Nexus, 13/10/2026): on LM Studio, a long conversation lost the rules for a "give" because the server cut CR's
// oversized request in its middle. Since 2.7.10 the SDK fits each request to the "Local model context (tokens)" MCM
// value (NpcChatService + ContextFitPolicy) and reports it here. Two things follow from a report:
//  - once the compact prompt had to replace the full one, it stays compact for the session (ModSettings.LeanPrompt reads
//    it), so the prompt does not flip back and forth and the conversation fold tightens with it;
//  - the player is told once, and told once more if even the compact prompt does not fit the declared window.

#region

using CalradiaRemembers.Foundation;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>The session's record of fitting requests to the local model's window.</summary>
   public static class LocalContextState
   {
      // The declared window at which the compact prompt became necessary; 0 while it has not.
      private static int _compactAtWindow;
      private static bool _toldCompact;
      private static bool _toldTooSmall;

      /// <summary>
      ///   The last fit, in one line, for the player report (hype, 23/11/2026: his report showed every request stripped
      ///   to the recap and one line, and nothing in it said whether CR had left lines out or why). Null before any fit.
      /// </summary>
      public static string LastFit { get; private set; }

      /// <summary>Whether conversations use the compact prompt for the rest of the session (until the window setting changes).</summary>
      public static bool CompactThisSession => _compactAtWindow > 0 && _compactAtWindow == ModSettings.LocalContextTokens;

      /// <summary>NpcChatService.ContextFitted: logs what was done and keeps / tells what matters.</summary>
      public static void OnFitted(ContextFitReport r)
      {
         if (r == null) return;

         LastFit = $"window {r.WindowTokens}, ~{r.EstimatedTokens} of {r.UsableTokens} planned, {r.DroppedMessages} old line(s) left out, "
                 + $"compact: {r.SwitchedToCompact}, fits: {r.Fits} ({r.CharsPerToken:0.00} chars/token)";

         if (r.DroppedMessages > 0 || r.SwitchedToCompact || !r.Fits)
            ModLog.Info($"[CONTEXT] {r.Subject}: window {r.WindowTokens}, sent ~{r.EstimatedTokens} of {r.UsableTokens} planned, "
                      + $"{r.DroppedMessages} old line(s) left out, compact: {r.SwitchedToCompact}, fits: {r.Fits} ({r.CharsPerToken:0.00} chars/token).");

         if (r.SwitchedToCompact && _compactAtWindow != r.WindowTokens)
         {
            _compactAtWindow = r.WindowTokens;
            if (!_toldCompact)
            {
               _toldCompact = true;
               Tell($"Calradia Remembers: your local model's context ({r.WindowTokens} tokens) cannot hold the full prompt, so conversations use the compact prompt for this session.");
            }
         }

         if (!r.Fits && !_toldTooSmall)
         {
            _toldTooSmall = true;
            Tell($"Calradia Remembers: even the compact prompt (about {r.EstimatedTokens} tokens with the reply) does not fit your local model's context ({r.WindowTokens}). Raise the context length in your server, then in the MCM setting.");
         }
      }

      /// <summary>Self-test seam: forgets the session's record (and its notices).</summary>
      internal static void DebugReset()
      {
         _compactAtWindow = 0;
         LastFit = null;
         _toldCompact = false;
         _toldTooSmall = false;
      }

      // The report can arrive off the main thread (the chat awaits with ConfigureAwait(false)).
      private static void Tell(string text)
         => MainThreadDispatcher.Enqueue(() => InformationManager.DisplayMessage(new InformationMessage(text, Colors.Yellow)));
   }
}
