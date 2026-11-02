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
using System.Text.Json.Serialization;

namespace Ambeeka.Calculation;

/// <summary>A single sanitized POS transaction row needed by the billing calculation.</summary>
public sealed record BillingTransactionInput(
    string TransactionId,
    DateTime MachineSettlementTime,
    string? ProductSelectionInfo,
    decimal SettlementValue,
    Guid? SourceImportId = null,
    int? SourceRowNumber = null,
    ProductPriceAlias? PriceOverride = null);

/// <summary>A product alias and price that is valid for a date range.</summary>
public sealed record ProductPriceAlias(
    string Alias,
    string ProductName,
    decimal VendPrice,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveThrough = null);

public enum ProductAliasComparison
{
    Ordinal,
    OrdinalIgnoreCase,
}

public enum PricingEffectiveDateBasis
{
    WeekPeriodStart,
    TransactionDate,
}

public sealed record BillingCalculationOptions
{
    public ProductAliasComparison AliasComparison { get; init; } = ProductAliasComparison.OrdinalIgnoreCase;

    // TODO: Confirm case-insensitive matching, the effective-date boundary, timezone, and currency/rounding rules with the client.
    public PricingEffectiveDateBasis EffectiveDateBasis { get; init; } = PricingEffectiveDateBasis.WeekPeriodStart;

    public bool AliasComparisonConfirmed { get; init; }

    public bool EffectiveDatePolicyConfirmed { get; init; }

    public bool PricingHistoryAvailable { get; init; }

    public bool SettlementTimeZoneConfirmed { get; init; }

    public bool CurrencyAndRoundingConfirmed { get; init; }
}

/// <summary>Employer-specific calculations are supplied to the engine; the engine does not hardcode employer names.</summary>
public interface IEmployerBillingPolicy
{
    decimal GetFreeItemAmount(decimal settlementValue);

    decimal CalculateAmountDue(decimal netPriceTotal, decimal employeeSettlementTotal);

    bool AmountDueFormulaConfirmed { get; }

    bool CountExcludedRowsAsVends { get; }

    bool TotalVendsRuleConfirmed { get; }
}

/// <summary>
/// Preview policy for the supplied Warren Valve workbook. The amount-due formula and total-vend rule remain
/// explicitly unconfirmed until the client approves them.
/// </summary>
public sealed class ZeroSettlementSubsidyPolicy(
    decimal freeItemAmount = 2.25m,
    bool amountDueFormulaConfirmed = false,
    bool countExcludedRowsAsVends = false,
    bool totalVendsRuleConfirmed = false) : IEmployerBillingPolicy
{
    public bool AmountDueFormulaConfirmed { get; } = amountDueFormulaConfirmed;

    public bool CountExcludedRowsAsVends { get; } = countExcludedRowsAsVends;

    public bool TotalVendsRuleConfirmed { get; } = totalVendsRuleConfirmed;

    public decimal GetFreeItemAmount(decimal settlementValue) => settlementValue == 0m ? freeItemAmount : 0m;

    // TODO: Client confirmation is pending. The supplied Warren workbook currently indicates Net Price minus Settlement.
    public decimal CalculateAmountDue(decimal netPriceTotal, decimal employeeSettlementTotal) =>
        netPriceTotal - employeeSettlementTotal;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnmatchedProductReason
{
    AliasNotFound,
    NoEffectivePrice,
    AmbiguousEffectivePrice,
}

public sealed record CalculatedBillingRow(
    string TransactionId,
    DateTime MachineSettlementTime,
    string SourceProductName,
    string ProductName,
    decimal Price,
    decimal FreeItem,
    decimal NetPrice,
    decimal Settlement,
    bool HighlightForReview,
    Guid? SourceImportId = null,
    int? SourceRowNumber = null);

public sealed record UnmatchedBillingRow(
    string TransactionId,
    DateTime MachineSettlementTime,
    string SourceProductName,
    decimal Settlement,
    UnmatchedProductReason Reason,
    Guid? SourceImportId = null,
    int? SourceRowNumber = null);

public sealed record BillingPeriodTotals(
    int TotalVends,
    decimal Price,
    decimal FreeItems,
    decimal NetPrice,
    decimal EmployeeSettlement,
    decimal? AmountDue);

public sealed record WeeklyBillingResult(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<CalculatedBillingRow> Rows,
    IReadOnlyList<UnmatchedBillingRow> UnmatchedRows,
    int RowsExcludedForNonpositiveNetPrice,
    BillingPeriodTotals Totals)
{
    /// <summary>
    /// Transactions excluded from this week's calculated rows, or null for snapshots created before exclusion details were stored.
    /// </summary>
    public IReadOnlyList<ExcludedBillingRow>? ExcludedRows { get; init; }
}

/// <summary>Details for one transaction excluded from calculated billing rows.</summary>
public sealed record ExcludedBillingRow(
    string TransactionId,
    DateTime MachineSettlementTime,
    string SourceProductName,
    decimal Price,
    decimal FreeItem,
    decimal NetPrice,
    decimal Settlement,
    BillingRowExclusionReason Reason,
    Guid? SourceImportId = null,
    int? SourceRowNumber = null);

/// <summary>Reason a transaction was omitted from calculated billing rows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingRowExclusionReason
{
    /// <summary>The price after the free-item adjustment was zero or less.</summary>
    NonpositiveNetPrice,
}

public sealed record MonthlyBillingResult(
    int Year,
    int Month,
    IReadOnlyList<WeeklyBillingResult> Weeks,
    int RowsWithoutProductName,
    BillingPeriodTotals Totals,
    bool CanFinalize,
    IReadOnlyList<string> UnconfirmedRules)
{
    public IReadOnlyList<AppliedBillingResolution> AppliedResolutions { get; init; } = [];

    public IReadOnlyList<IgnoredBillingTransaction> IgnoredTransactions { get; init; } = [];
}

public sealed record AppliedBillingResolution(
    Guid ImportId,
    int SourceRowNumber,
    string TransactionId,
    string Action,
    Guid? ProductId,
    string? ProductName,
    string? OriginalProductSelectionInfo);

public sealed record IgnoredBillingTransaction(
    Guid ImportId,
    int SourceRowNumber,
    string TransactionId,
    DateTime MachineSettlementTime,
    decimal SettlementValue,
    string? ProductSelectionInfo);

