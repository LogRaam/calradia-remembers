// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Sprint 17 — Captive Encounter System.
   ///   Each game day, while the player is a prisoner in Hardcore mode, rolls a
   ///   probability check to see whether their captor initiates an encounter.
   ///   When triggered, a popup warns the player, then opens the chat window with
   ///   the captor as main NPC and optional witnesses (lords, soldiers, or collective).
   ///   Probability: 5 % × days in captivity (cap 40 %) + kink bonuses.
   ///   Delay: minimum 2 days before first trigger.
   ///   Cooldown: 3 days between encounters.
   ///   Level requirement: Hardcore only.
   /// </summary>
   public static class CaptiveEncounterService
   {
      // Timing constants (the probability + intent tuning now lives in the pure, unit-tested
      // CalradiaRemembers.Logic.CaptiveEncounterPolicy).
      private const int CooldownDays = 2;
      private const int MinDaysBeforeFirstTrigger = 1;

      private static readonly string[] BanditEpithets = {
         "the Scar",
         "One-Eye",
         "the Crow",
         "Bloodtooth",
         "the Cur",
         "Knuckles",
         "the Wolf",
         "Blackhand",
         "Gutter",
         "the Jackal",
         "Gravesong",
         "the Bone Harvester",
         "the Butcher",
         "Ironjaw",
         "Skullcrusher",
         "the Quiet Calamity",
         "Shadowstep",
         "the Unseen",
         "the Pale",
         "the Faceless Fox",
         "Widowmaker",
         "the Red Raven",
         "the Blade"
      };

      // A distinctive manner per chief — gender-neutral so it fits a man or a woman. Gives each
      // synthesized captor a different voice and behaviour so scenes stop feeling formulaic.
      private static readonly string[] BanditManners = {
         "Given to mocking laughter — cruelty turned into a joke.",
         "Cold and nearly silent, letting hands and blade do the talking.",
         "A boastful braggart who loves the sound of their own threats.",
         "Superstitious — muttering of omens and road-gods even mid-atrocity.",
         "Sickeningly courteous, soft words wrapped around a knife.",
         "Quick-tempered and unpredictable — calm one breath, savage the next.",
         "Treats it all as a bored game, toying with prey for amusement.",
         "Greedy above all, weighing everyone by what they can fetch.",
         "A hard disciplinarian who craves obedience as much as plunder.",
         "Filthy-mouthed and lewd, relishing every appetite aloud."
      };

      private static readonly string[] BanditNamesF = {
         "Sable",
         "Mara",
         "Vesha",
         "Ryka",
         "Brusa",
         "Astrid",
         "Gerda",
         "Ingrid",
         "Brigid",
         "Maud",
         "Elyn",
         "Rowenna"
      };

      // ── Bandit/pirate captor synthesis (no real hero exists for these parties) ──

      private static readonly string[] BanditNamesM = {
         // Original names kept
         "Garron", "Vask", "Hroth", "Dax", "Korl", "Brennir",
         "Tarl", "Ulf", "Skoll", "Reaver",

         // New medieval-style bandit names
         "Aldric", "Baldwin", "Cedric", "Edmund", "Geoffrey",
         "Gilbert", "Hamon", "Hugh", "Ivo", "Jocelyn",
         "Lambert", "Miles", "Osric", "Piers", "Ranulf",
         "Reginald", "Tristan", "Walter", "Wymond",

         "Albrecht", "Conrad", "Dietrich", "Eberhard", "Friedrich",
         "Heinrich", "Konrad", "Leopold", "Otto", "Reinhard",
         "Siegfried", "Ulrich", "Wolfgang",

         "Armand", "Bertrand", "Etienne", "Guillaume", "Raoul",
         "Reynaud", "Thibault",

         "Alasdair", "Duncan", "Lachlan", "Malcolm", "Rory",

         // Strong / Warrior / Bandit-flavored
         "Bastian", "Draven", "Garrick", "Harlan", "Roderick",
         "Thorne", "Viktor", "Wolfram", "Godric", "Everard",
         "Fulke", "Oswin", "Percival", "Corvin", "Balthazar"
      };

      // Each of the chief's men gets a distinct AGENDA — their own want that pushes the scene, so
      // witnesses argue, demand, grab, and create friction instead of only leering and commenting.
      private static readonly string[] BanditWitnessAgendas = {
         "A filthy, grinning cutthroat, impatient for his OWN turn — he keeps pushing the chief to hurry and share the prize, and grumbles the longer he is made to wait.",
         "A greedy, calculating thug who thinks the captive is worth far more sold or ransomed WHOLE — he argues against spoiling the goods and resents every mark left on her.",
         "A vicious sadist who eggs on every cruelty and keeps suggesting something worse — pain delights him more than pleasure, and he wants to hear her scream.",
         "A crude joker who jeers and makes filthy jokes, narrating aloud exactly what he would do to her and mocking her struggles.",
         "A surly rival who resents taking orders — he tests the chief's authority, reaches in without leave, and pushes his luck.",
         "A hulking brute of few words who follows orders with relish — pinning the captive, hauling her into position, holding her still.",
         "A young, twitchy brigand, half-eager and half-nervous, trying to prove himself to the older men and overdoing it.",
         "A cold, watchful killer who says little but misses nothing — and moves fast to crush any move the captive makes."
      };

      private static readonly string[] GenericBanditTroopIds = {
         "looter", "forest_bandit", "mountain_bandit", "sea_raider", "steppe_bandit", "desert_bandit"
      };

      // In-memory state (reset each session — acceptable for v1)
      private static int _captiveSinceDay = -1;
      private static int _lastEncounterDay = -1;

      /// <summary>
      ///   Called from the daily tick. Checks all captive encounter conditions
      ///   and triggers the encounter popup when appropriate.
      /// </summary>
      public static void CheckAndTrigger(BannerlordNpcMemoryStore store, LlmServices llmServices)
      {
         if (store == null || llmServices == null) return;
         if (!llmServices.IsAvailable) return;
         if (ModSettings.AdultContentLevel != AdultContentLevel.Hardcore) return;

         var player = Hero.MainHero;

         if (player is not {IsPrisoner: true}) return;

         var today = (int) CampaignTime.Now.ToDays;

         // Track first day of captivity
         if (_captiveSinceDay < 0) _captiveSinceDay = today;

         int daysInCaptivity = today - _captiveSinceDay;

         if (daysInCaptivity < MinDaysBeforeFirstTrigger) return;
         if (_lastEncounterDay >= 0 && today - _lastEncounterDay < CooldownDays) return;

         MobileParty captorParty = player.PartyBelongedToAsPrisoner?.MobileParty;

         if (captorParty == null) return;

         // The ambush convoy owns its own scene (the paymaster lord's audience on arrival). Never let the random
         // roll fire a CNC scene with the throwaway bandit chief that leads the band.
         if (Stance.AmbushCaptivityBehavior.Instance?.IsAmbushCaptor(captorParty) == true) return;

         bool playerIsFemale = player.IsFemale;

         Hero captor = captorParty.LeaderHero;
         if (captor is {IsHumanPlayerCharacter: false})
         {
            // ── Lord captor: a profiled, named hero (existing CNC path) ──
            NpcProfile captorProfile = store.Get(captor.StringId);

            // No profile yet (player never spoke to this lord) — build one now from live
            // hero state so the scene can fire. A cruel lord abuses prisoners regardless
            // of whether the player has previously sought them out for conversation.
            if (captorProfile == null)
            {
               captorProfile = HeroProfileMapper.FromHero(captor);
               store.Set(captorProfile);
               ModLog.Info($"[CAPTIVE] lord encounter: created profile on-demand for {captor.Name}.");
            }

            // Romantic profile missing (profile pre-dates adult content, or was created
            // before the player reached an adult-capable content level) — build it lazily.
            if (captorProfile.Romantic == null)
            {
               captorProfile.Romantic = RomanticProfileBuilder.Build(captor, captorProfile.Trait ?? "Noble");
               store.Set(captorProfile);
               ModLog.Info($"[CAPTIVE] lord encounter: built Romantic profile on-demand for {captor.Name}.");
            }

            if (!captorProfile.Romantic.IsCompatibleWith(playerIsFemale)) return;

            if (MBRandom.RandomFloat > CalculateProbability(daysInCaptivity, captorProfile)) return;

            _lastEncounterDay = today;
            (List<WitnessEntry> witnesses, bool isCollective) = SelectWitnesses(captorParty, captor, store);
            CaptiveSceneIntent intent = SelectIntent(captorProfile, captorProfile.ReputationWithPlayer);
            ModLog.Info($"[CAPTIVE] lord encounter: captor={captor.Name}, intent={intent}, collective={isCollective}, witnesses={witnesses.Count}");
            ShowEncounterPopup(captor, captorProfile, witnesses, isCollective, intent);

            return;
         }

         // ── Bandit / pirate captor: no named hero — synthesize a brigand chief ──
         if (!captorParty.IsBandit) return;

         // Resolve the troop that will stand in as the chief's FACE first, then derive the
         // captor's sex from it — so the synthesized chief matches the actual gang shown on
         // screen (mostly men; a woman only when the gang's strongest troop is female, e.g. a
         // commander troop or a modded female bandit) instead of a disconnected random roll.
         CharacterObject banditFace = ResolveBanditTroop(captorParty);
         NpcProfile banditProfile = BuildBanditCaptor(captorParty, banditFace?.IsFemale);

         if (MBRandom.RandomFloat > CalculateProbability(daysInCaptivity, banditProfile)) return;

         _lastEncounterDay = today;
         CaptiveSceneIntent banditIntent = SelectBanditIntent(playerIsFemale, banditProfile);

         // Group aggression: the chief's men join in. Far more likely for a spoil-of-war
         // (PersonalDesire) scene — a captive woman handed to the band — than for a shakedown.
         // But never conjure more brigands than actually survived the capture: the chief is one
         // of the fighters, so extra participants are capped at (members − 1), max 2.
         int collectiveChance = banditIntent == CaptiveSceneIntent.PersonalDesire
            ? 55
            : 25;
         int extraBandits = Math.Max(0, CountBanditFighters(captorParty) - 1);
         bool banditCollective = extraBandits >= 1 && MBRandom.RandomInt(100) < collectiveChance;
         List<WitnessEntry> banditWitnesses = banditCollective
            ? BuildBanditWitnesses().Take(Math.Min(2, extraBandits)).ToList()
            : new List<WitnessEntry>();

         ModLog.Info($"[CAPTIVE] bandit encounter: captor={banditProfile.Name} (female={banditProfile.IsFemale}), intent={banditIntent}, collective={banditCollective}, witnesses={banditWitnesses.Count}, face={banditFace?.StringId ?? "none"}.");
         ShowEncounterPopup(null, banditProfile, banditWitnesses, banditCollective, banditIntent, banditFace);
      }

      /// <summary>Called once at session launch to reset tracking state.</summary>
      public static void ResetOnSessionLaunch()
      {
         _captiveSinceDay = -1;
         _lastEncounterDay = -1;
      }

      /// <summary>
      ///   Builds a synthetic brigand captor from an explicit id/seed/faction. Public so the
      ///   cr.bandit_scene console command can conjure one without a real capture.
      ///   <paramref name="isFemaleOverride" /> pins the sex (to match the real gang leader);
      ///   when null, a weighted random roll decides (mostly men).
      /// </summary>
      internal static NpcProfile BuildBanditCaptor(string id, int seed, string factionName, bool? isFemaleOverride = null)
      {
         var rng = new Random(seed);
         bool isFemale = isFemaleOverride ?? rng.Next(100) < 12; // match the real gang when known

         string[] firsts = isFemale
            ? BanditNamesF
            : BanditNamesM;
         var name = $"{firsts[rng.Next(firsts.Length)]} {BanditEpithets[rng.Next(BanditEpithets.Length)]}";

         // A distinctive manner so two synthesized chiefs never sound or behave the same.
         string manner = BanditManners[rng.Next(BanditManners.Length)];

         return new NpcProfile {
            Id = id,
            Name = name,
            IsFemale = isFemale,
            Faction = string.IsNullOrWhiteSpace(factionName)
               ? "Brigands"
               : factionName,
            Clan = "no honest house",
            Personality = "A hardened brigand chief of the wilds — lawless, contemptuous of lords and their " + "codes, ruled by greed and the cold pleasure of holding another's life in their hands. " + "Honor is a fool's currency to them. " + manner,
            Trait = "Ruthless toward those in their power. " + manner,
            Romantic = new RomanticProfile {
               IsFemale = isFemale,
               Orientation = rng.Next(100) < 75
                  ? SexualOrientation.Heterosexual
                  : SexualOrientation.Bisexual,
               ArchetypeName = "Ruthless brigand",
               Kinks = new List<Kink> {Kink.PowerImbalance, Kink.Dominance, Kink.FreeUse}
            }
         };
      }

      /// <summary>
      ///   The chief's men for a collective bandit scene — each with a DISTINCT agenda so they drive
      ///   the scene (argue, demand their turn, grab, grumble) rather than only leering. They have no
      ///   real hero behind them; their portraits are bandit troops assigned in the chat view.
      /// </summary>
      internal static List<WitnessEntry> BuildBanditWitnesses()
      {
         // Two distinct agendas, drawn at random, so no two collective scenes have the same men.
         int a = MBRandom.RandomInt(BanditWitnessAgendas.Length);
         int b = (a + 1 + MBRandom.RandomInt(BanditWitnessAgendas.Length - 1)) % BanditWitnessAgendas.Length;

         return new List<WitnessEntry> {
            new() {
               Name = "One of the brigands",
               HeroStringId = null,
               RelationToNpc = "one of your band, crowding in with his own appetite",
               IsPlayerCompanion = false,
               Persona = BanditWitnessAgendas[a]
            },
            new() {
               Name = "Another brigand",
               HeroStringId = null,
               RelationToNpc = "one of your band, in the thick of it with his own designs",
               IsPlayerCompanion = false,
               Persona = BanditWitnessAgendas[b]
            }
         };
      }

      /// <summary>
      ///   Resolves a real bandit-troop <see cref="CharacterObject" /> to stand in as the face of
      ///   a synthesized brigand captor (who has no Hero). Picks the strongest troop in the capturing
      ///   party — a sea raider, forest brigand, etc., matching their faction — and falls back to a
      ///   generic looter when no party is available (the console test path).
      /// </summary>
      internal static CharacterObject ResolveBanditTroop(MobileParty party)
      {
         CharacterObject best = null;
         TroopRoster roster = party?.MemberRoster;

         if (roster == null) return MBObjectManager.Instance?.GetObject<CharacterObject>("looter");
         for (var i = 0; i < roster.Count; i++)
         {
            CharacterObject c = roster.GetCharacterAtIndex(i);

            if (c == null || c.IsHero) continue;
            if (best == null || c.Level > best.Level) best = c;
         }

         return best ?? MBObjectManager.Instance?.GetObject<CharacterObject>("looter");
      }

      /// <summary>
      ///   Resolves up to <paramref name="count" /> DISTINCT bandit-troop faces for the chief's men,
      ///   so the witness portraits in a collective scene are not all identical. Prefers real troops
      ///   from the capturing party's roster (the actual gang), then fills from a generic bandit pool.
      /// </summary>
      internal static List<CharacterObject> ResolveBanditWitnessFaces(MobileParty party, int count)
      {
         var faces = new List<CharacterObject>();
         var seen = new HashSet<string>();


         void TryAdd(CharacterObject c)
         {
            if (c == null || faces.Count >= count || !seen.Add(c.StringId)) return;
            faces.Add(c);
         }


         // From the real gang: every distinct non-hero troop, weakest-first so the chief's face
         // (the strongest, used elsewhere) is not duplicated when avoidable.
         TroopRoster roster = party?.MemberRoster;
         if (roster != null)
            for (var i = 0; i < roster.Count && faces.Count < count; i++)
               TryAdd(roster.GetCharacterAtIndex(i));

         // Fill the rest from a generic bandit pool.
         foreach (string id in GenericBanditTroopIds)
         {
            if (faces.Count >= count) break;
            TryAdd(MBObjectManager.Instance?.GetObject<CharacterObject>(id));
         }

         // Last resort: pad with looters so every witness still gets a portrait.
         var looter = MBObjectManager.Instance?.GetObject<CharacterObject>("looter");
         while (faces.Count < count && looter != null) faces.Add(looter);

         return faces;
      }

      /// <summary>
      ///   Resolves a FEMALE <see cref="CharacterObject" /> to use as a female chief's portrait —
      ///   reusing a real in-game face rather than generating one. Prefers a bandit/commoner-flavored
      ///   female troop; falls back to any adult female non-hero character; null if none exists.
      /// </summary>
      internal static CharacterObject ResolveFemaleBanditFace()
      {
         MBReadOnlyList<CharacterObject> all = MBObjectManager.Instance?.GetObjectTypeList<CharacterObject>();

         if (all == null) return null;

         CharacterObject fallback = null;
         foreach (CharacterObject c in all)
         {
            if (c == null || c.IsHero || !c.IsFemale) continue;
            fallback ??= c;

            string id = c.StringId?.ToLowerInvariant() ?? "";

            if (id.Contains("bandit") || id.Contains("brigand") || id.Contains("raider") || id.Contains("outlaw") || id.Contains("looter") || id.Contains("villager") || id.Contains("townswoman") || id.Contains("commoner"))
               return c;
         }

         return fallback;
      }

      #region private

      /// <summary>
      ///   Builds a deterministic synthetic captor profile for a faceless bandit/pirate party.
      ///   When <paramref name="leaderIsFemale" /> is supplied (from the gang's face troop) the
      ///   chief's sex matches the real party; otherwise it falls back to a weighted random roll.
      /// </summary>
      private static NpcProfile BuildBanditCaptor(MobileParty party, bool? leaderIsFemale = null)
         => BuildBanditCaptor($"bandit_captor_{party.StringId}", party.StringId.GetHashCode(),
            party.MapFaction?.Name?.ToString() ?? "Brigands", leaderIsFemale);

      private static List<WitnessEntry> BuildSoldierWitnesses()
         => new() {
            new WitnessEntry {
               Name = "A soldier",
               HeroStringId = null,
               RelationToNpc = "one of your men, present on your orders",
               IsPlayerCompanion = false,
               Persona = "Imposing — silent, obedient, watching"
            },
            new WitnessEntry {
               Name = "Another soldier",
               HeroStringId = null,
               RelationToNpc = "one of your men, present on your orders",
               IsPlayerCompanion = false,
               Persona = "Impassive — expressionless, unmoved"
            }
         };

      // ── Private ───────────────────────────────────────────────────────────

      private static float CalculateProbability(int daysInCaptivity, NpcProfile captorProfile)
         => CaptiveEncounterPolicy.EncounterProbability(daysInCaptivity, captorProfile.Romantic?.Kinks);

      /// <summary>
      ///   Picks the bandit captor's intent. Spoil-of-war (the sexual CNC intent) is GENDER-WEIGHTED:
      ///   near-nil for a male prisoner, common for a female — and only when orientation-compatible.
      ///   Otherwise a non-sexual menace intent (extortion / intimidation / revenge).
      /// </summary>
      /// <summary>
      ///   How many fighters the capturing band actually has — the ceiling on how many can
      ///   appear in a collective scene. Prevents a 3-bandit gang-scene when only two raiders
      ///   survived the fight (a vanilla-consistency break the tester caught).
      /// </summary>
      private static int CountBanditFighters(MobileParty party)
      {
         try { return party?.MemberRoster?.TotalManCount ?? 0; }
         catch { return 0; }
      }

      private static void OpenEncounterChat(
         Hero captor,
         NpcProfile captorProfile,
         List<WitnessEntry> witnesses,
         bool isCollective,
         CaptiveSceneIntent intent,
         CharacterObject portraitCharacter = null)
      {
         ChatManager.OpenForCaptiveEncounter(captor, captorProfile, witnesses, isCollective, intent, portraitCharacter);
      }

      private static List<WitnessEntry> ResolveLordWitnesses(
         MobileParty captorParty,
         Hero captor,
         BannerlordNpcMemoryStore store)
      {
         var witnesses = new List<WitnessEntry>();

         foreach (Hero hero in Hero.AllAliveHeroes
                                   .Where(h => h.PartyBelongedTo == captorParty && h != captor && !h.IsHumanPlayerCharacter && h.IsAlive)
                                   .Take(2))
         {
            NpcProfile p = store.Get(hero.StringId);

            if (p == null) continue;

            witnesses.Add(new WitnessEntry {
               Name = hero.Name?.ToString() ?? "Unknown",
               HeroStringId = hero.StringId,
               RelationToNpc = "a companion of your captor",
               IsPlayerCompanion = false,
               Persona = p.Trait ?? "Aloof"
            });
         }

         return witnesses;
      }

      private static CaptiveSceneIntent SelectBanditIntent(bool playerIsFemale, NpcProfile captor)
      {
         int spoilChance = playerIsFemale
            ? 50
            : 3;

         if (MBRandom.RandomInt(100) < spoilChance && captor.Romantic != null && captor.Romantic.IsCompatibleWith(playerIsFemale))
            return CaptiveSceneIntent.PersonalDesire;

         int roll = MBRandom.RandomInt(100);

         if (roll < 45) return CaptiveSceneIntent.Extortion;
         if (roll < 75) return CaptiveSceneIntent.Intimidation;

         return CaptiveSceneIntent.Revenge;
      }

      private static CaptiveSceneIntent SelectIntent(NpcProfile captorProfile, int relation)
         => CaptiveEncounterPolicy.SelectIntent(captorProfile.Romantic?.Kinks, relation);

      private static (List<WitnessEntry> witnesses, bool isCollective) SelectWitnesses(
         MobileParty captorParty,
         Hero captor,
         BannerlordNpcMemoryStore store)
      {
         int roll = MBRandom.RandomInt(4);
         switch (roll)
         {
            case 0: // Private — captor alone
               return (new List<WitnessEntry>(), false);

            case 1: // Other lords present
               return (ResolveLordWitnesses(captorParty, captor, store), false);

            case 2: // Generic soldiers
               return (BuildSoldierWitnesses(), false);

            case 3: // Collective — lords if available, else soldiers
            {
               List<WitnessEntry> lords = ResolveLordWitnesses(captorParty, captor, store);

               return (lords.Count > 0
                  ? lords
                  : BuildSoldierWitnesses(), true);
            }

            default:
               return (new List<WitnessEntry>(), false);
         }
      }

      private static void ShowEncounterPopup(
         Hero captor,
         NpcProfile captorProfile,
         List<WitnessEntry> witnesses,
         bool isCollective,
         CaptiveSceneIntent intent,
         CharacterObject portraitCharacter = null)
      {
         string captorName = captor?.Name?.ToString() ?? captorProfile?.Name ?? "your captor";
         string flavor = isCollective
            ? "You are brought before your captors. You have no choice but to comply."
            : $"You are brought before {captorName}. Alone. You have no choice but to comply.";

         InformationManager.ShowInquiry(new InquiryData(
            $"{captorName} summons you",
            flavor,
            true,
            false,
            "Enter",
            null,
            () => OpenEncounterChat(captor, captorProfile, witnesses, isCollective, intent, portraitCharacter),
            null
         ), true);
      }

      #endregion
   }
}