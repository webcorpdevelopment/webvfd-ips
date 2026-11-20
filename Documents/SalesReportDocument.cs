using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InvoiceScanner.Models;

namespace InvoiceScanner.Documents
{
    public class SalesReportDocument : IDocument
    {
        private readonly SalesReportModel _model;

        public SalesReportDocument(SalesReportModel model)
        {
            _model = model;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
        }

        private void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("INVOICE SCANNER")
                        .Bold()
                        .FontSize(16)
                        .FontColor(Colors.Blue.Medium);

                    column.Item().Text("Sales Report")
                        .SemiBold()
                        .FontSize(12);
                });

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Generated on: ")
                        .SemiBold();
                    text.Span($"{DateTime.Now:dd MMM yyyy HH:mm:ss}")
                        .FontSize(10);
                });
            });

            // Create a new container scope for the border
            container.Column(column =>
            {
                column.Item().Height(5).BorderBottom(1).BorderColor(Colors.Grey.Medium);
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Report title
                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("SALES REPORT").FontSize(16).Bold();
                });

                // Date range
                column.Item().AlignCenter().Text(text =>
                {
                    text.Span($"Period: {_model.StartDate:dd MMM yyyy} to {_model.EndDate:dd MMM yyyy}").FontSize(12);
                });

                // Add spacing
                column.Item().Height(15);

                // Data table
                column.Item().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(70);  // Reference No
                        columns.ConstantColumn(60);  // Z Number
                        columns.RelativeColumn(2);   // Customer
                        columns.RelativeColumn(1);   // TIN
                        columns.RelativeColumn(1);   // Amount Tax(Ex)
                        columns.RelativeColumn(1);   // Tax
                        columns.RelativeColumn(1);   // Amount Tax(Inc)
                        columns.RelativeColumn(1);   // Processed Date
                        columns.RelativeColumn(1);   // Invoice Date
                        columns.RelativeColumn(1);   // Fiscal Code
                    });

                    // Table header
                    table.Header(header =>
                    {
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Reference No").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Z Number").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Customer").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("TIN").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Amount Tax(Ex)").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Tax").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Amount Tax(Inc)").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Processed Date").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Invoice Date").Bold();
                        header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text("Fiscal Code").Bold();
                    });

                    // Table data
                    foreach (var invoice in _model.Invoices)
                    {
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.ReferenceNumber);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.ZNumber);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.CustomerName);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.CustomerID);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text(invoice.TotalAmountTaxEx.ToString("N2"));
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text(invoice.TotalTaxAmount.ToString("N2"));
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text(invoice.TotalAmountTaxInc.ToString("N2"));
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.ProcessedDate);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.invoiceDate);
                        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(invoice.FiscalCode);
                    }

                    // Footer totals row
                    table.Cell().ColumnSpan(4).Border(1).Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text("TOTALS:").Bold();
                    table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(_model.TotalSubtotal.ToString("N2")).Bold();
                    table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(_model.TotalTax.ToString("N2")).Bold();
                    table.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text(_model.TotalAmount.ToString("N2")).Bold();
                    table.Cell().ColumnSpan(3).Border(1).Background(Colors.Grey.Lighten3).Padding(2).Text(string.Empty);
                });

                // Summary section
                column.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(10).Column(summary =>
                {
                    summary.Item().Text("REPORT SUMMARY").Bold().FontSize(12);
                    
                    summary.Item().PaddingTop(5).Grid(grid => 
                    {
                        grid.Columns(12);
                        
                        grid.Item(6).Text("Total Invoices:").Bold();
                        grid.Item(6).AlignRight().Text(_model.TotalCount.ToString());
                        
                        grid.Item(6).Text("Total Amount (Tax Ex):").Bold();
                        grid.Item(6).AlignRight().Text(_model.TotalSubtotal.ToString("N2"));
                        
                        grid.Item(6).Text("Total Tax:").Bold();
                        grid.Item(6).AlignRight().Text(_model.TotalTax.ToString("N2"));
                        
                        grid.Item(6).Text("Total Amount (Tax Inc):").Bold();
                        grid.Item(6).AlignRight().Text(_model.TotalAmount.ToString("N2")).FontColor(Colors.Blue.Medium);
                    });
                });
                
                // Generated info at bottom
                column.Item().AlignRight().PaddingTop(10).Text(text =>
                {
                    text.Span($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}").FontSize(8).Italic();
                });
            });
        }
    }
} 