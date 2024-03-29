﻿using Aozora.GaijiChuki.Xsd;
using System;
using System.Globalization;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Xml;

namespace Aozora.GaijiChuki;

public interface ISearchQuery
{
	IEnumerable<string> WordsNoHit { get; }
	void ResetWordsNoHit();
	bool Is(entry entry, page page);
	bool IsInNote(entry entry, page page);
	bool Is(PageOtherEntry entry);
	bool IsInNote(PageOtherEntry entry);
	bool Is(page page);
	bool Is(PageOther page);
}

public static partial class SearchQueries
{

	public class SearchQueryOr : ISearchQuery
	{
		public SearchQueryOr(IEnumerable<ISearchQuery> children)
		{
			Children = children ?? throw new ArgumentNullException(nameof(children));
		}

		public IEnumerable<ISearchQuery> Children { get; set; }

		public IEnumerable<string> WordsNoHit
		{
			get
			{
				foreach (var child in Children)
				{
					foreach (var nohit in child.WordsNoHit)
					{
						yield return nohit;
					}
				}
			}
		}

		public bool Is(entry entry, page page) => Children.Any(a => a.Is(entry, page));

		public bool Is(PageOtherEntry entry) => Children.Any(a => a.Is(entry));

		public bool Is(page page) => Children.Any(a => a.Is(page));

		public bool Is(PageOther page) => Children.Any(a => a.Is(page));

		public bool IsInNote(entry entry, page page) => Children.Any(a => a.IsInNote(entry, page));

		public bool IsInNote(PageOtherEntry entry) => Children.Any(a => a.IsInNote(entry));

		public void ResetWordsNoHit()
		{
			foreach (var child in Children) child.ResetWordsNoHit();
		}

		public override string ToString()
		{
			return string.Join(" or ", Children.Select(a => a is SearchQueryOr or SearchQueryAnd ? $"( {a} )" : a.ToString()));
		}
	}

	public class SearchQueryAnd : ISearchQuery
	{
		public SearchQueryAnd(IEnumerable<ISearchQuery> children)
		{
			Children = children ?? throw new ArgumentNullException(nameof(children));
		}

		public IEnumerable<ISearchQuery> Children { get; set; }

		public IEnumerable<string> WordsNoHit
		{
			get
			{
				IEnumerable<string>? list = null;
				foreach (var child in Children)
				{

					if (list is null) list = child.WordsNoHit;
					else
					{
						var nohitTemp = child.WordsNoHit.ToArray();
						list = list.Where(a => nohitTemp.Contains(a));
					}
				}
				return list?.ToArray() ?? new string[0];
			}
		}

		public bool Is(entry entry, page page) => Children.All(a => a.Is(entry, page));

		public bool Is(PageOtherEntry entry) => Children.All(a => a.Is(entry));

		public bool Is(page page) => Children.All(a => a.Is(page));

		public bool Is(PageOther page) => Children.All(a => a.Is(page));

		public bool IsInNote(entry entry, page page) => Children.All(a => a.IsInNote(entry, page));

		public bool IsInNote(PageOtherEntry entry) => Children.All(a => a.IsInNote(entry));

		public void ResetWordsNoHit()
		{
			foreach (var child in Children) child.ResetWordsNoHit();
		}

		public override string ToString()
		{
			return string.Join(" and ", Children.Select(a => a is SearchQueryOr or SearchQueryAnd ? $"( {a} )" : a.ToString()));
		}
	}

	public class SearchQueryStrokes : ISearchQuery
	{
		public SearchQueryStrokes(int stroke)
		{
			Stroke = stroke;
		}

		public int Stroke { get; init; }

		public IEnumerable<string> WordsNoHit => Array.Empty<string>();

		public bool Is(entry entry, page page)
		{
			if (page?.radical?.characters?.character is null) return false;
			if (!int.TryParse(entry.strokes, out var stroke)) return false;
			var strks = page.radical.characters.character.Select(Manager.Toc.GetStrokeCount).Where(a => a >= 0).GroupBy(a => a).Select(a => a.First()).ToArray();
			foreach (var strk in strks)
			{
				if (strk + stroke == Stroke) return true;
			}
			return false;
		}

		public bool Is(PageOtherEntry entry) => false;

		public bool Is(page page)
		{
			if (page?.radical?.characters?.character is null) return false;
			return page.radical.characters.character.Select(Manager.Toc.GetStrokeCount).Any(strk => strk > 0 && strk == Stroke);
		}

		public bool Is(PageOther page) => false;

		public bool IsInNote(entry entry, page page) => Is(entry, page);

		public bool IsInNote(PageOtherEntry entry) => Is(entry);

		public void ResetWordsNoHit() { }

		public override string ToString()
		{
			return $"{Stroke}画";
		}
	}

