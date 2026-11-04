using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Infrastructure.Notifications;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class NotificationsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public NotificationsTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Weekly_Run_Writes_Log_And_Second_Run_Reports_AlreadyRan()
    {
        var c = await AdminClientAsync(_factory);
        var weekStart = WeeklySummaryService.IsoWeekStart(DateOnly.FromDateTime(DateTime.UtcNow));

        var first = await c.PostAsync($"/api/v1/summary/weekly/run?weekStart={weekStart:yyyy-MM-dd}", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1 = (await first.Content.ReadFromJsonAsync<WeeklySummaryRunResult>())!;
        r1.AlreadyRan.Should().BeFalse();
        r1.Recipients.Should().BeGreaterThan(0, "the dev admin has reports.read via admin.full");

        var second = await c.PostAsync($"/api/v1/summary/weekly/run?weekStart={weekStart:yyyy-MM-dd}", null);
        var r2 = (await second.Content.ReadFromJsonAsync<WeeklySummaryRunResult>())!;
        r2.AlreadyRan.Should().BeTrue();
        r2.EmailsSent.Should().Be(0);
    }

    [Fact]
    public async Task Weekly_Run_Creates_InApp_Notification_That_Appears_In_List()
    {
        var c = await AdminClientAsync(_factory);
        // Use a past week we're unlikely to have run against — avoids collision with the first test.
        var weekStart = WeeklySummaryService.IsoWeekStart(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14));
        var r = await c.PostAsync($"/api/v1/summary/weekly/run?weekStart={weekStart:yyyy-MM-dd}", null);
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await c.GetFromJsonAsync<NotificationsController.NotificationsListResponse>("/api/v1/notifications");
        list!.Items.Should().Contain(n => n.Kind == "weekly_summary");
        list.UnreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Mark_Read_And_Mark_All_Read_Flow()
    {
        var c = await AdminClientAsync(_factory);
        // Ensure we have at least one notification by running against a different past week.
        var w = WeeklySummaryService.IsoWeekStart(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-21));
        (await c.PostAsync($"/api/v1/summary/weekly/run?weekStart={w:yyyy-MM-dd}", null)).EnsureSuccessStatusCode();

        var before = await c.GetFromJsonAsync<NotificationsController.NotificationsListResponse>("/api/v1/notifications");
        var target = before!.Items.First(n => n.ReadAtUtc is null);

        (await c.PostAsync($"/api/v1/notifications/{target.Id}/read", null)).EnsureSuccessStatusCode();

        var after = await c.GetFromJsonAsync<NotificationsController.NotificationsListResponse>("/api/v1/notifications");
        after!.Items.First(n => n.Id == target.Id).ReadAtUtc.Should().NotBeNull();
        after.UnreadCount.Should().BeLessThan(before.UnreadCount);

        (await c.PostAsync("/api/v1/notifications/mark-all-read", null)).EnsureSuccessStatusCode();
        var cleared = await c.GetFromJsonAsync<NotificationsController.NotificationsListResponse>("/api/v1/notifications");
        cleared!.UnreadCount.Should().Be(0);
    }
}
