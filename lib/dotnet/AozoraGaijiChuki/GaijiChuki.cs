//using Aozora.GaijiChuki.Xsd;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Aozora.GaijiChuki;

public static class Manager
{
    //中身は普通に書き換え可能。書き換えないでください。
    public static Xsd.dictionary? Instance { get; private set; }
    public static Xsd.dictionary? LoadContent()
    {
        //if (Instance is not null) return Instance;
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

    public static class Toc
    {
        //ところでfieldキーワードはC# 12に延長。
        //https://github.com/dotnet/csharplang/issues/140#issuecomment-1209645505
        static ReadOnlyDictionary<string, Xsd.page>? _Radicals;
        public static ReadOnlyDictionary<string, Xsd.page>? Radicals 
            => _Radicals ??= Instance?.kanji.page.SelectMany(a => a.radical.characters.character.Select(b => (b, a))).ToDictionary(a => a.b, a => a.a).AsReadOnly();
    }
}