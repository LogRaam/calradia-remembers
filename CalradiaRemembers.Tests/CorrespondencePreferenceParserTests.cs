// Code written by Gabriel Mailhot, 01/08/2026.
// CorrespondencePreferenceParser is the ONLY thing standing between an LLM's loosely-formatted reply and the
// correspondence-interest dial actually moving. Nearly every reply carries no preference at all, so the
// absent-marker path is the single most exercised branch and must always be silent (None), never an error.
// When the marker IS present, a weak model may still garble or omit a field; each such gap must degrade to a
// named, harmless default rather than losing the whole stated preference, because a player who clearly said
// "less from this lord" should not have that intent discarded over a missing strength token.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CorrespondencePreferenceParserTests
    {
        // The overwhelmingly common case: a reply with no preference at all must parse as None, not as a
        // parse failure. Almost every real reply takes this path.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("My dearest, your letter reached me at Sargot.")]
        public void GIVEN_no_marker_present_WHEN_parsing_THEN_the_result_is_None(string raw)
        {
            CorrespondencePreferenceParser.Parse(raw).Scope.Should().Be(PreferenceScope.None);
        }

        // Each scope must be readable on its own, so a stated preference actually targets the channel the
        // player meant: silencing one lord must not require a scope the parser cannot recognise.
        [TestCase("sender", PreferenceScope.Sender)]
        [TestCase("subject", PreferenceScope.Subject)]
        [TestCase("all", PreferenceScope.All)]
        public void GIVEN_each_recognised_scope_WHEN_parsing_THEN_it_maps_to_the_matching_PreferenceScope(string scopeToken, PreferenceScope expected)
        {
            string raw = $"[CORRESPONDENCE_PREFERENCE]\nscope={scopeToken}\ndirection=less\nstrength=mild";

            CorrespondencePreferenceParser.Parse(raw).Scope.Should().Be(expected);
        }

        // Direction is the sign of the whole preference; reading More as Less (or vice versa) would silence
        // a lord the player actually asked to hear MORE from.
        [TestCase("less", PreferenceDirection.Less)]
        [TestCase("more", PreferenceDirection.More)]
        public void GIVEN_each_recognised_direction_WHEN_parsing_THEN_it_maps_to_the_matching_PreferenceDirection(string directionToken, PreferenceDirection expected)
        {
            string raw = $"[CORRESPONDENCE_PREFERENCE] scope=sender direction={directionToken} strength=strong";

            CorrespondencePreferenceParser.Parse(raw).Direction.Should().Be(expected);
        }

        // Strength picks the magnitude in CorrespondenceInterestPolicy; a Strong preference read as Mild (or
        // the reverse) changes how firmly the player's stated ask actually moves the dial.
        [TestCase("mild", PreferenceStrength.Mild)]
        [TestCase("strong", PreferenceStrength.Strong)]
        public void GIVEN_each_recognised_strength_WHEN_parsing_THEN_it_maps_to_the_matching_PreferenceStrength(string strengthToken, PreferenceStrength expected)
        {
            string raw = $"[CORRESPONDENCE_PREFERENCE] scope=sender direction=less strength={strengthToken}";

            CorrespondencePreferenceParser.Parse(raw).Strength.Should().Be(expected);
        }

        // Tokens may arrive in any order; a model that writes strength before scope must parse identically to
        // one that writes them in the "expected" order.
        [Test]
        public void GIVEN_tokens_in_a_reordered_sequence_WHEN_parsing_THEN_the_preference_still_parses_correctly()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] strength=strong direction=more scope=subject";

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(raw);

            pref.Scope.Should().Be(PreferenceScope.Subject);
            pref.Direction.Should().Be(PreferenceDirection.More);
            pref.Strength.Should().Be(PreferenceStrength.Strong);
        }

        // Neither the marker nor the field values can be trusted to arrive in one particular case; a model
        // that shouts "SCOPE=ALL" must be read identically to one that whispers "scope=all".
        [Test]
        public void GIVEN_mixed_or_upper_case_marker_and_tokens_WHEN_parsing_THEN_it_still_parses_case_insensitively()
        {
            string raw = "[correspondence_preference] SCOPE=ALL Direction=LESS STRENGTH=Strong";

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(raw);

            pref.Scope.Should().Be(PreferenceScope.All);
            pref.Direction.Should().Be(PreferenceDirection.Less);
            pref.Strength.Should().Be(PreferenceStrength.Strong);
        }

        // Real LLM output wraps and pads unpredictably; the tokens may be split across newlines with extra
        // whitespace around the '='. None of that formatting noise should cost the player their stated
        // preference.
        [Test]
        public void GIVEN_extra_whitespace_and_newlines_between_tokens_WHEN_parsing_THEN_it_still_parses_correctly()
        {
            string raw = "Some narration first.\n[CORRESPONDENCE_PREFERENCE]\n  scope =  sender  \n direction= more \n  strength =mild  ";

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(raw);

            pref.Scope.Should().Be(PreferenceScope.Sender);
            pref.Direction.Should().Be(PreferenceDirection.More);
            pref.Strength.Should().Be(PreferenceStrength.Mild);
        }

        // Missing-field defaults: an explicit scope with no direction or strength at all must still produce a
        // usable preference rather than being discarded, defaulting to the gentlest, most conservative reading.
        [Test]
        public void GIVEN_a_marker_with_only_a_scope_WHEN_parsing_THEN_direction_defaults_to_Less_and_strength_to_Mild()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] scope=sender";

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(raw);

            pref.Scope.Should().Be(PreferenceScope.Sender);
            pref.Direction.Should().Be(PreferenceDirection.Less);
            pref.Strength.Should().Be(PreferenceStrength.Mild);
        }

        // Garbled (not merely missing) direction and strength tokens must fall back to the same safe
        // defaults, not throw and not silently zero out the whole preference.
        [Test]
        public void GIVEN_garbled_direction_and_strength_tokens_WHEN_parsing_THEN_they_fall_back_to_the_same_defaults()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] scope=subject direction=dunno strength=whatever";

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(raw);

            pref.Scope.Should().Be(PreferenceScope.Subject);
            pref.Direction.Should().Be(PreferenceDirection.Less);
            pref.Strength.Should().Be(PreferenceStrength.Mild);
        }

        // A missing scope makes the whole preference unusable (there is nothing to apply it to), so it must
        // discard back to None entirely rather than defaulting to some arbitrary scope.
        [Test]
        public void GIVEN_a_marker_with_no_scope_at_all_WHEN_parsing_THEN_the_whole_preference_is_None()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] direction=less strength=strong";

            CorrespondencePreferenceParser.Parse(raw).Scope.Should().Be(PreferenceScope.None);
        }

        // An explicit "scope=none" must behave exactly like a missing scope: the whole preference collapses
        // to None, never a half-formed preference with a None scope but a live direction/strength.
        [Test]
        public void GIVEN_an_explicit_scope_of_none_WHEN_parsing_THEN_the_whole_preference_is_None()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] scope=none direction=more strength=strong";

            CorrespondencePreferenceParser.Parse(raw).Should().Be(CorrespondencePreference.None);
        }

        // A marker present but followed by pure noise (an unrecognised scope) must degrade to None rather
        // than throwing or guessing a scope.
        [Test]
        public void GIVEN_the_marker_followed_by_garbage_WHEN_parsing_THEN_it_degrades_to_None_without_throwing()
        {
            string raw = "[CORRESPONDENCE_PREFERENCE] asdkfjqwer 12345 !!!";

            CorrespondencePreferenceParser.Parse(raw).Scope.Should().Be(PreferenceScope.None);
        }
    }
}
