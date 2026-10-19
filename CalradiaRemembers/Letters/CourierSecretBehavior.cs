// Code written by Gabriel Mailhot, 26/09/2026.
// Courier Inc 4a: an intercepted letter becomes a secret that travels in the world (raphareish, Nexus, 24/09/2026;
// Gabriel's ruling the same day). Not the old instant leak to an antagonist picked by a roll, but a thing a party
// carries:
//
//   - Bandits who take a courier CARRY its letter. Another band that beats them carries it on; a band that goes home
//     brings it to its hideout, where it waits.
//   - Whoever else beats the carrier sends it up to the lord who answers for that troop: the lord at its head, else its
//     owner (a caravan), else the owner of its home settlement (a Pravend patrol, Pravend's owner).
//   - A troop at war with the courier's clan that takes it directly hands it to its own lord at once.
//   - The player's side beating the carrier, or clearing the hideout that holds it, recovers the letter unread.
//   - A secret is only worth something for CourierSecretPolicy.SecretShelfLifeDays.
//
// Every decision is CourierSecretPolicy's (pure, tested); this is the engine half: reading parties, following the
// carrier, and filing what the lord who ends up with it now knows. What that lord then DOES about it, in proportion to
// what the letter says (increment 4b), is SecretReactionPolicy's, on one word the model gives from the reader's side.
// Additive save key.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>An intercepted letter, and where it is now.</summary>
   public sealed class CarriedSecret
   {
      /// <summary>The bandit party carrying it, or null once it rests in a hideout.</summary>
      public string CarrierPartyId { get; set; }

      /// <summary>The hideout it rests in, or null while a party carries it.</summary>
      public string HideoutId { get; set; }

      public string SenderId { get; set; }
      public string SenderName { get; set; }
      public string RecipientId { get; set; }
      public string RecipientName { get; set; }

      /// <summary>What the letter said.</summary>
      public string Content { get; set; }

      /// <summary>True when the player wrote it.</summary>
      public bool FromPlayer { get; set; }

      /// <summary>The day it was taken from its courier.</summary>
      public int CapturedDay { get; set; }
   }

   /// <summary>Follows intercepted letters from their captors to whoever ends up reading them.</summary>
   public sealed class CourierSecretBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_CourierSecrets_v1bytes";

      /// <summary>How close the player's party must stand to an emptied hideout to have cleared it (map units).</summary>
      private const float HideoutClearedByPlayerDistance = 3f;

      public static CourierSecretBehavior Instance { get; private set; }

      private List<CarriedSecret> _secrets = new();

      public CourierSecretBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>The secrets in the world now, for diagnostics and cr.testall.</summary>
      internal IReadOnlyList<CarriedSecret> Secrets => _secrets;

      public override void RegisterEvents()
      {
         CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
         CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
         CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnHideoutDeactivated);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      /// <summary>
      ///   A courier was intercepted and its letter is worth hiding (CourierSecretPolicy.BecomesSecret, checked by the
      ///   caller). Bandits carry it off; an enemy troop hands it straight to its own lord.
      /// </summary>
      public void Capture(CarriedSecret secret, PartyBase captor, CourierLoss loss)
      {
         try
         {
            if (secret == null || captor == null) return;

            secret.CapturedDay = Today;

            if (loss == CourierLoss.InterceptedByBandits && captor.MobileParty != null)
            {
               secret.CarrierPartyId = captor.MobileParty.StringId;
               _secrets.Add(secret);
               ModLog.Info($"[SECRET] {captor.Name} carry off {secret.SenderName}'s letter to {secret.RecipientName}.");

               return;
            }

            HandOff(secret, captor);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] capture failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Test seam (cr.testall): the band <paramref name="carrierPartyId" /> is beaten by <paramref name="victor" />
      ///   without being destroyed, and its secrets pass on exactly as a real defeat would, minus the model's judgement
      ///   and the rumour, so a self-test leaves no trace in the world.
      /// </summary>
      internal void DebugDefeatCarrier(string carrierPartyId, PartyBase victor)
      {
         foreach (CarriedSecret secret in _secrets.Where(s => s.CarrierPartyId == carrierPartyId).ToList())
         {
            _secrets.Remove(secret);
            HandOff(secret, victor, live: false);
         }
      }

      /// <summary>Test seam (cr.testall): drops every secret whose content carries <paramref name="tag" />.</summary>
      internal void DebugForgetTagged(string tag) => _secrets.RemoveAll(s => s.Content != null && s.Content.Contains(tag));

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_secrets, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SECRET] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SECRET] save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves */
         }

         if (loaded is not {Length: > 0}) return;

         try
         {
            _secrets = JsonConvert.DeserializeObject<List<CarriedSecret>>(Encoding.UTF8.GetString(loaded)) ?? new List<CarriedSecret>();
         }
         catch (Exception ex)
         {
            _secrets = new List<CarriedSecret>();
            ModLog.Error($"[SECRET] load failed, secrets dropped for this load. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>The carrier was beaten, or vanished: its secrets pass to whoever answers for the victor.</summary>
      private void OnPartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         try
         {
            string id = destroyed?.StringId;
            if (string.IsNullOrEmpty(id)) return;

            List<CarriedSecret> carried = _secrets.Where(s => s.CarrierPartyId == id).ToList();
            foreach (CarriedSecret secret in carried)
            {
               _secrets.Remove(secret);
               HandOff(secret, destroyer);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] party-destroyed handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>A band carrying a secret goes home: the letter rests in its hideout.</summary>
      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         try
         {
            if (party == null || settlement is not {IsHideout: true}) return;

            foreach (CarriedSecret secret in _secrets.Where(s => s.CarrierPartyId == party.StringId))
            {
               secret.CarrierPartyId = null;
               secret.HideoutId = settlement.StringId;
               ModLog.Info($"[SECRET] {secret.SenderName}'s letter to {secret.RecipientName} now rests in {settlement.Name}.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] settlement-entered handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   A hideout emptied. The engine says so however it emptied, so only a player standing at it has cleared it and
      ///   takes back what it held; otherwise the letters wait there for the next band.
      /// </summary>
      private void OnHideoutDeactivated(Settlement hideout)
      {
         try
         {
            if (hideout == null) return;

            MobileParty main = MobileParty.MainParty;
            bool playerClearedIt = main != null
                                   && (main.CurrentSettlement == hideout
                                       || main.GetPosition2D.Distance(hideout.GetPosition2D) <= HideoutClearedByPlayerDistance);
            if (!playerClearedIt) return;

            foreach (CarriedSecret secret in _secrets.Where(s => s.HideoutId == hideout.StringId).ToList())
            {
               _secrets.Remove(secret);
               Recover(secret);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] hideout handling failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Old secrets stop mattering; a carrier gone without a trace takes its letter with it.</summary>
      private void OnDailyTick()
      {
         try
         {
            int today = Today;
            int dropped = _secrets.RemoveAll(s =>
               CourierSecretPolicy.IsStale(s.CapturedDay, today)
               || (s.HideoutId == null && (string.IsNullOrEmpty(s.CarrierPartyId) || MobileParty.All.All(p => p.StringId != s.CarrierPartyId))));

            if (dropped > 0) ModLog.Info($"[SECRET] {dropped} secret(s) no longer matter or were lost with their carriers.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] daily tick failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Passes a secret to whoever answers for <paramref name="party" />, as CourierSecretPolicy decides.
      ///   <paramref name="live" /> false (cr.testall) skips the model's judgement and the rumour.
      /// </summary>
      private void HandOff(CarriedSecret secret, PartyBase party, bool live = true)
      {
         SecretHandoff handoff = CourierSecretPolicy.WhoGetsTheSecret(DefeaterFacts(party, secret));

         switch (handoff.Kind)
         {
            case SecretHandoffKind.CarriedOn:
               secret.CarrierPartyId = party?.MobileParty?.StringId;
               secret.HideoutId = null;
               if (secret.CarrierPartyId != null) _secrets.Add(secret);
               ModLog.Info($"[SECRET] {party?.Name} take {secret.SenderName}'s letter from the band that held it.");

               break;
            case SecretHandoffKind.RecoveredByPlayer:
               Recover(secret);

               break;
            case SecretHandoffKind.Lord:
               Hero reader = Hero.FindFirst(h => h != null && h.StringId == handoff.HeroId);
               if (reader is {IsAlive: true}) Learn(secret, reader, live);

               break;
            default:
               ModLog.Info($"[SECRET] {secret.SenderName}'s letter to {secret.RecipientName} reaches no one ({handoff.Kind}).");

               break;
         }
      }

      /// <summary>What CourierSecretPolicy needs to know about the troop that took a secret.</summary>
      private static SecretDefeaterFacts DefeaterFacts(PartyBase party, CarriedSecret secret)
      {
         if (party == null) return null;

         MobileParty mobile = party.MobileParty;
         Clan playerClan = Clan.PlayerClan;
         bool playerSide = party == PartyBase.MainParty
                           || (playerClan != null && (mobile?.ActualClan == playerClan || party.Owner?.Clan == playerClan));

         Settlement home = mobile?.HomeSettlement ?? party.Settlement;

         return new SecretDefeaterFacts {
            IsPlayerSide = playerSide,
            IsBandit = mobile?.IsBandit == true,
            LeaderHeroId = party.LeaderHero?.StringId,
            OwnerHeroId = party.Owner?.StringId,
            SettlementOwnerHeroId = home?.OwnerClan?.Leader?.StringId,
            IntendedRecipientId = secret.RecipientId,
            SenderId = secret.SenderId
         };
      }

      /// <summary>The player's side took the letter back, unread by anyone.</summary>
      private static void Recover(CarriedSecret secret)
      {
         string text = secret.FromPlayer
            ? $"Among the bandits' loot you find your own letter to {secret.RecipientName}. Its words are safe."
            : $"Among the bandits' loot you find the letter {secret.SenderName} once sent you.";
         MBInformationManager.AddQuickInformation(new TextObject(text));
         ModLog.Info($"[SECRET] recovered by the player: {secret.SenderName}'s letter to {secret.RecipientName}.");
      }

      /// <summary>
      ///   A lord now holds the letter: they remember whose it was, to whom, and what it said, and word of it gets about.
      ///   Then they act on it, in proportion to what it said (JudgeAndReactAsync).
      /// </summary>
      private static void Learn(CarriedSecret secret, Hero reader, bool live = true)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null) return;

            NpcProfile profile = behavior.Store.Get(reader.StringId);
            if (profile == null)
            {
               profile = HeroProfileMapper.FromHero(reader);
               behavior.Store.Set(profile);
            }

            int today = Today;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
               CourierSecretPolicy.ReaderMemory(secret.SenderName, secret.RecipientName, secret.Content)));
            behavior.Store.Set(profile);

            if (!live) return;

            WorldEventStore.Instance?.Record(new WorldEvent {
               Type = WorldEventType.Other,
               Day = today,
               InvolvesPlayer = true,
               Magnitude = 25,
               Status = "letter_leaked",
               Summary = $"A private letter {secret.SenderName} sent to {secret.RecipientName} was taken on the road and came into {reader.Name}'s hands.",
               ActorIds = new List<string> {reader.StringId},
               FactionIds = new List<string>()
            });

            ModLog.Info($"[SECRET] {reader.Name} now knows what {secret.SenderName} wrote to {secret.RecipientName}.");

            _ = JudgeAndReactAsync(secret, reader.StringId);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] learning failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Courier Inc 4b (Gabriel, 26/09/2026: an action "in proportion to the secret"). The model reads the letter as
      ///   the reader and names in one word how it touches them; SecretReactionPolicy turns that into a grudge against
      ///   its author, the player's renown, and, for a plot the player wrote, a letter confronting them. A failed or
      ///   garbled call reads as Nothing: the player is never punished for a reply nobody could read.
      /// </summary>
      private static async Task JudgeAndReactAsync(CarriedSecret secret, string readerId)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            ILlmClient client = behavior?.LlmServices?.HousekeepingClient;
            Hero reader = Hero.FindFirst(h => h != null && h.StringId == readerId);
            if (client == null || reader == null) return;

            string liege = reader.MapFaction?.Leader != null && reader.MapFaction.Leader != reader ? reader.MapFaction.Leader.Name.ToString() : null;
            string prompt = SeverityPrompt(reader.Name.ToString(), liege, secret);

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"secret judged by {reader.Name}");

            string answer = null;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            {
               var request = new LlmRequest {
                  SystemPrompt = prompt,
                  Messages = new[] {new LlmMessage(MessageRole.User, "One word.")},
                  Parameters = new LlmParameters {MaxTokens = 3000, Creativity = 0.1f} // a ceiling: reasoning models think first
               };
               LlmResponse resp = await Task.Run(() => client.CompleteAsync(request, cts.Token)).ConfigureAwait(false);
               if (resp is {IsSuccess: true}) answer = resp.Content;
            }

            await MainThreadDispatcher.SwitchToMainThread();

            SecretSeverity severity = SecretReactionPolicy.Parse(answer);
            ModLog.Info($"[SECRET] {reader.Name} reads {secret.SenderName}'s letter as {severity} (\"{answer?.Trim()}\").");
            React(secret, reader, severity);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] judging the letter failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The one-word question, asked from the reader's side.</summary>
      private static string SeverityPrompt(string readerName, string liegeName, CarriedSecret secret)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"You are {readerName}. A letter {secret.SenderName} wrote to {secret.RecipientName}, taken from a courier on the road, has come into your hands. It reads:");
         sb.AppendLine($"\"{secret.Content}\"");
         sb.AppendLine();
         sb.AppendLine($"How does this letter touch you, your house{(liegeName == null ? "" : $", or your liege {liegeName}")}? Answer with exactly one word:");
         sb.AppendLine("NOTHING (it does not concern you)");
         sb.AppendLine("EMBARRASSING (good gossip, but no harm to you)");
         sb.AppendLine("AGAINST (it works against your interests or your house)");
         sb.AppendLine("TREASON (it plots against you or your liege)");

         return sb.ToString();
      }

      /// <summary>Applies SecretReactionPolicy's reaction on the game thread. Never throws.</summary>
      internal static void React(CarriedSecret secret, Hero reader, SecretSeverity severity)
      {
         try
         {
            Hero author = secret.FromPlayer ? Hero.MainHero : Hero.FindFirst(h => h != null && h.StringId == secret.SenderId);
            if (reader == null || author == null || author == reader) return;

            int today = Today;
            bool alreadyHeld = GrudgeBehavior.Instance?.LiveGrudgesFor(reader, today)?.Any(g => g.TargetId == author.StringId) ?? false;
            SecretReaction reaction = SecretReactionPolicy.For(severity, secret.FromPlayer, alreadyHeld);

            if (reaction.GrudgeMagnitude > 0)
               GrudgeBehavior.Instance?.TryPlant(reader, author, GrudgeSource.LeakedLetter,
                  severity == SecretSeverity.Treason
                     ? $"Read a letter {author.Name} wrote to {secret.RecipientName}, taken on the road: it plotted against me or my liege."
                     : $"Read a letter {author.Name} wrote to {secret.RecipientName}, taken on the road: it worked against me.",
                  reaction.GrudgeMagnitude, knownToPlayer: secret.FromPlayer, today);

            if (reaction.PlayerRenownPenalty > 0 && Hero.MainHero?.Clan != null)
               GameState.RenownPenalty.Apply(Hero.MainHero, reaction.PlayerRenownPenalty);

            if (reaction.ConfrontsPlayerByLetter) ConfrontPlayer(secret, reader, today);

            // Arrests (Gabriel, 26/09/2026): a plot of the player's, read by a lord of the player's own realm, is treason
            // answered in the liege's name.
            if (severity == SecretSeverity.Treason && secret.FromPlayer && reader.MapFaction != null && reader.MapFaction == Hero.MainHero?.MapFaction)
               Arrests.ArrestBehavior.Instance?.Charge(ArrestCharge.TreasonLetter, $"a letter of yours that {reader.Name} read as treason");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRET] reaction failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The reader writes to the player about the plot they read, grounded in its real words.</summary>
      private static void ConfrontPlayer(CarriedSecret secret, Hero reader, int today)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.LlmServices == null || behavior.Store == null) return;

         NpcProfile profile = behavior.Store.Get(reader.StringId) ?? HeroProfileMapper.FromHero(reader);
         string context = $"A private letter the player sent to {secret.RecipientName} was taken from its courier on the road and came into your hands. "
                          + $"It plots against you or your liege. It read: \"{secret.Content}\"";
         LetterGenerationService.QueueInitialLetter(profile, reader, LetterReason.LeakedCorrespondence, context, behavior.LlmServices, behavior.Store, today);
      }

      #endregion
   }
}
