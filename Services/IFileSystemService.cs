namespace TelegramRAT.Services;

public interface IFileSystemService
{
    string GetCurrentDirectory();
    void SetCurrentDirectory(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string path);
    IEnumerable<string> EnumerateDirectories(string path);
    string GetFullPath(string path);
    string GetFileName(string path);
    void DeleteFile(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path);
    void MoveFile(string source, string destination);
    void CopyFile(string source, string destination);
    string GetTempPath();
    string SanitizePath(string pathInput);
    Stream OpenFileRead(string path);
    Stream OpenFileWrite(string path);
}

public class FileSystemService : IFileSystemService
{
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
    public void SetCurrentDirectory(string path) => Directory.SetCurrentDirectory(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    public IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path);
    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public string GetFileName(string path) => Path.GetFileName(path);
    public void DeleteFile(string path) => File.Delete(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void DeleteDirectory(string path) => Directory.Delete(path);
    public void MoveFile(string source, string destination) => File.Move(source, destination);
    public void CopyFile(string source, string destination) => File.Copy(source, destination);
    public string GetTempPath() => Path.GetTempPath();

    public string SanitizePath(string pathInput)
    {
        pathInput = pathInput?.Trim() ?? string.Empty;

        if (pathInput.Length >= 2)
        {
            if ((pathInput.StartsWith("\"") && pathInput.EndsWith("\"")) ||
                (pathInput.StartsWith("'") && pathInput.EndsWith("'")))
            {
                pathInput = pathInput[1..^1];
            }
        }

        return pathInput;
    }

    public Stream OpenFileRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    public Stream OpenFileWrite(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
}
