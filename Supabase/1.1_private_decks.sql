-- ReviFlash 1.1: private decks. The visibility column and the read policy (public decks,
-- plus your own) already exist; this only stops anything but the two known values getting
-- in. Safe to run more than once. Optional for the client, but run it before release.

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'decks_visibility_check') then
        alter table public.decks
            add constraint decks_visibility_check check (visibility in ('public', 'private'));
    end if;
end;
$$;
