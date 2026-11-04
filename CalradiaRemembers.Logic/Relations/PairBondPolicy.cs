// Code written by Gabriel Mailhot, 04/11/2026.
// How two NPCs stand with each other, personally, and why. tashmetu (Nexus, 04/11/2026), author of Regard and Standing,
// on two things in the base game that CR built on as if they were history:
//   - "Relations belong to the clan leader": the game's Hero.GetRelation answers for the two clan leaders, so a younger
//     son shared his father's friends and enemies, and a change made with him landed on his father.
//   - "The friendships and enmities the game hands out at start make no sense": the new campaign draws thousands of them
//     between lords who never met ("Derthert at -100 with Tasynor"), and CR read them as a real past.
// Gabriel (04/11/2026): a relation between two NPCs is believed only when CR can name a reason for it; one with no
// reason keeps a vague unease, never a number; and the houses' standing is said apart from the person's.
//
// So the personal value (the game's own slot for the pair, never the leaders') splits in two:
//   - what CHANGED since CR first saw the pair move: real history, believed as it is;
//   - the rest, the START value (the game's draw, or another mod's seeding): believed only when the two have a reason to
//     know each other (kin, one house, one court, neighbours, realms at war, two rulers, or a reason another mod gives).
// Temperament alone is never a reason: the game's own draw already set the sign from it, so it would excuse every draw.

#region

using System;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Relations
{
   /// <summary>On what CR believes the relation between two people.</summary>
   public enum PairBondBasis
   {
      /// <summary>Nothing lies between them.</summary>
      None,

      /// <summary>It moved through things that happened while CR watched.</summary>
      Earned,

      /// <summary>Its starting value has a reason the two would know each other by.</summary>
      Known,

      /// <summary>A starting value with no reason: not believed, only a vague feeling remains.</summary>
      Unexplained
   }

   /// <summary>The vague feeling an unexplained starting value leaves, said in words and never counted.</summary>
   public enum PairBondEcho
   {
      None,
      VagueGoodwill,
      VagueUnease
   }

   /// <summary>What decides how two people stand with each other, personally.</summary>
   public sealed class PairBondFacts
   {
      /// <summary>The game's own value for this pair (its personal slot, -100 to 100), never their clan leaders'.</summary>
      public int Personal { get; init; }

      /// <summary>
      ///   The pair's value just before the first change CR saw, or null when CR never saw it change. What lies between it
      ///   and <see cref="Personal" /> is history; the rest is the start.
      /// </summary>
      public int? Baseline { get; init; }

      /// <summary>Spouse, parent, child or sibling.</summary>
      public bool IsKin { get; init; }

      /// <summary>Members of one house.</summary>
      public bool SameHouse { get; init; }

      /// <summary>Sworn to one realm.</summary>
      public bool SameRealm { get; init; }

      /// <summary>Their houses' lands, or their realms', lie side by side.</summary>
      public bool Neighbours { get; init; }

      /// <summary>Their realms are at war.</summary>
      public bool RealmsAtWar { get; init; }

      /// <summary>Both rule a realm.</summary>
      public bool BothRulers { get; init; }

      /// <summary>The game's temperament score for the pair: above 0 alike, below 0 at odds. Colours a reason, never makes one.</summary>
      public int Temperament { get; init; }

      /// <summary>A reason another mod gives for the pair, completing "known to each other through ..." ("friendship at the same court"), or null.</summary>
      public string OutsideReason { get; init; }
   }

   /// <summary>How two people stand with each other, personally, and on what.</summary>
   public sealed class PairBond
   {
      /// <summary>The value CR acts on, -100 to 100: the whole when believed, only the history when not.</summary>
      public int Points { get; init; }

      /// <summary>On what it rests.</summary>
      public PairBondBasis Basis { get; init; }

      /// <summary>Why the start is believed, completing "known to each other through ..." ("the same court"), or null.</summary>
      public string Reason { get; init; }

      /// <summary>The vague feeling an unexplained start leaves, for words only.</summary>
      public PairBondEcho Echo { get; init; }

      /// <summary>The points on the engine's tiers.</summary>
      public VanillaRelation Relation => new VanillaRelation(Points);
   }

   /// <summary>Decides how two people stand with each other. Pure.</summary>
   public static class PairBondPolicy
   {
      /// <summary>An unexplained start this strong, either way, leaves a vague feeling; below it, nothing.</summary>
      public const int EchoFloor = VanillaRelation.FriendTier;

      /// <summary>
      ///   Days into a new campaign before anything kept for good reads a start (a shared past drawn, a plot launched).
      ///   tashmetu (Nexus, 04/11/2026): R&amp;S clears and seeds "in the first hours of a new campaign"; read before, the
      ///   game's blind numbers would be frozen into the save. Two days was her own suggestion.
      /// </summary>
      public const int StartGraceDays = 2;

      /// <summary>Whether the campaign is far enough along for a start to be read into something kept for good.</summary>
      public static bool PastStartGrace(int daysSinceCampaignStart) => daysSinceCampaignStart >= StartGraceDays;

      /// <summary>How the two stand, personally.</summary>
      public static PairBond Assess(PairBondFacts f)
      {
         if (f == null) return new PairBond();

         int personal = Clamp(f.Personal);
         int earned = f.Baseline is int b ? personal - Clamp(b) : 0;
         int start = personal - earned;

         if (start == 0) return new PairBond {Points = personal, Basis = earned == 0 ? PairBondBasis.None : PairBondBasis.Earned};

         string reason = ReasonFor(f, start);
         if (reason != null) return new PairBond {Points = personal, Basis = PairBondBasis.Known, Reason = WithTemperament(reason, f.Temperament, start)};

         PairBondEcho echo = Math.Abs(start) < EchoFloor ? PairBondEcho.None : start > 0 ? PairBondEcho.VagueGoodwill : PairBondEcho.VagueUnease;

         return new PairBond {Points = earned, Basis = PairBondBasis.Unexplained, Echo = echo};
      }

      /// <summary>
      ///   Why two people would know each other well enough for this start, or null. A war explains an enmity, never a
      ///   friendship; every other tie explains either.
      /// </summary>
      public static string ReasonFor(PairBondFacts f, int start)
      {
         if (f == null || start == 0) return null;
         if (!string.IsNullOrWhiteSpace(f.OutsideReason)) return f.OutsideReason.Trim();
         if (f.IsKin) return "kinship";
         if (f.SameHouse) return "the same house";
         if (f.BothRulers) return "years of dealings as rulers";
         if (f.SameRealm) return "the same court";
         if (f.Neighbours) return "neighbouring lands";
         if (f.RealmsAtWar && start < 0) return "a war between your realms";

         return null;
      }

      #region private

      private static int Clamp(int v) => Math.Max(-VanillaRelation.Bound, Math.Min(VanillaRelation.Bound, v));

      // Temperament colours a reason that stands on its own: alike natures in a friendship, clashing ones in an enmity.
      private static string WithTemperament(string reason, int temperament, int start)
      {
         if (temperament > 0 && start > 0) return reason + ", and alike in nature";
         if (temperament < 0 && start < 0) return reason + ", and at odds in nature";

         return reason;
      }

      #endregion
   }
}
