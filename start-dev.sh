#!/bin/bash

# Start development servers for LittlePublisher
# This script starts both the .NET backend and Vue frontend

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting LittlePublisher Development Environment${NC}"

API_PORT=5001
VUE_PORT=5173
DOTNET_PID=
VUE_PID=

port_in_use() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

require_free_port() {
    if port_in_use "$1"; then
        echo -e "${RED}Port $1 is already in use.${NC}"
        echo "Stop the existing process or choose another port before running this script."
        lsof -nP -iTCP:"$1" -sTCP:LISTEN
        exit 1
    fi
}

wait_for_port() {
    port=$1
    pid=$2
    name=$3
    attempts=60

    while [ "$attempts" -gt 0 ]; do
        if port_in_use "$port"; then
            return 0
        fi

        if ! kill -0 "$pid" 2>/dev/null; then
            echo -e "${RED}$name exited before port $port was ready.${NC}"
            return 1
        fi

        attempts=$((attempts - 1))
        sleep 1
    done

    echo -e "${RED}$name did not start listening on port $port in time.${NC}"
    return 1
}

# Handle shutdown
cleanup() {
    code=${1:-0}
    echo -e "\n${YELLOW}Shutting down...${NC}"
    if [ -n "$DOTNET_PID" ]; then
        kill "$DOTNET_PID" 2>/dev/null
    fi
    if [ -n "$VUE_PID" ]; then
        kill "$VUE_PID" 2>/dev/null
    fi
    exit "$code"
}

trap 'cleanup 0' SIGINT SIGTERM

require_free_port "$API_PORT"
require_free_port "$VUE_PORT"

# Start .NET backend
echo -e "${GREEN}Starting .NET backend...${NC}"
cd LittlePublisher.Web
dotnet run </dev/null &
DOTNET_PID=$!

if ! wait_for_port "$API_PORT" "$DOTNET_PID" ".NET backend"; then
    cleanup 1
fi

# Start Vue dev server
echo -e "${GREEN}Starting Vue dev server...${NC}"
cd ../client-app
npm run dev -- --strictPort </dev/null &
VUE_PID=$!

if ! wait_for_port "$VUE_PORT" "$VUE_PID" "Vue dev server"; then
    cleanup 1
fi

echo -e "${GREEN}Development servers started!${NC}"
echo -e "  .NET API:    https://localhost:${API_PORT}"
echo -e "  Vue Frontend: http://localhost:${VUE_PORT}"
echo -e "\nPress Ctrl+C to stop"

# Keep both servers tied together so a backend crash does not leave Vite
# running with a broken proxy.
while true; do
    if ! kill -0 "$DOTNET_PID" 2>/dev/null; then
        echo -e "${RED}.NET backend exited.${NC}"
        cleanup 1
    fi

    if ! kill -0 "$VUE_PID" 2>/dev/null; then
        echo -e "${RED}Vue dev server exited.${NC}"
        cleanup 1
    fi

    sleep 1
done
