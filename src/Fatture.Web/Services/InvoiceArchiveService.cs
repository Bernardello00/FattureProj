using System.IO.Compression;
using System.Security.Cryptography.Pkcs;
using Fatture.Web.Models;

namespace Fatture.Web.Services;

public sealed class InvoiceArchiveService(FatturaPaParser parser)
{
    public IReadOnlyList<InvoiceRow> ParseZip(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var rows = new List<InvoiceRow>();
        foreach (var entry in archive.Entries.Where(e => e.Length > 0 && (e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase))))
        {
            using var source = entry.Open();
            using var content = new MemoryStream();
            source.CopyTo(content);
            content.Position = 0;
            try
            {
                if (entry.Name.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase))
                {
                    var cms = new SignedCms();
                    cms.Decode(content.ToArray());
                    using var xml = new MemoryStream(cms.ContentInfo.Content);
                    var xmlName = Path.GetFileNameWithoutExtension(entry.Name);
                    rows.AddRange(parser.Parse(xml, xmlName, entry.FullName, entry.Name));
                }
                else rows.AddRange(parser.Parse(content, entry.Name, entry.FullName));
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or System.Security.Cryptography.CryptographicException)
            {
                rows.Add(new InvoiceRow { XmlFileName = entry.Name, P7mFileName = entry.Name.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase) ? entry.Name : "", ZipPath = entry.FullName, Error = ex.Message });
            }
        }
        return rows;
    }
}
