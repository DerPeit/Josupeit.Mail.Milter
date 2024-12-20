namespace Josupeit.Mail.Milter.Link;

// These values map to the SMFIC_ constants.
public enum CommandCode : byte
{
    Unspecified = default,

    // SMFIC_OPTNEG
    // Option negotiation
    // Expected response: SMFIC_OPTNEG packet
    OptionNegotiation = (byte)'O',

    // SMFIC_MACRO
    // Define macros
    // Expected response: NONE
    Macro = (byte)'D',

    // SMFIC_CONNECT
    // SMTP connection information
    // Expected response: Accept/reject action
    Connect = (byte)'C',

    // SMFIC_HELO
    // HELO/EHLO name
    // Expected response: Accept/reject action
    Helo = (byte)'H',

    // SMFIC_MAIL
    // MAIL FROM: information
    // Expected response: Accept/reject action
    Sender = (byte)'M',

    // SMFIC_RCPT
    // RCPT TO: information
    // Expected response: Accept/reject action
    Recipient = (byte)'R',

    // SMFIC_HEADER
    // Mail header
    // Expected response: Accept/reject action
    Header = (byte)'L',

    // SMFIC_EOH
    // End of headers marker
    // Expected response: Accept/reject action
    EndOfHeaders = (byte)'N',

    // SMFIC_BODY
    // Body chunk
    // Expected response: Accept/reject action
    BodyChunk = (byte)'B',

    // SMFIC_BODYEOB
    // End of body marker
    // Expected response: Zero or more modification actions, then accept/reject action
    EndOfBody = (byte)'E',

   
    // SMFIC_QUIT
    // Quit milter communication
    // Expected response: Close milter connection
    Quit = (byte)'Q',

    // SMFIC_ABORT
    // Abort current filter checks
    // Expected response: NONE
    Abort = (byte)'A',
}