// Code written by Gabriel Mailhot, 02/10/2026.
// The chat's partner's profile must be the partner's own. NpcMemoryCampaignBehavior.DialogFlow.Profile is a session-lifetime
// cache: the LAST person reached through the game's dialogue menu. Read as "the current partner" it sent work to the wrong
// ledger three times: a captor scene wore another hero's memories ("Garios thinks he's Fercread"), a captor scene's summary
// landed on the last person chatted with (Nemesis audit RC2), and on 02/10/2026 the offer bag looked for a companion's
// supply task in someone else's profile (Lordfadooboo: "a companion asked me to deliver 20 sacks of grain... the quest
// wouldnt complete"). The one legitimate read is MainSpeaker's last-resort fallback, behind every live resolution. If this
// guard is wrong, a fourth path quietly reads the cache and some player's deed is credited to, or refused by, the wrong person.

#region

using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SpeakerProfileSourceTests
    {
        // Every code read of DialogFlow.Profile, comments aside, must be the speaker selector's last-resort fallback.
        [Test]
        public void GIVEN_the_mod_source_WHEN_a_conversation_partners_profile_is_read_THEN_only_the_speaker_selectors_fallback_reads_the_dialog_cache()
        {
            if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

            var reads = SourceTree.Production
                .Where(f => f.Layer == Layer.Mod)
                .SelectMany(f => f.Text.Split('\n')
                                   .Select((line, i) => (f.RelativePath, Line: i + 1, Code: Regex.Replace(line, "//.*$", "")))
                                   .Where(l => Regex.IsMatch(l.Code, @"DialogFlow\??\.Profile")))
                .ToList();

            reads.Select(r => $"{r.RelativePath}:{r.Line}")
                 .Should().HaveCount(1, "only MainSpeaker's last-resort fallback may read the session cache; found: "
                                        + string.Join(", ", reads.Select(r => $"{r.RelativePath}:{r.Line}")));
            reads[0].RelativePath.Should().EndWith("ChatViewModel.Helpers.cs");
        }
    }
}
