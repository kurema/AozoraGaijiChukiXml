using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aozora.GaijiChuki;

namespace TestProject1;

public class UnitTestSearchQuery
{
	[Fact]
	public void TestSearchQueryTokenize()
	{
		Assert.Equal("(0, 1, BrancketOpen), (1, 3, Text), (7, 4, Unicode), (11, 1, BrancketClose), (13, 3, And), (17, 1, Strokes)", SearchQueries.Parser.TokenizeAndFormat("(テスト U+abcd) and 5画"));
		Assert.Equal("(0, 1, Text), (1, 5, Unicode), (6, 1, Text), (9, 4, Unicode), (13, 1, Text), (14, 4, Unicode), (18, 1, Text)", SearchQueries.Parser.TokenizeAndFormat("わ01234がU+a123は1234い"));
		Assert.Equal("(0, 4, Text), (5, 2, Or), (8, 4, Text), (13, 3, And), (17, 6, Unicode), (24, 4, Unicode)", SearchQueries.Parser.TokenizeAndFormat("わがはい or 猫である and abcdef 0001"));
		Assert.Equal("(0, 1, JisX0213Men), (2, 1, JisX0213Ku), (4, 1, JisX0213Ten), (11, 1, JisX0213Men), (17, 1, JisX0213Ku), (22, 1, JisX0213Ten)", SearchQueries.Parser.TokenizeAndFormat("1-2-3 第2水準 1面 0002  区 3 点"));
	}

	[Fact]
	public void TestSearchQueryParse()
	{
		{
			var parsed = SearchQueries.Parser.Parse("(テスト U+abcd) and 5画");
			var top = parsed as SearchQueries.SearchQueryAnd;
			Assert.NotNull(top);
			Assert.Equal(2, top.Children.Count());
			Assert.True(top.Children.First() is SearchQueries.SearchQueryOr);
			Assert.Equal("テスト", ((top?.Children?.First() as SearchQueries.SearchQueryOr)?.Children?.First() as SearchQueries.SearchQueryWord)?.Text);
		}
		{
			var parsed = SearchQueries.Parser.Parse("わ1234がU+a123は1234い");
			var top = parsed as SearchQueries.SearchQueryOr;
			Assert.NotNull(top);
			var list = top.Children.ToArray();
			var list2 = new[] { "わ", "\x1234", "が", "\xa123", "は", "\x1234", "い" };//文字コードは要修正。
			for (int i = 0; i < list.Length; i++)
			{
				Assert.True(list[i] is SearchQueries.SearchQueryWord);
				Assert.True((list[i] as SearchQueries.SearchQueryWord)?.Text == list2[i]);
			}
		}
		{
			var parsed = SearchQueries.Parser.Parse("わ or and or and and か");
			Assert.True(parsed is SearchQueries.SearchQueryAnd qu &&
				qu.Children.ToArray() is [SearchQueries.SearchQueryWord, SearchQueries.SearchQueryWord]);
		}
		{
			var parsed = SearchQueries.Parser.Parse("わ or か and ば");
			Assert.True(parsed is SearchQueries.SearchQueryOr por &&
				por.Children.ToArray() is [SearchQueries.SearchQueryWord, SearchQueries.SearchQueryAnd]
				);
		}
		{
			var parsed = SearchQueries.Parser.Parse("第3水準1-2-3 第4水準第2面第1区第2点");
			//Jisx0213が未登録の場合、1-2-3のような文字列と認識されます。これでも検索できます。
			Assert.True(parsed is SearchQueries.SearchQueryOr por &&
				por.Children.Select(a => ((SearchQueries.SearchQueryWord)a).Text).ToArray() is ["1-2-3", "2-1-2"]
				);
		}
	}

	[Fact]
	public void TestToHalf()
	{
		Assert.Equal("こんにちはabcde0164abc", SearchQueries.Parser.ToHalf("こんにちはａｂｃｄｅ０１６４ＡＢＣ"));
	}

}
