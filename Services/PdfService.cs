using System;
using System.Collections.Generic;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using InvoiceScanner.Models;
using System.Linq;
using Rectangle = iTextSharp.text.Rectangle;

namespace InvoiceScanner.Services
{
    public class PdfService
    {
        public byte[] GenerateSalesReport(SalesReportModel model)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Create document in landscape mode
                var document = new Document(PageSize.A4.Rotate(), 36, 36, 54, 36);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                
                // Add page numbers
                writer.PageEvent = new PageNumberFooter();
                
                document.Open();
                
                // Add header
                AddHeader(document, writer);
                
                // Add title and date range
                AddTitle(document, model.StartDate, model.EndDate);
                
                // Add table with data
                AddInvoicesTable(document, model.Invoices);
                
                // Add summary section
                AddSummary(document, model);
                
                document.Close();
                
                return memoryStream.ToArray();
            }
        }
        
        private void AddHeader(Document document, PdfWriter writer)
        {
            // Create a 2-column table for the header
            PdfPTable headerTable = new PdfPTable(2);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 1f, 1f });
            
            // Company name and report title
            PdfPCell leftCell = new PdfPCell();
            leftCell.Border = Rectangle.NO_BORDER;
            leftCell.AddElement(new Paragraph("INVOICE SCANNER", new Font(Font.FontFamily.HELVETICA, 16, Font.BOLD, BaseColor.BLUE)));
            leftCell.AddElement(new Paragraph("Sales Report", new Font(Font.FontFamily.HELVETICA, 12, Font.NORMAL)));
            headerTable.AddCell(leftCell);
            
            // Generated date on the right
            PdfPCell rightCell = new PdfPCell();
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            rightCell.AddElement(new Paragraph($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}", 
                new Font(Font.FontFamily.HELVETICA, 10, Font.NORMAL)));
            headerTable.AddCell(rightCell);
            
            document.Add(headerTable);
            
            // Add some spacing
            document.Add(new Paragraph(" "));
            
            // Draw a horizontal line directly using PdfContentByte
            PdfContentByte cb = writer.DirectContent;
            cb.SaveState();
            cb.SetLineWidth(1f);
            cb.SetColorStroke(BaseColor.LIGHT_GRAY);
            float x1 = document.LeftMargin;
            float x2 = document.PageSize.Width - document.RightMargin;
            float y = document.GetTop(0) - 50; // Adjust this value as needed
            cb.MoveTo(x1, y);
            cb.LineTo(x2, y);
            cb.Stroke();
            cb.RestoreState();
            
            // Add some spacing
            document.Add(new Paragraph(" "));
        }
        
        private void AddTitle(Document document, DateTime startDate, DateTime endDate)
        {
            // Add report title
            Paragraph title = new Paragraph("SALES REPORT", 
                new Font(Font.FontFamily.HELVETICA, 16, Font.BOLD));
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);
            
            // Add date range subtitle
            Paragraph dateRange = new Paragraph($"Period: {startDate:dd MMM yyyy} to {endDate:dd MMM yyyy}", 
                new Font(Font.FontFamily.HELVETICA, 12, Font.NORMAL));
            dateRange.Alignment = Element.ALIGN_CENTER;
            document.Add(dateRange);
            
            document.Add(new Paragraph(" ")); // Add spacing
        }
        
        private void AddInvoicesTable(Document document, List<InvoiceViewModel> invoices)
        {
            // Create the table with 10 columns
            PdfPTable table = new PdfPTable(10);
            table.WidthPercentage = 100;
            
            // Adjust column widths - reduce TIN and date columns, increase amount columns
            table.SetWidths(new float[] { 
                1.0f,   // Reference No - reduced
                0.8f,   // Z Number
                2f,     // Customer
                1f,     // TIN - reduced
                1.4f,   // Amount (Tax Ex) - increased
                1.2f,   // Tax - increased
                1.4f,   // Amount (Tax Inc) - increased
                1f,     // Processed Date - reduced
                1f,     // Invoice Date - reduced
                1.2f    // Fiscal Code - increased
            });
            
            // Define a header style
            Font headerFont = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD);
            BaseColor headerBgColor = new BaseColor(245, 245, 245);
            
            // Add table headers
            AddHeaderCell(table, "Reference No", headerFont, headerBgColor);
            AddHeaderCell(table, "Z Number", headerFont, headerBgColor);
            AddHeaderCell(table, "Customer", headerFont, headerBgColor);
            AddHeaderCell(table, "TIN", headerFont, headerBgColor);
            AddHeaderCell(table, "Amount (Tax Ex)", headerFont, headerBgColor);
            AddHeaderCell(table, "Tax", headerFont, headerBgColor);
            AddHeaderCell(table, "Amount (Tax Inc)", headerFont, headerBgColor);
            AddHeaderCell(table, "Processed Date", headerFont, headerBgColor);
            AddHeaderCell(table, "Invoice Date", headerFont, headerBgColor);
            AddHeaderCell(table, "Fiscal Code", headerFont, headerBgColor);
            
            // Define cell styles
            Font cellFont = new Font(Font.FontFamily.HELVETICA, 9, Font.NORMAL);
            Font moneyFont = new Font(Font.FontFamily.COURIER, 9, Font.NORMAL);
            BaseColor altRowColor = new BaseColor(248, 249, 250);
            
            // Add data rows
            for (int i = 0; i < invoices.Count; i++)
            {
                var invoice = invoices[i];
                BaseColor rowColor = (i % 2 == 0) ? BaseColor.WHITE : altRowColor;
                
                AddCell(table, invoice.ReferenceNumber, cellFont, rowColor, Element.ALIGN_LEFT);
                AddCell(table, invoice.ZNumber, cellFont, rowColor, Element.ALIGN_LEFT);
                AddCell(table, invoice.CustomerName, cellFont, rowColor, Element.ALIGN_LEFT);
                AddCell(table, invoice.CustomerID, cellFont, rowColor, Element.ALIGN_LEFT);
                AddCell(table, invoice.TotalAmountTaxEx.ToString("N2"), moneyFont, rowColor, Element.ALIGN_RIGHT);
                AddCell(table, invoice.TotalTaxAmount.ToString("N2"), moneyFont, rowColor, Element.ALIGN_RIGHT);
                AddCell(table, invoice.TotalAmountTaxInc.ToString("N2"), moneyFont, rowColor, Element.ALIGN_RIGHT);
                
                // Format dates in a more compact way for better fitting
                AddCell(table, FormatDate(invoice.ProcessedDate), cellFont, rowColor, Element.ALIGN_CENTER);
                AddCell(table, FormatDate(invoice.invoiceDate), cellFont, rowColor, Element.ALIGN_CENTER);
                AddCell(table, invoice.FiscalCode, cellFont, rowColor, Element.ALIGN_LEFT);
            }
            
            // Add totals row
            decimal totalSubtotal = invoices.Sum(i => i.TotalAmountTaxEx);
            decimal totalTax = invoices.Sum(i => i.TotalTaxAmount);
            decimal totalAmount = invoices.Sum(i => i.TotalAmountTaxInc);
            
            // Create a totals row
            PdfPCell totalLabelCell = new PdfPCell(new Phrase("TOTALS:", headerFont));
            totalLabelCell.BackgroundColor = headerBgColor;
            totalLabelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            totalLabelCell.Padding = 5;
            totalLabelCell.Colspan = 4;
            table.AddCell(totalLabelCell);
            
            // Add total amount cells
            Font totalFont = new Font(Font.FontFamily.COURIER, 9, Font.BOLD);
            
            AddCell(table, totalSubtotal.ToString("N2"), totalFont, headerBgColor, Element.ALIGN_RIGHT);
            AddCell(table, totalTax.ToString("N2"), totalFont, headerBgColor, Element.ALIGN_RIGHT);
            AddCell(table, totalAmount.ToString("N2"), totalFont, headerBgColor, Element.ALIGN_RIGHT);
            
            // Empty cells for the remaining columns
            PdfPCell emptyCell = new PdfPCell(new Phrase(""));
            emptyCell.BackgroundColor = headerBgColor;
            emptyCell.Colspan = 3;
            emptyCell.Padding = 5;
            table.AddCell(emptyCell);
            
            document.Add(table);
        }
        
        // Helper method to format dates more compactly
        private string FormatDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return string.Empty;
                
            if (DateTime.TryParse(dateString, out DateTime date))
                return date.ToString("dd/MM/yy");
                
            return dateString;
        }
        
        private void AddSummary(Document document, SalesReportModel model)
        {
            document.Add(new Paragraph(" ")); // Add spacing
            
            // Create a summary table with a blue left border
            PdfPTable summaryTable = new PdfPTable(1);
            summaryTable.WidthPercentage = 100;
            
            PdfPCell summaryCell = new PdfPCell();
            summaryCell.BackgroundColor = new BaseColor(248, 249, 250);
            summaryCell.Padding = 10;
            summaryCell.BorderWidthLeft = 4f;
            summaryCell.BorderColorLeft = new BaseColor(63, 106, 216); // Blue
            summaryCell.BorderWidthTop = 0.5f;
            summaryCell.BorderWidthRight = 0.5f;
            summaryCell.BorderWidthBottom = 0.5f;
            
            // Add content to the summary cell
            Paragraph summaryTitle = new Paragraph("SALES REPORT SUMMARY", 
                new Font(Font.FontFamily.HELVETICA, 12, Font.BOLD, new BaseColor(63, 106, 216)));
            summaryCell.AddElement(summaryTitle);
            
            // Create a 2-column layout for summary items
            PdfPTable summaryItems = new PdfPTable(2);
            summaryItems.WidthPercentage = 100;
            summaryItems.SpacingBefore = 10;
            
            // Total Invoices
            AddSummaryRow(summaryItems, "Total Invoices:", model.TotalCount.ToString());
            
            // Total Amount (Tax Ex)
            AddSummaryRow(summaryItems, "Total Amount (Tax Ex):", model.TotalSubtotal.ToString("N2"));
            
            // Total Tax
            AddSummaryRow(summaryItems, "Total Tax:", model.TotalTax.ToString("N2"));
            
            // Total Amount (Tax Inc)
            Font valueFont = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD, new BaseColor(63, 106, 216));
            AddSummaryRow(summaryItems, "Total Amount (Tax Inc):", model.TotalAmount.ToString("N2"), valueFont);
            
            summaryCell.AddElement(summaryItems);
            summaryTable.AddCell(summaryCell);
            
            document.Add(summaryTable);
            
            // Add generated info at the bottom
            document.Add(new Paragraph(" ")); // Add spacing
            Paragraph generatedInfo = new Paragraph($"Generated on: {DateTime.Now:dd MMM yyyy HH:mm:ss}", 
                new Font(Font.FontFamily.HELVETICA, 8, Font.ITALIC));
            generatedInfo.Alignment = Element.ALIGN_RIGHT;
            document.Add(generatedInfo);
        }
        
        private void AddHeaderCell(PdfPTable table, string text, Font font, BaseColor bgColor)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }
        
        private void AddCell(PdfPTable table, string text, Font font, BaseColor bgColor, int alignment)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }
        
        private void AddSummaryRow(PdfPTable table, string label, string value, Font valueFont = null)
        {
            // Label cell
            Font labelFont = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD);
            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.BOTTOM_BORDER;
            labelCell.BorderColor = new BaseColor(233, 236, 239); // Light gray
            labelCell.BorderWidth = 0.5f;
            labelCell.PaddingTop = 5;
            labelCell.PaddingBottom = 5;
            labelCell.PaddingRight = 10;
            table.AddCell(labelCell);
            
            // Value cell
            if (valueFont == null)
            {
                valueFont = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD);
            }
            
            PdfPCell valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = Rectangle.BOTTOM_BORDER;
            valueCell.BorderColor = new BaseColor(233, 236, 239); // Light gray
            valueCell.BorderWidth = 0.5f;
            valueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            valueCell.PaddingTop = 5;
            valueCell.PaddingBottom = 5;
            table.AddCell(valueCell);
        }
    }
    
    // Custom page event handler for adding page numbers
    class PageNumberFooter : PdfPageEventHelper
    {
        public override void OnEndPage(PdfWriter writer, Document document)
        {
            PdfPTable footerTable = new PdfPTable(1);
            footerTable.TotalWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
            
            // Create the page number cell
            PdfPCell cell = new PdfPCell(new Phrase($"Page {writer.PageNumber} of ___",
                new Font(Font.FontFamily.HELVETICA, 9)));
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Border = Rectangle.NO_BORDER;
            footerTable.AddCell(cell);
            
            // Write the footer table at the bottom of the page
            footerTable.WriteSelectedRows(0, -1, document.LeftMargin, 
                document.BottomMargin, writer.DirectContent);
        }
    }
} 