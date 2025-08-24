using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

using Octokit;

namespace AdvancedUpdaterGitHubProxy.Models;

[UsedImplicitly]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal partial class UpdateRelease(Release release)
{
    private static readonly Regex InstructionBlockRegex = InstructionBlockXtractRegex();
    private static readonly Regex VersionRegex = SemVerRegex();

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Expose release data you actually need
    public string Name => release.Name;
    public string TagName => release.TagName;
    public string Body => release.Body;
    public IReadOnlyList<ReleaseAsset> Assets => release.Assets;
    public DateTimeOffset CreatedAt => release.CreatedAt;
    public DateTimeOffset? PublishedAt => release.PublishedAt;
    public bool Prerelease => release.Prerelease;
    public Uri HtmlUrl => new(release.HtmlUrl);

    [JsonIgnore] public UpdaterInstructionsFile? UpdaterInstructions { get; private set; }

    public UpdaterInstructionsFile? EnsureUpdaterInstructions()
    {
        if (UpdaterInstructions is not null)
        {
            return UpdaterInstructions;
        }

        ReleaseAsset? asset = Assets.FirstOrDefault();
        if (asset is null)
        {
            return null;
        }

        Match m = InstructionBlockRegex.Match(Body);
        if (!m.Success)
        {
            return null;
        }

        UpdaterInstructionsFile? block =
            JsonSerializer.Deserialize<UpdaterInstructionsFile>(m.Groups[1].Value, SerializerOptions);
        if (block is null)
        {
            return null;
        }

        block.Name = Name;
        block.Url = asset.BrowserDownloadUrl;
        block.Size = asset.Size;
        if (PublishedAt.HasValue)
        {
            block.ReleaseDate = PublishedAt.Value.DateTime;
        }

        Match vm = VersionRegex.Match(TagName);
        if (!vm.Success)
        {
            return null;
        }

        block.Version = new Version(vm.Groups[1].Value);
        block.Description = $"<a href=\"{HtmlUrl}\">Click to view the full changelog online.</a>";

        UpdaterInstructions = block;
        return UpdaterInstructions;
    }

    [GeneratedRegex(@"((\d+\.)?(\d+\.)?(\*|\d+))")]
    private static partial Regex SemVerRegex();

    [GeneratedRegex(@"^<!--([\s\S]*?)-->", RegexOptions.Singleline)]
    private static partial Regex InstructionBlockXtractRegex();
}