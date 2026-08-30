"""Clone the subscriber Capacitor app into collection/employee variants."""
from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
APPS = Path(__file__).resolve().parent
MARK = ROOT / "RadaTik" / "wwwroot" / "images" / "brand" / "radatik-mark.png"
CLIENT = APPS / "radatik-client"

IGNORE_DIRS = {
    "node_modules",
    ".gradle",
    "build",
    "Pods",
    ".idea",
}

ROLES = [
    {
        "folder": "radatik-collection",
        "app_id": "com.radatik.collection",
        "app_name": "RadaTik التحصيل",
        "subtitle": "بوابة نقطة التحصيل",
        "url": "https://radatik.com/collectionPoint/dashboard",
        "scheme": "radatik-collection",
        "bg": (236, 253, 245, 255),
        "accent": (4, 120, 87, 255),
        "splash": "#047857",
    },
    {
        "folder": "radatik-employee",
        "app_id": "com.radatik.employee",
        "app_name": "RadaTik الموظف",
        "subtitle": "بوابة الموظف",
        "url": "https://radatik.com/employee/dashboard",
        "scheme": "radatik-employee",
        "bg": (239, 246, 255, 255),
        "accent": (29, 78, 216, 255),
        "splash": "#1d4ed8",
    },
]


def ignored(directory: str, names: list[str]) -> set[str]:
    return {name for name in names if name in IGNORE_DIRS}


def replace_in_file(path: Path, replacements: list[tuple[str, str]]) -> None:
    if not path.is_file() or path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp", ".apk"}:
        return
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return
    original = text
    for old, new in replacements:
        text = text.replace(old, new)
    if text != original:
        path.write_text(text, encoding="utf-8")


def write_icons(dest_app: Path, bg: tuple[int, int, int, int], accent: tuple[int, int, int, int]) -> None:
    src = Image.open(MARK).convert("RGBA")
    android = dest_app / "android" / "app" / "src" / "main" / "res"
    ios_icon = dest_app / "ios" / "App" / "App" / "Assets.xcassets" / "AppIcon.appiconset" / "AppIcon-512@2x.png"

    def fit_logo(size: int, pad_ratio: float) -> Image.Image:
        canvas = Image.new("RGBA", (size, size), bg)
        inner = max(1, int(size * (1 - 2 * pad_ratio)))
        logo = src.copy()
        logo.thumbnail((inner, inner), Image.Resampling.LANCZOS)
        x = (size - logo.width) // 2
        y = (size - logo.height) // 2
        canvas.alpha_composite(logo, (x, y))
        bar = max(3, size // 14)
        draw = ImageDraw.Draw(canvas)
        draw.rectangle((0, size - bar, size, size), fill=accent)
        return canvas

    sizes = {
        "mipmap-mdpi": 48,
        "mipmap-hdpi": 72,
        "mipmap-xhdpi": 96,
        "mipmap-xxhdpi": 144,
        "mipmap-xxxhdpi": 192,
    }
    fg_sizes = {
        "mipmap-mdpi": 108,
        "mipmap-hdpi": 162,
        "mipmap-xhdpi": 216,
        "mipmap-xxhdpi": 324,
        "mipmap-xxxhdpi": 432,
    }
    for folder, size in sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        icon = fit_logo(size, 0.12)
        icon.save(dest / "ic_launcher.png")
        icon.save(dest / "ic_launcher_round.png")
    for folder, size in fg_sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        fit_logo(size, 0.22).save(dest / "ic_launcher_foreground.png")
    if ios_icon.parent.exists():
        fit_logo(1024, 0.12).save(ios_icon)
    fit_logo(512, 0.10).save(dest_app / "www" / "icon.png")


def clone_role(role: dict) -> Path:
    dest = APPS / role["folder"]
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(CLIENT, dest, ignore=ignored)

    old_java = dest / "android" / "app" / "src" / "main" / "java" / "com" / "radatik" / "client"
    new_java = dest / "android" / "app" / "src" / "main" / "java" / "com" / "radatik" / role["folder"].split("-")[-1]
    if old_java.exists():
        new_java.parent.mkdir(parents=True, exist_ok=True)
        if new_java.exists():
            shutil.rmtree(new_java)
        shutil.move(str(old_java), str(new_java))
        main = new_java / "MainActivity.java"
        if main.exists():
            main.write_text(
                f'package {role["app_id"]};\n\nimport com.getcapacitor.BridgeActivity;\n\npublic class MainActivity extends BridgeActivity {{}}\n',
                encoding="utf-8",
            )

    replacements = [
        ("com.radatik.client", role["app_id"]),
        ("radatik-client", role["scheme"]),
        ("https://radatik.com/clientPortal/dashboard", role["url"]),
        ("بوابة المشترك", role["subtitle"]),
    ]
    for path in dest.rglob("*"):
        if path.is_file() and "node_modules" not in path.parts:
            replace_in_file(path, replacements)

    config_path = dest / "capacitor.config.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    config["appId"] = role["app_id"]
    config["appName"] = role["app_name"]
    config["server"]["url"] = role["url"]
    config["ios"]["scheme"] = role["scheme"]
    config["plugins"]["SplashScreen"]["backgroundColor"] = role["splash"]
    config["plugins"]["StatusBar"]["backgroundColor"] = role["splash"]
    config_path.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    strings = dest / "android" / "app" / "src" / "main" / "res" / "values" / "strings.xml"
    strings.write_text(
        "<?xml version='1.0' encoding='utf-8'?>\n"
        "<resources>\n"
        f"    <string name=\"app_name\">{role['app_name']}</string>\n"
        f"    <string name=\"title_activity_main\">{role['app_name']}</string>\n"
        f"    <string name=\"package_name\">{role['app_id']}</string>\n"
        f"    <string name=\"custom_url_scheme\">{role['app_id']}</string>\n"
        "</resources>\n",
        encoding="utf-8",
    )

    pkg = dest / "package.json"
    package = json.loads(pkg.read_text(encoding="utf-8"))
    package["name"] = role["folder"]
    package["description"] = f"تطبيق {role['app_name']}"
    pkg.write_text(json.dumps(package, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    write_icons(dest, role["bg"], role["accent"])

    import subprocess

    node_modules = dest / "node_modules"
    if not node_modules.exists():
        source = CLIENT / "node_modules"
        try:
            node_modules.symlink_to(source, target_is_directory=True)
        except OSError:
            subprocess.check_call(["cmd", "/c", "mklink", "/J", str(node_modules), str(source)])

    return dest


def update_client_name() -> None:
    strings = CLIENT / "android" / "app" / "src" / "main" / "res" / "values" / "strings.xml"
    strings.write_text(
        "<?xml version='1.0' encoding='utf-8'?>\n"
        "<resources>\n"
        "    <string name=\"app_name\">RadaTik المشترك</string>\n"
        "    <string name=\"title_activity_main\">RadaTik المشترك</string>\n"
        "    <string name=\"package_name\">com.radatik.client</string>\n"
        "    <string name=\"custom_url_scheme\">com.radatik.client</string>\n"
        "</resources>\n",
        encoding="utf-8",
    )
    config_path = CLIENT / "capacitor.config.json"
    config = json.loads(config_path.read_text(encoding="utf-8"))
    config["appName"] = "RadaTik المشترك"
    config_path.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> None:
    update_client_name()
    for role in ROLES:
        dest = clone_role(role)
        print(f"created {dest}")


if __name__ == "__main__":
    main()
