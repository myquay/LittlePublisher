# Combined writing desk and editor concept

Start with `writing-desk.html`, then select any post or New post. Both pages share the writing desk header, existing book logo, Activity dropdown and Profile dropdown. Click the logo to return home. Editor controls sit below the header. Serve the repository with a static server for consistent browser storage between pages. This is a standalone concept, not a replacement for the Vue app.

## Feature placement

| Existing capability | Proposed location |
| --- | --- |
| Title and Markdown source | Borderless writing canvas; source remains editable and intact |
| Content type | Compact selector above the writing; 13 types from ContentTypeCatalog |
| Media, linked URLs, event fields | Relevant fields on the canvas; photo alternative text required to publish |
| Summary, categories, slug | Post details drawer |
| Save draft | Persistent bottom bar; Cmd/Ctrl+S |
| Publish / republish | Bottom bar with review dialog |
| Draft, published, unpublished changes | Quiet status line above the title |
| Publish errors and retry | Publish dialog; demo outcome selector exercises failure |
| Published URL | View published action after publication (local snapshot in this demo) |
| Return to post list | Click the LittlePublisher logo; unsaved edits are saved locally before returning |

Additional proposed conveniences: focus mode, word count, basic Markdown preview, and an insertion menu. No scheduling UI is proposed: `requestedPublishedUtc` already exists in the data model but isn't exposed by the current editor; retain its value when wiring the real form.

## Prototype boundaries

Save stores each demo post separately in browser localStorage, under a `littlepublisher-combined-` key. The desk reads those records to show saved titles, excerpts and status. New post creates an empty draft. It does not save to Azure. Publishing, published snapshots, and failures are simulations; no server calls are made. Preview supports only basic Markdown and displays image references as placeholders. Production should use a full, sanitized renderer consistent with the publishing pipeline.

For integration, retain the current adminService create/update/publish operations and eTag concurrency behavior. Stop publication when saving fails. Preserve server errors, lastPublishError and publishedUrl; keep saved-draft status distinct from whether the saved revision is published. The real published action should open the server-returned URL. Do not treat this demo's local storage or simulation as backend implementation.

Combined header and editor visually checked in the browser; verified activity/profile switching and logo navigation home. JavaScript syntax checked with Node.

## Content type concept

The selector covers Article, Note, Photo, Activity, Thought, Reply, Like, Repost, Bookmark, Blogroll entry, Event, Audio and Video. Type-specific property arrays are retained when switching types and saving locally. The desk displays type labels and a type filter, with Photo and Bookmark samples. The current production Vue editor only exposes Article, Note and Photo; the extra property editors here are a proposed UI for the broader backend catalog.

Required publishing properties follow `ContentTypeCatalog`: photo and alt; reply target; like/repost/bookmark target; blogroll URL; event start; audio/video URL. Incomplete properties can be saved as drafts; title or content is needed, matching the admin controller. No article-only title/body requirement is imposed.

Media file selection previews a local file and assigns a simulated `/media/concept/` path. It does not upload. Supported MIME types and 20 MB limit match MediaPublicationService; this prototype does not perform server-side file signature validation. Object URLs are temporary: reselect the file after reopening. HTTPS/site-relative media URLs can also be previewed. Browser storage failure is reported. Publication remains simulated.

Verified photo required-alt validation and event field switching in the browser. Script syntax checked with Node.

Photo selection now uses a keyboard-accessible dropzone. Click the empty area or selected image to open the picker, or drop one image to replace it. Existing photo URLs preview automatically; the URL input is under “Use an image URL”. Selected images are decoded before replacement, so unsupported, oversized or unreadable files leave the current photo intact. Picker selection and clicking the image to replace it were checked in-browser. Drag-and-drop handlers are implemented but were not exercised through browser automation. Local photo previews remain temporary and no upload is performed.
