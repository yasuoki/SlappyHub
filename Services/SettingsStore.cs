using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SlappyHub.Services;

public class SettingsStore
{
	private AppSettings? _settings;
	public event EventHandler<AppSettings>? Changed;
	
	private readonly string _path =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"SlappyHub", "settings.json");

	public SettingsStore()
	{
	}
	
	public AppSettings Settings
	{
		get
		{
			if(_settings == null) 
				_settings = Load();
			return _settings;
		}
	}

	public AppSettings Load()
	{
		try
		{
			if (!File.Exists(_path)) return new AppSettings();
			var setting =  JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
			return setting;
		}
		catch
		{
			return new AppSettings();
		}
	}

	public void Save(AppSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
		File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
	}
	
	public void Update(Func<AppSettings, AppSettings> update)
	{
		while (true)
		{
			var oldValue = Volatile.Read(ref _settings);
			var newValue = update(oldValue);
			var exchanged = Interlocked.CompareExchange(ref _settings, newValue, oldValue);
			if (ReferenceEquals(exchanged, oldValue))
			{
				Save(newValue);
				Changed?.Invoke(this, newValue);
				return;
			}
		}
	}
	
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SlappyBellHub:v1");

	public static string ProtectString(string plainText)
	{
		var bytes = Encoding.UTF8.GetBytes(plainText);
		var protectedBytes = ProtectedData.Protect(
			bytes,
			Entropy,
			DataProtectionScope.CurrentUser);

		return Convert.ToBase64String(protectedBytes);
	}

	public static string UnprotectString(string protectedBase64)
	{
		var protectedBytes = Convert.FromBase64String(protectedBase64);
		var bytes = ProtectedData.Unprotect(
			protectedBytes,
			Entropy,
			DataProtectionScope.CurrentUser);

		return Encoding.UTF8.GetString(bytes);
	}
}