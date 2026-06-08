// Code written by Gabriel Mailhot, 06/06/2026. Sprint 17.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Sprint 17 — Captive Encounter System.
    ///   Each game day, while the player is a prisoner in Hardcore mode, rolls a
    ///   probability check to see whether their captor initiates an encounter.
    ///   When triggered, a popup warns the player, then opens the chat window with
    ///   the captor as main NPC and optional witnesses (lords, soldiers, or collective).
    ///
    ///   Probability: 5 % × days in captivity (cap 40 %) + kink bonuses.
    ///   Delay: minimum 2 days before first trigger.
    ///   Cooldown: 3 days between encounters.
    ///   Level requirement: Hardcore only.
    /// </summary>
    public static class CaptiveEncounterService
    {
        // Probability constants
        private const float BaseProbabilityPerDay = 0.05f;
        private const float MaxBaseProbability    = 0.40f;
        private const float KinkBonusPowerImbalance = 0.10f;
        private const float KinkBonusDominance      = 0.05f;
        private const float KinkBonusSadism         = 0.05f;
        private const float KinkBonusFreeUse        = 0.10f;
        private const float KinkBonusMasterSlave    = 0.05f;
        private const float MaxTotalProbability      = 0.60f;

        // Timing constants
        private const int MinDaysBeforeFirstTrigger = 2;
        private const int CooldownDays              = 3;

        // In-memory state (reset each session — acceptable for v1)
        private static int _captiveSinceDay   = -1;
        private static int _lastEncounterDay  = -1;

        /// <summary>Called once at session launch to reset tracking state.</summary>
        public static void ResetOnSessionLaunch()
        {
            _captiveSinceDay  = -1;
            _lastEncounterDay = -1;
        }

        /// <summary>
        ///   Called from the daily tick. Checks all captive encounter conditions
        ///   and triggers the encounter popup when appropriate.
        /// </summary>
        public static void CheckAndTrigger(BannerlordNpcMemoryStore store, Services.LlmServices llmServices)
        {
            if (store == null || llmServices == null) return;
            if (!llmServices.IsAvailable) return;
            if (ModSettings.AdultContentLevel != AdultContentLevel.Hardcore) return;

            Hero player = Hero.MainHero;
            if (player == null || !player.IsPrisoner) return;

            int today = (int)CampaignTime.Now.ToDays;

            // Track first day of captivity
            if (_captiveSinceDay < 0) _captiveSinceDay = today;

            int daysInCaptivity = today - _captiveSinceDay;
            if (daysInCaptivity < MinDaysBeforeFirstTrigger) return;
            if (_lastEncounterDay >= 0 && today - _lastEncounterDay < CooldownDays) return;

            // Resolve the captor (party leader of the party holding the player)
            MobileParty captorParty = player.PartyBelongedToAsPrisoner?.MobileParty;
            Hero captor = captorParty?.LeaderHero;
            if (captor == null || captor.IsHumanPlayerCharacter) return;

            NpcProfile captorProfile = store.Get(captor.StringId);
            if (captorProfile == null) return; // no profile = no encounter

            // Check orientation compatibility
            bool playerIsFemale = player.IsFemale;
            if (captorProfile.Romantic == null) return;
            if (!captorProfile.Romantic.IsCompatibleWith(playerIsFemale)) return;

            // Calculate probability
            float probability = CalculateProbability(daysInCaptivity, captorProfile);
            if (MBRandom.RandomFloat > probability) return;

            // Trigger
            _lastEncounterDay = today;
            (List<WitnessEntry> witnesses, bool isCollective) = SelectWitnesses(captorParty, captor, store);
            CaptiveSceneIntent intent = SelectIntent(captorProfile, captorProfile.ReputationWithPlayer);

            ModLog.Info($"[CAPTIVE] encounter triggered: captor={captor.Name}, intent={intent}, collective={isCollective}, witnesses={witnesses.Count}");

            ShowEncounterPopup(captor, captorProfile, witnesses, isCollective, intent);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static float CalculateProbability(int daysInCaptivity, NpcProfile captorProfile)
        {
            float base_ = Math.Min(daysInCaptivity * BaseProbabilityPerDay, MaxBaseProbability);

            float bonus = 0f;
            if (captorProfile.Romantic?.Kinks != null)
            {
                IReadOnlyList<Kink> kinks = captorProfile.Romantic.Kinks;
                if (kinks.Contains(Kink.PowerImbalance)) bonus += KinkBonusPowerImbalance;
                if (kinks.Contains(Kink.Dominance))      bonus += KinkBonusDominance;
                if (kinks.Contains(Kink.Sadism))         bonus += KinkBonusSadism;
                if (kinks.Contains(Kink.FreeUse))        bonus += KinkBonusFreeUse;
                if (kinks.Contains(Kink.MasterSlave))    bonus += KinkBonusMasterSlave;
            }

            return Math.Min(base_ + bonus, MaxTotalProbability);
        }

        private static (List<WitnessEntry> witnesses, bool isCollective) SelectWitnesses(
            MobileParty captorParty, Hero captor, BannerlordNpcMemoryStore store)
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
                    var lords = ResolveLordWitnesses(captorParty, captor, store);
                    return (lords.Count > 0 ? lords : BuildSoldierWitnesses(), true);
                }

                default:
                    return (new List<WitnessEntry>(), false);
            }
        }

        private static List<WitnessEntry> ResolveLordWitnesses(
            MobileParty captorParty, Hero captor, BannerlordNpcMemoryStore store)
        {
            var witnesses = new List<WitnessEntry>();

            foreach (Hero hero in Hero.AllAliveHeroes
                .Where(h => h.PartyBelongedTo == captorParty
                    && h != captor
                    && !h.IsHumanPlayerCharacter
                    && h.IsAlive)
                .Take(2))
            {
                NpcProfile p = store.Get(hero.StringId);
                if (p == null) continue;

                witnesses.Add(new WitnessEntry {
                    Name             = hero.Name?.ToString() ?? "Unknown",
                    HeroStringId     = hero.StringId,
                    RelationToNpc    = "a companion of your captor",
                    IsPlayerCompanion = false,
                    Persona          = p.Trait ?? "Aloof"
                });
            }

            return witnesses;
        }

        private static List<WitnessEntry> BuildSoldierWitnesses()
            => new List<WitnessEntry> {
                new WitnessEntry {
                    Name              = "A soldier",
                    HeroStringId      = null,
                    RelationToNpc     = "one of your men, present on your orders",
                    IsPlayerCompanion = false,
                    Persona           = "Imposing — silent, obedient, watching"
                },
                new WitnessEntry {
                    Name              = "Another soldier",
                    HeroStringId      = null,
                    RelationToNpc     = "one of your men, present on your orders",
                    IsPlayerCompanion = false,
                    Persona           = "Impassive — expressionless, unmoved"
                }
            };

        private static CaptiveSceneIntent SelectIntent(NpcProfile captorProfile, int relation)
        {
            bool Has(Kink k) => captorProfile.Romantic?.Kinks?.Contains(k) ?? false;

            // Cruelty / pain kinks or strong contempt → torture
            if (Has(Kink.Sadism) || Has(Kink.ImpactPlay) || Has(Kink.FearPlay) || relation <= -15)
                return CaptiveSceneIntent.Torture;

            // Strong positive regard + praise kink → reward (the positive register)
            if (relation >= 20 && Has(Kink.Praise))
                return CaptiveSceneIntent.Reward;

            // Conditioning kink → an ongoing training session
            if (Has(Kink.Training))
                return CaptiveSceneIntent.Training;

            // Positive regard, or desire/use kinks with neutral-or-better relation → personal desire
            if (relation >= 10
                || ((Has(Kink.Dominance) || Has(Kink.FreeUse) || Has(Kink.Breeding)
                     || Has(Kink.OrgasmControl) || Has(Kink.PetPlay)) && relation >= 0))
                return CaptiveSceneIntent.PersonalDesire;

            // Power / ownership kinks → domination
            if (Has(Kink.PowerImbalance) || Has(Kink.Dominance) || Has(Kink.MasterSlave)
                || Has(Kink.Objectification) || Has(Kink.Degradation))
                return CaptiveSceneIntent.Domination;

            return CaptiveSceneIntent.Interrogation;
        }

        private static void ShowEncounterPopup(
            Hero captor, NpcProfile captorProfile,
            List<WitnessEntry> witnesses, bool isCollective, CaptiveSceneIntent intent)
        {
            string flavor = isCollective
                ? "You are brought before your captors. You have no choice but to comply."
                : $"You are brought before {captor.Name}. Alone. You have no choice but to comply.";

            InformationManager.ShowInquiry(new InquiryData(
                titleText:                $"{captor.Name} summons you",
                text:                     flavor,
                isAffirmativeOptionShown: true,
                isNegativeOptionShown:    false,
                affirmativeText:          "Enter",
                negativeText:             null,
                affirmativeAction:        () => OpenEncounterChat(captor, captorProfile, witnesses, isCollective, intent),
                negativeAction:           null
            ), pauseGameActiveState: true);
        }

        private static void OpenEncounterChat(
            Hero captor, NpcProfile captorProfile,
            List<WitnessEntry> witnesses, bool isCollective, CaptiveSceneIntent intent)
        {
            ChatManager.OpenForCaptiveEncounter(captor, captorProfile, witnesses, isCollective, intent);
        }
    }
}
