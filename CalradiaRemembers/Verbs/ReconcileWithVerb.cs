// Code written by Gabriel Mailhot, 07/11/2026.
// reconcile_with: the speaking character makes peace, in their own voice, with someone sitting in the same conversation.
// kurushimi00 (Nexus, 06/11/2026): Ira and Adram, enemies since a tourney 8 years ago, made peace at the player's table
// several times, "nevertheless, the encyclopedia entry still lists them as enemies from the tourney". Gabriel (07/11/2026):
// personal grudges, one per direction. So the verb is the SPEAKER's pardon only; the other's pardon is their own line. It is
// taught only while someone present is someone the speaker still holds a pardonable wrong against (PairPeacePolicy), and the
// target must be in the room: peace is made with a person, never with a name.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>reconcile_with: the speaker lays to rest what they held against someone present.</summary>
   public sealed class ReconcileWithVerb : IConversationVerb
   {
      public string Name => "reconcile_with";

      public bool IsEligible(VerbContext ctx) => Pardonable(ctx).Count > 0;

      public string TeachingText(VerbContext ctx)
      {
         List<Hero> others = Pardonable(ctx);
         if (others.Count == 0) return null;

         string names = string.Join(", ", others.Select(h => h.Name.ToString()));
         var sb = new StringBuilder();
         sb.AppendLine($"RECONCILE_WITH, MAKING YOUR PEACE WITH SOMEONE HERE ({names}):");
         sb.AppendLine("You hold something against them, as your regard for them says. Only if, in this scene, you truly lay it to rest");
         sb.AppendLine("and say so in your own words, emit the block below. It is YOUR pardon only: whether they forgive you is theirs");
         sb.AppendLine("to say. A courteous word, a truce for the evening or the player's asking is not peace; a slain kinsman is never");
         sb.AppendLine("laid to rest this way.");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: reconcile_with");
         sb.AppendLine("target: their name");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero speaker = ctx?.Npc;
            NpcProfile profile = ctx?.Profile;
            if (speaker == null || profile == null) return null;

            action.Parameters.TryGetValue("target", out string targetName);
            Hero target = PresentByName(ctx.ActionContext?.PresentHeroIds, targetName, speaker);
            if (target == null)
            {
               ModLog.Info($"[PEACE] reconcile_with refused: '{targetName}' is not in {speaker.Name}'s conversation.");

               return ActionOutcome.Refused($"(No one called {targetName} sits in this conversation; {speaker.Name} makes peace with no one.)");
            }

            var today = (int) CampaignTime.Now.ToDays;
            PairPeaceBehavior peace = PairPeaceBehavior.Instance;
            if (peace == null) return null;

            if (PairPeacePolicy.IsRepeat(peace.PeaceDay(speaker.StringId, target.StringId), today))
               return new ActionOutcome($"{speaker.Name} has already made peace with {target.Name}.", true);

            if (!PairRegardReader.HoldsPardonableAgainst(speaker, target))
            {
               ModLog.Info($"[PEACE] reconcile_with: {speaker.Name} holds nothing against {target.Name} that peace can lay to rest.");

               return ActionOutcome.Refused($"({speaker.Name} holds nothing against {target.Name} that words can lay to rest.)");
            }

            peace.Record(speaker.StringId, target.StringId, today);
            int grudges = ResolveGrudges(speaker, target, today);
            // The game keeps one number for the pair, so it is evened out only once both have made their peace.
            if (peace.PeaceDay(target.StringId, speaker.StringId).HasValue) RelationLedgerBehavior.EvenOut(speaker, target);

            string place = speaker.CurrentSettlement?.Name?.ToString();
            profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
               PairPeacePolicy.MemoryLine(new PeaceMemoryNames {OtherName = target.Name.ToString(), PlayerName = Hero.MainHero?.Name?.ToString(), Place = place})));

            ModLog.Info($"[PEACE] reconcile_with: {speaker.Name} made peace with {target.Name} (grudges laid to rest: {grudges}).");

            return new ActionOutcome($"{speaker.Name} makes peace with {target.Name}.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PEACE] reconcile_with failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Everyone present this speaker still holds a pardonable wrong against.</summary>
      private static List<Hero> Pardonable(VerbContext ctx)
      {
         if (ctx?.Npc == null || ctx.OthersPresentIds == null || ctx.OthersPresentIds.Count == 0) return new List<Hero>();

         return ctx.OthersPresentIds.Distinct()
                   .Select(id => Hero.FindFirst(h => h.StringId == id))
                   .Where(h => h != null && h != ctx.Npc && h != Hero.MainHero && HeroPresence.IsInTheWorld(h) && PairRegardReader.HoldsPardonableAgainst(ctx.Npc, h))
                   .ToList();
      }

      /// <summary>The present character the name points at (whole name, else one of its words), never the speaker or the player.</summary>
      internal static Hero PresentByName(IReadOnlyList<string> presentIds, string name, Hero speaker)
      {
         if (presentIds == null || string.IsNullOrWhiteSpace(name)) return null;

         List<Hero> present = presentIds.Distinct()
                                        .Select(id => Hero.FindFirst(h => h.StringId == id))
                                        .Where(h => h != null && h != speaker && h != Hero.MainHero)
                                        .ToList();
         string wanted = name.Trim();
         Hero exact = present.FirstOrDefault(h => string.Equals(h.Name?.ToString(), wanted, StringComparison.OrdinalIgnoreCase));
         if (exact != null) return exact;

         List<Hero> partial = present.Where(h => SwayGroundingPolicy.PlayerSpokeOf(wanted, h.Name?.ToString())).ToList();

         return partial.Count == 1 ? partial[0] : null;
      }

      // The speaker's own grudges against the target that this peace closes (PairPeacePolicy decides which).
      private static int ResolveGrudges(Hero speaker, Hero target, int today)
      {
         if (GrudgeBehavior.Instance == null) return 0;

         var closed = 0;
         foreach (Grudge g in GrudgeBehavior.Instance.GrudgesFor(speaker.StringId).Where(g => g != null && !g.IsResolved && g.TargetId == target.StringId))
         {
            var wrong = new PairWrongFacts {Kind = PairWrongKind.Grudge, Day = g.EventDay, Resolution = g.ResolutionKind, PeaceDay = today};
            if (!PairPeacePolicy.Cancels(wrong)) continue;

            g.IsResolved = true;
            g.ResolvedDay = today;
            closed++;
         }

         return closed;
      }

   }
}
