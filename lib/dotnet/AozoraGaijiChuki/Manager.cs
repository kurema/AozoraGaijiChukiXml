//using Aozora.GaijiChuki.Xsd;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Aozora.GaijiChuki;

public static class Manager
{
	//中身は普通に書き換え可能。書き換えないでください。
	public static Xsd.dictionary? Instance { get; private set; }

	public static Xsd.dictionary? GetContentOrLoad() => Instance ??= LoadContent();
	public static async Task<Xsd.dictionary?> GetContentOrLoadAsync() => Instance ??= await LoadContentAsync().ConfigureAwait(false);

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
		static ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>? _RadicalCharacters;
		public static ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>? RadicalCharacters
		{
			get
			{
				var result = Instance?.kanji.page.SelectMany(a => a.radical.characters.character.Select(b => (b, a))).GroupBy(a => a.b).ToDictionary(a => a.Key, a => new ReadOnlyMemory<Xsd.page>(a.Select(c => c.a).ToArray()));
				if (result is null) return null;
				return new ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>(new SortedList<string, ReadOnlyMemory<Xsd.page>>(result, StringComparer.Ordinal));
			}
		}

		private static Xsd.page[][]? _RadicalFromStroke;

		public static Xsd.page[][]? RadicalFromStroke
		{
			get
			{
				if (_RadicalFromStroke is not null) return _RadicalFromStroke;
				if (Instance is null) return null;
				var result = new List<Xsd.page>[Instance.toc.strokesToRadical.strokes.Length + 1];
				for (int i = 0; i < result.Length; i++) result[i] = new List<Xsd.page>();
				foreach (var item in Instance.kanji.page)
				{
					foreach (var item3 in Instance.toc.strokesToRadical.strokes)
					{
						if (item.radical.characters.character.Any(item3.Value.Contains))
						{
							result[item3.stroke].Add(item);
						}
					}
				}
				return _RadicalFromStroke = result.Select(a => a.ToArray()).ToArray();
			}
		}

		private static (string, Xsd.page)[]? _RadicalFromReadings;

		public static (string, Xsd.page)[]? RadicalFromReadings
		{
			get
			{
				return _RadicalFromReadings ??= gen();

				static (string, Xsd.page)[]? gen()
				{
					if (Instance is null) return null;
					return Instance.kanji.page.SelectMany(page => page.radical.readings.reading.Select(reading => (reading, page))).OrderBy(a => a.reading).ToArray();
				}
			}
		}

		public static int GetStrokeCount(string radical)
		{
			if (Instance?.toc?.strokesToRadical?.strokes is null) return -1;
			foreach (var item in Instance.toc.strokesToRadical.strokes)
			{
				if (item.Value.Contains(radical)) return item.strokeSpecified ? item.stroke : -1;
			}
			return -1;
		}
	}
}