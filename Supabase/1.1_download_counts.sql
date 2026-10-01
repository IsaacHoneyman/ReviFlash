-- ReviFlash 1.1: per-user download counts on public decks.
-- Run once in the Supabase SQL editor BEFORE shipping the 1.1 client: the client
-- selects and orders by decks.download_count, so older databases would reject it.

-- 1. The count itself, shown in the app and used to rank community decks.
alter table public.decks
    add column if not exists download_count integer not null default 0;

create index if not exists decks_public_popular_idx
    on public.decks (download_count desc, updated_at desc)
    where visibility = 'public';

-- 2. Who has downloaded what. The primary key is what makes it one count per user.
create table if not exists public.deck_downloads (
    deck_id       uuid        not null references public.decks (id) on delete cascade,
    user_id       uuid        not null references auth.users (id) on delete cascade,
    downloaded_at timestamptz not null default now(),
    primary key (deck_id, user_id)
);

-- RLS on with no policies: the table is reachable only through the function below.
alter table public.deck_downloads enable row level security;

-- 3. Nobody can set download_count directly (insert or owner update through the REST
--    API). Only record_deck_download flips the guard flag for its own transaction, and
--    its increment leaves updated_at alone.
create or replace function public.protect_download_count()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
    if coalesce(current_setting('reviflash.counting_download', true), '') = 'on' then
        -- A download is not an edit, so undo decks_set_updated_at's bump.
        new.updated_at := old.updated_at;
    elsif tg_op = 'INSERT' then
        new.download_count := 0;
    else
        new.download_count := old.download_count;
    end if;
    return new;
end;
$$;

-- Same-timing triggers fire in name order; "zz" keeps this after decks_set_updated_at.
drop trigger if exists decks_protect_download_count on public.decks;
drop trigger if exists decks_zz_protect_download_count on public.decks;
create trigger decks_zz_protect_download_count
    before insert or update on public.decks
    for each row execute function public.protect_download_count();

-- 4. Called by the client after a successful download. Counts each signed-in user once
--    per deck, never counts the owner, and returns the deck's current total.
create or replace function public.record_deck_download(p_deck_id uuid)
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user  uuid := auth.uid();
    v_owner uuid;
    v_count integer;
begin
    if v_user is null then
        raise exception 'Sign in required' using errcode = '42501';
    end if;

    select owner_id, download_count into v_owner, v_count
    from decks
    where id = p_deck_id and visibility = 'public';

    if not found then
        return null;
    end if;

    if v_owner is distinct from v_user then
        insert into deck_downloads (deck_id, user_id)
        values (p_deck_id, v_user)
        on conflict do nothing;

        if found then
            perform set_config('reviflash.counting_download', 'on', true);
            update decks
            set download_count = download_count + 1
            where id = p_deck_id
            returning download_count into v_count;
            perform set_config('reviflash.counting_download', 'off', true);
        end if;
    end if;

    return v_count;
end;
$$;

revoke all on function public.record_deck_download(uuid) from public, anon;
grant execute on function public.record_deck_download(uuid) to authenticated;
