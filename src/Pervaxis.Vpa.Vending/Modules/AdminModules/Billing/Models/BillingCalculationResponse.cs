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
using Ambeeka.Calculation;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingCalculationResponse(
    Guid Id,
    string CalculationName,
    string EmployerName,
    string Status,
    DateTimeOffset CreatedAt,
    MonthlyBillingResult Result)
{
    /// <summary>The source import when this snapshot is scoped to exactly one POS workbook.</summary>
    public Guid? ImportId { get; init; }
}
