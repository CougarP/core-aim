
﻿using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Core_Aim.Services.Hardware
{
    // =====================================================================
    // GPC Standard Identifiers (identical to Consoletuner GPC scripting)
    // Source: https://www.consoletuner.com/wiki/index.php?id=t2:gpc_scripting
    // =====================================================================
    public static class GPC
    {
        // Buttons
        public const int BUTTON_1  =  1;  // Xbox / PS / Home
        public const int BUTTON_2  =  2;  // View / Touchpad / Select
        public const int BUTTON_3  =  3;  // Menu / Options / Start
        public const int BUTTON_4  =  4;  // RB / R1
        public const int BUTTON_5  =  5;  // RT / R2
        public const int BUTTON_6  =  6;  // RS / R3
        public const int BUTTON_7  =  7;  // LB / L1
        public const int BUTTON_8  =  8;  // LT / L2
        public const int BUTTON_9  =  9;  // LS / L3
        public const int BUTTON_10 = 10;  // D-Pad Up
        public const int BUTTON_11 = 11;  // D-Pad Down
        public const int BUTTON_12 = 12;  // D-Pad Left
        public const int BUTTON_13 = 13;  // D-Pad Right
        public const int BUTTON_14 = 14;  // Y / Triangle
        public const int BUTTON_15 = 15;  // B / Circle
        public const int BUTTON_16 = 16;  // A / Cross
        public const int BUTTON_17 = 17;  // X / Square
        public const int BUTTON_18 = 18;  // Share / Create / Capture
        public const int BUTTON_19 = 19;  // Touch P1 / SL
        public const int BUTTON_20 = 20;  // Touch P2 / SR
        public const int BUTTON_21 = 21;  // Sync / Mute

        // Sticks (-100.0 to +100.0)
        public const int STICK_1_X = 22;  // Right Analog X
        public const int STICK_1_Y = 23;  // Right Analog Y
        public const int STICK_2_X = 24;  // Left Analog X
        public const int STICK_2_Y = 25;  // Left Analog Y

        // Paddles (Xbox Elite)
        public const int PADDLE_1  = 26;
        public const int PADDLE_2  = 27;
        public const int PADDLE_3  = 28;
        public const int PADDLE_4  = 29;

        // Accelerometer / Gyroscope
        public const int ACCEL_1_X = 30;
        public const int ACCEL_1_Y = 31;
        public const int ACCEL_1_Z = 32;
        public const int GYRO_1_X  = 33;
        public const int GYRO_1_Y  = 34;
        public const int GYRO_1_Z  = 35;

        // Touch Points (PS4/PS5)
        public const int POINT_1_X = 36;
        public const int POINT_1_Y = 37;
        public const int POINT_2_X = 38;
        public const int POINT_2_Y = 39;

        // Force Feedback / Rumble
        public const int FFB_1     = 40;
        public const int FFB_2     = 41;
    }

    // --- Enums e Structs ---
    public enum TT2Status { Disconnected = 0, Connecting = 1, Connected = 2, Finalizing = 3, Error = 4 }
    public enum TT2_Proto : byte { Auto = 0, PS3 = 1, PS4 = 2, Xbox360 = 3, XboxOne = 4, Switch = 5, PS5 = 8, XboxSeries = 9 }
    public enum TT2_Poll : byte { Default = 0, Hz1000 = 1, Hz500 = 2, Hz250 = 4, Hz125 = 8 }

    [StructLayout(LayoutKind.Sequential)]
    public struct TT2Config
    {
        [MarshalAs(UnmanagedType.I1)] public bool exclusiveHandle;
        public int rateHz;
        [MarshalAs(UnmanagedType.I1)] public bool invertX;
        [MarshalAs(UnmanagedType.I1)] public bool invertY;
        public double clampMagnitude;
        [MarshalAs(UnmanagedType.I1)] public bool enableInputRead;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TT2ButtonEvent
    {
        public int index;
        public int oldValue;
        public int newValue;
        [MarshalAs(UnmanagedType.I1)] public bool pressed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TT2Info
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string manufacturer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string product;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string serial;
        public ushort versionBcd;
        [MarshalAs(UnmanagedType.I1)] public bool hasData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TT2StickState
    {
        public short LX;
        public short LY;
        public short RX;
        public short RY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TT2TriggerState
    {
        public byte LT;
        public byte RT;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TT2ButtonState
    {
        [MarshalAs(UnmanagedType.I1)] public bool A;
        [MarshalAs(UnmanagedType.I1)] public bool B;
        [MarshalAs(UnmanagedType.I1)] public bool X;
        [MarshalAs(UnmanagedType.I1)] public bool Y;
        [MarshalAs(UnmanagedType.I1)] public bool LB;
        [MarshalAs(UnmanagedType.I1)] public bool RB;
        [MarshalAs(UnmanagedType.I1)] public bool L3;
        [MarshalAs(UnmanagedType.I1)] public bool R3;
        [MarshalAs(UnmanagedType.I1)] public bool Up;
        [MarshalAs(UnmanagedType.I1)] public bool Down;
        [MarshalAs(UnmanagedType.I1)] public bool Left;
        [MarshalAs(UnmanagedType.I1)] public bool Right;
        [MarshalAs(UnmanagedType.I1)] public bool View;
        [MarshalAs(UnmanagedType.I1)] public bool Menu;
        [MarshalAs(UnmanagedType.I1)] public bool Xbox;
    }

    public class TitanTwo : IDisposable
    {
        private const string DllName = "tt2_bridge.dll";
        private IntPtr _instance;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ButtonCallback(ref TT2ButtonEvent evt);
        private ButtonCallback? _gcShieldCallback;

        public event EventHandler<TT2ButtonEvent>? OnButtonEvent;

        // =========================================================
        // DLL IMPORTS (NOMES ORIGINAIS)
        // =========================================================

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr tt2_create(ref TT2Config cfg);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_destroy(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_start(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_stop(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_force_disconnect(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_close_graceful(IntPtr instance);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern int tt2_get_status(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_get_info(IntPtr instance, out TT2Info info);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_send_aim_vector(IntPtr instance, double x, double y);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_notify_aim_active(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_send_aim_offset_pixels(IntPtr instance, double dx, double dy, int w, int h, double gx, double gy, double dz, double max, double alpha);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_is_lt_down(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_is_rt_down(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern int  tt2_get_lt_value(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern int  tt2_get_rt_value(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_button_callback(IntPtr instance, ButtonCallback? cb);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_invert_x(IntPtr instance, bool v);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_invert_y(IntPtr instance, bool v);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_rate_hz(IntPtr instance, int hz);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_exclusive_handle(IntPtr instance, bool v);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_clamp_magnitude(IntPtr instance, double m);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_output_protocol(IntPtr instance, int p);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_output_polling(IntPtr instance, int p);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_input_polling(IntPtr instance, int p);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_prog_interface_full_speed(IntPtr instance, bool enable);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_select_slot(IntPtr instance, byte n);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_slot_stop(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_slot_up(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_slot_down(IntPtr instance);

        // NOVOS IMPORTS ADICIONADOS
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_is_button_down(IntPtr instance, int buttonIndex);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_get_stick_state(IntPtr instance, out TT2StickState state);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_get_trigger_state(IntPtr instance, out TT2TriggerState state);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_get_button_state(IntPtr instance, out TT2ButtonState state);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_send_stick_vector(IntPtr instance, double lx, double ly, double rx, double ry);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_send_trigger_values(IntPtr instance, byte lt, byte rt);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_send_button_state(IntPtr instance, ref TT2ButtonState state);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_led_color(IntPtr instance, byte r, byte g, byte b);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_rumble(IntPtr instance, byte left, byte right);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_macro_flags(IntPtr instance, uint flags);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_macro_ar_vertical(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_macro_ar_horizontal(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_macro_rapid_fire_ms(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_player_blend(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_disable_on_move(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_quickscope_delay(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_jump_shot_delay(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_bunny_hop_delay(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern void tt2_set_strafe_power(IntPtr instance, double val);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_upload_gpc(IntPtr instance, byte[] data, uint size, byte slot, string name, string author);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_upload_gbc(IntPtr instance, byte[] gbcData, uint gbcSize, byte slot);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_save_config(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_load_config(IntPtr instance);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_factory_reset(IntPtr instance);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_sensitivity(IntPtr instance, double sens);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_deadzone(IntPtr instance, double deadzone);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)] static extern bool tt2_set_anti_deadzone(IntPtr instance, double antiDeadzone);

        // =========================================================
        // WRAPPER METHODS
        // =========================================================

        public TitanTwo(TT2Config config)
        {
            _instance = tt2_create(ref config);
            if (_instance == IntPtr.Zero) throw new Exception("Failed to create TitanTwo instance. DLL missing or invalid?");
        }

        public bool Start()
        {
            _gcShieldCallback = (ref TT2ButtonEvent e) => OnButtonEvent?.Invoke(this, e);
            tt2_set_button_callback(_instance, _gcShieldCallback);
            return tt2_start(_instance);
        }

        // PROTEÇÃO CONTRA CRASH — timeout de 3s para não travar ao desconectar USB
        public void Stop()
        {
            if (_instance == IntPtr.Zero) return;
            try { tt2_set_button_callback(_instance, null); } catch { }
            _gcShieldCallback = null;
            try
            {
                var ptr = _instance;
                var stopTask = Task.Run(() => tt2_stop(ptr));
                if (!stopTask.Wait(TimeSpan.FromSeconds(3)))
                    Console.WriteLine("[TitanTwo] WARN: tt2_stop não terminou em 3s — continuando");
            }
            catch { }
        }

        public TT2Status Status => (TT2Status)tt2_get_status(_instance);
        public TT2Info GetInfo() { tt2_get_info(_instance, out var i); return i; }

        // --- Métodos de Envio de Comandos ---
        public void SendAimVector(double x, double y) => tt2_send_aim_vector(_instance, x, y);
        public void NotifyAimActive() => tt2_notify_aim_active(_instance);
        public void SendAimPixels(double dx, double dy, int w, int h) => tt2_send_aim_offset_pixels(_instance, dx, dy, w, h, 1.0, 1.0, 0.05, 0.90, 0.80);

        public void SendStickVector(double lx, double ly, double rx, double ry) => tt2_send_stick_vector(_instance, lx, ly, rx, ry);
        public void SendTriggerValues(byte lt, byte rt) => tt2_send_trigger_values(_instance, lt, rt);
        public void SendButtonState(ref TT2ButtonState state) => tt2_send_button_state(_instance, ref state);

        // --- Métodos de Leitura de Estado ---
        public bool IsLTDown  => tt2_is_lt_down(_instance);
        public bool IsRTDown  => tt2_is_rt_down(_instance);
        /// <summary>Valor analógico do gatilho esquerdo: 0 (solto) … 100 (fundo).</summary>
        public int  LTValue   => tt2_get_lt_value(_instance);
        /// <summary>Valor analógico do gatilho direito: 0 (solto) … 100 (fundo).</summary>
        public int  RTValue   => tt2_get_rt_value(_instance);
        public bool IsButtonDown(int buttonIndex) => tt2_is_button_down(_instance, buttonIndex);

        public TT2StickState GetStickState() { tt2_get_stick_state(_instance, out var state); return state; }
        public TT2TriggerState GetTriggerState() { tt2_get_trigger_state(_instance, out var state); return state; }
        public TT2ButtonState GetButtonState() { tt2_get_button_state(_instance, out var state); return state; }

        // --- Métodos de Configuração ---
        public void SetInvertX(bool v) => tt2_set_invert_x(_instance, v);
        public void SetInvertY(bool v) => tt2_set_invert_y(_instance, v);
        public void SetRateHz(int hz) => tt2_set_rate_hz(_instance, hz);
        public void SetExclusiveHandle(bool v) => tt2_set_exclusive_handle(_instance, v);
        public void SetClampMagnitude(double m) => tt2_set_clamp_magnitude(_instance, m);

        public bool SetSensitivity(double sensitivity) => tt2_set_sensitivity(_instance, sensitivity);
        public bool SetDeadzone(double deadzone) => tt2_set_deadzone(_instance, deadzone);
        public bool SetAntiDeadzone(double antiDeadzone) => tt2_set_anti_deadzone(_instance, antiDeadzone);

        public bool SetOutputProtocol(TT2_Proto p) => tt2_set_output_protocol(_instance, (int)p);
        public bool SetOutputPolling(TT2_Poll p) => tt2_set_output_polling(_instance, (int)p);
        public bool SetInputPolling(TT2_Poll p) => tt2_set_input_polling(_instance, (int)p);
        public bool SetHighSpeedMode(bool v) => tt2_set_prog_interface_full_speed(_instance, v);

        // --- Métodos de LED e Rumble ---
        public bool SetLedColor(byte r, byte g, byte b) => tt2_set_led_color(_instance, r, g, b);
        public bool SetRumble(byte left, byte right) => tt2_set_rumble(_instance, left, right);
        public void SetMacroFlags(uint flags) => tt2_set_macro_flags(_instance, flags);
        public void SetMacroArVertical(double val) => tt2_set_macro_ar_vertical(_instance, val);
        public void SetMacroArHorizontal(double val) => tt2_set_macro_ar_horizontal(_instance, val);
        public void SetMacroRapidFireMs(double val) => tt2_set_macro_rapid_fire_ms(_instance, val);
        public void SetPlayerBlend(double val) => tt2_set_player_blend(_instance, val);
        public void SetDisableOnMove(double val) => tt2_set_disable_on_move(_instance, val);
        public void SetQuickscopeDelay(double val) => tt2_set_quickscope_delay(_instance, val);
        public void SetJumpShotDelay(double val) => tt2_set_jump_shot_delay(_instance, val);
        public void SetBunnyHopDelay(double val) => tt2_set_bunny_hop_delay(_instance, val);
        public void SetStrafePower(double val) => tt2_set_strafe_power(_instance, val);
        public bool UploadGpc(byte[] data, byte slot, string name, string author) =>
            tt2_upload_gpc(_instance, data, (uint)data.Length, slot, name, author);

        /// <summary>Upload .gbc compilado (formato Gtuner real).</summary>
        public bool UploadGbc(byte[] gbcData, byte slot) =>
            tt2_upload_gbc(_instance, gbcData, (uint)gbcData.Length, slot);

        // --- Métodos de Slot e Memória ---
        public bool SlotLoad(byte n) => tt2_select_slot(_instance, n);
        public bool SlotUnload() => tt2_slot_stop(_instance);
        public bool SlotUp() => tt2_slot_up(_instance);
        public bool SlotDown() => tt2_slot_down(_instance);

        // --- Métodos de Configuração Persistente ---
        public bool SaveConfig() => tt2_save_config(_instance);
        public bool LoadConfig() => tt2_load_config(_instance);
        public bool FactoryReset() => tt2_factory_reset(_instance);

        public bool CloseGracefully() => tt2_close_graceful(_instance);
        public void ForceDisconnect() => tt2_force_disconnect(_instance);

        // Dispose Pattern — com timeout para não travar no destructor
        private bool _disposed;
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (_instance != IntPtr.Zero)
            {
                Stop();
                try
                {
                    var ptr = _instance;
                    var destroyTask = Task.Run(() => tt2_destroy(ptr));
                    if (!destroyTask.Wait(TimeSpan.FromSeconds(2)))
                        Console.WriteLine("[TitanTwo] WARN: tt2_destroy não terminou em 2s");
                }
                catch { }
                _instance = IntPtr.Zero;
            }
            _disposed = true;
        }
        ~TitanTwo() { Dispose(false); }
    }
}
