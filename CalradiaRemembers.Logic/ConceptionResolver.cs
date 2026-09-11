// Code written by Gabriel Mailhot, 24/06/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>Which participant, if either, becomes the mother of a conception in this scene.</summary>
   public enum ConceptionRole
   {
      /// <summary>No conception is possible — a same-sex pairing, or no fertile female with a living male partner.</summary>
      None,

      /// <summary>The player is the mother; the NPC is the father.</summary>
      PlayerIsMother,

      /// <summary>The NPC is the mother; the player is the father.</summary>
      NpcIsMother
   }

   /// <summary>
   ///   Decides, from the two participants' sex / life / age, which (if either) could conceive from a
   ///   completed vaginal act. Conception needs exactly one fertile-female mother and a living male father;
   ///   a same-sex pairing, or a female outside the fertile window, yields <see cref="ConceptionRole.None" />.
   ///   Pure and engine-agnostic — the mod maps real heroes onto these primitives and performs the act.
   /// </summary>
   public static class ConceptionResolver
   {
      /// <summary>Oldest age a female can be the mother of a conception.</summary>
      public const double MaxFertileAge = 45d;

      /// <summary>Youngest age a female can be the mother of a conception.</summary>
      public const double MinFertileAge = 18d;

      /// <summary>
      ///   One person in the scene, as far as conception is concerned. Named rather than positional
      ///   (2026-09-10): the old signature was two of these triples laid end to end, six loose values whose
      ///   only distinction was their order, and the two halves are not interchangeable: swapping them
      ///   returns the OTHER role, which puts the pregnancy, and the child, in the wrong clan.
      /// </summary>
      public sealed class Participant
      {
         /// <summary>She could be the mother; a male is instead a candidate father.</summary>
         public bool IsFemale { get; set; }

         /// <summary>The dead conceive nothing, and father nothing either.</summary>
         public bool IsAlive { get; set; }

         /// <summary>Their age in years, weighed against the fertile window when they are female.</summary>
         public double Age { get; set; }
      }

      /// <summary>The two people the act was between, each named so neither can stand in for the other.</summary>
      public sealed class ConceptionFacts
      {
         /// <summary>The player.</summary>
         public Participant Player { get; set; }

         /// <summary>The other party.</summary>
         public Participant Npc { get; set; }
      }

      /// <summary>
      ///   True when this participant is a female who could carry a child: alive and within the fertile
      ///   window. An unknown participant is not fertile.
      /// </summary>
      public static bool IsFertileMother(Participant who)
         => who != null && who.IsFemale && who.IsAlive && who.Age is >= MinFertileAge and <= MaxFertileAge;

      /// <summary>
      ///   Resolves the mother (if any) for a player + NPC pairing: the fertile female is the mother and the
      ///   other party must be a living male father; otherwise <see cref="ConceptionRole.None" />. Two females
      ///   (no father) or two males (no mother) both resolve to None, as does anything unknown.
      /// </summary>
      public static ConceptionRole Resolve(ConceptionFacts facts)
      {
         if (facts?.Player == null || facts.Npc == null) return ConceptionRole.None;

         if (IsFertileMother(facts.Player) && IsViableFather(facts.Npc))
            return ConceptionRole.PlayerIsMother;

         if (IsFertileMother(facts.Npc) && IsViableFather(facts.Player))
            return ConceptionRole.NpcIsMother;

         return ConceptionRole.None;
      }

      #region private

      /// <summary>A living male is the only valid father.</summary>
      private static bool IsViableFather(Participant who)
         => who != null && !who.IsFemale && who.IsAlive;

      #endregion
   }
}