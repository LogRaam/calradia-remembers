// Code written by Gabriel Mailhot, 03/10/2026.
// Console doors to the authored quests (docs/design/AUTHORED_QUESTS.md): what loaded and what is wrong with the files
// (for authors), where the tale stands, and levers to start, step and abandon one while the remaining increments are
// built and for anyone writing a quest of their own.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.AuthoredQuests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.story_check [name]: the quests that loaded, and every problem in the quest files. With a name (or an open
      ///   conversation's character), whether that character could give each quest now, and if not, why.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_check", "cr")]
      public static string StoryCheck(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null) return "Authored quests are not active.";

         b.LoadFiles();
         var sb = new StringBuilder();
         sb.AppendLine($"{b.Quests.Count} quest(s) loaded: {string.Join(", ", b.Quests.Select(q => q.Id))}");
         if (b.Problems.Count == 0) sb.Append("No problem in the quest files.");
         else foreach (string p in b.Problems) sb.AppendLine("- " + p);

         if (args is {Count: > 0} || Hero.OneToOneConversationHero != null)
         {
            Hero giver = ResolveHero(args);
            if (giver == null) sb.AppendLine("No character by that name.");
            else
            {
               sb.AppendLine();
               sb.AppendLine($"Could {giver.Name} give them now?");
               foreach (AuthoredQuest q in b.Quests) sb.AppendLine($"- {q.Id}: {b.Eligibility(giver, q)}");
            }
         }

         return sb.ToString().TrimEnd();
      }

      /// <summary>cr.tale_on_map &lt;id&gt; [anyway]: the popup of a tale that begins on the map, now, the dice skipped.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("tale_on_map", "cr")]
      public static string TaleOnMap(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.tale_on_map <tale id> [anyway]   (tale ids: cr.story_check)";
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null) return "Authored quests are not active.";

         return b.DebugBeginOnMap(args[0], args.Skip(1).Any(a => a.Equals("anyway", System.StringComparison.OrdinalIgnoreCase)));
      }

      /// <summary>
      ///   cr.tale_test &lt;tale id&gt; [wife's name] (Gabriel, 11/10/2026: "il faut injecter et arranger car je ne prendrai
      ///   pas le temps de jouer pour mettre toutes les conditions en place"). For a spouse tale that begins on the map:
      ///   finds a spouse who fits it (the one named, else one of the tale's culture, of age, not with child, free to
      ///   marry), divorces the player's present spouse when it is someone else, marries them, brings them into the
      ///   player's party, raises their regard to the tale's least, then opens the popup with the place and hour skipped.
      ///   It changes the campaign for good (a divorce, a marriage): a test save only.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("tale_test", "cr")]
      public static string TaleTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.tale_test <tale id> [spouse's name]   (tale ids: cr.story_check). Changes the campaign: a test save only.";
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null) return "Authored quests are not active.";
         AuthoredQuest q = b.Quests.FirstOrDefault(x => x.Id == args[0]);
         if (q == null) return $"No tale '{args[0]}' is loaded (cr.story_check lists them).";
         GiverRequirement g = q.Requires?.Giver ?? new GiverRequirement();
         if (g.Role != "spouse") return $"'{q.Id}' is not given by the player's spouse; cr.tale_on_map opens a map tale as it stands.";

         Hero player = Hero.MainHero;
         string gender = g.Gender?.Trim().ToLowerInvariant();
         bool Fits(Hero h) => h != null && h != player && h.IsAlive && !h.IsChild && h.Age >= 18 && h.Age <= 45 && !h.IsPrisoner
                              && (gender == null || gender == (h.IsFemale ? "female" : "male"))
                              && AuthoredCulturePolicy.Matches(g.Culture, h.Culture?.StringId, h.Culture?.Name?.ToString())
                              && !(g.NotPregnant && h.IsPregnant);
         bool Free(Hero h) => h.Spouse == null && !h.IsClanLeader && h.GovernorOf == null && !(h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h)
                              && h.Clan != null && h.Clan != Clan.PlayerClan;

         Hero wife;
         if (args.Count > 1)
         {
            wife = ResolveHero(args.Skip(1));
            if (wife == null) return "No character by that name.";
            if (wife != player.Spouse && !Free(wife)) return $"{wife.Name} cannot be married for the test (already wed, heads a clan, governs, or leads a party).";
         }
         else
            wife = Fits(player.Spouse) ? player.Spouse : Hero.AllAliveHeroes.Where(h => Fits(h) && Free(h) && h.IsLord).OrderBy(h => h.Age).FirstOrDefault();

         if (!Fits(wife)) return $"No one fits '{q.Id}' ({AuthoredCulturePolicy.Describe(g.Culture)}, {gender ?? "either sex"}, 18 to 45, not with child, free to marry).";

         var sb = new StringBuilder();
         if (player.Spouse != null && player.Spouse != wife)
         {
            Hero former = player.Spouse;
            if (!Marriage.MarriageDissolutionService.TryDissolvePlayerMarriage(former, out string why)) return $"Could not divorce {former.Name}: {why}.";
            sb.AppendLine($"Divorced {former.Name}.");
         }

         if (player.Spouse != wife)
         {
            TaleWorlds.CampaignSystem.Actions.MarriageAction.Apply(player, wife);
            if (player.Spouse != wife) return sb + $"The game refused the marriage with {wife.Name} (an army or a battle under way?).";
            sb.AppendLine($"Married {wife.Name} ({wife.Culture?.Name}).");
         }

         if (g.WithPlayer && wife.PartyBelongedTo != TaleWorlds.CampaignSystem.Party.MobileParty.MainParty)
         {
            TaleWorlds.CampaignSystem.Actions.AddHeroToPartyAction.Apply(wife, TaleWorlds.CampaignSystem.Party.MobileParty.MainParty, false);
            sb.AppendLine($"{wife.Name} rides with you.");
         }

         NpcMemoryService.Core.Models.NpcProfile profile = behavior.Store.Get(wife.StringId) ?? Mapping.HeroProfileMapper.FromHero(wife);
         if (profile.ReputationWithPlayer < g.MinRegard)
         {
            NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(profile, g.MinRegard - profile.ReputationWithPlayer, NpcMemoryService.Core.Standing.RegardCause.SelfTest);
            sb.AppendLine($"Her regard for you raised to {profile.ReputationWithPlayer}.");
         }
         behavior.Store.Set(profile);

         if (!AuthoredContentPolicy.Allows(q.Requires?.Content, ModSettings.AdultContentLevel))
            return sb + $"The tale needs the '{q.Requires?.Content}' content level and the mod options are at {ModSettings.AdultContentLevel}: raise it in the mod options, then run cr.tale_on_map {q.Id} anyway.";

         sb.Append(b.DebugBeginOnMap(q.Id, true));

         return sb.ToString();
      }

      /// <summary>cr.story_status: the tale under way, its stage, and who it is bound to.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_status", "cr")]
      public static string StoryStatus(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestRun run = AuthoredQuestBehavior.Instance?.ActiveRun;
         if (run == null) return "No tale has been taken on in this campaign session.";

         AuthoredQuest q = AuthoredQuestBehavior.Instance.Quests.FirstOrDefault(x => x.Id == run.QuestId);
         IReadOnlyList<AuthoredChoice> open = q != null ? AuthoredQuestEngine.OpenChoices(run, q) : new List<AuthoredChoice>();

         return $"'{run.QuestId}' ({run.Status}{(run.EndingId != null ? ", ending '" + run.EndingId + "'" : "")}), stage '{run.StageId}' since day {run.StageStartDay}, started day {run.StartDay}. "
                + $"Bound: {string.Join(", ", run.BoundNames.Select(kv => kv.Key + "=" + kv.Value))}. "
                + $"Locals heard {run.TalkedThisStage.Count}, nights watched {run.NightsWatched}, findings [{string.Join(", ", run.Findings)}]. "
                + $"Flags [{string.Join(", ", run.Flags)}], path [{string.Join(", ", run.ChoicesTaken)}], open choices [{string.Join(", ", open.Select(c => c.Id))}].";
      }

      /// <summary>cr.story_choose &lt;choice id&gt;: takes that open choice of the current stage, however it would be taken.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_choose", "cr")]
      public static string StoryChoose(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.story_choose <choice id>   (open choices: cr.story_status)";

         return AuthoredQuestBehavior.Instance?.DebugChoose(args[0]) ?? "Authored quests are not active.";
      }

      /// <summary>cr.story_start &lt;quest id&gt; [name]: binds the quest to that character (or the one spoken to) and takes it on.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_start", "cr")]
      public static string StoryStart(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b == null || args == null || args.Count == 0) return "Usage: cr.story_start <quest id> [name]   (ids: cr.story_check)";

         AuthoredQuest q = b.Quests.FirstOrDefault(x => x.Id == args[0]);
         if (q == null) return $"No quest '{args[0]}'. See cr.story_check.";
         Hero giver = ResolveHero(args.Skip(1));
         if (giver == null) return "No character found. Pass a name, or open a conversation first.";

         b.ForgetPlayed(q.Id);
         if (!b.TryOffer(giver, (int) CampaignTime.Now.ToDays, 0, q.Id, out string whyNot))
         {
            ModLog.Info($"[TALE] cr.story_start: {giver.Name} cannot give '{q.Id}': {whyNot}.");

            return $"{giver.Name} cannot give '{q.Id}': {whyNot}.";
         }

         b.Accept(q, b.Offer);

         return $"'{q.Id}' taken on from {giver.Name}. {StoryStatus(null)}";
      }

      /// <summary>cr.tale_fight_spike [size]: SPIKE, a band beside the player to fight with his company only (TaleCompanyFightSpike).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("tale_fight_spike", "cr")]
      public static string TaleFightSpike(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         int size = args != null && args.Count > 0 && int.TryParse(args[0], out int n) ? n : 6;

         return TaleCompanyFightSpike.SpawnBand(size);
      }

      /// <summary>cr.story_step: completes the current stage as though its objective were met.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_step", "cr")]
      public static string StoryStep(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         return AuthoredQuestBehavior.Instance?.DebugStep() ?? "Authored quests are not active.";
      }

      /// <summary>cr.story_abandon: ends the tale under way, without reward or penalty.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("story_abandon", "cr")]
      public static string StoryAbandon(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;
         AuthoredQuestBehavior b = AuthoredQuestBehavior.Instance;
         if (b?.ActiveRun == null) return "No tale is under way.";
         string id = b.ActiveRun.QuestId;
         b.Abandon("Set aside.");

         return $"'{id}' set aside.";
      }
   }
}
