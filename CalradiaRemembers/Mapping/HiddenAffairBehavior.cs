// Code written by Gabriel Mailhot, 01/11/2026.
// Hidden affairs between lords (Gabriel's "go", 01/11/2026), ruled by HiddenAffairPolicy. It writes the same fields the player's
// partner's lover uses, so the Heart page, the secret lever and expose_secret read it with no second system, and picks the lover
// with LoverSelector. It keeps its own ledger because OpenPartnerLoverBehavior's weekly pass speaks of any
// TakenLoverId as a betrayal of the player: PartnersWithLover leaves these out through IsHidden. No Status is set: SecretLover
// means the player's own bond to SecretLoverGossipBehavior. No trysts and no children: it is a fact the game holds.
// tashmetu (Nexus, 03/11/2026), for Romance & Seduction: one lord should never carry an affair in each mod. Gabriel wanted CR
// to depend on no mod, so another mod registers a veto through CrRomance and hears each affair begin and end; CR names none.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using MissionResult = CalradiaRemembers.Logic.Companions.MissionResult;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>What a companion's inquiry brought back.</summary>
   internal sealed class AffairFinding
   {
      /// <summary>The line of their report.</summary>
      public string Line { get; set; }

      /// <summary>The married one, when the affair was found.</summary>
      public string SubjectId { get; set; }
   }

   /// <summary>Lets a lord take a hidden lover now and then, keeps the handful standing, and answers an inquiry.</summary>
   public sealed class HiddenAffairBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_hidden_affairs";

      public HiddenAffairBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static HiddenAffairBehavior Instance { get; private set; }

      /// <summary>The vetoes other mods registered against an affair: the married lord first, then the lover.</summary>
      internal static readonly PairVetoBook<Hero> Vetoes = new();

      /// <summary>An affair began (true) or ended (false): the married lord's id, then the lover's. CrRomance relays it to other mods.</summary>
      internal static event Action<string, string, bool> Changed;

      /// <summary>Every affair standing (never null).</summary>
      public HiddenAffairLedger Ledger { get; internal set; } = new HiddenAffairLedger();

      private static NpcMemoryCampaignBehavior Memory => NpcMemoryCampaignBehavior.Instance;

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[AFFAIR] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[AFFAIR] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            Ledger = HiddenAffairLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>True when this hero's lover belongs to this ledger, not to the player's partner path.</summary>
      internal bool IsHidden(string heroId) => Ledger.Affairs.Any(a => a.SubjectId == heroId);

      /// <summary>The married one this hero is the hidden lover of, once the player knows, or null.</summary>
      internal Hero KnownSubjectOf(Hero lover)
      {
         HiddenAffair affair = lover == null ? null : Ledger.Affairs.FirstOrDefault(a => a.LoverId == lover.StringId);
         if (affair == null || Memory?.Store?.Get(affair.SubjectId)?.Romantic?.LoverKnownToPlayer != true) return null;

         return Find(affair.SubjectId);
      }

      /// <summary>The lord these words name, for a companion sent to look into one. Never the player, nor the one sent.</summary>
      internal static Hero LordNamed(string about, Hero companion)
         => string.IsNullOrWhiteSpace(about)
            ? null
            : Hero.AllAliveHeroes.FirstOrDefault(h => h.IsLord && h != Hero.MainHero && h != companion && SwayGroundingPolicy.PlayerSpokeOf(about, h.Name?.ToString()));

      /// <summary>Begins an affair: the fields every reader knows, and the ledger. False when a marriage or a store is missing.</summary>
      internal bool Begin(Hero subject, Hero lover, int today)
      {
         if (Memory?.Store == null || subject == null || lover == null || subject.Spouse == null) return false;

         NpcProfile profile = Memory.Store.Get(subject.StringId) ?? HeroProfileMapper.FromHero(subject);
         profile.Romantic ??= new RomanticProfile();
         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = true;
         Memory.Store.Set(profile);
         Ledger.Affairs.Add(new HiddenAffair {SubjectId = subject.StringId, LoverId = lover.StringId, Day = today});
         ModLog.Info($"[AFFAIR] {subject.Name} took {lover.Name} as a hidden lover, behind {subject.Spouse.Name}.");
         Changed?.Invoke(subject.StringId, lover.StringId, true);

         return true;
      }

      /// <summary>Ends an affair and clears the fields, so no reader goes on calling them lovers.</summary>
      internal void End(HiddenAffair affair)
      {
         if (Ledger.Affairs.Remove(affair)) Changed?.Invoke(affair.SubjectId, affair.LoverId, false);
         NpcProfile profile = Memory?.Store?.Get(affair.SubjectId);
         if (profile?.Romantic == null || profile.Romantic.TakenLoverId != affair.LoverId) return;

         profile.Romantic.TakenLoverId = null;
         profile.Romantic.LoverIsSecret = false;
         profile.Romantic.LoverKnownToPlayer = false;
         Memory.Store.Set(profile);
         ModLog.Info($"[AFFAIR] the affair of {affair.SubjectId} and {affair.LoverId} is over.");
      }

      /// <summary>
      ///   Whether every mod that registered a veto lets these two begin an affair. A refusal or a failing veto is logged;
      ///   a veto that throws never refuses.
      /// </summary>
      internal static bool OtherModsAllow(Hero subject, Hero lover)
      {
         PairVetoVerdict veto = Vetoes.Ask(subject, lover);
         foreach (string fault in veto.Faults) ModLog.Error($"[AFFAIR] an affair veto failed and was passed over: {fault}");
         if (veto.Refused) ModLog.Info($"[AFFAIR] no affair: {veto.Source} forbids it for {subject?.Name} and {lover?.Name} ({veto.Reason}).");

         return !veto.Refused;
      }

      /// <summary>The player told it: a secret spent is no longer a hidden affair.</summary>
      internal void OnExposed(string subjectId)
      {
         HiddenAffair affair = Ledger.Affairs.FirstOrDefault(a => a.SubjectId == subjectId);
         if (affair != null) End(affair);
      }

      /// <summary>The player has heard the report: the lever and the telling read this field from now on.</summary>
      internal void MarkKnown(string subjectId, Hero teller)
      {
         HiddenAffair affair = Ledger.Affairs.FirstOrDefault(a => a.SubjectId == subjectId);
         NpcProfile profile = affair == null ? null : Memory?.Store?.Get(subjectId);
         if (profile?.Romantic == null) return;

         profile.Romantic.LoverKnownToPlayer = true;
         Memory.Store.Set(profile);
         ModLog.Info($"[AFFAIR] the player knows of {subjectId} and {affair.LoverId}.");

         if (teller == null) return;

         NpcProfile told = Memory.Store.Get(teller.StringId) ?? HeroProfileMapper.FromHero(teller);
         told.Events.Add(new NotableEvent(Today, NotableEventType.Other, $"I found out for {Hero.MainHero.Name} that {Name(subjectId)} and {Name(affair.LoverId)} are lovers in secret."));
         Memory.Store.Set(told);
      }

      /// <summary>What a companion finds on looking into this lord, on either side of an affair.</summary>
      internal AffairFinding Inquire(Hero lord, MissionResult result)
      {
         HiddenAffair affair = Enabled ? Ledger.Of(lord?.StringId) : null;
         Hero subject = Find(affair?.SubjectId);
         AffairInquiry inquiry = HiddenAffairPolicy.Inquire(result, subject != null);
         var names = new HiddenAffairNames {Subject = subject?.Name?.ToString(), Lover = Name(affair?.LoverId), Spouse = subject?.Spouse?.Name?.ToString()};

         return new AffairFinding {
            Line = HiddenAffairPolicy.ReportLine(inquiry, lord?.Name?.ToString(), names),
            SubjectId = inquiry == AffairInquiry.Found ? subject?.StringId : null
         };
      }

      /// <summary>What this character carries into a conversation about their own affair, or null.</summary>
      internal string PromptFor(Hero hero)
      {
         HiddenAffair affair = ModSettings.AdultContentLevel == AdultContentLevel.Off ? null : Ledger.Of(hero?.StringId);
         Hero subject = Find(affair?.SubjectId);
         if (subject?.Spouse == null) return null;

         return HiddenAffairPolicy.PromptBlock(Name(hero.StringId == affair.SubjectId ? affair.LoverId : affair.SubjectId), subject.Spouse.Name?.ToString());
      }

      private static bool Enabled => ModSettings.HiddenAffairs && ModSettings.AdultContentLevel != AdultContentLevel.Off;

      private static Hero Find(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h != null && h.StringId == id);

      private static string Name(string id) => Find(id)?.Name?.ToString();

      private void OnWeeklyTick()
      {
         SafeRun.Step("HiddenAffairBehavior.Review", () => Review(Today));
         if (Enabled) SafeRun.Step("HiddenAffairBehavior.TryBegin", () => TryBegin(Today));
      }

      private void Review(int today)
      {
         foreach (HiddenAffair affair in Ledger.Affairs.ToList())
         {
            Hero subject = Find(affair.SubjectId);
            Hero lover = Find(affair.LoverId);
            var span = new HiddenAffairSpan {
               BothInTheWorld = HeroPresence.IsInTheWorld(subject) && HeroPresence.IsInTheWorld(lover),
               StillMarried = HeroPresence.IsInTheWorld(subject?.Spouse) && subject.Spouse != Hero.MainHero,
               LoverStillFree = lover?.Spouse == null,
               AgeDays = today - affair.Day
            };
            if (HiddenAffairPolicy.IsOver(span)) End(affair);
         }
      }

      // One lord drawn a week, so the realm's handful fills slowly whatever the number of lords.
      private void TryBegin(int today)
      {
         var candidates = Hero.AllAliveHeroes.Select(h => (Hero: h, Facts: Candidate(h))).Where(c => c.Hero.IsLord && HiddenAffairPolicy.MayTake(c.Facts)).ToList();
         if (candidates.Count == 0) return;

         (Hero subject, HiddenAffairCandidate facts) = candidates[MBRandom.RandomInt(candidates.Count)];
         if (!HiddenAffairPolicy.Begins(Ledger.Affairs.Count, HiddenAffairPolicy.ChancePercent(facts), MBRandom.RandomInt(100))) return;

         NpcProfile profile = Memory?.Store?.Get(subject.StringId) ?? HeroProfileMapper.FromHero(subject);
         if (profile.Romantic == null) return;

         Hero lover = LoverSelector.Best(subject, profile);
         if (lover != null && Ledger.Of(lover.StringId) == null && Memory?.Store?.Get(lover.StringId)?.Romantic?.Status != RomanticStatus.SecretLover
             && OtherModsAllow(subject, lover)) Begin(subject, lover, today);
      }

      private HiddenAffairCandidate Candidate(Hero hero)
      {
         Hero player = Hero.MainHero;
         RomanticProfile romantic = Memory?.Store?.Get(hero.StringId)?.Romantic;

         return new HiddenAffairCandidate {
            IsMarriedAdult = !hero.IsChild && HeroPresence.IsInTheWorld(hero.Spouse),
            IsThePlayersOwn = hero == player || hero.Spouse == player,
            IsOfPlayerHousehold = Clan.PlayerClan != null && hero.Clan == Clan.PlayerClan,
            HasLover = !string.IsNullOrEmpty(romantic?.TakenLoverId) || Ledger.Of(hero.StringId) != null,
            IsPlayersLover = romantic?.Status == RomanticStatus.SecretLover,
            Honor = hero.GetTraitLevel(DefaultTraits.Honor),
            FeelingForSpouse = hero.Spouse == null ? 0 : PairBondReader.Points(hero, hero.Spouse)
         };
      }
   }
}
