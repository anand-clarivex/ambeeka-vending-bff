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

namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeResilienceOptions
{
    public const string SectionName = "Resilience";

    public bool Enabled { get; set; } = true;

    public int RetryCount { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 1000;

    public int MaxRetryAttempts { get; set; } = 3;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public int CircuitBreakerSamplingWindowSeconds { get; set; } = 30;

    public int CircuitBreakerBreakDurationSeconds { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 30;
}
