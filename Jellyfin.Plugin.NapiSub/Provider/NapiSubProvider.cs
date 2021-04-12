using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Providers;
using Jellyfin.Plugin.NapiSub.Core;
using Jellyfin.Plugin.NapiSub.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Jellyfin.Plugin.NapiSub.Provider
{
    public class NapiSubProvider : ISubtitleProvider, IHasOrder
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NapiSubProvider> _logger;
        private readonly IFileSystem _fileSystem;
        private ILocalizationManager _localizationManager;

        public NapiSubProvider(Logger<NapiSubProvider> logger, IFileSystem fileSystem, HttpClient httpClient, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _localizationManager = localizationManager;
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<SubtitleResponse> GetSubtitles(string hash, CancellationToken cancellationToken)
        {
            var request = NapiCore.CreateRequest(hash, "PL");

            try
            {
                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(await response.Content.ReadAsStringAsync()))
                    {
                        var xml = await reader.ReadToEndAsync().ConfigureAwait(false);
                        var status = XmlParser.GetStatusFromXml(xml);

                        if (status != null && status == "success")
                        {
                            var subtitlesBase64 = XmlParser.GetSubtitlesBase64(xml);
                            var stream = XmlParser.GetSubtitlesStream(subtitlesBase64);
                            var subRip = SubtitlesConverter.ConvertToSubRipStream(stream);

                            if (subRip != null)
                            {
                                return new SubtitleResponse
                                {
                                    Format = "srt",
                                    Language = "PL",
                                    Stream = subRip
                                };
                            }
                        }
                    }
                }

                _logger.LogInformation("No subtitles downloaded");
                return new SubtitleResponse();
            }
            catch (Exception)
            {
                return new SubtitleResponse();
            }
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request,
            CancellationToken cancellationToken)
        {
            var language = _localizationManager.FindLanguageInfo(request.Language);

            if (language == null || !string.Equals(language.TwoLetterISOLanguageName, "PL", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }

            var hash = await NapiCore.GetHash(request.MediaPath, cancellationToken, _fileSystem, _logger);
            var requestMessage = NapiCore.CreateRequest(hash, language.TwoLetterISOLanguageName);

            try
            {
                using (var response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false))
                {
                    using (var reader = new StreamReader(await response.Content.ReadAsStringAsync()))
                    {
                        var xml = await reader.ReadToEndAsync().ConfigureAwait(false);
                        var status = XmlParser.GetStatusFromXml(xml);

                        if (status != null && status == "success")
                        {
                            _logger.LogInformation("Subtitles found by NapiSub");

                            return new List<RemoteSubtitleInfo>
                            {
                                new RemoteSubtitleInfo
                                {
                                    IsHashMatch = true,
                                    ProviderName = Name,
                                    Id = hash,
                                    Name = "A subtitle matched by hash",
                                    ThreeLetterISOLanguageName = language.ThreeLetterISOLanguageName,
                                    Format = "srt"
                                }
                            };
                        }
                    }

                    _logger.LogInformation("No subtitles found by NapiSub");
                    return new List<RemoteSubtitleInfo>();
                }
            }
            catch (Exception)
            {
                return new List<RemoteSubtitleInfo>();
            }
        }

        public string Name => "NapiSub";

        public IEnumerable<VideoContentType> SupportedMediaTypes =>
            new List<VideoContentType> { VideoContentType.Episode, VideoContentType.Movie };

        public int Order => 1;
    }
}