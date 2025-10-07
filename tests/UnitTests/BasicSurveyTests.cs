using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Hostr.Api.Services;
using Hostr.Api.Models;
using Hostr.Api.Data;

namespace Hostr.Tests.UnitTests;

public class BasicSurveyTests : IDisposable
{
    private readonly HostrDbContext _context;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly Mock<ILogger<SurveyOrchestrationService>> _mockLogger;
    private readonly Mock<IWhatsAppRateLimiter> _mockRateLimiter;
    private readonly SurveyOrchestrationService _service;

    public BasicSurveyTests()
    {
        var options = new DbContextOptionsBuilder<HostrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HostrDbContext(options, null!);
        _mockRatingService = new Mock<IRatingService>();
        _mockLogger = new Mock<ILogger<SurveyOrchestrationService>>();
        _mockRateLimiter = new Mock<IWhatsAppRateLimiter>();

        _service = new SurveyOrchestrationService(
            _context,
            _mockRatingService.Object,
            _mockLogger.Object,
            _mockRateLimiter.Object);
    }

    [Fact]
    public async Task ShouldSendSurveyAsync_WhenBookingIsEligible_ReturnsTrue()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789",
            IsStaff = false,
            SurveyOptOut = false
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ShouldSendSurveyAsync(booking);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendSurveyAsync_WhenGuestOptedOut_ReturnsFalse()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789",
            IsStaff = false,
            SurveyOptOut = true
        };

        // Act
        var result = await _service.ShouldSendSurveyAsync(booking);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSendSurveyAsync_WhenStaffBooking_ReturnsFalse()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789",
            IsStaff = true,
            SurveyOptOut = false
        };

        // Act
        var result = await _service.ShouldSendSurveyAsync(booking);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSendSurveyAsync_WhenNoPhoneNumber_ReturnsFalse()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "",
            IsStaff = false,
            SurveyOptOut = false
        };

        // Act
        var result = await _service.ShouldSendSurveyAsync(booking);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSurveyWithRateLimitAsync_WhenRateLimitAllows_SendsSurvey()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789"
        };

        _mockRateLimiter.Setup(x => x.CanSendMessageAsync())
            .ReturnsAsync(true);

        _mockRatingService.Setup(x => x.SendPostStaySurveyAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendSurveyWithRateLimitAsync(booking);

        // Assert
        _mockRatingService.Verify(x => x.SendPostStaySurveyAsync(1), Times.Once);
    }

    [Fact]
    public async Task SendSurveyWithRateLimitAsync_WhenRateLimitExceeded_SkipsSurvey()
    {
        // Arrange
        var booking = new Booking
        {
            Id = 1,
            TenantId = 1,
            Phone = "+27123456789"
        };

        _mockRateLimiter.Setup(x => x.CanSendMessageAsync())
            .ReturnsAsync(false);

        // Act
        await _service.SendSurveyWithRateLimitAsync(booking);

        // Assert
        _mockRatingService.Verify(x => x.SendPostStaySurveyAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WhatsAppRateLimiter_RespectsMaximumMessages()
    {
        // Arrange
        var rateLimiter = new WhatsAppRateLimiter();

        // Act & Assert
        for (int i = 0; i < 20; i++)
        {
            var result = await rateLimiter.CanSendMessageAsync();
            result.Should().BeTrue();
        }

        // The 21st message should be rate limited
        var limitResult = await rateLimiter.CanSendMessageAsync();
        limitResult.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}