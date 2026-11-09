using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class FramePreferences
{
    private const string Firmware = "T-PTMDEUC-REL-202602250226";
    private const string Stock = "com.samsung.tv.csfs";
    private const string Launcher = "CJ6FnAfX3H.FrameHome";
    private const string LegacyLauncher = "FreeMyTVio.FreeMyTV";
    private const string Operation = "http://tizen.org/appcontrol/operation/main";
    private const string AutoKey = "db/menu/smart_hub/ontvsetting/auto_start";
    private static readonly string[] Keys = {
        "db/appfw/home_appid", "db/appfw/startup_appid",
        "db/appfw/booting_appid_backup", "db/first_screen/type"
    };

    [DllImport("libc.so.6")] private static extern uint getuid();
    [DllImport("libc.so.6")] private static extern void free(IntPtr value);
    [DllImport("libvconf.so.0")] private static extern IntPtr vconf_get_str(string key);
    [DllImport("libvconf.so.0")] private static extern int vconf_get_int(string key, out int value);
    [DllImport("libvconf.so.0")] private static extern int vconf_set_str(string key, string value);
    [DllImport("libvconf.so.0")] private static extern int vconf_set_int(string key, int value);
    [DllImport("libcapi-appfw-app-operation-tv.so.0")] private static extern int app_operation_get_current_app(string operation, out IntPtr value);
    [DllImport("libcapi-appfw-app-operation-tv.so.0")] private static extern int app_operation_set_current_app(string operation, string appId);
    [DllImport("libapp-context-manager-common.so")] private static extern byte util_is_appbooting_supported_webapp_byappid(string appId);
    [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_create(out IntPtr control);
    [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_destroy(IntPtr control);
    [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_set_app_id(IntPtr control, string appId);
    [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_add_extra_data(IntPtr control, string key, string value);
    [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_send_launch_request(IntPtr control, IntPtr callback, IntPtr userData);

    public sealed class Snapshot
    {
        public string Main { get; set; }
        public string[] Strings { get; set; }
        public int Auto { get; set; }
        public Snapshot Copy() => new Snapshot { Main = Main, Strings = (string[])Strings.Clone(), Auto = Auto };
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    private static void Check(int result, string action) => Require(result == 0, action + " returned " + result);

    private static string ReadString(IntPtr pointer)
    {
        Require(pointer != IntPtr.Zero, "Setting was unavailable.");
        try
        {
            string value = Marshal.PtrToStringAnsi(pointer);
            Require(!string.IsNullOrEmpty(value), "Setting was empty.");
            return value;
        }
        finally { free(pointer); }
    }

    private static Snapshot Read()
    {
        Check(app_operation_get_current_app(Operation, out var owner), "Read Home operation");
        string main = ReadString(owner);
        string[] strings = Keys.Select(key => ReadString(vconf_get_str(key))).ToArray();
        Check(vconf_get_int(AutoKey, out var auto), "Read Smart Hub autostart");
        return new Snapshot { Main = main, Strings = strings, Auto = auto };
    }

    private static bool Same(Snapshot first, Snapshot second) =>
        first.Main == second.Main && first.Auto == second.Auto && first.Strings.SequenceEqual(second.Strings);

    private static void LaunchControl(string target, params (string Key, string Value)[] data)
    {
        Check(app_control_create(out var control), "Create application request");
        try
        {
            Check(app_control_set_app_id(control, target), "Set application target");
            foreach (var pair in data)
                Check(app_control_add_extra_data(control, pair.Key, pair.Value), "Set application route");
            Check(app_control_send_launch_request(control, IntPtr.Zero, IntPtr.Zero), "Send application request");
        }
        finally { app_control_destroy(control); }
    }

    private static void Set(Snapshot desired, bool restore, int permittedPreference = -1)
    {
        var state = Read();
        if (state.Main != desired.Main)
        {
            Check(app_operation_set_current_app(Operation, desired.Main), "Set Home operation");
            Require(Read().Main == desired.Main, "Home operation readback differed.");
        }
        for (int index = 0; index < Keys.Length; index++)
        {
            if (Read().Strings[index] == desired.Strings[index]) continue;
            Require(index == 0 || restore || index == permittedPreference, "Unexpected change to " + Keys[index]);
            Check(vconf_set_str(Keys[index], desired.Strings[index]), "Set " + Keys[index]);
            Require(Read().Strings[index] == desired.Strings[index], "Readback differed for " + Keys[index]);
        }
        if (Read().Auto != desired.Auto)
            Check(vconf_set_int(AutoKey, desired.Auto), "Set Smart Hub autostart");
        Require(Same(Read(), desired), "Final settings readback differed.");
    }

    public static int Main(string[] args)
    {
        try
        {
            Require(getuid() == 901 && IntPtr.Size == 4 && RuntimeInformation.ProcessArchitecture == Architecture.Arm,
                "Expected the TV SDK account on 32-bit ARM.");
            Require(File.ReadLines("/etc/os-release").Any(line => line.StartsWith("BUILD_ID=") && line.Substring(9).Trim('"') == Firmware),
                "Unexpected firmware.");
            Snapshot current = Read();
            if (args.Length == 1 && (args[0] == "settings-open" || args[0] == "settings-menu" || args[0] == "disney-store" || args[0] == "welcome-close"))
            {
                switch (args[0])
                {
                    case "settings-open": LaunchControl(Stock, ("OnItemClick", "s/setting.layer.more")); break;
                    case "settings-menu": LaunchControl("org.tizen.menu", ("Entry", "s/setting")); break;
                    case "disney-store": LaunchControl("com.samsung.tv.store", ("Sub_Menu", "setting"), ("widget_id", "3201901017640")); break;
                    case "welcome-close": LaunchControl("org.tizen.welcome-mode", ("mode", "terminate")); break;
                }
                Console.WriteLine("Application request accepted by the TV.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "probe")
            {
                Console.WriteLine(JsonSerializer.Serialize(new { state = current, launcherBootSupported = util_is_appbooting_supported_webapp_byappid(Launcher) == 1 }));
                return 0;
            }
            Require(args.Length == 2, "Expected an action and saved baseline.");
            Snapshot before = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(args[1]));
            Require(before != null && before.Strings?.Length == Keys.Length && Same(current, before), "Current values differ from the saved baseline.");
            Snapshot desired = before.Copy();
            int permittedPreference = -1;
            switch (args[0])
            {
                case "hub-off":
                    Require(before.Auto == 1, "Expected Smart Hub autostart to be on.");
                    desired.Auto = 0;
                    break;
                case "hub-on":
                    Require(before.Auto == 0, "Expected Smart Hub autostart to be off.");
                    desired.Auto = 1;
                    break;
                case "home-apply":
                    Require(before.Main == Stock && before.Strings[0] == Stock, "Expected stock Home.");
                    Require(util_is_appbooting_supported_webapp_byappid(Launcher) == 1, "The TV does not mark CleanFrame eligible.");
                    desired.Main = desired.Strings[0] = Launcher;
                    break;
                case "home-restore":
                    Require(before.Main == Launcher && before.Strings[0] == Launcher, "Expected CleanFrame Home.");
                    desired.Main = desired.Strings[0] = Stock;
                    break;
                case "replace-free-my-tv-backup":
                    Require(before.Main == Launcher && before.Strings[0] == Launcher && before.Strings[1] == Launcher,
                            "Expected CleanFrame Home and startup.");
                    Require(before.Strings[2] == LegacyLauncher, "Expected Free My TV boot backup.");
                    desired.Strings[2] = Launcher;
                    permittedPreference = 2;
                    break;
                case "art-home-apply":
                case "art-home-restore":
                    Require(before.Main == Launcher && before.Strings[0] == Launcher, "Expected CleanFrame Home.");
                    bool apply = args[0] == "art-home-apply";
                    Require(before.Strings[3] == (apply ? Stock : Launcher), "Unexpected first-screen destination.");
                    desired.Strings[3] = apply ? Launcher : Stock;
                    permittedPreference = 3;
                    break;
                default: throw new InvalidOperationException("Unknown action.");
            }
            bool autostartOnly = args[0] == "hub-off" || args[0] == "hub-on";
            try
            {
                if (autostartOnly)
                {
                    Check(vconf_set_int(AutoKey, desired.Auto), "Set Smart Hub autostart");
                    var observed = Read();
                    Require(observed.Auto == desired.Auto && observed.Main == before.Main &&
                        observed.Strings[0] == before.Strings[0] && observed.Strings[3] == before.Strings[3],
                        "Autostart or Home readback differed.");
                }
                else Set(desired, false, permittedPreference);
            }
            catch
            {
                if (autostartOnly)
                {
                    Check(vconf_set_int(AutoKey, before.Auto), "Restore Smart Hub autostart");
                    Require(Read().Auto == before.Auto, "Restored autostart readback differed.");
                }
                else Set(before, true);
                Console.Error.WriteLine("Original values restored and verified.");
                throw;
            }
            Console.WriteLine(JsonSerializer.Serialize(Read()));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
