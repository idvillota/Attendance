using System.Collections.ObjectModel;
using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class PunchesViewModel : ViewModelBase
{
    private readonly IAttendanceStore _store;
    private readonly IAuthSession _session;

    public PunchesViewModel(IAttendanceStore store, IAuthSession session)
    {
        _store = store;
        _session = session;
        CanEditPunches = session.IsAdmin;
        IntroText = session.IsAdmin
            ? "Use esta pantalla para corregir una marcación ya guardada. Las nuevas del K30 se revisan primero en Logs K30. Para el pago, el orden de las marcas es lo que cuenta, no Entrada/Salida."
            : "Sus marcaciones según lo descargado en este PC. Si falta una, pídale al administrador que la corrija.";
        Direction = Directions[0];
        Reload();
    }

    public ObservableCollection<Punch> Punches { get; } = [];
    public ObservableCollection<Employee> Employees { get; } = [];
    public bool CanEditPunches { get; }
    public string IntroText { get; }
    public DirectionChoice[] Directions { get; } =
    [
        new(PunchDirection.In, "Entrada"),
        new(PunchDirection.Out, "Salida")
    ];

    [ObservableProperty]
    private Punch? _selected;

    [ObservableProperty]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    private DateTimeOffset? _punchedAt = DateTimeOffset.Now;

    [ObservableProperty]
    private DirectionChoice? _direction;

    [ObservableProperty]
    private string _status = string.Empty;

    public void Reload()
    {
        Punches.Clear();
        foreach (var punch in _store.GetPunches())
        {
            Punches.Add(punch);
        }

        Employees.Clear();
        foreach (var employee in _store.GetEmployees(activeOnly: true))
        {
            Employees.Add(employee);
        }

        SelectedEmployee ??= Employees.FirstOrDefault();
    }

    [RelayCommand]
    private void Add()
    {
        if (!_session.IsAdmin)
        {
            return;
        }
        if (SelectedEmployee is null)
        {
            Status = "Seleccione un empleado.";
            return;
        }

        _store.AddPunch(
            SelectedEmployee.Id,
            PunchedAt?.LocalDateTime ?? DateTime.Now,
            Direction?.Value ?? PunchDirection.In,
            PunchSource.Manual);
        Reload();
        Status = "Marcación agregada.";
    }

    [RelayCommand]
    private void Delete()
    {
        if (!_session.IsAdmin || Selected is null)
        {
            return;
        }

        _store.DeletePunch(Selected.Id);
        Reload();
        Status = "Marcación eliminada.";
    }
}

public sealed record DirectionChoice(PunchDirection Value, string Label);
