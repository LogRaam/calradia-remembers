// Code written by Gabriel Mailhot, 17/06/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Logic.Relations;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

using NpcMemoryService.Core.Standing;

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Mapping
{
   /// <summary>
   ///   Builds a human-readable RELATIONSHIPS block for an NPC from live game state.
   ///   Uses the same relation data the vanilla encyclopedia displays (Friends / Enemies)
   ///   plus family and liege, so the block matches what a player can verify in-game.
   ///   Refreshed every session launch — not persisted as identity data.
   /// </summary>
   public static class RelationshipsBuilder
   {
      // Same rungs as the words the model reads (ThirdPartyDescriptor) and as the scheme backlash: one home since
      // 21/09/2026, same values.
      private const int EnemyThreshold = VanillaRelation.EnemyTier;
      private const int FriendThreshold = VanillaRelation.FriendTier;
      private const int MaxEntries = 6;

      public static string Build(Hero hero)
      {
         if (hero == null) return "";

         var sb = new StringBuilder();

         AppendLiege(sb, hero);
         AppendFriends(sb, hero);
         AppendEnemies(sb, hero);
         AppendHouses(sb, hero);
         AppendFamily(sb, hero);

         return sb.ToString().TrimEnd();
      }

      /// <summary>
      ///   The close family alone, the same words as the family part of <see cref="Build" />. The Compact prompt drops
      ///   the rest of RELATIONSHIPS and keeps this (fearente, Nexus, 29/10/2026: a brother invented a son).
      /// </summary>
      public static string BuildFamily(Hero hero)
      {
         if (hero == null) return "";

         var sb = new StringBuilder();
         AppendFamily(sb, hero);

         return sb.ToString().TrimEnd();
      }

      #region private

      /// <summary>
      ///   Recently-dead foes the NPC would still remember — a hero is dropped from the live enemy
      ///   scan the moment they die, so without this an NPC denies ever knowing a rival who just
      ///   fell (tester: a lord did not recognize a recently-killed enemy). Limited to deaths within
      ///   the last ~2 Calradian years so the list stays short and relevant.
      /// </summary>
      private static void AppendDeadRivals(StringBuilder sb, Hero hero)
      {
         double now = CampaignTime.Now.ToDays;

         List<Hero> deadRivals = Hero.DeadOrDisabledHeroes
                                     .Where(h => h is {IsHumanPlayerCharacter: false, IsAlive: false, DeathDay.ToDays: > 0} &&
                                                 now - h.DeathDay.ToDays >= 0 &&
                                                 now - h.DeathDay.ToDays <= 168 // ~2 Calradian years
                                                 &&
                                                 PairBondReader.CouldReach(hero, h, r => r <= EnemyThreshold) &&
                                                 PairBondReader.Points(hero, h) <= EnemyThreshold)
                                     .OrderByDescending(h => h.DeathDay.ToDays)
                                     .Take(4)
                                     .ToList();

         if (deadRivals.Count > 0)
            sb.AppendLine($"Recent rivals now dead (you remember them): " +
                          $"{string.Join(", ", deadRivals.Select(h => $"the late {h.Name}{DescribeDeath(h)}"))}.");
      }

      private static void AppendEnemies(StringBuilder sb, Hero hero)
      {
         var enemies = new List<Hero>();
         foreach (Hero hero1 in Hero.AllAliveHeroes
                                    .Where(h => h != hero && !h.IsHumanPlayerCharacter)
                                    .Where(h => PairBondReader.CouldReach(hero, h, r => r <= EnemyThreshold))
                                    .Select(h => (h, rel: PairBondReader.Points(hero, h)))
                                    .Where(x => x.rel <= EnemyThreshold)
                                    .OrderBy(x => x.rel)
                                    .Where(x => PairRegardReader.Assess(hero, x.h).Verdict != PairRegardVerdict.Friend)
                                    .Take(MaxEntries)
                                    .Select(x => x.h))
            enemies.Add(hero1);

         if (enemies.Count > 0)
            sb.AppendLine($"Enemies: {string.Join(", ", enemies.Select(WithHouse))}.");

         AppendDeadRivals(sb, hero);
      }

      private static void AppendFamily(StringBuilder sb, Hero hero)
      {
         var parts = new List<string>();
         // Windmolen (08/10/2026): the player took his first wife's mother as a second wife, and the first wife never
         // knew. A relative who is also the player's partner is marked so, and a partner knows the others by name.
         List<(Hero Hero, bool IsSpouse)> partners = PlayerPartners();
         string Partner(Hero h) => partners.Where(p => p.Hero == h).Select(p => PlayerPartnersText.KinNote(p.IsSpouse)).FirstOrDefault() ?? "";

         // Spouse, with their own parentage so in-laws are known (tester: a wife did not realize
         // her husband was the ruler's eldest son, and invented his family).
         if (hero.Spouse is {IsAlive: true})
            // How recent the wedding is, when CR saw it (raphareish, 01/10/2026: a bride of days spoke of years).
            parts.Add($"Spouse: {hero.Spouse.Name}{DescribeParentage(hero.Spouse)}{CaptiveClause(hero.Spouse)}" + SpouseTenure(hero));

         // A spouse who died: the game moves them from Spouse to the former spouses, so a widow read as never wed.
         List<Hero> lateSpouses = hero.ExSpouses?.Where(s => s is {IsDead: true}).ToList();
         if (lateSpouses is {Count: > 0})
            parts.Add($"Spouse now dead: {string.Join(", ", lateSpouses.Select(s => $"the late {s.Name}{DescribeDeath(s)}"))}");

         // Parents are listed whether alive or dead, but a deceased one is marked as such —
         // otherwise the NPC reads "Father: X" and speaks of a dead parent as living,
         // even denying a death the player witnessed.
         if (hero.Father != null)
            parts.Add($"Father: {FormatKin(hero.Father)}{Partner(hero.Father)}");

         if (hero.Mother != null)
            parts.Add($"Mother: {FormatKin(hero.Mother)}{Partner(hero.Mother)}");

         // Maifailun (07/10/2026): Lidia, wed into the Takhi, knew her father's name but not the house she was born
         // into, and claimed she was born a Takhi. EvilPoptart1987 (21/11/2026): a wife married before the campaign
         // began has her husband's clan as OriginClan (the engine stamps the first clan ever given), so the parents
         // decide then (BirthClanPolicy).
         BirthClanSource source = ResolveBirthClanSource(hero);
         Clan born = source switch {
            BirthClanSource.Origin => Marriage.HeroClanCompat.OriginClanOrNull(hero),
            BirthClanSource.Father => hero.Father?.Clan,
            BirthClanSource.Mother => hero.Mother?.Clan,
            _ => null
         };
         string birthHouse = BirthHouseText.Line(new BirthHouseFacts {
            CurrentClan = hero.Clan?.Name?.ToString(), BirthClan = born?.Name?.ToString(), BirthRealm = born?.Kingdom?.Name?.ToString(),
            ByMarriage = hero.Spouse != null || hero.ExSpouses?.Any() == true, HouseUnknown = source == BirthClanSource.MarriedInUnknownHouse
         });
         if (birthHouse != null) parts.Add(birthHouse);

         // Children, EACH with their sex named — otherwise the NPC misgenders them (tester: a
         // mother called her daughter her "son"). Co-parent is named when not the current spouse
         // so the LLM does not treat a step-child as a shared child (tester: a lord's son from
         // a deceased first wife was addressed as "our child" by the player-character spouse).
         // Dead children too (Maifailun via tashmetu, 15/10/2026: the player's wife Debana died in childbirth and her
         // father asked "Which Debana?", because a dead child was left out of his family altogether).
         // Said as a whole list, and "none" for a grown lord with none (fearente, 29/10/2026: a brother spoke of a son
         // "Lorgand" when he has a daughter). Lords only: a wanderer's past is the backstory they told, not the game's.
         List<Hero> children = hero.Children?.Where(c => c != null).ToList() ?? new List<Hero>();
         string childrenPart = ChildrenListText.Part(children.Select(c => DescribeChild(c, hero) + Partner(c)).ToList(), hero.IsLord && IsGrown(hero));
         if (childrenPart != null) parts.Add(childrenPart);


         // Siblings, named with sex, so the NPC knows their own brothers and sisters.
         List<Hero> siblings = hero.Siblings?.Where(s => s != null).ToList();
         if (siblings is {Count: > 0})
            parts.Add($"Siblings: {string.Join(", ", siblings.Select(s => DescribeRelative(s) + Partner(s)))}");

         if (parts.Count > 0)
            sb.AppendLine($"Family: {string.Join("; ", parts)}.");

         if (partners.Any(p => p.Hero == hero))
         {
            string others = PlayerPartnersText.OtherPartners(partners.Where(p => p.Hero != hero).Select(p => new PartnerFacts {
               Name = p.Hero.Name?.ToString(), IsSpouse = p.IsSpouse, KinToSubject = KinTo(hero, p.Hero)
            }).ToList());
            if (others != null) sb.AppendLine(others);

            // Maifailun via tashmetu (15/10/2026): a co-wife who died is known too, how and when.
            string late = PlayerPartnersText.LateSpouses(Hero.MainHero?.ExSpouses?.Where(s => s is {IsDead: true} && s != hero).Select(s => new PartnerFacts {
               Name = s.Name?.ToString(), DeathClause = DeathOnly(s), KinToSubject = KinTo(hero, s)
            }).ToList());
            if (late != null) sb.AppendLine(late);
         }
      }

      /// <summary>Which house the hero was born into, read from the engine's OriginClan and the parents. Internal: cr.test reads it.</summary>
      internal static BirthClanSource ResolveBirthClanSource(Hero hero)
      {
         Clan clan = hero?.Clan;
         if (clan == null) return BirthClanSource.None;

         Hero spouse = hero.Spouse ?? hero.ExSpouses?.Where(s => s != null).LastOrDefault();
         bool spouseOfBlood = spouse != null && (spouse.Father?.Clan == clan || spouse.Mother?.Clan == clan || clan.Leader == spouse);

         return BirthClanPolicy.Resolve(new BirthClanFacts {
            CurrentClan = clan.StringId, OriginClan = Marriage.HeroClanCompat.OriginClanOrNull(hero)?.StringId,
            FatherClan = hero.Father?.Clan?.StringId, MotherClan = hero.Mother?.Clan?.StringId,
            IsMarried = spouse != null, LeadsTheClan = clan.Leader == hero, SpouseIsOfTheClanByBlood = spouseOfBlood
         });
      }

      /// <summary>Of an age to have children, by the game's own coming of age.</summary>
      private static bool IsGrown(Hero hero)
      {
         try
         {
            return hero.Age >= Logic.Conditions.ComingOfAge.YearsOr(Campaign.Current?.Models?.AgeModel?.HeroComesOfAge);
         }
         catch
         {
            return hero.Age >= Logic.Conditions.ComingOfAge.EngineDefault;
         }
      }

      /// <summary>The player's living spouse and consorts (CR's own consort bond, or a committed bond).</summary>
      private static List<(Hero Hero, bool IsSpouse)> PlayerPartners()
      {
         var partners = new List<(Hero Hero, bool IsSpouse)>();
         Hero player = Hero.MainHero;
         if (player == null) return partners;
         // Death alone is the question: a partner held captive, or away, is still a partner.
         if (player.Spouse is {IsDead: false}) partners.Add((player.Spouse, true));

         var store = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return partners;
         foreach (string id in store.ListIds())
         {
            NpcProfile p = store.Get(id);
            if (p?.Romantic == null || !(p.Romantic.IsConsort || p.Romantic.Status == RomanticStatus.Committed)) continue;
            Hero h = Hero.FindFirst(x => x.StringId == id);
            if (h is {IsDead: false} && h != player && partners.All(x => x.Hero != h)) partners.Add((h, false));
         }

         return partners;
      }

      /// <summary>What <paramref name="other" /> is to <paramref name="hero" />, among the closest kin; null otherwise.</summary>
      private static string KinTo(Hero hero, Hero other)
      {
         if (other == hero.Mother) return "your mother";
         if (other == hero.Father) return "your father";
         if (hero.Children?.Contains(other) == true) return other.IsFemale ? "your daughter" : "your son";
         if (hero.Siblings?.Contains(other) == true) return other.IsFemale ? "your sister" : "your brother";

         return null;
      }

      private static void AppendFriends(StringBuilder sb, Hero hero)
      {
         var friends = new List<Hero>();
         // Believed only (PairBondPolicy, tashmetu 04/11/2026): a start drawn blind between strangers is no friendship.
         foreach (Hero hero1 in Hero.AllAliveHeroes.Where(h => h != hero && !h.IsHumanPlayerCharacter && PairBondReader.CouldReach(hero, h, r => r >= FriendThreshold))
                                    .Select(h => (h, rel: PairBondReader.Points(hero, h)))
                                    .Where(x => x.rel >= FriendThreshold)
                                    .OrderByDescending(x => x.rel)
                                    .Select(x => x.h)
                                    // A derived enemy, with a reason, is no friend (NPC_REGARD.md section 9, 17/10/2026)
                                    .Where(h => PairRegardReader.Assess(hero, h).Verdict != PairRegardVerdict.Enemy)
                                    .Take(MaxEntries)) friends.Add(hero1);

         if (friends.Count > 0)
            sb.AppendLine($"Friends: {string.Join(", ", friends.Select(WithHouse))}.");
      }

      // A friend or enemy named with their house (Lordfadooboo, Nexus, 03/11/2026: asked about the people a character had
      // opinions on, "they started listing names ive never seen to houses that ive never heard of"). A bare first name
      // left the model to supply the house itself.
      private static string WithHouse(Hero h) => HeroLabel.Describe(new HeroLabel.HeroNaming {Name = h.Name?.ToString(), ClanName = h.Clan?.Name?.ToString()});

      /// <summary>
      ///   How the speaker's house stands with others, said apart from the speaker's own friends and enemies, for someone
      ///   who does not lead their house. tashmetu (Nexus, 04/11/2026): in the base game "a younger son, a wife or a cousin
      ///   shares the leader's friends and enemies"; Gabriel (04/11/2026) wanted it as "une phrase distincte".
      /// </summary>
      private static void AppendHouses(StringBuilder sb, Hero hero)
      {
         Hero head = hero.Clan?.Leader;
         if (head == null || head == hero || hero.Clan == Clan.PlayerClan) return;

         var houses = new List<HouseStanding>();
         foreach (Clan other in Clan.All)
         {
            Hero theirs = other?.Leader;
            if (other == hero.Clan || other.IsEliminated || theirs == null || theirs == Hero.MainHero || !HeroPresence.IsInTheWorld(theirs)) continue;
            if (!PairBondReader.CouldReach(head, theirs, r => r >= FriendThreshold || r <= EnemyThreshold)) continue;

            houses.Add(new HouseStanding {House = $"House {other.Name}", Points = PairBondReader.Points(head, theirs)});
         }

         string line = PairBondText.HousesLine(head.Name?.ToString(), houses);
         if (line != null) sb.AppendLine(line);
      }

      private static void AppendLiege(StringBuilder sb, Hero hero)
      {
         Hero kingdomLeader = hero.Clan?.Kingdom?.Leader;
         if (kingdomLeader != null && kingdomLeader != hero)
         {
            sb.AppendLine(
               $"Liege: {kingdomLeader.Name}, ruler of {hero.Clan.Kingdom.Name}.");

            return;
         }

         Hero clanLeader = hero.Clan?.Leader;
         if (clanLeader != null && clanLeader != hero)
            sb.AppendLine($"Clan chief: {clanLeader.Name} leads your clan.");
      }

      /// <summary>
      ///   Names a child with their sex. When the child's other parent is not the NPC's current
      ///   spouse (e.g. a child from a deceased first wife), that parent is named inline so the
      ///   LLM understands the child is NOT shared with the current spouse.
      /// </summary>
      /// <summary>How long the marriage has lasted: the day CR saw it, or else the couple's eldest living child (fkasad, 10/10/2026).</summary>
      private static string SpouseTenure(Hero hero)
      {
         int? wed = WeddingDayBehavior.WeddedOn(hero);
         if (wed != null) return SpouseTenurePolicy.Clause(wed, (int) CampaignTime.Now.ToDays);

         Hero eldest = hero.Children?.Where(c => c is {IsDead: false} && (c.Father == hero.Spouse || c.Mother == hero.Spouse))
                                     .OrderByDescending(c => c.Age).FirstOrDefault();

         return eldest == null ? "" : SpouseTenurePolicy.LongStanding(eldest.Name?.ToString(), (int) eldest.Age);
      }

      private static string DescribeChild(Hero child, Hero npcHero)
      {
         string sex = child.IsFemale
            ? "female"
            : "male";
         Hero other = child.Father == npcHero
            ? child.Mother
            : child.Father;

         string baseName = child.IsAlive
            ? $"{child.Name} ({sex}, age {(int) child.Age}"
            : $"the late {child.Name} ({sex}";

         string after = child.IsAlive ? CaptiveClause(child) : DescribeDeath(child);

         if (other == null || other == npcHero.Spouse)
            return baseName + ")" + after;

         string coParent = other.IsAlive
            ? other.Name.ToString()
            : $"the late {other.Name}";

         return $"{baseName}, by {coParent})" + after;
      }

      /// <summary>
      ///   The captivity clause for a kinsman named in the block: empty when free, a plain "currently held
      ///   prisoner" when captive elsewhere, and one naming the player as the captor when the player's own
      ///   clan holds them, so an NPC can never truthfully deny a captivity the player caused.
      /// </summary>
      private static string CaptiveClause(Hero h)
         => h == null
            ? ""
            : CaptiveKinDescriber.Marker(h.IsPrisoner, CourtActionResolver.IsHeldByPlayerClan(h));

      /// <summary>
      ///   How and how long ago a dead relative died — " (who fell in battle 6 days ago)".
      ///   Without this the LLM invents both ("three winters ago" for a father who died
      ///   last week in a siege — tester report). Empty when the game has no usable
      ///   death record (pre-campaign deaths often carry no DeathDay).
      /// </summary>
      private static string DescribeDeath(Hero kin)
      {
         try
         {
            double day = kin.DeathDay.ToDays;
            Hero player = Hero.MainHero;
            return KinDeathDescriber.Clause(new KinDeathFacts {
               Cause = kin.DeathMark.ToString(),
               DaysAgo = day > 0 ? CampaignTime.Now.ToDays - day : (double?) null,
               WasPlayersSpouse = player != null && player != kin && player.ExSpouses?.Contains(kin) == true,
               IsFemale = kin.IsFemale
            });
         }
         catch
         {
            return "";
         }
      }

      /// <summary>How and when a hero died, without saying they were wed to the player (the line already says so).</summary>
      internal static string DeathOnly(Hero kin)
      {
         try
         {
            double day = kin.DeathDay.ToDays;
            return KinDeathDescriber.Clause(new KinDeathFacts {Cause = kin.DeathMark.ToString(), DaysAgo = day > 0 ? CampaignTime.Now.ToDays - day : (double?) null});
         }
         catch
         {
            return "";
         }
      }

      /// <summary>" (son of X)" / " (daughter of X)" — a relative's parentage, for in-law awareness.</summary>
      private static string DescribeParentage(Hero h)
      {
         Hero parent = h.Father ?? h.Mother;

         if (parent == null) return "";
         string rel = h.IsFemale
            ? "daughter"
            : "son";

         return $" ({rel} of {parent.Name})";
      }

      /// <summary>Names a relative with their sex (so they are never misgendered), marking the deceased.</summary>
      private static string DescribeRelative(Hero h)
      {
         string sex = h.IsFemale
            ? "female"
            : "male";

         return h.IsAlive
            ? $"{h.Name} ({sex}, age {(int) h.Age}){CaptiveClause(h)}"
            : $"the late {h.Name} ({sex}){DescribeDeath(h)}";
      }

      /// <summary>Names a relative, marking the deceased as "the late …" so the NPC speaks of them accurately.</summary>
      private static string FormatKin(Hero kin)
         => kin.IsAlive
            // Eisendrache2 (19/11/2026): a living father read only "(age 61)", and lords said he fell at Pendraic.
            ? Logic.ParentLineText.Living(kin.Name?.ToString(), (int) kin.Age) + CaptiveClause(kin)
            : $"the late {kin.Name}{DescribeDeath(kin)}";

      #endregion
   }
}