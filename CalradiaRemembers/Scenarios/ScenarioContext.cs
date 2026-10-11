// Code written by Gabriel Mailhot, 24/06/2026.
// The salient game objects a fired trigger carries — the hero(es) and faction(s) it is about — so the guard
// resolver and consequence executor can interpret keyword references ("Rhagaea", "Southern Empire", "PLAYER")
// against the real event. Pure logic (ScenarioSelector) decides WHICH rule fires; this is how the engine side
// answers "who/what is involved".
// 11/10/2026 (cr.testall scenario_death): IF_HERO_IS asked every hero of the event, so a rule on Rhagaea's death
// fired when she was the KILLER. The heroes a guard asks about are now the event's SUBJECTS (a death: the one who
// died; a marriage: both), and a reference names its best match (ScenarioReferenceMatch: an id before any name).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>The heroes and factions a fired trigger involves, with reference resolution by name or id.</summary>
   public sealed class ScenarioContext
   {
      /// <param name="heroes">Everyone the event involves, the principal first.</param>
      /// <param name="factions">The factions the event involves.</param>
      /// <param name="subjects">Who the event is ABOUT, for IF_HERO_IS; null means all of <paramref name="heroes" />.</param>
      public ScenarioContext(IReadOnlyList<Hero> heroes = null, IReadOnlyList<IFaction> factions = null, IReadOnlyList<Hero> subjects = null)
      {
         Heroes = heroes ?? Array.Empty<Hero>();
         Factions = factions ?? Array.Empty<IFaction>();
         Subjects = subjects ?? Heroes;
      }

      /// <summary>The heroes the event is about (victim/killer, the wed pair, …). The first is the primary.</summary>
      public IReadOnlyList<Hero> Heroes { get; }

      /// <summary>The factions the event is about (the warring/peace-making sides, the involved kingdoms).</summary>
      public IReadOnlyList<IFaction> Factions { get; }

      /// <summary>The heroes the event is about (a death: the one who died, not the killer; a marriage: both).</summary>
      public IReadOnlyList<Hero> Subjects { get; }

      /// <summary>The principal hero of the event, or null when the event is faction-only.</summary>
      public Hero PrimaryHero => Heroes.Count > 0 ? Heroes[0] : null;

      /// <summary>True when one of the involved heroes is the player.</summary>
      public bool InvolvesPlayer => InvolvesHero("PLAYER");

      /// <summary>True when one of the event's heroes matches <paramref name="reference" /> (name or id).</summary>
      public bool InvolvesHero(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return false;
         string r = reference.Trim();
         if (string.Equals(r, "PLAYER", StringComparison.OrdinalIgnoreCase))
            return Contains(Heroes, Hero.MainHero);

         return FindHero(Heroes, r) != null;
      }

      /// <summary>True when one of the event's SUBJECTS matches <paramref name="reference" /> (IF_HERO_IS).</summary>
      public bool IsAbout(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return false;
         string r = reference.Trim();
         if (string.Equals(r, "PLAYER", StringComparison.OrdinalIgnoreCase))
            return Contains(Subjects, Hero.MainHero);

         return FindHero(Subjects, r) != null;
      }

      /// <summary>
      ///   Resolves a hero reference: <c>PLAYER</c> → the main hero; otherwise the best match among the event's
      ///   own heroes, then any living hero, by StringId or by name (a "Rhagaea" matches "Rhagaea Pethros"). An
      ///   exact id wins over any name, wherever it is listed. Null when nothing matches.
      /// </summary>
      public Hero ResolveHero(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return null;
         string r = reference.Trim();
         if (string.Equals(r, "PLAYER", StringComparison.OrdinalIgnoreCase)) return Hero.MainHero;

         return FindHero(Heroes.Concat(Hero.AllAliveHeroes), r);
      }

      /// <summary>Resolves a faction reference: the best match among the event's factions, then every kingdom (an exact id first).</summary>
      public IFaction ResolveFaction(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return null;

         return FindFaction(Factions.Concat(Kingdom.All), reference.Trim());
      }

      private static bool Contains(IEnumerable<Hero> heroes, Hero target)
      {
         if (heroes == null || target == null) return false;
         foreach (Hero h in heroes)
            if (h == target)
               return true;

         return false;
      }

      // The best match by exact StringId, exact name, or a name containing the reference (case-insensitive), so an
      // author can write a short name ("Rhagaea") or, for a pack that works in every language, an id.
      private static Hero FindHero(IEnumerable<Hero> heroes, string r)
      {
         List<Hero> list = heroes?.Where(h => h != null).ToList() ?? new List<Hero>();
         int best = ScenarioReferenceMatch.BestIndex(list.Select(h => new ScenarioCandidate {Id = h.StringId, Name = h.Name?.ToString()}).ToList(), r);

         return best >= 0 ? list[best] : null;
      }

      private static IFaction FindFaction(IEnumerable<IFaction> factions, string r)
      {
         List<IFaction> list = factions?.Where(f => f != null).ToList() ?? new List<IFaction>();
         int best = ScenarioReferenceMatch.BestIndex(list.Select(f => new ScenarioCandidate {Id = f.StringId, Name = f.Name?.ToString()}).ToList(), r);

         return best >= 0 ? list[best] : null;
      }
   }
}
