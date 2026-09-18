# Host Asistencia

Desktop app for a small restaurant: employees clock in/out on a **ZKTeco K30**, this program stores punches locally (SQLite, no server), and each week it builds **hours × role hourly rate**.

## Run on this Mac

```bash
cd apps/Attendance
dotnet run --project src/Attendance.Desktop
```

Use **Reloj K30 → Usar simulador**. Demo employees and a week of punches are created on first launch.

## Restaurant PC (Windows)

```bash
dotnet publish src/Attendance.Desktop -c Release -r win-x64 --self-contained true -o dist/win
```

Copy `dist/win` to the PC. Set the K30 IP (default port **4370**) and uncheck the simulator. Employee **ID en K30** must match the user number on the clock.

Backup: copy the SQLite file shown on the Reloj K30 screen (`HostAttendance/attendance.db`).

## Weekly pay

`pago = horas trabajadas × valor por hora del rol`

Entrada without salida (or the reverse) is listed as an anomaly and is not paid until you fix the punch.
