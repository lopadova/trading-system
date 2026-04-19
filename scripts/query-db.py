#!/usr/bin/env python3
import sqlite3
import sys
from datetime import datetime

db_path = "src/TradingSupervisorService/data/supervisor.db"
conn = sqlite3.connect(db_path)
cursor = conn.cursor()

print("\n=== LATEST HEARTBEATS (service_heartbeats) ===")
cursor.execute("""
    SELECT service_name, hostname, last_seen_at,
           cpu_percent, ram_percent, disk_free_gb,
           trading_mode, version, created_at
    FROM service_heartbeats
    ORDER BY updated_at DESC
    LIMIT 5
""")
rows = cursor.fetchall()
if not rows:
    print("  (No heartbeats found)")
else:
    for row in rows:
        print(f"Service: {row[0]} | Host: {row[1]}")
        print(f"  LastSeen: {row[2]}")
        print(f"  CPU: {row[3]:.1f}% | RAM: {row[4]:.1f}% | Disk: {row[5]:.1f}GB")
        print(f"  Mode: {row[6]} | Version: {row[7]}")
        print(f"  Created: {row[8]}\n")

print("\n=== OUTBOX EVENTS (sync_outbox) - heartbeat ===")
cursor.execute("""
    SELECT event_id, event_type, status, created_at, sent_at, retry_count
    FROM sync_outbox
    WHERE event_type = 'heartbeat'
    ORDER BY created_at DESC
    LIMIT 5
""")
rows = cursor.fetchall()
if not rows:
    print("  (No heartbeat outbox events found)")
else:
    for row in rows:
        print(f"ID: {row[0][:8]}... | Type: {row[1]} | Status: {row[2]}")
        print(f"  Created: {row[3]} | Sent: {row[4]} | Retries: {row[5]}\n")

print("\n=== OUTBOX SUMMARY ===")
cursor.execute("""
    SELECT event_type, status, COUNT(*) as count
    FROM sync_outbox
    GROUP BY event_type, status
    ORDER BY event_type, status
""")
rows = cursor.fetchall()
if not rows:
    print("  (No outbox events)")
else:
    for row in rows:
        print(f"  {row[0]} | {row[1]}: {row[2]} events")

conn.close()
