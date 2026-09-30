// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 3: carries out what the deed costs. The prices are the pure policy's (ViolenceConsequencePolicy);
// this only spends them, through the systems that already exist rather than any invented punishment.
//
// The law is the ENGINE'S OWN crime rating (IFaction.MainHeroCrimeRating via ChangeCrimeRatingAction), which CR had
// never touched before this pillar: it is visible in the vanilla UI and already drives guard hostility and bounties,
// so the punishment is real and legible instead of a number only this mod believes in. Kin carry a CR grudge, the
// victim's own regard collapses, and standing is lost only when the deed was seen.
//
// NOT DONE HERE (deliberate): being physically seized or arrested on a failed attempt. Bannerlord has no clean
// "guards apprehend the player mid-conversation" path, and faking one would strand the player in a scene the engine
// does not own. The crime rating is what brings the guards, in the game's own way and its own time.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Violence;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>Applies the real cost of an act of violence: the law, the victim, their kin, and the player's name.</summary>
   public static class ViolenceConsequenceService
   {
      /// <summary>The most kin of one house who will carry a grudge, so a large clan does not bury the player in identical grievances.</summary>
      private const int MaxKinBearingGrudge = 3;

      /// <summary>Charges the player for what they just did. Returns the resolved cost so a caller (or a self-test) can report it.</summary>
      public static ViolenceConsequence Apply(Hero victim, ViolentAct act, ViolenceVerdict verdict, ViolenceExposure exposure)
      {
         ViolenceConsequence cost = ViolenceConsequencePolicy.Resolve(act, verdict, exposure);

         try
         {
            if (victim == null || Hero.MainHero == null) return cost;

            ChargeCrime(victim, cost.CrimeRating);
            LoseStanding(cost.RenownLoss);
            CollapseRegard(victim, cost.RelationHit);
            RouseKin(victim, act, cost.KinGrudgeMagnitude);
            Circulate(victim, act, cost.RumorMagnitude);

            ModLog.Info($"[VIOLENCE] {act}/{verdict} on {victim.Name} ({exposure}): crime={cost.CrimeRating}, " +
                        $"regard={cost.RelationHit}, renown=-{cost.RenownLoss}, kinGrudge={cost.KinGrudgeMagnitude}.");

            // Arrests (Gabriel, 26/09/2026): violence against a member of the player's own realm is a charge in the
            // liege's name. A deed against a stranger's realm is that realm's crime rating, not an arrest by one's own.
            if (victim.MapFaction != null && victim.MapFaction == Hero.MainHero.MapFaction)
            {
               Logic.ArrestCharge? charge = Logic.ArrestPolicy.ChargeFor(act, verdict);
               if (charge != null) Arrests.ArrestBehavior.Instance?.Charge(charge.Value, $"violence against {victim.Name}");
            }
         }
         catch (Exception ex) { ModLog.Error($"[VIOLENCE] applying consequences failed: {ex.GetType().Name}: {ex.Message}"); }

         return cost;
      }

      /// <summary>
      ///   Lets word of the deed leave the room, when the policy says it becomes news. Shaped exactly like the
      ///   battle pillar's exploit event, and for the same reason: <c>InvolvesPlayer</c> is true, so a lord raises
      ///   it as a RUMOUR about the player rather than as impersonal gossip, and only the two who were in it are
      ///   named as actors, because tagging the victim's faction would let every lord of it claim to have been
      ///   there. This is the dark mirror of an exploit, on the same rails.
      /// </summary>
      private static void Circulate(Hero victim, ViolentAct act, int magnitude)
      {
         if (magnitude <= 0 || WorldEvents.WorldEventStore.Instance == null) return;

         var day = (int) CampaignTime.Now.ToDays;
         string playerName = Named(Hero.MainHero);
         string victimName = Named(victim);
         string place = victim.CurrentSettlement?.Name?.ToString() ?? "";

         WorldEvents.WorldEventStore.Instance.Record(new WorldEvents.WorldEvent {
            Id = $"cr_violence_{victim.StringId}_{day}",
            Type = WorldEvents.WorldEventType.Scandal,
            Day = day,
            Magnitude = magnitude,
            InvolvesPlayer = true,
            ActorIds = new List<string> {Hero.MainHero.StringId, victim.StringId},
            Summary = ViolenceRumorText.Gossiped(playerName, victimName, act, place),
            ChronicleLine = ViolenceRumorText.Chronicled(playerName, victimName, act, place)
         });

         RememberAmongWitnesses(playerName, victimName, act, day);
         ModLog.Info($"[VIOLENCE] word spreads: {ViolenceRumorText.Chronicled(playerName, victimName, act, place)}");
      }

      // Those who actually stood there carry it first-hand, not as something they were told.
      private static void RememberAmongWitnesses(string playerName, string victimName, ViolentAct act, int day)
      {
         string line = ViolenceRumorText.Witnessed(playerName, victimName, act);

         foreach (string witnessId in Chat.ChatManager.ActiveViewModel?.WitnessHeroIds ?? Enumerable.Empty<string>())
         {
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(witnessId);
            if (profile == null) continue;

            profile.Events.Add(new NotableEvent(day, NotableEventType.Confrontation, line));
            NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
         }
      }

      private static string Named(Hero hero)
         => Logic.HeroLabel.Describe(new Logic.HeroLabel.HeroNaming {Name = hero?.Name?.ToString(), ClanName = hero?.Clan?.Name?.ToString(), CultureName = hero?.Culture?.Name?.ToString()});

      // The engine's own crime rating against the victim's realm: this is what actually turns guards hostile.
      private static void ChargeCrime(Hero victim, int crime)
      {
         if (crime <= 0) return;

         IFaction realm = victim.MapFaction;
         if (realm == null) return;

         ChangeCrimeRatingAction.Apply(realm, crime);
      }

      private static void LoseStanding(int renownLoss)
      {
         if (renownLoss > 0) GameState.RenownPenalty.Apply(Hero.MainHero, renownLoss); // a negative GainRenownAction is a no-op
      }

      // The mod's OWN regard ledger, the one every conversation is judged on. A dead victim is charged nothing.
      private static void CollapseRegard(Hero victim, int relationHit)
      {
         if (relationHit >= 0 || victim.IsDead) return;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(victim.StringId);
         if (profile == null) return;

         NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(profile, relationHit, NpcMemoryService.Core.Standing.RegardCause.Violence);
         NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
         Standing.SelfEsteemService.Record(victim, Logic.SelfEsteemEvent.Humiliated); // beaten or robbed by the player
      }

      // Blood and beatings make enemies of a family. Capped, so a large house does not bury the player under a
      // dozen identical grievances, and the closest kin are the ones who take it up.
      private static void RouseKin(Hero victim, ViolentAct act, int magnitude)
      {
         if (magnitude <= 0 || GrudgeBehavior.Instance == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         string cause = act == ViolentAct.Kill
            ? $"The player murdered {victim.Name}."
            : $"The player beat {victim.Name} bloody.";

         foreach (Hero kin in KinOf(victim).Take(MaxKinBearingGrudge))
            GrudgeBehavior.Instance.TryPlant(kin, Hero.MainHero, GrudgeSource.External, cause, magnitude,
               knownToPlayer: true, today);
      }

      // Closest first: the family that shared a hearth, then the rest of the house.
      private static IEnumerable<Hero> KinOf(Hero victim)
      {
         var seen = new HashSet<string>();

         foreach (Hero close in new[] {victim.Spouse, victim.Father, victim.Mother}
                     .Concat(victim.Children ?? Enumerable.Empty<Hero>())
                     .Concat(victim.Siblings ?? Enumerable.Empty<Hero>()))
            if (Eligible(close, victim) && seen.Add(close.StringId))
               yield return close;

         foreach (Hero clansman in victim.Clan?.Heroes ?? Enumerable.Empty<Hero>())
            if (Eligible(clansman, victim) && seen.Add(clansman.StringId))
               yield return clansman;
      }

      private static bool Eligible(Hero kin, Hero victim)
         => kin != null && kin != victim && kin != Hero.MainHero && kin.IsAlive && !kin.IsChild;
   }
}
