# Writing desk concept

Open `writing-desk.html` in a browser. It uses plain HTML, CSS and JavaScript, plus the existing book mark in `client-app/src/assets/brand/`.

The header retains the brand and Webmentions. Activity and profile dropdowns sit at the right; the writing desk occupies a single central column. Activity contains publishing events. Profile contains identity, site settings, health checks, repository preview/import and sign out.

Dropdowns work with keyboard activation, close on Escape or outside click, and allow only one open at a time. Filters and search work. Each post opens its own editor draft; New post opens an empty draft. Save and the editor logo preserve edits locally, and saved posts appear on the desk. Both pages share `concept-chrome.css`, `concept-chrome.js`, and the sample data in `concept-posts.js`.

Account and repository actions explain their intended behavior in a dialog. Excerpts are illustrative. No real publishing, configuration, imports, health checks or account changes are performed.

JavaScript syntax checked with Node; shared header, editor layout, dropdown switching and logo navigation checked in the browser. This mockup does not modify the production application.

Content types: all 13 catalog types have labels and filtering. Photo and Bookmark samples demonstrate type-specific editors. See README.md for media simulation and validation boundaries.
