// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design [[project_npc_recent_matter]] phase 3b): a lord NEVER deduces who a secret lover is.
// Whispers of the bond's EXISTENCE circulate (SecretLoverGossipBehavior) and curious lords probe the player, but the
// IDENTITY is a fact no one can piece together, learned ONLY when the PLAYER chooses to confess it. This verb is that
// confession: when the player tells an NPC outright, in words, that they are the hidden lover behind such a whisper,
// the NPC is taught to emit [ACTION type: admit_secret_lover], and the bridge records that this NPC now privately
// knows (the knowledge ledger on SecretLoverGossipBehavior), seeding later propagation among his friends and an
// enemy lord's use of it against the player. Taught ONLY when the player actually has a secret bond this NPC does not
// already embody, so the LLM never hears of the verb when there is nothing to confess.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>admit_secret_lover: the player confesses to this NPC that they are the hidden lover behind a whispered scandal; the NPC now privately knows the identity.</summary>
   public sealed class AdmitSecretLoverVerb : IConversationVerb
   {
      public string Name => "admit_secret_lover";

      public bool IsEligible(VerbContext ctx)
         => ctx?.Npc != null
            && SecretLoverGossipBehavior.Instance != null
            && SecretLoverAdmissionPolicy.CanAdmitTo(SecretLoverGossipBehavior.Instance.PlayerSecretLoverSubjectIds(), ctx.Npc.StringId);

      public string TeachingText(VerbContext ctx)
      {
         if (!IsEligible(ctx)) return null;

         var sb = new StringBuilder();
         sb.AppendLine("ADMIT_SECRET_LOVER, THE PLAYER CONFESSING A HIDDEN BOND TO YOU:");
         sb.AppendLine("Word travels of a secret love kept hidden, its identity known to no one. If, in this");
         sb.AppendLine("conversation, the player tells YOU plainly that THEY are that hidden lover (or names whose");
         sb.AppendLine("secret lover they are), and you take it as a genuine confession, emit:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: admit_secret_lover");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine("You will remember this secret. Only emit it once the player has truly OWNED the bond in words:");
         sb.AppendLine("never on your own suspicion or guess, never if they deflect, joke, or deny. No one can deduce");
         sb.AppendLine("this; you learn it only because they told you.");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero npcHero = ctx?.Npc;
            SecretLoverGossipBehavior gossip = SecretLoverGossipBehavior.Instance;
            if (npcHero == null || gossip == null) return null;

            var subjects = SecretLoverAdmissionPolicy.SubjectsRevealedTo(gossip.PlayerSecretLoverSubjectIds(), npcHero.StringId);
            if (subjects.Count == 0)
               return ActionOutcome.Refused($"({npcHero.Name} senses no secret in you to confess.)");

            foreach (string subjectId in subjects) gossip.RecordKnows(npcHero, subjectId);

            RecordConfessionMemory(ctx, npcHero, subjects);

            ModLog.Info($"[ACTION] admit_secret_lover: {npcHero.Name} now knows the player's hidden bond ({subjects.Count} subject(s)).");

            return new ActionOutcome($"({npcHero.Name} now knows your secret. What becomes of it rests with them.)", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] admit_secret_lover failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      // A first-hand memory on the NPC so the confession is durable and visible (cr.memory), and so later systems can
      // see this lord holds the player's secret. Names the subject when it is a single, resolvable hero.
      private static void RecordConfessionMemory(VerbContext ctx, Hero npcHero, System.Collections.Generic.List<string> subjects)
      {
         if (ctx?.Profile == null) return;

         var today = (int) CampaignTime.Now.ToDays;
         string who = DescribeSubjects(subjects);
         ctx.Profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
            $"The player confided to me, in secret, that they are {who} secret lover. I alone know."));
      }

      private static string DescribeSubjects(System.Collections.Generic.List<string> subjects)
      {
         if (subjects.Count == 1)
         {
            Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.StringId == subjects[0]);
            if (subject != null)
               return HeroLabel.Describe(new HeroLabel.HeroNaming {Name = subject.Name?.ToString(), ClanName = subject.Clan?.Name?.ToString(), CultureName = subject.Culture?.Name?.ToString()}) + "'s";
         }

         return "another's";
      }
   }
}
