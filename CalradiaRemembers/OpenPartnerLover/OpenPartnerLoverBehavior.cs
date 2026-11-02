// Code written by Gabriel Mailhot, 09/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Social;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.OpenPartnerLover
{
   /// <summary>
   ///   Open relationship (Inc 3a): the symmetry of the Inc 2 jealousy suppression. A partner who holds
   ///   reciprocal open terms with the player (<see cref="RomanticProfile.IsOpenArrangement" />) does not only
   ///   tolerate the player's other loves, over time she takes a lover of HER OWN, the likelier the more the
   ///   player neglects her. Each week, for every open committed partner who has not yet taken one, this reads
   ///   the SAME neglect measure the jealousy competition loop uses (via <see cref="JealousyBehavior.NeglectDaysFor" />),
   ///   rolls the pure <see cref="OpenPartnerLoverPolicy" />, and on a hit selects a plausible lover and records
   ///   the link (<see cref="RomanticProfile.TakenLoverId" /> + a grounded memory + a faint world-event rumour).
   ///   It is SILENT to the player this increment: no popup, no letter. The trysts, the player's awareness /
   ///   jealousy, and pregnancy by the lover are later increments (3b, 3c) that build on the link formed here.
   ///   Stores nothing of its own (the lover id lives on the persisted profile). The CULTURE branch decides who
   ///   strays and how: an egalitarian partner takes an OPEN lover only under a reciprocal open arrangement; a
   ///   harem / patriarchal (submissive-wife) culture takes a SECRET lover, and may stray even without an
   ///   arrangement when neglected. That last unsolicited-infidelity path is the one case gated by the jealousy
   ///   MCM toggle (a drama-free household keeps wives faithful); the open-arrangement paths are never gated.
   /// </summary>
   public sealed class OpenPartnerLoverBehavior : CampaignBehaviorBase
   {
      // A faint, private "they have grown close" rumour, mirroring JealousyBehavior's own liaison magnitude:
      // low, so it reaches only those near or of the right faction, and ages out. It is what Inc 3b will read
      // to surface the affair; this increment only lays it down.
      private const int LiaisonRumorMagnitude = 18;

      // A tryst nudges the wife-to-lover bond a little each time, never past a ceiling: enough to read as a
      // warming attachment (and to seed the later pregnancy hook), never a runaway relation the tryst loop would
      // pump to the maximum. The bump only applies while their relation is still below the cap.
      private const int TrystRelationBump = 2;
      private const int TrystRelationCap = 30;

      // The discovered-affair scandal is chronicle material (it involves the player), so it carries far and is
      // kept; the gentle open-lover beat is a quieter chronicle note. Both distinct from the faint Inc 3a rumour.
      private const int AffairScandalMagnitude = 55;
      private const int OpenLoverKnownMagnitude = 22;

      public static OpenPartnerLoverBehavior Instance;

      public OpenPartnerLoverBehavior()
      {
         Instance = this;
      }

      /// <summary>
      ///   Test / console seam (cr.take_lover, cr.test open_partner_lover): drives the REAL lover selection
      ///   and link for <paramref name="partner" /> immediately, bypassing only the weekly neglect roll (so a
      ///   self-test need not wait a week or force a chance). Returns the chosen lover, or null when she is not
      ///   an eligible committed partner (the culture gate still applies) or no plausible candidate exists.
      ///   Every other step is the production path, including setting <see cref="RomanticProfile.LoverIsSecret" />.
      /// </summary>
      public static Hero DebugTakeLover(Hero partner)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || behavior?.Store == null) return null;

         NpcProfile profile = behavior.Store.Get(partner.StringId);

         if (profile?.Romantic == null) return null;

         return TakeLover(partner, profile, (int) CampaignTime.Now.ToDays, forced: true);
      }

      /// <summary>
      ///   Test / console seam (cr.tryst, cr.test all): forces ONE tryst between <paramref name="partner" /> and
      ///   her taken lover NOW, bypassing only the "player is away" gate and the weekly meeting roll (so a
      ///   self-test need not stage the player's position or wait for a hit). Every other step is the production
      ///   path: the bond bump, the counter, the private memory, and, for a SECRET affair, the discovery check.
      ///   <paramref name="discoveryRoll" /> lets a test drive that check deterministically; -1 (the default)
      ///   uses a live <see cref="MBRandom" /> roll like production. Returns null when she has no lover to meet.
      /// </summary>
      public static NpcProfile DebugTryst(Hero partner, int discoveryRoll = -1)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || behavior?.Store == null) return null;

         NpcProfile profile = behavior.Store.Get(partner.StringId);

         if (profile?.Romantic == null || string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return null;

         DoTryst(partner, profile, (int) CampaignTime.Now.ToDays, discoveryRoll);

         return behavior.Store.Get(partner.StringId);
      }

      /// <summary>
      ///   Test / console seam (cr.lover_child, cr.test all): forces <paramref name="partner" /> to conceive a child
      ///   BY HER TAKEN LOVER now, bypassing only the per-tryst conception roll (so a self-test need not stage a
      ///   fertile hit). She must already carry a lover (use cr.take_lover first) and be fertile
      ///   (<see cref="PregnancyService.IsEligible" />). Runs the REAL conception path: the child's father is the
      ///   lover (never the player), the pregnancy is registered as a lover-child so it lands in the lover's line
      ///   at birth, and a SECRET affair is revealed by the pregnancy. Returns the lover on a conception, else
      ///   null (no lover, infertile, or the engine refused).
      /// </summary>
      public static Hero DebugConceiveByLover(Hero partner)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || behavior?.Store == null) return null;

         NpcProfile profile = behavior.Store.Get(partner.StringId);

         if (profile?.Romantic == null || string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return null;

         Hero lover = ResolveLover(profile);

         if (lover == null) return null;

         return ConceiveByLover(partner, profile, lover, (int) CampaignTime.Now.ToDays, forced: true)
            ? lover
            : null;
      }

      /// <summary>
      ///   Test / console seam (cr.tryst, cr.test all): runs the OPEN-lover awareness step for
      ///   <paramref name="partner" /> now (word of an openly held lover reaching the player), the same one the
      ///   weekly pass runs. A no-op for a secret affair (which surfaces only on discovery) or one already known.
      ///   Returns whether the player now knows of the lover.
      /// </summary>
      public static bool DebugSurfaceOpenLover(Hero partner)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || behavior?.Store == null) return false;

         NpcProfile profile = behavior.Store.Get(partner.StringId);

         if (profile?.Romantic == null) return false;

         MaybeSurfaceOpenLover(partner, profile, (int) CampaignTime.Now.ToDays);

         return behavior.Store.Get(partner.StringId)?.Romantic?.LoverKnownToPlayer ?? false;
      }

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      /// <summary>Runs the weekly open-partner pass on demand (console/self-test path).</summary>
      public void DebugRunWeekly() => OnWeeklyTick();

      public override void SyncData(IDataStore dataStore) { } // the lover id lives on the persisted profile; nothing of its own to save

      #region private

      /// <summary>
      ///   Forms the lover link for one open partner. When <paramref name="forced" /> is false (the weekly
      ///   path) it first rolls the pure neglect-scaled chance and returns null on a miss; when true (the debug
      ///   seam) it skips only that roll. On a take, sets <see cref="RomanticProfile.TakenLoverId" />, records a
      ///   grounded memory and a faint rumour, persists, and returns the lover. Silent to the player this
      ///   increment.
      /// </summary>
      private static Hero TakeLover(Hero partner, NpcProfile profile, int today, bool forced)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || profile?.Romantic == null || behavior?.Store == null) return null;
         if (!string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return null; // already has one (Inc 3a forms a single link)

         // Culture decides eligibility (WHO may stray) and secrecy (open vs hidden). The unsolicited-infidelity
         // path (harem culture, no open arrangement) is gated by the jealousy toggle; the open-arrangement paths
         // are not (the player opted into openness). The debug seam still honours this: it bypasses only the roll.
         bool hasOpenArrangement = profile.Romantic.IsOpenArrangement;
         bool isHaremCulture = IsHaremCulture(partner);

         if (!OpenPartnerLoverPolicy.CanTakeLover(new OpenPartnerLoverPolicy.LoverEligibilityFacts {HasOpenArrangement = hasOpenArrangement, IsHaremCulture = isHaremCulture, UnsolicitedInfidelityAllowed = ModSettings.EnableJealousy}))
            return null;

         int neglect = JealousyBehavior.NeglectDaysFor(partner, today);

         if (!forced)
         {
            int roll = MBRandom.RandomInt(100);
            if (!OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: true, alreadyHasLover: false, neglectDays: neglect, rollPercent: roll))
               return null;
         }

         Hero lover = Mapping.LoverSelector.Best(partner, profile);

         if (lover == null) return null;

         bool secret = OpenPartnerLoverPolicy.LoverIsSecret(isHaremCulture);

         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = secret;
         profile.Events.Add(new NotableEvent(today, NotableEventType.Other, secret
            ? $"I have found quiet solace with {lover.Name}, a comfort I keep hidden from my husband."
            : $"I have found quiet solace with {lover.Name}, as is my right under the open terms the player and I hold."));
         behavior.Store.Set(profile);

         // A secret affair spreads no public rumour (it is secret); only the private memory above records it.
         // An open lover keeps the faint public rumour Inc 3a lays for a later increment to surface.
         if (!secret) RecordLiaisonRumor(partner, lover, today);

         ModLog.Info($"[OPEN-LOVER] {partner.Name} ({(isHaremCulture ? "harem" : "open")} partner, neglect {neglect}d) has taken {lover.Name} as her own {(secret ? "secret" : "open")} lover.");

         return lover;
      }

      /// <summary>
      ///   The committed partners eligible to take a lover this week: committed to the player (the native
      ///   spouse, or a committed consort), not already carrying a lover, and passing the culture-scaled
      ///   <see cref="OpenPartnerLoverPolicy.CanTakeLover" /> gate (an egalitarian partner needs an open
      ///   arrangement; a harem-culture one may also stray uninvited when neglected, subject to the jealousy
      ///   toggle). No longer limited to open-arrangement partners. Snapshotted so the tick can mutate profiles.
      /// </summary>
      private static IEnumerable<(Hero partner, NpcProfile profile)> EligibleCommittedPartners(NpcMemoryCampaignBehavior behavior)
      {
         var results = new List<(Hero, NpcProfile)>();
         Hero spouse = Hero.MainHero?.Spouse;

         foreach (string id in behavior.Store.ListIds().ToList())
         {
            NpcProfile p = behavior.Store.Get(id);

            if (p?.Romantic == null) continue;
            if (!string.IsNullOrEmpty(p.Romantic.TakenLoverId)) continue;

            Hero h = Hero.FindFirst(x => x.StringId == id);

            if (h is not {IsAlive: true} || h == Hero.MainHero) continue;

            bool committed = h == spouse || (p.Romantic.IsConsort && p.Romantic.Status == RomanticStatus.Committed);
            if (!committed) continue;

            if (!OpenPartnerLoverPolicy.CanTakeLover(new OpenPartnerLoverPolicy.LoverEligibilityFacts {HasOpenArrangement = p.Romantic.IsOpenArrangement, IsHaremCulture = IsHaremCulture(h), UnsolicitedInfidelityAllowed = ModSettings.EnableJealousy})) continue;

            results.Add((h, p));
         }

         return results;
      }

      /// <summary>
      ///   True when <paramref name="partner" />'s culture is a harem / patriarchal (submissive-wife) culture:
      ///   she keeps a lover SECRET and may stray even without an open arrangement. Derived from the JSON-backed
      ///   <see cref="JealousyCultureLoader" /> so modders keep control; never a hard-coded culture name.
      /// </summary>
      private static bool IsHaremCulture(Hero partner)
         => JealousyCultureLoader.IsHaremCulture(partner?.Culture?.Name?.ToString());

      /// <summary>
      ///   Lays down a faint, private "they have grown close" rumour so Inc 3b can later surface the affair to
      ///   the player. Low magnitude, InvolvesPlayer false (never the heroic chronicle), and it ages out.
      /// </summary>
      private static void RecordLiaisonRumor(Hero partner, Hero lover, int today)
      {
         var store = WorldEventStore.Instance;

         if (store == null) return;

         Settlement place = partner.CurrentSettlement ?? lover.CurrentSettlement;

         var factions = new List<string>();
         if (partner.MapFaction != null) factions.Add(partner.MapFaction.StringId);
         if (lover.MapFaction != null && !factions.Contains(lover.MapFaction.StringId)) factions.Add(lover.MapFaction.StringId);

         store.Record(new WorldEvent {
            Id = Guid.NewGuid().ToString("N"),
            Type = WorldEventType.Other,
            Day = today,
            PlaceId = place?.StringId,
            InvolvesPlayer = false,
            Magnitude = LiaisonRumorMagnitude,
            Summary = $"{partner.Name} has been keeping close company with {lover.Name}.",
            ActorIds = new List<string> {partner.StringId, lover.StringId},
            FactionIds = factions
         });
      }

      private void OnWeeklyTick()
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;
         var today = (int) CampaignTime.Now.ToDays;

         foreach ((Hero partner, NpcProfile profile) in EligibleCommittedPartners(behavior))
            // Each partner behind its own guard: one throwing must not skip the others this tick.
            SafeRun.Step("OpenPartnerLoverBehavior.TakeLover", () => TakeLover(partner, profile, today, forced: false));

         // Second pass (Inc 3b-a): for every partner who ALREADY carries a lover, surface an open lover to the
         // player once, and roll a tryst when the player is away (which may leak a secret affair over time).
         foreach ((Hero partner, NpcProfile profile) in PartnersWithLover(behavior))
            SafeRun.Step("OpenPartnerLoverBehavior.AwarenessAndTryst", () => AwarenessAndTryst(partner, profile, today));
      }

      /// <summary>
      ///   The weekly per-partner Inc 3b-a step for a partner who already has a lover: first surface an OPEN
      ///   lover to the player once (gently, she was free), then, when the player is NOT with her, roll the
      ///   modest tryst chance and, on a hit, run the tryst (which may in turn expose a secret affair).
      /// </summary>
      private static void AwarenessAndTryst(Hero partner, NpcProfile profile, int today)
      {
         MaybeSurfaceOpenLover(partner, profile, today);

         if (PlayerPresenceResolver.IsBeside(partner)) return; // she does not meet her lover while the player is with her

         int roll = MBRandom.RandomInt(100);
         if (TrystPolicy.ShouldMeet(hasLover: true, playerIsAway: true, rollPercent: roll))
            DoTryst(partner, profile, today, discoveryRoll: MBRandom.RandomInt(100));
      }

      /// <summary>
      ///   One tryst: the partner meets her taken lover while the player is away. Nudges the wife-to-lover bond
      ///   a little (capped, never a runaway relation), increments the persisted tryst counter, and records a
      ///   PRIVATE memory of the meeting. A secret tryst lays NO public rumour (it is secret); it instead runs
      ///   the rising discovery check (<see cref="AffairDiscoveryPolicy" />) that may expose the affair to the
      ///   player. <paramref name="discoveryRoll" /> of -1 uses a live roll; a test may pass a fixed value.
      ///   This is the mechanical/narrative hook Inc 3c (pregnancy by the lover) will build on; not a scene.
      /// </summary>
      private static void DoTryst(Hero partner, NpcProfile profile, int today, int discoveryRoll)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (partner == null || profile?.Romantic == null || behavior?.Store == null) return;
         if (string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return;

         Hero lover = ResolveLover(profile);

         // Warm the bond a little, but only while it is still below the ceiling (a tryst deepens attachment; it
         // does not pump the relation to the maximum over time). Guarded: a relation read must never crash the tick.
         if (lover != null && lover.IsAlive && partner.GetRelation(lover) < TrystRelationCap)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(partner, lover, TrystRelationBump, false);

         profile.Romantic.LoverTrystCount++;

         bool secret = profile.Romantic.LoverIsSecret;
         string loverName = lover?.Name?.ToString() ?? "her lover";
         profile.Events.Add(new NotableEvent(today, NotableEventType.Other, secret
            ? $"I stole away to be with {loverName} again while my husband was far off, and told no one."
            : $"I spent quiet hours with {loverName} while the player was away, as our open terms allow."));
         behavior.Store.Set(profile);

         ModLog.Info($"[OPEN-LOVER] {partner.Name} met {loverName} (tryst #{profile.Romantic.LoverTrystCount}, {(secret ? "secret" : "open")}).");

         // Inc 3c: a tryst may conceive a child BY THE LOVER. The child is the lover's (its genetic father) and
         // its mother's, never the player-husband's. A conception by a SECRET lover reveals the affair outright
         // (a pregnancy visibly not the husband's cannot stay hidden), so it may pre-empt the discovery roll below.
         if (lover != null && lover.IsAlive)
            ConceiveByLover(partner, profile, lover, today, forced: false);

         // A secret affair may LEAK, the likelier the more trysts it has seen. An open lover is never discovered
         // (he is already, or about to be, known). The scandal fires once (gated on LoverKnownToPlayer, which a
         // conception above may already have set, in which case IsDiscovered short-circuits and it never re-fires).
         int roll = discoveryRoll < 0 ? MBRandom.RandomInt(100) : discoveryRoll;
         if (AffairDiscoveryPolicy.IsDiscovered(secret, profile.Romantic.LoverKnownToPlayer, profile.Romantic.LoverTrystCount, roll))
            DiscoverSecretAffair(partner, profile, lover, today);
      }

      /// <summary>
      ///   Inc 3c: rolls (or forces, via the debug seam) a conception BY THE LOVER when the partner is fertile,
      ///   and, on a conception, attributes the child to the lover (its genetic father) and the mother, NEVER the
      ///   player-husband. The fertility read is the engine's own <see cref="PregnancyService.IsEligible" />; the
      ///   WHETHER-she-conceives roll is the pure <see cref="LoverConceptionPolicy" />. The reflective conception
      ///   sets the vanilla pregnancy Father to the lover; registering the pregnancy as a LOVER-CHILD
      ///   (<see cref="PregnancyBirthBehavior.TrackLoverChild" />) then makes the birth pipeline place it in the
      ///   lover's line rather than the mother's clan (which, for a wife married into the player's house, would be
      ///   the player's clan). A conception by a SECRET lover reveals the affair (undeniable proof). Returns
      ///   whether a child was conceived. Guarded: never throws out of the weekly tick.
      /// </summary>
      private static bool ConceiveByLover(Hero partner, NpcProfile profile, Hero lover, int today, bool forced)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;

            if (partner == null || profile?.Romantic == null || lover == null || !lover.IsAlive || behavior?.Store == null) return false;
            if (string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return false;

            // Fertility is the engine's read; the roll+gate is the pure policy. The debug seam forces past only
            // the roll, never the fertility gate (an infertile mother the engine would never deliver is refused).
            if (!PregnancyService.IsEligible(partner)) return false;

            if (!forced)
            {
               int roll = MBRandom.RandomInt(100);
               if (!LoverConceptionPolicy.ShouldConceive(affairActive: true, motherFertile: true, rollPercent: roll)) return false;
            }

            // force: true skips PregnancyService's OWN conception chance (LoverConceptionPolicy already decided).
            // The explicit father is the lover, so the reflective conception sets the vanilla Father to him.
            string outcome = PregnancyService.TryConceive(partner, lover, force: true);
            if (outcome == null) return false; // engine refused (reflection failure or ineligible after all)

            // Remember this is a lover-child so it lands in the lover's line, not the mother's (player's) clan.
            PregnancyBirthBehavior.Instance?.TrackLoverChild(partner, lover);

            bool secret = profile.Romantic.LoverIsSecret;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other, secret
               ? $"I am carrying {lover.Name}'s child, got in secret while my husband was away. It can belong to no one else."
               : $"I am carrying {lover.Name}'s child, conceived under the open terms the player and I hold."));
            behavior.Store.Set(profile);

            ModLog.Info($"[OPEN-LOVER] {partner.Name} has conceived a child by her lover {lover.Name} (father is the lover, never the player; the child will follow his line).");

            // Discovery via a child: a pregnancy visibly not the husband's is undeniable proof. Reuse the Inc 3b-a
            // surfacing path so a hidden affair cannot stay secret once a lover-child is on the way.
            if (secret && !profile.Romantic.LoverKnownToPlayer)
               DiscoverSecretAffair(partner, profile, lover, today);

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[OPEN-LOVER] lover-conception failed for {partner?.Name}: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Surfaces a discovered SECRET affair to the player as a scandal beat: a notification, a chronicle
      ///   world-event (it involves the player), and a private memory on the partner. Sets
      ///   <see cref="RomanticProfile.LoverKnownToPlayer" /> and <see cref="RomanticProfile.AffairDiscovered" />
      ///   so it fires once and Inc 3b-b's confrontation can gate on the discovery. No mechanical penalty and no
      ///   standing hit this increment: a purely surfaced narrative beat.
      /// </summary>
      private static void DiscoverSecretAffair(Hero partner, NpcProfile profile, Hero lover, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null || profile?.Romantic == null) return;

         profile.Romantic.LoverKnownToPlayer = true;
         profile.Romantic.AffairDiscovered = true;
         string loverName = lover?.Name?.ToString() ?? "another";
         profile.Events.Add(new NotableEvent(today, NotableEventType.Betrayal,
            $"My husband has learned of {loverName}. My secret is a secret no longer."));
         behavior.Store.Set(profile);

         InformationManager.DisplayMessage(new InformationMessage(
            $"Word reaches you that {partner.Name} has been unfaithful, taking {loverName} as a secret lover.", Colors.Red));

         RecordAffairChronicle(partner, lover, today, WorldEventType.Scandal, AffairScandalMagnitude,
            $"It came to light that {partner.Name} had taken {lover?.Name?.ToString() ?? "another"} as a secret lover, betraying the player.");

         ModLog.Info($"[OPEN-LOVER] the player DISCOVERED {partner.Name}'s secret affair with {loverName} (after {profile.Romantic.LoverTrystCount} trysts).");
      }

      /// <summary>
      ///   Surfaces an OPEN lover to the player the first time it is known-worthy: a gentle notification and a
      ///   quiet chronicle note that word has reached him she has grown close to another, with no scandal (she
      ///   was free under their open terms). Sets <see cref="RomanticProfile.LoverKnownToPlayer" /> so it fires
      ///   once, and never <see cref="RomanticProfile.AffairDiscovered" /> (an open lover is no betrayal). A
      ///   no-op for a secret affair (surfaced only on discovery) or a lover already known.
      /// </summary>
      private static void MaybeSurfaceOpenLover(Hero partner, NpcProfile profile, int today)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null || profile?.Romantic == null) return;
         if (profile.Romantic.LoverIsSecret) return; // a secret affair surfaces only when DISCOVERED
         if (profile.Romantic.LoverKnownToPlayer) return; // already surfaced once
         if (string.IsNullOrEmpty(profile.Romantic.TakenLoverId)) return;

         Hero lover = ResolveLover(profile);
         string loverName = lover?.Name?.ToString() ?? "another";

         profile.Romantic.LoverKnownToPlayer = true;
         behavior.Store.Set(profile);

         InformationManager.DisplayMessage(new InformationMessage(
            $"Word reaches you that {partner.Name} has grown close to {loverName}, as is her right under your open terms."));

         RecordAffairChronicle(partner, lover, today, WorldEventType.Other, OpenLoverKnownMagnitude,
            $"Word reached the player that {partner.Name} had grown close to {lover?.Name?.ToString() ?? "another"}, an openly held lover under their terms.");

         ModLog.Info($"[OPEN-LOVER] the player now knows of {partner.Name}'s open lover {loverName}.");
      }

      /// <summary>The hero <paramref name="profile" />'s partner has taken as her lover, or null when unresolved.</summary>
      private static Hero ResolveLover(NpcProfile profile)
      {
         string id = profile?.Romantic?.TakenLoverId;

         return string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);
      }

      /// <summary>
      ///   Every partner who ALREADY carries a lover link, alive and not the player: the population of the Inc
      ///   3b-a awareness / tryst pass. Snapshotted so the tick can mutate profiles while iterating.
      /// </summary>
      private static IEnumerable<(Hero partner, NpcProfile profile)> PartnersWithLover(NpcMemoryCampaignBehavior behavior)
      {
         var results = new List<(Hero, NpcProfile)>();

         foreach (string id in behavior.Store.ListIds().ToList())
         {
            NpcProfile p = behavior.Store.Get(id);

            if (p?.Romantic == null || string.IsNullOrEmpty(p.Romantic.TakenLoverId)) continue;

            // A lord's hidden affair is no partner's lover: every line below speaks of a betrayal of the player.
            if (Mapping.HiddenAffairBehavior.Instance?.IsHidden(id) == true) continue;

            Hero h = Hero.FindFirst(x => x.StringId == id);

            if (h is not {IsAlive: true} || h == Hero.MainHero) continue;

            results.Add((h, p));
         }

         return results;
      }

      /// <summary>
      ///   Records a player-facing chronicle world-event for a surfaced lover (open or discovered).
      ///   <see cref="WorldEvent.InvolvesPlayer" /> is true so it is kept long-term as chronicle material.
      /// </summary>
      private static void RecordAffairChronicle(Hero partner, Hero lover, int today, WorldEventType type, int magnitude, string summary)
      {
         var store = WorldEventStore.Instance;

         if (store == null) return;

         Settlement place = partner.CurrentSettlement ?? lover?.CurrentSettlement;

         var factions = new List<string>();
         if (partner.MapFaction != null) factions.Add(partner.MapFaction.StringId);
         if (lover?.MapFaction != null && !factions.Contains(lover.MapFaction.StringId)) factions.Add(lover.MapFaction.StringId);

         var actors = new List<string> {partner.StringId};
         if (lover != null) actors.Add(lover.StringId);

         store.Record(new WorldEvent {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Day = today,
            PlaceId = place?.StringId,
            InvolvesPlayer = true,
            Magnitude = magnitude,
            Summary = summary,
            ChronicleLine = summary,
            ActorIds = actors,
            FactionIds = factions
         });
      }

      #endregion
   }
}
