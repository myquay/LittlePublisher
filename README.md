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

By default, import considers every Markdown file beneath `App:GitHub:ContentPath`.
Repositories that keep non-post pages in the same content tree can restrict the
initial import with repository-relative paths:

```text
App__GitHub__ImportPaths__0=content/articles
App__GitHub__ImportPaths__1=content/notes
```

Each import path must be inside `App:GitHub:ContentPath`; section names and
repository layout remain deployment-specific rather than being built into
LittlePublisher.

## Book reviews

Choose **Book review** in the editor. Search Open Library by title/author or ISBN,
select **Use this book**, and edit any returned fields. Manual entry works without
search. Title searches describe a work and leave ISBN blank; ISBN searches retrieve
that edition's metadata. Your own rating and review text are never filled from a
provider. Covers can use an HTTPS URL or the existing photo upload control.

Publication requires a book title, author, cover, review text, and a whole-star
rating from 1 to 5. Date finished, ISBN, book URL, and cover alternative text are
optional. Incomplete drafts can be saved. New reviews publish to
`<ContentPath>/books/<slug>/index.md` and `/books/<slug>/`, using the blog's nested
`book` front matter and numeric `rating`. Imported reviews retain their existing
path, public URL, and relative cover reference on republish. Include the books
section in `App:GitHub:ImportPaths` if import filters are configured.

Open Library lookup needs no API key. The authenticated `/api/books/search?q=...`
endpoint caches up to 200 search/document entries for six hours and limits upstream requests to
one per second per application instance. Search failure leaves manual entry
available. Open Library covers use hosted URLs in accordance with its
[cover guidelines](https://openlibrary.org/dev/docs/api/covers); publication does
not depend on metadata lookup, although hosted image display depends on the cover
service. Uploads use the existing media publication path.

Micropub clients can explicitly send `post-type=book-review` with the
LittlePublisher extension properties `book-title`, `book-author`, `book-cover`,
`book-cover-alt`, `book-isbn`, `book-url`, `rating`, and `date-read`. These are scalar
string-array properties; `date-read` uses `YYYY-MM-DD`. Create, replace, delete,
and source queries use these fields. Nested `h-review` input is not supported.

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

### Conversations

Use **+ → conversation** in the editor to insert the blog's `{{< conversation >}}` / `{{< message role="human" >}}` format. Preview shows ordered turns with speaker labels and optional `name`, `model`, conversation `title`, and `note` metadata. Roles are `human`, `ai`, and `system`; each shortcode belongs on its own line and needs a closing tag. Message bodies use Markdown. The reading preview supports the editor's basic Markdown subset; Hugo renders the full Markdown on the blog.

Unfinished conversations can be saved as drafts. The editor checks conversation structure before opening the publishing review, and preview explains any errors with line numbers. Publishing preserves the shortcode source for Hugo.

### Private photo staging

Photo uploads from the editor (inline article images, photo posts, and book covers) and the Micropub media endpoint are stored in a private Azure Blob container. They are not committed to the website until a post referencing them is published. The article and all referenced staged images are pushed in one Git commit. External image URLs and previously published images continue to work as before; audio/video uploads retain their existing behavior.

The account in `App:Storage:ConnectionString` now needs Blob service access as well as Table access. `App:Storage:MediaContainer` defaults to `littlepublisher-staged-media`; LittlePublisher creates it with private access and rejects uploads if an existing container allows public access. Set `App:Host` to the stable absolute URL of this LittlePublisher instance. Upload responses contain stable URLs under that host's `/api/media/staged/` endpoint. Reading those URLs requires an authenticated publisher token (or an owner Micropub token); they are never public static files.

The editor retrieves previews with authentication and temporary browser object URLs. Saved drafts retain their staged references; publication substitutes public website URLs only in the generated content. Staged originals are retained for draft history, retries, and republishing. There is no automatic expiry: do not apply a blanket container lifecycle deletion rule, as older draft revisions may still reference those files.
