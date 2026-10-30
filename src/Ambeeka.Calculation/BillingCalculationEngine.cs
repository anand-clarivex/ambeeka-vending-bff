namespace Ambeeka.Calculation;

/// <summary>Pure calculation for monthly and Sunday-to-Saturday weekly billing periods.</summary>
public sealed class BillingCalculationEngine
{
    public MonthlyBillingResult CalculateMonth(
        int year,
        int month,
        IEnumerable<BillingTransactionInput> transactions,
        IEnumerable<ProductPriceAlias> productPrices,
        IEmployerBillingPolicy employerPolicy,
        BillingCalculationOptions? options = null,
        int rowsWithoutProductName = 0)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(productPrices);
        ArgumentNullException.ThrowIfNull(employerPolicy);
        if (rowsWithoutProductName < 0)
            throw new ArgumentOutOfRangeException(nameof(rowsWithoutProductName));

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var calculationOptions = options ?? new BillingCalculationOptions();
        var aliases = productPrices.ToArray();
        var inMonth = transactions
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductSelectionInfo))
            .Where(item =>
            {
                var date = DateOnly.FromDateTime(item.MachineSettlementTime);
                return date >= monthStart && date <= monthEnd;
            })
            .ToArray();
        var blankProductRows = rowsWithoutProductName + transactions.Count(item =>
            string.IsNullOrWhiteSpace(item.ProductSelectionInfo)
            && DateOnly.FromDateTime(item.MachineSettlementTime) >= monthStart
            && DateOnly.FromDateTime(item.MachineSettlementTime) <= monthEnd);

        var weeks = new List<WeeklyBillingResult>();
        for (var periodStart = monthStart; periodStart <= monthEnd;)
        {
            var sunday = periodStart.AddDays(-(int)periodStart.DayOfWeek);
            var saturday = sunday.AddDays(6);
            var clippedStart = sunday < monthStart ? monthStart : sunday;
            var clippedEnd = saturday > monthEnd ? monthEnd : saturday;
            var rows = inMonth.Where(item =>
            {
                var date = DateOnly.FromDateTime(item.MachineSettlementTime);
                return date >= clippedStart && date <= clippedEnd;
            });

            weeks.Add(CalculateWeek(
                clippedStart,
                clippedEnd,
                sunday,
                rows,
                aliases,
                employerPolicy,
                calculationOptions));
            periodStart = clippedEnd.AddDays(1);
        }

        var totalVends = weeks.Sum(week => week.Totals.TotalVends);
        var totalPrice = weeks.Sum(week => week.Totals.Price);
        var totalFree = weeks.Sum(week => week.Totals.FreeItems);
        var totalNet = weeks.Sum(week => week.Totals.NetPrice);
        var totalSettlement = weeks.Sum(week => week.Totals.EmployeeSettlement);
        var hasUnmatched = weeks.Any(week => week.UnmatchedRows.Count > 0);
        decimal? amountDue = hasUnmatched
            ? null
            : employerPolicy.CalculateAmountDue(totalNet, totalSettlement);

        var unconfirmedRules = GetUnconfirmedRules(employerPolicy, calculationOptions);
        var canFinalize = !hasUnmatched && unconfirmedRules.Count == 0;
        return new MonthlyBillingResult(
            year,
            month,
            weeks,
            blankProductRows,
            new BillingPeriodTotals(
                totalVends,
                totalPrice,
                totalFree,
                totalNet,
                totalSettlement,
                amountDue),
            canFinalize,
            unconfirmedRules);
    }

    private static WeeklyBillingResult CalculateWeek(
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly sunday,
        IEnumerable<BillingTransactionInput> transactions,
        IReadOnlyList<ProductPriceAlias> productPrices,
        IEmployerBillingPolicy employerPolicy,
        BillingCalculationOptions options)
    {
        var rows = new List<CalculatedBillingRow>();
        var unmatched = new List<UnmatchedBillingRow>();
        var excluded = 0;

        foreach (var transaction in transactions)
        {
            var sourceProductName = ExtractAlias(transaction.ProductSelectionInfo!);
            var effectiveDate = options.EffectiveDateBasis switch
            {
                PricingEffectiveDateBasis.WeekPeriodStart => periodStart,
                PricingEffectiveDateBasis.TransactionDate => DateOnly.FromDateTime(transaction.MachineSettlementTime),
                _ => throw new ArgumentOutOfRangeException(nameof(options)),
            };
            var match = ResolvePrice(sourceProductName, effectiveDate, productPrices, options.AliasComparison);
            if (match.Price is null)
            {
                unmatched.Add(new UnmatchedBillingRow(
                    transaction.TransactionId,
                    transaction.MachineSettlementTime,
                    sourceProductName,
                    transaction.SettlementValue,
                    match.Reason,
                    transaction.SourceImportId,
                    transaction.SourceRowNumber));
                continue;
            }

            var price = match.Price.VendPrice;
            var freeItem = employerPolicy.GetFreeItemAmount(transaction.SettlementValue);
            var netPrice = price - freeItem;
            if (netPrice <= 0m)
            {
                excluded++;
                continue;
            }

            rows.Add(new CalculatedBillingRow(
                transaction.TransactionId,
                transaction.MachineSettlementTime,
                sourceProductName,
                match.Price.ProductName,
                price,
                freeItem,
                netPrice,
                transaction.SettlementValue,
                transaction.SettlementValue == 0m,
                transaction.SourceImportId,
                transaction.SourceRowNumber));
        }

        var totalPrice = rows.Sum(row => row.Price);
        var totalFree = rows.Sum(row => row.FreeItem);
        var totalNet = rows.Sum(row => row.NetPrice);
        var totalSettlement = rows.Sum(row => row.Settlement);
        var totalVends = rows.Count + (employerPolicy.CountExcludedRowsAsVends ? excluded : 0);
        decimal? amountDue = unmatched.Count > 0
            ? null
            : employerPolicy.CalculateAmountDue(totalNet, totalSettlement);

        return new WeeklyBillingResult(
            periodStart,
            periodEnd,
            rows,
            unmatched,
            excluded,
            new BillingPeriodTotals(totalVends, totalPrice, totalFree, totalNet, totalSettlement, amountDue));
    }

    private static PriceMatch ResolvePrice(
        string alias,
        DateOnly effectiveDate,
        IReadOnlyList<ProductPriceAlias> prices,
        ProductAliasComparison comparison)
    {
        var comparer = comparison switch
        {
            ProductAliasComparison.Ordinal => StringComparison.Ordinal,
            ProductAliasComparison.OrdinalIgnoreCase => StringComparison.OrdinalIgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
        };
        var matchingAliases = prices
            .Where(item => string.Equals(item.Alias.Trim(), alias, comparer))
            .ToArray();
        if (matchingAliases.Length == 0)
            return new PriceMatch(null, UnmatchedProductReason.AliasNotFound);

        var effectivePrices = matchingAliases
            .Where(item => item.EffectiveFrom <= effectiveDate
                && (item.EffectiveThrough is null || item.EffectiveThrough >= effectiveDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .ToArray();
        if (effectivePrices.Length == 0)
            return new PriceMatch(null, UnmatchedProductReason.NoEffectivePrice);

        var latestDate = effectivePrices[0].EffectiveFrom;
        var latestPrices = effectivePrices.Where(item => item.EffectiveFrom == latestDate).Distinct().ToArray();
        return latestPrices.Length == 1
            ? new PriceMatch(latestPrices[0], default)
            : new PriceMatch(null, UnmatchedProductReason.AmbiguousEffectivePrice);
    }

    private static string ExtractAlias(string productSelectionInfo)
    {
        var parenthesis = productSelectionInfo.IndexOf('(');
        return (parenthesis < 0 ? productSelectionInfo : productSelectionInfo[..parenthesis]).Trim();
    }

    private static IReadOnlyList<string> GetUnconfirmedRules(
        IEmployerBillingPolicy employerPolicy,
        BillingCalculationOptions options)
    {
        var rules = new List<string>();
        if (!employerPolicy.AmountDueFormulaConfirmed)
            rules.Add("amountDueFormula");
        if (!employerPolicy.TotalVendsRuleConfirmed)
            rules.Add("totalVendsDefinition");
        if (!options.AliasComparisonConfirmed)
            rules.Add("aliasMatchingCaseAndWhitespace");
        if (!options.EffectiveDatePolicyConfirmed)
            rules.Add("productEffectiveDateBoundary");
        if (!options.PricingHistoryAvailable)
            rules.Add("historicalPricingVersions");
        if (!options.SettlementTimeZoneConfirmed)
            rules.Add("settlementTimeZone");
        if (!options.CurrencyAndRoundingConfirmed)
            rules.Add("currencyAndRounding");
        return rules;
    }

    private sealed record PriceMatch(ProductPriceAlias? Price, UnmatchedProductReason Reason);
}
