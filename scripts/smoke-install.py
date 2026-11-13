"""Run the published Endpoint Service Desk executable against disposable data."""

import argparse
from collections import deque
from contextlib import closing, contextmanager
from html.parser import HTMLParser
from http.cookiejar import CookieJar
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin
from urllib.request import HTTPCookieProcessor, ProxyHandler, Request, build_opener


SETUP_CODE = re.compile(r"Setup code: ([A-F0-9]+)")
NEW_USER = "smoke-technician"
NEW_PASSWORD = "SmokeTest!2026"
LEGACY_USER = "demo-tech"
LEGACY_PASSWORD = "LegacySmoke!2026"
LEGACY_NOTE = "Preserve this audit event from v0.1.0."


class Client:
    def __init__(self, base_url):
        self.base_url = base_url
        self.opener = build_opener(ProxyHandler({}), HTTPCookieProcessor(CookieJar()))

    def request(self, path, *, method="GET", body=None, csrf=None, expected=200):
        headers = {}
        payload = None
        if body is not None:
            headers["Content-Type"] = "application/json"
            payload = json.dumps(body).encode("utf-8")
        if csrf is not None:
            headers["X-CSRF-TOKEN"] = csrf
        url = urljoin(self.base_url, path)
        request = Request(url, data=payload, headers=headers, method=method)
        try:
            response = self.opener.open(request, timeout=5)
        except HTTPError as error:
            response = error
        with response:
            content = response.read()
            expected_codes = (expected,) if isinstance(expected, int) else expected
            if response.status not in expected_codes:
                detail = content[:300].decode("utf-8", errors="replace")
                raise AssertionError(f"{method} {path}: HTTP {response.status}, expected {expected_codes}. {detail}")
            return content, response.headers.get_content_type()

    def get_json(self, path, *, expected=200):
        content, _ = self.request(path, expected=expected)
        return json.loads(content)

    def write_json(self, method, path, body, *, expected=200):
        csrf = self.get_json("/api/auth/csrf")["requestToken"]
        content, _ = self.request(path, method=method, body=body, csrf=csrf, expected=expected)
        return json.loads(content) if content else None

    def sign_in(self, user_name, password):
        self.write_json("POST", "/api/auth/sign-in", {"userName": user_name, "password": password})
        identity = self.get_json("/api/auth/me")
        assert identity["userName"] == user_name, identity
        assert "Technician" in identity["roles"], identity


class AssetLinks(HTMLParser):
    def __init__(self):
        super().__init__()
        self.scripts = []
        self.stylesheets = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "script" and attrs.get("type") == "module" and attrs.get("src"):
            self.scripts.append(attrs["src"])
        if tag == "link" and attrs.get("rel") == "stylesheet" and attrs.get("href"):
            self.stylesheets.append(attrs["href"])


def check_web(client):
    page, content_type = client.request("/")
    assert content_type == "text/html", content_type
    assert b"Endpoint Service Desk" in page
    for route in ("/tickets/1", "/sign-in", "/setup"):
        content, content_type = client.request(route)
        assert content_type == "text/html" and content == page, route
    _, content_type = client.request("/api/unknown-smoke-route", expected=404)
    assert content_type != "text/html", content_type

    assets = AssetLinks()
    assets.feed(page.decode("utf-8"))
    assert assets.scripts and assets.stylesheets, "Published page does not link to built JavaScript and CSS"
    for path in assets.scripts:
        content, content_type = client.request(path)
        assert content and content_type in ("application/javascript", "text/javascript"), path
    for path in assets.stylesheets:
        content, content_type = client.request(path)
        assert content and content_type == "text/css", path


def available_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


@contextmanager
def running_install(executable, working_directory, data_directory):
    base_url = f"http://127.0.0.1:{available_port()}"
    environment = {
        key: value for key, value in os.environ.items()
        if not key.lower().startswith(("servicedesk__", "demoaccounts__", "connectionstrings__"))
    }
    environment.update({
        "ServiceDesk__DataDirectory": str(data_directory),
        "ASPNETCORE_ENVIRONMENT": "Production",
        "DOTNET_ENVIRONMENT": "Production",
    })
    with subprocess.Popen(
        [str(executable), "--urls", base_url],
        cwd=working_directory,
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    ) as process:
        output = deque(maxlen=150)
        codes = []

        def read_output():
            for line in process.stdout:
                match = SETUP_CODE.search(line)
                if match:
                    codes.append(match[1])
                output.append(SETUP_CODE.sub("Setup code: <redacted>", line.rstrip()))

        reader = threading.Thread(target=read_output, daemon=True)
        reader.start()
        try:
            client = Client(base_url)
            deadline = time.monotonic() + 45
            while True:
                if process.poll() is not None:
                    raise RuntimeError(f"Packaged app exited during startup with code {process.returncode}")
                try:
                    health = client.get_json("/api/health")
                    assert health["status"] == "ok", health
                    break
                except URLError:
                    if time.monotonic() >= deadline:
                        raise RuntimeError("Packaged app did not respond on its loopback port within 45 seconds")
                    time.sleep(0.2)
            yield client, codes
        except Exception as error:
            error.add_note("Packaged app output:\n" + "\n".join(tuple(output)))
            raise
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
            reader.join(timeout=5)


