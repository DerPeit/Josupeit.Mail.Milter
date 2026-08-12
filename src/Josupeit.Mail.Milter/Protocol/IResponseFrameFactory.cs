// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public interface IResponseFrameFactory
{
    ResponseFrame CreateResponseFrame(ResponsePacket response);
}