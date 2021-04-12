using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Jellyfin.Plugin.NapiSub.SubtitlesParser.Parsers
{
    public interface ISubtitlesParser
    {
        List<SubtitleItem> ParseStream(Stream stream, Encoding encoding);
    }
}