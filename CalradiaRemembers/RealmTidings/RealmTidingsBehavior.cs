// Code written by Gabriel Mailhot, 08/08/2026.
// Realm-tidings feature, Increment D: once a week, a friend of the player (a landed lord of a kingdom who holds
// them in genuine regard) may write UNBIDDEN to relay a real piece of news of their realm (a looming war, hard
// times fallen on a town, a rumour that reached them). The whole honesty of the feature is that the LLM only ever
// RELAYS a known WorldEvent, never invents one, so this host does no LLM work of its own: it enumerates eligible
// writers, scores the events each plausibly knows (RealmTidingsSalience), lets the pure RealmTidingsPolicy pick
// the one worth telling that this NPC has not already relayed, and then ENQUEUES a LetterCandidateIntent so the
// existing mailbox scheduler weighs it against everything else and only the winner reaches the player. The event's
// summary and a confidence framing ride the intent's Context string (the same channel every other unbidden letter
// uses); no new letter field is needed. Per-NPC dedup lives on NpcProfile.RelayedWorldEventIds.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.RealmTidings
{
   /// <summary>Weekly: a friendly lord relays a real piece of realm news to the player by unbidden letter.</summary>
   public sealed class RealmTidingsBehavior : CampaignBehaviorBase
   {
      // The bar for "genuine regard": a real friend, not a mere acquaintance. Sits with the mod's other
      // friendly-letter gates (a marriage solicitation needs 20, a battle-ally congratulation 20); the name
      // resolver labels >= 10 "a friend (genuine regard)", so this keeps realm tidings a notch above that,
      // a courtesy between actual friends. Named so Gabriel can retune it in one place.
      private const int MinRegardToWrite = Regard.WarmTier;

      // How many of the events a writer plausibly knows to score per pass. The player accumulates many
      // correspondents; a small cap keeps the weekly sweep cheap while still covering the events that matter
      // (the store already returns them best-known-first).
      private const int MaxEventsScanned = 8;

      // The per-NPC dedup ledger is capped and drop-oldest so it never grows without bound in a long campaign.
      private const int MaxRelayedIdsRemembered = 30;

      public override void RegisterEvents()
         => CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

      public override void SyncData(IDataStore dataStore) { } // dedup state lives on each NpcProfile.RelayedWorldEventIds

      /// <summary>
      ///   Debug seam for the console (<c>cr.letter_tidings</c>): evaluate one writer now and enqueue any warranted
      ///   realm-tidings letter, returning a human-readable report of what was chosen (or why nothing was).
      /// </summary>
      public static string DebugEvaluate(BannerlordNpcMemoryStore store, NpcProfile profile, Hero hero)
      {
         if (store == null || profile == null || hero == null) return "Missing store / profile / hero.";
         if (!CanRelay(hero))
            return $"{hero.Name} is not an eligible realm-tidings writer (needs an alive, free, landed lord of a kingdom, not of your clan).";
         if (profile.ReputationWithPlayer < MinRegardToWrite)
            return $"{hero.Name} does not hold you in high enough regard to write ({profile.ReputationWithPlayer} < {MinRegardToWrite}).";

         return Evaluate(store, profile, hero, (int) CampaignTime.Now.ToDays);
      }

      #region private

      private void OnWeeklyTick()
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            int today = (int) CampaignTime.Now.ToDays;

            // Resolve alive heroes once (O(1) lookup, skips the dead) rather than a per-profile scan, mirroring
            // StanceConsequenceBehavior's weekly sweep.
            var aliveById = new Dictionary<string, Hero>();
            foreach (Hero h in Hero.AllAliveHeroes)
               if (h?.StringId != null)
                  aliveById[h.StringId] = h;

            foreach (string id in store.ListIds())
            {
               if (!aliveById.TryGetValue(id, out Hero hero)) continue;
               if (!CanRelay(hero)) continue;

               NpcProfile profile = store.Get(id);
               if (profile == null) continue;
               if (profile.ReputationWithPlayer < MinRegardToWrite) continue;

               Evaluate(store, profile, hero, today);
            }
         }
         catch (Exception ex) { ModLog.Error($"[REALM-TIDINGS] weekly tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The shared core (steps 2-5): score the events this writer knows, let the pure policy pick the one worth
      ///   relaying they have not already sent, and enqueue a mailbox candidate carrying the news + confidence
      ///   framing. Records the chosen id in the per-NPC dedup ledger so it is never relayed again. Returns a
      ///   human-readable outcome for the console command; the weekly tick ignores the return.
      /// </summary>
      private static string Evaluate(BannerlordNpcMemoryStore store, NpcProfile profile, Hero hero, int today)
      {
         WorldEventStore events = WorldEventStore.Instance;
         if (events == null) return "World-event store not active.";

         // Do not stack realm-tidings letters: if one is already waiting in the mailbox for this writer, let it
         // compete and be released before choosing fresh news.
         foreach (LetterCandidateIntent pending in profile.LetterCandidates)
            if (pending.Reason == LetterReason.RealmTidings)
               return $"{hero.Name} already has a realm-tidings letter waiting in the mailbox.";

         IReadOnlyList<(WorldEvent Event, RumorConfidence Confidence)> known =
            events.GetKnownEventsRated(hero, today, MaxEventsScanned);

         var candidates = new List<RealmTidingsPolicy.RealmTidingsCandidate>(known.Count);
         var byId = new Dictionary<string, (WorldEvent Event, RumorConfidence Confidence)>();
         foreach ((WorldEvent evt, RumorConfidence confidence) in known)
         {
            // A friend relays news of the wider world, not the player's own deeds back to them, and cannot relay
            // an event that carries no id or no summary to quote.
            if (evt == null || string.IsNullOrEmpty(evt.Id) || string.IsNullOrWhiteSpace(evt.Summary) || evt.InvolvesPlayer)
               continue;

            candidates.Add(new RealmTidingsPolicy.RealmTidingsCandidate(evt.Id, RealmTidingsSalience.Of(evt.Magnitude, confidence)));
            byId[evt.Id] = (evt, confidence);
         }

         string chosenId = RealmTidingsPolicy.SelectSubject(candidates, profile.RelayedWorldEventIds);
         if (chosenId == null || !byId.TryGetValue(chosenId, out (WorldEvent Event, RumorConfidence Confidence) picked))
            return $"{hero.Name} has no fresh, worth-telling news to relay right now.";

         profile.LetterCandidates.Add(new LetterCandidateIntent {
            Reason = LetterReason.RealmTidings,
            Context = BuildContext(picked.Event, picked.Confidence),
            EligibleSinceDay = today,
            BaseWeightOverride = 0 // derive from LetterWeights.BaseWeight(RealmTidings)
         });
         RememberRelayed(profile, chosenId);
         store.Set(profile);

         ModLog.Info($"[REALM-TIDINGS] {hero.Name} will relay {picked.Event.Type} ({picked.Confidence}): {picked.Event.Summary}");

         return $"{hero.Name} will relay {picked.Event.Type} news ({picked.Confidence}): \"{picked.Event.Summary}\" (queued to the mailbox).";
      }

      /// <summary>
      ///   The trigger context handed to the letter generator: the news itself, framed by how surely it reached
      ///   the writer. Rides the same Context channel every other unbidden letter uses; the SDK prompt builder's
      ///   RealmTidings occasion text turns it into a relay and forbids inventing anything beyond it.
      /// </summary>
      private static string BuildContext(WorldEvent evt, RumorConfidence confidence)
         => $"News stirring in the realm ({DescribeKind(evt.Type)}). {ConfidenceFraming(confidence)} {evt.Summary} " +
            "Relay only this news, in your own voice, and add nothing that is not stated here.";

      /// <summary>The confidence framing derived from how surely the news reached the writer (faint / word-of-mouth / confirmed).</summary>
      private static string ConfidenceFraming(RumorConfidence confidence)
      {
         switch (confidence)
         {
            case RumorConfidence.WellKnown:  return "It is confirmed, and widely known, that:";
            case RumorConfidence.Secondhand: return "Word has reached you that:";
            default:                         return "You have caught only faint, unproven whispers that:";
         }
      }

      /// <summary>A short human-readable sense of the event kind, so the writer knows what sort of news they carry.</summary>
      private static string DescribeKind(WorldEventType type)
      {
         switch (type)
         {
            case WorldEventType.WarLooming:         return "a war may be brewing";
            case WorldEventType.WarDeclared:        return "war declared";
            case WorldEventType.PeaceDeclared:      return "a peace made";
            case WorldEventType.Hardship:           return "hard times in a town";
            case WorldEventType.Battle:             return "a battle fought";
            case WorldEventType.SettlementCaptured: return "a holding taken";
            case WorldEventType.Execution:          return "an execution";
            case WorldEventType.Marriage:           return "a marriage";
            case WorldEventType.Scandal:            return "a scandal";
            case WorldEventType.Rivalry:            return "bad blood between lords";
            case WorldEventType.Affront:            return "a public affront";
            default:                                return "word from the realm";
         }
      }

      /// <summary>Appends the relayed event id to the per-NPC ledger, capped drop-oldest so it never grows without bound.</summary>
      private static void RememberRelayed(NpcProfile profile, string eventId)
      {
         profile.RelayedWorldEventIds.Add(eventId);
         while (profile.RelayedWorldEventIds.Count > MaxRelayedIdsRemembered)
            profile.RelayedWorldEventIds.RemoveAt(0);
      }

      /// <summary>
      ///   An eligible writer: alive, free (not a prisoner), a landed/affiliated lord of a real kingdom, and not
      ///   of the player's own clan (a clanmate speaks in person). Regard is checked separately against the mod's
      ///   own personal-regard ledger (<see cref="NpcProfile.ReputationWithPlayer" />).
      /// </summary>
      private static bool CanRelay(Hero hero)
         => hero is {IsAlive: true, IsPrisoner: false}
            && hero != Hero.MainHero
            && hero.IsLord
            && hero.Clan != null
            && hero.Clan != Clan.PlayerClan
            && hero.Clan.Kingdom != null;

      #endregion
   }
}
