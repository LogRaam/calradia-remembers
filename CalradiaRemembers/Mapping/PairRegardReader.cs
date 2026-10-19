// Code written by Gabriel Mailhot, 17/10/2026.
// The engine side of PairRegardPolicy (docs/design/NPC_REGARD.md, section 9): what lies between a speaker and someone
// the player names, read from the game and the mod's ledgers, each thing weighed and dated. Gabriel, 17/10/2026: every
// friend or enemy carries its reason. Sources whose sides are clear in the code only: a kinsman the other killed (the
// engine's death mark), the speaker's own grudges and appreciations of the other, the campaign's written past (authored:
// actors and those harmed), a capture and a defeat in battle recorded by the game (captive, then captor; winner, then
// loser). A battle or a war naming both, with no side known, is left out.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads what lies between two characters into PairRegardFacts, and assesses it.</summary>
   internal static class PairRegardReader
   {
      // An authored kind in which the actor did the one named "harmed" a good turn rather than a wrong.
      private static readonly IReadOnlyCollection<string> KindnessKinds = new[] {"kindness", "rescue"};

      // Authored kinds whose sign cannot be read from actors and harmed alone.
      private static readonly IReadOnlyCollection<string> UnsignedKinds = new[] {"marriage", "peace", "war", "rebellion", "debt"};

      /// <summary>What <paramref name="speaker" /> thinks of <paramref name="other" />, and why. Never throws.</summary>
      public static PairRegard Assess(Hero speaker, Hero other)
      {
         try
         {
            if (speaker == null || other == null || speaker == other || other == Hero.MainHero) return new PairRegard();

            return PairRegardPolicy.Assess(Facts(speaker, other, (int) CampaignTime.Now.ToDays));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[REGARD] could not read what lies between {speaker?.Name} and {other?.Name}: {ex.Message}");

            return new PairRegard();
         }
      }

      /// <summary>The facts, for cr.testall.</summary>
      internal static PairRegardFacts Facts(Hero speaker, Hero other, int today)
      {
         int daysInYear = Math.Max(1, CampaignTime.DaysInSeason * CampaignTime.SeasonsInYear);
         var age = new PastMentionRequest {Today = today, DaysInYear = daysInYear};
         int YearsAgo(int day) => Math.Max(0, (today - day) / daysInYear);

         var traces = new List<PairRegardTrace>();
         AddKinKilled(traces, speaker, other, today, age);
         AddGrudgesAndKindnesses(traces, speaker, other, today);
         AddSharedPast(traces, speaker, other, age, YearsAgo);

         bool parentOrChild = speaker.Father == other || speaker.Mother == other || other.Father == speaker || other.Mother == speaker;
         bool sibling = speaker.Siblings != null && speaker.Siblings.Contains(other);

         return new PairRegardFacts {
            IsSpouse = speaker.Spouse == other,
            IsParentOrChild = parentOrChild,
            IsSibling = sibling,
            SameClan = speaker.Clan != null && speaker.Clan == other.Clan,
            RealmsAtWar = speaker.MapFaction != null && other.MapFaction != null && FactionManager.IsAtWarAgainstFaction(speaker.MapFaction, other.MapFaction),
            Traces = traces,
            OutsideTraces = CrPairRegard.TracesFor(speaker, other, today)
         };
      }

      // A kinsman of the speaker whom the other killed: the engine's own death mark names the killer.
      private static void AddKinKilled(List<PairRegardTrace> traces, Hero speaker, Hero other, int today, PastMentionRequest age)
      {
         foreach ((Hero kin, string word) in Kin(speaker))
         {
            if (kin == null || !kin.IsDead || kin.DeathMarkKillerHero != other) continue;

            bool executed = kin.DeathMark == KillCharacterAction.KillCharacterActionDetail.Executed;
            int deathDay = (int) kin.DeathDay.ToDays;
            bool dated = deathDay > 0 && deathDay <= today;
            int years = dated ? (today - deathDay) / Math.Max(1, age.DaysInYear) : 0;
            string when = dated ? $" ({PastOnMentionPolicy.AgeOf(deathDay, age)})" : "";

            string their = speaker.IsFemale ? "her" : "his";
            traces.Add(new PairRegardTrace {
               Weight = executed ? PairRegardPolicy.KinExecuted() : PairRegardPolicy.KinKilledInBattle(years),
               Line = executed ? $"had your {word} {kin.Name} executed{when}" : $"killed your {word} {kin.Name}{when}",
               PageLine = executed ? $"had {their} {word} {kin.Name} executed{when}" : $"killed {their} {word} {kin.Name}{when}",
               KnownToPlayer = true
            });
         }
      }

      private static IEnumerable<(Hero, string)> Kin(Hero h)
      {
         yield return (h.Father, "father");
         yield return (h.Mother, "mother");
         if (h.Spouse != null) yield return (h.Spouse, h.Spouse.IsFemale ? "wife" : "husband");
         if (h.ExSpouses != null)
            foreach (Hero ex in h.ExSpouses)
               yield return (ex, ex != null && ex.IsFemale ? "wife" : "husband");
         if (h.Children != null)
            foreach (Hero c in h.Children)
               yield return (c, c != null && c.IsFemale ? "daughter" : "son");
         if (h.Siblings != null)
            foreach (Hero s in h.Siblings)
               yield return (s, s != null && s.IsFemale ? "sister" : "brother");
      }

      // The speaker's own grudges against the other and his appreciations of them, as the ledgers weigh them today.
      private static void AddGrudgesAndKindnesses(List<PairRegardTrace> traces, Hero speaker, Hero other, int today)
      {
         if (GrudgeBehavior.Instance != null)
         {
            Logic.Grudges.TraitFacts traits = GrudgeBridge.BuildTraitFacts(speaker);
            foreach (Grudge g in GrudgeBehavior.Instance.LiveGrudgesFor(speaker, today).Where(g => g.TargetId == other.StringId))
               traces.Add(new PairRegardTrace {
                  Weight = -GrudgePolicy.EffectiveMagnitude(g, today, traits), Line = $"your grudge: {g.CauseSummary}", KnownToPlayer = g.KnownToPlayer,
                  PageLine = $"a grudge: {ShownText.Of(g.CauseSummary)}"
               });
         }

         if (AppreciationBehavior.Instance != null)
         {
            Logic.Appreciations.TraitFacts traits = AppreciationBridge.BuildTraitFacts(speaker);
            foreach (Appreciation a in AppreciationBehavior.Instance.LiveAppreciationsFor(speaker, today).Where(a => a.TargetId == other.StringId))
               traces.Add(new PairRegardTrace {
                  Weight = AppreciationPolicy.EffectiveMagnitude(a, today, traits), Line = $"you owe them: {a.CauseSummary}", KnownToPlayer = a.KnownToPlayer,
                  PageLine = $"a debt of gratitude: {ShownText.Of(a.CauseSummary)}"
               });
         }
      }

      // What the world remembers of the two: the campaign's written past by its actors and harmed, and the game's own
      // captures and battles by their order (captive then captor; winner then loser).
      private static void AddSharedPast(List<PairRegardTrace> traces, Hero speaker, Hero other, PastMentionRequest age, Func<int, int> yearsAgo)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return;

         string me = speaker.StringId, them = other.StringId;

         foreach (WorldEvent e in store.EventsNamingBoth(me, them))
         {
            if (string.IsNullOrWhiteSpace(e.Summary)) continue;

            string line = $"{e.Summary.Trim().TrimEnd('.')} ({PastOnMentionPolicy.AgeOf(e.Day, age)})";
            bool known = !e.KnownOnlyToThoseInvolved;
            int years = yearsAgo(e.Day);
            List<string> actors = e.ActorIds ?? new List<string>();
            List<string> harmed = e.AggrievedActorIds ?? new List<string>();

            int weight = 0;
            SharedPastKind past = SharedPastPolicy.KindOf(e.Status);
            if (past != SharedPastKind.None)
            {
               // A generated past (SharedPastBehavior): the kind's own weight for this side, aged from today.
               if (actors.Count >= 2 && (actors[0] == me || actors[1] == me))
                  weight = SharedPastPolicy.WeightToward(past, actors[0] == me, years);
            }
            else if (e.IsAuthored)
            {
               string kind = AuthoredKind(e);
               if (UnsignedKinds.Contains(kind)) continue;

               if (actors.Contains(them) && harmed.Contains(me))
                  weight = KindnessKinds.Contains(kind) ? PairRegardPolicy.HelpedInThePast(years) : PairRegardPolicy.WrongedInThePast(years);
               else if (kind == "feud" && actors.Contains(me) && actors.Contains(them))
                  weight = PairRegardPolicy.WrongedInThePast(years);
            }
            else if (e.Status == LongTermHistoryPolicy.CaptureStatus && actors.Count >= 2 && actors[0] == me && actors[1] == them)
               weight = PairRegardPolicy.TakenCaptive(years);
            else if (e.Type == WorldEventType.Battle && actors.Count >= 2 && actors[0] == them && actors[1] == me)
               weight = PairRegardPolicy.BeatenInBattle(years);

            if (weight != 0) traces.Add(new PairRegardTrace {Weight = weight, Line = line, KnownToPlayer = known});
         }
      }

      /// <summary>
      ///   Everyone something real ties to <paramref name="hero" />: the killers of his kin, the targets of his grudges and
      ///   appreciations, and those named with him in the world's events (news and long-term memory). The people whose
      ///   regard his encyclopedia page weighs beyond the vanilla lists. In the world, adult lords and wanderers only,
      ///   never the player (whose own page is read from personal regard).
      /// </summary>
      public static IReadOnlyList<Hero> TiedTo(Hero hero)
      {
         var ids = new HashSet<string>();
         if (hero == null) return Array.Empty<Hero>();

         try
         {
            foreach ((Hero kin, string _) in Kin(hero))
               if (kin?.DeathMarkKillerHero != null) ids.Add(kin.DeathMarkKillerHero.StringId);

            int today = (int) CampaignTime.Now.ToDays;
            if (GrudgeBehavior.Instance != null) ids.UnionWith(GrudgeBehavior.Instance.LiveGrudgesFor(hero, today).Select(g => g.TargetId));
            if (AppreciationBehavior.Instance != null) ids.UnionWith(AppreciationBehavior.Instance.LiveAppreciationsFor(hero, today).Select(a => a.TargetId));

            foreach (WorldEvent e in WorldEventStore.Instance?.EventsNaming(hero.StringId) ?? Array.Empty<WorldEvent>())
            {
               if (e.ActorIds != null) ids.UnionWith(e.ActorIds);
               if (e.AggrievedActorIds != null) ids.UnionWith(e.AggrievedActorIds);
            }
         }
         catch (Exception ex) { ModLog.Error($"[REGARD] could not gather who is tied to {hero.Name}: {ex.Message}"); }

         ids.Remove(hero.StringId);

         return ids.Where(id => !string.IsNullOrEmpty(id))
                   .Select(id => Hero.FindFirst(h => h.StringId == id))
                   .Where(h => h != null && h != Hero.MainHero && HeroPresence.IsInTheWorld(h) && !h.IsChild && (h.IsLord || h.IsWanderer))
                   .ToList();
      }

      // The authored kind back from the world event AuthoredHistoryBehavior.ToWorldEvent made of it.
      private static string AuthoredKind(WorldEvent e)
      {
         if (e.Status != null && e.Status.StartsWith(AuthoredHistoryBehavior.AuthoredStatusPrefix, StringComparison.Ordinal))
            return e.Status.Substring(AuthoredHistoryBehavior.AuthoredStatusPrefix.Length);
         if (e.Status == LongTermHistoryPolicy.CaptureStatus) return "capture";
         if (e.Status == LongTermHistoryPolicy.FellInBattleStatus) return "fell_in_battle";

         return e.Type switch
         {
            WorldEventType.Battle => "battle",
            WorldEventType.SettlementCaptured => "fief_taken",
            WorldEventType.Execution => "execution",
            WorldEventType.Marriage => "marriage",
            WorldEventType.Divorce => "divorce",
            WorldEventType.WarDeclared => "war",
            WorldEventType.PeaceDeclared => "peace",
            WorldEventType.Rebellion => "rebellion",
            WorldEventType.Rivalry => "scheme",
            _ => "other"
         };
      }
   }
}
