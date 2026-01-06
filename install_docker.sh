#!/usr/bin/env bash

# Variables
user="steam"
BRANCH="master"

# Auto-update mods (recommended for CS2, which updates frequently)
# Set to 0 to disable.
AUTO_UPDATE_METAMOD="${AUTO_UPDATE_METAMOD:-1}"
AUTO_UPDATE_CSHARP="${AUTO_UPDATE_CSHARP:-1}"
# Minimal mode: keep ONLY Metamod + CounterStrikeSharp (remove all managed CSSharp plugins).
# Set to 0 for the full modpack.
MINIMAL_MODE="${MINIMAL_MODE:-0}"
# Installs/updates MatchZy plugin. Set to 1 to enable.
# NOTE: This repo is commonly used as a modpack, but some users want a minimal
# setup (Metamod + CounterStrikeSharp only). Default to disabled.
AUTO_UPDATE_MATCHZY="${AUTO_UPDATE_MATCHZY:-0}"
# Cleanup Windows-only files inside the Linux container to reduce clutter.
AUTO_CLEAN_LINUX_ONLY="${AUTO_CLEAN_LINUX_ONLY:-1}"

die() {
    echo "ERROR: $1" >&2
    exit 1
}

download() {
    local url="$1"
    local out="$2"

    [ -n "$url" ] || die "download(): missing url"
    [ -n "$out" ] || die "download(): missing output path"

    curl -fsSL --retry 3 --retry-delay 2 -o "$out" "$url" || die "Failed to download: $url"
}

update_metamod_linux() {
    echo "Updating Metamod:Source (Linux latest)..."

    local latest_name
    latest_name=$(curl -fsSL https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-linux | tr -d '\r\n') || die "Failed to resolve Metamod latest build name"
    [ -n "$latest_name" ] || die "Metamod latest build name is empty"

    local url="https://mms.alliedmods.net/mmsdrop/2.0/${latest_name}"
    local tmp="/tmp/${latest_name}"

    rm -rf \
        "/home/${user}/cs2/game/csgo/addons/metamod" \
        "/home/${user}/cs2/game/csgo/addons/metamod.vdf" \
        "/home/${user}/cs2/game/csgo/addons/metamod_x64.vdf"

    download "$url" "$tmp"
    tar -xzf "$tmp" -C "/home/${user}/cs2/game/csgo/" || die "Failed to extract Metamod tarball"
}

update_counterstrikesharp_linux() {
    echo "Updating CounterStrikeSharp (Linux with runtime, latest)..."

    local api_json
    api_json=$(curl -fsSL https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest) || die "Failed to query CounterStrikeSharp releases"

    # Prefer the 'with-runtime-linux' asset (no external dotnet dependency)
    local url
    url=$(echo "$api_json" | grep -oE 'https://github\.com/roflmuffin/CounterStrikeSharp/releases/download/[^\"]+/counterstrikesharp-with-runtime-linux-[0-9.]+\.zip' | head -n 1)
    [ -n "$url" ] || die "Failed to find CounterStrikeSharp linux-with-runtime asset URL"

    local tmp="/tmp/counterstrikesharp-with-runtime-linux.zip"
    local unpack_dir="/tmp/counterstrikesharp-unpack"
    local target_dir="/home/${user}/cs2/game/csgo/addons/counterstrikesharp"
    local src_dir

    rm -rf "$unpack_dir"
    mkdir -p "$unpack_dir" || die "Failed to create temp dir for CounterStrikeSharp"

    download "$url" "$tmp"
    unzip -oq "$tmp" -d "$unpack_dir" || die "Failed to extract CounterStrikeSharp zip"

    src_dir="$unpack_dir/addons/counterstrikesharp"
    [ -d "$src_dir" ] || die "CounterStrikeSharp zip did not contain expected addons/counterstrikesharp folder"

    # Update CounterStrikeSharp core while preserving bundled plugins/configs from this modpack.
    mkdir -p "$target_dir" || die "Failed to create CounterStrikeSharp target dir"

    rm -rf \
        "$target_dir/bin" \
        "$target_dir/api" \
        "$target_dir/dotnet" \
        "$target_dir/gamedata"

    cp -a "$src_dir/bin" "$target_dir/" || die "Failed to copy CounterStrikeSharp bin"
    cp -a "$src_dir/api" "$target_dir/" || die "Failed to copy CounterStrikeSharp api"
    cp -a "$src_dir/dotnet" "$target_dir/" || die "Failed to copy CounterStrikeSharp dotnet runtime"
    cp -a "$src_dir/gamedata" "$target_dir/" || die "Failed to copy CounterStrikeSharp gamedata"
}

