-- ReviFlash 1.1: fixes for the Supabase security and performance advisors. Nothing here
-- changes what the client can do. Run after the other 1.1 files. Safe to run more than once.

-- 1. handle_new_user is the auth.users trigger; it was also callable as rpc/handle_new_user.
--    Auth fires it as supabase_auth_admin, which keeps an explicit grant.
revoke execute on function public.handle_new_user() from public, anon, authenticated;
grant execute on function public.handle_new_user() to supabase_auth_admin;

-- 2. (select auth.uid()) is evaluated once per query instead of once per row.
alter policy "Authenticated insert own decks" on public.decks
    with check ((select auth.uid()) = owner_id);
alter policy "Owner update own decks" on public.decks
    using ((select auth.uid()) = owner_id)
    with check ((select auth.uid()) = owner_id);
alter policy "Owner delete own decks" on public.decks
    using ((select auth.uid()) = owner_id);
alter policy "Profiles are updatable by the profile owner" on public.profiles
    using ((select auth.uid()) = id)
    with check ((select auth.uid()) = id);

-- 3. Covers deck_downloads' user foreign key (the primary key starts with deck_id).
create index if not exists deck_downloads_user_id_idx on public.deck_downloads (user_id);

-- 4. Usernames show on community decks, and anyone can PATCH their own profile directly,
--    so enforce the sign-up rule (3-24 characters, as LoginViewModel checks) here too.
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'profiles_display_name_length') then
        alter table public.profiles
            add constraint profiles_display_name_length
            check (char_length(btrim(display_name)) between 3 and 24);
    end if;
end;
$$;
