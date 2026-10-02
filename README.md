# Host Asistencia

Desktop app for a small restaurant: employees clock in/out on a **ZKTeco K30**, this program stores punches locally (SQLite, no server), and each week it prices hours by **when** they were worked (ordinary / night / Sunday / extra) using the COP amounts on each role.

## Run on this Mac

```bash
cd apps/Attendance
dotnet run --project src/Attendance.Desktop
```

The app talks to the **ZKTeco K30 at 192.168.39.201:4370**. Reloj and Logs K30 read the live clock only. Demo employees are still created on first launch. The first screen asks you to **create an administrator**.

## Restaurant PC (Windows)

```bash
dotnet publish src/Attendance.Desktop -c Release -r win-x64 --self-contained true -o dist/win
```

Copy `dist/win` to the PC. Set the K30 IP (default port **4370**). Employee **ID en K30** must match the user number on the clock.

Backup: copy the SQLite file shown on the Reloj K30 screen (`HostAttendance/attendance.db`).

## Weekly pay

Pay is sliced by the 7×24 calendar in **Horario y recargos** and the eight COP cells on each role. Colombia 2S 2026 values are a fill helper in the role editor; admins can change any amount.

Entrada without salida (or the reverse) is listed as an anomaly and is not paid until you fix the punch.

Excel export has three sheets: **Nomina**, **Por_hora**, and **Anomalias**. **Exportar PDF** prints the same weekly totals for the restaurant PC.

## Layout

- `Attendance.Core` — domain, SQLite store, banded payroll, K30/mock/CSV sync, Excel export
- `Attendance.Desktop` — Avalonia MVVM shell (Spanish UI)
- `Attendance.Tests.Unit` — pairing, banded pay, seed + Excel
