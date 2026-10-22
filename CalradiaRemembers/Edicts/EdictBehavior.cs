// Code written by Gabriel Mailhot, 22/10/2026.
// Edicts in the campaign (Gabriel, 21/10/2026: "ville et royaume. Les PNJ peuvent aussi. Un edit peut etre conteste.").
// Keeps the ledger (one additive save key; an old save loads with none), decrees an edict once the pure EdictPolicy
// allows it, and each day ends the ones whose term ran out or whose enactor lost the right, and lets a burdened, restless
// town contest an edict until it forces it off. Once a week, with the MCM switch on, the world's lords answer their towns'
// sorest needs and rulers their realms'. Every decree, contest and repeal is told as realm news, so the player meets it.
// What an edict DOES is applied by EdictModelPatches through the game's own settlement models. Presence and the realm's
// news are supplied at startup (NpcMemoryServiceSubmodule), the way WarLevyDesk is, so edicts never know the map layer.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Edicts;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Edicts
{
   /// <summary>One piece of realm news an edict makes (a decree, a contest, a repeal), told through <see cref="EdictBehavior.News" />.</summary>
   public sealed class EdictNews
   {
      /// <summary>The day.</summary>
      public int Day { get; set; }

      /// <summary>Where it happened (a town), or the realm's ruler's seat for a realm edict; null when unknown.</summary>
      public string PlaceId { get; set; }

      /// <summary>The realm it is told in.</summary>
      public string RealmId { get; set; }

      /// <summary>The player decreed the edict.</summary>
      public bool ByPlayer { get; set; }

      /// <summary>How loud it is (0 to 100).</summary>
      public int Magnitude { get; set; }

      /// <summary>What is said.</summary>
      public string Summary { get; set; }

      /// <summary>Whoever decreed the edict.</summary>
      public string EnactorId { get; set; }
   }

   /// <summary>The campaign's edicts: decreed, applied (through EdictModelPatches), contested, ended.</summary>
   public sealed class EdictBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_Edicts_v1bytes";

      /// <summary>TUNE: the magnitude of the news a decree, a contest and a repeal make (0 to 100).</summary>
      internal const int DecreeNews = 30, ContestNews = 35, RepealNews = 40;

      /// <summary>TUNE: the longest name an edict may carry (the model writes it; the tooltip shows it).</summary>
      internal const int NameMaxLength = 48;

      public static EdictBehavior Instance;

      /// <summary>Whether a hero is in the world (alive, not disabled); supplied at startup as HeroPresence.IsInTheWorld.</summary>
      internal static Func<Hero, bool> IsPresent = h => h != null && !h.IsDead;

      /// <summary>Tells the realm a piece of edict news; supplied at startup as a WorldEvent. Unset, nothing is told.</summary>
      internal static Action<EdictNews> News;

      public EdictBehavior()
      {
         Instance = this;
      }

      /// <summary>The ledger (read by the models' patches, the prompt and the console).</summary>
      public EdictLedger Ledger { get; private set; } = new();

      internal static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () => SafeRun.Step("Edicts.Daily", () => Daily(Today)));
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => SafeRun.Step("Edicts.AttachToModels", EdictModelPatches.EnsureOnActiveModels));
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Ledger, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[EDICT] serialize failed, persisting none this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[EDICT] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               // A save written before edicts: none.
            }

            try
            {
               Ledger = bytes is {Length: > 0} ? JsonConvert.DeserializeObject<EdictLedger>(Encoding.UTF8.GetString(bytes)) ?? new EdictLedger() : new EdictLedger();
               int dropped = Ledger.DropUnknownKinds();
               if (dropped > 0) ModLog.Info($"[EDICT] {dropped} edict(s) of a kind this build does not know were dropped on load.");
            }
            catch (Exception ex)
            {
               Ledger = new EdictLedger();
               ModLog.Error($"[EDICT] load failed, edicts dropped for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      // ── Which edict holds where ─────────────────────────────────────────

      /// <summary>The edict that holds on this town today, or null.</summary>
      public Edict HoldingOn(Town town)
         => town?.Settlement == null || !town.IsTown ? null : Ledger.HoldingOn(town.Settlement.StringId, RealmOf(town.Settlement)?.StringId);

      internal static Kingdom RealmOf(Settlement s) => s?.OwnerClan?.Kingdom;

      // ── Decreeing ───────────────────────────────────────────────────────

      /// <summary>
      ///   Why this hero cannot decree this edict here; null when he can. A town edict targets a town settlement, a realm
      ///   edict the realm the hero rules (the target is then ignored).
      /// </summary>
      public string WhyNot(Hero enactor, EdictScope scope, Settlement town)
      {
         if (enactor == null) return "No one to decree it.";
         if (scope == EdictScope.Realm)
         {
            Kingdom realm = enactor.Clan?.Kingdom;

            return EdictPolicy.WhyNot(new EdictDecreeFacts {
               Scope = scope, IsATown = true, HoldsTheRight = realm != null && realm.Leader == enactor, OneAlreadyHolds = Ledger.OfRealm(realm?.StringId) != null,
               DaysSinceLastEnded = SinceLast(realm?.StringId), Influence = enactor.Clan?.Influence ?? 0f
            });
         }

         return EdictPolicy.WhyNot(new EdictDecreeFacts {
            Scope = scope, IsATown = town?.IsTown == true, HoldsTheRight = HoldsTown(enactor, town), OneAlreadyHolds = Ledger.OwnOf(town?.StringId) != null,
            DaysSinceLastEnded = SinceLast(town?.StringId), Influence = enactor.Clan?.Influence ?? 0f
         });
      }

      /// <summary>
      ///   Decrees the edict when the rules allow it: the influence is spent, the ledger records it, the realm hears of it.
      ///   Returns the edict, or null with the reason in <paramref name="refusal" />.
      /// </summary>
      public Edict Decree(Hero enactor, EdictScope scope, Settlement town, EdictKind kind, int days, string name, out string refusal)
      {
         refusal = WhyNot(enactor, scope, town);
         if (refusal != null) return null;

         int today = Today;
         Kingdom realm = enactor.Clan?.Kingdom;
         string targetId = scope == EdictScope.Realm ? realm?.StringId : town?.StringId;
         EdictEffect effect = EdictCatalog.Of(kind);
         Edict e = Ledger.Decree(new Edict {
            Kind = kind, Scope = scope, TargetId = targetId, EnactorId = enactor.StringId, Name = NameFor(name, effect, enactor),
            ByPlayer = enactor == Hero.MainHero, EnactedDay = today, EndDay = today + EdictCatalog.Term(days)
         });

         try { ChangeClanInfluenceAction.Apply(enactor.Clan, -EdictCatalog.InfluenceCost(scope)); }
         catch (Exception ex) { ModLog.Error($"[EDICT] influence spend failed (edict kept): {ex.Message}"); }

         string where = scope == EdictScope.Realm ? $"all the towns of {realm?.Name}" : town?.Name?.ToString();
         Tell(e, today, scope == EdictScope.Realm ? null : town, DecreeNews,
            $"{enactor.Name} decreed {e.Name} for {where}: {effect.Describe()}, for {EdictCatalog.Term(days)} days.");
         ModLog.Info($"[EDICT] {e.Id} '{e.Name}' ({effect.Key}) decreed by {enactor.Name} for {where}, until day {e.EndDay}.");

         return e;
      }

      /// <summary>Withdraws an edict in force (its enactor's choice, or the console).</summary>
      public bool Withdraw(Edict e, string why)
      {
         if (e == null || e.Status != EdictStatus.Active) return false;
         Ledger.End(e, EdictStatus.Withdrawn, Today);
         ModLog.Info($"[EDICT] {e.Id} '{e.Name}' withdrawn: {why}");

         return true;
      }

      private static string NameFor(string written, EdictEffect effect, Hero enactor)
      {
         string clean = new string((written ?? "").Where(c => c != '{' && c != '}' && !char.IsControl(c)).ToArray()).Trim();
         if (clean.Length > NameMaxLength) clean = clean.Substring(0, NameMaxLength).Trim();

         return clean.Length > 0 ? clean : $"{enactor.Name}'s {effect.Title.ToLowerInvariant()}";
      }

      private int? SinceLast(string targetId)
      {
         int? ended = string.IsNullOrEmpty(targetId) ? null : Ledger.LastEndedOn(targetId);

         return ended.HasValue ? Today - ended.Value : (int?) null;
      }

      internal static bool HoldsTown(Hero hero, Settlement town) => hero != null && town?.OwnerClan != null && town.OwnerClan.Leader == hero;

      // ── Each day ────────────────────────────────────────────────────────

      /// <summary>Ends what ran out or lapsed, lets each burdened town grieve, and once a week lets the world's lords decree.</summary>
      internal void Daily(int today)
      {
         foreach (Edict e in Ledger.InForce().ToList())
         {
            Hero enactor = Hero.FindFirst(h => h.StringId == e.EnactorId);
            EdictStatus standing = EdictPolicy.Standing(new EdictStandingFacts {
               EnactorPresent = IsPresent(enactor), EnactorStillHoldsTheRight = StillHolds(enactor, e), Today = today, EndDay = e.EndDay
            });
            if (standing != EdictStatus.Active)
            {
               Ledger.End(e, standing, today);
               ModLog.Info($"[EDICT] {e.Id} '{e.Name}' {standing.ToString().ToLowerInvariant()} on day {today}.");
               continue;
            }

            foreach (Town town in TownsUnder(e)) Grieve(e, town, today);
         }

         if (today % 7 == 0 && ModSettings.EnableNpcEdicts) WorldDecrees(today);
      }

      private static bool StillHolds(Hero enactor, Edict e)
      {
         if (enactor == null) return false;
         if (e.Scope == EdictScope.Realm) return enactor.Clan?.Kingdom?.StringId == e.TargetId && enactor.Clan.Kingdom.Leader == enactor;

         return HoldsTown(enactor, Settlement.Find(e.TargetId));
      }

      /// <summary>The towns this edict holds on today.</summary>
      internal IEnumerable<Town> TownsUnder(Edict e)
      {
         if (e.Scope == EdictScope.Town)
         {
            Town t = Settlement.Find(e.TargetId)?.Town;
            if (t != null) yield return t;
            yield break;
         }

         Kingdom realm = Kingdom.All.FirstOrDefault(k => k.StringId == e.TargetId);
         if (realm == null) yield break;
         foreach (Town t in realm.Fiefs.Where(f => f.IsTown && HoldingOn(f) == e)) yield return t;
      }

      private void Grieve(Edict e, Town town, int today)
      {
         string id = town.Settlement.StringId;
         e.Grievance.TryGetValue(id, out int g);
         EdictGrievanceStep step = EdictPolicy.Grieve(new EdictGrievanceFacts {
            BurdensThePeople = e.Effect().BurdensThePeople, Loyalty = town.Loyalty, Grievance = g, AlreadyContested = e.ContestedIn.Contains(id)
         });
         e.Grievance[id] = step.Grievance;

         switch (step.Unrest)
         {
            case EdictUnrest.Contested:
               e.ContestedIn.Add(id);
               Tell(e, today, town.Settlement, ContestNews, $"The people of {town.Name} openly contest {e.Name}; its notables speak against it.");
               ModLog.Info($"[EDICT] {e.Id} '{e.Name}' contested in {town.Name} (loyalty {town.Loyalty:0}).");
               break;
            case EdictUnrest.Repealed:
               if (e.Scope == EdictScope.Town) Ledger.End(e, EdictStatus.Repealed, today);
               else e.RepealedIn.Add(id);
               Tell(e, today, town.Settlement, RepealNews, $"The people of {town.Name} forced {e.Name} off: it no longer holds there.");
               ModLog.Info($"[EDICT] {e.Id} '{e.Name}' forced off in {town.Name}.");
               break;
         }
      }

      // ── The world's lords ───────────────────────────────────────────────

      internal void WorldDecrees(int today)
      {
         foreach (Kingdom realm in Kingdom.All.Where(k => !k.IsEliminated && k.Leader != null))
         {
            int worldEdicts = Ledger.InForce().Count(e => !e.ByPlayer && (e.TargetId == realm.StringId || RealmOf(Settlement.Find(e.TargetId))?.StringId == realm.StringId));

            Hero ruler = realm.Leader;
            if (ruler != Hero.MainHero && IsPresent(ruler) && EdictCounselPolicy.DecreesThisWeek(MBRandom.RandomFloat, worldEdicts)
                && EdictCounselPolicy.AnswerForRealm(realm.Fiefs.Where(f => f.IsTown).Select(Facts).ToList()) is EdictKind realmKind
                && Decree(ruler, EdictScope.Realm, null, realmKind, EdictCatalog.DefaultDays, null, out _) != null)
               worldEdicts++;

            foreach (Town town in realm.Fiefs.Where(f => f.IsTown))
            {
               Hero lord = town.OwnerClan?.Leader;
               if (lord == null || lord == Hero.MainHero || !IsPresent(lord)) continue;
               if (!EdictCounselPolicy.DecreesThisWeek(MBRandom.RandomFloat, worldEdicts)) continue;
               if (EdictCounselPolicy.Answer(Facts(town)) is not EdictKind kind) continue;
               if (Decree(lord, EdictScope.Town, town.Settlement, kind, EdictCatalog.DefaultDays, null, out _) != null) worldEdicts++;
            }
         }
      }

      /// <summary>The town as its lord sees it when weighing an edict.</summary>
      internal static EdictTownFacts Facts(Town town)
      {
         float foodChange = 0f;
         try { foodChange = Campaign.Current.Models.SettlementFoodModel.CalculateTownFoodStocksChange(town).ResultNumber; }
         catch
         {
            // A model that throws reads as stocks holding.
         }

         return new EdictTownFacts {
            TownId = town.Settlement.StringId,
            FoodDaysLeft = foodChange < 0f ? town.FoodStocks / -foodChange : float.MaxValue,
            Loyalty = town.Loyalty, Security = town.Security, Prosperity = town.Prosperity, Militia = town.Militia,
            AtWar = RealmOf(town.Settlement) is Kingdom k && Kingdom.All.Any(o => o != k && k.IsAtWarWith(o)),
            HolderGold = town.OwnerClan?.Leader?.Gold ?? 0
         };
      }

      // ── News ────────────────────────────────────────────────────────────

      private static void Tell(Edict e, int today, Settlement place, int magnitude, string summary)
      {
         try
         {
            Kingdom realm = e.Scope == EdictScope.Realm ? Kingdom.All.FirstOrDefault(k => k.StringId == e.TargetId) : RealmOf(place);
            News?.Invoke(new EdictNews {
               Day = today, PlaceId = place?.StringId ?? realm?.Leader?.CurrentSettlement?.StringId, RealmId = realm?.StringId, ByPlayer = e.ByPlayer,
               Magnitude = magnitude, Summary = summary, EnactorId = e.EnactorId
            });
         }
         catch (Exception ex) { ModLog.Error($"[EDICT] news failed: {ex.Message}"); }
      }
   }
}
