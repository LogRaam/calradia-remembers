// Code written by Gabriel Mailhot, 18/07/2026.
// What the world is told when the player gets out of captivity, and whether anyone is named for it.
//
// The fault, from Gabriel's own campaign (2026-07-18), and the log is worth reading before touching this.
// A lord fell upon the bandits holding her; the game told her she slipped away in the confusion of the
// fight. She thanked him for the rescue. He answered "you found your way free before I arrived, or someone
// else did", which was not a denial and not a hallucination: it was the chronicle read back to him, since
// the only thing recorded was "Arwa won free of captivity" with no other actor in it.
//
// What the log settled, after two wrong readings of mine:
//   - The engine reported EndCaptivityDetail.ReleasedAfterEscape, NOT ReleasedAfterBattle. Gating on the
//     battle label missed this case entirely, which is the common one: the captors are merely ENGAGED, the
//     guard slips, the prisoner walks, and the engine files it as an escape.
//   - So the signal is that the captors are IN a fight when the player gets out, whatever label follows.
//   - And the wording must carry BOTH truths. She got herself out; he is the reason she could. Crediting
//     him with a clean rescue overstates it, and naming nobody erases him, which is what shipped.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptivityReleaseNarratorTests
   {
      // THE REPORTED CASE: the man who broke the band is named, so he cannot be told his own deed was
      // someone else's. Naming him is the entire fix; everything else here guards the edges of it.
      [Test]
      public void GIVEN_a_lord_broke_the_band_holding_the_player_WHEN_chronicling_THEN_he_is_named()
      {
         CaptivityReleaseNarrator.Summary("Arwa", "Maritzios")
                                 .Should().Contain("Maritzios");
         CaptivityReleaseNarrator.ChronicleLine(new CaptivityReleaseNarrator.ReleaseLineFacts {PlayerName = "Arwa", PossessivePronoun = "her", LiberatorName = "Maritzios"})
                                 .Should().Contain("Maritzios");
      }

      // And the claim it replaces has to GO, not merely be joined: "won free" is what the lord quoted back
      // at her. Leaving both readings in the chronicle would let the model keep picking the wrong one.
      [Test]
      public void GIVEN_a_liberator_is_known_WHEN_chronicling_THEN_the_player_is_not_said_to_have_freed_themself()
      {
         CaptivityReleaseNarrator.Summary("Arwa", "Maritzios")
                                 .Should().NotContain("won free");
         CaptivityReleaseNarrator.ChronicleLine(new CaptivityReleaseNarrator.ReleaseLineFacts {PlayerName = "Arwa", PossessivePronoun = "her", LiberatorName = "Maritzios"})
                                 .Should().NotContain("won free");
      }

      // The wording states the DEED and nothing else. Not the motive: the engine cannot know it, and the
      // transcript proves a guess would have been wrong (this lord had ridden out looking for her, so
      // "happened upon" would have been as false as "rode to her rescue"). The persona supplies the meaning.
      [Test]
      public void GIVEN_a_liberator_is_known_WHEN_chronicling_THEN_no_motive_is_asserted()
      {
         string summary = CaptivityReleaseNarrator.Summary("Arwa", "Maritzios");

         summary.Should().NotContain("rescue");
         summary.Should().NotContain("saved");
         summary.Should().NotContain("to save");
      }

      // The unchanged path, and it must stay unchanged: an escape or a ransom has no liberator, and "won
      // free" was always the right words for it. This is most releases, so a fix for the rescue case that
      // damaged this one would be a poor trade.
      [Test]
      public void GIVEN_no_liberator_WHEN_chronicling_THEN_the_original_wording_is_kept()
      {
         CaptivityReleaseNarrator.Summary("Arwa", null)
                                 .Should().Be("Arwa won free of captivity.");
         CaptivityReleaseNarrator.ChronicleLine(new CaptivityReleaseNarrator.ReleaseLineFacts {PlayerName = "Arwa", PossessivePronoun = "her", LiberatorName = null})
                                 .Should().Be("after captivity, Arwa won free again, and came back to her own");
      }

      // The fallback that keeps the entry alive: a battle freed the player but no leader could be resolved.
      // Nameless is a worse chronicle than named, but it beats losing the release from the record entirely,
      // which would leave every lord believing she is still in a bandit camp.
      [TestCase("")]
      [TestCase("   ")]
      public void GIVEN_the_liberator_could_not_be_named_WHEN_chronicling_THEN_the_release_is_still_recorded(string name)
      {
         CaptivityReleaseNarrator.Summary("Arwa", name).Should().Contain("won free of captivity");
      }

      // The ordinary credit: the captors were set upon, and their attacker is known.
      [Test]
      public void GIVEN_the_captors_were_engaged_by_a_known_attacker_WHEN_judging_THEN_the_attacker_is_credited()
      {
         CaptivityReleaseNarrator.CreditsLiberator(new CaptivityReleaseNarrator.LiberationFacts {CaptorWasEngaged = true, LiberatorKnown = true, LiberatorIsCaptor = false})
                                 .Should().BeTrue();
      }

      // THE CASE THAT ACTUALLY HAPPENED, and the one the first version of this got wrong. A lord fell upon
      // the band; the game offered the prisoner the confusion of the fight; the ENGINE reported a plain
      // escape. Crediting nobody erases the man who caused it, and calling it a clean rescue overstates
      // what he did. The sentence carries both: she got herself out, and he is why she could.
      [Test]
      public void GIVEN_the_player_slipped_away_during_the_attack_WHEN_chronicling_THEN_both_truths_are_told()
      {
         string summary = CaptivityReleaseNarrator.Summary("Arwa", "Maritzios", brokeFree: true);

         summary.Should().Contain("slipped away");        // she did it
         summary.Should().Contain("Maritzios");           // he is why it was possible
         summary.Should().NotContain("set free when");    // but he did not cut her loose
      }

      // Same distinction in the chronicle's own register.
      [Test]
      public void GIVEN_the_player_broke_loose_in_the_din_WHEN_chronicling_THEN_the_attacker_is_named_without_claiming_the_rescue()
      {
         string line = CaptivityReleaseNarrator.ChronicleLine(new CaptivityReleaseNarrator.ReleaseLineFacts {PlayerName = "Arwa", PossessivePronoun = "her", LiberatorName = "Maritzios", BrokeFree = true});

         line.Should().Contain("Maritzios");
         line.Should().NotContain("cut down her captors");
      }

      // The engine's own label is NOT the signal, which is the whole lesson of the miss: a release reported
      // as an escape can still have an author. Pins that the two wordings differ on how it happened, while
      // both name him.
      [Test]
      public void GIVEN_the_same_liberator_WHEN_the_manner_differs_THEN_the_wording_differs_but_the_name_stays()
      {
         string cutLoose = CaptivityReleaseNarrator.Summary("Arwa", "Maritzios");
         string slipped = CaptivityReleaseNarrator.Summary("Arwa", "Maritzios", brokeFree: true);

         cutLoose.Should().NotBe(slipped);
         cutLoose.Should().Contain("Maritzios");
         slipped.Should().Contain("Maritzios");
      }

      // The gaoler must never be thanked for the cell. A captor who WINS their own battle has freed nobody:
      // the player either changed hands or stayed exactly where they were.
      [Test]
      public void GIVEN_the_winner_is_the_captor_themselves_WHEN_judging_THEN_no_one_is_credited()
      {
         CaptivityReleaseNarrator.CreditsLiberator(new CaptivityReleaseNarrator.LiberationFacts {CaptorWasEngaged = true, LiberatorKnown = true, LiberatorIsCaptor = true})
                                 .Should().BeFalse();
      }

      // A battle the captors were not even in cannot be the reason the player walked out of it. Without
      // this, the next skirmish anywhere on the map would collect the credit.
      [Test]
      public void GIVEN_the_captors_were_not_in_this_battle_at_all_WHEN_judging_THEN_no_one_is_credited()
      {
         CaptivityReleaseNarrator.CreditsLiberator(new CaptivityReleaseNarrator.LiberationFacts {CaptorWasEngaged = false, LiberatorKnown = true, LiberatorIsCaptor = false})
                                 .Should().BeFalse();
      }

      // A leaderless winner (a bandit band, a garrison with no hero) leaves nobody to name, so the entry
      // falls back to the nameless wording rather than inventing a saviour.
      [Test]
      public void GIVEN_the_winning_side_has_no_leader_WHEN_judging_THEN_no_one_is_credited()
      {
         CaptivityReleaseNarrator.CreditsLiberator(new CaptivityReleaseNarrator.LiberationFacts {CaptorWasEngaged = true, LiberatorKnown = false, LiberatorIsCaptor = false})
                                 .Should().BeFalse();
      }
   }
}
