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
