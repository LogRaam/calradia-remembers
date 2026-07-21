// Code written by Gabriel Mailhot, 21/07/2026.
// Marriage-memory audit (player report, Nexus, v1.31.4): a player watched a full wedding narrated in chat
// and letters, and two [EVENT] entries recorded it ("Mesui and Huan Yi married under Khuzait law...",
// "...were married before the Khan's envoy..."), yet no marriage ever happened in the engine
// (cr.marriage_status reported the NPC still free). ProfileMutator.Apply stored the [EVENT] unconditionally,
// with no check that a matching [ACTION] type: marry had even been emitted, let alone accepted. This policy
// is the gate: it decides whether a summary's TEXT claims the NPC and the player are wed, so the mod can
// withhold the event (ChatViewModel.Exchanges.cs) until Hero.Spouse confirms it against real engine state.
// If this policy under-detects, a confabulated wedding slips through exactly like the bug report. If it
// over-detects, a real, engine-confirmed marriage's own event could get needlessly held back, or an
// unrelated third party's wedding could get misread as the player's own.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MarriageClaimPolicyTests
    {
        // The bug report's FIRST recorded [EVENT]. If the policy misses this, the whole fix is inert: this
        // exact line is what slipped past ProfileMutator ungated and became a 40-day-old false memory.
        [Test]
        public void GIVEN_the_first_bug_report_summary_WHEN_checking_THEN_it_is_detected()
        {
            const string summary = "Mesui and Huan Yi married under Khuzait law at Baltakhand, sealing their union before witnesses.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeTrue();
        }

        // The bug report's SECOND recorded [EVENT], same session, same day. The dedup guard in
        // ApplyNotableEvent already stops a literal re-emission from tripling the memory, but that guard
        // never questioned whether the FIRST one was true; this policy is what should have stopped both.
        [Test]
        public void GIVEN_the_second_bug_report_summary_WHEN_checking_THEN_it_is_detected()
        {
            const string summary = "Mesui and Huan Yi were married before the Khan's envoy; they departed immediately for Kaysar Castle.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeTrue();
        }

        // A THIRD PARTY's wedding must never be read as the player's own: the NPC's name legitimately
        // appears (it is her daughter), but the player is never mentioned and there is no "we/our" marker.
        // Without this boundary, any [EVENT] mentioning the NPC's name near a marriage word would falsely
        // gate a completely unrelated memory, or worse, falsely treat it as backed once the player later
        // marries someone else and Hero.Spouse happens to be non-null.
        [Test]
        public void GIVEN_a_third_partys_wedding_WHEN_checking_THEN_it_is_not_detected()
        {
            const string summary = "Mesui's daughter married a Sturgian lord in a quiet ceremony at the family estate.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeFalse();
        }

        // A mere ENGAGEMENT or agreement to marry LATER is not the falsifiable claim this policy guards:
        // only a consummated wedding is worth gating on Hero.Spouse. Courtship talk of an eventual marriage
        // is exactly what the courtship arc already handles, and treating it as a marriage claim would make
        // the mod nag the player about an "unbacked marriage" every time the NPC merely discusses the future.
        [Test]
        public void GIVEN_a_mere_agreement_to_marry_later_WHEN_checking_THEN_it_is_not_detected()
        {
            const string summary = "Mesui agreed to marry Huan Yi once the spring campaign season ends.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeFalse();
        }

        // Same pending-intention boundary via a different phrasing (a set date, not yet a vow spoken): the
        // bug report itself mentions "a set wedding date" during the courtship, which must not itself trip
        // the gate; only the two later [EVENT]s that actually asserted the vow had been sealed should.
        [Test]
        public void GIVEN_a_set_wedding_date_without_a_sealed_vow_WHEN_checking_THEN_it_is_not_detected()
        {
            const string summary = "Mesui and Huan Yi have set a wedding date for the next full moon.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeFalse();
        }

        // First-person-plural phrasing pins the claim to the player's OWN union even when neither name is
        // spelled out in full: the summary is already scoped to this NPC's profile, so "we were wed" said
        // in that context is unambiguous without needing name matching as a second signal.
        [Test]
        public void GIVEN_first_person_plural_wedding_phrasing_WHEN_checking_THEN_it_is_detected()
        {
            const string summary = "We were wed today before the gods, and I am now her husband in truth.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeTrue();
        }

        // Both names present is not enough on its own: without ANY marriage vocabulary, two people simply
        // being mentioned together (a shared meal, a shared campaign) must never trip the gate.
        [Test]
        public void GIVEN_both_names_present_but_no_marriage_word_WHEN_checking_THEN_it_is_not_detected()
        {
            const string summary = "Mesui and Huan Yi shared a quiet dinner together and spoke of the harvest.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeFalse();
        }

        // "wed" as a bare three-letter token must not fire on an unrelated word that merely contains it as a
        // substring ("steward", "wedged"): a naive Contains() would false-positive on ordinary prose. Also
        // exercises the null/whitespace/no-throw contract on the name parameters won't matter since a false
        // marriage-word match must be ruled out before names are even consulted.
        [Test]
        public void GIVEN_a_word_that_merely_contains_wed_as_a_substring_WHEN_checking_THEN_it_is_not_detected()
        {
            const string summary = "Mesui and Huan Yi's caravan was wedged between two carts on the narrow bridge.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", "Huan Yi").Should().BeFalse();
        }

        // Null, empty, and whitespace-only summaries must never throw (ProfileMutator.Apply already guards
        // an empty [EVENT] upstream, but this policy is called on it too and must degrade safely on its own).
        [Test]
        public void GIVEN_null_or_blank_summary_WHEN_checking_THEN_it_is_not_detected_and_does_not_throw()
        {
            MarriageClaimPolicy.ClaimsMarriageWithPlayer(null, "Mesui", "Huan Yi").Should().BeFalse();
            MarriageClaimPolicy.ClaimsMarriageWithPlayer("", "Mesui", "Huan Yi").Should().BeFalse();
            MarriageClaimPolicy.ClaimsMarriageWithPlayer("   ", "Mesui", "Huan Yi").Should().BeFalse();
        }

        // Null/blank names must not throw either (a defensive contract mirroring the summary case): a caller
        // that has not yet resolved a display name should get a safe "false", not an exception.
        [Test]
        public void GIVEN_null_or_blank_names_WHEN_checking_THEN_it_is_not_detected_and_does_not_throw()
        {
            const string summary = "Mesui and Huan Yi married under Khuzait law at Baltakhand.";

            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, null, "Huan Yi").Should().BeFalse();
            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "Mesui", null).Should().BeFalse();
            MarriageClaimPolicy.ClaimsMarriageWithPlayer(summary, "", "").Should().BeFalse();
        }
    }
}
