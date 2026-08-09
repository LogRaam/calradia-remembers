// Code written by Gabriel Mailhot, 09/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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

      public static OpenPartnerLoverBehavior Instance;

      public OpenPartnerLoverBehavior()
      {
         Instance = this;
      }

      /// <summary>
      ///   Test / console seam (cr.take_lover, cr.testall open_partner_lover): drives the REAL lover selection
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

         if (!OpenPartnerLoverPolicy.CanTakeLover(hasOpenArrangement, isHaremCulture, ModSettings.EnableJealousy))
            return null;

         int neglect = JealousyBehavior.NeglectDaysFor(partner, today);

         if (!forced)
         {
            int roll = MBRandom.RandomInt(100);
            if (!OpenPartnerLoverPolicy.ShouldTakeLover(isOpenCommittedPartner: true, alreadyHasLover: false, neglectDays: neglect, rollPercent: roll))
               return null;
         }

         Hero lover = SelectLover(partner, profile);

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
      ///   Scans the living heroes for the best-acquainted plausible lover for <paramref name="partner" />,
      ///   gating each candidate through the pure <see cref="OpenPartnerLoverPolicy.IsPlausibleLover" /> and
      ///   inferring compatibility the way CR infers a match elsewhere (unwed adult of a sex she is drawn to,
      ///   an associate, never the player or the player's own household). Prefers the highest relation to her,
      ///   with an <see cref="MBRandom" /> tie-break so equal ties do not always pick the same hero.
      /// </summary>
      private static Hero SelectLover(Hero partner, NpcProfile profile)
      {
         var player = Hero.MainHero;
         Clan playerClan = Clan.PlayerClan;

         Hero best = null;
         int bestRel = int.MinValue;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == null || h == partner) continue;

            int rel = h.GetRelation(partner);
            bool acquainted = rel > 0 || (h.MapFaction != null && h.MapFaction == partner.MapFaction);
            // The partner's own orientation against the candidate's sex: the single authoritative compatibility
            // rule (RomanticProfile.IsCompatibleWith), reused for a female-partner-seeking-a-male-lover direction.
            bool orientationCompatible = profile.Romantic.IsCompatibleWith(h.IsFemale);

            bool ok = OpenPartnerLoverPolicy.IsPlausibleLover(
               candidateAlive: h.IsAlive,
               candidateIsHero: h.IsLord, // a proper lord/lady, not a wanderer or notable
               orientationCompatible: orientationCompatible,
               candidateUnwed: h.Spouse == null,
               candidateAdult: !h.IsChild && h.Age >= MarriageEligibility.MinMarriageAge,
               acquainted: acquainted,
               isThePlayer: h == player,
               isSameClanAsPlayerHousehold: playerClan != null && h.Clan == playerClan);

            if (!ok) continue;

            if (rel > bestRel || (rel == bestRel && MBRandom.RandomInt(2) == 0))
            {
               bestRel = rel;
               best = h;
            }
         }

         return best;
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

            if (!OpenPartnerLoverPolicy.CanTakeLover(p.Romantic.IsOpenArrangement, IsHaremCulture(h), ModSettings.EnableJealousy)) continue;

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
      }

      #endregion
   }
}
