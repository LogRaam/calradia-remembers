// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Mapping
{
   public static class HeroProfileMapper
   {
      public static NpcProfile FromHero(Hero hero)
      {
         string archetype = RetrievePersonalityFor(hero);

         NpcProfile profile = new() {
            Id = hero.StringId,
            Name = hero.Name?.ToString() ?? "Unknown",
            IsFemale = hero.IsFemale,
            Age = (int) hero.Age,
            Faction = hero.MapFaction?.Name?.ToString() ?? "Unaligned",
            // Empty, not a "No clan" phrase: a clanless hero has no house name, and every consumer already treats
            // an empty clan as "no clan" (the prompt renders "of no noble clan", the digest drops the "of X" tail).
            // The old "No clan" sentinel leaked into templates as "of the No clan clan".
            Clan = hero.Clan?.Name?.ToString() ?? "",
            Personality = BuildPersonality(hero),
            Trait = archetype,
            SpouseName = hero.Spouse?.IsAlive == true
               ? hero.Spouse.Name?.ToString()
               : null,
            Romantic = RomanticProfileBuilder.Build(hero, archetype)
         };

         if (profile.Romantic != null)
            ModLog.Info($"  Romantic: " +
                        $"Orientation={profile.Romantic.Orientation}, " +
                        $"IsFemale={profile.Romantic.IsFemale}, " +
                        $"Prefs=[{string.Join(", ", profile.Romantic.Preferences)}], " +
                        $"Kinks=[{string.Join(", ", profile.Romantic.Kinks)}]");

         return profile;
      }

      /// <summary>
      ///   Rebuilds <see cref="NpcProfile.Personality" /> from live hero state and the
      ///   current <see cref="CulturalTraditionsLoader" /> cache. Call at every session
      ///   launch so cultural descriptions are always up to date (users may edit the JSON).
      /// </summary>
      public static void RefreshPersonality(NpcProfile profile, Hero hero)
      {
         if (profile == null || hero == null) return;
         profile.IsFemale = hero.IsFemale;
         profile.Age = (int) hero.Age;
         profile.Personality = BuildPersonality(hero);
         profile.SpouseName = hero.Spouse?.IsAlive == true
            ? hero.Spouse.Name?.ToString()
            : null;
         RefreshIdentity(profile, hero);
      }

      /// <summary>
      ///   Brings the hero's realm/house identity up to date with live game state. A hero can change kingdom or
      ///   clan at runtime (a defection, or a civil-war split spawned by another mod such as Bellum Civile), so the
      ///   frozen creation-time <see cref="NpcProfile.Faction" /> / <see cref="NpcProfile.Clan" /> would otherwise
      ///   keep introducing them by an allegiance they have left, even while the same prompt's war status and liege
      ///   line already reflect the new realm. Uses the exact fallbacks <see cref="FromHero" /> uses. Call at
      ///   conversation open (and session launch) so the identity line always names the current realm.
      /// </summary>
      public static void RefreshIdentity(NpcProfile profile, Hero hero)
      {
         if (profile == null || hero == null) return;
         profile.Faction = hero.MapFaction?.Name?.ToString() ?? "Unaligned";
         profile.Clan = hero.Clan?.Name?.ToString() ?? "";

         // THE NAME, for the reason this method's own doc gives about realm and house: a frozen creation-time
         // value keeps introducing somebody as who they used to be. A renamed hero was led by the old name for
         // the rest of the campaign, and {{char}} expanded to it (audit, 15/09/2026). Guarded rather than
         // assigned blindly: a blank name would leave the identity line with nobody in it, and keeping a stale
         // name is far less bad than that.
         string live = hero.Name?.ToString();
         if (!string.IsNullOrWhiteSpace(live)) profile.Name = live;

         // THE SPOUSE, and this one a player can actually feel. It was refreshed only at SESSION LAUNCH, so a
         // marriage and an intimate scene in the same sitting left the profile still believing the character
         // unwed - and the affair was classified SecretLover, which is precisely the case ProfileMutator
         // believes it is guarding. Marrying and then speaking to your spouse is ordinary play.
         profile.SpouseName = hero.Spouse?.IsAlive == true
            ? hero.Spouse.Name?.ToString()
            : null;
      }

      /// <summary>
      ///   Brings an existing profile up to date with current Sprint 8.x fields
      ///   without losing accumulated game state (Events, ReputationWithPlayer).
      ///   Call this whenever an existing profile is loaded from the save before
      ///   it is used to build a prompt.
      ///   Currently fixes two cases:
      ///   - <see cref="NpcProfile.Romantic" /> is null (profile created before Sprint 8.2).
      ///   - <see cref="NpcProfile.Trait" /> is the generic "Thoroughly Conventional"
      ///   fallback while the hero actually has active traits in the current
      ///   archetype catalog (profile created before Sprint 8.1 or 8.3).
      ///   Safe to call on every load — it is a no-op for already up-to-date profiles
      ///   and only logs when an actual upgrade happens.
      /// </summary>
      public static void UpgradeIfNeeded(NpcProfile profile, Hero hero)
      {
         if (profile == null || hero == null) return;

         // Diagnostic — surface an existing Romantic profile's compatibility so a
         // missing ROMANTIC NATURE section can be diagnosed as by-design (incompatible
         // orientation) versus a stale/incomplete profile. Runs before the cheap exit
         // below so it logs even for otherwise up-to-date profiles.
         if (profile.Romantic != null) LogExistingRomanticCompatibility(profile);

         // A profile from before 27/10/2026 has no gestures loved or disliked yet: rolled once now, weighed by its kinks and
         // preferences, then kept. One rolled before the less-loved gesture existed is given it alone, never one it already
         // loves or dislikes; one rolled before the list widened (TastesRevision 0) is completed with the new gestures, and
         // a dislike its kinks contradict goes unless the player already learned it.
         if (profile.Romantic is {LovedActs: null} || profile.Romantic is {DislikedActs: null})
         {
            Logic.Romance.IntimateTastes tastes = Logic.Romance.IntimateActPolicy.Roll(
               RomanticProfileBuilder.TasteFacts(hero, profile.Romantic.Kinks, profile.Romantic.Preferences));
            profile.Romantic.LovedActs ??= tastes.Loved;
            profile.Romantic.DislikedActs ??= tastes.Disliked;
            profile.Romantic.LessFondActs ??= tastes.LessFond;
            profile.Romantic.TastesRevision = Logic.Romance.IntimateActPolicy.Revision;
         }

         if (profile.Romantic is {LessFondActs: null})
            profile.Romantic.LessFondActs = Logic.Romance.IntimateActPolicy.LessFondFor(hero.StringId, hero.IsFemale,
               (profile.Romantic.LovedActs ?? new List<IntimateAct>()).Concat(profile.Romantic.DislikedActs ?? new List<IntimateAct>()));

         if (profile.Romantic != null && profile.Romantic.TastesRevision < Logic.Romance.IntimateActPolicy.Revision)
         {
            Logic.Romance.IntimateTastes done = Logic.Romance.IntimateActPolicy.Complete(
               new Logic.Romance.IntimateTastes {
                  Loved = profile.Romantic.LovedActs ?? new List<IntimateAct>(), LessFond = profile.Romantic.LessFondActs ?? new List<IntimateAct>(),
                  Disliked = profile.Romantic.DislikedActs ?? new List<IntimateAct>()
               }, profile.Romantic.KnownActs, RomanticProfileBuilder.TasteFacts(hero, profile.Romantic.Kinks, profile.Romantic.Preferences));
            profile.Romantic.LovedActs = done.Loved;
            profile.Romantic.LessFondActs = done.LessFond;
            profile.Romantic.DislikedActs = done.Disliked;
            profile.Romantic.TastesRevision = Logic.Romance.IntimateActPolicy.Revision;
         }

         // What matters most to them (Gabriel, 27/10/2026): chosen once among what is true of them, kept while its ground
         // stands, moved when the world takes it away. Read on every load, so a child's death is seen the next time.
         MattersMostReader.Refresh(hero, profile);

         bool needsRomantic = profile.Romantic == null;

         // Orientation conflicts with actual spouse gender → regenerate.
         // e.g. Martira (Homosexual) has husband Pharon → must become Bisexual.
         bool orientationConflictsWithSpouse = !needsRomantic && OrientationConflictsWithSpouse(profile.Romantic, hero);
         if (orientationConflictsWithSpouse)
         {
            ModLog.Info(
               $"  Orientation conflict for {profile.Name}: " + $"{profile.Romantic.Orientation} but spouse is " + $"{hero.Spouse?.Name} ({(hero.Spouse?.IsFemale == true ? "F" : "M")}) " + $"— regenerating RomanticProfile.");
            needsRomantic = true;
         }

         // A retired archetype (an ordinary trait mix once worded as an appetite for suffering; tashmetu, 02/10/2026) is
         // derived again, so a save made before the rewording does not keep it for good.
         bool maybeStaleTrait = string.IsNullOrEmpty(profile.Trait) || profile.Trait.StartsWith("Thoroughly Conventional")
                                || Logic.RetiredArchetypes.IsRetired(profile.Trait)
                                // every hero with a +-2 trait got this generic line before 03/10/2026 (no archetype matched)
                                || profile.Trait.StartsWith("A figure of contradictions");

         // Cheap exit — nothing to do for already-upgraded profiles.
         if (!needsRomantic && !maybeStaleTrait) return;

         // Compute current archetype (also writes trait diagnostics to the log).
         string currentArchetype = RetrievePersonalityFor(hero);

         if (maybeStaleTrait && profile.Trait != currentArchetype)
         {
            ModLog.Info(
               $"  Upgrading Trait for {profile.Name}: " + $"was '{profile.Trait}' → now '{currentArchetype}'");
            profile.Trait = currentArchetype;
         }

         if (!needsRomantic) return;

         // M-R4: a regeneration (an orientation fix, or a missing profile being built) rebuilds the STATIC parts
         // of the romantic profile, but must NEVER wipe the MUTABLE relationship state the player has built. The
         // orientation-conflict path can fire on every single load (a Heterosexual NPC given a same-sex spouse by
         // another mod), so a naive rebuild silently reset Status/attraction/consort/secret-lover to a stranger
         // indefinitely. Carry that state across the rebuild.
         RomanticProfile previous = profile.Romantic;

         profile.Romantic = RomanticProfileBuilder.Build(hero, currentArchetype);

         profile.Romantic.CarryRelationshipFrom(previous);

         ModLog.Info($"  Generated missing RomanticProfile for {profile.Name}: " +
                     $"Orientation={profile.Romantic?.Orientation}, " +
                     $"IsFemale={profile.Romantic?.IsFemale}, " +
                     $"Prefs=[{string.Join(", ", profile.Romantic?.Preferences ?? throw new InvalidOperationException())}], " +
                     $"Kinks=[{string.Join(", ", profile.Romantic?.Kinks ?? throw new InvalidOperationException())}]");
      }

      #region private

      private static string BuildDynamicFallback(Traits t)
      {
         var pos = new List<string>();
         var neg = new List<string>();

         if (t.Honor > 0) pos.Add("honorable");
         if (t.Honor < 0) neg.Add("dishonorable");
         if (t.Mercy > 0) pos.Add("merciful");
         if (t.Mercy < 0) neg.Add("ruthless");
         if (t.Valor > 0) pos.Add("brave");
         if (t.Valor < 0) neg.Add("cautious to a fault");
         if (t.Generosity > 0) pos.Add("generous");
         if (t.Generosity < 0) neg.Add("tight-fisted");
         if (t.Calculating > 0) pos.Add("calculating");
         if (t.Calculating < 0) neg.Add("impulsive");

         var parts = new List<string>();
         if (pos.Count > 0) parts.Add(string.Join(", ", pos));
         if (neg.Count > 0) parts.Add(string.Join(", ", neg));

         return $"A figure of contradictions: {string.Join(" — and yet ", parts)}. " + $"These qualities pull against each other; " + $"the lord lives them out moment by moment, without easy resolution.";
      }

      // ── Personality ───────────────────────────────────────────────────────

      private static string BuildPersonality(Hero hero)
      {
         var parts = new List<string>();
         string cultureName = hero.Culture?.Name?.ToString() ?? "";
         string culturalDesc = CulturalTraditionsLoader.Get(cultureName);

         parts.Add(!string.IsNullOrEmpty(culturalDesc)
            ? culturalDesc
            : !string.IsNullOrEmpty(cultureName)
               ? $"A {cultureName} by culture."
               : "");

         parts.Add(DescribeRank(hero));

         return string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
      }

      // ── Trait score conversion ────────────────────────────────────────────

      private static int ConvertLevel(int score)
      {
         if (Math.Abs(score) <= 3) return Math.Max(-2, Math.Min(2, score));
         if (score <= -4000) return -2;
         if (score <= -1000) return -1;
         if (score >= 4000) return 2;
         if (score >= 1000) return 1;

         return 0;
      }

      private static int CountActiveTraits(Traits t)
      {
         var n = 0;
         if (t.Honor != 0) n++;
         if (t.Mercy != 0) n++;
         if (t.Valor != 0) n++;
         if (t.Generosity != 0) n++;
         if (t.Calculating != 0) n++;

         return n;
      }

      private static string DescribeRank(Hero hero)
      {
         if (hero.IsKingdomLeader) return "Holds royal authority over a kingdom.";
         if (hero.IsFactionLeader) return "Leads a faction.";
         if (hero.IsClanLeader) return "Leads a noble clan.";
         if (hero.IsLord) return "A noble lord with land and retainers.";
         // All five notable trades by name (Windmolen, 19/10/2026): a guideline written for one trade needs the
         // model to know which trade it is speaking as.
         string trade = CalradiaRemembers.Logic.StationGuidelinesPolicy.NotableTradeLine(hero.Occupation.ToString());
         if (trade != null) return trade;
         if (hero.IsGangLeader) return "Leads an underworld gang.";

         return "A person of some standing in Calradia.";
      }

      // ── Five active traits ────────────────────────────────────────────────

      private static string FiveTraitPersonality(Traits t)
      {
         // ── Covered since Sprint 8.1 ──────────────────────────────────────
         if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == -1)
            return "Mad, Bad And Dangerous To Know. Brave, warm-hearted, and giving — but bound by no oath and ruled entirely by impulse. Magnificent and catastrophic in equal measure.";
         if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == 1)
            return "The Loyal Manipulator. Fiercely protective of their inner circle, calculating in all else, and without mercy for those outside it. A wonderful friend. A terrible enemy.";
         if (t.Generosity == -1 && t.Honor == 1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == -1)
            return "Pitiless. Principled and brave — their honour is real — but utterly without compassion. Sees mercy as weakness and charity as waste. The code they follow is iron.";
         if (t.Honor == -1 && t.Valor == -1 && t.Mercy == -1 && t.Generosity == -1 && t.Calculating == 1)
            return "The Spider. No virtue but cleverness — lies, schemes, hoards, and avoids danger without shame. Rules through fear and manipulation, never through respect or love.";
         if (t.Honor == 1 && t.Valor == 1 && t.Mercy == 1 && t.Generosity == 1 && t.Calculating == -1)
            return "The Impulsive Saint. Brave, generous, honourable, and merciful — but acts entirely on feeling, never on strategy. Beloved by all, outmaneuvered by most.";

         // ── New in Sprint 8.3 full coverage ───────────────────────────────
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == -1)
            return "The Paradox. Dishonest, cruel, and impulsive — and yet brave and lavishly generous. A lord of genuine contradictions whose best and worst qualities coexist without resolution.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == 1)
            return "The Calculating Warlord. Brave, generous, and strategic — and yet dishonest and merciless. What they give is real; what they promise is not; what they plan is ruthless.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == -1 && t.Generosity == 1 && t.Calculating == 1)
            return "The Scheming Benefactor. Dishonest, cruel, and cowardly — yet somehow generous and calculating. Their gifts buy the loyalty they have no intention of earning through character.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == -1 && t.Calculating == 1)
            return "The Iron Crusader. Brave, principled, calculating, and without mercy or generosity — a lord of pure disciplined conviction. Terrifying in their consistency; exhausting in their certainty.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == -1 && t.Calculating == -1)
            return "The Berserker Knight. Brave and honourable in battle — fights with conviction and fury — but shows no mercy, gives nothing, and acts without plan. A blade rather than a lord.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == -1 && t.Generosity == -1 && t.Calculating == -1)
            return "The Wretch. No virtue, no strategy, no courage, no generosity — an absence wearing a title. The question is not how they rose; it is why they have not yet fallen.";

         return null;
      }

      // ── Four active traits ────────────────────────────────────────────────

      private static string FourTraitPersonality(Traits t)
      {
         // ── Covered since Sprint 8.1 ──────────────────────────────────────
         if (t.Generosity == 1 && t.Honor == 1 && t.Valor == 1 && t.Mercy == 1) return "Gallant. Brave, generous, honourable, and merciful — the ideal knight, if such a thing truly exists in Calradia. Inspires loyalty and earns respect even from enemies.";
         if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1 && t.Calculating == -1) return "Passionate Rogue. Bold and generous but acts on impulse without regard for oaths or plans. Magnificent in a charge, disastrous in a negotiation.";
         if (t.Generosity == 1 && t.Honor == 1 && t.Mercy == -1 && t.Calculating == -1) return "Stiff-Necked Idealist. Principled and open-handed but inflexible — demands others meet their standard and punishes those who fall short without remorse.";
         if (t.Honor == -1 && t.Valor == -1 && t.Mercy == 1 && t.Calculating == 1) return "Devious But Soft-Headed. Schemes and manipulates but cannot bring themselves to see plans through when real suffering is required. A schemer without the stomach for it.";
         if (t.Generosity == 1 && t.Honor == -1 && t.Mercy == 1 && t.Calculating == 1) return "The Charming Manipulator. Warm, generous, and compassionate on the surface — but every kindness is calculated, every friendship a future leverage point.";
         if (t.Generosity == 1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == -1) return "Reckless And Unforgiving. Bold and generous with friends, merciless and impulsive with enemies. Their feasts are legendary — and so are their vendettas.";
         if (t.Honor == -1 && t.Valor == -1 && t.Mercy == -1 && t.Calculating == -1) return "The Hollow Lord. No courage, no compassion, no honour, no cunning. Holds their title by inheritance alone. A vacancy wearing armor, waiting to be removed.";
         if (t.Honor == 1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == 1) return "The Complete Warrior. Brave, principled, merciful, and calculating — a rare and formidable combination. Either the realm's greatest protector or its most dangerous adversary.";

         // ── New in Sprint 8.3 full coverage ───────────────────────────────

         // Dishonest + Cruel + Valor/Generosity combos
         if (t.Honor == -1 && t.Mercy == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Corrupted Patron. Schemes, punishes without mercy, and still somehow distributes generosity — the gift is real, the cruelty is waiting, and the oath means nothing.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1) return "The Violent Benefactor. Fights well and gives freely but breaks faith and forgives nothing — loyalty is rewarded; betrayal is punished; oaths are optional.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Generosity == -1 && t.Calculating == 1) return "The Calculating Tyrant. Dishonest, cruel, and tight-fisted — but clever enough to make it last. Manages fear like a resource; sentiment like a liability.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == 1 && t.Calculating == 1) return "The Ruthless Commander. Fights brilliantly and schemes constantly — dishonest and merciless, but effective. Not safe to serve; not safe to oppose.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Valor == -1 && t.Calculating == 1) return "The Cowardly Schemer. Plots from behind walls, lies without shame, and never risks themselves. Genuinely dangerous only to those who trust them.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Chaotic Patron. Dishonest, cruel, and impulsive — and yet generous in ways that make no sense. The rewards are real; so is the unpredictability.";

         // Honor=-1 with Mercy=1 combos
         if (t.Honor == -1 && t.Mercy == 1 && t.Valor == -1 && t.Calculating == 1) return "The Devious Mediator. Avoids conflict, extends compassion, and plans carefully — but the agreements they negotiate cannot be trusted to hold.";
         if (t.Honor == -1 && t.Mercy == 1 && t.Generosity == 1 && t.Calculating == 1) return "The Generous Deceiver. Kind, open-handed, and calculating — but their word cannot be trusted. Every act of generosity serves a purpose they have not disclosed.";
         if (t.Honor == -1 && t.Mercy == 1 && t.Valor == 1 && t.Calculating == 1) return "The Strategic Outlaw. Brave, kind, and calculating — unreliable at every level of commitment. Their gifts are real; their alliances are temporary.";
         if (t.Honor == -1 && t.Mercy == 1 && t.Valor == 1 && t.Generosity == 1) return "The Beloved Outcast. Brave, generous, and compassionate — but their word means nothing. Easy to love; impossible to trust.";

         // Honor=-1, Valor=1 combos
         if (t.Honor == -1 && t.Valor == 1 && t.Generosity == -1 && t.Calculating == 1) return "The Calculating Mercenary. Brave and strategic but dishonest and tight-fisted — fights for whoever makes it worth their while, plans carefully, keeps the coin.";
         if (t.Honor == -1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == -1) return "The Impulsive Rebel. Brave and generous but dishonest and ungoverned — acts on feeling, gives without thinking, breaks oaths without noticing.";
         if (t.Honor == -1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == 1) return "The Ambitious Outlaw. Brave, generous, and calculating — but unreliable. Fights brilliantly, distributes reward deliberately, keeps no oath that costs them.";

         // Honor=-1, Valor=-1 combos
         if (t.Honor == -1 && t.Valor == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Fearful Patron. Schemes and distributes generosity from a place of safety — dishonest, cowardly, yet somehow useful to those beneath them.";
         if (t.Honor == -1 && t.Valor == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Impulsive Coward. Gives and retreats in equal measure, without plan or principle. Well-meaning in their way; thoroughly unreliable in every other.";

         // Mercy=-1, Valor=1 combos
         if (t.Mercy == -1 && t.Valor == 1 && t.Generosity == -1 && t.Calculating == -1) return "The Reckless Fighter. Fights hard, acts without plan, keeps what they take: aggression with neither strategy nor generosity to balance it.";
         if (t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == -1) return "The Bloody Champion. Fights hard and gives generously but acts on impulse and forgives nothing — magnificent and terrible in equal measure.";
         if (t.Mercy == -1 && t.Valor == 1 && t.Generosity == -1 && t.Valor == 1) return "The Iron Soldier. Fights with precision, shows no mercy, keeps every resource — principled about violence, indifferent to everything else."; // edge case

         // Mercy=1, Valor=-1 combos
         if (t.Mercy == 1 && t.Valor == -1 && t.Generosity == -1 && t.Calculating == -1) return "The Helpless Idealist. Kind, cautious, tight-fisted, and impulsive — every compensating virtue undercut by another flaw. Means the best; achieves the least.";
         if (t.Mercy == 1 && t.Valor == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Impulsive Benefactor. Kind and generous but avoids danger and acts without plan — gives freely, forgives recklessly, plans poorly.";
         if (t.Mercy == 1 && t.Valor == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Careful Benefactor. Avoids danger and extends mercy, distributes resources with deliberate care. More capable than their aversion to risk suggests.";
         if (t.Mercy == 1 && t.Valor == -1 && t.Generosity == -1 && t.Calculating == 1) return "The Calculated Compassion. Plans carefully and extends mercy, keeps their resources close. Finds ways to help that cost very little and achieve quite a lot.";

         // Honor=1, Mercy=-1 combos
         if (t.Honor == 1 && t.Mercy == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Generous Inquisitor. Rewards loyalty and punishes failure without mercy — impulsive in execution, principled in code, open with resources when earned.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1) return "The Iron-Hearted Champion. Brave, principled, and generous, but shows no mercy — fights for the cause, rewards loyalty, forgives nothing.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Strategic Inquisitor. Principled, generous, calculating, and without compassion — distributes reward with precision and enforces the code without hesitation.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == -1 && t.Calculating == 1) return "The Bureaucratic Idealist. Keeps their word, plans carefully, shows no mercy, and avoids the front line. The administrative face of a cold code.";

         // Honor=1, Valor=1 combos
         if (t.Honor == 1 && t.Valor == 1 && t.Generosity == -1 && t.Calculating == 1) return "The Resourceful Warrior. Brave, principled, calculating, and tight with resources — takes what's needed and gives nothing extra. Wins wars carefully.";
         if (t.Honor == 1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == -1) return "The Chivalric Lord. Brave, honourable, and merciful but ungoverned by strategy — the perfect medieval ideal, occasionally disastrous in a complex world.";

         // Honor=1, Mercy=1 combos
         if (t.Honor == 1 && t.Mercy == 1 && t.Generosity == -1 && t.Calculating == 1) return "The Calculating Idealist. Principled, compassionate, and deliberate — careful with resources while generous with mercy and honesty. Achieves good through strategy.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Valor == -1 && t.Calculating == 1) return "The Diplomatic Idealist. Principled, compassionate, and calculating — finds alternatives to violence with genuine commitment and careful planning.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Generosity == -1 && t.Calculating == -1) return "The Spartan Saint. Keeps their word, shows genuine compassion, gives nothing away, and acts on instinct. Principle and kindness without resource or strategy.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Valor == -1 && t.Generosity == 1) return "The Gentle Patron. Honourable, compassionate, generous, and cautious — the kind of lord who builds lasting peace without ever drawing a sword.";

         // Honor=1, Generosity combos with Mercy neutral
         if (t.Honor == 1 && t.Generosity == 1 && t.Valor == -1 && t.Calculating == 1) return "The Wise Steward. Principled, generous, careful, and strategic — governs well, plans thoroughly, and avoids violence without avoiding responsibility.";
         if (t.Honor == 1 && t.Generosity == 1 && t.Calculating == -1 && t.Valor == -1) return "The Generous Idealist. Principled, generous, and impulsive — acts on principle and gives freely but never plans for the consequences. Admirable and costly.";

         // Mercy=-1, Generosity/-1 combos
         if (t.Mercy == -1 && t.Generosity == -1 && t.Honor == 1 && t.Calculating == -1) return "The Harsh Spartan. Principled and impulsive, neither generous nor merciful — keeps their word and lives by a code that demands everything and gives nothing back.";
         if (t.Mercy == -1 && t.Generosity == -1 && t.Valor == -1 && t.Calculating == 1) return "The Calculating Miser. Plans carefully, hoards everything, shows no mercy — never risks themselves and never extends anything to anyone without a return.";

         // Misc remaining 4-trait combos from freq >= 1
         if (t.Mercy == 1 && t.Valor == 1 && t.Generosity == 1 && t.Calculating == 1) return "The Beloved Strategist. Brave, merciful, generous, and calculating — leads with both heart and mind. Rarely wrong; almost always followed.";
         if (t.Mercy == 1 && t.Valor == 1 && t.Honor == -1 && t.Calculating == 1) return "The Cunning Champion. Brave, compassionate, and strategic — but their word is not reliable. The cause they fight for may be just; the promises they make are not.";
         if (t.Mercy == 1 && t.Generosity == 1 && t.Honor == -1 && t.Calculating == 1) return "The Scheming Saint. Kind, generous, and calculating — but dishonest at the foundation. Every act of compassion is real and serves a purpose at the same time.";
         if (t.Honor == -1 && t.Valor == 1 && t.Mercy == -1 && t.Calculating == 1) return "The Ruthless Tactician. Fights hard and plans carefully; dishonest, and merciless when it serves. A dangerous enemy and an unreliable ally.";
         if (t.Honor == -1 && t.Valor == 1 && t.Mercy == 1 && t.Calculating == -1) return "The Wild Knight. Brave and compassionate but impulsive and untrustworthy — charges into danger for the right reasons and breaks oaths without noticing.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Valor == 1 && t.Generosity == -1) return "The Frugal Paragon. Brave, principled, and merciful — holds everything else close. Will fight for you; will not fund you. Generous with courage; not with coin.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == -1 && t.Generosity == 1) return "The Exacting Patron. Principled and generous, but shows no mercy and avoids the front line. Rewards well; punishes the unworthy; sends others to do the fighting.";

         return null;
      }

      // ── Romantic diagnostic ───────────────────────────────────────────────

      /// <summary>
      ///   Logs the orientation, gender, and player-compatibility verdict of an
      ///   existing <see cref="RomanticProfile" />. Mirrors PromptBuilder.IsPlayerCompatible
      ///   so the log explains exactly why a ROMANTIC NATURE section is shown or suppressed.
      /// </summary>
      private static void LogExistingRomanticCompatibility(NpcProfile profile)
      {
         RomanticProfile r = profile.Romantic;
         bool playerIsFemale = Hero.MainHero != null && Hero.MainHero.IsFemale;
         bool compatible = r != null && r.IsCompatibleWith(playerIsFemale);

         ModLog.Info(
            $"  Existing Romantic for {profile.Name}: " + $"Orientation={r?.Orientation}, IsFemale={r?.IsFemale}, PlayerIsFemale={playerIsFemale} → " + $"{(compatible ? "compatible (ROMANTIC NATURE shown)" : "incompatible (boundary section active)")}");
      }

      // ── One active trait ──────────────────────────────────────────────────

      private static string OneTraitPersonality(Traits t)
      {
         if (t.Honor == 1) return "Honourable. Their word is their bond — they will refuse a profitable lie over a costly truth, and hold others to the same standard.";
         if (t.Mercy == 1) return "Kind-Hearted. Extends compassion where others would not, believes in second chances. Can be mistaken for weakness by those who prey on it.";
         if (t.Valor == 1) return "Daring. Seeks glory in danger and scorns those who hesitate. The battlefield is where they feel most alive.";
         if (t.Generosity == 1) return "Generous To Friends. Shares freely with those who have earned their loyalty — but gives nothing to strangers who have not yet proved their worth.";
         if (t.Calculating == 1) return "Ambitious. Every word is weighed, every favor catalogued. Speaks with purpose and expects the same from others.";
         if (t.Valor == -1) return "Conventional. Avoids unnecessary risk and prefers the safe path — not cowardly, but no hero either. Stability over glory.";
         if (t.Honor == -1) return "Unreliable. Keeps oaths loosely and breaks them easily. Likeable enough when it costs nothing; dangerous when tested.";
         if (t.Mercy == -1) return "Hard. Shows no sentiment for those who suffer — sees compassion as a weakness. Not needlessly cruel; simply unmoved.";
         if (t.Generosity == -1) return "Tight-Fisted. Holds coin and favor with both hands. Earns loyalty poorly; keeps it only through fear or utility.";
         if (t.Calculating == -1) return "Headstrong. Acts before thinking and lets instinct lead. Refreshingly direct or dangerously reckless, depending on the day.";

         return null;
      }

      // ── Orientation / spouse coherence ───────────────────────────────────

      private static bool OrientationConflictsWithSpouse(RomanticProfile romantic, Hero hero)
      {
         if (romantic == null || hero.Spouse is not {IsAlive: true}) return false;

         bool spouseOppositeGender = hero.Spouse.IsFemale != hero.IsFemale;

         return spouseOppositeGender && romantic.Orientation == SexualOrientation.Homosexual || !spouseOppositeGender && romantic.Orientation == SexualOrientation.Heterosexual;
      }

      // ── Archetype routing with diagnostic logging ─────────────────────────

      private static string RetrievePersonalityFor(Hero hero)
      {
         int rawH = hero.GetTraitLevel(DefaultTraits.Honor);
         int rawM = hero.GetTraitLevel(DefaultTraits.Mercy);
         int rawV = hero.GetTraitLevel(DefaultTraits.Valor);
         int rawG = hero.GetTraitLevel(DefaultTraits.Generosity);
         int rawC = hero.GetTraitLevel(DefaultTraits.Calculating);

         var levels = new Logic.PersonaTraitLevels {
            Honor = ConvertLevel(rawH), Mercy = ConvertLevel(rawM), Valor = ConvertLevel(rawV),
            Generosity = ConvertLevel(rawG), Calculating = ConvertLevel(rawC)
         };

         // The catalog is looked up by each trait's DIRECTION: it matches only -1/+1, so a hero at -2/+2, the game's
         // strongest, used to meet no archetype at all. What is extreme is said after it (03/10/2026).
         Traits t = new() {
            Honor = Logic.PersonaTraitIntensity.Direction(levels.Honor),
            Mercy = Logic.PersonaTraitIntensity.Direction(levels.Mercy),
            Valor = Logic.PersonaTraitIntensity.Direction(levels.Valor),
            Generosity = Logic.PersonaTraitIntensity.Direction(levels.Generosity),
            Calculating = Logic.PersonaTraitIntensity.Direction(levels.Calculating)
         };

         int count = CountActiveTraits(t);

         ModLog.Info(
            $"Traits for {hero.Name}: " + $"H={rawH}→{t.Honor}  M={rawM}→{t.Mercy}  V={rawV}→{t.Valor}  " + $"G={rawG}→{t.Generosity}  C={rawC}→{t.Calculating}  (active={count})");

         string archetype = count switch {
            1 => OneTraitPersonality(t),
            2 => TwoTraitPersonality(t),
            3 => ThreeTraitPersonality(t),
            4 => FourTraitPersonality(t),
            5 => FiveTraitPersonality(t),
            _ => null
         };

         if (string.IsNullOrEmpty(archetype))
         {
            if (count > 0)
               ModLog.Info(
                  $"  FALLBACK — combo not in catalog: " + $"H:{t.Honor} M:{t.Mercy} V:{t.Valor} G:{t.Generosity} C:{t.Calculating}");

            archetype = count > 0
               ? BuildDynamicFallback(t)
               : "Thoroughly Conventional. Shaped more by circumstance than by any particular virtue or vice.";
         }

         string extreme = Logic.PersonaTraitIntensity.Clause(levels);
         if (extreme.Length > 0) archetype = $"{archetype} {extreme}";

         ModLog.Info($"  Archetype: {archetype}");

         return archetype;
      }

      // ── Three active traits ───────────────────────────────────────────────

      private static string ThreeTraitPersonality(Traits t)
      {
         // ── Covered since Sprint 8.1 ──────────────────────────────────────
         if (t.Generosity == -1 && t.Valor == 1 && t.Mercy == -1) return "Fierce And Cruel. Brave in battle and vicious in victory — does not give, does not forgive. Enemies fear them; subjects endure them.";
         // Generosity -1, Honor -1, Mercy -1 is "The Small Tyrant" below. It read "Sadistic... a predator" here until 02/10/2026:
         // three ordinary leanings played as a monster (tashmetu). See RetiredArchetypes.
         if (t.Generosity == 1 && t.Honor == 1 && t.Calculating == -1) return "Noble And Generous. High-minded and open-handed, acts on principle before strategy. May be outmaneuvered by sharper minds, but never loses their honor.";
         if (t.Generosity == 1 && t.Valor == -1 && t.Calculating == 1) return "Decent But Cautious. Generous and strategic, but avoids the front line. Their gifts and plans speak louder than their sword arm.";
         if (t.Generosity == -1 && t.Mercy == -1 && t.Calculating == 1) return "Icy. Hoards, schemes, and shows no compassion. Everything is a transaction; sentiment is a liability they have long since shed.";
         if (t.Honor == -1 && t.Valor == -1 && t.Calculating == 1) return "Untrustworthy. Neither brave nor honest, but clever enough to be dangerous. Lies when cornered, retreats when challenged, schemes always.";
         if (t.Generosity == 1 && t.Honor == -1 && t.Valor == 1) return "Clever And Roguish. Bold, charming, and free with coin — but oaths sit lightly on their shoulders. Thrilling company, uncertain ally.";
         if (t.Honor == 1 && t.Valor == 1 && t.Calculating == 1) return "The Strategic Warrior. Brave, principled, and calculating — fights with a plan, holds to their word, and never charges without purpose. A formidable lord.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Calculating == -1) return "The Brute. Dishonest, cruel, and impulsive — acts on base instinct without plan or principle. Dangerous precisely because they are unpredictable.";
         if (t.Generosity == -1 && t.Valor == -1 && t.Calculating == 1) return "The Shadow Schemer. Risk-averse, tight-fisted, and calculating. Never fights when they can maneuver; never spends when they can leverage debt instead.";
         if (t.Honor == 1 && t.Mercy == -1 && t.Calculating == 1) return "The Cold Judge. Principled and strategic, utterly without compassion. Enforces their code precisely and punishes deviation without a moment's hesitation.";
         if (t.Mercy == 1 && t.Generosity == 1 && t.Valor == 1) return "The Beloved Warrior. Brave, generous, and compassionate — the kind of lord who inspires songs. Not always wise, but deeply loved by those they lead.";

         // ── New in Sprint 8.3 full coverage ───────────────────────────────

         // Valor=1, Mercy=-1 variants
         if (t.Mercy == -1 && t.Valor == 1 && t.Calculating == -1) return "The Pitiless Hothead. Fights hard and acts without plan, and spares no one who stands against them. Quick to anger, slow to forgive.";
         if (t.Mercy == -1 && t.Valor == 1 && t.Calculating == 1) return "The Cold Soldier. Plans the fight and carries it out without hesitation or pity. Skilled and unsentimental: a hard enemy to face.";
         if (t.Mercy == -1 && t.Valor == 1 && t.Generosity == -1) return "The Warlord. Fights hard, forgives nothing, gives nothing. Keeps victories and coin with equal jealousy. The battlefield is their only natural element.";
         if (t.Mercy == -1 && t.Valor == 1 && t.Generosity == 1) return "The Violent Patron. Rewards loyalty lavishly and punishes failure without mercy — generous to those in the circle, dangerous to those outside it.";

         // Valor=1, Generosity=1 variants
         if (t.Valor == 1 && t.Generosity == 1 && t.Calculating == -1) return "The Reckless Champion. Brave and open-handed but governed entirely by feeling — magnificent in the moment, chaotic in the long term. Campaigns won by spirit, lost by lack of plan.";
         if (t.Valor == 1 && t.Generosity == 1 && t.Calculating == 1) return "The War Captain. Brave, generous, and methodical — fights well, rewards loyalty, plans ahead. Exactly what a war leader should be and rarely is.";

         // Valor=1, Generosity=-1 variants
         if (t.Valor == 1 && t.Generosity == -1 && t.Calculating == -1) return "The Hungry Soldier. Fights hard and keeps everything — glory for themselves, nothing for others, no plan beyond the next battle. Tireless and ungrateful.";

         // Honor=-1 variants with Generosity and Calculating
         if (t.Honor == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Calculating Benefactor. Generous and clever, unreliable on oaths — every gift is placed strategically, loyalty bought rather than earned, accounts kept with precision.";
         if (t.Honor == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Impulsive Spender. Generous and unreliable, acts before thinking. The gifts come without calculation; the oaths go without commitment.";

         // Honor=1 with Generosity variants
         if (t.Honor == 1 && t.Generosity == 1 && t.Calculating == -1) return "The Noble Fool. Generous, principled, and entirely ungoverned by strategy. Does the right thing at the wrong moment with consistent reliability.";
         if (t.Honor == 1 && t.Generosity == 1 && t.Calculating == 1) return "The Principled Patron. Generous, honest, and deliberate — every gift is thoughtful, every oath is kept, every plan is measured. A genuinely good lord by design.";
         if (t.Honor == 1 && t.Generosity == -1 && t.Calculating == 1) return "The Cold Steward. Manages resources with iron precision and keeps every oath — warmth and generosity are luxuries they have calculated out of the budget.";
         if (t.Honor == 1 && t.Generosity == -1 && t.Calculating == -1) return "The Impulsive Ascetic. Keeps their word without planning for it — principled and reckless, holds nothing for others and charges into decisions without looking ahead.";

         // Honor=1 with Valor variants
         if (t.Honor == 1 && t.Valor == 1 && t.Calculating == -1) return "The Fearless Idealist. Fights with conviction and acts on principle without pausing to plan — charges into any battle they believe in, which is most of them.";
         if (t.Honor == 1 && t.Valor == -1 && t.Calculating == 1) return "The Statesman. Principled, careful, and calculating — fights rarely and only when necessary. Prefers governance to battle, wins through position rather than force.";
         if (t.Honor == 1 && t.Valor == 1 && t.Generosity == -1) return "The Spartan. Fights with both courage and principle, has no use for comfort or generosity. Leads by example; expects others to manage without reward.";
         if (t.Honor == 1 && t.Valor == -1 && t.Generosity == -1) return "The Stingy Idealist. Principled and unmartial, gives nothing away. Lives by the letter of the code and the letter of the budget — honest and ungenerous in equal measure.";
         if (t.Honor == 1 && t.Valor == -1 && t.Generosity == 1) return "The Principled Steward. Honourable and generous but avoids danger — manages well, gives freely, and lets others do the fighting.";

         // Honor=1 with Mercy variants
         if (t.Honor == 1 && t.Mercy == -1 && t.Generosity == -1) return "The Austere Idealist. Lives by a strict code, shows no mercy, keeps what they earn. Principle without warmth, honor without generosity — correct and cold in equal measure.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Generosity == -1) return "The Principled Recluse. Keeps their word and shows genuine compassion but holds their resources tightly. Will not lie; will not give; will always mean what they say.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Valor == 1) return "The Virtuous Knight. Brave, honourable, and merciful — the old ideal, observed without irony. Lacks only cunning; makes up for it in consistency.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Calculating == 1) return "The Righteous Strategist. Principled, compassionate, and calculating — does right through careful planning rather than impulsive action. Slow, deliberate, and correct.";
         if (t.Honor == 1 && t.Mercy == 1 && t.Generosity == 1) return "The Compassionate Noble. Principled, kind, and generous — complete in every virtue except boldness. The gentlest version of greatness.";

         // Honor=1 with Mercy=-1 and Valor=1
         if (t.Honor == 1 && t.Mercy == -1 && t.Valor == 1) return "The Iron Blade. Brave and principled but without compassion — fights with honor, kills without hesitation, shows no sentiment before or after the blade.";

         // Mercy=1 variants
         if (t.Mercy == 1 && t.Valor == 1 && t.Calculating == -1) return "The Impulsive Knight. Brave and compassionate but ungoverned — charges into danger and back into mercy without calculation. Noble and inefficient in equal measure.";
         if (t.Mercy == 1 && t.Valor == -1 && t.Calculating == 1) return "The Diplomatic Dove. Avoids conflict wherever possible, plans carefully, extends compassion to adversaries as readily as allies. Negotiates what others fight for.";
         if (t.Mercy == 1 && t.Generosity == 1 && t.Calculating == -1) return "The Warm Heart. Gives freely, forgives recklessly, plans not at all. Beloved by those they help; vulnerable to those who notice the lack of strategy.";
         if (t.Mercy == 1 && t.Generosity == 1 && t.Calculating == 1) return "The Helpful Patron. Compassionate, generous, and strategic — extends mercy with deliberate care and ensures every act of kindness is well-placed.";
         if (t.Mercy == -1 && t.Generosity == 1 && t.Calculating == 1) return "The Cold Patron. Distributes resources with precision and punishes failure without mercy. Generosity is a tool; loyalty is the currency; compassion is not in the ledger.";
         if (t.Mercy == -1 && t.Generosity == -1 && t.Calculating == -1) return "The Mean Brute. Hoards, acts on impulse, shows no compassion. Not calculating enough to be feared, not brave enough to be respected — simply unpleasant in every direction.";

         // Honor=-1, Mercy=-1 variants
         if (t.Honor == -1 && t.Mercy == -1 && t.Calculating == 1) return "The Petty Tyrant. Dishonest, cruel, and clever enough to make it stick. Not grand enough for genuine villainy — small-scale, persistent, and locally feared.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Generosity == 1) return "The Cruel Patron. Hands out coin and shows no mercy — buys loyalty while punishing failure. A lord worth following for the resources and fearing for the consequences.";
         if (t.Honor == -1 && t.Mercy == -1 && t.Generosity == -1) return "The Small Tyrant. Dishonest, cruel, and tight-fisted. Wears authority as a weapon and uses every kind of power poorly.";

         // Honor=-1, Generosity=-1, Calculating=1
         if (t.Honor == -1 && t.Generosity == -1 && t.Calculating == 1) return "The Calculating Hoarder. Schemes and accumulates, breaks faith when convenient. Manages resources for their own benefit without scruple or sentiment.";

         // Honor=-1, Valor variants
         if (t.Honor == -1 && t.Valor == -1 && t.Generosity == -1) return "The Lowest Lord. Dishonest, cowardly, and grasping — every lord's vice concentrated in one person. Holds their title through circumstance alone.";
         if (t.Honor == -1 && t.Valor == -1 && t.Generosity == 1) return "The Giving Coward. Generous to a fault but neither brave nor trustworthy. The gifts are real; the courage and the oaths are not.";

         // Honor=-1, Mercy=1, Calculating=1
         if (t.Honor == -1 && t.Mercy == 1 && t.Calculating == 1) return "The Scheming Peacemaker. Extends mercy and plans carefully, but their word cannot be relied upon. Means well and is unreliable despite it.";

         // Valor=-1 variants
         if (t.Valor == -1 && t.Generosity == 1 && t.Calculating == -1) return "The Profligate Steward. Spends freely and acts without plan, but keeps well away from danger. Loveable in peacetime; a liability when things get difficult.";

         // Mercy=1, Valor=-1, Generosity=-1
         if (t.Mercy == 1 && t.Valor == -1 && t.Generosity == -1) return "The Reluctant Lord. Kind at heart but neither brave nor generous enough to act well on it — compassion without the means or courage to express it.";

         // Honor=-1, Valor=1, Generosity=1
         if (t.Honor == -1 && t.Valor == 1 && t.Generosity == 1) return "The Bold Outcast. Brave and generous but unreliable — fights hard and gives freely; oaths are a formality they observe loosely at best.";

         // Honor=1, Mercy=-1 additional
         if (t.Honor == 1 && t.Mercy == -1 && t.Calculating == -1) return "The Harsh Idealist. Principled and impulsive, completely without compassion. Acts on their code in the moment and punishes those who fall short of it immediately.";

         // Mercy=1, Valor=1, Calculating=-1 → already covered above
         // Honor=1, Mercy=1, Calculating=1 → already covered

         // Honor=1, Generosity=1, Mercy=-1 would go to FourTrait (4 active). Skip here.

         // Remaining misc 3-trait combos from freq=1
         if (t.Mercy == 1 && t.Valor == -1 && t.Calculating == -1) return "The Careless Pacifist. Avoids conflict and extends compassion without any particular plan behind it. Well-meaning and strategically inert.";
         if (t.Mercy == 1 && t.Generosity == -1 && t.Calculating == 1) return "The Frugal Compassion. Plans carefully and shows real mercy — but keeps the purse shut. Finds ways to help that cost coin to someone else.";
         if (t.Honor == -1 && t.Valor == -1 && t.Generosity == 1) return "The Fearful Giver. Generous and thoroughly lacking in courage and integrity. The gifts are genuine; everything else is to avoid confrontation.";

         return null;
      }

      // ── Two active traits ─────────────────────────────────────────────────

      private static string TwoTraitPersonality(Traits t)
      {
         // ── Covered since Sprint 8.1 ──────────────────────────────────────
         if (t.Honor == 1 && t.Calculating == 1) return "The Just Schemer. Desires fairness and works hard to achieve it — but through careful strategy, not impulsive action. Slow to commit, precise in judgment.";
         if (t.Calculating == -1 && t.Valor == 1) return "Brave But Headstrong. Charges where others hesitate, driven by courage rather than caution. Plans are for the timid — deeds speak louder.";
         if (t.Generosity == 1 && t.Valor == 1) return "The Warrior's Friend. Brave and open-handed, loyal to a fault. Worth having at your side in battle and at your table in peace.";
         if (t.Generosity == 1 && t.Mercy == -1) return "Dangerous Ally. Generous and warm to friends, coldly unforgiving to enemies. Cross them once and the warmth vanishes entirely.";
         if (t.Mercy == 1 && t.Valor == -1) return "Kind But Cautious. Gentle in spirit and careful in action — will not charge into danger, but will quietly stand by those who need them.";
         if (t.Mercy == 1 && t.Calculating == 1) return "The Helpful Strategist. Uses their wits in service of others. Quietly arranges things to benefit those they care for — not always transparently.";
         if (t.Valor == 1 && t.Calculating == 1) return "Daring And Ambitious. Brave enough to act boldly, clever enough to act wisely. A dangerous combination in an enemy, invaluable in an ally.";
         if (t.Generosity == 1 && t.Honor == 1) return "Noble And Generous. Holds to principle and opens their purse freely. The kind of lord songs are written about — and rivals quietly resent.";
         if (t.Valor == -1 && t.Generosity == -1) return "Aloof. Keeps to themselves, neither spending freely nor risking their neck. Cold company, but reliable within narrow limits.";
         if (t.Mercy == 1 && t.Generosity == 1) return "Considerate. Puts others before themselves, generous with both coin and compassion. Can be taken advantage of by the ruthless.";
         if (t.Generosity == -1 && t.Calculating == 1) return "The Miser's Mind. Tight-fisted and calculating — every coin spent is an investment, every favor a future debt to collect.";
         if (t.Generosity == -1 && t.Honor == 1) return "Just But Ungrateful. Honest and principled, but expects others to earn every scrap. Will not lie, but will not reward loyalty either.";
         if (t.Generosity == -1 && t.Honor == -1) return "Dishonourable In Every Way. Breaks oaths and hoards coin without shame or remorse. Cannot be trusted in any dealing, ever.";
         if (t.Valor == 1 && t.Mercy == 1) return "Brave But Merciful. Fights when needed, spares when possible. Commands respect from allies and defeated enemies alike.";
         if (t.Honor == 1 && t.Mercy == 1) return "Decent And Kind. Principled and compassionate — rare in Calradia. Others mistake it for weakness; they call it the only way to live.";
         if (t.Generosity == 1 && t.Calculating == 1) return "The Patron. Rewards loyalty precisely and generously, keeping careful account of who serves them well. A good lord to follow.";
         if (t.Generosity == 1 && t.Honor == -1) return "Clever And Roguish. Charming and open-handed, but plays fast and loose with oaths. Likeable — just do not shake their hand on a deal.";
         if (t.Generosity == 1 && t.Calculating == -1) return "Profligate. Spends freely without thought of tomorrow. Beloved by those who benefit, doomed by those who plan against them.";
         if (t.Mercy == -1 && t.Calculating == 1) return "Ruthless. Cold, deliberate, and without compassion for those in the way. Does not hate enemies — simply removes them.";
         if (t.Calculating == 1 && t.Honor == -1) return "Cunning. Bends the truth when it suits them and plans three moves ahead. Never show them your hand.";
         if (t.Valor == 1 && t.Honor == 1) return "Brave And Honourable. Fights with courage and keeps their word. An old-fashioned lord in an age that has largely forgotten what that means.";
         if (t.Honor == -1 && t.Valor == 1) return "The Oath-Breaker. Brave enough to fight anyone, but their word means nothing. Will charge into battle beside you today and betray you tomorrow if it serves them.";
         if (t.Mercy == -1 && t.Honor == 1) return "Hard Justice. Principled and unforgiving — upholds their code to the letter but shows no compassion in doing so. Right and merciless in equal measure.";
         if (t.Mercy == -1 && t.Valor == -1) return "Cold And Cowardly. Neither brave nor kind. Avoids danger and shows no mercy when others face it in their place.";
         if (t.Calculating == -1 && t.Honor == 1) return "Impulsive But Honest. Says what they mean and acts on instinct — no schemes, no plots, just blunt principle and unfiltered reaction.";
         if (t.Mercy == -1 && t.Generosity == -1) return "Bitter And Hard. Neither gives nor forgives. Life in Calradia has not been kind to them, and they see no reason to be kind to others.";

         // ── New in Sprint 8.3 full coverage ───────────────────────────────
         if (t.Mercy == -1 && t.Valor == 1) return "The Hard Soldier. Brave, and without mercy for those who oppose them: spares no beaten enemy and does not dwell on it. A warrior first, whatever title they hold.";
         if (t.Honor == -1 && t.Valor == -1) return "Cowardly And Dishonest. Avoids danger and breaks oaths with equal ease. The combination of unreliability and self-preservation makes them the most useless kind of lord.";
         if (t.Valor == -1 && t.Generosity == 1) return "The Generous Rearguard. Gives freely and governs with care, but will not stand in the front line. Coin and comfort flow from their hand; danger they leave to others.";
         if (t.Honor == -1 && t.Generosity == 1) return "The Open-Handed Liar. Generous and charming, breaks faith without shame. The gifts are real; the promises are not.";
         if (t.Honor == -1 && t.Mercy == 1) return "The Soft Schemer. Compassionate and dishonest — genuinely moved by suffering, genuinely unable to be trusted. Kindness offered freely; reliability absent entirely.";
         if (t.Valor == -1 && t.Calculating == 1) return "The Careful Planner. Thinks before acting, avoids unnecessary risk. Not a soldier but a tactician — prefers to win without fighting.";
         if (t.Generosity == -1 && t.Calculating == -1) return "The Reckless Miser. Hoards resources and makes no plan to protect them. Acts on instinct, clutches what they have. Unpleasant in ways that do not even serve themselves.";
         if (t.Valor == 1 && t.Generosity == -1) return "The Fighting Miser. Brave in battle, closed in all other ways — glory is shared, coin and praise are not. Others fight for them; they fight for themselves.";
         if (t.Mercy == 1 && t.Generosity == -1) return "The Sympathetic Recluse. Genuinely feels the suffering of others but keeps their resources close. Compassionate without being generous — which frustrates those who hoped for more.";
         if (t.Mercy == -1 && t.Calculating == -1) return "Callous And Reckless. Acts without thinking, feels without caring. Neither cruel enough to be feared nor bold enough to be respected — simply careless in every direction.";
         if (t.Mercy == 1 && t.Calculating == -1) return "The Warm Fool. Moved by feeling and acts on instinct — extends mercy freely, gives impulsively, plans not at all. Loveable and exploitable in equal measure.";
         if (t.Honor == -1 && t.Valor == 1) return "The Brave Liar. Fights well and means it; swears oaths and breaks them. Courage is genuine; character is not.";
         if (t.Honor == 1 && t.Valor == -1) return "The Careful Idealist. Principled and unheroic — holds their code without putting their body in danger. Will not bend their word; will not charge into a blade for it either.";
         if (t.Honor == 1 && t.Calculating == -1) return "The Honest Hothead. Their word is good; their timing is terrible. Acts before the full implications are clear and says exactly what they mean before they have thought it through.";
         if (t.Generosity == -1 && t.Calculating == 1) return "The Miser's Mind. Tight-fisted and calculating — every coin spent is an investment, every favor a future debt to collect."; // already above, but leaving for safety

         return null;
      }

      #endregion
   }

   public class Traits
   {
      public int Calculating;
      public int Generosity;
      public int Honor;
      public int Mercy;
      public int Valor;
   }
}