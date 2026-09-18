using System.Collections.ObjectModel;
using Attendance.Core.Data;
using Attendance.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Attendance.Desktop.ViewModels;

public partial class PunchesViewModel : ViewModelBase
{
    private readonly AttendanceDb _db;

    public PunchesViewModel(AttendanceDb db)
    {
        _db = db;
        Direction = Directions[0];
        Reload();
    }

    public ObservableCollection<Punch> Punches { get; } = [];
    public ObservableCollection<Employee> Employees { get; } = [];
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
        foreach (var punch in _db.GetPunches())
        {
            Punches.Add(punch);
        }

        Employees.Clear();
        foreach (var employee in _db.GetEmployees(activeOnly: true))
        {
            Employees.Add(employee);
        }

        SelectedEmployee ??= Employees.FirstOrDefault();
    }

    [RelayCommand]
    private void Add()
    {
        if (SelectedEmployee is null)
        {
            Status = "Seleccione un empleado.";
            return;
        }

        _db.AddPunch(
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
        if (Selected is null)
        {
            return;
        }

        _db.DeletePunch(Selected.Id);
        Reload();
        Status = "Marcación eliminada.";
    }
}

public sealed record DirectionChoice(PunchDirection Value, string Label);
