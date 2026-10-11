// Code written by Gabriel Mailhot, 24/06/2026.
// The engine half of the guard contract: maps a guard keyword to a real game fact, against the fired event's
// context. The pure GuardEvaluator AND-combines these. An UNKNOWN guard fails (conservative — a rule that
// names a guard we don't implement must not fire by accident), and is logged so a modder sees the typo.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Resolves a single scenario guard keyword to true/false against the event context.</summary>
   public static class ScenarioGuardResolver
   {
      /// <summary>The guards <see cref="Resolve" /> knows; any other fails (checked by cr.testall scenario_packs).</summary>
      public static readonly IReadOnlyCollection<string> KnownVerbs = new[]
      {
         "IF_PLAYER_RULES", "IF_PLAYER_IS_FEMALE", "IF_PLAYER_IS_MALE", "IF_PLAYER_INVOLVED", "IF_HERO_IS", "IF_PLAYER_AT_WAR"
      };

      public static bool Resolve(KeywordToken guard, ScenarioContext context)
      {
         if (guard == null || context == null) return false;

         switch (guard.Verb)
         {
            case "IF_PLAYER_RULES":     return PlayerRules();
            case "IF_PLAYER_IS_FEMALE": return Hero.MainHero?.IsFemale == true;
            case "IF_PLAYER_IS_MALE":   return Hero.MainHero?.IsFemale == false;
            case "IF_PLAYER_INVOLVED":  return context.InvolvesPlayer;
            case "IF_HERO_IS":          return context.InvolvesHero(guard.Payload);
            case "IF_PLAYER_AT_WAR":    return PlayerAtWar(guard.Payload, context);
            default:
               ModLog.Info($"[SCENARIO] unknown guard '{guard.Verb}' — treated as failing (rule will not fire).");
               return false;
         }
      }

      private static bool PlayerRules()
         => Hero.MainHero?.MapFaction is Kingdom kingdom && kingdom.Leader == Hero.MainHero;

      private static bool PlayerAtWar(string factionRef, ScenarioContext context)
      {
         IFaction other = context.ResolveFaction(factionRef);
         IFaction player = Hero.MainHero?.MapFaction;
         if (other == null || player == null) return false;

         return FactionManager.IsAtWarAgainstFaction(player, other);
      }
   }
}
