﻿//using Aozora.GaijiChuki.Xsd;
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

	public static Func<int, int, int, string?>? Jisx0213Provider { get; set; } = (_, _, _) => null;
	public static Func<string, (int men, int ku, int ten)>? Jisx0213ReverseProvider { get; set; } = _ => (-1, -1, -1);


	static Xsd.dictionary? LoadContent()
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

	static async Task<Xsd.dictionary?> LoadContentAsync() => await Task.Run(LoadContent).ConfigureAwait(false);

	public static class Toc
	{
		public static IEnumerable<(Xsd.entry entry, Xsd.page page)> AllKanjiEntries => Instance?.kanji?.page?.SelectMany(a => a.entries.Select(b => (b, a))) ?? new (Xsd.entry entry, Xsd.page page)[0];
		public static IEnumerable<(Xsd.PageOtherEntry entry, Xsd.PageOther page)> AllOtherEntreies => Instance?.other?.PageOther?.SelectMany(a => a.entries.Select(b => (b, a))) ?? new (Xsd.PageOtherEntry entry, Xsd.PageOther page)[0];

		//ところでfieldキーワードはC# 12に延長。
		//https://github.com/dotnet/csharplang/issues/140#issuecomment-1209645505
		static ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>? _RadicalCharacters;
		public static ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>? RadicalCharacters
		{
			get
			{
				if (_RadicalCharacters is not null) return _RadicalCharacters;
				var result = Instance?.kanji.page.SelectMany(a => a.radical.characters.character.Select(b => (b, a))).GroupBy(a => a.b).ToDictionary(a => a.Key, a => new ReadOnlyMemory<Xsd.page>(a.Select(c => c.a).ToArray()));
				if (result is null) return null;
				return _RadicalCharacters = new ReadOnlyDictionary<string, ReadOnlyMemory<Xsd.page>>(new SortedList<string, ReadOnlyMemory<Xsd.page>>(result, StringComparer.Ordinal));
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

		static ReadOnlyDictionary<int, ReadOnlyMemory<(Xsd.entry, Xsd.page)>>? _StrokeCharacters;

		public static ReadOnlyDictionary<int, ReadOnlyMemory<(Xsd.entry entry, Xsd.page page)>>? StrokeCharacters
		{
			get
			{
				if (_StrokeCharacters is not null) return _StrokeCharacters;
				if (Instance is null) return null;
				var dic = new Dictionary<int, List<(Xsd.entry, Xsd.page)>>();
				foreach (var p in Instance.kanji.page)
				{
					var strokes = p.radical.characters.character.Select(GetStrokeCount).Where(a => a >= 0).GroupBy(a => a).Select(a => a.First()).ToArray();
					foreach (var c in p.entries)
					{
						if (!int.TryParse(c.strokes, out int stroke)) continue;
						foreach (var s in strokes)
						{
							var ss = s + stroke;
							if (dic.TryGetValue(ss, out var l)) l.Add((c, p));
							else dic.Add(ss, new List<(Xsd.entry, Xsd.page)> { (c, p) });
						}
					}
				}
				return _StrokeCharacters = dic.ToDictionary(a => a.Key, a => new ReadOnlyMemory<(Xsd.entry, Xsd.page)>((a.Value.ToArray()))).AsReadOnly();
			}
		}
	}

	public static class Tools
	{
		public static string? GetStrokesText(Xsd.entry entry, Xsd.page? page)
		{
			var (totals, other) = GetStrokes(entry, page);
			if (other < 0 || totals.Length == 0) return null;
			return string.Join(" / ", totals.GroupBy(a => a.total).Select(a => $"{a.Key}画 ({string.Join("", a.Select(a => a.radicalChar))}{a.FirstOrDefault().radical}+{other})"));
		}

		public static ((string radicalChar, int total, int radical)[], int other) GetStrokes(Xsd.entry entry, Xsd.page? page)
		{
			if (!int.TryParse(entry.strokes, out var strokes)) strokes = -1;
			if (page is null)
			{
				page = Instance?.kanji.page.FirstOrDefault(a => a.entries.Contains(entry));
				if (page is null) return (Array.Empty<(string, int, int)>(), strokes);
			}
			var strokesRadical = page.radical.characters.character.Select(chr => (chr, Toc.GetStrokeCount(chr))).Where(a => a.Item2 >= 0).ToArray();
			if (strokes < 0) return (strokesRadical.Select(a => (a.chr, -1, a.Item2)).ToArray(), strokes);
			return (strokesRadical.Select(a => (a.chr, a.Item2 + strokes, a.Item2)).ToArray(), strokes);
		}
	}
}