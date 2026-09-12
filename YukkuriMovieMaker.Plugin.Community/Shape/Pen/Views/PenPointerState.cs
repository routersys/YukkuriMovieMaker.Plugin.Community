using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenPointerState : IDisposable
    {
        const int PointerUpdateMessage = 0x0245;
        const int PointerDownMessage = 0x0246;
        const int PointerUpMessage = 0x0247;
        const int PointerLeaveMessage = 0x024A;
        const uint PenPointerType = 3;
        const uint PressureMask = 0x00000001;
        const float PressureRange = 1024f;

        HwndSource? source;
        float pressure = -1;

        public bool HasPressure => pressure >= 0;

        public float Pressure => pressure;

        public void Attach(Visual visual)
        {
            if (source is not null)
                return;

            source = HwndSource.FromVisual(visual) as HwndSource;
            source?.AddHook(OnMessage);
        }

        public void Dispose()
        {
            source?.RemoveHook(OnMessage);
            source = null;
            pressure = -1;
        }

        nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            switch (message)
            {
                case PointerDownMessage:
                case PointerUpdateMessage:
                    Update((uint)(wParam & 0xFFFF));
                    break;
                case PointerUpMessage:
                case PointerLeaveMessage:
                    pressure = -1;
                    break;
            }
            return 0;
        }

        void Update(uint pointerId)
        {
            if (!GetPointerType(pointerId, out var type) || type != PenPointerType
                || !GetPointerPenInfo(pointerId, out var info) || (info.PenMask & PressureMask) == 0)
            {
                pressure = -1;
                return;
            }

            var value = info.Pressure / PressureRange;
            pressure = value < 0 ? 0 : value > 1 ? 1 : value;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPointerType(uint pointerId, out uint pointerType);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPointerPenInfo(uint pointerId, out PointerPenInfo penInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct PointerInfo
        {
            public uint PointerType;
            public uint PointerId;
            public uint FrameId;
            public uint PointerFlags;
            public nint SourceDevice;
            public nint HwndTarget;
            public int PixelX;
            public int PixelY;
            public int HimetricX;
            public int HimetricY;
            public int PixelRawX;
            public int PixelRawY;
            public int HimetricRawX;
            public int HimetricRawY;
            public uint Time;
            public uint HistoryCount;
            public int InputData;
            public uint KeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PointerPenInfo
        {
            public PointerInfo PointerInfo;
            public uint PenFlags;
            public uint PenMask;
            public uint Pressure;
            public uint Rotation;
            public int TiltX;
            public int TiltY;
        }
    }
}
