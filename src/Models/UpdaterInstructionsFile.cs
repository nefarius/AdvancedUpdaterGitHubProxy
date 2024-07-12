#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace AdvancedUpdaterGitHubProxy.Models;

[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
internal sealed class UpdaterInstructionsFile
{
    /// <summary>
    ///     The name of the new release.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     The direct download URL to the new setup.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    ///     The size - in bytes - of the setup on the server.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    ///     The version that's available on the server.
    /// </summary>
    public Version Version { get; set; } = null!;

    /// <summary>
    ///     Gets whether this release should be made available as an update.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    ///     If set the registry key where the installed application version information can be found.
    /// </summary>
    public string RegistryKey { get; set; }

    public string FilePath { get; set; }

    public List<string> Features { get; set; } = new();

    public List<string> Enhancements { get; set; } = new();

    public List<string> BugFixes { get; set; } = new();

    public DateTime ReleaseDate { get; set; }

    public string Replaces { get; set; } = "All";

    public string Depends { get; set; } = default;

    public string NextDeprecated { get; set; } = default;

    public string Flags { get; set; } = default;

    /// <summary>
    ///     The description of the new release.
    /// </summary>
    public string Description { get; set; }

    [JsonIgnore]
    public string? FileContent { get; private set; }

    public string? EnsureFileContent()
    {
        if (!string.IsNullOrEmpty(FileContent))
        {
            return FileContent;
        }

        StringBuilder sb = new();

        sb.AppendLine(";aiu;");
        sb.AppendLine();

        if (!Available)
        {
            sb.AppendLine("[General]");
            sb.AppendLine("UpdatesDisabled = Updates are not available at this time");
            sb.AppendLine();
        }

        sb.AppendLine($"[{Name}]");
        sb.AppendLine($"Name = {Name}");
        sb.AppendLine($"Description = {Description}");
        sb.AppendLine($"URL = {Url}");
        sb.AppendLine($"Size = {Size}");
        sb.AppendLine($"Version = {Version}");
        sb.AppendLine($"ReleaseDate = {ReleaseDate.ToString("dd/MM/yyyy")}");

        // Give file version check priority over registry key
        if (!string.IsNullOrEmpty(FilePath))
        {
            sb.AppendLine($"FilePath = {FilePath}");
        }
        else if (!string.IsNullOrEmpty(RegistryKey))
        {
            sb.AppendLine($"RegistryKey = {RegistryKey}");
        }

        if (!string.IsNullOrEmpty(Flags))
        {
            sb.AppendLine($"Flags = {Flags}");
        }

        /*if (!string.IsNullOrEmpty(Replaces))
        {
            sb.AppendLine($"Replaces = {Replaces}");
        }*/

        if (!string.IsNullOrEmpty(Depends))
        {
            sb.AppendLine($"Depends = {Depends}");
        }

        if (!string.IsNullOrEmpty(NextDeprecated))
        {
            sb.AppendLine($"NextDeprecated = {NextDeprecated}");
        }

        FileContent = sb.ToString();

        return FileContent;
    }

    /// <summary>
    ///     Creates the body of the updater INI file.
    /// </summary>
    public override string ToString()
    {
        return FileContent ??
               throw new InvalidOperationException($"{nameof(FileContent)} is not supposed to be empty at this state.");
    }
}