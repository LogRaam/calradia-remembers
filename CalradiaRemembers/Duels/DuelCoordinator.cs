// Code written by Gabriel Mailhot, 22/07/2026.
// Duels pillar, task D: bridges the in-chat duel decision to the real duel mission and its map-side
// consequence, the same two-phase shape CaptiveEscapeCoordinator already validates. A mission cannot be
// opened from inside the chat screen, so the chat only ARMS a duel; this coordinator stages it once the
// chat has closed and the map is live again, then applies the finished fight's consequence on a LATER
// tick (the mission's own onDuelEnd callback fires just before the return to the map, while Mission.Current
// may still be non-null). Subscribed to TickEvent at session launch; the pending state is process-only (a
// duel never spans a save).

#region

using System;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Duels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Bridges the in-chat duel decision to the real duel mission and its map-side consequence.</summary>
   public static class DuelCoordinator
   {
      // ── Armed from chat, consumed by Phase 1 ────────────────────────────────
      private static Hero _pendingOpponent;        // set when the chat arms a duel; null = nothing pending
      private static DuelReason _pendingReason;
      private static DuelVenue _pendingVenue;
      private static bool _pendingNpcIssuedChallenge; // true = the NPC called the player out (player must answer)
      private static int _pendingWitnessCount;
      private static Hero _pendingCourtedHero;      // only meaningful for DuelReason.CourtshipRivalry
      private static bool _endedSettlementMissionForStaging; // guards the once-only EndMission on the per-frame tick

      // ── Fought, consumed by Phase 2 ──────────────────────────────────────────
      private static DuelResolution? _pendingResolution;
      private static Hero _outcomeOpponent;
      private static DuelReason _outcomeReason;
      private static Hero _outcomeCourtedHero;
      private static int _outcomeWitnessCount;

      /// <summary>
      ///   Arms a duel from the chat: stores the decision and returns immediately. Must NOT open anything
      ///   itself (a mission cannot be opened from inside the chat screen); <see cref="OnTick" /> stages it
      ///   once the chat has closed. <paramref name="npcIssuedChallenge" /> is true when the NPC called the
      ///   PLAYER out (the player must still answer, accept/refuse), false when the player called the NPC
      ///   out and the NPC has already accepted (the chat's own acceptance policy already ran).
      ///   <paramref name="courtedHero" /> is only meaningful for <see cref="DuelReason.CourtshipRivalry" />.
      /// </summary>
      public static void Arm(Hero opponent, DuelReason reason, DuelVenue venue, bool npcIssuedChallenge, int witnessCount, Hero courtedHero)
      {
         _pendingOpponent = opponent;
         _pendingReason = reason;
         _pendingVenue = venue;
         _pendingNpcIssuedChallenge = npcIssuedChallenge;
         _pendingWitnessCount = witnessCount;
         _pendingCourtedHero = courtedHero;
         ModLog.Info($"[DUEL] armed from chat: opponent={opponent?.Name?.ToString() ?? "(null)"}, reason={reason}, " +
                     $"venue={venue}, npcIssuedChallenge={npcIssuedChallenge}, witnesses={witnessCount}. Waiting for the chat to close.");
      }

      /// <summary>TickEvent handler (subscribed at session launch): stages the armed duel and, later, applies its outcome.</summary>
      public static void OnTick(float dt)
      {
         try
         {
            if (Campaign.Current == null) return;

            // Phase 2: the duel mission has ended -> apply the consequence on the live map.
            if (_pendingResolution.HasValue && Mission.Current == null)
            {
               DuelResolution resolution = _pendingResolution.Value;
               Hero opponent = _outcomeOpponent;
               DuelReason reason = _outcomeReason;
               Hero courtedHero = _outcomeCourtedHero;
               int witnessCount = _outcomeWitnessCount;
               _pendingResolution = null;
               _outcomeOpponent = null;
               _outcomeCourtedHero = null;

               DuelOutcomeApplier.Apply(reason, resolution, opponent, courtedHero, witnessCount);

               return;
            }

            // A settlement conversation leaves the player in the town's walkabout LOCATION mission when the chat
            // closes, so Mission.Current never clears on its own and the duel would wait until the player left the
            // town by hand (player report: "closing the conversation just ended the conversation, no duel"). End
            // that location mission now so the duel can stage at the gates next tick. It never touches a field
            // battle: LocationComplex.Current is null on the open map, so this only fires inside a settlement.
            if (_pendingOpponent != null && !_pendingResolution.HasValue && !ChatManager.IsOpen
                && Mission.Current != null && Settlement.CurrentSettlement != null && LocationComplex.Current != null)
            {
               // This runs every FRAME now (OnApplicationTick), so end the mission exactly once and then wait for
               // it to finalize (Mission.Current stays non-null for a few frames); calling EndMission repeatedly
               // would be wrong. The flag clears once the mission is gone (below), letting Phase 1 stage.
               if (!_endedSettlementMissionForStaging)
               {
                  _endedSettlementMissionForStaging = true;
                  ModLog.Info("[DUEL] ending the settlement walkabout mission so the armed duel can stage.");
                  Mission.Current.EndMission();
               }

               return;
            }

            _endedSettlementMissionForStaging = false; // no settlement mission is holding us up now

            // Phase 1: the chat has closed and no mission is running -> stage the duel exactly once.
            if (_pendingOpponent != null && Mission.Current == null && !ChatManager.IsOpen)
            {
               Hero opponent = _pendingOpponent;
               DuelReason reason = _pendingReason;
               bool npcIssuedChallenge = _pendingNpcIssuedChallenge;
               int witnessCount = _pendingWitnessCount;
               Hero courtedHero = _pendingCourtedHero;
               _pendingOpponent = null; // one-shot, whichever way this turns out

               // Resolve the venue from where the player stands NOW, not where they were when the duel was armed:
               // one who armed a duel in a town but has since walked out onto the map fights in the wilderness,
               // not a stale town duel whose guard fails silently (player report: "hit yes, nothing happened").
               bool atSea = MobileParty.MainParty?.IsCurrentlyAtSea == true;
               DuelVenue venue = DuelVenuePolicy.Choose(atSea, Settlement.CurrentSettlement != null);

               // The only unstageable case is at sea: tell the player rather than dropping it in silence.
               if (!DuelStagingSupport.CanStage(venue))
               {
                  InformationManager.DisplayMessage(new InformationMessage(
                     $"There is nowhere to hold a duel with {opponent?.Name} out here; it must wait until you are ashore."));
                  ModLog.Info($"[DUEL] armed venue {venue} cannot be staged; cleared.");

                  return;
               }

               if (npcIssuedChallenge)
                  AskPlayerToAnswer(opponent, reason, venue, courtedHero, witnessCount);
               else
                  LaunchDuel(opponent, reason, venue, courtedHero, witnessCount);
            }
         }
         catch (Exception ex) { ModLog.Error($"[DUEL] coordinator tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #region private

      /// <summary>
      ///   The NPC called the player out: the player answers with the same accept/refuse inquiry shape
      ///   ModCheats.ForceDuelChallenge already uses. "Draw your sword" launches the fight; "Decline" records
      ///   a PlayerForfeited outcome (no blades crossed).
      /// </summary>
      private static void AskPlayerToAnswer(Hero opponent, DuelReason reason, DuelVenue venue, Hero courtedHero, int witnessCount)
      {
         InformationManager.ShowInquiry(new InquiryData(
            "A Challenge",
            $"{opponent.Name} demands satisfaction of you: only steel will answer this. Do you take up the challenge?",
            true, true,
            "Draw your sword", "Decline",
            () => LaunchDuel(opponent, reason, venue, courtedHero, witnessCount),
            () =>
            {
               DuelOutcomeApplier.Apply(reason, DuelResolution.PlayerForfeited, opponent, courtedHero, witnessCount);
               InformationManager.DisplayMessage(new InformationMessage($"You would not draw on {opponent.Name}."));
            }), true);
      }

      /// <summary>
      ///   Opens the duel at the armed venue and stashes what Phase 2 needs to apply the outcome once the
      ///   mission ends. The two venues are genuinely different missions, not two skins of one: the open field
      ///   spawns both fighters into a fresh battle scene, while the town injects the opponent into the living
      ///   town-centre crowd. They share only this callback, which is the point of keeping the outcome in
      ///   Phase 2 rather than in either mission.
      /// </summary>
      private static void LaunchDuel(Hero opponent, DuelReason reason, DuelVenue venue, Hero courtedHero, int witnessCount)
      {
         void Report(bool playerWon)
         {
            _outcomeOpponent = opponent;
            _outcomeReason = reason;
            _outcomeCourtedHero = courtedHero;
            _outcomeWitnessCount = witnessCount;
            _pendingResolution = playerWon ? DuelResolution.PlayerWon : DuelResolution.NpcWon;
         }

         string result = venue == DuelVenue.TownGates
            ? TownDuelBehavior.Arm(opponent, Report)
            : DuelMissionLauncher.StartWildernessDuel(opponent, Report);

         // Show the outcome of the launch to the player (a confirmation on success, or the reason on failure)
         // rather than only logging it: a silent no-op on "Draw your sword" was the reported bug.
         if (!string.IsNullOrWhiteSpace(result))
            InformationManager.DisplayMessage(new InformationMessage(result));

         ModLog.Info($"[DUEL] staging at {venue}: {result}");
      }

      #endregion
   }
}
