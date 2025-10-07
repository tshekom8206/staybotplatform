using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hostr.Api.Services;

public class BusinessImpactReportPdfDocument : IDocument
{
    private readonly BusinessImpactReportData _data;

    public BusinessImpactReportPdfDocument(BusinessImpactReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(15).Column(column =>
        {
            // Company Name & Title Row
            column.Item().BorderBottom(2).BorderColor("#2563eb").PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_data.CompanyName).FontSize(22).Bold().FontColor("#1e40af");
                    col.Item().PaddingTop(2).Text("Business Impact Report").FontSize(16).FontColor("#64748b");
                });
                row.ConstantItem(120).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text($"Report Date").FontSize(8).FontColor("#64748b");
                    col.Item().AlignRight().Text(_data.ReportDate).FontSize(11).Bold().FontColor("#1e40af");
                });
            });

            // Subtitle
            column.Item().PaddingTop(8).Text(text =>
            {
                text.Span("Strategic Performance Analysis | ").FontSize(9).FontColor("#64748b");
                text.Span("Confidential").FontSize(9).Bold().FontColor("#dc2626");
            });
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            // Executive Summary Section
            column.Item().Element(ComposeExecutiveSummary);

            // Hotel Performance KPIs
            column.Item().PageBreak();
            column.Item().Element(ComposeHotelPerformance);

            // Operational Performance
            column.Item().PaddingTop(20).Element(ComposeOperationalPerformance);

            // Guest Satisfaction Trends
            column.Item().PageBreak();
            column.Item().Element(ComposeGuestSatisfaction);

            // Revenue Insights
            column.Item().PaddingTop(20).Element(ComposeRevenueInsights);

            // Upselling ROI Tracking
            if (_data.UpsellingRoi != null)
            {
                column.Item().PageBreak();
                column.Item().Element(ComposeUpsellingRoi);
            }

            // Strategic Recommendations
            column.Item().PageBreak();
            column.Item().Element(ComposeStrategicRecommendations);

            // Immediate Actions Required
            column.Item().PaddingTop(20).Element(ComposeImmediateActions);
        });
    }

    void ComposeExecutiveSummary(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Executive Summary").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            column.Item().PaddingTop(12).Background("#f8fafc").Padding(15).Column(col =>
            {
                col.Item().Row(row =>
                {
                    // Occupancy
                    row.RelativeItem().BorderRight(1).BorderColor("#e2e8f0").PaddingRight(10).Column(c =>
                    {
                        c.Item().Text("Occupancy Rate").FontSize(9).FontColor("#64748b");
                        c.Item().PaddingTop(3).Text($"{_data.HotelPerformance.OccupancyRate:F1}%")
                            .FontSize(20).Bold().FontColor(GetPerformanceColor(_data.HotelPerformance.OccupancyRate, "occupancy"));
                        c.Item().PaddingTop(2).Text($"{_data.HotelPerformance.CurrentOccupiedRooms} / {_data.HotelPerformance.TotalRooms} rooms")
                            .FontSize(8).FontColor("#64748b");
                    });

                    // ADR
                    row.RelativeItem().BorderRight(1).BorderColor("#e2e8f0").PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().Text("Average Daily Rate").FontSize(9).FontColor("#64748b");
                        c.Item().PaddingTop(3).Text($"R{_data.HotelPerformance.AverageDailyRate:F2}")
                            .FontSize(20).Bold().FontColor("#2563eb");
                        c.Item().PaddingTop(2).Text("Per room/night").FontSize(8).FontColor("#64748b");
                    });

                    // Guest Satisfaction
                    row.RelativeItem().BorderRight(1).BorderColor("#e2e8f0").PaddingHorizontal(10).Column(c =>
                    {
                        c.Item().Text("Guest Satisfaction").FontSize(9).FontColor("#64748b");
                        c.Item().PaddingTop(3).Text($"{_data.HotelPerformance.GuestSatisfactionScore:F1}/5.0")
                            .FontSize(20).Bold().FontColor(GetPerformanceColor(_data.HotelPerformance.GuestSatisfactionScore * 20, "satisfaction"));
                        c.Item().PaddingTop(2).Text($"NPS: {_data.HotelPerformance.NetPromoterScore}")
                            .FontSize(8).FontColor("#64748b");
                    });

                    // RevPAR
                    row.RelativeItem().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text("RevPAR").FontSize(9).FontColor("#64748b");
                        c.Item().PaddingTop(3).Text($"R{_data.HotelPerformance.RevPAR:F2}")
                            .FontSize(20).Bold().FontColor("#2563eb");
                        c.Item().PaddingTop(2).Text("Revenue per available room").FontSize(8).FontColor("#64748b");
                    });
                });
            });

            // Key Insights
            if (_data.HotelPerformance.TrendIndicators != null)
            {
                column.Item().PaddingTop(15).Column(col =>
                {
                    col.Item().Text("Key Performance Indicators").FontSize(12).Bold().FontColor("#1e40af");
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Padding(8).Background("#fef3c7").Column(c =>
                        {
                            c.Item().Text("Occupancy Trend").FontSize(8).Bold().FontColor("#92400e");
                            c.Item().Text(_data.HotelPerformance.TrendIndicators.OccupancyTrend?.ToUpper() ?? "STABLE")
                                .FontSize(10).Bold().FontColor("#92400e");
                        });

                        row.ConstantItem(8);

                        row.RelativeItem().Padding(8).Background("#dbeafe").Column(c =>
                        {
                            c.Item().Text("Satisfaction Trend").FontSize(8).Bold().FontColor("#1e40af");
                            c.Item().Text(_data.HotelPerformance.TrendIndicators.SatisfactionTrend?.ToUpper() ?? "STABLE")
                                .FontSize(10).Bold().FontColor("#1e40af");
                        });

                        row.ConstantItem(8);

                        row.RelativeItem().Padding(8).Background("#d1fae5").Column(c =>
                        {
                            c.Item().Text("Revenue Trend").FontSize(8).Bold().FontColor("#065f46");
                            c.Item().Text(_data.HotelPerformance.TrendIndicators.RevenueTrend?.ToUpper() ?? "STABLE")
                                .FontSize(10).Bold().FontColor("#065f46");
                        });
                    });
                });
            }
        });
    }

    void ComposeHotelPerformance(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Hotel Performance Metrics").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            column.Item().PaddingTop(12).Column(col =>
            {
                col.Item().Text("Detailed Performance Analysis").FontSize(11).Bold();
                col.Item().PaddingTop(8).PaddingLeft(15).Column(c =>
                {
                    c.Item().Text(text =>
                    {
                        text.Span("• Repeat Guest Percentage: ").FontSize(10).FontColor("#64748b");
                        text.Span($"{_data.HotelPerformance.RepeatGuestPercentage:F1}%").FontSize(10).Bold();
                    });
                    c.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span("• Current Occupied Rooms: ").FontSize(10).FontColor("#64748b");
                        text.Span($"{_data.HotelPerformance.CurrentOccupiedRooms} of {_data.HotelPerformance.TotalRooms}").FontSize(10).Bold();
                    });
                });
            });

            if (_data.HotelPerformance.ComparisonToPreviousPeriod != null)
            {
                column.Item().PaddingTop(15).Background("#f1f5f9").Padding(12).Column(col =>
                {
                    col.Item().Text("Period-over-Period Comparison").FontSize(11).Bold();
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Occupancy Change").FontSize(9).FontColor("#64748b");
                            c.Item().Text($"{_data.HotelPerformance.ComparisonToPreviousPeriod.OccupancyChange:+0.0;-0.0}%")
                                .FontSize(14).Bold()
                                .FontColor(_data.HotelPerformance.ComparisonToPreviousPeriod.OccupancyChange >= 0 ? "#16a34a" : "#dc2626");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("ADR Change").FontSize(9).FontColor("#64748b");
                            c.Item().Text($"{_data.HotelPerformance.ComparisonToPreviousPeriod.AdrChange:+0.0;-0.0}%")
                                .FontSize(14).Bold()
                                .FontColor(_data.HotelPerformance.ComparisonToPreviousPeriod.AdrChange >= 0 ? "#16a34a" : "#dc2626");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Satisfaction Change").FontSize(9).FontColor("#64748b");
                            c.Item().Text($"{_data.HotelPerformance.ComparisonToPreviousPeriod.GssChange:+0.0;-0.0}")
                                .FontSize(14).Bold()
                                .FontColor(_data.HotelPerformance.ComparisonToPreviousPeriod.GssChange >= 0 ? "#16a34a" : "#dc2626");
                        });
                    });
                });
            }
        });
    }

    void ComposeOperationalPerformance(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Operational Performance").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            column.Item().PaddingTop(12).Column(col =>
            {
                // Overall Metrics
                col.Item().Background("#f8fafc").Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Overall Completion Rate").FontSize(9).FontColor("#64748b");
                        c.Item().Text($"{_data.OperationalPerformance.OverallCompletionRate:F1}%")
                            .FontSize(16).Bold().FontColor("#2563eb");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Avg Response Time").FontSize(9).FontColor("#64748b");
                        c.Item().Text($"{_data.OperationalPerformance.AverageResponseTime:F1} min")
                            .FontSize(16).Bold().FontColor("#2563eb");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Total Tasks (30 days)").FontSize(9).FontColor("#64748b");
                        c.Item().Text($"{_data.OperationalPerformance.TotalTasks}")
                            .FontSize(16).Bold().FontColor("#2563eb");
                    });
                });

                // Department Breakdown
                if (_data.OperationalPerformance.Departments?.Any() == true)
                {
                    col.Item().PaddingTop(15).Text("Department Performance").FontSize(11).Bold();

                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background("#e2e8f0").Padding(6).Text("Department").FontSize(9).Bold();
                            header.Cell().Background("#e2e8f0").Padding(6).Text("Completion Rate").FontSize(9).Bold();
                            header.Cell().Background("#e2e8f0").Padding(6).Text("Avg Time (min)").FontSize(9).Bold();
                            header.Cell().Background("#e2e8f0").Padding(6).Text("Tasks Count").FontSize(9).Bold();
                        });

                        // Rows
                        foreach (var dept in _data.OperationalPerformance.Departments.Take(10))
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text(dept.Name).FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text($"{dept.CompletionRate:F1}%").FontSize(9)
                                .FontColor(GetPerformanceColor(dept.CompletionRate, "completion"));
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text($"{dept.AverageTime:F0}").FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text($"{dept.TasksCount}").FontSize(9);
                        }
                    });
                }
            });
        });
    }

    void ComposeGuestSatisfaction(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Guest Satisfaction Analysis").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            column.Item().PaddingTop(12).Column(col =>
            {
                // NPS Breakdown
                if (_data.SatisfactionTrends.NpsBreakdown != null)
                {
                    col.Item().Background("#f8fafc").Padding(12).Column(c =>
                    {
                        c.Item().Text("Net Promoter Score Breakdown").FontSize(11).Bold();
                        c.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Padding(8).Background("#dcfce7").Column(r =>
                            {
                                r.Item().Text("Promoters (9-10)").FontSize(8).FontColor("#166534");
                                r.Item().Text($"{_data.SatisfactionTrends.NpsBreakdown.Promoters}").FontSize(16).Bold().FontColor("#166534");
                                r.Item().Text($"{(_data.SatisfactionTrends.NpsBreakdown.Total > 0 ? (decimal)_data.SatisfactionTrends.NpsBreakdown.Promoters / _data.SatisfactionTrends.NpsBreakdown.Total * 100 : 0):F1}%")
                                    .FontSize(9).FontColor("#166534");
                            });

                            row.ConstantItem(8);

                            row.RelativeItem().Padding(8).Background("#fef9c3").Column(r =>
                            {
                                r.Item().Text("Passives (7-8)").FontSize(8).FontColor("#713f12");
                                r.Item().Text($"{_data.SatisfactionTrends.NpsBreakdown.Passives}").FontSize(16).Bold().FontColor("#713f12");
                                r.Item().Text($"{(_data.SatisfactionTrends.NpsBreakdown.Total > 0 ? (decimal)_data.SatisfactionTrends.NpsBreakdown.Passives / _data.SatisfactionTrends.NpsBreakdown.Total * 100 : 0):F1}%")
                                    .FontSize(9).FontColor("#713f12");
                            });

                            row.ConstantItem(8);

                            row.RelativeItem().Padding(8).Background("#fee2e2").Column(r =>
                            {
                                r.Item().Text("Detractors (0-6)").FontSize(8).FontColor("#991b1b");
                                r.Item().Text($"{_data.SatisfactionTrends.NpsBreakdown.Detractors}").FontSize(16).Bold().FontColor("#991b1b");
                                r.Item().Text($"{(_data.SatisfactionTrends.NpsBreakdown.Total > 0 ? (decimal)_data.SatisfactionTrends.NpsBreakdown.Detractors / _data.SatisfactionTrends.NpsBreakdown.Total * 100 : 0):F1}%")
                                    .FontSize(9).FontColor("#991b1b");
                            });
                        });
                    });
                }

                // Critical Alerts
                if (_data.SatisfactionTrends.CriticalAlerts?.Any() == true)
                {
                    col.Item().PaddingTop(15).Column(c =>
                    {
                        c.Item().Text("Critical Guest Alerts").FontSize(11).Bold();
                        c.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span($"{_data.SatisfactionTrends.CriticalAlerts.Count} ")
                                .FontSize(10).Bold().FontColor("#dc2626");
                            text.Span("guests require immediate follow-up to prevent negative reviews and improve satisfaction.")
                                .FontSize(10).FontColor("#64748b");
                        });

                        c.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background("#fee2e2").Padding(6).Text("Guest").FontSize(9).Bold();
                                header.Cell().Background("#fee2e2").Padding(6).Text("Rating").FontSize(9).Bold();
                                header.Cell().Background("#fee2e2").Padding(6).Text("Comment").FontSize(9).Bold();
                                header.Cell().Background("#fee2e2").Padding(6).Text("Priority").FontSize(9).Bold();
                            });

                            // Rows (limit to first 5)
                            foreach (var alert in _data.SatisfactionTrends.CriticalAlerts.Take(5))
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(alert.GuestPhone ?? "N/A").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"{alert.Rating}/5").FontSize(8).FontColor("#dc2626").Bold();
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(alert.Comment?.Length > 50 ? alert.Comment.Substring(0, 47) + "..." : alert.Comment ?? "No comment")
                                    .FontSize(8).FontColor("#64748b");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(alert.Priority ?? "medium").FontSize(8)
                                    .FontColor(alert.Priority?.ToLower() == "high" ? "#dc2626" : "#f59e0b");
                            }
                        });
                    });
                }
            });
        });
    }

    void ComposeRevenueInsights(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Revenue Insights & Opportunities").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            column.Item().PaddingTop(12).Column(col =>
            {
                // ROI Insights
                if (_data.RevenueInsights.RoiInsights != null)
                {
                    col.Item().Background("#f0fdf4").Padding(15).Column(c =>
                    {
                        c.Item().Text("Revenue Improvement Potential").FontSize(13).Bold().FontColor("#166534");
                        c.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Column(r =>
                            {
                                r.Item().Text("Potential Additional Revenue").FontSize(9).FontColor("#166534");
                                r.Item().Text($"R{_data.RevenueInsights.RoiInsights.ImprovementPotential:N2}")
                                    .FontSize(18).Bold().FontColor("#15803d");
                                r.Item().PaddingTop(3).Text("By improving guest satisfaction from Fair/Poor to Good+ (4.0+)")
                                    .FontSize(8).FontColor("#64748b");
                            });

                            row.RelativeItem().Column(r =>
                            {
                                r.Item().Text("ROI from Satisfaction Investment").FontSize(9).FontColor("#166534");
                                r.Item().Text($"{_data.RevenueInsights.RoiInsights.RoiPercentage:F1}%")
                                    .FontSize(18).Bold().FontColor("#15803d");
                                r.Item().PaddingTop(3).Text("Estimated return on guest experience improvements")
                                    .FontSize(8).FontColor("#64748b");
                            });
                        });
                    });
                }

                // Revenue by Satisfaction Segment
                if (_data.RevenueInsights.SatisfactionRevenueCorrelation?.Segments?.Any() == true)
                {
                    col.Item().PaddingTop(15).Column(c =>
                    {
                        c.Item().Text("Revenue by Guest Satisfaction Level").FontSize(11).Bold();

                        c.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Satisfaction Level").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Guests").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Avg ADR").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Total Revenue").FontSize(9).Bold();
                            });

                            // Rows
                            foreach (var segment in _data.RevenueInsights.SatisfactionRevenueCorrelation.Segments)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(segment.SatisfactionLevel).FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"{segment.GuestCount}").FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"R{segment.AverageSpend:F2}").FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"R{segment.TotalRevenue:F2}").FontSize(9).Bold();
                            }
                        });
                    });
                }
            });
        });
    }

    void ComposeUpsellingRoi(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Upselling ROI Tracking").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");
            column.Item().PaddingTop(6).Text("Revenue generated through intelligent service recommendations")
                .FontSize(10).FontColor("#64748b");

            column.Item().PaddingTop(12).Column(col =>
            {
                // Overview Metrics
                col.Item().Background("#f0fdf4").Padding(15).Column(c =>
                {
                    c.Item().Text("Chatbot Revenue Generation").FontSize(13).Bold().FontColor("#166534");
                    c.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(r =>
                        {
                            r.Item().Text("Total Upsell Revenue").FontSize(9).FontColor("#166534");
                            r.Item().Text($"R{_data.UpsellingRoi.TotalRevenue:N2}")
                                .FontSize(20).Bold().FontColor("#15803d");
                            r.Item().PaddingTop(2).Text("Chatbot-generated revenue")
                                .FontSize(8).FontColor("#64748b");
                        });

                        row.RelativeItem().Column(r =>
                        {
                            r.Item().Text("Total Suggestions").FontSize(9).FontColor("#166534");
                            r.Item().Text($"{_data.UpsellingRoi.TotalSuggestions}")
                                .FontSize(20).Bold().FontColor("#15803d");
                            r.Item().PaddingTop(2).Text("Upsells offered to guests")
                                .FontSize(8).FontColor("#64748b");
                        });

                        row.RelativeItem().Column(r =>
                        {
                            r.Item().Text("Conversions").FontSize(9).FontColor("#166534");
                            r.Item().Text($"{_data.UpsellingRoi.TotalConversions}")
                                .FontSize(20).Bold().FontColor("#15803d");
                            r.Item().PaddingTop(2).Text("Accepted by guests")
                                .FontSize(8).FontColor("#64748b");
                        });

                        row.RelativeItem().Column(r =>
                        {
                            r.Item().Text("Conversion Rate").FontSize(9).FontColor("#166534");
                            r.Item().Text($"{_data.UpsellingRoi.ConversionRate:F1}%")
                                .FontSize(20).Bold().FontColor("#15803d");
                            r.Item().PaddingTop(2).Text("Acceptance rate")
                                .FontSize(8).FontColor("#64748b");
                        });
                    });
                });

                // ROI Demonstration Box
                col.Item().PaddingTop(15).Background("#dbeafe").BorderLeft(3).BorderColor("#2563eb").Padding(12).Column(c =>
                {
                    c.Item().Text(text =>
                    {
                        text.Span("The chatbot has generated ").FontSize(10).FontColor("#475569");
                        text.Span($"R{_data.UpsellingRoi.TotalRevenue:N2}").FontSize(10).Bold().FontColor("#1e40af");
                        text.Span(" in additional revenue through intelligent upselling, demonstrating clear ROI and helping pay for itself through increased service bookings.")
                            .FontSize(10).FontColor("#475569");
                    });
                });

                // Top Upsell Services Table
                if (_data.UpsellingRoi.TopServices?.Any() == true)
                {
                    col.Item().PaddingTop(15).Column(c =>
                    {
                        c.Item().Text("Top Upsell Services").FontSize(11).Bold();
                        c.Item().PaddingTop(4).Text("Services generating the most revenue through upselling")
                            .FontSize(9).FontColor("#64748b");

                        c.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Service").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Category").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Offered").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Accepted").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Rate").FontSize(9).Bold();
                                header.Cell().Background("#e2e8f0").Padding(6).Text("Revenue").FontSize(9).Bold();
                            });

                            // Rows
                            foreach (var service in _data.UpsellingRoi.TopServices.Take(10))
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(service.ServiceName).FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text(service.Category ?? "N/A").FontSize(9).FontColor("#64748b");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"{service.Suggestions}").FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"{service.Conversions}").FontSize(9);
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"{service.ConversionRate:F1}%").FontSize(9)
                                    .FontColor(service.ConversionRate >= 50 ? "#16a34a" : "#64748b");
                                table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                    .Text($"R{service.Revenue:N2}").FontSize(9).Bold().FontColor("#15803d");
                            }
                        });
                    });
                }
            });
        });
    }

    void ComposeStrategicRecommendations(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Strategic Recommendations").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            if (_data.StrategicRecommendations?.Any() == true)
            {
                column.Item().PaddingTop(12).Column(col =>
                {
                    foreach (var rec in _data.StrategicRecommendations.Take(6))
                    {
                        var bgColor = rec.Priority?.ToLower() == "critical" ? "#fef2f2" :
                                      rec.Priority?.ToLower() == "high" ? "#fef3c7" : "#f0f9ff";
                        var borderColor = rec.Priority?.ToLower() == "critical" ? "#dc2626" :
                                          rec.Priority?.ToLower() == "high" ? "#f59e0b" : "#2563eb";

                        col.Item().PaddingBottom(12).Background(bgColor)
                            .BorderLeft(3).BorderColor(borderColor).Padding(12).Column(c =>
                        {
                            c.Item().Row(row =>
                            {
                                row.RelativeItem().Text(rec.Title).FontSize(11).Bold().FontColor("#1e293b");
                                row.ConstantItem(60).AlignRight().Background(borderColor).Padding(4).Text(rec.Priority?.ToUpper() ?? "MEDIUM")
                                    .FontSize(7).Bold().FontColor("#ffffff");
                            });

                            c.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span("Category: ").FontSize(8).FontColor("#64748b");
                                text.Span(rec.Category).FontSize(8).Bold();
                                text.Span(" | Impact: ").FontSize(8).FontColor("#64748b");
                                text.Span(rec.Impact).FontSize(8).Bold();
                                text.Span(" | Timeframe: ").FontSize(8).FontColor("#64748b");
                                text.Span(rec.Timeframe).FontSize(8).Bold();
                            });

                            c.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span("Insight: ").FontSize(9).Bold();
                                text.Span(rec.Insight).FontSize(9).FontColor("#475569");
                            });

                            c.Item().PaddingTop(4).Text(text =>
                            {
                                text.Span("Action: ").FontSize(9).Bold();
                                text.Span(rec.Action).FontSize(9).FontColor("#475569");
                            });
                        });
                    }
                });
            }
            else
            {
                column.Item().PaddingTop(12).Background("#f8fafc").Padding(15).Text("No strategic recommendations available at this time.")
                    .FontSize(10).FontColor("#64748b").Italic();
            }
        });
    }

    void ComposeImmediateActions(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Immediate Actions Required").FontSize(18).Bold().FontColor("#1e40af");
            column.Item().PaddingTop(3).BorderBottom(1).BorderColor("#e2e8f0");

            if (_data.ImmediateActions?.Any() == true)
            {
                column.Item().PaddingTop(12).Column(col =>
                {
                    col.Item().Background("#fef2f2").Padding(10).Text(text =>
                    {
                        text.Span($"{_data.ImmediateActions.Count(a => a.Priority?.ToLower() == "critical")} critical ")
                            .FontSize(10).Bold().FontColor("#dc2626");
                        text.Span("and ")
                            .FontSize(10);
                        text.Span($"{_data.ImmediateActions.Count(a => a.Priority?.ToLower() == "high")} high priority ")
                            .FontSize(10).Bold().FontColor("#f59e0b");
                        text.Span("issues require immediate attention.")
                            .FontSize(10);
                    });

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(2);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background("#fee2e2").Padding(6).Text("Priority").FontSize(9).Bold();
                            header.Cell().Background("#fee2e2").Padding(6).Text("Action Required").FontSize(9).Bold();
                            header.Cell().Background("#fee2e2").Padding(6).Text("Department").FontSize(9).Bold();
                        });

                        // Rows (limit to first 10)
                        foreach (var action in _data.ImmediateActions.Take(10))
                        {
                            var priorityColor = action.Priority?.ToLower() == "critical" ? "#dc2626" :
                                                action.Priority?.ToLower() == "high" ? "#f59e0b" : "#64748b";

                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text(action.Priority?.ToUpper() ?? "MEDIUM").FontSize(8).Bold().FontColor(priorityColor);
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text(action.Issue).FontSize(9).FontColor("#1e293b");
                            table.Cell().BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(6)
                                .Text(action.Department ?? "N/A").FontSize(9).FontColor("#64748b");
                        }
                    });
                });
            }
            else
            {
                column.Item().PaddingTop(12).Background("#f0fdf4").Padding(15).Column(col =>
                {
                    col.Item().Text("✓ No immediate actions required").FontSize(11).Bold().FontColor("#166534");
                    col.Item().PaddingTop(4).Text("All critical and high-priority issues have been addressed. Continue monitoring performance metrics.")
                        .FontSize(9).FontColor("#64748b");
                });
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.AlignCenter().BorderTop(1).BorderColor("#e2e8f0").PaddingTop(8).Column(column =>
        {
            column.Item().AlignCenter().Text(text =>
            {
                text.Span($"{_data.CompanyName} | ").FontSize(8).FontColor("#64748b");
                text.Span(_data.Website ?? "").FontSize(8).FontColor("#2563eb");
                text.Span($" | {_data.Phone} | ").FontSize(8).FontColor("#64748b");
                text.Span("Confidential").FontSize(8).Bold().FontColor("#dc2626");
            });
            column.Item().PaddingTop(3).AlignCenter().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor("#94a3b8");
                text.CurrentPageNumber().FontSize(8).FontColor("#94a3b8");
                text.Span(" of ").FontSize(8).FontColor("#94a3b8");
                text.TotalPages().FontSize(8).FontColor("#94a3b8");
            });
        });
    }

    string GetPerformanceColor(decimal value, string type)
    {
        return type switch
        {
            "occupancy" => value >= 90 ? "#16a34a" : value >= 70 ? "#f59e0b" : "#dc2626",
            "satisfaction" => value >= 90 ? "#16a34a" : value >= 80 ? "#f59e0b" : "#dc2626",
            "completion" => value >= 90 ? "#16a34a" : value >= 70 ? "#f59e0b" : "#dc2626",
            _ => "#2563eb"
        };
    }
}

