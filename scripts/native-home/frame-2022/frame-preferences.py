from pathlib import Path
import argparse
import json
from aerotizen import home, remote, sdk
from aerotizen.device import DeviceSession

HERE = Path(__file__).resolve().parent
BUILD = HERE / "native-preferences/bin/Release/netcoreapp3.1"
FIRMWARE = "T-PTMDEUC-REL-202602250226"
KEYS = ("db/appfw/home_appid", "db/appfw/startup_appid", "db/appfw/booting_appid_backup", "db/first_screen/type")
AUTO_KEY = "db/menu/smart_hub/ontvsetting/auto_start"

def snapshot(status):
    owner = status["home"]["main_operation"]
    settings = status["home"]["settings"]
    if owner["status"] != "ok" or any(settings[key]["status"] != "ok" for key in (*KEYS, AUTO_KEY)):
        raise RuntimeError("Native settings baseline is incomplete.")
    return {"Main": owner["value"], "Strings": [settings[key]["value"] for key in KEYS], "Auto": settings[AUTO_KEY]["value"]}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("probe", "hub-off", "hub-on", "home-apply", "home-restore", "replace-free-my-tv-backup", "art-home-apply", "art-home-restore", "settings-open", "settings-menu", "disney-store", "welcome-close"))
    parser.add_argument("--host", required=True, help="The TV address on your local network.")
    parser.add_argument("--state-dir", type=Path, required=True, help="Private directory containing native-home-readback.json.")
    parser.add_argument("--record", default="diagnostic")
    args = parser.parse_args()
    if not args.record.isascii() or not args.record.replace("-", "").isalnum():
        raise SystemExit("Invalid local record name.")
    original = json.loads((args.state_dir / "native-home-readback.json").read_text(encoding="utf-8"))
    info = remote.info(args.host)
    if info.get("modelName") != original["device"]["model"] or info.get("model") != original["device"]["platform"]:
        raise RuntimeError("Unexpected TV model.")
    names = ("FramePreferences.dll", "FramePreferences.runtimeconfig.json")
    files = {name: (BUILD / name).read_bytes() for name in names}
    with DeviceSession(args.host, timeout=12, direct=True, lock_dir=args.state_dir / "sessions") as tv:
        state = home.read_status(tv, None, info)
        if state["device"]["firmware_build"] != FIRMWARE or state["device"]["identity_sha256"] != original["device"]["identity_sha256"]:
            raise RuntimeError("Unexpected TV or firmware.")
        current = snapshot(state)
        read_only = args.action in ("probe", "settings-open", "settings-menu", "disney-store", "welcome-close")
        if read_only:
            parameters = (args.action,)
        else:
            record = args.state_dir / ("preferences-before-" + args.record + ".json")
            with record.open("x", encoding="utf-8") as stream:
                json.dump({"device": state["device"], "state": current, "action": args.action}, stream, indent=2)
            files["frame-baseline.json"] = json.dumps(current).encode()
            parameters = (args.action, "frame-baseline.json")
        result = sdk.run(tv, files, names[0], parameters)
        print(result["stdout"].strip(), flush=True)
        if result["exit_code"] != 0:
            raise SystemExit(result["exit_code"])
        actual = snapshot(home.read_status(tv, None, info))
        if not read_only:
            print("Independent native readback:", json.dumps(actual), flush=True)
            with (args.state_dir / ("preferences-after-" + args.record + ".json")).open("x", encoding="utf-8") as stream:
                json.dump({"device": state["device"], "state": actual}, stream, indent=2)

if __name__ == "__main__":
    main()
