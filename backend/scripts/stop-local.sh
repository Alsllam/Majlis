#!/usr/bin/env bash
# Stops the hosts started by run-local.sh. A pid file may be stale after a reboot (the pid reused by
# an unrelated process), so a process is killed only if it is one of ours.
cd "$(dirname "$0")/.."
for f in .local/*.pid; do
  [ -f "$f" ] || continue
  pid=$(cat "$f")
  if ps -p "$pid" -o args= 2>/dev/null | grep -qE "Majlis\.|ai_service\.main"; then
    kill "$pid" 2>/dev/null
  fi
  rm -f "$f"
done
echo "Stopped."
