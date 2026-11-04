// Code written by Gabriel Mailhot, 04/11/2026.
// The one reader of how two NPCs stand with each other (PairBondPolicy). tashmetu (Nexus, 04/11/2026) on the base game:
// Hero.GetRelation answers for the two clan leaders, and the starting friendships and enmities are drawn "without looking
// at realm, culture or family". Some of CR asked Hero.GetRelation and some read the pair's own slot, so the two could
// disagree on the same pair, and both trusted the start. Every relation between two NPCs is now read here: the pair's own
// slot, believed when it moved in play or has a reason; the houses' standing read apart, between their heads.
// Gabriel (04/11/2026): "phrase distincte et mefiance vague, batis-le".

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Relations;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads how two NPCs stand with each other, personally and between their houses.</summary>
   internal static class PairBondReader
   {
      /// <summary>Two houses (or realms) are neighbours when a fief of one lies within this many map units of a fief of the other.</summary>
      internal const float NeighbourDistance = 40f;

      /// <summary>The reasons other mods give for a pair's start (CrPairRegard.RegisterStartReason): the first that answers.</summary>
      internal static readonly PairVetoBook<Hero> StartReasons = new();

      private static readonly Dictionary<string, bool> RealmNeighbours = new();
      private static int _realmNeighboursDay = -1;
      private static readonly HashSet<string> LoggedFaults = new();

      /// <summary>How <paramref name="a" /> and <paramref name="b" /> stand, personally. Never null.</summary>
      internal static PairBond Of(Hero a, Hero b)
      {
         if (a == null || b == null || a == b) return new PairBond();

         try
         {
            var facts = new PairBondFacts {
               Personal = CharacterRelationManager.GetHeroRelation(a, b),
               Baseline = RelationLedgerBehavior.Instance?.Ledger.BaselineOf(a.StringId, b.StringId),
               IsKin = AreKin(a, b),
               SameHouse = a.Clan != null && a.Clan == b.Clan,
               SameRealm = a.MapFaction != null && a.MapFaction == b.MapFaction,
               RealmsAtWar = a.MapFaction != null && b.MapFaction != null && a.MapFaction != b.MapFaction && FactionManager.IsAtWarAgainstFaction(a.MapFaction, b.MapFaction),
               BothRulers = a.IsKingdomLeader && b.IsKingdomLeader,
               Temperament = HeroHelper.NPCPersonalityClashWithNPC(a, b)
            };
            PairBond bond = PairBondPolicy.Assess(facts);
            if (bond.Basis != PairBondBasis.Unexplained) return bond;

            // The dearer facts only for a start the cheap ones could not explain: the lands' distances, then other mods.
            bool neighbours = HousesAreNeighbours(a.Clan, b.Clan) || RealmsAreNeighbours(a.MapFaction, b.MapFaction);
            string outside = neighbours ? null : OutsideReason(a, b);
            if (!neighbours && outside == null) return bond;

            return PairBondPolicy.Assess(new PairBondFacts {
               Personal = facts.Personal, Baseline = facts.Baseline, IsKin = facts.IsKin, SameHouse = facts.SameHouse, SameRealm = facts.SameRealm,
               RealmsAtWar = facts.RealmsAtWar, BothRulers = facts.BothRulers, Temperament = facts.Temperament,
               Neighbours = neighbours, OutsideReason = outside
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELATION] reading {a.Name} / {b.Name} failed: {ex.GetType().Name}: {ex.Message}");

            return new PairBond();
         }
      }

      /// <summary>
      ///   The believed points between the two, for a rule that acts on them. With the player on either side it is the
      ///   game's own relation, as before: the player's standing is a ledger of its own and not a start drawn blind.
      /// </summary>
      internal static int Points(Hero a, Hero b)
      {
         if (a == null || b == null || a == b) return 0;
         if (a == Hero.MainHero || b == Hero.MainHero) return a.GetRelation(b);

         return Of(a, b).Points;
      }

      /// <summary>
      ///   How the heads of the two people's houses stand, or null when they share a house, a head is missing, or both
      ///   already lead their houses (then it is their own bond).
      /// </summary>
      internal static PairBond OfHouses(Hero a, Hero b)
      {
         Hero headA = a?.Clan?.Leader, headB = b?.Clan?.Leader;
         if (headA == null || headB == null || headA == headB || (headA == a && headB == b)) return null;

         return Of(headA, headB);
      }

      /// <summary>
      ///   Whether the pair could stand where <paramref name="tier" /> asks, read cheaply before the whole bond: with no
      ///   change ever seen the believed points are the pair's own value or nothing, so its value alone decides.
      /// </summary>
      internal static bool CouldReach(Hero a, Hero b, Func<int, bool> tier)
      {
         if (a == null || b == null || a == b || tier == null) return false;

         return tier(CharacterRelationManager.GetHeroRelation(a, b)) || RelationLedgerBehavior.Instance?.Ledger.BaselineOf(a.StringId, b.StringId) != null;
      }

      /// <summary>Whether the campaign is past its first days, when other mods may still be seeding their own ties.</summary>
      internal static bool PastStartGrace()
      {
         try
         {
            int start = (int) Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays;

            return PairBondPolicy.PastStartGrace((int) CampaignTime.Now.ToDays - start);
         }
         catch
         {
            return true;
         }
      }

      /// <summary>Whether two houses' lands lie side by side.</summary>
      internal static bool HousesAreNeighbours(Clan a, Clan b)
      {
         if (a?.Settlements == null || b?.Settlements == null || a == b) return false;

         return a.Settlements.Any(x => b.Settlements.Any(y => x.GetPosition2D.Distance(y.GetPosition2D) <= NeighbourDistance));
      }

      #region private

      private static bool AreKin(Hero a, Hero b)
         => a.Spouse == b || b.Spouse == a || a.Father == b || a.Mother == b || b.Father == a || b.Mother == a
            || (a.Siblings != null && a.Siblings.Contains(b));

      // Realms change their lands slowly: their neighbours are measured once a day.
      private static bool RealmsAreNeighbours(IFaction a, IFaction b)
      {
         if (a is not Kingdom ka || b is not Kingdom kb || ka == kb) return false;

         int today = (int) CampaignTime.Now.ToDays;
         if (today != _realmNeighboursDay)
         {
            RealmNeighbours.Clear();
            _realmNeighboursDay = today;
         }

         string key = ForeignDivorceDetection.PairKey(ka.StringId, kb.StringId);
         if (RealmNeighbours.TryGetValue(key, out bool known)) return known;

         List<Settlement> mine = ka.Settlements.Where(s => s.IsFortification).ToList();
         List<Settlement> theirs = kb.Settlements.Where(s => s.IsFortification).ToList();
         bool near = mine.Any(x => theirs.Any(y => x.GetPosition2D.Distance(y.GetPosition2D) <= NeighbourDistance));
         RealmNeighbours[key] = near;

         return near;
      }

      private static string OutsideReason(Hero a, Hero b)
      {
         if (StartReasons.Count == 0) return null;

         PairVetoVerdict answer = StartReasons.Ask(a, b);
         foreach (string fault in answer.Faults)
            if (LoggedFaults.Add(fault)) ModLog.Error($"[RELATION] a start reason failed and was passed over: {fault}");

         return answer.Refused ? answer.Reason : null;
      }

      #endregion
   }
}
