#!/bin/sh
# Creates the database the integration tests use, alongside the dev one.
# Runs only on first initialisation of the data volume.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-SQL
	CREATE DATABASE ukweli_test OWNER $POSTGRES_USER;
SQL

echo "created database ukweli_test"
