// Code written by Gabriel Mailhot, 04/09/2026.
// Strategic Assessment, the engine seam: decides WHO voices the counsel and gathers the player's own standing, so
// the pure StrategicAssessmentPromptBuilder / StrategicAssessmentSummary can shape the text. The rule (Gabriel):
// a companion (highest Tactics) counsels; failing that a common soldier of the warband stands in; and if the player
// travels alone, there is no advisor at all and the caller falls back to the offline summary.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>The gathered inputs for the Strategic Assessment: who speaks (or no one), and the player's own position.</summary>
   public sealed class StrategicAssessmentContext
   {
      /// <summary>The voice of the counsel, or null when the player travels alone (then the caller uses the offline summary).</summary>
      public StrategicAdvisor Advisor { get; set; }

      /// <summary>The companion Hero voicing the counsel (for their portrait), or null for a soldier / alone.</summary>
      public Hero AdvisorHero { get; set; }

      /// <summary>True when an advisor (companion or soldier) will voice an LLM counsel; false when alone (offline summary).</summary>
      public bool HasAdvisor => Advisor != null;

      /// <summary>The player's realm, rank and wars, so the counsel is FOR them.</summary>
      public PlayerStrategicContext Player { get; set; } = new PlayerStrategicContext("", 0, null);
   }

   /// <summary>Chooses the advisor and gathers the player's strategic context from the live campaign.</summary>
   public static class StrategicAssessmentContextBuilder
   {
      // A soldier is no strategist: their counsel tops out low. Scale it modestly by the best troop tier so a warband
      // of elites reads a touch keener than a rabble, but never reaches a trained officer's tier.
      private const int SoldierSkillPerTier = 10;
      private const int SoldierSkillCap = 60;

      public static StrategicAssessmentContext Build(PowerIndexResult result)
      {
         var ctx = new StrategicAssessmentContext {Player = BuildPlayerContext(result)};

         try
         {
            Hero companion = PickBestCompanion();
            if (companion != null)
            {
               ctx.AdvisorHero = companion;
               ctx.Advisor = new StrategicAdvisor(
                  companion.Name?.ToString(), companion.Culture?.Name?.ToString(),
                  companion.GetSkillValue(DefaultSkills.Tactics), AdvisorKind.CompanionStrategist);
            }
            else
            {
               ctx.Advisor = PickSoldierAdvisor(); // no hero portrait: a soldier is anonymous
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STRAT_ASSESS] advisor selection failed (falling back to the offline summary): {ex.GetType().Name}: {ex.Message}");
            ctx.Advisor = null;
            ctx.AdvisorHero = null;
         }

         return ctx;
      }

      #region private

      // The player's living companion with the highest Tactics, or null when the clan has none.
      private static Hero PickBestCompanion()
      {
         IReadOnlyList<Hero> companions = Clan.PlayerClan?.Companions;
         if (companions == null || companions.Count == 0) return null;

         Hero best = null;
         var bestTactics = int.MinValue;
         foreach (Hero h in companions)
         {
            if (h == null || !h.IsAlive) continue;
            int tactics = h.GetSkillValue(DefaultSkills.Tactics);
            if (tactics > bestTactics)
            {
               bestTactics = tactics;
               best = h;
            }
         }

         return best;
      }

      // No companion, but the party carries soldiers: an anonymous veteran stands in, their keenness scaled modestly
      // by the best troop tier. Null when the player rides with no soldiers at all (then the caller goes offline).
      private static StrategicAdvisor PickSoldierAdvisor()
      {
         MobileParty party = MobileParty.MainParty;
         if (party?.MemberRoster == null) return null;

         var bestTier = 0;
         var hasTroop = false;
         foreach (TaleWorlds.CampaignSystem.Roster.TroopRosterElement e in party.MemberRoster.GetTroopRoster())
         {
            if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
            hasTroop = true;
            if (e.Character.Tier > bestTier) bestTier = e.Character.Tier;
         }

         if (!hasTroop) return null; // the player travels alone

         int skill = Math.Min(bestTier * SoldierSkillPerTier, SoldierSkillCap);
         string culture = Hero.MainHero?.Culture?.Name?.ToString() ?? "";

         return new StrategicAdvisor("", culture, skill, AdvisorKind.CommonSoldier);
      }

      private static PlayerStrategicContext BuildPlayerContext(PowerIndexResult result)
      {
         try
         {
            Kingdom kingdom = Hero.MainHero?.Clan?.Kingdom;
            if (kingdom == null || kingdom.IsEliminated)
               return new PlayerStrategicContext("", 0, null);

            string name = kingdom.Name?.ToString() ?? kingdom.StringId;
            int rank = RankOf(result, kingdom.StringId);
            List<string> wars = WarsOf(kingdom);

            return new PlayerStrategicContext(name, rank, wars);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STRAT_ASSESS] player context failed: {ex.GetType().Name}: {ex.Message}");

            return new PlayerStrategicContext("", 0, null);
         }
      }

      private static int RankOf(PowerIndexResult result, string kingdomId)
      {
         IReadOnlyList<KingdomPowerScore> ranked = result?.Ranked;
         if (ranked == null) return 0;
         foreach (KingdomPowerScore s in ranked)
            if (s != null && s.KingdomId == kingdomId)
               return s.Rank;

         return 0;
      }

      private static List<string> WarsOf(Kingdom kingdom)
      {
         var wars = new List<string>();
         if (Kingdom.All == null) return wars;

         foreach (Kingdom other in Kingdom.All)
         {
            if (other == null || other == kingdom || other.IsEliminated) continue;
            if (kingdom.IsAtWarWith(other)) wars.Add(other.Name?.ToString() ?? other.StringId);
         }

         return wars;
      }

      #endregion
   }
}
