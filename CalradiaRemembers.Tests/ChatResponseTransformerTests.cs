// Code written by Gabriel Mailhot, 29/06/2026.
// Documents the defensive net that lets CR read a STREAMED (Server-Sent-Events) chat response. The flagship case
// is the reported one: a player on Chub / Mars got "Failed to parse response: Unexpected character ... value: d"
// because the endpoint streamed "data: {chunk}" lines instead of one JSON object, even with stream:false.

#region

using FluentAssertions;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatResponseTransformerTests
    {
        // Reproduces the actual reported crash shape end to end: if reassembly breaks, the player is back to
        // "Unexpected character ... value: d" instead of getting a reply.
        [Test]
        public void GIVEN_a_streamed_sse_body_WHEN_normalizing_THEN_the_chunks_are_reassembled_into_one_message()
        {
            // The "data: {chunk}" shape that produced the "Unexpected character ... d" error on Chub / Mars.
            string sse =
                "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\",\"content\":\"Hel\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"},\"finish_reason\":null}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                "data: [DONE]\n\n";

            bool normalized = ChatResponseTransformer.TryNormalizeStreamedResponse(sse, out string json);

            normalized.Should().BeTrue();
            JObject root = JObject.Parse(json);
            root["choices"]![0]!["message"]!["content"]!.Value<string>().Should().Be("Hello");
            root["choices"]![0]!["finish_reason"]!.Value<string>().Should().Be("stop");
        }

        // This is the common case (stream:false honoured by the endpoint): the transformer must be a true
        // no-op here, or it would corrupt the vast majority of normal responses that never needed fixing.
        [Test]
        public void GIVEN_a_normal_single_json_object_WHEN_normalizing_THEN_it_is_left_untouched()
        {
            string single = "{\"choices\":[{\"message\":{\"content\":\"Hello\"}}]}";

            ChatResponseTransformer.TryNormalizeStreamedResponse(single, out _).Should().BeFalse();
        }

        // Guards a second real-world shape variant: without the message.content fallback, an endpoint that
        // streams full messages instead of deltas would reassemble to an empty string, a silent empty reply
        // rather than an error.
        [Test]
        public void GIVEN_a_streamed_body_carrying_full_messages_WHEN_normalizing_THEN_message_content_is_used()
        {
            // Some endpoints stream but put the text under message.content rather than delta.content.
            string sse =
                "data: {\"choices\":[{\"message\":{\"content\":\"Once \"}}]}\n" +
                "data: {\"choices\":[{\"message\":{\"content\":\"more.\"}}]}\n" +
                "data: [DONE]\n";

            ChatResponseTransformer.TryNormalizeStreamedResponse(sse, out string json).Should().BeTrue();
            JObject.Parse(json)["choices"]![0]!["message"]!["content"]!.Value<string>().Should().Be("Once more.");
        }

        // Real SSE streams are noisy (blank keep-alive lines, the [DONE] sentinel, an occasional garbled chunk);
        // one bad chunk must not abort the whole reassembly and turn a recoverable response into a hard failure.
        [Test]
        public void GIVEN_sse_with_blank_lines_done_markers_and_one_malformed_chunk_WHEN_normalizing_THEN_they_are_skipped()
        {
            string sse =
                "\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"A\"}}]}\n" +
                "data: not-json-here\n" +
                "\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"B\"}}]}\n" +
                "data: [DONE]\n";

            ChatResponseTransformer.TryNormalizeStreamedResponse(sse, out string json).Should().BeTrue();
            JObject.Parse(json)["choices"]![0]!["message"]!["content"]!.Value<string>().Should().Be("AB");
        }

        // An empty/null body (e.g. a dropped connection) must fall through to the caller's own error handling,
        // not be swallowed here as a false "normalized" success.
        [Test]
        public void GIVEN_empty_or_null_or_whitespace_WHEN_normalizing_THEN_it_is_not_treated_as_streamed()
        {
            ChatResponseTransformer.TryNormalizeStreamedResponse(null, out _).Should().BeFalse();
            ChatResponseTransformer.TryNormalizeStreamedResponse("", out _).Should().BeFalse();
            ChatResponseTransformer.TryNormalizeStreamedResponse("   ", out _).Should().BeFalse();
        }

        // Prevents a worse failure mode than the original bug: a body that merely looks like SSE (a comment or
        // ping preamble) but holds nothing must not fabricate an empty success the caller shows as a blank
        // reply; better to let the caller report the raw body.
        [Test]
        public void GIVEN_an_sse_shaped_body_with_no_parseable_data_WHEN_normalizing_THEN_it_falls_through_to_the_caller()
        {
            // Looks like SSE (an event/comment preamble) but carries no usable data line — let the caller report
            // the raw body rather than fabricate an empty success.
            string sse = "event: ping\n: keep-alive comment\n";

            ChatResponseTransformer.TryNormalizeStreamedResponse(sse, out _).Should().BeFalse();
        }
    }
}
