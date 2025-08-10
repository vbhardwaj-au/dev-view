#!/bin/bash

# Configuration setup script for Bitbucket Analytics Dashboard
echo "🔧 Setting up Bitbucket Analytics Dashboard Configuration"
echo "======================================================="

# Function to check if file exists
check_file() {
    if [[ -f "$1" ]]; then
        echo "✅ $1 already exists"
        return 0
    else
        echo "❌ $1 does not exist"
        return 1
    fi
}

# Function to copy template if original doesn't exist
setup_config() {
    local template_file="$1"
    local target_file="$2"
    local description="$3"
    
    if [[ ! -f "$target_file" ]]; then
        echo "📋 Creating $description..."
        cp "$template_file" "$target_file"
        echo "✅ Created $target_file from template"
        return 1  # Indicates file was created and needs configuration
    else
        echo "✅ $description already exists"
        return 0  # File already exists
    fi
}

echo ""
echo "🔍 Checking configuration files..."

# Setup API configuration
setup_config "BB.Api/appsettings.template.json" "BB.Api/appsettings.json" "API configuration"
api_needs_config=$?

# Setup Web configuration  
setup_config "BB.Web/appsettings.template.json" "BB.Web/appsettings.json" "Web configuration"
web_needs_config=$?

echo ""
echo "📝 Configuration Setup Instructions:"
echo "===================================="

if [[ $api_needs_config -eq 1 ]]; then
    echo ""
    echo "🔧 BB.Api/appsettings.json requires configuration:"
    echo "   1. Update database password in DefaultConnection"
    echo "   2. Add your Bitbucket Consumer Key"
    echo "   3. Add your Bitbucket Consumer Secret"
    echo ""
    echo "   Example:"
    echo '   "DefaultConnection": "Server=localhost;Database=bb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"'
    echo '   "ConsumerKey": "your-actual-consumer-key"'
    echo '   "ConsumerSecret": "your-actual-consumer-secret"'
fi

if [[ $web_needs_config -eq 1 ]]; then
    echo ""
    echo "🌐 BB.Web/appsettings.json is ready to use (no sensitive data required)"
fi

echo ""
echo "📋 Next Steps:"
echo "1. Configure the settings as described above"
echo "2. Ensure SQL Server is running and create 'bb' database"
echo "3. Run the database schema: BB.Api/SqlSchema/schema.sql"
echo "4. Start the application with: ./start-dev.sh"
echo ""
echo "📖 For more details, see README.md"

# Make start-dev.sh executable if it exists
if [[ -f "start-dev.sh" ]]; then
    chmod +x start-dev.sh
    echo "✅ Made start-dev.sh executable"
fi

echo ""
echo "🎉 Configuration setup complete!" 