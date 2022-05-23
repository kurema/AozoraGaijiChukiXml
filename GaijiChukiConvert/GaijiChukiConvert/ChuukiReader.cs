using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace GaijiChukiConvert;

public static class ChuukiReader
{
    // language=regex
    private const string NotePattern = @$"※{NotePatternBasic}";
    // language=regex
    private const string NotePatternBasic = @"［＃([^］]+)］";
    private const string InputableKeyword = "入力可能";

    public async static Task<Schemas.dictionary> LoadDictionary(TextReader reader)
    {

        int pageCnt = 1;

        while (true)
        {
            //先頭読み飛ばし
            var line = await reader.ReadLineAsync();

            if (line == null) break;
            if (line.Contains('\f')) pageCnt++;
            if (line.Contains("【十五・十六・十七画】")) break;
        }

        string? nextLine = null;
        var result = new Schemas.dictionary();
        {
            Schemas.entry? current = null;
            List<Schemas.entry> entries = new();
            Schemas.page? page = null;
            List<Schemas.page> pages = new();
            bool duplicate = false;

            while (true)
            {
                var line = await reader.ReadLineAsync();

                if (line == null) break;

                //while (line.Contains('［') && !line.Contains('］'))
                while (line.Count(a => a == '［') != line.Count(a => a == '］'))
                {
                    line = Regex.Replace(line, @"[\s　]+$", "");
                    var tmp = await reader.ReadLineAsync();
                    if (tmp?.Contains('\f') == true) pageCnt++;
                    line += tmp;
                }

                //while (line.Contains('【') && !line.Contains('】'))
                while (line.Count(a => a == '【') != line.Count(a => a == '】'))
                {
                    line = Regex.Replace(line, @"[\s　]+$", "");
                    var tmp = await reader.ReadLineAsync();
                    if (tmp?.Contains('\f') == true) pageCnt++;
                    line += tmp;
                }
                if (line.Contains("包摂適用"))
                {
                    line = Regex.Replace(line, @"[\s　]+$", "");
                    var tmp = await reader.ReadLineAsync();
                    if (tmp?.Contains('\f') == true) pageCnt++;
                    line += tmp;
                }

                if (line.Contains('\f')) pageCnt++;

                {
                    var match = Regex.Match(line, @"^(.+)【その他】に戻る[\s　]*$");
                    if (match.Success)
                    {
                        if (page is not null)
                        {
                            if (current is not null) entries.Add(current);
                            page.entries = entries.ToArray();
                            pages.Add(page);
                        }
                        nextLine = line;
                        break;
                    }
                }

                {
                    var match = Regex.Match(line, @"^(\d+)．");
                    if (match.Success)
                    {
                        if (current is not null)
                        {
                            entries.Add(current);
                        }
                        current = new Schemas.entry() { docPage = pageCnt.ToString(), characters = new Schemas.entryCharacters(), duplicate = duplicate };
                        duplicate = false;
                        current.strokes = match.Groups[1].Value;
                    }
                }

                {
                    var match = Regex.Match(line, @"^(.+)【(.+)】[\s　]*部首・読み索引に戻る[\s　]*部首・画数索引に戻る[\s　]*$");
                    if (match.Success)
                    {
                        string r = match.Groups[1].Value;
                        r = new Regex(@"[\s　]").Replace(r, "");
                        string c = match.Groups[2].Value;
                        if (page is not null)
                        {
                            if (current is not null) entries.Add(current);
                            page.entries = entries.ToArray();
                            entries = new List<Schemas.entry>();
                            pages.Add(page);
                            current = null;
                        }
                        page = new Schemas.page();
                        page.radical = new Schemas.pageRadical()
                        {
                            readings = new Schemas.pageRadicalReadings() { reading = r.Split('・') },
                            characters = new Schemas.pageRadicalCharacters() { character = EnumerateCharacters(c) }
                        };
                        continue;
                    }
                }

                if (current is null) continue;

                {
                    var match = Regex.Match(line, @"([^］．]+)→[\s\t　]*［");
                    if (match.Success)
                    {
                        var text = Regex.Replace(match.Groups[1].Value, @"[\s　]", "");
                        if (text.Length > 0)
                        {
                            current.characters = new Schemas.entryCharacters() { character = EnumerateCharacters(text) };
                        }
                    }
                }

                {
                    // language=regex
                    var regex = @"［包摂適用[\s　]+(.+)］[\s　]*([\d、]*)";
                    var match = Regex.Match(line, regex);
                    if (match.Success)
                    {
                        var tmp = new Schemas.entryInclusionApplication();
                        var match2 = Regex.Match(match.Groups[1].Value, NotePattern);
                        if (match2.Success)
                        {
                            tmp.Item = GetNoteSerializable(match2.Groups[1].Value);
                        }
                        else
                        {
                            tmp.Item = match.Groups[1].Value;
                        }

                        {
                            tmp.reference = match.Groups[2].Value.Split("、").Where(a => !string.IsNullOrWhiteSpace(a))
                                .Select(a => new Schemas.entryInclusionApplicationReference() { page = a }).ToArray();
                        }

                        line = line.Replace(match.Value, "");
                        current.Item = tmp;
                    }
                }

                {
                    // language=regex
                    var regex = @"［統合適用[\s　]+(.+)］";
                    var match = Regex.Match(line, regex);
                    if (match.Success)
                    {
                        var tmp = new Schemas.entryIntegrationApplication();
                        var match2 = Regex.Match(match.Groups[1].Value, NotePattern);
                        if (match2.Success)
                        {
                            tmp.Item = GetNoteSerializable(match2.Groups[1].Value);
                        }
                        else
                        {
                            tmp.Item = match.Groups[1].Value;
                        }

                        line = line.Replace(match.Value, "");
                        current.Item = tmp;
                    }
                }

                {
                    // language=regex
                    var regex = @"［78互換包摂[\s　]+(.+)］";
                    var match = Regex.Match(line, regex);
                    if (match.Success)
                    {
                        var tmp = new Schemas.entryCompatible78Inclusion
                        {
                            @ref = match.Groups[1].Value,
                        };
                        line = line.Replace(match.Value, "");
                        current.Item = tmp;
                    }
                }

                {
                    // language=regex
                    var regex = @"［デザイン差[\s　]+(.+)］";
                    var match = Regex.Match(line, regex);
                    if (match.Success)
                    {
                        var tmp = new Schemas.entryDesignVariant
                        {
                            @ref = match.Groups[1].Value
                        };
                        line = line.Replace(match.Value, "");
                        current.Item = tmp;
                    }
                }

                {
                    // language=regex
                    var regex = @$"([^］．\s　]+)[\s　]+{InputableKeyword}";
                    var match = Regex.Match(line, regex);
                    if (match.Success)
                    {
                        line = line.Replace(match.Value, "");
                        current.Item = new object();//意味不明だけど、objectがinputableになるっぽい。
                    }

                    {
                        var text = Regex.Replace(match.Groups[1].Value, @"[\s　]", "");
                        if (text.Length > 0)
                        {
                            current.characters = new Schemas.entryCharacters() { character = EnumerateCharacters(text) };
                        }
                    }
                }

                {
                    var match = Regex.Match(line, $@"([^\d+．]+){NotePattern}");
                    if (match.Success)
                    {
                        {
                            var chars = match.Groups[1].Value;
                            chars = Regex.Replace(chars, @"[\s　]", "");
                            current.characters = new Schemas.entryCharacters()
                            {
                                character = EnumerateCharacters(chars),
                            };
                        }

                        {
                            current.note = GetNoteSerializable(match.Groups[2].Value);
                        }
                    }
                }

                {
                    var matches = Regex.Matches(line, @"UCV(\d+)");
                    var list = current.UCV?.ToList() ?? new List<Schemas.entryUCV>();
                    foreach (Match match in matches)
                    {
                        list.Add(new Schemas.entryUCV() { number = match.Groups[1].Value });
                    }
                    current.UCV = list.ToArray();
                }

                {
                    if (line.Contains('★')) duplicate = true;
                    if (line.Contains("補助のみ")) current.supplement = Schemas.entrySupplement.supplementOnly;
                    if (line.Contains("補助漢字と共通")) current.supplement = Schemas.entrySupplement.supplementCommon;
                }
            }

            result.kanji = new Schemas.dictionaryKanji() { page = pages.ToArray() };
        }

        {
            var current = new Schemas.PageOther();
            var currentTop = current;
            var otherPages = new List<Schemas.PageOther>();
            var entries = new List<Schemas.PageOtherEntry>();
            bool skip = false;

            while (true)
            {
                var line = nextLine ?? await reader.ReadLineAsync();
                nextLine = null;

                if (line == null) break;
                if (line.Contains('\f')) pageCnt++;

                if (line.StartsWith("アクセント付きラテン文字（アクセント分解）【その他】に戻る"))
                {
                    skip = true;
                    continue;
                }
                else if (line.StartsWith("アクセント付きラテン文字（アクセント分解以外）【その他】に戻る"))
                {
                    skip = false;
                }
                else if (line.StartsWith("改訂内容目次"))
                {
                    current.entries = entries.ToArray();
                    break;
                }

                if (skip) continue;

                while (line.Count(a => a == '［') != line.Count(a => a == '］'))
                {
                    line = Regex.Replace(line, @"[\s　]+$", "");
                    var tmp = await reader.ReadLineAsync();
                    if (tmp?.Contains('\f') == true) pageCnt++;
                    line += tmp;
                }

                {
                    var match = Regex.Match(line, @"^(.+)【(.+?)(?:目次)?】に戻る");
                    if (match.Success)
                    {
                        current.entries = entries.ToArray();
                        entries = new();

                        if (match.Groups[2].Value == "その他")
                        {
                            currentTop = current = new Schemas.PageOther()
                            {
                                header = match.Groups[1].Value,
                            };
                            otherPages.Add(current);

                        }
                        else if (match.Groups[2].Value == currentTop.header || match.Groups[2].Value is "母音")
                        {
                            current = new Schemas.PageOther()
                            {
                                header = match.Groups[1].Value,
                            };

                            {
                                var list = (currentTop.PageOther1?.ToList() ?? new());
                                list.Add(current);
                                currentTop.PageOther1 = list.ToArray();
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }

                {
                    var match = Regex.Match(line, @$"^(.+?)(（.+）)[\s\t]*{NotePatternBasic}(.*)$");

                    if (match.Success)
                    {
                        if (match.Groups[2].Value is not "（例）") continue;

                        var word = match.Groups[1].Value;
                        word = Regex.Replace(word, @"[\s　]", "");

                        var info = match.Groups[4].Value;
                        info = Regex.Replace(info, @"[\s　]", "");

                        var entry = new Schemas.PageOtherEntry()
                        {
                            note = GetNoteSerializable(match.Groups[3].Value),
                            character = word,
                            docPage = pageCnt.ToString(),
                        };

                        if (!string.IsNullOrEmpty(match.Groups[2].Value)) entry.note.pre = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(info)) entry.info = info;

                        entries.Add(entry);

                        nextLine = line.Replace(match.Value, "");

                        continue;
                    }
                }

                {
                    var match = Regex.Match(line, @$"^(.+?)[\s\t]*{NotePattern}(.*)$");

                    if (match.Success)
                    {
                        var word = match.Groups[1].Value;
                        word = Regex.Replace(word, @"[\s　]", "");
                        word = word.Replace("\x06", "");
                        word = word.Replace("\x1e", "");
                        word = word.Replace("\x1f", "");

                        var info = match.Groups[3].Value;
                        bool inputable = info.Contains(InputableKeyword);
                        info = info.Replace(InputableKeyword, "");
                        info = Regex.Replace(info, @"^[\s　]", "");
                        info = Regex.Replace(info, @"[\s　]$", "");

                        var entry = new Schemas.PageOtherEntry()
                        {
                            note = GetNoteSerializable(match.Groups[2].Value),
                            character = word,
                            docPage = pageCnt.ToString(),
                            inputable = inputable,
                        };

                        if (!string.IsNullOrEmpty(info)) entry.info = info;

                        entries.Add(entry);

                        nextLine = line.Replace(match.Value, "");

                        continue;
                    }
                }

                {
                    // language=regex
                    var regex = @$"([^］．\s　]+)[\s　]+{InputableKeyword}";
                    var match = Regex.Match(line, regex);

                    if (match.Success)
                    {
                        var charString = match.Groups[1].Value;
                        charString = Regex.Replace(charString, @"[\s　]", "");
                        var chars = EnumerateCharacters(charString);
                        foreach (var @char in chars)
                        {
                            var entry = new Schemas.PageOtherEntry()
                            {
                                character = @char,
                                docPage = pageCnt.ToString(),
                                inputable = true,
                            };
                            entries.Add(entry);
                        }
                    }
                }

                //break;
            }

            result.other = new Schemas.dictionaryOther() { PageOther = otherPages.ToArray() };
        }

        return result;
    }

    public static Schemas.note GetNoteSerializable(string text)
    {
        var texts = text.Split('、');

        var note = new Schemas.note()
        {
            full = text,
            description = texts[0],
        };
        if (texts.Length >= 2) note.Item = GetSerializableFromCharCode(texts[1]);
        return note;
    }

    public static void WriteDictionary(string path, Schemas.dictionary dictionary)
    {
        using var writerXml = System.Xml.XmlWriter.Create(path, new System.Xml.XmlWriterSettings() { Indent = true });
        var xs = new System.Xml.Serialization.XmlSerializer(typeof(Schemas.dictionary));
        xs.Serialize(writerXml, dictionary);
        writerXml.Close();
    }

    public static object? GetSerializableFromCharCode(string text)
    {
        {
            var match = Regex.Match(text, @"^U\+([\d+a-fA-F]+)");
            if (match.Success)
            {
                //var code = match.Groups[1].Value;//Span使えそう。
                //byte[] codeByte = new byte[code.Length / 2];
                //for (int i = 0; i < code.Length; i += 2)
                //{
                //    codeByte[i] = Convert.ToByte(code.Substring(i, 2), 16);
                //}
                return new Schemas.noteUnicode()
                {
                    code = match.Groups[1].Value,
                };
            }
        }
        {
            var match = Regex.Match(text, @"^第(\d)水準(\d)+\-(\d+)-(\d+)");
            if (match.Success)
            {
                return new Schemas.noteJisx0213()
                {
                    levelSpecified = true,
                    level = int.Parse(match.Groups[1].Value),
                    menSpecified = true,
                    men = int.Parse(match.Groups[2].Value),
                    kuSpecified = true,
                    ku = int.Parse(match.Groups[3].Value),
                    tenSpecified = true,
                    ten = int.Parse(match.Groups[4].Value),
                };
            }
        }
        {
            var match = Regex.Match(text, @"^(\d)+\-(\d+)-(\d+)");
            if (match.Success)
            {
                return new Schemas.noteJisx0213()
                {
                    levelSpecified = true,
                    level = 0,
                    menSpecified = true,
                    men = int.Parse(match.Groups[1].Value),
                    kuSpecified = true,
                    ku = int.Parse(match.Groups[2].Value),
                    tenSpecified = true,
                    ten = int.Parse(match.Groups[3].Value),
                };
            }
        }
        return null;
    }

    public static string[] EnumerateCharacters(string text)
    {
        //サロゲートペアに配慮して文字を分割。
        //https://qiita.com/koara-local/items/95e07949021a5a87fed8
        var result = new List<string>();
        var charEnum = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (charEnum.MoveNext())
        {
            result.Add(charEnum.GetTextElement());
        }
        return result.ToArray();
    }
}
