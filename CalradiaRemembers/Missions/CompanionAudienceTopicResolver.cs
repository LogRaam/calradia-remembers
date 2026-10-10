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
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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
   ///   Scans one companion's live state against all eight found-topics. A pure no-op read: never mutates
   ///   anything, never consumes a mission report, marks a rumour reported, marks a kindness thanked for,
   ///   marks a scheme warned of, or stamps an ambition asked (the caller does that only once an audience is
   ///   actually granted, in <see cref="CompanionAudienceTopicBehavior" />).
   /// </summary>
   public static class CompanionAudienceTopicResolver
   {
      // A real deed already happened (the errand came home); the counsel should land solidly among the other
      // topics without automatically outranking a strong grudge or an old callback.
      private const int MissionFollowUpStrength = 55;

      private const int SurgeonMedicineThreshold = CompanionCounselPolicy.SurgeonMedicineThreshold;

      // Ambition's own party-role fit bar: mirrors SurgeonMedicineThreshold exactly (100, "a genuinely trained
      // level, not a dabbler's handful of points"), reused for the OTHER three roles too so the bar for "worth
      // promoting into a post" reads the same regardless of which skill it is measured on.
      private const int PartyRoleQualifiedSkillThreshold = SurgeonMedicineThreshold;

      /// <summary>Scans all eight topics for <paramref name="companion" />, in enum order.</summary>
      public static List<CompanionAudienceTopicScan> Scan(
         Hero companion, ISet<string> alreadyReportedRumourIds, ISet<string> usedCallbackKeys, ISet<string> thankedKeys,
         ISet<string> warnedKeys, IDictionary<string, int> ambitionAskedDays)
      {
         var results = new List<CompanionAudienceTopicScan> {
            ResolveMissionFollowUp(companion),
            ResolveCompanionConflict(companion),
            ResolvePracticalCounsel(),
            ResolveCallback(companion, usedCallbackKeys),
            ResolveRumourReport(companion, alreadyReportedRumourIds),
            ResolveGratitude(companion, thankedKeys),
            ResolveSchemeWarning(companion, warnedKeys),
            ResolveAmbition(companion, ambitionAskedDays)
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

      /// <summary>
      ///   The live hero for this StringId, only when they are another traveller of the player's party who may ask for
      ///   a word (a companion, or the player's family since 09/10/2026): a grudge between a sister and a companion counts.
      /// </summary>
      private static Hero FindPartyCompanion(string targetId, Hero exclude)
      {
         if (string.IsNullOrEmpty(targetId)) return null;

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != exclude && h.StringId == targetId
            && TravellingAudiencePolicy.MayAskForAWord(TravellerFactsOf(h)));
      }

      /// <summary>What the private-word roster needs to know of a hero (TravellingAudiencePolicy).</summary>
      public static TravellerFacts TravellerFactsOf(Hero h)
         => new() {
            IsPlayer = h == Hero.MainHero,
            IsHiredCompanion = h?.IsPlayerCompanion == true,
            IsOfPlayersClan = h?.Clan != null && h.Clan == Clan.PlayerClan,
            InPlayersParty = h?.PartyBelongedTo != null && h.PartyBelongedTo == MobileParty.MainParty
         };

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

            if (!CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = effectiveMagnitude, DaysAgo = daysAgo, HappinessScore = happiness})) continue;

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

      #region private: Ambition

      /// <summary>
      ///   Gathers the seven engine facts <see cref="CompanionAmbitionPolicy" /> needs and lets it choose which
      ///   (if any) of the four bound ambitions this companion voices today. <see cref="CompanionAmbitionFacts.IsLandless" />
      ///   folds in "a fief could actually be granted right now" (the player still leads their clan AND holds a
      ///   vacant governable fief), and <see cref="CompanionAmbitionFacts.HoldsPartyRole" /> folds in "no role
      ///   currently fits their skills" (<see cref="FindFittingPartyRole" />): neither auxiliary fact has its own
      ///   slot in the pure policy's signature, so each rides the closest matching boolean, documented on the
      ///   facts class itself.
      /// </summary>
      private static CompanionAudienceTopicScan ResolveAmbition(Hero companion, IDictionary<string, int> ambitionAskedDays)
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(companion?.StringId);

         if (profile == null)
            return NotHeld(CompanionAudienceReason.Ambition, "no profile to read this companion's standing from");

         var today = (int) CampaignTime.Now.ToDays;
         int daysSinceLastAsked = ambitionAskedDays != null && ambitionAskedDays.TryGetValue(companion.StringId, out int lastAsked)
            ? today - lastAsked
            : int.MaxValue / 2; // never asked before: comfortably past any cooldown

         bool hasFieldPartyOfOwn = companion.PartyBelongedTo?.LeaderHero == companion
                                    || CompanionMissionBehavior.Instance?.IsAway(companion.StringId) == true;

         (string roleKey, string roleLabel) = FindFittingPartyRole(companion);
         bool holdsPartyRole = HoldsAnyPartyRole(companion) || roleKey == null;

         bool playerLeadsOwnClan = Clan.PlayerClan?.Leader == Hero.MainHero;
         bool isLanded = companion.GovernorOf != null || companion.PartyBelongedTo?.LeaderHero == companion;
         bool isLandless = !isLanded && playerLeadsOwnClan && HasVacantGovernableFief();
         bool isHighStanding = profile.ReputationWithPlayer >= GovernorAppointmentPolicy.StandingFloor;

         Hero match = FindMarriageMatch(companion);

         var facts = new CompanionAmbitionFacts {
            HasFieldPartyOfOwn = hasFieldPartyOfOwn,
            HoldsPartyRole = holdsPartyRole,
            IsLandless = isLandless,
            IsHighStanding = isHighStanding,
            IsUnmarriedWithMatch = match != null,
            Happiness = CompanionHappinessService.Get(companion),
            DaysSinceLastAsked = daysSinceLastAsked
         };

         (CompanionAmbition ambition, int strength) = CompanionAmbitionPolicy.Select(facts);

         if (ambition == CompanionAmbition.None)
            return NotHeld(CompanionAudienceReason.Ambition, "no ambition of their own currently fits this companion's state");

         return new CompanionAudienceTopicScan {
            Topic = CompanionAudienceReason.Ambition, Holds = true, Strength = strength, Detail = DescribeAmbition(ambition, roleLabel, match)
         };
      }

      private static string DescribeAmbition(CompanionAmbition ambition, string fittingRoleLabel, Hero match)
      {
         switch (ambition)
         {
            case CompanionAmbition.Command: return "a command of your own to lead";
            case CompanionAmbition.Post:
               return string.IsNullOrEmpty(fittingRoleLabel) ? "a post in the party" : $"the {fittingRoleLabel}'s post in the party";
            case CompanionAmbition.Fief: return "a fief of your own to govern in your name";
            case CompanionAmbition.Marriage: return match != null ? $"your blessing to wed {match.Name}" : "your blessing to wed";
            default: return null;
         }
      }

      /// <summary>Whether this companion currently serves as ANY of the party's four roles (the engine's own EffectiveX properties).</summary>
      private static bool HoldsAnyPartyRole(Hero companion)
      {
         MobileParty party = MobileParty.MainParty;

         return party != null && (party.EffectiveScout == companion || party.EffectiveEngineer == companion
                                   || party.EffectiveQuartermaster == companion || party.EffectiveSurgeon == companion);
      }

      /// <summary>
      ///   The single party role this companion is best skilled for (scout/Scouting, engineer/Engineering,
      ///   quartermaster/Steward, surgeon/Medicine), or a null key/label when none of the four skills clears
      ///   <see cref="PartyRoleQualifiedSkillThreshold" />. assign_party_role's own bridge (CourtActionResolver.
      ///   TryAssignPartyRole) applies no skill gate of its own ("instant, no cost, no trust gate"), so THIS is
      ///   the only place a party-role promotion is judged plausible before it is ever voiced.
      /// </summary>
      private static (string Key, string Label) FindFittingPartyRole(Hero companion)
      {
         var candidates = new[] {
            (Key: "scout", Label: "scout", Skill: companion.GetSkillValue(DefaultSkills.Scouting)),
            (Key: "engineer", Label: "engineer", Skill: companion.GetSkillValue(DefaultSkills.Engineering)),
            (Key: "quartermaster", Label: "quartermaster", Skill: companion.GetSkillValue(DefaultSkills.Steward)),
            (Key: "surgeon", Label: "surgeon", Skill: companion.GetSkillValue(DefaultSkills.Medicine))
         };

         (string Key, string Label, int Skill) best = candidates.OrderByDescending(c => c.Skill).First();

         return best.Skill >= PartyRoleQualifiedSkillThreshold ? (best.Key, best.Label) : (null, null);
      }

      /// <summary>
      ///   Whether the player's own clan currently holds a town or castle with no governor seated, the SAME
      ///   fief-matching CourtActionResolver.TryAppointGovernor re-checks at commit. Fief's own bound verb is
      ///   appoint_governor (not grant_fief, which the catalog restricts to a recipient of a SEPARATE clan, and
      ///   every companion this audience ever scans is, by construction, of the player's OWN clan): see this
      ///   file's ResolveAmbition header remark and the delivery report for the full reasoning.
      /// </summary>
      private static bool HasVacantGovernableFief()
         => Settlement.All != null && Settlement.All.Any(s =>
            (s.IsTown || s.IsCastle) && s.OwnerClan == Clan.PlayerClan && s.Town?.Governor == null);

      /// <summary>
      ///   The first plausible unwed match for this companion among the PLAYER's own clan kin (deterministic
      ///   StringId order), engine-suitable per <see cref="Campaign.Models" />.MarriageModel and
      ///   <see cref="MarriageGuardPolicy" />, or null when none qualify. This is deliberately narrow (the
      ///   player's OWN kin only): arrange_marriage's "a" parameter must resolve to one of the player's own
      ///   unwed clan members (CourtActionResolver.TryArrangeMarriage), and the companion self-authorizes their
      ///   OWN hand (b == authorizer), the one shape of arrange_marriage that can ever apply to a same-clan
      ///   companion. See this file's ResolveAmbition header remark and the delivery report for the full reasoning.
      /// </summary>
      private static Hero FindMarriageMatch(Hero companion)
      {
         if (companion.Spouse != null || companion.Age < MarriageEligibility.MinMarriageAge) return null;
         if (Clan.PlayerClan?.Heroes == null) return null;

         foreach (Hero candidate in Clan.PlayerClan.Heroes
                     .Where(h => h != null && h != companion && h != Hero.MainHero)
                     .OrderBy(h => h.StringId, StringComparer.Ordinal))
         {
            if (candidate is not {IsAlive: true} || candidate.Spouse != null || candidate.Age < MarriageEligibility.MinMarriageAge) continue;
            if (Campaign.Current?.Models?.MarriageModel?.IsCoupleSuitableForMarriage(candidate, companion) != true) continue;
            if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = candidate.IsPrisoner, PlayerPrisoner = companion.IsPrisoner, AtWarWithCandidate = false})) continue;

            return candidate;
         }

         return null;
      }

      #endregion

      private static CompanionAudienceTopicScan NotHeld(CompanionAudienceReason topic, string whyNot)
         => new CompanionAudienceTopicScan {Topic = topic, Holds = false, WhyNot = whyNot};
   }
}
