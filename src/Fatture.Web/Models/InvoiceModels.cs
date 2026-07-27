namespace Fatture.Web.Models;

public enum InvoiceDirection { Attiva, Passiva, NonDeterminata }

public sealed record VatDetail(
    string DocumentId, DateOnly? Date, string Number, InvoiceDirection Direction,
    string Counterparty, decimal? Rate, string Nature, decimal Taxable,
    decimal Tax, string TaxDue, string LegalReference);

public sealed class InvoiceRow
{
    public InvoiceDirection Direction { get; init; }
    public DateOnly? Date { get; init; }
    public string Number { get; init; } = "";
    public string DocumentId { get; init; } = "";
    public string DocumentType { get; init; } = "";
    public string Counterparty { get; init; } = "";
    public string SupplierName { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string CounterpartyVat { get; init; } = "";
    public string CounterpartyFiscalCode { get; init; } = "";
    public decimal Taxable { get; init; }
    public decimal Tax { get; init; }
    public decimal? Total { get; init; }
    public decimal CalculatedTotal => Taxable + Tax;
    public string Currency { get; init; } = "";
    public string XmlFileName { get; init; } = "";
    public string P7mFileName { get; init; } = "";
    public string ZipPath { get; init; } = "";
    public int BodySequence { get; init; }
    public string Outcome => string.IsNullOrEmpty(Error) ? "Elaborato" : "Errore";
    public string Warning { get; init; } = "";
    public string Error { get; init; } = "";
    public int SuggestedAccountingSign => DocumentType == "TD04" ? -1 : 1;
    public List<VatDetail> VatDetails { get; } = [];
}
