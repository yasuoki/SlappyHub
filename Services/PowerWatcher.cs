using Microsoft.Win32;
using System.Diagnostics;

namespace SlappyHub.Services;

public class PowerWatcher
{
    private bool _isRunning = false;
    public event EventHandler? SystemSuspend;
    public event EventHandler? SystemResume;

    public PowerWatcher()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    public void Start()
    {
        _isRunning = true;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        Debug.WriteLine($"Power mode changed: {e.Mode}");
        if (!_isRunning)
            return;
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                SystemSuspend?.Invoke(this, EventArgs.Empty);
                break;

            case PowerModes.Resume:
                SystemResume?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        Debug.WriteLine("Windows session ending");
        SystemSuspend?.Invoke(this, EventArgs.Empty);
    }
}