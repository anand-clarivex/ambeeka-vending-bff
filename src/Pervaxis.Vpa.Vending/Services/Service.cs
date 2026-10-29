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

using Pervaxis.Vpa.Vending.Models;

namespace Pervaxis.Vpa.Vending.Services;

public sealed class VendingServiceService : IVendingService
{
    private readonly ILogger<VendingServiceService> _logger;

    public VendingServiceService(ILogger<VendingServiceService> logger)
    {
        _logger = logger;
    }

    public Task<VendingServiceResponse?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("VendingServiceService.GetAsync is not yet implemented.");

        return Task.FromResult<VendingServiceResponse?>(new VendingServiceResponse
        {
            Id = id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
