// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Josupeit.Mail.Milter;

public enum ProtocolStage
{
    Connect,
    ClientHello,
    Sender,
    Recipient,
    Header,
    EndOfHeaders,
    Body,
    EndOfBody,
}