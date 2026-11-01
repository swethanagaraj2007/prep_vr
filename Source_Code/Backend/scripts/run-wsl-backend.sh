#!/bin/bash
set -e

cd /mnt/d/PREP_VR/Backend

# 1. Ensure PostgreSQL service is active
echo "[PREP-VR] Checking PostgreSQL service..."
service postgresql status || service postgresql start

# 2. Push Prisma Schema
echo "[PREP-VR] Syncing relational PostgreSQL database with Prisma..."
npx prisma generate
npx prisma db push --accept-data-loss

# 3. Seed Demo Data
echo "[PREP-VR] Seeding demo student placement dataset..."
npx ts-node prisma/seed.ts

# 4. Start API Server
echo "[PREP-VR] Starting Centralized Placement Data API Server on port 5000..."
npx ts-node src/index.ts
