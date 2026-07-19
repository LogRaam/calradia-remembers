// Code written by Gabriel Mailhot, 18/07/2026.
// Guards a bug that SHIPPED and reached a player (JoshGray700, Nexus 2026-07-18, v1.30.16, Grok low
// reasoning). After joining the Aserai as a vassal, the Sultan Unqid was told by the prompt "The player is
// your liege lord.", and answered in character: "I am no Sultan, only Unqid of Banu Hulyan, sworn to the
// realm you now lead... What do you require of me this day, my liege?" He denied his own throne, invented a
// third-party Sultan to reconcile the contradiction with his rank line, and called his new vassal his liege.
//
// Root cause: PlayerStatusVsNpc describes THE PLAYER's standing, but the mapping in the bridge was written
// from the call site's point of view ("this NPC is my liege"), so vassal and liege were returned inverted.
// The bridge is net472, so nothing could test it.
//
// THESE TESTS ASSERT THE RENDERED SENTENCE, NOT THE ENUM. That is the whole point, and it is the ROADMAP's
// own chain rule: a test that pinned Resolve(...) == PlayerStatusVsNpc.Liege would merely restate the
// convention it is supposed to check, and would have declared the shipped bug correct, exactly as the grudge
// saturation tests once did. The sentence is what the model actually reads, so the sentence is the assertion.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerStandingPolicyTests
   {
      // THE SHIPPED BUG, in the reporter's own situation: the player swore to the Aserai, whose ruler is the
      // NPC. The NPC must be told he is the LIEGE. Before the fix this rendered "The player is your liege
      // lord." and Unqid disowned his own throne to make sense of it.
      [Test]
      public void GIVEN_the_npc_rules_the_realm_the_player_swore_to_WHEN_describing_THEN_the_player_is_the_vassal()
      {
         string note = Describe(new PlayerStandingFacts(npcRulesPlayerRealm: true), "the Aserai");

         note.Should().Contain("You rule the Aserai");
         note.Should().Contain("they are your vassal");
         note.Should().NotContain("liege");
      }

      // The mirror case, which is what makes the inversion an inversion: when the player genuinely rules the
      // realm this NPC serves, "your liege lord" is correct and must survive the fix.
      [Test]
      public void GIVEN_the_player_rules_the_realm_the_npc_serves_WHEN_describing_THEN_the_player_is_the_liege()
      {
         string note = Describe(new PlayerStandingFacts(playerRulesNpcRealm: true), "Vlandia");

         note.Should().Contain("The player rules Vlandia");
         note.Should().Contain("your liege lord");
         note.Should().NotContain("your vassal");
      }

      // The grounding itself, and the reason the realm is named at all. A bare conclusion is unfalsifiable:
      // it contradicted the NPC's own rank line ("Holds royal authority over a kingdom.") and the model
      // resolved the clash the wrong way. Naming who rules what gives it something to check against.
      [Test]
      public void GIVEN_the_realm_is_known_WHEN_describing_THEN_the_note_says_who_rules_it()
      {
         Describe(new PlayerStandingFacts(npcRulesPlayerRealm: true), "the Aserai")
            .Should().Contain("rule the Aserai");
      }

      // Fail safe: the realm can be unknown (an unusual save, a faction mid-collapse). The standing must
      // still be stated, just without its basis, rather than emitting a dangling sentence.
      [TestCase(null)]
      [TestCase("")]
      public void GIVEN_the_realm_is_unknown_WHEN_describing_THEN_the_bare_standing_is_still_stated(string realm)
      {
         Describe(new PlayerStandingFacts(npcRulesPlayerRealm: true), realm)
            .Should().Contain("The player is your sworn vassal.");
      }

      // Captivity is the immediate physical situation and outranks any oath: a liege in chains is a prisoner
      // first. Pins the precedence order, which the extraction had to preserve exactly.
      [Test]
      public void GIVEN_the_player_is_held_by_a_ruler_they_serve_WHEN_describing_THEN_captivity_wins()
      {
         Describe(new PlayerStandingFacts(playerIsCaptiveOfNpc: true, npcRulesPlayerRealm: true), "the Aserai")
            .Should().Contain("The player is currently your captive.");
      }

      // Same blood outranks the feudal chain: a lord of the player's OWN clan is addressed as kin, not as a
      // rung on the ladder. Also the gate that keeps clan lords out of the vassal/liege wording entirely.
      [Test]
      public void GIVEN_the_npc_is_of_the_players_own_clan_WHEN_describing_THEN_kinship_wins()
      {
         Describe(new PlayerStandingFacts(sharesPlayerClan: true, playerRulesNpcRealm: true), "Vlandia")
            .Should().Contain("The player belongs to your own clan.");
      }

      // THE MERCENARY TRAP, and the reason the order in Resolve is load-bearing. The engine records a
      // mercenary contract by setting the clan's Kingdom exactly as it does for a sworn vassal, so BOTH facts
      // are true at once in a real campaign, and CR's own join_as_mercenary action creates that state. Asking
      // the feudal question first reports hired swords as oath-bound ones.
      [Test]
      public void GIVEN_the_player_is_hired_by_the_realm_the_npc_rules_WHEN_describing_THEN_they_are_not_called_a_vassal()
      {
         string note = Describe(
            new PlayerStandingFacts(npcRulesPlayerRealm: true, playerIsMercenaryOfNpcRealm: true, sharesRealm: true),
            "the Aserai");

         note.Should().Contain("hired to the Aserai");
         note.Should().Contain("not by oath");
         // Asserted against the vassal branch's own CLAIM, not the bare word: the mercenary note ends
         // "mercenaries, not sworn vassals", so it names the oath precisely in order to deny it.
         note.Should().NotContain("they are your vassal");
         note.Should().NotContain("has sworn to it");
      }

      // What follows from the distinction, and why it is worth a sentence: a contract buys service, not
      // loyalty. It ends, and they may take the enemy's coin next. A lord who forgets that misjudges them.
      [Test]
      public void GIVEN_a_mercenary_contract_WHEN_describing_THEN_the_note_says_it_is_pay_and_not_fealty()
      {
         Describe(new PlayerStandingFacts(playerIsMercenaryOfNpcRealm: true, sharesRealm: true), "Battania")
            .Should().Contain("mercenaries, not sworn vassals");
      }

      // The gap that rendered as silence: two lords of one crown, neither ruling. The prompt said NOTHING,
      // so a comrade sworn to the same king met the player as a stranger of no particular standing.
      [Test]
      public void GIVEN_both_serve_the_same_crown_and_neither_rules_WHEN_describing_THEN_they_are_named_fellow_vassals()
      {
         string note = Describe(new PlayerStandingFacts(sharesRealm: true), "Vlandia");

         note.Should().Contain("both sworn to Vlandia");
         note.Should().Contain("fellow vassals");
      }

      // The boundary of that new note: equals, explicitly. Without this the model is free to read "same
      // realm" as a chain of command and invent a rank between two lords who have none over each other.
      [Test]
      public void GIVEN_fellow_vassals_WHEN_describing_THEN_neither_is_placed_above_the_other()
      {
         string note = Describe(new PlayerStandingFacts(sharesRealm: true), "Vlandia");

         note.Should().Contain("neither commanding the other");
         note.Should().NotContain("liege lord");
      }

      // The MIRROR of the mercenary trap: the player's own crown rents this NPC's sword. Before this, the
      // liege branch claimed their fealty, which is precisely the thing a contract does not buy.
      [Test]
      public void GIVEN_the_players_realm_hires_the_npcs_clan_WHEN_describing_THEN_the_player_is_paymaster_not_liege()
      {
         string note = Describe(
            new PlayerStandingFacts(playerRulesNpcRealm: true, sharesRealm: true, npcIsMercenaryOfSharedRealm: true),
            "Vlandia");

         note.Should().Contain("paymaster");
         note.Should().Contain("not your liege");
         note.Should().NotContain("they are your liege lord");
      }

      // The subtler half of the same mirror, and the reason it needed its own fact rather than a tweak to the
      // liege branch: a hired band in a realm the player merely SERVES is not a comrade of the same oath.
      // They took the same coin, not the same vow. Saying nothing beats saying something false.
      [Test]
      public void GIVEN_a_hired_band_in_the_realm_the_player_serves_WHEN_describing_THEN_they_are_not_called_fellow_vassals()
      {
         string note = Describe(
            new PlayerStandingFacts(sharesRealm: true, npcIsMercenaryOfSharedRealm: true), "Vlandia");

         note.Should().NotContain("fellow vassals");
         note.Should().NotContain("both sworn to");
      }

      // The ordinary lord, who is most NPCs: no oath binds them to the player in either direction, so the
      // prompt must say nothing at all rather than invent a rank.
      [Test]
      public void GIVEN_no_oath_binds_them_WHEN_describing_THEN_no_standing_is_claimed()
      {
         string note = Describe(new PlayerStandingFacts(), null);

         note.Should().NotContain("vassal");
         note.Should().NotContain("liege");
      }

      // Fail safe: the bridge returns null facts when the engine has no player yet (a menu, a broken save).
      // Unknown renders nothing, which is the only honest answer.
      [Test]
      public void GIVEN_no_facts_at_all_WHEN_resolving_THEN_the_standing_is_unknown()
      {
         PlayerStandingPolicy.Resolve(null).Should().Be(PlayerStatusVsNpc.Unknown);
      }

      #region private

      /// <summary>
      ///   The full chain the game runs: engine facts, the policy's verdict, and the sentence the SDK puts in
      ///   front of the model. Asserting anywhere short of the sentence is what let the inversion ship.
      /// </summary>
      private static string Describe(PlayerStandingFacts facts, string realm)
         => new EncounterContext {
            PlayerStatus = PlayerStandingPolicy.Resolve(facts),
            SharedRealmName = realm
         }.ToPromptDescription();

      #endregion
   }
}
