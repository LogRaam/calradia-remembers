// Code written by Gabriel Mailhot, 14/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Duels;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Duels;
using NpcMemoryService.Core.Models;
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

      /// <summary>
      ///   cr.duel_arm [name] [npc|player]
      ///   Arms <see cref="DuelCoordinator" /> directly, the same way the chat handler does, without going
      ///   through the LLM: stages a duel against the named lord (or the conversation NPC, or any nearby lord)
      ///   once you close whatever window is open and no mission is running. The VENUE is resolved from where
      ///   you stand, exactly as the real path resolves it: inside a town it stages in the town centre, on the
      ///   open map it stages a wilderness field, and at sea it refuses. The second argument
      ///   picks who issued the challenge: "npc" (the default) means the NPC called YOU out, so once the map is
      ///   live you are shown the accept/refuse inquiry; "player" means you called them out and they have
      ///   already accepted (the chat's own acceptance policy already ran), so the duel launches with no prompt.
      ///   Close any window and watch: this is how the coordinator's deferred launch and the inquiry path are
      ///   tested without a live LLM turn.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_take_field", "cr")]
      public static string ForceDuelTakeField(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         ChatViewModel chat = Chat.ChatManager.ActiveViewModel;
         if (chat == null) return "Open a conversation with the character first (a companion, a lord, in a town, castle or village), then run this.";

         return chat.DebugChallengeTakenUp();
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("duel_arm", "cr")]
      public static string ForceDuelArm(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var nameTokens = args != null ? new List<string>(args) : new List<string>();
         bool npcIssuedChallenge = true;

         if (nameTokens.Count > 0)
         {
            string last = nameTokens[nameTokens.Count - 1];
            if (string.Equals(last, "player", StringComparison.OrdinalIgnoreCase))
            {
               npcIssuedChallenge = false;
               nameTokens.RemoveAt(nameTokens.Count - 1);
            }
            else if (string.Equals(last, "npc", StringComparison.OrdinalIgnoreCase))
               nameTokens.RemoveAt(nameTokens.Count - 1);
         }

         Hero opponent = ResolveHero(nameTokens)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         if (opponent == null) return "No living lord found to duel.";

         // Resolved BY THE REAL RESOLVER, which is the whole point of the command: it must exercise the same
         // venue choice the chat does, or a bug in that choice can never show up here. It used to mimic it
         // with its own IsTown test instead, and so could not have found the castle case (offered by the
         // resolver since 10/09/2026, refused by the staging until 21/09/2026) however often it was run. This
         // is the "a guard that measures its own construction" failure, in a diagnostic command.
         DuelVenue venue = DuelEligibilityResolver.VenueForPlayerNow();

         if (!DuelStagingSupport.CanStage(venue))
            return "No duel can be staged where you stand (at sea there is no ground to fight on; come ashore).";

         DuelCoordinator.Arm(opponent, DuelReason.HonourAffront, venue, npcIssuedChallenge, 0, null);

         return $"Armed a {venue} duel against {opponent.Name} " +
                $"({(npcIssuedChallenge ? "NPC issued: answer the inquiry" : "player issued: launches directly")}). " +
                "Close any window and watch.";
      }

      /// <summary>
      ///   cr.duel_town [name]
      ///   Stages the SETTLEMENT duel directly, bypassing <see cref="DuelCoordinator" /> entirely: stand inside a
      ///   town or a castle (on its menu, not in a scene) and this opens the centre scene (a town square or a
      ///   castle courtyard), injects the named lord among the crowd, and starts the non-lethal duel. Use it to isolate the SCENE staging (does he spawn, does
      ///   the fight start, does the knockout return you to the town) from the deferral and the inquiry, which
      ///   cr.duel_arm covers. Watch the log for the "[DUEL] injected" and "[DUEL] town duel started" lines.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_town", "cr")]
      public static string ForceTownDuel(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         // No kind test of its own: TownDuelBehavior.Arm asks the real questions (inside, and a "center"
         // location to open) and says which one failed. A castle courtyard IS such a location, which this
         // command denied for two months on a claim the game's own data contradicts.
         if (Settlement.CurrentSettlement == null)
            return "You must be inside a town or a castle (stand on its menu, not in a scene).";

         Hero opponent = ResolveHero(args)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         if (opponent == null) return "No living lord found to duel.";

         return TownDuelBehavior.Arm(opponent, playerWon =>
         {
            DuelOutcomeApplier.Apply(DuelReason.HonourAffront, playerWon, opponent, null, witnessCount: 0);
            InformationManager.DisplayMessage(new InformationMessage(playerWon
               ? $"You bested {opponent.Name} in the streets."
               : $"{opponent.Name} bested you in the streets."));
         });
      }

      /// <summary>
      ///   cr.escape_test [n]
      ///   Captivity-escape spike (W1): stand on the OPEN map and stage the 1-vs-N escape scene, you UNARMED in
      ///   civilian clothes against n aggressors (default 2, clamp 1..3) each with a Knobbed Club. Knock them all
      ///   down OR run to the edge of the scene to "escape"; get knocked out and you are "recaptured". Everyone
      ///   falls unconscious, never dies. This is plumbing only: the real release / lost gear+horse / nemesis
      ///   memory ride the reported outcome (shown here as a message), not yet wired to captivity.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("escape_test", "cr")]
      public static string ForceEscapeTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "You are inside a settlement. Leave to the open map to stage the wilderness escape scene.";

         var foes = 2;
         if (args != null && args.Count > 0 && int.TryParse(args[0], out int parsed)) foes = parsed;

         // All-null list = the requested count of bandit fillers (no real captor in the standalone test path).
         var roster = new CharacterObject[foes < 1 ? 1 : foes > 3 ? 3 : foes];

         return CaptiveEscapeMissionLauncher.StartEscape(roster, outcome =>
         {
            string line = outcome switch {
               EscapeOutcome.BeatThemAll => "You beat them all down and fought free. (Real release + lost gear/horse comes next.)",
               EscapeOutcome.Fled => "You broke away and reached the edge. (Real release + lost gear/horse comes next.)",
               _ => "You were overpowered and go back to the map still a prisoner. (Nemesis memory of the attempt comes next.)"
            };
            ModLog.Info($"[ESCAPE] outcome: {outcome}");
            InformationManager.DisplayMessage(new InformationMessage(line));
         });
      }

      /// <summary>
      ///   cr.postduel [win|loss] [name]
      ///   Stages the POST-DUEL conversation directly, without fighting a duel first, so the resume and the
      ///   victor/loser register can be inspected in game. "win" (the default) opens it as though the PLAYER won
      ///   (the NPC concedes and may grant a favour); "loss" opens it as though the NPC won (they press what they
      ///   expect). A short synthetic pre-duel exchange is seeded so the replayed bubbles and the LLM continuity
      ///   are visible. Resolves the named lord, or the conversation NPC, or any nearby lord. Stand on the open
      ///   map to see it stage a scene (the real flow triggers there right after a fought duel).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("postduel", "cr")]
      public static string ForcePostDuel(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         var nameTokens = args != null ? new List<string>(args) : new List<string>();
         var playerWon = true;

         if (nameTokens.Count > 0)
         {
            string first = nameTokens[0];
            if (string.Equals(first, "loss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(first, "lose", StringComparison.OrdinalIgnoreCase)
                || string.Equals(first, "lost", StringComparison.OrdinalIgnoreCase))
            {
               playerWon = false;
               nameTokens.RemoveAt(0);
            }
            else if (string.Equals(first, "win", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(first, "won", StringComparison.OrdinalIgnoreCase))
               nameTokens.RemoveAt(0);
         }

         Hero opponent = ResolveHero(nameTokens)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         if (opponent == null) return "No living lord found for a post-duel conversation.";

         // A short synthetic pre-duel exchange so the replayed bubbles and the seeded LLM continuity are visible.
         var prior = new ConversationTranscript();
         prior.Record(new TranscriptLine {IsPlayer = true, SpeakerName = "You", SpeakerHeroId = Hero.MainHero?.StringId, Text = "You have gone too far. Draw your blade and answer for it."});
         prior.Record(new TranscriptLine {IsPlayer = false, SpeakerName = opponent.Name?.ToString(), SpeakerHeroId = opponent.StringId, Text = "Bold words. We shall see whose steel has the last of it."});

         NpcProfile profile = behavior.Store?.Get(opponent.StringId);
         ChatSceneStager.StagePostDuel(opponent, profile, playerWon, DuelReason.HonourAffront, null, prior);

         return $"Staging the post-duel conversation with {opponent.Name} as a {(playerWon ? "player WIN (they concede)" : "player LOSS (they prevail)")}. " +
                "Close any window and watch it open over the scene.";
      }
   }
}
