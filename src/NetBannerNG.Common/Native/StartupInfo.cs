using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct StartupInfo : IEquatable<StartupInfo>
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;

        public override readonly bool Equals(object? obj) => obj is StartupInfo info && Equals(info);

        public readonly bool Equals(StartupInfo other) => cb == other.cb && lpReserved == other.lpReserved && lpDesktop == other.lpDesktop && lpTitle == other.lpTitle && dwX == other.dwX && dwY == other.dwY && dwXSize == other.dwXSize && dwYSize == other.dwYSize && dwXCountChars == other.dwXCountChars && dwYCountChars == other.dwYCountChars && dwFillAttribute == other.dwFillAttribute && dwFlags == other.dwFlags && wShowWindow == other.wShowWindow && cbReserved2 == other.cbReserved2 && lpReserved2.Equals(other.lpReserved2) && hStdInput.Equals(other.hStdInput) && hStdOutput.Equals(other.hStdOutput) && hStdError.Equals(other.hStdError);

        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(cb);
            hash.Add(lpReserved);
            hash.Add(lpDesktop);
            hash.Add(lpTitle);
            hash.Add(dwX);
            hash.Add(dwY);
            hash.Add(dwXSize);
            hash.Add(dwYSize);
            hash.Add(dwXCountChars);
            hash.Add(dwYCountChars);
            hash.Add(dwFillAttribute);
            hash.Add(dwFlags);
            hash.Add(wShowWindow);
            hash.Add(cbReserved2);
            hash.Add(lpReserved2);
            hash.Add(hStdInput);
            hash.Add(hStdOutput);
            hash.Add(hStdError);
            return hash.ToHashCode();
        }

        public static bool operator ==(StartupInfo left, StartupInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StartupInfo left, StartupInfo right)
        {
            return !(left == right);
        }

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}