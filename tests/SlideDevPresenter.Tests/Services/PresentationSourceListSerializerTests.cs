using System.Text;
using System.Text.Json;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class PresentationSourceListSerializerTests
{
  [Theory]
  [InlineData(PresentationSourceType.Git, "ssh://git@example.com/team/slides.git")]
  [InlineData(PresentationSourceType.Git, "git@github.com:team/slides.git")]
  [InlineData(PresentationSourceType.GitHub, "team/slides")]
  public async Task RepositorySource_RoundTripsWithoutLocalCheckoutPath(PresentationSourceType type, string location)
  {
    var source = new PresentationSource
    {
      Id = Guid.NewGuid(), Name = "Team slides", Type = type, Location = location,
            CheckoutPath = "/private/local-checkout", RequiresAuthentication = true, IsPublic = false,
            Branch = "release/slides"
    };
    await using var stream = new MemoryStream();

    await PresentationSourceListSerializer.ExportAsync([source], stream);
    Assert.DoesNotContain("local-checkout", Encoding.UTF8.GetString(stream.ToArray()));
    stream.Position = 0;
    var imported = Assert.Single(await PresentationSourceListSerializer.ImportAsync(stream));

    Assert.Equal(type, imported.Type);
    Assert.Equal(RepositoryLocation.Normalize(location, type), imported.Location);
    Assert.Null(imported.CheckoutPath);
    Assert.Equal("release/slides", imported.Branch);
    Assert.True(imported.RequiresAuthentication);
    Assert.False(imported.IsPublic);
  }

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
    public async Task ExportAsync_SanitizesCredentialsForAnyHttpSourceAndFallsBackToAddressForBlankName()
    {
        var sources = new[]
        {
            new PresentationSource
            {
                Id = Guid.NewGuid(),
                Name = " ",
                Type = PresentationSourceType.LocalRoot,
                Location = "https://slides.example.com/talk?auth_token=auth-secret&oauth_token=oauth-secret&X-Amz-Credential=aws-secret&X-Goog-Signature=google-secret&ref=main#access_token=fragment-secret&slide=4"
            }
        };
        await using var stream = new MemoryStream();

        await PresentationSourceListSerializer.ExportAsync(sources, stream);
        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(stream);
        var entry = Assert.Single(document.RootElement.GetProperty("sources").EnumerateArray());
        const string sanitizedAddress = "https://slides.example.com/talk?ref=main#slide=4";

        Assert.Equal(sanitizedAddress, entry.GetProperty("address").GetString());
        Assert.Equal(sanitizedAddress, entry.GetProperty("title").GetString());
        Assert.True(entry.GetProperty("requiresAuthentication").GetBoolean());

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("auth-secret", json);
        Assert.DoesNotContain("oauth-secret", json);
        Assert.DoesNotContain("aws-secret", json);
        Assert.DoesNotContain("google-secret", json);
        Assert.DoesNotContain("fragment-secret", json);
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
    public async Task ImportAsync_RemovesCredentialParametersAndPreservesOrdinarySlideAnchors()
    {
        const string json = """
            {
              "version": 1,
              "sources": [
                {
                  "title": "Slides",
                  "address": "https://slides.example.com/talk?auth_token=auth-secret&oauth_token=oauth-secret&X-Amz-Credential=aws-secret&X-Goog-Signature=google-secret&ref=main#access_token=fragment-secret&slide=4",
                  "type": "https",
                  "comment": "",
                  "isPublic": true,
                  "requiresAuthentication": false
                },
                {
                  "title": "Anchored slides",
                  "address": "https://slides.example.com/talk#slide-4",
                  "type": "https",
                  "comment": "",
                  "isPublic": true,
                  "requiresAuthentication": false
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var sources = await PresentationSourceListSerializer.ImportAsync(stream);

        Assert.Equal("https://slides.example.com/talk?ref=main#slide=4", sources[0].Location);
        Assert.True(sources[0].RequiresAuthentication);
        Assert.Equal("https://slides.example.com/talk#slide-4", sources[1].Location);
        Assert.False(sources[1].RequiresAuthentication);
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
