using System.Buffers.Binary;

namespace Attendance.Core.Device;

public sealed partial class ZkTecoNetworkDevice
{
    private const ushort CmdUserWrq = 8;
    private const ushort CmdRefreshData = 1013;
    private const ushort CmdVerifyWrq = 79;
    private const ushort CmdCancelCapture = 62;
    private const ushort CmdStartEnroll = 61;
    private const ushort CmdStartVerify = 60;
    private const ushort CmdFreeSizes = 50;
    private const ushort CmdDeleteUser = 18;
    private const ushort CmdDeleteUserTemp = 19;

    public async Task<IReadOnlyList<DeviceUser>> DownloadUsersAsync(CancellationToken cancellationToken = default)
    {
        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var disabled = false;
        try
        {
            await EnsureAckAsync(session, CmdDisableDevice, [], "No se pudo deshabilitar el K30 para leer usuarios.", cancellationToken)
                .ConfigureAwait(false);
            disabled = true;
            var payload = await ReadTableAsync(session, K30UserRecord.TableRequest, "usuarios", cancellationToken)
                .ConfigureAwait(false);
            return K30UserRecord.ParseTable(payload);
        }
        finally
        {
            await FinishAsync(session, disabled, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<DeviceUserWriteResult> UpsertUserAsync(
        string pin,
        string name,
        string? password,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (!K30UserRecord.IsValidPin(pin))
        {
            throw new InvalidOperationException("El ID del K30 debe ser numérico (1 a 9 dígitos).");
        }

        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var disabled = false;
        try
        {
            await EnsureAckAsync(session, CmdDisableDevice, [], "No se pudo deshabilitar el K30 para escribir usuarios.", cancellationToken)
                .ConfigureAwait(false);
            disabled = true;

            var existing = K30UserRecord.ParseTable(
                await ReadTableAsync(session, K30UserRecord.TableRequest, "usuarios", cancellationToken).ConfigureAwait(false));
            var current = existing.FirstOrDefault(user =>
                user.Pin.Equals(pin.Trim(), StringComparison.OrdinalIgnoreCase));
            var created = current is null;
            var serial = current?.Serial ?? K30UserRecord.NextSerial(existing);
            var privilege = K30UserRecord.WithEnabled(current?.Privilege ?? K30UserRecord.CommonEnabled, enabled);
            var secret = string.IsNullOrWhiteSpace(password)
                ? current?.Password ?? K30UserRecord.NewPassword()
                : password.Trim();
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = K30UserRecord.NewPassword();
            }

            var record = K30UserRecord.Pack(
                serial,
                privilege,
                secret,
                name,
                pin,
                current?.Card ?? 0,
                current?.Group is > 0 ? current.Group : (byte)1);

            await EnsureAckAsync(session, CmdUserWrq, record, "El K30 rechazó crear/actualizar el usuario.", cancellationToken)
                .ConfigureAwait(false);
            await EnsureAckAsync(session, CmdRefreshData, [], "El K30 no aplicó el usuario nuevo.", cancellationToken)
                .ConfigureAwait(false);

            var verify = current is { HasPassword: false } && string.IsNullOrWhiteSpace(password)
                ? K30UserRecord.VerifyFingerprintOrPassword
                : K30UserRecord.VerifyPassword;
            if (!string.IsNullOrWhiteSpace(secret))
            {
                verify = K30UserRecord.VerifyFingerprintOrPassword;
            }

            await session.SendCommandAsync(
                    CmdVerifyWrq,
                    K30UserRecord.VerifyModePayload(serial, verify),
                    cancellationToken)
                .ConfigureAwait(false);
            await session.SendCommandAsync(CmdRefreshData, [], cancellationToken).ConfigureAwait(false);

            return new DeviceUserWriteResult(serial, pin.Trim(), secret, created);
        }
        finally
        {
            await FinishAsync(session, disabled, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<DeviceUserDeleteResult> DeleteUserAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (!K30UserRecord.IsValidPin(pin))
        {
            throw new InvalidOperationException("El ID del K30 debe ser numérico (1 a 9 dígitos).");
        }

        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var disabled = false;
        try
        {
            await EnsureAckAsync(session, CmdDisableDevice, [], "No se pudo deshabilitar el K30 para borrar el usuario.", cancellationToken)
                .ConfigureAwait(false);
            disabled = true;

            var users = K30UserRecord.ParseTable(
                await ReadTableAsync(session, K30UserRecord.TableRequest, "usuarios", cancellationToken).ConfigureAwait(false));
            var current = users.FirstOrDefault(user =>
                user.Pin.Equals(pin.Trim(), StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                return new DeviceUserDeleteResult(true, true, "El PIN no está en el K30.");
            }

            for (byte finger = 0; finger <= 9; finger++)
            {
                await session.SendCommandAsync(
                        CmdDeleteUserTemp,
                        K30UserRecord.DeleteTemplatePayload(current.Serial, finger),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var deleted = await session.SendCommandAsync(
                    CmdDeleteUser,
                    K30UserRecord.DeleteUserPayload(current.Serial),
                    cancellationToken)
                .ConfigureAwait(false);
            if (deleted.Command != CmdAckOk)
            {
                deleted = await session.SendCommandAsync(
                        CmdDeleteUser,
                        K30UserRecord.DeleteUserByPinPayload(pin),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await session.SendCommandAsync(CmdRefreshData, [], cancellationToken).ConfigureAwait(false);
            if (deleted.Command != CmdAckOk)
            {
                return new DeviceUserDeleteResult(
                    false,
                    false,
                    $"El K30 rechazó borrar el PIN {pin.Trim()} (código {deleted.Command}).");
            }

            var remaining = K30UserRecord.ParseTable(
                await ReadTableAsync(session, K30UserRecord.TableRequest, "usuarios", cancellationToken).ConfigureAwait(false));
            if (remaining.Any(user => user.Pin.Equals(pin.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return new DeviceUserDeleteResult(false, false, $"El K30 no quitó el PIN {pin.Trim()}.");
            }

            return new DeviceUserDeleteResult(true, false, $"PIN {pin.Trim()} quitado del K30 (huella y clave).");
        }
        finally
        {
            await FinishAsync(session, disabled, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<DeviceEnrollResult> EnrollFingerprintAsync(
        string pin,
        int fingerIndex = 0,
        TimeSpan? wait = null,
        CancellationToken cancellationToken = default)
    {
        if (!K30UserRecord.IsValidPin(pin))
        {
            throw new InvalidOperationException("El ID del K30 debe ser numérico (1 a 9 dígitos).");
        }

        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var before = await ReadFingerprintCountAsync(session, cancellationToken).ConfigureAwait(false);
            await session.SendCommandAsync(CmdEnableDevice, [], cancellationToken).ConfigureAwait(false);
            await session.SendCommandAsync(CmdCancelCapture, [], cancellationToken).ConfigureAwait(false);

            var enroll = await session.SendCommandAsync(
                    CmdStartEnroll,
                    K30UserRecord.EnrollPayload(pin, fingerIndex),
                    cancellationToken)
                .ConfigureAwait(false);
            if (enroll.Command != CmdAckOk)
            {
                return new DeviceEnrollResult(false, $"El K30 no inició la huella (código {enroll.Command}). Use la clave temporal.");
            }

            var waitFor = wait ?? TimeSpan.FromSeconds(75);
            var captured = await WaitForEnrollmentAsync(session, waitFor, cancellationToken).ConfigureAwait(false);
            if (captured.Duplicate)
            {
                return new DeviceEnrollResult(false, "Esa huella ya está en el reloj (otro usuario o el mismo). Use otro dedo o la clave temporal.");
            }

            await session.SendCommandAsync(CmdCancelCapture, [], cancellationToken).ConfigureAwait(false);
            await session.SendCommandAsync(CmdStartVerify, [], cancellationToken).ConfigureAwait(false);
            await session.SendCommandAsync(CmdRefreshData, [], cancellationToken).ConfigureAwait(false);
            var after = await ReadFingerprintCountAsync(session, cancellationToken).ConfigureAwait(false);
            if (after > before || captured.Succeeded)
            {
                await TrySetFingerprintOrPasswordAsync(session, pin, cancellationToken).ConfigureAwait(false);
                return new DeviceEnrollResult(true, "Huella registrada. Puede marcar con huella o con la clave.");
            }

            return new DeviceEnrollResult(false, "El reloj no guardó la huella. Deje el dedo hasta oír las 3 tomas, o use la clave temporal.");
        }
        finally
        {
            await FinishAsync(session, disabled: false, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(bool Succeeded, bool Duplicate)> WaitForEnrollmentAsync(
        ZkSession session,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        var samples = 0;
        var completed = false;
        var duplicate = false;
        var deadline = DateTime.UtcNow + wait;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested && !completed && !duplicate)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(1))
            {
                break;
            }

            ZkReply packet;
            try
            {
                packet = await session.ReadRealtimeAsync(cancellationToken, remaining).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or IOException)
            {
                break;
            }

            try
            {
                await session.SendAckOkAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Event ACK is required by the K30; if the socket died, the loop will exit next.
            }

            var code = K30UserRecord.EnrollEventCode(packet.Command, packet.Data);
            if (K30UserRecord.IsEnrollDuplicate(code))
            {
                duplicate = true;
                break;
            }

            if (K30UserRecord.IsEnrollFailed(code))
            {
                break;
            }

            if (K30UserRecord.IsEnrollSample(code, packet.Command))
            {
                samples++;
            }

            if (K30UserRecord.IsEnrollComplete(code, packet.Command) || samples >= 3)
            {
                completed = true;
            }
        }

        return (completed || samples >= 3, duplicate);
    }

    private async Task TrySetFingerprintOrPasswordAsync(ZkSession session, string pin, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureAckAsync(session, CmdDisableDevice, [], "No se pudo leer el usuario después de la huella.", cancellationToken)
                .ConfigureAwait(false);
            var users = K30UserRecord.ParseTable(
                await ReadTableAsync(session, K30UserRecord.TableRequest, "usuarios", cancellationToken).ConfigureAwait(false));
            var user = users.FirstOrDefault(item => item.Pin.Equals(pin.Trim(), StringComparison.Ordinal));
            if (user is not null)
            {
                await session.SendCommandAsync(
                        CmdVerifyWrq,
                        K30UserRecord.VerifyModePayload(user.Serial, K30UserRecord.VerifyFingerprintOrPassword),
                        cancellationToken)
                    .ConfigureAwait(false);
                await session.SendCommandAsync(CmdRefreshData, [], cancellationToken).ConfigureAwait(false);
            }

            await session.SendCommandAsync(CmdEnableDevice, [], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Fingerprint already landed; verify-mode is best-effort.
        }
    }

    private async Task<byte[]> ReadTableAsync(
        ZkSession session,
        byte[] request,
        string label,
        CancellationToken cancellationToken)
    {
        return await session.ReadDatasetAsync(request, label, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadFingerprintCountAsync(ZkSession session, CancellationToken cancellationToken)
    {
        var reply = await session.SendCommandAsync(CmdFreeSizes, [], cancellationToken).ConfigureAwait(false);
        if (reply.Data.Length >= 28)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(reply.Data.AsSpan(24));
        }

        return 0;
    }

    private static async Task EnsureAckAsync(
        ZkSession session,
        ushort command,
        byte[] data,
        string error,
        CancellationToken cancellationToken)
    {
        var reply = await session.SendCommandAsync(command, data, cancellationToken).ConfigureAwait(false);
        if (reply.Command != CmdAckOk)
        {
            throw new InvalidOperationException($"{error} (código {reply.Command}).");
        }
    }

    private static async Task FinishAsync(ZkSession session, bool disabled, CancellationToken cancellationToken)
    {
        if (disabled)
        {
            try
            {
                await session.SendCommandAsync(CmdEnableDevice, [], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Re-enable is best-effort so disconnect still runs.
            }
        }

        try
        {
            await session.SendCommandAsync(CmdExit, [], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Device may already have closed the session.
        }
    }
}
