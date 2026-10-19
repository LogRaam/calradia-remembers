// Code written by Gabriel Mailhot, 18/10/2026.
// Extension Surface: other mods add to what one character thinks of another. tashmetu (Regard and Standing, 18/10/2026):
// PairRegard "only sees Calradia Remembers' own traces, so it can disagree with what another mod knows: two lords R&S
// has made close through years of battles side by side can show as 'neither' or even 'enemy' on the page, right next to
// R&S's own tooltip that says the opposite." A registered source adds its traces to the same weighing the chat and the
// encyclopedia read; together they count for at most PairRegardPolicy.OutsideCap either way, so another mod may tip a
// pair and never drown what happened here. Each source is called under a guard: one that throws adds nothing.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>What another mod knows lies between two characters.</summary>
   public interface IPairRegardSource
   {
      /// <summary>
      ///   What lies between <paramref name="speaker" /> and <paramref name="other" />, as the speaker sees it: each trace
      ///   with its signed weight (on the regard scale, minus for a wrong), its line from the speaker's side ("you"),
      ///   its page line about the speaker, and whether the player could know of it. Return nothing when nothing does.
      ///   Called often (each page and each named character in the chat): keep it cheap.
      /// </summary>
      IEnumerable<PairRegardTrace> TracesFor(Hero speaker, Hero other, int today);
   }

   /// <summary>The door through which other mods add to what one character thinks of another.</summary>
   public static class CrPairRegard
   {
      private static readonly List<IPairRegardSource> Sources = new();
      private static readonly HashSet<IPairRegardSource> Faulted = new();

      /// <summary>Adds a source; registering the same one twice keeps one.</summary>
      public static void RegisterPairRegardSource(IPairRegardSource source)
      {
         if (source == null) return;

         lock (Sources)
            if (!Sources.Contains(source)) Sources.Add(source);
      }

      /// <summary>Takes a source away.</summary>
      public static void UnregisterPairRegardSource(IPairRegardSource source)
      {
         lock (Sources) Sources.Remove(source);
      }

      // Read by PairRegardReader. A trace with no line cannot be a reason, and is dropped.
      internal static IReadOnlyList<PairRegardTrace> TracesFor(Hero speaker, Hero other, int today)
      {
         IPairRegardSource[] sources;
         lock (Sources) sources = Sources.ToArray();
         if (sources.Length == 0) return Array.Empty<PairRegardTrace>();

         var traces = new List<PairRegardTrace>();
         foreach (IPairRegardSource s in sources)
            try
            {
               traces.AddRange((s.TracesFor(speaker, other, today) ?? Enumerable.Empty<PairRegardTrace>())
                                .Where(t => t != null && t.Weight != 0 && !string.IsNullOrWhiteSpace(t.Line)));
            }
            catch (Exception ex)
            {
               bool first;
               lock (Faulted) first = Faulted.Add(s);
               if (first) ModLog.Error($"[REGARD] a pair-regard source ({s.GetType().FullName}) threw and adds nothing (logged once): {ex.GetType().Name}: {ex.Message}");
            }

         return traces;
      }
   }
}
