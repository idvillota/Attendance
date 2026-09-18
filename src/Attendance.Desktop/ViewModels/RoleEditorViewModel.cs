using System.Globalization;
using Attendance.Core.Data;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class RoleEditorViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;
    private readonly long? _id;

    public RoleEditorViewModel(AttendanceDb db, Role? role)
    {
        _db = db;
        var rates = role?.Rates ?? RoleRates.LegalColombia2026();
        if (role is null)
        {
            Title = "Nuevo rol";
        }
        else
        {
            _id = role.Id;
            Title = "Editar rol";
            FormName = role.Name;
            IsActive = role.IsActive;
        }

        LoadRates(rates);
    }

    public string Title { get; }

    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private string _ordinaryDay = string.Empty;
    [ObservableProperty] private string _ordinaryNight = string.Empty;
    [ObservableProperty] private string _sundayDay = string.Empty;
    [ObservableProperty] private string _sundayNight = string.Empty;
    [ObservableProperty] private string _extraOrdinaryDay = string.Empty;
    [ObservableProperty] private string _extraOrdinaryNight = string.Empty;
    [ObservableProperty] private string _extraSundayDay = string.Empty;
    [ObservableProperty] private string _extraSundayNight = string.Empty;

    [RelayCommand]
    private void FillLegal2026() => LoadRates(RoleRates.LegalColombia2026());

    [RelayCommand]
    private void RecalcFromOrdinary()
    {
        if (!TryParse(OrdinaryDay, out var ordinary))
        {
            Error = "Escriba primero la hora ordinaria.";
            return;
        }

        LoadRates(RoleRates.FromOrdinary(ordinary));
        Error = string.Empty;
    }

    public bool TrySave()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            Error = "El nombre del rol es obligatorio.";
            return false;
        }

        if (!TryBuildRates(out var rates, out var message))
        {
            Error = message ?? "Nombre y tarifas son obligatorios.";
            return false;
        }

        if (_id is null)
        {
            _db.AddRole(FormName, rates);
        }
        else
        {
            _db.UpdateRole(new Role
            {
                Id = _id.Value,
                Name = FormName,
                IsActive = IsActive,
                Rates = rates
            });
        }

        return true;
    }

    private void LoadRates(RoleRates rates)
    {
        OrdinaryDay = Format(rates.OrdinaryDay);
        OrdinaryNight = Format(rates.OrdinaryNight);
        SundayDay = Format(rates.SundayDay);
        SundayNight = Format(rates.SundayNight);
        ExtraOrdinaryDay = Format(rates.ExtraOrdinaryDay);
        ExtraOrdinaryNight = Format(rates.ExtraOrdinaryNight);
        ExtraSundayDay = Format(rates.ExtraSundayDay);
        ExtraSundayNight = Format(rates.ExtraSundayNight);
    }

    private bool TryBuildRates(out RoleRates rates, out string? error)
    {
        rates = new RoleRates();
        error = null;
        if (!TryParse(OrdinaryDay, out var od) || !TryParse(OrdinaryNight, out var on) ||
            !TryParse(SundayDay, out var sd) || !TryParse(SundayNight, out var sn) ||
            !TryParse(ExtraOrdinaryDay, out var eod) || !TryParse(ExtraOrdinaryNight, out var eon) ||
            !TryParse(ExtraSundayDay, out var esd) || !TryParse(ExtraSundayNight, out var esn))
        {
            error = "Todas las tarifas deben ser números (0 si no aplica).";
            return false;
        }

        rates = new RoleRates
        {
            OrdinaryDay = od,
            OrdinaryNight = on,
            SundayDay = sd,
            SundayNight = sn,
            ExtraOrdinaryDay = eod,
            ExtraOrdinaryNight = eon,
            ExtraSundayDay = esd,
            ExtraSundayNight = esn
        };
        return true;
    }

    private static bool TryParse(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CO"), out value);

    private static string Format(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
}
