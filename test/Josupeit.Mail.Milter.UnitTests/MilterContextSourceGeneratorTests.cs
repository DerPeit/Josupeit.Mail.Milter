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

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

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
            Assert.That(run.GeneratorDiagnostics[0].GetMessage(), Contains.Substring("the partial modifier on 'SeagullFilter'; declare 'ProtocolRequirements' and 'ActionRequirements' by hand"));
            Assert.That(run.GeneratedSources, Is.Empty);
        });
    }

    // The four virtual properties fall back to their interface defaults in a class nothing can be
    // generated into. For a filter that uses none of what they govern, those defaults are exactly what
    // the generator would have written, so there is nothing to complain about.
    [Test]
    public void A_context_that_is_not_partial_but_needs_only_the_defaults_is_left_alone()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal sealed class PelicanFilter : IMilterContext
            {
                public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.Header;

                public static ActionRequirements ActionRequirements => ActionRequirements.None;

                public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorDiagnostics, Is.Empty, "A provable no-reply is an optimisation; losing it is no reason to fail the build.");
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSources, Is.Empty);
        });
    }

    // The macro list is not an optimisation: it replaces the MTA's configured set for the stages it
    // names. Without it the MTA sends only that set - Postfix sends nothing but "i" at end-of-body - and
    // the filter finds any other key missing at runtime.
    [Test]
    public void A_context_that_is_not_partial_and_reads_macros_is_told_to_declare_them()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal sealed class CormorantFilter : IMilterContext
            {
                public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.EndOfBody;

                public static ActionRequirements ActionRequirements => ActionRequirements.None;

                private string? _client;

                public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
                {
                    if (info.ProtocolStage == ProtocolStage.EndOfBody)
                        _client = info.Macros["{client_name}"];

                    return new();
                }
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Is.EqualTo(new[] { "MILT001" }));
            Assert.That(run.GeneratorDiagnostics[0].GetMessage(), Contains.Substring("declare 'MacroRequirements' by hand"));
            Assert.That(run.GeneratedSources, Is.Empty);
        });
    }

    // Reading IsRejectedByMta derives RejectedRecipients, which in turn needs version 6. Without either,
    // the MTA never reports a rejected recipient and the read is false for good.
    [Test]
    public void A_context_that_is_not_partial_and_reads_rejected_recipients_is_told_to_declare_them()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal sealed class PuffinFilter : IMilterContext
            {
                public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.Recipient;

                public static ActionRequirements ActionRequirements => ActionRequirements.None;

                public ValueTask<CommitAction> OnRecipient(RecipientInformation info, CancellationToken cancellationToken)
                {
                    return new(info.IsRejectedByMta ? CommitActions.Continue : CommitActions.Accept);
                }
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Is.EqualTo(new[] { "MILT001" }));
            Assert.That(run.GeneratorDiagnostics[0].GetMessage(), Contains.Substring("declare 'DeliveryRequirements' and 'ProtocolVersionRequirement' by hand"));
            Assert.That(run.GeneratedSources, Is.Empty);
        });
    }

    // The class itself is partial here; the type around it is not, and the generator has to write both.
    // The message has to name the one that is missing the modifier, not the one the author is looking at.
    [Test]
    public void A_context_inside_a_type_that_is_not_partial_names_that_type()
    {
        var run = GeneratorHarness.Run(
            PREAMBLE +
            """
            internal static class Lighthouse
            {
                internal sealed partial class KeeperFilter : IMilterContext
                {
                }
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Is.EqualTo(new[] { "MILT001" }));
            Assert.That(run.GeneratorDiagnostics[0].GetMessage(), Contains.Substring("the partial modifier on 'Lighthouse';"));
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

    // The implementation the analysis is handed is the one that satisfies the interface, which is the
    // base's - not the override that actually runs. So a callback inherited from a base is never proved
    // quiet, whatever either body says, and both directions are here because only one of them is safe by
    // luck: the second would be a wrong derivation if the analysis followed the base's body.
    [TestCase("Reject", "Continue", TestName = "The_base_decides_and_the_override_is_quiet")]
    [TestCase("Continue", "Reject", TestName = "The_base_is_quiet_and_the_override_decides")]
    public void A_callback_inherited_from_a_base_is_never_proved_quiet(string baseAnswer, string overrideAnswer)
    {
        var run = GeneratorHarness.Run(PREAMBLE +
            $$"""
              internal abstract class QuietBase : IMilterContext
              {
                  public static ProtocolRequirements ProtocolRequirements => ProtocolRequirements.Header;

                  public static ActionRequirements ActionRequirements => ActionRequirements.None;

                  public virtual ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.{{baseAnswer}});
              }

              internal sealed partial class BarnacleFilter : QuietBase
              {
                  public override ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.{{overrideAnswer}});
              }
              """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Does.Not.Contain("NoReplyRequirements.Header"));
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

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version3;"));
    }

    [Test]
    public void The_data_event_needs_version_4()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnMessageDataStarted(CancellationToken cancellationToken) => new(CommitActions.Reject);"));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

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

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
    }

    // AddRecipientAction_V6 derives from AddRecipientAction_V2 and both carry a marker. The analysis
    // takes the nearest one up the hierarchy, so the derived action has to declare the parameters right
    // on its own: picking up the base's as well would be harmless on the wire but would put the version
    // fold and the requirement flags out of step for anyone reading the generated line.
    [Test]
    public void Adding_a_recipient_with_arguments_needs_version_6_and_only_the_parameters_right()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public IAsyncEnumerable<Action> OnEndOfBody()
            {
                return new Action[] { ModificationActions.AddRecipient("<audit@example.com>", "NOTIFY=NEVER"), CommitActions.Continue }.ToAsyncEnumerable();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version6;"));
            Assert.That(run.GeneratedSource, Contains.Substring("ActionRequirements => Josupeit.Mail.Milter.ActionRequirements.AddRecipientsWithParameters;"));
        });
    }

    // The skip action contributes no ActionRequirements flag at all - SMFIP_SKIP is an event bit - so a
    // version fold driven off the requirement flags alone would miss it entirely.
    [Test]
    public void Skipping_needs_version_6_even_though_it_asks_for_no_modification_right()
    {
        var run = GeneratorHarness.Run(Context(
            "public ValueTask<CommitAction> OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken) => new(CommitActions.SkipRemainingEvents);"));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

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

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

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

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

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

    // Neither of these may raise the floor. Both degrade into behaviour the filter already copes with -
    // an MTA below version 6 sends its own, larger macro set and waits for every answer - so demanding
    // version 6 would refuse an MTA the filter works perfectly well against. Reading one macro at
    // end-of-body is the commonest thing a filter does, and it used to derive Version6.
    [Test]
    public void Reading_macros_and_answering_quietly_leave_the_version_floor_alone()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private string? _queueId;

            public ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken) => new(CommitActions.Continue);

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (info.ProtocolStage == ProtocolStage.EndOfBody)
                    _queueId = info.Macros["i"];

                return new();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("NoReplyRequirements => Josupeit.Mail.Milter.NoReplyRequirements.Header;"));
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements => Josupeit.Mail.Milter.MacroRequirements.For(Josupeit.Mail.Milter.ProtocolStage.EndOfBody, \"i\");"));
            Assert.That(run.GeneratedSource, Contains.Substring("ProtocolVersionRequirement => Josupeit.Mail.Milter.ProtocolVersionRequirement.Version2;"));
        });
    }

    [Test]
    public void A_filter_that_never_reads_a_macro_asks_for_no_list()
    {
        var run = GeneratorHarness.Run(Context(string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratorErrorIds, Does.Not.Contain("MILT003"), "Reading no macros is the commonest case, not a failure.");
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements => Josupeit.Mail.Milter.MacroRequirements.None;"));
        });
    }

    [Test]
    public void Stage_guarded_macro_reads_are_derived_per_stage()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private string? _sender;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (info.ProtocolStage == ProtocolStage.Sender)
                    _sender = info.Macros["{mail_addr}"];

                return new();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements.For(Josupeit.Mail.Milter.ProtocolStage.Sender, \"{mail_addr}\")"));
        });
    }

    [Test]
    public void A_switch_over_the_stage_is_understood_as_well()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private string? _queueId;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                switch (info.ProtocolStage)
                {
                    case ProtocolStage.EndOfBody:
                        _queueId = info.Macros["i"];
                        break;
                }

                return new();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements.For(Josupeit.Mail.Milter.ProtocolStage.EndOfBody, \"i\")"));
        });
    }

    // The shape C# leads to: TryGetValue in the condition, behind the stage test. The right operand of &&
    // only runs once the left one held, so the stage test guards it as surely as an enclosing if would.
    [Test]
    public void A_lookup_sharing_the_condition_with_the_stage_test_is_derived()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private string? _queueId;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (info.ProtocolStage == ProtocolStage.EndOfBody && info.Macros.TryGetValue("i", out var queueId))
                    _queueId = queueId;

                return new();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratorErrorIds, Does.Not.Contain("MILT003"));
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements.For(Josupeit.Mail.Milter.ProtocolStage.EndOfBody, \"i\")"));
        });
    }

    [Test]
    public void A_stage_test_anywhere_in_an_and_chain_guards_the_body()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private string? _queueId;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (_queueId == null && (info.ProtocolStage == ProtocolStage.EndOfBody && !cancellationToken.IsCancellationRequested))
                    _queueId = info.Macros["i"];

                return new();
            }
            """));

        Assert.Multiple(() =>
        {
            Assert.That(run.Errors, Is.Empty);
            Assert.That(run.GeneratorErrorIds, Does.Not.Contain("MILT003"));
            Assert.That(run.GeneratedSource, Contains.Substring("MacroRequirements.For(Josupeit.Mail.Milter.ProtocolStage.EndOfBody, \"i\")"));
        });
    }

    // The left operand of && runs whatever the stage, so a lookup there is not guarded by a stage test to
    // its right. And two comparisons naming different stages can never both hold, so picking either one
    // would be a guess.
    [TestCase("info.Macros.TryGetValue(\"i\", out var queueId) && info.ProtocolStage == ProtocolStage.EndOfBody", TestName = "A_lookup_ahead_of_the_stage_test_is_reported")]
    [TestCase("info.ProtocolStage == ProtocolStage.EndOfBody && info.ProtocolStage == ProtocolStage.Sender && info.Macros.TryGetValue(\"i\", out var queueId)", TestName = "An_and_chain_naming_two_stages_is_reported")]
    public void An_and_chain_that_does_not_pin_the_stage_is_reported(string condition)
    {
        var run = GeneratorHarness.Run(Context(
            $$"""
            private string? _queueId;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if ({{condition}})
                    _queueId = queueId;

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratorErrorIds, Does.Contain("MILT003"));
    }

    // The common stash. It genuinely cannot be analysed, and a list that leaves a macro out fails at
    // runtime rather than at negotiation - so it is refused rather than guessed.
    [Test]
    public void Keeping_the_whole_dictionary_cannot_be_analysed_and_is_reported()
    {
        var run = GeneratorHarness.Run(Context(
            """
            private IReadOnlyDictionary<string, string>? _macros;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                _macros = info.Macros;

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Does.Contain("MILT003"));
            Assert.That(run.GeneratedSource, Does.Not.Contain("MacroRequirements =>"));
        });
    }

    [Test]
    public void A_macro_name_that_is_not_a_constant_is_reported()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (info.ProtocolStage == ProtocolStage.Sender)
                {
                    var key = "{mail" + "_addr}";

                    _ = info.Macros[key];
                }

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratorErrorIds, Does.Contain("MILT003"));
    }

    [Test]
    public void A_macro_read_no_stage_guards_is_reported()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                _ = info.Macros["j"];

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratorErrorIds, Does.Contain("MILT003"));
    }

    // Header and body carry no macros of their own, so no list can name either stage. Emitting the call
    // anyway would compile and then throw the first time the filter was used.
    [Test]
    public void A_macro_read_at_a_stage_with_no_macro_class_is_reported()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                if (info.ProtocolStage == ProtocolStage.Header)
                    _ = info.Macros["j"];

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.That(run.GeneratorErrorIds, Does.Contain("MILT003"));
    }

    [Test]
    public void A_hand_written_macro_requirement_is_left_alone()
    {
        var run = GeneratorHarness.Run(Context(
            """
            public static MacroRequirements MacroRequirements => MacroRequirements.None;

            public ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
            {
                _ = info.Macros["j"];

                return new();
            }
            """));

        Assert.That(run.Errors, Is.Empty, "The generated code has to compile, not merely read correctly.");

        Assert.Multiple(() =>
        {
            Assert.That(run.GeneratorErrorIds, Does.Not.Contain("MILT003"), "The escape hatch has to work, or the diagnostic is a dead end.");
            Assert.That(run.GeneratedSource, Does.Not.Contain("MacroRequirements =>"));
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
