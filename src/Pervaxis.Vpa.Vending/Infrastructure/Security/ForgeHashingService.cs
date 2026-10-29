/*
 ************************************************************************
 * Copyright (C) 2026 Clarivex Technologies Private Limited
 * All Rights Reserved.
 *
 * NOTICE: All intellectual and technical concepts contained
 * herein are proprietary to Clarivex Technologies Private Limited
 * and may be covered by Indian and Foreign Patents,
 * patents in process, and are protected by trade secret or
 * copyright law. Dissemination of this information or reproduction
 * of this material is strictly forbidden unless prior written
 * permission is obtained from Clarivex Technologies Private Limited.
 *
 * Product:   Pervaxis Platform
 * Website:   https://clarivex.tech
 ************************************************************************
 */

namespace Pervaxis.Vpa.Vending.Infrastructure.Security;

public sealed class ForgeHashingService : IForgeHashingService
{
    private const int WorkFactor = 12;

    public string HashContent(string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return BCrypt.Net.BCrypt.HashPassword(input, workFactor: WorkFactor);
    }

    public bool VerifyContent(string input, string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return BCrypt.Net.BCrypt.Verify(input, hash);
    }
}
