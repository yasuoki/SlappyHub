using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using SlappyHub.Common;
using SlappyHub.Services;
using LinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using System.Windows;
using Microsoft.Win32;

namespace SlappyHub.ViewModels;

public sealed class NotifySettingsViewModel : INotifyPropertyChanged
{
	private SlappyDevice? _slappyDevice;

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	// =========================
	// Message Binding
	// =========================
	private bool _isDirectMessage;

	public bool IsDirectMessage
	{
		get => _isDirectMessage;
		set
		{
			if (_isDirectMessage == value) return;
			_isDirectMessage = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsChannelMode));
			OnPropertyChanged(nameof(IsDmMode));
			OnPropertyChanged(nameof(ResolvedChannelValue));
		}
	}

	public bool IsChannelMode => !IsDirectMessage;
	public bool IsDmMode => IsDirectMessage;

	private string _channelName = "";

	public string ChannelName
	{
		get => _channelName;
		set
		{
			if (_channelName == value) return;
			_channelName = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(ResolvedChannelValue));
		}
	}

	private string _directMessagePeer = "";

	public string DirectMessagePeer
	{
		get => _directMessagePeer;
		set
		{
			if (_directMessagePeer == value) return;
			_directMessagePeer = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(ResolvedChannelValue));
		}
	}

	public string ResolvedChannelValue
		=> IsDirectMessage
			? $"[DM]{(DirectMessagePeer ?? "").Trim()}"
			: (ChannelName ?? "").Trim();

	// =========================
	// LED Pattern
	// =========================
	public ObservableCollection<LedPatternPreset> LedPatterns { get; } = new();

	private LedPatternPreset? _selectedLedPattern;

	public LedPatternPreset? SelectedLedPattern
	{
		get => _selectedLedPattern;
		set
		{
			if (Equals(_selectedLedPattern, value)) return;
			_selectedLedPattern = value;
			OnPropertyChanged();

			// 互換：保存用のLedPattern文字列も更新
			LedPattern = _selectedLedPattern?.Id ?? "";
		}
	}

	private string _ledPattern = "";

	public string LedPattern
	{
		get => _ledPattern;
		set
		{
			if (_ledPattern == value) return;
			_ledPattern = value;
			OnPropertyChanged();
		}
	}

	// =========================
	// Sound
	// =========================
	private readonly MediaPlayer _player = new();
	public ObservableCollection<SoundChoice> AvailableSounds { set; get; } = new();
	private SoundChoice? _selectedSound;

	public SoundChoice? SelectedSound
	{
		get => _selectedSound;
		set
		{
			if (Equals(_selectedSound, value)) return;
			_selectedSound = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsUrlSoundSelected));
			OnPropertyChanged(nameof(IsUploadSoundSelected));
			OnPropertyChanged(nameof(IsDeviceSoundSelected));
			OnPropertyChanged(nameof(CanPlayDeviceSound));
			OnPropertyChanged(nameof(CanPlaySoundUrl));
			OnPropertyChanged(nameof(CanPlayUploadSoundFile));
			SyncSoundToString();
		}
	}

	private string _sound = "";

	public string Sound
	{
		get => _sound;
		set
		{
			if (_sound == value) return;
			_sound = value;
			OnPropertyChanged();
		}
	}

	public bool IsUrlSoundSelected => SelectedSound?.Kind == SoundChoiceKind.Url;
	public bool IsUploadSoundSelected => SelectedSound?.Kind == SoundChoiceKind.Upload;
	public bool IsDeviceSoundSelected => SelectedSound?.Kind == SoundChoiceKind.DeviceFile;

	public bool CanPlayDeviceSound
	{
		get
		{
			if (SelectedSound?.Kind == SoundChoiceKind.DeviceFile)
			{
				var fileName = (SelectedSound?.Value ?? "").Trim();
				return !string.IsNullOrEmpty(fileName) && _slappyDevice != null;
			}

			return false;
		}
	}

	private async Task PlayDeviceSound()
	{
		if (SelectedSound?.Kind == SoundChoiceKind.DeviceFile)
		{
			var fileName = (SelectedSound?.Value ?? "").Trim();
			if (_slappyDevice != null)
			{
				await _slappyDevice.Play(fileName);
			}
		}
	}

	private async Task DeleteDeviceSoundFile()
	{
		if (SelectedSound?.Kind == SoundChoiceKind.DeviceFile)
		{
			var fileName = (SelectedSound?.Value ?? "").Trim();
			if (_slappyDevice != null)
			{
				var ret = await _slappyDevice.Remove(fileName);
				if (ret.Code == ReceiveMessage.ResultCode.Success)
				{
					AvailableSounds.Remove(AvailableSounds.First(x => x.Value == fileName));
					SelectedSound = AvailableSounds[0];
					OnPropertyChanged(nameof(AvailableSounds));
					OnPropertyChanged(nameof(SelectedSound));
				}
			}
		}
	}

	private string _uploadSoundFilePath = "";

	public string UploadSoundFilePath
	{
		get => _uploadSoundFilePath;
		set
		{
			if (_uploadSoundFilePath == value) return;
			_uploadSoundFilePath = value;
			OnPropertyChanged(nameof(UploadSoundFilePath));
			OnPropertyChanged(nameof(CanPlayUploadSoundFile));
		}
	}

	private string _uploadDeviceSoundFile = "";

	public string UploadDeviceSoundFile
	{
		get => _uploadDeviceSoundFile;
		set
		{
			if (_uploadDeviceSoundFile == value) return;
			_uploadDeviceSoundFile = value;
			OnPropertyChanged(nameof(UploadDeviceSoundFile));
			OnPropertyChanged(nameof(CanPlayUploadSoundFile));
		}
	}

	public bool CanPlayUploadSoundFile => !string.IsNullOrWhiteSpace(UploadSoundFilePath) &&
	                                      Path.GetExtension(UploadSoundFilePath).ToLower() == ".mp3" &&
	                                      File.Exists(UploadSoundFilePath);

	private void BrowseUploadSoundFile()
	{
		var dlg = new OpenFileDialog
		{
			Title = "MP3ファイルを選択",
			Filter = "MP3 (*.mp3)|*.mp3|すべてのファイル (*.*)|*.*",
			DefaultExt = ".mp3",
			CheckFileExists = true,
			Multiselect = false,
			InitialDirectory = !string.IsNullOrWhiteSpace(UploadSoundFilePath) && File.Exists(UploadSoundFilePath)
				? Path.GetDirectoryName(UploadSoundFilePath)
				: Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
		};

		if (dlg.ShowDialog() == true)
		{
			UploadSoundFilePath = dlg.FileName;
			UploadDeviceSoundFile = Path.GetFileName(UploadSoundFilePath);
		}
	}

	private void PlayUploadSoundFile()
	{
		var filePath = UploadSoundFilePath.Trim();
		if (!string.IsNullOrEmpty(filePath))
		{
			_player.Stop();
			_player.Open(new Uri(filePath!, UriKind.Absolute));
			_player.Volume = 1.0; // 0.0〜1.0
			_player.Position = TimeSpan.Zero;
			_player.Play();
		}
	}

	private static readonly Regex AllowedFileNameCharactor =
		new(@"^(?=.{1,31}$)[A-Za-z0-9\-\+\._ #]+$",
			RegexOptions.Compiled);

	private bool ValidateFileName(string? name, out string error)
	{
		name ??= "";

		if (name.Length == 0)
		{
			error = "名前を入力してください。";
			return false;
		}

		if (name.Length > 31)
		{
			error = "名前は31文字以下にしてください。";
			return false;
		}

		if (!AllowedFileNameCharactor.IsMatch(name))
		{
			error = "使用できるのは英数字、- + . _（スペース）# のみです。";
			return false;
		}

		error = "";
		return true;
	}

	private double _uploadPercent;

	public double UploadPercent
	{
		get => _uploadPercent;
		set
		{
			if (_uploadPercent == value) return;
			_uploadPercent = value;
			OnPropertyChanged(nameof(UploadPercent));
		}
	}

	private string _uploadStatus = "";

	public string UploadStatus
	{
		get => _uploadStatus;
		set
		{
			if (_uploadStatus == value) return;
			_uploadStatus = value;
			OnPropertyChanged(nameof(UploadStatus));
		}
	}

	private bool _isUploading;

	public bool IsUploading
	{
		get => _isUploading;
		set
		{
			if (_isUploading == value) return;
			_isUploading = value;
			OnPropertyChanged(nameof(IsUploading));
		}
	}

	private async Task UploadSoundFile()
	{
		if (IsUploading) return;
		var local = UploadSoundFilePath.Trim();
		var remote = UploadDeviceSoundFile.Trim();
		if (string.IsNullOrEmpty(local) || !File.Exists(local) || string.IsNullOrEmpty(remote))
			return;
		if (!ValidateFileName(remote, out var error))
		{
			MessageBox.Show(error);
			return;
		}

		if (_slappyDevice == null)
		{
			MessageBox.Show("SlappyBellデバイスが接続していません");
			return;
		}

		try
		{
			IsUploading = true;
			UploadPercent = 0;
			UploadStatus = "準備中…";

			// local はバリデーション済みとのことなので読み込みのみ
			byte[] data = await File.ReadAllBytesAsync(local);

			var progress = new Progress<TransferProgress>(p =>
			{
				UploadPercent = Math.Clamp(p.Ratio * 100, 0, 100);
				UploadStatus = $"{p.SentBytes:n0} / {p.TotalBytes:n0} bytes";
			});

			UploadStatus = "アップロード中…";

			try
			{
				ReceiveMessage result = await _slappyDevice.Upload(remote, data, progress);
				if (result.Code != ReceiveMessage.ResultCode.Success)
				{
					MessageBox.Show($"アップロードに失敗しました: {result.Message}");
					return;
				}

				UploadPercent = 100;
				UploadStatus = "完了";
				await Task.Delay(600);
				await UpdateDeviceSoundFileList();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"アップロードに失敗しました: {ex.Message}");
			}
		}
		catch (Exception ex)
		{
			UploadStatus = $"失敗: {ex.Message}";
			// ここでMessageBox/Toastなどに出すならUI側で
		}
		finally
		{
			IsUploading = false;
		}
	}

	private string _soundUrl = "";

	public string SoundUrl
	{
		get => _soundUrl;
		set
		{
			if (_soundUrl == value) return;
			_soundUrl = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(CanPlaySoundUrl));
			if (IsUrlSoundSelected) SyncSoundToString();
		}
	}

	public bool CanPlaySoundUrl => !string.IsNullOrWhiteSpace(SoundUrl) && SoundUrl.StartsWith("http://") &&
	                               SoundUrl.ToLower().EndsWith(".mp3") && _slappyDevice != null;

	private async Task PlaySoundUrl()
	{
		var url = SoundUrl.Trim();
		if (!string.IsNullOrEmpty(url))
		{
			var r = await _slappyDevice.Play(url);
			if (r.Code != ReceiveMessage.ResultCode.Success)
				MessageBox.Show("Failed to play sound: " + r.Message);
		}
	}

	private async Task UpdateDeviceSoundFileList(bool saveCurrentSelection = true)
	{
		await Application.Current.Dispatcher.InvokeAsync(async () =>
		{
			var save = SelectedSound;

			AvailableSounds.Clear();
			AvailableSounds.Add(SoundChoice.None());
			if (_slappyDevice != null)
			{
				var r = await _slappyDevice.List(TimeSpan.FromSeconds(5));
				if (r.Code == ReceiveMessage.ResultCode.Success)
				{
					var storageSize = 0;
					var storageUsage = 0;
					List<string> files = new();
					var lines = r.Body.Split('\n');
					foreach (var line in lines)
					{
						if (line.StartsWith("Storage Usage:"))
						{
							var seg = line.Substring("Storage Usage:".Length).Trim().Split("/");
							if (seg.Length == 2)
							{
								storageUsage = int.Parse(seg[0].Trim());
								storageSize = int.Parse(seg[1].Trim());
								Debug.WriteLine($"strorage size: {storageSize}, usage: {storageUsage}");
							}
						}
						else if (line.StartsWith("Files:"))
						{
							// do nothing
							Debug.WriteLine("avalable files");
						}
						else
						{
							var p = line.LastIndexOf(" ");
							var fileName = line.Substring(0, p).Trim();
							files.Add(fileName);
							Debug.WriteLine($"file: {fileName}");
						}
					}

					foreach (var file in files)
						AvailableSounds.Add(SoundChoice.DeviceFile(file));
				}
			}

			AvailableSounds.Add(SoundChoice.UploadOption());
			AvailableSounds.Add(SoundChoice.UrlOption());
			if (saveCurrentSelection)
			{
				SelectedSound = AvailableSounds.FirstOrDefault(x =>
					                x.Kind == save.Kind && x.DisplayName == save.DisplayName && x.Value == save.Value)
				                ?? AvailableSounds.First(); // なければ(なし)
			}

			OnPropertyChanged(nameof(AvailableSounds));
		});
	}

	// =========================
	// SenderFilter / TextFilter（YouTube風タグ入力）
	// =========================
	public ObservableCollection<string> SenderFilterTokens { get; } = new();
	public ObservableCollection<string> TextFilterTokens { get; } = new();
	private int _slotIndex;

	public int SlotIndex
	{
		get => _slotIndex;
		private set
		{
			if (_slotIndex == value) return;
			_slotIndex = value;
			OnPropertyChanged();
		}
	}

	private string _newSenderTokenText = "";

	public string NewSenderTokenText
	{
		get => _newSenderTokenText;
		set
		{
			if (_newSenderTokenText == value) return;
			_newSenderTokenText = value;
			OnPropertyChanged();
		}
	}

	private string _newTextTokenText = "";

	public string NewTextTokenText
	{
		get => _newTextTokenText;
		set
		{
			if (_newTextTokenText == value) return;
			_newTextTokenText = value;
			OnPropertyChanged();
		}
	}

	private string _senderFilter = "";

	public string SenderFilter
	{
		get => _senderFilter;
		set
		{
			if (_senderFilter == value) return;
			_senderFilter = value;
			OnPropertyChanged();

			ReplaceAll(SenderFilterTokens, ParseCsvTokens(_senderFilter));
		}
	}

	private string _textFilter = "";

	public string TextFilter
	{
		get => _textFilter;
		set
		{
			if (_textFilter == value) return;
			_textFilter = value;
			OnPropertyChanged();

			ReplaceAll(TextFilterTokens, ParseCsvTokens(_textFilter));
		}
	}

	public void CommitSenderToken()
	{
		CommitTokenCore(SenderFilterTokens, NewSenderTokenText,
			setNewText: v => NewSenderTokenText = v,
			setCsv: csv => SenderFilter = csv);
	}

	public void CommitTextToken()
	{
		CommitTokenCore(TextFilterTokens, NewTextTokenText,
			setNewText: v => NewTextTokenText = v,
			setCsv: csv => TextFilter = csv);
	}

	public void RemoveLastSenderToken()
	{
		RemoveLastToken(SenderFilterTokens);
		SenderFilter = BuildCsv(SenderFilterTokens);
	}

	public void RemoveLastTextToken()
	{
		RemoveLastToken(TextFilterTokens);
		TextFilter = BuildCsv(TextFilterTokens);
	}

	// =========================
	// Sound Manager（UI雛形用）
	// =========================
	private bool _isSoundManagerOpen;

	public bool IsSoundManagerOpen
	{
		get => _isSoundManagerOpen;
		set
		{
			if (_isSoundManagerOpen == value) return;
			_isSoundManagerOpen = value;
			OnPropertyChanged();
		}
	}

	private long _storageTotalBytes = 1;

	public long StorageTotalBytes
	{
		get => _storageTotalBytes;
		set
		{
			var v = Math.Max(1, value);
			if (_storageTotalBytes == v) return;
			_storageTotalBytes = v;
			OnPropertyChanged();
			OnPropertyChanged(nameof(StorageSummary));
		}
	}

	private long _storageUsedBytes;

	public long StorageUsedBytes
	{
		get => _storageUsedBytes;
		set
		{
			var v = Math.Max(0, value);
			if (_storageUsedBytes == v) return;
			_storageUsedBytes = v;
			OnPropertyChanged();
			OnPropertyChanged(nameof(StorageSummary));
		}
	}

	public string StorageSummary
		=>
			$"使用量: {ToSize(StorageUsedBytes)} / 合計: {ToSize(StorageTotalBytes)}（空き: {ToSize(StorageTotalBytes - StorageUsedBytes)}）";

	private string _soundManagerStatusText = "";

	public string SoundManagerStatusText
	{
		get => _soundManagerStatusText;
		set
		{
			if (_soundManagerStatusText == value) return;
			_soundManagerStatusText = value;
			OnPropertyChanged();
		}
	}

	private string _selectedLedPatternId = "1"; // 例：初期値

	public string SelectedLedPatternId
	{
		get => _selectedLedPatternId;
		set
		{
			if (_selectedLedPatternId == value) return;
			_selectedLedPatternId = value;
			OnPropertyChanged();
		}
	}

	private void OnOk()
	{
		RequestClose?.Invoke(this, true);
	}

	private void OnCancel()
	{
		RequestClose?.Invoke(this, false);
	}

	// =========================
	// Commands / events
	// =========================
	public ICommand ApplyCommand { get; }
	public ICommand CloseCommand { get; }
	public ICommand OkCommand { get; }
	public ICommand CancelCommand { get; }

	public ICommand PlayDeviceSoundCommand { get; }
	public ICommand DeleteDeviceSoundFileCommand { get; }
	public ICommand BrowseUploadSoundFileCommand { get; }
	public ICommand PlayUploadSoundFileCommand { get; }

	public ICommand PlaySoundUrlCommand { get; }
	public ICommand UploadSoundFileCommand { get; }

	public ICommand UploadSoundCommand { get; }

	public ICommand RemoveSenderTokenCommand { get; }
	public ICommand RemoveTextTokenCommand { get; }

	public event EventHandler<bool>? RequestClose;
	public event EventHandler? RequestPickUploadFile;

	private static LinearGradientBrush Smooth(params (double offset, System.Windows.Media.Color color)[] stops)
	{
		var b = new System.Windows.Media.LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0, 0.5),
			EndPoint = new System.Windows.Point(1, 0.5)
		};
		foreach (var (o, c) in stops)
			b.GradientStops.Add(new System.Windows.Media.GradientStop(c, o));
		b.Freeze();
		return b;
	}

	private static LinearGradientBrush Step(
		double split,
		System.Windows.Media.Color left,
		System.Windows.Media.Color right)
	{
		var b = new System.Windows.Media.LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0, 0.5),
			EndPoint = new System.Windows.Point(1, 0.5)
		};

		// 左は split まで
		b.GradientStops.Add(new System.Windows.Media.GradientStop(left, 0.0));
		b.GradientStops.Add(new System.Windows.Media.GradientStop(left, split));

		// split から右へ（同じoffsetに2個置くと段差）
		b.GradientStops.Add(new System.Windows.Media.GradientStop(right, split));
		b.GradientStops.Add(new System.Windows.Media.GradientStop(right, 1.0));

		b.Freeze();
		return b;
	}


	public NotifySettingsViewModel(SlappyBellController slappyBellController)
	{
		// Apply/Cancel/Close は Slack設定と同じく親側で処理する想定。
		OkCommand = new RelayCommand(OnOk);
		CancelCommand = new RelayCommand(OnCancel);

		// LED presets（例）
		var pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 1000, LinkType.Smooth);
		pat.AddSegment(0x00ff00, 1000, LinkType.Smooth);
		pat.AddSegment(0x0000ff, 1000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000>00ff00>0000ff>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 2000, LinkType.Smooth);
		pat.AddSegment(0x00ff00, 2000, LinkType.Smooth);
		pat.AddSegment(0x0000ff, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000:2000>00ff00:2000>0000ff:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 1000, LinkType.Smooth);
		pat.AddSegment(0x00ff00, 1000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000>00ff00>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 2000, LinkType.Smooth);
		pat.AddSegment(0x00ff00, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000:2000>00ff00:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 1000, LinkType.Smooth);
		pat.AddSegment(0x0000ff, 1000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ff00>0000ff>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 2000, LinkType.Smooth);
		pat.AddSegment(0x0000ff, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ff00:2000>0000ff:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x0000ff, 1000, LinkType.Smooth);
		pat.AddSegment(0xff0000, 1000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("0000ff>ff0000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x0000ff, 2000, LinkType.Smooth);
		pat.AddSegment(0xff0000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("0000ff:2000>ff0000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000>000000:100>,", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff0000:2000>000000:2000>,", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ff00>000000:100>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ff00:2000>000000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x0000ff, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("0000ff>000000:100>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x0000ff, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("0000ff:2000>000000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xffff00, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ffff00>000000:100>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xffff00, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ffff00:2000>000000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ffff, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ffff>000000:100>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ffff, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("00ffff:2000>000000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff00ff, 1000, LinkType.Smooth);
		pat.AddSegment(0x000000, 100, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff00ff>000000:100>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff00ff, 2000, LinkType.Smooth);
		pat.AddSegment(0x000000, 2000, LinkType.Smooth);
		LedPatterns.Add(new LedPatternPreset("ff00ff:2000>000000:2000>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0xff0000, 600, LinkType.Step);
		pat.AddSegment(0x0000ff, 600, LinkType.Step);
		LedPatterns.Add(new LedPatternPreset("ff0000:600>0000ff:600>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 600, LinkType.Step);
		pat.AddSegment(0x0000ff, 600, LinkType.Step);
		LedPatterns.Add(new LedPatternPreset("00ff00:600>0000ff:600>", "", pat.Build()));
		pat = new LedPresetBuilder();
		pat.AddSegment(0x00ff00, 600, LinkType.Step);
		pat.AddSegment(0xff0000, 600, LinkType.Step);
		LedPatterns.Add(new LedPatternPreset("00ff00:600>ff0000:600>", "", pat.Build()));
		SelectedLedPattern = LedPatterns[0];

		// Sound choices（URL常設 + none。デバイスmp3は後でRefreshで追加）
		AvailableSounds.Add(SoundChoice.UploadOption());
		AvailableSounds.Add(SoundChoice.UrlOption());
		AvailableSounds.Add(SoundChoice.None());
		SelectedSound = AvailableSounds[0];

		PlayDeviceSoundCommand = new AsyncRelayCommand(async () => { await PlayDeviceSound(); });

		DeleteDeviceSoundFileCommand = new AsyncRelayCommand(async () => { await DeleteDeviceSoundFile(); });

		BrowseUploadSoundFileCommand = new RelayCommand(() => { BrowseUploadSoundFile(); });

		PlayUploadSoundFileCommand = new RelayCommand(() => { PlayUploadSoundFile(); });
		PlaySoundUrlCommand = new RelayCommand(async () => { await PlaySoundUrl(); });

		UploadSoundFileCommand = new AsyncRelayCommand(async () => { await UploadSoundFile(); });

		UploadSoundCommand = new RelayCommand(() => RequestPickUploadFile?.Invoke(this, EventArgs.Empty));

		RemoveSenderTokenCommand = new RelayCommand(p =>
		{
			if (p is string token)
			{
				RemoveToken(SenderFilterTokens, token);
				SenderFilter = BuildCsv(SenderFilterTokens);
			}
		});

		RemoveTextTokenCommand = new RelayCommand(p =>
		{
			if (p is string token)
			{
				RemoveToken(TextFilterTokens, token);
				TextFilter = BuildCsv(TextFilterTokens);
			}
		});

		slappyBellController.SlappyDeviceConnected += async (sender, device) =>
		{
			_slappyDevice = device;
			await UpdateDeviceSoundFileList();
		};
		slappyBellController.SlappyDeviceDisconnected += async (sender, device) =>
		{
			_slappyDevice = null;
			await UpdateDeviceSoundFileList();
		};

		RaiseAll();
	}

	// =========================
	// Slack設定と同じ：LoadFrom / ApplyTo
	// =========================
	public void LoadFrom(AppSettings s, int slotIndex)
	{
		SlotIndex = slotIndex;

		var ns = slotIndex switch
		{
			0 => s.Slot0,
			1 => s.Slot1,
			2 => s.Slot2,
			3 => s.Slot3,
			4 => s.Slot4,
			5 => s.Slot5,
			_ => null
		} ?? new NotifySettings();

		LoadNotifySettingsToEditor(ns);
		RaiseAll();
	}

	public AppSettings ApplyTo(AppSettings s)
	{
		var edited = new NotifySettings
		{
			Channel = NormalizeChannel(ResolvedChannelValue),
			Sound = NormalizeSoundString(Sound),
			LedPattern = (SelectedLedPattern?.Id ?? LedPattern ?? "").Trim(),
			SenderFilter = BuildCsv(SenderFilterTokens),
			TextFilter = BuildCsv(TextFilterTokens)
		};

		return SlotIndex switch
		{
			0 => s with { Slot0 = edited },
			1 => s with { Slot1 = edited },
			2 => s with { Slot2 = edited },
			3 => s with { Slot3 = edited },
			4 => s with { Slot4 = edited },
			5 => s with { Slot5 = edited },
			_ => s
		};
	}

	// =========================
	// Internal helpers
	// =========================
	private void LoadNotifySettingsToEditor(NotifySettings ns)
	{
		// Channel decode
		DecodeChannel(ns.Channel ?? "");

		// LED
		var ledId = (ns.LedPattern ?? "").Trim();
		SelectedLedPattern = FindLedPresetOrNull(ledId) ?? (LedPatterns.Count > 0 ? LedPatterns[0] : null);
		LedPattern = ledId;

		// Sound
		Sound = ns.Sound ?? "";
		DecodeSoundForUi(Sound);

		// Filters
		SenderFilter = ns.SenderFilter ?? "";
		TextFilter = ns.TextFilter ?? "";
		ReplaceAll(SenderFilterTokens, ParseCsvTokens(SenderFilter));
		ReplaceAll(TextFilterTokens, ParseCsvTokens(TextFilter));
		NewSenderTokenText = "";
		NewTextTokenText = "";
	}

	private void RaiseAll()
	{
		OnPropertyChanged(nameof(IsDirectMessage));
		OnPropertyChanged(nameof(IsChannelMode));
		OnPropertyChanged(nameof(IsDmMode));
		OnPropertyChanged(nameof(ChannelName));
		OnPropertyChanged(nameof(DirectMessagePeer));
		OnPropertyChanged(nameof(ResolvedChannelValue));

		OnPropertyChanged(nameof(SelectedLedPattern));
		OnPropertyChanged(nameof(LedPattern));

		OnPropertyChanged(nameof(SelectedSound));
		OnPropertyChanged(nameof(IsUrlSoundSelected));
		OnPropertyChanged(nameof(IsUploadSoundSelected));
		OnPropertyChanged(nameof(SoundUrl));
		OnPropertyChanged(nameof(Sound));

		OnPropertyChanged(nameof(SenderFilter));
		OnPropertyChanged(nameof(TextFilter));
		OnPropertyChanged(nameof(NewSenderTokenText));
		OnPropertyChanged(nameof(NewTextTokenText));

		OnPropertyChanged(nameof(IsSoundManagerOpen));
		OnPropertyChanged(nameof(StorageTotalBytes));
		OnPropertyChanged(nameof(StorageUsedBytes));
		OnPropertyChanged(nameof(StorageSummary));
		OnPropertyChanged(nameof(SoundManagerStatusText));
	}

	// Channel decode/normalize
	private void DecodeChannel(string channelValue)
	{
		channelValue = (channelValue ?? "").Trim();

		if (channelValue.StartsWith("[DM]", StringComparison.OrdinalIgnoreCase))
		{
			IsDirectMessage = true;
			DirectMessagePeer = channelValue.Substring(4);
			ChannelName = "";
		}
		else
		{
			IsDirectMessage = false;
			ChannelName = channelValue;
			DirectMessagePeer = "";
		}

		OnPropertyChanged(nameof(ResolvedChannelValue));
	}

	private static string NormalizeChannel(string value) => (value ?? "").Trim();

	private void DecodeSoundForUi(string soundValue)
	{
		var v = (soundValue ?? "").Trim();

		if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			SelectedSound = AvailableSounds.FirstOrDefault(x => x.Kind == SoundChoiceKind.Url) ?? SelectedSound;
			SoundUrl = v;
			return;
		}

		SoundUrl = "";
		if (!string.IsNullOrWhiteSpace(v))
		{
			SelectedSound = AvailableSounds.FirstOrDefault(x => x.Kind == SoundChoiceKind.DeviceFile && x.Value == v) ??
			                SelectedSound;
			return;
		}
		SelectedSound = AvailableSounds[0];
	}

	private void SyncSoundToString()
	{
		if (SelectedSound?.Kind == SoundChoiceKind.Url)
		{
			Sound = (SoundUrl ?? "").Trim();
		}
		else if (SelectedSound?.Kind == SoundChoiceKind.DeviceFile)
		{
			Sound = SelectedSound.Value ?? "";
		}
		else
		{
			Sound = "";
		}
	}

	private static string NormalizeSoundString(string value) => (value ?? "").Trim();

	// LED helper
	private LedPatternPreset? FindLedPresetOrNull(string id)
	{
		if (string.IsNullOrWhiteSpace(id)) return null;
		foreach (var p in LedPatterns)
			if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
				return p;
		return null;
	}

	// Tag helpers（Enter確定、カンマは文字、末尾カンマのみ除去）
	private static void CommitTokenCore(
		ObservableCollection<string> tokens,
		string newText,
		Action<string> setNewText,
		Action<string> setCsv)
	{
		var t = (newText ?? "").Trim();
		if (t.Length == 0) return;

		t = t.TrimEnd(',', '，').Trim();
		if (t.Length == 0)
		{
			setNewText("");
			return;
		}

		if (!tokens.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
			tokens.Add(t);

		setCsv(BuildCsv(tokens));
		setNewText("");
	}

	private static string[] ParseCsvTokens(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();

		raw = raw.Replace('，', ',');

		return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string BuildCsv(ObservableCollection<string> tokens)
		=> string.Join(", ", tokens);

	private static void ReplaceAll(ObservableCollection<string> target, string[] items)
	{
		target.Clear();
		foreach (var i in items) target.Add(i);
	}

	private static void RemoveToken(ObservableCollection<string> tokens, string? token)
	{
		if (string.IsNullOrWhiteSpace(token)) return;

		for (int i = 0; i < tokens.Count; i++)
		{
			if (string.Equals(tokens[i], token, StringComparison.OrdinalIgnoreCase))
			{
				tokens.RemoveAt(i);
				break;
			}
		}
	}

	private static void RemoveLastToken(ObservableCollection<string> tokens)
	{
		if (tokens.Count == 0) return;
		tokens.RemoveAt(tokens.Count - 1);
	}

	private static int ClampSlot(int i) => i < 0 ? 0 : (i > 5 ? 5 : i);

	private static string ToSize(long bytes)
	{
		if (bytes < 0) bytes = 0;
		string[] units = { "B", "KB", "MB", "GB" };
		double v = bytes;
		int idx = 0;
		while (v >= 1024 && idx < units.Length - 1)
		{
			v /= 1024;
			idx++;
		}

		return $"{v:0.##} {units[idx]}";
	}
}

// ---- UI表示用の小クラス/レコード ----
public sealed class SlotItem : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	public int Index { get; }
	public string DisplayName { get; }

	private bool _isSelected;

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (_isSelected == value) return;
			_isSelected = value;
			OnPropertyChanged();
		}
	}

	public SlotItem(int index, string displayName)
	{
		Index = index;
		DisplayName = displayName;
	}
}

public sealed class LedPatternPreset
{
	public string Id { get; }
	public string Name { get; }
	public LinearGradientBrush PreviewBrush { get; }

	public LedPatternPreset(string id, string name, LinearGradientBrush previewBrush)
	{
		Id = id;
		Name = name;
		PreviewBrush = previewBrush;
	}
}

public enum LinkType
{
	Step,
	Smooth
}

public sealed class LedPresetBuilder
{
	private sealed record Segment(Color Color, int Ms, LinkType Link);

	private readonly List<Segment> _segments = new();

	/// <summary>横幅に対応させるプレビュー時間（ms）。例：5000ms = 5秒。</summary>
	public int PreviewMs { get; set; } = 5000;

	/// <summary>プレビューが細かすぎる場合の安全弁（Stop数上限）。</summary>
	public int MaxStops { get; set; } = 400;

	public void AddSegment(uint rgb, uint ms, LinkType linkType)
	{
		_segments.Add(new Segment(ToColor(rgb), (int)Math.Max(1, ms), linkType));
	}

	public LinearGradientBrush Build()
	{
		if (_segments.Count == 0)
			return Solid(Colors.Transparent);

		int previewMs = Math.Max(1, PreviewMs);

		double Offset(int tMs) => Clamp01(tMs / (double)previewMs);

		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0.5),
			EndPoint = new Point(1, 0.5)
		};

		void AddStep(int atMs, Color from, Color to)
		{
			var o = Offset(atMs);
			brush.GradientStops.Add(new GradientStop(from, o));
			brush.GradientStops.Add(new GradientStop(to, o)); // 同offsetで段差
		}

		// 開始色（t=0）
		brush.GradientStops.Add(new GradientStop(_segments[0].Color, 0.0));

		int t = 0;
		int idx = 0;

		while (t < previewMs && brush.GradientStops.Count < MaxStops)
		{
			var cur = _segments[idx];
			var next = _segments[(idx + 1) % _segments.Count];

			// このセグメントの終了時刻（プレビュー窓でクリップ）
			int endT = t + cur.Ms;
			if (endT > previewMs) endT = previewMs;

			switch (cur.Link)
			{
				case LinkType.Smooth:
					// t→endT で next.Color へ滑らかに（終端StopだけでOK）
					brush.GradientStops.Add(new GradientStop(next.Color, Offset(endT)));
					break;

				case LinkType.Step:
				default:
					// cur.Color を endT まで保持
					brush.GradientStops.Add(new GradientStop(cur.Color, Offset(endT)));

					// まだ窓の中なら段差（パキッ）を入れる
					// endT == previewMs のときは入れても見えないので省略
					if (endT < previewMs)
						AddStep(endT, cur.Color, next.Color);
					break;
			}

			// 次へ
			t = endT;
			idx = (idx + 1) % _segments.Count;

			// もしセグメントがプレビュー窓で切られて終了したら終わり
			if (t >= previewMs) break;
		}

		// 念のため末端まで色があるように（Stop数不足対策）
		if (brush.GradientStops.Count == 1)
			brush.GradientStops.Add(new GradientStop(_segments[0].Color, 1.0));

		brush.Freeze();
		return brush;
	}

	// ---- helpers ----
	private static Color ToColor(uint rgb)
		=> Color.FromRgb(
			(byte)((rgb >> 16) & 0xFF),
			(byte)((rgb >> 8) & 0xFF),
			(byte)(rgb & 0xFF));

	private static LinearGradientBrush Solid(Color c)
	{
		var b = new LinearGradientBrush(c, c, 0);
		b.Freeze();
		return b;
	}

	private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}

public enum SoundChoiceKind
{
	None,
	DeviceFile,
	Url,
	Upload
}

public sealed class SoundChoice
{
	public SoundChoiceKind Kind { get; }
	public string DisplayName { get; }
	public string? Value { get; }

	private SoundChoice(SoundChoiceKind kind, string displayName, string? value)
	{
		Kind = kind;
		DisplayName = displayName;
		Value = value;
	}

	public static SoundChoice None() => new(SoundChoiceKind.None, "（なし）", null);
	public static SoundChoice UploadOption() => new(SoundChoiceKind.Upload, "新しいmp3ファイルをアップロード", null);
	public static SoundChoice UrlOption() => new(SoundChoiceKind.Url, "URLで指定（http://.../xxx.mp3）", null);
	public static SoundChoice DeviceFile(string fileName) => new(SoundChoiceKind.DeviceFile, fileName, fileName);
}

public sealed class DeviceSoundFileInfo
{
	public string FileName { get; init; } = "";
	public long SizeBytes { get; init; }
	public string SizeText => $"{SizeBytes / 1024.0:0.##} KB";
}