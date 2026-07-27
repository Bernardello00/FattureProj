using System.Text;
using Fatture.Web.Models;
using Fatture.Web.Options;
using Fatture.Web.Services;
using Xunit;

namespace Fatture.Web.Tests;

public sealed class FatturaPaParserTests
{
    [Fact] public void Riconosce_passiva_e_denominazione() { var row = Parse(VatNumber: "IT12345678901")[0]; Assert.Equal(InvoiceDirection.Passiva, row.Direction); Assert.Equal("Fornitore Spa", row.SupplierName); Assert.Equal("", row.CustomerName); }
    [Fact] public void Riconosce_attiva() { var row = Parse(VatNumber: "11111111111")[0]; Assert.Equal(InvoiceDirection.Attiva, row.Direction); Assert.Equal("Cliente Srl", row.CustomerName); }
    [Theory] [InlineData("12345678901")] [InlineData("IT 123-456-78901")]
    public void Normalizza_prefisso_partita_iva(string configured) => Assert.Equal(InvoiceDirection.Passiva, Parse(VatNumber: configured)[0].Direction);
    [Fact] public void Riconosce_tramite_codice_fiscale() => Assert.Equal(InvoiceDirection.Passiva, Parse(VatNumber: "", FiscalCode: "RSS-MRA 80A01H501U")[0].Direction);
    [Fact] public void Somma_piu_aliquote() { var row = Parse(Summaries: Summary("22.00", "100", "22") + Summary("10.00", "50", "5"))[0]; Assert.Equal(150m, row.Taxable); Assert.Equal(27m, row.Tax); Assert.Equal(2, row.VatDetails.Count); }
    [Fact] public void Conserva_esenzione_e_natura() { var row = Parse(Summaries: Summary("0.00", "40", "0", "N4"))[0]; Assert.Equal("N4", row.VatDetails[0].Nature); Assert.Equal(0m, row.Tax); }
    [Fact] public void Gestisce_iva_zero() => Assert.Equal(0m, Parse(Summaries: Summary("0.00", "20", "0"))[0].Tax);
    [Fact] public void Nota_credito_non_cambia_segno() { var row = Parse(Type: "TD04", Total: "122.00")[0]; Assert.Equal(122m, row.Total); Assert.Equal(-1, row.SuggestedAccountingSign); }
    [Theory] [InlineData("<DatiRitenuta><ImportoRitenuta>20</ImportoRitenuta></DatiRitenuta>")] [InlineData("<DatiBollo><BolloVirtuale>SI</BolloVirtuale><ImportoBollo>2</ImportoBollo></DatiBollo>")] [InlineData("<DatiCassaPrevidenziale><ImportoContributoCassa>4</ImportoContributoCassa></DatiCassaPrevidenziale>")]
    public void Totale_dichiarato_non_viene_ricalcolato_per_elementi_accessori(string extra) { var row = Parse(GeneralExtra: extra, Total: "125.00")[0]; Assert.Equal(125m, row.Total); Assert.Equal(122m, row.CalculatedTotal); }
    [Fact] public void Usa_pagamenti_se_totale_assente() { var row = Parse(Total: null, BodyExtra: "<DatiPagamento><DettaglioPagamento><ImportoPagamento>121.50</ImportoPagamento></DettaglioPagamento></DatiPagamento>")[0]; Assert.Equal(121.5m, row.Total); Assert.Contains("pagamento", row.Warning); }
    [Fact] public void Estrae_body_multipli() { var rows = Parse(SecondBody: true); Assert.Equal(2, rows.Count); Assert.Equal(2, rows[1].BodySequence); Assert.Equal("INV-2", rows[1].Number); }
    [Fact] public void Usa_nome_e_cognome() { var row = Parse(SupplierRegistry: "<Nome>Mario</Nome><Cognome>Rossi</Cognome>")[0]; Assert.Equal("Mario Rossi", row.Counterparty); }
    [Fact] public void Conserva_valuta_non_eur() => Assert.Equal("USD", Parse(Currency: "USD")[0].Currency);
    [Fact] public void Non_determinata_genera_warning() { var row = Parse(VatNumber: "99999999999")[0]; Assert.Equal(InvoiceDirection.NonDeterminata, row.Direction); Assert.NotEmpty(row.Warning); }
    [Fact] public void Distingue_totale_dichiarato_e_calcolato() { var row = Parse(Total: "119.50")[0]; Assert.Equal(100m, row.Taxable); Assert.Equal(22m, row.Tax); Assert.Equal(119.5m, row.Total); Assert.Equal(122m, row.CalculatedTotal); }
    [Fact] public void Namespace_prefissato_non_influenza_mapping() => Assert.Single(Parse(Prefixed: true));

