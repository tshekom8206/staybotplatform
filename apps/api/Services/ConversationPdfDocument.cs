using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hostr.Api.Services;

public class ConversationPdfDocument : IDocument
{
    private readonly Models.Conversation _conversation;
    private readonly string _guestName;
    private readonly string _roomNumber;
    private readonly string _agentName;

    public ConversationPdfDocument(Models.Conversation conversation, string guestName, string roomNumber, string agentName)
    {
        _conversation = conversation;
        _guestName = guestName;
        _roomNumber = roomNumber;
        _agentName = agentName;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    void ComposeHeader(IContainer container)
    {
        container.BorderBottom(1).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Conversation Report").FontSize(20).Bold();
                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor("#666666");
            });
            row.ConstantItem(100).AlignRight().Text($"ID: {_conversation.Id}").FontSize(9).FontColor("#666666");
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(20).Column(column =>
        {
            // Guest Information Section
            column.Item().PaddingBottom(15).Row(row =>
            {
                row.RelativeItem().Background("#f5f5f5").Padding(10).Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Guest Information").FontSize(14).Bold();
                    });
                    col.Item().Text($"Name: {_guestName}");
                    col.Item().Text($"Phone: {_conversation.WaUserPhone}");
                    col.Item().Text($"Room: {_roomNumber}");
                });
            });

            // Conversation Details Section
            column.Item().PaddingBottom(15).Row(row =>
            {
                row.RelativeItem().Background("#f5f5f5").Padding(10).Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Conversation Details").FontSize(14).Bold();
                    });
                    col.Item().Text($"Started: {_conversation.CreatedAt:yyyy-MM-dd HH:mm}");
                    col.Item().Text($"Status: {_conversation.Status}");
                    col.Item().Text($"Handled by: {_agentName}");
                    col.Item().Text($"Total Messages: {_conversation.Messages.Count}");
                });
            });

            // Messages Section
            column.Item().PaddingBottom(10).Text(text =>
            {
                text.Span("Messages").FontSize(16).Bold();
            });

            foreach (var message in _conversation.Messages.OrderBy(m => m.CreatedAt))
            {
                var isInbound = message.Direction == "Inbound";
                var sender = isInbound ? _guestName : _agentName;
                var bgColor = isInbound ? "#e3f2fd" : "#f5f5f5";

                column.Item().PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Background(bgColor).Padding(10).Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(text =>
                            {
                                text.Span(sender).Bold();
                            });
                            r.ConstantItem(80).AlignRight().Text(text =>
                            {
                                text.Span(message.CreatedAt.ToString("HH:mm")).FontSize(9).FontColor("#666666");
                            });
                        });
                        col.Item().PaddingTop(5).Text(message.Body);
                    });
                });
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).Padding(10).AlignCenter().Text(x =>
        {
            x.Span("Page ").FontSize(9);
            x.CurrentPageNumber().FontSize(9);
            x.Span(" of ").FontSize(9);
            x.TotalPages().FontSize(9);
        });
    }
}
