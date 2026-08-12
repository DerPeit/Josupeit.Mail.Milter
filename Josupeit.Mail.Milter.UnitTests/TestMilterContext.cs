namespace Josupeit.Mail.Milter.UnitTests;

/// <summary>
/// A hand-written context that relies entirely on the default callback implementations of
/// <see cref="IMilterCallbacks"/>.
/// </summary>
internal sealed class TestMilterContext : IMilterContext
{
    public static ProtocolRequirements ProtocolRequirements { get; set; } = ProtocolRequirements.All;

    public static ActionRequirements ActionRequirements { get; set; } = ActionRequirements.None;
}
