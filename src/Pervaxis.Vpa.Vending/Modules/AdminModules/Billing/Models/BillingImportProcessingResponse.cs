/*
 ************************************************************************
 * Copyright (C) 2026 Clarivex Technologies Private Limited
 * All Rights Reserved.
 ************************************************************************
 */
namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportProcessingResponse(
    BillingImportHistoryItem Import,
    IReadOnlyList<BillingImportTransactionRow> ImportedRows,
    IReadOnlyList<BillingImportIgnoredRow>? IgnoredRows,
    BillingImportProductReviewResponse PricingReview,
    BillingCalculationResponse? LatestCalculation,
    IReadOnlyList<BillingImportCalculationSnapshotItem> Calculations);

public sealed record BillingImportTransactionRow(
    int SourceRowNumber,
    string TransactionId,
    string? SiteId,
    string? Currency,
    string? MachineName,
    DateTime MachineSettlementTime,
    string? ProductSelectionInfo,
    decimal SettlementValue);

public sealed record BillingImportCalculationSnapshotsResponse(
    BillingCalculationResponse? LatestCalculation,
    IReadOnlyList<BillingImportCalculationSnapshotItem> Items);

public sealed record BillingImportCalculationSnapshotItem(
    Guid Id,
    string CalculationName,
    string Status,
    DateTimeOffset CreatedAt);
