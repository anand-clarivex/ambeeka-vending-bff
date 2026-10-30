using Ambeeka.Calculation;
using Xunit;

namespace Ambeeka.Calculation.Tests;

public sealed class BillingCalculationEngineTests
{
    private readonly BillingCalculationEngine engine = new();

    [Fact]
    public void Zero_settlement_gets_free_item_and_is_highlighted_when_net_is_positive()
    {
        var result = Calculate(
            new BillingTransactionInput("txn-1", new DateTime(2026, 10, 5, 12, 0, 0), "Energy Drink(1)", 0m),
            new ProductPriceAlias("Energy Drink", "Energy Drink 16 oz", 3.00m, new DateOnly(2026, 1, 1)));

        var row = Assert.Single(result.Weeks[1].Rows);
        Assert.Equal(3.00m, row.Price);
        Assert.Equal(2.25m, row.FreeItem);
        Assert.Equal(0.75m, row.NetPrice);
        Assert.True(row.HighlightForReview);
    }

    [Theory]
    [InlineData(1.50)]
    [InlineData(2.25)]
    public void Zero_settlement_rows_with_nonpositive_net_are_excluded(decimal price)
    {
        var result = Calculate(
            new BillingTransactionInput("txn-1", new DateTime(2026, 10, 5), "Snack(1)", 0m),
            new ProductPriceAlias("Snack", "Snack", price, new DateOnly(2026, 1, 1)));

        Assert.Empty(result.Weeks[1].Rows);
        Assert.Equal(1, result.Weeks[1].RowsExcludedForNonpositiveNetPrice);
        Assert.Equal(0, result.Weeks[1].Totals.TotalVends);
    }

    [Fact]
    public void Unmatched_alias_is_reported_and_amount_due_is_not_returned()
    {
        var result = Calculate(
            new BillingTransactionInput("txn-1", new DateTime(2026, 10, 5), "Unknown Name(1)", 1.00m));

        var unmatched = Assert.Single(result.Weeks[1].UnmatchedRows);
        Assert.Equal("Unknown Name", unmatched.SourceProductName);
        Assert.Equal(UnmatchedProductReason.AliasNotFound, unmatched.Reason);
        Assert.Null(result.Weeks[1].Totals.AmountDue);
        Assert.Null(result.Totals.AmountDue);
        Assert.False(result.CanFinalize);
    }

    [Fact]
    public void Unconfirmed_assumptions_keep_an_otherwise_calculated_month_in_preview()
    {
        var result = engine.CalculateMonth(
            2026,
            10,
            [new BillingTransactionInput("txn-1", new DateTime(2026, 10, 5), "Snack(1)", 1.00m)],
            [new ProductPriceAlias("Snack", "Snack", 2.00m, new DateOnly(2026, 1, 1))],
            new ZeroSettlementSubsidyPolicy());

        Assert.Equal(1.00m, result.Totals.AmountDue);
        Assert.False(result.CanFinalize);
        Assert.Contains("amountDueFormula", result.UnconfirmedRules);
        Assert.Contains("historicalPricingVersions", result.UnconfirmedRules);
    }

    [Fact]
    public void Rows_without_a_product_name_are_excluded_and_counted()
    {
        var result = Calculate(
            new BillingTransactionInput("txn-1", new DateTime(2026, 10, 5), "  ", 1.00m),
            new ProductPriceAlias("Snack", "Snack", 2.00m, new DateOnly(2026, 1, 1)));

        Assert.Equal(1, result.RowsWithoutProductName);
        Assert.All(result.Weeks, week => Assert.Empty(week.Rows));
        Assert.Equal(0, result.Totals.TotalVends);
    }

    [Fact]
    public void Month_uses_partial_calendar_weeks_and_month_total_equals_week_totals()
    {
        var rows = new[]
        {
            new BillingTransactionInput("oct-1", new DateTime(2026, 10, 1, 10, 0, 0), "Snack(1)", 1.00m),
            new BillingTransactionInput("oct-4", new DateTime(2026, 10, 4, 10, 0, 0), "Snack(1)", 1.00m),
            new BillingTransactionInput("nov-1", new DateTime(2026, 11, 1, 10, 0, 0), "Snack(1)", 1.00m),
        };

        var result = Calculate(
            rows,
            new ProductPriceAlias("Snack", "Snack", 2.00m, new DateOnly(2026, 1, 1)));

        Assert.Equal(new DateOnly(2026, 10, 1), result.Weeks[0].StartDate);
        Assert.Equal(new DateOnly(2026, 10, 3), result.Weeks[0].EndDate);
        Assert.Equal(new DateOnly(2026, 10, 4), result.Weeks[1].StartDate);
        Assert.Equal(new DateOnly(2026, 10, 10), result.Weeks[1].EndDate);
        Assert.Equal(5, result.Weeks.Count);
        Assert.Equal(result.Weeks.Sum(week => week.Totals.Price), result.Totals.Price);
        Assert.Equal(result.Weeks.Sum(week => week.Totals.AmountDue ?? 0m), result.Totals.AmountDue);
        Assert.Equal(2, result.Totals.TotalVends);
    }

    [Fact]
    public void Pricing_lookup_uses_effective_price_at_partial_week_start()
    {
        var result = Calculate(
            new BillingTransactionInput("txn-1", new DateTime(2026, 10, 1), "Snack(1)", 1.00m),
            new ProductPriceAlias("Snack", "Snack old", 1.50m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
            new ProductPriceAlias("Snack", "Snack new", 2.00m, new DateOnly(2026, 10, 1)));

        var row = Assert.Single(result.Weeks[0].Rows);
        Assert.Equal("Snack new", row.ProductName);
        Assert.Equal(2.00m, row.Price);
    }

    private MonthlyBillingResult Calculate(
        BillingTransactionInput transaction,
        params ProductPriceAlias[] prices) => Calculate([transaction], prices);

    private MonthlyBillingResult Calculate(
        IEnumerable<BillingTransactionInput> transactions,
        params ProductPriceAlias[] prices) => engine.CalculateMonth(
            2026,
            10,
            transactions,
            prices,
            new ZeroSettlementSubsidyPolicy(
                amountDueFormulaConfirmed: true,
                totalVendsRuleConfirmed: true),
            new BillingCalculationOptions
            {
                AliasComparisonConfirmed = true,
                EffectiveDatePolicyConfirmed = true,
                PricingHistoryAvailable = true,
                SettlementTimeZoneConfirmed = true,
                CurrencyAndRoundingConfirmed = true,
            });
}
