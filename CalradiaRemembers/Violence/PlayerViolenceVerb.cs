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
// Teaching is the action catalog's (GameActionCatalog): TeachingText returns null, and the catalogue spec is taught to both
// readers, the interpreter and, since 25/10/2026, the prose model too (ProseActionVocabulary; Gabriel: "enseigne la
// violence aux deux modes", once it showed a player in Standard composition could never act violently in words).

#region

using System;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
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

      /// <summary>Null on purpose: the catalogue spec teaches these, to both readers (ProseActionVocabulary), so no second text exists.</summary>
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
               return ActionOutcome.Refused($"(This game's settings do not allow killing, so {target.Name} was not killed.)");

            // A killing is the one irreversible act here, and the MODEL decides it happened. So the player, not the
            // model, has the last word: ask, and only then strike. The inquiry is asynchronous while Execute must
            // answer now, so the deed resolves in the callback and reports itself; this turn only says the blade is
            // raised. Anything recoverable (a purse, a beating) proceeds without interrupting the scene.
            if (ViolencePolicy.RequiresConfirmation(_act)) return OfferKill(target, ctx);

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

      /// <summary>
      ///   Puts the killing to the PLAYER before it happens, with its chance (zhenja1, 31/10/2026: he confirmed a
      ///   killing twice, the roll failed twice, and nothing had said there was a roll). Nothing is resolved here: no
      ///   roll, no death, no crime. In a chat that can hold it, the chat's own window asks, plays the roll and has the
      ///   scene told (ChatViewModel.KillAttempt); elsewhere the game's popup asks and the outcome shows as a notice.
      ///   Saying no means nothing whatsoever occurred, which is the point: an LLM that over-read a threat can no
      ///   longer cost a life.
      /// </summary>
      private ActionOutcome OfferKill(Hero target, VerbContext ctx)
      {
         string name = target.Name?.ToString() ?? "them";

         // The game itself may hold this life (a task under way that needs them alive). Said now, before any
         // window: a "you kill him" that the game then quietly refuses is exactly the report.
         if (!target.CanDie(KillCharacterAction.KillCharacterActionDetail.Murdered))
         {
            ModLog.Info($"[VIOLENCE] kill_npc on {name}: the game will not let them die now (CanHeroDie); refused before asking.");

            return ActionOutcome.Refused($"({name} cannot be killed now: the game holds them for a task under way.)");
         }

         // The chance is measured once, here, and the roll is made against these same facts, so the number the
         // player read is the number that decides.
         ViolenceFacts facts = ViolenceSceneReader.FactsFor(target);
         int chance = ViolencePolicy.SuccessChance(ViolentAct.Kill, facts);

         if (ChatManager.ActiveViewModel?.OfferKillAttempt(target, facts, chance, roll => ResolveKill(target, ctx, facts, roll)) == true)
            return new ActionOutcome($"Your blade is raised against {name}, and the moment waits on your word.", false);

         InformationManager.ShowInquiry(new InquiryData(
            "Take a life?",
            $"You are about to kill {name}. Your chance: {chance} in 100. {KillAttemptPolicy.ChanceReasons(facts)} This cannot be undone, and the realm will answer for it.",
            true, true,
            "Kill", "Stay your hand",
            () => ResolveConfirmedKill(target, ctx, facts), () => DeclineKill(name)), true);

         return new ActionOutcome($"Your blade is raised against {name}, and the moment waits on your word.", false);
      }

      // The popup's yes: roll, apply, and announce the outcome, since the chat line for this turn is already gone.
      private void ResolveConfirmedKill(Hero target, VerbContext ctx, ViolenceFacts facts)
      {
         KillAttemptResult result = ResolveKill(target, ctx, facts, MBRandom.RandomInt(100));
         if (!string.IsNullOrEmpty(result?.Message)) InformationManager.DisplayMessage(new InformationMessage(result.Message));
      }

      /// <summary>
      ///   The player struck: the roll against the facts the chance was read from, the deed, its consequences. Null
      ///   when nothing could happen (the target already gone, the setting turned off meanwhile, an error).
      /// </summary>
      private KillAttemptResult ResolveKill(Hero target, VerbContext ctx, ViolenceFacts facts, int roll)
      {
         try
         {
            if (target == null || !target.IsAlive) return null;

            // Re-read the toggle even now: the player could have changed it while the window sat open.
            if (!ViolencePolicy.IsPermitted(ViolentAct.Kill, ModSettings.AllowKillingCharacters)) return null;

            ViolenceVerdict verdict = ViolencePolicy.Resolve(ViolentAct.Kill, facts, ModSettings.AllowKillingCharacters, roll);

            // The game may still refuse the death (a task that began while the window was open): then nothing landed.
            if (verdict == ViolenceVerdict.Succeeded && !target.CanDie(KillCharacterAction.KillCharacterActionDetail.Murdered))
            {
               ModLog.Info($"[VIOLENCE] kill_npc on {target.Name}: the roll came off but the game will not let them die now; counted as failed.");
               verdict = ViolenceVerdict.Failed;
            }

            ModLog.Info($"[VIOLENCE] kill_npc CONFIRMED on {target.Name}: exposure={facts.Exposure}, chance={ViolencePolicy.SuccessChance(ViolentAct.Kill, facts)}%, roll={roll} -> {verdict}.");

            ActionOutcome outcome = verdict == ViolenceVerdict.Succeeded
               ? Succeeded(target, ctx)
               : Failed(target, facts.Exposure);

            ViolenceConsequenceService.Apply(target, ViolentAct.Kill, verdict, facts.Exposure);

            bool killed = verdict == ViolenceVerdict.Succeeded && target.IsDead;
            if (verdict == ViolenceVerdict.Succeeded && !killed)
               ModLog.Error($"[VIOLENCE] kill_npc on {target.Name}: the game applied the murder but they are not dead (state {target.HeroState}).");

            return new KillAttemptResult {Verdict = verdict, Killed = killed, Message = outcome?.Message};
         }
         catch (Exception ex)
         {
            ModLog.Error($"[VIOLENCE] confirmed kill failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static void DeclineKill(string name)
      {
         ModLog.Info($"[VIOLENCE] kill_npc declined by the player; {name} lives and nothing was recorded.");
         InformationManager.DisplayMessage(new InformationMessage($"You stay your hand. {name} lives."));
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
               Mapping.TraumaLedgerBehavior.Record(target, null, Logic.Trauma.TraumaKind.Robbed, Logic.Trauma.TraumaAuthor.Player);

               return new ActionOutcome($"You take {take} denars from {target.Name} by force (purse: {Hero.MainHero.Gold}).", false);
            }
            case ViolentAct.Assault:
               target.MakeWounded(Hero.MainHero);
               RememberOnTarget(target, "You beat me down with your own hands.");
               Mapping.TraumaLedgerBehavior.Record(target, null, Logic.Trauma.TraumaKind.Beaten, Logic.Trauma.TraumaAuthor.Player);
               Mapping.DeedRegardApplier.Apply(target, Logic.PlayerDeed.Beat); // the family; the victim's own hit is CollapseRegard's

               return new ActionOutcome($"You beat {target.Name} down. They will live, and they will remember.", false);
            default:
               // The victim keeps no memory of their own death; what the world makes of it is a later increment.
               Mapping.DeedRegardApplier.Apply(target, Logic.PlayerDeed.Murdered); // before death, while the family is still his
               KillCharacterAction.ApplyByMurder(target, Hero.MainHero);
               // The game's death log keeps only a victim of a house; a wanderer or a notable killed here needs this record.
               Mapping.MisdeedLedgerBehavior.Record(target.StringId, Logic.Trauma.MisdeedKind.Murdered);

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
