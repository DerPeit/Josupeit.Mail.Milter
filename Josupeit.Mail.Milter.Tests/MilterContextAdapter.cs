namespace Josupeit.Mail.Milter.Tests;

internal abstract class MilterContextAdapter : IMilterContext
{
    public static ProtocolRequirements ProtocolRequirements { get; set; }

    public static ActionRequirements ActionRequirements { get; set; }

    public virtual ValueTask OnDefineMacro(MacroDefinitionInformation info, CancellationToken cancellationToken)
    {
        return new();
    }

    public virtual ValueTask<CommitAction> OnConnectionEstablished(ConnectionInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }
    
    public virtual ValueTask<CommitAction> OnClientHello(ClientHelloInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public virtual ValueTask<CommitAction> OnSender(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public virtual ValueTask<CommitAction> OnRecipient(SenderRecipientInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public virtual ValueTask<CommitAction> OnHeader(HeaderInformation info, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public virtual ValueTask<CommitAction> OnEndOfHeaders(CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    ValueTask<CommitAction> IMilterCallbacks.OnBodyChunk(ReadOnlySpan<byte> chunk, CancellationToken cancellationToken)
    {
        // NSubstitute cannot mock ReadOnlySpan<T>. This leads to invalid IL code.
        return OnBodyChunk(chunk.ToArray(), cancellationToken);
    }

    public virtual ValueTask<CommitAction> OnBodyChunk(byte[] chunk, CancellationToken cancellationToken)
    {
        return new(CommitActions.Continue);
    }

    public virtual IAsyncEnumerable<Action> OnEndOfBody()
    {
        return new Action[] { CommitActions.Continue }.ToAsyncEnumerable();
    }
}