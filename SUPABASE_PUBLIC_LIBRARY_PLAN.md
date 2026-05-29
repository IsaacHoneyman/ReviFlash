# Public Deck Library Plan

This document sketches a free, public-first backend for shared flashcards using Supabase as the storage and metadata layer.

## Goal

Keep the app simple for users:

- Browse public decks
- Download a deck directly into the app
- Publish, update, and delete a deck without exposing storage complexity

## Recommended Shape

- Supabase Postgres stores deck metadata and indexing fields
- Supabase Storage stores each deck as one JSON package
- The Avalonia app talks to Supabase over HTTPS
- Local SQLite remains the user's offline study database

## v1 Data Model

### `decks`

One row per public deck.

Suggested columns:

- `id` uuid primary key
- `title` text not null
- `description` text null
- `author_name` text null
- `author_user_id` text null
- `visibility` text not null default 'public'
- `storage_path` text not null
- `version` integer not null default 1
- `card_count` integer not null default 0
- `tags` text[] null
- `created_at` timestamptz not null default now()
- `updated_at` timestamptz not null default now()

### `deck_versions` optional

Use this only if you want revision history later.

Suggested columns:

- `id` uuid primary key
- `deck_id` uuid not null references `decks(id)`
- `version` integer not null
- `storage_path` text not null
- `created_at` timestamptz not null default now()

## Storage Layout

Keep one deck package per file.

Example path pattern:

- `decks/{deck-id}/v1.json`
- `decks/{deck-id}/v2.json`

Deck package format should contain:

- deck metadata
- card list
- card type fields
- optional export version

JSON is the simplest first choice. Zip only if media becomes important later.

## App Flow

### Browse

1. App queries `decks` for public rows.
2. UI shows title, description, card count, tags, and author.
3. User selects a deck.

### Download

1. App reads `storage_path`.
2. App fetches the deck JSON from Supabase Storage.
3. App imports cards into local SQLite.

### Publish

1. App serializes the deck into JSON.
2. App uploads the file to Storage.
3. App inserts or updates the `decks` row.

### Update

1. App uploads a new versioned JSON file.
2. App updates `version`, `storage_path`, `card_count`, and `updated_at`.

### Delete

1. App removes or hides the `decks` row.
2. App deletes the Storage object if appropriate.

## Authentication Choice

For a public library, there are two realistic paths:

### Simple start

- Anonymous read access
- Authenticated write access only
- The user signs in only when publishing

### Cleaner long-term path

- Supabase Auth for creators
- Row-level security on `decks`
- Public read, owner-only write

## Scaling Guidance

This approach is a better fit than GitHub-only once the app needs:

- search and filtering
- update/delete without commit workflows
- more than a small group of active publishers
- a stable public catalog

For about 100 users, this is fine if most activity is read-only and the app caches deck lists.

## First Build Steps

1. Create a Supabase project.
2. Create the `decks` table.
3. Create a public Storage bucket for deck JSON.
4. Add a simple JSON export/import format in the app.
5. Add a browse page that reads from Supabase.
6. Add download/import.
7. Add publish/update/delete only after the read path works.

## Suggested v1 Constraints

- No media attachments
- No nested dependencies between decks
- One deck package per file
- Public reading only until the rest works
- Keep upload size small enough to avoid slow downloads

## Next Decision

Decide whether writes are:

- owner-only through Supabase Auth
- or routed through a service account you control

That choice determines how much user login UX you need.