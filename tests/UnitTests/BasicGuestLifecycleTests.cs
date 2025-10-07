using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Hostr.Api.Services;
using Hostr.Api.Models;
using Hostr.Api.Data;

namespace Hostr.Tests.UnitTests;

public class BasicGuestLifecycleTests : IDisposable
{
    private readonly HostrDbContext _context;
    private readonly Mock<ILogger<GuestLifecycleService>> _mockLogger;
    private readonly GuestLifecycleService _service;

    public BasicGuestLifecycleTests()
    {
        var options = new DbContextOptionsBuilder<HostrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HostrDbContext(options, null!);
        _mockLogger = new Mock<ILogger<GuestLifecycleService>>();
        _service = new GuestLifecycleService(_context, _mockLogger.Object);
    }

    [Fact]
    public async Task UpdateGuestMetricsAsync_WhenNewGuest_CreatesMetricsRecord()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = 1,
            Name = "Test Hotel"
        };

        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789",
            TotalRevenue = 1500.00m
        };

        _context.Tenants.Add(tenant);
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateGuestMetricsAsync("+27123456789");

        // Assert
        var metrics = await _context.GuestBusinessMetrics
            .FirstOrDefaultAsync(m => m.PhoneNumber == "+27123456789");

        metrics.Should().NotBeNull();
        metrics!.TenantId.Should().Be(1);
        metrics.PhoneNumber.Should().Be("+27123456789");
        metrics.TotalStays.Should().Be(1);
        metrics.LifetimeValue.Should().Be(1500.00m);
    }

    [Fact]
    public async Task UpdateGuestMetricsAsync_WhenExistingGuest_UpdatesMetricsRecord()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = 1,
            Name = "Test Hotel"
        };

        var existingMetrics = new GuestBusinessMetrics
        {
            TenantId = 1,
            PhoneNumber = "+27123456789",
            TotalStays = 1,
            LifetimeValue = 1000.00m
        };

        var booking1 = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789",
            TotalRevenue = 1000.00m
        };

        var booking2 = new Booking
        {
            Id = 2,
            TenantId = 1,
            Phone = "+27123456789",
            TotalRevenue = 1500.00m
        };

        _context.Tenants.Add(tenant);
        _context.GuestBusinessMetrics.Add(existingMetrics);
        _context.Bookings.AddRange(booking1, booking2);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateGuestMetricsAsync("+27123456789");

        // Assert
        var updatedMetrics = await _context.GuestBusinessMetrics
            .FirstOrDefaultAsync(m => m.PhoneNumber == "+27123456789");

        updatedMetrics.Should().NotBeNull();
        updatedMetrics!.TotalStays.Should().Be(2);
        updatedMetrics.LifetimeValue.Should().Be(2500.00m);
    }

    [Fact]
    public async Task GetGuestMetricsAsync_WhenMetricsExist_ReturnsMetrics()
    {
        // Arrange
        var metrics = new GuestBusinessMetrics
        {
            TenantId = 1,
            PhoneNumber = "+27123456789",
            TotalStays = 2,
            LifetimeValue = 3000.00m
        };

        _context.GuestBusinessMetrics.Add(metrics);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetGuestMetricsAsync("+27123456789");

        // Assert
        result.Should().NotBeNull();
        result!.PhoneNumber.Should().Be("+27123456789");
        result.TotalStays.Should().Be(2);
        result.LifetimeValue.Should().Be(3000.00m);
    }

    [Fact]
    public async Task GetGuestMetricsAsync_WhenMetricsDoNotExist_ReturnsNull()
    {
        // Act
        var result = await _service.GetGuestMetricsAsync("+27123456789");

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}