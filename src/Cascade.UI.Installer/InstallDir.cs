namespace Cascade.UI.Installer;

public abstract class InstallDir
{
    public abstract string Resolve(string appName);

    public static InstallDir ProgramFiles(string appName) => new ProgramFilesDir(appName);
    public static InstallDir ProgramFilesX86(string appName) => new ProgramFilesX86Dir(appName);
    public static InstallDir UserProfile(string appName) => new UserProfileDir(appName);
    public static InstallDir AppData(string appName) => new AppDataDir(appName);
    public static InstallDir LocalAppData(string appName) => new LocalAppDataDir(appName);
    public static InstallDir Custom(string path) => new CustomDir(path);

    private sealed class ProgramFilesDir(string appName) : InstallDir
    {
        public override string Resolve(string name) => $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\{appName}";
    }

    private sealed class ProgramFilesX86Dir(string appName) : InstallDir
    {
        public override string Resolve(string name) => $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\{appName}";
    }

    private sealed class UserProfileDir(string appName) : InstallDir
    {
        public override string Resolve(string name) => $@"{Environment.GetEnvironmentVariable("USERPROFILE") ?? "."}\{appName}";
    }

    private sealed class AppDataDir(string appName) : InstallDir
    {
        public override string Resolve(string name) => $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\{appName}";
    }

    private sealed class LocalAppDataDir(string appName) : InstallDir
    {
        public override string Resolve(string name) => $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\{appName}";
    }

    private sealed class CustomDir(string path) : InstallDir
    {
        public override string Resolve(string name) => path;
    }
}

public static class Dir
{
    public static string App => "[APP]";
    public static string AppFile(string name) => $"[APP]/{name}";
    public static string AppSubdir(string name) => $"[APP]/{name}";
    public static string Temp => "[TEMP]";
    public static string TempFile(string name) => $"[TEMP]/{name}";
    public static string AppData => "[APPDATA]";
    public static string AppDataFile(string name) => $"[APPDATA]/{name}";
    public static string LocalAppData => "[LOCALAPPDATA]";
    public static string LocalAppDataFile(string name) => $"[LOCALAPPDATA]/{name}";
    public static string ProgramData => "[PROGRAMDATA]";
    public static string ProgramDataFile(string name) => $"[PROGRAMDATA]/{name}";
    public static string Desktop => "[DESKTOP]";
    public static string StartMenu => "[STARTMENU]";
}
