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
namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingDashboardResponse(
    BillingDashboardSummary Summary,
    IReadOnlyList<BillingDashboardWeek> WeeklyChart,
    IReadOnlyList<BillingDashboardRecentImport> RecentImports);

public sealed record BillingDashboardSummary(
    int ImportCount,
    int ImportsNeedingReview,
    int CompletedCalculations,
    int CountedVends,
    int ExcludedTransactionCount);

public sealed record BillingDashboardWeek(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int CountedVends,
    int ExcludedCount);

public sealed record BillingDashboardRecentImport(
    Guid ImportId,
    string FileName,
    string? Employer,
    DateTimeOffset UploadDate,
    DateOnly? PosWeekStart,
    DateOnly? PosWeekEnd,
    int ImportedCount,
    string ImportStatus,
    string CalculationStatus,
    Guid? LatestCalculationId,
    int? CountedVends,
    int? ExcludedCount);