install_matchzy_local() {
    # Backwards-compatible no-op (MatchZy is installed via GitHub releases now).
    return 0
}

update_matchzy_linux() {
    echo "Updating MatchZy (Linux with CSSharp, latest)..."

    local api_json
    if ! api_json=$(curl -fsSL https://api.github.com/repos/shobhit-pathak/MatchZy/releases/latest); then
        echo "WARN: Failed to query MatchZy releases, skipping."
        return 0
    fi

    # Prefer the linux bundle that includes the CSSharp plugin bits
    local url
    url=$(echo "$api_json" | grep -oE 'https://github\.com/shobhit-pathak/MatchZy/releases/download/[^"]+/MatchZy-[^"]*with-cssharp-linux\.zip' | head -n 1)
    if [ -z "$url" ]; then
        # Fallback: any asset that looks like a linux zip
        url=$(echo "$api_json" | grep -oE 'https://github\.com/shobhit-pathak/MatchZy/releases/download/[^"]+/MatchZy-[^"]*linux[^\"]*\.zip' | head -n 1)
    fi
    if [ -z "$url" ]; then
        echo "WARN: Failed to find MatchZy linux asset URL, skipping."
        return 0
    fi

    local tmp="/tmp/matchzy-linux.zip"
    local unpack_dir="/tmp/matchzy-unpack"

    rm -rf "$unpack_dir"
    mkdir -p "$unpack_dir" || {
        echo "WARN: Failed to create MatchZy temp dir, skipping."
        return 0
    }

    if ! curl -fsSL --retry 3 --retry-delay 2 -o "$tmp" "$url"; then
        echo "WARN: Failed to download MatchZy, skipping."
        return 0
    fi

    if ! unzip -oq "$tmp" -d "$unpack_dir"; then
        echo "WARN: Failed to extract MatchZy zip, skipping."
        return 0
    fi

    local src_plugin_dir="$unpack_dir/addons/counterstrikesharp/plugins/MatchZy"
    local src_cfg_dir="$unpack_dir/cfg/MatchZy"
    local dst_plugin_dir="/home/${user}/cs2/game/csgo/addons/counterstrikesharp/plugins/MatchZy"
    local dst_cfg_dir="/home/${user}/cs2/game/csgo/cfg/MatchZy"

    if [ ! -d "$src_plugin_dir" ]; then
        echo "WARN: MatchZy plugin folder not found in zip, skipping."
        return 0
    fi

    echo "Installing MatchZy plugin..."
    mkdir -p "/home/${user}/cs2/game/csgo/addons/counterstrikesharp/plugins" || {
        echo "WARN: Failed to create CSSharp plugins folder, skipping."
        return 0
    }

    rm -rf "$dst_plugin_dir"
    cp -a "$src_plugin_dir" "$dst_plugin_dir" || {
        echo "WARN: Failed to install MatchZy plugin, skipping."
        return 0
    }

    if [ -d "$src_cfg_dir" ]; then
        mkdir -p "/home/${user}/cs2/game/csgo/cfg" || true
        rm -rf "$dst_cfg_dir"
        cp -a "$src_cfg_dir" "$dst_cfg_dir" || true
    fi
}

remove_matchzy_and_css_plugins() {
    # Docker uses a persistent volume mounted at /home/steam.
    # If MatchZy or other CSSharp plugins were installed in a previous run,
    # remove them here so the server runs with ONLY:
    # - Metamod
    # - CounterStrikeSharp
    local csgo_dir="/home/${user}/cs2/game/csgo"
    local css_plugins_dir="${csgo_dir}/addons/counterstrikesharp/plugins"

    echo "Removing MatchZy + all CounterStrikeSharp plugins (minimal mode)..."

    # Remove any MatchZy config folder if present
    rm -rf "${csgo_dir}/cfg/MatchZy" || true

    # Remove all managed plugins (including any disabled ones) and recreate an empty dir
    rm -rf "${css_plugins_dir}" || true
    mkdir -p "${css_plugins_dir}" || die "Failed to create CounterStrikeSharp plugins folder"
}

patch_cs2_cfg_noise() {
    # CS2 still ships some old CS:GO-era cvars in Valve gamemode cfgs.
    # In CS2 they log as "Unknown command" even though they are harmless.
    # Remove them to keep the console clean.
    local cfg_dir="/home/${user}/cs2/game/csgo/cfg"

    # Create empty files so CS2's exec chain doesn't warn about missing cfgs.
    mkdir -p "$cfg_dir" || true
    touch "${cfg_dir}/gamemode_casual_server.cfg" "${cfg_dir}/gamemode_casual_last.cfg" || true

    # Remove lines that trigger "Unknown command" spam on CS2.
    # Apply broadly so switching game modes doesn't reintroduce the noise.
    for cfg in "${cfg_dir}"/gamemode_*.cfg "${cfg_dir}"/1v1_settings.cfg; do
        [ -f "$cfg" ] || continue
        sed -i '/mp_weapons_glow_on_ground/d;/sv_gameinstructor_enable/d' "$cfg" || true
    done
}

cleanup_linux_only() {
    # Remove Windows-only bundle shipped for Windows installs.
    rm -rf "/home/${user}/cs2/game/csgo/addons/windows" || true

    # Metamod ships binaries for many games; keep only the CS2 loader pieces.
    if [ -d "/home/${user}/cs2/game/csgo/addons/metamod/bin" ]; then
        rm -rf "/home/${user}/cs2/game/csgo/addons/metamod/bin/win64" || true
        find "/home/${user}/cs2/game/csgo/addons/metamod/bin" -maxdepth 1 -type f -name '*.dll' -delete || true
        # Keep only CS2 on linuxsteamrt64 if present
        if [ -d "/home/${user}/cs2/game/csgo/addons/metamod/bin/linuxsteamrt64" ]; then
            find "/home/${user}/cs2/game/csgo/addons/metamod/bin/linuxsteamrt64" -maxdepth 1 -type f -name 'metamod.2.*.so' ! -name 'metamod.2.cs2.so' -delete || true
        fi
        # Keep only CS2 on linux64 if present
        if [ -d "/home/${user}/cs2/game/csgo/addons/metamod/bin/linux64" ]; then
            find "/home/${user}/cs2/game/csgo/addons/metamod/bin/linux64" -maxdepth 1 -type f -name 'metamod.2.*.so' ! -name 'metamod.2.cs2.so' -delete || true
        fi
    fi

    # CounterStrikeSharp: remove unused windows binaries if present
    if [ -d "/home/${user}/cs2/game/csgo/addons/counterstrikesharp/bin" ]; then
        rm -rf "/home/${user}/cs2/game/csgo/addons/counterstrikesharp/bin/win64" || true
    fi
}

sanity_check_mods() {
    [ -f "/home/${user}/cs2/game/csgo/addons/metamod.vdf" ] || die "Metamod VDF not found at /home/${user}/cs2/game/csgo/addons/metamod.vdf"
    [ -f "/home/${user}/cs2/game/csgo/addons/counterstrikesharp/bin/linuxsteamrt64/counterstrikesharp.so" ] || die "CounterStrikeSharp linux binary not found (counterstrikesharp.so)"
    # Metamod binary name differs across builds; check for CS2-specific .so in typical folders
    if [ ! -f "/home/${user}/cs2/game/csgo/addons/metamod/bin/linuxsteamrt64/metamod.2.cs2.so" ] && [ ! -f "/home/${user}/cs2/game/csgo/addons/metamod/bin/linux64/metamod.2.cs2.so" ]; then
        die "Metamod CS2 binary not found (metamod.2.cs2.so)"
    fi
}

# Check if MOD_BRANCH is set and not empty
if [ -n "$MOD_BRANCH" ]; then
    BRANCH="$MOD_BRANCH"
fi

CUSTOM_FILES="${CUSTOM_FOLDER:-custom_files}"

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
    # Lightweight sanity check; avoids passing arbitrary strings to -ip.
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

# Defaults (docker-compose/.env might not provide these; empty values break CS2 args)
export PORT="${PORT:-27015}"
export TICKRATE="${TICKRATE:-128}"
export MAXPLAYERS="${MAXPLAYERS:-32}"
export LAN="${LAN:-0}"
export EXEC="${EXEC:-on_boot.cfg}"
export MAP="${MAP:-de_dust2}"
export GAME_TYPE="${GAME_TYPE:-0}"
export GAME_MODE="${GAME_MODE:-0}"
export MAP_GROUP="${MAP_GROUP:-mg_active}"

if ! echo "$PORT" | grep -Eq '^[0-9]+$'; then
    die "PORT must be numeric, got: '$PORT'"
fi

if [ -f /etc/os-release ]; then
    # freedesktop.org and systemd
    . /etc/os-release
    DISTRO_OS=$NAME
    DISTRO_VERSION=$VERSION_ID
elif type lsb_release >/dev/null 2>&1; then
    # linuxbase.org
    DISTRO_OS=$(lsb_release -si)
    DISTRO_VERSION=$(lsb_release -sr)
elif [ -f /etc/lsb-release ]; then
    # For some versions of Debian/Ubuntu without lsb_release command
    . /etc/lsb-release
    DISTRO_OS=$DISTRIB_ID
    DISTRO_VERSION=$DISTRIB_RELEASE
elif [ -f /etc/debian_version ]; then
    # Older Debian/Ubuntu/etc.
    DISTRO_OS=Debian
    DISTRO_VERSION=$(cat /etc/debian_version)
else
    # Fall back to uname, e.g. "Linux <version>", also works for BSD, etc.
    DISTRO_OS=$(uname -s)
    DISTRO_VERSION=$(uname -r)
fi

echo "Starting on $DISTRO_OS: $DISTRO_VERSION..."

# Get the free space on the root filesystem in GB
FREE_SPACE=$(df / --output=avail -BG | tail -n 1 | tr -d 'G')

echo "With $FREE_SPACE Gb free space..."

# Check root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run this script as root..."
    exit 1
fi

PUBLIC_IP=$(dig +short myip.opendns.com @resolver1.opendns.com)

if [ -z "$PUBLIC_IP" ]; then
    echo "ERROR: Cannot retrieve your public IP address..."
    exit 1
fi

# In Docker/NAT setups, explicitly advertising the public address helps Steam master server listing.
export NET_PUBLIC_ADR="${NET_PUBLIC_ADR:-$PUBLIC_IP}"
NET_PUBLIC_ADR_ARGS=""
if [[ "${LAN:-0}" == "0" ]] && (is_valid_ipv4 "$NET_PUBLIC_ADR" || is_valid_ipv6 "$NET_PUBLIC_ADR"); then
    NET_PUBLIC_ADR_ARGS="+net_public_adr $NET_PUBLIC_ADR"
fi

# Prevent the server from hibernating when empty (hibernation can make it disappear from Steam server browser).
HIBERNATE_ARGS=""
if [[ "${LAN:-0}" == "0" ]]; then
    HIBERNATE_ARGS="+sv_hibernate_when_empty 0"
fi

# Update DuckDNS with our current IP
if [ ! -z "$DUCK_TOKEN" ]; then
    echo url="http://www.duckdns.org/update?domains=$DUCK_DOMAIN&token=$DUCK_TOKEN&ip=$PUBLIC_IP" | curl -k -o /duck.log -K -
fi

echo "Checking $user user exists..."
getent passwd ${user} 2 >/dev/null &>1
if [ "$?" -ne "0" ]; then
    echo "Adding $user user..."
    addgroup ${user} &&
        adduser --system --home /home/${user} --shell /bin/false --ingroup ${user} ${user} &&
        usermod -a -G tty ${user} &&
        mkdir -m 777 /home/${user}/cs2 &&
        chown -R ${user}:${user} /home/${user}/cs2
    if [ "$?" -ne "0" ]; then
        echo "ERROR: Cannot add user $user..."
        exit 1
    fi
fi

chmod 777 /home/${user}/cs2
chown -R ${user}:${user} /home/${user}

echo "Checking steamcmd exists..."
if [ ! -d "/steamcmd" ]; then
    mkdir /steamcmd && cd /steamcmd || exit
    wget https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz
    tar -xvzf steamcmd_linux.tar.gz
fi

chown -R ${user}:${user} /steamcmd
chown -R ${user}:${user} /home/${user}

# Removido download do Steam Runtime - CS2 já inclui as bibliotecas necessárias

# https://developer.valvesoftware.com/wiki/Command_line_options
sudo -u $user /steamcmd/steamcmd.sh \
    +api_logging 1 1 \
    +@sSteamCmdForcePlatformType linux \
    +@sSteamCmdForcePlatformBitness "$BITS" \
    +force_install_dir /home/${user}/cs2 \
    +login anonymous \
    +app_update 730 \
    +quit

cd /home/${user} || exit

# Set up steam client libraries
# 32-bit
mkdir -p /home/${user}/.steam/sdk32/
rm /home/${user}/.steam/sdk32/steamclient.so
cp -v /steamcmd/linux32/steamclient.so /home/${user}/.steam/sdk32/steamclient.so || {
	echo "ERROR: Failed to copy 32-bit libraries"
}
# 64-bit
mkdir -p /home/${user}/.steam/sdk64/
rm /home/${user}/.steam/sdk64/steamclient.so
cp -v /steamcmd/linux64/steamclient.so /home/${user}/.steam/sdk64/steamclient.so || {
	echo "ERROR: Failed to copy 64-bit libraries"
}

echo "Installing mods"
cp -R /home/cs2-modded-server/game/csgo/ /home/${user}/cs2/game/

# Keep Metamod/CounterStrikeSharp current (CS2 updates can break older builds)
if [ "$AUTO_UPDATE_METAMOD" = "1" ]; then
    update_metamod_linux
fi

if [ "$AUTO_UPDATE_CSHARP" = "1" ]; then
    update_counterstrikesharp_linux
fi

# Enable/install MatchZy
if [ "$AUTO_UPDATE_MATCHZY" = "1" ]; then
    update_matchzy_linux
fi

if [ "$AUTO_CLEAN_LINUX_ONLY" = "1" ]; then
    cleanup_linux_only
fi

sanity_check_mods

echo "Merging in custom files"
cp -RT /home/custom_files/ /home/${user}/cs2/game/csgo/

# Keep only Metamod + CounterStrikeSharp (no CSSharp managed plugins)
if [ "$MINIMAL_MODE" = "1" ]; then
    remove_matchzy_and_css_plugins
fi

# Keep CS2 console clean (remove Valve cfg noise)
patch_cs2_cfg_noise

# Re-merge custom files at the end to ensure overrides win.
echo "Final merge of custom files"
cp -RT /home/custom_files/ /home/${user}/cs2/game/csgo/ || true

# If MatchZy auto-update is disabled, ensure the plugin is not left enabled from a previous run.
if [ "$AUTO_UPDATE_MATCHZY" != "1" ]; then
    rm -rf "/home/${user}/cs2/game/csgo/addons/counterstrikesharp/plugins/MatchZy" || true
fi

chown -R ${user}:${user} /home/${user}

cd /home/${user}/cs2 || exit

# Define the file name
FILE="game/csgo/gameinfo.gi"

# Define the pattern to search for and the line to add
PATTERN="Game_LowViolence[[:space:]]*csgo_lv // Perfect World content override"
LINE_TO_ADD="\t\t\tGame\tcsgo/addons/metamod"

# Use a regular expression to ignore spaces when checking if the line exists
REGEX_TO_CHECK="^[[:space:]]*Game[[:space:]]*csgo/addons/metamod"

# Check if the line already exists in the file, ignoring spaces
if grep -qE "$REGEX_TO_CHECK" "$FILE"; then
    echo "$FILE already patched for Metamod."
else
    # If the line isn't there, use awk to add it after the pattern
    awk -v pattern="$PATTERN" -v lineToAdd="$LINE_TO_ADD" '{
        print $0;
        if ($0 ~ pattern) {
            print lineToAdd;
        }
    }' "$FILE" >tmp_file && mv tmp_file "$FILE"
    echo "$FILE successfully patched for Metamod."
