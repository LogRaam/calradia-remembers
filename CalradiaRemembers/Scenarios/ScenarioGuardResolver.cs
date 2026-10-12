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
         "IF_PLAYER_RULES", "IF_PLAYER_IS_FEMALE", "IF_PLAYER_IS_MALE", "IF_PLAYER_INVOLVED", "IF_HERO_IS", "IF_PLAYER_AT_WAR",
         "IF_AT_WAR", "IF_AT_PEACE", "IF_ALIVE", "IF_KINGDOM_EXISTS", "IF_RULES"
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
            case "IF_HERO_IS":          return context.IsAbout(guard.Payload);
            case "IF_PLAYER_AT_WAR":    return PlayerAtWar(guard.Payload, context);
            // Gabriel (11/10/2026): guards that check the game is in a state where the scenario makes sense.
            case "IF_AT_WAR":           return Pair(guard, context, out IFaction w1, out IFaction w2) && FactionManager.IsAtWarAgainstFaction(w1, w2);
            case "IF_AT_PEACE":         return Pair(guard, context, out IFaction p1, out IFaction p2) && !FactionManager.IsAtWarAgainstFaction(p1, p2);
            case "IF_ALIVE":            return Mapping.HeroPresence.IsInTheWorld(context.ResolveHero(guard.Payload));
            case "IF_KINGDOM_EXISTS":   return context.ResolveFaction(guard.Payload) is Kingdom k && !k.IsEliminated;
            case "IF_RULES":            return Rules(guard, context);
            default:
               ModLog.Info($"[SCENARIO] unknown guard '{guard.Verb}' — treated as failing (rule will not fire).");
               return false;
         }
      }

      // Two distinct factions named by the guard's two arguments (IF_AT_WAR:empire,empire_s).
      private static bool Pair(KeywordToken guard, ScenarioContext context, out IFaction a, out IFaction b)
      {
         a = guard.Args.Count > 0 ? context.ResolveFaction(guard.Args[0]) : null;
         b = guard.Args.Count > 1 ? context.ResolveFaction(guard.Args[1]) : null;

         return a != null && b != null && a != b;
      }

      // IF_RULES:hero,kingdom: the hero leads that kingdom, which still stands. IF_RULES:hero alone (a tale's {husband}):
      // the hero leads the kingdom he belongs to.
      private static bool Rules(KeywordToken guard, ScenarioContext context)
      {
         Hero hero = guard.Args.Count > 0 ? context.ResolveHero(guard.Args[0]) : null;
         if (guard.Args.Count == 1) return hero?.MapFaction is Kingdom own && !own.IsEliminated && own.Leader == hero;
         Kingdom kingdom = guard.Args.Count > 1 ? context.ResolveFaction(guard.Args[1]) as Kingdom : null;

         return hero != null && kingdom != null && !kingdom.IsEliminated && kingdom.Leader == hero;
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
