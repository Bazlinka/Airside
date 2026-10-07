#!/usr/bin/env python3
"""Exercise the local design study with Chromium; never launch Unity or touch its saves."""
import argparse
import json
import subprocess
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]


def run(base_url, out):
    out.mkdir(parents=True, exist_ok=True)
    report = {"prototype_only": True, "checks": [], "page_errors": [], "failed_resources": [], "layouts": []}
    model_path = ROOT / 'docs/art/player-flow-prototype-2026-10-07/flow-model.js'
    model_checks = """
const assert=require('node:assert/strict'), M=require(process.argv[1]);
for(const scenario of ['starter','growing','recovery'])assert(M.validSave(M.create(scenario)));
const malformed=M.create();malformed.fleet[0].job={};assert(!M.validSave(malformed));
const injected=M.create();injected.colour='red; background:url(https://example.com)';assert(!M.validSave(injected));
const s=M.create('growing');assert(M.action(s,'schedule',{id:'VH-PAX',route:'KGC',departure:s.time+15}).ok);
const booked=s.funds;assert(!M.action(s,'schedule',{id:'VH-PAX',route:'KGC',departure:s.time+15}).ok);assert.equal(s.funds,booked);
M.settle(s,s.fleet[0]);const settled=s.funds;assert(!M.settle(s,s.fleet[0]).ok);assert.equal(s.funds,settled);
assert(!M.action(s,'stand',{id:'VH-PAX',stand:'G1'}).ok);
const fleet=M.create('growing');fleet.fleet.push({...M.copy(fleet.fleet[2]),id:'VH-O02'});
assert(M.action(fleet,'ferry',{id:'VH-O01'}).ok);assert(M.action(fleet,'ferry',{id:'VH-O02'}).ok);
assert.notEqual(fleet.fleet[2].booking.stand,fleet.fleet[3].booking.stand);
console.log('Model integrity passed: save validation, duplicate commands/settlement, incompatible stand and inbound ferry reservations.');
"""
    subprocess.run(['node', '-e', model_checks, str(model_path)], check=True)
    report['model_integrity'] = 'passed'
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path="/usr/bin/chromium", headless=True, args=["--no-sandbox"])

        def fresh(scenario=None, width=1440, height=900):
            context = browser.new_context(viewport={"width": width, "height": height})
            page = context.new_page()
            page.set_default_timeout(5000)
            page.on("pageerror", lambda error: report["page_errors"].append(str(error)))
            page.on("response", lambda r: report["failed_resources"].append({"url": r.url, "status": r.status}) if r.status >= 400 and not r.url.endswith("favicon.ico") else None)
            page.goto(base_url + ("?scenario=" + scenario if scenario else ""))
            return context, page

        def action(page, name, arg=None, scope=None):
            selector = f'[data-action="{name}"]'
            if arg is not None:
                selector += f'[data-arg="{arg}"]'
            (scope or page).locator(selector).first.click()

        def snap(page):
            return page.evaluate("FlowStudy.snapshot()")

        def book(page, aircraft="VH-PAX", route="KGC"):
            action(page, "plan", aircraft)
            page.locator("#plan-route").select_option(route)
            action(page, "plan-review")
            action(page, "confirm")

        def check(name):
            report["checks"].append(name)
            print("PASS", name, flush=True)

        context, page = fresh()
        action(page, "new")
        page.locator("#setup-name").fill("Southern Study")
        page.locator("#setup-code").fill("SS")
        page.get_by_role("button", name="Continue", exact=True).click()
        assert page.locator("#setup-colour").is_visible()
        page.get_by_role("button", name="Continue", exact=True).click()
        page.get_by_role("button", name="Start airline", exact=True).click()
        assert snap(page)["name"] == "Southern Study" and snap(page)["funds"] == 2253
        action(page, "nav", "career")
        action(page, "pin", "fleet")
        assert snap(page)["goal"] == "fleet"
        action(page, "tab", "identity")
        page.locator("#identity-name").fill("My Regional Airline")
        page.get_by_role("button", name="Apply identity", exact=True).click()
        assert snap(page)["name"] == "My Regional Airline"
        check("F01/F12/F22: setup, goal pin and airline identity")
        context.close()

        context, page = fresh("starter")
        action(page, "plan", "VH-PAX")
        action(page, "plan-review")
        page.keyboard.press("Escape")
        assert snap(page)["fleet"][0]["booking"] is None
        action(page, "nav", "network")
        action(page, "back")
        assert page.locator("#plan-route").input_value() == "KGC"
        action(page, "plan-review")
        action(page, "confirm")
        assert snap(page)["funds"] == 1916
        action(page, "watch", "VH-PAX")
        action(page, "camera", "Cockpit")
        assert page.locator(".view-frame img").evaluate("e => e.complete && e.naturalWidth > 0")
        action(page, "profile", "VH-PAX")
        for _ in range(7):
            action(page, "advance", "VH-PAX")
        s = snap(page)
        assert len(s["history"]) == 1 and s["funds"] == 2441 and s["fleet"][0]["booking"] is None
        action(page, "menu")
        action(page, "quit")
        action(page, "continue")
        assert len(snap(page)["history"]) == 1 and snap(page)["funds"] == 2441
        check("F04/F05/F09/F10/F24: review/back, booking, camera, settlement once and save/return")
        context.close()

        context, page = fresh("growing")
        book(page)
        action(page, "cancel-review", "VH-PAX")
        page.keyboard.press("Escape")
        assert snap(page)["fleet"][0]["booking"] is not None
        action(page, "cancel-review", "VH-PAX")
        action(page, "confirm")
        assert snap(page)["fleet"][0]["booking"] is None and snap(page)["reliability"] == 99
        action(page, "stand-review", "VH-PAX")
        page.locator("#stand-choice").select_option("R2")
        page.get_by_role("button", name="Assign stand", exact=True).click()
        assert snap(page)["fleet"][0]["stand"] == "R1"
        assert "reserved" in page.locator(".toast").inner_text()
        action(page, "stand-review", "VH-PAX")
        page.locator("#stand-choice").select_option("R3")
        page.get_by_role("button", name="Assign stand", exact=True).click()
        assert snap(page)["fleet"][0]["stand"] == "R3"
        check("F06/F08: cancellation confirmation, dismissal and protected stand assignment")
        context.close()

        context, page = fresh("starter")
        action(page, "study")
        action(page, "low-funds")
        action(page, "plan", "VH-PAX")
        assert page.locator('[data-action="plan-review"]').is_disabled()
        assert "available funds" in page.locator("#plan-reason").inner_text()
        page.locator("#plan-route").select_option("SIN")
        assert "International" in page.locator("#plan-reason").inner_text()
        assert snap(page)["fleet"][0]["booking"] is None
        check("F04/F05: insufficient funds and locked-route refusals keep draft")
        context.close()

        context, page = fresh("starter")
        action(page, "nav", "career")
        action(page, "tab", "contracts")
        action(page, "contract-review", "KGC")
        action(page, "confirm")
        for i in range(3):
            if i == 0:
                action(page, "contract-plan", "KGC")
                action(page, "plan-review")
                action(page, "confirm")
            else:
                book(page)
            for _ in range(7):
                action(page, "advance", "VH-PAX")
        s = snap(page)
        assert s["contract"] is None and len(s["contractHistory"]) == 1 and len(s["history"]) == 3
        assert s["funds"] == 3687 and s["tier"] == 1
        action(page, "nav", "career")
        action(page, "tab", "contracts")
        action(page, "contract-review", "PLO")
        action(page, "confirm")
        action(page, "abandon-review")
        action(page, "confirm")
        assert snap(page)["contract"] is None and snap(page)["reliability"] == 98
        check("F11/F12/F21: complete contract, capability proof, reconciling rewards and abandonment")
        context.close()

        context, page = fresh("growing")
        action(page, "nav", "fleet")
        action(page, "market", "ADL")
        action(page, "buy-review", "DH8D")
        action(page, "confirm")
        s = snap(page)
        assert len(s["fleet"]) == 4 and s["funds"] == 360000
        new_id = s["selected"]
        action(page, "sell-review", new_id)
        assert "repeat plan" in page.get_by_role("dialog").inner_text()
        action(page, "confirm")
        assert len(snap(page)["fleet"]) == 3 and snap(page)["funds"] == 444000
        action(page, "nav", "fleet")
        action(page, "tab", "bases")
        action(page, "base-review")
        action(page, "base-confirm", "SYD")
        action(page, "confirm")
        assert len(snap(page)["bases"]) == 3 and snap(page)["funds"] == 419000
        action(page, "market", "SYD")
        action(page, "buy-review", "DH8D")
        action(page, "confirm")
        assert snap(page)["fleet"][-1]["base"] == "SYD"
        check("F13/F14/F15/F18: unified roster, purchase, sale and new-base purchase")
        context.close()

        context, page = fresh("starter")
        action(page, "profile", "VH-PAX")
        initial = snap(page)["funds"]
        action(page, "check-review", "VH-PAX")
        action(page, "confirm")
        assert snap(page)["funds"] == initial - 320
        action(page, "nav", "airport")
        assert snap(page)["fleet"][0]["job"] is not None
        for _ in range(6):
            action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["wear"] == 0 and snap(page)["fleet"][0]["job"] is not None
        action(page, "study")
        action(page, "issue", "return")
        action(page, "advance", "VH-PAX")
        action(page, "advance", "VH-PAX")
        assert "return stand" in page.locator(".toast").inner_text()
        assert snap(page)["fleet"][0]["job"] is not None
        action(page, "study")
        action(page, "clear-issue")
        action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["job"] is None and snap(page)["funds"] == initial - 320
        check("F16: check charges once, repair differs from return, stand wait and UI dismissal")
        context.close()

        context, page = fresh("growing")
        action(page, "nav", "fleet")
        action(page, "profile", "VH-O01")
        assert page.locator('[data-action="watch"][data-arg="VH-O01"]').is_disabled()
        before = snap(page)
        action(page, "ferry-review", "VH-O01")
        action(page, "confirm")
        action(page, "advance", "VH-O01")
        after = snap(page)
        moved = next(a for a in after["fleet"] if a["id"] == "VH-O01")
        assert moved["base"] == "ADL" and moved["flights"] == 11
        assert after["funds"] == before["funds"] - 900 and len(after["history"]) == 0
        action(page, "nav", "fleet")
        action(page, "profile", "VH-PAX")
        action(page, "refit-review", "VH-PAX")
        action(page, "confirm")
        action(page, "watch", "VH-PAX")
        assert page.locator('[data-action="camera"][data-arg="Cabin"]').is_disabled()
        check("F09/F17/F19: remote-camera limits, non-commercial ferry and freighter seat restriction")
        context.close()

        context, page = fresh("growing")
        action(page, "profile", "VH-PAX")
        action(page, "repeat-review", "VH-PAX")
        page.get_by_role("button", name="Enable repeat", exact=True).click()
        action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["booking"] is not None
        action(page, "profile", "VH-PAX")
        action(page, "repeat-pause", "VH-PAX")
        assert snap(page)["fleet"][0]["repeat"]["paused"] and snap(page)["fleet"][0]["booking"] is not None
        action(page, "menu")
        action(page, "quit")
        action(page, "continue")
        assert len(snap(page)["history"]) == 1 and snap(page)["fleet"][0]["booking"] is None
        action(page, "nav", "airport")
        action(page, "profile", "VH-PAX")
        action(page, "repeat-pause", "VH-PAX")
        action(page, "study")
        action(page, "low-funds")
        action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["repeat"]["paused"]
        assert "funds" in snap(page)["fleet"][0]["repeat"]["reason"]
        check("F02/F20/F24: repeat pause preserves flight, away commits settle once, refusal pauses automation")
        context.close()

        context, page = fresh("recovery")
        action(page, "inbox")
        assert page.get_by_role("complementary", name="Attention").is_visible()
        action(page, "profile", "VH-PAX")
        action(page, "blocker")
        page.keyboard.press("Escape")
        assert snap(page)["selected"] == "VH-PAX"
        action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["booking"]["stage"] == 0
        action(page, "study")
        action(page, "clear-issue")
        action(page, "advance", "VH-PAX")
        assert snap(page)["fleet"][0]["booking"]["stage"] == 1
        action(page, "nav", "airport")
        action(page, "traffic")
        action(page, "blocker")
        action(page, "traffic-watch")
        assert page.get_by_role("heading", name="Watch EM204", exact=True).is_visible()
        assert snap(page)["selected"] == "VH-PAX"
        action(page, "back")
        action(page, "study")
        action(page, "load-error")
        assert "preserved" in page.locator(".title-card").inner_text()
        action(page, "continue")
        assert "could not be loaded" in page.locator(".title-card").inner_text()
        action(page, "clear-error")
        action(page, "continue")
        assert len(snap(page)["history"]) == 1
        check("F02/F03/F07: attention, blocker inspection/back, automatic hold and recoverable load error")
        context.close()

        context, page = fresh("growing")
        action(page, "study")
        action(page, "save")
        action(page, "study")
        with page.expect_download() as download_info:
            action(page, "backup")
        backup = out / 'sample-study-backup.json'
        download_info.value.save_as(str(backup))
        action(page, "scenario", "starter")
        action(page, "confirm")
        assert snap(page)["tier"] == 0
        action(page, "study")
        with page.expect_file_chooser() as chooser_info:
            action(page, "restore")
        chooser_info.value.set_files(str(backup))
        page.get_by_role("heading", name="Soak Air", exact=True).wait_for()
        assert snap(page)["tier"] == 2 and snap(page)["funds"] == 480000
        invalid = out / 'invalid-study-backup.json'
        invalid.write_text('{"schema":1,"fleet":[{"job":{}}]}')
        action(page, "nav", "airport")
        action(page, "study")
        with page.expect_file_chooser() as chooser_info:
            action(page, "restore")
        chooser_info.value.set_files(str(invalid))
        page.locator(".toast.error").wait_for()
        assert snap(page)["funds"] == 480000
        assert page.evaluate("JSON.parse(localStorage.getItem('airside-player-flow-study-v1')).funds") == 480000
        check("F02/F24: portable study backup restores valid state and rejects corruption without overwrite")
        context.close()

        for width, height in [(1440, 900), (1280, 720), (900, 720), (800, 600), (390, 844)]:
            context, page = fresh("growing", width, height)
            for view in ["airport", "schedule", "network", "fleet", "career"]:
                action(page, "nav", view)
                assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth + 1"), (width, height, view)
                if width in [1440, 1280] and view in ["airport", "schedule", "fleet", "career"]:
                    page.screenshot(path=str(out / f"{view}-{width}.png"), full_page=True)
            action(page, "nav", "fleet")
            action(page, "profile", "VH-PAX")
            action(page, "menu")
            action(page, "settings")
            page.locator("#setting-large").check()
            page.locator("#setting-dark").check()
            assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth + 1"), (width, "large")
            page.keyboard.press("Tab")
            assert page.evaluate("!!document.activeElement.closest('[role=dialog]')")
            action(page, "settings-reset")
            assert not snap(page)["settings"]["dark"] and not snap(page)["settings"]["large"]
            page.keyboard.press("Escape")
            report["layouts"].append({"width": width, "height": height, "no_document_overflow": True, "settings_focus": True})
            context.close()
        check("F23: settings/defaults, modal keyboard focus and five responsive sizes")
        assert not report["page_errors"], report["page_errors"]
        assert not report["failed_resources"], report["failed_resources"]
        browser.close()
    (out / "verification.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Verified {len(report['checks'])} journey groups; report: {out / 'verification.json'}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://localhost:8765/art/player-flow-prototype-2026-10-07/index.html")
    parser.add_argument("--out", type=Path, default=ROOT / "work/player-flow-review")
    args = parser.parse_args()
    run(args.url, args.out)
