// Code written by Gabriel Mailhot, 01/11/2026.
// expose_secret (Gabriel's "go", 01/11/2026): the player tells an affair the game really holds, which SecretExposurePolicy rules.
// No code simulates an affair between two characters, so what can be told is what the extortion lever knows: the player's own
// married secret lover, or a partner of the player whose hidden lover was found out. The world cannot hear of it through
// WorldGossip (NpcsRaiseWorldNews is off by default), so the scandal involves the player, as the bastard scandal does, which
// is also true: they are the lover, the betrayed, or the one who spoke. Romantic state is left as it is: a told affair is
// spent in the ledger, and the gossip stops, so nobody later whispers an unknown lover or sells what everyone has heard.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Bastards;
using CalradiaRemembers.Logic.Extortion;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Trauma;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>An affair the player can tell.</summary>
   internal sealed class HeldSecret
   {
      /// <summary>The one whose affair it is.</summary>
      public Hero Subject { get; set; }

      /// <summary>Their lover, who may be the player.</summary>
      public Hero Lover { get; set; }

      /// <summary>The secret in one sentence, for the prompt.</summary>
      public string Words => $"{Subject.Name} and {Lover.Name} are lovers in secret.";
   }

   /// <summary>Lists the affairs the player can tell, and carries out the telling.</summary>
   internal static class SecretExposure
   {
      /// <summary>Every affair this listener can be told, the spent ones gone.</summary>
      internal static List<HeldSecret> HeldFor(Hero listener)
      {
         Hero player = Hero.MainHero;
         ExtortionBehavior behavior = ExtortionBehavior.Instance;
         if (ModSettings.AdultContentLevel == AdultContentLevel.Off || player == null || behavior == null || listener == null || listener == player) return new List<HeldSecret>();

         return Affairs(player).Where(s => !behavior.Ledger.IsExposed(s.Subject.StringId, s.Lover.StringId)
                                           && SecretExposurePolicy.MayHear(listener.StringId, s.Subject.StringId, s.Lover.StringId)).ToList();
      }

      /// <summary>The held affair this name points to, or the only one held when the model gave no name.</summary>
      internal static HeldSecret Named(IReadOnlyList<HeldSecret> held, string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return held.Count == 1 ? held[0] : null;

         string query = name.Trim();

         return held.FirstOrDefault(s => Names(s.Subject, query) || s.Lover != Hero.MainHero && Names(s.Lover, query));
      }

      /// <summary>True when the player's words name someone in the affair other than the player.</summary>
      internal static bool PlayerSpokeOf(HeldSecret secret, string playerWords)
         => SwayGroundingPolicy.PlayerSpokeOf(playerWords, secret.Subject.Name?.ToString())
            || secret.Lover != Hero.MainHero && SwayGroundingPolicy.PlayerSpokeOf(playerWords, secret.Lover.Name?.ToString());

      /// <summary>Tells the affair: the scandal, the shame of each house, what those it names do about it. Returns the event's id.</summary>
      internal static string Expose(HeldSecret secret, Hero listener)
      {
         ExtortionBehavior behavior = ExtortionBehavior.Instance;
         Hero player = Hero.MainHero;
         var today = (int) CampaignTime.Now.ToDays;
         List<Hero> named = new[] {secret.Subject, secret.Lover}.Where(h => h != player).ToList();

         behavior.Ledger.Exposures.Add(new ExposureRecord {SubjectId = secret.Subject.StringId, LoverId = secret.Lover.StringId, ListenerId = listener.StringId, Day = today});
         string eventId = RecordScandal(secret, today);
         ShameTheHouses(secret);

         foreach (Hero hero in named)
         {
            Answer(hero, secret, listener, today);
            BetrayTheSpouse(hero, secret, today);
         }

         Remember(listener, NotableEventType.Other, $"{player.Name} told me that {secret.Subject.Name} and {secret.Lover.Name} are lovers in secret.", today);
         SecretLoverGossipBehavior.Instance?.Retire(secret.Subject.StringId);
         if (secret.Lover == player) JealousyBehavior.Instance?.OnAffairExposed(secret.Subject, today);

         ModLog.Info($"[EXTORT] {player.Name} told {listener.Name} of {secret.Subject.Name} and {secret.Lover.Name}.");

         return eventId;
      }

      private static IEnumerable<HeldSecret> Affairs(Hero player)
      {
         IEnumerable<string> lovers = SecretLoverGossipBehavior.Instance?.PlayerSecretLoverSubjectIds() ?? Enumerable.Empty<string>();
         foreach (string id in lovers)
         {
            Hero hero = Hero.FindFirst(h => h != null && h.StringId == id);
            if (HeroPresence.IsInTheWorld(hero) && HeroPresence.IsInTheWorld(hero.Spouse) && hero.Spouse != player) yield return new HeldSecret {Subject = hero, Lover = player};
         }

         foreach (Hero hero in Hero.AllAliveHeroes.ToList())
         {
            RomanticProfile romantic = ExtortionBehavior.RomanticOf(hero);
            if (!ExtortionBehavior.IsAffairKnown(romantic)) continue;

            Hero lover = Hero.FindFirst(h => h != null && h.StringId == romantic.TakenLoverId);
            if (HeroPresence.IsInTheWorld(lover)) yield return new HeldSecret {Subject = hero, Lover = lover};
         }
      }

      private static bool Names(Hero hero, string query) => hero.Name?.ToString().IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

      private static string Label(Hero hero)
         => HeroLabel.Describe(new HeroLabel.HeroNaming {Name = hero.Name?.ToString(), ClanName = hero.Clan?.Name?.ToString(), CultureName = hero.Culture?.Name?.ToString()});

      private static JealousyOutrage Outrage(Hero hero) => JealousyCultureLoader.GetCulture(hero.Culture?.Name?.ToString()).Outrage;

      private static string RecordScandal(HeldSecret secret, int today)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return null;

         Hero player = Hero.MainHero;
         var names = new ExposureNames {Subject = Label(secret.Subject), Lover = Label(secret.Lover), Teller = player.Name?.ToString(), TellerIsLover = secret.Lover == player};
         var e = new WorldEvent {
            Id = System.Guid.NewGuid().ToString("N"),
            Type = WorldEventType.Scandal,
            Day = today,
            Magnitude = BastardScandalPolicy.Scale(SecretExposurePolicy.ScandalMagnitude, Outrage(secret.Subject)),
            InvolvesPlayer = true,
            ActorIds = new List<string> {secret.Subject.StringId, secret.Lover.StringId},
            FactionIds = new[] {secret.Subject, secret.Lover}.Select(h => h.MapFaction?.StringId).Where(id => id != null).Distinct().ToList(),
            Summary = SecretExposurePolicy.Rumour(names),
            ChronicleLine = SecretExposurePolicy.Chronicle(names)
         };
         store.Record(e);

         return e.Id;
      }

      // Each house once, the player's among them when it is in the affair: the shame of it is not only the victim's.
      private static void ShameTheHouses(HeldSecret secret)
      {
         foreach (Hero hero in new[] {secret.Subject, secret.Lover}.Where(h => h.Clan != null).GroupBy(h => h.Clan).Select(g => g.First()))
         {
            RenownPenalty.Apply(hero, BastardScandalPolicy.Scale(SecretExposurePolicy.RenownLoss, Outrage(hero)));
            ChangeClanInfluenceAction.Apply(hero.Clan, -BastardScandalPolicy.Scale(SecretExposurePolicy.InfluenceLoss, Outrage(hero)));
         }
      }

      private static void Answer(Hero hero, HeldSecret secret, Hero listener, int today)
      {
         ExtortionBehavior behavior = ExtortionBehavior.Instance;
         ExtortionRecord bargain = behavior.Ledger.SecretBargainWith(hero.StringId, today);
         ExposureCost cost = SecretExposurePolicy.Cost(bargain != null);
         if (bargain != null) bargain.BargainBroken = true;

         MisdeedLedgerBehavior.Record(hero.StringId, bargain != null ? MisdeedKind.BrokeSecretBargain : MisdeedKind.ExposedSecret);
         ExtortionBehavior.Charge(hero, new ExtortionCharge {
            Deed = bargain != null ? PlayerDeed.SecretBargainBroken : PlayerDeed.SecretExposed,
            Source = GrudgeSource.SecretExposed,
            RelationHit = cost.RelationHit,
            Grudge = cost.GrudgeMagnitude,
            Cause = bargain != null ? "The player took my coin for their silence, and told my secret anyway." : "The player told my secret.",
            Memory = bargain != null ? $"You took my coin for your silence, and told {listener.Name} anyway." : $"You told {listener.Name} what should have stayed hidden."
         });
      }

      // The player is left out: their wounds are their own to carry, in the chat and on the Heart page.
      private static void BetrayTheSpouse(Hero hero, HeldSecret secret, int today)
      {
         Hero spouse = hero.Spouse;
         if (!HeroPresence.IsInTheWorld(spouse) || spouse == Hero.MainHero) return;

         Hero other = hero == secret.Subject ? secret.Lover : secret.Subject;
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouse, hero, DeedRegardPolicy.BetrayedSpouseRelation, false);
         Remember(spouse, NotableEventType.Jealousy, $"Word reached me that {hero.Name} has been lying with {other.Name}.", today);
      }

      private static void Remember(Hero hero, NotableEventType type, string memory, int today)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return;

         NpcProfile profile = behavior.Store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         profile.Events.Add(new NotableEvent(today, type, memory));
         behavior.Store.Set(profile);
      }
   }
}
