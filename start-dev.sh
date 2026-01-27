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


# Start .NET backend
echo -e "${GREEN}Starting .NET backend...${NC}"
cd LittlePublisher.Web
dotnet run &
DOTNET_PID=$!

# Wait for .NET to start
sleep 3

# Start Vue dev server
echo -e "${GREEN}Starting Vue dev server...${NC}"
cd ../client-app
npm run dev &
VUE_PID=$!

# Handle shutdown
cleanup() {
    echo -e "\n${YELLOW}Shutting down...${NC}"
    kill $DOTNET_PID 2>/dev/null
    kill $VUE_PID 2>/dev/null
    exit 0
}

trap cleanup SIGINT SIGTERM

echo -e "${GREEN}Development servers started!${NC}"
echo -e "  .NET API:    https://localhost:5001"
echo -e "  Vue Frontend: http://localhost:5173"
echo -e "\nPress Ctrl+C to stop"

# Wait for either to exit
wait