// Data models for the PDF report
public class BusinessImpactReportData
{
    public string CompanyName { get; set; } = string.Empty;
    public string ReportDate { get; set; } = DateTime.Now.ToString("MMMM dd, yyyy");
    public string? Website { get; set; }
    public string? Phone { get; set; }

    public HotelPerformanceData HotelPerformance { get; set; } = new();
    public OperationalPerformanceData OperationalPerformance { get; set; } = new();
    public SatisfactionTrendsData SatisfactionTrends { get; set; } = new();
    public RevenueInsightsData RevenueInsights { get; set; } = new();
    public UpsellingRoiData? UpsellingRoi { get; set; }
    public List<StrategicRecommendation> StrategicRecommendations { get; set; } = new();
    public List<ImmediateAction> ImmediateActions { get; set; } = new();
}

public class HotelPerformanceData
{
    public decimal OccupancyRate { get; set; }
    public int CurrentOccupiedRooms { get; set; }
    public int TotalRooms { get; set; }
    public decimal AverageDailyRate { get; set; }
    public decimal RevPAR { get; set; }
    public decimal GuestSatisfactionScore { get; set; }
    public int NetPromoterScore { get; set; }
    public decimal RepeatGuestPercentage { get; set; }
    public TrendIndicators? TrendIndicators { get; set; }
    public PeriodComparison? ComparisonToPreviousPeriod { get; set; }
}

