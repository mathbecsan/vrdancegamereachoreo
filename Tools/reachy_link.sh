#!/usr/bin/env bash
# Connect the VR game to your real Reachy Minis.
#
#   Tools/reachy_link.sh          start each plugged-in Reachy's daemon (if not running) and link the headset
#   Tools/reachy_link.sh --sim    no robots: start a simulated Reachy on port 8000 to test the link
#   Tools/reachy_link.sh --stop   stop the daemons this script started
#
# The Reachys are the USB "Lite" version: their daemons run on this Mac (A on port 8000, B on 8001, the same as
# the reachy-dance app's config/show.yaml). With the Quest plugged in by USB, `adb reverse` makes the headset's
# "localhost:8000/8001" reach them, so it works on any Wi-Fi (even campus networks that block devices).
# Don't run the reachy-dance robot agents at the same time: both would steer the same robots.
set -euo pipefail

APP="${REACHY_DANCE:-$HOME/Downloads/reachy-dance}"
DAEMON="$APP/.venv/bin/reachy-mini-daemon"
ADB="${ADB:-/Applications/Unity/Hub/Editor/6000.3.28f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb}"
LOGS="$APP/logs"; mkdir -p "$LOGS"
# Same serial ports as config/show.yaml (robot A, robot B)
SERIAL_A="${SERIAL_A:-/dev/cu.usbmodem5B7B0101971}"
SERIAL_B="${SERIAL_B:-/dev/cu.usbmodem5B7B0111871}"

listening() { lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1; }

start() {  # name port serial-or-sim
  local name=$1 port=$2 src=$3
  if listening "$port"; then echo "  $name: already running on port $port"; return; fi
  local args=(--fastapi-port "$port" --fastapi-host 0.0.0.0 --no-media --no-preload-datasets --headless)
  if [[ $src == sim ]]; then args+=(--mockup-sim); elif [[ -e $src ]]; then args+=(-p "$src"); else echo "  $name: not plugged in ($src not found), skipped"; return; fi
  nohup "$DAEMON" "${args[@]}" >"$LOGS/vr_daemon_$port.log" 2>&1 &
  echo $! >"$LOGS/vr_daemon_$port.pid"
  for _ in $(seq 1 40); do listening "$port" && break; sleep 0.5; done
  if listening "$port"; then echo "  $name: started on port $port"; else echo "  $name: did not start, see $LOGS/vr_daemon_$port.log"; fi
}

if [[ ${1:-} == --stop ]]; then
  for p in 8000 8001; do f="$LOGS/vr_daemon_$p.pid"; [[ -f $f ]] && kill "$(cat "$f")" 2>/dev/null && echo "stopped daemon on $p"; rm -f "$f"; done
  exit 0
fi

echo "Reachy daemons:"
if [[ ${1:-} == --sim ]]; then start "Simulated Reachy A" 8000 sim
else start "Reachy A" 8000 "$SERIAL_A"; start "Reachy B" 8001 "$SERIAL_B"; fi

echo "Headset:"
if "$ADB" get-state >/dev/null 2>&1; then
  "$ADB" reverse tcp:8000 tcp:8000 >/dev/null && "$ADB" reverse tcp:8001 tcp:8001 >/dev/null
  echo "  linked: the game reaches the robots at localhost:8000 and :8001"
else
  echo "  not connected by USB. Plug it in (and allow USB debugging in the headset), then run this again."
  echo "  Or use Wi-Fi: put this Mac's address in the game's reachy_link.json (this Mac: $(ipconfig getifaddr en0 2>/dev/null || echo unknown))."
fi
echo "In the game: Settings > Real Reachy Minis > Send moves: On."
