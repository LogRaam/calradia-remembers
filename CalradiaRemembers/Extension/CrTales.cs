// Code written by Gabriel Mailhot, 15/10/2026.
// Extension Surface: the PUBLIC tale-progress events. tashmetu (Regard and Standing, 15/10/2026): ON_TALE lets a scenario
// pack answer a tale, but a scenario rule cannot reach another mod's code, and some answers belong there (a lady who
// elopes with the player is an affair found out at court for R&S). He asked to READ a tale's progress: the stage or the
// ending reached, the roles bound, the flags, and which consequences were really carried out, since "elope" does nothing
// when the marriage cannot end and another mod must not answer an elopement that did not happen. Subscribers only
// LISTEN; only CR raises these, and each subscriber is invoked under a guard so one that throws breaks nothing.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The public tale-progress events other mods subscribe to. CR is the only raiser.</summary>
   public static class CrTales
   {
      /// <summary>
      ///   Fired when a tale ENTERS a stage, after the stage's own consequences (its "on_enter") were carried out, so
      ///   <see cref="CrTaleProgress.Consequences" /> says what really happened. The first stage, the request, is left
      ///   when the tale is accepted, so the first stage reported is the one after it.
      /// </summary>
      public static event Action<CrTaleProgress> StageReached;

      /// <summary>
      ///   Fired when a tale ENDS: an ending ("end:reward", "end:&lt;id&gt;"), the failure ("end:failure"), or abandoned
      ///   ("end:abandoned", from the journal or the console; no consequence is carried out then).
      /// </summary>
      public static event Action<CrTaleProgress> Ended;

      /// <summary>
      ///   Fired when a consequence reported <see cref="CrTaleConsequenceStatus.Waiting" /> settles later: a lady's return
      ///   to her husband waits while he is on campaign or either is a captive, retried each day for up to 60 days.
      ///   <see cref="CrTaleProgress.Where" /> is "settled"; the one consequence is Done or NotDone, and names her
      ///   (<see cref="CrTaleConsequence.WhoId" />) and him (<see cref="CrTaleConsequence.OtherId" />); Roles holds only the player.
      /// </summary>
      public static event Action<CrTaleProgress> ConsequenceSettled;

      // Raised by CR (internal). A caller only subscribes; it never raises.
      internal static void RaiseStageReached(CrTaleProgress p) => Invoke(StageReached, p);
      internal static void RaiseEnded(CrTaleProgress p) => Invoke(Ended, p);
      internal static void RaiseConsequenceSettled(CrTaleProgress p) => Invoke(ConsequenceSettled, p);

      private static void Invoke(Action<CrTaleProgress> evt, CrTaleProgress arg)
      {
         if (evt == null || arg == null) return;

         foreach (Delegate handler in evt.GetInvocationList())
            try { ((Action<CrTaleProgress>) handler)(arg); }
            catch (Exception ex)
            {
               ModLog.Error($"[TALE EVENT] a subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}");
            }
      }
   }

   /// <summary>Whether a tale's consequence really happened.</summary>
   public enum CrTaleConsequenceStatus
   {
      /// <summary>Carried out.</summary>
      Done,

      /// <summary>Not carried out; <see cref="CrTaleConsequence.Reason" /> says why (she is with child, a captive, the chance did not fall...).</summary>
      NotDone,

      /// <summary>Begun but not finished: <see cref="CrTales.ConsequenceSettled" /> follows when it settles.</summary>
      Waiting
   }

   /// <summary>One consequence of a stage or an ending, and what became of it. Consequences whose "if" did not hold are not listed.</summary>
   public sealed class CrTaleConsequence
   {
      internal CrTaleConsequence(string type, string whoId, string otherId, CrTaleConsequenceStatus status, string reason)
      {
         Type = type;
         WhoId = whoId;
         OtherId = otherId;
         Status = status;
         Reason = reason;
      }

      /// <summary>"memory", "world_event", "grudge", "conception", "elope" or "return_spouse".</summary>
      public string Type { get; }

      /// <summary>The hero StringId it is about (the lady who elopes or returns, the grudge holder, the partner); null for news.</summary>
      public string WhoId { get; }

      /// <summary>The other hero StringId (the husband left or returned to, the grudge's target); null when none.</summary>
      public string OtherId { get; }

      public CrTaleConsequenceStatus Status { get; }

      /// <summary>Why it was not done, or why it waits, in plain words; null when done.</summary>
      public string Reason { get; }
   }

   /// <summary>A tale's progress, delivered by <see cref="CrTales" />. Copies: changing them changes nothing in the tale.</summary>
   public sealed class CrTaleProgress
   {
      internal CrTaleProgress(string taleId, string where, int gameDay, IDictionary<string, string> roles, IEnumerable<string> flags,
         IEnumerable<CrTaleConsequence> consequences)
      {
         TaleId = taleId;
         Where = where;
         GameDay = gameDay;
         Roles = new Dictionary<string, string>(roles ?? new Dictionary<string, string>());
         Flags = new List<string>(flags ?? Array.Empty<string>());
         Consequences = new List<CrTaleConsequence>(consequences ?? Array.Empty<CrTaleConsequence>());
      }

      /// <summary>The tale's id, as in its file. Ids beginning "cr_selftest_" are cr.test all's own tales.</summary>
      public string TaleId { get; }

      /// <summary>
      ///   The stage id reached, or the ending: "end:reward", "end:&lt;id&gt;", "end:failure", "end:abandoned"; "settled"
      ///   for <see cref="CrTales.ConsequenceSettled" />. The same strings scenario rules answer with ON_TALE.
      /// </summary>
      public string Where { get; }

      /// <summary>The in-game day.</summary>
      public int GameDay { get; }

      /// <summary>Role name to hero StringId or place id ("giver", "husband", "village", "player"...).</summary>
      public IReadOnlyDictionary<string, string> Roles { get; }

      /// <summary>The flags set so far on the path the player walked.</summary>
      public IReadOnlyList<string> Flags { get; }

      /// <summary>What this stage or ending carried out, and whether it really happened.</summary>
      public IReadOnlyList<CrTaleConsequence> Consequences { get; }
   }
}
