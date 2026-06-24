// Code written by Gabriel Mailhot, 24/06/2026.
// The salient game objects a fired trigger carries — the hero(es) and faction(s) it is about — so the guard
// resolver and consequence executor can interpret keyword references ("Rhagaea", "Southern Empire", "PLAYER")
// against the real event. Pure logic (ScenarioSelector) decides WHICH rule fires; this is how the engine side
// answers "who/what is involved".

#region

using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>The heroes and factions a fired trigger involves, with reference resolution by name or id.</summary>
   public sealed class ScenarioContext
   {
      public ScenarioContext(IReadOnlyList<Hero> heroes = null, IReadOnlyList<IFaction> factions = null)
      {
         Heroes = heroes ?? Array.Empty<Hero>();
         Factions = factions ?? Array.Empty<IFaction>();
      }

      /// <summary>The heroes the event is about (victim/killer, the wed pair, …). The first is the primary.</summary>
      public IReadOnlyList<Hero> Heroes { get; }

      /// <summary>The factions the event is about (the warring/peace-making sides, the involved kingdoms).</summary>
      public IReadOnlyList<IFaction> Factions { get; }

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

      /// <summary>
      ///   Resolves a hero reference: <c>PLAYER</c> → the main hero; otherwise a hero among the event's own
      ///   heroes first, then any living hero, by exact StringId or by name (a "Rhagaea" matches "Rhagaea
      ///   Pethros"). Null when nothing matches.
      /// </summary>
      public Hero ResolveHero(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return null;
         string r = reference.Trim();
         if (string.Equals(r, "PLAYER", StringComparison.OrdinalIgnoreCase)) return Hero.MainHero;

         return FindHero(Heroes, r) ?? FindHero(Hero.AllAliveHeroes, r);
      }

      /// <summary>Resolves a faction reference among the event's factions first, then any kingdom, by id or name.</summary>
      public IFaction ResolveFaction(string reference)
      {
         if (string.IsNullOrWhiteSpace(reference)) return null;
         string r = reference.Trim();

         IFaction inEvent = FindFaction(Factions, r);
         if (inEvent != null) return inEvent;

         foreach (Kingdom kingdom in Kingdom.All)
            if (Matches(kingdom.StringId, kingdom.Name?.ToString(), r))
               return kingdom;

         return null;
      }

      private static bool Contains(IEnumerable<Hero> heroes, Hero target)
      {
         if (heroes == null || target == null) return false;
         foreach (Hero h in heroes)
            if (h == target)
               return true;

         return false;
      }

      private static Hero FindHero(IEnumerable<Hero> heroes, string r)
      {
         if (heroes == null) return null;
         foreach (Hero h in heroes)
            if (h != null && Matches(h.StringId, h.Name?.ToString(), r))
               return h;

         return null;
      }

      private static IFaction FindFaction(IEnumerable<IFaction> factions, string r)
      {
         if (factions == null) return null;
         foreach (IFaction f in factions)
            if (f != null && Matches(f.StringId, f.Name?.ToString(), r))
               return f;

         return null;
      }

      // Match by exact StringId, by exact name, or by name containing the reference (case-insensitive) so an
      // author can write a recognizable short name ("Rhagaea") instead of a cryptic StringId.
      private static bool Matches(string id, string name, string r)
      {
         if (!string.IsNullOrEmpty(id) && string.Equals(id, r, StringComparison.OrdinalIgnoreCase)) return true;
         if (string.IsNullOrEmpty(name)) return false;
         if (string.Equals(name, r, StringComparison.OrdinalIgnoreCase)) return true;

         return name.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0;
      }
   }
}
