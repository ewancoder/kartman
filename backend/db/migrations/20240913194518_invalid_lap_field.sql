-- migrate:up
alter table "kart_data"
add "invalid_lap" boolean null;

-- migrate:down
alter table "kart_data"
drop column "invalid_lap";
