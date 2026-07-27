using ClosedXML.Excel;
using Fatture.Web.Models;
using Fatture.Web.Services;
using Xunit;

namespace Fatture.Web.Tests;
public sealed class ExcelReportServiceTests
{
 [Fact] public void Crea_fogli_tabelle_e_valori_numerici()
 {
  var invoice = new InvoiceRow { Direction=InvoiceDirection.Passiva, Date=new DateOnly(2026,1,1), Number="1", DocumentId="IT1|2026-01-01|1|1", Currency="EUR", Taxable=100m, Tax=22m, Total=122m, Warning="Test" };
  invoice.VatDetails.Add(new(invoice.DocumentId, invoice.Date, invoice.Number, invoice.Direction, "Fornitore", 22m, "", 100m, 22m, "I", ""));
  using var workbook = new XLWorkbook(new MemoryStream(new ExcelReportService().Create([invoice])));
  Assert.NotNull(workbook.Worksheet("Fatture").Table("TabellaFatture"));
  Assert.Equal(XLDataType.Number, workbook.Worksheet("Fatture").Cell(2,11).DataType);
  Assert.NotNull(workbook.Worksheet("Dettaglio IVA").Table("TabellaDettaglioIva"));
 }
}
