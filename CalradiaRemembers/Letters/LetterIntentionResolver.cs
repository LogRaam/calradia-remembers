// Code written by Gabriel Mailhot, 12/07/2026.
// A letter must never outlive the intent that wrote it: at delivery time each pending letter is re-validated
// against its sender's CURRENT intention, and dropped (never shown) if that intention has lapsed (a reconciled
// spouse no longer wants a divorce; a lord who made peace no longer threatens war). The pure keep/drop RULE
// lives in LetterDeliveryPolicy; this thin resolver only reads live game state into the one boolean it needs.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Decides whether a pending letter should be dropped as stale (its sender's motivating intention has
    ///   lapsed) rather than delivered. Combines the pure <see cref="LetterDeliveryPolicy.ShouldDropAsStale" />
    ///   rule with a live read of game state. A reason it does not yet assess is treated as "intention still
    ///   holds", so an unknown case is delivered rather than silently swallowed.
    /// </summary>
    public static class LetterIntentionResolver
    {
        /// <summary>True when <paramref name="letter" /> should be DROPPED (never delivered) because its sender's intention is gone.</summary>
        public static bool ShouldDrop(PendingLetter letter, Hero sender)
            => letter != null && LetterDeliveryPolicy.ShouldDropAsStale(letter.Reason, StillHolds(letter, sender));

        /// <summary>True when the intention that motivated the letter still holds (so it may still be delivered).</summary>
        public static bool StillHolds(PendingLetter letter, Hero sender)
        {
            if (letter == null) return true;
            if (!LetterDeliveryPolicy.IsIntentionBearing(letter.Reason)) return true;
            try
            {
                switch (letter.Reason)
                {
                    case LetterReason.SpouseDivorceDemand:
                        return SpouseDivorceDemandBehavior.Instance?.IsDemanding(sender?.StringId) ?? true;

                    case LetterReason.SpouseDivorceEscalation:
                        return SpouseDivorceDemandBehavior.Instance?.IsEstranging(sender?.StringId) ?? true;

                    case LetterReason.ReinforcementRequest:
                        return StillAtWarWithPlayer(sender);

                    case LetterReason.StanceHostility:
                    {
                        StanceConsequence? v = CurrentVerdict(sender);
                        return v == null || IsHostile(v.Value); // cannot assess -> keep
                    }

                    case LetterReason.StanceFavor:
                    {
                        StanceConsequence? v = CurrentVerdict(sender);
                        return v == null || IsFriendly(v.Value);
                    }

                    default:
                        // MarriageProposal / JealousThreat / CorruptionAttempt / Blackmail: assessed in a later increment.
                        return true;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LETTER] intention resolve failed ({letter.Reason}): {ex.Message}");
                return true; // fail-safe: deliver rather than wrongly swallow
            }
        }

        private static bool StillAtWarWithPlayer(Hero sender)
        {
            IFaction s = sender?.MapFaction;
            IFaction p = Hero.MainHero?.MapFaction;
            return s == null || p == null || s.IsAtWarWith(p);
        }

        private static StanceConsequence? CurrentVerdict(Hero sender)
        {
            if (sender == null) return null;
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(sender.StringId);
            if (profile == null) return null;

            return StanceConsequencePolicy.Resolve(
                StanceService.ToStance(profile),
                sender.GetTraitLevel(DefaultTraits.Honor),
                sender.GetTraitLevel(DefaultTraits.Valor),
                sender.GetTraitLevel(DefaultTraits.Mercy),
                sender.GetTraitLevel(DefaultTraits.Calculating));
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
