#!/usr/bin/env bash
set -e

echo "============================================================"
echo " CMS Practice Management - PostgreSQL Setup & Fix Script"
echo "============================================================"
echo ""

echo "Step 1: Setting password for PostgreSQL user 'postgres' to 'postgres'..."
sudo -u postgres psql -c "ALTER USER postgres WITH PASSWORD 'postgres';"

echo ""
echo "Step 2: Ensuring database 'cms' exists and is owned by 'postgres'..."
sudo -u postgres psql -c "CREATE DATABASE cms OWNER postgres;" 2>/dev/null || echo "Database 'cms' already exists (OK)."

echo ""
echo "Step 3: Granting full privileges to 'postgres' on database 'cms'..."
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE cms TO postgres;"

echo ""
echo "Step 4: Testing TCP connection to localhost:5432 with password 'postgres'..."
if PGPASSWORD='postgres' psql -h localhost -U postgres -d cms -c "SELECT 'SUCCESS: Connected to PostgreSQL cms database!' AS status;" ; then
    echo ""
    echo "============================================================"
    echo " [SUCCESS] PostgreSQL is now properly configured!"
    echo " Next step: Restart your backend by running:"
    echo "   cd /home/haran/NN/new/CMS_CA/backend/src/CMS.Api"
    echo "   dotnet run"
    echo "============================================================"
else
    echo ""
    echo "============================================================"
    echo " [ERROR] Connection test failed. Please check PostgreSQL logs."
    echo "============================================================"
    exit 1
fi

