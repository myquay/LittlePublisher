# LittlePublisher

Self-hostable Micropub API for GitHub websites.

## Architecture

- **Backend**: ASP.NET Core 10 Web API with JWT authentication
- **Frontend**: Vue.js 3 SPA with Composition API, Pinia, Vue Router
- **Authentication**: IndieAuth (via [AspNet.Security.IndieAuth](https://github.com/myquay/IndieAuth))
- **Authoring storage**: Azure Table Storage is authoritative for post metadata and immutable Markdown revisions
- **Publication**: GitHub contains only the latest explicitly published revision of each post

## Post storage and publication

Creating or editing a post saves a new revision to the configured Azure Storage account. It does not modify the website repository. The editor's **Publish** action projects the current Azure revision into Hugo Markdown and pushes that file to GitHub. Editing an already-published post therefore leaves the public GitHub revision unchanged until **Republish** is selected.

Post bodies are split into bounded Table Storage entities to avoid Azure's individual string-property limit. The mutable post head uses Azure ETags, and editor updates must supply `If-Match` so concurrent saves cannot silently overwrite each other.

Micropub creation follows the same store-first path. The `post-status=draft` extension creates an Azure-only draft; an omitted status or `post-status=published` publishes it. JSON Micropub updates can replace authoring properties and publish a stored draft with `post-status=published`.

### Initial repository import

Use **Preview sync** on the dashboard before **Import from repository**. The import is idempotent by repository path and public URL and reports:

- published files imported with their existing URL, path, date, and commit SHA;
- files explicitly marked `draft: true`, which become Azure-only drafts;
- ambiguous files that have neither a published date nor `draft: true`;
- future-dated files requiring an explicit scheduling decision.

After a successful import with no unresolved failures, published files remain in GitHub as the public baseline. Any imported draft files are listed by the report and should be removed from GitHub in a separately reviewed cleanup commit. Repository import is a bootstrap/recovery operation; it is not a continuing two-way synchronization mechanism.

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
    "AllowedEditors": [
      "https://editor.example.com"
    ],
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

`AllowedEditors` contains the exact browser origins of hosted Micropub editors that
may call the API. Each entry must use HTTP or HTTPS and must not include a path,
query string, fragment, or trailing slash. Environment variables can populate the
array with indexed keys such as
`App__AllowedEditors__0=https://editor.example.com`.

### Webmention configuration

Webmention support is disabled until the runtime supplies `App__Webmention__Enabled=true`. It uses the same Azure Storage connection string as publishing, and requires both Table and Queue service access.

For the michael-mckenna.com deployment, configure:

```text
App__Webmention__Enabled=true
App__Webmention__PublicEndpoint=https://lilpub.michael-mckenna.com/webmention
App__Webmention__OwnedOrigins__0=https://michael-mckenna.com
App__Webmention__DeploymentWebhookSecret=<random shared secret of at least 32 characters>
App__Webmention__QueuePrefix=littlepublisher
```

Store the same shared secret in the site repository's production environment as `LITTLEPUBLISHER_DEPLOYMENT_SECRET`, and set `LITTLEPUBLISHER_DEPLOYMENT_ENDPOINT` to `https://lilpub.michael-mckenna.com/api/integrations/site-deployments`. Automatic outgoing sending and incoming publication remain disabled; both actions require approval in the authenticated Webmentions screen.

## License

MIT
