using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;
using Labz.Modules.Circuits;

namespace Labz.App.ViewModels;

public sealed class ResultsViewModel : ObservableObject
{
    private readonly LiveSolveController _calculator;
    private readonly Func<int> _significantDigits;
    private string? _blockedMessage;
    private string _balanceText = string.Empty;
    private bool _powerBalanceOk;
    private string _signConvention = string.Empty;
    private string _chargeEfficiency = Strings.ChargeEfficiencyLabel;
    private string _loadHours = "0";
    private string _peakSunHours = "0";
    private string? _hoursError;
    private bool _suspendHours;

    public ResultsViewModel(SchematicSession session, LiveSolveController calculator, Func<int>? significantDigits = null)
    {
        Session = session;
        _calculator = calculator;
        _significantDigits = significantDigits ?? (() => 6);
        Rows = [];
        Cards = [];
        Problems = [];
        foreach (var card in ResultProjection.Empty().Cards)
        {
            Cards.Add(card);
        }
        CancelCommand = new RelayCommand(() => _calculator.Cancel(), () => _calculator.IsRunning);
        _calculator.Changed += Refresh;
        Session.Changed += affects =>
        {
            if (affects)
            {
                _calculator.Schedule(Session.State);
            }
        };
        ReadHours();
        _calculator.Schedule(Session.State);
    }

    public SchematicSession Session { get; }

    public ObservableCollection<ComponentResultRow> Rows { get; }

    public ObservableCollection<EnergyCard> Cards { get; }

    public ObservableCollection<CircuitProblem> Problems { get; }

    public RelayCommand CancelCommand { get; }

    public string ColumnId => Strings.ColumnId;

    public string ColumnType => Strings.ColumnType;

    public string ColumnVoltage => Strings.ColumnVoltage;

    public string ColumnCurrent => Strings.ColumnCurrent;

    public string ColumnPower => Strings.ColumnPower;

    public string ColumnRating => Strings.ColumnRating;

    public string ValidationHeader => Strings.ValidationHeader;

    public string Calculating => Strings.Calculating;

    public string CancelLabel => Strings.Cancel;

    public string LoadHoursLabel => Strings.LoadHours;

    public string PeakSunHoursLabel => Strings.PeakSunHours;

    public string? BlockedMessage
    {
        get => _blockedMessage;
        private set => SetProperty(ref _blockedMessage, value);
    }

    public string BalanceText
    {
        get => _balanceText;
        private set => SetProperty(ref _balanceText, value);
    }

    public bool PowerBalanceOk
    {
        get => _powerBalanceOk;
        private set => SetProperty(ref _powerBalanceOk, value);
    }

    public string SignConvention
    {
        get => _signConvention;
        private set => SetProperty(ref _signConvention, value);
    }

    public string ChargeEfficiency
    {
        get => _chargeEfficiency;
        private set => SetProperty(ref _chargeEfficiency, value);
    }

    public bool ShowProgress => _calculator.ShowProgress;

    public double Progress => _calculator.Progress;

    public string LoadHours
    {
        get => _loadHours;
        set
        {
            if (_suspendHours || _loadHours == value)
            {
                return;
            }

            if (!TryCommit(ComponentLibrary.LoadTarget, value))
            {
                return;
            }

            _loadHours = value;
            OnPropertyChanged();
        }
    }

    public string PeakSunHours
    {
        get => _peakSunHours;
        set
        {
            if (_suspendHours || _peakSunHours == value)
            {
                return;
            }

            var panel = Session.State.Components.FirstOrDefault(component => component.Type == "solar_panel")?.Id;
            if (panel is null || !TryCommit(panel, value))
            {
                return;
            }

            _peakSunHours = value;
            OnPropertyChanged();
        }
    }

    public bool CanEditPeakSun => Session.State.Components.Any(component => component.Type == "solar_panel");

    public string? HoursError
    {
        get => _hoursError;
        private set => SetProperty(ref _hoursError, value);
    }

    public int SignificantDigits
    {
        get => _significantDigits();
        set => Refresh();
    }

    private void Refresh()
    {
        Problems.Clear();
        foreach (var problem in _calculator.Problems)
        {
            Problems.Add(problem);
        }

        Rows.Clear();
        Cards.Clear();
        if (_calculator.BlockedMessage is not null || _calculator.Solution is null)
        {
            var empty = ResultProjection.Empty();
            foreach (var card in empty.Cards)
            {
                Cards.Add(card);
            }

            BlockedMessage = _calculator.BlockedMessage ?? (_calculator.Problems.Count > 0 ? Strings.FixSchematicMessage : null);
            BalanceText = string.Empty;
            PowerBalanceOk = false;
            SignConvention = empty.SignConvention;
            ChargeEfficiency = empty.ChargeEfficiency;
        }
        else
        {
            var projection = ResultProjection.From(Session.State, _calculator.Solution, _significantDigits());
            foreach (var row in projection.Rows)
            {
                Rows.Add(row);
            }

            foreach (var card in projection.Cards)
            {
                Cards.Add(card);
            }

            BlockedMessage = null;
            BalanceText = projection.BalanceText;
            PowerBalanceOk = projection.PowerBalanceOk;
            SignConvention = projection.SignConvention;
            ChargeEfficiency = projection.ChargeEfficiency;
        }

        OnPropertyChanged(nameof(ShowProgress));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(CanEditPeakSun));
        CancelCommand.NotifyCanExecuteChanged();
        ReadHours();
        Updated?.Invoke();
    }

    public event Action? Updated;

    private void ReadHours()
    {
        _suspendHours = true;
        _loadHours = FormatHours(ComponentLibrary.LoadTarget);
        var panel = Session.State.Components.FirstOrDefault(component => component.Type == "solar_panel")?.Id;
        _peakSunHours = panel is null ? string.Empty : FormatHours(panel);
        _suspendHours = false;
        OnPropertyChanged(nameof(LoadHours));
        OnPropertyChanged(nameof(PeakSunHours));
    }

    private string FormatHours(string target)
    {
        var hours = Session.State.UsageProfiles.FirstOrDefault(profile => profile.Target == target)?.HoursPerDay;
        return hours?.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR")) ?? string.Empty;
    }

    private bool TryCommit(string target, string text)
    {
        if (!double.TryParse(text.Trim().Replace('.', ','), NumberStyles.Float, CultureInfo.GetCultureInfo("pt-BR"), out var hours)
            && !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out hours))
        {
            HoursError = Strings.HoursInvalid;
            return false;
        }

        if (!double.IsFinite(hours) || hours is < 0 or > 24)
        {
            HoursError = Strings.HoursInvalid;
            return false;
        }

        var existing = Session.State.UsageProfiles.FirstOrDefault(profile => profile.Target == target)?.HoursPerDay;
        HoursError = null;
        if (existing is double current && current == hours)
        {
            return true;
        }

        Session.SetUsageHours(target, hours);
        return true;
    }
}
