// Code written by Gabriel Mailhot, 18/10/2026.
// Extension Surface: a letter another mod wrote, delivered as CR's own. tashmetu (Regard and Standing, 18/10/2026): R&S
// has lords invite the player to their towns' tournaments, written as the lord's own letter, but "they still open in the
// plain popup", next to CR's courier window "with its parchment, its hand and the sender's portrait". He asked for "a
// small public way for another mod to deliver a letter from a hero, shown in your letter window": it arrives the way CR's
// letters do (the courier, the window, the letter book), with its body as given, and the sender remembers having written
// it. Gabriel (18/10/2026) took the letter to read first; answer buttons with a callback may come later.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Another mod's letters, delivered through CR's courier and letter window.</summary>
   public static class CrLetters
   {
      /// <summary>A letter body longer than this is refused (a letter, not a book; the save keeps every one).</summary>
      public const int MaxBodyLength = 4000;

      /// <summary>
      ///   Sends <paramref name="body" />, written in full by your mod, from <paramref name="sender" /> to the player. It
      ///   rides with a courier and opens in CR's letter window like CR's own letters; the player may reply, and the
      ///   sender answers in the chat's voice; it stays in the letter book, and the sender remembers having written it.
      ///   False when it is refused: no campaign, no body or one longer than <see cref="MaxBodyLength" />, the player as
      ///   sender, or a sender who cannot write now (a child, a captive, dead or out of the world).
      ///   <paramref name="sourceMod" /> names your mod in CR's log.
      /// </summary>
      public static bool Deliver(Hero sender, string body, string sourceMod)
      {
         string from = string.IsNullOrWhiteSpace(sourceMod) ? "another mod" : sourceMod.Trim();
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || sender == null || sender == Hero.MainHero || string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
         {
            ModLog.Info($"[LETTER] {from}: a letter from {sender?.Name} refused (no campaign, the player as sender, or no body / one over {MaxBodyLength} chars).");

            return false;
         }

         if (!ActionGate.Allows(sender, Logic.ActionKind.Letter, log: true)) return false;

         NpcProfile profile = store.Get(sender.StringId) ?? HeroProfileMapper.FromHero(sender);
         var today = (int) CampaignTime.Now.ToDays;
         profile.SentLetters.Add(new PendingLetter {
            SenderId = sender.StringId,
            SenderName = sender.Name?.ToString(),
            Reason = LetterReason.FromAnotherMod,
            Content = body.Trim(),
            SentOnDay = today,
            DispatchDay = today,
            DeliveryDay = today + LetterGenerationService.ComputeDeliveryDays(sender),
            IsDelivered = false
         });
         store.Set(profile);
         ModLog.Info($"[LETTER] {from}: a letter from {sender.Name} queued; the courier rides out day {today}.");

         return true;
      }
   }
}