def get_setup_code(codes):
    deadline = time.monotonic() + 5
    while not codes:
        if time.monotonic() >= deadline:
            raise RuntimeError("Packaged app did not print the one-time setup code")
        time.sleep(0.1)
    return codes[0]


def check_fresh_install(executable, working_directory, data_directory):
    with running_install(executable, working_directory, data_directory) as (client, codes):
        check_web(client)
        assert client.get_json("/api/setup/status") == {"required": True}
        client.request("/api/tickets/1", expected=401)
        setup_path = "/api/setup/technician"
        setup = {"userName": NEW_USER, "password": NEW_PASSWORD, "setupCode": get_setup_code(codes)}
        client.request(setup_path, method="POST", body=setup, expected=400)
        bad_code = "0" * len(setup["setupCode"])
        if bad_code == setup["setupCode"]:
            bad_code = "F" * len(bad_code)
        client.write_json("POST", setup_path, {**setup, "setupCode": bad_code}, expected=(400, 403))
        assert client.get_json("/api/setup/status") == {"required": True}
        assert client.write_json("POST", setup_path, setup, expected=(200, 201)) == {"created": True}
        assert client.get_json("/api/setup/status") == {"required": False}
        client.write_json("POST", setup_path, setup, expected=409)

        client.sign_in(NEW_USER, NEW_PASSWORD)
        ticket = client.write_json(
            "POST", "/api/tickets", {"assetId": 1, "summary": "Installed smoke test", "priority": "P2"},
            expected=201,
        )
        ticket_path = f"/api/tickets/{ticket['id']}"
        client.write_json("PATCH", f"{ticket_path}/status", {"expectedStatus": "open", "status": "in-progress"})
        client.write_json("PATCH", f"{ticket_path}/status", {
            "expectedStatus": "in-progress", "status": "resolved", "note": "Installed ticket audit entry.",
        })

    assert (data_directory / "servicedesk.db").is_file(), "Database not found in the configured data directory"
    assert not (working_directory / "servicedesk.db").exists(), "Database was written to the working directory"

    with running_install(executable, working_directory, data_directory) as (client, _):
        assert client.get_json("/api/setup/status") == {"required": False}
        client.sign_in(NEW_USER, NEW_PASSWORD)
        ticket = client.get_json(ticket_path)
        assert ticket["summary"] == "Installed smoke test" and ticket["status"] == "resolved", ticket
        assert [event["newStatus"] for event in ticket["history"]] == ["in-progress", "resolved"], ticket
        assert ticket["history"][-1]["note"] == "Installed ticket audit entry.", ticket


def check_upgrade(executable, working_directory, data_directory):
    fixture = Path(__file__).parent / "fixtures" / "v0.1.0-upgrade.sql"
    data_directory.mkdir()
    with closing(sqlite3.connect(data_directory / "servicedesk.db")) as database:
        database.executescript(fixture.read_text(encoding="utf-8"))
    with running_install(executable, working_directory, data_directory) as (client, _):
        assert client.get_json("/api/setup/status") == {"required": False}
        client.sign_in(LEGACY_USER, LEGACY_PASSWORD)
        ticket = client.get_json("/api/tickets/1")
        assert ticket["status"] == "resolved", ticket
        assert [event["newStatus"] for event in ticket["history"]] == ["in-progress", "resolved"], ticket
        assert ticket["history"][-1]["note"] == LEGACY_NOTE, ticket


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path, help="Path to the extracted ServiceDesk.Api executable")
    args = parser.parse_args()
    executable = args.executable.resolve()
    if not executable.is_file():
        parser.error(f"Executable does not exist: {executable}")

    with tempfile.TemporaryDirectory(prefix="endpoint-service-desk-smoke-") as folder:
        root = Path(folder)
        working_directory = root / "working-directory"
        working_directory.mkdir()
        check_fresh_install(executable, working_directory, root / "fresh-data")
        print("Fresh install, technician setup, packaged UI, ticket history, and restart passed.")
        check_upgrade(executable, working_directory, root / "upgrade-data")
        print("Upgrade from v0.1.0 preserved the technician account and ticket history.")


if __name__ == "__main__":
    main()
