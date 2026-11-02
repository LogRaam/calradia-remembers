// Code written by Gabriel Mailhot, 01/11/2026.
// expose_secret: the player tells this character an affair the game really holds (Gabriel's "go", 01/11/2026). Taught only while
// such a secret exists, so a false accusation has no verb and stays words. The listener has to read the secret to recognise it,
// so the teaching says they do not know it yet. The name must have left the player's mouth, as with sway_opinion.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>expose_secret: the player tells a hidden affair to the character before them.</summary>
   public sealed class ExposeSecretVerb : IConversationVerb
   {
      public string Name => "expose_secret";

      public bool IsEligible(VerbContext ctx) => SecretExposure.HeldFor(ctx?.Npc).Count > 0;

      public string TeachingText(VerbContext ctx)
      {
         List<HeldSecret> held = SecretExposure.HeldFor(ctx?.Npc);
         if (held.Count == 0) return null;

         string player = Hero.MainHero.Name.ToString();
         var sb = new StringBuilder();
         sb.AppendLine($"EXPOSE SECRET, WHAT {player.ToUpperInvariant()} MIGHT TELL YOU:");
         foreach (HeldSecret secret in held) sb.AppendLine(secret.Words);
         sb.AppendLine($"You do not know this. Never think it, hint at it or raise it. It exists for you only once {player} plainly tells you, naming them.");
         sb.AppendLine("Only on the turn they tell you, emit:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: expose_secret");
         sb.AppendLine("target: <the name they told you of>");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero listener = ctx?.Npc;
            List<HeldSecret> held = SecretExposure.HeldFor(listener);
            string name = null;
            action?.Parameters?.TryGetValue("target", out name);
            HeldSecret secret = SecretExposure.Named(held, name);
            if (secret == null) return ActionOutcome.Refused("(You hold no such secret to tell.)");

            if (!SecretExposure.PlayerSpokeOf(secret, ctx.ActionContext?.PlayerWordsThisConversation))
            {
               ModLog.Info($"[EXTORT] expose_secret refused: the player never named {secret.Subject.Name} in this conversation.");

               return ActionOutcome.Refused($"(Nothing was said of {secret.Subject.Name} in this conversation; the secret stands.)");
            }

            SecretExposure.Expose(secret, listener);

            return new ActionOutcome($"{listener.Name} has it from your own mouth: {secret.Words} It will not stay in this room.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[EXTORT] expose_secret failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
