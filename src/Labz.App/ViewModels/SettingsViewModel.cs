using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Labz.App.Resources;
using Labz.Core.Project;

namespace Labz.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly UserSettings _settings;
    private readonly string _path;
    private readonly Action _changed;
    private string _autosaveText;
    private string _digitsText;
    private string? _error;

    public SettingsViewModel(UserSettings settings, string path, Action changed)
    {
        _settings = settings;
        _path = path;
        _changed = changed;
        _autosaveText = settings.AutosaveIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        _digitsText = settings.SignificantDigits.ToString(CultureInfo.InvariantCulture);
    }

    public string AutosaveLabel => Strings.AutosaveLabel;

    public string AutosaveHint => Strings.AutosaveHint;

    public string DigitsLabel => Strings.SignificantDigitsLabel;

    public string AutosaveText
    {
        get => _autosaveText;
        set
        {
            if (_autosaveText == value)
            {
                return;
            }

            _autosaveText = value;
            OnPropertyChanged();
            string? error = null;
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                || !UserSettings.TryAutosaveMinutes(minutes, out error))
            {
                Error = error ?? Strings.AutosaveHint;
                return;
            }

            Error = null;
            _settings.AutosaveIntervalMinutes = minutes;
            UserSettingsStore.Save(_path, _settings);
            _changed();
        }
    }

    public string DigitsText
    {
        get => _digitsText;
        set
        {
            if (_digitsText == value)
            {
                return;
            }

            _digitsText = value;
            OnPropertyChanged();
            string? error = null;
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var digits)
                || !UserSettings.TrySignificantDigits(digits, out error))
            {
                Error = error ?? Strings.AutosaveHint;
                return;
            }

            Error = null;
            _settings.SignificantDigits = digits;
            UserSettingsStore.Save(_path, _settings);
            _changed();
        }
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }
}
