#!/bin/bash

# Development startup script for BB solution
echo "🚀 Starting BB Development Environment"
echo "======================================"

# Function to check if a port is in use
check_port() {
    if lsof -i:$1 > /dev/null 2>&1; then
        echo "⚠️  Port $1 is already in use"
        return 1
    else
        echo "✅ Port $1 is available"
        return 0
    fi
}

# Check required ports
echo "📋 Checking ports..."
check_port 5005 || exit 1
check_port 5084 || exit 1

# Start API in background
echo ""
echo "🔧 Starting BB.Api on http://localhost:5005..."
cd BB.Api
dotnet run --no-launch-profile --urls="http://localhost:5005" > ../api.log 2>&1 &
API_PID=$!
cd ..

# Wait for API to start
echo "⏳ Waiting for API to start..."
sleep 5

# Test API endpoint
echo "🧪 Testing API endpoint..."
if curl -s http://localhost:5005/api/analytics/repositories > /dev/null; then
    echo "✅ API is responding"
else
    echo "❌ API is not responding"
    echo "📝 API logs:"
    tail -20 api.log
    kill $API_PID 2>/dev/null
    exit 1
fi

# Start Web app
echo ""
echo "🌐 Starting BB.Web on http://localhost:5084..."
cd BB.Web
dotnet run --no-launch-profile --urls="http://localhost:5084" > ../web.log 2>&1 &
WEB_PID=$!
cd ..

echo ""
echo "🎉 Both applications are starting!"
echo "📊 Dashboard: http://localhost:5084/dashboard"
echo "🧪 API Test: http://localhost:5084/api-test"
echo "📖 API Docs: http://localhost:5005/swagger"
echo ""
echo "📝 Logs:"
echo "   API logs: tail -f api.log"
echo "   Web logs: tail -f web.log"
echo ""
echo "🛑 To stop: kill $API_PID $WEB_PID"

# Wait for user to stop
echo "Press Ctrl+C to stop all services..."
trap "echo '🛑 Stopping services...'; kill $API_PID $WEB_PID 2>/dev/null; exit 0" INT

# Keep script running
while true; do
    sleep 1
done 