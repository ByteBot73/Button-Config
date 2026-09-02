using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;

namespace Button_Config
{
    public partial class Form1 : Form
    {
        private AppSettings _settings = new AppSettings();
        private string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopilotRemapperSettings.json");

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_F23 = 0x86;
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        private TextBox txtTarget;
        private Button btnSave;
        private NotifyIcon trayIcon;

        public Form1()
        {
            LoadSettings();
            SetupUI();
            SetupTray();

            _proc = HookCallback;
            _hookID = SetHook(_proc);

            this.Text = "Copilot Key Configurator";
            this.Size = new System.Drawing.Size(400, 200);
        }

        private void SetupUI()
        {
            Label lbl = new Label() { Text = "Enter App Path or URL:", Left = 20, Top = 20, Width = 200 };
            txtTarget = new TextBox() { Left = 20, Top = 50, Width = 340, Text = _settings.Target };
            btnSave = new Button() { Text = "Save & Hide", Left = 285, Top = 90 };
            
            btnSave.Click += (s, e) => {
                _settings.Target = txtTarget.Text;
                SaveSettings();
                this.Hide(); 
            };

            this.Controls.Add(lbl);
            this.Controls.Add(txtTarget);
            this.Controls.Add(btnSave);
        }

        private void SetupTray()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "Copilot Remapper"
            };
            
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
            
            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings", null, (s, e) => { this.Show(); });
            menu.Items.Add("Exit", null, (s, e) => { Application.Exit(); });
            trayIcon.ContextMenuStrip = menu;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_F23)
                {
                    bool winDown = (GetKeyState(0x5B) & 0x8000) != 0;
                    bool shiftDown = (GetKeyState(0xA0) & 0x8000) != 0;

                    if (winDown && shiftDown)
                    {
                        try {
                            Process.Start(new ProcessStartInfo(_settings.Target) { UseShellExecute = true });
                        } catch (Exception ex) {
                            MessageBox.Show("Error launching action: " + ex.Message);
                        }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void LoadSettings() {
            try {
                if (File.Exists(_settingsPath)) {
                    string json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json);
                }
            } catch { }
        }

        private void SaveSettings() {
            try {
                string json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(_settingsPath, json);
            } catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }

    // THIS IS THE CLASS THAT WAS MISSING
    public class AppSettings {
        public string ActionType { get; set; } = "App";
        public string Target { get; set; } = "notepad.exe";
    }
}