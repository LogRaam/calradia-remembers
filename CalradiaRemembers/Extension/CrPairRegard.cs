// Code written by Gabriel Mailhot, 18/10/2026.
// Extension Surface: other mods add to what one character thinks of another. tashmetu (Regard and Standing, 18/10/2026):
// PairRegard "only sees Calradia Remembers' own traces, so it can disagree with what another mod knows: two lords R&S
// has made close through years of battles side by side can show as 'neither' or even 'enemy' on the page, right next to
// R&S's own tooltip that says the opposite." A registered source adds its traces to the same weighing the chat and the
// encyclopedia read; together they count for at most PairRegardPolicy.OutsideCap either way, so another mod may tip a
// pair and never drown what happened here. Each source is called under a guard: one that throws adds nothing.
// tashmetu (Nexus, 04/11/2026): R&S clears the game's starting draw and seeds ties of its own, with reasons ("friends at
// the same court", "an old rival realm"). CR now believes a starting relation between two NPCs only when it has a reason
// (PairBondPolicy), so another mod may give one (RegisterStartReason) and read what CR believes (GetBond). CR names no mod.

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

      /// <summary>
      ///   Adds a source as a function, for a mod that keeps Calradia Remembers optional (since 2.7.19). tashmetu (20/10/2026):
      ///   the game reads every type of a mod's DLL when it loads it, so a class implementing <see cref="IPairRegardSource" />
      ///   cannot load where CR is absent, and Regard and Standing was refused altogether. The function returns each trace as
      ///   (weight, line, pageLine, knownToPlayer), the fields of <see cref="PairRegardTrace" />, so the mod's own types
      ///   name nothing of CR. Registering the same <paramref name="sourceId" /> again replaces it. Names no Calradia
      ///   Remembers type.
      /// </summary>
      public static void RegisterPairRegardSource(string sourceId,
         Func<Hero, Hero, int, IEnumerable<(int weight, string line, string pageLine, bool knownToPlayer)>> traces)
      {
         if (string.IsNullOrWhiteSpace(sourceId) || traces == null) return;

         lock (Sources)
         {
            Sources.RemoveAll(s => s is FunctionSource f && f.Id == sourceId);
            Sources.Add(new FunctionSource(sourceId, traces));
         }
      }

      /// <summary>Takes away the source registered as a function under this id. Names no Calradia Remembers type.</summary>
      public static void UnregisterPairRegardSource(string sourceId)
      {
         lock (Sources) Sources.RemoveAll(s => s is FunctionSource f && f.Id == sourceId);
      }

      /// <summary>
      ///   CR waits this many days into a new campaign before anything it keeps for good reads a starting relation between
      ///   two NPCs (a shared past drawn, a plot launched), so a mod seeding its own ties in the first days is read, not
      ///   the game's draw. Since 2.8.3.
      /// </summary>
      public const int StartGraceDays = Logic.Relations.PairBondPolicy.StartGraceDays;

      /// <summary>
      ///   Registers a reason giver under <paramref name="source" /> (your mod's name, shown in the log). CR believes a
      ///   relation two NPCs started the campaign with only when it can name why they know each other (kin, one house, one
      ///   court, neighbours, a war); when it cannot, it calls <paramref name="reason" /> with the pair. Return a few words
      ///   that complete "known to each other through ..." (e.g. "an old rivalry between your peoples") to have it
      ///   believed, or null. A reason giver that throws is logged once and passed over. Registering the same source again
      ///   replaces it. Not saved: register again when a game is loaded. Names no Calradia Remembers type. Since 2.8.3.
      /// </summary>
      public static void RegisterStartReason(string source, Func<Hero, Hero, string> reason)
      {
         if (Mapping.PairBondReader.StartReasons.Register(source, reason)) ModLog.Info($"[REGARD API] start reason registered by '{source}'");
      }

      /// <summary>Withdraws the start reason of <paramref name="source" />. Returns whether one was there. Since 2.8.3.</summary>
      public static bool UnregisterStartReason(string source) => Mapping.PairBondReader.StartReasons.Unregister(source);

      /// <summary>
      ///   How CR believes two characters stand with each other, personally: the points it acts on (-100 to 100), on what
      ///   they rest ("None", "Earned" in play, "Known" by a reason, "Unexplained" start set aside), and the reason when
      ///   known. Null for an unknown hero or outside a campaign. Names no Calradia Remembers type. Since 2.8.3.
      /// </summary>
      public static (int points, string basis, string reason)? GetBond(string heroId, string otherId)
      {
         try
         {
            Hero a = string.IsNullOrWhiteSpace(heroId) ? null : Hero.FindFirst(h => h.StringId == heroId);
            Hero b = string.IsNullOrWhiteSpace(otherId) ? null : Hero.FindFirst(h => h.StringId == otherId);
            if (a == null || b == null || a == b) return null;

            Logic.Relations.PairBond bond = Mapping.PairBondReader.Of(a, b);

            return (bond.Points, bond.Basis.ToString(), bond.Reason);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[REGARD API] GetBond failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private sealed class FunctionSource : IPairRegardSource
      {
         private readonly Func<Hero, Hero, int, IEnumerable<(int weight, string line, string pageLine, bool knownToPlayer)>> _traces;

         public FunctionSource(string id, Func<Hero, Hero, int, IEnumerable<(int weight, string line, string pageLine, bool knownToPlayer)>> traces)
         {
            Id = id;
            _traces = traces;
         }

         public string Id { get; }

         public IEnumerable<PairRegardTrace> TracesFor(Hero speaker, Hero other, int today)
            => (_traces(speaker, other, today) ?? Enumerable.Empty<(int, string, string, bool)>())
               .Select(t => new PairRegardTrace {Weight = t.weight, Line = t.line, PageLine = t.pageLine, KnownToPlayer = t.knownToPlayer});

         public override string ToString() => Id;
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
               if (first) ModLog.Error($"[REGARD] a pair-regard source ({(s is FunctionSource f ? f.Id : s.GetType().FullName)}) threw and adds nothing (logged once): {ex.GetType().Name}: {ex.Message}");
            }

         return traces;
      }
   }
}
