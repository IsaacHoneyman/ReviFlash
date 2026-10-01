-- ReviFlash 1.1: uploader names on community decks, visibility-aware reads, policy tidy-up.
-- Run once in the Supabase SQL editor, after 1.1_download_counts.sql and BEFORE shipping
-- the 1.1 client: the client embeds owner:profiles(display_name) in its deck queries,
-- which needs the foreign key below. Safe to run more than once.

-- 1. Exact duplicate of "Owner update own decks".
drop policy if exists "Users can update their own deck metadata" on public.decks;

-- 2. Decks were readable whatever their visibility. Now: public decks, plus your own.
--    Every existing deck is 'public', so nothing disappears. Note the "decks" storage
--    bucket is public, so this hides a private deck from listings, not its file.
drop policy if exists "Public read decks" on public.decks;
drop policy if exists "Read public or own decks" on public.decks;
create policy "Read public or own decks" on public.decks
    for select to public
    using (visibility = 'public' or owner_id = (select auth.uid()));

-- 3. Profiles were readable only by their owner, so nobody could see who uploaded a
--    deck. They hold just id and display_name, which are meant to be public.
drop policy if exists "Public read profiles" on public.profiles;
create policy "Public read profiles" on public.profiles
    for select to public
    using (true);

-- 4. Link decks to their uploader's profile so PostgREST can embed the display name.
--    Every user already has a profile (handle_new_user on auth.users). Deleting a user
--    cascades through profiles to their deck rows; storage files must be removed first.
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'decks_owner_id_fkey') then
        alter table public.decks
            add constraint decks_owner_id_fkey
            foreign key (owner_id) references public.profiles (id) on delete cascade;
    end if;
end;
$$;

create index if not exists decks_owner_id_idx on public.decks (owner_id);
