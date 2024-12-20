using System.Collections.Generic;

namespace Josupeit.Mail.Milter;

public readonly record struct MacroDefinitionInformation(ProtocolStage ProtocolStage, IReadOnlyDictionary<string, string> Macros);