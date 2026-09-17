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
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Social;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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

            Settlement here = Settlement.CurrentSettlement;
            bool atSea = MobileParty.MainParty?.IsCurrentlyAtSea ?? false;
            venue = DuelVenuePolicy.Choose(atSea, here != null);

            // The settlement duel is staged in the town-centre scene (TownDuelBehavior, on the street ambush's
            // recipe), and only a TOWN has one: a village or a castle would send the player into a location
            // that does not exist. The pure policy cannot know that (it is engine truth, not design), so the
            // narrowing belongs here, in the engine adapter, rather than in DuelVenuePolicy's contract.
            if (venue == DuelVenue.TownGates && !SettlementCanHostADuel(here)) venue = DuelVenue.NotNow;

            var ctx = new DuelChallengeContext {
               Challenger = DuelChallenger.Player,
               BothAbleToFight = BothAbleToFight(player, npc),
               IsSelfOrFamily = IsSelfOrFamily(player, npc),
               EitherHoldsOtherPrisoner = EitherHoldsOtherPrisoner(player, npc),
               // A lord is the obvious peer, but any NAMED hero of standing is a fair field of honour: your own
               // sworn companion, and a settlement notable (a gang leader, a merchant, a headman). A player who
               // wants to cross blades with one should be able to (player report: a duel negotiated with a
               // companion was never staged, because a Wanderer companion is not IsLord, so the challenge was
               // ruled NotAPeer and the action was never offered). Only the truly nameless commoner stays out.
               TargetIsAPeer = npc.IsLord || npc.IsPlayerCompanion || npc.IsNotable,
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
      public static bool SettlementCanHostADuel(Settlement here)
      {
         try { return here?.LocationComplex?.GetLocationWithId("center") != null; }
         catch { return false; }
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
            npc.IsAlive && !npc.IsChild && !npc.IsWounded;

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
