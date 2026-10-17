// Code written by Gabriel Mailhot, 17/10/2026.
// The world's long-term memory (Gabriel's ruling, 17/10/2026): "Pour le World Events, l'expiration de 42 jours devrait
// etre pour les evenements recents. Mais apres 42 jours, ca devrait basculer vers une memoire long terme. La memoire
// long terme servirait entre autre a definir les relations entre NPC.. et pourrait parfois etre utilise pour des
// conversations entre joueur et NPC sur le passe."
//
// LongTermHistoryPolicy decides three things, all from docs/design/NPC_REGARD.md section 7.7a, whose bar Gabriel
// accepted the same day:
// - WHICH third-party event is history rather than news (a battle of several hundred men or more, a siege, a fief
//   changing hands, the death or execution of a lord, a marriage, a war or a peace, a discovered scheme, and, by his
//   answer to question 6, a lord taken captive by a lord). If it is too loose, the tier fills with skirmishes and evicts
//   the battles a lord's father lost; too strict, and the regard between two lords has nothing to stand on.
// - WHO knows a long-term event: no longer by distance (that is news) but by being in it, its kin and clan, its place,
//   and for the greatest events their realms. Wrong, and a lord far away speaks of a quarrel he could not know, or a
//   man cannot speak of his own father's execution.
// - WHAT is evicted past the cap of 3000: the lowest magnitude first, then the oldest, so the bound on the save never
//   costs the realm its great battles.
// The pruner's hand-off (aged-out significant events go to the long-term tier instead of being deleted) is pinned here
// too, next to the 42-day window it must not move.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LongTermHistoryPolicyTests
   {
      private const int Today = 1000;

      // ── Significance ─────────────────────────────────────────────────────────────

      // The ruling's own example: "a skirmish of a few dozen men fades as today". Kept, it would crowd the 3000 slots
      // with police actions nobody tells of years later.
      [Test]
      public void GIVEN_a_skirmish_of_a_few_dozen_men_WHEN_it_leaves_the_news_THEN_the_world_forgets_it()
      {
         //Arrange
         var skirmish = new HistoryCandidateFacts {Kind = HistoryKind.Battle, MenAtStart = 40, Magnitude = 25};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(skirmish);

         //Assert
         kept.Should().BeFalse();
      }

      // 7.7a: "a battle of several hundred men or more" is what a region still tells years later. The line sits where
      // the record already starts calling it "a battle of several hundred men" (BattleRecordText.SeveralHundredFrom),
      // so the history keeps exactly what the news named as such.
      [Test]
      public void GIVEN_a_battle_of_several_hundred_men_WHEN_it_leaves_the_news_THEN_it_becomes_history()
      {
         //Arrange
         var battle = new HistoryCandidateFacts {Kind = HistoryKind.Battle, MenAtStart = BattleRecordText.SeveralHundredFrom, Magnitude = 35};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(battle);

         //Assert
         kept.Should().BeTrue();
      }

      // The other side of that line: "a clash of a hundred or two" is the record's own wording for what falls short of
      // several hundred, and the bar Gabriel accepted does not keep it.
      [Test]
      public void GIVEN_a_clash_of_a_hundred_or_two_WHEN_it_leaves_the_news_THEN_it_fades()
      {
         //Arrange
         var clash = new HistoryCandidateFacts {Kind = HistoryKind.Battle, MenAtStart = BattleRecordText.SeveralHundredFrom - 1, Magnitude = 35};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(clash);

         //Assert
         kept.Should().BeFalse();
      }

      // 7.7a lists "a siege" on its own: a siege is remembered whatever the count of men at the walls.
      [Test]
      public void GIVEN_a_siege_of_a_small_garrison_WHEN_it_leaves_the_news_THEN_it_becomes_history()
      {
         //Arrange
         var siege = new HistoryCandidateFacts {Kind = HistoryKind.Battle, IsSiege = true, MenAtStart = 90, Magnitude = 45};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(siege);

         //Assert
         kept.Should().BeTrue();
      }

      // Battles recorded before this increment carry no count of men. Their magnitude still tells a field battle (35)
      // from a fight over a settlement or a great battle (45 and up), so an old save keeps the fights over walls and the
      // great battles rather than every one of them or none.
      [Test]
      public void GIVEN_a_battle_of_unknown_size_over_a_settlement_WHEN_it_leaves_the_news_THEN_it_becomes_history()
      {
         //Arrange
         var old = new HistoryCandidateFacts {Kind = HistoryKind.Battle, MenAtStart = 0, Magnitude = 45};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(old);

         //Assert
         kept.Should().BeTrue();
      }

      // Same old-save case, the ordinary field battle: its size is unknowable, and the bar asks for several hundred men,
      // so it is not promoted on a guess.
      [Test]
      public void GIVEN_a_field_battle_of_unknown_size_WHEN_it_leaves_the_news_THEN_it_fades()
      {
         //Arrange
         var old = new HistoryCandidateFacts {Kind = HistoryKind.Battle, MenAtStart = 0, Magnitude = 35};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(old);

         //Assert
         kept.Should().BeFalse();
      }

      // Every kind the accepted bar names outright, plus the captures of Gabriel's answer to question 6. Each one lost
      // would leave the regard between two lords without the event that made it.
      [TestCase(HistoryKind.FiefTaken)]
      [TestCase(HistoryKind.Execution)]
      [TestCase(HistoryKind.Marriage)]
      [TestCase(HistoryKind.War)]
      [TestCase(HistoryKind.Peace)]
      [TestCase(HistoryKind.SchemeExposed)]
      [TestCase(HistoryKind.Capture)]
      public void GIVEN_an_event_the_bar_names_WHEN_it_leaves_the_news_THEN_it_becomes_history(HistoryKind kind)
      {
         //Arrange
         var facts = new HistoryCandidateFacts {Kind = kind, Magnitude = 20};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(facts);

         //Assert
         kept.Should().BeTrue();
      }

      // Gabriel's answer of 17/10/2026 to the agent's second question ("2: oui"): rebellions, divorces and lords killed
      // in battle are history too. Lost, a house's rising is forgotten six weeks after it split the realm.
      [TestCase(HistoryKind.Rebellion)]
      [TestCase(HistoryKind.Divorce)]
      [TestCase(HistoryKind.FellInBattle)]
      public void GIVEN_a_rising_a_divorce_or_a_lord_fallen_in_battle_WHEN_it_leaves_the_news_THEN_it_becomes_history(HistoryKind kind)
      {
         //Arrange
         var facts = new HistoryCandidateFacts {Kind = kind, Magnitude = 30};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(facts);

         //Assert
         kept.Should().BeTrue();
      }

      // STAKE: a lord who fell in battle is remembered by his whole realm, not only by his house; below the realm-wide
      // line, a lord of his own kingdom would never have heard that he died.
      [Test]
      public void GIVEN_a_lord_fallen_in_battle_WHEN_a_lord_of_his_realm_with_no_other_tie_asks_THEN_he_knows_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsOfARealmInvolved = true, Magnitude = LongTermHistoryPolicy.FellInBattleMagnitude};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeTrue();
      }

      // Gossip, hardship, omens, looming wars and the rest are news by nature: none is on the bar, and keeping them
      // would fill the tier with what was only ever talk.
      [Test]
      public void GIVEN_mere_gossip_WHEN_it_leaves_the_news_THEN_it_fades()
      {
         //Arrange
         var gossip = new HistoryCandidateFacts {Kind = HistoryKind.None, Magnitude = 60};

         //Act
         bool kept = LongTermHistoryPolicy.IsMemorable(gossip);

         //Assert
         kept.Should().BeFalse();
      }

      // ── Who knows ────────────────────────────────────────────────────────────────

      // 7.7a: "its actors". The man who lost the battle remembers it, whatever his distance from the field today.
      [Test]
      public void GIVEN_a_lord_who_was_in_the_event_WHEN_asked_THEN_he_knows_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsActor = true, Magnitude = 30};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeTrue();
      }

      // 7.7a: "their kin and clan". A son speaks of the battle his father lost at Pravend ten years ago, the design's
      // own example; his house's other members know it too.
      [Test]
      public void GIVEN_the_son_of_a_lord_who_lost_a_battle_WHEN_asked_THEN_he_knows_it()
      {
         //Arrange
         var kin = new HistoryKnowledgeFacts {IsKinOfAnActor = true, Magnitude = 35};
         var clan = new HistoryKnowledgeFacts {SharesAClanWithAnActor = true, Magnitude = 35};

         //Act
         bool kinKnows = LongTermHistoryPolicy.WouldKnow(kin);
         bool clanKnows = LongTermHistoryPolicy.WouldKnow(clan);

         //Assert
         kinKnows.Should().BeTrue();
         clanKnows.Should().BeTrue();
      }

      // 7.7a: "the people of the place it happened in". The lord whose seat it is knows what happened under its walls.
      [Test]
      public void GIVEN_a_lord_of_the_place_WHEN_asked_THEN_he_knows_what_happened_there()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsOfThePlace = true, Magnitude = 35};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeTrue();
      }

      // 7.7a: "for the greatest (magnitude 45 and up), everyone of the realms involved". A war or a city's fall is
      // known across both realms; it has no actors to carry it otherwise.
      [Test]
      public void GIVEN_one_of_the_greatest_events_WHEN_a_lord_of_a_realm_involved_is_asked_THEN_he_knows_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsOfARealmInvolved = true, Magnitude = LongTermHistoryPolicy.RealmWideMagnitude};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeTrue();
      }

      // Below that line a realm tag alone is not enough: a marriage of two minor houses is not something every lord of
      // the realm still knows years later. Without it, the realm rule would swallow the actors-and-kin rule.
      [Test]
      public void GIVEN_a_lesser_event_WHEN_a_lord_of_the_realm_with_no_tie_to_it_is_asked_THEN_he_does_not_know_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsOfARealmInvolved = true, Magnitude = LongTermHistoryPolicy.RealmWideMagnitude - 1};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeFalse();
      }

      // A stranger, far away, of another realm and no kin: 7.7a says the long-term tier is not reached by distance any
      // more, so even the greatest event of a foreign realm is not his to tell.
      [Test]
      public void GIVEN_a_stranger_with_no_tie_to_the_event_WHEN_asked_THEN_he_does_not_know_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {Magnitude = 90};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeFalse();
      }

      // IdentityCrysys (03/10/2026) can keep an event secret or hide it from one character (cr.world_event_secret,
      // cr.world_event_hide). Ageing into history must not undo that veto 42 days later.
      [Test]
      public void GIVEN_an_event_the_player_kept_from_a_lord_WHEN_it_has_become_history_THEN_he_still_does_not_know_it()
      {
         //Arrange
         var facts = new HistoryKnowledgeFacts {IsKinOfAnActor = true, IsOfARealmInvolved = true, Magnitude = 70, WithheldByThePlayer = true};

         //Act
         bool knows = LongTermHistoryPolicy.WouldKnow(facts);

         //Assert
         knows.Should().BeFalse();
      }

      // ── Eviction ─────────────────────────────────────────────────────────────────

      // 7.7a: "a cap (3000 entries), evicting the lowest magnitude first". A flood of lesser captures must never push
      // out the fall of a city.
      [Test]
      public void GIVEN_a_full_history_WHEN_one_more_event_arrives_THEN_the_least_of_them_is_forgotten_not_the_greatest()
      {
         //Arrange
         var great = new FakeHistory(1, 90);
         List<FakeHistory> history = Enumerable.Range(0, LongTermHistoryPolicy.Cap)
                                               .Select(i => new FakeHistory(500 + i % 300, 25))
                                               .Prepend(great)
                                               .ToList();

         //Act
         LongTermHistoryPolicy.Evict(history);

         //Assert
         history.Should().HaveCount(LongTermHistoryPolicy.Cap);
         history.Should().Contain(great, "the oldest entry is also the greatest, and magnitude decides first");
      }

      // Authored history (docs/design/AUTHORED_HISTORY.md section 2): the cap bounds what the game records, not what the
      // campaign's author wrote. Evicted, an author's founding war would vanish once the game had fought 3000 battles.
      [Test]
      public void GIVEN_a_full_history_and_authored_events_WHEN_the_cap_is_applied_THEN_every_authored_event_stays()
      {
         //Arrange
         List<FakeHistory> authored = Enumerable.Range(0, 50).Select(i => new FakeHistory(-1000 + i, 1, authored: true)).ToList();
         List<FakeHistory> history = Enumerable.Range(0, LongTermHistoryPolicy.Cap + 10)
                                               .Select(i => new FakeHistory(500 + i % 300, 40))
                                               .Concat(authored)
                                               .ToList();

         //Act
         LongTermHistoryPolicy.Evict(history);

         //Assert
         history.Should().Contain(authored);
         history.Count(h => !h.IsAuthored).Should().Be(LongTermHistoryPolicy.Cap);
      }

      // "then the oldest": among equals, the most distant past goes first, so what was lived more recently stays.
      [Test]
      public void GIVEN_a_full_history_of_equal_events_WHEN_one_more_arrives_THEN_the_oldest_is_forgotten()
      {
         //Arrange
         var oldest = new FakeHistory(1, 40);
         var newest = new FakeHistory(900, 40);
         List<FakeHistory> history = Enumerable.Range(0, LongTermHistoryPolicy.Cap - 1)
                                               .Select(i => new FakeHistory(100 + i % 500, 40))
                                               .Prepend(oldest)
                                               .Append(newest)
                                               .ToList();

         //Act
         LongTermHistoryPolicy.Evict(history);

         //Assert
         history.Should().HaveCount(LongTermHistoryPolicy.Cap);
         history.Should().NotContain(oldest);
         history.Should().Contain(newest);
      }

      // ── The pruner's hand-off ───────────────────────────────────────────────────

      // The ruling itself: after 42 days a significant event "should switch to long-term memory" rather than vanish.
      [Test]
      public void GIVEN_a_significant_event_past_the_window_WHEN_pruned_THEN_it_is_handed_to_history()
      {
         //Arrange
         var battle = new FakeRumour(Today - WorldEventPruner.RumorWindowDays - 1, true);
         var events = new List<FakeRumour> {battle};

         //Act
         List<FakeRumour> handedOff = WorldEventPruner.PruneAndHandOff(events, Today, e => e.Memorable);

         //Assert
         events.Should().BeEmpty("it is no longer news");
         handedOff.Should().ContainSingle().Which.Should().BeSameAs(battle);
      }

      // The same window, unmoved: the hand-off must not shorten news, which still lasts 42 days inclusive.
      [Test]
      public void GIVEN_a_significant_event_at_the_window_edge_WHEN_pruned_THEN_it_is_still_news()
      {
         //Arrange
         var battle = new FakeRumour(Today - WorldEventPruner.RumorWindowDays, true);
         var events = new List<FakeRumour> {battle};

         //Act
         List<FakeRumour> handedOff = WorldEventPruner.PruneAndHandOff(events, Today, e => e.Memorable);

         //Assert
         events.Should().ContainSingle();
         handedOff.Should().BeEmpty();
      }

      // "A skirmish of a few dozen men fades as today": what is not history is dropped exactly as before.
      [Test]
      public void GIVEN_a_skirmish_past_the_window_WHEN_pruned_THEN_it_is_simply_forgotten()
      {
         //Arrange
         var events = new List<FakeRumour> {new(Today - WorldEventPruner.RumorWindowDays - 1, false)};

         //Act
         List<FakeRumour> handedOff = WorldEventPruner.PruneAndHandOff(events, Today, e => e.Memorable);

         //Assert
         events.Should().BeEmpty();
         handedOff.Should().BeEmpty();
      }

      // In a busy war the 200-rumour cap can push a great battle out of the news before its 42 days are up. The ruling
      // says news ends and history begins; it does not say a battle is forgotten because the season was crowded.
      [Test]
      public void GIVEN_a_crowded_season_WHEN_the_news_cap_pushes_out_a_significant_event_THEN_it_is_handed_to_history()
      {
         //Arrange
         var battle = new FakeRumour(Today - 30, true);
         List<FakeRumour> events = Enumerable.Range(0, WorldEventPruner.MaxRumorEvents)
                                             .Select(_ => new FakeRumour(Today, false))
                                             .Prepend(battle)
                                             .ToList();

         //Act
         List<FakeRumour> handedOff = WorldEventPruner.PruneAndHandOff(events, Today, e => e.Memorable);

         //Assert
         events.Should().HaveCount(WorldEventPruner.MaxRumorEvents);
         handedOff.Should().ContainSingle().Which.Should().BeSameAs(battle);
      }

      // The player's own deeds are the chronicle and never age out; they must not be copied into the third-party history
      // as well, or the player's story would be told twice.
      [Test]
      public void GIVEN_a_deed_of_the_player_far_past_the_window_WHEN_pruned_THEN_it_stays_in_the_chronicle_and_nothing_is_handed_off()
      {
         //Arrange
         var deed = new FakeRumour(Today - 500, true, true);
         var events = new List<FakeRumour> {deed};

         //Act
         List<FakeRumour> handedOff = WorldEventPruner.PruneAndHandOff(events, Today, e => e.Memorable);

         //Assert
         events.Should().ContainSingle();
         handedOff.Should().BeEmpty();
      }

      #region private

      private sealed class FakeHistory : IHistoryRecord
      {
         public FakeHistory(int day, int magnitude, bool authored = false)
         {
            Day = day;
            Magnitude = magnitude;
            IsAuthored = authored;
         }

         public int Day { get; }
         public int Magnitude { get; }
         public bool IsAuthored { get; }
      }

      private sealed class FakeRumour : IRumorRecord
      {
         public FakeRumour(int day, bool memorable, bool involvesPlayer = false)
         {
            Day = day;
            Memorable = memorable;
            InvolvesPlayer = involvesPlayer;
         }

         public bool Memorable { get; }
         public int Day { get; }
         public bool InvolvesPlayer { get; }
         public bool IsPersistentDeed => false;
      }

      #endregion
   }
}
