// Code written by Gabriel Mailhot, 30/07/2026.
// The parser is the ONLY thing standing between raw LLM output and EngagementLedger.Reconcile: the ledger mirror,
// and thus what applies to the world at the lift, is only as honest as this parse. A throw here would kill a
// live council turn; a mis-mapped status would apply a decision the table never actually settled; a lost
// well-formed head to a malformed tail would make the panel flicker away decisions the player just watched form.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class EngagementSnapshotParserTests
    {
        // STAKE: the extractor's documented shape (a flat array of flat objects) must map field-for-field, since
        // a swapped or dropped field would mislabel who is bound or what was decided on the panel the player reads.
        [Test]
        public void GIVEN_a_clean_json_array_of_two_items_WHEN_parsing_THEN_both_map_to_snapshot_items()
        {
            const string raw = "[" +
                "{\"id\":\"raid-sturgia\",\"subject\":\"Aeldric\",\"decision\":\"Aeldric leads the raid on Sturgia\",\"status\":\"agreed\"}," +
                "{\"id\":\"levy\",\"subject\":\"the clan\",\"decision\":\"Raise a new levy\",\"status\":\"discussed\"}" +
                "]";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().HaveCount(2);
            items[0].Key.Should().Be("raid-sturgia");
            items[0].Subject.Should().Be("Aeldric");
            items[0].Text.Should().Be("Aeldric leads the raid on Sturgia");
            items[0].Status.Should().Be(EngagementStatus.Agreed);
            items[1].Key.Should().Be("levy");
            items[1].Status.Should().Be(EngagementStatus.Discussed);
        }

        // STAKE: models routinely fence structured output in markdown even when told not to. If a fence broke the
        // parse the ledger would silently stop updating every time the model reached for that habit.
        [Test]
        public void GIVEN_a_markdown_fenced_array_WHEN_parsing_THEN_it_still_parses()
        {
            const string raw = "```json\n[{\"id\":\"dowry\",\"subject\":\"the clan\",\"decision\":\"A dowry of 5000\",\"status\":\"agreed\"}]\n```";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().ContainSingle().Which.Key.Should().Be("dowry");
        }

        // STAKE: the system prompt demands the array and nothing else, but a model can still wrap it in a
        // sentence. The parser must find the array anyway rather than the ledger going stale on a compliant-ish
        // but not perfectly bare reply.
        [Test]
        public void GIVEN_prose_surrounding_the_array_WHEN_parsing_THEN_the_array_is_still_located_and_parsed()
        {
            const string raw = "Here are the decisions: [{\"id\":\"peace-vlandia\",\"subject\":\"the clan\",\"decision\":\"Offer peace to Vlandia\",\"status\":\"agreed\"}]. Nothing else stands.";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().ContainSingle().Which.Text.Should().Be("Offer peace to Vlandia");
        }

        // STAKE: only an explicit "agreed" may map to Agreed. Since only Agreed engagements commit at the lift, a
        // mis-mapped status here would apply a decision the table is still merely weighing, exactly the unseen
        // agreement the whole ledger design exists to prevent.
        [TestCase("agreed", EngagementStatus.Agreed)]
        [TestCase("Agreed", EngagementStatus.Agreed)]
        [TestCase("AGREED", EngagementStatus.Agreed)]
        [TestCase("discussed", EngagementStatus.Discussed)]
        [TestCase("something-else", EngagementStatus.Discussed)]
        [TestCase(null, EngagementStatus.Discussed)]
        public void GIVEN_a_status_string_WHEN_parsing_THEN_it_maps_case_insensitively_and_defaults_to_discussed(string status, EngagementStatus expected)
        {
            string statusJson = status == null ? "null" : "\"" + status + "\"";
            string raw = "[{\"id\":\"x\",\"subject\":\"s\",\"decision\":\"d\",\"status\":" + statusJson + "}]";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().ContainSingle().Which.Status.Should().Be(expected);
        }

        // STAKE: a keyless or textless element is exactly what EngagementLedger.Reconcile itself would ignore.
        // Letting it through here would still be harmless downstream, but skipping it early keeps the ledger
        // input clean and matches the extractor contract (id must be stable, decision must exist).
        [Test]
        public void GIVEN_an_element_missing_id_or_with_blank_decision_WHEN_parsing_THEN_it_is_skipped()
        {
            const string raw = "[" +
                "{\"subject\":\"someone\",\"decision\":\"A decision with no id\",\"status\":\"agreed\"}," +
                "{\"id\":\"empty\",\"subject\":\"someone\",\"decision\":\"   \",\"status\":\"agreed\"}," +
                "{\"id\":\"kept\",\"subject\":\"someone\",\"decision\":\"This one is kept\",\"status\":\"agreed\"}" +
                "]";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().ContainSingle().Which.Key.Should().Be("kept");
        }

        // STAKE: null/blank input is the extractor reporting nothing (e.g. an LLM failure already turned into
        // null upstream). The parser must not throw on it, or a quiet upstream failure would become a crash here.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GIVEN_null_or_blank_input_WHEN_parsing_THEN_an_empty_list_is_returned(string raw)
        {
            EngagementSnapshotParser.Parse(raw).Should().BeEmpty();
        }

        // STAKE: a reply that never became JSON at all (a model ignoring the instruction outright) must degrade
        // to "nothing standing" rather than crash the council turn that reads it.
        [Test]
        public void GIVEN_input_that_is_not_json_WHEN_parsing_THEN_an_empty_list_is_returned()
        {
            EngagementSnapshotParser.Parse("not json").Should().BeEmpty();
        }

        // STAKE: a reply cut off mid-stream (context limit, a dropped connection) never even reaches a closing
        // bracket. With no array to locate at all, this must degrade to nothing standing rather than throw and
        // take the council turn down with it.
        [Test]
        public void GIVEN_a_truncated_array_missing_its_closing_bracket_WHEN_parsing_THEN_an_empty_list_is_returned()
        {
            const string raw = "[{\"id\":\"raid\",\"subject\":\"Aeldric\",\"decision\":\"Aeldric leads the raid\",\"status\":\"agreed\"},{\"id\":\"levy\",\"sub";

            EngagementSnapshotParser.Parse(raw).Should().BeEmpty();
        }

        // STAKE: this is the whole reason the parser reads objects one at a time instead of parsing the array in
        // one shot. When the array itself IS bracket-closed but one element mid-way is malformed, the well-formed
        // decision that came before it must still reach the ledger rather than a single bad element blanking a
        // decision the player already watched the table settle.
        [Test]
        public void GIVEN_a_well_formed_head_followed_by_a_malformed_element_WHEN_parsing_THEN_the_head_survives_and_nothing_throws()
        {
            const string raw = "[{\"id\":\"raid\",\"subject\":\"Aeldric\",\"decision\":\"Aeldric leads the raid\",\"status\":\"agreed\"},{\"id\": not valid at all}]";

            IReadOnlyList<EngagementSnapshotItem> items = EngagementSnapshotParser.Parse(raw);

            items.Should().ContainSingle().Which.Key.Should().Be("raid");
        }

        // STAKE: a fully malformed array (broken from the very first element) has no well-formed head to save,
        // but must still degrade to empty rather than throw and take the council turn down with it.
        [Test]
        public void GIVEN_a_malformed_array_with_no_well_formed_head_WHEN_parsing_THEN_an_empty_list_is_returned()
        {
            EngagementSnapshotParser.Parse("[{\"id\": not valid at all").Should().BeEmpty();
        }
    }
}
