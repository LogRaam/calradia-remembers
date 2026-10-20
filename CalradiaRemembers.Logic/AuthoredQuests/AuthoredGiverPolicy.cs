// Code written by Gabriel Mailhot, 11/10/2026.
// Who may give a written tale: the role the tale asks for, and, since Gabriel's tale of intimacy (10/10/2026), the
// player's own spouse, of a sex the tale names, travelling in the player's party, and not already with child ("il faut
// aussi que la femme ne soit pas deja enceinte"). The giver's rules never read the player's own sex: a woman player married to
// a woman gets the tale written for a wife. A tale may ask the player's sex since 2.7.19 (requires.player.gender, tashmetu
// 20/10/2026; AuthoredPlayerPolicy), and a notable giver's trade (requires.giver.trade: "players keep asking for tales
// from town merchants").

#region

using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What the game knows of a character who might give a tale.</summary>
   public sealed class GiverFacts
   {
      public bool IsLord { get; init; }

      public bool IsNotable { get; init; }

      public bool IsGangLeader { get; init; }

      /// <summary>The player's own husband or wife.</summary>
      public bool IsPlayersSpouse { get; init; }

      /// <summary>A companion of the player's house (hired, not kin by blood or marriage).</summary>
      public bool IsPlayersCompanion { get; init; }

      public bool IsFemale { get; init; }

      /// <summary>Travelling in the player's own party.</summary>
      public bool InPlayersParty { get; init; }

      /// <summary>Already with child.</summary>
      public bool IsPregnant { get; init; }

      /// <summary>Married, and not to the player (a living spouse in the world).</summary>
      public bool IsMarriedToAnother { get; init; }

      /// <summary>Leads her (or his) own house.</summary>
      public bool IsClanLeader { get; init; }

      /// <summary>Of the player's own realm.</summary>
      public bool IsOfPlayersRealm { get; init; }

      /// <summary>A notable's trade key ("merchant", "artisan", "ruralnotable", "headman", "preacher"); null for anyone else.</summary>
      public string Trade { get; init; }
   }

   /// <summary>What the game knows of the player, for a tale's requires.player.</summary>
   public sealed class AuthoredPlayerFacts
   {
      /// <summary>The player's clan tier, 0 to 6.</summary>
      public int ClanTier { get; init; }

      /// <summary>The player's character is a woman.</summary>
      public bool IsFemale { get; init; }
   }

   /// <summary>Whether the player fits the tale's requires.player. Pure.</summary>
   public static class AuthoredPlayerPolicy
   {
      /// <summary>Null when the player fits; otherwise why not, for cr.story_check.</summary>
      public static string WhyNot(PlayerRequirement p, AuthoredPlayerFacts f)
      {
         f ??= new AuthoredPlayerFacts();
         if (p == null) return null;
         if (f.ClanTier < p.MinClanTier) return $"your clan is tier {f.ClanTier}, and the quest needs tier {p.MinClanTier}";
         string gender = p.Gender?.Trim().ToLowerInvariant();
         if (gender == "female" && !f.IsFemale) return "the quest is told to a woman";
         if (gender == "male" && f.IsFemale) return "the quest is told to a man";

         return null;
      }
   }

   /// <summary>What the game knows of the giver's husband.</summary>
   public sealed class HusbandFacts
   {
      /// <summary>The giver has a spouse at all.</summary>
      public bool Exists { get; init; }

      /// <summary>Alive and in the world (not disabled).</summary>
      public bool InTheWorld { get; init; }

      /// <summary>Rules a realm.</summary>
      public bool RulesARealm { get; init; }
   }

   /// <summary>Whether the giver's husband fits the tale. Pure.</summary>
   public static class AuthoredHusbandPolicy
   {
      /// <summary>Null when he fits; otherwise why not, for cr.story_check.</summary>
      public static string WhyNot(HusbandRequirement h, HusbandFacts f)
      {
         f ??= new HusbandFacts();
         if (!f.Exists) return "the quest wants the giver's husband, and she has none";
         if (!f.InTheWorld) return "the quest wants the giver's husband, and he is not in the world";
         if (h?.RulesARealm == true && !f.RulesARealm) return "the quest wants a husband who rules a realm, and hers rules no realm";

         return null;
      }
   }

   /// <summary>Whether a character fits the giver a tale asks for. Pure.</summary>
   public static class AuthoredGiverPolicy
   {
      /// <summary>Null when they fit; otherwise the first thing that does not, in plain words for cr.story_check.</summary>
      public static string WhyNot(GiverRequirement g, GiverFacts f)
      {
         g ??= new GiverRequirement();
         f ??= new GiverFacts();

         bool roleOk = g.Role switch {
            "lord" => f.IsLord,
            "notable" => f.IsNotable,
            "gang_leader" => f.IsGangLeader,
            "spouse" => f.IsPlayersSpouse,
            "companion" => f.IsPlayersCompanion,
            _ => true
         };
         if (!roleOk) return g.Role switch {
            "spouse" => "the quest wants the player's own spouse",
            "companion" => "the quest wants one of the player's companions",
            _ => $"the quest wants a giver who is a {g.Role}"
         };

         string gender = g.Gender?.Trim().ToLowerInvariant();
         if (gender == "female" && !f.IsFemale) return "the quest wants a woman to give it";
         if (gender == "male" && f.IsFemale) return "the quest wants a man to give it";

         if (g.WithPlayer && !f.InPlayersParty) return "the quest wants the giver travelling in your party";
         if (g.NotPregnant && f.IsPregnant) return "they are with child, and the quest waits until they are not";
         if (g.Married && !f.IsMarriedToAnother) return "the quest wants a giver married to someone other than you";
         if (g.NotClanLeader && f.IsClanLeader) return "they lead their house, and the quest wants one who could leave it";
         if (g.NotOfPlayersRealm && f.IsOfPlayersRealm) return "they are of your own realm";
         if (g.Trade is {Count: > 0} && !g.Trade.Any(t => string.Equals(t?.Trim().ToLowerInvariant(), f.Trade, System.StringComparison.Ordinal)))
            return $"the quest wants a {string.Join(" or ", g.Trade.Select(t => t?.Trim()))}, and they are {(f.Trade == null ? "no notable of a trade" : "a " + f.Trade)}";

         return null;
      }
   }
}
