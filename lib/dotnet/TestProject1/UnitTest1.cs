using Xunit;

namespace TestProject1;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var content= Aozora.GaijiChuki.Manager.LoadContent();

        Assert.NotNull(content);
        Assert.Equal("一", content.kanji.page[0].radical.characters.character[0]);
        Assert.Equal("いち", content.kanji.page[0].radical.readings.reading[0]);
        Assert.Equal("𠂉", content.kanji.page[0].entries[0].characters.character[0]);
        Assert.Equal("「尓－小」、第4水準2-1-1", content.kanji.page[0].entries[0].note.full);
        Assert.Equal("「尓－小」", content.kanji.page[0].entries[0].note.description);

        //var r1 = Aozora.GaijiChuki.Manager.Toc.RadicalFromStroke;
        //var r2 = Aozora.GaijiChuki.Manager.Toc.RadicalCharacters;
        //var r3 = Aozora.GaijiChuki.Manager.Toc.RadicalFromReadings;
    }
}