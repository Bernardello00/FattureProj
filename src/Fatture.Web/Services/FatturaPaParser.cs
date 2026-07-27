using System.Globalization;
using System.Xml.Linq;
using Fatture.Web.Models;
using Fatture.Web.Options;
using Microsoft.Extensions.Options;

namespace Fatture.Web.Services;

public sealed class FatturaPaParser(IOptions<CompanyIdentityOptions> options)
{
    private readonly CompanyIdentityOptions company = options.Value;
    private static XElement? Child(XContainer? node, string name) => node?.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    private static IEnumerable<XElement> Children(XContainer? node, string name) => node?.Elements().Where(x => x.Name.LocalName == name) ?? [];
    private static string Value(XContainer? node, params string[] path)
    {
        XElement? current = node as XElement;
        foreach (var part in path) current = Child(current, part);
        return current?.Value.Trim() ?? "";
    }
    private static decimal? DecimalValue(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;

    public IReadOnlyList<InvoiceRow> Parse(Stream xml, string xmlFileName, string zipPath = "", string p7mFileName = "")
    {
        var document = XDocument.Load(xml, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("Documento XML privo di elemento radice.");
        var header = root.Descendants().FirstOrDefault(x => x.Name.LocalName == "FatturaElettronicaHeader")
            ?? throw new InvalidDataException("FatturaElettronicaHeader non trovato.");
        var supplier = ReadParty(Child(header, "CedentePrestatore"));
        var customer = ReadParty(Child(header, "CessionarioCommittente"));
        var direction = DetermineDirection(supplier, customer);
        var counterparty = direction == InvoiceDirection.Passiva ? supplier : direction == InvoiceDirection.Attiva ? customer : Party.Empty;
        var bodies = root.Descendants().Where(x => x.Name.LocalName == "FatturaElettronicaBody").ToList();
        var rows = new List<InvoiceRow>();

        for (var index = 0; index < bodies.Count; index++)
        {
            var body = bodies[index];
            var general = Child(Child(body, "DatiGenerali"), "DatiGeneraliDocumento");
            var number = Value(general, "Numero");
            var dateText = Value(general, "Data");
            DateOnly? date = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ? parsedDate : null;
            var type = Value(general, "TipoDocumento");
            var summaries = Children(Child(body, "DatiBeniServizi"), "DatiRiepilogo").ToList();
            var taxable = summaries.Sum(x => DecimalValue(Value(x, "ImponibileImporto")) ?? 0m);
            var tax = summaries.Sum(x => DecimalValue(Value(x, "Imposta")) ?? 0m);
            var warnings = new List<string>();
            if (direction == InvoiceDirection.NonDeterminata) warnings.Add("Direzione non determinabile dai dati aziendali configurati.");
            if (string.IsNullOrEmpty(counterparty.Name)) warnings.Add("Nome o denominazione della controparte assente.");
            if (date is null) warnings.Add("Data fattura assente o non valida.");
            var declaredTotal = DecimalValue(Value(general, "ImportoTotaleDocumento"));
            decimal? total = declaredTotal;
            if (total is null)
            {
                var payments = body.Descendants().Where(x => x.Name.LocalName == "ImportoPagamento").Select(x => DecimalValue(x.Value)).Where(x => x.HasValue).Select(x => x!.Value).ToList();
                if (payments.Count > 0) { total = payments.Sum(); warnings.Add("Totale derivato dalla somma degli importi di pagamento."); }
                else warnings.Add("Totale fattura assente: nessun totale dichiarato o dato di pagamento disponibile.");
            }
            var sequence = index + 1;
            var id = $"{supplier.Vat}|{dateText}|{number}|{sequence}";
            var row = new InvoiceRow
            {
                Direction = direction, Date = date, Number = number, DocumentId = id, DocumentType = type,
                Counterparty = counterparty.Name,
                SupplierName = direction == InvoiceDirection.Passiva ? supplier.Name : "",
                CustomerName = direction == InvoiceDirection.Attiva ? customer.Name : "",
                CounterpartyVat = counterparty.DisplayVat, CounterpartyFiscalCode = counterparty.FiscalCode,
                Taxable = taxable, Tax = tax, Total = total, Currency = Value(general, "Divisa"),
                XmlFileName = xmlFileName, P7mFileName = p7mFileName, ZipPath = zipPath,
                BodySequence = sequence, Warning = string.Join(" ", warnings)
            };
            foreach (var summary in summaries)
                row.VatDetails.Add(new VatDetail(id, date, number, direction, counterparty.Name,
                    DecimalValue(Value(summary, "AliquotaIVA")), Value(summary, "Natura"),
                    DecimalValue(Value(summary, "ImponibileImporto")) ?? 0m,
                    DecimalValue(Value(summary, "Imposta")) ?? 0m, Value(summary, "EsigibilitaIVA"),
                    Value(summary, "RiferimentoNormativo")));
            rows.Add(row);
        }
        return rows;
    }

    private InvoiceDirection DetermineDirection(Party supplier, Party customer)
    {
        var configuredVat = IdentityNormalizer.NormalizeVat(company.VatCountryCode, company.VatNumber);
        var configuredFiscalCode = IdentityNormalizer.Normalize(company.FiscalCode);
        bool Matches(Party party) =>
            (configuredVat.Length > 0 && configuredVat == party.Vat) ||
            (configuredFiscalCode.Length > 0 && configuredFiscalCode == IdentityNormalizer.Normalize(party.FiscalCode));
        var supplierMatches = Matches(supplier);
        var customerMatches = Matches(customer);
        if (customerMatches && !supplierMatches) return InvoiceDirection.Passiva;
        if (supplierMatches && !customerMatches) return InvoiceDirection.Attiva;
        return InvoiceDirection.NonDeterminata;
    }

    private static Party ReadParty(XElement? node)
    {
        var data = Child(node, "DatiAnagrafici");
        var registry = Child(data, "Anagrafica");
        var denomination = Value(registry, "Denominazione");
        var fullName = string.Join(" ", new[] { Value(registry, "Nome"), Value(registry, "Cognome") }.Where(x => x.Length > 0));
        var country = Value(data, "IdFiscaleIVA", "IdPaese");
        var code = Value(data, "IdFiscaleIVA", "IdCodice");
        return new Party(denomination.Length > 0 ? denomination : fullName,
            IdentityNormalizer.NormalizeVat(country, code), country + code, Value(data, "CodiceFiscale"));
    }

    private sealed record Party(string Name, string Vat, string DisplayVat, string FiscalCode)
    { public static Party Empty { get; } = new("", "", "", ""); }
}
