#!/bin/bash
set -e
PG_HBA="/etc/postgresql/18/main/pg_hba.conf"
PG_CONF="/etc/postgresql/18/main/postgresql.conf"

echo "local all all trust" > /tmp/hba_head
echo "host all all 127.0.0.1/32 md5" >> /tmp/hba_head
echo "host all all ::1/128 md5" >> /tmp/hba_head
echo "host all all 0.0.0.0/0 md5" >> /tmp/hba_head
cat "$PG_HBA" >> /tmp/hba_head
cp /tmp/hba_head "$PG_HBA"

sed -i "s/#listen_addresses = 'localhost'/listen_addresses = '*'/g" "$PG_CONF"
sed -i "s/listen_addresses = 'localhost'/listen_addresses = '*'/g" "$PG_CONF"

service postgresql restart
echo "PostgreSQL reconfigured and restarted successfully!"
