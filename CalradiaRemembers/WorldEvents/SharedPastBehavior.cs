// Code written by Gabriel Mailhot, 17/10/2026.
// The generated shared past, in the game (docs/design/NPC_REGARD.md section 7; SharedPastPolicy decides). Gabriel's
// rulings: a pair with nothing between them may receive a past, drawn once and true forever after; it is written into
// the world's long-term memory (7.7a), dated before the campaign. A draw happens only when a character's encyclopedia
// page opens (7.1): at most six new pairs per opening, the nearest first, never in a conversation, so a conversation
// reads what the pages already drew and stays deterministic. Every outcome, nothing included, is kept in the save, so a
// pair is never drawn again when its facts move during the campaign.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Draws the shared past of the pairs a page shows, once each, and keeps who was drawn.</summary>
   public sealed class SharedPastBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_SharedPastDrawn_v1bytes";

      /// <summary>At most this many new pairs drawn per page opening (design 7.1).</summary>
      public const int MaxDrawsPerOpening = 6;

      /// <summary>Two houses are neighbours when a fief of one lies within this many map units of a fief of the other.</summary>
      public const float NeighbourDistance = 40f;

      /// <summary>No new draw once the long-term memory is this close to its cap: a past once drawn should not be evicted.</summary>
      public const int HeadroomBelowCap = 500;

      /// <summary>A pair with a trace this strong already has its story and is never drawn (7.1).</summary>
      public const int StrongTrace = 10;

      public static SharedPastBehavior Instance;

      private HashSet<string> _drawn = new();

      public SharedPastBehavior() => Instance = this;

      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_drawn.ToList(), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[PAST] drawn pairs serialize failed, persisting none. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[PAST] drawn pairs save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves: nothing drawn yet */
            }

            _drawn = Deserialize(bytes);
         }
      }

      /// <summary>True when this pair was already drawn (something or nothing).</summary>
      public bool WasDrawn(Hero a, Hero b) => a != null && b != null && _drawn.Contains(PairKey(a, b));

      /// <summary>
      ///   Draws, for <paramref name="hero" />, the pairs among <paramref name="candidates" /> never drawn, not kin, with no
      ///   strong trace already: the nearest first, at most <see cref="MaxDrawsPerOpening" />. Returns how many were drawn.
      /// </summary>
      public int DrawFor(Hero hero, IEnumerable<Hero> candidates)
      {
         if (hero == null || candidates == null || WorldEventStore.Instance == null) return 0;
         if (WorldEventStore.Instance.AllHistory().Count >= LongTermHistoryPolicy.Cap - HeadroomBelowCap) return 0;

         int drawn = 0;
         try
         {
            Vec2 at = Where(hero);
            foreach (Hero other in candidates.Where(c => c != null && c != hero && c != Hero.MainHero && !_drawn.Contains(PairKey(hero, c)))
                                             .Distinct()
                                             .OrderBy(c => Where(c).Distance(at)))
            {
               if (drawn >= MaxDrawsPerOpening) break;
               if (!Eligible(hero, other)) continue;

               DrawPair(hero, other);
               drawn++;
            }
         }
         catch (Exception ex) { ModLog.Error($"[PAST] drawing for {hero.Name} failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

         return drawn;
      }

      /// <summary>For cr.testall: forgets a pair's draw and removes its past from the long-term memory.</summary>
      internal void DebugForget(Hero a, Hero b)
      {
         string key = PairKey(a, b);
         _drawn.Remove(key);
         WorldEventStore.Instance?.RemoveHistory(RecordId(key));
      }

      /// <summary>The id a drawn past carries in the long-term memory.</summary>
      internal static string RecordId(string pairKey) => "shared_past:" + pairKey;

      internal static string PairKey(Hero a, Hero b) => ForeignDivorceDetection.PairKey(a.StringId, b.StringId);

      #region private

      private static HashSet<string> Deserialize(byte[] bytes)
      {
         if (bytes is not {Length: > 0}) return new HashSet<string>();

         try { return new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(bytes)) ?? new List<string>()); }
         catch (Exception ex)
         {
            ModLog.Error($"[PAST] drawn pairs load failed, none kept for this load. {ex.GetType().Name}: {ex.Message}");

            return new HashSet<string>();
         }
      }

      // Adults in the world, not kin (kin belong in the family, design 5.2), with no story of their own already.
      private static bool Eligible(Hero hero, Hero other)
      {
         if (!HeroPresence.IsInTheWorld(other) || other.IsChild || hero.IsChild || !(other.IsLord || other.IsWanderer)) return false;
         if (hero.Spouse == other || hero.Father == other || hero.Mother == other || other.Father == hero || other.Mother == hero) return false;
         if (hero.Siblings != null && hero.Siblings.Contains(other)) return false;

         int today = (int) CampaignTime.Now.ToDays;
         bool storied = PairRegardReader.Facts(hero, other, today).Traces.Any(t => Math.Abs(t.Weight) >= StrongTrace)
                        || PairRegardReader.Facts(other, hero, today).Traces.Any(t => Math.Abs(t.Weight) >= StrongTrace);

         return !storied;
      }

      private void DrawPair(Hero a, Hero b)
      {
         string key = PairKey(a, b);
         Settlement place = NearestTown(a.HomeSettlement ?? b.HomeSettlement);
         int daysInYear = CampaignTime.DaysInSeason * CampaignTime.SeasonsInYear;

         SharedPastDraw draw = SharedPastPolicy.Draw(new SharedPastPairFacts {
            A = Side(a), B = Side(b),
            Neighbours = Neighbours(a.Clan, b.Clan),
            Compatibility = CompanionRapport.Compatibility(Traits(a), Traits(b)),
            SameRealm = a.MapFaction != null && a.MapFaction == b.MapFaction,
            SameCulture = a.Culture != null && a.Culture == b.Culture,
            VanillaRelation = CharacterRelationManager.GetHeroRelation(a, b),
            PlaceName = place?.Name?.ToString(), PlaceId = place?.StringId,
            CampaignStartDay = (int) Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays,
            DaysInYear = daysInYear,
            ComesOfAgeYears = Campaign.Current.Models.AgeModel.HeroComesOfAge
         });

         _drawn.Add(key);
         if (draw == null)
         {
            ModLog.Info($"[PAST] {a.Name} and {b.Name}: nothing between them before the campaign.");

            return;
         }

         WorldEventStore.Instance.RecordHistory(new WorldEvent {
            Id = RecordId(key), Type = WorldEventType.Other, Status = SharedPastPolicy.StatusPrefix + draw.Kind,
            Day = draw.Day, Magnitude = SharedPastPolicy.Magnitude, Summary = draw.Summary,
            ActorIds = new List<string> {draw.FirstId, draw.SecondId}, PlaceId = place?.StringId, SourceMod = "CalradiaRemembers.SharedPast"
         });
         ModLog.Info($"[PAST] {a.Name} and {b.Name}: {draw.Kind}, day {draw.Day}: {draw.Summary}");
      }

      private static SharedPastSide Side(Hero h) => new() {
         Id = h.StringId, Name = h.Name?.ToString(), IsFemale = h.IsFemale, BirthDay = (int) h.BirthDay.ToDays,
         Valor = h.GetTraitLevel(DefaultTraits.Valor), Generosity = h.GetTraitLevel(DefaultTraits.Generosity),
         NeverMarried = h.Spouse == null && (h.ExSpouses == null || h.ExSpouses.Count == 0), IsLord = h.IsLord
      };

      private static RomanticTraits Traits(Hero h) => new() {
         Valor = h.GetTraitLevel(DefaultTraits.Valor), Honor = h.GetTraitLevel(DefaultTraits.Honor), Mercy = h.GetTraitLevel(DefaultTraits.Mercy),
         Generosity = h.GetTraitLevel(DefaultTraits.Generosity), Calculating = h.GetTraitLevel(DefaultTraits.Calculating)
      };

      private static bool Neighbours(Clan a, Clan b)
      {
         if (a?.Settlements == null || b?.Settlements == null || a == b) return false;

         return a.Settlements.Any(x => b.Settlements.Any(y => x.GetPosition2D.Distance(y.GetPosition2D) <= NeighbourDistance));
      }

      private static Settlement NearestTown(Settlement from)
      {
         if (from == null) return null;
         if (from.IsTown) return from;

         Vec2 at = from.GetPosition2D;

         return Settlement.All.Where(s => s != null && s.IsTown).OrderBy(s => s.GetPosition2D.Distance(at)).FirstOrDefault();
      }

      private static Vec2 Where(Hero h)
         => h.CurrentSettlement?.GetPosition2D ?? h.HomeSettlement?.GetPosition2D ?? h.PartyBelongedTo?.GetPosition2D ?? Vec2.Zero;

      #endregion
   }
}
