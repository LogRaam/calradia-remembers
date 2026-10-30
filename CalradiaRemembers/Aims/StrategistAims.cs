// Code written by Gabriel Mailhot, 30/10/2026.
// The strategist asks the player for help (Gabriel, 30/10/2026, "go" on the second increment, then "go pour le pillage
// et le siège"). Once a week, with the lords' strategy switched on, a few house heads who know the player turn to them
// with an aim of their own (StrategistAimPolicy): against the head of a rival house grown too strong who rides for a
// realm at war with theirs, or against the realm grown too strong, they ask the player to beat it in the field, raid one
// of its villages, or storm one of its strongholds. The aim is an ordinary CrAims aim under its own source tag, so it is
// voiced in conversation and may bring the lord to the player through the world's initiative, as any aim does; the war
// duty still sets it aside on a ruler it summons. Its deed is put at the head of the lord's grounded quest menu
// (ViableQuestScanner.LeadingEntries), so the task he asks for is the very one the quest factory issues and the deed
// tracker verifies. The aim falls as soon as the deed can no longer be done (the target fallen, the place lost or
// looted, peace made) or once the task has been given.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Aims;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Logic.Strategy;
using CalradiaRemembers.Strategy;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Aims
{
   /// <summary>The strategists' aims toward the player: seeded weekly, pruned when their deed no longer stands, offered as a real task.</summary>
   internal static class StrategistAims
   {
      /// <summary>The source tag of these aims: voiced like another mod's (CR's own war duty aims are not), and evicted by the war duty.</summary>
      internal const string Source = "CalradiaRemembers.Strategy";

      private const string Prefix = "quest:";

      /// <summary>A deed read back from an aim: whom, what, where.</summary>
      internal sealed class Deed
      {
         public StrategistDeed Whom { get; set; }
         public StrategistAct Act { get; set; }
         public Hero Rival { get; set; }
         public Kingdom Realm { get; set; }
         public Settlement Place { get; set; }
      }

      /// <summary>One weekly pass: aims whose deed fell are retracted, then the open places are filled.</summary>
      internal static void Weekly(int today)
      {
         AimsBehavior aims = AimsBehavior.Instance;
         if (aims == null) return;

         Prune(aims);

         int open = StrategistAimPolicy.OpenPlaces(aims.Book.All.Count(a => a.SourceMod == Source));
         if (open == 0) return;

         Kingdom dominant = LordStrategyBehavior.DominantRealm();
         var deeds = new Dictionary<string, Deed>();
         var givers = new List<StrategistGiverFacts>();
         foreach (Hero head in HouseHeads())
         {
            Deed deed = DeedFor(head, dominant);
            if (deed == null) continue;

            deeds[head.StringId] = deed;
            givers.Add(GiverFacts(head, aims));
         }

         foreach (string id in StrategistAimPolicy.Choose(givers, open))
         {
            Hero giver = FindHero(id);
            StandingAim aim = Compose(giver, deeds[id], today);
            if (aims.Register(aim) == AimAdmission.Accepted)
               ModLog.Info($"[STRATEGY] {giver.Name} turns to the player: {aim.MissionId}.");
         }
      }

      /// <summary>
      ///   What this lord would ask of the player now, or null: whom (his rival, else the dominant realm) and what (a
      ///   battle, a raid, a siege), with the place, all read from the live world.
      /// </summary>
      internal static Deed DeedFor(Hero head, Kingdom dominant)
      {
         Hero rival = FindHero(SchemeStore.SelectOvershadowingTargetFor(head));
         StrategistDeed? whom = StrategistAimPolicy.ChooseDeed(RivalFacts(head, rival), RealmFacts(head, dominant));
         if (whom == null) return null;

         var deed = new Deed {Whom = whom.Value, Rival = whom == StrategistDeed.StrikeTheRival ? rival : null, Realm = whom == StrategistDeed.BreakTheDominant ? dominant : null};
         Settlement village = Places(deed, false).OrderBy(s => DistanceFrom(head, s)).FirstOrDefault();
         List<Settlement> strongholds = Places(deed, true).ToList();
         Settlement besieged = strongholds.FirstOrDefault(s => s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction == head.MapFaction);
         Settlement stronghold = besieged ?? strongholds.OrderBy(s => DistanceFrom(head, s)).FirstOrDefault();

         StrategistAct? act = StrategistAimPolicy.ChooseAct(new StrategistActFacts {
            SiegeUnderWay = besieged != null,
            HoldsAStronghold = stronghold != null,
            HoldsAVillage = village != null,
            InTheField = deed.Whom == StrategistDeed.StrikeTheRival
               ? rival.PartyBelongedTo != null && !rival.IsPrisoner
               : dominant.Clans.Any(c => c != null && !c.IsEliminated && c.WarPartyComponents.Count > 0),
            GiverMercy = head.GetTraitLevel(DefaultTraits.Mercy)
         });
         if (act == null) return null;

         deed.Act = act.Value;
         deed.Place = act == StrategistAct.Raid ? village : act == StrategistAct.Siege ? stronghold : null;

         return deed;
      }

      /// <summary>
      ///   The deed of this lord's strategist aim, as the first entry of his grounded quest menu, or none when he holds
      ///   no such aim or its deed no longer stands. The entry names the target exactly as the quest factory binds it.
      /// </summary>
      internal static IEnumerable<QuestMenuEntry> MenuEntriesFor(Hero giver)
      {
         StandingAim aim = giver == null ? null : AimsBehavior.Instance?.Book.ForNpc(giver.StringId);
         if (aim == null || aim.SourceMod != Source) yield break;

         Deed deed = Read(aim);
         if (deed == null || !Stands(giver, deed)) yield break;

         StrategistAimWording w = Wording(deed);
         yield return new QuestMenuEntry {
            TypeToken = StrategistAimPolicy.TypeToken(w), TargetField = StrategistAimPolicy.TargetField(w), TargetName = StrategistAimPolicy.TargetName(w), Cue = StrategistAimPolicy.MenuCue(w)
         };
      }

      /// <summary>Retracts every strategist aim whose deed no longer stands, or whose task the lord has already given.</summary>
      internal static int Prune(AimsBehavior aims)
      {
         int retracted = 0;
         foreach (StandingAim aim in aims.Book.All.Where(a => a.SourceMod == Source).ToList())
         {
            Hero giver = FindHero(aim.NpcId);
            Deed deed = Read(aim);
            bool valid = giver != null && deed != null && Stands(giver, deed);
            bool given = valid && TaskGiven(giver, deed);
            if (valid && !given) continue;

            aims.Retract(Source, aim.Id);
            retracted++;
            ModLog.Info($"[STRATEGY] {giver?.Name}'s aim {aim.MissionId} withdrawn: {(given ? "the task was given" : "the deed no longer stands")}.");
         }

         return retracted;
      }

      /// <summary>The aim a lord would hold for this deed (also the self-test seam).</summary>
      internal static StandingAim Compose(Hero giver, Deed deed, int today)
      {
         StrategistAimWording w = Wording(deed);
         string whom = deed.Whom == StrategistDeed.StrikeTheRival ? "r:" + deed.Rival.StringId : "k:" + deed.Realm.StringId;

         return new StandingAim {
            Id = "strategy:" + giver.StringId,
            NpcId = giver.StringId,
            SourceMod = Source,
            Want = StrategistAimPolicy.Want(w),
            PlayerPart = StrategistAimPolicy.PlayerPart(w),
            // Not a MissionBoard id: the deed is the grounded menu's own task (MenuEntriesFor), real and verified, so the
            // lord may ask outright. The id also carries whom, what and where, for the menu and the pruning.
            MissionId = $"{Prefix}{StrategistAimPolicy.TypeToken(w)}:{whom}:{deed.Place?.StringId}",
            Channels = AimChannels.Conversation | AimChannels.Audience,
            ExpiresOnDay = today + StrategistAimPolicy.AimDays
         };
      }

      /// <summary>The deed an aim holds, read back from its MissionId, or null when it no longer resolves.</summary>
      internal static Deed Read(StandingAim aim)
      {
         string[] parts = (aim?.MissionId ?? "").Split(':');
         if (parts.Length < 5 || parts[0] + ":" != Prefix) return null;

         var deed = new Deed {
            Act = parts[1] switch {
               "raid_village" => StrategistAct.Raid,
               "siege" => StrategistAct.Siege,
               _ => StrategistAct.Battle
            }
         };
         if (parts[2] == "r")
         {
            deed.Whom = StrategistDeed.StrikeTheRival;
            deed.Rival = FindHero(parts[3]);
            if (deed.Rival == null) return null;
         }
         else
         {
            deed.Whom = StrategistDeed.BreakTheDominant;
            deed.Realm = Kingdom.All.FirstOrDefault(k => k.StringId == parts[3]);
            if (deed.Realm == null) return null;
         }

         if (deed.Act == StrategistAct.Battle) return deed;

         deed.Place = Settlement.All.FirstOrDefault(s => s.StringId == parts[4]);

         return deed.Place == null ? null : deed;
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

      /// <summary>The target's villages not already looted (false) or its towns and castles (true).</summary>
      private static IEnumerable<Settlement> Places(Deed deed, bool strongholds)
         => Settlement.All.Where(s => s != null
                                      && (strongholds ? s.IsTown || s.IsCastle : s.IsVillage && s.Village?.VillageState != Village.VillageStates.Looted)
                                      && Belongs(deed, s));

      private static bool Belongs(Deed deed, Settlement s)
         => deed.Whom == StrategistDeed.StrikeTheRival ? deed.Rival?.Clan != null && s.OwnerClan == deed.Rival.Clan : deed.Realm != null && s.MapFaction == deed.Realm;

      private static float DistanceFrom(Hero head, Settlement s)
      {
         Settlement home = head.HomeSettlement;

         return home == null ? 0f : home.GetPosition2D.Distance(s.GetPosition2D);
      }

      private static bool Stands(Hero giver, Deed deed)
      {
         if (giver.Clan?.Leader != giver || !LordStrategyBehavior.IsPresent(giver)) return false;

         StrategistTargetFacts target = deed.Whom == StrategistDeed.StrikeTheRival ? RivalFacts(giver, deed.Rival) : RealmFacts(giver, deed.Realm);
         if (!StrategistAimPolicy.DeedStands(target)) return false;

         return deed.Act switch {
            StrategistAct.Raid => deed.Place?.IsVillage == true && deed.Place.Village?.VillageState != Village.VillageStates.Looted && Belongs(deed, deed.Place),
            StrategistAct.Siege => deed.Place != null && (deed.Place.IsTown || deed.Place.IsCastle) && Belongs(deed, deed.Place),
            _ => true
         };
      }

      private static StrategistAimWording Wording(Deed deed)
         => deed.Whom == StrategistDeed.StrikeTheRival
            ? new StrategistAimWording {
               Deed = deed.Whom, Act = deed.Act, Place = deed.Place?.Name?.ToString(),
               TargetName = deed.Rival?.Name?.ToString(), RivalHouse = deed.Rival?.Clan?.Name?.ToString(), RivalRealm = deed.Rival?.MapFaction?.Name?.ToString()
            }
            : new StrategistAimWording {Deed = deed.Whom, Act = deed.Act, Place = deed.Place?.Name?.ToString(), TargetName = deed.Realm?.Name?.ToString()};

      /// <summary>The lord has given the player this very task, still on his books.</summary>
      private static bool TaskGiven(Hero giver, Deed deed)
      {
         NpcProfile profile = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(giver.StringId);
         if (profile == null) return false;

         string place = deed.Place?.Name?.ToString();

         return profile.ActiveQuests.Any(q => q.IsOutstanding && deed.Act switch {
            StrategistAct.Raid => q.Type == QuestType.RaidVillage && string.Equals(q.TargetSettlement, place, StringComparison.OrdinalIgnoreCase),
            StrategistAct.Siege => q.Type == QuestType.Siege && string.Equals(q.TargetSettlement, place, StringComparison.OrdinalIgnoreCase),
            _ => deed.Whom == StrategistDeed.StrikeTheRival
               ? q.Type == QuestType.AttackLord && q.TargetHeroId == deed.Rival.StringId
               : q.Type == QuestType.AttackFaction && q.TargetFactionId == deed.Realm.StringId
         });
      }

      private static Hero FindHero(string id) => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id);
   }
}
