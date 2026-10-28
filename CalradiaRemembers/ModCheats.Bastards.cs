// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Bastards;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.preg [name]            — the female PLAYER is the mother; [name] is the father (a lord),
      ///   else the shared hidden "Unknown" father (the faceless-bandit case).
      ///   cr.preg mother [name]     — that NPC is the mother and the player is the father (the bastard
      ///   path): used to run the in-game SPIKE on which clan the child lands in.
      ///   Forces a conception (bypasses the 15% roll) so the pregnancy → delivery path can be validated
      ///   without waiting on a real scene.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("preg", "cr")]
      public static string ForcePregnancy(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         // cr.preg mother <name> → that NPC is the mother, the player is the father.
         if (args is {Count: > 1} && string.Equals(args[0], "mother", StringComparison.OrdinalIgnoreCase))
         {
            List<string> motherArgs = args.GetRange(1, args.Count - 1);
            Hero mother = ResolveHero(motherArgs);

            if (mother == null) return $"No hero matched \"{string.Join(" ", motherArgs)}\".";

            string motherNote = PregnancyService.TryConceive(mother, Hero.MainHero, true);

            return motherNote ??
                   $"Conception did not occur — {mother.Name} must be a female of fertile age (18–45), " +
                   "not already pregnant, and past the post-birth recovery window. Check CalradiaRemembers.log.";
         }

         bool named = args is {Count: > 0};
         Hero father = named
            ? ResolveHero(args)
            : null;

         if (named && father == null) return $"No hero matched \"{string.Join(" ", args)}\".";

         string note = PregnancyService.TryConceive(father, true);

         if (note == null)
            return "Conception did not occur — the player must be a female of fertile age (18–45), " + "not already pregnant, and past the post-birth recovery window. Check CalradiaRemembers.log.";

         return note +
                (father == null
                   ? " [father: shared Unknown]"
                   : "");
      }

      /// <summary>
      ///   cr.preg_birth [name|reset]
      ///   Two-phase test that a CR-created pregnancy is actually DELIVERED by the engine (the open
      ///   question behind pregnancy's Hero.IsPregnant=False). First call ARMS: it force-conceives a
      ///   fertile female (named, or auto-picked) and records her child count. Advance ~84 days (fast-
      ///   forward, NO reload — the marker is in memory), then call again to CHECK: a new child = PASS
      ///   (the IsPregnant flag is cosmetic); 84+ days and none = FAIL (the flag blocks delivery).
      ///   Pass "reset" to re-arm. Not part of cr.test all, a birth cannot resolve inside one command.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("preg_birth", "cr")]
      public static string PregBirthTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         int today = behavior.GameStateBridge.GetCurrentGameDay();
         bool reset = args is {Count: > 0} && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase);

         // ── PHASE 2 — armed: has the pregnancy been delivered? ──
         if (!reset && _pregBirthMotherId != null)
         {
            Hero mom = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == _pregBirthMotherId);

            if (mom == null)
            {
               string lost = _pregBirthMotherId;
               _pregBirthMotherId = null;

               return $"Armed mother ({lost}) not found (reloaded, or she died). Marker cleared — run cr.preg_birth to arm again.";
            }

            int childrenNow = mom.Children?.Count ?? 0;
            int elapsed = today - _pregBirthArmDay;

            if (childrenNow > _pregBirthChildrenBefore)
            {
               _pregBirthMotherId = null;

               return $"[PREG_BIRTH] PASS — {mom.Name} delivered: {_pregBirthChildrenBefore} -> {childrenNow} children after {elapsed} days. " + "The reflective pregnancy IS delivered despite Hero.IsPregnant=False (the flag is cosmetic).";
            }

            if (elapsed < 84)
               return $"[PREG_BIRTH] PENDING — {mom.Name}: {elapsed}/84 days elapsed, still {childrenNow} children. Advance more time (no reload), then re-run.";

            return $"[PREG_BIRTH] FAIL — {mom.Name}: {elapsed} days passed and no new child ({_pregBirthChildrenBefore} -> {childrenNow}). " + "The reflectively-created pregnancy was NOT delivered — Hero.IsPregnant=False blocks the engine. PregnancyService must also set the flag.";
         }

         // ── PHASE 1 — arm: conceive on a fertile female and record the baseline ──
         List<string> nameTokens = reset && args != null
            ? args.GetRange(1, args.Count - 1)
            : args ?? new List<string>();
         Hero mother = nameTokens.Count > 0
            ? ResolveHero(nameTokens)
            : Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && !h.IsPregnant && h.Age >= 18f && h.Age <= 45f);

         if (mother == null) return "No fertile (female, 18-45, not pregnant) NPC found. Pass a name, or none to auto-pick.";

         int before = mother.Children?.Count ?? 0;
         string note = PregnancyService.TryConceive(mother, Hero.MainHero, true);

         if (note == null)
            return $"{mother.Name}: conception did NOT take (ineligible — already pregnant, recently gave birth, or out of 18-45). Try another.";

         _pregBirthMotherId = mother.StringId;
         _pregBirthArmDay = today;
         _pregBirthChildrenBefore = before;

         return $"[PREG_BIRTH] ARMED — {mother.Name} conceived on day {today} (had {before} children, IsPregnant={mother.IsPregnant}). " + "Advance ~84 days WITHOUT reloading (the marker is in memory), then run cr.preg_birth again to verify delivery.";
      }

      private static string _pregBirthMotherId;

      private static int _pregBirthArmDay = -1;

      private static int _pregBirthChildrenBefore;

      /// <summary>
      ///   cr.bastard_check &lt;motherName&gt; &lt;fatherName&gt;
      ///   Read-only diagnostic for the bastards prestige gate ([[project-bastards]]): maps both heroes'
      ///   clans to a prestige score and prints whether a grown bastard would press a claim on the FATHER's
      ///   clan or follow the MOTHER's grander dynasty, plus the discovery risk for a few knower counts.
      ///   Mutates nothing. The last token is the father; everything before it is the mother's name.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_check", "cr")]
      public static string BastardCheck(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (args == null || args.Count < 2)
            return "Usage: cr.bastard_check <motherName> <fatherName>";

         Hero father = ResolveHero(new List<string> {args[args.Count - 1]});
         Hero mother = ResolveHero(args.GetRange(0, args.Count - 1));

         if (mother == null || father == null)
            return "Need two heroes — cr.bastard_check <motherName> <fatherName>.";

         int maternal = ClanPrestige(mother.Clan);
         int paternal = ClanPrestige(father.Clan);
         BastardClaimVerdict verdict = BastardPrestigePolicy.Resolve(maternal, paternal);

         var sb = new StringBuilder();
         sb.AppendLine($"Bastard of {mother.Name} (mother) x {father.Name} (father):");
         sb.AppendLine($"  maternal house {mother.Clan?.Name?.ToString() ?? "—"}: prestige {maternal}");
         sb.AppendLine($"  paternal house {father.Clan?.Name?.ToString() ?? "—"}: prestige {paternal}");
         sb.AppendLine($"  claim verdict: {verdict} (the child is born into the MOTHER's clan regardless)");
         sb.AppendLine($"  discovery risk by #knowers: 1={BastardDiscoveryPolicy.DiscoveryChancePercent(1)}% " +
                       $"3={BastardDiscoveryPolicy.DiscoveryChancePercent(3)}% " +
                       $"6={BastardDiscoveryPolicy.DiscoveryChancePercent(6)}%");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.bastards — lists the player's tracked bastards: name, hidden/discovered, age, and the
      ///   per-season discovery chance at that age. Diagnostic only.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastards", "cr")]
      public static string ListBastards(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         BastardBehavior b = BastardBehavior.Instance;
         if (b == null) return "Bastard system not active.";
         if (b.Bastards.Count == 0)
            return "No tracked bastards. (They form at a non-marital CR birth, or via cr.bastard_tag <name>.)";

         var sb = new StringBuilder();
         sb.AppendLine($"{b.Bastards.Count} tracked bastard(s):");
         foreach (KeyValuePair<string, bool> kv in b.Bastards)
         {
            Hero h = Hero.AllAliveHeroes.FirstOrDefault(x => x.StringId == kv.Key);
            string name = h?.Name?.ToString() ?? kv.Key;
            string detail = h != null
               ? $"age {h.Age:0}, {BastardDiscoveryPolicy.DiscoveryChancePercent((int) h.Age)}%/season"
               : "—";
            sb.AppendLine($"  {name}: {(kv.Value ? "DISCOVERED" : "hidden")} ({detail}; blackmail: {b.BlackmailStatusOf(kv.Key)})");
         }

         return sb.ToString();
      }

      /// <summary>cr.bastard_tag [name] — force-tag a hero as a concealed player bastard (testing, no birth needed).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_tag", "cr")]
      public static string TagBastard(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardBehavior.Instance.DebugTrack(hero);

         return $"{hero.Name} is now tracked as a concealed bastard (age {hero.Age:0}). " +
                "Run cr.bastard_season to roll discovery, or cr.bastards to list.";
      }

      /// <summary>cr.bastard_season — runs the seasonal discovery evaluation now (testing, skips waiting a season).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_season", "cr")]
      public static string RunBastardSeason(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         BastardBehavior.Instance.DebugEvaluateSeason();

         return "Seasonal discovery evaluated. Use cr.bastards to see who was exposed; a discovery records a " +
                "Scandal in the world-event store (cr.chronicle / talk to a nearby NPC) and dents your standing.";
      }

      /// <summary>
      ///   cr.bastard_claim [name] — resolves a grown bastard's claim against your house using the real
      ///   prestige gate (maternal house vs yours) and applies it: a lesser/equal maternal house presses a
      ///   claim (hostility + scandal); a grander one follows the mother's dynasty. Tests the coming-of-age path.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_claim", "cr")]
      public static string ForceBastardClaim(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardClaimVerdict verdict = BastardBehavior.Instance.ResolveClaimVerdict(hero);
         BastardBehavior.Instance.DebugClaim(hero, verdict == BastardClaimVerdict.PressesClaim);

         return verdict == BastardClaimVerdict.PressesClaim
            ? $"{hero.Name} presses a claim against your house — relation drop + scandal recorded. (Verdict: {verdict}.)"
            : $"{hero.Name} follows the mother's grander dynasty — no claim. (Verdict: {verdict}.)";
      }

      /// <summary>
      ///   cr.bastard_blackmail [name] — reports whether an NPC holds a bastard secret over the player (she is
      ///   the mother of a still-concealed bastard of yours) and, if so, the gold she could demand for silence.
      ///   Diagnostic only. Tag one first with cr.bastard_tag if testing without a real birth.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_blackmail", "cr")]
      public static string CheckBastardBlackmail(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         int demand = BastardBehavior.Instance.BlackmailDemandOver(hero);

         return demand > 0
            ? $"{hero.Name} holds a bastard secret over you — she could demand ~{demand} denars for her silence."
            : $"{hero.Name} holds no bastard leverage over you (not the mother of a concealed bastard of yours).";
      }

      /// <summary>
      ///   cr.bastard_mother [name] — shows how an NPC would behave as the mother of the player's bastard,
      ///   from her traits + regard: blackmail (mercenary), longing, resentful, or a pragmatic plea for keep.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_mother", "cr")]
      public static string CheckBastardMother(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         BastardMotherDisposition d = BastardBehavior.Instance.MotherDisposition(hero);
         string note = d switch {
            BastardMotherDisposition.Mercenary => "she would BLACKMAIL for silence",
            BastardMotherDisposition.Longing => "she longs for you — come see the child, wishes you'd wed her",
            BastardMotherDisposition.Resentful => "she writes coldly, feeling wronged",
            _ => "she pragmatically asks for the child's keep"
         };

         return $"{hero.Name} — bastard-mother disposition: {d} ({note}). " +
                $"Honor {hero.GetTraitLevel(DefaultTraits.Honor):+#;-#;0}, Calculating {hero.GetTraitLevel(DefaultTraits.Calculating):+#;-#;0}, " +
                $"relation {hero.GetRelation(Hero.MainHero)}.";
      }

      /// <summary>cr.bastard_demand [name] — force-raise a blackmail demand over the named tracked bastard (testing the pay/leak loop).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_demand", "cr")]
      public static string RaiseBastardDemand(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass the bastard child's name fragment.";

         BastardBehavior.Instance.DebugRaiseDemand(hero);

         return $"Raised a blackmail demand over {hero.Name} — their mother now holds an active demand. " +
                $"Pay via cr.bastard_pay <mother>, or run cr.bastard_season across seasons to watch her leak it if unpaid.";
      }

      /// <summary>cr.bastard_pay [name] — pay an NPC's blackmail demands for her silence (gold to her, a grace period bought).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bastard_pay", "cr")]
      public static string PayBastardBlackmail(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (BastardBehavior.Instance == null) return "Bastard system not active.";

         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         (int settled, int gold) = BastardBehavior.Instance.Pay(hero);

         return settled > 0
            ? $"Paid {hero.Name} {gold} denars for silence over {settled} bastard(s) — she keeps quiet, for now."
            : $"{hero.Name} holds no bastard leverage to settle.";
      }

      /// <summary>An abstract prestige score for a clan (tier-weighted + renown + a ruling-dynasty bonus).</summary>
      private static int ClanPrestige(Clan clan)
      {
         if (clan == null) return 0;
         bool ruling = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;

         return BastardPrestigePolicy.PrestigeScore(clan.Tier, (int) clan.Renown, ruling);
      }
   }
}
