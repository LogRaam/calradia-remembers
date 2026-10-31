// Code written by Gabriel Mailhot, 18/10/2026.
// Extension Surface: a letter another mod wrote, delivered as CR's own. tashmetu (Regard and Standing, 18/10/2026): R&S
// has lords invite the player to their towns' tournaments, written as the lord's own letter, but "they still open in the
// plain popup", next to CR's courier window "with its parchment, its hand and the sender's portrait". He asked for "a
// small public way for another mod to deliver a letter from a hero, shown in your letter window": it arrives the way CR's
// letters do (the courier, the window, the letter book), with its body as given, and the sender remembers having written
// it. Gabriel (18/10/2026) took the letter to read first; answer buttons with a callback may come later.
// tashmetu (20/10/2026), with 2.7.17 out: R&S found its letter by sender, day and text, and read PlayerReply by reflection
// every hour, "a public way would keep it from breaking the day your internals change". Since 2.7.19 a letter carries an
// id, the player's reply and his setting it aside are raised as events, and a letter can be asked about by its id.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Letters;
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
      public static bool Deliver(Hero sender, string body, string sourceMod) => DeliverWithId(sender, body, sourceMod) != null;

      /// <summary>
      ///   As <see cref="Deliver" />, and returns the letter's id (null when it is refused), to ask about it later
      ///   (<see cref="GetStatus" />, <see cref="GetReply" />) and to know it again in <see cref="Replied" /> and
      ///   <see cref="SetAside" />. Since 2.7.19. Names no Calradia Remembers type.
      /// </summary>
      public static string DeliverWithId(Hero sender, string body, string sourceMod)
      {
         string from = string.IsNullOrWhiteSpace(sourceMod) ? "another mod" : sourceMod.Trim();
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || sender == null || sender == Hero.MainHero || string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
         {
            ModLog.Info($"[LETTER] {from}: a letter from {sender?.Name} refused (no campaign, the player as sender, or no body / one over {MaxBodyLength} chars).");

            return null;
         }

         if (!ActionGate.Allows(sender, Logic.ActionKind.Letter, log: true)) return null;

         NpcProfile profile = store.Get(sender.StringId) ?? HeroProfileMapper.FromHero(sender);
         var today = (int) CampaignTime.Now.ToDays;
         string id = Guid.NewGuid().ToString("N");
         profile.SentLetters.Add(new PendingLetter {
            Id = id,
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
         ModLog.Info($"[LETTER] {from}: a letter from {sender.Name} queued ({id}); the courier rides out day {today}.");

         return id;
      }

      /// <summary>
      ///   The player sealed a reply to one of your letters: its id, its sender, and the reply's text. Raised for every mod's
      ///   letters: keep the ids <see cref="DeliverWithId" /> gave you and ignore the others. Since 2.7.19. Names no
      ///   Calradia Remembers type.
      /// </summary>
      public static event Action<string, Hero, string> Replied;

      /// <summary>
      ///   The player read one of your letters and set it aside without replying (the window's acknowledge): its id and its
      ///   sender. Raised once per letter. Since 2.7.19. Names no Calradia Remembers type.
      /// </summary>
      public static event Action<string, Hero> SetAside;

      /// <summary>
      ///   What has become of the letter with this id: "on_the_road", "lost" (its courier was lost; CR sends it again),
      ///   "delivered", "set_aside", "answered", or "unknown" when no letter has this id. Since 2.7.19. Names no Calradia
      ///   Remembers type.
      /// </summary>
      public static string GetStatus(string letterId)
      {
         PendingLetter l = Find(letterId);

         return ModLetterStatusPolicy.Of(l == null ? null : new ModLetterFacts
         {
            IsDelivered = l.IsDelivered, IsLost = l.IsLost, HasReply = !string.IsNullOrWhiteSpace(l.PlayerReply), SetAside = l.SetAside, InBox = l.InBox
         });
      }

      /// <summary>The text of the player's reply to the letter with this id, or null when there is none. Since 2.7.19. Names no Calradia Remembers type.</summary>
      public static string GetReply(string letterId)
      {
         string reply = Find(letterId)?.PlayerReply;

         return string.IsNullOrWhiteSpace(reply) ? null : reply;
      }

      private static PendingLetter Find(string letterId)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || string.IsNullOrWhiteSpace(letterId)) return null;

         return store.ListIds().Select(store.Get).Where(p => p?.SentLetters != null)
                     .SelectMany(p => p.SentLetters).FirstOrDefault(l => l.Id == letterId);
      }

      // The letter window's notices (Letters.ModLetterNotices), connected by the submodule at load.
      internal static void OnReplied(PendingLetter letter) => Raise(Replied, d => d(letter.Id, SenderOf(letter), letter.PlayerReply), letter, "Replied");

      internal static void OnSetAside(PendingLetter letter) => Raise(SetAside, d => d(letter.Id, SenderOf(letter)), letter, "SetAside");

      private static Hero SenderOf(PendingLetter letter) => Hero.FindFirst(h => h.StringId == letter.SenderId);

      // Each subscriber under its own guard: one that throws must not keep the others from hearing it, nor break the window.
      private static void Raise<T>(T handlers, Action<T> call, PendingLetter letter, string name) where T : Delegate
      {
         if (handlers == null || letter?.Id == null) return;
         foreach (Delegate d in handlers.GetInvocationList())
            try { call((T) d); }
            catch (Exception ex) { ModLog.Error($"[LETTER] a {name} subscriber ({d.Method.DeclaringType?.FullName}) threw: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
