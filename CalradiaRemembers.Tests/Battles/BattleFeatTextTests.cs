// Code written by Gabriel Mailhot, 06/09/2026.
// BattleFeatText writes the deeds a battle leaves in the memory of those who were there, and the version that
// circulates to those who were not. It guards ONE rule above all (Gabriel, ratified 2026-09-06): a LORD is bested,
// put down, or taken alive, and is called killed ONLY when the engine really killed them. Bannerlord knocks heroes
// out far more often than it kills them, so a line saying "you killed Lucon" would have NPCs mourning a man sitting
// in the player's own dungeon, which is exactly the amnesia-and-nonsense this whole mod exists to avoid. The second
// thing these pin is the VOICE: a witness speaks first-hand ("I saw you..."), the realm speaks as hearsay ("They
// say..."), because the recent-matter approach routes those two registers differently.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Battles
{
    [TestFixture]
    public sealed class BattleFeatTextTests
    {
        // The truth rule: a beaten lord is BESTED, never killed, because he is almost certainly alive.
        [Test]
        public void GIVEN_a_bested_foe_WHEN_written_THEN_it_never_claims_a_death()
        {
            string line = BattleFeatText.WitnessedFeat("Lucon of Fen Company", CombatFeatOutcome.Bested, "the siege of Poros");

            line.Should().Contain("best").And.Contain("Lucon of Fen Company");
            line.Should().NotContain("cut").And.NotContain("kill");
        }

        // A captive is a fact the player can verify in their own prisoner roster, so it is stated plainly and, again,
        // without claiming a death.
        [Test]
        public void GIVEN_a_foe_taken_alive_WHEN_written_THEN_the_capture_is_stated_and_no_death_is_claimed()
        {
            string line = BattleFeatText.WitnessedFeat("Lucon", CombatFeatOutcome.TakenAlive, "");

            line.Should().Contain("alive");
            line.Should().NotContain("kill");
        }

        // Only a REAL death is spoken of as one. This is the single case where the harsher verb is the honest one,
        // and it must stay reachable or genuine deaths would read as mere defeats.
        [Test]
        public void GIVEN_a_genuinely_slain_foe_WHEN_written_THEN_the_death_is_spoken_plainly()
        {
            BattleFeatText.WitnessedFeat("Lucon", CombatFeatOutcome.Slain, "").Should().Contain("cut");
        }

        // A witness speaks in the FIRST PERSON: this line is fed to an NPC who was there, and the recent-matter
        // approach frames such a memory as something he lived, never as distant rumor.
        [Test]
        public void GIVEN_a_witnessed_deed_WHEN_written_THEN_it_is_in_the_first_person()
        {
            BattleFeatText.WitnessedFeat("Lucon", CombatFeatOutcome.Bested, "").Should().StartWith("I saw");
            BattleFeatText.WitnessedPlayerDowned("Lucon", "").Should().StartWith("I saw");
        }

        // The mirror deed is recorded too: who put the PLAYER down. It seeds rivalries and grudges, so it must name
        // the foe rather than blur into a generic defeat.
        [Test]
        public void GIVEN_the_player_was_downed_WHEN_written_THEN_the_foe_is_named()
        {
            BattleFeatText.WitnessedPlayerDowned("Ganak", "open battle", "Adam").Should().Contain("Ganak").And.Contain("open battle").And.Contain("Adam");

            // WHOSE "YOU" (reminensce, 19/09/2026): a memory read in a prompt that calls the CHARACTER "you" must
            // not use "you" for the player. With no name to hand it says "the player", never "you".
            BattleFeatText.WitnessedPlayerDowned("Ganak", "open battle").Should().Contain("the player").And.NotContain(" you ");
        }

        // The version that reaches lords who were NOT there is hearsay, third person, naming both parties: an
        // exploit travels as something people say, never as something the listener witnessed.
        [Test]
        public void GIVEN_an_exploit_that_travels_WHEN_written_THEN_it_is_hearsay_naming_both_parties()
        {
            string line = BattleFeatText.GossipedExploit("Aeliana", "Lucon", CombatFeatOutcome.Bested, "the siege of Poros");

            line.Should().StartWith("They say").And.Contain("Aeliana").And.Contain("Lucon");
            line.Should().NotContain("I saw");
        }

        // The place is optional: a deed with no engagement name must still read as a clean sentence, never leaving a
        // dangling "at ." for the model to imitate.
        [Test]
        public void GIVEN_no_engagement_name_WHEN_written_THEN_the_line_has_no_dangling_place()
        {
            BattleFeatText.WitnessedFeat("Lucon", CombatFeatOutcome.Bested, "").Should().NotContain(" at .");
            BattleFeatText.GossipedExploit("Aeliana", "Lucon", CombatFeatOutcome.Bested, null).Should().NotContain(" at .");
        }

        // A missing name must degrade to something sayable rather than an empty gap, since these lines go straight
        // into a prompt.
        [Test]
        public void GIVEN_a_missing_foe_name_WHEN_written_THEN_it_degrades_to_a_sayable_phrase()
        {
            BattleFeatText.WitnessedFeat("", CombatFeatOutcome.Bested, "").Should().Contain("a foe");
        }

        // Inc 4: a deed handed to the very man it is about must be in HIS voice. Without this, the profile of the
        // lord the player bested would read "I saw you best Lucon" when he IS Lucon, which is exactly the kind of
        // nonsense a memory mod cannot afford.
        [Test]
        public void GIVEN_the_bested_foe_himself_WHEN_written_THEN_he_speaks_of_his_own_defeat()
        {
            string line = BattleFeatText.SubjectBestedByPlayer(CombatFeatOutcome.Bested, "the siege of Poros", "Adam");

            line.Should().StartWith("Adam bested me");
            line.Should().NotContain("I saw");
        }

        // The truth rule holds in the subject's voice too: a beaten lord recounts a defeat, never his own death.
        [Test]
        public void GIVEN_the_bested_foe_himself_WHEN_written_THEN_no_death_is_claimed()
        {
            BattleFeatText.SubjectBestedByPlayer(CombatFeatOutcome.TakenAlive, "").Should().NotContain("cut");
            BattleFeatText.SubjectBestedByPlayer(CombatFeatOutcome.Bested, "").Should().NotContain("cut");
        }

        // The mirror, from the man who did it: he did not WATCH someone put the player down, so his memory must own
        // the deed, which is what makes him a credible rival afterwards.
        [Test]
        public void GIVEN_the_man_who_downed_the_player_WHEN_written_THEN_he_owns_the_deed()
        {
            string line = BattleFeatText.SubjectDownedThePlayer("open battle", "Adam");

            line.Should().Contain("I put Adam down myself");
            line.Should().NotContain("I saw");
        }
    }
}
