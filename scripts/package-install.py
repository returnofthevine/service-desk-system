"""Assemble a platform archive from a self-contained publish and a Vite build."""

import argparse
from pathlib import Path
import shutil
import tempfile


APP_NAME = "endpoint-service-desk"
RUNTIMES = (
    "win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64",
)


def package(runtime: str, published: Path, web: Path, output: Path) -> Path:
    windows = runtime.startswith("win-")
    launcher = "ServiceDesk.Api.exe" if windows else "ServiceDesk.Api"
    if not (published / launcher).is_file() or not (web / "index.html").is_file():
        raise ValueError("Expected a self-contained API publish and a built web directory.")

    command = f".\\{launcher}" if windows else f"./{launcher}"
    launch = f"Open a terminal in this folder and run {command}."
    if windows:
        launch = f"Double-click {launcher}, or run {command} in PowerShell."

    quickstart = [
        "Endpoint Service Desk",
        "",
        f"1. {launch}",
        "2. Keep the terminal open and visit http://127.0.0.1:5087/ in your browser.",
        "3. On first launch, enter the setup code shown in the terminal and create",
        "   a technician username and password.",
        "4. To stop the app, press Ctrl+C in its terminal.",
        "",
        "For an upgrade, stop the running app, extract the new archive to a new",
        "folder, and launch it. The app keeps its database in your user data folder.",
    ]
    if runtime.startswith("osx-"):
        quickstart += ["", "This build has no Developer ID signature or notarization."]
    elif runtime.startswith("linux-"):
        quickstart += ["", "Use a supported glibc-based Linux distribution."]

    output.mkdir(parents=True, exist_ok=True)
    archive = output.resolve() / f"{APP_NAME}-{runtime}"
    with tempfile.TemporaryDirectory(prefix="endpoint-service-desk-package-") as staging:
        package_root = Path(staging) / APP_NAME
        shutil.copytree(published, package_root)
        shutil.copytree(web, package_root / "wwwroot", dirs_exist_ok=True)
        (package_root / "service-desk-installed").touch()
        (package_root / "QUICKSTART.txt").write_text("\n".join(quickstart) + "\n", encoding="utf-8")
        return Path(shutil.make_archive(str(archive), "zip" if windows else "gztar", staging, APP_NAME))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runtime", choices=RUNTIMES)
    parser.add_argument("published", type=Path)
    parser.add_argument("web", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    print(package(args.runtime, args.published, args.web, args.output))


if __name__ == "__main__":
    main()
