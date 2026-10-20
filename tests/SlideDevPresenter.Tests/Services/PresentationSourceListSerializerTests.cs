using System.Text;
using System.Text.Json;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class PresentationSourceListSerializerTests
{
    [Fact]
    public async Task ExportAsync_ExportsOnlyOnlineSourcesWithoutAuthenticationData()
    {
        var sources = new[]
        {
            new PresentationSource
            {
                Id = Guid.NewGuid(),
                Name = "Private repository",
                Type = PresentationSourceType.HostedUrl,
                Location = "https://github.com/team/slides?access_token=sample&ref=main",
                Comment = "Team deck",
                IsPublic = false
            },
            new PresentationSource
            {
                Id = Guid.NewGuid(),
                Name = "Local",
                Type = PresentationSourceType.LocalRoot,
                Location = "~/slides"
            }
        };
        await using var stream = new MemoryStream();

        await PresentationSourceListSerializer.ExportAsync(sources, stream);
        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(stream);
        var entries = document.RootElement.GetProperty("sources");

        var entry = Assert.Single(entries.EnumerateArray());
        Assert.Equal("Private repository", entry.GetProperty("title").GetString());
        Assert.Equal("https://github.com/team/slides?ref=main", entry.GetProperty("address").GetString());
        Assert.Equal("github", entry.GetProperty("type").GetString());
        Assert.Equal("Team deck", entry.GetProperty("comment").GetString());
        Assert.False(entry.GetProperty("isPublic").GetBoolean());
        Assert.True(entry.GetProperty("requiresAuthentication").GetBoolean());

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("access_token", json);
        Assert.DoesNotContain("sample", json);
    }

    [Fact]
    public async Task ImportAsync_ImportsOnlineSourcesAndMarksAuthenticationNeeded()
    {
        const string json = """
            {
              "version": 1,
              "sources": [
                {
                  "title": "Slides",
                  "address": "https://github.com/team/slides?token=sample",
                  "type": "github",
                  "comment": "Requires access",
                  "isPublic": false,
                  "requiresAuthentication": false
                },
                {
                  "title": "Public deck",
                  "address": "http://slides.example.com/talk",
                  "type": "http",
                  "comment": "",
                  "isPublic": true,
                  "requiresAuthentication": false
                },
                {
                  "title": "Secure deck",
                  "address": "https://slides.example.com/secure",
                  "type": "https",
                  "comment": "",
                  "isPublic": true,
                  "requiresAuthentication": true
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var sources = await PresentationSourceListSerializer.ImportAsync(stream);

        Assert.Equal(3, sources.Count);
        Assert.Equal(PresentationSourceType.HostedUrl, sources[0].Type);
        Assert.Equal("https://github.com/team/slides", sources[0].Location);
        Assert.True(sources[0].RequiresAuthentication);
        Assert.False(sources[0].IsPublic);
        Assert.Equal("Requires access", sources[0].Comment);
        Assert.Equal("http://slides.example.com/talk", sources[1].Location);
        Assert.Equal("https://slides.example.com/secure", sources[2].Location);
        Assert.True(sources[2].RequiresAuthentication);
    }

    [Fact]
    public async Task ImportAsync_RejectsLocalAddresses()
    {
        const string json = """
            {
              "version": 1,
              "sources": [
                {
                  "title": "Local",
                  "address": "~/slides",
                  "type": "https",
                  "comment": "",
                  "isPublic": true,
                  "requiresAuthentication": false
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PresentationSourceListSerializer.ImportAsync(stream));
    }
}