    private static string Summary(string rate, string taxable, string tax, string nature = "") => $"<DatiRiepilogo><AliquotaIVA>{rate}</AliquotaIVA>{(nature.Length > 0 ? $"<Natura>{nature}</Natura>" : "")}<ImponibileImporto>{taxable}</ImponibileImporto><Imposta>{tax}</Imposta><EsigibilitaIVA>I</EsigibilitaIVA><RiferimentoNormativo>Test</RiferimentoNormativo></DatiRiepilogo>";

    private static IReadOnlyList<InvoiceRow> Parse(string VatNumber = "12345678901", string FiscalCode = "", string? Total = "122.00", string Type = "TD01", string Currency = "EUR", string? Summaries = null, string GeneralExtra = "", string BodyExtra = "", bool SecondBody = false, string SupplierRegistry = "<Denominazione>Fornitore Spa</Denominazione>", bool Prefixed = false)
    {
        Summaries ??= Summary("22.00", "100", "22");
        var ns = Prefixed ? "p:" : "";
        var xmlns = Prefixed ? " xmlns:p=\"urn:fatturapa:test\"" : " xmlns=\"urn:fatturapa:test\"";
        string Body(string number) => $"<{ns}FatturaElettronicaBody><{ns}DatiGenerali><{ns}DatiGeneraliDocumento><{ns}TipoDocumento>{Type}</{ns}TipoDocumento><{ns}Divisa>{Currency}</{ns}Divisa><{ns}Data>2026-01-10</{ns}Data><{ns}Numero>{number}</{ns}Numero>{(Total is null ? "" : $"<{ns}ImportoTotaleDocumento>{Total}</{ns}ImportoTotaleDocumento>")}{GeneralExtra}</{ns}DatiGeneraliDocumento></{ns}DatiGenerali><{ns}DatiBeniServizi>{Summaries}</{ns}DatiBeniServizi>{BodyExtra}</{ns}FatturaElettronicaBody>";
        var xml = $"<{ns}FatturaElettronica{xmlns}><{ns}FatturaElettronicaHeader><{ns}CedentePrestatore><{ns}DatiAnagrafici><{ns}IdFiscaleIVA><{ns}IdPaese>IT</{ns}IdPaese><{ns}IdCodice>11111111111</{ns}IdCodice></{ns}IdFiscaleIVA><{ns}Anagrafica>{SupplierRegistry}</{ns}Anagrafica></{ns}DatiAnagrafici></{ns}CedentePrestatore><{ns}CessionarioCommittente><{ns}DatiAnagrafici><{ns}IdFiscaleIVA><{ns}IdPaese>IT</{ns}IdPaese><{ns}IdCodice>12345678901</{ns}IdCodice></{ns}IdFiscaleIVA><{ns}CodiceFiscale>RSSMRA80A01H501U</{ns}CodiceFiscale><{ns}Anagrafica><{ns}Denominazione>Cliente Srl</{ns}Denominazione></{ns}Anagrafica></{ns}DatiAnagrafici></{ns}CessionarioCommittente></{ns}FatturaElettronicaHeader>{Body("INV-1")}{(SecondBody ? Body("INV-2") : "")}</{ns}FatturaElettronica>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var parser = new FatturaPaParser(Microsoft.Extensions.Options.Options.Create(
            new CompanyIdentityOptions { VatCountryCode = "IT", VatNumber = VatNumber, FiscalCode = FiscalCode }));
        return parser.Parse(stream, "fattura.xml", "cartella/fattura.xml");
    }
}
