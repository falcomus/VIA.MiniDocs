using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace VIA.MiniDocs.Api.Security;

/// <summary>Protects the original keyset position, independently of later document changes.</summary>
internal sealed class DocumentCursor(IDataProtectionProvider protection)
{
  internal sealed record Position(Guid Id, string? Title, DateOnly? Date);
  private readonly IDataProtector protector = protection.CreateProtector("VIA.MiniDocs.Workspace.Documents.Page.v1");

  internal string Create(Guid workspace, string criteria, int limit, string sort, Guid id, string title, DateOnly? date)
  {
    // Compact UTF-8 keeps even a 255-scalar Unicode title within the 2048-character cursor limit.
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
    {
      writer.Write(workspace.ToByteArray());
      writer.Write(criteria);
      writer.Write(limit);
      writer.Write(id.ToByteArray());
      if (sort == "title") writer.Write(title);
      else writer.Write(date?.DayNumber ?? -1);
    }
    return WebEncoders.Base64UrlEncode(protector.Protect(stream.ToArray()));
  }

  internal bool TryRead(HttpRequest request, Guid workspace, string criteria, string sort,
      out int limit, out Position? after)
  {
    limit = 50;
    after = null;
    var size = request.Query["limit"];
    if (size.Count > 1 || (size.Count == 1 &&
        (!int.TryParse(size[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)))
      return false;
    var cursor = request.Query["cursor"];
    if (cursor.Count == 0) return true;
    if (cursor.Count != 1 || cursor[0] is not { Length: > 0 and <= 2048 } value) return false;
    try
    {
      using var stream = new MemoryStream(protector.Unprotect(WebEncoders.Base64UrlDecode(value)));
      using var reader = new BinaryReader(stream, Encoding.UTF8);
      if (new Guid(reader.ReadBytes(16)) != workspace || reader.ReadString() != criteria || reader.ReadInt32() != limit)
        return false;
      var id = new Guid(reader.ReadBytes(16));
      if (id == Guid.Empty) return false;
      string? title = null;
      DateOnly? date = null;
      if (sort == "title") title = reader.ReadString();
      else
      {
        var day = reader.ReadInt32();
        if (day != -1) date = DateOnly.FromDayNumber(day);
      }
      if (stream.Position != stream.Length) return false;
      after = new(id, title, date);
      return true;
    }
    catch (Exception error) when (error is CryptographicException or FormatException or IOException or ArgumentException) { return false; }
  }
}
