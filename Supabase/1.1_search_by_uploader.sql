-- ReviFlash 1.1: community search matches the uploader's name as well as the deck title.
-- Run once in the Supabase SQL editor, after 1.1_profiles_and_visibility.sql and BEFORE
-- shipping the 1.1 client, which searches through rpc/search_public_decks. Safe to run
-- more than once.

-- PostgREST can't OR a column with an embedded resource's column, so the match lives here.
-- Returning setof decks keeps PostgREST's select and embedding (owner:profiles) working.
-- PostgREST's order= fails on this function ("column decks.download_count does not
-- exist"), so the ranking and the result cap are done here. Security invoker: the
-- caller's RLS still applies.
drop function if exists public.search_public_decks(text);

create or replace function public.search_public_decks(p_query text default '', p_limit integer default 25)
returns setof public.decks
language sql
stable
security invoker
set search_path = ''
as $$
    select d.*
    from public.decks d
    left join public.profiles p on p.id = d.owner_id
    where d.visibility = 'public'
      and (coalesce(p_query, '') = ''
           -- strpos, not ilike, so % and _ in the search text are literal.
           or strpos(lower(d.title), lower(p_query)) > 0
           or strpos(lower(coalesce(p.display_name, '')), lower(p_query)) > 0)
    -- Popular decks first, so the cap keeps the ones people actually use.
    order by d.download_count desc, d.updated_at desc
    limit least(greatest(coalesce(p_limit, 25), 1), 100);
$$;

grant execute on function public.search_public_decks(text, integer) to anon, authenticated;
