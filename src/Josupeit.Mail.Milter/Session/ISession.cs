// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session;

public interface ISession
{
    Task Run(CancellationToken cancellationToken);
}