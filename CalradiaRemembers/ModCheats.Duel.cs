// Code written by Gabriel Mailhot, 14/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Duels;
using CalradiaRemembers.Logic.Duels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.duel_here [name]
      ///   WILDERNESS SPIKE. Stand on the open map (not inside a settlement) and run this to open a throwaway
      ///   field-combat scene against the named lord (or the conversation NPC, or any nearby lord), spawn the
      ///   two fighters, hold it for about half a minute, and return to the map. It measures VRAM before and
      ///   after in the log under [DUELSPIKE], to prove whether a field scene opens and closes cleanly with no
      ///   render-target leak. This is plumbing only: the real non-lethal duel (witnesses, stakes, the
      ///   challenge_duel action) is the rest of Inc 1, not this.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_here", "cr")]
      public static string ForceDuelHere(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "You are inside a settlement. The town path is already covered by the ambush code; " +
                   "leave to the open map to test the WILDERNESS scene.";

         Hero opponent = ResolveHero(args)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         return DuelSceneSpike.OpenFieldDuel(opponent);
      }

      /// <summary>
      ///   cr.duel [name]
      ///   Stages the REAL non-lethal duel (not the spike): stand on the open map and cross blades with the
      ///   named lord (or the conversation NPC, or any nearby lord). The two spawn a clear distance apart and
      ///   close in; the loser is knocked UNCONSCIOUS, never killed; the winner is reported when it ends. This
      ///   is Inc 1's mission only, the stakes/relation/world-event wiring rides the reported outcome.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel", "cr")]
      public static string ForceDuel(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "You are inside a settlement. Leave to the open map to stage the wilderness duel (town / gates come later).";

         Hero opponent = ResolveHero(args)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         if (opponent == null) return "No living lord found to duel.";

         return DuelMissionLauncher.StartWildernessDuel(opponent, playerWon =>
         {
            string line = playerWon
               ? $"You bested {opponent.Name} in single combat."
               : $"{opponent.Name} bested you in single combat.";
            ModLog.Info($"[DUEL] outcome: {line}");
            InformationManager.DisplayMessage(new InformationMessage(line));
         });
      }

      /// <summary>
      ///   cr.duel_courtship [rival] [lady]
      ///   Stages a COURTSHIP-RIVALRY duel and applies its full consequence: on top of the generic renown/stance,
      ///   the courted lady's attraction toward you swings (win -> admiration, lose -> doubt), and a world-event
      ///   records it so she learns of an outcome she did not witness. Resolves the rival from the first name (or
      ///   a nearby lord) and the lady from the second (or the first female hero with a profile). Watch the log
      ///   for "attraction toward you: X -> Y".
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_courtship", "cr")]
      public static string ForceCourtshipDuel(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "Leave to the open map to stage the wilderness courtship duel (town / gates come later).";

         Hero rival = (args != null && args.Count >= 1 ? ResolveHero(new List<string> {args[0]}) : null)
                   ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner && !h.IsFemale);

         if (rival == null) return "No living lord found to stand as the rival.";

         Hero lady = (args != null && args.Count >= 2 ? ResolveHero(new List<string> {args[1]}) : null)
                  ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != rival && h.IsFemale && h.IsAlive
                                                          && NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId)?.Romantic != null);

         if (lady == null) return "No female hero with a romantic profile found to court. Chat with one first so a profile exists.";

         ModLog.Info($"[DUEL] courtship duel: rival={rival.Name}, courted={lady.Name}.");

         return DuelMissionLauncher.StartWildernessDuel(rival, playerWon =>
         {
            DuelOutcomeApplier.Apply(DuelReason.CourtshipRivalry, playerWon, rival, lady, witnessCount: 0);
            string line = playerWon
               ? $"You bested {rival.Name} for {lady.Name}'s regard."
               : $"{rival.Name} bested you before {lady.Name}'s name.";
            InformationManager.DisplayMessage(new InformationMessage(line));
         });
      }

      /// <summary>
      ///   cr.duel_challenge [rival] [lady]
      ///   The interactive courtship challenge: a rival presents his demand and you ANSWER it. Accept to fight
      ///   the duel (its outcome then applies as cr.duel_courtship does); DECLINE to walk away, which costs you
      ///   renown before the challenge AND the lady's regard (a man who will not fight for her is judged worse
      ///   than one who fought and lost). This is the accept/refuse core of Inc B2; the LLM chat intro (the rival
      ///   announcing it in his own voice) and the autonomous approach are the remaining pieces.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_challenge", "cr")]
      public static string ForceDuelChallenge(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "Leave to the open map to answer the challenge in the wilderness (town / gates come later).";

         Hero rival = (args != null && args.Count >= 1 ? ResolveHero(new List<string> {args[0]}) : null)
                   ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner && !h.IsFemale);

         if (rival == null) return "No living lord found to stand as the rival.";

         Hero lady = (args != null && args.Count >= 2 ? ResolveHero(new List<string> {args[1]}) : null)
                  ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != rival && h.IsFemale && h.IsAlive
                                                          && NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId)?.Romantic != null);

         if (lady == null) return "No female hero with a romantic profile found to court. Chat with one first so a profile exists.";

         InformationManager.ShowInquiry(new InquiryData(
            "A Challenge",
            $"{rival.Name} bars your way and demands satisfaction: he means to settle by steel which of you is worthier of {lady.Name}. Do you take up the challenge?",
            true, true,
            "Draw your sword", "Decline",
            () => DuelMissionLauncher.StartWildernessDuel(rival, playerWon =>
            {
               DuelOutcomeApplier.Apply(DuelReason.CourtshipRivalry, playerWon, rival, lady, witnessCount: 0);
               InformationManager.DisplayMessage(new InformationMessage(playerWon
                  ? $"You bested {rival.Name} for {lady.Name}'s regard."
                  : $"{rival.Name} bested you before {lady.Name}'s name."));
            }),
            () =>
            {
               DuelOutcomeApplier.Apply(DuelReason.CourtshipRivalry, DuelResolution.PlayerForfeited, rival, lady, witnessCount: 0);
               InformationManager.DisplayMessage(new InformationMessage(
                  $"You would not draw on {rival.Name}. The refusal will be spoken of, and {lady.Name} will hear of it."));
            }), true);

         return $"{rival.Name} challenges you over {lady.Name}. Answer the inquiry.";
      }
   }
}
