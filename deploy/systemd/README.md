# CloudSync systemd user unit

The daemon runs as a systemd user service so it keeps running when the UI closes.

## Install

1. Publish the daemon to the directory the unit expects:
   `dotnet publish apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon -c Release -o ~/.local/share/astardev-cloudsync`
2. Install the unit:
   `install -D deploy/systemd/astardev-cloudsync.service ~/.config/systemd/user/astardev-cloudsync.service`
3. Reload and start:
   `systemctl --user daemon-reload && systemctl --user enable --now astardev-cloudsync`

## Check

- Status: `systemctl --user status astardev-cloudsync`
- Logs: `journalctl --user -u astardev-cloudsync -f`
- Verify the unit file: `systemd-analyze --user verify deploy/systemd/astardev-cloudsync.service`

The unit runs `dotnet` from the user service PATH; if it is not found, set `Environment=PATH=...` or `DOTNET_ROOT` in a drop-in (`systemctl --user edit astardev-cloudsync`).

The socket is `$XDG_RUNTIME_DIR/astardev-cloudsync/daemon.sock`, the same path the AppHost passes as `Ipc__SocketPath`.

## Stop and restart from the UI

Settings > Advanced > Daemon sends `StopDaemon`. Stop ends the daemon with exit code 0, so systemd leaves it stopped (`systemctl --user start astardev-cloudsync` starts it again). Restart ends it with exit code 75, which `RestartForceExitStatus=75` turns into a restart after `RestartSec` seconds. Run any other way, Restart only stops the daemon.
