#!/usr/bin/env bash

# As root (sudo su)
# cd / && curl -s -H "Cache-Control: no-cache" -o "run.sh" "https://raw.githubusercontent.com/kus/cs2-modded-server/master/run.sh" && chmod +x run.sh && bash run.sh


user="steam"
PUBLIC_IP=$(dig +short myip.opendns.com @resolver1.opendns.com)

# In Docker/NAT setups, explicitly advertising the public address helps Steam master server listing.
export NET_PUBLIC_ADR="${NET_PUBLIC_ADR:-$PUBLIC_IP}"
NET_PUBLIC_ADR_ARGS=""
if [[ "${LAN:-0}" == "0" ]] && (is_valid_ipv4 "$NET_PUBLIC_ADR" || is_valid_ipv6 "$NET_PUBLIC_ADR"); then
    NET_PUBLIC_ADR_ARGS="+net_public_adr $NET_PUBLIC_ADR"
fi

# 32 or 64 bit Operating System
# If BITS environment variable is not set, try determine it
if [ -z "$BITS" ]; then
    # Determine the operating system architecture
    architecture=$(uname -m)

    # Set OS_BITS based on the architecture
    if [[ $architecture == *"64"* ]]; then
        export BITS=64
    elif [[ $architecture == *"i386"* ]] || [[ $architecture == *"i686"* ]]; then
        export BITS=32
    else
        echo "Unknown architecture: $architecture"
        exit 1
    fi
fi

is_valid_ipv4() {
    local ip="$1" IFS='.'
    local -a parts
    [[ "$ip" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]] || return 1
    read -r -a parts <<< "$ip"
    (( ${#parts[@]} == 4 )) || return 1
    for part in "${parts[@]}"; do
        [[ "$part" =~ ^[0-9]+$ ]] || return 1
        (( part >= 0 && part <= 255 )) || return 1
    done
    return 0
}

is_valid_ipv6() {
    local ip="$1"
    [[ "$ip" == *:* ]] || return 1
    [[ "$ip" =~ ^[0-9A-Fa-f:]+$ ]] || return 1
    return 0
}

BIND_IP_VALUE="${BIND_IP:-${IP:-}}"
if [[ -z "$BIND_IP_VALUE" ]]; then
    IP_ARGS=""
elif is_valid_ipv4 "$BIND_IP_VALUE" || is_valid_ipv6 "$BIND_IP_VALUE"; then
    IP_ARGS="-ip ${BIND_IP_VALUE}"
else
    echo "WARNING: Ignoring invalid IP bind value: '$BIND_IP_VALUE' (set BIND_IP to a valid IP or leave it empty)"
    IP_ARGS=""
fi

echo "Downloading any updates for Steam Linux Runtime 3.0 (sniper)..."
# https://discord.com/channels/1160907911501991946/1160907912445710479/1411330429679829013
# https://steamdb.info/app/1628350/depots/
sudo -u $user /steamcmd/steamcmd.sh \
  +api_logging 1 1 \
  +@sSteamCmdForcePlatformType linux \
  +@sSteamCmdForcePlatformBitness $BITS \
  +force_install_dir /home/${user}/steamrt \
  +login anonymous \
  +app_update 1628350 \
  +validate \
  +quit
chown -R ${user}:${user} /home/${user}/steamrt

echo "Downloading any updates for CS2..."
# https://developer.valvesoftware.com/wiki/Command_line_options
sudo -u $user /steamcmd/steamcmd.sh \
  +api_logging 1 1 \
  +@sSteamCmdForcePlatformType linux \
  +@sSteamCmdForcePlatformBitness $BITS \
  +force_install_dir /home/${user}/cs2 \
  +login anonymous \
  +app_update 730 \
  +quit

cd /home/${user}/cs2

# Steam networking ports are derived from PORT by default.
# Keeping them explicit helps with NAT/firewall rules and Steam server browser discovery.
export STEAM_PORT="${STEAM_PORT:-$((PORT + 1))}"
export CLIENT_PORT="${CLIENT_PORT:-$((PORT + 2))}"

echo "Starting server on $PUBLIC_IP:$PORT"
echo /home/${user}/steamrt/run ./game/bin/linuxsteamrt64/cs2 --graphics-provider "" -- \
    -dedicated \
    -console \
    -usercon \
    -autoupdate \
    -tickrate $TICKRATE \
	$IP_ARGS \
    -port $PORT \
    -steamport $STEAM_PORT \
    -clientport $CLIENT_PORT \
    +map de_dust2 \
    +sv_visiblemaxplayers $MAXPLAYERS \
    -authkey $API_KEY \
    +sv_setsteamaccount $STEAM_ACCOUNT \
    +game_type 0 \
    +game_mode 0 \
    +mapgroup mg_active \
    $NET_PUBLIC_ADR_ARGS \
    +sv_lan $LAN \
	+sv_password $SERVER_PASSWORD \
	+rcon_password $RCON_PASSWORD \
	+exec $EXEC
sudo -u $user /home/${user}/steamrt/run ./game/bin/linuxsteamrt64/cs2 --graphics-provider "" -- \
    -dedicated \
    -console \
    -usercon \
    -autoupdate \
    -tickrate $TICKRATE \
	$IP_ARGS \
    -port $PORT \
    -steamport $STEAM_PORT \
    -clientport $CLIENT_PORT \
    +map de_dust2 \
    +sv_visiblemaxplayers $MAXPLAYERS \
    -authkey $API_KEY \
    +sv_setsteamaccount $STEAM_ACCOUNT \
    +game_type 0 \
    +game_mode 0 \
    +mapgroup mg_active \
    $NET_PUBLIC_ADR_ARGS \
    +sv_lan $LAN \
	+sv_password $SERVER_PASSWORD \
	+rcon_password $RCON_PASSWORD \
	+exec $EXEC
