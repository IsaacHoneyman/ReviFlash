# Supabase setup

Changes that have to be made on the Supabase project (hegjwggsueldwtnxpnnv) itself, in the order they shipped.

## 1.1

1. **Download counts:** run [`1.1_download_counts.sql`](1.1_download_counts.sql) in the SQL editor. It is safe to run more than once. Do this before releasing the 1.1 client, because the client reads and sorts by `decks.download_count`.

2. **Uploader names and visibility:** run [`1.1_profiles_and_visibility.sql`](1.1_profiles_and_visibility.sql) after step 1. It makes profiles publicly readable, links `decks.owner_id` to `profiles` so the client can show who uploaded a deck, limits deck reads to public decks plus your own, and drops a duplicate update policy. Also required before releasing the 1.1 client.

3. **Search by uploader:** run [`1.1_search_by_uploader.sql`](1.1_search_by_uploader.sql) after step 2. It adds `search_public_decks`, which community search calls so a query matches the deck title or the uploader's name. Also required before releasing the 1.1 client.

4. **Private decks:** run [`1.1_private_decks.sql`](1.1_private_decks.sql). It only adds a check that `visibility` is `public` or `private`; the read policy from step 2 is what hides private decks. Note the `decks` storage bucket is still public: private deck files get random names and are renamed when a deck goes private, so they can't be found, but anyone given the exact path could still download one.

5. **Tidy-up:** run [`1.1_tidy_up.sql`](1.1_tidy_up.sql) last. It clears the Supabase advisor warnings: `handle_new_user` is no longer callable through the API (Auth keeps its grant), the deck and profile policies evaluate `auth.uid()` once per query, `deck_downloads.user_id` gets an index, and usernames must be 3-24 characters, as the client already requires at sign-up.

6. **Password reset by code:** the desktop app has no web page for a reset link to open, so the reset email has to contain a one-time code instead. Go to Authentication → Emails → *Reset Password* and make the body include the token, for example:

   ```html
   <h2>Reset your ReviFlash password</h2>
   <p>Enter this code in ReviFlash to choose a new password:</p>
   <p style="font-size:24px;font-weight:bold;letter-spacing:4px">{{ .Token }}</p>
   <p>If you didn't ask for this, you can ignore this email.</p>
   ```

   The app sends the code to `/auth/v1/verify` (type `recovery`) and then sets the new password itself.
