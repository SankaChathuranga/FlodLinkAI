#!/usr/bin/env bash
# run : chmod +x start_floodlink.sh
#       ./start_floodlink.sh

set -e

cd "$(dirname "$0")"

echo "Starting PostgreSQL..."
if docker compose version >/dev/null 2>&1; then
  docker compose up -d
else
  docker-compose up -d
fi

echo "Starting backend..."
(
  cd backend/src/FloodLink.Api
  dotnet run
) &

echo "Starting web app..."
(
  cd web
  [ -d node_modules ] || npm install
  npm run dev -- --host 0.0.0.0
) &

echo "FloodLink is starting..."
echo "API: http://localhost:5000"
echo "Web: http://localhost:5173"
echo "DB: localhost:5432"

wait 