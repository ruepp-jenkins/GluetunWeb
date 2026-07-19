using System.Formats.Tar;
using System.Text;

namespace GluetunWeb.Api.Docker;

/// <summary>
/// Builds an in-memory tar for the Docker "extract archive to container" (PutArchive) API.
///
/// The archive is flat (file names only) and is extracted *into an existing directory* — which is
/// always a mounted named volume here, so the injected config persists across container recreation.
/// This mirrors `docker cp &lt;file&gt; &lt;container&gt;:/dir/file` semantics exactly.
/// </summary>
public static class TarBuilder
{
    /// <summary>Builds a flat tar containing the given (fileName, content) entries.</summary>
    public static Stream BuildFlat(IEnumerable<(string FileName, string Content)> files)
    {
        var ms = new MemoryStream();
        using (var tar = new TarWriter(ms, TarEntryFormat.Ustar, leaveOpen: true))
        {
            foreach (var (fileName, content) in files)
            {
                var entry = new UstarTarEntry(TarEntryType.RegularFile, fileName)
                {
                    Mode = (UnixFileMode)Convert.ToInt32("644", 8),
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                };
                tar.WriteEntry(entry);
            }
        }

        ms.Position = 0;
        return ms;
    }
}
