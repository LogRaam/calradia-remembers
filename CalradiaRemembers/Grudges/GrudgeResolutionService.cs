// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: the ONE engine-side seam both the make_amends verb and the gift
// routing (take_gold / give_item) call into. Picks the holder's dominant resolvable live grudge against
// the player, evaluates the offered gesture through the pure GrudgeResolutionPolicy, then applies the
// verdict (Resolve / soften the stored Magnitude / do nothing) and persists it by direct mutation — the
// same object reference GrudgeBehavior already holds in its ledger, exactly like GrudgePolicy.Resolve and
// GrudgeBehavior.TryPlant already do. Never throws.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Grudges
{
    /// <summary>
    ///   The outcome of one <see cref="GrudgeResolutionService.TryResolveDominant" /> call: whether a
    ///   resolvable live grudge against the player existed at all, the verdict when it did, and the
    ///   affected <see cref="Grudge" /> itself (already mutated in place when the verdict changed
    ///   anything), so a caller can phrase its own player-facing message or inspect the before/after
    ///   magnitude for a diagnostic.
    /// </summary>
    public sealed class GrudgeResolutionAttemptResult
    {
        /// <summary>True when the holder nursed at least one resolvable live grudge against the player to evaluate the gesture against.</summary>
        public bool HadResolvableGrudge { get; set; }

        /// <summary>The verdict; meaningless when <see cref="HadResolvableGrudge" /> is false (left at its default, Resolved).</summary>
        public GrudgeResolutionOutcome Outcome { get; set; }

        /// <summary>The grudge the gesture was evaluated against, already mutated when the verdict changed anything. Null when <see cref="HadResolvableGrudge" /> is false.</summary>
        public Grudge Grudge { get; set; }

        /// <summary>How many magnitude points were actually subtracted (0 for Rejected, the full effective magnitude for Resolved).</summary>
        public int MagnitudeReduction { get; set; }

        /// <summary>True when the gesture actually moved the grudge (Resolved or Softened) — the caller's cue to mention it at all.</summary>
        public bool Changed => HadResolvableGrudge && Outcome != GrudgeResolutionOutcome.Rejected;
    }

    /// <summary>
    ///   Shared glue between the make_amends verb and the gift-routing hooks (ExecuteTakeGold,
    ///   ExecuteGiveItem): evaluate the player's offered gesture against the holder's dominant resolvable
    ///   live grudge, apply the verdict, and persist it. A single seam so both callers can never drift on
    ///   the evaluate-apply-persist sequence.
    /// </summary>
    public static class GrudgeResolutionService
    {
        /// <summary>Tuning: the small goodwill nudge applied holder-to-player when a grudge is fully Resolved (a wound genuinely mended deserves a touch of warmth back, not just the grudge's own silence).</summary>
        public const int ResolvedRelationBump = 4;

        /// <summary>
        ///   Picks <paramref name="holder" />'s dominant resolvable live grudge against the player (the
        ///   first of <see cref="GrudgeBehavior.LiveGrudgesFor" />, already ranked by today's effective
        ///   magnitude, filtered to <see cref="GrudgePolicy.IsResolvable" /> and targeting
        ///   <see cref="Hero.MainHero" />), evaluates the offered <paramref name="gesture" /> against it via
        ///   <see cref="GrudgeResolutionPolicy.Evaluate" />, then applies the verdict: Resolved marks the
        ///   grudge resolved (<see cref="GrudgePolicy.Resolve" />) and nudges relation up by
        ///   <see cref="ResolvedRelationBump" />; Softened subtracts the reduction from the grudge's stored
        ///   <see cref="Grudge.Magnitude" />; Rejected mutates nothing. Returns a result carrying whether
        ///   anything was found/changed, for the caller to phrase its own message. Never throws — a
        ///   "nothing to resolve" result (<see cref="GrudgeResolutionAttemptResult.HadResolvableGrudge" />
        ///   false) is returned on any failure or absence.
        /// </summary>
        public static GrudgeResolutionAttemptResult TryResolveDominant(Hero holder, GrudgeGesture gesture, int giftValue, int today)
        {
            try
            {
                Hero player = Hero.MainHero;
                if (holder == null || player == null || GrudgeBehavior.Instance == null)
                    return new GrudgeResolutionAttemptResult();

                IReadOnlyList<Grudge> live = GrudgeBehavior.Instance.LiveGrudgesFor(holder, today);
                Grudge grudge = live?.FirstOrDefault(g => GrudgePolicy.IsResolvable(g) && g.TargetId == player.StringId);

                return TryResolve(holder, grudge, gesture, giftValue, today);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] TryResolveDominant failed: {ex.GetType().Name}: {ex.Message}");

                return new GrudgeResolutionAttemptResult();
            }
        }

        /// <summary>
        ///   C5 (regard audit): resolves ONE SPECIFIC grudge with the given gesture, instead of re-picking "the
        ///   dominant live grudge". The targeted verbs (make_amends, grudge_terms) pass the exact grudge they
        ///   taught the player, their dominant KNOWN grudge, so a sincere apology can never silently mend a heavier
        ///   HIDDEN grudge while the prompt and the "grievance mended" banner both speak of the known one. A null
        ///   grudge yields a no-op result; never throws (called from the conversation action path).
        /// </summary>
        public static GrudgeResolutionAttemptResult TryResolve(Hero holder, Grudge grudge, GrudgeGesture gesture, int giftValue, int today)
        {
            try
            {
                if (holder == null || grudge == null || GrudgeBehavior.Instance == null)
                    return new GrudgeResolutionAttemptResult();

                TraitFacts traits = GrudgeBridge.BuildTraitFacts(holder);
                int effectiveMagnitude = GrudgePolicy.EffectiveMagnitude(grudge, today, traits);

                var attempt = new GrudgeResolutionAttempt {
                    Rigidity = grudge.Rigidity,
                    ResolutionKind = GrudgePolicy.ResolutionKindFor(grudge.Source, grudge.Rigidity),
                    EffectiveMagnitude = effectiveMagnitude,
                    Gesture = gesture,
                    GiftValue = giftValue < 0 ? 0 : giftValue,
                    Traits = traits
                };

                GrudgeResolutionResult verdict = GrudgeResolutionPolicy.Evaluate(attempt);

                switch (verdict.Outcome)
                {
                    case GrudgeResolutionOutcome.Resolved:
                        GrudgePolicy.Resolve(grudge, today);
                        ApplyResolvedRelationBump(holder);
                        RecordChronicleResolution(holder, today);
                        ModLog.Info($"[GRUDGE] resolved: {holder.Name}'s {grudge.Source} grudge, gesture={gesture}, personal regard +{ResolvedRelationBump}.");

                        break;
                    case GrudgeResolutionOutcome.Softened:
                        grudge.Magnitude = Math.Max(0, grudge.Magnitude - verdict.MagnitudeReduction);
                        ModLog.Info($"[GRUDGE] softened: {holder.Name}'s {grudge.Source} grudge, gesture={gesture}, -{verdict.MagnitudeReduction} magnitude (now {grudge.Magnitude}).");

                        break;
                    default:
                        ModLog.Info($"[GRUDGE] rejected: {holder.Name}'s {grudge.Source} grudge, gesture={gesture} did not move it.");

                        break;
                }

                // M-G1 (regard audit): a gesture that actually MOVED a hidden grudge has revealed it. The player
                // watched the NPC soften over "an old grievance", so it is no longer secret: flip it to known. This
                // turns the old information LEAK into a genuine revelation, and, crucially, unblocks the
                // hidden -> known -> mended loop: the targeted verbs (make_amends/grudge_terms) can now finish what
                // an untargeted gift began, instead of a hidden grudge staying forever beyond every resolution path.
                if (!grudge.KnownToPlayer
                    && verdict.Outcome is GrudgeResolutionOutcome.Softened or GrudgeResolutionOutcome.Resolved)
                {
                    grudge.KnownToPlayer = true;
                    ModLog.Info($"[GRUDGE] {holder.Name}'s {grudge.Source} grudge is now known to the player (revealed by softening it).");
                }

                return new GrudgeResolutionAttemptResult {
                    HadResolvableGrudge = true,
                    Outcome = verdict.Outcome,
                    Grudge = grudge,
                    MagnitudeReduction = verdict.MagnitudeReduction
                };
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] TryResolve failed: {ex.GetType().Name}: {ex.Message}");

                return new GrudgeResolutionAttemptResult();
            }
        }

        /// <summary>
        ///   Grudges pillar (Deed gesture): routes a verified real-world favour done for
        ///   <paramref name="beneficiary" /> (freeing them, freeing their kin, completing a quest for
        ///   them) through <see cref="TryResolveDominant" />, returning a short clause to append to the
        ///   caller's own outcome message when it actually moved a grudge, or an empty string otherwise.
        ///   The single shared seam for the three verified-deed sites (free_prisoner, release_prisoner,
        ///   quest completion), mirroring the bridge's own gift-tail helper so none of them can drift on
        ///   the evaluate-apply-persist sequence. Independent of any Appreciation plant at the same site
        ///   (separate ledger, same holder) — never throws, a resolution failure must never break the
        ///   deed that already succeeded.
        /// </summary>
        public static string TryGrudgeDeedTail(Hero beneficiary, int today)
        {
            try
            {
                GrudgeResolutionAttemptResult result = TryResolveDominant(beneficiary, GrudgeGesture.Deed, 0, today);

                if (!result.Changed) return "";

                return result.Outcome == GrudgeResolutionOutcome.Resolved
                    ? " It also mends an old grievance between you."
                    : " It also softens an old grievance between you.";
            }
            catch (Exception ex)
            {
                ModLog.Error($"[GRUDGE] deed routing failed: {ex.GetType().Name}: {ex.Message}");

                return "";
            }
        }

        /// <summary>
        ///   A mended wound of the heart is a PERSONAL warming, not a clan-political one: routed to
        ///   <paramref name="holder" />'s own <see cref="NpcProfile.ReputationWithPlayer" /> via the store,
        ///   mirroring the exact fetch/apply/persist pattern <c>CommitmentBehavior.Fulfill</c> uses for its
        ///   own positive credit. A missing store/behavior quietly skips the bump (never throws).
        /// </summary>
        private static void ApplyResolvedRelationBump(Hero holder)
        {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            NpcProfile profile = store.Get(holder.StringId) ?? HeroProfileMapper.FromHero(holder);
            ProfileMutator.ApplyReputationDelta(profile, ResolvedRelationBump);
            profile.LastRelationGainHour = CampaignTime.Now.ToHours;
            store.Set(profile);
        }

        /// <summary>
        ///   A mended grudge is a third, independent chronicle channel (alongside the relation bump and
        ///   the grudge's own ledger entry) — naturally rare, so no further anti-spam guard is needed.
        ///   Never throws into the resolution flow that calls it.
        /// </summary>
        private static void RecordChronicleResolution(Hero holder, int today)
        {
            try
            {
                Hero player = Hero.MainHero;
                if (player == null) return;

                string pronoun = player.IsFemale ? "her" : "his";
                WorldEventStore.Instance?.Record(new WorldEvent {
                    Type = WorldEventType.PlayerFeat,
                    Day = today,
                    Magnitude = 40,
                    InvolvesPlayer = true,
                    Summary = $"{player.Name} made peace with {holder.Name}.",
                    ActorIds = new List<string> {player.StringId, holder.StringId},
                    FactionIds = holder.MapFaction != null
                        ? new List<string> {holder.MapFaction.StringId}
                        : new List<string>(),
                    ChronicleLine = $"{player.Name} made {pronoun} peace with {holder.Name}, and an old grievance was laid to rest"
                });
            }
            catch (Exception ex) { ModLog.Error($"[GRUDGE] chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
