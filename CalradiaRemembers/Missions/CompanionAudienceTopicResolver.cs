// Code written by Gabriel Mailhot, 22/07/2026.
// Player feedback (Nexus): companions travelling with the player "had a lot to say" but no way to raise it.
// The ratified design rule is that the mod must FIND a real thing worth saying BEFORE asking for an audience,
// never ask and hope the model invents one, mirroring the viable-quest menu's own pre-validated-against-the-
// live-world principle. This is the engine glue: it reads live state for each of the five topics and hands the
// facts to the pure policies (CompanionAudienceTopicPolicy et al. in CalradiaRemembers.Logic), which do the
// actual deciding. Nothing here decides anything; it only observes.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>One of the five found-topics as scanned against live state for a given companion.</summary>
   public sealed class CompanionAudienceTopicScan
   {
      public CompanionAudienceReason Topic { get; set; }

      /// <summary>Whether the topic's precondition actually holds right now.</summary>
      public bool Holds { get; set; }

      /// <summary>How strong/interesting this topic is (0..100), meaningful only when <see cref="Holds" />.</summary>
      public int Strength { get; set; }

      /// <summary>The concrete fact this topic would voice (the actual rumour/memory/grudge/counsel), when <see cref="Holds" />.</summary>
      public string Detail { get; set; }

      /// <summary>Why this topic did NOT qualify: diagnostic text for cr.audience_topics. Null when <see cref="Holds" />.</summary>
      public string WhyNot { get; set; }

      /// <summary>Topic-specific bookkeeping key (currently only the rumour's WorldEvent id); null otherwise.</summary>
      public string Key { get; set; }
   }

   /// <summary>
   ///   Scans one companion's live state against all seven found-topics. A pure no-op read: never mutates
   ///   anything, never consumes a mission report, marks a rumour reported, marks a kindness thanked for, or
   ///   marks a scheme warned of (the caller does that only once an audience is actually granted, in
   ///   <see cref="CompanionAudienceTopicBehavior" />).
   /// </summary>
   public static class CompanionAudienceTopicResolver
   {
      // A real deed already happened (the errand came home); the counsel should land solidly among the other
      // topics without automatically outranking a strong grudge or an old callback.
      private const int MissionFollowUpStrength = 55;

      private const int SurgeonMedicineThreshold = CompanionCounselPolicy.SurgeonMedicineThreshold;

      /// <summary>Scans all seven topics for <paramref name="companion" />, in enum order.</summary>
      public static List<CompanionAudienceTopicScan> Scan(
         Hero companion, ISet<string> alreadyReportedRumourIds, ISet<string> usedCallbackKeys, ISet<string> thankedKeys,
         ISet<string> warnedKeys)
      {
         var results = new List<CompanionAudienceTopicScan> {
            ResolveMissionFollowUp(companion),
            ResolveCompanionConflict(companion),
            ResolvePracticalCounsel(),
            ResolveCallback(companion, usedCallbackKeys),
            ResolveRumourReport(companion, alreadyReportedRumourIds),
            ResolveGratitude(companion, thankedKeys),
            ResolveSchemeWarning(companion, warnedKeys)
         };

         return results;
      }

      #region private: Mission follow-up

      private static CompanionAudienceTopicScan ResolveMissionFollowUp(Hero companion)
      {
         string report = CompanionMissionBehavior.Instance?.PeekNewsReport(companion);

         if (string.IsNullOrWhiteSpace(report))
            return NotHeld(CompanionAudienceReason.MissionFollowUp, "no errand report is waiting");

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.MissionFollowUp, Holds = true, Strength = MissionFollowUpStrength, Detail = report
         };
      }

      #endregion

      #region private: Companion conflict

      private static CompanionAudienceTopicScan ResolveCompanionConflict(Hero companion)
      {
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(companion?.StringId);

         if (grudges == null || grudges.Count == 0)
            return NotHeld(CompanionAudienceReason.CompanionConflict, "holds no grudge at all");

         var today = (int) CampaignTime.Now.ToDays;
         // Fully qualified: CalradiaRemembers.Logic.Grudges and CalradiaRemembers.Logic.Appreciations (needed
         // below, for ResolveGratitude) each declare their own TraitFacts.
         CalradiaRemembers.Logic.Grudges.TraitFacts traits = GrudgeBridge.BuildTraitFacts(companion);

         Grudge best = null;
         var bestMagnitude = 0;
         Hero bestTarget = null;

         foreach (Grudge g in grudges)
         {
            if (g == null || g.IsResolved) continue;

            Hero target = FindPartyCompanion(g.TargetId, companion);
            if (target == null) continue; // not a grudge against another member of THIS party

            int magnitude = GrudgePolicy.EffectiveMagnitude(g, today, traits);
            if (magnitude <= 0) continue;
            if (best != null && magnitude <= bestMagnitude) continue;

            best = g;
            bestMagnitude = magnitude;
            bestTarget = target;
         }

         if (best == null)
            return NotHeld(CompanionAudienceReason.CompanionConflict, "no live grudge against a fellow party member");

         string detail = string.IsNullOrWhiteSpace(best.CauseSummary)
            ? $"a grudge against {bestTarget.Name}"
            : $"against {bestTarget.Name}: {best.CauseSummary}";

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.CompanionConflict, Holds = true, Strength = bestMagnitude, Detail = detail
         };
      }

      /// <summary>The live hero for this StringId, only when they are another companion presently in the main party.</summary>
      private static Hero FindPartyCompanion(string targetId, Hero exclude)
      {
         if (string.IsNullOrEmpty(targetId)) return null;

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != exclude && h.StringId == targetId
            && h.IsPlayerCompanion && h.PartyBelongedTo == MobileParty.MainParty);
      }

      #endregion

      #region private: Practical counsel

      private static CompanionAudienceTopicScan ResolvePracticalCounsel()
      {
         MobileParty party = MobileParty.MainParty;

         if (party == null)
            return NotHeld(CompanionAudienceReason.PracticalCounsel, "no active party");

         var facts = new PartyCounselFacts {
            DistinctFoodKinds = DistinctFoodKinds(party),
            Morale = party.Morale,
            WoundedTroops = WoundedTroopCount(party),
            HasSurgeon = HasSurgeon(party),
            CarriedWeight = party.TotalWeightCarried,
            CarryCapacity = party.InventoryCapacity
         };

         (PartyCounselKind kind, int strength) = CompanionCounselPolicy.Select(facts);

         if (kind == PartyCounselKind.None)
            return NotHeld(CompanionAudienceReason.PracticalCounsel, "no measurable party condition currently qualifies");

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.PracticalCounsel, Holds = true, Strength = strength, Detail = DescribeCounsel(kind)
         };
      }

      private static string DescribeCounsel(PartyCounselKind kind)
      {
         switch (kind)
         {
            case PartyCounselKind.FoodMonotony: return "the party has been eating the same rations for far too long, with no variety to lift spirits at the fire";
            case PartyCounselKind.LowMorale: return "the camp's spirits have soured, and it shows in every face around the fire";
            case PartyCounselKind.WoundedNoSurgeon: return "wounded men march in the ranks with no one skilled enough to properly tend them";
            case PartyCounselKind.Overloaded: return "the pack train can barely bear what it is hauling, and it is slowing the whole column";
            default: return null;
         }
      }

      private static int DistinctFoodKinds(MobileParty party)
      {
         ItemRoster roster = party.ItemRoster;
         if (roster == null) return 0;

         var kinds = new HashSet<string>();
         foreach (ItemRosterElement el in roster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;
            if (item is {IsFood: true}) kinds.Add(item.StringId);
         }

         return kinds.Count;
      }

      private static bool HasSurgeon(MobileParty party)
      {
         foreach (TroopRosterElement el in party.MemberRoster.GetTroopRoster())
         {
            Hero hero = el.Character?.HeroObject;
            if (hero != null && hero.GetSkillValue(DefaultSkills.Medicine) >= SurgeonMedicineThreshold) return true;
         }

         return false;
      }

      private static int WoundedTroopCount(MobileParty party)
      {
         var wounded = 0;
         foreach (TroopRosterElement el in party.MemberRoster.GetTroopRoster())
            wounded += el.WoundedNumber;

         return wounded;
      }

      #endregion

      #region private: Callback

      private static CompanionAudienceTopicScan ResolveCallback(Hero companion, ISet<string> usedCallbackKeys)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion?.StringId);
         List<NotableEvent> events = profile?.Events;

         if (events == null || events.Count == 0)
            return NotHeld(CompanionAudienceReason.Callback, "no stored memory of shared history yet");

         var today = (int) CampaignTime.Now.ToDays;

         NotableEvent best = null;
         var bestStrength = 0;
         string bestKey = null;

         foreach (NotableEvent ev in events)
         {
            // NotableEvent has no id of its own; gameDay+type is a stable enough composite identity to
            // remember "already used as a callback" without ever resurfacing the SAME stored memory on
            // every audience the strongest-first ranking would otherwise keep picking.
            string key = CallbackKey(companion, ev);
            if (usedCallbackKeys != null && usedCallbackKeys.Contains(key)) continue;

            int strength = CompanionCallbackPolicy.Strength(ev.type, today - ev.gameDay);
            if (strength <= bestStrength) continue;

            best = ev;
            bestStrength = strength;
            bestKey = key;
         }

         if (best == null)
            return NotHeld(CompanionAudienceReason.Callback, "nothing old, significant, and not-yet-called-back-to remains");

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.Callback, Holds = true, Strength = bestStrength, Detail = best.summary, Key = bestKey
         };
      }

      /// <summary>The composite identity used to remember a NotableEvent already spent as a callback.</summary>
      public static string CallbackKey(Hero companion, NotableEvent ev)
         => $"{companion?.StringId}:{ev.gameDay}:{(int) ev.type}";

      #endregion

      #region private: Rumour report

      private static CompanionAudienceTopicScan ResolveRumourReport(Hero companion, ISet<string> alreadyReported)
      {
         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> rated =
            WorldEventStore.Instance?.GetKnownEventsRated(companion, today, 8)
            ?? Array.Empty<(WorldEvent, RumorConfidence)>();

         foreach ((WorldEvent ev, RumorConfidence confidence) in rated)
         {
            if (ev == null || string.IsNullOrEmpty(ev.Id)) continue;
            if (alreadyReported != null && alreadyReported.Contains(ev.Id)) continue;

            int strength = CompanionRumourPolicy.Strength(ev.Magnitude, confidence);
            if (strength <= 0) continue;

            return new CompanionAudienceTopicScan {
               Topic = CompanionAudienceReason.RumourReport, Holds = true, Strength = strength, Detail = ev.Summary, Key = ev.Id
            };
         }

         return NotHeld(CompanionAudienceReason.RumourReport, "no un-reported rumour this companion could plausibly have heard");
      }

      #endregion

      #region private: Gratitude

      private static CompanionAudienceTopicScan ResolveGratitude(Hero companion, ISet<string> thankedKeys)
      {
         int happiness = CompanionHappinessService.Get(companion);

         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<Appreciation> live = AppreciationBehavior.Instance?.LiveAppreciationsFor(companion, today)
            ?? Array.Empty<Appreciation>();

         if (live.Count == 0)
            return NotHeld(CompanionAudienceReason.Gratitude, "holds no live appreciation of the player at all");

         // Fully qualified: CalradiaRemembers.Logic.Grudges and CalradiaRemembers.Logic.Appreciations each
         // declare their own TraitFacts (the mirrored design AppreciationPolicy's header describes), and this
         // file already uses the Grudges one unqualified above (ResolveCompanionConflict).
         CalradiaRemembers.Logic.Appreciations.TraitFacts traits = AppreciationBridge.BuildTraitFacts(companion);

         // Already ranked strongest-first by AppreciationBehavior.LiveAppreciationsFor, so the first one that
         // clears the pure gate below is also the strongest qualifying kindness, mirroring RumourReport's
         // already-sorted iterate-then-take-first shape (Callback needs its own manual best-tracking only
         // because its source list is NOT pre-sorted).
         foreach (Appreciation a in live)
         {
            if (a == null || string.IsNullOrEmpty(a.Id)) continue;
            if (thankedKeys != null && thankedKeys.Contains(a.Id)) continue;

            int effectiveMagnitude = AppreciationPolicy.EffectiveMagnitude(a, today, traits);
            int daysAgo = today - a.EventDay;

            if (!CompanionGratitudePolicy.Holds(effectiveMagnitude, daysAgo, happiness)) continue;

            return new CompanionAudienceTopicScan {
               Topic = CompanionAudienceReason.Gratitude, Holds = true,
               Strength = CompanionGratitudePolicy.Strength(effectiveMagnitude),
               Detail = string.IsNullOrWhiteSpace(a.CauseSummary) ? "a kindness you showed me" : a.CauseSummary,
               Key = a.Id
            };
         }

         return NotHeld(CompanionAudienceReason.Gratitude, "no recent, significant, not-yet-thanked-for kindness remains");
      }

      #endregion

      #region private: Scheme warning

      private static CompanionAudienceTopicScan ResolveSchemeWarning(Hero companion, ISet<string> warnedKeys)
      {
         if (Hero.MainHero == null || SchemeStore.Instance?.HasSecretSchemeAgainst(Hero.MainHero) != true)
            return NotHeld(CompanionAudienceReason.SchemeWarning, "no secret scheme against you is currently warnable");

         Hero plotter = SchemeStore.Instance.GetSecretSchemerAgainst(Hero.MainHero);
         if (plotter == null)
            return NotHeld(CompanionAudienceReason.SchemeWarning, "the live plot could not be attributed to a plotter");

         string key = SchemeWarningKey(plotter, Hero.MainHero);
         if (warnedKeys != null && warnedKeys.Contains(key))
            return NotHeld(CompanionAudienceReason.SchemeWarning, "already warned of this exact plot");

         int happiness = CompanionHappinessService.Get(companion);
         if (!CompanionSchemeWarningPolicy.Holds(happiness))
            return NotHeld(CompanionAudienceReason.SchemeWarning, "not content enough in the player's service to risk carrying this warning");

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.SchemeWarning, Holds = true,
            Strength = CompanionSchemeWarningPolicy.Strength,
            Detail = $"word that {plotter.Name} is moving against you",
            Key = key
         };
      }

      /// <summary>
      ///   The composite identity used to remember a scheme already spent as a SchemeWarning: Scheme itself
      ///   carries no id of its own (see WorldEvents.Scheme), and a plotter/target pair is unique among active
      ///   schemes (SchemeStore.HasActiveBetween), so it is a stable enough identity for spend-once bookkeeping.
      /// </summary>
      public static string SchemeWarningKey(Hero plotter, Hero target)
         => $"{plotter?.StringId}:{target?.StringId}";

      #endregion

      private static CompanionAudienceTopicScan NotHeld(CompanionAudienceReason topic, string whyNot)
         => new CompanionAudienceTopicScan {Topic = topic, Holds = false, WhyNot = whyNot};
   }
}