	public class SearchQueryWord : ISearchQuery
	{
		public SearchQueryWord(string text)
		{
			Text = text ?? throw new ArgumentNullException(nameof(text));
			Unicode = text.EnumerateRunes().Select(a => (a.ToString(), $"{a.Value:X}")).ToArray();

			var info = new StringInfo(text);
			TextSplited = new string[info.LengthInTextElements];
			for (int i = 0; i < info.LengthInTextElements; i++) TextSplited[i] = info.SubstringByTextElements(i, 1);
			Jisx0213Code = TextSplited.Select(a =>
			{
				var mkt = Manager.Jisx0213ReverseProvider?.Invoke(a) ?? (-1, -1, -1);
				if (mkt.men <= 0) { return (string.Empty, mkt); } else { return (a, mkt); }
			})
				.Where(a => !string.IsNullOrEmpty(a.Item1)).ToArray();
			_WordsNoHit = TextSplited.ToList();
		}

		public override string ToString()
		{
			return $"\"{Text}\"";
		}

		public static SearchQueryWord FromCodepoint(params string[] text)
		{
			var r = string.Join(string.Empty, text.Select(org =>
			{ try { return char.ConvertFromUtf32(Convert.ToInt32(org, 16)); } catch { return org; } }));
			return new SearchQueryWord(r);
		}

		public static SearchQueryWord? FromJisX0213(string men, string ku, string ten)
		{
			if (!int.TryParse(men, out int menN)) return null;
			if (!int.TryParse(ku, out int kuN)) return null;
			if (!int.TryParse(ten, out int tenN)) return null;
			var r = Manager.Jisx0213Provider?.Invoke(menN, kuN, tenN);
			if (r is null) return null;
			return new SearchQueryWord(r);
		}

		public string Text { get; init; }

		protected List<string> _WordsNoHit;

		public IEnumerable<string> WordsNoHit => _WordsNoHit.ToArray();

		public void ResetWordsNoHit()
		{
			_WordsNoHit = TextSplited.ToList();
		}


		protected (string original, string code)[] Unicode { get; init; }

		protected string[] TextSplited { get; init; }

		protected (string original, (int men, int ku, int ten) code)[] Jisx0213Code { get; init; }

		bool checkCharacters(object? noteItem, params string[]? characters)
		{
			switch (noteItem)
			{
				case noteJisx0213 jx:
					{
						var hit = Jisx0213Code.FirstOrDefault(a => a.code.men == jx.men && a.code.ku == jx.ku && a.code.ten == jx.ten);
						if (hit != default) _WordsNoHit.Remove(hit.original);
						return hit != default;
					}
				case noteUnicode uc:
					{
						var hit = Unicode.FirstOrDefault(a => uc.code.Equals(a.code, StringComparison.OrdinalIgnoreCase));
						if (hit != default) _WordsNoHit.Remove(hit.original);
						return hit != default;
					}
				default:
					{
						if (characters is null) return false;
						var hit = TextSplited.FirstOrDefault(a => characters.Any(b => a.Equals(b, StringComparison.CurrentCultureIgnoreCase)));
						if (hit is not null) _WordsNoHit.Remove(hit);
						return hit is not null;
					}
			}
		}

		public bool Is(entry entry, page page) => entry is not null && checkCharacters(entry.note?.Item, entry.characters?.character);

		public bool Is(PageOtherEntry entry) => entry is not null && checkCharacters(entry.note?.Item, entry.character);

		public bool Is(page page)
		{
			if (page is null) return false;
			var fd = TextSplited.FirstOrDefault(a => page.radical?.characters?.character?.Any(b => b.Contains(a, StringComparison.CurrentCultureIgnoreCase)) == true);
			if (fd is not null) { _WordsNoHit.Remove(fd); return true; }
			return page.radical?.readings?.reading?.Any(a => a.Contains(Text, StringComparison.CurrentCultureIgnoreCase)) == true;
		}

		public bool Is(PageOther page)
		{
			return page?.header?.Contains(Text, StringComparison.CurrentCultureIgnoreCase) == true;
		}

		public bool IsInNote(entry entry, page page)
		{
			if (entry?.note is null) return false;
			return entry.note.full.Contains(Text, StringComparison.CurrentCultureIgnoreCase) == true;
		}

		public bool IsInNote(PageOtherEntry entry)
		{
			if (entry?.note is null) return false;
			return entry.note.full.Contains(Text, StringComparison.CurrentCultureIgnoreCase) == true;
		}
	}

	public class SearchQueryAny : ISearchQuery
	{
		public IEnumerable<string> WordsNoHit => Array.Empty<string>();

		public bool Is(entry entry, page page) => true;

		public bool Is(PageOtherEntry entry) => true;

		public bool Is(page page) => true;

		public bool Is(PageOther page) => true;

		public bool IsInNote(entry entry, page page) => true;

		public bool IsInNote(PageOtherEntry entry) => true;

		public void ResetWordsNoHit() { }

		public override string ToString()
		{
			return "*";
		}
	}

	public class SearchQueryNone : ISearchQuery
	{
		public IEnumerable<string> WordsNoHit => Array.Empty<string>();

		public bool Is(entry entry, page page) => false;

		public bool Is(PageOtherEntry entry) => false;

		public bool Is(page page) => false;

		public bool Is(PageOther page) => false;

		public bool IsInNote(entry entry, page page) => false;

		public bool IsInNote(PageOtherEntry entry) => false;

		public void ResetWordsNoHit() { }

		public override string ToString()
		{
			return "∅";
		}
	}
}