public class TrendIndicators
{
    public string? OccupancyTrend { get; set; }
    public string? SatisfactionTrend { get; set; }
    public string? RevenueTrend { get; set; }
}

public class PeriodComparison
{
    public decimal OccupancyChange { get; set; }
    public decimal AdrChange { get; set; }
    public decimal GssChange { get; set; }
}

public class OperationalPerformanceData
{
    public decimal OverallCompletionRate { get; set; }
    public decimal AverageResponseTime { get; set; }
    public int TotalTasks { get; set; }
    public List<DepartmentPerformance> Departments { get; set; } = new();
}

public class DepartmentPerformance
{
    public string Name { get; set; } = string.Empty;
    public decimal CompletionRate { get; set; }
    public decimal AverageTime { get; set; }
    public int TasksCount { get; set; }
}

public class SatisfactionTrendsData
{
    public NpsBreakdown? NpsBreakdown { get; set; }
    public List<CriticalAlert> CriticalAlerts { get; set; } = new();
}

public class NpsBreakdown
{
    public int Promoters { get; set; }
    public int Passives { get; set; }
    public int Detractors { get; set; }
    public int Total => Promoters + Passives + Detractors;
}

public class CriticalAlert
{
    public string? GuestPhone { get; set; }
    public decimal Rating { get; set; }
    public string? Comment { get; set; }
    public string? Priority { get; set; }
}

public class RevenueInsightsData
{
    public RoiInsights? RoiInsights { get; set; }
    public SatisfactionRevenueCorrelation? SatisfactionRevenueCorrelation { get; set; }
}

public class RoiInsights
{
    public decimal ImprovementPotential { get; set; }
    public decimal RoiPercentage { get; set; }
}

public class SatisfactionRevenueCorrelation
{
    public List<RevenueSegment> Segments { get; set; } = new();
}

public class RevenueSegment
{
    public string SatisfactionLevel { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public decimal AverageSpend { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class StrategicRecommendation
{
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Insight { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Impact { get; set; }
    public string? Timeframe { get; set; }
}

public class ImmediateAction
{
    public string? Priority { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string? Department { get; set; }
}

public class UpsellingRoiData
{
    public int TotalSuggestions { get; set; }
    public int TotalConversions { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<TopUpsellService> TopServices { get; set; } = new();
}

public class TopUpsellService
{
    public string ServiceName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Suggestions { get; set; }
    public int Conversions { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal Revenue { get; set; }
}
