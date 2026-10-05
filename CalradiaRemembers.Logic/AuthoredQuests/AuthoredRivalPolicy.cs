// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales: the {rival} a tale may need (docs/design/quests/smoke_over_the_hills.json: the farms were burned by
// riders of another house of the giver's own realm). The rival is the leader of another clan of the giver's realm:
// someone the player can find and speak to, never the giver, never the player or the player's own house, never
// someone absent from the world. The engine reads the realm; this decides who may be bound.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>One lord who might be bound as the {rival}, as the engine reads him.</summary>
   public sealed class RivalCandidate
   {
      public string HeroId { get; init; }

      /// <summary>He leads his clan. A {rival} is a house's head, the one who answers for its riders.</summary>
      public bool LeadsHisClan { get; init; }

      /// <summary>His clan serves the giver's realm. False: he is no rival "of the giver's own realm".</summary>
      public bool OfTheGiversRealm { get; init; }

      /// <summary>He is the giver himself, or of the giver's clan: a rival is another house.</summary>
      public bool OfTheGiversHouse { get; init; }

      /// <summary>He is the player, or of the player's clan: the player cannot be told to expose themselves.</summary>
      public bool OfThePlayersHouse { get; init; }

      /// <summary>He is in the world (not dead, not disabled, not a prisoner far away): someone the player can go and speak to.</summary>
      public bool InTheWorld { get; init; }
   }

   /// <summary>Chooses the {rival} among the lords the engine offers. Pure.</summary>
   public static class AuthoredRivalPolicy
   {
      public const string NoneEligible = "no other house of their realm has a leader the player could find";

      /// <summary>Whether this lord may be the rival.</summary>
      public static bool Eligible(RivalCandidate c)
         => c != null && !string.IsNullOrEmpty(c.HeroId) && c.LeadsHisClan && c.OfTheGiversRealm && !c.OfTheGiversHouse && !c.OfThePlayersHouse && c.InTheWorld;

      /// <summary>
      ///   The rival's id, picked among the eligible by <paramref name="roll" /> (0 to 1, the engine's dice), or null with
      ///   the reason when none may be bound (the tale is then not offered by this giver).
      /// </summary>
      public static string Choose(IEnumerable<RivalCandidate> candidates, double roll, out string whyNot)
      {
         List<RivalCandidate> eligible = (candidates ?? Enumerable.Empty<RivalCandidate>()).Where(Eligible).ToList();
         whyNot = eligible.Count == 0 ? NoneEligible : null;
         if (eligible.Count == 0) return null;

         int index = (int) (roll * eligible.Count);

         return eligible[index < 0 ? 0 : index >= eligible.Count ? eligible.Count - 1 : index].HeroId;
      }
   }
}
