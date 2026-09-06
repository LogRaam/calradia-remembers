// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence Inc 2: the three verbs the interpreter may emit when the PLAYER robs, beats, or kills the NPC they
// are speaking to. The direction is the reverse of every other harm verb in the mod, which is precisely the confusion
// that produced the player report (the model reached for execute_player, meaning the NPC killing the player).
//
// THE BRIDGE IS LAW. Nothing here trusts the exchange: the lethal toggle is re-read at execution (never mind what the
// prompt allowed when the turn began), the odds are recomputed from the live scene, and the roll happens here. So a
// narrated murder in a campaign with killing switched off changes nothing at all, and a narrated murder of a guarded
// champion by a beggar almost always fails.
//
// Teaching is left to the action catalog (GameActionCatalog): TeachingText returns null so the ordinary conversation
// prompt never advertises violence to every NPC the player greets.

#region

using System;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Violence;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>rob_npc / assault_npc / kill_npc: the player turning on the character they are speaking to.</summary>
   public sealed class PlayerViolenceVerb : IConversationVerb
   {
      private readonly ViolentAct _act;

      public PlayerViolenceVerb(ViolentAct act) => _act = act;

      public string Name
      {
         get
         {
            switch (_act)
            {
               case ViolentAct.Rob: return "rob_npc";
               case ViolentAct.Assault: return "assault_npc";
               default: return "kill_npc";
            }
         }
      }

      public bool IsEligible(VerbContext ctx)
         => ctx?.Npc != null && ctx.Npc != Hero.MainHero
            && ViolencePolicy.IsPermitted(_act, ModSettings.AllowKillingCharacters);

      /// <summary>Null on purpose: the action catalog teaches these, so an ordinary conversation is never invited to violence.</summary>
      public string TeachingText(VerbContext ctx) => null;

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero target = ctx?.Npc;
            if (target == null || Hero.MainHero == null || target == Hero.MainHero) return null;

            // Re-read the toggle here, not at teaching time: a campaign with killing off must never lose a life,
            // whatever the exchange narrated.
            if (!ViolencePolicy.IsPermitted(_act, ModSettings.AllowKillingCharacters))
               return ActionOutcome.Refused($"(You could not bring yourself to end {target.Name}. Nothing happened.)");

            ViolenceFacts facts = ViolenceSceneReader.FactsFor(target);
            int chance = ViolencePolicy.SuccessChance(_act, facts);
            int roll = MBRandom.RandomInt(100);
            ViolenceVerdict verdict = ViolencePolicy.Resolve(_act, facts, ModSettings.AllowKillingCharacters, roll);

            ModLog.Info($"[VIOLENCE] {Name} on {target.Name}: exposure={facts.Exposure}, chance={chance}%, roll={roll} -> {verdict}.");

            // The deed is done (or botched) BEFORE the world answers: a killing must land before its kin can mourn,
            // and the victim's regard must be charged while they are still there to lose it.
            ActionOutcome outcome = verdict == ViolenceVerdict.Succeeded
               ? Succeeded(target, ctx)
               : Failed(target, facts.Exposure);

            ViolenceConsequenceService.Apply(target, _act, verdict, facts.Exposure);

            return outcome;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[VIOLENCE] {Name} failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      // A failed attempt is a real event, not a refusal: it happened, and it changed nothing but the air between
      // them. What it costs the player (guards, crime, a grudge) is the next increment.
      private ActionOutcome Failed(Hero target, ViolenceExposure exposure)
      {
         string why = exposure == ViolenceExposure.Guarded
            ? " Those at their side saw to that."
            : exposure == ViolenceExposure.Onlookers
               ? " There were too many eyes on you."
               : "";

         RememberOnTarget(target, $"You turned on me and it came to nothing.{(exposure == ViolenceExposure.Alone ? "" : " Others were there.")}");

         return new ActionOutcome($"Your attempt on {target.Name} fails.{why}", false);
      }

      private ActionOutcome Succeeded(Hero target, VerbContext ctx)
      {
         switch (_act)
         {
            case ViolentAct.Rob:
            {
               int take = ViolencePolicy.RobberyTake(target.Gold);
               if (take <= 0)
               {
                  RememberOnTarget(target, "You turned on me for my purse and found it empty.");

                  return new ActionOutcome($"{target.Name} has nothing worth taking.", false);
               }

               GiveGoldAction.ApplyBetweenCharacters(target, Hero.MainHero, take);
               RememberOnTarget(target, $"You robbed me of {take} denars, with a blade between us.");

               return new ActionOutcome($"You take {take} denars from {target.Name} by force (purse: {Hero.MainHero.Gold}).", false);
            }
            case ViolentAct.Assault:
               target.MakeWounded(Hero.MainHero);
               RememberOnTarget(target, "You beat me down with your own hands.");

               return new ActionOutcome($"You beat {target.Name} down. They will live, and they will remember.", false);
            default:
               // The victim keeps no memory of their own death; what the world makes of it is a later increment.
               KillCharacterAction.ApplyByMurder(target, Hero.MainHero);

               return new ActionOutcome($"You kill {target.Name}.", false);
         }
      }

      // The victim's own first-hand memory, so the deed is never silent and they can speak of it next time.
      private static void RememberOnTarget(Hero target, string summary)
      {
         NpcProfile profile = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(target.StringId);
         if (profile == null) return;

         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Confrontation, summary));
         Behaviors.NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
      }
   }
}
