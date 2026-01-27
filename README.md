# LittlePublisher

Self-hostable Micropub API for GitHub websites.

## Architecture

- **Backend**: ASP.NET Core 10 Web API with JWT authentication
- **Frontend**: Vue.js 3 SPA with Composition API, Pinia, Vue Router
- **Authentication**: IndieAuth (via [AspNet.Security.IndieAuth](https://github.com/myquay/IndieAuth))

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- Git (for submodule support)

## Getting Started

### 1. Clone with submodules

```bash
git clone --recurse-submodules https://github.com/myquay/LittlePublisher.git
cd LittlePublisher
```

If you already cloned without submodules:

```bash
git submodule update --init --recursive
```

### 2. Configure HTTPS development certificate

```bash
dotnet dev-certs https --trust
```

This creates and trusts a self-signed certificate for local HTTPS development.

### 3. Install dependencies

```bash
cd client-app
npm install
cd ..
```

### 4. Run in development

Option A - Use the start script:

```bash
./start-dev.sh
```

Option B - Run separately:

```bash
# Terminal 1: .NET API
cd LittlePublisher.Web
dotnet run

# Terminal 2: Vue dev server
cd client-app
npm run dev
```

Then open http://localhost:5173

### 5. Build for production

```bash
# Build Vue app (outputs to LittlePublisher.Web/wwwroot)
cd client-app
npm run build

# Build and run .NET
cd ../LittlePublisher.Web
dotnet run -c Release
```

## Project Structure

```
LittlePublisher/
├── .plans/                      # Migration and feature plans
├── lib/
│   └── IndieAuth/               # Git submodule for IndieAuth
├── client-app/                  # Vue.js 3 SPA
│   ├── src/
│   │   ├── composables/         # Vue composables
│   │   ├── router/              # Vue Router config
│   │   ├── services/            # API services
│   │   ├── stores/              # Pinia stores
│   │   ├── types/               # TypeScript types
│   │   └── views/               # Vue views/pages
│   └── vite.config.ts
├── LittlePublisher.Web/         # .NET 10 API
│   ├── Controllers/             # API controllers
│   ├── Configuration/           # App configuration
│   ├── Services/                # Business services
│   └── wwwroot/                 # Static files (Vue build output)
├── archive/                     # Archived Razor Pages (reference)
├── start-dev.sh                 # Development start script
└── LittlePublisher.sln
```

## Configuration

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | No | `Development` or `Production` |

### appsettings.json

```json
{
  "App": {
    "Host": "https://your-domain.com",
    "IndieAuth": {
      "ClientId": "https://your-domain.com"
    },
    "Jwt": {
      "Issuer": "https://your-domain.com",
      "Audience": "https://your-domain.com",
      "SecretKey": "your-secret-key-at-least-32-characters-long",
      "ExpiryMinutes": 60
    }
  }
}
```

## License

MIT
