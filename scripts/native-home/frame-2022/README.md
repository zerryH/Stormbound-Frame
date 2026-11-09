# Native Home on the 2022 Frame

This is the local source used for the QE55LS03BAUXXH with Samsung firmware T-PTMDEUC-1720.7 (internal build T-PTMDEUC-REL-202602250226). The helper requires the TV SDK account (UID 901), 32-bit ARM and that exact internal build. The app target in Program.cs is the package ID of the owner’s signed installation. A different signed installation requires reviewing and rebuilding the helper for its application ID.

The Home action checks the current Home operation and Home app ID, verifies the launcher is eligible and changes both values. The Art Mode action separately changes the first-screen destination. Home restore and Art Mode restore reverse those respective bindings. The helper reads the resulting values and attempts to restore its starting state if a write fails. Keep an independent copy of your original state and a working developer connection before using any write action. Settings and Store actions request the original Samsung screens.

## Build and inspect

Build the C# source with a .NET SDK that supports targeting netcoreapp3.1 and can resolve that targeting pack. Install the AeroTizen CLI source with its native Home query helper and SDK transport. Configure SDB and your TV’s existing developer authorization separately. The tool expects the full device-bound readback from AeroTizen home.read_status saved locally as native-home-readback.json. The report includes device identity and must stay out of Git. Keep the original pre-change report, plus transaction receipts, outside this repository.

```sh
dotnet build scripts/native-home/frame-2022/native-preferences -c Release
python scripts/native-home/frame-2022/frame-preferences.py probe --host TV_IP --state-dir PRIVATE_STATE_DIRECTORY
```

The probe stages a temporary helper on the TV and reads native settings. The runner takes a fresh snapshot before every write and uses the supplied state directory for per-run before and after receipts. Use a distinct `--record` for each action. The separate upstream profiles elsewhere in this directory target other devices.
