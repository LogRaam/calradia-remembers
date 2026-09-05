// Code written by Gabriel Mailhot, 04/09/2026.
// Strategic Assessment, the engine seam: decides WHO voices the counsel and gathers the player's own standing, so
// the pure StrategicAssessmentPromptBuilder / StrategicAssessmentSummary can shape the text. The rule (Gabriel):
// a companion (highest Tactics) counsels; failing that a common soldier of the warband stands in; and if the player
// travels alone, there is no advisor at all and the caller falls back to the offline summary.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
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

      /// <summary>The representative troop voicing the counsel (its portrait + class name stand in for the anonymous soldier), or null for a companion / alone.</summary>
      public CharacterObject AdvisorCharacter { get; set; }

      /// <summary>The wars presently under way across Calradia (who fights whom), so the counsel reasons about real fronts.</summary>
      public CalradiaWarState Wars { get; set; } = new CalradiaWarState(null);

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
         var ctx = new StrategicAssessmentContext {Player = BuildPlayerContext(result), Wars = BuildWarState()};

         try
         {
            Hero companion = PickBestCompanion();
            if (companion != null)
            {
               ctx.AdvisorHero = companion;
               ctx.Advisor = new StrategicAdvisor(
                  companion.Name?.ToString(), companion.Culture?.Name?.ToString(),
                  companion.GetSkillValue(DefaultSkills.Tactics), AdvisorKind.CompanionStrategist,
                  BuildAdvisorPersona(companion));
            }
            else
            {
               CharacterObject troop = PickBestTroop();
               ctx.AdvisorCharacter = troop; // the anonymous soldier's face + class name (its counsel voice stays nameless)
               ctx.Advisor = BuildSoldierAdvisor(troop);
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

      // The highest-Tactics companion who actually RIDES WITH the player, or null when none travel with them. A
      // companion stationed in a town (or away on their own errand) cannot counsel over the war table before the
      // player, so they are skipped even if their Tactics is higher (player report 2026-09-04: the advisor was one
      // "staying at Diathma"). PartyBelongedTo == MainParty is the "in my party right now" test.
      private static Hero PickBestCompanion()
      {
         IReadOnlyList<Hero> companions = Clan.PlayerClan?.Companions;
         if (companions == null || companions.Count == 0) return null;

         MobileParty main = MobileParty.MainParty;
         Hero best = null;
         var bestTactics = int.MinValue;
         foreach (Hero h in companions)
         {
            if (h == null || !h.IsAlive) continue;
            if (h.PartyBelongedTo != main) continue; // must be travelling with the player, not stationed elsewhere
            int tactics = h.GetSkillValue(DefaultSkills.Tactics);
            if (tactics > bestTactics)
            {
               bestTactics = tactics;
               best = h;
            }
         }

         return best;
      }

      // The companion's own character, in their own words, so the counsel is voiced as THAT person (the same voice
      // the chatroom gives them), not a faceless oracle: the vivid trait archetype (which already reflects any
      // authored traits), their culture/rank, and any authored backstory / conviction. Defensive: any failure just
      // yields an empty persona, and the advisor falls back to the plain strategist line.
      private static string BuildAdvisorPersona(Hero companion)
      {
         var parts = new List<string>();

         try
         {
            NpcProfile profile = HeroProfileMapper.FromHero(companion);
            if (!string.IsNullOrWhiteSpace(profile.Trait)) parts.Add(profile.Trait.Trim());
            if (!string.IsNullOrWhiteSpace(profile.Personality)) parts.Add(profile.Personality.Trim());
         }
         catch (Exception ex) { ModLog.Error($"[STRAT_ASSESS] advisor archetype failed: {ex.GetType().Name}: {ex.Message}"); }

         try
         {
            CharacterOverride ov = CharacterOverrideLoader.Set?.Resolve(companion.StringId, companion.Name?.ToString());
            if (ov != null)
            {
               if (ov.HasBackstory) parts.Add(ov.Backstory.Trim());
               if (ov.HasConviction) parts.Add($"They hold this to be true: {ov.Conviction.Trim()}");
            }
         }
         catch (Exception ex) { ModLog.Error($"[STRAT_ASSESS] advisor authored lore failed: {ex.GetType().Name}: {ex.Message}"); }

         var sb = new StringBuilder();
         foreach (string p in parts)
         {
            if (string.IsNullOrWhiteSpace(p)) continue;
            sb.Append(p.Trim());
            if (!p.TrimEnd().EndsWith(".")) sb.Append('.');
            sb.Append(' ');
         }

         return sb.ToString().Trim();
      }

      // Every war presently under way between two living kingdoms, named. This is the single richest input for a real
      // strategic reading (fronts, shared enemies) and the fact the model most wants to invent when it is missing.
      private static CalradiaWarState BuildWarState()
      {
         try
         {
            List<Kingdom> kingdoms = Kingdom.All?.Where(k => k != null && !k.IsEliminated).ToList() ?? new List<Kingdom>();
            var pairs = new List<WarPair>();

            for (var i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
               if (kingdoms[i].IsAtWarWith(kingdoms[j]))
                  pairs.Add(new WarPair(
                     kingdoms[i].Name?.ToString() ?? kingdoms[i].StringId,
                     kingdoms[j].Name?.ToString() ?? kingdoms[j].StringId));

            return new CalradiaWarState(pairs);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STRAT_ASSESS] war state failed: {ex.GetType().Name}: {ex.Message}");

            return new CalradiaWarState(null);
         }
      }

      // The highest-tier ordinary troop in the player's party, whose face and class name stand in for the anonymous
      // soldier advisor. Null when the player rides with no soldiers at all (then the caller goes offline).
      private static CharacterObject PickBestTroop()
      {
         MobileParty party = MobileParty.MainParty;
         if (party?.MemberRoster == null) return null;

         CharacterObject best = null;
         foreach (TaleWorlds.CampaignSystem.Roster.TroopRosterElement e in party.MemberRoster.GetTroopRoster())
         {
            if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
            if (best == null || e.Character.Tier > best.Tier) best = e.Character;
         }

         return best;
      }

      // The anonymous veteran who stands in when no companion is at hand: their keenness scaled modestly by the troop
      // tier. Name stays blank so the counsel voice reads as a nameless soldier; the panel shows the class + portrait.
      private static StrategicAdvisor BuildSoldierAdvisor(CharacterObject troop)
      {
         if (troop == null) return null; // the player travels alone

         int skill = Math.Min(troop.Tier * SoldierSkillPerTier, SoldierSkillCap);
         string culture = troop.Culture?.Name?.ToString() ?? Hero.MainHero?.Culture?.Name?.ToString() ?? "";

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
