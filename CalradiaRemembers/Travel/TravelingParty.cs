// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Travel
{
   /// <summary>
   ///   Low-level, stateless helpers for a visible, fast, defeatable travelling party — the mechanics
   ///   behind the letter courier, now shared with the companion news-errand. Callers own the party's
   ///   identity tracking and save-safety; this only spawns, steers, and destroys.
   /// </summary>
   public static class TravelingParty
   {
      /// <summary>Base map speed: faster than any army, but still a horseman rather than a racing car.</summary>
      public const float BaseSpeed = 6.5f;

      /// <summary>A small, weak escort so the party is a real (defeatable) target, not invincible.</summary>
      public const int EscortSize = 3;

      /// <summary>
      ///   A larger mounted escort for a courier the player has paid to guard (an escorted correspondence thread).
      ///   Purely the visible, cosmetic justification for that courier's interception immunity: a few times the
      ///   base so it plainly reads as an armed guard detail, while still riding at full map speed.
      /// </summary>
      public const int HeavyEscortSize = 12;

      // Extra sumpter horses on top of the escort, to hold max map speed even with foot-bound troops.
      private const int SumpterExtra = 2;

      // Courier audit C3: grain stocked at spawn so a small escort stays fed across a long journey. Without any
      // food the engine starves and destroys a long-blocked courier, which CR then misreports as "waylaid by
      // bandits". 100 units is many weeks for so small a party, covering any normal (or slow) delivery.
      private const int CourierRationUnits = 100;

      /// <summary>Disbands the party out of the world (the caller drops its id tracking first).</summary>
      public static void Destroy(MobileParty party)
      {
         if (party is {IsActive: true})
            DestroyPartyAction.ApplyForDisbanding(party, party.CurrentSettlement);
      }

      /// <summary>
      ///   Points the party at a map point — refresh it each tick to track a moving target (e.g. the
      ///   player). The same AI-lock dance as <see cref="DirectToSettlement" />.
      /// </summary>
      public static void DirectToMovingPoint(MobileParty party, Vec2 point)
      {
         if (party == null) return;
         party.Ai.SetDoNotMakeNewDecisions(false);
         party.SetMoveGoToPoint(new CampaignVec2(point, true), MobileParty.NavigationType.Default);
         party.Ai.SetDoNotMakeNewDecisions(true);
      }

      /// <summary>
      ///   Points the party at a settlement (the AI-lock is released for the order then re-held so the
      ///   party cannot wander off it between ticks). Safe to re-issue while travelling; do NOT call while
      ///   the party already sits inside the target settlement (re-issuing ejects it — the caller guards).
      /// </summary>
      public static void DirectToSettlement(MobileParty party, Settlement settlement)
      {
         if (party == null || settlement == null) return;
         party.Ai.SetDoNotMakeNewDecisions(false);
         party.SetMoveGoToSettlement(settlement, MobileParty.NavigationType.Default, false);
         party.Ai.SetDoNotMakeNewDecisions(true);
      }

      /// <summary>
      ///   The land position where travel from <paramref name="seaPos" /> comes ashore: the nearest port
      ///   settlement (heavily preferred — where a ship's mail is put off), else the nearest settlement.
      /// </summary>
      public static Vec2 NearestLandfall(Vec2 seaPos)
      {
         Settlement best = null;
         var bestScore = float.MaxValue;
         foreach (Settlement s in Settlement.All)
         {
            if (s.IsHideout) continue;
            float score = s.GetPosition2D.Distance(seaPos);
            if (!s.HasPort) score *= 3f; // strong port preference

            if (score >= bestScore) continue;
            bestScore = score;
            best = s;
         }

         return best?.GetPosition2D ?? seaPos;
      }

      /// <summary>
      ///   Spawns a fast, lightly-escorted custom party at <paramref name="spawnPos" />, belonging to the
      ///   player's clan, with a mounted escort drawn from <paramref name="preferredCulture" /> (falling
      ///   back to the player's culture). Returns null on failure. The caller steers and tracks it.
      ///   <paramref name="ownerClan" /> and <paramref name="ownerHero" /> give the party to someone else (the
      ///   sergeants ride in their liege's colours); left null, it is the player's, as every courier is.
      /// </summary>
      public static MobileParty Spawn(Vec2 spawnPos,
                                      Settlement home,
                                      TextObject name,
                                      float speed,
                                      CultureObject preferredCulture,
                                      bool avoidHostile,
                                      int escortSize = EscortSize,
                                      Clan ownerClan = null,
                                      Hero ownerHero = null)
      {
         // Never a point the engine cannot stand on (MapSpot): a courier or hunters restored from a save, or sent from
         // a party at sea, would otherwise reach the native placement with no navigation face.
         CampaignVec2? spot = MapSpot.Walkable(spawnPos, home, name?.ToString() ?? "traveling party");
         if (spot == null) return null;

         TroopRoster roster = BuildEscortRoster(preferredCulture, escortSize);

         MobileParty party = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
            spot.Value,
            1f,
            home,
            name,
            ownerClan ?? Clan.PlayerClan,
            roster,
            TroopRoster.CreateDummyTroopRoster(),
            ownerHero ?? Hero.MainHero,
            null,
            null,
            speed,
            avoidHostile);

         if (party == null) return null;

         var sumpter = Game.Current.ObjectManager.GetObject<ItemObject>("sumpter_horse");
         if (sumpter != null) party.ItemRoster.AddToCounts(sumpter, escortSize + SumpterExtra);

         // C3: feed the escort so a long or stalled trip never starves the party into a false "bandit waylay".
         var grain = Game.Current.ObjectManager.GetObject<ItemObject>("grain");
         if (grain != null) party.ItemRoster.AddToCounts(grain, CourierRationUnits);

         return party;
      }

      #region private

      private static TroopRoster BuildEscortRoster(CultureObject preferred, int escortSize)
      {
         var roster = TroopRoster.CreateDummyTroopRoster();
         // Prefer mounted troops so the party rides faster than foot-bound parties and can keep pace;
         // fall back through the player's culture to the basic recruit.
         CharacterObject troop = FindMountedTroop(preferred) ?? FindMountedTroop(Hero.MainHero?.Culture) ?? preferred?.BasicTroop ?? Hero.MainHero?.Culture?.BasicTroop;
         if (troop != null) roster.AddToCounts(troop, escortSize);

         return roster;
      }

      /// <summary>
      ///   Walks a culture's basic-troop upgrade tree (breadth-first) for the first mounted troop, so the
      ///   party rides rather than marches. Null when the culture's tree has no cavalry.
      /// </summary>
      private static CharacterObject FindMountedTroop(CultureObject culture)
      {
         CharacterObject basic = culture?.BasicTroop;

         if (basic == null) return null;

         var seen = new HashSet<string>();
         var queue = new Queue<CharacterObject>();
         queue.Enqueue(basic);
         while (queue.Count > 0)
         {
            CharacterObject c = queue.Dequeue();

            if (c == null || !seen.Add(c.StringId)) continue;

            if (c.IsMounted) return c;

            if (c.UpgradeTargets == null) continue;
            foreach (CharacterObject up in c.UpgradeTargets) queue.Enqueue(up);
         }

         return null;
      }

      #endregion
   }
}