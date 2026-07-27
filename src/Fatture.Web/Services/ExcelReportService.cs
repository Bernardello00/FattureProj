using ClosedXML.Excel;
using Fatture.Web.Models;

namespace Fatture.Web.Services;

public sealed class ExcelReportService
{
    private static readonly string[] InvoiceHeaders = [
        "Tipo fattura", "Data fattura", "Numero fattura", "Identificativo documento", "Tipo documento",
        "Controparte", "Nome fornitore", "Nome cliente", "Partita IVA controparte", "Codice fiscale controparte",
        "Imponibile", "IVA", "Totale fattura", "Totale calcolato", "Valuta", "Nome file XML", "Nome file P7M",
        "Percorso nello ZIP", "Progressivo body", "Esito", "Warning", "Errore", "SegnoContabileSuggerito"];
    private static readonly string[] VatHeaders = ["Identificativo documento", "Data fattura", "Numero fattura", "Tipo fattura", "Controparte", "Aliquota IVA", "Natura IVA", "Imponibile", "Imposta", "Esigibilità IVA", "Riferimento normativo"];

    public byte[] Create(IReadOnlyCollection<InvoiceRow> invoices)
    {
        using var workbook = new XLWorkbook(XLEventTracking.Disabled);
        workbook.Culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        var sheet = workbook.AddWorksheet("Fatture");
        WriteHeaders(sheet, InvoiceHeaders);
        var rowNumber = 2;
        foreach (var invoice in invoices)
        {
            var values = new object?[] { DirectionText(invoice.Direction), invoice.Date?.ToDateTime(TimeOnly.MinValue), invoice.Number, invoice.DocumentId, invoice.DocumentType,
                invoice.Counterparty, invoice.SupplierName, invoice.CustomerName, invoice.CounterpartyVat, invoice.CounterpartyFiscalCode,
                invoice.Taxable, invoice.Tax, invoice.Total, invoice.CalculatedTotal, invoice.Currency, invoice.XmlFileName, invoice.P7mFileName,
                invoice.ZipPath, invoice.BodySequence, invoice.Outcome, invoice.Warning, invoice.Error, invoice.SuggestedAccountingSign };
            for (var column = 1; column <= values.Length; column++) SetValue(sheet.Cell(rowNumber, column), values[column - 1]);
            sheet.Cell(rowNumber, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            var numberFormat = invoice.Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase) ? "[$€-it-IT] #,##0.00" : "#,##0.00";
            sheet.Range(rowNumber, 11, rowNumber, 14).Style.NumberFormat.Format = numberFormat;
            if (!string.IsNullOrEmpty(invoice.Error)) sheet.Range(rowNumber, 1, rowNumber, InvoiceHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightPink;
            else if (!string.IsNullOrEmpty(invoice.Warning)) sheet.Range(rowNumber, 1, rowNumber, InvoiceHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;
            rowNumber++;
        }
        Configure(sheet, InvoiceHeaders.Length, Math.Max(2, rowNumber - 1), "TabellaFatture");

        var vatSheet = workbook.AddWorksheet("Dettaglio IVA");
        WriteHeaders(vatSheet, VatHeaders);
        var vatRow = 2;
        foreach (var detail in invoices.SelectMany(x => x.VatDetails))
        {
            object?[] values = [detail.DocumentId, detail.Date?.ToDateTime(TimeOnly.MinValue), detail.Number, DirectionText(detail.Direction), detail.Counterparty,
                detail.Rate, detail.Nature, detail.Taxable, detail.Tax, detail.TaxDue, detail.LegalReference];
            for (var column = 1; column <= values.Length; column++) SetValue(vatSheet.Cell(vatRow, column), values[column - 1]);
            vatSheet.Cell(vatRow, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            vatSheet.Range(vatRow, 6, vatRow, 9).Style.NumberFormat.Format = "#,##0.00";
            vatRow++;
        }
        Configure(vatSheet, VatHeaders.Length, Math.Max(2, vatRow - 1), "TabellaDettaglioIva");
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static string DirectionText(InvoiceDirection direction) => direction == InvoiceDirection.NonDeterminata ? "Non determinata" : direction.ToString();
    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Clear(); break;
            case string text: cell.Value = text; break;
            case decimal number: cell.Value = number; break;
            case int number: cell.Value = number; break;
            case DateTime date: cell.Value = date; break;
            default: cell.Value = value.ToString(); break;
        }
    }
    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }
    private static void Configure(IXLWorksheet sheet, int columns, int lastRow, string tableName)
    {
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, lastRow, columns).CreateTable(tableName);
        sheet.Columns(1, columns).AdjustToContents();
        foreach (var column in sheet.Columns(1, columns)) if (column.Width > 45) column.Width = 45;
    }
}
