using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pos.Infrastructure.Security;

// Envoltorio directo sobre CryptProtectData/CryptUnprotectData (DPAPI) de Windows, ligado al
// perfil del usuario actual (CRYPTPROTECT_UI_FORBIDDEN evita cualquier prompt de UI). Se invoca
// mediante P/Invoke en lugar del paquete NuGet System.Security.Cryptography.ProtectedData para no
// agregar una dependencia externa (ver CLAUDE.md sección C: no NuGet sin autorización, y
// Pos.Architecture.Tests que exige una lista cerrada de PackageReference en Pos.Infrastructure).
[SupportedOSPlatform("windows")]
public static class DpapiProtector
{
    private const uint CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return RunProtection(plaintext, unprotect: false);
    }

    public static byte[] Unprotect(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        return RunProtection(ciphertext, unprotect: true);
    }

    private static byte[] RunProtection(byte[] input, bool unprotect)
    {
        var inputBlob = new DataBlob();
        var outputBlob = new DataBlob();
        var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);

        try
        {
            inputBlob.cbData = (uint)input.Length;
            inputBlob.pbData = inputHandle.AddrOfPinnedObject();

            var success = unprotect
                ? CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob)
                : CryptProtectData(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref outputBlob);

            if (!success)
            {
                throw new InvalidOperationException(
                    $"La operación DPAPI ({(unprotect ? "unprotect" : "protect")}) falló con el código de error {Marshal.GetLastWin32Error()}.");
            }

            var result = new byte[outputBlob.cbData];
            Marshal.Copy(outputBlob.pbData, result, 0, result.Length);
            return result;
        }
        finally
        {
            inputHandle.Free();

            if (outputBlob.pbData != IntPtr.Zero)
            {
                LocalFree(outputBlob.pbData);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        ref DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
