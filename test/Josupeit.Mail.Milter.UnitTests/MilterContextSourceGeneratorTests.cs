// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using NUnit.Framework;

namespace Josupeit.Mail.Milter.UnitTests;

[TestFixture]
public sealed class MilterContextSourceGeneratorTests
{
    private const string PREAMBLE =
        """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Josupeit.Mail.Milter;
        using Action = Josupeit.Mail.Milter.Action;

        namespace Filters;

        """;

    [Test]
    public void Protocol_requirements_name_the_callbacks_that_were_overridden()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnSender(SenderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolRequirements => Josupeit.Mail.Milter.ProtocolRequirements.Sender | Josupeit.Mail.Milter.ProtocolRequirements.Header;"));
        });
    }

    [Test]
    public void A_callback_left_at_the_interface_default_asks_for_no_event()
    {
        var run = GeneratorHarness.Run(Context(string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolRequirements => default(Josupeit.Mail.Milter.ProtocolRequirements);"));
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => default(Josupeit.Mail.Milter.ActionRequirements);"));
        });
    }

    [Test]
    public void Delivery_requirements_are_derived_from_reading_the_rejection_flag()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
            {
                return new(info.IsRejectedByMta ? CommitActions.Continue : CommitActions.Accept);
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("DeliveryRequirements => Josupeit.Mail.Milter.DeliveryRequirements.RejectedRecipients;"));
        });
    }

    [Test]
    public void The_rejection_flag_is_found_through_a_helper_as_well()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
            {
                return new(Decide(info));
            }

            private static CommitAction Decide(RecipientInformation info)
            {
                return info.IsRejectedByMta ? CommitActions.Continue : CommitActions.Accept;
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("DeliveryRequirements => Josupeit.Mail.Milter.DeliveryRequirements.RejectedRecipients;"));
        });
    }

    [Test]
    public void A_filter_that_never_asks_whether_a_recipient_was_rejected_needs_no_delivery_change()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);"));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("DeliveryRequirements => default(Josupeit.Mail.Milter.DeliveryRequirements);"));
            Assert.That(run.GeneratedSource, Does.Not.Contain("LeadingHeaderSpace"), "Nothing in a filter's code can ask for it, so it is never derived.");
        });
    }

    [Test]
    public void A_hand_written_delivery_requirement_is_left_alone()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public static DeliveryRequirements DeliveryRequirements => DeliveryRequirements.LeadingHeaderSpace;

            public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
            {
                return new(info.IsRejectedByMta ? CommitActions.Continue : CommitActions.Accept);
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Does.Not.Contain("DeliveryRequirements =>"), "The author decided; the generator does not argue.");
        });
    }

    [Test]
    public void An_explicit_interface_implementation_counts_as_an_override()
    {
        var run = GeneratorHarness.Run(Context(
            "ValueTask<CommitAction> IMilterCallbacks.OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);"));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolRequirements => Josupeit.Mail.Milter.ProtocolRequirements.ClientHello;"));
    }

    [Test]
    public void A_hand_written_declaration_is_left_alone()
    {
        var run = GeneratorHarness.Run(Context(
            "public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.All;"));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Does.Not.Contain("ProtocolRequirements ProtocolRequirements"));
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements ActionRequirements"));
        });
    }

    [Test]
    public void Action_requirements_name_the_modifications_the_context_builds()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public IAsyncEnumerable<Action> OnEndOfBody()
            {
                var actions = new List<Action>
                {
                    ModificationActions.AddHeader("X-Cruise-Score", "9"),
                    ModificationActions.AddRecipient("bilge.pumps@example.com"),
                    CommitActions.Continue,
                };

                return actions.ToAsyncEnumerable();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => Josupeit.Mail.Milter.ActionRequirements.AddRecipients | Josupeit.Mail.Milter.ActionRequirements.AddHeaders;"));
        });
    }

    [Test]
    public void A_modification_only_a_branch_can_reach_is_still_declared()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private bool _bilgeIsRising;

            public async IAsyncEnumerable<Action> OnEndOfBody()
            {
                await Task.Yield();

                if (_bilgeIsRising)
                {
                    foreach (var deck in Enumerable.Range(0, 3))
                        yield return ModificationActions.DeleteHeader("X-Deck", deck);
                }
                else
                {
                    try
                    {
                        yield return ModificationActions.QuarantineMessage("the ship is fine, allegedly");
                    }
                    finally
                    {
                        _bilgeIsRising = false;
                    }
                }

                yield return CommitActions.Continue;
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => Josupeit.Mail.Milter.ActionRequirements.DeleteHeaders | Josupeit.Mail.Milter.ActionRequirements.QuarantineMessage;"));
        });
    }

    [Test]
    public void A_modification_built_by_another_class_is_followed_into_it()
    {
        var helper =
            """
            using Josupeit.Mail.Milter;

            namespace Filters;

            internal static class Stamps
            {
                public static ModificationAction ForLatecomers() => ModificationActions.ChangeHeader("Subject", "[late] arrived after the gangway closed");
            }
            """;

        var run = GeneratorHarness.Run(
            Context(
                """
                public async IAsyncEnumerable<Action> OnEndOfBody()
                {
                    await Task.Yield();

                    yield return Stamps.ForLatecomers();
                    yield return CommitActions.Continue;
                }
                """),
            helper);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSources[0], Contains.Substring("ActionRequirements => Josupeit.Mail.Milter.ActionRequirements.ChangeHeaders;"));
        });
    }

    [Test]
    public void A_modification_collected_by_an_earlier_callback_is_declared()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private readonly List<Action> _pending = new();

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
            {
                _pending.Add(ModificationActions.ReplaceBody(new System.IO.MemoryStream()));

                return new(CommitActions.Continue);
            }

            public IAsyncEnumerable<Action> OnEndOfBody()
            {
                _pending.Add(CommitActions.Accept);

                return _pending.ToAsyncEnumerable();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => Josupeit.Mail.Milter.ActionRequirements.ChangeBody;"));
        });
    }

    [Test]
    public void An_action_from_behind_an_interface_is_reported_rather_than_guessed()
    {
        var policy =
            """
            using Josupeit.Mail.Milter;

            namespace Filters;

            internal interface IHarbourPolicy
            {
                ModificationAction Decide(string sender);
            }
            """;

        var run = GeneratorHarness.Run(
            Context(
                """
                private readonly IHarbourPolicy _policy = null!;

                public async IAsyncEnumerable<Action> OnEndOfBody()
                {
                    await Task.Yield();

                    yield return _policy.Decide("harbourmaster@example.com");
                    yield return CommitActions.Continue;
                }
                """),
            policy);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Is.EqualTo(new[] { "MILT002" }));
            Assert.That(run.Errors, Contains.Item("CS0535"));
            Assert.That(run.GeneratedSources[0], Does.Not.Contain("ActionRequirements ActionRequirements"));
            Assert.That(run.GeneratedSources[0], Contains.Substring("ProtocolRequirements"));
        });
    }

    [Test]
    public void A_context_that_is_not_partial_is_reported()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal class SeagullFilter : IMilterContext
            {
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Is.EqualTo(new[] { "MILT001" }));
            Assert.That(run.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void A_class_that_is_not_a_context_is_ignored()
    {
        var run = GeneratorHarness.Run(
            """
            namespace Filters;

            internal sealed class Deckhand
            {
                public string Name { get; } = "Rita Regression";
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratedSources, Is.Empty);
            Assert.That(run.GeneratorDiagnostics, Is.Empty);
        });
    }

    [Test]
    public void A_context_split_over_two_files_is_written_once()
    {
        var second =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Josupeit.Mail.Milter;

            namespace Filters;

            internal partial class BarnacleFilter
            {
                public ValueTask<CommitAction> OnEndOfHeaders(CancellationToken cancellationToken) => new(CommitActions.Continue);
            }
            """;

        var run = GeneratorHarness.Run(Context(string.Empty), second);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSources, Has.Length.EqualTo(1));
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolRequirements.EndOfHeaders;"));
        });
    }

    [Test]
    public void A_nested_context_is_written_inside_its_containing_type()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal partial class Harbour
            {
                internal partial class TugFilter : IMilterContext
                {
                    public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
                }
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("partial class Harbour"));
            Assert.That(run.GeneratedSource, Contains.Substring("partial class TugFilter"));
        });
    }

    // The no-reply analysis is the one place a generator bug reaches the wire, so its failure direction
    // is pinned from both sides: proved means the flag is set, anything else means it is not.
    [Test]
    public void A_callback_that_can_only_continue_need_not_be_answered()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private int _headers;

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
            {
                _headers++;

                return new(CommitActions.Continue);
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => Josupeit.Mail.Milter.NoReplyRequirements.Header;"));
        });
    }

    [Test]
    public void An_awaited_callback_that_can_only_continue_need_not_be_answered()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public async ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
            {
                await Task.Yield();

                return CommitActions.Continue;
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => Josupeit.Mail.Milter.NoReplyRequirements.Header;"));
        });
    }

    [Test]
    public void A_callback_that_can_decide_keeps_its_reply()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
            {
                return new(info.Name == "X-Spam" ? CommitActions.Reject : CommitActions.Continue);
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => default(Josupeit.Mail.Milter.NoReplyRequirements);"));
        });
    }

    // Every way of losing track has to land on the safe side, because the unsafe side desynchronises the
    // connection rather than merely costing a round trip.
    [Test]
    public void A_callback_that_delegates_its_answer_keeps_its_reply()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private readonly Func<CommitAction> _policy = () => CommitActions.Continue;

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
            {
                return new(_policy());
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => default(Josupeit.Mail.Milter.NoReplyRequirements);"));
        });
    }

    [Test]
    public void A_callback_nobody_overrode_contributes_no_no_reply_flag()
    {
        var run = GeneratorHarness.Run(Context(string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => default(Josupeit.Mail.Milter.NoReplyRequirements);"));
        });
    }

    [Test]
    public void End_of_body_is_always_answered_however_quiet_the_rest_of_the_filter_is()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);

            public IAsyncEnumerable<Action> OnEndOfBody() => new Action[] { CommitActions.Continue }.ToAsyncEnumerable();
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => Josupeit.Mail.Milter.NoReplyRequirements.Header;"));
            Assert.That(run.GeneratedSource, Does.Not.Contain("NoReplyRequirements.EndOfBody"));
        });
    }

    [Test]
    public void A_hand_written_no_reply_requirement_is_left_alone()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public static NoReplyRequirements NoReplyRequirements => NoReplyRequirements.Body;

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Does.Not.Contain("NoReplyRequirements =>"));
        });
    }

    // A body only proves anything if it is the body that will run. This is not a hypothetical: a partial
    // mock overrides exactly these methods, and so does any filter written as a base class with hooks.
    [Test]
    public void An_overridable_callback_on_an_unsealed_context_proves_nothing()
    {
        var run = GeneratorHarness.Run(PREAMBLE +
            """
            internal partial class BarnacleFilter : IMilterContext
            {
                public virtual ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => default(Josupeit.Mail.Milter.NoReplyRequirements);"));
        });
    }

    [Test]
    public void A_sealed_context_can_still_be_proved_quiet()
    {
        var run = GeneratorHarness.Run(PREAMBLE +
            """
            internal sealed partial class BarnacleFilter : IMilterContext
            {
                public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => Josupeit.Mail.Milter.NoReplyRequirements.Header;"));
        });
    }

    [Test]
    public void A_filter_using_nothing_newer_asks_for_no_particular_version()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Reject);"));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version2;"));
        });
    }

    [Test]
    public void The_unrecognised_command_event_needs_version_3()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnUnknownCommand(UnknownCommandInformation info, CancellationToken cancellationToken) => new(CommitActions.Reject);"));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version3;"));
    }

    [Test]
    public void The_data_event_needs_version_4()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnMessageDataStarted(CancellationToken cancellationToken) => new(CommitActions.Reject);"));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version4;"));
    }

    [Test]
    public void Changing_the_sender_needs_version_6()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public IAsyncEnumerable<Action> OnEndOfBody()
            {
                return new Action[] { ModificationActions.ChangeSender("<bounces@example.com>"), CommitActions.Continue }.ToAsyncEnumerable();
            }
            """));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
    }

    // The skip action contributes no ActionRequirements flag at all - SMFIP_SKIP is an event bit - so a
    // version fold driven off the requirement flags alone would miss it entirely.
    [Test]
    public void Skipping_needs_version_6_even_though_it_asks_for_no_modification_right()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken) => new(CommitActions.SkipRemainingEvents);"));

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => default(Josupeit.Mail.Milter.ActionRequirements);"));
        });
    }

    [Test]
    public void Reading_the_rejection_flag_needs_version_6()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
            {
                return new(info.IsRejectedByMta ? CommitActions.Reject : CommitActions.Accept);
            }
            """));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
    }

    // The maximum wins, not the first or the last thing found.
    [Test]
    public void The_highest_requirement_of_several_is_the_one_asked_for()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask<CommitAction> OnUnknownCommand(UnknownCommandInformation info, CancellationToken cancellationToken) => new(CommitActions.Reject);

            public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken) => new(CommitActions.SkipRemainingEvents);
            """));

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
    }

    [Test]
    public void A_hand_written_version_requirement_is_left_alone()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public static ProtocolVersionRequirement ProtocolVersionRequirement => ProtocolVersionRequirement.Version2;

            public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken) => new(CommitActions.SkipRemainingEvents);
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Does.Not.Contain("ProtocolVersionRequirement =>"));
        });
    }

    private static string Context(string body)
    {
        return PREAMBLE +
            $$"""
              internal partial class BarnacleFilter : IMilterContext
              {
              {{body}}
              }
              """;
    }
}
