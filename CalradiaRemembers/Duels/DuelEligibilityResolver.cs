// Code written by Gabriel Mailhot, 22/07/2026.
// ONE resolver, TWO callers, so they can never quietly diverge: EncounterContextBuilder asks it at PROMPT time
// (whether to teach the challenge_duel verb this turn) and the chat action handler asks it again at COMMIT time,
// right before it actually opens the duel mission. "The prompt is advice, the bridge is law": the context is
// built at the START of a turn, but the LLM may not emit the challenge for several turns after, and by then the
// facts (a fresh captivity, the NPC walking off, a duel fought meanwhile) can have changed. If the eligibility
// logic lived twice, one copy would eventually drift from the other and either teach a duel the bridge refuses,
// or refuse one the prompt just promised.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Social;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Resolves whether a duel between the player and an NPC is admissible RIGHT NOW, and where it would be staged.</summary>
   public static class DuelEligibilityResolver
   {
      /// <summary>
      ///   True when a duel challenge between the player and <paramref name="npc" /> is admissible this instant
      ///   (<see cref="DuelChallengePolicy.Evaluate" />) AND a venue actually exists to stage it right now
      ///   (<see cref="DuelStagingSupport.CanStage" />). Null-safe: a null <paramref name="npc" /> or
      ///   <paramref name="profile" />, or a missing <see cref="Hero.MainHero" />, returns false. Never throws:
      ///   this gates both what the prompt teaches and what the bridge is about to commit to, so a bad save or an
      ///   engine surprise must never crash a live conversation. <paramref name="venue" /> is always assigned
      ///   (<see cref="DuelVenue.NotNow" /> on any failure or refusal path), so a caller building a refusal line
      ///   still has a venue to reason about.
      /// </summary>
      public static bool CanDuelNow(Hero npc, NpcProfile profile, out DuelVenue venue)
      {
         venue = DuelVenue.NotNow;

         try
         {
            Hero player = Hero.MainHero;
            if (npc == null || profile == null || player == null) return false;

            // THE SAME QUESTION THE COORDINATOR ASKS, asked here in the same words. It used to be asked twice
            // and differently: this line passed "is there a settlement at all" and then narrowed to NotNow,
            // while DuelCoordinator passed "does the settlement own a centre" and narrowed to nothing. So on
            // the map beside a town, one of them refused the duel outright and the other staged a town duel
            // from outside the walls. Disagreeing on this exact question is what crashed a player twice.
            //
            // The narrowing goes with it: standing outside the walls is not a reason to refuse a duel, it is
            // a reason to fight in the open field, which is what Choose already answers.
            // Composed in ONE place (VenueForPlayerNow), so nothing can compose it slightly differently again.
            venue = VenueForPlayerNow();

            var ctx = new DuelChallengeContext {
               Challenger = DuelChallenger.Player,
               BothAbleToFight = BothAbleToFight(player, npc),
               IsSelfOrFamily = IsSelfOrFamily(player, npc),
               EitherHoldsOtherPrisoner = EitherHoldsOtherPrisoner(player, npc),
               // avalanche24 (Nexus, 28/10/2026) and Gabriel's ruling: the duel is the nobles' affair; a companion only
               // brawls, and a notable has no duel to call. The v1.31.9 widening to companions and notables is undone.
               TargetIsAPeer = DuelPeerPolicy.IsHonourPeer(new DuelPeerFacts {
                  IsNoble = npc.IsLord, IsPlayerCompanion = npc.IsPlayerCompanion, IsNotable = npc.IsNotable
               }),
               // The two ONLY share a place if they are genuinely beside one another right now: the exact
               // engine-live gate PlayerPresenceResolver already offers every other channel (chat, letters,
               // jealousy), re-checked fresh at commit time, which is what catches an NPC who walked off
               // mid-conversation between the prompt build and the LLM finally emitting the action.
               SharePlace = PlayerPresenceResolver.IsBeside(npc),
               DaysSinceLastDuel = profile.LastDuelDay.HasValue
                  ? (double?) ((int) CampaignTime.Now.ToDays - profile.LastDuelDay.Value)
                  : null
            };

            DuelEligibility eligibility = DuelChallengePolicy.Evaluate(ctx);

            return eligibility == DuelEligibility.Eligible && DuelStagingSupport.CanStage(venue);
         }
         catch (Exception ex)
         {
            ModLog.Fault("DuelEligibilityResolver.CanDuelNow", ex);
            venue = DuelVenue.NotNow;

            return false;
         }
      }

      /// <summary>
      ///   The short phrase for the NPC's own line. An empty string (never null) for
      ///   <see cref="DuelVenue.NotNow" />, so a caller can render it unconditionally. The
      ///   <see cref="DuelVenue.TownGates" /> wording says "in the streets" rather than naming a gate, because
      ///   the fight is actually staged in the town-centre scene: the NPC must not promise the player a place
      ///   the game will not take them to.
      /// </summary>
      /// <summary>
      ///   Whether this settlement has somewhere two people can actually cross blades: a walkable "center"
      ///   location, which the engine itself answers for.
      ///   <para>
      ///     This used to read <c>here.IsTown</c>, and a player found what that cost (FloatingMammal, Nexus,
      ///     2026-09-10): "none of the lords I 'challenged' to a duel went anywhere other than a dialogue driven
      ///     event... I think now they're assuming its a sort spar, even when the wording 'duel' is being said by
      ///     me continuously." Lords are met in CASTLES more often than anywhere else, and a castle courtyard is
      ///     as good a place to fight as a town square, but the type check refused it, so challenge_duel was
      ///     never taught and the model improvised a sparring fiction. The same silence-reads-as-permission
      ///     failure as follow_me, one verb over.
      ///   </para>
      ///   <para>
      ///     Asking the engine for a center rather than naming settlement types means a castle works, a village
      ///     works if this build gives one a walkable centre, and a future settlement kind works without anyone
      ///     remembering this line. Fails CLOSED: an unreadable settlement is not a duelling ground.
      ///   </para>
      /// </summary>
      /// <summary>
      ///   Whether a duel can actually be STAGED in this settlement: it needs a "center" location to open, and
      ///   not every settlement has one. A modded or total-conversion town may carry none at all.
      ///
      ///   PUBLIC since 17/09/2026, because it was right and private while the STAGING path asked a different
      ///   and emptier question — Settlement.CurrentSettlement != null. So the verb was correctly withheld
      ///   where it could not be honoured, and a duel armed by the ACTION BUTTON still resolved to a town
      ///   venue, whose location came back null and went into the engine unchecked. It crashed loading the
      ///   scene (player report, 17/09/2026, in a settlement named Vaen-Griefendok, which is nobody's
      ///   Calradia). Two predicates for one question, and only one of them was ever right.
      /// </summary>
      /// <summary>
      ///   Whether the player can fight a TOWN duel right where they stand: they are INSIDE a settlement, and
      ///   that settlement owns a "center" location to fight in. The second half alone was the old question,
      ///   and it is the one that crashed yozakura12 twice (see DuelStagingSupport.CanHoldATownDuel): a
      ///   settlement the party is merely standing on, on the map, owns a centre just the same.
      ///   <para>
      ///     Deliberately renamed from SettlementCanHostADuel. The old name described the SETTLEMENT, and the
      ///     defect was that nobody was asking about the PLAYER. A predicate whose name answers a different
      ///     question than its body is how both halves of this went wrong.
      ///   </para>
      /// </summary>
      public static bool PlayerCanFightADuelHere(Settlement here)
      {
         try
         {
            return DuelStagingSupport.CanHoldATownDuel(
               PlayerEncounter.InsideSettlement,
               here?.LocationComplex?.GetLocationWithId("center") != null);
         }
         catch { return false; }
      }

      /// <summary>
      ///   The venue a duel armed RIGHT NOW would be fought at, composed exactly as <c>CanDuelNow</c> composes
      ///   it. It exists so the diagnostic commands stop mimicking that composition with a test of their own:
      ///   <c>cr.duel_arm</c> used to read <c>IsTown</c> and could therefore never reproduce the castle case,
      ///   which is the "a guard that measures its own construction" failure in a tool whose entire job is to
      ///   reproduce. Anything that wants to know where a duel would happen asks here.
      /// </summary>
      public static DuelVenue VenueForPlayerNow()
      {
         try
         {
            return DuelVenuePolicy.Choose(
               MobileParty.MainParty?.IsCurrentlyAtSea ?? false,
               PlayerCanFightADuelHere(Settlement.CurrentSettlement));
         }
         catch { return DuelVenue.Wilderness; }
      }

      public static string VenueLabel(DuelVenue venue)
      {
         switch (venue)
         {
            case DuelVenue.Wilderness: return "on the open field";
            // "here" rather than "in the streets of this town": the same venue now covers a castle courtyard,
            // and telling a player they will fight in the streets of a castle reads as a bug.
            case DuelVenue.TownGates: return "here, within these walls";
            default: return string.Empty;
         }
      }

      #region private

      /// <summary>Both alive, of fighting age, and not wounded past fighting. Captivity is judged separately, not here.</summary>
      private static bool BothAbleToFight(Hero player, Hero npc)
         => player.IsAlive && !player.IsChild && !player.IsWounded &&
            Mapping.ActionGate.Allows(npc, Logic.ActionKind.Duel) && !npc.IsWounded;

      /// <summary>Either party currently holds the other prisoner, mirroring BannerlordGameStateBridge.BuildStandingFacts exactly.</summary>
      private static bool EitherHoldsOtherPrisoner(Hero player, Hero npc)
         => (player.IsPrisoner && player.PartyBelongedToAsPrisoner?.LeaderHero == npc) ||
            (npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == player);

      /// <summary>The NPC is the player's spouse, parent, or child. Siblings are deliberately excluded: the policy's own contract names only these three.</summary>
      private static bool IsSelfOrFamily(Hero player, Hero npc)
         => npc == player || npc == player.Spouse || npc == player.Father || npc == player.Mother ||
            (player.Children != null && player.Children.Contains(npc));

      #endregion
   }
}
