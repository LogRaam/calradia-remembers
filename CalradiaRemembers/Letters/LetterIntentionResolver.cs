// Code written by Gabriel Mailhot, 12/07/2026.
// A letter must never outlive the intent that wrote it. This reads the live campaign through the
// ILetterIntentionWorld seam and hands the per-reason keep/drop rule to the pure LetterIntentionPolicy, so
// the rules stay unit-tested against a stub while the actual game reads live in the private CampaignWorld.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Letters;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Decides whether a pending letter should be dropped as stale. Delegates the per-reason decision to the
    ///   pure <see cref="LetterIntentionPolicy" /> (fed by the <see cref="CampaignWorld" /> reads), then gates
    ///   it on <see cref="LetterDeliveryPolicy.ShouldDropAsStale" />. Fail-safe: any fault keeps the letter.
    /// </summary>
    public static class LetterIntentionResolver
    {
        private static readonly ILetterIntentionWorld World = new CampaignWorld();

        /// <summary>
        ///   Whether the war-duty reckoning with this sender is still open. Supplied by the submodule, so Letters does
        ///   not reach into WarDuty (which sends its letters through Letters). Null keeps the letter.
        /// </summary>
        internal static Func<string, bool> WarDutyOpenFor;

        /// <summary>True when <paramref name="letter" /> should be DROPPED (never delivered) because its sender's intention is gone.</summary>
        public static bool ShouldDrop(PendingLetter letter, Hero sender)
        {
            if (letter == null) return false;
            try
            {
                bool holds = LetterIntentionPolicy.StillHolds(letter.Reason, sender?.StringId, World);

                return LetterDeliveryPolicy.ShouldDropAsStale(letter.Reason, holds);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LETTER] intention resolve failed ({letter.Reason}): {ex.Message}");

                return false; // fail-safe: deliver rather than wrongly swallow
            }
        }

        /// <summary>
        ///   The real campaign reads behind <see cref="ILetterIntentionWorld" />. Every read is fail-safe:
        ///   when a source cannot be read it returns "intention still holds", so a letter is never dropped on
        ///   a missing signal.
        /// </summary>
        private sealed class CampaignWorld : ILetterIntentionWorld
        {
            public bool PlayerIsMarried => Hero.MainHero?.Spouse != null;

            public bool PlayerHasSecretLover
            {
                get
                {
                    var store = NpcMemoryCampaignBehavior.Instance?.Store;
                    if (store == null) return true; // cannot assess -> keep
                    foreach (string id in store.ListIds())
                        if (store.Get(id)?.Romantic?.IsSecretLover == true)
                            return true;

                    return false;
                }
            }

            public bool SpouseStillDemandsDivorce(string senderId)
                => SpouseDivorceDemandBehavior.Instance?.IsDemanding(senderId) ?? true;

            public bool SpouseStillEstranging(string senderId)
                => SpouseDivorceDemandBehavior.Instance?.IsEstranging(senderId) ?? true;

            public bool PleaStillStands(string senderId) => LetterPleas.Desk?.StillStands(senderId) == true;

            public bool RestraintStillStands(string senderId) => Stance.RaidRestraintBehavior.Instance?.StillSpares(senderId) == true;

            public bool SenderStanceStillHostile(string senderId)
            {
                StanceConsequence? v = Verdict(HeroById(senderId));

                return v == null || IsHostile(v.Value); // cannot assess -> keep
            }

            public bool SenderStanceStillFriendly(string senderId)
            {
                StanceConsequence? v = Verdict(HeroById(senderId));

                return v == null || IsFriendly(v.Value);
            }

            public bool SenderHasLiveJealousyGrudge(string senderId)
            {
                Hero sender = HeroById(senderId);
                if (sender == null || Hero.MainHero == null) return true;
                IReadOnlyList<Grudge> grudges =
                    GrudgeBehavior.Instance?.LiveGrudgesFor(sender, (int) CampaignTime.Now.ToDays);
                if (grudges == null) return true;
                foreach (Grudge g in grudges)
                    if (g.Source == GrudgeSource.Jealousy && g.TargetId == Hero.MainHero.StringId)
                        return true;

                return false;
            }

            public bool SenderStillAdverseToPlayer(string senderId)
            {
                Hero sender = HeroById(senderId);
                if (sender == null) return true;
                if (StillAtWar(sender)) return true;
                int regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(senderId)?.ReputationWithPlayer ?? 0;

                return regard < Regard.NeutralTier;
            }

            public bool WarDutyStillOpen(string senderId) => WarDutyOpenFor?.Invoke(senderId) ?? true;

            public bool ConfessionStillHeartfelt(string senderId)
            {
                NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(senderId);
                Hero sender = HeroById(senderId);
                if (profile == null || sender == null) return true; // cannot assess -> keep

                return LoveConfessionReader.StillHeartfelt(sender, profile);
            }

            private static Hero HeroById(string id)
                => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h != null && h.StringId == id);

            private static bool StillAtWar(Hero sender)
            {
                IFaction s = sender?.MapFaction;
                IFaction p = Hero.MainHero?.MapFaction;

                return s == null || p == null || s.IsAtWarWith(p);
            }

            private static StanceConsequence? Verdict(Hero sender)
            {
                if (sender == null) return null;
                NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(sender.StringId);
                if (profile == null) return null;

                return StanceConsequencePolicy.Resolve(StanceService.ToStance(profile), new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = sender.GetTraitLevel(DefaultTraits.Honor), Valor = sender.GetTraitLevel(DefaultTraits.Valor), Mercy = sender.GetTraitLevel(DefaultTraits.Mercy), Calculating = sender.GetTraitLevel(DefaultTraits.Calculating)});
            }

            private static bool IsHostile(StanceConsequence v)
                => v == StanceConsequence.ColdShoulder || v == StanceConsequence.Threat
                   || v == StanceConsequence.Plot || v == StanceConsequence.Intercept
                   || v == StanceConsequence.Assassinate;

            private static bool IsFriendly(StanceConsequence v)
                => v == StanceConsequence.WarmWelcome || v == StanceConsequence.Favor
                   || v == StanceConsequence.Warning;
        }
    }
}
