// Code written by Gabriel Mailhot, 30/10/2026.
// The strategist asks the player for help (Gabriel, 30/10/2026, "go" on the second increment). Once a week, with the
// lords' strategy switched on, a few house heads who know the player turn to them with an aim of their own
// (StrategistAimPolicy): beat the head of a rival house grown too strong who rides for a realm at war with theirs, or
// beat the forces of the realm grown too strong. The aim is an ordinary CrAims aim under its own source tag, so it is
// voiced in conversation and may bring the lord to the player through the world's initiative, as any aim does; the
// war duty still sets it aside on a ruler it summons. Its deed is put at the head of the lord's grounded quest menu
// (ViableQuestScanner), so the task he asks for is the very one the quest factory issues and the deed tracker
// verifies. The aim falls as soon as the deed can no longer be done (the target fallen, peace made) or once the task
// has been given.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Aims;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Logic.Strategy;
using CalradiaRemembers.Strategy;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Aims
{
   /// <summary>The strategists' aims toward the player: seeded weekly, pruned when their deed no longer stands, offered as a real task.</summary>
   internal static class StrategistAims
   {
      /// <summary>The source tag of these aims: voiced like another mod's (CR's own war duty aims are not), and evicted by the war duty.</summary>
      internal const string Source = "CalradiaRemembers.Strategy";

      private const string LordPrefix = "quest:attack_lord:";
      private const string RealmPrefix = "quest:attack_faction:";

      /// <summary>One weekly pass: aims whose deed fell are retracted, then the open places are filled.</summary>
      internal static void Weekly(int today)
      {
         AimsBehavior aims = AimsBehavior.Instance;
         if (aims == null) return;

         Prune(aims);

         int standing = aims.Book.All.Count(a => a.SourceMod == Source);
         int open = StrategistAimPolicy.OpenPlaces(standing);
         if (open == 0) return;

         Kingdom dominant = LordStrategyBehavior.DominantRealm();
         var deeds = new Dictionary<string, (StrategistDeed Deed, Hero Rival)>();
         var givers = new List<StrategistGiverFacts>();
         foreach (Hero head in HouseHeads())
         {
            Hero rival = FindHero(SchemeStore.SelectOvershadowingTargetFor(head));
            StrategistDeed? deed = StrategistAimPolicy.ChooseDeed(RivalFacts(head, rival), RealmFacts(head, dominant));
            if (deed == null) continue;

            deeds[head.StringId] = (deed.Value, rival);
            givers.Add(GiverFacts(head, aims));
         }

         foreach (string id in StrategistAimPolicy.Choose(givers, open))
         {
            Hero giver = FindHero(id);
            (StrategistDeed deed, Hero rival) = deeds[id];
            StandingAim aim = Compose(giver, deed, rival, dominant, today);
            AimAdmission admitted = aims.Register(aim);
            if (admitted == AimAdmission.Accepted)
               ModLog.Info($"[STRATEGY] {giver.Name} turns to the player: {deed} ({aim.MissionId}).");
         }
      }

      /// <summary>
      ///   The deed of this lord's strategist aim, as the first entry of his grounded quest menu, or none when he holds
      ///   no such aim or its deed no longer stands. The entry names the target exactly as the quest factory binds it.
      /// </summary>
      internal static IEnumerable<QuestMenuEntry> MenuEntriesFor(Hero giver)
      {
         StandingAim aim = giver == null ? null : AimsBehavior.Instance?.Book.ForNpc(giver.StringId);
         if (aim == null || aim.SourceMod != Source || !Read(aim, out StrategistDeed deed, out Hero rival, out Kingdom realm)) yield break;
         if (!Stands(giver, deed, rival, realm)) yield break;

         StrategistAimWording w = Wording(deed, rival, realm);
         yield return new QuestMenuEntry {
            TypeToken = StrategistAimPolicy.TypeToken(deed), TargetField = StrategistAimPolicy.TargetField(deed), TargetName = w.TargetName, Cue = StrategistAimPolicy.MenuCue(w)
         };
      }

      /// <summary>Retracts every strategist aim whose deed no longer stands, or whose task the lord has already given.</summary>
      internal static int Prune(AimsBehavior aims)
      {
         int retracted = 0;
         foreach (StandingAim aim in aims.Book.All.Where(a => a.SourceMod == Source).ToList())
         {
            Hero giver = FindHero(aim.NpcId);
            bool valid = giver != null && Read(aim, out StrategistDeed deed, out Hero rival, out Kingdom realm) && Stands(giver, deed, rival, realm);
            bool given = giver != null && TaskGiven(giver, aim);
            if (valid && !given) continue;

            aims.Retract(Source, aim.Id);
            retracted++;
            ModLog.Info($"[STRATEGY] {giver?.Name}'s aim {aim.MissionId} withdrawn: {(given ? "the task was given" : "the deed no longer stands")}.");
         }

         return retracted;
      }

      /// <summary>The aim a lord would hold for this deed (also the self-test seam).</summary>
      internal static StandingAim Compose(Hero giver, StrategistDeed deed, Hero rival, Kingdom dominant, int today)
      {
         StrategistAimWording w = Wording(deed, rival, dominant);

         return new StandingAim {
            Id = "strategy:" + giver.StringId,
            NpcId = giver.StringId,
            SourceMod = Source,
            Want = StrategistAimPolicy.Want(w),
            PlayerPart = StrategistAimPolicy.PlayerPart(w),
            // Not a MissionBoard id: the deed is the grounded menu's own task (MenuEntriesFor), real and verified, so the
            // lord may ask outright. The id also carries the target for the menu and the pruning.
            MissionId = deed == StrategistDeed.StrikeTheRival ? LordPrefix + rival.StringId : RealmPrefix + dominant.StringId,
            Channels = AimChannels.Conversation | AimChannels.Audience,
            ExpiresOnDay = today + StrategistAimPolicy.AimDays
         };
      }

      private static IEnumerable<Hero> HouseHeads()
         => Clan.All.Where(c => c != null && !c.IsEliminated && c.Kingdom != null && c != Clan.PlayerClan)
                    .Select(c => c.Leader)
                    .Where(h => h != null && h != Hero.MainHero && !h.IsPrisoner && LordStrategyBehavior.IsPresent(h));

      private static StrategistGiverFacts GiverFacts(Hero head, AimsBehavior aims)
      {
         NpcProfile profile = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(head.StringId);

         return new StrategistGiverFacts {
            Id = head.StringId,
            IsHouseHead = head.Clan?.Leader == head,
            KnowsThePlayer = profile != null,
            RelationWithPlayer = Hero.MainHero == null ? 0 : head.GetRelation(Hero.MainHero),
            AtWarWithPlayer = Hero.MainHero?.MapFaction != null && FactionManager.IsAtWarAgainstFaction(head.MapFaction, Hero.MainHero.MapFaction),
            HoldsAnAim = aims.Book.ForNpc(head.StringId) != null,
            HasOutstandingTask = profile?.ActiveQuests.Any(q => q.IsOutstanding) == true,
            IsPlayerSide = head.Clan == Clan.PlayerClan
         };
      }

      private static StrategistTargetFacts RivalFacts(Hero giver, Hero rival)
         => rival == null
            ? null
            : new StrategistTargetFacts {
               Stands = !rival.IsPrisoner && LordStrategyBehavior.IsPresent(rival),
               AtWarWithGiversRealm = giver.MapFaction != null && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, rival.MapFaction),
               IsPlayersOwn = rival == Hero.MainHero || rival.Clan == Clan.PlayerClan || rival.MapFaction == Hero.MainHero?.MapFaction
            };

      private static StrategistTargetFacts RealmFacts(Hero giver, Kingdom realm)
         => realm == null
            ? null
            : new StrategistTargetFacts {
               Stands = !realm.IsEliminated,
               AtWarWithGiversRealm = giver.MapFaction != null && giver.MapFaction != realm && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, realm),
               IsPlayersOwn = realm == Hero.MainHero?.MapFaction
            };

      private static bool Stands(Hero giver, StrategistDeed deed, Hero rival, Kingdom realm)
         => giver.Clan?.Leader == giver && LordStrategyBehavior.IsPresent(giver)
            && StrategistAimPolicy.DeedStands(deed == StrategistDeed.StrikeTheRival ? RivalFacts(giver, rival) : RealmFacts(giver, realm));

      private static StrategistAimWording Wording(StrategistDeed deed, Hero rival, Kingdom realm)
         => deed == StrategistDeed.StrikeTheRival
            ? new StrategistAimWording {Deed = deed, TargetName = rival?.Name?.ToString(), RivalHouse = rival?.Clan?.Name?.ToString(), RivalRealm = rival?.MapFaction?.Name?.ToString()}
            : new StrategistAimWording {Deed = deed, TargetName = realm?.Name?.ToString()};

      private static bool Read(StandingAim aim, out StrategistDeed deed, out Hero rival, out Kingdom realm)
      {
         deed = StrategistDeed.BreakTheDominant;
         rival = null;
         realm = null;
         string m = aim?.MissionId ?? "";
         if (m.StartsWith(LordPrefix, StringComparison.Ordinal))
         {
            deed = StrategistDeed.StrikeTheRival;
            rival = FindHero(m.Substring(LordPrefix.Length));

            return rival != null;
         }

         if (!m.StartsWith(RealmPrefix, StringComparison.Ordinal)) return false;

         string id = m.Substring(RealmPrefix.Length);
         realm = Kingdom.All.FirstOrDefault(k => k.StringId == id);

         return realm != null;
      }

      /// <summary>The lord has given the player this very task (an attack on the aim's target, still on his books).</summary>
      private static bool TaskGiven(Hero giver, StandingAim aim)
      {
         NpcProfile profile = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(giver.StringId);
         if (profile == null || !Read(aim, out StrategistDeed deed, out Hero rival, out Kingdom realm)) return false;

         return deed == StrategistDeed.StrikeTheRival
            ? profile.ActiveQuests.Any(q => q.Type == QuestType.AttackLord && q.TargetHeroId == rival.StringId && q.IsOutstanding)
            : profile.ActiveQuests.Any(q => q.Type == QuestType.AttackFaction && q.TargetFactionId == realm.StringId && q.IsOutstanding);
      }

      private static Hero FindHero(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);
   }
}
