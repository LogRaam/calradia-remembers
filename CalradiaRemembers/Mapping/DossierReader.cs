// Code written by Gabriel Mailhot, 31/10/2026.
// What the chat's NPC dossier shows of what the encyclopedia knows. Gabriel (31/10/2026): "Dans le panneau du NPC, on
// pourrait ajouter tout ce que le joueur a appris sur ce NPC et qui a ete ajoute a l'Encyclopedia". The prompt holds truths
// the player has not learned yet (a fear the model may reveal), so nothing here reads a character's secrets from the
// profile: each section goes through the same gated reader as its encyclopedia row, and Grievances, Kindnesses and
// Standing accords live here so the page and the chat cannot drift apart.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Nemesis;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The sections of a hero's dossier, as shown. An empty section hides.</summary>
   internal sealed class DossierSections
   {
      /// <summary>Their stance toward the player, in words.</summary>
      internal string Bearing { get; set; } = "";

      /// <summary>Who they are, as far as the player has learned it, then what they let slip.</summary>
      internal string Learned { get; set; } = "";

      /// <summary>Their bond with the player.</summary>
      internal string Heart { get; set; } = "";

      /// <summary>The kindnesses the player knows they remember.</summary>
      internal string Kindnesses { get; set; } = "";

      /// <summary>The grievances the player knows they hold.</summary>
      internal string Grievances { get; set; } = "";

      /// <summary>The accords standing between them and the player.</summary>
      internal string Accords { get; set; } = "";

      /// <summary>The heading of the nemesis dossier.</summary>
      internal string NemesisLabel { get; set; } = "";

      /// <summary>The nemesis dossier, once the player has met them as one.</summary>
      internal string Nemesis { get; set; } = "";

      /// <summary>The aggressions they lived through, at Hardcore.</summary>
      internal string Trauma { get; set; } = "";
   }

   /// <summary>Reads what the player has learned of a hero, for the encyclopedia and the chat's dossier.</summary>
   internal static class DossierReader
   {
      /// <summary>The sections of a character's dossier (cr.test npc_dossier reads this through the chat). All empty for the player.</summary>
      internal static DossierSections SectionsFor(Hero hero, NpcProfile profile)
      {
         if (hero == null || hero == Hero.MainHero) return new DossierSections();

         NemesisDossierText nemesis = Safe(() => NemesisDossierFactory.BuildFor(hero));

         return new DossierSections {
            Bearing = Safe(() => BearingOf(hero, profile)) ?? "",
            Learned = Section(() => LearnedLines(hero, profile)),
            Heart = Section(() => HeartPageReader.LinesFor(hero)),
            Kindnesses = Section(() => KindnessLines(hero)),
            Grievances = Section(() => GrievanceLines(hero)),
            Accords = Section(() => AccordLines(hero)),
            NemesisLabel = nemesis?.Label ?? "",
            Nemesis = ShownText.Of(nemesis?.Items ?? ""),
            Trauma = Section(() => TraumaPageReader.LinesFor(hero))
         };
      }

      // The strongest grudge or kindness toward the player that the player knows of, the one that agrees with the regard
      // first (a grudge for a low regard, a kindness for a high one). Also the reason on the "Personal regard" row.
      internal static string WhyTowardThePlayer(Hero h, int regard)
      {
         int today = (int) CampaignTime.Now.ToDays;
         string main = Hero.MainHero?.StringId;
         var grudge = GrudgeBehavior.Instance?.KnownGrudgesFor(h.StringId, today).FirstOrDefault(g => g.TargetId == main);
         var kindness = AppreciationBehavior.Instance?.KnownAppreciationsFor(h.StringId, today).FirstOrDefault(a => a.TargetId == main);
         string g = grudge == null ? null : ShownText.Of(grudge.CauseSummary);
         string k = kindness == null ? null : ShownText.Of(kindness.CauseSummary);

         return regard < Regard.NeutralTier ? g ?? k : k ?? g;
      }

      /// <summary>The grievances the player has earned knowledge of, as shown. KnownGrudgesFor is the knowledge gate.</summary>
      internal static IReadOnlyList<string> GrievanceLines(Hero hero)
      {
         GrudgeBehavior behavior = GrudgeBehavior.Instance;
         if (hero == null || behavior == null) return Array.Empty<string>();

         var today = (int) CampaignTime.Now.ToDays;
         var lines = new List<string>();
         foreach (Grudge grudge in behavior.KnownGrudgesFor(hero.StringId, today))
         {
            bool targetIsPlayer = grudge.TargetId == Hero.MainHero?.StringId;
            string targetName = targetIsPlayer ? null : FindHero(grudge.TargetId)?.Name?.ToString();

            // Tolerant of a stale StringId after a save: the narrator falls back to "them" rather than skip the line.
            CalradiaRemembers.Logic.Grudges.TraitFacts traits = GrudgeBridge.BuildTraitFacts(FindHero(grudge.HolderId));
            int magnitude = GrudgePolicy.EffectiveMagnitude(grudge, today, traits);
            GrudgeResolutionKind kind = GrudgePolicy.ResolutionKindFor(grudge.Source, grudge.Rigidity);

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer, targetName, magnitude, kind);
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
         }

         return lines;
      }

      /// <summary>
      ///   The kindnesses the player has earned knowledge of, as shown. A live grudge tarnishes them here too, so a soured
      ///   kindness shows dimmer, or not at all once fully tarnished.
      /// </summary>
      internal static IReadOnlyList<string> KindnessLines(Hero hero)
      {
         AppreciationBehavior behavior = AppreciationBehavior.Instance;
         if (hero == null || behavior == null) return Array.Empty<string>();

         var today = (int) CampaignTime.Now.ToDays;
         int grudgeMagnitude = DominantLiveGrudgeMagnitude(hero, today);
         CalradiaRemembers.Logic.Appreciations.TraitFacts traits = AppreciationBridge.BuildTraitFacts(hero);

         var lines = new List<string>();
         foreach (Appreciation appreciation in behavior.KnownAppreciationsFor(hero.StringId, today))
         {
            bool targetIsPlayer = appreciation.TargetId == Hero.MainHero?.StringId;
            string targetName = targetIsPlayer ? null : FindHero(appreciation.TargetId)?.Name?.ToString();

            int effective = AppreciationPolicy.EffectiveMagnitude(appreciation, today, traits);
            int tarnished = AppreciationPolicy.TarnishedMagnitude(effective, grudgeMagnitude);

            string line = AppreciationNarrator.DisplayLine(appreciation, targetIsPlayer, targetName, tarnished);
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
         }

         return lines;
      }

      /// <summary>
      ///   The accords standing between this hero and the player, as shown, through the third-person
      ///   CouncilAccordPanelText (never the first-person clause bound for the NPC's own prompt).
      /// </summary>
      internal static IReadOnlyList<string> AccordLines(Hero hero)
      {
         var lines = new List<string>();
         if (hero == null) return lines;

         CouncilTribute tribute = CouncilTributeBehavior.Instance?.Get(hero.StringId);
         if (tribute != null)
            lines.Add(CouncilAccordPanelText.TributeOwed(new CouncilAccordPanelText.TributeLine {AmountPerDay = tribute.AmountPerDay, DaysRemaining = tribute.DaysRemaining, MissedDays = tribute.MissedDays}));

         CouncilStipend stipend = CouncilStipendBehavior.Instance?.Get(hero.StringId);
         if (stipend != null)
            lines.Add(CouncilAccordPanelText.StipendReceived(stipend.AmountPerDay, stipend.DaysRemaining));

         CouncilPact pact = CouncilPactBehavior.Instance?.Get(Hero.MainHero?.MapFaction, hero.MapFaction);
         if (pact != null)
            lines.Add(CouncilAccordPanelText.Pact(pact.DaysRemaining));

         CouncilHostage held = CouncilHostageBehavior.Instance?.GetByHostage(hero.StringId);
         if (held != null)
            lines.Add(CouncilAccordPanelText.HeldAsHostage(held.DaysRemaining));

         CouncilHostage pledged = CouncilHostageBehavior.Instance?.GetByPledger(hero.StringId);
         if (pledged != null)
            lines.Add(CouncilAccordPanelText.HostageGiven(FindHero(pledged.HostageId)?.Name?.ToString(), pledged.DaysRemaining));

         CouncilWard asProtege = CouncilWardBehavior.Instance?.GetAsProtege(hero.StringId);
         if (asProtege != null)
            lines.Add(CouncilAccordPanelText.WardAsProtege(FindHero(asProtege.MentorId)?.Name?.ToString()));

         CouncilWard asMentor = CouncilWardBehavior.Instance?.GetAsMentor(hero.StringId);
         if (asMentor != null)
            lines.Add(CouncilAccordPanelText.WardAsMentor(FindHero(asMentor.ProtegeId)?.Name?.ToString()));

         return lines;
      }

      private static IEnumerable<string> LearnedLines(Hero hero, NpcProfile profile)
      {
         IEnumerable<string> discoveries = profile?.DiscoveredTraits?.Select(t => t.Description) ?? Enumerable.Empty<string>();

         return KnownPageReader.LinesFor(hero).Concat(discoveries);
      }

      // A dead hero holds no living bearing toward the player, and one never met has nothing to voice.
      private static string BearingOf(Hero hero, NpcProfile profile)
         => profile == null || hero.IsDead
            ? ""
            : StanceBearingText.Compose(new StanceBearingText.BearingFacts {Trust = profile.StanceTrust, Respect = profile.StanceRespect, Fear = profile.StanceFear});

      private static int DominantLiveGrudgeMagnitude(Hero hero, int today)
      {
         IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);
         if (live == null || live.Count == 0) return 0;

         return GrudgePolicy.EffectiveMagnitude(live[0], today, GrudgeBridge.BuildTraitFacts(hero));
      }

      private static Hero FindHero(string id)
         => string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      private static string Section(Func<IEnumerable<string>> read) => ShownText.Of(NpcDossierText.Bullets(Safe(read)));

      // One section that fails hides alone; the rest of the dossier still opens.
      private static T Safe<T>(Func<T> read) where T : class
      {
         try { return read(); }
         catch (Exception ex)
         {
            ModLog.Error($"[DOSSIER] a section failed and stays hidden: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
