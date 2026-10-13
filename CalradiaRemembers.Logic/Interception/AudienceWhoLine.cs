// Code written by Gabriel Mailhot, 13/10/2026.
// Who is asking for a word, said in the invitation itself.
//
// Lordfadooboo (Nexus, 13/10/2026): "(person) wants to speak to you" when often i find myself saying "who is that? and
// dont know if i want to actually talk to that person." AudienceTopicLine says what the matter is; this says who: the
// name with house and culture (HeroLabel), their station, whether the player has met them, and their regard on CR's
// own ledger in the encyclopedia's words (RegardBands), so the popup and the encyclopedia never disagree.

#region

using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>What the invitation can say about who is asking.</summary>
   public sealed class AudienceWhoFacts
   {
      /// <summary>Their name with house and culture (HeroLabel.Describe). Blank: nothing is said.</summary>
      public string Label { get; init; }

      /// <summary>Their station in the game's words ("a lord of Battania", "a merchant of Askar"), or null.</summary>
      public string Station { get; init; }

      /// <summary>The game's own flag that the player has met them.</summary>
      public bool HasMet { get; init; }

      /// <summary>Their personal regard on CR's ledger, or null when CR holds no memory of them.</summary>
      public int? Regard { get; init; }
   }

   /// <summary>What decides how someone's station is said.</summary>
   public sealed class AudienceStationFacts
   {
      /// <summary>A lord (a noble of a house), as opposed to a notable or a wanderer.</summary>
      public bool IsLord { get; init; }

      /// <summary>They rule their realm.</summary>
      public bool IsRuler { get; init; }

      /// <summary>They lead their house.</summary>
      public bool IsClanLeader { get; init; }

      /// <summary>Their house's name, or null.</summary>
      public string ClanName { get; init; }

      /// <summary>Their realm's name, or null when their house serves none.</summary>
      public string RealmName { get; init; }

      /// <summary>The game's own word for their trade ("Merchant"), for anyone who is not a lord.</summary>
      public string OccupationName { get; init; }

      /// <summary>The town or village a notable belongs to, or null.</summary>
      public string SettlementName { get; init; }
   }

   /// <summary>Someone's station in a few words ("ruler of Vlandia", "a merchant of Askar"). Pure.</summary>
   public static class AudienceStation
   {
      /// <summary>The station, or null when nothing is known.</summary>
      public static string Describe(AudienceStationFacts f)
      {
         if (f == null) return null;

         bool hasRealm = !string.IsNullOrWhiteSpace(f.RealmName);
         if (f.IsLord)
         {
            if (f.IsRuler && hasRealm) return "ruler of " + f.RealmName.Trim();
            if (f.IsClanLeader && !string.IsNullOrWhiteSpace(f.ClanName))
               return "head of " + f.ClanName.Trim() + (hasRealm ? ", sworn to " + f.RealmName.Trim() : "");

            return hasRealm ? "a lord of " + f.RealmName.Trim() : "a lord";
         }

         if (string.IsNullOrWhiteSpace(f.OccupationName)) return null;

         string trade = f.OccupationName.Trim().ToLowerInvariant();
         string article = "aeiou".IndexOf(trade[0]) >= 0 ? "an " : "a ";

         return article + trade + (string.IsNullOrWhiteSpace(f.SettlementName) ? "" : " of " + f.SettlementName.Trim());
      }
   }

   /// <summary>The "Who: ..." paragraph of an audience invitation. Pure.</summary>
   public static class AudienceWhoLine
   {
      /// <summary>
      ///   "\n\nWho: Luichan of Fen Company (Battanian), a lord of Battania. You have never met." Someone CR remembers
      ///   has been met, whatever the engine's flag says, and their regard is given; someone CR does not remember gets
      ///   no regard at all, never an invented one. Empty when there is no name.
      /// </summary>
      public static string For(AudienceWhoFacts f)
      {
         if (f == null || string.IsNullOrWhiteSpace(f.Label)) return "";

         string who = f.Label.Trim();
         if (!string.IsNullOrWhiteSpace(f.Station)) who += ", " + f.Station.Trim();

         bool met = f.HasMet || f.Regard.HasValue;
         string line = "\n\nWho: " + who + "." + (met ? " You have met." : " You have never met.");
         if (f.Regard.HasValue) line += " Their regard for you: " + RegardBands.Describe(f.Regard.Value) + ".";

         return line;
      }
   }
}
