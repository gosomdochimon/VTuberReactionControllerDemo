#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using VTuberReactionControllerDemo.Controller;

namespace VTuberReactionControllerDemo.Tests
{
    public sealed class ReactionEventJsonMapperTests
    {
        [Test]
        public void TryFromJson_StandardPayload_UsesReactionTypeAliases()
        {
            const string json = "{"
                + "\"reactionType\":\" throw \","
                + "\"displayName\":\"Tester\","
                + "\"amount\":1234,"
                + "\"comment\":\"test message\""
                + "}";

            bool mapped = ReactionEventJsonMapper.TryFromJson(
                json,
                out ReactionEvent reactionEvent,
                out string failureReason);

            Assert.That(mapped, Is.True, failureReason);
            Assert.That(reactionEvent.Type, Is.EqualTo("throw"));
            Assert.That(reactionEvent.User, Is.EqualTo("Tester"));
            Assert.That(reactionEvent.Amount, Is.EqualTo(1234));
            Assert.That(reactionEvent.Message, Is.EqualTo("test message"));
        }

        [Test]
        public void TryFromJson_BroadcastSelectedReaction_TakesPriorityOverDonationAmount()
        {
            const string json = "{"
                + "\"source\":\"soop-scon\","
                + "\"eventKind\":\"donation\","
                + "\"selectedReaction\":\"throw\","
                + "\"displayName\":\"Viewer\","
                + "\"amount\":20000,"
                + "\"message\":\"selected throw\""
                + "}";

            bool mapped = ReactionEventJsonMapper.TryFromJson(
                json,
                out ReactionEvent reactionEvent,
                out string failureReason);

            Assert.That(mapped, Is.True, failureReason);
            Assert.That(reactionEvent.Type, Is.EqualTo("throw"));
            Assert.That(reactionEvent.Amount, Is.EqualTo(20000));
        }

        [Test]
        public void TryFromJson_BroadcastSelectedBonk_MapsToBonk()
        {
            const string json = "{"
                + "\"source\":\"soop-scon\","
                + "\"eventKind\":\"donation\","
                + "\"selectedReaction\":\"bonk\","
                + "\"displayName\":\"Viewer\","
                + "\"amount\":3000,"
                + "\"message\":\"selected bonk\""
                + "}";

            bool mapped = ReactionEventJsonMapper.TryFromJson(
                json,
                out ReactionEvent reactionEvent,
                out string failureReason);

            Assert.That(mapped, Is.True, failureReason);
            Assert.That(reactionEvent.Type, Is.EqualTo("bonk"));
            Assert.That(reactionEvent.User, Is.EqualTo("Viewer"));
            Assert.That(reactionEvent.Message, Is.EqualTo("selected bonk"));
        }

        [Test]
        public void TryFromJson_BroadcastDonationAmount_MapsToSparkFallback()
        {
            const string json = "{"
                + "\"source\":\"soop-scon\","
                + "\"eventKind\":\"donation\","
                + "\"displayName\":\"Viewer\","
                + "\"amount\":10000,"
                + "\"message\":\"large donation\""
                + "}";

            bool mapped = ReactionEventJsonMapper.TryFromJson(
                json,
                out ReactionEvent reactionEvent,
                out string failureReason);

            Assert.That(mapped, Is.True, failureReason);
            Assert.That(reactionEvent.Type, Is.EqualTo("spark"));
        }

        [Test]
        public void TryFromJson_EmptyPayload_ReturnsFailureReason()
        {
            bool mapped = ReactionEventJsonMapper.TryFromJson(
                " ",
                out ReactionEvent reactionEvent,
                out string failureReason);

            Assert.That(mapped, Is.False);
            Assert.That(reactionEvent, Is.Null);
            Assert.That(failureReason, Is.EqualTo("payload is empty"));
        }
    }
}
#endif
