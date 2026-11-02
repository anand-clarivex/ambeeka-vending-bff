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
using System.Text.Json;
using Ambeeka.Calculation;
using Microsoft.EntityFrameworkCore;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

public sealed class BillingDashboardService(VendingDbContext dbContext)
{
    private const int RecentImportLimit = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingDashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var imports = await dbContext.BillingImports
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new DashboardImportSource(
                item.Id,
                item.OriginalFileName,
                item.Status,
                item.TransactionCount,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var calculationCandidates = await dbContext.BillingCalculations
            .AsNoTracking()
            .Include(item => item.Imports)
            .Where(item => item.Imports.Count == 1)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        // Only one-import, one-week snapshots belong on this weekly dashboard.
        // The first matching snapshot for each import is its newest weekly snapshot.
        var latestWeeklyByImport = new Dictionary<Guid, DashboardCalculationSnapshot>();
        foreach (var calculation in calculationCandidates)
        {
            var importId = calculation.Imports[0].BillingImportId;
            if (latestWeeklyByImport.ContainsKey(importId))
                continue;

            var result = JsonSerializer.Deserialize<MonthlyBillingResult>(calculation.ResultJson, JsonOptions);
            if (result is null || result.Weeks.Count != 1)
                continue;

            latestWeeklyByImport.Add(importId, new DashboardCalculationSnapshot(
                calculation.Id,
                calculation.EmployerName,
                calculation.Status,
                calculation.CreatedAt,
                result.Weeks[0]));
        }

        var weeklyChart = latestWeeklyByImport.Values
            .Select(item => item.Week)
            .GroupBy(week => new { week.StartDate, week.EndDate })
            .OrderBy(group => group.Key.StartDate)
            .ThenBy(group => group.Key.EndDate)
            .Select(group => new BillingDashboardWeek(
                group.Key.StartDate,
                group.Key.EndDate,
                group.Sum(week => week.Totals.TotalVends),
                group.Sum(week => week.RowsExcludedForNonpositiveNetPrice)))
            .ToList();

        var countedVends = latestWeeklyByImport.Values.Sum(item => item.Week.Totals.TotalVends);
        var excludedCount = latestWeeklyByImport.Values.Sum(item => item.Week.RowsExcludedForNonpositiveNetPrice);
        var importsNeedingReview = imports.Count(item =>
            string.Equals(item.Status, "NeedsReview", StringComparison.OrdinalIgnoreCase)
            && !latestWeeklyByImport.ContainsKey(item.Id));
        var summary = new BillingDashboardSummary(
            imports.Count,
            importsNeedingReview,
            latestWeeklyByImport.Count,
            countedVends,
            excludedCount);

        var recentImports = imports.Take(RecentImportLimit).ToList();
        var recentImportIds = recentImports.Select(item => item.Id).ToArray();
        var employerRows = await dbContext.BillingImportTransactions
            .AsNoTracking()
            .Where(item => recentImportIds.Contains(item.BillingImportId) && item.MachineName != null)
            .OrderBy(item => item.SourceRowNumber)
            .Select(item => new { item.BillingImportId, item.MachineName })
            .ToListAsync(cancellationToken);
        var employerByImport = employerRows
            .GroupBy(item => item.BillingImportId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.MachineName?.Trim())
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)));

        var recentImportItems = recentImports
            .Select(item =>
            {
                latestWeeklyByImport.TryGetValue(item.Id, out var calculation);
                var employer = calculation?.EmployerName;
                if (string.IsNullOrWhiteSpace(employer))
                    employerByImport.TryGetValue(item.Id, out employer);

                return new BillingDashboardRecentImport(
                    item.Id,
                    item.FileName,
                    employer,
                    item.CreatedAt,
                    calculation?.Week.StartDate,
                    calculation?.Week.EndDate,
                    item.TransactionCount,
                    item.Status,
                    calculation?.Status ?? "NotCalculated",
                    calculation?.Id,
                    calculation?.Week.Totals.TotalVends,
                    calculation?.Week.RowsExcludedForNonpositiveNetPrice);
            })
            .ToList();

        return new BillingDashboardResponse(summary, weeklyChart, recentImportItems);
    }

    private sealed record DashboardImportSource(
        Guid Id,
        string FileName,
        string Status,
        int TransactionCount,
        DateTimeOffset CreatedAt);

    private sealed record DashboardCalculationSnapshot(
        Guid Id,
        string EmployerName,
        string Status,
        DateTimeOffset CreatedAt,
        WeeklyBillingResult Week);
}
