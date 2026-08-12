// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public interface IRequestParser
{
    void Start();

    Task Stop();
}