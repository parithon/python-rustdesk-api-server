#!/bin/bash
# RustDesk API Server (.NET 9) Startup Script

echo "Starting RustDesk API Server (.NET 9)..."
echo "Default admin credentials: admin/admin123"
echo "Please change the default password after first login!"
echo ""
echo "Server will be available at: http://localhost:21114"
echo "Press Ctrl+C to stop the server"
echo ""

dotnet run --urls "http://0.0.0.0:21114"
