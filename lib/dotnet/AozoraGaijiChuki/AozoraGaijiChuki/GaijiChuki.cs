using System.Reflection;

namespace Aozora.GaijiChuki;

public static class Manager
{
    public static Xsd.dictionary? Instance { get; private set; }
    public static Xsd.dictionary? LoadContent()
    {
        if (Instance is not null) return Instance;
        using var stream = Assembly.GetAssembly(typeof(Manager))?.GetManifestResourceStream("Aozora.GaijiChuki.Chuki.gz");
        if (stream is null) return null;
        using var gz = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        var xr = new System.Xml.Serialization.XmlSerializer(typeof(Xsd.dictionary));
        try
        {
            return Instance = xr.Deserialize(gz) as Xsd.dictionary;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<Xsd.dictionary?> LoadContentAsync() => await Task.Run(LoadContent).ConfigureAwait(false);
}