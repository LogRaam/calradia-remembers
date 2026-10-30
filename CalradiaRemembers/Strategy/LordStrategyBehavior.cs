// Code written by Gabriel Mailhot, 30/10/2026.
// Lords who play the realm's strategy (Gabriel, 29/10/2026: "faire des arrangements avec d'autres factions plus faibles
// pour attaquer une faction plus puissante"; 30/10/2026: the mod may have a house put the game's own decisions to the
// vote). Once a week, with the MCM switch on, when one realm has grown far too strong, ONE other realm takes one step of
// a counterweight against it (RealmCounterweightPolicy): calls an ally to arms, makes peace with a weaker enemy, seeks an
// alliance, or puts the war itself to the vote. The step is the game's own kingdom decision, filed by the house that
// backs it most by the game's own support; the house pays its influence, the clans vote and the ruler decides. A realm
// steps at most once a month (one additive save key; an old save starts with none). The player's own realm is left to
// the player in this first build: a decision filed there would raise the game's vote popup, an interruption outside the
// world's initiative regulator, and an offer to it would do the same. The news says who urged it, never the outcome.
// Presence, the war rules and the world's news are supplied at startup (NpcMemoryServiceSubmodule), as for the edicts,
// so the strategists never know the map, verb or event layers.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Strategy;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Strategy
{
   /// <summary>The news of one strategist step, told through <see cref="LordStrategyBehavior.Tell" />.</summary>
   public sealed class StrategyNews
   {
      /// <summary>The day.</summary>
      public int Day { get; set; }

      /// <summary>What the court hears.</summary>
      public string Summary { get; set; }

      /// <summary>The step leads toward war (a war, a call to arms) rather than toward peace or a pact.</summary>
      public bool TowardWar { get; set; }

      /// <summary>The house head who urged it.</summary>
      public string ProposerId { get; set; }

      /// <summary>The realms concerned.</summary>
      public List<string> RealmIds { get; set; } = new();
   }

   /// <summary>The strategists' weekly step against a realm grown too strong, through the game's own kingdom decisions.</summary>
   public sealed class LordStrategyBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_LordStrategy_v1bytes";

      public static LordStrategyBehavior Instance;

      /// <summary>Whether a hero is in the world (Mapping.HeroPresence), supplied at startup.</summary>
      public static Func<Hero, bool> IsPresent = h => h != null && !h.IsDead;

      /// <summary>
      ///   Why a house may not put war on a realm to the vote now (the press_for_war rules: peace too recent, a proposal
      ///   waiting, the influence), or null when it may. Supplied at startup; unsupplied, no war is ever filed.
      /// </summary>
      public static Func<Clan, Kingdom, string> WarRefusal = (_, _) => "the war rules are not supplied";

      /// <summary>The strategists' aims toward the player, renewed once a week (Aims.StrategistAims), supplied at startup.</summary>
      public static Action<int> SeedAims = _ => { };

      /// <summary>Where the news is told (the world's event store), supplied at startup.</summary>
      public static Action<StrategyNews> Tell = _ => { };

      private Dictionary<string, int> _lastStepDay = new();

      public LordStrategyBehavior()
      {
         Instance = this;
      }

      /// <summary>A step a realm would take now: the house that files it and the game's decision, not yet filed.</summary>
      public sealed class Proposal
      {
         public Kingdom Realm { get; set; }
         public Kingdom Dominant { get; set; }
         public Kingdom Other { get; set; }
         public Clan House { get; set; }
         public CounterweightMove Move { get; set; }
         public KingdomDecision Decision { get; set; }
         public float Support { get; set; }
      }

      internal static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>The day each realm last took a strategist step, by realm StringId.</summary>
      public IReadOnlyDictionary<string, int> LastStepDay => _lastStepDay;

      public override void RegisterEvents()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("LordStrategy.Weekly", () =>
         {
            if (!ModSettings.EnableLordStrategy) return;

            Weekly(Today);
            SeedAims(Today);
         }));
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_lastStepDay)); }
            catch (Exception ex)
            {
               ModLog.Error($"[STRATEGY] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[STRATEGY] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               // A save written before this behavior: no realm has stepped yet.
            }

            try { _lastStepDay = bytes is {Length: > 0} ? JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>() : new Dictionary<string, int>(); }
            catch (Exception ex)
            {
               _lastStepDay = new Dictionary<string, int>();
               ModLog.Error($"[STRATEGY] load failed, realm steps forgotten for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      /// <summary>The realms still standing.</summary>
      internal static List<Kingdom> Standing() => Kingdom.All.Where(k => k != null && !k.IsEliminated).ToList();

      /// <summary>The realm grown too strong (RealmCounterweightPolicy.Dominant), or null while the balance holds.</summary>
      internal static Kingdom DominantRealm()
      {
         List<Kingdom> standing = Standing();
         string id = RealmCounterweightPolicy.Dominant(standing.Select(k => new RealmPower {Id = k.StringId, Strength = k.CurrentTotalStrength}).ToList());

         return standing.FirstOrDefault(k => k.StringId == id);
      }

      /// <summary>
      ///   One weekly pass: the first realm (in a random order, so no realm always leads) able to take a step against the
      ///   dominant realm takes it. At most one step a week in all the world, at most one a month per realm.
      /// </summary>
      internal Proposal Weekly(int today)
      {
         Kingdom dominant = DominantRealm();
         if (dominant == null) return null;

         foreach (Kingdom realm in Standing().Where(k => k != dominant).OrderBy(_ => MBRandom.RandomFloat))
         {
            if (!RealmCounterweightPolicy.RealmMayAct(_lastStepDay.TryGetValue(realm.StringId, out int last) ? last : (int?) null, today)) continue;

            Proposal p = Prepare(realm, dominant, out _);
            if (p == null) continue;

            File(p, today);

            return p;
         }

         return null;
      }

      /// <summary>
      ///   The step this realm would take against the dominant one now, or null with the reason: the policy's steps in order,
      ///   the first the game allows and some house backs and can pay for, filed by the house that backs it most. Files
      ///   nothing (outside the player's realm, a vote resolves the moment a decision is added).
      /// </summary>
      internal static Proposal Prepare(Kingdom realm, Kingdom dominant, out string whyNot)
      {
         if (realm == null || dominant == null || realm == dominant || realm.IsEliminated) { whyNot = "no realm facing a dominant one"; return null; }
         if (realm == Clan.PlayerClan?.Kingdom) { whyNot = "the player's own realm is left to the player"; return null; }

         List<Kingdom> others = Standing().Where(k => k != realm && k != dominant).ToList();
         var facts = new CounterweightFacts {
            Strength = realm.CurrentTotalStrength,
            AtWarWithDominant = realm.IsAtWarWith(dominant),
            Others = others.Select(k => new OtherRealm {
               Id = k.StringId, Strength = k.CurrentTotalStrength, AtWarWithUs = realm.IsAtWarWith(k), AtWarWithDominant = k.IsAtWarWith(dominant), AlliedWithUs = realm.IsAllyWith(k)
            }).ToList()
         };

         foreach (CounterweightStep step in RealmCounterweightPolicy.Steps(facts, dominant.StringId))
         {
            Kingdom other = step.RealmId == dominant.StringId ? dominant : others.FirstOrDefault(k => k.StringId == step.RealmId);
            if (other == null) continue;

            // An alliance, a peace or a call with the player's realm would put an offer to the player: left to the player.
            if (step.Move != CounterweightMove.WarOnDominant && other == Clan.PlayerClan?.Kingdom) continue;

            Proposal best = null;
            foreach (Clan house in Houses(realm))
            {
               KingdomDecision decision = Build(step.Move, house, other, dominant);
               if (decision == null) continue;
               if (realm.UnresolvedDecisions.Any(d => d.GetType() == decision.GetType())) break;
               if (!Allowed(decision)) continue;
               if (house.Influence < decision.GetProposalInfluenceCost()) continue;

               float support = Support(decision, house);
               if (!RealmCounterweightPolicy.HouseWouldPropose(step.Move, support)) continue;
               if (best == null || support > best.Support)
                  best = new Proposal {Realm = realm, Dominant = dominant, Other = other, House = house, Move = step.Move, Decision = decision, Support = support};
            }

            if (best != null)
            {
               whyNot = null;

               return best;
            }
         }

         whyNot = "no step the game allows, that a house backs and can pay for";

         return null;
      }

      /// <summary>The news the step is told with: who urged what, against whom (RealmCounterweightPolicy.News).</summary>
      internal static string News(Proposal p)
         => RealmCounterweightPolicy.News(new CounterweightNewsFacts {
            Move = p.Move,
            Proposer = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = p.House?.Leader?.Name?.ToString(), ClanName = p.House?.Name?.ToString()}),
            Realm = p.Realm?.Name?.ToString(),
            Other = p.Other?.Name?.ToString(),
            Dominant = p.Dominant?.Name?.ToString()
         });

      /// <summary>
      ///   Files the step: the game's own decision, which charges the house its influence and is voted on at once in a realm
      ///   the player does not belong to; then, once it is filed, the news of who urged it.
      /// </summary>
      internal void File(Proposal p, int today)
      {
         // The game's decision first: if the engine refuses it, the court must not hear of a proposal that never happened.
         try { p.Realm.AddDecision(p.Decision); }
         catch (Exception ex)
         {
            ModLog.Error($"[STRATEGY] {p.Move} by {p.House?.Name} in {p.Realm?.Name} could not be filed: {ex.GetType().Name}: {ex.Message}");

            return;
         }

         _lastStepDay[p.Realm.StringId] = today;
         string news = News(p);
         Tell(new StrategyNews {
            Day = today,
            Summary = news,
            TowardWar = p.Move is CounterweightMove.WarOnDominant or CounterweightMove.CallAllyToWar,
            ProposerId = p.House?.Leader?.StringId,
            RealmIds = new[] {p.Realm?.StringId, p.Other?.StringId, p.Dominant?.StringId}.Where(s => s != null).Distinct().ToList()
         });

         ModLog.Info($"[STRATEGY] {news} ({p.Move}, support {p.Support:0}.)");
      }

      /// <summary>cr.test all seam: forgets a realm's last step.</summary>
      internal void DebugForget(string realmId) => _lastStepDay.Remove(realmId);

      /// <summary>The houses of a realm that may file a step: free heads in the world, never the player's, never a mercenary.</summary>
      private static IEnumerable<Clan> Houses(Kingdom realm)
         => realm.Clans.Where(c => c != null && !c.IsEliminated && c != Clan.PlayerClan && !c.IsUnderMercenaryService
                                   && c.Leader != null && !c.Leader.IsPrisoner && IsPresent(c.Leader));

      /// <summary>The game's own decision for the step, or null when the game's own terms refuse it outright.</summary>
      private static KingdomDecision Build(CounterweightMove move, Clan house, Kingdom other, Kingdom dominant)
      {
         try
         {
            switch (move)
            {
               case CounterweightMove.WarOnDominant:
                  // The rules a lord pressed by the player answers to (peace too recent, a proposal waiting, the influence).
                  return WarRefusal(house, other) == null ? new DeclareWarDecision(house, other) : null;
               case CounterweightMove.PeaceWithWeaker:
               {
                  var diplomacy = Campaign.Current.Models.DiplomacyModel;
                  if (other.RulingClan == null || !diplomacy.IsPeaceSuitable(house.Kingdom, other)) return null;
                  // The game's own tribute; a negative one is a peace the game itself would not offer.
                  int tribute = diplomacy.GetDailyTributeToPay(house, other.RulingClan, out int days);

                  return tribute < 0 ? null : new MakePeaceKingdomDecision(house, other, tribute, days);
               }
               case CounterweightMove.AllianceAgainstDominant:
                  return new StartAllianceDecision(house, other);
               default:
                  return new ProposeCallToWarAgreementDecision(house, other, dominant);
            }
         }
         catch (Exception ex)
         {
            ModLog.Info($"[STRATEGY] {move} for {house?.Name} with {other?.Name} could not be built: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static bool Allowed(KingdomDecision d)
      {
         try { return d.IsAllowed() && d.CanMakeDecision(out _); }
         catch { return false; }
      }

      private static float Support(KingdomDecision d, Clan house)
      {
         try
         {
            return d switch {
               DeclareWarDecision w => w.CalculateSupport(house),
               MakePeaceKingdomDecision m => m.CalculateSupport(house),
               StartAllianceDecision a => a.CalculateSupport(house, out _),
               ProposeCallToWarAgreementDecision c => c.CalculateSupport(house),
               _ => 0f
            };
         }
         catch { return float.MinValue; }
      }
   }
}
