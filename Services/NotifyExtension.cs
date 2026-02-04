using System.Diagnostics;
using System.IO;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using SlappyHub.Models;

namespace SlappyHub.Services;

public class Log
{
    private static readonly string LOG_FILE_NAME = "slappy_hub.log";
    private static string _logFile;
    private static TextWriter? _writer;

    public static void Close()
    {
        if (_writer != null)
        {
            _writer.Close();
            _writer.Dispose();
            _writer = null;
        }
        _logFile = LOG_FILE_NAME;

    }
    public static void SetLogDirectory(string basePath)
    {
        Close();
        _logFile = Path.Combine(Path.GetDirectoryName(basePath)??"", LOG_FILE_NAME);
    }
    
    public static void print(string message)
    {
        if (_writer == null)
        {
            if (!File.Exists(_logFile))
            {
                File.CreateText(_logFile).Close();
            }
            _writer = File.AppendText(_logFile);
        }
        _writer.WriteLine(message);
        _writer.Flush();
    }
}

public class NotifyExtension
{
    static readonly string EXTENSION_FILE_NAME = "slappy_extension.js";
    private V8ScriptEngine? _engine;
    private readonly object _lock = new object();
    private string? _jsFile;
    private DateTime? _jsFileTime;
    
    public NotifyExtension()
    {
        string exePath = Application.ExecutablePath;
        _jsFile = Path.Combine("D:\\usr\\bin\\", EXTENSION_FILE_NAME);
        if (!File.Exists(_jsFile))
        {
            _jsFile = Path.Combine("C:\\usr\\bin\\", EXTENSION_FILE_NAME);
            if (!File.Exists(_jsFile))
            {
                _jsFile = Path.Combine(Path.GetDirectoryName(exePath)??"", EXTENSION_FILE_NAME);
            }
        }
        Log.SetLogDirectory(_jsFile);
    }
    
    private void LogError(Exception e)
    {
        if (e.InnerException is ScriptEngineException se )
        {
            Log.print(se.ErrorDetails);
        }
        else
        {
            Log.print(e.Message);
        }
    }
    public void Start()
    {
        lock (_lock)
        {
            Debug.WriteLine($"loading extension script file {_jsFile}");
            LoadScript();
        }
    }

    private void LoadScript()
    {
        if (_engine != null)
        {
            Log.Close();
            _engine.Dispose();
            _engine = null;
        }

        if (!File.Exists(_jsFile))
        {
            _jsFileTime = null;
            return;
        }
        
        _jsFileTime = File.GetLastWriteTime(_jsFile);
        _engine = new V8ScriptEngine(
            V8ScriptEngineFlags.EnableTaskPromiseConversion
            | V8ScriptEngineFlags.EnableDynamicModuleImports);
        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
        _engine.AddHostType(typeof(Log));
        _engine.AddHostType(typeof(NotificationEvent));
        _engine.AddHostType("ViewChangeEvent",typeof(ViewChangeEvent));
        try
        {
            string jsCode = File.ReadAllText(_jsFile);
            _engine.Execute(new DocumentInfo(_jsFile)
            {
                Category = ModuleCategory.CommonJS
            }, jsCode);
        }
        catch (Exception e)
        {
            LogError(e);
            _engine.Dispose();
            _engine = null;
        }
    }

    private void ReloadScript()
    {
        if (_jsFileTime != null)
        {
            if (File.Exists(_jsFile))
            {
                var ft = File.GetLastWriteTime(_jsFile);
                if (ft > _jsFileTime)
                {
                    Debug.WriteLine($"reloading extension script file {_jsFile}");
                    LoadScript();
                }
            }
            else
            {
                Debug.WriteLine($"stop extension script");
                _jsFileTime = null;
                _engine?.Dispose();
                _engine = null;
            }
        }
        else
        {
            if (File.Exists(_jsFile))
            {
                Debug.WriteLine($"loading extension script file {_jsFile}");
                LoadScript();
            }
        }
    }

    public NotificationEvent? OnNotify(string app, string title, string body)
    {
        lock (_lock)
        {
            ReloadScript();
            if (_engine != null && !(_engine.Script.onNotify is Undefined))
            {
                try
                {
                    var ret = _engine.Script.onNotify(app, title, body);
                    return ret as NotificationEvent;
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        return null;
    }
    
    public ViewChangeEvent? OnForeground(string process, string title)
    {
        lock (_lock)
        {
            ReloadScript();
            if (_engine != null && !(_engine.Script.onForeground is Undefined))
            {
                try
                {
                    var ret = _engine.Script.onForeground(process, title);
                    return ret as ViewChangeEvent;
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        return null;
    }
    
    public ViewChangeEvent? OnTitleChange(string process, string title)
    {
        lock (_lock)
        {
            ReloadScript();
            if (_engine != null && !(_engine.Script.onTitleChange is Undefined))
            {
                try
                {
                    var ret = _engine.Script.onTitleChange(process, title);
                    return ret as ViewChangeEvent;
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        return null;
    }
}

