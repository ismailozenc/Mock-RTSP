using System.Text.Json;

namespace MockRtsp;

/// <summary>One camera in a project: its stream path, playlist and how it was playing.</summary>
public sealed class ProjectCamera
{
    public string StreamPath { get; set; } = string.Empty;

    /// <summary>The playlist. In the file: relative to the project's folder when inside it, otherwise full paths.</summary>
    public List<string> Videos { get; set; } = new();

    public bool Running { get; set; } = true;
    public bool Paused { get; set; }
    public double Speed { get; set; } = 1.0;

    /// <summary>Which video of the playlist was playing, and where.</summary>
    public int Video { get; set; }
    public double PositionSeconds { get; set; }
}

/// <summary>
/// A saved setup: the server port, the settings (defaults for videos, and the server), the video
/// folder and every camera with its playlist. Opening it puts everything back. Each video's own
/// settings, skips and motion data stay in the files next to the video, so they come along too.
/// </summary>
public sealed class ProjectFile
{
    public const string Extension = ".mockrtsp-project";
    public const string Filter = "MockRtsp projects (*" + Extension + ")|*" + Extension + "|All files (*.*)|*.*";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public int Version { get; set; } = 1;
    public int Port { get; set; } = 8554;
    public bool StartServer { get; set; }
    public StreamOptions Settings { get; set; } = new();
    public string VideoFolder { get; set; } = string.Empty;
    public List<ProjectCamera> Cameras { get; set; } = new();

    /// <summary>Reads a project, with every path made full. Throws InvalidDataException if the file is not a usable project.</summary>
    public static ProjectFile Load(string path)
    {
        string text = File.ReadAllText(path);
        ProjectFile? project;

        try
        {
            project = JsonSerializer.Deserialize<ProjectFile>(text, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a MockRtsp project ({ex.Message}).", ex);
        }

        if (project is null || project.Version != 1)
            throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a MockRtsp project this version can open.");

        project.Settings ??= new StreamOptions();
        project.Cameras ??= new List<ProjectCamera>();

        var problems = project.Settings.Validate();
        if (problems.Count > 0)
            throw new InvalidDataException($"The settings in '{Path.GetFileName(path)}' are not usable: {string.Join(" ", problems)}");

        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        project.VideoFolder = Resolve(folder, project.VideoFolder);
        foreach (var camera in project.Cameras)
        {
            camera.Videos = (camera.Videos ?? new List<string>()).Select(v => Resolve(folder, v)).ToList();
            camera.StreamPath ??= string.Empty;
        }

        return project;
    }

    /// <summary>Saves the project. Paths inside the project's folder are kept relative, so the folder can be moved or copied.</summary>
    public void Save(string path)
    {
        string full = Path.GetFullPath(path);
        string folder = Path.GetDirectoryName(full)!;

        var copy = new ProjectFile
        {
            Version = Version,
            Port = Port,
            StartServer = StartServer,
            Settings = Settings.Clone(),
            VideoFolder = Relative(folder, VideoFolder),
            Cameras = Cameras.Select(c => new ProjectCamera
            {
                StreamPath = c.StreamPath,
                Videos = c.Videos.Select(v => Relative(folder, v)).ToList(),
                Running = c.Running,
                Paused = c.Paused,
                Speed = c.Speed,
                Video = c.Video,
                PositionSeconds = Math.Round(c.PositionSeconds, 2)
            }).ToList()
        };

        // What the app remembers between runs is not part of a project.
        copy.Settings.VideoFolder = string.Empty;
        copy.Settings.RecentFolders = Array.Empty<string>();
        copy.Settings.LastProject = string.Empty;

        string temp = full + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(copy, Json));
        File.Move(temp, full, overwrite: true);
    }

    /// <summary>The project as text, leaving out where each camera is in its video, which changes all the time.</summary>
    public string Fingerprint()
    {
        var copy = (ProjectFile)MemberwiseClone();
        copy.Settings = Settings.Clone();
        copy.Settings.VideoFolder = string.Empty;
        copy.Settings.RecentFolders = Array.Empty<string>();
        copy.Settings.LastProject = string.Empty;
        copy.Settings.OpenLastProject = false;
        copy.Cameras = Cameras.Select(c => new ProjectCamera
        {
            StreamPath = c.StreamPath,
            Videos = c.Videos,
            Running = c.Running,
            Speed = c.Speed
        }).ToList();
        return JsonSerializer.Serialize(copy, Json);
    }

    private static string Relative(string folder, string path)
    {
        if (path.Length == 0)
            return path;

        string relative = Path.GetRelativePath(folder, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    private static string Resolve(string folder, string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(Path.Combine(folder, path));
}
