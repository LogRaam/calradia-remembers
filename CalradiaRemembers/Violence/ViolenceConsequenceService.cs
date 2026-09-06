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

            ModLog.Info($"[VIOLENCE] {act}/{verdict} on {victim.Name} ({exposure}): crime={cost.CrimeRating}, " +
                        $"regard={cost.RelationHit}, renown=-{cost.RenownLoss}, kinGrudge={cost.KinGrudgeMagnitude}.");
         }
         catch (Exception ex) { ModLog.Error($"[VIOLENCE] applying consequences failed: {ex.GetType().Name}: {ex.Message}"); }

         return cost;
      }

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
         if (renownLoss > 0) GainRenownAction.Apply(Hero.MainHero, -renownLoss, true);
      }

      // The mod's OWN regard ledger, the one every conversation is judged on. A dead victim is charged nothing.
      private static void CollapseRegard(Hero victim, int relationHit)
      {
         if (relationHit >= 0 || victim.IsDead) return;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(victim.StringId);
         if (profile == null) return;

         profile.ReputationWithPlayer += relationHit;
         NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
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
