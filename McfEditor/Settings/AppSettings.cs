namespace McfEditor.Settings;

public sealed class AppSettings
{
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public bool IncludePrereleaseVersionsInUpdateCheck { get; set; } = false; 

    public bool RememberWindowSizeAndPosition { get; set; } = true;
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public string PythonExecutablePath { get; set; } = "python";
    public bool ParseImageIdMapAutomatically { get; set; } = true;
    public bool OpenWorkingFolderAfterExtraction { get; set; } = false;
    public string? DefaultOutputFolder { get; set; }
    public string? LastOpenedMcfPath { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            AutoCheckUpdatesOnStartup = AutoCheckUpdatesOnStartup,
            IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseVersionsInUpdateCheck,
            RememberWindowSizeAndPosition = RememberWindowSizeAndPosition,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            PythonExecutablePath = PythonExecutablePath,
            ParseImageIdMapAutomatically = ParseImageIdMapAutomatically,
            OpenWorkingFolderAfterExtraction = OpenWorkingFolderAfterExtraction,
            DefaultOutputFolder = DefaultOutputFolder,
            LastOpenedMcfPath = LastOpenedMcfPath
        };
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(PythonExecutablePath))
            PythonExecutablePath = "python";
        else
            PythonExecutablePath = PythonExecutablePath.Trim();

        if (string.IsNullOrWhiteSpace(DefaultOutputFolder))
            DefaultOutputFolder = null;
        else
            DefaultOutputFolder = DefaultOutputFolder.Trim();

        if (string.IsNullOrWhiteSpace(LastOpenedMcfPath))
            LastOpenedMcfPath = null;
        else
            LastOpenedMcfPath = LastOpenedMcfPath.Trim();
    }
}
