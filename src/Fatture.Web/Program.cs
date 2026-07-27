using Fatture.Web.Options;
using Fatture.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<CompanyIdentityOptions>(builder.Configuration.GetSection(CompanyIdentityOptions.SectionName));
builder.Services.AddSingleton<FatturaPaParser>();
builder.Services.AddSingleton<InvoiceArchiveService>();
builder.Services.AddSingleton<ExcelReportService>();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapPost("/api/fatture/report", async (HttpRequest request, InvoiceArchiveService archive, ExcelReportService excel) =>
{
    if (!request.HasFormContentType) return Results.BadRequest("È richiesto multipart/form-data.");
    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("File ZIP mancante.");
    await using var stream = file.OpenReadStream();
    try
    {
        var report = excel.Create(archive.ParseZip(stream));
        return Results.File(report, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "report-fatture.xlsx");
    }
    catch (InvalidDataException) { return Results.BadRequest("Il file caricato non è uno ZIP valido."); }
}).DisableAntiforgery();
app.Run();

public partial class Program;
