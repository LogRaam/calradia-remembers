// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc B: turns a finished duel into its live-game consequences. The pure policies decide the
// numbers (DuelOutcomePolicy the generic renown/stance, CourtshipDuelPolicy the courted hero's attraction);
// this is the single place that WRITES them to the campaign, plus a world-event so the outcome travels (the
// courted lady learns of it without having to be present). Non-lethal is assumed upstream, so nothing here ever
// touches a hero's life; it moves only perception, which is the whole point of keeping a chat-born duel unable
// to reach the strategic board.

#region

using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Applies a duel's consequences to the live campaign: renown, the opponent's stance, the courted hero's attraction, and a world-event.</summary>
   public static class DuelOutcomeApplier
   {
      /// <summary>The attraction axis is [-100,100]; clamp so a run of duels can never push it out of range.</summary>
      private const int AttractionFloor = -100;
      private const int AttractionCeiling = 100;

      /// <summary>Modest weight for a duel world-event: real news, but not a battle that shakes a realm.</summary>
      private const int DuelEventMagnitude = 6;

      /// <summary>Convenience for a duel that was actually FOUGHT: true means the player won, false the NPC won.</summary>
      public static void Apply(DuelReason reason, bool playerWon, Hero opponent, Hero courtedHero, int witnessCount)
         => Apply(reason, playerWon ? DuelResolution.PlayerWon : DuelResolution.NpcWon, opponent, courtedHero, witnessCount);

      /// <summary>
      ///   Applies the aftermath of a duel by its <paramref name="resolution" /> (fought either way, or refused
      ///   by either side). <paramref name="courtedHero" /> is only meaningful for a
      ///   <see cref="DuelReason.CourtshipRivalry" /> duel (the third party whose regard is at stake); pass null
      ///   otherwise. Witness count feeds the renown swing (a win, or a cowardice, before a crowd travels further).
      ///   Also stamps <see cref="NpcProfile.LastDuelDay" /> on the OPPONENT for every resolution, including both
      ///   forfeits: the field's own SDK summary reads "the day these two last crossed blades", but it must mean
      ///   "the day this matter was last put to steel between these two, settled or ducked". A refusal that reset
      ///   nothing would let the player re-challenge the same lord every turn and farm
      ///   <see cref="DuelOutcomePolicy.RenownForCalledBluff" /> (the chat handler applies NpcForfeited whenever
      ///   the NPC balks at the player's own challenge), so the cooldown has to close on a duck exactly as it
      ///   does on a real fight.
      /// </summary>
      public static void Apply(DuelReason reason, DuelResolution resolution, Hero opponent, Hero courtedHero, int witnessCount)
      {
         if (opponent == null || Hero.MainHero == null) return;

         // Whether a live grudge the opponent holds against the player drove this duel, read from the same
         // ranked source the prompt itself reads (LiveGrudgesFor), filtered to one a duel could plausibly
         // settle: a vendetta-grade grudge (IsResolvable false) is never something steel alone answers.
         Grudge drivingGrudge = GrudgeBehavior.Instance?.LiveGrudgesFor(opponent, (int) CampaignTime.Now.ToDays)
            ?.FirstOrDefault(g => GrudgePolicy.IsResolvable(g) && g.TargetId == Hero.MainHero.StringId);

         // 1. Generic aftermath: the player's renown and the OPPONENT's stance toward them. Honour is the only
         //    stake for now (no material wager from the console path).
         var ctx = new DuelResolutionContext {
            Resolution = resolution,
            AGrudgeDroveIt = drivingGrudge != null,
            Stake = DuelStakeKind.Honour,
            WitnessCount = witnessCount
         };
         DuelOutcomeResult generic = DuelOutcomePolicy.Resolve(ctx);

         if (generic.PlayerRenownDelta != 0)
            GainRenownAction.Apply(Hero.MainHero, generic.PlayerRenownDelta, true);

         // A duel lost in single combat costs the loser's HOUSE standing (Gabriel 2026-08-26). The policy sets
         // this only for a fought duel; the loser is the NPC when the player won and the player when the NPC won
         // (a forfeit leaves it zero, so this no-ops there). Applied to the clan, not the hero, since influence
         // is a clan resource; guarded because a wandering hero may have no clan.
         if (generic.LoserInfluenceDelta != 0)
         {
            Hero loser = resolution == DuelResolution.PlayerWon ? opponent
               : resolution == DuelResolution.NpcWon ? Hero.MainHero
               : null;

            if (loser?.Clan != null)
            {
               ChangeClanInfluenceAction.Apply(loser.Clan, generic.LoserInfluenceDelta);
               ModLog.Info($"[DUEL] {loser.Name}'s clan loses {-generic.LoserInfluenceDelta} influence for the defeat.");
            }
         }

         // ApplyDeed is a no-op on a zero delta or an unmet hero, and it persists the profile itself.
         StanceService.ApplyDeed(opponent, StanceAxis.Affection, generic.NpcAffectionDelta);
         StanceService.ApplyDeed(opponent, StanceAxis.Respect, generic.NpcRespectDelta);
         StanceService.ApplyDeed(opponent, StanceAxis.Fear, generic.NpcFearDelta);

         // NOT YET APPLIED: generic.GrudgeEffect (Settled/Hardened) is computed above but never mutates
         // drivingGrudge itself. Settling it cleanly is possible today (GrudgePolicy.Resolve), but there is no
         // equivalent clean API anywhere in the Grudges pillar to HARDEN one (raise its Magnitude/Rigidity);
         // only formation (GrudgeBehavior.TryPlant) and softening/resolving (GrudgeResolutionService) exist.
         // Applying just the settle half and silently dropping the harden half would read as "duels only ever
         // help a grudge, never worsen one", which is the opposite of what DuelOutcomePolicy's own header
         // promises. Left unapplied on both sides rather than invented or half-wired; see the report for this
         // increment. The stance/renown numbers above already move correctly either way (AGrudgeDroveIt feeds
         // DuelOutcomePolicy's existing warmth bonus on a fought duel).
         if (drivingGrudge != null && generic.GrudgeEffect != DuelGrudgeEffect.None)
            ModLog.Info($"[DUEL] a live grudge drove this duel; computed effect {generic.GrudgeEffect} on {opponent.Name}'s grudge, NOT applied (no clean harden API).");

         // 2. Courtship-specific: the courted hero's attraction toward the player swings on who prevailed.
         if (reason == DuelReason.CourtshipRivalry && courtedHero != null)
         {
            CourtshipDuelConsequence courtship = CourtshipDuelPolicy.Resolve(resolution);
            ApplyAttraction(courtedHero, courtship.CourtedAttractionDelta);
         }

         StampLastDuelDay(opponent);
         RecordDuelEvent(reason, resolution, opponent, courtedHero);
      }

      #region private

      /// <summary>
      ///   Stamps <see cref="NpcProfile.LastDuelDay" /> on the opponent for every resolution (fought or
      ///   forfeited by either side): see <see cref="Apply(DuelReason, DuelResolution, Hero, Hero, int)" />'s
      ///   own doc for why a forfeit must close the cooldown exactly as a real fight does. No-op when the
      ///   opponent has no stored profile yet.
      /// </summary>
      private static void StampLastDuelDay(Hero opponent)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(opponent.StringId);

         if (profile == null) return;

         profile.LastDuelDay = (int) CampaignTime.Now.ToDays;
         store.Set(profile);
      }

      /// <summary>Moves the courted hero's attraction toward the player, clamped, and persists it. Logs the swing so a test can see it.</summary>
      private static void ApplyAttraction(Hero hero, int delta)
      {
         if (hero == null || delta == 0) return;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(hero.StringId);

         if (profile?.Romantic == null) return;

         int before = profile.Romantic.AttractionToPlayer;
         int after = before + delta;
         after = after < AttractionFloor
            ? AttractionFloor
            : after > AttractionCeiling
               ? AttractionCeiling
               : after;

         profile.Romantic.AttractionToPlayer = after;
         store.Set(profile);
         ModLog.Info($"[DUEL] {hero.Name}'s attraction toward you: {before} -> {after} (delta {delta}).");
      }

      /// <summary>Records the duel so it travels as hearsay and chronicle, letting the courted hero learn of an outcome they did not witness.</summary>
      private static void RecordDuelEvent(DuelReason reason, DuelResolution resolution, Hero opponent, Hero courtedHero)
      {
         WorldEventStore store = WorldEventStore.Instance;

         if (store == null) return;

         string player = Hero.MainHero.Name?.ToString();
         string foe = opponent.Name?.ToString();
         var actors = new System.Collections.Generic.List<string> {Hero.MainHero.StringId, opponent.StringId};
         bool playerRefused = resolution == DuelResolution.PlayerForfeited;
         bool playerWon = resolution == DuelResolution.PlayerWon || resolution == DuelResolution.NpcForfeited;

         string summary;
         string chronicle;

         if (reason == DuelReason.CourtshipRivalry && courtedHero != null)
         {
            string lady = courtedHero.Name?.ToString();
            if (!string.IsNullOrEmpty(courtedHero.StringId)) actors.Add(courtedHero.StringId);

            summary = playerRefused
               ? $"{player} refused {foe}'s challenge to a duel over {lady}, and would not take the field."
               : playerWon
                  ? $"{player} bested {foe} in single combat, fought over {lady}."
                  : $"{foe} bested {player} in single combat, fought over {lady}.";
            chronicle = playerRefused
               ? $"I would not cross blades with {foe} over {lady}, and let the challenge stand unanswered."
               : playerWon
                  ? $"I crossed blades with {foe} for {lady}'s regard, and won."
                  : $"I crossed blades with {foe} for {lady}'s regard, and was bested.";
         }
         else
         {
            summary = playerRefused
               ? $"{player} refused {foe}'s challenge to a duel of honour."
               : playerWon
                  ? $"{player} bested {foe} in a duel of honour."
                  : $"{foe} bested {player} in a duel of honour.";
            chronicle = playerRefused
               ? $"I refused {foe}'s challenge, and would not answer with steel."
               : playerWon
                  ? $"I answered {foe} with steel, and won."
                  : $"I answered {foe} with steel, and was bested.";
         }

         store.Record(new WorldEvent {
            Type = reason == DuelReason.CourtshipRivalry
               ? WorldEventType.Rivalry
               : WorldEventType.Affront,
            Day = (int) CampaignTime.Now.ToDays,
            Magnitude = DuelEventMagnitude,
            InvolvesPlayer = true,
            ActorIds = actors,
            Summary = summary,
            ChronicleLine = chronicle
         });
      }

      #endregion
   }
}
