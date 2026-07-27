namespace Fatture.Web.Options;

public sealed class CompanyIdentityOptions
{
    public const string SectionName = "CompanyIdentity";
    public string VatCountryCode { get; set; } = "IT";
    public string VatNumber { get; set; } = "";
    public string FiscalCode { get; set; } = "";
}
