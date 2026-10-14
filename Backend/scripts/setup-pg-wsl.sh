#!/bin/bash
set -e

echo "[WSL-PG] Updating apt and installing PostgreSQL..."
apt-get update -y
apt-get install -y postgresql postgresql-contrib

echo "[WSL-PG] Starting PostgreSQL service..."
service postgresql start

echo "[WSL-PG] Configuring user and database..."
su - postgres -c "psql -c \"DO \$\$ BEGIN IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'prepvr') THEN CREATE USER prepvr WITH PASSWORD 'prepvr_password' SUPERUSER; END IF; END \$\$;\""
su - postgres -c "psql -c \"SELECT 'CREATE DATABASE prepvr_placement OWNER prepvr' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'prepvr_placement')\gexec\""

PG_CONF=$(ls /etc/postgresql/*/main/postgresql.conf | head -n 1)
PG_HBA=$(ls /etc/postgresql/*/main/pg_hba.conf | head -n 1)

if [ -f "$PG_CONF" ]; then
    sed -i "s/#listen_addresses = 'localhost'/listen_addresses = '*'/g" "$PG_CONF"
fi

if [ -f "$PG_HBA" ]; then
    if ! grep -q "0.0.0.0/0" "$PG_HBA"; then
        echo "host all all 0.0.0.0/0 md5" >> "$PG_HBA"
    fi
fi

service postgresql restart
echo "[WSL-PG] PostgreSQL is running and ready for PREP VR!"
