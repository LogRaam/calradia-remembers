// Code written by Gabriel Mailhot, 26/09/2026.
// Outlaws and bounties (arrests Inc 5; Gabriel's rulings, 26/09/2026):
//
//   - A player who flees the sergeants (or breaks out of the law's hold) is an OUTLAW with a price on their head from
//     their own realm: a vassal leaves the kingdom, a mercenary loses the contract.
//   - BRIGANDAGE: attacking a merchant (a caravan by land, a merchant ship by sea) while at peace puts a price on the
//     player's head from the realm that SENT it and the realm that was to RECEIVE the goods; a fishing boat, only from
//     the realm that owns it. Never for the ruler of a kingdom: a sovereign's deeds are politics and war.
//   - Hunters come. A player who leads their own clan outside any kingdom is at war with the realm (the game's own
//     outlaw rule: a crime rating past its threshold brings war on an independent clan); a player still sworn to a
//     kingdom cannot be warred on alone, so outlaw hunters come for them instead.
//   - Captured, the player is handed to the sovereign who wanted them, and tried by that realm's council.
//   - A bounty ends only by AMNESTY, which a realm declares rarely.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a realm has put a price on the player's head.</summary>
   public enum BountyReason
   {
      /// <summary>They fled that realm's own law.</summary>
      Flight,

      /// <summary>They attacked that realm's merchants or fishers in peace.</summary>
      Brigandage
   }

   /// <summary>A realm's price on the player's head.</summary>
   public sealed class Bounty
   {
      public string RealmId { get; set; } = "";
      public BountyReason Reason { get; set; }
      public int PlacedDay { get; set; }
   }

   /// <summary>What was attacked, for brigandage.</summary>
   public sealed class BrigandageFacts
   {
      /// <summary>A fishing boat, not a merchant.</summary>
      public bool IsFishing { get; set; }

      /// <summary>The realm that owns the boat or sent the merchant.</summary>
      public string OwnerRealmId { get; set; }

      /// <summary>The realm the merchant's goods were bound for, when known.</summary>
      public string DestinationRealmId { get; set; }
   }

   /// <summary>How the realm comes after an outlaw.</summary>
   public enum HuntMode
   {
      /// <summary>The player leads their own clan outside any kingdom: the realm makes war on it.</summary>
      War,

      /// <summary>The player is still sworn to a kingdom: outlaw hunters, since the realm cannot war on one vassal.</summary>
      OutlawHunters
   }

   /// <summary>The rules of outlawry, bounties and amnesty. Pure.</summary>
   public static class OutlawPolicy
   {
      /// <summary>TUNE: the fewest men a band of bounty hunters rides with.</summary>
      public const int MinHunters = 15;

      /// <summary>TUNE: the most men a band of bounty hunters rides with.</summary>
      public const int MaxHunters = 60;

      /// <summary>TUNE: days before a realm sends new hunters after its last band was lost.</summary>
      public const int HunterCooldownDays = 10;

      /// <summary>
      ///   How many hunters a realm sends: about four in five of the player's own number, so a fight is real but
      ///   winnable, within <see cref="MinHunters" /> and <see cref="MaxHunters" />.
      /// </summary>
      public static int HunterStrength(int playerPartySize)
         => Math.Max(MinHunters, Math.Min(MaxHunters, (int) Math.Round(playerPartySize * 0.8)));

      /// <summary>True when a realm may send new hunters, <see cref="HunterCooldownDays" /> after its last band.</summary>
      public static bool MaySendHunters(int? lastSentDay, int today)
         => lastSentDay is not int last || today - last >= HunterCooldownDays;

      /// <summary>
      ///   The charges a realm's bounty brings to trial: a flight from that realm's own law, or brigandage against it.
      /// </summary>
      public static ArrestCharge ChargeFor(BountyReason reason)
         => reason == BountyReason.Flight ? ArrestCharge.FledArrest : ArrestCharge.Brigandage;

      /// <summary>TUNE: the chance, each week and for each realm, that it declares an amnesty (about 40% over a year).</summary>
      public const double AmnestyWeeklyChance = 0.01;

      /// <summary>
      ///   The realms that put a price on the player's head for an attack on <paramref name="facts" />: a merchant's
      ///   sender and receiver, a fishing boat's owner, never the same realm twice, never an unknown one.
      /// </summary>
      public static IReadOnlyList<string> BrigandageRealms(BrigandageFacts facts)
      {
         if (facts == null) return Array.Empty<string>();

         IEnumerable<string> realms = facts.IsFishing
            ? new[] {facts.OwnerRealmId}
            : new[] {facts.OwnerRealmId, facts.DestinationRealmId};

         return realms.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
      }

      /// <summary>
      ///   True when an attack on a realm's merchants or fishers makes the player a brigand in that realm's eyes: never
      ///   for the ruler of a kingdom, and never against a realm the player is already at war with (that is war).
      /// </summary>
      public static bool IsBrigandage(bool playerRulesAKingdom, bool atWarWithThatRealm)
         => !playerRulesAKingdom && !atWarWithThatRealm;

      /// <summary>How a realm hunts the outlaw: war on an independent clan, outlaw hunters otherwise.</summary>
      public static HuntMode ModeFor(bool playerLeadsAnIndependentClan)
         => playerLeadsAnIndependentClan ? HuntMode.War : HuntMode.OutlawHunters;

      /// <summary>True when this week's roll (in [0,1)) brings a realm's amnesty.</summary>
      public static bool AmnestyDeclared(double roll) => roll >= 0 && roll < AmnestyWeeklyChance;

      /// <summary>Adds a realm's bounty, or keeps the one it already has (one price per realm).</summary>
      public static void Place(List<Bounty> bounties, string realmId, BountyReason reason, int day)
      {
         if (bounties == null || string.IsNullOrWhiteSpace(realmId)) return;
         if (bounties.Any(b => b.RealmId == realmId)) return;

         bounties.Add(new Bounty {RealmId = realmId, Reason = reason, PlacedDay = day});
      }
   }
}
