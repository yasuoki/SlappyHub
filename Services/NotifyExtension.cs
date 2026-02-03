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
    public static void Init(string path)
    {
        _logFile = Path.Combine(Path.GetDirectoryName(path)??"", LOG_FILE_NAME);
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
    public void Start()
    {
        string exePath = Application.ExecutablePath;
        Debug.WriteLine($"Starting NotifyExtension from {exePath}");
        var jsFile = Path.Combine(Path.GetDirectoryName(exePath)??"", EXTENSION_FILE_NAME);
        if (!File.Exists(jsFile))
        {
            jsFile = Path.Combine("C:\\usr\\bin\\", EXTENSION_FILE_NAME);
            if (!File.Exists(jsFile))
            {
                jsFile = Path.Combine("D:\\usr\\bin\\", EXTENSION_FILE_NAME);
                if (!File.Exists(jsFile))
                    return;
            }
        }
        Debug.WriteLine($"extension script file {jsFile}");
        Log.Init(jsFile);
        _engine = new V8ScriptEngine(
            V8ScriptEngineFlags.EnableTaskPromiseConversion
            | V8ScriptEngineFlags.EnableDynamicModuleImports);
        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
        _engine.AddHostType(typeof(Log));
        _engine.AddHostType(typeof(NotificationEvent));
        _engine.AddHostType("ViewChangeEvent",typeof(ViewChangeEvent));
        try
        {
            string jsCode = File.ReadAllText(jsFile);
            _engine.Execute(new DocumentInfo(jsFile)
            {
                Category = ModuleCategory.CommonJS
            }, jsCode);
        }
        catch (Exception e)
        {
            var se = e.InnerException as ScriptEngineException;
            if (se != null )
            {
                Log.print(se.ErrorDetails);
            }
            else
            {
                Log.print(e.Message);
            }
        }
    }

    public NotificationEvent? OnNotify(string app, string title, string body)
    {
        if (_engine != null && !(_engine.Script.onNotify is Undefined))
        {
            try
            {
                var ret = _engine.Script.onNotify(app, title, body);
                return ret as NotificationEvent;
            }
            catch (Exception e)
            {
                var se = e.InnerException as ScriptEngineException;
                if (se != null )
                {
                    Log.print(se.ErrorDetails);
                }
                else
                {
                    Log.print(e.Message);
                }
            }
        }
        return null;
    }
    
    public ViewChangeEvent? OnForeground(string process, string title)
    {
        if (_engine != null && !(_engine.Script.onForeground is Undefined))
        {
            try
            {
                var ret = _engine.Script.onForeground(process, title);
                return ret as ViewChangeEvent;
            }
            catch (Exception e)
            {
                var se = e.InnerException as ScriptEngineException;
                if (se != null )
                {
                    Log.print(se.ErrorDetails);
                }
                else
                {
                    Log.print(e.Message);
                }
            }
        }
        return null;
    }
    
    public ViewChangeEvent? OnTitleChange(string process, string title)
    {
        if (_engine != null && !(_engine.Script.onTitleChange is Undefined))   
        {
            try
            {
                var ret = _engine.Script.onTitleChange(process, title);
                return ret as ViewChangeEvent;
            }
            catch (Exception e)
            {
                var se = e.InnerException as ScriptEngineException;
                if (se != null )
                {
                    Log.print(se.ErrorDetails);
                }
                else
                {
                    Log.print(e.Message);
                }
            }
        }
        return null;
    }
}

