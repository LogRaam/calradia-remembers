// Code written by Gabriel Mailhot, 03/07/2026.
// GrudgeNarrator turns a Grudge's raw numbers (Magnitude, Rigidity) into the two pieces of prose the
// mod actually ships: the encyclopedia's "Grievances" sentence and the one-liner injected straight into
// the NPC's LLM prompt (EncounterContextBuilder.ResolveGrudgeNote). The prompt line is not flavour text,
// it steers how the model plays the NPC this turn: wrong wording lets a HIDDEN grudge's true cause slip
// (spoiling the late-unmasking beat the concealment design exists for), a stray digit breaks the
// numberless-narrative rule the ROADMAP ratifies for both surfaces, or a wrong resolution hint sends the
// player chasing an action the grudge does not actually accept. These tests pin the exact wording and
// gating so a wording tweak cannot silently reintroduce one of those.

#region

using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
   [TestFixture]
   public sealed class GrudgeNarratorTests
   {
      // Stake: pins the exact player-reported bug at the boundary that caused it (a cause ending in its
      // own period), so a regression here reintroduces the visible ".." on the encyclopedia page.
      [Test]
      public void GIVEN_a_cause_that_ends_in_a_period_WHEN_building_the_display_line_THEN_the_sentence_ends_in_a_single_period()
      {
         // A player saw "...unfinished.." on the encyclopedia: causes are stored as full sentences with
         // their own punctuation, and the display line appended a second period.
         Grudge grudge = NewGrudge(30, "The player left a task to them unfinished.");

         string line = GrudgeNarrator.DisplayLine(grudge, true, null,
            20, GrudgeResolutionKind.Amends);

         line.Should().EndWith("unfinished.");
         line.Should().NotContain("..");
      }

      // Stake: effectiveMagnitude <= 0 must stop the encyclopedia line, or a grudge the design says has
      // fully softened would keep reading as an open wound forever, contradicting decay itself.
      [Test]
      public void GIVEN_a_fully_decayed_grudge_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
      {
         Grudge grudge = NewGrudge(30);

         GrudgeNarrator.DisplayLine(grudge, true, null,
            0, GrudgeResolutionKind.Amends).Should().BeNull();
      }

      // Stake: same gate, for the LLM prompt this time; without it a faded grudge would keep colouring
      // the NPC's tone (cold, guarded) turn after turn long after nothing should be tinting the exchange.
      [Test]
      public void GIVEN_a_fully_decayed_grudge_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
      {
         Grudge grudge = NewGrudge(20);

         GrudgeNarrator.PromptLine(grudge, true, 0).Should().BeNull();
      }

      // Stake: this wording IS the ratified "assassin register" (ROADMAP): a hidden grudge must read
      // courteous but cold and must never say "openly", or dialogue would leak the true cause and spoil
      // the late-unmasking beat the whole concealment design exists to deliver.
      [Test]
      public void GIVEN_a_hidden_grudge_WHEN_building_the_prompt_line_THEN_the_npc_is_instructed_to_conceal_it()
      {
         Grudge grudge = NewGrudge(50);

         string line = GrudgeNarrator.PromptLine(grudge, false, 30);

         line.Should().Contain("conceal");
         line.Should().NotContain("openly");
      }

      // Stake: pins the OldBitternessCeiling boundary exactly one point past "an old bitterness"; an
      // off-by-one here would misreport to the player how hard a grudge actually is to resolve.
      [Test]
      public void GIVEN_a_high_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_deep_grudge()
      {
         GrudgeNarrator.RigidityWord(GrudgeNarrator.OldBitternessCeiling + 1).Should().Be("a deep grudge");
      }

      // ── Prompt line: the known-vs-hidden mirror ─────────────────────────

      // Stake: a KNOWN grudge (public cause, or one the NPC already voiced) must be allowed to surface in
      // dialogue, or the NPC would stay inexplicably cagey about something the player already knows happened.
      [Test]
      public void GIVEN_a_known_grudge_WHEN_building_the_prompt_line_THEN_the_npc_may_name_it_openly()
      {
         Grudge grudge = NewGrudge(50);

         string line = GrudgeNarrator.PromptLine(grudge, true, 30);

         line.Should().Contain("openly");
         line.Should().Contain(grudge.CauseSummary);
      }

      // Stake: pins the PassingCeiling boundary; misplacing it would call a still-stinging grudge "a
      // passing grievance", underselling how much it should colour the scene.
      [Test]
      public void GIVEN_a_low_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_fresh_wound()
      {
         GrudgeNarrator.RigidityWord(GrudgeNarrator.PassingCeiling + 1).Should().Be("a fresh wound");
      }

      // Stake: pins the FreshWoundCeiling boundary between "a fresh wound" and "an old bitterness", the
      // two middle bands most grudges will actually sit in during a normal playthrough.
      [Test]
      public void GIVEN_a_mid_rigidity_WHEN_naming_the_band_THEN_it_reads_as_an_old_bitterness()
      {
         GrudgeNarrator.RigidityWord(GrudgeNarrator.FreshWoundCeiling + 1).Should().Be("an old bitterness");
      }

      // Stake: a stale TargetId (the target hero unresolved after a save, per
      // EncyclopediaHeroPageGrievancesMixin.BuildLine) must still render as "them" rather than blank
      // text; without this fallback the encyclopedia would show a grammatically broken line.
      [Test]
      public void GIVEN_a_missing_target_name_WHEN_building_the_display_line_THEN_it_falls_back_to_them_rather_than_blank()
      {
         Grudge grudge = NewGrudge(30);

         string line = GrudgeNarrator.DisplayLine(grudge, false, "",
            20, GrudgeResolutionKind.Amends);

         line.Should().Contain("against them");
      }

      // Stake: DisplayLine is called once per grudge on every encyclopedia refresh; a throw here (instead
      // of null) would blank the whole Grievances section for one bad entry, not just skip that entry.
      [Test]
      public void GIVEN_a_null_grudge_WHEN_building_the_display_line_THEN_it_returns_null_not_throw()
      {
         GrudgeNarrator.DisplayLine(null, true, null,
            20, GrudgeResolutionKind.Amends).Should().BeNull();
      }

      // Stake: PromptLine runs inside EncounterContextBuilder.ResolveGrudgeNote on every chat turn; a
      // throw here would only be caught by that caller's broad try/catch, silently dropping the ENTIRE
      // grudge note for the turn instead of just the offending line.
      [Test]
      public void GIVEN_a_null_grudge_WHEN_building_the_prompt_line_THEN_it_returns_null_not_throw()
      {
         GrudgeNarrator.PromptLine(null, true, 50).Should().BeNull();
      }

      // Stake: once GrudgePolicy.Resolve marks a grudge mended, it must vanish from the encyclopedia
      // immediately, or the Amends/Gift/Deed reconciliation the player just performed would look like it
      // changed nothing.
      [Test]
      public void GIVEN_a_resolved_grudge_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
      {
         Grudge grudge = NewGrudge(30);
         grudge.IsResolved = true;

         GrudgeNarrator.DisplayLine(grudge, true, null,
            20, GrudgeResolutionKind.Amends).Should().BeNull();
      }

      // Stake: PromptLine trusts the caller's effectiveMagnitude rather than reading Grudge.IsResolved
      // itself; this pins that a non-positive value (what GrudgePolicy.EffectiveMagnitude returns for a
      // resolved grudge) always suppresses the line, so a resolved grudge can never leak stale coldness
      // into the prompt through this path.
      [Test]
      public void GIVEN_a_resolved_grudges_zero_effective_magnitude_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
      {
         Grudge grudge = NewGrudge(20);

         GrudgeNarrator.PromptLine(grudge, true, -1).Should().BeNull();
      }

      // Stake: a vendetta (Rigidity == VendettaRigidity, e.g. ExecutedKin) must always read as having no
      // mending path, matching GrudgePolicy.IsResolvable's refusal; wording that hinted otherwise would
      // send the player chasing a resolution action that can never actually succeed.
      [Test]
      public void GIVEN_a_vendetta_grudge_with_the_hint_discovered_WHEN_building_the_display_line_THEN_it_reads_as_unforgivable_with_no_mending_path()
      {
         Grudge grudge = NewGrudge(GrudgePolicy.VendettaRigidity);
         grudge.ResolutionHintDiscovered = true;

         string line = GrudgeNarrator.DisplayLine(grudge, false, "Derthert",
            20, GrudgeResolutionKind.Unforgivable);

         line.Should().Contain("unforgivable");
         line.Should().Contain("There is no mending this.");
      }

      // ── Rigidity band words ──────────────────────────────────────────────

      // Stake: pins the floor of the band ladder (rigidity 0), so a barely-formed grudge reads as minor
      // rather than alarming.
      [Test]
      public void GIVEN_a_very_low_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_passing_grievance()
      {
         GrudgeNarrator.RigidityWord(0).Should().Be("a passing grievance");
      }

      // Stake: "narrative text, no numbers" is a ratified surfacing rule (ROADMAP); a leaked digit on the
      // encyclopedia page would expose the internal Rigidity/Magnitude the qualitative-band design exists
      // to hide from the player.
      [Test]
      public void GIVEN_every_rigidity_band_WHEN_building_the_display_line_THEN_no_digits_ever_appear_in_the_generated_text()
      {
         foreach (int rigidity in new[] {0, 10, 30, 60, 90, 100})
         {
            Grudge grudge = NewGrudge(rigidity);
            grudge.ResolutionHintDiscovered = true;

            string line = GrudgeNarrator.DisplayLine(grudge, true, null,
               20, GrudgeResolutionKind.Deed);

            Regex.IsMatch(line, @"\d").Should().BeFalse($"'{line}' must carry no digits");
         }
      }

      // Stake: same no-digits rule for the LLM prompt; a leaked number here risks the model parroting it
      // back in dialogue ("your rigidity is 73"), breaking the numberless chronicler voice both surfaces share.
      [Test]
      public void GIVEN_every_rigidity_band_WHEN_building_the_prompt_line_THEN_no_digits_ever_appear_in_the_generated_text()
      {
         foreach (int rigidity in new[] {0, 10, 30, 60, 90, 100})
         {
            Grudge grudge = NewGrudge(rigidity);

            string known = GrudgeNarrator.PromptLine(grudge, true, 30);
            string hidden = GrudgeNarrator.PromptLine(grudge, false, 30);

            Regex.IsMatch(known, @"\d").Should().BeFalse($"'{known}' must carry no digits");
            Regex.IsMatch(hidden, @"\d").Should().BeFalse($"'{hidden}' must carry no digits");
         }
      }

      // Stake: pins the exact VendettaRigidity value as the top band; this is the same value
      // GrudgePolicy.IsResolvable and ResolutionKindFor key off to refuse any mending path at all.
      [Test]
      public void GIVEN_maximum_rigidity_WHEN_naming_the_band_THEN_it_reads_as_unforgivable()
      {
         GrudgeNarrator.RigidityWord(GrudgePolicy.VendettaRigidity).Should().Be("unforgivable");
      }

      // Stake: an empty CauseSummary (e.g. an old-save grudge synthesized by GrudgeMigration without full
      // text) must fall back to "an old matter" rather than produce a broken sentence like "over ." on
      // the player's encyclopedia page.
      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_display_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Grudge grudge = NewGrudge(30, "");

         string line = GrudgeNarrator.DisplayLine(grudge, true, null,
            20, GrudgeResolutionKind.Amends);

         line.Should().Contain("an old matter");
      }

      // Stake: same fallback, for the prompt line; an empty cause would otherwise hand the LLM a sentence
      // ending "...over , and...", a grammar break that risks confusing the model's own output.
      [Test]
      public void GIVEN_no_cause_summary_WHEN_building_the_prompt_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
      {
         Grudge grudge = NewGrudge(50, "");

         string line = GrudgeNarrator.PromptLine(grudge, true, 30);

         line.Should().NotBeNullOrWhiteSpace();
      }

      // Stake: TargetId is general, not always the player (Grudge.cs: an ex-spouse grudges the NPC who
      // left them, not the player); naming the actual target keeps an NPC-vs-NPC grudge from misreading
      // as "against you" on the player's own screen.
      [Test]
      public void GIVEN_the_grudge_targets_a_named_npc_WHEN_building_the_display_line_THEN_the_npc_is_named_by_name()
      {
         Grudge grudge = NewGrudge(30);

         string line = GrudgeNarrator.DisplayLine(grudge, false, "Derthert",
            20, GrudgeResolutionKind.Amends);

         line.Should().Contain("against Derthert");
         line.Should().NotContain("against you");
      }

      // ── Display line: the encyclopedia's third-person chronicler sentence ──

      // Stake: pins the majority-case branch (most CR grudges target the player), so it must read
      // naturally in second person rather than the third-person "against Derthert" form used for others.
      [Test]
      public void GIVEN_the_grudge_targets_the_player_WHEN_building_the_display_line_THEN_the_player_is_named_you()
      {
         Grudge grudge = NewGrudge(30);

         string line = GrudgeNarrator.DisplayLine(grudge, true, null,
            20, GrudgeResolutionKind.Amends);

         line.Should().Contain("against you");
         line.Should().Contain(grudge.CauseSummary);
      }

      // Stake: Gabriel's screenshot (17/10/2026): a cause stored as a whole sentence read "...over The marriage was
      // ended by..."; it follows a colon now, while a bare matter still reads "over a broken promise".
      [Test]
      public void GIVEN_a_cause_stored_as_a_sentence_WHEN_building_the_display_line_THEN_it_follows_a_colon_not_over()
      {
         GrudgeNarrator.DisplayLine(NewGrudge(50, "The marriage was ended by Arwen's own choice."), true, null, 20, GrudgeResolutionKind.Amends)
                       .Should().Be("Holds an old bitterness against you: The marriage was ended by Arwen's own choice.");
         GrudgeNarrator.DisplayLine(NewGrudge(50), true, null, 20, GrudgeResolutionKind.Amends)
                       .Should().Be("Holds an old bitterness against you, over a broken promise.");
      }

      // Stake: the hint text must match the grudge's actual GrudgeResolutionKind (the ratified
      // Amends/Gift/Deed/Time trio, GrudgePolicy.ResolutionKindFor); a mismatched hint would send the
      // player toward an action GrudgeResolutionPolicy will not accept for that grudge, wasting their effort.
      [TestCase(GrudgeResolutionKind.Amends, "amend")]
      [TestCase(GrudgeResolutionKind.Gift, "gift")]
      [TestCase(GrudgeResolutionKind.Deed, "deed")]
      [TestCase(GrudgeResolutionKind.Time, "Time")]
      [TestCase(GrudgeResolutionKind.Unforgivable, "no mending")]
      public void GIVEN_the_resolution_hint_is_discovered_WHEN_building_the_display_line_THEN_the_hint_matches_the_resolution_kind(
         GrudgeResolutionKind kind,
         string expectedFragment)
      {
         Grudge grudge = NewGrudge(30);
         grudge.ResolutionHintDiscovered = true;

         string line = GrudgeNarrator.DisplayLine(grudge, true, null,
            20, kind);

         line.Should().Contain(expectedFragment);
      }

      // Stake: the ROADMAP's knowledge-gating rule (mirroring DiscoveredTraits): a player who has not yet
      // learned HOW to mend a grudge must see only the grievance itself; leaking the hint early would let
      // them "solve" it without earning that knowledge in-fiction.
      [Test]
      public void GIVEN_the_resolution_hint_is_not_discovered_WHEN_building_the_display_line_THEN_no_hint_sentence_is_appended()
      {
         Grudge grudge = NewGrudge(30);
         grudge.ResolutionHintDiscovered = false;

         string line = GrudgeNarrator.DisplayLine(grudge, true, null,
            20, GrudgeResolutionKind.Amends);

         line.Should().NotContain("might");
         line.Should().NotContain("balm");
         line.Should().NotContain("mending");
      }

      #region private

      private static Grudge NewGrudge(int rigidity, string cause = "a broken promise")
         => new() {
            Id = "holder|3|100",
            HolderId = "holder",
            TargetId = "player",
            Source = GrudgeSource.FailedQuest,
            CauseSummary = cause,
            EventDay = 100,
            Magnitude = 50,
            Rigidity = rigidity
         };

      #endregion
   }
}