fi

echo "Starting server on $PUBLIC_IP:$PORT"
# Executar CS2 com LD_LIBRARY_PATH correto
cd /home/${user}/cs2 || exit

# Steam networking ports are derived from PORT by default.
# Keeping them explicit helps with NAT/firewall rules and Steam server browser discovery.
export STEAM_PORT="${STEAM_PORT:-$((PORT + 1))}"
export CLIENT_PORT="${CLIENT_PORT:-$((PORT + 2))}"

sudo -u $user LD_LIBRARY_PATH="./bin/linuxsteamrt64:$LD_LIBRARY_PATH" ./game/bin/linuxsteamrt64/cs2 \
    -dedicated \
    -console \
    -usercon \
    -tickrate "$TICKRATE" \
    "$IP_ARGS" \
    -port "$PORT" \
    -steamport "$STEAM_PORT" \
    -clientport "$CLIENT_PORT" \
    +map "${MAP:-de_dust2}" \
    +sv_visiblemaxplayers "$MAXPLAYERS" \
    -authkey "$API_KEY" \
    +sv_setsteamaccount "$STEAM_ACCOUNT" \
    +game_type "${GAME_TYPE:-0}" \
    +game_mode "${GAME_MODE:-0}" \
    +mapgroup "${MAP_GROUP:-mg_active}" \
    $HIBERNATE_ARGS \
    $NET_PUBLIC_ADR_ARGS \
    +sv_lan "$LAN" \
    +sv_password "$SERVER_PASSWORD" \
    +rcon_password "$RCON_PASSWORD" \
    +exec "$EXEC"
